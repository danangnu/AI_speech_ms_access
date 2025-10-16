using System.Windows;

namespace RealtimeSTTHost
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Cli.Run(e.Args);
            Shutdown(); // window controls shutdown
        }
    }
}
