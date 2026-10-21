using System;
using NacleFoto.Core.Linking;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// WAHA installation previously failed due to peer dependency conflicts
    /// (ERESOLVE). These tests verify the correct command.
    /// </summary>
    public class WahaInstallTests
    {
        [Fact]
        public void Installation_uses_yarn_first_and_npm_as_fallback()
        {
            // the WAHA project has yarn.lock: npm alone fails with ERESOLVE
            Assert.Contains("yarn", WahaManager.YarnCommand);
            Assert.Contains("corepack", WahaManager.YarnCommand);
            Assert.Contains("--legacy-peer-deps", WahaManager.LegacyNpmCommand);
        }

        [Fact]
        public void Installation_skips_chromium()
        {
            // the NOWEB engine does not need a browser: saves ~150 MB
            Assert.Contains("PUPPETEER_SKIP_DOWNLOAD", WahaManager.EnvironmentVariable);
        }

        [Fact]
        public void Installation_accepts_github_dependency_tarball()
        {
            // GitHub regenerates tarballs and the hash changes: without this, yarn aborts with
            // "The remote archive doesn't match the expected checksum"
            Assert.Contains("YARN_CHECKSUM_BEHAVIOR", WahaManager.ChecksumVariable);
            Assert.Contains("update", WahaManager.ChecksumVariable);
        }

        [Fact]
        public void Yarn_command_uses_npm_install_as_fallback()
        {
            var manager = new WahaManager(System.IO.Path.GetTempPath());
            Assert.Equal("https://github.com/devlikeapro/waha", WahaManager.Repository);
            Assert.True(manager.InstallDir.EndsWith("Waha"));
        }
    }
}
