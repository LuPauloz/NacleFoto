using System;
using System.Collections.Generic;
using System.IO;
using NacleFoto.Core.Imaging;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// Builds a minimal JPEG with an EXIF block to test the parser directly
    /// (without depending on a real camera file).
    /// </summary>
    internal static class ExifBuilder
    {
        public class Entry
        {
            public int Tag;
            public int Type;      // 2=ASCII 3=SHORT 4=LONG 5=RATIONAL
            public object Value;  // string, short, int or long[] (numerator/denominator)
        }

        public static byte[] Build(IEnumerable<Entry> ifd0, IEnumerable<Entry> exifIfd, bool littleEndian = true)
        {
            var e0 = new List<Entry>(ifd0);
            var e1 = new List<Entry>(exifIfd);

            var tiff = new MemoryStream();
            var le = littleEndian;
            tiff.Write(le ? new byte[] { 0x49, 0x49 } : new byte[] { 0x4D, 0x4D }, 0, 2);
            WriteU16(tiff, 42, le);
            WriteU32(tiff, 8, le); // IFD0 offset

            const int ifd0Offset = 8;
            int ifd0Size = 2 + e0.Count * 12 + 4;
            int ifd0DataStart = ifd0Offset + ifd0Size;

            // ---- layout (2nd pass): computes offsets without overlapping ----
            var offsets = new Dictionary<Entry, int>();
            int cursor = ifd0DataStart;
            foreach (var e in e0)
                if (e.Type == 2 || e.Type == 5) { offsets[e] = cursor; cursor += SizeOf(e); }

            int exifIfdOffset = cursor;
            int exifIfdSize = 2 + e1.Count * 12 + 4;
            int exifDataStart = exifIfdOffset + exifIfdSize;
            cursor = exifDataStart;
            foreach (var e in e1)
                if (e.Type == 2 || e.Type == 5) { offsets[e] = cursor; cursor += SizeOf(e); }

            // ---- IFD0 ----
            tiff.Position = ifd0Offset;
            WriteU16(tiff, (ushort)e0.Count, le);
            foreach (var e in e0)
            {
                WriteU16(tiff, (ushort)e.Tag, le);
                WriteU16(tiff, (ushort)e.Type, le);
                WriteU32(tiff, (uint)CountOf(e), le);
                if (e.Tag == 0x8769) WriteU32(tiff, (uint)exifIfdOffset, le);
                else if (e.Type == 2 || e.Type == 5) WriteU32(tiff, (uint)offsets[e], le);
                else if (e.Type == 3) { WriteShort(tiff, (short)e.Value, le); }
                else if (e.Type == 4) { WriteU32(tiff, (uint)e.Value, le); }
            }
            WriteU32(tiff, 0, le);

            // IFD0 data
            tiff.Position = ifd0DataStart;
            foreach (var e in e0)
            {
                if (e.Type == 2) { var bytes = System.Text.Encoding.ASCII.GetBytes((string)e.Value + "\0"); tiff.Write(bytes, 0, bytes.Length); }
                else if (e.Type == 5) { var v = (long[])e.Value; WriteU32(tiff, (uint)v[0], le); WriteU32(tiff, (uint)v[1], le); }
            }

            // ---- Exif IFD ----
            tiff.Position = exifIfdOffset;
            WriteU16(tiff, (ushort)e1.Count, le);
            foreach (var e in e1)
            {
                WriteU16(tiff, (ushort)e.Tag, le);
                WriteU16(tiff, (ushort)e.Type, le);
                WriteU32(tiff, (uint)CountOf(e), le);
                if (e.Type == 2 || e.Type == 5) WriteU32(tiff, (uint)offsets[e], le);
                else if (e.Type == 3) { WriteShort(tiff, (short)e.Value, le); }
                else if (e.Type == 4) { WriteU32(tiff, (uint)e.Value, le); }
            }
            WriteU32(tiff, 0, le);

            // Exif IFD data
            tiff.Position = exifDataStart;
            foreach (var e in e1)
            {
                if (e.Type == 2) { var bytes = System.Text.Encoding.ASCII.GetBytes((string)e.Value + "\0"); tiff.Write(bytes, 0, bytes.Length); }
                else if (e.Type == 5) { var v = (long[])e.Value; WriteU32(tiff, (uint)v[0], le); WriteU32(tiff, (uint)v[1], le); }
            }

            var tiffBytes = tiff.ToArray();

            // ---- builds JPEG: SOI + APP1 + (minimal body) ----
            var jpeg = new MemoryStream();
            jpeg.WriteByte(0xFF); jpeg.WriteByte(0xD8);                 // SOI
            jpeg.WriteByte(0xFF); jpeg.WriteByte(0xE1);                 // APP1
            int app1Length = 2 + 6 + tiffBytes.Length;
            jpeg.WriteByte((byte)(app1Length >> 8)); jpeg.WriteByte((byte)(app1Length & 0xFF));
            jpeg.Write(System.Text.Encoding.ASCII.GetBytes("Exif"), 0, 4);
            jpeg.WriteByte(0); jpeg.WriteByte(0);
            jpeg.Write(tiffBytes, 0, tiffBytes.Length);
            jpeg.WriteByte(0xFF); jpeg.WriteByte(0xD9);                 // EOI
            return jpeg.ToArray();
        }

        private static int SizeOf(Entry e)
        {
            if (e.Type == 2) return ((string)e.Value).Length + 1;
            if (e.Type == 5) return 8;
            return 4;
        }

        private static int CountOf(Entry e)
        {
            if (e.Type == 2) return ((string)e.Value).Length + 1;
            return 1;
        }

        private static void WriteU16(Stream s, ushort v, bool le)
        {
            if (le) { s.WriteByte((byte)(v & 0xFF)); s.WriteByte((byte)(v >> 8)); }
            else { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)(v & 0xFF)); }
        }

        private static void WriteShort(Stream s, short v, bool le)
        {
            if (le) { s.WriteByte((byte)(v & 0xFF)); s.WriteByte((byte)((v >> 8) & 0xFF)); }
            else { s.WriteByte((byte)((v >> 8) & 0xFF)); s.WriteByte((byte)(v & 0xFF)); }
            s.WriteByte(0); s.WriteByte(0);
        }

        private static void WriteU32(Stream s, uint v, bool le)
        {
            if (le)
            {
                s.WriteByte((byte)(v & 0xFF)); s.WriteByte((byte)((v >> 8) & 0xFF));
                s.WriteByte((byte)((v >> 16) & 0xFF)); s.WriteByte((byte)((v >> 24) & 0xFF));
            }
            else
            {
                s.WriteByte((byte)((v >> 24) & 0xFF)); s.WriteByte((byte)((v >> 16) & 0xFF));
                s.WriteByte((byte)((v >> 8) & 0xFF)); s.WriteByte((byte)(v & 0xFF));
            }
        }
    }

    public class ExifParserTests
    {
        [Fact]
        public void Reads_make_model_and_technical_data()
        {
            var jpeg = ExifBuilder.Build(
                new[]
                {
                    new ExifBuilder.Entry { Tag = 0x010F, Type = 2, Value = "NIKON" },
                    new ExifBuilder.Entry { Tag = 0x0110, Type = 2, Value = "D750" },
                    new ExifBuilder.Entry { Tag = 0x8769, Type = 4, Value = 0 }
                },
                new[]
                {
                    new ExifBuilder.Entry { Tag = 0x829A, Type = 5, Value = new long[] { 1, 250 } },
                    new ExifBuilder.Entry { Tag = 0x829D, Type = 5, Value = new long[] { 18, 10 } },
                    new ExifBuilder.Entry { Tag = 0x8827, Type = 3, Value = (short)400 }
                });

            var exif = ExifParser.ParseBuffer(jpeg);

            Assert.NotNull(exif);
            Assert.Equal("NIKON", exif.CameraMake);
            Assert.Equal("D750", exif.CameraModel);
            Assert.Equal(1.0 / 250.0, exif.ExposureSeconds.Value, 6);
            Assert.Equal(1.8, exif.FNumber.Value, 6);
            Assert.Equal(400, exif.Iso.Value);
        }

        [Fact]
        public void Reads_exif_with_big_endian_byte_order()
        {
            var jpeg = ExifBuilder.Build(
                new[]
                {
                    new ExifBuilder.Entry { Tag = 0x010F, Type = 2, Value = "CANON" },
                    new ExifBuilder.Entry { Tag = 0x8769, Type = 4, Value = 0 }
                },
                new[] { new ExifBuilder.Entry { Tag = 0x829A, Type = 5, Value = new long[] { 1, 60 } } },
                littleEndian: false);

            var exif = ExifParser.ParseBuffer(jpeg);

            Assert.NotNull(exif);
            Assert.Equal("CANON", exif.CameraMake);
            Assert.Equal(1.0 / 60.0, exif.ExposureSeconds.Value, 6);
        }

        [Fact]
        public void Exif_date_time_is_converted()
        {
            var dt = ExifParser.ParseExifDate("2026:10:03 14:35:22");
            Assert.NotNull(dt);
            Assert.Equal(2026, dt.Value.Year);
            Assert.Equal(10, dt.Value.Month);
            Assert.Equal(3, dt.Value.Day);
            Assert.Equal(14, dt.Value.Hour);
        }

        [Fact]
        public void File_that_is_not_jpeg_returns_null()
        {
            var exif = ExifParser.ParseBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Assert.Null(exif);
        }
    }
}
