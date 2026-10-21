using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using NacleFoto.App;
using NacleFoto.App.Services;
using NacleFoto.App.ViewModels;
using NacleFoto.App.Views;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// Opening a photo in full screen: the folder stays behind it, Esc goes back
    /// to the grid, and the window does not get stuck on top of everything.
    /// </summary>
    public class FullScreenPhotoTests : IDisposable, IClassFixture<UiFixture>
    {
        private readonly string _dir;

        public FullScreenPhotoTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-full-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private sealed class FakeDialog : IUserDialog
        {
            public bool Answer = true;
            public bool Confirm(string title, string message) { return Answer; }
            public void Info(string title, string message) { }
            public void Error(string title, string message) { }
        }

        private string CriarFoto(string nome)
        {
            var caminho = Path.Combine(_dir, nome);
            File.WriteAllBytes(caminho, new byte[] { 1, 2, 3, 4 });
            return caminho;
        }

        [Fact]
        public void A_janela_de_zoom_nao_fica_por_cima_de_tudo()
        {
            // com Topmost a foto abafava as outras janelas: nem o Alt+Tab funcionava
            Ui.Run(() =>
            {
                var window = new ZoomWindow();
                Assert.False(window.Topmost, "a tela cheia nao pode ser Topmost (nao sai da frente)");
            });
        }

        [Fact]
        public void Abrir_o_app_com_uma_foto_mostra_a_foto_em_tela_cheia()
        {
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());

            var foto = CriarFoto("DSC_0001.jpg");
            var outra = CriarFoto("DSC_0002.jpg");

            Ui.Run(() =>
            {
                try
                {
                    var main = new MainWindow();
                    main.Projects.Dialog = new FakeDialog();
                    main.Selection.Dialog = new FakeDialog();

                    // the app started with a photo (Windows Explorer sends the path)
                    main.OpenFolderFromCommandLine(foto);

                    // the folder is scanned in the background
                    Assert.True(Ui.WaitUntil(() => main.Selection.PhotoCount >= 2, 8000),
                        "a pasta deveria ter sido varrida");

                    // and the photo opens in full screen
                    Assert.True(Ui.WaitUntil(() => main.Selection.ZoomOpenCount >= 1, 8000),
                        "a foto deveria ter aberto em tela cheia");
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }

        [Fact]
        public void Uma_foto_inexistente_nao_abre_nada_em_tela_cheia()
        {
            var anterior = NacleFoto.App.App.Settings;
            NacleFoto.App.App.Settings = AppSettings.CreateDefault(ProjectStore.GetAppDataDir());

            var foto = CriarFoto("DSC_0001.jpg");

            Ui.Run(() =>
            {
                try
                {
                    var main = new MainWindow();
                    main.Projects.Dialog = new FakeDialog();
                    main.Selection.Dialog = new FakeDialog();

                    // opens the folder
                    main.OpenFolderFromCommandLine(_dir);
                    Assert.True(Ui.WaitUntil(() => main.Selection.PhotoCount >= 1, 8000));

                    // and asks for a photo that is not in it
                    main.Selection.OpenPhotoWhenReady(Path.Combine(_dir, "nao-existe.jpg"));
                    Ui.Idle(600);

                    Assert.Equal(0, main.Selection.ZoomOpenCount);
                }
                finally { NacleFoto.App.App.Settings = anterior; }
            });
        }
    }
}
