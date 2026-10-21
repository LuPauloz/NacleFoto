using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using NacleFoto.Core.Models;

namespace NacleFoto.Core.Packaging
{
    /// <summary>A file to be included in the send package.</summary>
    public sealed class PackageItem
    {
        /// <summary>Absolute file path on disk.</summary>
        public string SourcePath { get; set; }
        /// <summary>Name inside the ZIP (preserves folders so the print lab can organize).</summary>
        public string ZipName { get; set; }
        public long Size { get; set; }
    }

    public sealed class PackageResult
    {
        public string Folder { get; set; }
        public List<PackagePart> Parts { get; set; } = new List<PackagePart>();
        public long TotalBytes { get; set; }
        public int TotalFiles { get; set; }
        public string ReadmePath { get; set; }
        public string InventoryPath { get; set; }
    }

    /// <summary>
    /// Builds the send package: splits the selection into ZIPs that fit within
    /// WhatsApp limits (100 MB on WhatsApp Web) and generates the README for the print lab.
    /// </summary>
    public static class PackageBuilder
    {
        public static PackageResult Build(
            IEnumerable<PackageItem> items,
            string destinationFolder,
            string baseName,
            long maxPartBytes,
            Wedding wedding,
            Action<int, int> progress = null,
            CancellationToken cancellationToken = default(CancellationToken),
            bool includeExtras = true)
        {
            if (maxPartBytes < 1024 * 1024) maxPartBytes = 95L * 1024 * 1024;

            Directory.CreateDirectory(destinationFolder);
            var list = items.Where(i => File.Exists(i.SourcePath)).ToList();

            var readme = BuildReadme(wedding, list.Count, 0);
            var inventory = BuildInventory(list, wedding);
            var bytesReadme = Encoding.UTF8.GetByteCount(readme);
            var bytesInv = Encoding.UTF8.GetByteCount(inventory);

            // 1) split
            var groups = new List<List<PackageItem>>();
            var current = new List<PackageItem>();
            long currentBytes = 0;
            long reserved = (groups.Count == 0) ? bytesReadme + bytesInv : 0;

            foreach (var item in list)
            {
                if (item.Size + currentBytes + reserved > maxPartBytes && current.Count > 0)
                {
                    groups.Add(current);
                    current = new List<PackageItem>();
                    currentBytes = 0;
                    reserved = 0;
                }
                current.Add(item);
                currentBytes += item.Size;
            }
            if (current.Count > 0) groups.Add(current);
            if (groups.Count == 0) groups.Add(new List<PackageItem>());

            var result = new PackageResult
            {
                Folder = destinationFolder,
                TotalFiles = list.Count
            };

            // 2) write zip files
            int total = groups.Count;
            int processed = 0;
            for (int g = 0; g < total; g++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = groups[g];
                var name = baseName + "_" + (g + 1).ToString("00") + "de" + total.ToString("00") + ".zip";
                var path = Path.Combine(destinationFolder, name);

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 16, FileOptions.SequentialScan))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    if (g == 0 && includeExtras)
                    {
                        WriteEntry(zip, "LEIA-ME.txt", readme);
                        WriteEntry(zip, "INVENTARIO.csv", inventory);
                    }
                    foreach (var item in group)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var entry = zip.CreateEntry(item.ZipName, CompressionLevel.Fastest);
                        entry.LastWriteTime = new DateTimeOffset(File.GetLastWriteTime(item.SourcePath));
                        using (var src = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var dst = entry.Open())
                        {
                            src.CopyTo(dst, 1 << 16);
                        }
                        processed++;
                        if (progress != null) progress(processed, list.Count);
                    }
                }

                result.Parts.Add(new PackagePart
                {
                    FilePath = path,
                    Bytes = new FileInfo(path).Length,
                    Part = g + 1,
                    TotalParts = total,
                    FileCount = group.Count
                });
            }

            // 3) supporting files in the folder (make resending and verification easier)
            var readmePath = Path.Combine(destinationFolder, "LEIA-ME.txt");
            File.WriteAllText(readmePath, readme, new UTF8Encoding(true));
            var invPath = Path.Combine(destinationFolder, "INVENTARIO.csv");
            File.WriteAllText(invPath, inventory, new UTF8Encoding(true));

            result.ReadmePath = readmePath;
            result.InventoryPath = invPath;
            result.TotalBytes = result.Parts.Sum(p => p.Bytes);
            return result;
        }

        private static void WriteEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
            using (var s = entry.Open())
            using (var w = new StreamWriter(s, new UTF8Encoding(true)))
            {
                w.Write(content);
            }
        }

        public static string BuildReadme(Wedding wedding, int count, int parts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ENVIO DE FOTOS PARA IMPRESSÃO");
            sb.AppendLine(new string('=', 46));
            sb.AppendLine();
            sb.AppendLine("Wedding : " + (wedding?.Name ?? ""));
            if (!string.IsNullOrEmpty(wedding?.Client)) sb.AppendLine("Client   : " + wedding.Client);
            if (wedding?.EventDate.HasValue == true) sb.AppendLine("Data      : " + wedding.EventDate.Value.ToString("dd/MM/yyyy"));
            sb.AppendLine("Photos     : " + count);
            sb.AppendLine("Sent   : " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            sb.AppendLine();
            sb.AppendLine("COMO IMPRIMIR");
            sb.AppendLine(new string('-', 46));
            sb.AppendLine("1. Ao receber os arquivos .zip, descompacte todos numa mesma pasta.");
            sb.AppendLine("2. Se vierem vários arquivos, junte as fotos de todos em uma só pasta.");
            sb.AppendLine("3. Envie essa pasta com as fotos em resolução original.");
            sb.AppendLine("4. Para impressão em papel fotográfico, use 20x30 cm (ou 10x15 cm).");
            sb.AppendLine("   O fotógrafo já enquadrou as fotos na proporção 3:2.");
            sb.AppendLine();
            sb.AppendLine("Observação: as fotos já foram selecionadas pelo fotógrafo.");
            sb.AppendLine("A lista completa dos arquivos está no INVENTARIO.csv.");
            return sb.ToString();
        }

        public static string BuildInventory(IEnumerable<PackageItem> items, Wedding wedding)
        {
            var sb = new StringBuilder();
            sb.AppendLine("arquivo;bytes;data_modificacao");
            foreach (var i in items)
            {
                string data = "";
                try { data = File.GetLastWriteTime(i.SourcePath).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
                sb.AppendLine(i.ZipName + ";" + i.Size + ";" + data);
            }
            return sb.ToString();
        }

        /// <summary>Copies the generated package to a synchronized folder (link mode).</summary>
        public static string CopyToSyncFolder(PackageResult package, string syncFolder, string subfolder)
        {
            var destination = Path.Combine(syncFolder, subfolder);
            Directory.CreateDirectory(destination);
            foreach (var p in package.Parts)
            {
                var target = Path.Combine(destination, Path.GetFileName(p.FilePath));
                File.Copy(p.FilePath, target, true);
            }
            if (!string.IsNullOrEmpty(package.ReadmePath))
                File.Copy(package.ReadmePath, Path.Combine(destination, "LEIA-ME.txt"), true);
            if (!string.IsNullOrEmpty(package.InventoryPath))
                File.Copy(package.InventoryPath, Path.Combine(destination, "INVENTARIO.csv"), true);
            return destination;
        }
    }
}
