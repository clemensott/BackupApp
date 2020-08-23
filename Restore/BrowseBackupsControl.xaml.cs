using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BackupApp.Helper;
using StdOttFramework;
using StdOttStandard;
using StdOttStandard.Linq;

namespace BackupApp.Restore
{
    /// <summary>
    /// Interaktionslogik für BrowseBackupsControl.xaml
    /// </summary>
    public partial class BrowseBackupsControl : UserControl
    {
        public static readonly DependencyProperty SrcFolderPathProperty =
            DependencyProperty.Register(nameof(SrcFolderPath), typeof(string), typeof(BrowseBackupsControl),
                new PropertyMetadata(OnSrcFolderPathPropertyChanged));

        private async static void OnSrcFolderPathPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BrowseBackupsControl s = (BrowseBackupsControl)sender;
            await s.ReloadBackups();
        }

        public static readonly DependencyProperty SelectedFolderPathProperty =
            DependencyProperty.Register(nameof(SelectedFolderPath), typeof(string), typeof(BrowseBackupsControl),
                new PropertyMetadata(string.Empty, OnSelectedFolderPathPropertyChanged));

        private async static void OnSelectedFolderPathPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BrowseBackupsControl s = (BrowseBackupsControl)sender;
            string value = (string)e.NewValue;

