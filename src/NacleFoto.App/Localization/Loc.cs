using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using NacleFoto.Core.Localization;

namespace NacleFoto.App.Localization
{
    /// <summary>
    /// Text source for XAML. When the language changes, notifies all bindings
    /// so the entire screen updates automatically.
    /// </summary>
    public sealed class LocSource : INotifyPropertyChanged
    {
        public static readonly LocSource Instance = new LocSource();

        private LocSource()
        {
            Strings.LanguageChanged += (s, e) =>
            {
                var h = PropertyChanged;
                if (h != null) h(this, new PropertyChangedEventArgs("Item[]"));
            };
        }

        public string this[string key]
        {
            get { return Strings.T(key); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// XAML usage: Text="{loc:Loc app.name}" or Text="{loc:Loc Key=app.name}".
    /// Returns the text directly. When language changes, the main window is recreated
    /// (see MainWindow.ReloadUi) so all text changes at once.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key) { Key = key; }

        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;
            return Strings.T(Key);
        }
    }

    /// <summary>Marker for namespace: xmlns:loc="clr-namespace:NacleFoto.App.Localization"</summary>
    public sealed class Loc
    {
    }
}
