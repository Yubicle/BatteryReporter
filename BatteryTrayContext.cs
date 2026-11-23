using Microsoft.Win32;
using System.Drawing.Text;

namespace BatteryTray;

public class BatteryTrayContext : ApplicationContext
{
    private readonly Dictionary<string, NotifyIcon> _trayIcons = [];
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly ContextMenuStrip _contextMenu;

    public BatteryTrayContext()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Refresh Now", null, async (_, _) => await RefreshDevicesAsync());
        _contextMenu.Items.Add("-");
        _contextMenu.Items.Add("Exit", null, (_, _) => ExitApp());

        _pollTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _pollTimer.Tick += async (_, _) => await RefreshDevicesAsync();
        _pollTimer.Start();
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await BatteryService.GetAllBatteriesAync();
            System.Diagnostics.Debug.WriteLine($"[Tray] Found {devices.Count} device(s).");
            var activeIds = devices.Select(d => d.Id).ToHashSet();

            // remove icons for disconnected devices
            foreach (var staleId in _trayIcons.Keys.Where(id => !activeIds.Contains(id)).ToList())
            {
                _trayIcons[staleId].Visible = false;
                _trayIcons[staleId].Dispose();
                _trayIcons.Remove(staleId);
            }

            // add/update icons for connected devices
            foreach (var device in devices)
            {
                if (!_trayIcons.TryGetValue(device.Id, out var notifyIcon))
                {
                    notifyIcon = new NotifyIcon
                    {
                        ContextMenuStrip = _contextMenu,
                        Visible = true,
                    };
                    _trayIcons[device.Id] = notifyIcon;
                }

                var oldIcon = notifyIcon.Icon;
                static bool isTaskBarLight()
                {
                    const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                    using var key = Registry.CurrentUser.OpenSubKey(keyPath);

                    return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
                }

                notifyIcon.Icon = TrayIconRenderer.CreatePercentageIcon(device.Percentage, device.Type, isTaskBarLight());
                oldIcon?.Dispose();

                string tooltip = $"{device.Name}: {device.Percentage}%";
                notifyIcon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;
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
        foreach(var icon in _trayIcons.Values)
        {
            icon.Visible = false;
            icon.Dispose();
        }
        Application.Exit();
    }
}