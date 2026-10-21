using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NacleFoto.App.ViewModels;
using NacleFoto.Core.Imaging;

namespace NacleFoto.App.Services
{
    /// <summary>
    /// Scans photo folders and builds the list. Scanning is done in the background
    /// (the UI thread cannot wait for the OneDrive cloud).
    /// </summary>
    public sealed class PhotoIndexer
    {
        public static readonly string[] Extensions = { ".jpg", ".jpeg" };
        public static readonly string[] Extensoes = Extensions;

        /// <summary>File markers for files that are still "in the cloud" (OneDrive, etc).</summary>
        private const FileAttributes Cloud = FileAttributes.Offline | (FileAttributes)0x00400000; // RecallOnDataAccess
        private const FileAttributes Nuvem = Cloud;

        /// <summary>
        /// Is the file available locally? Files "in the cloud" (OneDrive) would wait
        /// for download and freeze the entire app.
        /// </summary>
        public static bool IsLocal(string path)
        {
            try
            {
                var attrs = File.GetAttributes(path);
                return (attrs & Cloud) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }


        /// <summary>
        /// Lists image files. <paramref name="excluded"/> contains relative paths
        /// of subfolders that should not be included (e.g. another wedding in the same folder).
        /// </summary>
        public List<PhotoItemViewModel> Scan(IEnumerable<string> roots, ICollection<string> excluded,
                                      bool includeCloud = false,
                                      Action<int> progress = null,
                                      Action<string> warning = null,
                                      CancellationToken cancel = default(CancellationToken))
        {
            var result = new List<PhotoItemViewModel>();
            var exclusions = (excluded ?? new List<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Replace('\\', '/').TrimEnd('/'))
                .ToList();

            int cloudCount = 0;

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

                IEnumerable<string> files;
                try
                {
                    // enumeration can be slow in the cloud: foreach is consumed here,
                    // but ALWAYS on a background thread (see SelectionView)
                    files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
                }
                catch (Exception) { continue; }

                try
                {
                    foreach (var file in files)
                    {
                        cancel.ThrowIfCancellationRequested();

                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (Array.IndexOf(Extensions, ext) < 0) continue;

                        string relative;
                        try { relative = GetRelativePath(root, file); }
                        catch { continue; }

                        var relNorm = relative.Replace('\\', '/');
                        if (exclusions.Any(x => relNorm.StartsWith(x + "/", StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // cloud-only file? do not wait for download
                        if (!IsLocal(file))
                        {
                            cloudCount++;
                            if (!includeCloud) { if (progress != null) progress(result.Count); continue; }
                        }

                        try
                        {
                            var info = new FileInfo(file);
                            result.Add(new PhotoItemViewModel(relNorm, file, info.Length, info.LastWriteTimeUtc.Ticks));
                        }
                        catch (Exception) { }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { }
            }

            result.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

            if (cloudCount > 0 && warning != null)
                warning(cloudCount + " foto(s) estao na nuvem (OneDrive) e foram puladas, para o app nao travar esperando o download. Se quiser ver essas fotos, ative \"Incluir fotos que ainda estao na nuvem\" nas Configuracoes.");

            return result;
        }

        public static string GetRelativePath(string root, string file)
        {
            var r = Path.GetFullPath(root).TrimEnd('\\');
            var a = Path.GetFullPath(file);
            if (a.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase))
                return a.Substring(r.Length + 1);
            return Path.GetFileName(a);
        }


        /// <summary>Reads EXIF of a file (header only, without decoding the image).</summary>
        public static Core.Models.PhotoMetadata ReadExif(PhotoItemViewModel item)
        {
            // do not open cloud-only files: this hangs waiting for download
            if (!IsLocal(item.FullPath)) return null;
            var exif = ExifParser.ParseFile(item.FullPath);
            return ExifParser.ToMeta(exif, item.RelativePath, item.Size, item.ModifiedUtcTicks);
        }

        /// <summary>Lists subfolders (up to 2 levels) from root, for the folders panel.</summary>
        public static List<string> ListSubfolders(string root)
        {
            var list = new List<string>();
            if (!Directory.Exists(root)) return list;
            try
            {
                foreach (var d in Directory.GetDirectories(root))
                {
                    list.Add(Path.GetFileName(d));
                    try
                    {
                        foreach (var d2 in Directory.GetDirectories(d))
                            list.Add(Path.GetFileName(d) + "\\" + Path.GetFileName(d2));
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }
    }
}
