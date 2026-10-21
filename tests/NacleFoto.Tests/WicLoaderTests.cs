using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NacleFoto.Windows.Imaging;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// Scaled decode is core to performance: without it, a thumbnail for a 24 MP
    /// photo would require decoding all 24 MP.
    /// </summary>
    public class WicLoaderTests : IDisposable
    {
        private readonly string _dir;

        public WicLoaderTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-wic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private string CriarJpeg(int largura, int altura)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.CornflowerBlue, null, new Rect(0, 0, largura, altura));
                dc.DrawEllipse(Brushes.Orange, null, new Point(largura / 2, altura / 2), largura / 3, altura / 3);
            }
            var rtb = new RenderTargetBitmap(largura, altura, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 90;
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            var caminho = Path.Combine(_dir, "foto.jpg");
            using (var fs = File.Create(caminho)) encoder.Save(fs);
            return caminho;
        }

        [Fact]
        public void Reduces_the_image_to_maximum_side_preserving_aspect_ratio()
        {
            var caminho = CriarJpeg(800, 600);
            var bmp = WicLoader.LoadScaled(caminho, 100);

            Assert.NotNull(bmp);
            Assert.Equal(100, bmp.PixelWidth);
            Assert.Equal(75, bmp.PixelHeight);
        }

        [Fact]
        public void Vertical_image_is_also_reduced()
        {
            var caminho = CriarJpeg(600, 1200);
            var bmp = WicLoader.LoadScaled(caminho, 100);

            Assert.NotNull(bmp);
            Assert.Equal(50, bmp.PixelWidth);
            Assert.Equal(100, bmp.PixelHeight);
        }

        [Fact]
        public void Does_not_enlarge_small_image()
        {
            var caminho = CriarJpeg(120, 90);
            var bmp = WicLoader.LoadScaled(caminho, 500);

            Assert.NotNull(bmp);
            Assert.Equal(120, bmp.PixelWidth);
            Assert.Equal(90, bmp.PixelHeight);
        }

        [Fact]
        public void Releases_the_file_after_loading()
        {
            var caminho = CriarJpeg(800, 600);
            var bmp = WicLoader.LoadScaled(caminho, 100);
            Assert.NotNull(bmp);

            // the file must be able to be deleted/moved (the app never locks the photo)
            File.Delete(caminho);
            Assert.False(File.Exists(caminho));
        }

        [Fact]
        public void Reads_dimensions_without_decoding()
        {
            var caminho = CriarJpeg(800, 600);
            int w, h;
            Assert.True(WicLoader.TryGetSize(caminho, out w, out h));
            Assert.Equal(800, w);
            Assert.Equal(600, h);
        }

        [Fact]
        public void Encodes_and_decodes_jpeg()
        {
            var caminho = CriarJpeg(400, 300);
            var bmp = WicLoader.LoadScaled(caminho, 0);
            var jpeg = WicLoader.EncodeJpeg(bmp, 80);

            Assert.NotNull(jpeg);
            Assert.True(jpeg.Length > 0);

            var deVolta = WicLoader.DecodeJpeg(jpeg);
            Assert.NotNull(deVolta);
            Assert.Equal(400, deVolta.PixelWidth);
        }

        [Fact]
        public void Cached_image_has_pixels_after_stream_is_disposed()
        {
            // real bug: with lazy decoding the stream is disposed before pixels
            // are read, resulting in an empty thumbnail
            var caminho = CriarJpeg(400, 300);
            var bmp = WicLoader.LoadScaled(caminho, 0);
            var jpeg = WicLoader.EncodeJpeg(bmp, 80);
            var deVolta = WicLoader.DecodeJpeg(jpeg);

            var pixels = new byte[deVolta.PixelWidth * deVolta.PixelHeight * 4];
            deVolta.CopyPixels(new Int32Rect(0, 0, deVolta.PixelWidth, deVolta.PixelHeight),
                pixels, deVolta.PixelWidth * 4, 0);

            int max = 0, naoZeros = 0;
            foreach (var b in pixels) { if (b > max) max = b; if (b != 0) naoZeros++; }
            Assert.True(naoZeros > 0,
                "a imagem decodificada do cache saiu vazia (formato=" + deVolta.Format +
                ", maior byte=" + max + ", nao zeros=" + naoZeros + ")");
        }
    }
}
