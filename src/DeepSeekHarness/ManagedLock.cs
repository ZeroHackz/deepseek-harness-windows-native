using System;
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
                if (int.TryParse(File.ReadAllText(path).Trim(), out var pid) && Proc.IsAlive(pid))
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

    public void Dispose()
    {
        if (!Owner) return;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
