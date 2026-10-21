using System;
using System.Collections.Generic;
using System.IO;
using NacleFoto.Core.Models;
using NacleFoto.Core.Packaging;

namespace NacleFoto.Core.Storage
{
    /// <summary>
    /// Resolves selected files for a wedding on disk.
    /// The selection stores RELATIVE paths, so the same file is found
    /// even if the USB drive changes letter (E: -> F:).
    /// </summary>
    public static class WeddingFiles
    {
        public static string ResolvePath(Wedding w, string relative)
        {
            if (w == null || string.IsNullOrEmpty(relative)) return null;
            var rel = relative.Replace('\\', '/').TrimStart('/');

            var roots = new List<string>();
            if (!string.IsNullOrEmpty(w.RootFolder)) roots.Add(w.RootFolder);
            if (w.ExtraFolders != null) roots.AddRange(w.ExtraFolders);

            foreach (var root in roots)
            {
                try
                {
                    var full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(full)) return full;
                }
                catch (Exception) { }
            }

            // photo may be in one of the additional folders, with folder name prefixed
            foreach (var root in roots)
            {
                try
                {
                    var folderName = Path.GetFileName(root.TrimEnd('\\'));
                    if (rel.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        var remainder = rel.Substring(folderName.Length + 1);
                        var full = Path.Combine(root, remainder.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(full)) return full;
                    }
                }
                catch (Exception) { }
            }

            return null;
        }

        /// <summary>Lists selected files that still exist on disk.</summary>
        public static List<PackageItem> BuildPackageItems(Wedding w)
        {
            var list = new List<PackageItem>();
            if (w == null || w.Selection == null) return list;

            foreach (var s in w.Selection)
            {
                var full = ResolvePath(w, s.Path);
                if (full == null) continue;
                long size = s.Size;
                try
                {
                    var info = new FileInfo(full);
                    if (!info.Exists) continue;
                    size = info.Length;
                }
                catch (Exception) { continue; }

                list.Add(new PackageItem
                {
                    SourcePath = full,
                    ZipName = s.Path.Replace('\\', '/'),
                    Size = size
                });
            }
            return list;
        }

        /// <summary>Safe file name for the filesystem and zip.</summary>
        public static string SafeName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "envio";
            var chars = text.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                chars = chars.Replace(c, '_');
            chars = chars.Replace(" ", "_");
            if (chars.Length > 60) chars = chars.Substring(0, 60);
            return chars;
        }
    }
}
