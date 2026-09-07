using System;

namespace DShNative;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0].Equals("--apply-update", StringComparison.OrdinalIgnoreCase))
        {
            AppPaths.Ensure();
            return AppUpdate.ApplyNow(args[1]);
        }

        var opts = Options.Parse(args);
        AppPaths.Ensure();
        Log.Info($"=== DeepSeek Harness desktop start === url {opts.Url}, auto-update {(opts.NoUpdate ? "off" : "on")}, no-window {opts.NoWindow}");
        try
        {
            if (opts.SelfTest) return SelfTest.Run(opts);
            if (opts.Stop) return StopOnly.Run(opts);
            var code = Orchestrator.Run(opts);
            if (AppUpdate.PendingApply != null)
            {
                AppUpdate.ApplyAndExit(AppUpdate.PendingApply);
            }
            return code;
        }
        catch (Exception ex)
        {
            Log.Error("unhandled exception: " + ex);
            Ui.Error(opts, "Unexpected error:\n" + ex.Message);
            return 1;
        }
    }
}
