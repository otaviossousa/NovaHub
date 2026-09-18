using System.Collections.Concurrent;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;
using NovaLite.Core;

namespace NovaLite.Desktop;

public sealed record SwitchInputSample(DateTimeOffset At, byte ReportId, int Bytes, string Hex,
    Gamepad? Input, BatteryLevel? Battery, byte? RawBatteryLevel);
public sealed record SwitchInputProbeReport(DateTimeOffset CreatedAt, double RequestedSeconds,
    int DevicesFound, bool Opened, SwitchInputSample[] Samples, string? Error);

/// <summary>Passive observer for the Switch-compatible 057E:2009 input stream.</summary>
public static class SwitchInputProbe
{
    public static async Task<SwitchInputProbeReport> CaptureAsync(TimeSpan duration,
        Action<SwitchInputSample>? onSample = null, bool retainSamples = true)
    {
        if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(duration));

        var found = NativeDeviceTopology.Capture().HidCollections.Where(h =>
            h.VendorId == 0x057E && h.ProductId == 0x2009 &&
            h.UsagePage == 0x0001 && h.Usage == 0x0005).ToArray();
        var samples = new ConcurrentQueue<SwitchInputSample>();
        HidDevice? opened = null;
        try
        {
            foreach (var info in found)
            {
                opened = await HidDevice.FromIdAsync(info.DevicePath, FileAccessMode.Read);
                if (opened != null) break;
            }
            if (opened == null)
                return Result(false, "O Windows encontrou o modo Switch, mas não abriu o fluxo HID.");

            void OnReport(HidDevice _, HidInputReportReceivedEventArgs args)
            {
                try
                {
                    var id = (byte)args.Report.Id;
                    using var reader = DataReader.FromBuffer(args.Report.Data);
                    var payload = new byte[args.Report.Data.Length];
                    reader.ReadBytes(payload);
                    var full = payload.Length > 0 && payload[0] == id
                        ? payload : new[] { id }.Concat(payload).ToArray();
                    var decoded = SwitchInputDecoder.TryDecode(full, out var reading) ? reading : null;
                    var sample = new SwitchInputSample(DateTimeOffset.UtcNow, id, full.Length,
                        retainSamples ? Convert.ToHexString(full) : "", decoded?.Input,
                        decoded?.Battery, decoded?.RawBatteryLevel);
                    if (retainSamples && samples.Count < 512) samples.Enqueue(sample);
                    onSample?.Invoke(sample);
                }
                catch { }
            }

            opened.InputReportReceived += OnReport;
            await Task.Delay(duration);
            opened.InputReportReceived -= OnReport;
            return Result(true, null);
        }
        catch (Exception ex)
        {
            return Result(opened != null, $"{ex.GetType().Name}: 0x{ex.HResult:X8} • {ex.Message}");
        }
        finally { opened?.Dispose(); }

        SwitchInputProbeReport Result(bool isOpen, string? error) => new(
            DateTimeOffset.UtcNow, duration.TotalSeconds, found.Length, isOpen, samples.ToArray(), error);
    }
}
