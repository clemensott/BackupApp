using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StdOttFramework;

namespace BackupApp
{
    class FolderFileIconConverter : IValueConverter
    {
        private static ImageSource folderImg, fileImg;

        private static ImageSource GetFolderImage()
        {
            if (folderImg != null) return folderImg;

            try
            {
                string fullPath = Utils.GetFullPath("genericFolderThumbnail.png");
                return folderImg = new BitmapImage(new Uri(fullPath));
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource GetFileImage()
        {
            if (fileImg != null) return fileImg;

            try
            {
                string fullPath = Utils.GetFullPath("genericFileThumbnail.png");
                return fileImg = new BitmapImage(new Uri(fullPath));
            }
            catch
            {
                return null;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is IBackupNode ? GetFolderImage() : GetFileImage();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
