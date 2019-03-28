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
            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            int threadID = Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("DispatcherUnhandledException", "ThreadID: " + threadID, e.Exception.ToString());
        }
    }
}
