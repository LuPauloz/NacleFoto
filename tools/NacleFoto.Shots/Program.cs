using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NacleFoto.App.Views;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using NacleFoto.App;

namespace NacleFoto.Shots
{
    /// <summary>
    /// Development tool: generates sample photos with EXIF and renders the
    /// app screens to PNG, to check the look without opening the interface.
    /// </summary>
    internal static class Program
    {
        private static string _saida;

        /// <summary>
        /// Root folder of the demo. It is a fixed path on purpose: the
        /// screenshots end up in the repository and must not show the name of
        /// the user account (the temp folder would).
        /// </summary>
        private static string DemoRoot
        {
            get
            {
                var raiz = @"C:\NacleDemo";
                try { Directory.CreateDirectory(raiz); return raiz; }
                catch { return Path.GetTempPath(); }
            }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            _saida = args.Length > 0 ? args[0] : Path.Combine(DemoRoot, "nacle-shots");
            Directory.CreateDirectory(_saida);

            var pastaFotos = Path.Combine(DemoRoot, "nacle-demo", "Wedding Demo");
            CreateTestPhotos(pastaFotos, 140);

            var app = new NacleFoto.App.App();
            app.InitializeComponent();

            // the real app runs with DispatcherSynchronizationContext (Application.Run);
            // in the harness we need to install it so await returns to the UI thread
            System.Threading.SynchronizationContext.SetSynchronizationContext(
                new System.Windows.Threading.DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            // ---- screen 0: the real app window (off-screen) ----
            var main = new MainWindow();
            main.WindowState = WindowState.Normal;
            main.Left = -4000;
            main.Top = -4000;
            main.Width = 1380;
            main.Height = 860;
            main.Show();
            Pump(900);
            Render(main, 1380, 860, "00-app-real.png");
            main.Close();

            // ---- screen 1: weddings ----
            var projetos = new ProjectsView();
            Render(projetos, 1380, 860, "01-casamentos.png");

            // ---- screen 2: selection ----
            var cas = new Wedding
            {
                Id = "demo",
                Name = "Wedding Silva & Souza",
                Client = "Familia Silva",
                Type = "Wedding",
                EventDate = new DateTime(2026, 10, 3),
                RootFolder = pastaFotos
            };
            var selecao = new SelectionView();
            selecao.Open(cas);
            Pump(2500);
            Render(selecao, 1380, 860, "02-selecao.png");
            Console.WriteLine("  com exif: " + selecao.PhotosWithExif + " / 140  -> " + selecao.ExampleBadge);

            // inspect first tile
            var tile = Find<PhotoTile>(selecao);
            if (tile != null)
            {
                var badge = FindByName<System.Windows.Controls.Border>(tile, "Badge");
                var texto = FindByName<System.Windows.Controls.TextBlock>(tile, "TextoBadge");
                Console.WriteLine("  tile: dc=" + (tile.DataContext == null ? "null" : tile.DataContext.GetType().Name)
                    + " | badge=" + (badge == null ? "?" : badge.Visibility.ToString())
                    + " | texto='" + (texto == null ? "?" : texto.Text) + "'");
            }
            else Console.WriteLine("  nenhum tile encontrado");

            // ---- screen 3: zoom (off-screen to avoid disturbing desktop) ----
            var zoom = new ZoomWindow();
            zoom.WindowState = WindowState.Normal;
            zoom.Left = -4000;
            zoom.Top = -4000;
            zoom.Width = 1380;
            zoom.Height = 860;
            zoom.Show();
            zoom.Open(selecao.Photos, 3);
            Pump(1200);
            var rtbZoom = new RenderTargetBitmap(1380, 860, 96, 96, PixelFormats.Pbgra32);
            rtbZoom.Render(zoom);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtbZoom));
            using (var fs = File.Create(Path.Combine(_saida, "03-zoom.png"))) enc.Save(fs);
            Console.WriteLine("  " + Path.Combine(_saida, "03-zoom.png"));
            zoom.Close();

            // ---- screen 4: send ----
            var comSelecao = new NacleFoto.Core.Models.Wedding
            {
                Id = "demo2",
                Name = "Casamento Silva & Souza",
                Client = "Familia Silva",
                EventDate = new DateTime(2026, 10, 3),
                RootFolder = pastaFotos,
                State = NacleFoto.Core.Models.WeddingState.Closed
            };
            foreach (var p in Directory.GetFiles(pastaFotos, "*.jpg").Take(120))
            {
                comSelecao.Selection.Add(new NacleFoto.Core.Models.SelectionItem
                {
                    Path = Path.GetFileName(p),
                    Size = new FileInfo(p).Length,
                    ModifiedUtcTicks = new FileInfo(p).LastWriteTimeUtc.Ticks
                });
            }
            var envio = new SendWindow();
            envio.WindowState = WindowState.Normal;
            envio.Left = -4000;
            envio.Top = -4000;
            envio.Width = 820;
            envio.Height = 620;
            envio.Show();
            envio.Open(comSelecao);
            Pump(700);
            Render(envio, 820, 620, "04-envio.png");
            envio.Close();

