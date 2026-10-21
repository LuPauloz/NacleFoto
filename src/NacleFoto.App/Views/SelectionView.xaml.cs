using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NacleFoto.App.Services;
using NacleFoto.App.ViewModels;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Shell;
using NacleFoto.Windows.Storage;

namespace NacleFoto.App.Views
{
    /// <summary>The selection screen: photo grid, folders on the left, data on the right.</summary>
    public partial class SelectionView : UserControl, IDisposable
    {
        public event Action BackRequested;

        /// <summary>How many photos were indexed in the folder.</summary>
        public int PhotoCount { get { return _photos.Count; } }

        /// <summary>How many times a photo was opened in full screen (public for the tests).</summary>
        public int ZoomOpenCount { get; private set; }
        /// <summary>How many photos are selected.</summary>
        public int SelectedCount { get { return _photos.Count(f => f.IsSelected); } }
        /// <summary>The wedding open on screen.</summary>
        public Wedding CurrentWedding { get { return _wedding; } }
        /// <summary>Injectable for testing: folder picker does not open a real dialog.</summary>
        public IFolderPicker FolderPicker { get; set; } = new FolderBrowserPicker();

        /// <summary>Injectable for testing: dialog boxes become automated responses.</summary>
        public IUserDialog Dialog { get; set; } = new MessageBoxDialog();
        /// <summary>Fires after the selection is closed, to open the send screen.</summary>
        public event Action<Wedding> SendRequested;

        /// <summary>Fires when the user wants to start ANOTHER selection in the same folder.</summary>
        public event Action<Wedding> NewWeddingRequested;

        /// <summary>Injectable for testing: what to do after closing the selection.</summary>
        public Func<string, AfterCloseAction> AfterCloseHandler { get; set; }

        /// <summary>Diagnostics: how many photos already have EXIF read.</summary>
        public int PhotosWithExif { get { return _photos.Count(f => f.Meta != null); } }

        /// <summary>True while scanning folders in background.</summary>
        public bool IsScanning { get { return _scanning; } }

        /// <summary>Photos visible under current filter (used by zoom).</summary>
        public IList<PhotoItemViewModel> Photos { get { return FilteredPhotos(); } }

        public string ExampleBadge
        {
            get
            {
                var f = _photos.FirstOrDefault(x => x.Meta != null);
                return f == null ? "(nenhum)" : f.Meta.ShortBadge + " | " + f.Meta.Camera;
            }
        }
        private const int TileSize = 160;

        private Wedding _wedding;
        private readonly List<PhotoItemViewModel> _photos = new List<PhotoItemViewModel>();
        private List<PhotoRowViewModel> _rows = new List<PhotoRowViewModel>();
        private PhotoItemViewModel _focus;
        private ThumbService _thumbs;
        private ThumbnailCache _cache;
        private readonly Stack<Action> _undo = new Stack<Action>();
        private readonly Stack<Action> _redo = new Stack<Action>();
        private int _columns = 1;
        private CancellationTokenSource _cts;
        private DispatcherTimer _saveTimer;
        private int _exifReadCount;
        private bool _isReady;
        private bool _scanning;
        private int _lastColumn = -1;

        public SelectionView()
        {
            InitializeComponent();
            Scroller.SizeChanged += (s, e) => RecalculateColumns();
            PreviewKeyDown += SelectionView_PreviewKeyDown;
            _isReady = true;
        }

        // =========================== open ===========================

        public void Open(Wedding c)
        {
            if (c == null) return;
            _wedding = c;

            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }
            if (_thumbs != null) { _thumbs.Dispose(); _thumbs = null; }

            _undo.Clear();
            _redo.Clear();
            _photos.Clear();
            _focus = null;

            NameBox.Text = c.Name;
            UpdateSubtitle();

            Scan();
        }

