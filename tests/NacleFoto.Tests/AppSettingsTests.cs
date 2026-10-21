using System;
using System.IO;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    public class AppSettingsTests : IDisposable
    {
        private readonly string _dir;

        public AppSettingsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nacle-cfg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void Default_is_loose_photos_and_no_phone()
        {
            var s = AppSettings.CreateDefault(_dir);

            // the current workflow is to send loose photos on WhatsApp
            Assert.Equal(SendFormat.Loose, s.Format);
            // no phone number or credentials in code: user fills them in
            Assert.True(string.IsNullOrEmpty(s.WhatsAppPhone));
            Assert.True(string.IsNullOrEmpty(s.MessageTemplate));
            // the link is ready to use (sendit.sh requires no login)
            Assert.Equal("sendit", s.LinkProvider);
            Assert.False(s.UseDownloadLink);
        }

        [Fact]
        public void Saves_and_recovers_what_the_user_configured()
        {
            var s = AppSettings.CreateDefault(_dir);
            s.WhatsAppPhone = "5511999999999";
            s.Format = SendFormat.Zip;
            s.PartSizeMb = 80;
            s.UseDownloadLink = true;
            s.LinkProvider = "sendit";
            s.SyncFolder = _dir;
            s.RetentionDays = 180;
            s.AppLanguage = AppLanguage.English;
            s.Save(_dir);

            var loaded = AppSettings.Load(_dir);

            Assert.Equal("5511999999999", loaded.WhatsAppPhone);
            Assert.Equal(SendFormat.Zip, loaded.Format);
            Assert.Equal(80, loaded.PartSizeMb);
            Assert.True(loaded.UseDownloadLink);
            Assert.Equal("sendit", loaded.LinkProvider);
            Assert.Equal(_dir, loaded.SyncFolder);
            Assert.Equal(180, loaded.RetentionDays);
            Assert.Equal(AppLanguage.English, loaded.AppLanguage);
        }

        [Fact]
        public void Whatsapp_number_is_normalized()
        {
            Assert.Equal("5511999999999", NacleFoto.Core.Packaging.SendPreparer.NormalizePhone("+55 (11) 99999-9999"));
            Assert.Equal("5511999999999", NacleFoto.Core.Packaging.SendPreparer.NormalizePhone("5511999999999"));
            Assert.Null(NacleFoto.Core.Packaging.SendPreparer.NormalizePhone(""));
        }

        [Fact]
        public void Whatsapp_link_includes_conversation_and_message()
        {
            var w = new NacleFoto.Core.Models.Wedding { Name = "Casamento Silva" };
            var mensagem = NacleFoto.Core.Packaging.SendPreparer.BuildWhatsAppMessage(w, 3, AppLanguage.Portuguese);
            var link = NacleFoto.Core.Packaging.SendPreparer.BuildWhatsAppLink("5511999999999", mensagem);

            Assert.Contains("Casamento Silva", mensagem);
            Assert.Contains("3 partes", mensagem);
            Assert.StartsWith("https://wa.me/5511999999999?text=", link);
        }
    }
}
