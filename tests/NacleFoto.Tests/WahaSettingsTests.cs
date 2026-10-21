using System;
using System.IO;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    public class WahaSettingsTests : IDisposable
    {
        private readonly string _dir;

        public WahaSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-wahaset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void Waha_starts_automatically_by_default()
        {
            // users should not need to configure anything for the server to start
            var s = AppSettings.CreateDefault(_dir);
            Assert.True(s.WahaAutoStart);
        }

        [Fact]
        public void Default_address_is_localhost()
        {
            var s = AppSettings.CreateDefault(_dir);
            Assert.Equal("http://localhost:3000", s.WahaUrl);
            Assert.Equal("default", s.WahaSession);
            // no external key: the app generates its own
            Assert.True(string.IsNullOrEmpty(s.WahaApiKey));
        }

        [Fact]
        public void Waha_session_is_created_if_it_does_not_exist()
        {
            // WAHA returns 404 if the session does not exist: without this there is no QR code
            var cliente = new WahaClient("http://127.0.0.1:39999", "chave", "default");
            // closed port: must not throw an exception
            var ok = cliente.EnsureSession();
            Assert.False(ok);
        }

        [Fact]
        public void Nonexistent_qr_code_returns_null_without_throwing()
        {
            var cliente = new WahaClient("http://127.0.0.1:39999", "chave", "default");
            Assert.Null(cliente.GetQrCodePng());
        }

        [Fact]
        public void Waha_installation_folder_is_in_app_data()
        {
            var manager = new WahaManager(_dir);
            Assert.Equal(Path.Combine(_dir, "Waha"), manager.InstallDir);
            Assert.False(manager.IsWahaInstalled());
            Assert.False(manager.IsBuilt());
        }
    }
}
