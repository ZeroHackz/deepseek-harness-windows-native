using System;
using System.Windows.Forms;

namespace DShNative;

/** User errors: message box when we have a UI, log regardless. */
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
