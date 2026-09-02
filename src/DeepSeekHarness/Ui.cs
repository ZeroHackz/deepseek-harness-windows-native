using System;
using System.Windows.Forms;

namespace DShNative;

/** <summary>User-facing error reporting (message box in GUI modes, log always).</summary> */
public static class Ui
{
    public static void Error(Options o, string message)
    {
        Log.Error(message.ReplaceLineEndings(" | "));
        if (!o.ShowDialogs) return;
        try
        {
            MessageBox.Show(message, "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }
}
