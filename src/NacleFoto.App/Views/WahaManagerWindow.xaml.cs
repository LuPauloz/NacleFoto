using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Localization;

namespace NacleFoto.App.Views
{
    /// <summary>
    /// Manages WAHA on the machine: installs Node, installs/updates WAHA, and starts
    /// the server on localhost. All with one button, no manual commands.
    /// </summary>
    public partial class WahaManagerWindow : Window
    {
        private readonly WahaManager _manager = new WahaManager();
        private bool _busy;

        public WahaManagerWindow()
        {
            InitializeComponent();
            Title = Strings.T("waha.title");
            UpdateStatus();
        }

        private void Log(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Dispatcher.Invoke(() =>
            {
                CaixaLog.AppendText(text.TrimEnd() + Environment.NewLine);
                CaixaLog.ScrollToEnd();
            });
        }

        private void UpdateStatus()
        {
            string nodeVersion;
            bool node = WahaManager.IsNodeInstalled(out nodeVersion);
            TxtNode.Text = (node ? "✓ " : "✗ ") + Strings.T("waha.nodeStatus") + ": " + (node ? nodeVersion : Strings.T("waha.missing"));

            bool installed = _manager.IsWahaInstalled();
            TxtWaha.Text = (installed ? "✓ " : "✗ ") + Strings.T("waha.wahaStatus") + ": " +
                           (installed ? (Strings.T("waha.ok")) : Strings.T("waha.missing"));

            var client = GetCurrentWahaClient();
            bool running = client != null && client.IsRunning();
            bool responds = client != null && client.IsServerUp();
            TxtServidor.Text = (running ? "✓ " : responds ? "! " : "✗ ") + Strings.T("waha.serverStatus") + ": " +
                                (running ? Strings.T("waha.ok")
                                      : responds ? Strings.T("waha.wrongKey")
                                                 : Strings.T("waha.stopped"));

            BtnNode.IsEnabled = !node && !_busy;
            BtnInstall.IsEnabled = node && !installed && !_busy;
            BtnUpdate.IsEnabled = node && installed && !_busy;
            BtnStart.IsEnabled = node && _manager.IsBuilt() && !running && !_busy;
            BtnStop.IsEnabled = running && !_busy;
        }


        private WahaClient GetCurrentWahaClient()
        {
            var s = App.Settings;
            if (string.IsNullOrWhiteSpace(s.WahaUrl)) return null;
            return new WahaClient(s.WahaUrl, s.WahaApiKey, s.WahaSession);
        }

        private WahaClient WahaClientAtual() => GetCurrentWahaClient();

        private async void BtnNode_Click(object sender, RoutedEventArgs e)
        {
            _busy = true; UpdateStatus();
            Log(Strings.T("waha.installingNode"));
            var r = await Task.Run(() => WahaManager.InstallNode(Log));
            Log(r.Output);
            _busy = false; UpdateStatus();
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            _busy = true; UpdateStatus();
            Log(Strings.T("waha.installing"));
            var r = await Task.Run(() => _manager.Install(Log));
            if (r.Ok)
            {
                Log(Strings.T("waha.installed"));
            }
            else
            {
                Log(Strings.T("waha.installFailed"));
                Log(GetLastLines(r.Output, 12));
            }
            _busy = false; UpdateStatus();
        }

        /// <summary>Last lines of the log (npm is too verbose to read everything).</summary>
        private static string GetLastLines(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var lines = text.Replace("\r", "").Split('\n');
            var end = Math.Max(0, lines.Length - count);
            return string.Join(Environment.NewLine, lines, end, lines.Length - end);
        }


        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            _busy = true; UpdateStatus();
            Log(Strings.T("waha.updating"));
            var r = await Task.Run(() => _manager.Update(Log));
            Log(r.Output);
            _busy = false; UpdateStatus();
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var s = App.Settings;
            if (string.IsNullOrEmpty(s.WahaApiKey))
            {
                s.WahaApiKey = GenerateKey();
                s.Save(App.AppDataDir);
            }

            _busy = true; UpdateStatus();
            Log(Strings.T("waha.starting"));

            var port = GetPort(s.WahaUrl);
            var r = await Task.Run(() => _manager.StartServer(s.WahaApiKey, port));
            Log(r.Output);

            if (r.Ok)
            {
                // wait for server to respond (without freezing the UI)
                bool running = false;
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(1000);
                    var client = GetCurrentWahaClient();
                    if (client != null && client.IsRunning()) { running = true; break; }
                }

                if (!running)
                {
                    Log(Strings.T("waha.serverFailed"));
                    var log = await Task.Run(() => _manager.GetLastLogLines(12));
                    if (!string.IsNullOrEmpty(log)) Log(log);
                }
            }

            _busy = false; UpdateStatus();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
            UpdateStatus();
        }

        private static int GetPort(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Port > 0 ? uri.Port : WahaManager.DefaultPort;
            }
            catch { return WahaManager.DefaultPort; }
        }

        private static int PortaDe(string url) => GetPort(url);

        private static string GenerateKey()
        {
            var bytes = new byte[24];
            new Random().NextBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }


        /// <summary>Stops the WAHA server (it runs detached, outside the app).</summary>
        public void StopServer()
        {
            try
            {
                var url = App.Settings.WahaUrl;
                _manager.StopServer(GetPort(url));
            }
            catch { }
        }


        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
