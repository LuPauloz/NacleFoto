using System;
using System.Diagnostics;
using System.Threading;
using NacleFoto.Core.Linking;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// End-to-end test against real WAHA (starts server, creates session,
    /// and fetches the QR code). Slow and requires WAHA installed, so it only
    /// runs when the environment variable NACLE_WAHA_LIVE=1 is set.
    ///
    ///   set NACLE_WAHA_LIVE=1 &amp;&amp; dotnet test
    /// </summary>
    public class WahaLiveTests
    {
        private static bool Habilitado
        {
            get { return Environment.GetEnvironmentVariable("NACLE_WAHA_LIVE") == "1"; }
        }

        [Fact]
        public void Server_starts_and_delivers_the_qr_code()
        {
            if (!Habilitado) return; // only runs when requested (environment variable)

            var chave = "chave-teste-nacle";
            var porta = 3010; // separate port: the app might be running on 3000
            var manager = new WahaManager();
            if (!manager.IsBuilt()) return; // WAHA not installed on this machine

            // separate folders: two WAHA instances cannot share the same session database
            var pastaDados = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NacleFotoWahaTeste");
            System.IO.Directory.CreateDirectory(pastaDados);

            var inicio = DateTime.Now;
            manager.StartServer(chave, porta, pastaDados);

            // wait for server to respond
            var client = new WahaClient("http://localhost:" + porta, chave, "default");
            var subiu = false;
            while (DateTime.Now - inicio < TimeSpan.FromSeconds(90))
            {
                if (client.IsRunning()) { subiu = true; break; }
                Thread.Sleep(1000);
            }
            Assert.True(subiu, "O servidor WAHA nao respondeu em 90 segundos.");

            // session must exist and be running to provide a QR code
            Assert.True(client.EnsureSession(), "Nao deu para criar/iniciar a sessao.");

            byte[] png = null;
            for (int i = 0; i < 10 && png == null; i++)
            {
                png = client.GetQrCodePng();
                if (png == null) Thread.Sleep(2000);
            }

            Assert.NotNull(png);
            Assert.True(png.Length > 100, "O QR code veio vazio.");
            // PNG signature: 89 50 4E 47
            Assert.Equal(0x89, png[0]);
            Assert.Equal(0x50, png[1]);
            Assert.Equal(0x4E, png[2]);
            Assert.Equal(0x47, png[3]);

            // stops the test server (only the test port: app might be running on 3000)
            try
            {
                int pid = WahaManager.ProcessOnPort(porta);
                if (pid > 0) Process.GetProcessById(pid).Kill();
            }
            catch { }
        }
    }
}
