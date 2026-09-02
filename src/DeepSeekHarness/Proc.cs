using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DShNative;

/**
 * Helpers around child processes: capture output to files, kill whole trees.
 */
public static class Proc
{
    /**
     * Runs a process and waits for it to finish. stdout/stderr land in files
     * when outFile/errFile are given. Returns the process exit code; -1 when
     * it never even started; -999 on timeout (the whole tree gets killed).
     */
    public static int Run(string exePath, string[] args, string? outFile, string? errFile, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = outFile != null,
            RedirectStandardError = errFile != null,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = new Process { StartInfo = psi };
        if (!p.Start()) return -1;

        FileStream? so = null;
        FileStream? se = null;
        Task? cout = null;
        Task? cerr = null;
        try
        {
            if (outFile != null)
            {
                so = new FileStream(outFile, FileMode.Create, FileAccess.Write);
                cout = p.StandardOutput.BaseStream.CopyToAsync(so);
            }
            if (errFile != null)
            {
                se = new FileStream(errFile, FileMode.Create, FileAccess.Write);
                cerr = p.StandardError.BaseStream.CopyToAsync(se);
            }

            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                p.WaitForExit(10_000);
                WaitQuietly(cout, cerr);
                return -999;
            }
            WaitQuietly(cout, cerr);
            return p.ExitCode;
        }
        finally
        {
            so?.Dispose();
            se?.Dispose();
        }
    }

    /**
     * Fire-and-forget process start (used for the server). Output goes to
     * files. Returns the pid, or 0 if it failed to start.
     */
    public static int Spawn(string exePath, string[] args, string outFile, string errFile)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        try
        {
            var p = new Process { StartInfo = psi };
            if (!p.Start())
            {
                p.Dispose();
                return 0;
            }
            var so = new FileStream(outFile, FileMode.Create, FileAccess.Write);
            var se = new FileStream(errFile, FileMode.Create, FileAccess.Write);
            _ = p.StandardOutput.BaseStream.CopyToAsync(so);
            _ = p.StandardError.BaseStream.CopyToAsync(se);
            return p.Id;
        }
        catch (Exception ex)
        {
            Log.Error("spawn failed: " + ex.Message);
            return 0;
        }
    }

    public static void KillTree(int pid)
    {
        if (pid <= 0) return;
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            p.WaitForExit(5000);
        }
        catch { }
    }

    public static bool IsAlive(int pid)
    {
        if (pid <= 0) return false;
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void WaitQuietly(Task? a, Task? b)
    {
        try { a?.Wait(5000); } catch { }
        try { b?.Wait(5000); } catch { }
    }
}
