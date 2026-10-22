using System.Configuration;
using System.Windows;
using System.Windows.Threading;
using GerberPanelizer.Build;

namespace GerberPanelizer.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private SingleInstanceCoordinator? singleInstance;
    internal bool IsUpdateInstallerStarting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceCoordinator.TryAcquire(this, out singleInstance))
        {
            Shutdown(0);
            return;
        }

        try
        {
            AppLog.Initialize(BuildStamp.Version, BuildStamp.Revision, BuildStamp.DateUtc);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            base.OnStartup(e);
            LocalizationService.Instance.SetLanguage(AppSettingsService.Load().Language);
        }
        catch (Exception exception)
        {
            AppLog.Critical("Application startup failed.", exception);
            singleInstance?.Dispose();
            singleInstance = null;
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Information($"Application exited. ExitCode={e.ApplicationExitCode}");
        try { base.OnExit(e); }
        finally
        {
            singleInstance?.Dispose();
            singleInstance = null;
        }
    }

    internal void StartUpdateInstallerShutdown()
    {
        IsUpdateInstallerStarting = true;
        Shutdown(0);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs) =>
        AppLog.Critical("Unhandled UI exception. The application will terminate.", eventArgs.Exception);

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs) =>
        AppLog.Critical($"Unhandled application exception. IsTerminating={eventArgs.IsTerminating}", eventArgs.ExceptionObject as Exception);

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs) =>
        AppLog.Error("Unobserved background task exception.", eventArgs.Exception);
}
