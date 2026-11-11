using FreeTeachClass.Infrastructure.Storage;

namespace FreeTeachClass.App;

/// <summary>
/// Last-resort crash log for exceptions the XAML runtime turns into a process
/// crash. Writes the exception detail to %LOCALAPPDATA%\FreeTeachClass\crash.log
/// so failures are diagnosable without a debugger. Never logs tokens or
/// personal data — only exception details.
/// </summary>
public static class CrashLog
{
    private static readonly object Sync = new();

    /// <summary>
    /// Registers process-wide handlers. Must be called as early as possible,
    /// before the DI container or any XAML is created.
    /// </summary>
    public static void Attach()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Write("AppDomain", args.ExceptionObject as Exception ?? null,
                args.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write("TaskScheduler", args.Exception, isTerminating: false);
            args.SetObserved();
        };
    }

    public static void Write(string source, Exception? exception, bool isTerminating)
    {
        if (exception is null)
        {
            return;
        }

        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(AppDirectories.Root);
                var path = Path.Combine(AppDirectories.Root, "crash.log");
                File.AppendAllText(
                    path,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] " +
                    $"(terminating={isTerminating}){Environment.NewLine}" +
                    exception + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception)
            {
                // The crash log must never cause a second failure.
            }
        }
    }
}
