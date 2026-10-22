using System;
using System.IO;
using System.Linq;
using NacleFoto.App.Services;
using Xunit;

namespace NacleFoto.Tests
{
    public class PhotoIndexerTests : IDisposable
    {
        private readonly string _tempDir;

        public PhotoIndexerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "nacle-indexer-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void ShouldSkipDirectory_IgnoresKnownHeavyAndHiddenFolders()
        {
            Assert.True(PhotoIndexer.ShouldSkipDirectory(".git"));
            Assert.True(PhotoIndexer.ShouldSkipDirectory("node_modules"));
            Assert.True(PhotoIndexer.ShouldSkipDirectory("_nacle"));
            Assert.True(PhotoIndexer.ShouldSkipDirectory("$recycle.bin"));
            Assert.True(PhotoIndexer.ShouldSkipDirectory("appdata"));
            Assert.True(PhotoIndexer.ShouldSkipDirectory(".vscode"));
            Assert.True(PhotoIndexer.ShouldSkipDirectory(".custom_hidden"));

            Assert.False(PhotoIndexer.ShouldSkipDirectory("Cerimonia"));
            Assert.False(PhotoIndexer.ShouldSkipDirectory("Festa"));
            Assert.False(PhotoIndexer.ShouldSkipDirectory("Ensaio"));
        }

        [Fact]
        public void Scan_SkipsIgnoredFoldersAndOnlyIndexesJpg()
        {
            var validSubdir = Path.Combine(_tempDir, "Cerimonia");
            var gitSubdir = Path.Combine(_tempDir, ".git");
            var nodeSubdir = Path.Combine(_tempDir, "node_modules");

            Directory.CreateDirectory(validSubdir);
            Directory.CreateDirectory(gitSubdir);
            Directory.CreateDirectory(nodeSubdir);

            // Valid images
            File.WriteAllBytes(Path.Combine(_tempDir, "foto1.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });
            File.WriteAllBytes(Path.Combine(validSubdir, "foto2.jpeg"), new byte[] { 0xFF, 0xD8, 0xFF });

            // Ignored files (non-jpg)
            File.WriteAllText(Path.Combine(_tempDir, "documento.pdf"), "test");
            File.WriteAllText(Path.Combine(validSubdir, "script.js"), "test");

            // Files inside ignored directories
            File.WriteAllBytes(Path.Combine(gitSubdir, "git_foto.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });
            File.WriteAllBytes(Path.Combine(nodeSubdir, "node_foto.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });

            var indexer = new PhotoIndexer();
            var results = indexer.Scan(new[] { _tempDir }, null);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Name.Equals("foto1.jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(results, r => r.Name.Equals("foto2.jpeg", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(results, r => r.FullPath.Contains(".git"));
            Assert.DoesNotContain(results, r => r.FullPath.Contains("node_modules"));
        }

        [Fact]
        public void ListSubfolders_SkipsIgnoredDirectories()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Cerimonia"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "Festa"));
            Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "_nacle"));

            var subfolders = PhotoIndexer.ListSubfolders(_tempDir);

            Assert.Equal(2, subfolders.Count);
            Assert.Contains("Cerimonia", subfolders);
            Assert.Contains("Festa", subfolders);
            Assert.DoesNotContain(".git", subfolders);
            Assert.DoesNotContain("node_modules", subfolders);
            Assert.DoesNotContain("_nacle", subfolders);
        }

        [Fact]
        public void Scan_RootOnly_WhenIncludeAllSubfoldersFalse_DoesNotTraverseSubfolders()
        {
            var cerimonia = Path.Combine(_tempDir, "Cerimonia");
            Directory.CreateDirectory(cerimonia);

            File.WriteAllBytes(Path.Combine(_tempDir, "raiz.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });
            File.WriteAllBytes(Path.Combine(cerimonia, "sub.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });

            var indexer = new PhotoIndexer();
            var results = indexer.Scan(new[] { _tempDir }, null, false, null, null, default(System.Threading.CancellationToken), false, null);

            Assert.Single(results);
            Assert.Equal("raiz.jpg", results[0].Name);
        }

        [Fact]
        public void Scan_SelectiveSubfolders_OnlyTraversesIncludedSubfolders()
        {
            var cerimonia = Path.Combine(_tempDir, "Cerimonia");
            var festa = Path.Combine(_tempDir, "Festa");
            Directory.CreateDirectory(cerimonia);
            Directory.CreateDirectory(festa);

            File.WriteAllBytes(Path.Combine(_tempDir, "raiz.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });
            File.WriteAllBytes(Path.Combine(cerimonia, "foto_cerimonia.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });
            File.WriteAllBytes(Path.Combine(festa, "foto_festa.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });

            var indexer = new PhotoIndexer();
            var results = indexer.Scan(new[] { _tempDir }, null, false, null, null, default(System.Threading.CancellationToken), false, new[] { "Cerimonia" });

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Name == "raiz.jpg");
            Assert.Contains(results, r => r.Name == "foto_cerimonia.jpg");
            Assert.DoesNotContain(results, r => r.Name == "foto_festa.jpg");
        }
    }
}
