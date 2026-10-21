using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NacleFoto.App.ViewModels;

namespace NacleFoto.App.Views
{
    /// <summary>
    /// A thumbnail in the grid. Now each action has its target:
    /// - clicking the PHOTO opens in full screen
    /// - clicking the CHECKBOX (or right-clicking) marks / unmarks
    /// </summary>
    public partial class PhotoTile : UserControl
    {
        public static readonly DependencyProperty PhotoProperty =
            DependencyProperty.Register("Photo", typeof(PhotoItemViewModel), typeof(PhotoTile),
                new PropertyMetadata(null, (d, e) => ((PhotoTile)d).Update()));

        public static readonly RoutedEvent MarkedEvent = EventManager.RegisterRoutedEvent(
            "Marked", RoutingStrategy.Bubble, typeof(EventHandler), typeof(PhotoTile));

        public static readonly RoutedEvent ZoomRequestedEvent = EventManager.RegisterRoutedEvent(
            "ZoomRequested", RoutingStrategy.Bubble, typeof(EventHandler), typeof(PhotoTile));

        public event EventHandler Marked
        {
            add { AddHandler(MarkedEvent, value); }
            remove { RemoveHandler(MarkedEvent, value); }
        }

        public event EventHandler ZoomRequested
        {
            add { AddHandler(ZoomRequestedEvent, value); }
            remove { RemoveHandler(ZoomRequestedEvent, value); }
        }

        public PhotoTile()
        {
            InitializeComponent();
        }

        public PhotoItemViewModel Photo
        {
            get { return (PhotoItemViewModel)GetValue(PhotoProperty); }
            set { SetValue(PhotoProperty, value); }
        }

        private void Update()
        {
            var f = Photo;
            // DO NOT set DataContext here: it is inherited from the ItemsControl
            // container and is already the correct photo. Forcing it here broke
            // recycling of the virtualized panel (the tile was left without data).
            ToolTip = f == null ? null : f.ToolTipText;
        }


        private void BtnMarcar_Click(object sender, RoutedEventArgs e)
        {
            if (Photo == null) return;
            RaiseEvent(new RoutedEventArgs(MarkedEvent, this));
            e.Handled = true;
        }

        private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Photo == null) return;
            // clicking photo opens in full screen
            RaiseEvent(new RoutedEventArgs(ZoomRequestedEvent, this));
            e.Handled = true;
        }

        private void Tile_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Photo == null) return;
            // right-click marks / unmarks
            RaiseEvent(new RoutedEventArgs(MarkedEvent, this));
            e.Handled = true;
        }
    }
}
