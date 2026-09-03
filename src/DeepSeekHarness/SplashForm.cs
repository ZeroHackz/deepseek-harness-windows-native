using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DShNative;

/**
 * Startup window shown while the server boots. Plays one of the embedded
 * loading clips (random pick per launch) above the live status line. Normal
 * window: movable, minimizable; closing it cancels the launch. The whale
 * picture is the instant fallback until the video surface is ready.
 */
public sealed class SplashForm : Form
{
    private static readonly string[] SplashClips = { "DShNative.splash.1.mp4", "DShNative.splash.2.mp4" };

    private static readonly Color DarkBg = Color.FromArgb(16, 16, 18);
    private static readonly Color DarkText = Color.FromArgb(235, 238, 244);
    private static readonly Color LightBg = Color.FromArgb(250, 250, 250);
    private static readonly Color LightText = Color.FromArgb(31, 35, 40);
    private static readonly Color DarkHint = Color.FromArgb(148, 152, 162);
    private static readonly Color LightHint = Color.FromArgb(110, 115, 125);

    private static readonly Icon IconLight = LoadIcon("icon-light.ico");
    private static readonly Icon IconDark = LoadIcon("icon-dark.ico");

    private readonly Panel _stage;
    private readonly PictureBox _whale;
    private readonly Label _status;
    private readonly Action _onCancel;
    private WebView2? _video;
    private bool _finished;

    public SplashForm(string url, Action onCancel)
    {
        _onCancel = onCancel;
        var dark = NativeTheme.IsSystemDark();
        var bg = dark ? DarkBg : LightBg;

        Text = "DeepSeek Harness";
        Icon = dark ? IconDark : IconLight;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = true;
        MaximizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 330);
        BackColor = bg;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = bg,
            Padding = new Padding(24, 16, 24, 14),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        // Stage: the video plays here once ready; the whale covers the gap
        _stage = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        _whale = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(128, 128),
            BackColor = Color.Transparent,
        };
        using (var big = new Icon(Icon, 128, 128)) _whale.Image = big.ToBitmap();
        CenterWhale();
        _stage.Resize += (_, _) => CenterWhale();
        _stage.Controls.Add(_whale);
        table.Controls.Add(_stage, 0, 0);

        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10f),
            ForeColor = dark ? DarkText : LightText,
            Text = "Starting DeepSeek Harness ...",
        };
        table.Controls.Add(_status, 0, 1);

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = dark ? DarkHint : LightHint,
            Text = "You can move or minimize this window. Closing it cancels startup.",
        };
        table.Controls.Add(hint, 0, 2);

        Controls.Add(table);
        Load += async (_, _) => await InitVideoAsync();
        FormClosing += (_, _) =>
        {
            try { _video?.Dispose(); } catch { }
            if (!_finished) { try { _onCancel(); } catch { } }
        };
    }

    /** Thread-safe; the boot runs on a background task. */
    public void SetStatus(string text)
    {
        RunOnUi(() => _status.Text = text);
    }

    /** Startup finished (or gave up): closing now must not cancel anything. */
    public void Finish()
    {
        _finished = true;
    }

    // Picks a random embedded clip, writes it next to a tiny html page, and
    // plays it in a WebView2 surface via a virtual host. Falls back to the
    // static whale when anything goes wrong (e.g. no WebView2 runtime).
    private async Task InitVideoAsync()
    {
        try
        {
            var clip = SplashClips[Random.Shared.Next(SplashClips.Length)];
            var dir = Path.Combine(AppPaths.Root, "splash");
            Directory.CreateDirectory(dir);

            using (var stream = typeof(SplashForm).Assembly.GetManifestResourceStream(clip))
            {
                if (stream == null) { Log.Warn("splash clip missing: " + clip); return; }
                using var file = File.Create(Path.Combine(dir, "load.mp4"));
                await stream.CopyToAsync(file);
            }
            const string html =
                "<!doctype html><html><head><meta charset=\"utf-8\">" +
                "<style>html,body{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:transparent}" +
                "video{width:100%;height:100%;object-fit:contain;display:block}</style></head>" +
                "<body><video src=\"https://splash.local/load.mp4\" autoplay muted loop playsinline></video></body></html>";
            await File.WriteAllTextAsync(Path.Combine(dir, "index.html"), html);

            var dark = NativeTheme.IsSystemDark();
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dir, "webview2"));

            await RunOnUiAsync(() =>
            {
                if (IsDisposed) return;
                var video = new WebView2 { Dock = DockStyle.Fill };
                _video = video;
                _stage.Controls.Add(video); // covers the whale once initialized
                video.DefaultBackgroundColor = dark ? DarkBg : LightBg;
                video.CoreWebView2InitializationCompleted += (_, args) =>
                {
                    if (args.IsSuccess && video.CoreWebView2 != null)
                    {
                        video.CoreWebView2.SetVirtualHostNameToFolderMapping(
                            "splash.local", dir, CoreWebView2HostResourceAccessKind.Allow);
                        video.CoreWebView2.NavigationCompleted += (_, _) =>
                            RunOnUi(() => { try { _whale.Visible = false; } catch { } });
                        video.CoreWebView2.Navigate("https://splash.local/index.html");
                    }
                    else
                    {
                        Log.Warn("splash video init failed: " + args.InitializationException?.Message);
                    }
                };
                _ = video.EnsureCoreWebView2Async(env);
            });
        }
        catch (Exception ex)
        {
            Log.Warn("splash video unavailable, showing the static whale: " + ex.Message);
        }
    }

    private void CenterWhale()
    {
        if (_whale.Parent == null) return;
        _whale.Left = Math.Max(0, (_whale.Parent.ClientSize.Width - _whale.Width) / 2);
        _whale.Top = Math.Max(0, (_whale.Parent.ClientSize.Height - _whale.Height) / 2);
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed) return;
        try
        {
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
        catch { }
    }

    private Task RunOnUiAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        RunOnUi(() => { try { action(); tcs.SetResult(); } catch (Exception ex) { tcs.SetException(ex); } });
        return tcs.Task;
    }

    private static Icon LoadIcon(string resourceName)
    {
        try
        {
            using var stream = typeof(SplashForm).Assembly.GetManifestResourceStream("DShNative." + resourceName);
            if (stream != null) return new Icon(stream);
        }
        catch { }
        return SystemIcons.Application;
    }
}
