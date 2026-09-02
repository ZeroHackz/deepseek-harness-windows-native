using System;
using System.IO;

namespace DShNative;

/**
 * Single-instance guard for the owned (server-booting) mode. The first
 * instance to acquire the lock owns the server; later ones wait and attach.
 */
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
                if (int.TryParse(File.ReadAllText(path).Trim(), out var pid) && Proc.IsAlive(pid))
                    return new ManagedLock(path, owner: false);
                try { File.Delete(path); } catch { }
            }
            File.WriteAllText(path, Environment.ProcessId.ToString());
            return new ManagedLock(path, owner: true);
        }
        catch
        {
            // Degrade gracefully: without the lock we behave like a peer that
            // waits for the port and attaches (never kills a server).
            return new ManagedLock(path, owner: false);
        }
    }

    public void Dispose()
    {
        if (!Owner) return;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
