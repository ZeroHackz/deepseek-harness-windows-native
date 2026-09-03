using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DShNative;

/** Attach to an existing server or boot our own, show the window, stop on close. */
public static class Orchestrator
{
    private enum Mode { Cancelled, Attach, Owned }

    private sealed class Outcome
    {
        public Mode Mode;
        public int Pid;
        public int ExitCode;
        public string? Error;
    }

    public static int Run(Options o)
    {
        return o.NoWindow ? RunHeadless(o) : RunGui(o);
    }

    // --no-window: boot test, no UI at all
    private static int RunHeadless(Options o)
    {
        var r = Boot(o, status: null, CancellationToken.None);
        if (r.ExitCode != 0)
        {
            if (r.Error != null) Ui.Error(o, r.Error);
            return r.ExitCode;
        }
        if (r.Mode == Mode.Owned)
        {
            Log.Info("=== READY (boot test) ===");
            ServerManager.Stop(r.Pid);
            Log.Info("=== boot test complete (server stopped) ===");
        }
        return 0;
    }

    // Normal launch: splash window with progress while the server boots.
    private static int RunGui(Options o)
    {
        using var cts = new CancellationTokenSource();
        using var splash = new SplashForm(o.Url, () => { try { cts.Cancel(); } catch { } });

        splash.Show();
        var task = Task.Run(() => Boot(o, splash.SetStatus, cts.Token));

        // Pump the splash: it is a normal window (move, minimize, close all work)
        while (!splash.IsDisposed && !task.IsCompleted)
        {
            Application.DoEvents();
            Thread.Sleep(30);
        }
        if (!splash.IsDisposed)
        {
            splash.Finish();
            splash.Close();
        }

        Outcome r;
        try
        {
            r = task.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error("unexpected boot error: " + ex);
            Ui.Error(o, "Unexpected error:\n" + ex.Message);
            return 1;
        }
        if (r.ExitCode != 0)
        {
            if (r.Error != null) Ui.Error(o, r.Error);
            return r.ExitCode;
        }
        if (r.Mode == Mode.Cancelled) return 0;

        RunWindow(o, owned: r.Mode == Mode.Owned, managedPid: r.Pid);
        return 0;
    }

    private static void RunWindow(Options o, bool owned, int managedPid)
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
    }

    /**
     * The actual boot, with progress + cancellation. Runs on a background
     * task so the splash can stay responsive.
     */
    private static Outcome Boot(Options o, Action<string>? status, CancellationToken ct)
    {
        void Say(string msg) { status?.Invoke(msg); Log.Info(msg); }
        var noUpdate = o.NoUpdate;

        var tools = Tools.Discover();
        if (tools.Node == null)
        {
            return Fail(10, "Node.js was not found on PATH.\nInstall it from https://nodejs.org and try again.\n\nTarget: " + o.Url);
        }
        if (tools.DshMissing)
        {
            if (noUpdate)
            {
                return Fail(11, "@deepseek-ai/dsh is not installed globally.\nRun:  npm install -g @deepseek-ai/dsh\n(or launch without --no-update so the app installs it).");
            }
            Say("Installing @deepseek-ai/dsh via npm ...");
            Updater.Run(tools, o, ct);
            ct.ThrowIfCancellationRequested();
            tools = Tools.Discover();
            if (tools.DshMissing)
            {
                return Fail(12, "Auto-install of @deepseek-ai/dsh failed. Check the logs and your network, then run:\n  npm install -g @deepseek-ai/dsh");
            }
        }

        // Something already on the port? Attach and do not touch it.
        if (NetProbe.IsOpen(o.Address, o.Port))
        {
            Say($"A server is already running on port {o.Port} - attaching");
            return new Outcome { Mode = Mode.Attach };
        }

        // One launcher owns the server; everyone else waits and attaches.
        using var guard = ManagedLock.TryAcquire();
        if (!guard.Owner)
        {
            Say("Another instance is starting a server; waiting for it ...");
            if (WaitForReady(o, ct)) return new Outcome { Mode = Mode.Attach };
            return Fail(20, $"Another instance is running, but the server on {o.Url} never became ready within {o.ReadyTimeoutSec}s.\nLogs: {AppPaths.LogsDir}");
        }

        var pid = 0;
        try
        {
            if (!noUpdate)
            {
                Say("Updating @deepseek-ai/dsh to the latest npm release ...");
                Updater.Run(tools, o, ct);
                ct.ThrowIfCancellationRequested();
                tools = Tools.Discover();
                if (tools.DshMissing)
                {
                    return Fail(13, "The dsh CLI disappeared after the update; run  npm install -g @deepseek-ai/dsh  and retry.");
                }
            }

            // Another launcher may have grabbed the port while npm was running
            if (NetProbe.IsOpen(o.Address, o.Port))
            {
                Say("The port became busy during startup - attaching instead");
                return new Outcome { Mode = Mode.Attach };
            }

            Say($"Starting the dsh web server on {o.Url} ...");
            pid = ServerManager.Start(tools, o);
            if (pid <= 0)
            {
                return Fail(22, $"Failed to start the dsh web server.\nLogs: {AppPaths.LogsDir}");
            }

            Say("Waiting for the server to come up ...");
            if (!WaitForReady(o, ct))
            {
                ServerManager.Stop(pid);
                return Fail(21, $"dsh web did not become ready on {o.Url} within {o.ReadyTimeoutSec}s.\nServer logs: {AppPaths.LogsDir}");
            }

            Say("Server is ready");
            return new Outcome { Mode = Mode.Owned, Pid = pid };
        }
        catch (OperationCanceledException)
        {
            // Splash closed: stop the server we started, leave everything else alone
            if (pid > 0) ServerManager.Stop(pid);
            return new Outcome { Mode = Mode.Cancelled };
        }
        catch (Exception ex)
        {
            Log.Error("owned flow error: " + ex);
            if (pid > 0) ServerManager.Stop(pid);
            return Fail(23, "Error while booting the server:\n" + ex.Message);
        }
    }

    private static Outcome Fail(int code, string message) => new Outcome { ExitCode = code, Error = message };

    private static bool WaitForReady(Options o, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalSeconds < o.ReadyTimeoutSec)
        {
            ct.ThrowIfCancellationRequested();
            if (NetProbe.IsOpen(o.Address, o.Port) && NetProbe.IsHttp200(o.Url))
            {
                Log.Info($"server is ready at {o.Url}");
                return true;
            }
            Thread.Sleep(1000);
        }
        Log.Error($"server did not become ready within {o.ReadyTimeoutSec}s");
        return false;
    }
}
