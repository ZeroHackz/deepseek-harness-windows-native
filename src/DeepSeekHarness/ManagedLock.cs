using System;
using System.IO;

namespace DShNative;

/**
 * One launcher boots the server, everyone else attaches. The lock is a
 * simple pid file: whoever wrote it owns the server, latecomers wait for
 * the port and then behave like attach mode.
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
            // Lock file broken or unwritable - degrade to a plain peer that
            // waits for the port and attaches. Never kill anything we may
            // not own.
            return new ManagedLock(path, owner: false);
        }
    }

    public void Dispose()
    {
        if (!Owner) return;
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}
