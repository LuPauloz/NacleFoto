using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NacleFoto.App.ViewModels;
using NacleFoto.App.Services;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Shell;

namespace NacleFoto.App.Views
{
    /// <summary>Home screen: list of weddings and the button to start a selection.</summary>
    public partial class ProjectsView : UserControl
    {
        public event Action<string> FolderChosen;

        public event Action<Wedding> OpenWeddingRequested;

        public event Action<Wedding> OpenSendRequested;

        /// <summary>Multiple weddings at once (two weddings on the same day).</summary>
        public event Action<List<Wedding>> OpenMultipleSendRequested;

        /// <summary>Fires when the language changes, so the window can recreate text.</summary>
        public event Action LanguageChanged;

        /// <summary>Injectable for testing: folder picker does not open a real dialog.</summary>
        public IFolderPicker FolderPicker { get; set; } = new FolderBrowserPicker();

        /// <summary>Injectable for testing: dialog boxes become automated responses.</summary>
        public IUserDialog Dialog { get; set; } = new MessageBoxDialog();

        private readonly List<WeddingViewModel> _list = new List<WeddingViewModel>();

        public ProjectsView()
        {
            InitializeComponent();
            Refresh();
        }

        public void Refresh()
        {
            _list.Clear();
            var recents = ProjectStore.LoadRecent(App.AppDataDir);
            foreach (var r in recents)
            {
                var c = ProjectStore.Load(r.FilePath);
                if (c != null) _list.Add(WeddingViewModel.Create(c));
            }
            List.ItemsSource = null;
            List.ItemsSource = _list;
            List.Items.Refresh();

            EmptyState.Visibility = _list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ItemCount.Text = _list.Count == 0 ? "" : Strings.T("projects.count", _list.Count);
            UpdateOpenButton();
        }

        private void UpdateOpenButton()
        {
            var vm = List.SelectedItem as WeddingViewModel;
            if (vm == null) { BtnOpen.Content = Strings.T("btn.openSelection"); return; }
            BtnOpen.Content = vm.Wedding.State == WeddingState.Closed
                ? Strings.T("send.title")
                : Strings.T("btn.openSelection");
        }


        /// <summary>
        /// Closed weddings among those selected in the list. This is how multiple
        /// weddings are sent at once (two weddings on the same day).
        /// </summary>
        private List<Wedding> GetSelectedClosedWeddings()
        {
            var list = new List<Wedding>();
            foreach (var item in List.SelectedItems)
            {
                var vm = item as WeddingViewModel;
                if (vm != null && vm.Wedding.State == WeddingState.Closed) list.Add(vm.Wedding);
            }
            return list;
        }


        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            var closed = GetSelectedClosedWeddings();
            if (closed.Count == 0)
            {
                Dialog.Info(Strings.T("app.name"), Strings.T("send.nothingToSend"));
                return;
            }
            if (OpenMultipleSendRequested != null) OpenMultipleSendRequested(closed);
            else if (OpenSendRequested != null) OpenSendRequested(closed[0]);
        }

        private void BtnNewSelection_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderPicker.Pick(Strings.T("btn.newSelection"), LastFolder());
            if (string.IsNullOrEmpty(folder)) return;
            if (FolderChosen != null) FolderChosen(folder);
        }

        private string LastFolder()
        {
            var recents = ProjectStore.LoadRecent(App.AppDataDir);
            foreach (var r in recents)
                if (!string.IsNullOrEmpty(r.RootFolder) && System.IO.Directory.Exists(r.RootFolder))
                    return r.RootFolder;
            return null;
        }

        private void List_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var vm = List.SelectedItem as WeddingViewModel;
            if (vm == null) return;

            // double click opens the SELECTION (to continue editing, even after sent)
            if (vm.Wedding.State == WeddingState.Sent)
            {
                if (!Dialog.Confirm(Strings.T("app.name"), Strings.T("send.alreadySent"))) return;
            }
            if (OpenWeddingRequested != null) OpenWeddingRequested(vm.Wedding);
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenSelected();
        }

        private void OpenSelected()
        {
            var vm = List.SelectedItem as WeddingViewModel;
            if (vm == null) return;

            if (vm.Wedding.State == WeddingState.Closed)
            {
                if (OpenSendRequested != null) OpenSendRequested(vm.Wedding);
                return;
            }
            if (OpenWeddingRequested != null) OpenWeddingRequested(vm.Wedding);
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var vm = List.SelectedItem as WeddingViewModel;
            if (vm == null) return;
            if (!Dialog.Confirm(Strings.T("app.name"), Strings.T("msg.removeConfirm", vm.Name))) return;
            ProjectStore.RemoveRecent(vm.Id, App.AppDataDir);
            Refresh();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var prevLang = App.Settings.AppLanguage;
            var window = new SettingsWindow(App.Settings);
            if (window.ShowDialog() == true)
            {
                Strings.Language = App.Settings.AppLanguage;
                Refresh();
                if (App.Settings.AppLanguage != prevLang && LanguageChanged != null) LanguageChanged();
            }
        }
    }
}
