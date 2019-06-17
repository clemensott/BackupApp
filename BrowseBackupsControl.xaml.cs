using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StdOttStandard;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für BrowseBackupsControl.xaml
    /// </summary>
    public partial class BrowseBackupsControl : UserControl
    {
        public static readonly DependencyProperty SrcFolderProperty =
            DependencyProperty.Register("SrcFolder", typeof(string), typeof(BrowseBackupsControl),
                new PropertyMetadata(null, OnSrcFolderPropertyChanged));

        private static async void OnSrcFolderPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BrowseBackupsControl s = (BrowseBackupsControl)sender;

            await s.UpdateBackups();
        }

        public static readonly DependencyProperty SelectedFolderProperty =
            DependencyProperty.Register("SelectedFolder", typeof(string), typeof(BrowseBackupsControl),
                new PropertyMetadata(null, OnSelectedFolderPropertyChanged));

        private static void OnSelectedFolderPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BrowseBackupsControl s = (BrowseBackupsControl)sender;
            string value = (string)e.NewValue;

            s.btnUp.IsEnabled = !string.IsNullOrWhiteSpace(value);
            s.UpdateSelectedFolder();
        }

        private Backup[] backups;
        private IBackupNode currentSelectedNode;
        private TreeViewItem currentSelectedTvi;

        public string SrcFolder
        {
            get => (string)GetValue(SrcFolderProperty);
            set => SetValue(SrcFolderProperty, value);
        }

        public string SelectedFolder
        {
            get => (string)GetValue(SelectedFolderProperty);
            set => SetValue(SelectedFolderProperty, value);
        }

        public BrowseBackupsControl()
        {
            InitializeComponent();

            SrcFolder = string.Empty;
            SelectedFolder = string.Empty;
        }

        public async Task UpdateBackups()
        {
            gidMain.IsEnabled = false;

            try
            {
                backups = string.IsNullOrWhiteSpace(SrcFolder) ?
                    null : (await BackupUtils.GetBackups(SrcFolder)).ToArray();
            }
            catch
            {
                backups = null;
            }

            tvwFolders.ItemsSource = backups;

            UpdateSelectedFolder();

            gidMain.IsEnabled = true;
        }

        private void UpdateSelectedFolder()
        {
            IBackupNode node;
            TreeViewItem tvi;
            if (TryGetNode(out node, out tvi))
            {
                lbxSelectedFolder.ItemsSource = node.Folders.Concat(node.Files.Cast<object>());

                lbxSelectedFolder.SelectedItems.Add(currentSelectedNode);
                currentSelectedNode = node;

                if (currentSelectedTvi != null) currentSelectedTvi.IsSelected = false;
                currentSelectedTvi = tvi;
                if (currentSelectedTvi != null) currentSelectedTvi.IsSelected = true;
            }
            else
            {
                lbxSelectedFolder.ItemsSource = null;
                SelectedFolder = string.Empty;

                if (currentSelectedTvi != null) currentSelectedTvi.IsSelected = false;

                currentSelectedNode = null;
                currentSelectedTvi = null;
            }
        }

        private bool TryGetNode(out IBackupNode node, out TreeViewItem tvi)
        {
            node = null;
            tvi = null;

            if (backups == null || backups.Length == 0) return false;

            string[] parts = SelectedFolder?.Trim('\\').Split('\\');

            if (parts == null || parts.Length == 0) return false;
            if (TryGetNode(backups, tvwFolders.ItemContainerGenerator, parts, out node, out tvi)) return true;

            node = null;
            return false;
        }

        private static bool TryGetNode(IEnumerable<IBackupNode> nodes, ItemContainerGenerator generator,
            IEnumerable<string> parts, out IBackupNode node, out TreeViewItem tvi)
        {
            IBackupNode tmpNode;
            TreeViewItem tmpTvi;
            node = null;
            tvi = null;

            foreach (string part in parts)
            {
                if (!nodes.TryFirst(n => n.Name == part, out tmpNode)) return false;

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
            IBackupNode node = (IBackupNode)e.NewValue;

            string path;
            if (TryGetNodePath(node, out path)) SelectedFolder = path;
        }

        private bool TryGetNodePath(IBackupNode node, out string path)
        {
            foreach (Backup backup in backups.ToNotNull())
            {
                if (TryGetNodePath(backup, "", node, out path)) return true;
            }

            path = null;
            return false;
        }

        private static bool TryGetNodePath(IBackupNode currentNode,
            string currentPath, IBackupNode searchNode, out string outPath)
        {
            currentPath += "\\" + currentNode.Name;

            if (ReferenceEquals(currentNode, searchNode))
            {
                outPath = currentPath.Trim('\\');
                return true;
            }

            foreach (BackupFolder folder in currentNode.Folders)
            {
                if (TryGetNodePath(folder, currentPath, searchNode, out outPath)) return true;
            }

            outPath = null;
            return false;
        }

        private void LbxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            object item = ((FrameworkElement)sender).DataContext;

            if (e.ChangedButton == MouseButton.Left && item is BackupFolder folder)
            {
                SelectedFolder = Path.Combine(SelectedFolder, folder.Name);
            }
        }

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFolder.Contains('\\')) SelectedFolder = Path.GetDirectoryName(SelectedFolder);
            else SelectedFolder = null;
        }

        private void BtnRestoreTree_Click(object sender, RoutedEventArgs e)
        {
            IBackupNode node = (IBackupNode)tvwFolders.SelectedItem;

            WindowManager.Current.SetRestoreTask(RestoreTask.Run(SrcFolder, node));
        }

        private void BtnRestoreLbx_Click(object sender, RoutedEventArgs e)
        {
            BackupFolder[] folders = lbxSelectedFolder.SelectedItems.OfType<BackupFolder>().ToArray();
            BackupFile[] files = lbxSelectedFolder.SelectedItems.OfType<BackupFile>().ToArray();
            IBackupNode node = new BackupFolder("Restore", folders, files);

            WindowManager.Current.SetRestoreTask(RestoreTask.Run(SrcFolder, node));
        }

        private async void BtnRefreshBackups_Click(object sender, RoutedEventArgs e)
        {
            await UpdateBackups();
        }
    }
}
