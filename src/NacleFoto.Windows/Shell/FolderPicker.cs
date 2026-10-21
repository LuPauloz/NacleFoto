using System;
using System.IO;
using System.Windows.Forms;

namespace NacleFoto.Windows.Shell
{
    /// <summary>
    /// Folder picker. Exists as an interface so UI buttons can
    /// be tested without opening a real dialog.
    /// </summary>
    public interface IFolderPicker
    {
        /// <summary>Returns the chosen folder or null if the user cancels.</summary>
        string Pick(string title, string initialFolder);
    }

    /// <summary>Managed implementation (without COM interop, which used to crash the process).</summary>
    public sealed class FolderBrowserPicker : IFolderPicker
    {
        public string Pick(string title, string initialFolder)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = title;
                    dialog.ShowNewFolderButton = false;
                    dialog.SelectedPath = string.IsNullOrEmpty(initialFolder) ? null : initialFolder;

                    if (dialog.ShowDialog() == DialogResult.OK)
                        return string.IsNullOrEmpty(dialog.SelectedPath) ? null : dialog.SelectedPath;
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
