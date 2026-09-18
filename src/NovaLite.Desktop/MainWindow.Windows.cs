using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NovaLite.Core;
using WButtons = Windows.Gaming.Input.GamepadButtons;

namespace NovaLite.Desktop;

public partial class MainWindow
{
    private string? WindowsKey => (DeviceSelector.SelectedItem as ComboBoxItem)?.Tag as string;
    private string _inputKeys = "";
    private WindowsDevice? SelectedWindowsDevice => _windows.Snapshot.Devices.FirstOrDefault(d => d.Key == WindowsKey);
    private WindowsDevice[] VisibleWindowsDevices()
    {
        var devices = _windows.Snapshot.Devices;
        var xinputActive = _monitor.Snapshot().Any(s => s.Input != null && DateTimeOffset.UtcNow - s.SampleAt < TimeSpan.FromSeconds(1));
        return WindowsDeviceFilter.VisibleDevices(devices, xinputActive);
    }
    private void UpdateInputDevices()
    {
        var inventory = _windows.Snapshot;
        var devices = DateTimeOffset.UtcNow - inventory.At < TimeSpan.FromSeconds(1) ? VisibleWindowsDevices() : [];
        var keys = string.Join("|", devices.Select(d => d.Key));
        if (keys == _inputKeys) return;
        var previous = WindowsKey;
        // Keep the selected identity on disconnection. Never silently switch to another physical device.
        for (int i = DeviceSelector.Items.Count - 1; i >= 4; i--)
            if (((ComboBoxItem)DeviceSelector.Items[i]).Tag as string != previous) DeviceSelector.Items.RemoveAt(i);
        foreach (var d in devices)
            if (!DeviceSelector.Items.Cast<ComboBoxItem>().Any(i => i.Tag as string == d.Key))
                DeviceSelector.Items.Add(new ComboBoxItem { Tag = d.Key, Content = $"Windows • {d.Name}" });
        _inputKeys = keys;
    }

    private BatteryLevel? _batteryDrawnLevel;
    private bool _batteryDrawn;
    private void RenderBattery(BatteryReading reading)
    {
        if (_batteryDrawn && _batteryDrawnLevel == reading.Level) return;
        _batteryDrawn = true; _batteryDrawnLevel = reading.Level;
        BatteryCategories.Children.Clear();
        var color = reading.Level switch
        {
            null or BatteryLevel.Empty => ThemeManager.Brush("BorderBrush", "#30414E"),
            _ => ThemeManager.Brush("AccentBrush", "#90E3C8")
        };
        for (int i = 0; i < 3; i++)
        {
            var filled = reading.Level is { } level && i < (int)level;
            BatteryCategories.Children.Add(new Border
            {
                Width = 13,
                Height = 18,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Background = filled ? color : ThemeManager.Brush("BorderBrush", "#30414E"),
                Child = new TextBlock { Text = reading.Level == null && i == 1 ? "?" : "", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.LightGray }
            });
        }
    }
    private void RenderWindowsInput()
    {
        var inventory = _windows.Snapshot;
        var d = SelectedWindowsDevice;
        bool current = DateTimeOffset.UtcNow - inventory.At < TimeSpan.FromSeconds(1);
        bool live = IsActive && current && d != null && (d.ProtocolInput != null || d.StandardReading != null || d.ReadingError == null && d.ReadingTimestamp != 0);
        var physical = d == null ? null : ResolvePhysical(d);
        VibrationPanel.IsEnabled = false;
        VibrationPanel.Opacity = .45;
        MotorButtons.IsEnabled = false;
        var windowsBattery = physical?.Battery is { } hid
            ? new BatteryReading(BatteryStatus.Valid, $"{hid.Percent}% informado por HID", hid.ReadAt, hid.ReadAt,
                RawType: 3, RawLevel: (byte)hid.Level)
            : d?.Battery.Percent is { } ds4Percent
                ? new BatteryReading(BatteryStatus.Valid, d.Battery.Label, d.Battery.ReadAt, d.Battery.ReadAt,
                    RawType: 3, RawLevel: (byte)(ds4Percent switch
                    {
                        0 => BatteryLevel.Empty,
                        <= 20 => BatteryLevel.Low,
                        <= 60 => BatteryLevel.Medium,
                        _ => BatteryLevel.Full
                    }))
            : d?.Battery.Category is { } switchLevel
                ? new BatteryReading(BatteryStatus.Valid, d.Battery.Label, d.Battery.ReadAt,
                    d.Battery.ReadAt, RawType: 3, RawLevel: (byte)switchLevel)
            : new(BatteryStatus.Unsupported, d == null ? "Controle desconectado" :
                ControllerModes.Identify(d.VendorId, d.ProductId) == NovaLiteProtocolMode.AndroidHid
                    ? "Indisponível • modo Android HID não envia bateria"
                    : "Nível não informado por fonte validada");
        RenderBattery(windowsBattery);
        PresentConnection(current && d != null,
            d == null ? PhysicalConnection.Unknown : physical?.Transport.Connection ?? PhysicalConnection.Unknown);
        var pad = live ? ReadWindowsPad(d!) : null;
        ControllerDrawing.SetInput(pad); PresentInputs(pad);
        if (!live) ControllerDrawing.ClearTrails();
    }
    private Gamepad? ReadWindowsPad(WindowsDevice d)
    {
        Gamepad? pad = null;
        if (d.ProtocolInput is { } protocol)
            pad = protocol;
        else if (d.StandardReading is { } r)
        {
            var buttons = (Buttons)0;
            foreach (var (source, target) in new (WButtons, Buttons)[] {
                (WButtons.A, Buttons.A), (WButtons.B, Buttons.B), (WButtons.X, Buttons.X), (WButtons.Y, Buttons.Y),
                (WButtons.Menu, Buttons.Menu), (WButtons.View, Buttons.View), (WButtons.LeftShoulder, Buttons.LB), (WButtons.RightShoulder, Buttons.RB),
                (WButtons.LeftThumbstick, Buttons.LS), (WButtons.RightThumbstick, Buttons.RS),
                (WButtons.DPadUp, Buttons.Up), (WButtons.DPadDown, Buttons.Down), (WButtons.DPadLeft, Buttons.Left), (WButtons.DPadRight, Buttons.Right) })
                if (r.Buttons.HasFlag(source)) buttons |= target;
            pad = new Gamepad
            {
                Buttons = buttons,
                LX = RawMapping.DisplayAxis(r.LeftThumbstickX),
                LY = RawMapping.DisplayAxis(r.LeftThumbstickY),
                RX = RawMapping.DisplayAxis(r.RightThumbstickX),
                RY = RawMapping.DisplayAxis(r.RightThumbstickY),
                LT = RawMapping.DisplayTrigger(r.LeftTrigger),
                RT = RawMapping.DisplayTrigger(r.RightTrigger)
            };
        }
        else if (ControllerProfiles.Create(d.VendorId, d.ProductId, d.ButtonCount, d.AxisCount,
                     d.SwitchCount) is { } automatic)
            pad = automatic.Convert(d.Buttons, d.Axes, d.Switches);
        return pad;
    }
}





