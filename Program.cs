namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        //BatteryService.DumpVenderHidDevices(); // debug
        Application.Run(new BatteryTrayContext());
    }



}