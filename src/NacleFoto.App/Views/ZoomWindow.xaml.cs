using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NacleFoto.App.ViewModels;
using NacleFoto.Core.Localization;
using NacleFoto.Windows.Imaging;

namespace NacleFoto.App.Views
{
    /// <summary>
    /// Fullscreen viewer: zoom with mouse wheel, drag to pan,
    /// arrow keys to navigate, X to select, Esc to exit.
    /// </summary>
    public partial class ZoomWindow : Window
    {
        private readonly ScaleTransform _scale = new ScaleTransform(1, 1);
        private readonly TranslateTransform _translation = new TranslateTransform(0, 0);
        private readonly Dictionary<string, BitmapSource> _cache = new Dictionary<string, BitmapSource>();

        private PhotoItemViewModel _photo;
        private int _index;
        private bool _dragging;
        private Point _startPoint;
        private double _startX, _startY;
        private bool _loading;

        public event Action<PhotoItemViewModel> Marked;
        public event Action<int> NavigationRequested;

        public ZoomWindow()
        {
            InitializeComponent();
            KeyDown += ZoomWindow_KeyDown;
            Loaded += ZoomWindow_Loaded;
            SizeChanged += ZoomWindow_SizeChanged;
        }

        private void ZoomWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            if (WindowState != WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
            FitToScreen();
        }

