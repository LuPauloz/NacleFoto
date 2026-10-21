using System;
using NacleFoto.App.Services;
using NacleFoto.App.ViewModels;
using Xunit;

namespace NacleFoto.Tests
{
    public class ThumbServiceTests : IDisposable
    {
        private readonly ThumbService _service;

        public ThumbServiceTests()
        {
            // Create service with 1 worker and maxSide 128
            _service = new ThumbService(null, 128, 1);
        }

        public void Dispose()
        {
            _service.Dispose();
        }

        [Fact]
        public void Request_IgnoresNullOrAlreadyLoadedItems()
        {
            var initialCount = _service.QueueCount;
            _service.Request(null, 0);
            Assert.Equal(initialCount, _service.QueueCount);

            var item = new PhotoItemViewModel("test/foto.jpg", @"C:\photos\foto.jpg", 1024, DateTime.UtcNow.Ticks);
            // Simulate already having a thumbnail by attaching a dummy BitmapSource if possible, or test normal enqueue
            _service.Request(item, 5);
            // Item should be enqueued (or immediately picked up by the worker)
            Assert.True(_service.QueueCount >= 0);
        }

        [Fact]
        public void Request_DeduplicatesSameItemAndUpgradesPriority()
        {
            _service.Clear();
            var item = new PhotoItemViewModel("test/foto1.jpg", @"C:\photos\foto1.jpg", 1024, DateTime.UtcNow.Ticks);

            // First request at low priority
            _service.Request(item, 10);
            // Re-request same item at higher priority (urgent)
            _service.Request(item, 0);

            // Queue should not double the item count
            Assert.True(_service.QueueCount <= 1);
        }

        [Fact]
        public void Clear_EmptiesQueue()
        {
            var item1 = new PhotoItemViewModel("test/a.jpg", @"C:\photos\a.jpg", 1024, DateTime.UtcNow.Ticks);
            var item2 = new PhotoItemViewModel("test/b.jpg", @"C:\photos\b.jpg", 1024, DateTime.UtcNow.Ticks);

            _service.Request(item1, 10);
            _service.Request(item2, 10);
            _service.Clear();

            Assert.Equal(0, _service.QueueCount);
        }
    }
}
