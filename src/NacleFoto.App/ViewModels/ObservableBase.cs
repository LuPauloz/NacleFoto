using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NacleFoto.App.ViewModels
{
    public abstract class ObservableBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string propertyName = null)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void Raise(params string[] properties)
        {
            if (properties == null) return;
            foreach (var p in properties)
            {
                var h = PropertyChanged;
                if (h != null) h(this, new PropertyChangedEventArgs(p));
            }
        }
    }
}
