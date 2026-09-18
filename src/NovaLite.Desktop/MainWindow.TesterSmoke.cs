using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NovaLite.Core;
namespace NovaLite.Desktop;

public partial class MainWindow
{
    public async void RenderSmoke(string directory, bool windowsSource = false, string? themeKey = null,
        string? languageKey = null, bool showSettings = false)
    {
        try
        {
            await Task.Delay(1500);
            if (themeKey != null)
            {
                ThemeManager.Apply(themeKey);
                UpdateHeaderBrand();
                _batteryDrawn = false;
                InvalidateThemeDrawings(this);
            }
            if (languageKey != null)
            {
                LanguageManager.Apply(languageKey);
                _initializingLanguage = true;
                LanguageSelector.SelectedIndex = Array.FindIndex(LanguageManager.Languages,
                    language => language.Key == languageKey);
                _initializingLanguage = false;
                foreach (ComboBoxItem item in ThemeSelector.Items)
                    if (item.Tag is string key) item.Content = LanguageManager.ThemeLabel(key);
                UpdateStickTestButtons();
            }
            if (showSettings)
            {
                TestPage.Visibility = Visibility.Collapsed;
                SettingsPage.Visibility = Visibility.Visible;
                SettingsButton.Visibility = Visibility.Collapsed;
            }
            Render();
            UpdateLayout();
            if (windowsSource)
            {
                if (DeviceSelector.Items.Count <= 4)
                    throw new InvalidOperationException("Nenhuma interface Windows real disponível para este ensaio.");
                var visibleWindowsSource = _tabSources.FirstOrDefault(index => index >= 4);
                if (visibleWindowsSource < 4)
                    throw new InvalidOperationException("Nenhuma fonte Windows independente está visível como slot neste ensaio.");
                DeviceSelector.SelectedIndex = visibleWindowsSource;
                Render();
                if (WindowsKey == null || VibrationPanel.IsEnabled || MotorButtons.IsEnabled)
                    throw new InvalidOperationException("Fonte Windows habilitou operação XInput.");
            }
            Directory.CreateDirectory(directory);
            await Task.Delay(150);
            Render();
            UpdateLayout();
            SaveWindowImage(Path.Combine(directory, "mvp-page-0.png"));
            Reports.Write(Path.Combine(directory, "poc-smoke.json"), _monitor);
            VerifyTesterStates(directory);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "smoke-error.txt"), ex.ToString());
            Environment.ExitCode = 1;
        }
        finally
        {
            _allowExit = true;
            Close();
        }
    }

    private void SaveWindowImage(string path)
    {
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    // Only invoked by --smoke. Synthetic UI data never enters the hardware monitor or its reports.
    private void VerifyTesterStates(string directory)
    {
        _render.Stop();
        var now = DateTimeOffset.UtcNow;
        var inventory = new WindowsInventory(now, []);
        var slots = Enumerable.Range(0, 4).Select(i => new SlotSnapshot((uint)i, null, XInputApi.Disconnected, null, now, BatteryReading.Disconnected, null, null)).ToArray();
        void Assert(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
        void Save(string name)
        {
            UpdateLayout(); var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, name)); encoder.Save(file);
        }
        UpdateTesterSlots(slots, inventory);
        Assert(EmptyState.Visibility == Visibility.Visible && TesterContent.Visibility == Visibility.Collapsed &&
            ButtonCombinationsPanel.Visibility == Visibility.Collapsed && _slotTabs.All(b => !b.IsEnabled),
            "Estado vazio incorreto");
        Save("simulated-empty.png");
        var pad = new Gamepad { Buttons = Buttons.A | Buttons.Up | Buttons.Right | Buttons.LS, LT = 128, LX = 16384, RY = -16384 };
        slots[0] = slots[0] with { Input = new InputState { Gamepad = pad }, ReturnCode = 0, Session = Guid.NewGuid() };
        UpdateTesterSlots(slots, inventory); PresentInputs(pad);
        Assert(ButtonCombinationsPanel.Visibility == Visibility.Visible, "As combinações devem aparecer com controle conectado");
        Assert(SlotTabs.Visibility == Visibility.Collapsed && TesterContent.Visibility == Visibility.Visible, "Um controle deve ocultar abas");
        Assert(_buttonValues[0].Text == "1.00" && _buttonValues[6].Text == "0.50" &&
            _buttonValues[8].Text == "1.00" && _buttonValues[9].Text == "0.00",
            "Valores de botões e cliques dos sticks incorretos");
        slots[2] = slots[0] with { Slot = 2, Session = Guid.NewGuid() };
        UpdateTesterSlots(slots, inventory);
        Assert(SlotTabs.Visibility == Visibility.Visible && _slotTabs[0].IsEnabled && !_slotTabs[1].IsEnabled && _slotTabs[2].IsEnabled && !_slotTabs[3].IsEnabled, "Abas ativas incorretas");
        _slotTabs[2].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Assert(DeviceSelector.SelectedIndex == 2, "Aba não selecionou o slot correto");
        UpdateTesterSlots(slots, inventory); PresentInputs(pad); ControllerDrawing.SetInput(pad);
        PresentConnection(true, PhysicalConnection.UsbUnresolved);
        Assert(ConnectionText.Text == LanguageManager.Get("Connection") && UnknownConnectionIcon.Visibility == Visibility.Visible &&
            ConnectionButton.IsHitTestVisible, "Identificação resumida incorreta");
        PresentConnection(true, PhysicalConnection.Dongle24Ghz);
        Assert(DongleConnectionIcon.Visibility == Visibility.Visible && !ConnectionButton.IsHitTestVisible,
            "Ícone do dongle ou caixa exclusiva do nome incorretos");
        PresentConnection(true, PhysicalConnection.Bluetooth);
        Assert(BluetoothConnectionIcon.Visibility == Visibility.Visible &&
            UnknownConnectionIcon.Visibility == Visibility.Collapsed, "Ícone Bluetooth incorreto");
        PresentConnection(true, PhysicalConnection.UsbCable);
        Assert(UsbCableConnectionIcon.Visibility == Visibility.Visible &&
            BluetoothConnectionIcon.Visibility == Visibility.Collapsed, "Ícone USB incorreto");
        PresentConnection(true, PhysicalConnection.UsbUnresolved);
        Assert(BatteryCategories.ToolTip == null, "A bateria não deve exibir tooltip");
        RenderBattery(BatteryDecoder.Decode(0, new BatteryRaw { Type = 3, Level = 2 }, now));
        Assert(_slotTabs.Count(b => b.Visibility == Visibility.Visible) == 2 && SlotTabs.Columns == 2, "Devem existir exatamente duas abas visíveis");
        slots[1] = slots[0] with { Slot = 1, Session = Guid.NewGuid() };
        UpdateTesterSlots(slots, inventory);
        Assert(_slotTabs.Count(b => b.Visibility == Visibility.Visible) == 3, "Terceiro controle deve adicionar uma aba");
        slots[3] = slots[0] with { Slot = 3, Session = Guid.NewGuid() };
        UpdateTesterSlots(slots, inventory);
        Assert(_slotTabs.Count(b => b.Visibility == Visibility.Visible) == 4, "Quarto controle deve adicionar uma aba");
        slots[1] = slots[1] with { Input = null, Session = null }; slots[3] = slots[3] with { Input = null, Session = null };
        UpdateTesterSlots(slots, inventory);
        Assert(_slotTabs.Count(b => b.Visibility == Visibility.Visible) == 2, "Desconexão deve remover abas");
        CircularityButton.IsChecked = true;
        CircularityButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert(LeftPlot.Recording && RightPlot.Recording && (string)CircularityButton.Content == LanguageManager.Get("StopTest"),
            "Teste de precisão deve ativar ambos os gráficos e oferecer parada");
        for (int i = 0; i < 180; i++) LeftPlot.Update(Math.Cos(i * Math.PI / 90), Math.Sin(i * Math.PI / 90));
        var runningKey = SelectedCircleKey!;
        UpdateTesterSlots(slots, inventory);
        var originalCount = _circles[runningKey].Left.Count;
        _slotTabs[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        PresentInputs(pad);
        Assert(CircularityButton.IsChecked == false && _circles[runningKey].Active, "Troca de aba deve preservar o teste original e não ativar o novo");
        UpdateTesterSlots(slots, inventory);
        Assert(_circles[runningKey].Left.Count > originalCount && SelectedCircle!.Left.Count == 0, "Aba em segundo plano deve continuar coletando isoladamente");
        _slotTabs[2].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        PresentInputs(pad);
        Assert(CircularityButton.IsChecked == true, "Retorno à aba deve restaurar teste ativo");
        Assert((string)InfiniteButton.Content == LanguageManager.Get("Continuous") && ContinuousButtonText(true) == LanguageManager.Get("Stop"),
            "Botão contínuo deve alternar entre Continua e Parar");
        UpdateTesterSlots(slots, inventory);
        RotationTestButton.IsChecked = true;
        RotationTestButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert(!SelectedCircle!.Active && !LeftPlot.Recording && (string)RotationTestButton.Content == LanguageManager.Get("StopTest") &&
            (string)CircularityButton.Content == LanguageManager.Get("PrecisionTest"), "Iniciar circularidade deve encerrar precisão e atualizar os botões");
        for (int i = 0; i <= 360; i++)
        {
            var angle = i * Math.PI / 180;
            SelectedRotationCircle!.Observe(new Gamepad
            {
                LX = (short)Math.Round(Math.Cos(angle) * short.MaxValue),
                LY = (short)Math.Round(Math.Sin(angle) * short.MaxValue),
                RX = (short)Math.Round(Math.Cos(angle) * short.MaxValue * .9),
                RY = (short)Math.Round(Math.Sin(angle) * short.MaxValue * .9)
            });
        }
        PresentInputs(pad);
        Assert(LeftPlot.PrecisionAverageErrorPercent is < .1 && RightPlot.PrecisionAverageErrorPercent is > 9 and < 11,
            "Teste visual deve preencher os círculos e mostrar o erro médio separado por analógico");
        MotorButtons.IsEnabled = true;
        ((ProgressButton)MotorButtons.Children[0]).Progress = .5;
        Save("simulated-multiple.png");
        RotationTestButton.IsChecked = false;
        RotationTestButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert(LeftPlot.PrecisionAverageErrorPercent == null && SelectedRotationCircle!.Left.CoveredSectors == 0,
            "Encerrar teste de circularidade deve limpar preenchimento e erro médio");
        Assert((string)RotationTestButton.Content == LanguageManager.Get("CircularityTest"), "Botão deve recuperar o nome ao encerrar");
        CircularityButton.IsChecked = false;
        CircularityButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert(!LeftPlot.Recording && !RightPlot.Recording && SelectedCircle!.Left.Count == 0 && SelectedCircle.Right.Count == 0,
            "Circularidade deve desativar e apagar ambos os rastros");
        slots[2] = slots[2] with { Input = null, Session = null, ReturnCode = XInputApi.Disconnected };
        UpdateTesterSlots(slots, inventory);
        Assert(DeviceSelector.SelectedIndex == 0, "Desconexão não selecionou o controle restante");
        File.WriteAllText(Path.Combine(directory, "ui-checks.txt"), "Simulação: vazio, um controle, dois controles, abas dinâmicas de 1 a 4 controles, circularidade isolada por sessão e teste independente de cobertura angular/erro médio aprovados. Nenhum motor acionado.");
    }
}



