using System;
using System.IO;
using System.Threading;

namespace DShNative;

/** npm-updates the global dsh; fails soft, keeps the installed version. */
public static class Updater
{
    public static void Run(Tools t, Options o, CancellationToken ct = default)
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
        }, outFile, errFile, 300_000, ct);

        if (code == 0)
        {
            Log.Info("npm auto-update finished successfully");
        }
        else if (code is -998 or -999)
        {
            Log.Warn($"npm auto-update did not finish (code {code}); continuing with the installed dsh");
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
