using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NacleFoto.App.ViewModels;
using NacleFoto.Windows.Imaging;
using NacleFoto.Windows.Storage;

namespace NacleFoto.App.Services
{
    /// <summary>
    /// Generates thumbnails in the background, prioritizing what is visible on screen.
    /// Uses 2 threads: on low-end PCs this does not freeze the UI or saturate the disk.
    /// </summary>
    public sealed class ThumbService : IDisposable
    {
        private sealed class Work
        {
            public PhotoItemViewModel Item;
            public int Priority; // lower = more urgent
            public long Seq;
        }

        private readonly ThumbnailCache _cache;
        private readonly int _maxSide;
        private readonly List<Work> _queue = new List<Work>();
        private readonly HashSet<string> _inProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();
        private readonly Thread[] _workers;
        private readonly Dispatcher _dispatcher;
        private volatile bool _stopping;
        private long _seq;

        public event Action<string> Failed;
        public event Action<string> Falhou
        {
            add { Failed += value; }
            remove { Failed -= value; }
        }

        public ThumbService(ThumbnailCache cache, int maxSide, int workers = 2)
        {
            _cache = cache;
            _maxSide = maxSide <= 0 ? 256 : maxSide;
            _dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;

            _workers = new Thread[Math.Max(1, workers)];
            for (int i = 0; i < _workers.Length; i++)
            {
                var t = new Thread(Loop) { IsBackground = true, Name = "miniaturas-" + i, Priority = ThreadPriority.BelowNormal };
                _workers[i] = t;
                t.Start();
            }
        }

        /// <summary>Queues a photo thumbnail. Priority 0 = urgent (visible).</summary>
        public void Request(PhotoItemViewModel item, int priority)
        {
            if (item == null || item.HasThumbnail) return;
            lock (_sync)
            {
                if (_stopping) return;
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (string.Equals(_queue[i].Item.RelativePath, item.RelativePath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (priority < _queue[i].Priority) _queue[i].Priority = priority;
                        return;
                    }
                }
                _queue.Add(new Work { Item = item, Priority = priority, Seq = _seq++ });
                Monitor.PulseAll(_sync);
            }
        }

        public void Clear()
        {
            lock (_sync) _queue.Clear();
        }

        private void Loop()
        {
            while (true)
            {
                Work work = null;
                lock (_sync)
                {
                    if (_stopping) return;
                    if (_queue.Count == 0)
                    {
                        Monitor.Wait(_sync, 250);
                        if (_stopping) return;
                    }
                    if (_queue.Count > 0)
                    {
                        int best = 0;
                        for (int i = 1; i < _queue.Count; i++)
                        {
                            if (_queue[i].Priority < _queue[best].Priority ||
                                (_queue[i].Priority == _queue[best].Priority && _queue[i].Seq < _queue[best].Seq))
                                best = i;
                        }
                        work = _queue[best];
                        _queue.RemoveAt(best);
                        if (!_inProgress.Add(work.Item.RelativePath)) { work = null; continue; }
                    }
                }
                if (work == null) continue;

                try
                {
                    Process(work.Item);
                }
                catch (Exception ex)
                {
                    var h = Failed;
                    if (h != null) h(ex.Message);
                }
                finally
                {
                    lock (_sync) _inProgress.Remove(work.Item.RelativePath);
                }
            }
        }

        private void Process(PhotoItemViewModel item)
        {
            if (item.HasThumbnail) return;

            System.Windows.Media.Imaging.BitmapSource bitmap = null;
            try
            {
                if (_cache != null && _cache.TryGet(item.RelativePath, item.Size, item.ModifiedUtcTicks, out bitmap))
                {
                    Apply(item, bitmap);
                    return;
                }
            }
            catch (Exception) { }

            var scaled = WicLoader.LoadScaled(item.FullPath, _maxSide);
            if (scaled == null) return;

            try
            {
                if (_cache != null)
                    _cache.Save(item.RelativePath, item.Size, item.ModifiedUtcTicks, scaled);
            }
            catch (Exception) { }

            Apply(item, scaled);
        }

        private void Apply(PhotoItemViewModel item, System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            try
            {
                if (_dispatcher != null && !_dispatcher.CheckAccess())
                {
                    _dispatcher.BeginInvoke(new Action(() => item.Thumb = bitmap));
                }
                else
                {
                    item.Thumb = bitmap;
                }
            }
            catch (Exception)
            {
                // dispatcher may have been shut down (window closed / app exiting)
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _stopping = true;
                Monitor.PulseAll(_sync);
            }
        }
    }
}
