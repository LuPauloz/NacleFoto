using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using NacleFoto.App.Services;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Shell;

namespace NacleFoto.App.Views
{
    /// <summary>Settings: language, print lab phone, send format and link.</summary>
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        /// <summary>Injectable for testing.</summary>
        public IFolderPicker FolderPicker { get; set; } = new FolderBrowserPicker();

        /// <summary>Injectable for testing: dialog boxes become automated responses.</summary>
        public IUserDialog Dialog { get; set; } = new MessageBoxDialog();

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings ?? AppSettings.CreateDefault(App.AppDataDir);
            LoadSettings();
        }

        private void LoadSettings()
        {
            ComboLanguage.Items.Clear();
            ComboLanguage.Items.Add(new LanguageItem("auto", Strings.T("settings.languageAuto")));
            ComboLanguage.Items.Add(new LanguageItem("pt", Strings.T("settings.languagePt")));
            ComboLanguage.Items.Add(new LanguageItem("en", Strings.T("settings.languageEn")));

            var atual = string.IsNullOrEmpty(_settings.Language) ? "auto" : _settings.Language;
            foreach (var item in ComboLanguage.Items)
            {
                if (((LanguageItem)item).Value == atual)
                {
                    ComboLanguage.SelectedItem = item;
                    break;
                }
            }
            if (ComboLanguage.SelectedItem == null) ComboLanguage.SelectedIndex = 0;
            ComboLanguage.SelectionChanged += (s, e) => ApplyLanguage();

            CampoPhone.Text = _settings.WhatsAppPhone;
            CampoPartSize.Text = _settings.PartSizeMb.ToString(CultureInfo.InvariantCulture);
            CampoDestino.Text = _settings.DestinationFolder;

            switch (_settings.Format)
            {
                case SendFormat.Zip: RadioZip.IsChecked = true; break;
                case SendFormat.Both: RadioBoth.IsChecked = true; break;
                default: RadioLoose.IsChecked = true; break;
            }

            CheckUseLink.IsChecked = _settings.UseDownloadLink;
            CheckNuvem.IsChecked = _settings.IncludeCloudFiles;
            CampoRetencao.Text = _settings.RetentionDays.ToString(CultureInfo.InvariantCulture);
            CampoLote.Text = _settings.WhatsAppBatchSize.ToString(CultureInfo.InvariantCulture);
            CampoMensagem.Text = _settings.MessageTemplate ?? "";
            CampoWahaUrl.Text = _settings.WahaUrl ?? "";
            CampoWahaKey.Text = _settings.WahaApiKey ?? "";
            CampoWahaSession.Text = _settings.WahaSession ?? "default";
            CheckWahaAuto.IsChecked = _settings.WahaAutoStart;
            CampoWahaComando.Text = _settings.WahaStartCommand ?? "";

            CampoSyncFolder.Text = _settings.SyncFolder;

            switch (_settings.LinkProvider)
            {
                case "folder": RadioFolder.IsChecked = true; break;
                case "sendit": RadioSendit.IsChecked = true; break;
                default: RadioNone.IsChecked = true; break;
            }
            UpdateProviderPanel();
        }


        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            UpdateProviderPanel();
        }

        private void UpdateProviderPanel()
        {
            if (PainelSendit == null) return;
            bool sendit = RadioSendit != null && RadioSendit.IsChecked == true;
            PainelSendit.Visibility = sendit ? Visibility.Visible : Visibility.Collapsed;
            PainelFolder.Visibility = (RadioFolder != null && RadioFolder.IsChecked == true)
                ? Visibility.Visible : Visibility.Collapsed;
        }


        private void ApplyLanguage()
        {
            var item = ComboLanguage.SelectedItem as LanguageItem;
            if (item == null) return;
            Strings.Language = LanguageOf(item.Value);
            Title = Strings.T("settings.title");
        }

        /// <summary>Turns "auto" / "pt" / "en" into the language actually used.</summary>
        private static AppLanguage LanguageOf(string value)
        {
            if (value == "en") return AppLanguage.English;
            if (value == "pt") return AppLanguage.Portuguese;
            return Strings.DetectSystemLanguage();
        }


        private void BtnChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.Pick(Strings.T("settings.destFolder"), CampoDestino.Text);
            if (!string.IsNullOrEmpty(folder)) CampoDestino.Text = folder;
        }

        private void BtnEscolherSync_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.Pick(Strings.T("settings.syncFolder"), CampoSyncFolder.Text);
            if (!string.IsNullOrEmpty(folder)) CampoSyncFolder.Text = folder;
        }

        private void BtnGerenciarWaha_Click(object sender, RoutedEventArgs e)
        {
            var window = new WahaManagerWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        /// <summary>Opens WhatsApp QR code (pairing).</summary>
        private void BtnQrWaha_Click(object sender, RoutedEventArgs e)
        {
            var settings = App.Settings;
            if (string.IsNullOrWhiteSpace(settings.WahaUrl))
            {
                MessageBox.Show(Strings.T("send.wahaNotConfigured"), Strings.T("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var waha = new NacleFoto.Core.Linking.WahaClient(settings.WahaUrl, settings.WahaApiKey, settings.WahaSession);
            var window = new QrCodeWindow(waha);
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            var item = ComboLanguage.SelectedItem as LanguageItem;
            if (item != null) _settings.Language = item.Value;

            _settings.WhatsAppPhone = (CampoPhone.Text ?? "").Trim();

            int partSize;
            if (int.TryParse((CampoPartSize.Text ?? "").Trim(), out partSize) && partSize >= 5 && partSize <= 100)
                _settings.PartSizeMb = partSize;

            _settings.DestinationFolder = (CampoDestino.Text ?? "").Trim();

            _settings.Format = RadioZip.IsChecked == true ? SendFormat.Zip
                            : RadioBoth.IsChecked == true ? SendFormat.Both
                            : SendFormat.Loose;

            int retention;
            if (int.TryParse((CampoRetencao.Text ?? "").Trim(), out retention) && retention >= 0)
                _settings.RetentionDays = retention;

            int batch;
            if (int.TryParse((CampoLote.Text ?? "").Trim(), out batch) && batch >= 1 && batch <= 200)
                _settings.WhatsAppBatchSize = batch;

            _settings.UseDownloadLink = CheckUseLink.IsChecked == true;
            _settings.IncludeCloudFiles = CheckNuvem.IsChecked == true;
            _settings.MessageTemplate = (CampoMensagem.Text ?? "").Trim();
            _settings.WahaUrl = (CampoWahaUrl.Text ?? "").Trim();
            _settings.WahaApiKey = (CampoWahaKey.Text ?? "").Trim();
            _settings.WahaSession = (CampoWahaSession.Text ?? "").Trim();
            if (string.IsNullOrEmpty(_settings.WahaSession)) _settings.WahaSession = "default";
            _settings.WahaAutoStart = CheckWahaAuto.IsChecked == true;
            _settings.WahaStartCommand = (CampoWahaComando.Text ?? "").Trim();
            _settings.LinkProvider = RadioFolder.IsChecked == true ? "folder"
                                  : RadioSendit.IsChecked == true ? "sendit"
                                  : "none";

            _settings.SyncFolder = (CampoSyncFolder.Text ?? "").Trim();

            _settings.Save(App.AppDataDir);
            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private sealed class LanguageItem
        {
            public LanguageItem(string value, string name)
            {
                Value = value;
                Name = name;
            }
            /// <summary>"auto" (follow the system), "pt" or "en".</summary>
            public string Value { get; private set; }
            public string Name { get; private set; }
            public override string ToString() { return Name; }
        }
    }
}
