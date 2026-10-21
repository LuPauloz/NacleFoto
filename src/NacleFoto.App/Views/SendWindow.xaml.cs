using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NacleFoto.App.Services;
using NacleFoto.App.ViewModels;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Models;
using NacleFoto.Core.Packaging;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Shell;

namespace NacleFoto.App.Views
{
    /// <summary>
    /// Send screen. The primary path is automatic sending via WhatsApp Web:
    /// photos are sent from original disk, in batches, without creating any folder.
    /// </summary>
    public partial class SendWindow : Window
    {
        private List<Wedding> _weddings = new List<Wedding>();
        private readonly List<SendWeddingVm> _items = new List<SendWeddingVm>();
        private bool _sending;

        /// <summary>Injectable for testing: does not actually open a browser or Explorer.</summary>
        public IShellLauncher Launcher { get; set; } = new ShellLauncher();

        /// <summary>Injectable for testing: dialog boxes become automated responses.</summary>
        public IUserDialog Dialog { get; set; } = new MessageBoxDialog();

        /// <summary>
        /// Injectable for testing: indicates if the target number already has a chat on
        /// WhatsApp (true = yes, false = no, null = unable to determine).
        /// </summary>
        public Func<string, bool?> NumberVerifier { get; set; }
        public Func<string, bool?> VerificadorNumero
        {
            get { return NumberVerifier; }
            set { NumberVerifier = value; }
        }

        /// <summary>Screen status text (public for tests).</summary>
        public string StatusText { get { return TxtStatus.Text; } }

        /// <summary>How many weddings are on the send screen (public for tests).</summary>
        public int WeddingCount { get { return _items.Count; } }

        /// <summary>Message that will be pasted into WhatsApp (public for tests).</summary>
        public string BuildWhatsAppPreview()
        {
            var parts = 0;
            return BuildWhatsAppMessage(GetSelectedWeddings(), parts, App.Settings.AppLanguage);
        }

        public SendWindow()
        {
            InitializeComponent();
        }

        public void Open(Wedding wedding)
        {
            Open(new List<Wedding> { wedding });
        }

        /// <summary>Opens send screen for one or multiple weddings.</summary>
        public void Open(IEnumerable<Wedding> weddings)
        {
            Title = Strings.T("send.title");
            _weddings = (weddings ?? Enumerable.Empty<Wedding>()).Where(w => w != null).ToList();
            _items.Clear();
            foreach (var w in _weddings)
                _items.Add(new SendWeddingVm { Wedding = w, Send = true });

            ListaCasamentos.ItemsSource = _items;
            TxtResumo.Text = GetGeneralSummary();
            TxtStatus.Text = "";
            BlocoLink.Visibility = Visibility.Collapsed;
            BtnSent.IsEnabled = _items.Any(i => i.Send && i.Wedding.State == WeddingState.Closed);
            UpdateDestination();
        }

        private string GetGeneralSummary()
        {
            var sel = _items.Where(i => i.Send).ToList();
            if (sel.Count == 0) return Strings.T("send.noneSelected");
            int photos = sel.Sum(i => i.Wedding.TotalSelected);
            long bytes = sel.Sum(i => i.Wedding.SelectedSize);
            if (sel.Count == 1) return Strings.T("send.summary", photos, PhotoItemViewModel.FormatSize(bytes));
            return sel.Count + " " + Strings.T("send.weddings") + "  ·  " +
                   Strings.T("send.summary", photos, PhotoItemViewModel.FormatSize(bytes));
        }

        private string ResumoGeral() => GetGeneralSummary();

        private List<Wedding> GetSelectedWeddings()
        {
            return _items.Where(i => i.Send).Select(i => i.Wedding).ToList();
        }


        /// <summary>
        /// WhatsApp message. Comes from settings (default is empty: photos only).
        /// </summary>
        private static string BuildWhatsAppMessage(IEnumerable<Wedding> weddings, int parts, AppLanguage lang)
        {
            var template = App.Settings.MessageTemplate;
            if (string.IsNullOrWhiteSpace(template)) return "";

            var list = weddings.ToList();
            if (list.Count == 0) return "";
            if (list.Count == 1) return SendPreparer.BuildWhatsAppMessage(list[0], parts, lang);

            var names = string.Join(", ", list.Select(w => w.Name));
            return template
                .Replace("{wedding}", names)
                .Replace("{parts}", parts > 1 ? "(" + parts + ")" : "")
                .Replace("{parts}", parts > 1 ? "(" + parts + ")" : "");
        }

