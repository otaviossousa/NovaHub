using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NovaLite.Core;

namespace NovaLite.Desktop;

public partial class MainWindow : Window
{
    private readonly ControllerMonitor _monitor = new(new XInputApi());
    private readonly WindowsDevices _windows = new();
    private readonly DeviceTopologyMonitor _topology = new();
    private readonly DispatcherTimer _render = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly TrayBatteryIcon _tray;
    private bool _allowExit;
    private Guid? _shownSession;
    private bool _initializingLanguage = true;
    private uint Selected => (uint)Math.Clamp(DeviceSelector.SelectedIndex, 0, 3);
    public MainWindow()
    {
        LanguageManager.Apply(LanguageManager.LoadPreference());
        ThemeManager.Apply(ThemeManager.LoadPreference());
        InitializeComponent();
        UpdateHeaderBrand();
        _tray = new TrayBatteryIcon();
        _tray.ShowRequested += (_, _) => RestoreFromTray();
        _tray.ExitRequested += (_, _) => { _allowExit = true; Close(); };
        foreach (var theme in ThemeManager.Themes)
            ThemeSelector.Items.Add(new ComboBoxItem { Content = LanguageManager.ThemeLabel(theme.Key), Tag = theme.Key });
        ThemeSelector.SelectedIndex = Array.FindIndex(ThemeManager.Themes,
            theme => theme.Key == ThemeManager.CurrentKey);
        foreach (var language in LanguageManager.Languages)
            LanguageSelector.Items.Add(new ComboBoxItem { Content = language.Label, Tag = language.Key });
        LanguageSelector.SelectedIndex = Array.FindIndex(LanguageManager.Languages,
            language => language.Key == LanguageManager.CurrentKey);
        _initializingLanguage = false;
        InitializeTester();
        for (var i = 0; i < 4; i++) DeviceSelector.Items.Add(new ComboBoxItem { Content = ControllerStatus(i + 1, LanguageManager.Get("Consulting")) });
        DeviceSelector.SelectedIndex = 0;
        _render.Tick += (_, _) => Render();
        _monitor.Start(); _windows.Start(); _topology.Start(); _render.Start();
        Closing += (_, e) =>
        {
            if (_allowExit) return;
            e.Cancel = true;
            HideToTray();
        };
        Closed += (_, _) => { _render.Stop(); _monitor.Dispose(); _windows.Dispose(); _topology.Dispose(); _tray.Dispose(); };
        Deactivated += (_, _) => _monitor.StopVibration();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) { _monitor.StopVibration(); HideToTray(); } };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            _monitor.StopVibration();
            if (SettingsPage.Visibility == Visibility.Visible) ShowTestPage();
            e.Handled = true;
        };
        Loaded += (_, _) => SetInitialWindowBounds();
    }

    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is not ComboBoxItem { Tag: string key }) return;
        ThemeManager.Apply(key);
        ThemeManager.SavePreference(key);
        UpdateHeaderBrand();
        _batteryDrawn = false;
        ControllerDrawing?.InvalidateVisual();
        InvalidateThemeDrawings(this);
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingLanguage || LanguageSelector.SelectedItem is not ComboBoxItem { Tag: string key }) return;
        LanguageManager.Apply(key);
        LanguageManager.SavePreference(key);
        foreach (ComboBoxItem item in ThemeSelector.Items)
            if (item.Tag is string themeKey) item.Content = LanguageManager.ThemeLabel(themeKey);
        UpdateStickTestButtons();
        _tray.InvalidateLanguage();
        Render();
    }

    private void UpdateHeaderBrand()
    {
        var color = ThemeManager.CurrentKey == "stellar-white" ? "Black" : "White";
        HeaderIcon.Source = LoadBrandAsset($"NovaHub_{color}_Icon.png");
        HeaderLogo.Source = LoadBrandAsset($"NovaHub_Logo_{color}_No_Icon.png");
    }

    private static BitmapImage LoadBrandAsset(string fileName) => new(
        new Uri($"pack://application:,,,/Assets/{fileName}", UriKind.Absolute));

    private static void InvalidateThemeDrawings(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is CombinationGlyph or StickPlot or ProgressButton or ControllerView)
                ((UIElement)child).InvalidateVisual();
            InvalidateThemeDrawings(child);
        }
    }
    private void SetInitialWindowBounds()
    {
        var workArea = SystemParameters.WorkArea;
        WindowState = WindowState.Normal;
        Width = Math.Min(1160, workArea.Width);
        Height = workArea.Height;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top;
    }
    private void ShowSettings(object sender, RoutedEventArgs e)
    {
        TestPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Visible;
        SettingsButton.Visibility = Visibility.Collapsed;
        PageScroll.ScrollToTop();
    }
    private void HideSettings(object sender, RoutedEventArgs e) => ShowTestPage();
    private void ShowTestPage()
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        TestPage.Visibility = Visibility.Visible;
        SettingsButton.Visibility = Visibility.Visible;
        PageScroll.ScrollToTop();
    }
    private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void HideToTray()
    {
        _monitor.StopVibration();
        Hide();
    }
    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    private void OpenOfficial(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo((string)((Button)sender).Tag) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception ex) { MessageBox.Show(this, LanguageManager.Format("UnableOpenBrowser", ex.Message)); }
    }
    private void Render()
    {
        UpdateInputDevices();
        var slots = _monitor.Snapshot();
        _tray.Update(slots, _windows.Snapshot.Devices);
        UpdateTesterSlots(slots);
        foreach (var item in slots)
            ((ComboBoxItem)DeviceSelector.Items[(int)item.Slot]).Content = ControllerStatus((int)item.Slot + 1,
                item.Input != null ? LanguageManager.Get("Active") :
                item.ReturnCode == XInputApi.Disconnected ? LanguageManager.Get("NoController") : LanguageManager.Get("ApiError"));
        if (WindowsKey != null) { RenderWindowsInput(); return; }
        var slot = slots[Selected];
        if (slot.Session != _shownSession)
        {
            ControllerDrawing.ClearTrails();
            _shownSession = slot.Session;

        }
        // An old sample is not rendered as live if the reader stalls.
        bool live = slot.Input != null && DateTimeOffset.UtcNow - slot.SampleAt < TimeSpan.FromSeconds(1);
        var gamepad = live ? slot.Input?.Gamepad : null;
        var physical = ResolveXInputPhysical(slots);
        PresentConnection(live, physical?.Transport.Connection ?? PhysicalConnection.Unknown);
        ControllerDrawing.SetInput(gamepad); PresentInputs(gamepad);
        var presentedBattery = SelectBattery(slot.Battery, physical);
        RenderBattery(presentedBattery);
        VibrationPanel.IsEnabled = live;
        VibrationPanel.Opacity = live ? 1 : .45;
        MotorButtons.IsEnabled = live;
    }
    private static string ControllerStatus(int number, string status) =>
        $"{LanguageManager.Get("Controller")} {number} • {status}";
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _monitor.StopVibration();
        if (ControllerDrawing != null) ControllerDrawing.ClearTrails();
        _shownSession = null;

    }
    private void StartMotor(object sender, RoutedEventArgs e)
    {
        if (WindowsKey != null) return;
        var slot = _monitor.Snapshot()[Selected];
        if (slot.Session is not { } session) return;
        var channel = (string)((Button)sender).Tag;
        if (_monitor.StartVibration(Selected, session, channel == "right" ? 0 : Intensity.Value / 100,
            channel == "left" ? 0 : Intensity.Value / 100, TimeSpan.FromSeconds(1)))
        {
            if (_progressButton != null) _progressButton.Progress = 0;
            _progressButton = (ProgressButton)sender; _vibrationClock.Restart(); _progressButton.Progress = .015;
        }
    }
}










