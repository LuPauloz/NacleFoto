using System;
using NacleFoto.Core.Linking;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// The command that starts the WAHA server previously broke twice due to the
    /// environment variable format. These tests verify the correct format.
    /// </summary>
    public class WahaServerCommandTests
    {
        private static string Comando(string apiKey = "abc123", int porta = 3000)
        {
            var manager = new WahaManager(System.IO.Path.GetTempPath());
            return manager.BuildServerCommand(apiKey, porta);
        }

        [Fact]
        public void Environment_variable_uses_quotes_in_assignment()
        {
            // "set NAME=value" works, but "set NAME="value" with quotes in value
            // keeps quotes in the value and WAHA creates an invalid folder name
            var cmd = Comando();
            Assert.Contains("set \"WAHA_API_KEY=abc123\"", cmd);
            Assert.DoesNotContain("WAHA_API_KEY=\"", cmd);
        }

        [Fact]
        public void Variable_names_are_those_of_waha()
        {
            var cmd = Comando();
            Assert.Contains("set \"WAHA_API_KEY=", cmd);           // not WHATSAPP_API_KEY
            Assert.Contains("set \"WHATSAPP_API_PORT=3000\"", cmd); // not WHATSAPP_PORT
            Assert.Contains("set \"WHATSAPP_API_HOSTNAME=localhost\"", cmd);
            Assert.Contains("set \"WHATSAPP_DEFAULT_ENGINE=NOWEB\"", cmd);
        }

        [Fact]
        public void Sessions_folder_uses_the_correct_name()
        {
            // WAHA_LOCAL_STORE_BASE_DIR is the real name (WHATSAPP_SESSIONS_FOLDER does not exist)
            var cmd = Comando();
            Assert.Contains("set \"WAHA_LOCAL_STORE_BASE_DIR=", cmd);
            Assert.DoesNotContain("WHATSAPP_SESSIONS_FOLDER", cmd);
        }

        [Fact]
        public void Photos_folder_is_passed_with_absolute_path()
        {
            // without this WAHA uses C:\tmp\whatsapp-files and crashes on boot
            var cmd = Comando();
            const string prefixo = "set \"WHATSAPP_FILES_FOLDER=";
            var inicio = cmd.IndexOf(prefixo, StringComparison.Ordinal);
            Assert.True(inicio >= 0, "a variavel WHATSAPP_FILES_FOLDER nao esta no comando");

            var valor = cmd.Substring(inicio + prefixo.Length).Split('"')[0];
            Assert.True(System.IO.Path.IsPathRooted(valor), "a pasta das fotos precisa ser absoluta: " + valor);
        }

        [Fact]
        public void Qr_is_not_printed_to_console()
        {
            // the QR already appears inside the app: in console it only pollutes the log
            var cmd = Comando();
            Assert.Contains("set \"WAHA_PRINT_QR=false\"", cmd);
        }

        [Fact]
        public void Command_runs_the_waha_server()
        {
            var cmd = Comando();
            Assert.Contains("node dist/main", cmd);
        }

        [Fact]
        public void Server_output_goes_to_the_log()
        {
            // the server runs detached (outside the app): without a log file
            // there is no way to know why it failed to start
            var cmd = Comando();
            Assert.Contains(">>", cmd);
            Assert.Contains("2>&1", cmd);
            Assert.Contains("waha-server.log", cmd);
        }
    }
}
