using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NacleFoto.Core.Models;

namespace NacleFoto.Core.Imaging
{
    /// <summary>EXIF data extracted from a JPEG (APP1 header).</summary>
    public sealed class ExifData
    {
        public string CameraMake { get; set; }
        public string CameraModel { get; set; }
        public string LensModel { get; set; }
        public string LensMake { get; set; }
        public string Software { get; set; }
        public string Artist { get; set; }
        public string Copyright { get; set; }
        public string BodySerial { get; set; }
        public string OwnerName { get; set; }
        public string UserComment { get; set; }

        public DateTime? TakenLocal { get; set; }
        public string TakenOffset { get; set; }

        public double? ExposureSeconds { get; set; }
        public double? FNumber { get; set; }
        public double? Iso { get; set; }
        public double? FocalLength { get; set; }
        public double? FocalLength35 { get; set; }
        public double? ExposureBias { get; set; }
        public int? MeteringMode { get; set; }
        public int? ExposureProgram { get; set; }
        public int? WhiteBalance { get; set; }
        public int? Flash { get; set; }
        public int? ColorSpace { get; set; }
        public int? Orientation { get; set; }
        public int? Rating { get; set; }
        public int? PixelWidth { get; set; }
        public int? PixelHeight { get; set; }

        public double? GpsLatitude { get; set; }
        public double? GpsLongitude { get; set; }
        public double? GpsAltitude { get; set; }
        public DateTime? GpsDateTaken { get; set; }

        public bool HasMakerNote { get; set; }

        public string MeteringModeText
        {
            get
            {
                if (!MeteringMode.HasValue) return null;
                switch (MeteringMode.Value)
                {
                    case 0: return "Desconhecido";
                    case 1: return "Media";
                    case 2: return "Central ponderado";
                    case 3: return "Ponto";
                    case 4: return "Multi-ponto";
                    case 5: return "Padrao";
                    case 6: return "Parcial";
                    case 255: return "Outro";
                    default: return "Codigo " + MeteringMode.Value;
                }
            }
        }

        public string ExposureProgramText
        {
            get
            {
                if (!ExposureProgram.HasValue) return null;
                switch (ExposureProgram.Value)
                {
                    case 0: return "Nao definido";
                    case 1: return "Manual";
                    case 2: return "Programado";
                    case 3: return "Prioridade abertura";
                    case 4: return "Prioridade velocidade";
                    case 5: return "Criativo";
                    case 6: return "Acao";
                    case 7: return "Retrato";
                    case 8: return "Paisagem";
                    default: return "Codigo " + ExposureProgram.Value;
                }
            }
        }

        public string WhiteBalanceText
        {
            get
            {
                if (!WhiteBalance.HasValue) return null;
                switch (WhiteBalance.Value)
                {
                    case 0: return "Automatico";
                    case 1: return "Manual";
                    default: return "Codigo " + WhiteBalance.Value;
                }
            }
        }

        public string ColorSpaceText
        {
            get
            {
                if (!ColorSpace.HasValue) return null;
                if (ColorSpace.Value == 1) return "sRGB";
                if (ColorSpace.Value == 65535) return "Nao calibrado";
                return "Codigo " + ColorSpace.Value;
            }
        }

        public string FlashText
        {
            get
            {
                if (!Flash.HasValue) return null;
                int f = Flash.Value;
                bool fired = (f & 1) != 0;
                bool noFunction = (f & 0x20) != 0;
                if (noFunction) return "Sem flash";
                return fired ? "Disparou" : "Nao disparou";
            }
        }
    }

    /// <summary>
    /// Pure managed EXIF parser. Reads only the file header (APP1),
    /// without decoding the image. Indexing 2000 photos takes seconds.
    /// </summary>
    public static class ExifParser
    {
        private const int MaxHeaderBytes = 256 * 1024;