        /// <summary>
        /// Scans folder in background. Enumeration can be VERY slow in cloud
        /// folders (OneDrive): if left on the UI thread, the app would freeze.
        /// </summary>
        private async void Scan()
        {
            var roots = new List<string>();
            if (!string.IsNullOrEmpty(_wedding.RootFolder)) roots.Add(_wedding.RootFolder);
            if (_wedding.ExtraFolders != null) roots.AddRange(_wedding.ExtraFolders);

            _scanning = true;
            Barra.Visibility = Visibility.Visible;
            Barra.Value = 0;
            TxtStatus.Text = Strings.T("status.scanning");

            var excludeCloud = !App.Settings.IncludeCloudFiles;
            var wedding = _wedding;
            var found = await Task.Run(() =>
            {
                var indexer = new PhotoIndexer();
                return indexer.Scan(roots, wedding.ExcludedFolders, excludeCloud,
                    n => Dispatcher.Invoke(() => { Barra.Value = Math.Min(100, n / 50.0); }),
                    warning => Dispatcher.Invoke(() => TxtStatus.Text = warning));
            });

            if (wedding != _wedding) return; // user switched wedding mid-scan

            _photos.Clear();
            _photos.AddRange(found);

            // a photo the app was started with: opens in full screen now
            OpenPendingPhoto();

            // restore saved selection
            var selectedMap = new Dictionary<string, SelectionItem>(StringComparer.OrdinalIgnoreCase);
            if (_wedding.Selection != null)
                foreach (var s in _wedding.Selection)
                    selectedMap[s.Path] = s;

            foreach (var f in _photos)
            {
                SelectionItem s;
                if (selectedMap.TryGetValue(f.RelativePath, out s))
                {
                    f.IsSelected = true;
                    f.Rating = s.Rating;
                    f.Color = s.Color;
                }
            }

            // thumbnail cache (keyed by volume: USB drive with changed drive letter is reused)
            try
            {
                var volumeKey = VolumeIdentity.GetVolumeKey(
                    string.IsNullOrEmpty(_wedding.RootFolder) ? roots.FirstOrDefault() ?? "" : _wedding.RootFolder);
                _cache = new ThumbnailCache(App.AppDataDir, volumeKey,
                    string.IsNullOrEmpty(_wedding.RootFolder) ? roots.FirstOrDefault() ?? "" : _wedding.RootFolder,
                    App.Settings.ThumbnailSize);
                _thumbs = new ThumbService(_cache, App.Settings.ThumbnailSize, 1);
            }
            catch (Exception)
            {
                _cache = null;
                _thumbs = new ThumbService(null, App.Settings.ThumbnailSize, 1);
            }

            BuildFolderList();
            RebuildGrid();
            UpdateCounters();
            Barra.Visibility = Visibility.Collapsed;
            _scanning = false;

            StartExifReading();
            RequestThumbnails();
        }

        private void BuildFolderList()
        {
            var items = new List<FolderItemViewModel>();
            var root = _wedding.RootFolder;
            if (!string.IsNullOrEmpty(root))
            {
                items.Add(new FolderItemViewModel
                {
                    Name = Strings.T("selection.allFolders"),
                    RelativePath = null,
                    IsIncluded = true
                });
                var subfolders = PhotoIndexer.ListSubfolders(root);
                foreach (var s in subfolders)
                {
                    var norm = s.Replace('\\', '/');
                    var excluded = _wedding.ExcludedFolders.Contains(norm, StringComparer.OrdinalIgnoreCase);
                    items.Add(new FolderItemViewModel
                    {
                        Name = s.Replace('\\', '/'),
                        RelativePath = norm,
                        IsIncluded = !excluded
                    });
                }
            }
            foreach (var p in _wedding.ExtraFolders)
            {
                items.Add(new FolderItemViewModel { Name = p, RelativePath = null, IsIncluded = true, IsExternal = true });
            }
            FolderList.ItemsSource = items;
        }

        // =========================== grid ===========================

        private int CalculateColumns()
        {
            if (Scroller == null) return 1;
            double w = Scroller.ActualWidth;
            if (w < 200) w = 800;
            return Math.Max(1, (int)((w - 6) / TileSize));
        }

