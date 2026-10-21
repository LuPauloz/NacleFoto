using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NacleFoto.Windows.Shell
{
    /// <summary>
    /// Places files on the Windows clipboard as files (CF_HDROP).
    /// In WhatsApp Web, simply press Ctrl+V to attach - no dragging, no file picker.
    /// </summary>
    public static class ClipboardFiles
    {
        public static bool CopyFiles(IEnumerable<string> files)
        {
            var list = new List<string>();
            foreach (var f in files)
                if (!string.IsNullOrEmpty(f) && File.Exists(f))
                    list.Add(f);
            if (list.Count == 0) return false;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var collection = new System.Collections.Specialized.StringCollection();
                    collection.AddRange(list.ToArray());
                    Clipboard.SetFileDropList(collection);
                    return true;
                }
                catch (Exception)
                {
                    Thread.Sleep(60);
                }
            }
            return false;
        }

        public static bool CopyFiles(string file)
        {
            return CopyFiles(new[] { file });
        }

        /// <summary>Places plain text on the clipboard.</summary>
        public static bool CopyText(string text)
        {
            if (text == null) return false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (Exception)
                {
                    Thread.Sleep(60);
                }
            }
            return false;
        }
    }
}
