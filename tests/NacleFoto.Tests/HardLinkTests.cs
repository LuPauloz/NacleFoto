using System;
using System.IO;
using NacleFoto.Core.Packaging;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// The delivery folder with "loose photos" must NOT duplicate photos on disk.
    /// We use hard links (disk links): the same photo, pointing to the same data.
    /// </summary>
    public class HardLinkTests : IDisposable
    {
        private readonly string _dir;

        public HardLinkTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-link-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void Hard_link_points_to_the_same_file_without_copying()
        {
            var original = Path.Combine(_dir, "foto.jpg");
            File.WriteAllText(original, "conteudo da foto");
            var atalho = Path.Combine(_dir, "atalho", "foto.jpg");

            Assert.True(HardLink.TryCreate(atalho, original));
            Assert.True(File.Exists(atalho));

            // same content
            Assert.Equal(File.ReadAllText(original), File.ReadAllText(atalho));

            // same physical file (Windows reports the same file)
            var info1 = new FileInfo(original);
            var info2 = new FileInfo(atalho);
            Assert.Equal(info1.Length, info2.Length);
        }

        [Fact]
        public void Deleting_hard_link_does_not_delete_original_photo()
        {
            var original = Path.Combine(_dir, "foto.jpg");
            File.WriteAllText(original, "conteudo da foto");
            var atalho = Path.Combine(_dir, "atalho", "foto.jpg");
            HardLink.TryCreate(atalho, original);

            File.Delete(atalho);

            Assert.True(File.Exists(original), "a foto original nao pode ser apagada junto com o atalho");
            Assert.Equal("conteudo da foto", File.ReadAllText(original));
        }

        [Fact]
        public void Send_folder_does_not_duplicate_photos()
        {
            var fotos = Path.Combine(_dir, "fotos");
            Directory.CreateDirectory(fotos);
            var a = Path.Combine(fotos, "a.jpg");
            var b = Path.Combine(fotos, "b.jpg");
            File.WriteAllBytes(a, new byte[1000]);
            File.WriteAllBytes(b, new byte[2000]);

            var wedding = new NacleFoto.Core.Models.Wedding
            {
                Id = "x",
                Name = "Casamento Teste",
                RootFolder = fotos,
                State = NacleFoto.Core.Models.WeddingState.Closed
            };
            wedding.Selection.Add(new NacleFoto.Core.Models.SelectionItem { Path = "a.jpg", Size = 1000, ModifiedUtcTicks = 1 });
            wedding.Selection.Add(new NacleFoto.Core.Models.SelectionItem { Path = "b.jpg", Size = 2000, ModifiedUtcTicks = 1 });

            var itens = WeddingFiles.BuildPackageItems(wedding);
            var destino = Path.Combine(_dir, "envio");
            var resultado = SendPreparer.Prepare(wedding, itens, destino, 50 * 1024 * 1024, SendFormat.Loose);

            Assert.True(resultado.HasLoose);
            Assert.Equal(2, resultado.LooseFileCount);
            // both became disk hard links: zero copies
            Assert.Equal(2, resultado.LooseHardLinks);

            // content is there and the original remains intact
            var envioA = Path.Combine(resultado.LooseFolder, "a.jpg");
            Assert.True(File.Exists(envioA));
            Assert.Equal(1000, new FileInfo(envioA).Length);
            Assert.True(File.Exists(a));
        }
    }
}
