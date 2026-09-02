using System;
using System.IO;

namespace DShNative;

/** Logs to file + console (when attached). Never throws. */
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
            // logging must never crash the app
        }
        try { Console.WriteLine(line); } catch { }
    }
}
