using System;
using System.Windows;

namespace NacleFoto.App.Services
{
    /// <summary>
    /// Windows dialog boxes, abstracted. This allows buttons to be tested
    /// without opening any modal dialog (and without hanging the test).
    /// </summary>
    public interface IUserDialog
    {
        bool Confirm(string title, string message);
        void Info(string title, string message);
        void Error(string title, string message);
    }

    public sealed class MessageBoxDialog : IUserDialog
    {
        public bool Confirm(string title, string message)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                   == MessageBoxResult.Yes;
        }

        public void Info(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Error(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
