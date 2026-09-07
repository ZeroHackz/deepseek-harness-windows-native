using System;
using System.Net.Http;
using System.Net.Sockets;

namespace DShNative;

/** Loopback probes: is the port open, does it answer HTTP 200. */
public static class NetProbe
{
    public static bool IsOpen(string host, int port, int timeoutMs = 600)
    {
        using var client = new TcpClient();
        try
        {
            var connect = client.ConnectAsync(host, port);
            if (!connect.Wait(timeoutMs)) return false;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

    /// True when the url answers HTTP at all (2xx-4xx). dsh token-gates its
    /// pages since 0.1.2-rc.1, so a plain 401 means the server is up.
    public static bool IsUp(string url)
    {
        try
        {
            using var resp = Http.GetAsync(url).GetAwaiter().GetResult();
            var code = (int)resp.StatusCode;
            return code >= 200 && code < 500;
        }
        catch
        {
            return false;
        }
    }
}
