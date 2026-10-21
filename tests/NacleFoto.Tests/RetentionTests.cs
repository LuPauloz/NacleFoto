using System;
using System.IO;
using System.Linq;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    public class RetentionAndOrderTests : IDisposable
    {
        private readonly string _dir;

        public RetentionAndOrderTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-ret-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private Wedding CriarCasamento(string nome, DateTime criado)
        {
            var w = new Wedding
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = nome,
                RootFolder = _dir,
                CreatedAt = criado,
                UpdatedAt = criado
            };
            ProjectStore.Save(w, _dir);
            return w;
        }

        [Fact]
        public void List_is_ordered_by_newest_first()
        {
            var antigo = CriarCasamento("Antigo", DateTime.Now.AddDays(-10));
            var medio = CriarCasamento("Medio", DateTime.Now.AddDays(-5));
            var novo = CriarCasamento("Novo", DateTime.Now.AddDays(-1));

            var lista = ProjectStore.LoadRecent(_dir);

            Assert.Equal(3, lista.Count);
            Assert.Equal(novo.Id, lista[0].Id);
            Assert.Equal(medio.Id, lista[1].Id);
            Assert.Equal(antigo.Id, lista[2].Id);
        }

        [Fact]
        public void Retention_removes_the_old_marking_from_the_list()
        {
            var antigo = CriarCasamento("Antigo", DateTime.Now.AddDays(-400));
            var recente = CriarCasamento("Recente", DateTime.Now.AddDays(-2));

            var removidos = ProjectStore.ApplyRetention(365, _dir);

            Assert.Equal(1, removidos);
            var lista = ProjectStore.LoadRecent(_dir);
            Assert.Single(lista);
            Assert.Equal(recente.Id, lista[0].Id);

            // the old selection file was deleted
            var arquivoAntigo = ProjectStore.ResolveProjectPath(antigo, _dir);
            Assert.False(File.Exists(arquivoAntigo));
        }

        [Fact]
        public void Retention_does_not_delete_anything_with_zero_days()
        {
            CriarCasamento("Antigo", DateTime.Now.AddDays(-400));
            var removidos = ProjectStore.ApplyRetention(0, _dir);
            Assert.Equal(0, removidos);
            Assert.Single(ProjectStore.LoadRecent(_dir));
        }

        [Fact]
        public void Retention_does_not_touch_photos()
        {
            var foto = Path.Combine(_dir, "foto.jpg");
            File.WriteAllText(foto, "sou uma foto");
            CriarCasamento("Antigo", DateTime.Now.AddDays(-400));

            ProjectStore.ApplyRetention(365, _dir);

            Assert.True(File.Exists(foto), "a foto no disco nunca pode ser apagada pela politica de retencao");
        }
    }

    public class SenditTests
    {
        [Fact]
        public void Extracts_link_from_service_response()
        {
            var resposta =
                "=========================\n\n" +
                "Uploaded 1 file, 30 bytes\n\n" +
                "wget https://sendit.sh/blQdR/nacle_teste.txt\n\n" +
                "=========================\n";

            var link = SenditClient.ExtractUrl(resposta);
            Assert.Equal("https://sendit.sh/blQdR/nacle_teste.txt", link);
        }

        [Fact]
        public void Aceita_resposta_só_com_a_url()
        {
            Assert.Equal("https://sendit.sh/abc/foto.jpg",
                SenditClient.ExtractUrl("https://sendit.sh/abc/foto.jpg"));
        }

        [Fact]
        public void Response_without_link_returns_null()
        {
            Assert.Null(SenditClient.ExtractUrl("erro qualquer"));
            Assert.Null(SenditClient.ExtractUrl(null));
        }
    }
}
