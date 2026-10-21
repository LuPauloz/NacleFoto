using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace NacleFoto.Core.Linking
{
    /// <summary>Result of a command-line command.</summary>
    public sealed class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public bool Ok { get { return ExitCode == 0; } }
    }

    /// <summary>
    /// Executes command-line commands (node, npm, git, winget) and returns the output.
    /// </summary>
    public static class Shell
    {
        public static CommandResult Run(string executable, string arguments, string workingDirectory = null,
                                        int timeoutSeconds = 900, Action<string> onOutput = null)
        {
            var info = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory;

            var sb = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo = info;
                process.OutputDataReceived += (s, e) => { if (e.Data != null) { sb.AppendLine(e.Data); if (onOutput != null) onOutput(e.Data); } };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) { sb.AppendLine(e.Data); if (onOutput != null) onOutput(e.Data); } };
                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(timeoutSeconds * 1000))
                    {
                        try { process.Kill(); } catch { }
                        return new CommandResult { ExitCode = -1, Output = sb + "\n(tempo esgotado)" };
                    }
                    return new CommandResult { ExitCode = process.ExitCode, Output = sb.ToString() };
                }
                catch (Exception ex)
                {
                    return new CommandResult { ExitCode = -1, Output = ex.Message };
                }
            }
        }

        /// <summary>Executes a command and waits for it to finish, without capturing output.</summary>
        public static CommandResult RunDetached(string executable, string arguments, string workingDirectory = null)
        {
            var info = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory;
            try
            {
                var p = Process.Start(info);
                return new CommandResult { ExitCode = 0, Output = "iniciado" };
            }
            catch (Exception ex)
            {
                return new CommandResult { ExitCode = -1, Output = ex.Message };
            }
        }
    }
}
