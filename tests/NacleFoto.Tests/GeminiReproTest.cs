using System;
using System.IO;
using NacleFoto.App;
using NacleFoto.App.Views;
using NacleFoto.App.ViewModels;
using Xunit;

namespace NacleFoto.Tests
{
    public class GeminiReproTest : IClassFixture<UiFixture>
    {
        [Fact]
        public void TestGeminiCommandLine()
        {
            var path = @"C:\Users\luizp\Downloads\Gemini_Generated_Image_olfm1yolfm1yolfm.jpeg";
            if (!File.Exists(path)) return;

            Ui.Run(() =>
            {
                var main = new MainWindow();
                main.Show();
                main.OpenFolderFromCommandLine(path);
                Assert.True(Ui.WaitUntil(() => !main.Selection.IsScanning, 15000));
                Assert.True(Ui.WaitUntil(() => main.Selection.ZoomOpenCount >= 1, 8000));
                Ui.Idle(300);
                main.Close();
            });
        }
    }
}
