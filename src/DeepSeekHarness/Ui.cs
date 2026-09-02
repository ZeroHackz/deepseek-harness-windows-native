using System;
using System.Windows.Forms;

namespace DShNative;

/**
 * Shows errors to the user. Pop a message box when we actually have a UI;
 * otherwise just log and move on.
 */
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
