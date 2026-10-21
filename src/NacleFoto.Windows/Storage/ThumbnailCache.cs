using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using NacleFoto.Windows.Imaging;

namespace NacleFoto.Windows.Storage
{
    /// <summary>
    /// On-disk thumbnail cache. Self-indexing: the file name is the hash of
    /// (path, size, timestamp), so a modified file generates a new thumbnail
    /// and there is no table to invalidate.
    /// </summary>
    public sealed class ThumbnailCache
    {
        private readonly string _dir;
        private readonly int _maxSide;

        public ThumbnailCache(string appDataRoot, string volumeKey, string folderPath, int maxSide)
        {
            _maxSide = maxSide <= 0 ? 256 : maxSide;
            var key = Hash(volumeKey + "|" + Normalize(folderPath));
            _dir = Path.Combine(appDataRoot, "Thumbs", key);
            Directory.CreateDirectory(_dir);
        }

        public string Folder => _dir;

        private static string Normalize(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd('\\').ToUpperInvariant(); }
            catch { return (p ?? "").ToUpperInvariant(); }
        }

        private static string Hash(string s)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private string PathFor(string relativePath, long size, long mtimeTicks)
        {
            var key = Hash(relativePath.ToUpperInvariant() + "|" + size + "|" + mtimeTicks);
            return Path.Combine(_dir, key + ".jpg");
        }

        public bool TryGet(string relativePath, long size, long mtimeTicks, out BitmapSource bitmap)
        {
            bitmap = null;
            try
            {
                var file = PathFor(relativePath, size, mtimeTicks);
                if (!File.Exists(file)) return false;
                var bytes = File.ReadAllBytes(file);
                bitmap = WicLoader.DecodeJpeg(bytes);
                if (bitmap == null)
                {
                    try { File.Delete(file); } catch { }
                    return false;
                }
                Touch(file);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Save(string relativePath, long size, long mtimeTicks, byte[] jpeg)
        {
            if (jpeg == null || jpeg.Length == 0) return;
            try
            {
                var file = PathFor(relativePath, size, mtimeTicks);
                var tmp = file + ".tmp";
                File.WriteAllBytes(tmp, jpeg);
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            catch (Exception)
            {
            }
        }

        public void Save(string relativePath, long size, long mtimeTicks, BitmapSource source)
        {
            var jpeg = WicLoader.EncodeJpeg(source, 82);
            if (jpeg != null) Save(relativePath, size, mtimeTicks, jpeg);
        }

        private static void Touch(string file)
        {
            try { File.SetLastWriteTimeUtc(file, DateTime.UtcNow); } catch { }
        }

        /// <summary>Removes thumbnails not used for a long time (flash drive full of junk).</summary>
        public int Clear(int olderThanDays = 45)
        {
            int removed = 0;
            try
            {
                var threshold = DateTime.UtcNow.AddDays(-olderThanDays);
                foreach (var f in Directory.GetFiles(_dir, "*.jpg"))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(f) < threshold) { File.Delete(f); removed++; }
                    }
                    catch { }
                }
            }
            catch (Exception)
            {
            }
            return removed;
        }
    }
}
