using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Shell;

namespace NacleFoto.App
{
    public partial class App : Application
    {
        private static string _appDataDir;
        private static AppSettings _settings;

        public static string AppDataDir
        {
            get
            {
                if (_appDataDir == null) _appDataDir = ProjectStore.GetAppDataDir();
                return _appDataDir;
            }
        }

        public static AppSettings Settings
        {
            get
            {
                if (_settings == null) _settings = AppSettings.Load(AppDataDir);
                return _settings;
            }
            set { _settings = value; }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var _ = AppDataDir;
            var __ = Settings;

            // application language
            Strings.Language = Settings.AppLanguage;

            // retention policy: old selections are pruned from the list
            try { ProjectStore.ApplyRetention(Settings.RetentionDays, AppDataDir); }
            catch (Exception) { }

            // start WAHA (WhatsApp server) and check if update is needed
            try { StartWaha(); }
            catch (Exception) { }

            DispatcherUnhandledException += OnError;

            try
            {
                var exe = Assembly.GetEntryAssembly().Location;
                if (!string.IsNullOrEmpty(exe)) ShellIntegration.Register(exe);
            }
            catch (Exception) { }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            if (e.Args != null && e.Args.Length > 0)
                mainWindow.OpenFolderFromCommandLine(e.Args[0]);
        }

        /// <summary>
        /// Stops the WhatsApp server when the app closes.
        ///
        /// The server was started by the app (or by the manage window), so it
        /// should not stay behind holding memory and the port. A remote WAHA
        /// is not ours to stop.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try { StopWaha(); }
            catch (Exception) { }
            base.OnExit(e);
        }

        private void StopWaha()
        {
            var url = Settings.WahaUrl ?? "";
            if (url.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) < 0 &&
                url.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) < 0) return;

            int port = 3000;
            try { port = new Uri(url).Port; } catch { }
            new NacleFoto.Core.Linking.WahaManager().StopServer(port);
        }

        /// <summary>
        /// Starts WAHA server in the background (if user configured it)
        /// and checks for new versions. QR code only needs to be scanned once.
        /// </summary>
        private void StartWaha()
        {
            var manager = new WahaManager();
            if (!manager.IsWahaInstalled()) return;

            var client = new WahaClient(
                Settings.WahaUrl, Settings.WahaApiKey, Settings.WahaSession);

            if (Settings.WahaAutoStart && !client.IsRunning())
            {
                // a server already exists on the port (with another key?): don't start another
                int port = 3000;
                try { port = new Uri(Settings.WahaUrl).Port; } catch { }
                if (WahaManager.ProcessOnPort(port) > 0) return;

                if (string.IsNullOrEmpty(Settings.WahaApiKey))
                {
                    Settings.WahaApiKey = GenerateApiKey();
                    Settings.Save(AppDataDir);
                }
                try { manager.StartServer(Settings.WahaApiKey, port); } catch (Exception) { }
            }

            // check for updates in the background (does not freeze the app)
            Task.Run(() =>
            {
                try
                {
                    // ensures session exists, store is enabled (pairing
                    // must survive server restarts) and chat store
                    // is synchronized
                    client.EnsureStore();
                    client.EnsureSession();

                    string localVer, remoteVer;
                    if (!manager.IsUpdateAvailable(out localVer, out remoteVer)) return;
                    Dispatcher.Invoke(() =>
                    {
                        var res = MessageBox.Show(
                            "Ha uma versao nova do WAHA (o servidor do WhatsApp).\n\nAtualizar agora?",
                            Strings.T("app.name"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (res != MessageBoxResult.Yes) return;
                        var managerWindow = new Views.WahaManagerWindow();
                        managerWindow.Show();
                    });
                }
                catch (Exception) { }
            });
        }

        private static string GenerateApiKey()
        {
            var bytes = new byte[24];
            new Random().NextBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private void OnError(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    Strings.T("msg.unexpectedError", e.Exception.Message),
                    Strings.T("app.name"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception) { }
            e.Handled = true;
        }
    }
}
