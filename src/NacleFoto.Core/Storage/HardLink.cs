using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NacleFoto.Core.Storage
{
    /// <summary>
    /// File "shortcut": hard link. Points to the same file without consuming disk
    /// space. Used in delivery folder to avoid duplicating 1 GB of photos.
    /// </summary>
    public static class HardLink
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes);

        /// <summary>
        /// Creates a hard link. Only works on the same volume (same drive/USB drive).
        /// </summary>
        public static bool TryCreate(string newPath, string originalFile)
        {
            try
            {
                if (string.IsNullOrEmpty(newPath) || string.IsNullOrEmpty(originalFile)) return false;
                if (!File.Exists(originalFile)) return false;

                var dir = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(newPath)) File.Delete(newPath);

                return CreateHardLink(newPath, originalFile, IntPtr.Zero);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Link path, or null if unsuccessful (copying is plan B).</summary>
        public static string CreateOrCopy(string newPath, string originalFile)
        {
            if (TryCreate(newPath, originalFile)) return newPath;

            try
            {
                var dir = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.Copy(originalFile, newPath, true);
                return newPath;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
