using System;

namespace DShNative;

/**
 * --self-test: environment report (console + log file), always exit 0.
 */
public static class SelfTest
{
    public static int Run(Options o)
    {
        var t = Tools.Discover();

        var webView2 = "n/a";
        try
        {
            webView2 = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "n/a";
        }
        catch { }

        void Line(string s)
        {
            Console.WriteLine(s);
            Log.Info("selftest: " + s);
        }

        Line("== DeepSeek Harness self test ==");
        Line("node        : " + (t.Node ?? "NOT FOUND"));
        Line("npm cli     : " + (t.NpmCli ?? "NOT FOUND"));
        Line("dsh cli     : " + (t.DshCli ?? "NOT FOUND"));
        Line("dsh version : " + (t.DshVersion ?? "n/a"));
        Line("DSH_HOME    : " + Environment.GetEnvironmentVariable("DSH_HOME"));
        Line("webview2    : " + webView2);
        Line("target      : " + o.Url);
        Line("port " + o.Port + "    : " + (NetProbe.IsOpen(o.Address, o.Port) ? "IN USE (attach mode)" : "free (owned mode)"));
        Line("logs dir    : " + AppPaths.LogsDir);
        Line("== done ==");
        return 0;
    }
}
