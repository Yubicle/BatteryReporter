using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BatteryTray;

public static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreatePercentageIcon(int percentage, DeviceType type, bool isLightTaskbar)
    {
        // 48x48 = nicer side by side on windows 11
        const int size = 48;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        Color color = percentage switch
        {
            <= 15 => Color.FromArgb(255, 85, 85),   // Red
            <= 40 => Color.FromArgb(255, 193, 7),   // Amber
            _ => isLightTaskbar ? Color.Black : Color.White
        };

        string glyph = type switch
        {
            DeviceType.Mouse => "\uE962",    // win11 mouse
            DeviceType.Headset => "\uE7F6",  // win11 headphones -- "\uE95B" for mic boom
            _ => "\uE83F"                    // generic device
        };

        string label = percentage >= 100 ? "100" : percentage.ToString();
        float numberFontSize = percentage >= 100 ? 15f : 20f;

        using var brush = new SolidBrush(color);
        using var iconFont = new Font("Segoe Fluent Icons", 20f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var textFont = new Font("Segoe UI", numberFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        // left side win11 glyph
        var iconRect = new RectangleF(-2, 0, 22, size);
        g.DrawString(glyph, iconFont, brush, iconRect, format);

        // right side battery num
        var textRect = new RectangleF(18, 0, 31, size);
        g.DrawString(label, textFont, brush, textRect, format);

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally { DestroyIcon(hIcon); }
    }
}