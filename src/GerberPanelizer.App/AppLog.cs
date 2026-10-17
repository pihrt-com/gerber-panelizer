using System.IO;
using System.Text;

namespace GerberPanelizer.App;

internal static class AppLog
{
    private const long MaximumLogBytes = 2L * 1024 * 1024;
    private static readonly object SyncRoot = new();
    private static readonly string FallbackLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GerberPanelizer", "logs");
    private static string? logDirectory;

    internal static string LogFilePath => Path.Combine(ResolveLogDirectory(), "gerber-panelizer.log");
    internal static string PreviousLogFilePath => Path.Combine(ResolveLogDirectory(), "gerber-panelizer.previous.log");

    internal static void Initialize(string version, string revision, string buildDateUtc) =>
        Write("INFO", $"Application started. Version={version}; Revision={revision}; Build={buildDateUtc}; Executable={Environment.ProcessPath}; OS={Environment.OSVersion}; Runtime={Environment.Version}");

    internal static void Information(string message) => Write("INFO", message);
    internal static void Warning(string message, Exception? exception = null) => Write("WARN", message, exception);
    internal static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);
    internal static void Critical(string message, Exception? exception = null) => Write("CRITICAL", message, exception);

    private static void Write(string level, string message, Exception? exception = null)
    {
        try
        {
            lock (SyncRoot)
            {
                var directory = ResolveLogDirectory();
                Directory.CreateDirectory(directory);
                RotateIfNeeded();
                var entry = new StringBuilder()
                    .Append(DateTimeOffset.UtcNow.ToString("O"))
                    .Append(" [").Append(level).Append("] [thread ").Append(Environment.CurrentManagedThreadId).Append("] ")
                    .AppendLine(message);
                if (exception is not null) entry.AppendLine(exception.ToString());
                File.AppendAllText(LogFilePath, entry.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Logging must never replace the original application error with a logging failure.
        }
    }

    private static string ResolveLogDirectory()
    {
        if (logDirectory is not null) return logDirectory;
        var applicationDirectory = AppContext.BaseDirectory;
        if (CanWriteTo(applicationDirectory)) return logDirectory = applicationDirectory;
        return logDirectory = FallbackLogDirectory;
    }

    private static bool CanWriteTo(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".gerber-panelizer-write-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogFilePath) || new FileInfo(LogFilePath).Length < MaximumLogBytes) return;
        File.Move(LogFilePath, PreviousLogFilePath, overwrite: true);
    }
}
