using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NovaLite.Core;

namespace NovaLite.Desktop;

public sealed record HidReportPattern(string Hex, int Count, double FirstMilliseconds, double LastMilliseconds);

public sealed record HidReportTiming(
    int IntervalCount,
    double? MinimumMilliseconds,
    double? AverageMilliseconds,
    double? P95Milliseconds,
    double? MaximumMilliseconds);

public sealed record HidVendorInterfaceCapture(
    string InstanceId,
    Guid? ContainerId,
    int? XInputSlot,
    PhysicalConnection Connection,
    ushort VendorId,
    ushort ProductId,
    ushort UsagePage,
    ushort Usage,
    ushort ReportBytes,
    int ReportsRead,
    HidReportTiming Timing,
    HidReportPattern[] Patterns,
    string? Error);

public sealed record HidVendorCaptureReport(
    DateTimeOffset CreatedAt,
    double RequestedSeconds,
    string Safety,
    HidVendorInterfaceCapture[] Interfaces);

/// <summary>Reads vendor input queues only. No output or feature report is sent.</summary>
public static class HidVendorInputCapture
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 1;
    private const uint FileShareWrite = 2;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;

    public static async Task<HidVendorCaptureReport> CaptureAsync(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(duration), "Use de 1 a 30 segundos.");
        var topology = NativeDeviceTopology.Capture();
        var physical = DeviceTopologyMonitor.Build(topology);
        var nodes = topology.RelevantNodes.ToDictionary(n => n.InstanceId, StringComparer.OrdinalIgnoreCase);
        var candidates = topology.HidCollections.Where(h =>
            h.InputReportBytes > 0 &&
            h.VendorId == 0x3537 &&
            ((h.UsagePage == 0xFF7A && h.Usage == 0x0001) ||
             (h.UsagePage == 0x0001 && h.Usage is 0x0004 or 0x0005))).ToArray();
        using var deadline = new CancellationTokenSource(duration);
        var captures = await Task.WhenAll(candidates.Select(h => ReadAsync(h, ResolveContainer(h, nodes),
            physical, duration, deadline.Token)));
        return new(DateTimeOffset.UtcNow, duration.TotalSeconds,
            "Somente input reports; nenhum output report, feature report ou comando proprietário foi enviado.", captures);
    }

    public static async Task<IReadOnlyDictionary<Guid, ConnectionAssessment>> DetectNovaLiteUsbTransportAsync(
        NativeTopologySnapshot topology, TimeSpan duration)
    {
        var nodes = topology.RelevantNodes.ToDictionary(n => n.InstanceId, StringComparer.OrdinalIgnoreCase);
        var candidates = topology.HidCollections.Where(h =>
            h.VendorId == 0x3537 && h.ProductId == 0x1040 &&
            h.UsagePage == 0x0001 && h.Usage == 0x0005 && h.InputReportBytes > 0).ToArray();
        using var deadline = new CancellationTokenSource(duration);
        var captures = await Task.WhenAll(candidates.Select(h => ReadAsync(h, ResolveContainer(h, nodes),
            PhysicalControllerInventory.Waiting, duration, deadline.Token)));
        var result = new Dictionary<Guid, ConnectionAssessment>();
        foreach (var capture in captures)
        {
            if (capture.ContainerId is not { } container || capture.Error != null ||
                capture.Timing.AverageMilliseconds is not { } average)
                continue;
            var detected = HidCadenceTransportClassifier.Assess(
                capture.Timing.IntervalCount, capture.Timing.AverageMilliseconds);
            if (detected == PhysicalConnection.UsbCable)
                result[container] = new(PhysicalConnection.UsbCable, "alta",
                    $"Cadência HID passiva de {average:F2} ms ({capture.Timing.IntervalCount} intervalos), compatível com USB direto.",
                    "cadencia-hid-usb-direto");
            else if (detected == PhysicalConnection.Dongle24Ghz)
                result[container] = new(PhysicalConnection.Dongle24Ghz, "alta",
                    $"Cadência HID passiva de {average:F2} ms ({capture.Timing.IntervalCount} intervalos), compatível com receptor 2,4 GHz.",
                    "cadencia-hid-receptor-2.4g");
        }
        return result;
    }

    private static Guid? ResolveContainer(HidCollection hid, IReadOnlyDictionary<string, PnpNode> nodes) =>
        new[] { hid.InstanceId }.Concat(hid.Ancestors).Select(id => nodes.GetValueOrDefault(id)?.ContainerId)
            .FirstOrDefault(id => id != null);

    private static async Task<HidVendorInterfaceCapture> ReadAsync(HidCollection hid, Guid? container,
        PhysicalControllerInventory physical, TimeSpan duration, CancellationToken cancellationToken)
    {
        var device = container == null ? null : physical.Devices.FirstOrDefault(d => d.ContainerId == container);
        var reportBytes = hid.InputReportBytes!.Value;
        var intervals = new List<double>();
        using var handle = CreateFileW(hid.DevicePath, GenericRead, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        if (handle.IsInvalid)
            return Result(0, [], new Win32Exception(Marshal.GetLastWin32Error()).Message);

        var patterns = new Dictionary<string, MutablePattern>(StringComparer.Ordinal);
        double? previousReportMilliseconds = null;
        var read = 0;
        var clock = Stopwatch.StartNew();
        try
        {
            foreach (var reportId in hid.InputReportIds)
            {
                var snapshot = new byte[reportBytes];
                snapshot[0] = reportId;
                if (!HidD_GetInputReport(handle, snapshot, snapshot.Length)) continue;
                read++;
                AddPattern(snapshot);
            }
            // Bluetooth DS4-compatible descriptors advertise a 547-byte maximum
            // control buffer. The current report snapshots above contain battery
            // data; a shared streaming read can remain pending indefinitely on some
            // Windows Bluetooth stacks, so do not hold that channel open here.
            if (reportBytes > 128)
                return Result(read, patterns.Select(p => new HidReportPattern(p.Key, p.Value.Count,
                    p.Value.FirstMilliseconds, p.Value.LastMilliseconds)).ToArray(), null);
            using var stream = new FileStream(handle, FileAccess.Read, reportBytes, isAsync: true);
            using var cancelRead = cancellationToken.Register(() =>
            {
                if (!handle.IsClosed)
                {
                    CancelIoEx(handle.DangerousGetHandle(), IntPtr.Zero);
                    handle.Dispose();
                }
            });
            while (!cancellationToken.IsCancellationRequested)
            {
                var buffer = new byte[reportBytes];
                int count;
                try { count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken); }
                catch (OperationCanceledException) { break; }
                catch (IOException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { break; }
                if (count <= 0) break;
                read++;
                var elapsed = clock.Elapsed.TotalMilliseconds;
                if (previousReportMilliseconds is { } previous && intervals.Count < 200_000)
                    intervals.Add(elapsed - previous);
                previousReportMilliseconds = elapsed;
                AddPattern(buffer.AsSpan(0, count), elapsed);
            }
            return Result(read, patterns.Select(p => new HidReportPattern(p.Key, p.Value.Count,
                p.Value.FirstMilliseconds, p.Value.LastMilliseconds)).ToArray(), null);

            void AddPattern(ReadOnlySpan<byte> buffer, double? at = null)
            {
                var elapsed = at ?? clock.Elapsed.TotalMilliseconds;
                var hex = Convert.ToHexString(buffer);
                if (patterns.TryGetValue(hex, out var existing))
                {
                    existing.Count++;
                    existing.LastMilliseconds = elapsed;
                }
                else if (patterns.Count < 256)
                {
                    patterns[hex] = new(1, elapsed, elapsed);
                }
            }
        }
        catch (Exception ex)
        {
            return Result(read, patterns.Select(p => new HidReportPattern(p.Key, p.Value.Count,
                p.Value.FirstMilliseconds, p.Value.LastMilliseconds)).ToArray(), $"{ex.GetType().Name}: {ex.Message}");
        }

        HidVendorInterfaceCapture Result(int total, HidReportPattern[] found, string? error) => new(
            hid.InstanceId, container, device?.XInputInterfaceIndex, device?.Transport.Connection ?? PhysicalConnection.Unknown,
            hid.VendorId!.Value, hid.ProductId!.Value, hid.UsagePage!.Value, hid.Usage!.Value, reportBytes, total,
            Summarize(intervals), found, error);
    }

    private static HidReportTiming Summarize(List<double> intervals)
    {
        if (intervals.Count == 0) return new(0, null, null, null, null);
        intervals.Sort();
        var p95 = intervals[(int)Math.Ceiling(intervals.Count * .95) - 1];
        return new(intervals.Count, intervals[0], intervals.Average(), p95, intervals[^1]);
    }

    private sealed class MutablePattern(int count, double firstMilliseconds, double lastMilliseconds)
    {
        public int Count { get; set; } = count;
        public double FirstMilliseconds { get; } = firstMilliseconds;
        public double LastMilliseconds { get; set; } = lastMilliseconds;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(IntPtr handle, IntPtr overlapped);
    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetInputReport(SafeFileHandle handle, byte[] report, int length);
}
