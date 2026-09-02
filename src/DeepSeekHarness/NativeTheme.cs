using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DShNative;

/**
 * Windows theming interop: OS dark/light detection, immersive (dark)
 * title bars, and per-window caption/border/text colors on Windows 11 22H2+.
 */
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

    /**
     * True when Windows uses the dark app theme (AppsUseLightTheme = 0).
     */
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

    /**
     * True on Windows 11 22H2+ where caption/border/text colors are supported.
     */
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

    /**
     * COLORREF layout is 0x00BBGGRR, while Color.ToArgb is 0xAARRGGBB.
     */
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    /**
     * Perceived luminance in [0,1]; < 0.45 reads as a dark background.
     */
    public static bool IsDark(Color color)
    {
        var lum = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return lum < 0.45;
    }
}
