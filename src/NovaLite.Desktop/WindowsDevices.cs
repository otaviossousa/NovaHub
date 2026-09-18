using System.Security.Cryptography;
using System.Text;
using Windows.Gaming.Input;
using NovaLite.Core;

namespace NovaLite.Desktop;

public sealed record WindowsBattery(string Status, string Label, int? Percent = null,
    BatteryLevel? Category = null, DateTimeOffset? ReadAt = null);
public sealed record WindowsDevice(string Key, string Name, ushort VendorId, ushort ProductId,
    bool IsWirelessReported, int ButtonCount, int AxisCount, int SwitchCount, WindowsBattery Battery,
    bool[] Buttons, double[] Axes, string[] Switches, ulong ReadingTimestamp, string? ReadingError,
    bool HasStandardGamepad, GamepadReading? StandardReading, NovaLite.Core.Gamepad? ProtocolInput);
public sealed record WindowsInventory(DateTimeOffset At, WindowsDevice[] Devices);

internal static class WindowsDeviceFilter
{
    public static WindowsDevice[] VisibleDevices(WindowsDevice[] devices, bool xInputActive) =>
        xInputActive
            ? devices.Where(device => ControllerProfiles.IsNovaLiteSwitchMode(device.VendorId, device.ProductId)).ToArray()
            : devices;
}

