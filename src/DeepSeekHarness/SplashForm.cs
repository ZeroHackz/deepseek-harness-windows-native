using System;
using System.Drawing;
using System.Windows.Forms;

namespace DShNative;

/**
 * Startup window shown while the server boots. Normal window: movable,
 * minimizable, and closing it cancels the launch. The whale icon is just
 * bigger here so the app gives feedback before the UI appears.
 */
public sealed class SplashForm : Form
{
    private static readonly Color DarkBg = Color.FromArgb(16, 16, 18);
    private static readonly Color DarkText = Color.FromArgb(235, 238, 244);
    private static readonly Color LightBg = Color.FromArgb(250, 250, 250);
    private static readonly Color LightText = Color.FromArgb(31, 35, 40);
    private static readonly Color DarkHint = Color.FromArgb(148, 152, 162);
    private static readonly Color LightHint = Color.FromArgb(110, 115, 125);

    private static readonly Icon IconLight = LoadIcon("icon-light.ico");
    private static readonly Icon IconDark = LoadIcon("icon-dark.ico");

    private readonly Label _status;
    private readonly Action _onCancel;
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
        ClientSize = new Size(460, 250);
        BackColor = bg;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = bg,
            Padding = new Padding(24, 20, 24, 16),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var whale = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Anchor = AnchorStyles.None,
            BackColor = Color.Transparent,
        };
        using (var big = new Icon(Icon, 128, 128)) whale.Image = big.ToBitmap();
        table.Controls.Add(whale, 0, 0);

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
        FormClosing += (_, _) =>
        {
            if (!_finished) { try { _onCancel(); } catch { } }
        };
    }

    /** Thread-safe; the boot runs on a background task. */
    public void SetStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => SetStatus(text))); } catch { }
            return;
        }
        _status.Text = text;
    }

    /** Startup finished (or gave up): closing now must not cancel anything. */
    public void Finish()
    {
        _finished = true;
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