            s.btnUp.IsEnabled = !string.IsNullOrWhiteSpace(value);
            await s.UpdateSelectedFolderPath();
        }

        private static readonly DependencyPropertyKey SelectedFolderPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(SelectedFolder), typeof(BackupFolder),
                typeof(BrowseBackupsControl), new PropertyMetadata(default(BackupFolder)));

        public static readonly DependencyProperty SelectedFolderProperty = SelectedFolderPropertyKey.DependencyProperty;

        private int reloadingCount = 0;
        private BackupFolder[] baseNodes;
        private TreeViewItem currentSelectedTvi;

        public event EventHandler<BackupFolder> Restore;

        public string SrcFolderPath
        {
            get => (string)GetValue(SrcFolderPathProperty);
            set => SetValue(SrcFolderPathProperty, value);
        }

        public string SelectedFolderPath
        {
            get => (string)GetValue(SelectedFolderPathProperty);
            set => SetValue(SelectedFolderPathProperty, value);
        }

        public BackupFolder SelectedFolder
        {
            get => (BackupFolder)GetValue(SelectedFolderProperty);
            private set => SetValue(SelectedFolderPropertyKey, value);
        }

        public BrowseBackupsControl()
        {
            InitializeComponent();

            SelectedFolderPath = string.Empty;
        }

        public async Task ReloadBackups()
        {
            string folderPath = SrcFolderPath;
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            int currentCount = ++reloadingCount;
            gidMain.IsEnabled = false;

            try
            {
                BackupFolder[] backups = await Task.Run(
                    () => Task.WhenAll(BackupUtils.GetReadDBs(folderPath).ToNotNull().Select(GetLoadedFolder)));

                if (currentCount == reloadingCount) tvwFolders.ItemsSource = baseNodes = backups.Where(b => b != null).ToArray();
            }
            finally
            {
                if (currentCount == reloadingCount) gidMain.IsEnabled = true;
            }
        }

        private static async Task<BackupFolder> GetLoadedFolder(BackupReadDb db)
        {
            try
            {
                BackupFolder folder = db.AsFolder();
                await folder.LoadFolders();
                return folder;
            }
            catch
            {
                return null;
            }
        }

        private async Task UpdateSelectedFolderPath()
        {
            BackupFolder node;
            TreeViewItem tvi;
            if (TryGetNode(out node, out tvi))
            {
                await node.LoadFolders();
                await node.LoadFiles();

                lbxSelectedFolder.ItemsSource = node.Folders.ToNotNull().Concat(node.Files.ToNotNull().Cast<object>());

                lbxSelectedFolder.SelectedItems.Clear();
                if (SelectedFolder != null) lbxSelectedFolder.SelectedItems.Add(SelectedFolder);
                SelectedFolder = node;

                if (currentSelectedTvi != null) currentSelectedTvi.IsSelected = false;
                currentSelectedTvi = tvi;
                if (currentSelectedTvi != null) currentSelectedTvi.IsSelected = true;
            }
            else
            {
                lbxSelectedFolder.ItemsSource = null;
                SelectedFolderPath = string.Empty;

                if (currentSelectedTvi != null) currentSelectedTvi.IsSelected = false;

                SelectedFolder = null;
                currentSelectedTvi = null;
            }
        }

        private bool TryGetNode(out BackupFolder node, out TreeViewItem tvi)
        {
            node = null;
            tvi = null;

            if (baseNodes == null || baseNodes.Length == 0) return false;

            string[] parts = SelectedFolderPath?.Trim('\\').Split('\\');

            if (parts == null || parts.Length == 0) return false;
            if (TryGetNode(baseNodes, tvwFolders.ItemContainerGenerator, parts, out node, out tvi)) return true;

            node = null;
            return false;
        }

        private static bool TryGetNode(IEnumerable<BackupFolder> nodes, ItemContainerGenerator generator,
            IEnumerable<string> parts, out BackupFolder node, out TreeViewItem tvi)
        {
            BackupFolder tmpNode;
            TreeViewItem tmpTvi;
            node = null;
            tvi = null;

            foreach (string part in parts)
            {
                if (!nodes.ToNotNull().TryFirst(n => n.Name == part, out tmpNode)) return false;

                tmpTvi = generator?.ContainerFromItem(tmpNode) as TreeViewItem;

                node = tmpNode;
                nodes = tmpNode.Folders;

                tvi = tmpTvi;
                generator = tmpTvi?.ItemContainerGenerator;
            }

            return true;
        }

        private void TvwFolders_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            BackupFolder node = (BackupFolder)e.NewValue;

            string path;
            if (TryGetNodePath(node, out path)) SelectedFolderPath = path;
        }

        private bool TryGetNodePath(BackupFolder node, out string path)
        {
            foreach (BackupFolder baseNode in baseNodes.ToNotNull())
            {
                if (TryGetNodePath(baseNode, string.Empty, node, out path)) return true;
            }

            path = null;
            return false;
        }

        private static bool TryGetNodePath(BackupFolder currentNode,
            string currentPath, BackupFolder searchNode, out string outPath)
        {
            currentPath = Path.Combine(currentPath, currentNode.Name);

            if (ReferenceEquals(currentNode, searchNode))
            {
                outPath = currentPath.Trim('\\');
                return true;
            }

            foreach (BackupFolder folder in currentNode.Folders.ToNotNull())
            {
                if (TryGetNodePath(folder, currentPath, searchNode, out outPath)) return true;
            }

            outPath = null;
            return false;
        }

        private async void TbxNodeName_Loaded(object sender, RoutedEventArgs e)
        {
            BackupFolder node = FrameworkUtils.GetDataContext<BackupFolder>(sender);
            await node.LoadFolders();
        }

        private void LbxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            object item = ((FrameworkElement)sender).DataContext;

            if (e.ChangedButton == MouseButton.Left && item is BackupFolder node)
            {
                SelectedFolderPath = Path.Combine(SelectedFolderPath, node.Name);
            }
        }

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFolderPath.Contains('\\')) SelectedFolderPath = Path.GetDirectoryName(SelectedFolderPath);
            else SelectedFolderPath = null;
        }

        private void BtnRestoreTree_Click(object sender, RoutedEventArgs e)
        {
            Restore?.Invoke(this, SelectedFolder);
        }

        private void BtnRestoreLbx_Click(object sender, RoutedEventArgs e)
        {
            BackupFolder[] folders = lbxSelectedFolder.SelectedItems.OfType<BackupFolder>().ToArray();
            BackupFile[] files = lbxSelectedFolder.SelectedItems.OfType<BackupFile>().ToArray();
            BackupFolder node = new BackupFolder(-1, "Restore", SelectedFolder.DB)
            {
                Folders = folders,
                Files = files
            };

            Restore?.Invoke(this, node);
        }

        private async void BtnRefreshBackups_Click(object sender, RoutedEventArgs e)
        {
            await ReloadBackups();
        }
    }
}
