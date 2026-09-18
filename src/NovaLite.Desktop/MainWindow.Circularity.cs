using NovaLite.Core;
namespace NovaLite.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<string, CircularitySession> _circles = new();
    private readonly Dictionary<string, RotationCircularitySession> _rotationCircles = new();
    private readonly string?[] _xCircleKeys = new string?[4];
    private string? SelectedCircleKey => WindowsKey is { } key ? "w:" + key : _xCircleKeys[Selected];
    private CircularitySession? SelectedCircle => SelectedCircleKey is { } key && _circles.TryGetValue(key, out var state) ? state : null;
    private RotationCircularitySession? SelectedRotationCircle => SelectedCircleKey is { } key && _rotationCircles.TryGetValue(key, out var state) ? state : null;
    private void StopAllStickTests()
    {
        foreach (var state in _circles.Values) state.SetActive(false);
        foreach (var state in _rotationCircles.Values) state.SetActive(false);
    }
    private void UpdateStickTestButtons()
    {
        var precisionActive = SelectedCircle?.Active == true;
        var circularityActive = SelectedRotationCircle?.Active == true;
        CircularityButton.IsChecked = precisionActive;
        RotationTestButton.IsChecked = circularityActive;
        CircularityButton.Content = LanguageManager.Get(precisionActive ? "StopTest" : "PrecisionTest");
        RotationTestButton.Content = LanguageManager.Get(circularityActive ? "StopTest" : "CircularityTest");
    }
    private void TrackCircularity(SlotSnapshot[] slots, WindowsInventory inventory)
    {
        var present = new HashSet<string>();
        foreach (var slot in slots)
        {
            var key = slot.Session is { } session && slot.Input != null && DateTimeOffset.UtcNow - slot.SampleAt < TimeSpan.FromSeconds(1) ? $"x:{slot.Slot}:{session}" : null;
            _xCircleKeys[slot.Slot] = key;
            if (key == null) continue;
            present.Add(key);
            if (!_circles.TryGetValue(key, out var state)) _circles[key] = state = new();
            if (!_rotationCircles.TryGetValue(key, out var rotation)) _rotationCircles[key] = rotation = new();
            state.Observe(slot.Input!.Value.Gamepad);
            rotation.Observe(slot.Input!.Value.Gamepad);
        }
        if (DateTimeOffset.UtcNow - inventory.At < TimeSpan.FromSeconds(1))
            foreach (var device in inventory.Devices)
            {
                var key = "w:" + device.Key; present.Add(key);
                if (!_circles.TryGetValue(key, out var state)) _circles[key] = state = new();
                if (!_rotationCircles.TryGetValue(key, out var rotation)) _rotationCircles[key] = rotation = new();
                if (IsActive && (device.ProtocolInput != null || device.StandardReading != null || device.ReadingTimestamp != 0 && device.ReadingError == null) && ReadWindowsPad(device) is { } pad)
                {
                    state.Observe(pad);
                    rotation.Observe(pad);
                }
            }
        foreach (var old in _circles.Keys.Where(k => !present.Contains(k)).ToArray()) _circles.Remove(old);
        foreach (var old in _rotationCircles.Keys.Where(k => !present.Contains(k)).ToArray()) _rotationCircles.Remove(old);
    }
    private void ShowCircularity(Gamepad? pad)
    {
        var state = SelectedCircle;
        CircularityButton.IsEnabled = state != null;
        var rotation = SelectedRotationCircle;
        RotationTestButton.IsEnabled = rotation != null;
        UpdateStickTestButtons();
        LeftPlot.Recording = RightPlot.Recording = state?.Active == true;
        LeftPlot.Show(pad is { } l ? Normalize.Axis(l.LX) : null, pad is { } ly ? Normalize.Axis(ly.LY) : null, state?.Left ?? []);
        RightPlot.Show(pad is { } r ? Normalize.Axis(r.RX) : null, pad is { } ry ? Normalize.Axis(ry.RY) : null, state?.Right ?? []);
        LeftPlot.ShowPrecision(rotation?.Left);
        RightPlot.ShowPrecision(rotation?.Right);
    }
    private ProgressButton? _progressButton;
    private readonly System.Diagnostics.Stopwatch _vibrationClock = new();
    private void UpdateMotorProgress()
    {
        if (_progressButton == null) return;
        if (!_monitor.VibrationActive || _monitor.ContinuousVibration || _vibrationClock.Elapsed.TotalSeconds >= 1)
        { _progressButton.Progress = 0; _progressButton = null; _vibrationClock.Reset(); return; }
        _progressButton.Progress = Math.Max(.015, _vibrationClock.Elapsed.TotalSeconds);
    }
}
