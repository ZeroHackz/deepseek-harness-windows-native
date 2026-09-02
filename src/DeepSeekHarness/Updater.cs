using System;
using System.IO;

namespace DShNative;

/// <summary>Auto-update of the global @deepseek-ai/dsh npm package (non-fatal).</summary>
public static class Updater
{
    public static void Run(Tools t, Options o)
    {
        Log.Info("checking for the latest @deepseek-ai/dsh on npm ...");
        if (string.IsNullOrEmpty(t.Node) || string.IsNullOrEmpty(t.NpmCli))
        {
            Log.Warn("node or npm not found; skipping auto-update");
            return;
        }

        var outFile = Path.Combine(AppPaths.LogsDir, "npm-update.out.log");
        var errFile = Path.Combine(AppPaths.LogsDir, "npm-update.err.log");
        var code = Proc.Run(t.Node, new[]
        {
            t.NpmCli, "install", "-g", "@deepseek-ai/dsh@latest",
            "--no-audit", "--no-fund", "--loglevel=error"
        }, outFile, errFile, 300_000);

        if (code == 0)
        {
            Log.Info("npm auto-update finished successfully");
        }
        else if (code == -999)
        {
            Log.Warn("npm auto-update timed out after 300s; continuing with the installed dsh");
        }
        else
        {
            var tail = "";
            try
            {
                if (File.Exists(errFile))
                {
                    var txt = File.ReadAllText(errFile);
                    tail = txt.Length > 300 ? txt[^300..] : txt;
                }
            }
            catch { }
            Log.Warn($"npm auto-update failed (exit {code}): {tail.ReplaceLineEndings(" ")}");
        }
    }
}
