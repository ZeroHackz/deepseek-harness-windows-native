using System;

namespace DShNative;

/**
 * The CLI flags this app understands (see README for what each does).
 */
public sealed class Options
{
    public string Address { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = 3080;
    public bool NoUpdate { get; private set; }
    public bool NoWindow { get; private set; }
    public bool SelfTest { get; private set; }
    public bool Stop { get; private set; }
    public int ReadyTimeoutSec { get; private set; } = 120;

    /**
     * True when a message box is appropriate, i.e. not in --self-test or
     * --no-window mode (nobody is looking at a window there).
     */
    public bool ShowDialogs => !NoWindow && !SelfTest;

    public string Url => $"http://{Address}:{Port}";

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--port":
                case "-port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var p)) o.Port = p;
                    break;
                case "--address":
                case "-address":
                    if (i + 1 < args.Length) o.Address = args[++i];
                    break;
                case "--ready-timeout":
                case "-ready-timeout":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var r)) o.ReadyTimeoutSec = Math.Max(10, r);
                    break;
                case "--no-update":
                case "-no-update":
                    o.NoUpdate = true;
                    break;
                case "--no-window":
                case "-no-window":
                    o.NoWindow = true;
                    break;
                case "--self-test":
                case "-self-test":
                    o.SelfTest = true;
                    break;
                case "--stop":
                case "-stop":
                    o.Stop = true;
                    break;
            }
        }
        return o;
    }
}
