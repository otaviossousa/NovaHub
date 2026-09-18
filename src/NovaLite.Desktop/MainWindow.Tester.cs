using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NovaLite.Core;
namespace NovaLite.Desktop;

public partial class MainWindow
{
    private readonly Button[] _slotTabs = new Button[4];
    private readonly int[] _tabSources = [-1, -1, -1, -1];
    private readonly Border[] _buttonCells = new Border[16];
    private readonly TextBlock[] _buttonValues = new TextBlock[16];
    private static readonly Buttons[] DigitalButtons =
    [
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LB, Buttons.RB, 0, 0,
        Buttons.LS, Buttons.RS, Buttons.View, Buttons.Menu,
        Buttons.Up, Buttons.Down, Buttons.Left, Buttons.Right
    ];
    private void InitializeTester()
    {
        for (int i = 0; i < 4; i++)
        {
            var index = i;
            var label = new TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 };
            var button = new Button { Content = label, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(12, 14, 12, 14) };
            button.Click += (_, _) => { if (_tabSources[index] >= 0) DeviceSelector.SelectedIndex = _tabSources[index]; };
            _slotTabs[i] = button; SlotTabs.Children.Add(button);
        }
        string[] names = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS", "View", "Menu", "↑", "↓", "←", "→"];
        for (int i = 0; i < 16; i++)
        {
            var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = names[i], FontSize = 11, Foreground = Brushes.LightGray });
            var value = new TextBlock { Text = "—", FontSize = 14 }; panel.Children.Add(value); _buttonValues[i] = value;
            var cell = new Border { Child = panel, Padding = new Thickness(9, 3, 9, 3), Margin = new Thickness(2), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1) };
            _buttonCells[i] = cell; ButtonGrid.Children.Add(cell);
        }
    }
    private void UpdateTesterSlots(SlotSnapshot[] slots, WindowsInventory? inventoryOverride = null)
    {
        Array.Fill(_tabSources, -1);
        string[] names = new string[4];
        var xinputIdentity = ResolveUniqueWindowsIdentity(slots);
        for (int i = 0; i < 4; i++) if (slots[i].Input != null && DateTimeOffset.UtcNow - slots[i].SampleAt < TimeSpan.FromSeconds(1)) { _tabSources[i] = i; names[i] = ResolveXInputPhysical(slots, (uint)i)?.Model ?? xinputIdentity?.Name ?? "Controle XInput"; }
        var inv = inventoryOverride ?? _windows.Snapshot;
        TrackCircularity(slots, inv);
        if (DateTimeOffset.UtcNow - inv.At < TimeSpan.FromSeconds(1))
        {
            var visibleWindowsDevices = WindowsDeviceFilter.VisibleDevices(
                inv.Devices, slots.Any(s => s.Input != null));
            foreach (var device in visibleWindowsDevices)
            {
                var free = Array.IndexOf(_tabSources, -1); if (free < 0) break;
                var index = DeviceSelector.Items.Cast<ComboBoxItem>().ToList().FindIndex(x => x.Tag as string == device.Key);
                if (index < 0) continue;
                _tabSources[free] = index; names[free] = ResolvePhysical(device)?.Model ?? device.Name;
            }
        }
        int count = _tabSources.Count(i => i >= 0);
        if (count > 0 && !_tabSources.Contains(DeviceSelector.SelectedIndex)) DeviceSelector.SelectedIndex = _tabSources.First(i => i >= 0);
        SlotTabs.Columns = Math.Max(1, count);
        SlotTabs.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TesterContent.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ButtonCombinationsPanel.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        for (int i = 0; i < 4; i++)
        {
            var active = _tabSources[i] >= 0; var selected = active && _tabSources[i] == DeviceSelector.SelectedIndex;
            _slotTabs[i].IsEnabled = active;
            _slotTabs[i].Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            ((TextBlock)_slotTabs[i].Content).Text = active ? $"{LanguageManager.Get("Controller")} {i + 1} · {names[i]}" : "";
            _slotTabs[i].Foreground = active ? Brushes.White : Brushes.Gray;
            _slotTabs[i].Opacity = active ? 1 : .45;
            _slotTabs[i].Background = ThemeManager.Brush(selected ? "SelectedSurfaceBrush" : "PanelBrush", selected ? "#294A46" : "#19232C");
            _slotTabs[i].BorderBrush = ThemeManager.Brush(selected ? "AccentBrush" : "BorderBrush", selected ? "#90E3C8" : "#30414E");
            _slotTabs[i].FontWeight = selected ? FontWeights.Bold : FontWeights.Normal;
        }
        if (count == 0) { _monitor.StopVibration(); PresentInputs(null); }
    }
    private void PresentConnection(bool connected, PhysicalConnection connection)
    {
        ConnectionText.Text = LanguageManager.Get("Connection");
        UsbCableConnectionIcon.Visibility = connection == PhysicalConnection.UsbCable ? Visibility.Visible : Visibility.Collapsed;
        DongleConnectionIcon.Visibility = connection == PhysicalConnection.Dongle24Ghz ? Visibility.Visible : Visibility.Collapsed;
        BluetoothConnectionIcon.Visibility = connection == PhysicalConnection.Bluetooth ? Visibility.Visible : Visibility.Collapsed;
        var unresolved = connection is PhysicalConnection.Unknown or PhysicalConnection.UsbUnresolved;
        UnknownConnectionIcon.Visibility = unresolved ? Visibility.Visible : Visibility.Collapsed;
        ConnectionButton.Cursor = unresolved && connected ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
        ConnectionButton.IsHitTestVisible = unresolved && connected;
    }
    private void PresentInputs(Gamepad? pad)
    {
        for (int i = 0; i < 16; i++)
        {
            double? value = pad is { } p ? i == 6 ? Normalize.Trigger(p.LT) : i == 7 ? Normalize.Trigger(p.RT) : p.Buttons.HasFlag(DigitalButtons[i]) ? 1 : 0 : null;
            _buttonValues[i].Text = value?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "—";
            _buttonCells[i].Background = ThemeManager.Brush(value > 0 ? "SelectedSurfaceBrush" : "SurfaceBrush", value > 0 ? "#294A46" : "#202F39");
            _buttonCells[i].BorderBrush = ThemeManager.Brush(value > 0 ? "AccentBrush" : "BorderBrush", value > 0 ? "#90E3C8" : "#30414E");
        }
        ShowCircularity(pad); UpdateMotorProgress();
        InfiniteButton.Background = ThemeManager.Brush(_monitor.ContinuousVibration ? "ActiveSurfaceBrush" : "ButtonBrush", _monitor.ContinuousVibration ? "#367765" : "#2B414F");
        InfiniteButton.Content = ContinuousButtonText(_monitor.ContinuousVibration);
    }
    private void ToggleCircularity(object sender, RoutedEventArgs e)
    {
        var toggle = (System.Windows.Controls.Primitives.ToggleButton)sender;
        var activate = toggle.IsChecked == true;
        var state = SelectedCircle;
        if (activate) StopAllStickTests();
        state?.SetActive(activate);
        LeftPlot.Recording = RightPlot.Recording = state?.Active == true;
        if (state?.Active != true) { LeftPlot.Clear(); RightPlot.Clear(); }
        LeftPlot.ShowPrecision(SelectedRotationCircle?.Left);
        RightPlot.ShowPrecision(SelectedRotationCircle?.Right);
        UpdateStickTestButtons();
    }
    private void ToggleRotationTest(object sender, RoutedEventArgs e)
    {
        var toggle = (System.Windows.Controls.Primitives.ToggleButton)sender;
        var activate = toggle.IsChecked == true;
        var state = SelectedRotationCircle;
        if (activate) StopAllStickTests();
        state?.SetActive(activate);
        LeftPlot.Recording = RightPlot.Recording = false;
        LeftPlot.Clear(); RightPlot.Clear();
        LeftPlot.ShowPrecision(state?.Left);
        RightPlot.ShowPrecision(state?.Right);
        UpdateStickTestButtons();
    }
    private void QuickVibration(object sender, RoutedEventArgs e)
    {
        bool infinite = (string)((Button)sender).Tag == "infinite";
        if (infinite && _monitor.ContinuousVibration) { _monitor.StopVibration(); return; }
        if (WindowsKey != null) return;
        var slot = _monitor.Snapshot()[Selected];
        if (slot.Session is { } session) _monitor.StartVibration(Selected, session, infinite ? Intensity.Value / 100 : .5, infinite ? Intensity.Value / 100 : .5, TimeSpan.FromSeconds(1), infinite);
    }
    private static string ContinuousButtonText(bool active) => LanguageManager.Get(active ? "Stop" : "Continuous");
}






