using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Models;
using NacleFoto.Core.Packaging;
using NacleFoto.Core.Storage;

namespace NacleFoto.Core.Linking
{
    /// <summary>Result of download link generation.</summary>
    public sealed class LinkResult
    {
        public string Text { get; set; }
        public List<string> Urls { get; set; } = new List<string>();
        public string Folder { get; set; }
        public bool IsFolderMode { get; set; }
    }

    /// <summary>
    /// Generates the download link from the SELECTED photos (original files).
    /// Does not create a permanent folder: the zip goes to the temporary folder and is deleted afterwards.
    /// </summary>
    public static class LinkBuilder
    {
        public static LinkResult Build(
            AppSettings settings,
            Wedding wedding,
            IList<PackageItem> items,
            Action<int, int> progress = null,
            CancellationToken cancel = default(CancellationToken))
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (items == null || items.Count == 0) throw new InvalidOperationException("No photos selected.");

            switch (settings.LinkProvider)
            {
                case "folder": return BuildFolder(settings, wedding, items, progress, cancel);
                case "sendit": return BuildSendit(settings, wedding, items, progress, cancel);
                default:
                    throw new InvalidOperationException("Link provider not configured.");
            }
        }

        /// <summary>Compresses photos into a temporary zip, uploads, and deletes the zip.</summary>
        private static LinkResult BuildSendit(AppSettings settings, Wedding wedding, IList<PackageItem> items,
            Action<int, int> progress, CancellationToken cancel)
        {
            var client = new SenditClient();
            var tempFolder = Path.Combine(Path.GetTempPath(), "NacleFoto", "Envio-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            var result = new LinkResult();

            try
            {
                Directory.CreateDirectory(tempFolder);
                // only photos inside the zip: no README and no spreadsheet
                var package = PackageBuilder.Build(items, tempFolder, "Envio", 95L * 1024 * 1024, wedding,
                    (done, total) => { if (progress != null) progress(done, total); }, cancel, false);

                int i = 0;
                foreach (var part in package.Parts)
                {
                    cancel.ThrowIfCancellationRequested();
                    var link = client.UploadFile(part.FilePath, Path.GetFileName(part.FilePath), null, cancel);
                    result.Urls.Add(link);
                    i++;
                    if (progress != null) progress(i, package.Parts.Count);
                }

                // the text is ONLY the link (ready to paste)
                result.Text = string.Join(Environment.NewLine, result.Urls);
                return result;
            }
            finally
            {
                // the temporary zip does not remain on disk
                try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
            }
        }

        /// <summary>Copies photos to the synchronized folder (hard links, without consuming disk space).</summary>
        private static LinkResult BuildFolder(AppSettings settings, Wedding wedding, IList<PackageItem> items,
            Action<int, int> progress, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(settings.SyncFolder) || !Directory.Exists(settings.SyncFolder))
                throw new InvalidOperationException("Synced folder not configured.");

            var subfolder = WeddingFiles.SafeName(wedding?.Name) + "_" + DateTime.Now.ToString("yyyy-MM-dd");
            var target = Path.Combine(settings.SyncFolder, subfolder);
            Directory.CreateDirectory(target);

            int i = 0;
            foreach (var item in items)
            {
                cancel.ThrowIfCancellationRequested();
                var destination = Path.Combine(target, Path.GetFileName(item.SourcePath));
                if (File.Exists(destination)) File.Delete(destination);
                if (!HardLink.TryCreate(destination, item.SourcePath))
                    File.Copy(item.SourcePath, destination, true);
                i++;
                if (progress != null) progress(i, items.Count);
            }

            var result = new LinkResult
            {
                Folder = target,
                IsFolderMode = true
            };
            result.Text = settings.AppLanguage == AppLanguage.English
                ? "The photos were copied to the synced folder:\n" + target +
                  "\n\nShare this folder with the print shop (right click > Share in Windows Explorer)."
                : "As fotos foram copiadas para a pasta sincronizada:\n" + target +
                  "\n\nCompartilhe essa pasta com a grafica (botao direito > Compartilhar no Explorer).";
            return result;
        }

        private static string BuildText(Wedding wedding, List<string> urls, AppLanguage language)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(language == AppLanguage.English
                ? "Photos of " + (wedding?.Name ?? "") + ":"
                : "Fotos do " + (wedding?.Name ?? "") + ":");
            foreach (var u in urls) sb.AppendLine(u);
            return sb.ToString().Trim();
        }
    }
}