        private void RecalculateColumns()
        {
            if (!_isReady) return;
            var newCols = CalculateColumns();
            if (newCols == _lastColumn) return;
            _lastColumn = newCols;
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (!_isReady || PhotoGrid == null) return;
            _columns = CalculateColumns();
            var visible = new List<PhotoItemViewModel>();
            bool onlyMarked = FilterMarked.IsChecked == true;
            bool onlyUnmarked = FilterUnmarked.IsChecked == true;

            foreach (var f in _photos)
            {
                if (onlyMarked && !f.IsSelected) continue;
                if (onlyUnmarked && f.IsSelected) continue;
                visible.Add(f);
            }

            _rows = new List<PhotoRowViewModel>();
            for (int i = 0; i < visible.Count; i += _columns)
            {
                var slice = visible.GetRange(i, Math.Min(_columns, visible.Count - i));
                _rows.Add(new PhotoRowViewModel(slice, _columns, _columns * TileSize));
            }

            PhotoGrid.ItemsSource = _rows;
            TxtStatus.Text = _photos.Count == 0
                ? Strings.T("status.noPhotos")
                : Strings.T("status.folderCount", _photos.Count, visible.Count);
            PrioritizeVisible();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isReady) return;
            RebuildGrid();
        }

        private void Tile_Zoom(object sender, EventArgs e)
        {
            var tile = sender as PhotoTile;
            if (tile == null || tile.Photo == null) return;
            OpenZoom(tile.Photo);
        }

        /// <summary>Opens the photo in full screen.</summary>
        private void OpenZoom(PhotoItemViewModel photo)
        {
            if (photo == null) return;
            _focus = photo;
            ZoomOpenCount++;

            var visible = FilteredPhotos();
            int idx = visible.IndexOf(photo);
            if (idx < 0) idx = 0;

            var window = new ZoomWindow();
            // owner = the app window: the photo stays above the grid, but Alt+Tab
            // works normally (a window on top of everything gets stuck)
            try { window.Owner = Window.GetWindow(this); } catch (Exception) { }
            window.Open(visible, idx);
            window.NavigationRequested += d => window.Navigate(d);
            window.Marked += f =>
            {
                ToggleSelection(f);
                UpdateExifPanel();
                if (window.IsLoaded) window.UpdateMarkedState();
            };
            window.Closed += (s, ee) => RebuildGrid();
            window.Show();
        }

        /// <summary>
        /// Opens a photo by full path in full screen (used when the app is
        /// started with a photo, for example from the Windows Explorer).
        /// </summary>
        public void OpenPhotoByPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;

            var target = _photos.FirstOrDefault(f =>
                f != null && string.Equals(f.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (target == null) return;

            OpenZoom(target);
        }

        /// <summary>
        /// Opens a photo in full screen as soon as the folder is scanned. Used
        /// when the app is started with a photo: the folder opens and the photo
        /// shows on top of it, Esc goes back to the grid.
        /// </summary>
        public void OpenPhotoWhenReady(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            _pendingPhoto = fullPath;
            if (_photos.Count > 0) OpenPendingPhoto();
        }

        private string _pendingPhoto;

        private void OpenPendingPhoto()
        {
            if (string.IsNullOrEmpty(_pendingPhoto)) return;
            var path = _pendingPhoto;
            _pendingPhoto = null;

            var target = _photos.FirstOrDefault(f =>
                f != null && string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase));
            if (target != null) OpenZoom(target);
        }

        private List<PhotoItemViewModel> FilteredPhotos()
        {
            var list = new List<PhotoItemViewModel>();
            bool onlyMarked = FilterMarked.IsChecked == true;
            bool onlyUnmarked = FilterUnmarked.IsChecked == true;
            foreach (var f in _photos)
            {
                if (onlyMarked && !f.IsSelected) continue;
                if (onlyUnmarked && f.IsSelected) continue;
                list.Add(f);
            }
            return list;
        }

        private void Scroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0) PrioritizeVisible();
        }

        private void PrioritizeVisible()
        {
            if (_thumbs == null) return;
            int first = (int)(Scroller.VerticalOffset / TileSize);
            int last = (int)((Scroller.VerticalOffset + Scroller.ViewportHeight) / TileSize);
            last = Math.Min(last, _rows.Count - 1);
            for (int i = Math.Max(0, first); i <= last; i++)
                foreach (var f in _rows[i].Photos)
                    _thumbs.Request(f, 0);
        }

        private void RequestThumbnails()
        {
            if (_thumbs == null) return;
            foreach (var f in _photos) _thumbs.Request(f, 10);
        }

        // =========================== selection ===========================

        private void Tile_Marked(object sender, EventArgs e)
        {
            var tile = sender as PhotoTile;
            if (tile == null || tile.Photo == null) return;
            _focus = tile.Photo;
            ToggleSelection(tile.Photo);
            UpdateExifPanel();
        }

        private void ToggleSelection(PhotoItemViewModel f)
        {
            var previous = f.IsSelected;
            f.IsSelected = !previous;

            var photo = f;
            _undo.Push(() => { photo.IsSelected = previous; AfterMark(photo, true); });
            _redo.Clear();

            AfterMark(f, false);
        }

        private void AfterMark(PhotoItemViewModel f, bool isRedo)
        {
            UpdateCounters();
            ScheduleSave();
            if (FilterUnmarked.IsChecked == true || FilterMarked.IsChecked == true)
                RebuildGrid();
        }

        private void BtnMarkVisible_Click(object sender, RoutedEventArgs e)
        {
            var targets = VisiblePhotos().Where(f => !f.IsSelected).ToList();
            if (targets.Count == 0) return;
            foreach (var f in targets) f.IsSelected = true;
            RegisterBatchAction(targets, true);
        }

        private void BtnUnmarkVisible_Click(object sender, RoutedEventArgs e)
        {
            var targets = VisiblePhotos().Where(f => f.IsSelected).ToList();
            if (targets.Count == 0) return;
            foreach (var f in targets) f.IsSelected = false;
            RegisterBatchAction(targets, false);
        }

        /// <summary>
        /// A batch action (mark/unmark several at once) becomes ONE undo step,
        /// not one per photo.
        /// </summary>
        private void RegisterBatchAction(List<PhotoItemViewModel> targets, bool isMarked)
        {
            var list = targets.ToList();
            _undo.Push(() =>
            {
                foreach (var f in list) f.IsSelected = !isMarked;
                UpdateCounters();
                ScheduleSave();
                RebuildGrid();
            });
            _redo.Clear();
            UpdateCounters();
            ScheduleSave();
            if (FilterMarked.IsChecked == true || FilterUnmarked.IsChecked == true) RebuildGrid();
        }

        private void BtnMarkAll_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialog.Confirm(Strings.T("app.name"), Strings.T("msg.markAllConfirm", _photos.Count))) return;
            var targets = _photos.Where(f => !f.IsSelected).ToList();
            foreach (var f in targets) f.IsSelected = true;
            RegisterBatchAction(targets, true);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_wedding.TotalSelected == 0 && !_photos.Any(f => f.IsSelected)) return;
            if (!Dialog.Confirm(Strings.T("app.name"), Strings.T("msg.unmarkAllConfirm"))) return;
            var targets = _photos.Where(f => f.IsSelected).ToList();
            foreach (var f in targets) f.IsSelected = false;
            RegisterBatchAction(targets, false);
        }

        private List<PhotoItemViewModel> VisiblePhotos()
        {
            var list = new List<PhotoItemViewModel>();
            if (_rows.Count == 0) return list;
            int first = Math.Max(0, (int)(Scroller.VerticalOffset / TileSize));
            int last = Math.Min(_rows.Count - 1, (int)((Scroller.VerticalOffset + Scroller.ViewportHeight) / TileSize));
            for (int i = first; i <= last; i++) list.AddRange(_rows[i].Photos);
            return list;
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void Undo()
        {
            if (_undo.Count == 0) return;
            var action = _undo.Pop();
            action();
            _redo.Push(action);
        }

        // =========================== folders ===========================

        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            var chk = sender as CheckBox;
            if (chk == null) return;
            var item = chk.DataContext as FolderItemViewModel;
            if (item == null || item.RelativePath == null) return;

            var norm = item.RelativePath;
            if (chk.IsChecked == true)
                _wedding.ExcludedFolders.RemoveAll(x => string.Equals(x, norm, StringComparison.OrdinalIgnoreCase));
            else
            {
                if (!_wedding.ExcludedFolders.Contains(norm, StringComparer.OrdinalIgnoreCase))
                    _wedding.ExcludedFolders.Add(norm);
            }
            Scan();
            ScheduleSave();
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.Pick(Strings.T("btn.addFolder"), null);
            if (string.IsNullOrEmpty(folder)) return;
            if (!_wedding.ExtraFolders.Contains(folder))
                _wedding.ExtraFolders.Add(folder);
            Scan();
            ScheduleSave();
        }

        // =========================== keyboard ===========================

        private void SelectionView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox) return;

            if (e.Key == Key.X && _focus != null)
            {
                ToggleSelection(_focus);
                UpdateExifPanel();
                e.Handled = true;
            }
            else if (e.Key == Key.Space && _focus != null)
            {
                ToggleSelection(_focus);
                UpdateExifPanel();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _focus != null)
            {
                // Enter opens photo in full screen
                OpenZoom(_focus);
                e.Handled = true;
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (BackRequested != null) BackRequested();
                e.Handled = true;
            }
        }

        // =========================== panel ===========================

        private void UpdateCounters()
        {
            int markedCount = _photos.Count(f => f.IsSelected);
            long bytes = _photos.Where(f => f.IsSelected).Sum(f => f.Size);
            TxtSelected.Text = markedCount.ToString();
            TxtSelectedLabel.Text = Strings.T("selection.markedPhotos");
            TxtSize.Text = PhotoItemViewModel.FormatSize(bytes);
            UpdateSubtitle();
        }

        private void UpdateSubtitle()
        {
            if (_wedding == null) return;
            var folder = _wedding.RootFolder;
            var extra = _wedding.ExtraFolders != null && _wedding.ExtraFolders.Count > 0
                ? " (+" + _wedding.ExtraFolders.Count + " pasta(s))" : "";
            Subtitle.Text = (folder ?? "") + extra;
        }

        private void UpdateExifPanel()
        {
            if (_focus == null)
            {
                TxtPhotoName.Text = "";
                ExifList.ItemsSource = null;
                TxtNoPhoto.Visibility = Visibility.Visible;
                return;
            }
            TxtNoPhoto.Visibility = Visibility.Collapsed;
            TxtPhotoName.Text = _focus.Name;

            var rows = new List<ExifRow>();
            var m = _focus.Meta;
            if (m != null)
            {
                Add(rows, Strings.T("exif.date"), m.TakenUtc.HasValue ? m.TakenUtc.Value.ToString("dd/MM/yyyy HH:mm:ss") : null);
                Add(rows, Strings.T("exif.camera"), m.Camera);
                Add(rows, Strings.T("exif.lens"), m.LensModel);
                Add(rows, Strings.T("exif.shutter"), m.ExposureSeconds.HasValue ? PhotoMetadata.FormatShutter(m.ExposureSeconds.Value) : null);
                Add(rows, Strings.T("exif.aperture"), m.FNumber.HasValue ? "f/" + m.FNumber.Value.ToString("0.#") : null);
                Add(rows, Strings.T("exif.iso"), m.Iso.HasValue ? m.Iso.Value.ToString("0") : null);
                Add(rows, Strings.T("exif.focal"), m.FocalLength.HasValue ? m.FocalLength.Value.ToString("0") + " mm" : null);
                Add(rows, Strings.T("exif.dimensions"), m.Width > 0 && m.Height > 0 ? m.Width + " x " + m.Height + " px" : null);
                Add(rows, Strings.T("exif.gps"), m.GpsLatitude.HasValue ? m.GpsLatitude.Value.ToString("0.0000") + ", " + m.GpsLongitude.Value.ToString("0.0000") : null);
            }
            Add(rows, Strings.T("exif.file"), _focus.SizeText);
            Add(rows, Strings.T("exif.folder"), _focus.RelativePath.Contains('\\') || _focus.RelativePath.Contains('/')
                ? _focus.RelativePath.Substring(0, _focus.RelativePath.LastIndexOfAny(new[] { '\\', '/' })) : "");
            ExifList.ItemsSource = rows;
        }

        private static void Add(List<ExifRow> list, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            list.Add(new ExifRow(label, value));
        }

        // =========================== background exif ===========================

        private async void StartExifReading()
        {
            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _exifReadCount = 0;

            var photos = _photos.ToList();
            await Task.Run(() =>
            {
                // batches of 20: parallel reading within batch (2 threads, to avoid overloading
                // low-end PCs) and applying to UI after each batch, so data appears
                // incrementally rather than all at the end
                const int BATCH_SIZE = 20;
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = token
                };

                for (int start = 0; start < photos.Count; start += BATCH_SIZE)
                {
                    if (token.IsCancellationRequested) return;
                    int end = Math.Min(start + BATCH_SIZE, photos.Count);
                    var batch = new List<KeyValuePair<PhotoItemViewModel, PhotoMetadata>>(end - start);

                    Parallel.For(start, end, options, index =>
                    {
                        var f = photos[index];
                        var meta = PhotoIndexer.ReadExif(f);
                        lock (batch) batch.Add(new KeyValuePair<PhotoItemViewModel, PhotoMetadata>(f, meta));
                    });

                    if (token.IsCancellationRequested) return;
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var i in batch) i.Key.Meta = i.Value;
                        _exifReadCount += batch.Count;
                        UpdateProgressBar();
                    });
                }
            });

            Dispatcher.Invoke(() =>
            {
                _exifReadCount = photos.Count;
                UpdateProgressBar();
                if (_focus != null) UpdateExifPanel();
            });
        }

        private void UpdateProgressBar()
        {
            if (_photos.Count == 0) { Barra.Visibility = Visibility.Collapsed; return; }
            Barra.Visibility = Visibility.Visible;
            Barra.Value = 100.0 * _exifReadCount / _photos.Count;
            if (_exifReadCount >= _photos.Count)
            {
                Barra.Visibility = Visibility.Collapsed;
                TxtStatus.Text = Strings.T("status.ready", _photos.Count);
            }
        }

        // =========================== save ===========================

        private void ScheduleSave()
        {
            if (_wedding == null) return;
            if (_saveTimer == null)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); SaveNow(); };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveNow()
        {
            if (_wedding == null) return;
            try
            {
                var items = new List<SelectionItem>();
                foreach (var f in _photos)
                {
                    if (!f.IsSelected) continue;
                    items.Add(new SelectionItem
                    {
                        Path = f.RelativePath,
                        Size = f.Size,
                        ModifiedUtcTicks = f.ModifiedUtcTicks,
                        Rating = f.Rating,
                        Color = f.Color
                    });
                }
                _wedding.Selection = items;
                ProjectStore.Save(_wedding, App.AppDataDir);
            }
            catch (Exception ex)
            {
                Dialog.Error(Strings.T("app.name"), Strings.T("msg.saveError", ex.Message));
            }
        }

        // =========================== close ===========================

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseSelection();
        }

        private void CloseSelection()
        {
            if (_wedding == null) return;

            int markedCount = _photos.Count(f => f.IsSelected);
            if (markedCount == 0)
            {
                if (!Dialog.Confirm(Strings.T("app.name"), Strings.T("msg.closeNone"))) return;
            }

            long bytes = _photos.Where(f => f.IsSelected).Sum(f => f.Size);
            if (!Dialog.Confirm(Strings.T("app.name"), Strings.T("msg.closeConfirm", _wedding.Name, markedCount, PhotoItemViewModel.FormatSize(bytes)))) return;

            SaveNow();
            _wedding.State = WeddingState.Closed;
            _wedding.ClosedAt = DateTime.Now;
            ProjectStore.Save(_wedding, App.AppDataDir);

            // closing the selection does NOT mean wanting to send: ask the user
            var summary = Strings.T("after.summary", markedCount, PhotoItemViewModel.FormatSize(bytes));
            AfterCloseAction action;
            if (AfterCloseHandler != null)
            {
                action = AfterCloseHandler(summary);
            }
            else
            {
                var dialog = new AfterCloseDialog(summary);
                try { dialog.Owner = Window.GetWindow(this); } catch (Exception) { }
                dialog.ShowDialog();
                action = dialog.Result;
            }

            if (action == AfterCloseAction.SendNow)
            {
                if (SendRequested != null) SendRequested(_wedding);
            }
            else if (action == AfterCloseAction.NewSelection)
            {
                if (NewWeddingRequested != null) NewWeddingRequested(_wedding);
                else if (BackRequested != null) BackRequested();
            }
            else if (BackRequested != null) BackRequested();
        }

        /// <summary>Releases thumbnail threads and in-progress tasks.</summary>
        public void Dispose()
        {
            if (_thumbs != null)
            {
                _thumbs.Dispose();
                _thumbs = null;
            }
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
                _cts = null;
            }
            if (_saveTimer != null)
            {
                _saveTimer.Stop();
                _saveTimer = null;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            SaveNow();
            if (BackRequested != null) BackRequested();
        }

        private void NameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_wedding == null) return;
            var newName = (NameBox.Text ?? "").Trim();
            if (newName.Length == 0) { NameBox.Text = _wedding.Name; return; }
            _wedding.Name = newName;
            ScheduleSave();
        }

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ((TextBox)sender).Focus();
                ((TextBox)sender).SelectAll();
            }
        }

        /// <summary>A folder in the left list.</summary>
        private sealed class FolderItemViewModel
        {
            public string Name { get; set; }
            public string RelativePath { get; set; }
            public bool IsIncluded { get; set; }
            public bool IsExternal { get; set; }
        }
    }
}