            // ---- screen 5: settings ----
            // the settings are built by hand: the default points at the real
            // desktop, and the screenshots must not show the user name
            var settings = NacleFoto.Core.Storage.AppSettings.CreateDefault(NacleFoto.Core.Storage.ProjectStore.GetAppDataDir());
            settings.DestinationFolder = Path.Combine(DemoRoot, "Envios");
            settings.WhatsAppPhone = "5511999999999";
            var conf = new SettingsWindow(settings);
            conf.WindowState = WindowState.Normal;
            conf.Left = -4000;
            conf.Top = -4000;
            conf.Show();
            Pump(500);
            // opens download link panel to verify provider
            var expander = Find<System.Windows.Controls.Expander>(conf);
            if (expander != null) expander.IsExpanded = true;
            Pump(300);
            Render(conf, 660, 900, "05-config.png");
            conf.Close();

            // ---- screen 6: english ----
            NacleFoto.Core.Localization.Strings.Language = NacleFoto.Core.Localization.AppLanguage.English;
            var en = new SelectionView();
            en.Open(comSelecao);
            Pump(2000);
            Render(en, 1380, 860, "06-selection-en.png");
            NacleFoto.Core.Localization.Strings.Language = NacleFoto.Core.Localization.AppLanguage.Portuguese;

            // ---- screen 7: after closing selection ----
            var depois = new NacleFoto.App.Views.AfterCloseDialog("300 fotos marcadas (820 MB).");
            depois.WindowState = WindowState.Normal;
            depois.Left = -4000;
            depois.Top = -4000;
            depois.Show();
            Pump(400);
            Render(depois, 560, 420, "07-depois-de-fechar.png");
            depois.Close();

            // ---- screen 8: WAHA manager ----
            var waha = new NacleFoto.App.Views.WahaManagerWindow();
            waha.WindowState = WindowState.Normal;
            waha.Left = -4000;
            waha.Top = -4000;
            waha.Show();
            Pump(600);
            Render(waha, 720, 620, "08-waha.png");
            waha.Close();

            // ---- benchmark: large folder ----
            Benchmark();

            Console.WriteLine("PNG em: " + _saida);
            return 0;
        }

        /// <summary>Measures time to scan a folder with many photos.</summary>
        private static void Benchmark()
        {
            var pasta = Path.Combine(DemoRoot, "nacle-bench");
            if (!Directory.Exists(pasta)) Directory.CreateDirectory(pasta);

            var origem = Directory.GetFiles(Path.Combine(DemoRoot, "nacle-demo", "Wedding Demo"), "*.jpg");
            if (origem.Length == 0) { Console.WriteLine("  (sem fotos de teste para o benchmark)"); return; }

            const int TOTAL = 3000;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < TOTAL; i++)
            {
                var destino = Path.Combine(pasta, "IMG_" + (10000 + i) + ".jpg");
                if (!File.Exists(destino)) File.Copy(origem[i % origem.Length], destino, true);
            }
            Console.WriteLine("  preparo de " + TOTAL + " arquivos: " + sw.ElapsedMilliseconds + " ms");

            sw.Restart();
            var indexador = new NacleFoto.App.Services.PhotoIndexer();
            var lista = indexador.Scan(new[] { pasta }, null, false);
            Console.WriteLine("  varredura de " + lista.Count + " fotos: " + sw.ElapsedMilliseconds + " ms");

            sw.Restart();
            int lidos = 0;
            foreach (var f in lista.Take(200))
            {
                var meta = NacleFoto.App.Services.PhotoIndexer.ReadExif(f);
                if (meta != null) lidos++;
            }
            var porFoto = sw.ElapsedMilliseconds / 200.0;
            Console.WriteLine("  EXIF de 200 fotos: " + sw.ElapsedMilliseconds + " ms (" + porFoto.ToString("0.0") + " ms/foto) -> " +
                              "2000 fotos em ~" + (porFoto * 2000 / 1000).ToString("0") + " s");

            // real upload to sendit.sh (test file) - only with --rede argument
            if (Environment.GetCommandLineArgs().Contains("--rede"))
            {
            try
            {
                var arquivoTeste = Path.Combine(DemoRoot, "nacle-teste-envio.txt");
                File.WriteAllText(arquivoTeste, "teste de upload do Nacle Foto");
                var client = new NacleFoto.Core.Linking.SenditClient();
                var link = client.UploadFile(arquivoTeste, "nacle-teste-envio.txt");
                Console.WriteLine("  sendit.sh: " + link);
                using (var wc = new System.Net.WebClient())
                {
                    var conteudo = wc.DownloadString(link);
                    Console.WriteLine("  download ok: " + (conteudo.Trim() == "teste de upload do Nacle Foto" ? "conteudo igual" : "CONTEUDO DIFERENTE"));
                }
                File.Delete(arquivoTeste);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  sendit.sh: falhou - " + ex.Message);
                var inner = ex.InnerException;
                while (inner != null) { Console.WriteLine("      -> " + inner.GetType().Name + ": " + inner.Message); inner = inner.InnerException; }
            }
            }

