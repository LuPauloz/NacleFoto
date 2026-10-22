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

        public const int DefaultMaxDepth = 8;

        public static readonly HashSet<string> IgnoredDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".vscode", ".idea", ".github",
            "node_modules", "_nacle", "$recycle.bin", "$sysreset",
            "system volume information", "appdata", "windows",
            "program files", "program files (x86)", "recovery",
            "backups", "databases"
        };

        public static bool ShouldSkipDirectory(string dirName, FileAttributes attrs = 0)
        {
            if (string.IsNullOrWhiteSpace(dirName)) return true;
            if (IgnoredDirectories.Contains(dirName)) return true;
            if (dirName.StartsWith(".") && dirName.Length > 1) return true;
            if ((attrs & (FileAttributes.Hidden | FileAttributes.System)) != 0) return true;
            if (dirName.IndexOf("voice notes", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (dirName.IndexOf("video notes", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private struct DirEntry
        {
            public string Path;
            public int Depth;
            public DirEntry(string path, int depth) { Path = path; Depth = depth; }
        }

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
                                      CancellationToken cancel = default(CancellationToken),
                                      bool includeAllSubfolders = true,
                                      ICollection<string> includedSubfolders = null,
                                      Action<List<PhotoItemViewModel>> batchProgress = null)
        {
            var result = new List<PhotoItemViewModel>();
            var currentBatch = new List<PhotoItemViewModel>();
            int targetBatchSize = 40;

            var exclusions = (excluded ?? new List<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Replace('\\', '/').TrimEnd('/'))
                .ToList();

            var inclusions = (includedSubfolders ?? new List<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Replace('\\', '/').TrimEnd('/'))
                .ToList();

            int cloudCount = 0;

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

                bool isDriveRoot = false;
                try
                {
                    var rootPath = Path.GetFullPath(root).TrimEnd('\\') + "\\";
                    var driveRoot = Path.GetPathRoot(rootPath);
                    isDriveRoot = string.Equals(driveRoot, rootPath, StringComparison.OrdinalIgnoreCase);
                }
                catch { }

                int maxDepth = isDriveRoot ? 2 : DefaultMaxDepth;
                var queue = new Queue<DirEntry>();
                queue.Enqueue(new DirEntry(root, 0));

                while (queue.Count > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    var current = queue.Dequeue();

                    IEnumerable<string> dirFiles = null;
                    try
                    {
                        dirFiles = Directory.EnumerateFiles(current.Path);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var file in dirFiles)
                        {
                            cancel.ThrowIfCancellationRequested();

                            var ext = Path.GetExtension(file);
                            if (string.IsNullOrEmpty(ext)) continue;
                            ext = ext.ToLowerInvariant();
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
                                var item = new PhotoItemViewModel(relNorm, file, info.Length, info.LastWriteTimeUtc.Ticks);
                                result.Add(item);
                                if (progress != null && result.Count % 50 == 0) progress(result.Count);

                                if (batchProgress != null)
                                {
                                    currentBatch.Add(item);
                                    if (currentBatch.Count >= targetBatchSize)
                                    {
                                        batchProgress(currentBatch);
                                        currentBatch = new List<PhotoItemViewModel>();
                                        targetBatchSize = 100;
                                    }
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception) { }

                    if (current.Depth < maxDepth)
                    {
                        if (!includeAllSubfolders && inclusions.Count == 0)
                            continue;

                        IEnumerable<string> subDirs = null;
                        try
                        {
                            subDirs = Directory.EnumerateDirectories(current.Path);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        foreach (var dir in subDirs)
                        {
                            var dirName = Path.GetFileName(dir);
                            FileAttributes attrs = 0;
                            try { attrs = File.GetAttributes(dir); } catch { }

                            if (ShouldSkipDirectory(dirName, attrs)) continue;

                            string subRel;
                            try { subRel = GetRelativePath(root, dir).Replace('\\', '/'); }
                            catch { subRel = null; }
                            if (string.IsNullOrEmpty(subRel)) continue;

                            if (exclusions.Any(x => string.Equals(x, subRel, StringComparison.OrdinalIgnoreCase) || subRel.StartsWith(x + "/", StringComparison.OrdinalIgnoreCase)))
                                continue;

                            if (!includeAllSubfolders)
                            {
                                bool isIncluded = inclusions.Any(x =>
                                    string.Equals(x, subRel, StringComparison.OrdinalIgnoreCase) ||
                                    subRel.StartsWith(x + "/", StringComparison.OrdinalIgnoreCase) ||
                                    x.StartsWith(subRel + "/", StringComparison.OrdinalIgnoreCase));
                                if (!isIncluded) continue;
                            }

                            queue.Enqueue(new DirEntry(dir, current.Depth + 1));
                        }
                    }
                }
            }

            if (batchProgress != null && currentBatch.Count > 0)
            {
                batchProgress(currentBatch);
                currentBatch.Clear();
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
                    var name = Path.GetFileName(d);
                    FileAttributes attrs = 0;
                    try { attrs = File.GetAttributes(d); } catch { }
                    if (ShouldSkipDirectory(name, attrs)) continue;

                    list.Add(name);
                    try
                    {
                        foreach (var d2 in Directory.GetDirectories(d))
                        {
                            var name2 = Path.GetFileName(d2);
                            FileAttributes attrs2 = 0;
                            try { attrs2 = File.GetAttributes(d2); } catch { }
                            if (ShouldSkipDirectory(name2, attrs2)) continue;

                            list.Add(name + "\\" + name2);
                        }
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
