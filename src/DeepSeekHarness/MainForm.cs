using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DShNative;

/// <summary>
/// The app window: a WebView2 (shared Edge runtime) rendering the harness UI.
/// No browser profile, no sign-in, no sync - only a private user-data folder.
/// </summary>
public sealed class MainForm : Form
{
    private readonly string _url;
    private readonly string _userDataDir;
    private readonly Label _status;
    private WebView2? _web;

    public MainForm(string url, string userDataDir)
    {
        _url = url;
        _userDataDir = userDataDir;

        Text = "DeepSeek Harness";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(860, 560);

        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12f),
            Text = "Starting DeepSeek Harness ...",
        };
        Controls.Add(_status);

        Load += OnLoadAsync;
        FormClosing += (_, _) => _web?.Dispose();
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

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || e.HttpStatusCode >= 400)
        {
            SetStatus("The page could not be loaded.\nIs the DeepSeek Harness server running on " + _url + "?");
            return;
        }
        SetStatus(string.Empty); // hide overlay
    }

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
}
