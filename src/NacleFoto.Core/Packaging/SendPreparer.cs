using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using NacleFoto.Core.Packaging;

namespace NacleFoto.Core.Packaging
{
    /// <summary>Result of send preparation (zip, loose files, or both).</summary>
    public sealed class SendPreparation
    {
        public List<PackagePart> Parts { get; set; } = new List<PackagePart>();
        public string Folder { get; set; }
        public string ReadmePath { get; set; }
        public string InventoryPath { get; set; }
        public string LooseFolder { get; set; }
        public int LooseFileCount { get; set; }
        /// <summary>How many of the files are hard links (do not consume disk space).</summary>
        public int LooseHardLinks { get; set; }
        public long TotalBytes { get; set; }
        public bool HasZip { get; set; }
        public bool HasLoose { get; set; }
    }

    /// <summary>
    /// Prepares files for sending: splits into ZIPs (WhatsApp limit) and/or copies
    /// loose photos to a folder, generates README for the print lab and inventory.
    /// </summary>
    public static class SendPreparer
    {
        public static SendPreparation Prepare(
            Wedding wedding,
            IEnumerable<PackageItem> items,
            string destinationFolder,
            long maxPartBytes,
            SendFormat format,
            Action<int, int> progress = null,
            CancellationToken cancel = default(CancellationToken))
        {
            var list = (items ?? new List<PackageItem>()).ToList();
            var result = new SendPreparation();
            Directory.CreateDirectory(destinationFolder);

            var zipName = WeddingFiles.SafeName(wedding?.Name) + "_" + DateTime.Now.ToString("yyyy-MM-dd");
            var zipFolder = Path.Combine(destinationFolder, zipName);

            if (format == SendFormat.Zip || format == SendFormat.Both)
            {
                var package = PackageBuilder.Build(list, zipFolder, zipName, maxPartBytes, wedding, progress, cancel);
                result.Parts = package.Parts;
                result.ReadmePath = package.ReadmePath;
                result.InventoryPath = package.InventoryPath;
                result.TotalBytes = package.TotalBytes;
                result.HasZip = true;
                result.Folder = zipFolder;
            }

            if (format == SendFormat.Loose || format == SendFormat.Both)
            {
                var looseFolder = Path.Combine(destinationFolder, zipName + "_fotos");
                int copied = 0;
                int shortcuts = 0;
                foreach (var item in list)
                {
                    cancel.ThrowIfCancellationRequested();
                    try
                    {
                        var destination = Path.Combine(looseFolder, item.ZipName.Replace('/', Path.DirectorySeparatorChar));
                        var dir = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        if (File.Exists(destination)) File.Delete(destination);

                        // hard link: points to the original photo without copying 1 GB.
                        // If not supported (different drives), falls back to copy.
                        if (HardLink.TryCreate(destination, item.SourcePath)) shortcuts++;
                        else File.Copy(item.SourcePath, destination, true);

                        copied++;
                    }
                    catch (Exception) { }
                    if (progress != null) progress(copied, list.Count);
                }
                result.LooseFolder = looseFolder;
                result.LooseFileCount = copied;
                result.LooseHardLinks = shortcuts;
                result.HasLoose = true;
                if (!result.HasZip) result.Folder = looseFolder;
            }

            return result;
        }

        /// <summary>WhatsApp message containing the wedding name and multi-part notice.</summary>
        public static string BuildWhatsAppMessage(Wedding wedding, int parts, AppLanguage language)
        {
            var template = language == AppLanguage.English
                ? "Hello! Here are the selected photos from {wedding}. {parts}When you receive them, extract and print in high resolution. Thank you!"
                : "Ola! Segue o envio das fotos selecionadas do {wedding}. {parts}Ao receber, descompacte e imprima em alta resolucao. Obrigado!";

            var partsNotice = "";
            if (parts > 1)
            {
                partsNotice = language == AppLanguage.English
                    ? string.Format(CultureInfo.CurrentCulture,
                        "The file was split into {0} parts because WhatsApp does not accept large files. ",
                        parts)
                    : string.Format(CultureInfo.CurrentCulture,
                        "O arquivo foi dividido em {0} partes porque o WhatsApp nao aceita arquivos grandes. ",
                        parts);
            }

            return template
                .Replace("{wedding}", wedding?.Name ?? "")
                .Replace("{parts}", partsNotice);
        }

        /// <summary>WhatsApp link with the print lab conversation pre-opened and message pre-filled.</summary>
        public static string BuildWhatsAppLink(string phone, string message)
        {
            var normalized = NormalizePhone(phone);
            if (string.IsNullOrEmpty(normalized)) return null;
            return "https://wa.me/" + normalized + "?text=" + Uri.EscapeDataString(message ?? "");
        }

        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new System.Text.StringBuilder();
            foreach (var c in phone)
                if (char.IsDigit(c)) digits.Append(c);
            var d = digits.ToString();
            if (d.Length == 0) return null;
            if (d.StartsWith("00")) d = d.Substring(2);
            return d;
        }
    }
}
