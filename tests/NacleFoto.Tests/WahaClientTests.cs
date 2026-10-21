using System;
using System.IO;
using NacleFoto.Core.Linking;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    public class WahaClientTests
    {
        [Fact]
        public void Builds_chat_id_in_waha_format()
        {
            Assert.Equal("5511999999999@c.us", WahaClient.BuildChatId("5511999999999"));
            Assert.Equal("5511999999999@c.us", WahaClient.BuildChatId("+55 (11) 99999-9999"));
            Assert.Equal("5538999999999@c.us", WahaClient.BuildChatId("55 38 99999 9999"));
        }

        [Fact]
        public void Duplicate_international_prefix_is_removed()
        {
            Assert.Equal("5511999999999@c.us", WahaClient.BuildChatId("005511999999999"));
        }

        [Fact]
        public void Empty_number_does_not_generate_chat_id()
        {
            Assert.Null(WahaClient.BuildChatId(""));
            Assert.Null(WahaClient.BuildChatId(null));
        }

        [Fact]
        public void Without_address_client_is_not_configured()
        {
            Assert.False(new WahaClient("", "", "default").IsConfigured);
            Assert.True(new WahaClient("http://localhost:3000", "", "default").IsConfigured);
        }

        [Fact]
        public void Waha_settings_save_and_reload()
        {
            var dir = Path.Combine(Path.GetTempPath(), "nacle-waha-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var s = AppSettings.CreateDefault(dir);
                s.WahaUrl = "http://192.168.0.10:3000";
                s.WahaApiKey = "chave-secreta";
                s.WahaSession = "nacle";
                s.WahaAutoStart = true;
                s.WahaStartCommand = "docker run -d -p 3000:3000 devlikeapro/waha";
                s.Save(dir);

                var l = AppSettings.Load(dir);
                Assert.Equal("http://192.168.0.10:3000", l.WahaUrl);
                Assert.Equal("chave-secreta", l.WahaApiKey);
                Assert.Equal("nacle", l.WahaSession);
                Assert.True(l.WahaAutoStart);
                Assert.Contains("docker", l.WahaStartCommand);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
