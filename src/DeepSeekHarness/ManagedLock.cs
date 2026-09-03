using System;
using System.Diagnostics;
using System.IO;

namespace DShNative;

/** Pid-file lock: whoever holds it owns the server; the rest attach. */
public sealed class ManagedLock : IDisposable
{
    private readonly string _path;
    public bool Owner { get; }

    private ManagedLock(string path, bool owner)
    {
        _path = path;
        Owner = owner;
    }

    public static ManagedLock TryAcquire()
    {
        var path = AppPaths.LockFile;
        try
        {
            if (File.Exists(path))
            {
                // Only trust the lock when the owner is a live instance of
                // this app; a stale pid may have been recycled by something
                // else entirely.
                if (int.TryParse(File.ReadAllText(path).Trim(), out var pid) && IsOwnInstance(pid))
                    return new ManagedLock(path, owner: false);
                try { File.Delete(path); } catch { }
            }
            File.WriteAllText(path, Environment.ProcessId.ToString());
            return new ManagedLock(path, owner: true);
        }
        catch
        {
            // broken lock -> degrade to a peer: attach, never kill
            return new ManagedLock(path, owner: false);
        }
    }

    private static bool IsOwnInstance(int pid)
    {
        try
        {
            // Builds may name the exe differently (e.g. DeepSeekHarness vs
            // DeepSeekHarness-win-x64-2026.09.26), so match on the prefix.
            using var p = Process.GetProcessById(pid);
            return !p.HasExited && p.ProcessName.StartsWith("DeepSeekHarness", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!Owner) return;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
