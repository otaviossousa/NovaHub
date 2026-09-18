namespace NovaLite.Core;

public sealed record SlotSnapshot(uint Slot, Guid? Session, uint ReturnCode, InputState? Input,
    DateTimeOffset SampleAt, BatteryReading Battery, Capabilities? Capabilities, uint? CapabilitiesCode);
public sealed record Observation(DateTimeOffset At, string Kind, uint Slot, Guid? Session, string Detail);

/// <summary>Owns session state and all motor commands. No UI thread is needed for the stop deadline.</summary>
public sealed class ControllerMonitor : IDisposable
{
    private readonly IControllerApi _api;
    private readonly object _gate = new();
    private readonly SlotSnapshot[] _slots;
    private readonly DateTimeOffset[] _nextRead = new DateTimeOffset[4];
    private readonly DateTimeOffset[] _nextBattery = new DateTimeOffset[4];
    private readonly Queue<Observation> _events = new();
    private readonly Timer _motorDeadline;
    private readonly CancellationTokenSource _cancel = new();
    private Task? _worker;
    private (uint Slot, Guid Session)? _rumble;
    private bool _disposed;
    private bool _continuous;
    public bool ContinuousVibration { get { lock (_gate) return _continuous; } }
    public bool VibrationActive { get { lock (_gate) return _rumble != null; } }

    public ControllerMonitor(IControllerApi api)
    {
        _api = api;
        _slots = Enumerable.Range(0, 4).Select(i => new SlotSnapshot((uint)i, null, XInputApi.Disconnected,
            null, DateTimeOffset.MinValue, BatteryReading.Disconnected, null, null)).ToArray();
        _motorDeadline = new Timer(_ => StopVibration(), null, Timeout.Infinite, Timeout.Infinite);
    }
    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _worker ??= Task.Run(async () =>
            {
                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(8));
                    do { Poll(DateTimeOffset.UtcNow); } while (await timer.WaitForNextTickAsync(_cancel.Token));
                }
                catch (OperationCanceledException) { }
            });
        }
    }
    public SlotSnapshot[] Snapshot()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            return _slots.Select(s => s.Battery.HasLevel && now - s.Battery.ValidAt > TimeSpan.FromSeconds(120)
                ? s with { Battery = s.Battery with { Status = BatteryStatus.Stale, Label = "Leitura de bateria antiga" } } : s).ToArray();
        }
    }
    public Observation[] Observations() { lock (_gate) return _events.ToArray(); }
    private void Log(DateTimeOffset at, string kind, uint slot, Guid? session, string detail)
    {
        _events.Enqueue(new(at, kind, slot, session, detail));
        while (_events.Count > 2000) _events.Dequeue();
    }
    public void Poll(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_disposed) return;
            for (uint i = 0; i < 4; i++)
            {
                if (now < _nextRead[i]) continue;
                var previous = _slots[i];
                var code = _api.Read(i, out var state);
                if (code != 0)
                {
                    if (previous.Input != null)
                    {
                        StopCore();
                        Log(now, "session-ended", i, previous.Session, $"XInputGetState: {code}");
                    }
                    else if (previous.ReturnCode != code) Log(now, "read-error", i, null, $"XInputGetState: {code}");
                    _slots[i] = new(i, null, code, null, now,
                        code == XInputApi.Disconnected ? BatteryReading.Disconnected : new(BatteryStatus.Error, "Sem comunicação com a API"), null, null);
                    _nextRead[i] = now.AddMilliseconds(500);
                    continue;
                }
                _nextRead[i] = now;
                var session = previous.Session ?? Guid.NewGuid();
                var caps = previous.Capabilities;
                var capsCode = previous.CapabilitiesCode;
                var battery = previous.Battery;
                if (previous.Session == null)
                {
                    capsCode = _api.Capabilities(i, out var found);
                    caps = capsCode == 0 ? found : null;
                    _nextBattery[i] = DateTimeOffset.MinValue;
                    Log(now, "session-started", i, session, "Slot XInput ativo; modelo e conexão não identificados");
                }
                if (now >= _nextBattery[i])
                {
                    var batteryCode = _api.Battery(i, out var raw);
                    battery = BatteryDecoder.Decode(batteryCode, raw, now);
                    if (battery.Status == BatteryStatus.Error && previous.Session == session)
                        battery = battery with { ValidAt = previous.Battery.ValidAt };
                    Log(now, "battery-observed", i, session, $"retorno={batteryCode}; tipo={(batteryCode == 0 ? raw.Type : null)}; nível={(batteryCode == 0 ? raw.Level : null)}; {battery.Label}");
                    // Match the working standalone monitor: refresh often enough for
                    // category changes to appear without requiring a manual action.
                    _nextBattery[i] = now.AddSeconds(5);
                }
                _slots[i] = new(i, session, code, state, now, battery, caps, capsCode);
            }
        }
    }
    public void RefreshBattery(uint slot)
    {
        lock (_gate) { if (slot < 4) _nextBattery[slot] = DateTimeOffset.MinValue; }
    }
    public bool StartVibration(uint slot, Guid session, double left, double right, TimeSpan duration, bool continuous = false)
    {
        lock (_gate)
        {
            if (_disposed || slot >= 4 || _slots[slot].Session != session) return false;
            if (!double.IsFinite(left) || !double.IsFinite(right) || duration <= TimeSpan.Zero || duration > TimeSpan.FromSeconds(5)) return false;
            StopCore();
            // Re-read immediately; do not command a slot known to have disappeared.
            if (_api.Read(slot, out _) != 0) return false;
            var code = _api.Vibrate(slot, (ushort)(Math.Clamp(left, 0, 1) * ushort.MaxValue), (ushort)(Math.Clamp(right, 0, 1) * ushort.MaxValue));
            Log(DateTimeOffset.UtcNow, "vibration-command", slot, session, $"retorno={code}; esquerda={left}; direita={right}; duração={(continuous ? "contínua" : duration.TotalSeconds + "s")}; resposta física não confirmada");
            if (code != 0) { _api.Vibrate(slot, 0, 0); return false; }
            _rumble = (slot, session);
            _continuous = continuous;
            _motorDeadline.Change(continuous ? Timeout.InfiniteTimeSpan : duration, Timeout.InfiniteTimeSpan);
            return true;
        }
    }
    public void StopVibration() { lock (_gate) { if (!_disposed) StopCore(); } }
    private void StopCore()
    {
        _continuous = false;
        _motorDeadline.Change(Timeout.Infinite, Timeout.Infinite);
        if (_rumble is not { } running) return;
        _rumble = null;
        var code = _api.Vibrate(running.Slot, 0, 0);
        Log(DateTimeOffset.UtcNow, "vibration-stop", running.Slot, running.Session, $"retorno={code}");
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            StopCore();
            _disposed = true;
            _cancel.Cancel();
            _motorDeadline.Dispose();
        }
        _worker?.GetAwaiter().GetResult();
        _cancel.Dispose();
    }
}


