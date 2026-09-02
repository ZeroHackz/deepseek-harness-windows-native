using System;

namespace DShNative;

/** <summary>--stop: stop the managed server this app previously started.</summary> */
public static class StopOnly
{
    public static int Run(Options o)
    {
        var killed = ServerManager.StopByPidFile();
        if (killed > 0) Log.Info($"stopped the managed server for port {o.Port}");
        else Log.Warn($"no managed server found for port {o.Port} (pid file absent or process dead)");
        return 0;
    }
}
