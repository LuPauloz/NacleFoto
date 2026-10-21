using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NacleFoto.Shots
{
    /// <summary>Builds an EXIF block and injects it into a JPEG (for generating test photos only).</summary>
    internal static class ExifInjector
    {
        private class Item
        {
            public int Tag;
            public int Type;   // 2=ASCII 3=SHORT 5=RATIONAL
            public string Str;
            public short Sh;
            public long[] Rat;
        }

        public static byte[] Injetar(byte[] jpeg, string camera, string lente,
                                      double abertura, int iso, double shutter, DateTime dt)
        {
            var ifd0 = new List<Item>
            {
                new Item { Tag = 0x010F, Type = 2, Str = camera.Split(' ')[0] },
                new Item { Tag = 0x0110, Type = 2, Str = camera },
                new Item { Tag = 0x0132, Type = 2, Str = dt.ToString("yyyy:MM:dd HH:mm:ss") },
                new Item { Tag = 0x8769, Type = 4 }   // pointer to Exif IFD
            };
            var exif = new List<Item>
            {
                new Item { Tag = 0x829A, Type = 5, Rat = Ratio(shutter) },
                new Item { Tag = 0x829D, Type = 5, Rat = Ratio(abertura) },
                new Item { Tag = 0x8827, Type = 3, Sh = (short)iso },
                new Item { Tag = 0x9003, Type = 2, Str = dt.ToString("yyyy:MM:dd HH:mm:ss") },
                new Item { Tag = 0x920A, Type = 5, Rat = Ratio(50) },
                new Item { Tag = 0xA434, Type = 2, Str = lente }
            };

            var tiff = MontarTiff(ifd0, exif);
            var outBytes = new MemoryStream();
            outBytes.Write(jpeg, 0, 2); // SOI
            outBytes.WriteByte(0xFF); outBytes.WriteByte(0xE1);
            int len = 2 + 6 + tiff.Length;
            outBytes.WriteByte((byte)(len >> 8)); outBytes.WriteByte((byte)(len & 0xFF));
            outBytes.Write(Encoding.ASCII.GetBytes("Exif"), 0, 4);
            outBytes.WriteByte(0); outBytes.WriteByte(0);
            outBytes.Write(tiff, 0, tiff.Length);
            outBytes.Write(jpeg, 2, jpeg.Length - 2);
            return outBytes.ToArray();
        }

        private static long[] Ratio(double v)
        {
            if (v >= 1) return new long[] { (long)Math.Round(v), 1 };
            return new long[] { 1, (long)Math.Round(1.0 / v) };
        }

        private static byte[] MontarTiff(List<Item> ifd0, List<Item> exif)
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("II"), 0, 2);
            W16(ms, 42);
            W32(ms, 8);

            var offsets = new Dictionary<Item, int>();
            int cursor = 8 + (2 + ifd0.Count * 12 + 4);
            foreach (var i in ifd0) if (i.Type == 2 || i.Type == 5) { offsets[i] = cursor; cursor += Size(i); }
            int exifOffset = cursor;
            int exifSize = 2 + exif.Count * 12 + 4;
            int exifData = exifOffset + exifSize;
            cursor = exifData;
            foreach (var i in exif) if (i.Type == 2 || i.Type == 5) { offsets[i] = cursor; cursor += Size(i); }

            ms.Position = 8;
            W16(ms, (ushort)ifd0.Count);
            foreach (var i in ifd0) WriteEntry(ms, i, offsets, exifOffset);
            W32(ms, 0);

            ms.Position = 8 + (2 + ifd0.Count * 12 + 4);
            foreach (var i in ifd0) WriteData(ms, i);

            ms.Position = exifOffset;
            W16(ms, (ushort)exif.Count);
            foreach (var i in exif) WriteEntry(ms, i, offsets, 0);
            W32(ms, 0);

            ms.Position = exifData;
            foreach (var i in exif) WriteData(ms, i);

            return ms.ToArray();
        }

        private static void WriteEntry(MemoryStream ms, Item i, Dictionary<Item, int> offsets, int exifOffset)
        {
            W16(ms, (ushort)i.Tag);
            W16(ms, (ushort)i.Type);
            W32(ms, (uint)(i.Type == 2 ? i.Str.Length + 1 : 1));
            if (i.Tag == 0x8769) W32(ms, (uint)exifOffset);
            else if (i.Type == 2 || i.Type == 5) W32(ms, (uint)offsets[i]);
            else { ms.WriteByte((byte)(i.Sh & 0xFF)); ms.WriteByte((byte)(i.Sh >> 8)); ms.WriteByte(0); ms.WriteByte(0); }
        }

        private static void WriteData(MemoryStream ms, Item i)
        {
            if (i.Type == 2)
            {
                var b = Encoding.ASCII.GetBytes(i.Str + "\0");
                ms.Write(b, 0, b.Length);
            }
            else if (i.Type == 5)
            {
                W32(ms, (uint)i.Rat[0]);
                W32(ms, (uint)i.Rat[1]);
            }
        }

        private static int Size(Item i)
        {
            if (i.Type == 2) return i.Str.Length + 1;
            if (i.Type == 5) return 8;
            return 4;
        }

        private static void W16(Stream s, ushort v)
        {
            s.WriteByte((byte)(v & 0xFF)); s.WriteByte((byte)(v >> 8));
        }

        private static void W32(Stream s, uint v)
        {
            s.WriteByte((byte)(v & 0xFF)); s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF)); s.WriteByte((byte)((v >> 24) & 0xFF));
        }
    }
}
