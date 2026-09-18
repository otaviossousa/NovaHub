using System.Globalization;
using System.IO;
using System.Text.Json;
using NovaLite.Core;

namespace NovaLite.Desktop;

internal static class DiagnosticCommandRunner
{
    public static async Task<int?> TryRunAsync(string[] args)
    {
        if (args.Length == 3 && args[0] == "--switch-input-probe")
            return await RunTimedCaptureAsync(args, async duration =>
            {
                var report = await SwitchInputProbe.CaptureAsync(duration);
                return (report, report.Opened && report.Samples.Length > 0 ? 0 : 2);
            });

        if (args.Length == 3 && args[0] == "--ds4-battery-probe")
            return await RunTimedCaptureAsync(args, async duration =>
            {
                var report = await Ds4BatteryProbe.CaptureAsync(duration);
                return (report, report.Opened && report.Samples.Length > 0 ? 0 : 2);
            });

        if (args.Length == 3 && args[0] == "--hid-vendor-input-capture")
            return await RunTimedCaptureAsync(args, async duration =>
                (await HidVendorInputCapture.CaptureAsync(duration), 0));

        if (args.Length == 2 && args[0] == "--device-topology-probe")
            return await RunAsync(args[1], CaptureDeviceTopologyAsync);

        if (args.Length == 2 && args[0] == "--windows-probe")
            return await RunAsync(args[1], CaptureWindowsDevicesAsync);

        return null;
    }

    private static async Task<int> RunTimedCaptureAsync(
        string[] args,
        Func<TimeSpan, Task<(object Report, int ExitCode)>> capture)
    {
        return await RunAsync(args[1], async () =>
        {
            if (!double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                throw new ArgumentException("Duração inválida; informe segundos usando ponto decimal.");
            return await capture(TimeSpan.FromSeconds(seconds));
        });
    }

    private static async Task<int> RunAsync(string outputPath, Func<Task<(object Report, int ExitCode)>> capture)
    {
        var path = Path.GetFullPath(outputPath);
        try
        {
            var (report, exitCode) = await capture();
            WriteReport(path, report);
            return exitCode;
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(path + ".error.txt", ex.ToString()); } catch { }
            return 1;
        }
    }

    private static async Task<(object Report, int ExitCode)> CaptureDeviceTopologyAsync()
    {
        using var devices = new WindowsDevices();
        using var monitor = new ControllerMonitor(new XInputApi());
        devices.Start();
        monitor.Start();
        await Task.Delay(1800);
        var native = NativeDeviceTopology.Capture();
        var automaticUsbTransports = await HidVendorInputCapture.DetectNovaLiteUsbTransportAsync(
            native, TimeSpan.FromMilliseconds(750));
        var report = new
        {
            SchemaVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            Purpose = "Sondagem somente de leitura para correlação de transporte e bateria",
            XInput = monitor.Snapshot(),
            WindowsGamingInput = devices.Snapshot,
            NativeTopology = native,
            AutomaticUsbTransports = automaticUsbTransports,
            ResolvedPhysicalControllers = DeviceTopologyMonitor.Build(native, automaticUsbTransports)
        };
        return (report, 0);
    }

    private static async Task<(object Report, int ExitCode)> CaptureWindowsDevicesAsync()
    {
        using var devices = new WindowsDevices();
        devices.Start();
        await Task.Delay(3500);
        return (devices.Snapshot, 0);
    }

    private static void WriteReport(string path, object report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, Reports.JsonOptions));
    }
}