        public static ExifData ParseFile(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan))
                {
                    return Parse(fs);
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public static ExifData Parse(Stream stream)
        {
            var buffer = ReadHeader(stream);
            if (buffer == null) return null;
            return ParseBuffer(buffer);
        }

        private static byte[] ReadHeader(Stream stream)
        {
            if (stream == null || !stream.CanRead) return null;
            var buffer = new byte[MaxHeaderBytes];
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0) break;
                total += read;
                if (LooksComplete(buffer, total)) break;
            }
            if (total == 0) return null;
            if (total == buffer.Length) return buffer;
            var exact = new byte[total];
            Buffer.BlockCopy(buffer, 0, exact, 0, total);
            return exact;
        }

        private static bool LooksComplete(byte[] b, int len)
        {
            for (int i = 4; i < len - 10; i++)
            {
                if (b[i] == 'E' && b[i + 1] == 'x' && b[i + 2] == 'i' && b[i + 3] == 'f' && b[i + 4] == 0 && b[i + 5] == 0)
                    return i + 10 + 128 <= len;
            }
            return false;
        }

        public static ExifData ParseBuffer(byte[] b)
        {
            if (b == null || b.Length < 4) return null;
            if (b[0] != 0xFF || b[1] != 0xD8) return null; // not a JPEG

            int offset = 2;
            while (offset + 4 <= b.Length)
            {
                if (b[offset] != 0xFF) { offset++; continue; }
                int marker = b[offset + 1];
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { offset += 2; continue; }
                if (marker == 0xD9 || marker == 0xDA) break; // EOI / start of compressed data
                int length = (b[offset + 2] << 8) | b[offset + 3];
                if (length < 2) break;
                if (marker == 0xE1)
                {
                    int seg = offset + 4;
                    if (seg + 6 <= b.Length &&
                        b[seg] == 'E' && b[seg + 1] == 'x' && b[seg + 2] == 'i' && b[seg + 3] == 'f' &&
                        b[seg + 4] == 0 && b[seg + 5] == 0)
                    {
                        var reader = new TiffReader(b, seg + 6);
                        return reader.Read();
                    }
                }
                offset += 2 + length;
            }
            return null;
        }

        // ---------------- TIFF / IFD ----------------

        private sealed class Entry
        {
            public int Tag;
            public int Type;
            public int Count;
            public int Raw;      // ValueOffset field value (or external offset)
            public int ValuePos;  // position of the ValueOffset field within the buffer
        }

        private sealed class TiffReader
        {
            private readonly byte[] _b;
            private readonly int _tiff;
            private readonly bool _le;
            private readonly List<Entry> _ifd0 = new List<Entry>();

            public TiffReader(byte[] b, int tiff)
            {
                _b = b;
                _tiff = tiff;
                _le = b[tiff] == 'I' && b[tiff + 1] == 'I';
            }

            public ExifData Read()
            {
                if (_tiff + 8 > _b.Length) return null;
                if (!(_le || (_b[_tiff] == 'M' && _b[_tiff + 1] == 'M'))) return null;
                if (U16(_tiff + 2) != 42) return null;

                int ifd0 = _tiff + (int)U32(_tiff + 4);
                _ifd0.AddRange(ReadIfd(ifd0));

                var d = new ExifData();
                ApplyIfd0(d);

                var exifPtr = RawValue(0x8769);
                if (exifPtr.HasValue)
                {
                    var entries = ReadIfd(_tiff + exifPtr.Value);
                    ApplyExifIfd(d, entries);
                }

                var gpsPtr = RawValue(0x8825);
                if (gpsPtr.HasValue)
                {
                    var entries = ReadIfd(_tiff + gpsPtr.Value);
                    ApplyGpsIfd(d, entries);
                }

                return d;
            }

            private List<Entry> ReadIfd(int dirOffset)
            {
                var list = new List<Entry>();
                if (dirOffset < 0 || dirOffset + 2 > _b.Length) return list;
                int count = U16(dirOffset);
                int p = dirOffset + 2;
                for (int i = 0; i < count; i++)
                {
                    if (p + 12 > _b.Length) break;
                    list.Add(new Entry
                    {
                        Tag = U16(p),
                        Type = U16(p + 2),
                        Count = (int)U32(p + 4),
                        Raw = (int)U32(p + 8),
                        ValuePos = p + 8
                    });
                    p += 12;
                }
                return list;
            }

            private int? RawValue(int tag)
            {
                foreach (var e in _ifd0)
                    if (e.Tag == tag) return e.Raw;
                return null;
            }

            private static int TypeSize(int type)
            {
                switch (type)
                {
                    case 1: case 2: case 6: case 7: return 1;
                    case 3: case 8: return 2;
                    case 4: case 9: case 11: return 4;
                    case 5: case 10: case 12: return 8;
                    default: return 0;
                }
            }

            private int ValueOffset(Entry e)
            {
                int size = TypeSize(e.Type) * e.Count;
                // small value fits within the field itself: the data is at ValuePos
                if (size <= 4) return e.ValuePos;
                // otherwise it is an offset relative to the start of TIFF
                return _tiff + e.Raw;
            }

            private double? GetDouble(Entry e)
            {
                if (e.Type != 5 && e.Type != 10) return null;
                int o = ValueOffset(e);
                if (o < 0 || o + 8 > _b.Length) return null;
                double num = U32(o);
                double den = U32(o + 4);
                if (den == 0) return null;
                return num / den;
            }

            private double? GetSRational(Entry e)
            {
                if (e.Type != 10) return null;
                int o = ValueOffset(e);
                if (o < 0 || o + 8 > _b.Length) return null;
                double num = U32(o);
                double den = U32(o + 4);
                if (den == 0) return null;
                return num / den;
            }

            private string GetString(Entry e)
            {
                if (e.Type != 2 && e.Type != 7) return null;
                int o = ValueOffset(e);
                if (o < 0 || o >= _b.Length) return null;
                int max = Math.Min(e.Count, _b.Length - o);
                int end = o;
                while (end < o + max && _b[end] != 0) end++;
                if (end == o) return null;
                return Encoding.UTF8.GetString(_b, o, end - o).Trim();
            }

            private int? GetInt(Entry e)
            {
                int o = ValueOffset(e);
                if (o < 0 || o + 4 > _b.Length) return null;
                switch (e.Type)
                {
                    case 1: return _b[o];
                    case 3: return U16(o);
                    case 4: return (int)U32(o);
                    case 8: return (short)U16(o);
                    case 9: return (int)U32(o);
                    default: return null;
                }
            }

            private double? GetDms(Entry e)
            {
                if (e.Count < 3) return null;
                int o = ValueOffset(e);
                if (o < 0 || o + 24 > _b.Length) return null;
                double d = U32(o) / (double)U32(o + 4);
                double m = U32(o + 8) / (double)U32(o + 12);
                double s = U32(o + 16) / (double)U32(o + 20);
                return d + m / 60.0 + s / 3600.0;
            }

            private ushort U16(int o)
            {
                if (o + 2 > _b.Length) return 0;
                return _le ? (ushort)(_b[o] | (_b[o + 1] << 8)) : (ushort)((_b[o] << 8) | _b[o + 1]);
            }

            private uint U32(int o)
            {
                if (o + 4 > _b.Length) return 0;
                return _le
                    ? (uint)(_b[o] | (_b[o + 1] << 8) | (_b[o + 2] << 16) | (_b[o + 3] << 24))
                    : (uint)((_b[o] << 24) | (_b[o + 1] << 16) | (_b[o + 2] << 8) | _b[o + 3]);
            }

            private void ApplyIfd0(ExifData d)
            {
                foreach (var e in _ifd0)
                {
                    switch (e.Tag)
                    {
                        case 0x010F: d.CameraMake = GetString(e); break;
                        case 0x0110: d.CameraModel = GetString(e); break;
                        case 0x0112: d.Orientation = GetInt(e); break;
                        case 0x0131: d.Software = GetString(e); break;
                        case 0x0132: d.TakenLocal = ParseExifDate(GetString(e)) ?? d.TakenLocal; break;
                        case 0x013B: d.Artist = GetString(e); break;
                        case 0x8298: d.Copyright = GetString(e); break;
                    }
                }
            }

            private void ApplyExifIfd(ExifData d, List<Entry> entries)
            {
                foreach (var e in entries)
                {
                    switch (e.Tag)
                    {
                        case 0x829A: d.ExposureSeconds = GetDouble(e); break;
                        case 0x829D: d.FNumber = GetDouble(e); break;
                        case 0x8822: d.ExposureProgram = GetInt(e); break;
                        case 0x8827:
                        case 0x8833:
                            {
                                var dv = GetDouble(e);
                                if (dv.HasValue) d.Iso = dv;
                                else if (!d.Iso.HasValue)
                                {
                                    var iv = GetInt(e);
                                    if (iv.HasValue) d.Iso = iv.Value;
                                }
                                break;
                            }
                        case 0x9003: d.TakenLocal = ParseExifDate(GetString(e)) ?? d.TakenLocal; break;
                        case 0x9011: d.TakenOffset = GetString(e); break;
                        case 0x9201:
                            {
                                var v = GetSRational(e);
                                if (v.HasValue && !d.ExposureSeconds.HasValue) d.ExposureSeconds = Math.Pow(2, -v.Value);
                                break;
                            }
                        case 0x9202:
                            {
                                var v = GetDouble(e);
                                if (v.HasValue && !d.FNumber.HasValue) d.FNumber = Math.Pow(2, v.Value / 2.0);
                                break;
                            }
                        case 0x9204: d.ExposureBias = GetSRational(e); break;
                        case 0x9207: d.MeteringMode = GetInt(e); break;
                        case 0x9209: d.Flash = GetInt(e); break;
                        case 0x920A: d.FocalLength = GetDouble(e); break;
                        case 0x927C: d.HasMakerNote = true; break;
                        case 0x9286: d.UserComment = GetString(e); break;
                        case 0xA001: d.ColorSpace = GetInt(e); break;
                        case 0xA002: d.PixelWidth = GetInt(e); break;
                        case 0xA003: d.PixelHeight = GetInt(e); break;
                        case 0xA403: d.WhiteBalance = GetInt(e); break;
                        case 0xA405: d.FocalLength35 = GetInt(e); break;
                        case 0xA430: d.OwnerName = GetString(e); break;
                        case 0xA431: d.BodySerial = GetString(e); break;
                        case 0xA433: d.LensMake = GetString(e); break;
                        case 0xA434: d.LensModel = GetString(e); break;
                    }
                }
            }

            private void ApplyGpsIfd(ExifData d, List<Entry> entries)
            {
                double? lat = null, lon = null;
                string latRef = null, lonRef = null;
                foreach (var e in entries)
                {
                    switch (e.Tag)
                    {
                        case 0x0001: latRef = GetString(e); break;
                        case 0x0002: lat = GetDms(e); break;
                        case 0x0003: lonRef = GetString(e); break;
                        case 0x0004: lon = GetDms(e); break;
                        case 0x0006: d.GpsAltitude = GetDouble(e); break;
                        case 0x001D: d.GpsDateTaken = ParseExifDate(GetString(e)); break;
                    }
                }
                if (lat.HasValue && lon.HasValue)
                {
                    double la = lat.Value, lo = lon.Value;
                    if (string.Equals(latRef, "S", StringComparison.OrdinalIgnoreCase)) la = -la;
                    if (string.Equals(lonRef, "W", StringComparison.OrdinalIgnoreCase)) lo = -lo;
                    d.GpsLatitude = la;
                    d.GpsLongitude = lo;
                }
            }
        }

        public static DateTime? ParseExifDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (s.Length >= 19 && s[4] == ':' && s[7] == ':')
                s = s.Substring(0, 4) + "-" + s.Substring(5, 2) + "-" + s.Substring(8, 2) + " " + s.Substring(11, 8);
            DateTime dt;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;
            return null;
        }

        /// <summary>Converts the read EXIF into metadata stored in cache by the app.</summary>
        public static PhotoMetadata ToMeta(ExifData exif, string relativePath, long size, long modifiedUtcTicks)
        {
            var m = new PhotoMetadata
            {
                RelativePath = relativePath,
                Size = size,
                ModifiedUtcTicks = modifiedUtcTicks,
                CameraMake = exif?.CameraMake,
                CameraModel = exif?.CameraModel,
                LensModel = exif?.LensModel,
                Software = exif?.Software,
                BodySerial = exif?.BodySerial,
                ExposureSeconds = exif?.ExposureSeconds,
                FNumber = exif?.FNumber,
                Iso = exif?.Iso,
                FocalLength = exif?.FocalLength,
                ExposureBias = exif?.ExposureBias,
                Flash = exif?.Flash,
                MeteringMode = exif?.MeteringMode,
                WhiteBalance = exif?.WhiteBalance,
                ColorSpace = exif?.ColorSpace,
                Orientation = exif?.Orientation,
                Rating = exif?.Rating,
                Width = exif?.PixelWidth ?? 0,
                Height = exif?.PixelHeight ?? 0,
                GpsLatitude = exif?.GpsLatitude,
                GpsLongitude = exif?.GpsLongitude,
                GpsAltitude = exif?.GpsAltitude,
                GpsDateTaken = exif?.GpsDateTaken
            };
            if (exif != null && exif.TakenLocal.HasValue)
                m.TakenUtc = DateTime.SpecifyKind(exif.TakenLocal.Value, DateTimeKind.Utc);
            return m;
        }
    }
}
