using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NacleFoto.App;
using NacleFoto.App.Services;
using NacleFoto.App.Views;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// WhatsApp sending previously "succeeded" with a phone number that does not
    /// exist on WhatsApp: the app said "sent" and photos vanished. These
    /// tests lock down phone number verification before sending.
    /// </summary>
    [Collection("ui")]
    public class SendNumberCheckTests : IDisposable, IClassFixture<UiFixture>
    {
        private readonly string _dir;

        public SendNumberCheckTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-num-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private sealed class FakeDialog : IUserDialog
        {
            public bool Answer = true;
            public List<string> Messages = new List<string>();
            public bool Confirm(string title, string message) { Messages.Add(message); return Answer; }
            public void Info(string title, string message) { Messages.Add(message); }
            public void Error(string title, string message) { Messages.Add(message); }
        }

        private static T Find<T>(DependencyObject raiz, string nome) where T : FrameworkElement
        {
            var fe = raiz as FrameworkElement;
            if (fe != null)
            {
                var achado = fe.FindName(nome) as T;
                if (achado != null) return achado;
            }

            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(raiz);
            for (int i = 0; i < n; i++)
            {
                var c = System.Windows.Media.VisualTreeHelper.GetChild(raiz, i);
                var el = c as FrameworkElement;
                if (el != null && el.Name == nome && c is T) return (T)c;
                var r = Find<T>(c, nome);
                if (r != null) return r;
            }
            return null;
        }

        private static void Clicar(Button botao)
        {
            Assert.NotNull(botao);
            botao.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private static string PastaComFoto()
        {
            var pasta = Path.Combine(Path.GetTempPath(), "nacle-fotos-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pasta);
            File.WriteAllBytes(Path.Combine(pasta, "DSC_0001.jpg"), new byte[] { 1, 2, 3, 4 });
            return pasta;
        }

        private static Wedding CasamentoComFoto(string pasta)
        {
            var w = new Wedding { Id = "w1", Name = "Casamento", RootFolder = pasta, State = WeddingState.Closed };
            w.Selection.Add(new SelectionItem { Path = "DSC_0001.jpg", Size = 4, ModifiedUtcTicks = 1 });
            return w;
        }

        [Fact]
        public void Send_screen_shows_where_photos_go()
        {
            // user needs to see the print lab number when sending
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());
            NacleFoto.App.App.Settings.WhatsAppPhone = "5538991234567";

            Ui.Run(() =>
            {
                try
                {
                    var janela = new SendWindow();
                    janela.Dialog = new FakeDialog();
                    janela.Open(CasamentoComFoto(PastaComFoto()));

                    var texto = (TextBlock)janela.FindName("TxtDestino");
                    Assert.NotNull(texto);
                    Assert.Equal("5538991234567", texto.Text);
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }

        [Fact]
        public void Number_unknown_to_account_requests_confirmation_before_sending()
        {
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());
            NacleFoto.App.App.Settings.WhatsAppPhone = "5538991234567";

            var dialog = new FakeDialog { Answer = false };

            Ui.Run(() =>
            {
                try
                {
                    var janela = new SendWindow();
                    janela.Dialog = dialog;
                    // WhatsApp has never chatted with this number
                    janela.VerificadorNumero = chatId => false;
                    janela.Open(CasamentoComFoto(PastaComFoto()));

                    Clicar(Find<Button>(janela, "BtnAutoWhatsapp"));

                    // prompted for confirmation before sending
                    Assert.NotEmpty(dialog.Messages);
                    Assert.Contains("5538991234567", dialog.Messages[0]);
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }

        [Fact]
        public void Number_known_to_account_does_not_prompt()
        {
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());
            NacleFoto.App.App.Settings.WhatsAppPhone = "5538991234567";

            var dialog = new FakeDialog();

            Ui.Run(() =>
            {
                try
                {
                    var janela = new SendWindow();
                    janela.Dialog = dialog;
                    janela.VerificadorNumero = chatId => true;
                    janela.Open(CasamentoComFoto(PastaComFoto()));

                    Clicar(Find<Button>(janela, "BtnAutoWhatsapp"));

                    // did not prompt (any failure now is from the server, not the number)
                    Assert.Empty(dialog.Messages);
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }
    }
}
