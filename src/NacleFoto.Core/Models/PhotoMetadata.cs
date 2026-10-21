using System;

namespace NacleFoto.Core.Models
{
    /// <summary>
    /// Metadata of a photo, as extracted from the file (EXIF) or from the cached index.
    /// Holds no file reference: serializable and inexpensive to cache.
    /// </summary>
    public sealed class PhotoMetadata
    {
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public long ModifiedUtcTicks { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime? TakenUtc { get; set; }

        public string CameraMake { get; set; }
        public string CameraModel { get; set; }
        public string LensModel { get; set; }
        public string Software { get; set; }
        public string BodySerial { get; set; }

        public double? ExposureSeconds { get; set; }
        public double? FNumber { get; set; }
        public double? Iso { get; set; }
        public double? FocalLength { get; set; }
        public double? ExposureBias { get; set; }
        public int? Flash { get; set; }
        public int? MeteringMode { get; set; }
        public int? WhiteBalance { get; set; }
        public int? ColorSpace { get; set; }
        public int? Orientation { get; set; }
        public int? Rating { get; set; }

        public double? GpsLatitude { get; set; }
        public double? GpsLongitude { get; set; }
        public double? GpsAltitude { get; set; }
        public DateTime? GpsDateTaken { get; set; }

        public string ExifJson { get; set; }

        public DateTime ModifiedUtc
        {
            get { return new DateTime(ModifiedUtcTicks, DateTimeKind.Utc); }
            set { ModifiedUtcTicks = value.Ticks; }
        }

        public string Camera
        {
            get
            {
                if (string.IsNullOrEmpty(CameraMake) && string.IsNullOrEmpty(CameraModel))
                    return null;
                if (string.IsNullOrEmpty(CameraMake)) return CameraModel;
                if (string.IsNullOrEmpty(CameraModel)) return CameraMake;
                if (CameraModel.StartsWith(CameraMake, StringComparison.OrdinalIgnoreCase))
                    return CameraModel;
                return CameraMake + " " + CameraModel;
            }
        }

        /// <summary>Short badge text for the thumbnail overlay: 1/250  f/1.8  ISO400</summary>
        public string ShortBadge
        {
            get
            {
                if (!ExposureSeconds.HasValue && !FNumber.HasValue && !Iso.HasValue)
                    return null;
                var sb = new System.Text.StringBuilder();
                if (ExposureSeconds.HasValue) sb.Append(FormatShutter(ExposureSeconds.Value));
                if (FNumber.HasValue) sb.Append("  f/").Append(FNumber.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                if (Iso.HasValue) sb.Append("  ISO").Append(Iso.Value.ToString("0"));
                return sb.ToString().Trim();
            }
        }

        public static string FormatShutter(double seconds)
        {
            if (seconds <= 0) return "?";
            if (seconds >= 1) return seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s";
            return "1/" + (1.0 / seconds).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
