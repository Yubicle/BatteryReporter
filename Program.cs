namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        //BatteryService.DumpVenderHidDevices();
        Application.Run(new BatteryTrayContext());
    }
}