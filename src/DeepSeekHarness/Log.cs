using System;
using System.IO;

namespace DShNative;

/**
 * Tiny logger: timestamped lines go to the log file, and to the console
 * when one is attached. Deliberately never throws - logging should not be
 * the thing that takes the app down.
 */
public static class Log
{
    private static readonly object Sync = new();
    private static readonly string FilePath = Path.Combine(AppPaths.LogsDir, "desktop.log");

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}";
        try
        {
            lock (Sync)
            {
                AppPaths.Ensure();
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // log file unwritable or whatever - not worth crashing over
        }
        try { Console.WriteLine(line); } catch { }
    }
}
