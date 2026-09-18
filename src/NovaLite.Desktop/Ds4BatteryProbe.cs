using System.Collections.Concurrent;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;
using NovaLite.Core;

namespace NovaLite.Desktop;

public sealed record Ds4BatterySample(
    DateTimeOffset At,
    byte ReportId,
    int Bytes,
    string Hex,
    byte? PowerByte,
    int? LevelStep,
    int? ApproximatePercent,
    string? BatteryLabel,
    bool LowBatteryWarning,
    bool IsCalibratedPoint,
    bool? CableConnected,
    bool? Charging,
    Gamepad? Input);

public sealed record Ds4BatteryProbeReport(
    DateTimeOffset CreatedAt,
    double RequestedSeconds,
    string Safety,
    int DevicesFound,
    bool Opened,
    Ds4BatterySample[] Samples,
    string? Error);

/// <summary>Observes input reports already emitted by a DS4-compatible controller.</summary>
public static class Ds4BatteryProbe
{
    public static async Task<Ds4BatteryProbeReport> CaptureAsync(TimeSpan duration,
        Action<Ds4BatterySample>? onSample = null, bool retainSamples = true)
    {
        if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(duration));

        var found = NativeDeviceTopology.Capture().HidCollections.Where(h =>
            h.VendorId == 0x054C && h.ProductId == 0x09CC &&
            h.UsagePage == 0x0001 && h.Usage == 0x0005).ToArray();
        var samples = new ConcurrentQueue<Ds4BatterySample>();
        HidDevice? opened = null;

        try
        {
            foreach (var info in found)
            {
                opened = await HidDevice.FromIdAsync(info.DevicePath, FileAccessMode.Read);
                if (opened != null) break;
            }
            if (opened == null)
                return Result(false, "O Windows encontrou a interface DS4, mas não permitiu abrir o fluxo HID assíncrono.");

            void OnReport(HidDevice _, HidInputReportReceivedEventArgs args)
            {
                try
                {
                    var report = args.Report;
                    var reportId = (byte)report.Id;
                    using var reader = DataReader.FromBuffer(report.Data);
                    var payload = new byte[report.Data.Length];
                    reader.ReadBytes(payload);
                    var full = payload.Length > 0 && payload[0] == reportId
                        ? payload
                        : new[] { reportId }.Concat(payload).ToArray();
                    byte? power = full.Length > 32 ? full[32] : null;
                    var step = power is { } p ? p & 0x0F : (int?)null;
                    var cable = power is { } c ? (c & 0x10) != 0 : (bool?)null;
                    var charging = cable == true && step is <= 10;
                    var estimate = step is { } level ? NovaLiteDs4BatteryDecoder.Decode(level) : null;
                    var percent = estimate?.PercentForCategory;
                    var input = Ds4InputDecoder.TryDecode(full, out var decoded) ? decoded : (Gamepad?)null;
                    var sample = new Ds4BatterySample(DateTimeOffset.UtcNow, reportId, full.Length,
                        retainSamples ? Convert.ToHexString(full) : "", power, step, percent,
                        estimate?.Label, estimate?.LowBatteryWarning == true,
                        estimate?.IsCalibratedPoint == true, cable, charging, input);
                    if (retainSamples) samples.Enqueue(sample);
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
        finally
        {
            opened?.Dispose();
        }

        Ds4BatteryProbeReport Result(bool isOpen, string? error) => new(
            DateTimeOffset.UtcNow, duration.TotalSeconds,
            "Somente eventos de input; nenhum output report, feature report ou comando foi enviado.",
            found.Length, isOpen, samples.ToArray(), error);
    }
}
