using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NacleFoto.Windows.Shell
{
    /// <summary>
    /// Volume identity. On a USB flash drive, the drive letter may change (E: -> F:) from one day to
    /// the next, but the volume serial number remains the same. Using the serial as the cache key
    /// ensures thumbnails reappear even when the drive letter changes.
    /// </summary>
    public static class VolumeIdentity
    {
        public static string GetVolumeKey(string path)
        {
            try
            {
                string root = GetRoot(path);
                if (string.IsNullOrEmpty(root)) return Path.GetPathRoot(path) ?? path;

                var name = new StringBuilder(260);
                int serial, maxComp, flags;
                var fsName = new StringBuilder(260);
                bool ok = GetVolumeInformation(root, name, name.Capacity, out serial,
                                              out maxComp, out flags, fsName, fsName.Capacity);
                if (ok) return serial.ToString("X8");
                return root;
            }
            catch (Exception)
            {
                return path;
            }
        }

        public static string GetRoot(string path)
        {
            try
            {
                var full = Path.GetFullPath(path);
                var root = Path.GetPathRoot(full);
                return string.IsNullOrEmpty(root) ? full : root;
            }
            catch (Exception)
            {
                return path;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string lpRootPathName,
            StringBuilder lpVolumeNameBuffer,
            int dwVolumeNameSize,
            out int lpVolumeSerialNumber,
            out int lpMaximumComponentLength,
            out int lpFileSystemFlags,
            StringBuilder lpFileSystemNameBuffer,
            int dwFileSystemNameSize);
    }
}
