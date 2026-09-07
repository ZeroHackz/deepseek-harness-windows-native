using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DShNative;

/** Owns the dsh web child: start, stop, remember its pid. */
public static class ServerManager
{
    public static string? LastOutLog { get; private set; }

    /// Token captured directly from the child's stdout, set while the server
    /// process is running. No log-file race, unlike the log scan below.
    public static string? LastToken { get; set; }

    private static readonly Regex TokenRe = new(@"token=([A-Za-z0-9_-]+)", RegexOptions.Compiled);

    /// Where a web_token.txt may live: next to the exe, its parent, the app
    /// data dir, and the current directory.
    private static IEnumerable<string> TokenFileCandidates()
    {
        var list = new List<string>();
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
            {
                var dir = Path.GetDirectoryName(exe);
                if (dir != null)
                {
                    list.Add(Path.Combine(dir, "web_token.txt"));
                    var parent = Path.GetDirectoryName(dir);
                    if (parent != null) list.Add(Path.Combine(parent, "web_token.txt"));
                }
            }
        }
        catch { }
        list.Add(Path.Combine(AppPaths.Root, "web_token.txt"));
        list.Add(Path.Combine(Environment.CurrentDirectory, "web_token.txt"));
        return list.Distinct();
    }

    /** Token for a port from the server logs, or from a web_token.txt. */
    public static string? FindToken(int port)
    {
        // newest server log that printed a ready url for this port
        var logs = AppPaths.LogsDir;
        if (Directory.Exists(logs))
        {
            var wanted = ":" + port + "/?token=";
            foreach (var f in Directory.EnumerateFiles(logs, "server-*.out.log")
                         .OrderByDescending(f => new FileInfo(f).LastWriteTime))
            {
                string text;
                try { text = File.ReadAllText(f); }
                catch { continue; }
                if (!text.Contains(wanted)) continue;
                var m = TokenRe.Match(text);
                if (m.Success) return m.Groups[1].Value;
            }
        }

        // a server started by start-dsh-web.ps1 writes its url to web_token.txt
        foreach (var tf in TokenFileCandidates())
        {
            string text;
            try
            {
                if (!File.Exists(tf)) continue;
                text = File.ReadAllText(tf);
            }
            catch { continue; }
            if (text.Contains(":" + port + "/?token="))
            {
                var m = TokenRe.Match(text);
                if (m.Success) return m.Groups[1].Value;
            }
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
        LastToken = null;
        var port = o.Port;
        var pid = Proc.Spawn(t.Node!, args, outLog, errLog, line =>
        {
            if (LastToken == null && line != null && line.Contains(":" + port + "/?token="))
            {
                var m = TokenRe.Match(line);
                if (m.Success) LastToken = m.Groups[1].Value;
            }
        });
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
