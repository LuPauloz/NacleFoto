using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NacleFoto.App;
using NacleFoto.App.Services;
using NacleFoto.App.ViewModels;
using NacleFoto.App.Views;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Shell;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// BUTTON ACTION tests. Each test actually clicks the button and verifies
    /// the effect. This is how we found bugs where the app closed when picking
    /// a folder and where the selection screen became invisible.
    /// </summary>
    [Collection("ui")]
    public class ButtonTests : IDisposable, IClassFixture<UiFixture>
    {
        private readonly string _dir;

        public ButtonTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-btn-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        // ---------------- fakes ----------------

        private sealed class FakePicker : IFolderPicker
        {
            public string Result;
            public int Calls;
            public string Pick(string title, string initialFolder)
            {
                Calls++;
                return Result;
            }
        }

        private sealed class FakeDialog : IUserDialog
        {
            public bool Answer = true;
            public List<string> Messages = new List<string>();
            public bool Confirm(string title, string message) { Messages.Add(message); return Answer; }
            public void Info(string title, string message) { Messages.Add(message); }
            public void Error(string title, string message) { Messages.Add(message); }
        }

        private sealed class FakeLauncher : IShellLauncher
        {
            public List<string> Urls = new List<string>();
            public List<string> Folders = new List<string>();
            public void OpenUrl(string url) { Urls.Add(url); }
            public void OpenFolder(string path) { Folders.Add(path); }
        }

        // ---------------- utilities ----------------

        private string CriarPastaComFotos(int quantidade)
        {
            var pasta = Path.Combine(_dir, "fotos-" + Guid.NewGuid().ToString("N").Substring(0, 4));
            Directory.CreateDirectory(pasta);
            for (int i = 0; i < quantidade; i++)
                File.WriteAllBytes(Path.Combine(pasta, "DSC_" + (1000 + i) + ".jpg"), Jpeg1x1());
            return pasta;
        }

        private static byte[] Jpeg1x1()
        {
            return Convert.FromBase64String(
                "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0a" +
                "HBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAFAABAAAAAAAA" +
                "AAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==");
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

        /// <summary>Opens the weddings screen with a fake folder picker.</summary>
        private static MainWindow AbrirApp(string pasta, FakeDialog dialog)
        {
            var main = new MainWindow();
            main.Projects.FolderPicker = new FakePicker { Result = pasta };
            main.Projects.Dialog = dialog;
            main.Selection.Dialog = dialog;
            return main;
        }

        private static void Descartar(MainWindow main)
        {
            if (main == null) return;
            var selecao = main.Selection;
            if (selecao != null)
            {
                var d = selecao as IDisposable;
                if (d != null) d.Dispose();
            }
        }

        // ---------------- tests ----------------

        [Fact]
        public void New_photo_selection_button_creates_wedding_and_opens_marking()
        {
            var pasta = CriarPastaComFotos(5);
            var picker = new FakePicker { Result = pasta };

            Ui.Run(() =>
            {
                var main = new MainWindow();
                try
                {
                    main.Projects.FolderPicker = picker;
                    main.Projects.Dialog = new FakeDialog();
                    main.Selection.Dialog = new FakeDialog();

                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));
                    Ui.WaitUntil(() => main.Selection.PhotoCount > 0);

                    Assert.Equal(Visibility.Visible, main.Selection.Visibility);
                    Assert.Equal(5, main.Selection.PhotoCount);
                    Assert.Equal(1, picker.Calls);
                }
                finally { Descartar(main); }
            });
        }

        [Fact]
        public void Canceling_folder_selection_does_not_break_anything()
        {
            // reported bug scenario: app closed when choosing folder
            var picker = new FakePicker { Result = null };

            Ui.Run(() =>
            {
                var main = new MainWindow();
                try
                {
                    main.Projects.FolderPicker = picker;
                    main.Projects.Dialog = new FakeDialog();

                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));

                    Assert.Equal(Visibility.Visible, main.Projects.Visibility);
                    Assert.Equal(0, main.Selection.PhotoCount);
                }
                finally { Descartar(main); }
            });
        }

        [Fact]
        public void Mark_all_button_selects_all_photos()
        {
            var pasta = CriarPastaComFotos(4);

            Ui.Run(() =>
            {
                var main = AbrirApp(pasta, new FakeDialog());
                try
                {
                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));
                    Ui.WaitUntil(() => main.Selection.PhotoCount > 0);
                    Clicar(Find<Button>(main.Selection, "BtnMarkAll"));

                    Assert.Equal(4, main.Selection.SelectedCount);
                }
                finally { Descartar(main); }
            });
        }

        [Fact]
        public void Undo_button_undoes_the_marking()
        {
            var pasta = CriarPastaComFotos(3);

            Ui.Run(() =>
            {
                var main = AbrirApp(pasta, new FakeDialog());
                try
                {
                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));
                    Ui.WaitUntil(() => main.Selection.PhotoCount > 0);
                    Clicar(Find<Button>(main.Selection, "BtnMarkAll"));
                    Assert.Equal(3, main.Selection.SelectedCount);

                    // mark all is ONE undo step
                    Clicar(Find<Button>(main.Selection, "BtnUndo"));
                    Assert.Equal(0, main.Selection.SelectedCount);
                }
                finally { Descartar(main); }
            });
        }

        [Fact]
        public void Clear_button_unmarks_all()
        {
            var pasta = CriarPastaComFotos(3);

            Ui.Run(() =>
            {
                var main = AbrirApp(pasta, new FakeDialog());
                try
                {
                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));
                    Ui.WaitUntil(() => main.Selection.PhotoCount > 0);
                    Clicar(Find<Button>(main.Selection, "BtnMarkAll"));
                    Clicar(Find<Button>(main.Selection, "BtnClear"));

                    Assert.Equal(0, main.Selection.SelectedCount);
                }
                finally { Descartar(main); }
            });
        }

        [Fact]
        public void Second_marking_from_same_folder_gets_number()
        {
            // they don't separate folders per wedding: one folder per week, multiple
            // weddings inside. The second selection from the same folder gets "(2)".
            var pasta = CriarPastaComFotos(2);
            var nomePasta = System.IO.Path.GetFileName(pasta);

            Ui.Run(() =>
            {
                var main = AbrirApp(pasta, new FakeDialog());
                try
                {
                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));
                    Ui.WaitUntil(() => main.Selection.PhotoCount > 0);
                    Assert.Equal(nomePasta, main.Selection.CurrentWedding.Name);

                    // returns to the list and starts another selection in the SAME folder
                    Clicar(Find<Button>(main.Selection, "BtnBack"));
                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));
                    Ui.WaitUntil(() => main.Selection.CurrentWedding != null &&
                                       main.Selection.CurrentWedding.Name != nomePasta);

                    Assert.Equal(nomePasta + " (2)", main.Selection.CurrentWedding.Name);
                }
                finally { Descartar(main); }
            });
        }

        [Fact]
        public void Close_button_freezes_selection_and_asks_what_to_do()
        {
            var pasta = CriarPastaComFotos(2);
            var dialog = new FakeDialog { Answer = true };
            AfterCloseAction? escolhido = null;

            Ui.Run(() =>
            {
                var main = AbrirApp(pasta, dialog);
                try
                {
                    main.Selection.AfterCloseHandler = resumo =>
                    {
                        escolhido = AfterCloseAction.NewSelection;
                        return AfterCloseAction.NewSelection;
                    };

                    Clicar(Find<Button>(main.Projects, "BtnNewSelection"));
                    Ui.WaitUntil(() => main.Selection.PhotoCount > 0);
                    Clicar(Find<Button>(main.Selection, "BtnMarkAll"));

                    var wedding = main.Selection.CurrentWedding;
                    Clicar(Find<Button>(main.Selection, "BtnClose"));

                    // selection was frozen
                    Assert.Equal(WeddingState.Closed, wedding.State);
                    // and the user was asked what to do
                    Assert.Equal(AfterCloseAction.NewSelection, escolhido);
                }
                finally { Descartar(main); }
            });
        }

        [Fact]
        public void Open_whatsapp_button_builds_link_with_phone()
        {
            var launcher = new FakeLauncher();
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());
            NacleFoto.App.App.Settings.WhatsAppPhone = "5511999999999";

            Ui.Run(() =>
            {
                try
                {
                    var janela = new SendWindow();
                    janela.Launcher = launcher;
                    janela.Dialog = new FakeDialog();
                    janela.Open(new Wedding { Id = "x", Name = "Casamento Teste" });

                    Clicar(Find<Button>(janela, "BtnWhatsapp"));

                    Assert.Single(launcher.Urls);
                    // opens WhatsApp Web directly (wa.me would prompt to open
                    // the desktop app every time)
                    Assert.StartsWith("https://web.whatsapp.com/send?phone=5511999999999", launcher.Urls[0]);
                    // no message: only photos
                    Assert.DoesNotContain("&text=", launcher.Urls[0]);
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }

        [Fact]
        public void Generate_link_button_without_configured_provider_warns_without_breaking()
        {
            var launcher = new FakeLauncher();
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());
            NacleFoto.App.App.Settings.LinkProvider = "none";

            Ui.Run(() =>
            {
                try
                {
                    var janela = new SendWindow();
                    janela.Launcher = launcher;
                    janela.Dialog = new FakeDialog();
                    janela.Open(new Wedding { Id = "x", Name = "Casamento Teste" });

                    Clicar(Find<Button>(janela, "BtnLink"));

                    Assert.Empty(launcher.Urls);
                    Assert.False(string.IsNullOrWhiteSpace(janela.StatusText), "esperava um aviso na tela");
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }

        [Fact]
        public void Sending_two_weddings_on_the_same_day()
        {
            // real-world scenario: two civil weddings on Friday. Mark the first,
            // close, mark the second, close, and finally choose which one to send.
            var pasta = CriarPastaComFotos(3);
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());

            Ui.Run(() =>
            {
                try
                {
                    var a = new Wedding { Id = "a", Name = "Casamento A", RootFolder = pasta, State = WeddingState.Closed };
                    a.Selection.Add(new SelectionItem { Path = "DSC_1000.jpg", Size = 100, ModifiedUtcTicks = 1 });
                    var b = new Wedding { Id = "b", Name = "Casamento B", RootFolder = pasta, State = WeddingState.Closed };
                    b.Selection.Add(new SelectionItem { Path = "DSC_1001.jpg", Size = 100, ModifiedUtcTicks = 1 });

                    var janela = new SendWindow();
                    janela.Dialog = new FakeDialog();
                    janela.Open(new List<Wedding> { a, b });

                    // both appear in the list
                    Assert.Equal(2, janela.WeddingCount);

                    // without a configured message, the app writes nothing (only photos)
                    Assert.Equal("", janela.BuildWhatsAppPreview());

                    // with a message, it includes the names of both weddings
                    NacleFoto.App.App.Settings.MessageTemplate = "Photos of {wedding}";
                    var mensagem = janela.BuildWhatsAppPreview();
                    Assert.Contains("Casamento A", mensagem);
                    Assert.Contains("Casamento B", mensagem);
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }

        [Fact]
        public void Zoom_has_button_to_mark_and_unmark()
        {
            var foto = new PhotoItemViewModel("a.jpg", "a.jpg", 100, 1);

            Ui.Run(() =>
            {
                var janela = new ZoomWindow();
                janela.Open(new List<PhotoItemViewModel> { foto }, 0);

                var botao = Find<Button>(janela, "BtnMarcar");
                Assert.NotNull(botao);
                Assert.False(string.IsNullOrWhiteSpace(botao.Content.ToString()));

                // the button fires the mark event
                bool marcou = false;
                janela.Marked += f => marcou = true;
                Clicar(botao);
                Assert.True(marcou, "o botao do zoom deveria marcar a foto");
            });
        }
    }
}
