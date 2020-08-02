using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BackupApp.Restore.Caching;
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
        public static readonly DependencyProperty DBProperty =
            DependencyProperty.Register(nameof(DB), typeof(RestoreDb), typeof(BrowseBackupsControl),
                new PropertyMetadata(OnDBPropertyChanged));

        private async static void OnDBPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BrowseBackupsControl s = (BrowseBackupsControl)sender;
            await s.ReloadBackups();
        }

        public static readonly DependencyProperty SelectedFolderProperty =
            DependencyProperty.Register("SelectedFolder", typeof(string), typeof(BrowseBackupsControl),
                new PropertyMetadata(null, OnSelectedFolderPropertyChanged));

        private async static void OnSelectedFolderPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BrowseBackupsControl s = (BrowseBackupsControl)sender;
            string value = (string)e.NewValue;

            s.btnUp.IsEnabled = !string.IsNullOrWhiteSpace(value);
            await s.UpdateSelectedFolder();
        }

        private int reloadingCount = 0;
        private RestoreNode[] baseNodes;
        private RestoreNode currentSelectedNode;
        private TreeViewItem currentSelectedTvi;

        public event EventHandler<RestoreNode> Restore;

        public RestoreDb DB
        {
            get => (RestoreDb)GetValue(DBProperty);
            set => SetValue(DBProperty, value);
        }

        public string SelectedFolder
        {
            get => (string)GetValue(SelectedFolderProperty);
            set => SetValue(SelectedFolderProperty, value);
        }

        public BrowseBackupsControl()
        {
            InitializeComponent();

            SelectedFolder = string.Empty;
        }

        public async Task ReloadBackups()
        {
            RestoreDb db = DB;
            if (db == null) return;

            reloadingCount++;
            gidMain.IsEnabled = false;

            IEnumerable<RestoreNode> backups = await db.GetBackups();
            if (db == DB) tvwFolders.ItemsSource = baseNodes = backups?.ToArray();

            if (--reloadingCount == 0) gidMain.IsEnabled = true;
        }

        private async Task UpdateSelectedFolder()
        {
            RestoreNode node;
            TreeViewItem tvi;
            if (TryGetNode(out node, out tvi))
            {
                if (node.Folders == null) node.Folders = (await DB.GetFolders(node))?.ToArray();
                if (node.Files == null) node.Files = (await DB.GetFiles(node))?.ToArray();

                lbxSelectedFolder.ItemsSource = node.Folders.ToNotNull().Concat(node.Files.ToNotNull().Cast<object>());

                lbxSelectedFolder.SelectedItems.Clear();
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

        private bool TryGetNode(out RestoreNode node, out TreeViewItem tvi)
        {
            node = null;
            tvi = null;

            if (baseNodes == null || baseNodes.Length == 0) return false;

            string[] parts = SelectedFolder?.Trim('\\').Split('\\');

            if (parts == null || parts.Length == 0) return false;
            if (TryGetNode(baseNodes, tvwFolders.ItemContainerGenerator, parts, out node, out tvi)) return true;

            node = null;
            return false;
        }

        private static bool TryGetNode(IEnumerable<RestoreNode> nodes, ItemContainerGenerator generator,
            IEnumerable<string> parts, out RestoreNode node, out TreeViewItem tvi)
        {
            RestoreNode tmpNode;
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
            RestoreNode node = (RestoreNode)e.NewValue;

            string path;
            if (TryGetNodePath(node, out path)) SelectedFolder = path;
        }

        private bool TryGetNodePath(RestoreNode node, out string path)
        {
            foreach (RestoreNode baseNode in baseNodes.ToNotNull())
            {
                if (TryGetNodePath(baseNode, string.Empty, node, out path)) return true;
            }

            path = null;
            return false;
        }

        private static bool TryGetNodePath(RestoreNode currentNode,
            string currentPath, RestoreNode searchNode, out string outPath)
        {
            currentPath = Path.Combine(currentPath, currentNode.Name);

            if (ReferenceEquals(currentNode, searchNode))
            {
                outPath = currentPath.Trim('\\');
                return true;
            }

            foreach (RestoreNode folder in currentNode.Folders.ToNotNull())
            {
                if (TryGetNodePath(folder, currentPath, searchNode, out outPath)) return true;
            }

            outPath = null;
            return false;
        }

        private async void TbxNodeName_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreNode node = FrameworkUtils.GetDataContext<RestoreNode>(sender);
            if (node.Folders == null) node.Folders = (await DB.GetFolders(node)).ToArray();
        }

        private void LbxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            object item = ((FrameworkElement)sender).DataContext;

            if (e.ChangedButton == MouseButton.Left && item is RestoreNode node)
            {
                SelectedFolder = Path.Combine(SelectedFolder, node.Name);
            }
        }

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFolder.Contains('\\')) SelectedFolder = Path.GetDirectoryName(SelectedFolder);
            else SelectedFolder = null;
        }

        private async void BtnRestoreTree_Click(object sender, RoutedEventArgs e)
        {
            RestoreNode node = (RestoreNode)tvwFolders.SelectedItem;

            await LoadAllFiles(node);

            Restore?.Invoke(this, node);
        }

        private async void BtnRestoreLbx_Click(object sender, RoutedEventArgs e)
        {
            RestoreNode[] folders = lbxSelectedFolder.SelectedItems.OfType<RestoreNode>().ToArray();
            RestoreFile[] files = lbxSelectedFolder.SelectedItems.OfType<RestoreFile>().ToArray();
            RestoreNode node = new RestoreNode(-1, "Restore", RestoreNodeType.Folder)
            {
                Folders = folders,
                Files = files
            };

            await LoadAllFiles(node);

            Restore?.Invoke(this, node);
        }

        private async Task LoadAllFiles(RestoreNode node)
        {
            Queue<RestoreNode> nodes = new Queue<RestoreNode>();
            nodes.Enqueue(node);

            while (nodes.Count > 0)
            {
                node = nodes.Dequeue();

                if (node.Files == null) node.Files = (await DB.GetFiles(node))?.ToArray();
                if (node.Folders == null) node.Folders = (await DB.GetFolders(node))?.ToArray();

                foreach (RestoreNode folder in node.Folders.ToNotNull())
                {
                    nodes.Enqueue(folder);
                }
            }
        }

        private async void BtnRefreshBackups_Click(object sender, RoutedEventArgs e)
        {
            await ReloadBackups();
        }
    }
}
