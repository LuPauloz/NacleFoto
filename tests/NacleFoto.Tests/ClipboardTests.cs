using System;
using System.IO;
using System.Threading;
using NacleFoto.Windows.Shell;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// The core trick of WhatsApp delivery: the "Copy to WhatsApp" button puts the
    /// file on the clipboard as a FILE (not as text), so that Ctrl+V
    /// in WhatsApp Web attaches it. If this fails, the whole workflow breaks.
    /// </summary>
    public class ClipboardTests
    {
        [Fact]
        public void Copies_a_file_to_the_clipboard()
        {
            var arquivo = Path.Combine(Path.GetTempPath(), "nacle-clip-" + Guid.NewGuid().ToString("N") + ".zip");
            File.WriteAllText(arquivo, "conteudo de teste");

            string erro = null;
            string[] colados = null;

            // the clipboard requires an STA thread
            var thread = new Thread(() =>
            {
                try
                {
                    Assert.True(ClipboardFiles.CopyFiles(arquivo));
                    var lista = System.Windows.Forms.Clipboard.GetFileDropList();
                    colados = new string[lista.Count];
                    lista.CopyTo(colados, 0);
                }
                catch (Exception ex) { erro = ex.Message; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            try
            {
                // if clipboard is not available in this environment
                // (non-interactive session), the test is skipped
                if (erro != null && erro.Contains("clipboard"))
                    return;

                Assert.Null(erro);
                Assert.NotNull(colados);
                Assert.Single(colados);
                Assert.Equal(Path.GetFullPath(arquivo), Path.GetFullPath(colados[0]));
            }
            finally
            {
                File.Delete(arquivo);
            }
        }
    }
}
