using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NacleFoto.Windows.Imaging
{
    /// <summary>
    /// Image decoding via WIC (the same engine used by Windows Photos / Explorer).
    /// Performs scaled decoding: does not load 50 MP into memory to display a thumbnail.
    /// </summary>
    public static class WicLoader
    {
        /// <summary>
        /// Loads the image scaled to fit within maxSide pixels (0 = original size).
        /// Preserves aspect ratio and never upscales.
        /// </summary>
        public static BitmapSource LoadScaled(string path, int maxSide)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                // dimensions: reads only the file header without decoding
                int w, h;
                if (!TryGetSize(path, out w, out h) || w <= 0 || h <= 0) return null;

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bi.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);

                // only set ONE of the dimensions: WIC maintains the aspect ratio. If both are set,
                // it stretches the image.
                if (maxSide > 0)
                {
                    int maxDimension = Math.Max(w, h);
                    if (maxDimension > maxSide)
                    {
                        double scale = (double)maxSide / maxDimension;
                        if (w >= h) bi.DecodePixelWidth = Math.Max(1, (int)Math.Round(w * scale));
                        else bi.DecodePixelHeight = Math.Max(1, (int)Math.Round(h * scale));
                    }
                }

                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Reads only the dimensions without decoding pixels.</summary>
        public static bool TryGetSize(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    if (decoder.Frames.Count == 0) return false;
                    width = decoder.Frames[0].PixelWidth;
                    height = decoder.Frames[0].PixelHeight;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Encodes a BitmapSource as JPEG (used in the thumbnail cache).</summary>
        public static byte[] EncodeJpeg(BitmapSource source, int qualityLevel)
        {
            if (source == null) return null;
            try
            {
                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.QualityLevel = Math.Max(1, Math.Min(100, qualityLevel));
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Decodes JPEG bytes already in memory.</summary>
        public static BitmapSource DecodeJpeg(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                // without DelayCreation: the frame must be decoded before
                // disposing the stream (with lazy decode the image would be empty)
                using (var ms = new MemoryStream(bytes, false))
                {
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0) return null;
                    var bmp = decoder.Frames[0];
                    if (!bmp.IsFrozen) bmp.Freeze();
                    return bmp;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
