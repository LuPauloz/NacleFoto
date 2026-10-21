using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NacleFoto.Core.Models;
using NacleFoto.Core.Packaging;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    public class PackageBuilderTests : IDisposable
    {
        private readonly string _dir;

        public PackageBuilderTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-teste-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private List<PackageItem> CriarFotos(int quantidade, int bytesPorFoto)
        {
            var lista = new List<PackageItem>();
            for (int i = 0; i < quantidade; i++)
            {
                var nome = "fotos\\sub" + (i % 3) + "\\IMG_" + (1000 + i) + ".jpg";
                var caminho = Path.Combine(_dir, "origem", "fotos", "sub" + (i % 3), "IMG_" + (1000 + i) + ".jpg");
                Directory.CreateDirectory(Path.GetDirectoryName(caminho));
                var dados = new byte[bytesPorFoto];
                new Random(i).NextBytes(dados);
                File.WriteAllBytes(caminho, dados);
                lista.Add(new PackageItem { SourcePath = caminho, ZipName = nome, Size = bytesPorFoto });
            }
            return lista;
        }

        [Fact]
        public void Splits_the_package_respecting_size_limit()
        {
            var itens = CriarFotos(10, 1024 * 1024); // 1 MB each = 10 MB
            var limite = 3L * 1024 * 1024;

            var resultado = PackageBuilder.Build(itens, Path.Combine(_dir, "pacote"), "Casamento_Teste", limite, new Wedding { Name = "Teste" });

            Assert.True(resultado.Parts.Count >= 3, "esperava pelo menos 3 partes, veio " + resultado.Parts.Count);
            foreach (var parte in resultado.Parts)
            {
                Assert.True(parte.Bytes <= limite + 64 * 1024,
                    "parte " + parte.Part + " ficou com " + parte.Bytes + " bytes, acima do limite");
            }
            Assert.Equal(10, resultado.TotalFiles);
        }

        [Fact]
        public void Generates_readme_and_inventory()
        {
            var itens = CriarFotos(3, 1024);
            var resultado = PackageBuilder.Build(itens, Path.Combine(_dir, "pacote"), "Casamento_Teste", 10 * 1024 * 1024, new Wedding { Name = "Wedding Teste" });

            Assert.True(File.Exists(resultado.ReadmePath));
            Assert.True(File.Exists(resultado.InventoryPath));

            var readme = File.ReadAllText(resultado.ReadmePath);
            Assert.Contains("Wedding Teste", readme);
            Assert.Contains("3", readme);
        }

        [Fact]
        public void All_photos_are_included_in_the_package()
        {
            var itens = CriarFotos(7, 2048);
            var resultado = PackageBuilder.Build(itens, Path.Combine(_dir, "pacote"), "Teste", 10 * 1024 * 1024, new Wedding { Name = "Teste" });

            var total = resultado.Parts.Sum(p => p.FileCount);
            Assert.Equal(7, total);
        }

        [Fact]
        public void Zip_can_contain_only_photos()
        {
            // in download link workflow the zip contains only photos:
            // no README or spreadsheet included
            var itens = CriarFotos(3, 1024);
            var pasta = Path.Combine(_dir, "somente-fotos");
            PackageBuilder.Build(itens, pasta, "Teste", 10 * 1024 * 1024,
                new Wedding { Name = "Teste" }, null, default(System.Threading.CancellationToken), false);

            var zip = Directory.GetFiles(pasta, "*.zip")[0];
            using (var archive = System.IO.Compression.ZipFile.OpenRead(zip))
            {
                var nomes = archive.Entries.Select(e => e.FullName).ToList();
                Assert.Equal(3, nomes.Count);
                Assert.DoesNotContain(nomes, n => n.Contains("LEIA-ME"));
                Assert.DoesNotContain(nomes, n => n.Contains("INVENTARIO"));
                Assert.All(nomes, n => Assert.EndsWith(".jpg", n));
            }
        }
    }

    public class ProjectStoreTests : IDisposable
    {
        private readonly string _dir;

        public ProjectStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-proj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void Saves_and_loads_the_marking()
        {
            var c = new Wedding
            {
                Id = "abc123",
                Name = "Wedding Silva",
                RootFolder = _dir,
                Selection = new List<SelectionItem>
                {
                    new SelectionItem { Path = "fotos\\a.jpg", Size = 100, ModifiedUtcTicks = DateTime.UtcNow.Ticks }
                }
            };

            ProjectStore.Save(c, _dir);
            var arquivo = ProjectStore.ResolveProjectPath(c, _dir);
            var carregado = ProjectStore.Load(arquivo);

            Assert.NotNull(carregado);
            Assert.Equal("Wedding Silva", carregado.Name);
            Assert.Equal(1, carregado.TotalSelected);
            Assert.Equal(100, carregado.SelectedSize);
        }

        [Fact]
        public void Slug_removes_accents_and_spaces()
        {
            Assert.Equal("wedding-silva-e-souza", ProjectStore.Slug("Wedding Silva e Souza"));
            Assert.Equal("ana-clara", ProjectStore.Slug("Ana Clára"));
        }
    }
}
