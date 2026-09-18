using NovaLite.Core;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NovaLite.Desktop;

public sealed record PhysicalBatteryReading(
    BatteryLevel Level,
    int Percent,
    DateTimeOffset ReadAt,
    string Source,
    string Evidence);

public sealed record PhysicalControllerDevice(
    string Key,
    Guid? ContainerId,
    ushort VendorId,
    ushort ProductId,
    int? XInputInterfaceIndex,
    string Model,
    ConnectionAssessment Transport,
    bool Has24GDescriptor,
    PhysicalBatteryReading? Battery,
    string Evidence,
    string? PhysicalInstanceId,
    DateTimeOffset? PhysicalLastArrival);

public sealed record PhysicalControllerInventory(
    DateTimeOffset At,
    PhysicalControllerDevice[] Devices,
    string? Error = null)
{
    public static PhysicalControllerInventory Waiting => new(DateTimeOffset.MinValue, []);

    public PhysicalControllerDevice? UniqueMatch(ushort vendorId, ushort productId)
    {
        var matches = Devices.Where(d => d.VendorId == vendorId && d.ProductId == productId).ToArray();
        if (matches.Length == 1) return matches[0];

        // Windows can retain an older Bluetooth HID container after reconnecting the
        // same controller. It is still safe to reuse transport/model evidence when all
        // matching containers agree and none carries device-specific battery data.
        if (matches.Length > 1 && matches.All(d => d.Battery == null) &&
            matches.Select(d => d.Transport.Connection).Distinct().Count() == 1 &&
            matches.Select(d => d.Model).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            return matches[0];

        return null;
    }
}

/// <summary>
/// Periodically builds a read-only physical-controller inventory. This runs outside the
/// input loop so SetupAPI/HID enumeration cannot delay XInput polling or rumble deadlines.
/// </summary>
public sealed class DeviceTopologyMonitor : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private PhysicalControllerInventory _snapshot = PhysicalControllerInventory.Waiting;
    private Task? _worker;
    private string _transportFingerprint = "";
    private IReadOnlyDictionary<Guid, ConnectionAssessment> _automaticUsbTransports =
        new Dictionary<Guid, ConnectionAssessment>();

    public PhysicalControllerInventory Snapshot => Volatile.Read(ref _snapshot);

    public void Start() => _worker ??= Task.Run(async () =>
    {
        try
        {
            while (!_cancel.IsCancellationRequested)
            {
                Refresh();
                await Task.Delay(TimeSpan.FromSeconds(4), _cancel.Token);
            }
        }
        catch (OperationCanceledException) { }
    });

    public static PhysicalControllerInventory Build(NativeTopologySnapshot native,
        IReadOnlyDictionary<Guid, ConnectionAssessment>? automaticUsbTransports = null)
    {
        var nodes = native.RelevantNodes.ToDictionary(n => n.InstanceId, StringComparer.OrdinalIgnoreCase);
        var collections = native.HidCollections.Select(h => new
        {
            Hid = h,
            Container = new[] { h.InstanceId }.Concat(h.Ancestors)
                .Select(id => nodes.GetValueOrDefault(id)?.ContainerId)
                .FirstOrDefault(id => id != null)
        }).ToArray();

        var devices = new List<PhysicalControllerDevice>();
        foreach (var group in collections.GroupBy(x => x.Container?.ToString("D") ?? x.Hid.InstanceId,
                     StringComparer.OrdinalIgnoreCase))
        {
            var hids = group.Select(x => x.Hid).ToArray();
            var gamepad = hids.FirstOrDefault(IsGamepadCollection);
            if (gamepad == null) continue;

            var vid = gamepad.VendorId;
            var pid = gamepad.ProductId;
            if (vid == null || pid == null) continue;

            var transport = hids.Select(h => h.Transport)
                .OrderByDescending(t => t.Connection == PhysicalConnection.Bluetooth)
                .ThenByDescending(t => t.Connection != PhysicalConnection.Unknown)
                .First();
            var has24G = hids.Any(h => Contains24G(h.Manufacturer) || Contains24G(h.Product));
            // Both Nova Lite transports expose the same "2.4G" auxiliary descriptor.
            // Only a passive timing measurement can distinguish them automatically.
            var ambiguousNovaLite24G = vid == 0x3537 && pid == 0x1040;
            if (transport.Connection == PhysicalConnection.UsbUnresolved && has24G && !ambiguousNovaLite24G)
                transport = new(PhysicalConnection.Dongle24Ghz, "alta",
                    "A cadeia PnP é USB e uma interface do mesmo dispositivo se identifica explicitamente como 2.4G.",
                    "descritor-hid-2.4g");
            var container = group.Select(x => x.Container).FirstOrDefault(x => x != null);
            if (container is { } transportContainer &&
                automaticUsbTransports?.GetValueOrDefault(transportContainer) is { } automaticTransport)
                transport = automaticTransport;
            var chainIds = hids.SelectMany(h => new[] { h.InstanceId }.Concat(h.Ancestors)).Distinct().ToArray();
            var physicalUsbNode = chainIds.Select(id => nodes.GetValueOrDefault(id))
                .FirstOrDefault(n => n != null && IsPhysicalUsbDevice(n.InstanceId));
            var xinputIndex = ParseXInputInterfaceIndex(gamepad.InstanceId);
            var containerFriendlyName = container is { } modelContainer
                ? nodes.Values.Where(n => n.ContainerId == modelContainer)
                    .Select(n => n.FriendlyName)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) &&
                        s.Contains("GameSir", StringComparison.OrdinalIgnoreCase))
                : null;
            var model = containerFriendlyName
                ?? hids.Select(h => h.Product).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                ?? hids.SelectMany(h => new[] { h.InstanceId }.Concat(h.Ancestors))
                    .Select(id => nodes.GetValueOrDefault(id)?.FriendlyName ?? nodes.GetValueOrDefault(id)?.Description)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                ?? "Controle sem modelo informado";
            model = ControllerModes.DisplayName(vid.Value, pid.Value, transport.Connection) ?? model;
            var battery = hids.SelectMany(h => h.StandardBattery)
                .Select(b => DecodeStandardBattery(b, native.CreatedAt))
                .FirstOrDefault(b => b != null);
            var interfaces = string.Join(", ", hids.Select(h =>
                $"{h.VendorId:X4}:{h.ProductId:X4} {h.UsagePage:X2}:{h.Usage:X2}").Distinct());
            var evidence = $"Container={container?.ToString("D") ?? "ausente"}; HID={interfaces}" +
                           (has24G ? "; descritor do dispositivo contém 2.4G" : "");
            devices.Add(new(group.Key, container, vid.Value, pid.Value, xinputIndex, model, transport, has24G,
                battery, evidence, physicalUsbNode?.InstanceId, physicalUsbNode?.LastArrival));
        }
        return new(native.CreatedAt, devices.ToArray());
    }

    private static bool IsGamepadCollection(HidCollection h) =>
        h.UsagePage == 0x01 && h.Usage is 0x04 or 0x05 or 0x08;

    private static bool Contains24G(string? value) =>
        value?.Contains("2.4G", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains("2.4 GHz", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsPhysicalUsbDevice(string instanceId) =>
        Regex.IsMatch(instanceId, @"^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\", RegexOptions.IgnoreCase) &&
        !instanceId.Contains("&MI_", StringComparison.OrdinalIgnoreCase) &&
        !instanceId.Contains("&IG_", StringComparison.OrdinalIgnoreCase);

    public void Refresh()
    {
        try
        {
            var native = NativeDeviceTopology.Capture();
            var fingerprint = string.Join("|", native.RelevantNodes
                .Where(n => n.InstanceId.StartsWith("USB\\VID_3537&PID_1040\\", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(n => $"{n.InstanceId}:{n.LastArrival:O}"));
            if (!string.Equals(fingerprint, _transportFingerprint, StringComparison.Ordinal))
            {
                _automaticUsbTransports = HidVendorInputCapture.DetectNovaLiteUsbTransportAsync(
                    native, TimeSpan.FromMilliseconds(750)).GetAwaiter().GetResult();
                _transportFingerprint = fingerprint;
            }
            Volatile.Write(ref _snapshot, Build(native, _automaticUsbTransports));
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _snapshot, new(DateTimeOffset.UtcNow, [], $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static int? ParseXInputInterfaceIndex(string instanceId)
    {
        var match = Regex.Match(instanceId, @"&IG_([0-9A-Fa-f]{2})\\");
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out var index) ? index : null;
    }

    private static PhysicalBatteryReading? DecodeStandardBattery(HidBatteryObservation observation,
        DateTimeOffset at)
    {
        // Battery Strength is percentage only when the HID descriptor declares the exact 0..100 range.
        if (observation.RawValue is not { } raw || observation.LogicalMinimum != 0 ||
            observation.LogicalMaximum != 100 || raw > 100) return null;
        var percent = (int)raw;
        var level = percent switch
        {
            0 => BatteryLevel.Empty,
            <= 20 => BatteryLevel.Low,
            <= 60 => BatteryLevel.Medium,
            _ => BatteryLevel.Full
        };
        return new(level, percent, at, $"HID Battery Strength {observation.UsagePage:X2}:{observation.Usage:X2}",
            $"Report {observation.ReportType}, ID {observation.ReportId}, faixa lógica 0..100");
    }

    public void Dispose()
    {
        _cancel.Cancel();
        _cancel.Dispose();
    }
}
