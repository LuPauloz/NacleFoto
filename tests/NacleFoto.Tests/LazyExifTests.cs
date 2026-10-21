using System;
using System.IO;
using NacleFoto.App.Services;
using NacleFoto.App.ViewModels;
using Xunit;

namespace NacleFoto.Tests
{
    public class LazyExifTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _sampleJpg;

        public LazyExifTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "nacle-lazy-exif-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            // Create a minimal valid JPEG with SOI and EOI markers
            _sampleJpg = Path.Combine(_tempDir, "sample.jpg");
            File.WriteAllBytes(_sampleJpg, new byte[] {
                0xFF, 0xD8,             // SOI
                0xFF, 0xE0, 0x00, 0x10, // APP0
                0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48, 0x00, 0x48, 0x00, 0x00,
                0xFF, 0xD9              // EOI
            });
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void PhotoItem_InitiallyHasNoExifUntilLazyRead()
        {
            var item = new PhotoItemViewModel("sample.jpg", _sampleJpg, new FileInfo(_sampleJpg).Length, DateTime.UtcNow.Ticks);

            // Initially Meta should be null, saving memory and CPU
            Assert.Null(item.Meta);
            Assert.False(item.HasBadge);
            Assert.Null(item.Badge);

            // On-demand EXIF read (as done when focused or visible)
            var meta = PhotoIndexer.ReadExif(item);
            item.Meta = meta;

            Assert.NotNull(item.Meta);
        }

        [Fact]
        public void ReadExif_HandlesMissingOrInvalidFileGracefully()
        {
            var nonExistent = new PhotoItemViewModel("nonexistent.jpg", Path.Combine(_tempDir, "ghost.jpg"), 0, 0);
            var result = PhotoIndexer.ReadExif(nonExistent);

            Assert.Null(result);
        }
    }
}
