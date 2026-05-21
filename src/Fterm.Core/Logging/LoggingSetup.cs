using Serilog;
using Serilog.Events;

namespace Fterm.Core.Logging;

public static class LoggingSetup
{
    public static string DefaultLogDirectory()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "fterm", "logs");
    }

    /// <summary>
    /// <see cref="Log.Logger"/> を初期化する。日次ローテーション + コンソール出力。
    /// </summary>
    public static void Initialize(string? logDirectory = null, int retentionDays = 14)
    {
        var dir = logDirectory ?? DefaultLogDirectory();
        Directory.CreateDirectory(dir);
        var logPath = Path.Combine(dir, "fterm-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.WithProperty("App", "fterm")
            .WriteTo.Console()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retentionDays,
                shared: true,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