        /// <summary>Marked files, from original disk, of selected weddings.</summary>
        private List<string> GetOriginalFiles()
        {
            var list = new List<string>();
            foreach (var w in GetSelectedWeddings())
                foreach (var item in WeddingFiles.BuildPackageItems(w))
                    list.Add(item.SourcePath);
            return list;
        }


        // ---------------------------------------------------------------- automatic sending (WAHA)

        private async void BtnAutoWhatsapp_Click(object sender, RoutedEventArgs e)
        {
            if (_sending) return;

            var settings = App.Settings;
            var waha = new WahaClient(settings.WahaUrl, settings.WahaApiKey, settings.WahaSession);
            if (!waha.IsConfigured)
            {
                ShowError(Strings.T("send.wahaNotConfigured"));
                return;
            }

            var chatId = WahaClient.BuildChatId(settings.WhatsAppPhone);
            if (string.IsNullOrEmpty(chatId))
            {
                ShowError(Strings.T("send.noPhone"));
                return;
            }

            var files = GetOriginalFiles();
            if (files.Count == 0)
            {
                ShowError(Strings.T("send.noSelection"));
                return;
            }

            // WhatsApp accepts the photo but does not deliver it if the number is wrong
            // (with no warning). Before sending, we check it.
            if (!ConfirmNumber(waha, chatId)) return;

            _sending = true;
            BtnAutoWhatsapp.IsEnabled = false;
            Barra.Visibility = Visibility.Visible;
            Barra.Value = 0;
            TxtStatus.Text = Strings.T("send.checkingWaha");

            try
            {
                var status = await Task.Run(() => waha.GetSessionStatus());
                if (status != "WORKING")
                {
                    // the server may have restarted: try to reconnect the session
                    await Task.Run(() => waha.EnsureSession());
                    status = await Task.Run(() => waha.GetSessionStatus());
                }
                if (status != "WORKING")
                {
                    ShowError(Strings.T("send.wahaNotReady", status ?? "?"));
                    return;
                }

                int i = 0;
                foreach (var file in files)
                {
                    await Task.Run(() => waha.SendFile(chatId, file));
                    i++;
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatus.Text = Strings.T("send.sending", i, files.Count);
                        Barra.Value = files.Count > 0 ? 100.0 * i / files.Count : 0;
                    });
                    // WhatsApp rate limits: a short pause between photos
                    if (i < files.Count) await Task.Delay(350);
                }

                TxtStatus.Text = Strings.T("send.sentTo", settings.WhatsAppPhone);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                _sending = false;
                BtnAutoWhatsapp.IsEnabled = true;
                Barra.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Checks the target number before sending.
        ///
        /// If the WhatsApp account has never chatted with this number, it is
        /// almost certainly wrong - and photos disappear without any warning.
        /// In that case the app asks for confirmation before sending.
        /// </summary>
        private bool ConfirmNumber(WahaClient waha, string chatId)
        {
            var settings = App.Settings;

            bool? known = null;
            try
            {
                var verifier = NumberVerifier;
                if (verifier != null)
                {
                    known = verifier(chatId);
                }
                else
                {
                    // read-only: if store is not yet enabled, WAHA does not
                    // know the chats and the answer is "unknown" (the app asks)
                    known = waha.KnowsNumber(chatId);
                }
            }
            catch (Exception) { }

            if (known == true) return true;

            var text = Strings.T("send.confirmNumber", settings.WhatsAppPhone);
            var r = Dialog.Confirm(Strings.T("app.name"), text);
            if (r) return true;

            // cancelled: open settings to fix the number
            ShowError(Strings.T("send.wrongNumber", settings.WhatsAppPhone));
            return false;
        }


        /// <summary>Opens settings to change the print lab number.</summary>
        private void BtnChangeNumber_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(App.Settings);
            window.Owner = this;
            window.ShowDialog();
            UpdateDestination();
        }

        private void UpdateDestination()
        {
            var phone = (App.Settings.WhatsAppPhone ?? "").Trim();
            TxtDestino.Text = phone.Length == 0 ? Strings.T("send.noNumberSet") : phone;
        }


