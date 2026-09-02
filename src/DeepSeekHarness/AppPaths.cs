using System;
using System.IO;

namespace DShNative;

/** App-data layout under %LOCALAPPDATA%\DeepSeekHarness (logs, WebView2 data, pid/lock files). */
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeepSeekHarness");

    public static string LogsDir => Path.Combine(Root, "logs");
    public static string WebView2Data => Path.Combine(Root, "webview2");
    public static string ServerPidFile => Path.Combine(Root, "server.pid");
    public static string LockFile => Path.Combine(Root, "launcher.pid");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDir);
    }
}
