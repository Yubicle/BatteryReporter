using Microsoft.Win32;

namespace BatteryTray;

public class BatteryTrayContext : ApplicationContext
{
    private readonly Dictionary<string, (NotifyIcon Glyph, NotifyIcon Number)> _trayIcons = [];
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly ContextMenuStrip _contextMenu;

    public BatteryTrayContext()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Refresh Now", null, async (_, _) => await RefreshDevicesAsync());
        _contextMenu.Items.Add("-");
        _contextMenu.Items.Add("Exit", null, (_, _) => ExitApp());

        _pollTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _pollTimer.Tick += async (_, _) =>
        {
            _pollTimer.Interval = 60_000;
            await RefreshDevicesAsync();
        };
        _pollTimer.Start();
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await BatteryService.GetAllBatteriesAsync();
            var activeIds = devices.Select(d => d.Id).ToHashSet();

            // remove icons for disconnected devices
            foreach (var staleId in _trayIcons.Keys.Where(id => !activeIds.Contains(id)).ToList())
            {
                var (glyphIcon, numberIcon) = _trayIcons[staleId];
                glyphIcon.Visible = false;
                numberIcon.Visible = false;
                glyphIcon.Dispose();
                numberIcon.Dispose();
                _trayIcons.Remove(staleId);
            }
            // helper
            static bool isTaskBarLight()
            {
                const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);

                return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
            }
            bool taskbarTheme = isTaskBarLight();
            // add/update icons for connected devices
            foreach (var device in devices)
            {
                if (!_trayIcons.TryGetValue(device.Id, out var pair))
                {
                    var glyphIcon = new NotifyIcon { ContextMenuStrip = _contextMenu, Visible = true };
                    var numberIcon = new NotifyIcon { ContextMenuStrip = _contextMenu, Visible = true };
                    pair = (glyphIcon, numberIcon);
                    _trayIcons[device.Id] = pair;
                }

                var oldGlyph = pair.Glyph.Icon;
                pair.Glyph.Icon = TrayIconRenderer.CreateGlyphIcon(device.Type, device.Percentage, taskbarTheme);
                oldGlyph?.Dispose();

                var oldNumber = pair.Number.Icon;
                pair.Number.Icon = TrayIconRenderer.CreateNumberIcon(device.Percentage, taskbarTheme);
                oldNumber?.Dispose();

                string tooltip = $"{device.Name}: {device.Percentage}%";
                if (tooltip.Length > 127) tooltip = tooltip[..127];
                pair.Glyph.Text = tooltip;
                pair.Number.Text = tooltip;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Tray] Crash in RefreshDevicesAsync: {ex}");
        }
        
    }

    private void ExitApp()
    {
        _pollTimer.Stop();
        foreach(var (glyphIcon, numberIcon) in _trayIcons.Values)
        {
            glyphIcon.Visible = false;
            numberIcon.Visible = false;
            glyphIcon.Dispose();
            numberIcon.Dispose();
        }
        Application.Exit();
    }
}