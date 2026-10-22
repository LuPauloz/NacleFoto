using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NacleFoto.App.Views;
using NacleFoto.Core.Localization;
using NacleFoto.Core.Models;
using NacleFoto.Core.Storage;
using NacleFoto.Windows.Shell;

namespace NacleFoto.App
{
    public partial class MainWindow : Window
    {
        /// <summary>Weddings screen (public for UI tests).</summary>
        public ProjectsView Projects { get { return TelaProjetos; } }
        /// <summary>Selection screen (public for UI tests).</summary>
        public SelectionView Selection { get { return TelaSelecao; } }

        public MainWindow()
        {
            InitializeComponent();
            TelaProjetos.FolderChosen += OpenFolder;
            TelaProjetos.OpenWeddingRequested += OpenWedding;
            TelaProjetos.OpenSendRequested += OpenSend;
            TelaProjetos.OpenMultipleSendRequested += OpenSendMultiple;
            TelaProjetos.LanguageChanged += ReloadUi;
            TelaSelecao.BackRequested += BackToProjects;
            TelaSelecao.SendRequested += OpenSend;
            TelaSelecao.NewWeddingRequested += NewWedding;
        }

        /// <summary>
        /// Recreates views so that all text appears in the new language.
        /// </summary>
        public void ReloadUi()
        {
            var root = Content as Grid;
            if (root == null || root.Children.Count < 2) return;

            var projects = new ProjectsView();
            projects.FolderChosen += OpenFolder;
            projects.OpenWeddingRequested += OpenWedding;
            projects.OpenSendRequested += OpenSend;
            projects.LanguageChanged += ReloadUi;

            var selection = new SelectionView();
            selection.BackRequested += BackToProjects;
            selection.SendRequested += OpenSend;

            var visProjects = root.Children[0].Visibility;
            var visSelection = root.Children[1].Visibility;

            root.Children[0] = projects;
            root.Children[1] = selection;
            root.Children[0].Visibility = visProjects;
            root.Children[1].Visibility = visSelection;

            TelaProjetos = projects;
            TelaSelecao = selection;
        }

        public void OpenFolder(string folder)
        {
            // ALWAYS a new selection: the same folder can have multiple weddings
            // (one folder per week). Anyone wanting to resume an old selection opens
            // from the weddings list.
            var w = new Wedding
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = NameWithIndex(folder),
                Type = "Wedding",
                RootFolder = folder,
                State = WeddingState.Open,
                IncludeAllSubfolders = false,
                IncludedSubfolders = new List<string>()
            };
            TelaProjetos.Visibility = Visibility.Collapsed;
            TelaSelecao.Visibility = Visibility.Visible;
            TelaSelecao.Open(w);
        }

        /// <summary>
        /// Name of new wedding: the folder name. If one with that name already exists
        /// in this folder, it gets a number: "Memes", "Memes (2)", "Memes (3)"...
        /// </summary>
        private string NameWithIndex(string folder)
        {
            string baseName;
            try { baseName = Path.GetFileName(folder.TrimEnd('\\')); }
            catch { baseName = null; }
            if (string.IsNullOrWhiteSpace(baseName)) baseName = Strings.T("btn.newSelection");

            // names already used in this folder
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in ProjectStore.LoadRecent(App.AppDataDir))
            {
                if (string.Equals(r.RootFolder, folder, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(r.Name))
                    used.Add(r.Name);
            }

            int i = 1;
            while (used.Contains(i == 1 ? baseName : baseName + " (" + i + ")")) i++;
            return i == 1 ? baseName : baseName + " (" + i + ")";
        }

        /// <summary>New selection in the same folder (another wedding).</summary>
        public void NewWedding(Wedding previous)
        {
            if (previous == null) { BackToProjects(); return; }
            OpenFolder(previous.RootFolder);
        }


        public void OpenWedding(Wedding w)
        {
            if (w == null) return;
            TelaProjetos.Visibility = Visibility.Collapsed;
            TelaSelecao.Visibility = Visibility.Visible;
            TelaSelecao.Open(w);
        }

        public void OpenSend(Wedding w)
        {
            OpenSendMultiple(new List<Wedding> { w });
        }

        /// <summary>Opens send screen for multiple weddings at once.</summary>
        public void OpenSendMultiple(List<Wedding> weddings)
        {
            if (weddings == null || weddings.Count == 0) return;
            TelaProjetos.Visibility = Visibility.Collapsed;
            TelaSelecao.Visibility = Visibility.Collapsed;

            var window = new SendWindow();
            try { window.Owner = this; } catch (Exception) { }
            window.Open(weddings);
            window.Closed += (s, e) =>
            {
                TelaProjetos.Visibility = Visibility.Visible;
                TelaProjetos.Refresh();
            };
            window.Show();
        }

        public void OpenSendMultiplo(List<Wedding> casamentos) => OpenSendMultiple(casamentos);

        public void BackToProjects()
        {
            TelaSelecao.Visibility = Visibility.Collapsed;
            TelaProjetos.Visibility = Visibility.Visible;
            TelaProjetos.Refresh();
        }

        public void OpenFolderFromCommandLine(string path)
        {
            try
            {
                if (Directory.Exists(path)) { OpenFolder(path); return; }
                if (File.Exists(path))
                {
                    // a photo: opens the folder it is in AND the photo in full
                    // screen on top of it (Esc goes back to the grid)
                    OpenFolder(Path.GetDirectoryName(path));
                    TelaSelecao.OpenPhotoWhenReady(path);
                }
            }
            catch (Exception) { }
        }

        private static string SuggestedName(string folder)
        {
            try
            {
                var name = Path.GetFileName(folder.TrimEnd('\\'));
                if (string.IsNullOrWhiteSpace(name)) name = Strings.T("btn.newSelection");
                return name;
            }
            catch (Exception)
            {
                return Strings.T("btn.newSelection");
            }
        }
    }
}