        private void ZoomWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_dragging && _scale.ScaleX <= 1.0001)
            {
                FitToScreen();
            }
        }

        public void Open(IList<PhotoItemViewModel> photos, int index)
        {
            _photos = photos;
            _index = index;
            Display();
        }

        public void Navigate(int delta)
        {
            if (_photos == null) return;
            int next = _index + delta;
            if (next < 0 || next >= _photos.Count) return;
            _index = next;
            Display();
        }


        public void UpdateMarkedState()
        {
            if (_photo == null) return;
            bool marked = _photo.IsSelected;
            TxtMarca.Text = marked ? Strings.T("zoom.marked") : "";
            BtnMarcar.Content = marked ? Strings.T("zoom.unmark") : Strings.T("zoom.mark");
        }

        private void BtnMarcar_Click(object sender, RoutedEventArgs e)
        {
            if (_photo == null) return;
            if (Marked != null) Marked(_photo);
            UpdateMarkedState();
        }

        private IList<PhotoItemViewModel> _photos;

        private void Display()
        {
            if (_photos == null || _photos.Count == 0) return;
            if (_index < 0) _index = 0;
            if (_index >= _photos.Count) _index = _photos.Count - 1;

            _photo = _photos[_index];
            TxtNome.Text = _photo.Name;
            TxtMarca.Text = _photo.IsSelected ? Strings.T("zoom.marked") : "";
            BtnMarcar.Content = _photo.IsSelected ? Strings.T("zoom.unmark") : Strings.T("zoom.mark");
            UpdateMetadata();

            _cache.Clear();
            _scale.ScaleX = _scale.ScaleY = 1;
            _translation.X = 0;
            _translation.Y = 0;

            LoadImage();
        }


        private void UpdateMetadata()
        {
            if (_photo == null) return;
            if (_photo.Meta == null)
            {
                try { _photo.Meta = NacleFoto.App.Services.PhotoIndexer.ReadExif(_photo); } catch { }
            }
            var m = _photo.Meta;
            if (m == null) { TxtDados.Text = _photo.SizeText; return; }
            var parts = new List<string>();
            if (m.TakenUtc.HasValue) parts.Add(m.TakenUtc.Value.ToString("dd/MM/yyyy HH:mm:ss"));
            if (!string.IsNullOrEmpty(m.Camera)) parts.Add(m.Camera);
            var tech = m.ShortBadge;
            if (!string.IsNullOrEmpty(tech)) parts.Add(tech);
            parts.Add(_photo.SizeText);
            TxtDados.Text = string.Join("   ·   ", parts.ToArray());
        }


        private void LoadImage()
        {
            if (_photo == null) return;
            _loading = true;

            // resolves to screen size, with headroom for zoom
            double screenWidth = Math.Max(800, SystemParameters.PrimaryScreenWidth);
            int maxSide = (int)Math.Min(4000, screenWidth * 1.5);
            var bmp = WicLoader.LoadScaled(_photo.FullPath, maxSide);

            if (bmp == null) { _loading = false; return; }

            var image = new Image
            {
                Source = bmp,
                Width = bmp.PixelWidth,
                Height = bmp.PixelHeight,
                Stretch = Stretch.Fill,
                RenderTransform = new TransformGroup { Children = { _scale, _translation } },
                SnapsToDevicePixels = false
            };
            Cnv.Children.Clear();
            Cnv.Children.Add(image);
            FitToScreen();
            _loading = false;
        }

        private void FitToScreen()
        {
            if (Cnv.Children.Count == 0) return;
            var img = Cnv.Children[0] as Image;
            if (img == null || img.Width <= 0 || img.Height <= 0) return;

            double winW = Cnv.ActualWidth > 0 ? Cnv.ActualWidth : (ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth);
            double winH = Cnv.ActualHeight > 0 ? Cnv.ActualHeight : (ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight);

            double usableWidth = Math.Max(200, winW - 40);
            double usableHeight = Math.Max(200, winH - 120);

            double scale = Math.Min(usableWidth / img.Width, usableHeight / img.Height);
            if (scale > 1.0) scale = 1.0;
            if (scale <= 0.001) scale = 0.1;

            _scale.ScaleX = _scale.ScaleY = scale;
            Center();
        }

        private void Center()
        {
            if (Cnv.Children.Count == 0) return;
            var img = Cnv.Children[0] as Image;
            if (img == null) return;

            double winW = Cnv.ActualWidth > 0 ? Cnv.ActualWidth : (ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth);
            double winH = Cnv.ActualHeight > 0 ? Cnv.ActualHeight : (ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight);

            double w = img.Width * _scale.ScaleX;
            double h = img.Height * _scale.ScaleY;

            _translation.X = (winW - w) / 2.0;
            _translation.Y = Math.Max(10, (winH - h) / 2.0 - 25);
        }


        private void Cnv_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_loading || Cnv.Children.Count == 0) return;
            double factor = e.Delta > 0 ? 1.18 : 1 / 1.18;
            double currentScale = _scale.ScaleX;
            double newScale = Math.Max(0.05, Math.Min(20, currentScale * factor));
            double ratio = newScale / currentScale;

            var pos = e.GetPosition(Cnv);
            _scale.ScaleX = _scale.ScaleY = newScale;
            _translation.X = pos.X - (pos.X - _translation.X) * ratio;
            _translation.Y = pos.Y - (pos.Y - _translation.Y) * ratio;
            e.Handled = true;
        }

        private void Cnv_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            _startPoint = e.GetPosition(Cnv);
            _startX = _translation.X;
            _startY = _translation.Y;
            Cnv.CaptureMouse();
        }

        private void Cnv_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var pos = e.GetPosition(Cnv);
            _translation.X = _startX + (pos.X - _startPoint.X);
            _translation.Y = _startY + (pos.Y - _startPoint.Y);
        }

        private void Cnv_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            Cnv.ReleaseMouseCapture();
        }

        private void ZoomWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
                case Key.Left:
                    if (NavigationRequested != null) NavigationRequested(-1);
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (NavigationRequested != null) NavigationRequested(1);
                    e.Handled = true;
                    break;
                case Key.X:
                case Key.Space:
                    if (_photo != null && Marked != null) Marked(_photo);
                    UpdateMarkedState();
                    e.Handled = true;
                    break;
                case Key.F:
                    FitToScreen();
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Cnv.Children.Clear();
            _cache.Clear();
            _photos = null;
            _photo = null;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
