using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NacleFoto.App;
using NacleFoto.App.Services;
using NacleFoto.App.ViewModels;
using NacleFoto.App.Views;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Imaging;
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

        private static byte[] _validJpegBytes;
        private static byte[] GetValidJpegBytes()
        {
            if (_validJpegBytes == null)
            {
                Ui.Run(() =>
                {
                    var rtb = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
                    _validJpegBytes = WicLoader.EncodeJpeg(rtb, 85);
                });
            }
            return _validJpegBytes;
        }

        private string CriarFoto(string nome)
        {
            var caminho = Path.Combine(_dir, nome);
            File.WriteAllBytes(caminho, GetValidJpegBytes());
            return caminho;
        }

        [Fact]
        public void Janela_de_zoom_maximiza_com_owner()
        {
            var foto = CriarFoto("DSC_0001.jpg");
            Ui.Run(() =>
            {
                var rtb = new RenderTargetBitmap(3000, 2000, 96, 96, PixelFormats.Pbgra32);
                var jpegBytes = WicLoader.EncodeJpeg(rtb, 90);
                Assert.NotNull(jpegBytes);
                Assert.True(jpegBytes.Length > 0);
                File.WriteAllBytes(foto, jpegBytes);

                var loadedBmp = WicLoader.LoadScaled(foto, 1000);
                Assert.NotNull(loadedBmp);

                var main = new MainWindow();
                main.WindowState = WindowState.Normal;
                main.Width = 1000;
                main.Height = 700;
                main.Show();

                var window = new ZoomWindow();
                window.Owner = main;
                var item = new PhotoItemViewModel("DSC_0001.jpg", foto, jpegBytes.Length, 1);
                window.Open(new List<PhotoItemViewModel> { item }, 0);
                window.Show();
                Ui.Idle(200);

                Assert.Equal(WindowState.Maximized, window.WindowState);
                Assert.True(window.ActualWidth >= SystemParameters.PrimaryScreenWidth - 20,
                    $"Largura esperada perto de {SystemParameters.PrimaryScreenWidth}, mas foi {window.ActualWidth}");
                Assert.True(window.ActualHeight >= SystemParameters.PrimaryScreenHeight - 40,
                    $"Altura esperada perto de {SystemParameters.PrimaryScreenHeight}, mas foi {window.ActualHeight}");

                var cnv = window.FindName("Cnv") as Canvas;
                Assert.NotNull(cnv);
                Assert.True(cnv.Children.Count > 0, "Canvas deve conter a imagem");
                var img = cnv.Children[0] as Image;
                Assert.NotNull(img);

                var tg = img.RenderTransform as TransformGroup;
                Assert.NotNull(tg);
                var st = tg.Children[0] as ScaleTransform;
                var tt = tg.Children[1] as TranslateTransform;

                // Photo must be scaled to fit within usable canvas and centered
                double scaledW = img.Width * st.ScaleX;
                double scaledH = img.Height * st.ScaleY;
                Assert.True(scaledW <= cnv.ActualWidth, "Largura escalada deve caber no canvas");
                Assert.True(scaledH <= cnv.ActualHeight, "Altura escalada deve caber no canvas");
                Assert.True(tt.X >= 0, "Translacao X deve centralizar (>= 0)");
                Assert.True(tt.Y >= 0, "Translacao Y deve centralizar (>= 0)");

                window.Close();
                main.Close();
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
