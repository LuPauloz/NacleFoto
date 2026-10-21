using System;
using System.Diagnostics;

namespace NacleFoto.Windows.Shell
{
    /// <summary>
    /// Opens links (WhatsApp) and folders (Explorer). Interface to allow testing
    /// without actually opening a browser or Explorer.
    /// </summary>
    public interface IShellLauncher
    {
        void OpenUrl(string url);
        void OpenFolder(string path);
    }

    public sealed class ShellLauncher : IShellLauncher
    {
        public void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try { Process.Start(url); }
            catch (Exception) { }
        }

        public void OpenFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { Process.Start("explorer.exe", "\"" + path + "\""); }
            catch (Exception) { }
        }
    }
}
