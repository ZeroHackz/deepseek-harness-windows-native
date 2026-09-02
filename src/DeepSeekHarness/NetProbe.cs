using System;
using System.Net.Http;
using System.Net.Sockets;

namespace DShNative;

/// <summary>Loopback probes: TCP reachability and HTTP 200 checks.</summary>
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

    public static bool IsHttp200(string url)
    {
        try
        {
            using var resp = Http.GetAsync(url).GetAwaiter().GetResult();
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
