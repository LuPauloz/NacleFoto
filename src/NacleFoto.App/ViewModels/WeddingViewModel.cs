using System;
using System.Collections.Generic;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Models;

namespace NacleFoto.App.ViewModels
{
    /// <summary>A row in the grid (queue of thumbnails).</summary>
    public sealed class PhotoRowViewModel
    {
        public PhotoRowViewModel(IList<PhotoItemViewModel> photos, int columns, int rowWidth)
        {
            Photos = photos is System.Collections.ObjectModel.ObservableCollection<PhotoItemViewModel> oc
                ? oc
                : new System.Collections.ObjectModel.ObservableCollection<PhotoItemViewModel>(photos);
            Columns = columns;
            RowWidth = rowWidth;
        }

        public System.Collections.ObjectModel.ObservableCollection<PhotoItemViewModel> Photos { get; private set; }
        public int Columns { get; private set; }
        public int RowWidth { get; private set; }
    }

    /// <summary>A "label / value" row for the EXIF panel.</summary>
    public sealed class ExifRow
    {
        public ExifRow(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; private set; }
        public string Value { get; private set; }
    }

    /// <summary>A wedding in the projects list.</summary>
    public sealed class WeddingViewModel : ObservableBase
    {
        private WeddingViewModel(Wedding wedding)
        {
            Wedding = wedding;
        }

        public Wedding Wedding { get; private set; }

        public string Id { get { return Wedding.Id; } }
        public string Name { get { return Wedding.Name; } }

        public string DateText
        {
            get { return Wedding.EventDate.HasValue ? Wedding.EventDate.Value.ToString("dd/MM/yyyy") : ""; }
        }

        public string SelectedText
        {
            get { return Wedding.TotalSelected + " " + Strings.T("selection.markedPhotos"); }
        }

        public string SizeText
        {
            get { return PhotoItemViewModel.FormatSize(Wedding.SelectedSize); }
        }

        public string StateText
        {
            get
            {
                switch (Wedding.State)
                {
                    case WeddingState.Open: return Strings.T("state.open");
                    case WeddingState.Closed: return Strings.T("state.closed");
                    case WeddingState.Sent: return Strings.T("state.sent");
                }
                return "";
            }
        }

        public bool WaitingForSend { get { return Wedding.State == WeddingState.Closed; } }

        public System.Windows.Media.Brush StateBrush
        {
            get
            {
                switch (Wedding.State)
                {
                    case WeddingState.Open: return Brush(0x3D, 0xA5, 0xF5);
                    case WeddingState.Closed: return Brush(0xF0, 0xA6, 0x3C);
                    case WeddingState.Sent: return Brush(0x37, 0xC9, 0x78);
                }
                return System.Windows.Media.Brushes.Gray;
            }
        }

        private static System.Windows.Media.Brush Brush(byte r, byte g, byte b)
        {
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        }

        public static WeddingViewModel Create(Wedding wedding)
        {
            return new WeddingViewModel(wedding);
        }
    }
}
