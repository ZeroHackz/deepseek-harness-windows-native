using System;
using System.IO;

namespace DShNative;

/**
 * Owns the dsh web child process: start it, stop it, remember its pid.
 */
public static class ServerManager
{
    /**
     * Spawns `dsh web --no-open` with our host/port. Returns the pid,
     * or 0 when it could not be started.
     */
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

    /**
     * --stop: read the pid file and kill that server. Returns 1 when
     * something actually got killed.
     */
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
