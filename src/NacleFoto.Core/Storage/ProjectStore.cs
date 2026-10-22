using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NacleFoto.Core.Models;

namespace NacleFoto.Core.Storage
{
    /// <summary>
    /// Where .nproj files (each wedding's selection) are saved.
    /// Prefers the _nacle folder inside the photos folder itself, so selections
    /// travel alongside photos (works on external hard drives and USB drives). If the
    /// folder is not writable, falls back to the app data directory.
    /// </summary>
    public static class ProjectStore
    {
        public const string NacleFolder = "_nacle";
        public const string ProjectExtension = ".nproj";

        public static string GetAppDataDir()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NacleFoto");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string Slug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "casamento";
            var chars = name.ToLowerInvariant()
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();
            var slug = new string(chars);
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            slug = slug.Trim('-');
            if (slug.Length > 60) slug = slug.Substring(0, 60).Trim('-');
            return string.IsNullOrEmpty(slug) ? "casamento" : slug;
        }

        /// <summary>Path to the project file, preferring the photos folder.</summary>
        public static string ResolveProjectPath(Wedding wedding, string appDataDir)
        {
            if (!string.IsNullOrEmpty(wedding.RootFolder))
            {
                try
                {
                    var dir = Path.Combine(wedding.RootFolder, NacleFolder);
                    if (CanWrite(dir)) return Path.Combine(dir, Slug(wedding.Name) + ProjectExtension);
                    // file already exists in writable location? reuse it
                    var existing = Path.Combine(dir, Slug(wedding.Name) + ProjectExtension);
                    if (File.Exists(existing)) return existing;
                }
                catch (Exception) { }
            }
            return Path.Combine(appDataDir, "Projetos", wedding.Id + ProjectExtension);
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                using (var fs = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.WriteByte(0);
                }
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Save(Wedding wedding, string appDataDir)
        {
            wedding.UpdatedAt = DateTime.Now;
            var file = ResolveProjectPath(wedding, appDataDir);
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(wedding, Formatting.Indented);
            AtomicWrite(file, json);
            UpdateRecent(wedding, file, appDataDir);
        }

        public static Wedding Load(string file)
        {
            if (!File.Exists(file)) return null;
            try
            {
                var json = File.ReadAllText(file);
                var c = JsonConvert.DeserializeObject<Wedding>(json);
                if (c == null) return null;
                if (string.IsNullOrEmpty(c.Id)) c.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrEmpty(c.Name)) c.Name = "Sem nome";
                if (c.Selection == null) c.Selection = new List<SelectionItem>();
                if (c.Packages == null) c.Packages = new List<PackagePart>();
                if (c.ExcludedFolders == null) c.ExcludedFolders = new List<string>();
                if (c.IncludedSubfolders == null) c.IncludedSubfolders = new List<string>();
                if (c.ExtraFolders == null) c.ExtraFolders = new List<string>();
                return c;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Loads the project belonging to a photo folder (if any).</summary>
        public static Wedding LoadForRoot(string root, string appDataDir)
        {
            var dir = Path.Combine(root, NacleFolder);
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*" + ProjectExtension);
                if (files.Length > 0)
                {
                    var newest = files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();
                    var c = Load(newest);
                    if (c != null) return c;
                }
            }
            var all = LoadRecent(appDataDir);
            var found = all.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.RootFolder) &&
                string.Equals(Normalize(r.RootFolder), Normalize(root), StringComparison.OrdinalIgnoreCase));
            return found == null ? null : Load(found.FilePath);
        }

        private static string Normalize(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd('\\'); } catch { return p; }
        }

        // ---------------- recents (app entry point) ----------------

        public sealed class RecentItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string FilePath { get; set; }
            public string RootFolder { get; set; }
            public string State { get; set; }

            /// <summary>
            /// Number of marked photos. The JSON name stays "Marcadas" so the
            /// recent list files written by older versions keep loading.
            /// </summary>
            [JsonProperty("Marcadas")]
            public int MarkedCount { get; set; }

            public long Bytes { get; set; }
            public DateTime UpdatedAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public static List<RecentItem> LoadRecent(string appDataDir)
        {
            var file = Path.Combine(appDataDir, "recentes.json");
            if (!File.Exists(file)) return new List<RecentItem>();
            try
            {
                var list = JsonConvert.DeserializeObject<List<RecentItem>>(File.ReadAllText(file));
                return list ?? new List<RecentItem>();
            }
            catch (Exception)
            {
                return new List<RecentItem>();
            }
        }

        public static void UpdateRecent(Wedding wedding, string file, string appDataDir)
        {
            var list = LoadRecent(appDataDir);
            var existing = list.FirstOrDefault(r => r.Id == wedding.Id);
            DateTime createdAt = existing != null ? existing.CreatedAt : wedding.CreatedAt;

            list.RemoveAll(r => r.Id == wedding.Id);
            list.Add(new RecentItem
            {
                Id = wedding.Id,
                Name = wedding.Name,
                FilePath = file,
                RootFolder = wedding.RootFolder,
                State = wedding.State.ToString(),
                MarkedCount = wedding.TotalSelected,
                Bytes = wedding.SelectedSize,
                UpdatedAt = DateTime.Now,
                CreatedAt = createdAt
            });

            // newest first list
            list.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            if (list.Count > 60) list.RemoveRange(60, list.Count - 60);
            AtomicWrite(Path.Combine(appDataDir, "recentes.json"),
                        JsonConvert.SerializeObject(list, Formatting.Indented));
        }

        /// <summary>
        /// Retention policy: after N days old selections are removed from the list
        /// (the .nproj file is deleted). PHOTOS on disk are never touched.
        /// </summary>
        public static int ApplyRetention(int days, string appDataDir)
        {
            if (days <= 0) return 0;

            var list = LoadRecent(appDataDir);
            if (list.Count == 0) return 0;

            var threshold = DateTime.Now.AddDays(-days);
            var toRemove = list.Where(r => r.CreatedAt < threshold).ToList();
            if (toRemove.Count == 0) return 0;

            foreach (var r in toRemove)
            {
                try
                {
                    if (!string.IsNullOrEmpty(r.FilePath) && File.Exists(r.FilePath))
                        File.Delete(r.FilePath);
                }
                catch (Exception) { }
            }

            list.RemoveAll(r => r.CreatedAt < threshold);
            AtomicWrite(Path.Combine(appDataDir, "recentes.json"),
                        JsonConvert.SerializeObject(list, Formatting.Indented));
            return toRemove.Count;
        }


        public static void RemoveRecent(string id, string appDataDir)
        {
            var list = LoadRecent(appDataDir);
            list.RemoveAll(r => r.Id == id);
            AtomicWrite(Path.Combine(appDataDir, "recentes.json"),
                        JsonConvert.SerializeObject(list, Formatting.Indented));
        }

        /// <summary>
        /// Atomic write: writes to .tmp and replaces, so the file never gets corrupted.
        ///
        /// The content is written as UTF-8 WITH BOM on purpose: without the BOM,
        /// Windows tools that read text files as ANSI (PowerShell 5.1, old
        /// Notepad, some editors) mangle accented characters - a folder called
        /// "Área de Trabalho" turns into "ÃƒÂrea de Trabalho".
        /// </summary>
        public static void AtomicWrite(string file, string content)
        {
            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(true));
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }
    }
}
