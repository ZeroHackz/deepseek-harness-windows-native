using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DShNative;

/**
 * DWM interop: detect the OS dark/light theme and drive the title-bar look -
 * immersive dark mode, plus caption/border/text colors on Win11 22H2+.
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
     * True when Windows apps are in dark mode (AppsUseLightTheme = 0).
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
     * Win11 22H2+ only - older builds simply ignore the color attributes.
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
     * DWM wants COLORREF (0x00BBGGRR); Color.ToArgb hands us 0xAARRGGBB,
     * so swap the red and blue bytes around.
     */
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    /**
     * Rough perceived luminance; below 0.45 we call the color dark.
     */
    public static bool IsDark(Color color)
    {
        var lum = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return lum < 0.45;
    }
}
