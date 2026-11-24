using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BatteryTray;

public static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    
    public static Icon CreateGlyphIcon(DeviceType type, int percentage, bool isLightTaskbar)
    {
        // 32x32 for more uniform sizing in numbers and icons
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        Color color = GetBatteryColor(percentage, isLightTaskbar);

        string glyph = type switch
        {
            DeviceType.Mouse => "\uE962",    // win11 mouse
            DeviceType.Headset => "\uE7F6",  // win11 headphones -- "\uE95B" for mic boom
            _ => "\uE83F"                    // generic device
        };

        using var brush = new SolidBrush(color);
        using var font = new Font("Segoe Fluent Icons", 26f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        g.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size), format);

        return ConvertBitmapToIcon(bitmap);
    }

    public static Icon CreateNumberIcon(int percentage, bool isLightTaskbar)
    {
        const int size = 50;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        Color color = GetBatteryColor(percentage, isLightTaskbar);
        string label = percentage >= 100 ? "100" : percentage.ToString();
        float fontSize = percentage >= 100 ? 17f : 32f;

        using var brush = new SolidBrush(color);
        using var font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        g.DrawString(label, font, brush, new RectangleF(0, 0, size, size));

        return ConvertBitmapToIcon(bitmap);
    }

    private static Color GetBatteryColor(int percentage, bool isLightTaskbar)
    {
        Color color = percentage switch
        {
            <= 15 => Color.FromArgb(255, 85, 85),
            <= 40 => Color.FromArgb(255, 193, 7),
            _ => isLightTaskbar ? Color.Black : Color.White
        };
        return color;
    }

    private static Icon ConvertBitmapToIcon(Bitmap bitmap)
    {
        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}