using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Localization;

namespace NacleFoto.App.Views
{
    /// <summary>
    /// Displays the WAHA QR code to pair WhatsApp. Scan with phone (one time only).
    /// </summary>
    public partial class QrCodeWindow : Window
    {
        private readonly WahaClient _waha;
        private readonly DispatcherTimer _timer;
        private bool _loading;
        private bool _serverRunning;

        public QrCodeWindow(WahaClient waha)
        {
            InitializeComponent();
            _waha = waha;
            Title = Strings.T("qr.title");
            RefreshQr();

            // WhatsApp only provides about 3 QR codes before the session drops:
            // auto-refreshes so the user never stares at an expired code
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _timer.Tick += (s, e) => RefreshQr();
            _timer.Start();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshQr();
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Refreshes the QR code. Runs off the UI thread: renewing the QR code
        /// can take a few seconds (WAHA restarts the session when it drops).
        /// </summary>
        private async void RefreshQr()
        {
            if (_loading) return;
            _loading = true;
            try
            {
                var status = await Task.Run(() => _waha.IsRunning() ? _waha.GetSessionStatus() : null);

                if (status == null && !_serverRunning)
                {
                    _serverRunning = await Task.Run(() => _waha.IsRunning());
                    if (!_serverRunning)
                    {
                        ShowStatus(Strings.T("qr.serverDown"), false);
                        return;
                    }
                }
                _serverRunning = true;

                if (status == "WORKING")
                {
                    ShowStatus(Strings.T("qr.ready"), false);
                    _timer.Stop(); // already paired: no need to refresh anymore
                    return;
                }

                var png = await Task.Run(() => _waha.GetQrCodePng());
                if (png != null && png.Length > 0)
                {
                    using (var ms = new MemoryStream(png))
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze();
                        ImgQr.Source = img;
                    }
                    TxtStatus.Text = Strings.T("qr.scan");
                }
                else
                {
                    ShowStatus(Strings.T("qr.unavailable", status ?? "?"), true);
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
            }
            finally
            {
                _loading = false;
            }
        }


        private void ShowStatus(string text, bool keepQr)
        {
            TxtStatus.Text = text;
            if (!keepQr) ImgQr.Source = null;
        }

    }
}
