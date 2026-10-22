using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;

namespace GerberPanelizer.App;

/// <summary>Owns the per-user application mutex and forwards second launches to the existing window.</summary>
internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexPrefix = @"Global\GerberPanelizer.SingleInstance.";
    private const string ActivationPrefix = @"Global\GerberPanelizer.Activate.";
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task listener;
    private bool disposed;

    private SingleInstanceCoordinator(Mutex mutex, EventWaitHandle activationEvent, Application application)
    {
        this.mutex = mutex;
        this.activationEvent = activationEvent;
        listener = Task.Run(() => Listen(application));
    }

    public static bool TryAcquire(Application application, out SingleInstanceCoordinator? coordinator)
    {
        ArgumentNullException.ThrowIfNull(application);
        var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var mutexName = MutexPrefix + identity;
        var activationName = ActivationPrefix + identity;
        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            SignalExistingInstance(activationName);
            coordinator = null;
            return false;
        }

        var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activationName);
        coordinator = new SingleInstanceCoordinator(mutex, activationEvent, application);
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cancellation.Cancel();
        activationEvent.Set();
        try { listener.Wait(TimeSpan.FromSeconds(1)); }
        catch (AggregateException) { }
        activationEvent.Dispose();
        mutex.ReleaseMutex();
        mutex.Dispose();
        cancellation.Dispose();
    }

    private static void SignalExistingInstance(string activationName)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(activationName);
                activationEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private void Listen(Application application)
    {
        while (!cancellation.IsCancellationRequested)
        {
            activationEvent.WaitOne();
            if (cancellation.IsCancellationRequested) return;
            application.Dispatcher.BeginInvoke(ActivateMainWindow, DispatcherPriority.ApplicationIdle);
        }
    }

    private static void ActivateMainWindow()
    {
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault();
        if (window is null)
        {
            Application.Current.Dispatcher.BeginInvoke(ActivateMainWindow, DispatcherPriority.ApplicationIdle);
            return;
        }

        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
