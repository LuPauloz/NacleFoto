using System.Windows;
using NacleFoto.Core.Localization;

namespace NacleFoto.App.Views
{
    /// <summary>What to do after closing a wedding selection.</summary>
    public enum AfterCloseAction
    {
        SendNow,
        NewSelection,
        Later
    }

    /// <summary>
    /// Asks what to do after closing the selection: send now, start another
    /// selection (another wedding in the same folder) or leave for later.
    /// </summary>
    public partial class AfterCloseDialog : Window
    {
        public AfterCloseDialog(string summary)
        {
            InitializeComponent();
            Title = Strings.T("app.name");
            TxtResumo.Text = summary;
        }

        public AfterCloseAction Result { get; private set; } = AfterCloseAction.Later;

        private void BtnSendNow_Click(object sender, RoutedEventArgs e)
        {
            Result = AfterCloseAction.SendNow;
            DialogResult = true;
        }

        private void BtnNovo_Click(object sender, RoutedEventArgs e)
        {
            Result = AfterCloseAction.NewSelection;
            DialogResult = true;
        }

        private void BtnDepois_Click(object sender, RoutedEventArgs e)
        {
            Result = AfterCloseAction.Later;
            DialogResult = true;
        }
    }
}
