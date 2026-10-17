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
    protected override void OnStartup(StartupEventArgs e)
    {
        AppLog.Initialize(BuildStamp.Version, BuildStamp.Revision, BuildStamp.DateUtc);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        try
        {
            base.OnStartup(e);
            LocalizationService.Instance.SetLanguage(AppSettingsService.Load().Language);
        }
        catch (Exception exception)
        {
            AppLog.Critical("Application startup failed.", exception);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Information($"Application exited. ExitCode={e.ApplicationExitCode}");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs) =>
        AppLog.Critical("Unhandled UI exception. The application will terminate.", eventArgs.Exception);

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs) =>
        AppLog.Critical($"Unhandled application exception. IsTerminating={eventArgs.IsTerminating}", eventArgs.ExceptionObject as Exception);

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs) =>
        AppLog.Error("Unobserved background task exception.", eventArgs.Exception);
}
