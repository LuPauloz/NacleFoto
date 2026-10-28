using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NacleFoto.Core.Linking
{
    /// <summary>
    /// Manages WAHA (WhatsApp server) on the machine: installs Node if missing,
    /// installs/updates WAHA, and starts the server on localhost.
    ///
    /// The app does not depend on WAHA to work: if not installed, automatic
    /// sending is simply unavailable.
    /// </summary>
    public sealed class WahaManager
    {
        public const string Repository = "https://github.com/devlikeapro/waha";
        public const string Repositorio = Repository;
        public const int DefaultPort = 3000;
        public const int PortaPadrao = DefaultPort;

        /// <summary>
        /// Dependency installation command. The WAHA project uses yarn.lock,
        /// and npm alone fails with ERESOLVE (peer dep of nestjs-redis). Therefore
        /// we try yarn (via corepack) and fall back to npm in legacy mode.
        /// </summary>
        public const string YarnCommand = "corepack yarn install";
        public const string ComandoYarn = YarnCommand;
        public const string LegacyNpmCommand = "npm install --legacy-peer-deps --no-audit --no-fund";
        public const string ComandoNpmLegado = LegacyNpmCommand;

        /// <summary>Skips Chromium download (NOWEB engine does not need it): saves ~150 MB.</summary>
        public const string EnvironmentVariable = "PUPPETEER_SKIP_DOWNLOAD=true";
        public const string VariavelAmbiente = EnvironmentVariable;

        /// <summary>
        /// Dependencies coming from GitHub repositories (wa-js, baileys, libsignal)
        /// are packaged by GitHub, and the generated tarball can change hash. yarn
        /// aborts with "The remote archive doesn't match the expected checksum" - with
        /// checksumBehavior=update it accepts and continues.
        /// </summary>
        public const string ChecksumVariable = "YARN_CHECKSUM_BEHAVIOR=update";
        public const string VariavelChecksum = ChecksumVariable;

        private readonly string _baseFolder;

        public WahaManager(string baseFolder = null)
        {
            _baseFolder = baseFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NacleFoto");
            InstallDir = Path.Combine(_baseFolder, "Waha");
        }

        public string InstallDir { get; private set; }
        public string SessionDir { get { return Path.Combine(InstallDir, ".sessions"); } }

        // ---------------------------------------------------------------- node

        public static bool IsNodeInstalled(out string version)
        {
            version = null;
            var r = Shell.Run("node", "--version", null, 20);
            if (!r.Ok) return false;
            version = (r.Output ?? "").Replace("\r", "").Split('\n')
                        .Select(l => l.Trim()).Where(l => l.Length > 0).LastOrDefault();
            return !string.IsNullOrWhiteSpace(version);
        }

        public static bool IsWingetAvailable()
        {
            return Shell.Run("winget", "--version", null, 30).Ok;
        }

        /// <summary>Installs Node LTS via winget (takes a few minutes).</summary>
        public static CommandResult InstallNode(Action<string> progress = null)
        {
            return Shell.Run("winget",
                "install --id OpenJS.NodeJS.LTS -e --accept-source-agreements --accept-package-agreements --silent --disable-interactivity",
                null, 1200, progress);
        }

        // ---------------------------------------------------------------- waha

        public bool IsWahaInstalled()
        {
            return Directory.Exists(Path.Combine(InstallDir, "node_modules"))
                && File.Exists(Path.Combine(InstallDir, "package.json"));
        }

        public bool IsBuilt()
        {
            return File.Exists(Path.Combine(InstallDir, "dist", "main.js"));
        }

        /// <summary>
        /// Installs WAHA dependencies.
        ///
        /// The WAHA project uses yarn.lock (yarn 4). npm alone fails with ERESOLVE
        /// (peer dependency conflict with @liaoliaots/nestjs-redis). Therefore the
        /// order is: yarn (via corepack) and, if it fails, npm with --legacy-peer-deps.
        /// </summary>
        public CommandResult InstallDependencies(Action<string> progress = null)
        {
            var env = "set " + EnvironmentVariable + "&& set " + ChecksumVariable + "&& ";

            var yarn = Shell.Run("cmd.exe", "/c " + env + YarnCommand, InstallDir, 2400, progress);
            if (yarn.Ok) return yarn;

            // fallback: npm accepting peer deps conflict
            return Shell.Run("cmd.exe", "/c " + env + LegacyNpmCommand, InstallDir, 2400, progress);
        }

        /// <summary>Downloads WAHA and installs dependencies. Takes a few minutes.</summary>
        public CommandResult Install(Action<string> progress = null)
        {
            Directory.CreateDirectory(_baseFolder);
            if (!Directory.Exists(InstallDir))
            {
                var clone = Shell.Run("git", "clone --depth 1 " + Repository + " \"" + InstallDir + "\"",
                    _baseFolder, 900, progress);
                if (!clone.Ok) return clone;
            }

            var install = InstallDependencies(progress);
            if (!install.Ok) return install;

            return Build(progress);
        }

        /// <summary>Builds WAHA (nest build).</summary>
        public CommandResult Build(Action<string> progress = null)
        {
            return Shell.Run("cmd.exe", "/c npm run build", InstallDir, 1800, progress);
        }

        /// <summary>Is there a new version in the repository?</summary>
        public bool IsUpdateAvailable(out string local, out string remote)
        {
            local = "";
            remote = "";
            if (!IsWahaInstalled()) return false;

            var head = Shell.Run("git", "rev-parse HEAD", InstallDir, 60);
            if (!head.Ok) return false;
            local = LastLine(head.Output);

            var fetch = Shell.Run("git", "fetch origin core --depth 1", InstallDir, 300);
            if (!fetch.Ok) return false;

            var remoteResult = Shell.Run("git", "rev-parse origin/core", InstallDir, 60);
            if (!remoteResult.Ok) return false;
            remote = LastLine(remoteResult.Output);

            return !string.IsNullOrEmpty(local) && !string.IsNullOrEmpty(remote) && local != remote;
        }

        /// <summary>Updates WAHA to the latest version.</summary>
        public CommandResult Update(Action<string> progress = null, int port = DefaultPort)
        {
            StopServer(port);

            var fetch = Shell.Run("git", "fetch origin core --depth 1", InstallDir, 300, progress);
            if (!fetch.Ok) return fetch;

            var reset = Shell.Run("git", "reset --hard origin/core", InstallDir, 60, progress);
            if (!reset.Ok) return reset;

            var install = InstallDependencies(progress);
            if (!install.Ok) return install;

            return Build(progress);
        }

        // ---------------------------------------------------------------- server

        /// <summary>
        /// Starts the WAHA server on localhost (in the background).
        ///
        /// Environment variable names follow WAHA's .env.example:
        /// WAHA_API_KEY, WHATSAPP_API_PORT, WHATSAPP_API_HOSTNAME,
        /// WHATSAPP_DEFAULT_ENGINE and WHATSAPP_FILES_FOLDER (without this WAHA
        /// tries to create "C:\tmp\whatsapp-files" and dies on boot).
        ///
        /// Warning: the command must be "set "VAR=value"" (quotes around the
        /// assignment). With "set VAR="value"" quotes stay in the value and WAHA
        /// tries to create a folder with an invalid name.
        /// </summary>
        public CommandResult StartServer(string apiKey, int port = DefaultPort, string dataFolder = null)
        {
            if (!IsBuilt()) return new CommandResult { ExitCode = -1, Output = "O WAHA ainda nao foi compilado." };

            var baseData = string.IsNullOrEmpty(dataFolder) ? InstallDir : dataFolder;
            Directory.CreateDirectory(SessionDir);
            Directory.CreateDirectory(Path.Combine(baseData, ".media"));

            return Shell.RunDetached("cmd.exe", "/c " + BuildServerCommand(apiKey, port, baseData), InstallDir);
        }

        /// <summary>
        /// Builds the command that starts the WAHA server.
        ///
        /// Warning: the command must be "set "VAR=value"" (quotes around the
        /// assignment). With "set VAR="value"" quotes stay in the value and WAHA
        /// tries to create a folder with an invalid name.
        ///
        /// Output goes to the log file (the server runs detached, outside the
        /// app, so its output cannot be read directly later).
        /// </summary>
        public string BuildServerCommand(string apiKey, int port = DefaultPort, string dataFolder = null)
        {
            var baseData = string.IsNullOrEmpty(dataFolder) ? InstallDir : dataFolder;
            var files = Path.Combine(baseData, ".media");
            var sessions = Path.Combine(baseData, ".sessions");
            var log = Path.Combine(baseData, "waha-server.log");

            var env = string.Join("&& ", new[]
            {
                Variable("WAHA_API_KEY", apiKey),
                Variable("WHATSAPP_API_HOSTNAME", "localhost"),
                Variable("WHATSAPP_API_PORT", port.ToString()),
                Variable("WHATSAPP_DEFAULT_ENGINE", "NOWEB"),
                Variable("WHATSAPP_FILES_FOLDER", files),
                Variable("WAHA_LOCAL_STORE_BASE_DIR", sessions),
                Variable("WAHA_PRINT_QR", "false"), // QR already appears in the app
                Variable("PUPPETEER_SKIP_DOWNLOAD", "true")
            }) + "&& ";

            return env + "node dist/main >> \"" + log + "\" 2>&1";
        }

        /// <summary>File where WAHA server log is stored.</summary>
        public string LogPath
        {
            get { return Path.Combine(InstallDir, "waha-server.log"); }
        }

        /// <summary>
        /// Last lines of the server log. Without this, when the server fails
        /// to start the user has no clue why.
        /// </summary>
        public string GetLastLogLines(int count = 15)
        {
            try
            {
                if (!File.Exists(LogPath)) return "";
                var lines = File.ReadAllLines(LogPath);
                if (lines.Length == 0) return "";

                var start = Math.Max(0, lines.Length - count);
                var text = string.Join(Environment.NewLine, lines, start, lines.Length - start);
                return text.Trim();
            }
            catch (Exception)
            {
                return "";
            }
        }


        /// <summary>
        /// Stops the WAHA server listening on the port.
        ///
        /// The server is started detached (not a child of the app), so stopping
        /// it requires finding the process listening on the port.
        /// </summary>
        public bool StopServer(int port = DefaultPort)
        {
            var pid = ProcessOnPort(port);
            if (pid <= 0) return false;
            try
            {
                var p = Process.GetProcessById(pid);
                p.Kill();
                p.WaitForExit(5000);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>PID of the process listening on the port (0 if none).</summary>
        public static int ProcessOnPort(int port)
        {
            var netstat = Shell.Run("netstat", "-ano", null, 30);
            if (!netstat.Ok) return 0;

            foreach (var line in netstat.Output.Replace("\r", "").Split('\n'))
            {
                if (line.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;

                // parts[1] = local address (0.0.0.0:3000, [::]:3000, ...)
                var address = parts[1];
                if (!address.EndsWith(":" + port, StringComparison.Ordinal)) continue;

                int pid;
                if (int.TryParse(parts[parts.Length - 1], out pid)) return pid;
            }
            return 0;
        }


        /// <summary>
        /// "set "NAME=value"" - quotes around the assignment (not the value)
        /// are the right way to set an environment variable in cmd.
        /// </summary>
        public static string Variable(string name, string value)
        {
            return "set \"" + name + "=" + (value ?? "") + "\"";
        }

        private static string Variavel(string nome, string valor) => Variable(nome, valor);

        private static string LastLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var lines = text.Replace("\r", "").Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var t = lines[i].Trim();
                if (t.Length > 0) return t;
            }
            return "";
        }
    }

    internal static class StringExtensions
    {
        public static string LastOrDefaultLine(this string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var lines = text.Replace("\r", "").Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var t = lines[i].Trim();
                if (t.Length > 0) return t;
            }
            return "";
        }
    }
}
