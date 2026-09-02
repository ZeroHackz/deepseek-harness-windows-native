using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DShNative;

/** DWM interop: OS dark mode + caption/border/text colors (Win11 22H2+). */
public static class NativeTheme
{
    public const int WmSettingChange = 0x001A;

    // DWMWA_* attribute ids (dwmapi.h)
    private const int DwmwaUseImmersiveDarkMode = 20; // 20 on Win10 1903+ (19 before)
    private const int DwmwaBorderColor = 34;          // Win11 22H2+
    private const int DwmwaCaptionColor = 35;         // Win11 22H2+
    private const int DwmwaTextColor = 36;            // Win11 22H2+

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    /** True when Windows apps use dark mode. */
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    /** True on Win11 22H2+; earlier builds ignore the color attributes. */
    public static bool SupportsCustomCaptionColors => Environment.OSVersion.Version.Build >= 22621;

    public static void SetImmersiveDark(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    public static void SetCaptionColors(IntPtr hwnd, Color? caption, Color? text)
    {
        if (hwnd == IntPtr.Zero || !SupportsCustomCaptionColors) return;
        if (caption is { } c)
        {
            var value = ToColorRef(c);
            _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref value, sizeof(int));
        }
        if (text is { } t)
        {
            var value = ToColorRef(t);
            _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref value, sizeof(int));
        }
    }

    public static void SetBorderColor(IntPtr hwnd, Color border)
    {
        if (hwnd == IntPtr.Zero || !SupportsCustomCaptionColors) return;
        var value = ToColorRef(border);
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref value, sizeof(int));
    }

    /** ARGB -> DWM COLORREF (0x00BBGGRR): swap R and B. */
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    /** Perceived luminance < 0.45 counts as dark. */
    public static bool IsDark(Color color)
    {
        var lum = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return lum < 0.45;
    }
}
