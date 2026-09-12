using System.Windows;

namespace GroundedMolar.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) => AppLog.Error("Unhandled UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Error("Unhandled process exception", e.ExceptionObject.ToString());
        TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Error("Unobserved task exception", e.Exception);
    }
}