            // real OneDrive folder (the one that hung)
            var onedrive = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive", "Imagens", "Memes");
            if (Directory.Exists(onedrive))
            {
                sw.Restart();
                var listaNuvem = indexador.Scan(new[] { onedrive }, null, false);
                Console.WriteLine("  OneDrive\\Imagens\\Memes: " + listaNuvem.Count + " fotos locais em " +
                                  sw.ElapsedMilliseconds + " ms (as que estao na nuvem foram ignoradas)");
            }
        }

        private static T Find<T>(DependencyObject raiz) where T : DependencyObject
        {
            var n = VisualTreeHelper.GetChildrenCount(raiz);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(raiz, i);
                if (c is T) return (T)c;
                var r = Find<T>(c);
                if (r != null) return r;
            }
            return null;
        }

        private static T FindByName<T>(DependencyObject raiz, string nome) where T : FrameworkElement
        {
            var n = VisualTreeHelper.GetChildrenCount(raiz);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(raiz, i);
                var fe = c as FrameworkElement;
                if (fe != null && fe.Name == nome && c is T) return (T)c;
                var r = FindByName<T>(c, nome);
                if (r != null) return r;
            }
            return null;
        }

        private static void Pump(int ms)
        {
            var fim = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < fim)
            {
                // processes dispatcher queue (inline Invoke does not queue anything)
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(30);
            }
        }

        private static void Render(FrameworkElement elemento, int largura, int altura, string arquivo)
        {
            elemento.Width = largura;
            elemento.Height = altura;
            elemento.Measure(new Size(largura, altura));
            elemento.Arrange(new Rect(0, 0, largura, altura));
            elemento.UpdateLayout();
            Pump(300);

            var rtb = new RenderTargetBitmap(largura, altura, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(elemento);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            var caminho = Path.Combine(_saida, arquivo);
            using (var fs = File.Create(caminho)) encoder.Save(fs);
            Console.WriteLine("  " + caminho);
        }

        /// <summary>Generates realistic JPEGs (with EXIF) for development.</summary>
        private static void CreateTestPhotos(string pasta, int quantidade)
        {
            Directory.CreateDirectory(pasta);
            var rng = new Random(7);
            var cameras = new[] { "NIKON D750", "SONY A7R IV", "CANON EOS R6" };
            var lentes = new[] { "24-70mm f/2.8", "85mm f/1.4", "35mm f/1.8" };

            for (int i = 0; i < quantidade; i++)
            {
                var caminho = Path.Combine(pasta, string.Format("DSC_{0:D4}.jpg", 1000 + i));
                if (File.Exists(caminho)) continue;

                int w = 900, h = 600;
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    var fundo = Color.FromRgb((byte)rng.Next(30, 90), (byte)rng.Next(30, 90), (byte)rng.Next(30, 120));
                    dc.DrawRectangle(new SolidColorBrush(fundo), null, new Rect(0, 0, w, h));
                    var rnd = new Random(i);
                    for (int k = 0; k < 14; k++)
                    {
                        var cor = Color.FromRgb((byte)rnd.Next(40, 230), (byte)rnd.Next(40, 230), (byte)rnd.Next(40, 230));
                        dc.DrawRectangle(new SolidColorBrush(cor), null, new Rect(rnd.Next(w), rnd.Next(h), rnd.Next(20, 180), rnd.Next(20, 180)));
                    }
                    var ft = new FormattedText("DSC_" + (1000 + i), System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, new Typeface("Arial"), 28, Brushes.White);
                    dc.DrawText(ft, new Point(20, 20));
                }

                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);
                var encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 88;
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                byte[] jpeg;
                using (var ms = new MemoryStream()) { encoder.Save(ms); jpeg = ms.ToArray(); }

                var camera = cameras[i % cameras.Length];
                var lente = lentes[i % lentes.Length];
                double abertura = 1.4 + (i % 8) * 0.2;
                int iso = 100 * (1 + (i % 6));
                var shutter = new[] { 1.0 / 2000, 1.0 / 500, 1.0 / 250, 1.0 / 125, 1.0 / 60, 1.0 / 30 }[i % 6];
                var dt = new DateTime(2026, 10, 3, 9, 0, 0).AddSeconds(i * 37);

                jpeg = InjetarExif(jpeg, camera, lente, abertura, iso, shutter, dt);
                File.WriteAllBytes(caminho, jpeg);
            }
        }

        private static byte[] InjetarExif(byte[] jpeg, string camera, string lente, double abertura, int iso, double shutter, DateTime dt)
        {
            return ExifInjector.Injetar(jpeg, camera, lente, abertura, iso, shutter, dt);
        }
    }
}