        /// <summary>
        /// Opens the pairing screen (QR code). Kept in settings so parents
        /// do not accidentally stumble on this option.
        /// </summary>
        private void BtnWhatsappQr_Click(object sender, RoutedEventArgs e)
        {
            var settings = App.Settings;
            var waha = new WahaClient(settings.WahaUrl, settings.WahaApiKey, settings.WahaSession);
            if (!waha.IsConfigured)
            {
                ShowError(Strings.T("send.wahaNotConfigured"));
                return;
            }
            var window = new QrCodeWindow(waha);
            window.Owner = this;
            window.ShowDialog();
        }

        /// <summary>Displays the error visibly (reddish background).</summary>
        private void ShowError(string message)
        {
            TxtStatus.Text = Strings.T("send.errorTitle") + ": " + message;
            BlocoStatus.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4A, 0x1F, 0x1F));
            TxtStatus.Foreground = Brushes.White;
        }


        // ---------------------------------------------------------------- link

        private async void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            var settings = App.Settings;
            if (string.IsNullOrEmpty(settings.LinkProvider) || settings.LinkProvider == "none")
            {
                TxtStatus.Text = Strings.T("send.noLinkProvider");
                return;
            }

            var weddings = GetSelectedWeddings();
            var items = new List<PackageItem>();
            foreach (var w in weddings)
                items.AddRange(WeddingFiles.BuildPackageItems(w));

            if (items.Count == 0)
            {
                TxtStatus.Text = Strings.T("send.noSelection");
                return;
            }

            BtnLink.IsEnabled = false;
            Barra.Visibility = Visibility.Visible;
            Barra.Value = 0;
            TxtStatus.Text = Strings.T("send.makingLink");

            try
            {
                var result = await Task.Run(() => LinkBuilder.Build(
                    settings, weddings.FirstOrDefault(), items,
                    (done, total) => Dispatcher.Invoke(() => Barra.Value = total > 0 ? 100.0 * done / total : 0)));

                CampoLink.Text = result.Text;
                BlocoLink.Visibility = Visibility.Visible;
                ClipboardFiles.CopyText(result.Text);
                TxtStatus.Text = Strings.T("send.linkCopied");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = Strings.T("send.linkError", ex.Message);
            }
            finally
            {
                BtnLink.IsEnabled = true;
                Barra.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCopyLink_Click(object sender, RoutedEventArgs e)
        {
            ClipboardFiles.CopyText(CampoLink.Text);
            TxtStatus.Text = Strings.T("send.linkCopied");
        }

        // ---------------------------------------------------------------- open chat

        private void BtnWhatsapp_Click(object sender, RoutedEventArgs e)
        {
            var settings = App.Settings;
            var phone = SendPreparer.NormalizePhone(settings.WhatsAppPhone);
            if (string.IsNullOrEmpty(phone))
            {
                TxtStatus.Text = Strings.T("send.noPhone");
                return;
            }

            // opens chat directly in WhatsApp Web (wa.me link would ask whether you
            // want to open the desktop app, every time)
            var url = "https://web.whatsapp.com/send?phone=" + phone;
            var message = BuildWhatsAppMessage(GetSelectedWeddings(), 0, settings.AppLanguage);
            if (!string.IsNullOrEmpty(message))
                url += "&text=" + Uri.EscapeDataString(message);

            Launcher.OpenUrl(url);
        }

        // ---------------------------------------------------------------- finish

        private void BtnSent_Click(object sender, RoutedEventArgs e)
        {
            bool any = false;
            foreach (var w in GetSelectedWeddings())
            {
                w.State = WeddingState.Sent;
                w.SentAt = DateTime.Now;
                w.SendChannel = "whatsapp";
                ProjectStore.Save(w, App.AppDataDir);
                any = true;
            }
            if (any)
            {
                TxtStatus.Text = Strings.T("send.sentDone");
                BtnSent.IsEnabled = false;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private sealed class PartRow
        {
            public string FilePath { get; set; }
            public string Name { get; set; }
            public string Size { get; set; }
        }

        /// <summary>A wedding in the send list (with the option to include or exclude).</summary>
        private sealed class SendWeddingVm
        {
            public Wedding Wedding { get; set; }
            public bool Send { get; set; }
            public string Summary
            {
                get
                {
                    return Wedding.TotalSelected + " " + Strings.T("selection.markedPhotos") +
                           "  ·  " + PhotoItemViewModel.FormatSize(Wedding.SelectedSize);
                }
            }
        }
    }
}
