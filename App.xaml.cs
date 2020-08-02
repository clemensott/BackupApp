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
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            DebugEvent.SaveText("DispatcherUnhandledException", e.Exception.ToString(), e.Exception.StackTrace);
        }
    }
}
