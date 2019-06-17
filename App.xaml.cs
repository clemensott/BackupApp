using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //BackupFolder folder = BackupFolder.FromPath(@"D:\Musik");
            //Backup backup = new Backup(new[] { folder });
            //string serial = Backups.Serialize(backup);
            //System.Diagnostics.Debug.WriteLine("Serial: {0}", serial.Length);
            //var backup2 = Backups.Deserialize(serial);

            //System.Diagnostics.Debug.WriteLine("Equal: " + backup2.Equals(backup2));

            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            DebugEvent.SaveText("DispatcherUnhandledException", e.Exception.ToString(), e.Exception.StackTrace);
        }
    }
}
