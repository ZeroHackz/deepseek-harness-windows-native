using System;
using System.Threading;
using System.Windows.Forms;

namespace DShNative;

/** Update dsh, attach to or boot the server, show the window, stop on close. */
public static class Orchestrator
{
    public static int Run(Options o)
    {
        var tools = Tools.Discover();

        if (tools.Node == null)
        {
            Ui.Error(o, "Node.js was not found on PATH.\nInstall it from https://nodejs.org and try again.\n\nTarget: " + o.Url);
            return 10;
        }
        if (tools.DshMissing)
        {
            if (o.NoUpdate)
            {
                Ui.Error(o, "@deepseek-ai/dsh is not installed globally.\nRun:  npm install -g @deepseek-ai/dsh\n(or launch without --no-update so the app installs it).");
                return 11;
            }
            Log.Warn("@deepseek-ai/dsh is not installed; installing it now (this can take a minute) ...");
            Updater.Run(tools, o);
            tools = Tools.Discover();
            if (tools.DshMissing)
            {
                Ui.Error(o, "Auto-install of @deepseek-ai/dsh failed. Check the logs and your network, then run:\n  npm install -g @deepseek-ai/dsh");
                return 12;
            }
        }

        // ATTACH: something already serves the port
        if (NetProbe.IsOpen(o.Address, o.Port))
        {
            Log.Info($"port {o.Port} is already in use -> ATTACH mode (no update; the server is left untouched)");
            if (o.NoWindow)
            {
                Log.Info("attach mode with --no-window: exiting");
                return 0;
            }
            return RunWindow(o, owned: false, managedPid: 0);
        }

        // OWNED: we boot and manage the server
        using var guard = ManagedLock.TryAcquire();
        if (!guard.Owner)
        {
            Log.Warn("another instance is already starting a server; waiting for it");
            if (WaitForReady(o))
            {
                if (o.NoWindow)
                {
                    Log.Info("other instance ready; --no-window: exiting");
                    return 0;
                }
                return RunWindow(o, owned: false, managedPid: 0);
            }
            Ui.Error(o, $"Another instance is running, but the server on {o.Url} never became ready within {o.ReadyTimeoutSec}s.\nLogs: {AppPaths.LogsDir}");
            return 20;
        }

        var startedPid = 0;
        try
        {
            if (!o.NoUpdate)
            {
                Updater.Run(tools, o);
                tools = Tools.Discover();
                if (tools.DshMissing)
                {
                    Ui.Error(o, "The dsh CLI disappeared after the update; run  npm install -g @deepseek-ai/dsh  and retry.");
                    return 13;
                }
            }

            // re-check after a potentially slow update: another instance may have won
            if (NetProbe.IsOpen(o.Address, o.Port))
            {
                Log.Warn("port became busy during startup -> attach mode");
                if (o.NoWindow) return 0;
                return RunWindow(o, owned: false, managedPid: 0);
            }

            startedPid = ServerManager.Start(tools, o);
            if (startedPid <= 0)
            {
                Ui.Error(o, $"Failed to start the dsh web server.\nLogs: {AppPaths.LogsDir}");
                return 22;
            }

            if (!WaitForReady(o))
            {
                ServerManager.Stop(startedPid);
                Ui.Error(o, $"dsh web did not become ready on {o.Url} within {o.ReadyTimeoutSec}s.\nServer logs: {AppPaths.LogsDir}");
                return 21;
            }

            if (o.NoWindow)
            {
                Log.Info("=== READY (boot test) ===");
                ServerManager.Stop(startedPid);
                Log.Info("=== boot test complete (server stopped) ===");
                return 0;
            }

            return RunWindow(o, owned: true, managedPid: startedPid);
        }
        catch (Exception ex)
        {
            Log.Error("owned flow error: " + ex);
            Ui.Error(o, "Error while booting the server:\n" + ex.Message);
            if (startedPid > 0) ServerManager.Stop(startedPid);
            return 23;
        }
    }

    private static bool WaitForReady(Options o)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalSeconds < o.ReadyTimeoutSec)
        {
            if (NetProbe.IsOpen(o.Address, o.Port) && NetProbe.IsHttp200(o.Url))
            {
                Log.Info($"server is ready at {o.Url}");
                return true;
            }
            var elapsed = (int)(DateTime.UtcNow - started).TotalSeconds;
            if (elapsed > 0 && elapsed % 10 == 0)
            {
                Log.Info($"waiting for the server on {o.Url} ... ({elapsed}s elapsed)");
            }
            Thread.Sleep(1000);
        }
        Log.Error($"server did not become ready within {o.ReadyTimeoutSec}s");
        return false;
    }

    /** Blocks until the window closes; stops the server if we own it. */
    private static int RunWindow(Options o, bool owned, int managedPid)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
        }
        catch { }

        using var form = new MainForm(o.Url, AppPaths.WebView2Data);
        Application.Run(form);

        if (owned && managedPid > 0)
        {
            Log.Info("window closed; stopping the managed server");
            ServerManager.Stop(managedPid);
        }
        Log.Info("=== exit ===");
        return 0;
    }
}
