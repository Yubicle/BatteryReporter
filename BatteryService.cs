using Windows.Devices.Enumeration;
using System.Diagnostics;
using HidSharp;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BatteryTray;

public enum DeviceType { Mouse, Headset, Other };
public record PeripheralBattery(string Id, string Name, int Percentage, DeviceType Type);

public static class BatteryService
{
    private const int RazerVid = 0x1532;
    private const int RazerDeathAdderV3HyperSpeedPid = 0x00C5;

    private const int SteelSeriesVid = 0x1038;
    private const int SteelSeriesArctisNova7Pid = 0x22A1;
    private const string BatteryLevelKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string IsConnectedKey = "System.Devices.Aep.IsConnected";


    public static async Task<List<PeripheralBattery>> GetAllBatteriesAync()
    {
        var results = new List<PeripheralBattery>();

        // Razer DeathAdder V3 HyperSpeed (2.4ghz dongle)
        var razer = ReadRazerMouseBattery();
        if (razer != null) results.Add(razer);

        // SteelSeries Arctis Nova 7 (2.4ghz dongle)
        var steelSeries = ReadSteelSeriesHeadsetBattery();
        if (steelSeries != null) results.Add(steelSeries);

        return results;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] lpReportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] lpReportBuffer, int reportBufferLength);

    private static PeripheralBattery? ReadRazerMouseBattery()
    {
        var device = DeviceList.Local
            .GetHidDevices(RazerVid, RazerDeathAdderV3HyperSpeedPid)
            .FirstOrDefault(d => d.GetMaxFeatureReportLength() == 91);
        if (device is null)
        {
            Debug.WriteLine("[Razer] Matching HID endpoint (Feat 91) not found.");
            return null;
        }

        // open with access = 0 (metadata/feature-report only) and full sharing (3 = READ | WRITE)
        using var handle = CreateFile(device.DevicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            Debug.WriteLine("[Razer] Found endpoint, but TryOpen failed (is Razer Synapse locking it?).");
            return null;
        }

        // 1 byte report ID (0x00) + 90 byte razer protocol report
        byte[] request = new byte[91];
        request[0] = 0x00; // report id
        request[1] = 0x00; // status (new cmd)
        request[2] = 0x1F; // transaction id for modern razer wireless
        request[6] = 0x02; // data size
        request[7] = 0x07; // cmd class: power
        request[8] = 0x80; // cmd id: get battery level

        // crc: XOR of byte 2..87 of the 90 byte payload (indices 3..88 here)
        byte crc = 0;
        for (int i = 3; i <= 88; i++) crc ^= request[i];
        request[89] = crc;

        if (!HidD_SetFeature(handle, request, request.Length))
        {
            Debug.WriteLine($"[Razer] HidD_SetFeature failed. Win32 Error: {Marshal.GetLastWin32Error()}");
            return null;
        }

        Thread.Sleep(100);
        byte[] response = new byte[91];
        response[0] = 0x00;

        if (!HidD_GetFeature(handle, response, response.Length))
        {
            Debug.WriteLine($"[Razer] HidD_GetFeature failed. Win32 Error: {Marshal.GetLastWin32Error()}");
            return null;
        }

        Debug.WriteLine($"[Razer] Raw response: {BitConverter.ToString(response, 0, 12)}");

        if (response[1] == 0x02)
        {
            int percentage = (int)Math.Round(response[10] / 255.0 * 100.0);
            return new PeripheralBattery("razer-dav3-hs", "Razer DeathAdder V3 HyperSpeed", Math.Clamp(percentage, 0, 100), DeviceType.Mouse);
        }
        
        return null;
     }

    private static PeripheralBattery? ReadSteelSeriesHeadsetBattery()
    {
        var device = DeviceList.Local
            .GetHidDevices(SteelSeriesVid, SteelSeriesArctisNova7Pid)
            .FirstOrDefault(d => d.GetMaxFeatureReportLength() == 515);

        if (device is null || !device.TryOpen(out var stream))
            return null;

        using (stream)
        {
            stream.ReadTimeout = 500;
            stream.WriteTimeout = 500;

            try
            {
                byte[] request = new byte[65];
                request[0] = 0x00;
                request[1] = 0xB0;
                stream.Write(request);

                byte[] response = new byte[65];
                int bytesRead = stream.Read(response, 0, response.Length);

                // response[2]: connection status (0x00 = headset off/disconnected)
                // response[3]: battery percentage (0-100)
                if (bytesRead >= 4 && response[1] == 0xB0 && response[2] != 0x00)
                {
                    int percentage = Math.Clamp((int)response[3], 0, 100);
                    return new PeripheralBattery("steelseries-nova7", "Arctis Nova 7", percentage, DeviceType.Headset);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteelSeries] HID error: {ex.Message}");
            }
        }
            return null;
    }

    public static async Task<List<PeripheralBattery>> GetWindowsReportedBatteriesAsync()
    {
        string[] requestedProperties = [BatteryLevelKey, IsConnectedKey];

        // query bluetooth classic and ble devices
        string aqsFilter =
            "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR " +
            "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\""; 

        var devices = await DeviceInformation.FindAllAsync(
            aqsFilter,
            requestedProperties,
            DeviceInformationKind.AssociationEndpoint);

        var results = new List<PeripheralBattery>();

        foreach(var device in devices)
        {
            bool isConnected = device.Properties.TryGetValue(IsConnectedKey, out var conn) && conn is true;
            if (!isConnected) continue;

            if (device.Properties.TryGetValue(BatteryLevelKey, out var rawLevel) && rawLevel is byte level)
            {
                results.Add(new PeripheralBattery(device.Id, device.Name, level, DeviceType.Other));
            }
        }

        return results;
    }

    /**
     * Use to find new device pid
    public static void DumpVenderHidDevices()
    {
        const int RazerVid = 0x1532;
        const int SteelSeriesVid = 0x1038;

        var devices = DeviceList.Local.GetHidDevices().
            Where(d => d.VendorID is RazerVid or SteelSeriesVid);

        foreach(var dev in devices)
        {
            string name;
            try { name = dev.GetProductName(); }
            catch { name = dev.GetFriendlyName(); }
            Debug.WriteLine(
                $"VID: 0x{dev.VendorID:X4} | PID: 0x{dev.ProductID:X4} | " +
                $"Name: {name} | " +
                $"In: {dev.GetMaxInputReportLength()} | " +
                $"Out: {dev.GetMaxOutputReportLength()} | " +
                $"Feat: {dev.GetMaxFeatureReportLength()}");
        }
    }
    */
}