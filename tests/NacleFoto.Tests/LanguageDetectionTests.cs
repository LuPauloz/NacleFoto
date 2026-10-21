using NacleFoto.Core.Localization;
using NacleFoto.Core.Storage;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// The app opens in the language of the operating system: Portuguese on a
    /// Portuguese machine, English anywhere else, and the user can force one
    /// in Settings.
    /// </summary>
    public class LanguageDetectionTests
    {
        [Fact]
        public void Portuguese_culture_opens_in_portuguese()
        {
            Assert.Equal(AppLanguage.Portuguese, Strings.DetectLanguage("pt-BR"));
            Assert.Equal(AppLanguage.Portuguese, Strings.DetectLanguage("pt"));
            Assert.Equal(AppLanguage.Portuguese, Strings.DetectLanguage("PT"));
        }

        [Fact]
        public void Any_other_culture_opens_in_english()
        {
            Assert.Equal(AppLanguage.English, Strings.DetectLanguage("en-US"));
            Assert.Equal(AppLanguage.English, Strings.DetectLanguage("es-ES"));
            Assert.Equal(AppLanguage.English, Strings.DetectLanguage("fr"));
            Assert.Equal(AppLanguage.English, Strings.DetectLanguage(""));
            Assert.Equal(AppLanguage.English, Strings.DetectLanguage(null));
        }

        [Fact]
        public void The_system_language_is_detected()
        {
            // must not throw, whatever the machine is set to
            var lang = Strings.DetectSystemLanguage();
            Assert.True(lang == AppLanguage.Portuguese || lang == AppLanguage.English);
        }

        [Fact]
        public void A_new_installation_follows_the_system_language()
        {
            var settings = AppSettings.CreateDefault(System.IO.Path.GetTempPath());
            Assert.Equal("auto", settings.Language);
            Assert.Equal(Strings.DetectSystemLanguage(), settings.AppLanguage);
        }

        [Fact]
        public void The_user_can_force_a_language()
        {
            var settings = AppSettings.CreateDefault(System.IO.Path.GetTempPath());

            settings.Language = "en";
            Assert.Equal(AppLanguage.English, settings.AppLanguage);

            settings.Language = "pt";
            Assert.Equal(AppLanguage.Portuguese, settings.AppLanguage);
        }
    }
}
