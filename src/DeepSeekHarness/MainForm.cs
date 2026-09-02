using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DShNative;

/**
 * The actual window: a WebView2 (shared Edge runtime) rendering the harness
 * UI. No browser profile, no sign-in, no sync - just a private user-data
 * folder.
 *
 * Look & feel: the icon follows the OS theme (blue whale in light mode,
 * white whale in dark), and once the page has rendered we measure its real
 * background and re-theme the title bar to match - otherwise you get a
 * white bar on top of a dark app, which looks broken.
 */
public sealed class MainForm : Form
{
    // Reads the page's real background via JS: body first, then <html>.
    private const string BgScript =
        "(function(){try{var s=getComputedStyle(document.body).backgroundColor;" +
        "if(!s||s==='transparent'||s==='rgba(0, 0, 0, 0)'){s=getComputedStyle(document.documentElement).backgroundColor;}" +
        "return s||'';}catch(e){return '';}})()";

    private static readonly Color DarkBg = Color.FromArgb(16, 16, 18);
    private static readonly Color DarkText = Color.FromArgb(235, 238, 244);
    private static readonly Color LightBg = Color.FromArgb(250, 250, 250);
    private static readonly Color LightText = Color.FromArgb(31, 35, 40);

    private static readonly Icon IconLight = LoadIcon("icon-light.ico");
    private static readonly Icon IconDark = LoadIcon("icon-dark.ico");

    private readonly string _url;
    private readonly string _userDataDir;
    private readonly Label _status;
    private WebView2? _web;
    private Color? _pageBg; // measured from the rendered page once loaded

    public MainForm(string url, string userDataDir)
    {
        _url = url;
        _userDataDir = userDataDir;

        Text = "DeepSeek Harness";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(860, 560);

        Icon = NativeTheme.IsSystemDark() ? IconDark : IconLight;
        ApplyPalette(null); // sets BackColor from the OS theme before anything paints

        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12f),
            Text = "Starting DeepSeek Harness ...",
        };
        Controls.Add(_status);
        RecolorStatus();

        Load += OnLoadAsync;
        FormClosing += (_, _) => _web?.Dispose();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyWindowChrome();
    }

    /**
     * Windows flipped its theme - swap the icon and, until we measured the
     * page, refresh the OS-based defaults too.
     */
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeTheme.WmSettingChange && !IsDisposed)
        {
            Icon = NativeTheme.IsSystemDark() ? IconDark : IconLight;
            if (_pageBg == null)
            {
                ApplyPalette(null);
                RecolorStatus();
                ApplyWindowChrome();
            }
        }
        base.WndProc(ref m);
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, _userDataDir);
            var web = new WebView2 { Dock = DockStyle.Fill };
            _web = web;
            Controls.Add(web);

            web.CoreWebView2InitializationCompleted += (_, args) =>
            {
                if (args.IsSuccess && web.CoreWebView2 != null)
                {
                    web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    web.DefaultBackgroundColor = CurrentBackground();
                    web.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                    web.CoreWebView2.Navigate(_url);
                }
                else
                {
                    SetStatus("WebView2 failed to initialize:\n" + args.InitializationException?.Message);
                }
            };

            await web.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex)
        {
            SetStatus("Could not start the embedded browser:\n" + ex.Message +
                      "\n\nInstall the WebView2 Runtime (Evergreen):\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703");
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || e.HttpStatusCode >= 400)
        {
            SetStatus("The page could not be loaded.\nIs the DeepSeek Harness server running on " + _url + "?");
            return;
        }
        SetStatus(string.Empty); // hide overlay
        await MeasurePageBackgroundAsync();
    }

    /**
     * Asks the rendered page what its background color is, then re-themes
     * the window chrome to match.
     */
    private async Task MeasurePageBackgroundAsync()
    {
        try
        {
            if (_web?.CoreWebView2 == null) return;
            var json = await _web.CoreWebView2.ExecuteScriptAsync(BgScript);
            var color = ParseCssColor(json);
            if (color == null) return;

            _pageBg = color;
            if (_web != null) _web.DefaultBackgroundColor = color.Value;
            if (!IsDisposed && IsHandleCreated)
            {
                ApplyPalette(color);
                RecolorStatus();
                ApplyWindowChrome();
            }
            Log.Info($"measured page background {color.Value.R},{color.Value.G},{color.Value.B}");
        }
        catch
        {
            // measurement failed - the OS-based theme is a fine fallback
        }
    }

    /**
     * Drives the DWM chrome: immersive dark caption for dark content, and on
     * Win11 22H2+ paint the caption/border in the page's own background so
     * the title bar visually disappears into the app.
     */
    private void ApplyWindowChrome()
    {
        if (!IsHandleCreated) return;

        var dark = _pageBg != null ? NativeTheme.IsDark(_pageBg.Value) : NativeTheme.IsSystemDark();
        NativeTheme.SetImmersiveDark(Handle, dark);

        if (_pageBg != null && NativeTheme.SupportsCustomCaptionColors)
        {
            var bg = _pageBg.Value;
            var fg = dark ? DarkText : LightText;
            NativeTheme.SetCaptionColors(Handle, bg, fg);
            NativeTheme.SetBorderColor(Handle, bg);
        }
    }

    private void ApplyPalette(Color? bg)
    {
        if (bg is { } page)
        {
            BackColor = page;
            return;
        }
        BackColor = NativeTheme.IsSystemDark() ? DarkBg : LightBg;
    }

    private void RecolorStatus()
    {
        var dark = _pageBg != null ? NativeTheme.IsDark(_pageBg.Value) : NativeTheme.IsSystemDark();
        var bg = _pageBg ?? (dark ? DarkBg : LightBg);
        _status.BackColor = bg;
        _status.ForeColor = dark ? DarkText : LightText;
    }

    private Color CurrentBackground() => _pageBg ?? (NativeTheme.IsSystemDark() ? DarkBg : LightBg);

    private void SetStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => SetStatus(text))); } catch { }
            return;
        }
        if (text.Length == 0)
        {
            _status.Visible = false;
        }
        else
        {
            _status.Text = text;
            _status.Visible = true;
            _status.BringToFront();
        }
    }

    private static Icon LoadIcon(string resourceName)
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("DShNative." + resourceName);
            if (stream != null) return new Icon(stream);
        }
        catch { }
        return SystemIcons.Application;
    }

    private static Color? ParseCssColor(string? jsonResult)
    {
        if (string.IsNullOrEmpty(jsonResult)) return null;
        var match = Regex.Match(jsonResult, @"rgba?\(\s*(\d{1,3})\s*[, ]\s*(\d{1,3})\s*[, ]\s*(\d{1,3})");
        if (!match.Success) return null;
        return Color.FromArgb(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));
    }
}