/// <summary>Read-only WinRT discovery. It never associates enumeration order with an XInput slot.</summary>
public sealed class WindowsDevices : IDisposable
{
    private WindowsInventory _snapshot = new(DateTimeOffset.MinValue, []);
    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;
    private Task? _ds4Worker;
    private Task? _switchWorker;
    private Ds4BatterySample? _ds4Battery;
    private SwitchInputSample? _switchInput;
    private readonly Dictionary<string, WindowsBattery> _batteryCache = new();
    public WindowsInventory Snapshot => Volatile.Read(ref _snapshot);
    public void Start()
    {
        _worker ??= Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(33));
                do { Volatile.Write(ref _snapshot, Read()); } while (await timer.WaitForNextTickAsync(_cancel.Token));
            }
            catch (OperationCanceledException) { }
        });
        _ds4Worker ??= Task.Run(async () =>
        {
            try
            {
                while (!_cancel.IsCancellationRequested)
                {
                    var hasDs4 = Snapshot.Devices.Any(d => d.VendorId == 0x054C && d.ProductId == 0x09CC);
                    if (hasDs4)
                    {
                        await Ds4BatteryProbe.CaptureAsync(TimeSpan.FromSeconds(30),
                            sample => Volatile.Write(ref _ds4Battery, sample), retainSamples: false);
                    }
                    else
                        await Task.Delay(TimeSpan.FromMilliseconds(250), _cancel.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
        _switchWorker ??= Task.Run(async () =>
        {
            try
            {
                while (!_cancel.IsCancellationRequested)
                {
                    var hasSwitch = Snapshot.Devices.Any(d =>
                        ControllerProfiles.IsNovaLiteSwitchMode(d.VendorId, d.ProductId));
                    if (hasSwitch)
                        await SwitchInputProbe.CaptureAsync(TimeSpan.FromSeconds(30),
                            sample => Volatile.Write(ref _switchInput, sample), retainSamples: false);
                    else
                        await Task.Delay(TimeSpan.FromMilliseconds(250), _cancel.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
    }
    private WindowsInventory Read()
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            var devices = new List<WindowsDevice>();
            foreach (var raw in RawGameController.RawGameControllers.ToArray())
            {
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw.NonRoamableId)))[..16];
                WindowsBattery battery;
                if (_batteryCache.TryGetValue(key, out var cached) && now - cached.ReadAt < TimeSpan.FromSeconds(60)) battery = cached;
                else
                {
                    try
                    {
                        var report = raw.TryGetBatteryReport();
                        battery = report == null ? new("Unavailable", "O Windows não forneceu relatório de bateria") : new(
                            report.Status.ToString(), report.Status switch
                            {
                                Windows.System.Power.BatteryStatus.NotPresent => "Sem bateria exposta por esta interface",
                                Windows.System.Power.BatteryStatus.Charging => "API informa carregamento • confirmar no hardware",
                                Windows.System.Power.BatteryStatus.Discharging => "API informa uso da bateria • nível não validado",
                                _ => "Estado recebido • nível de carga não validado"
                            });
                    }
                    catch (Exception) { battery = new("Error", "Não foi possível consultar a bateria"); }
                    battery = battery with { ReadAt = now };
                    _batteryCache[key] = battery;
                }
                if (raw.HardwareVendorId == 0x054C && raw.HardwareProductId == 0x09CC &&
                    Volatile.Read(ref _ds4Battery) is { } ds4 && now - ds4.At < TimeSpan.FromSeconds(10) &&
                    ds4.ApproximatePercent is { } percent)
                    battery = new(ds4.Charging == true ? "Charging" : "Discharging",
                        ds4.BatteryLabel ?? $"Cerca de {percent}% • estimativa DS4",
                        Percent: percent, ReadAt: ds4.At);
                var protocolSample = raw.HardwareVendorId == 0x054C && raw.HardwareProductId == 0x09CC &&
                    Volatile.Read(ref _ds4Battery) is { } inputSample && now - inputSample.At < TimeSpan.FromSeconds(1) &&
                    inputSample.Input is not null ? inputSample : null;
                var switchSample = ControllerProfiles.IsNovaLiteSwitchMode(raw.HardwareVendorId, raw.HardwareProductId) &&
                    Volatile.Read(ref _switchInput) is { } observedSwitch &&
                    now - observedSwitch.At < TimeSpan.FromSeconds(1) && observedSwitch.Input is not null
                    ? observedSwitch : null;
                if (switchSample?.Battery is { } switchBattery)
                {
                    var switchLabel = switchBattery switch
                    {
                        BatteryLevel.Empty => "Crítica/vazia • protocolo Switch",
                        BatteryLevel.Low => "Baixa • protocolo Switch",
                        BatteryLevel.Medium => "Média • protocolo Switch",
                        _ => "Cheia • protocolo Switch"
                    };
                    battery = new("Discharging", switchLabel, Category: switchBattery,
                        ReadAt: switchSample.At);
                }
                var buttons = new bool[raw.ButtonCount];
                var axes = new double[raw.AxisCount];
                var switches = new GameControllerSwitchPosition[raw.SwitchCount];
                ulong stamp = 0; string? error = null;
                try
                {
                    stamp = raw.GetCurrentReading(buttons, switches, axes);
                    if (stamp == 0)
                    {
                        error = "Ainda não há amostra com timestamp válido. Abra a janela e mova o controle.";
                        buttons = []; axes = []; switches = [];
                    }
                }
                catch (Exception ex) { error = $"{ex.GetType().Name}: 0x{ex.HResult:X8}"; buttons = []; axes = []; switches = []; }
                GamepadReading? standardReading = null;
                bool hasStandard = false;
                try
                {
                    var standard = Windows.Gaming.Input.Gamepad.FromGameController(raw);
                    hasStandard = standard != null;
                    if (standard != null)
                    {
                        var reading = standard.GetCurrentReading();
                        if (reading.Timestamp != 0) standardReading = reading;
                    }
                }
                catch (Exception) { }
                devices.Add(new(key, string.IsNullOrWhiteSpace(raw.DisplayName) ? "Controle sem nome informado" : raw.DisplayName,
                    raw.HardwareVendorId, raw.HardwareProductId, raw.IsWireless, raw.ButtonCount, raw.AxisCount, raw.SwitchCount,
                    battery, buttons, axes, switches.Select(s => s.ToString()).ToArray(), stamp, error, hasStandard, standardReading,
                    switchSample?.Input ?? protocolSample?.Input));
            }
            foreach (var absent in _batteryCache.Keys.Except(devices.Select(d => d.Key)).ToArray()) _batteryCache.Remove(absent);
            return new(now, devices.ToArray());
        }
        catch (Exception) { _batteryCache.Clear(); return new(now, []); }
    }
    public void Dispose()
    {
        _cancel.Cancel();
        // WinRT callbacks can be delayed by device/driver services. Do not block the window on shutdown.
        // The worker only reads; it does not own actuator commands or persist data.
    }
}

