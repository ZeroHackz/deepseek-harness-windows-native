using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DShNative;

/** Owns the dsh web child: start, stop, remember its pid. */
public static class ServerManager
{
    public static string? LastOutLog { get; private set; }

    /** Token from the newest server log that printed a ready url for this port. */
    public static string? FindToken(int port)
    {
        var tokenRe = new Regex(@"token=([A-Za-z0-9_-]+)", RegexOptions.Compiled);
        var logs = AppPaths.LogsDir;
        if (!Directory.Exists(logs)) return null;
        var wanted = ":" + port + "/?token=";
        foreach (var f in Directory.EnumerateFiles(logs, "server-*.out.log")
                     .OrderByDescending(f => new FileInfo(f).LastWriteTime))
        {
            string text;
            try { text = File.ReadAllText(f); }
            catch { continue; }
            if (!text.Contains(wanted)) continue;
            var m = tokenRe.Match(text);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    /** Spawns `dsh web --no-open`; returns pid or 0. */
    public static int Start(Tools t, Options o)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var outLog = Path.Combine(AppPaths.LogsDir, $"server-{stamp}.out.log");
        var errLog = Path.Combine(AppPaths.LogsDir, $"server-{stamp}.err.log");

        Log.Info($"starting managed server: node {t.DshCli} web --no-open --host {o.Address} --port {o.Port}");
        var args = new[] { t.DshCli!, "web", "--no-open", "--host", o.Address, "--port", o.Port.ToString() };
        var pid = Proc.Spawn(t.Node!, args, outLog, errLog);
        if (pid <= 0)
        {
            Log.Error("failed to start the managed server");
            return 0;
        }

        LastOutLog = outLog;
        try { File.WriteAllText(AppPaths.ServerPidFile, pid.ToString()); } catch { }
        Log.Info($"managed server pid {pid}; logs: {outLog}");
        return pid;
    }

    public static void Stop(int pid)
    {
        if (pid > 0)
        {
            Log.Info($"stopping managed server pid {pid}");
            Proc.KillTree(pid);
        }
        try { if (File.Exists(AppPaths.ServerPidFile)) File.Delete(AppPaths.ServerPidFile); } catch { }
    }

    /** --stop: kill the server from the pid file. Returns 1 when killed. */
    public static int StopByPidFile()
    {
        try
        {
            if (!File.Exists(AppPaths.ServerPidFile)) return 0;
            if (int.TryParse(File.ReadAllText(AppPaths.ServerPidFile).Trim(), out var pid) && Proc.IsAlive(pid))
            {
                Log.Info($"--stop: killing managed server pid {pid}");
                Proc.KillTree(pid);
                Log.Info("--stop complete");
                return 1;
            }
            Log.Warn("--stop: pid file found but the process is no longer running");
        }
        catch (Exception ex)
        {
            Log.Warn("--stop error: " + ex.Message);
        }
        return 0;
    }
}
