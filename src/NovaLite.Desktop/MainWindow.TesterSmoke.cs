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

    public async void GenerateShowcase(string directory)
    {
        try
        {
            _render.Stop();
            await Task.Delay(1200);

            var ptDir = Path.Combine(directory, "pt-BR");
            var enDir = Path.Combine(directory, "en-US");
            var themesDir = Path.Combine(directory, "themes");
            var batteryDir = Path.Combine(directory, "battery-modes");
            Directory.CreateDirectory(ptDir);
            Directory.CreateDirectory(enDir);
            Directory.CreateDirectory(themesDir);
            Directory.CreateDirectory(batteryDir);

            void SetLang(string langKey)
            {
                LanguageManager.Apply(langKey);
                _initializingLanguage = true;
                LanguageSelector.SelectedIndex = Array.FindIndex(LanguageManager.Languages, l => l.Key == langKey);
                _initializingLanguage = false;
                foreach (ComboBoxItem item in ThemeSelector.Items)
                    if (item.Tag is string key) item.Content = LanguageManager.ThemeLabel(key);
                UpdateStickTestButtons();
                _tray.InvalidateLanguage();
            }

            void SetTheme(string themeKey)
            {
                ThemeManager.Apply(themeKey);
                UpdateHeaderBrand();
                _batteryDrawn = false;
                InvalidateThemeDrawings(this);
                ControllerDrawing?.InvalidateVisual();
            }

            void ShowSettings(bool show)
            {
                TestPage.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                SettingsPage.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                SettingsButton.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                UpdateLayout();
            }

            var now = DateTimeOffset.UtcNow;
            var inventory = new WindowsInventory(now, []);

            void ApplyState(int activeCount, int selectedIndex, string[] controllerModels,
                Gamepad? pad, PhysicalConnection connection, BatteryLevel? batteryLevel,
                double leftMotor = 0, double rightMotor = 0,
                bool precisionActive = false, bool rotationActive = false,
                double rightStickScale = 0.902)
            {
                var slots = Enumerable.Range(0, 4).Select(i =>
                {
                    if (i < activeCount)
                    {
                        var reading = batteryLevel is { } bl
                            ? new BatteryReading(BatteryStatus.Valid, "Bateria", now, now, 3, (byte)bl)
                            : new BatteryReading(BatteryStatus.Unknown, "Bateria desconhecida", now, now);
                        return new SlotSnapshot((uint)i, Guid.NewGuid(), 0, new InputState { Gamepad = pad ?? default }, now, reading, null, null);
                    }
                    return new SlotSnapshot((uint)i, null, XInputApi.Disconnected, null, now, BatteryReading.Disconnected, null, null);
                }).ToArray();

                UpdateTesterSlots(slots, inventory);

                for (int i = 0; i < 4; i++)
                {
                    if (i < activeCount)
                    {
                        _slotTabs[i].Visibility = Visibility.Visible;
                        _slotTabs[i].IsEnabled = true;
                        var model = i < controllerModels.Length ? controllerModels[i] : "GameSir Nova Lite";
                        ((TextBlock)_slotTabs[i].Content).Text = $"{LanguageManager.Get("Controller")} {i + 1} · {model}";
                        bool isSel = (i == selectedIndex);
                        _slotTabs[i].Background = ThemeManager.Brush(isSel ? "SelectedSurfaceBrush" : "PanelBrush", isSel ? "#294A46" : "#19232C");
                        _slotTabs[i].BorderBrush = ThemeManager.Brush(isSel ? "AccentBrush" : "BorderBrush", isSel ? "#90E3C8" : "#30414E");
                        _slotTabs[i].FontWeight = isSel ? FontWeights.Bold : FontWeights.Normal;
                    }
                    else
                    {
                        _slotTabs[i].Visibility = Visibility.Collapsed;
                        _slotTabs[i].IsEnabled = false;
                    }
                }
                SlotTabs.Columns = Math.Max(1, activeCount);
                SlotTabs.Visibility = activeCount > 1 ? Visibility.Visible : Visibility.Collapsed;
                EmptyState.Visibility = activeCount == 0 ? Visibility.Visible : Visibility.Collapsed;
                TesterContent.Visibility = activeCount == 0 ? Visibility.Collapsed : Visibility.Visible;
                ButtonCombinationsPanel.Visibility = activeCount == 0 ? Visibility.Collapsed : Visibility.Visible;

                PresentInputs(pad);
                if (pad != null) ControllerDrawing.SetInput(pad);
                PresentConnection(activeCount > 0, connection);
                if (batteryLevel is { } bLevel)
                    RenderBattery(BatteryDecoder.Decode(0, new BatteryRaw { Type = 3, Level = (byte)bLevel }, now));
                else
                    RenderBattery(new BatteryReading(BatteryStatus.Unknown, "Desconhecido", now, now));

                LeftPlot.Clear();
                RightPlot.Clear();

                if (rotationActive)
                {
                    RotationTestButton.IsChecked = true;
                    CircularityButton.IsChecked = false;
                    if (SelectedRotationCircle is { } rot)
                    {
                        rot.SetActive(true);
                        for (int deg = 0; deg <= 360; deg += 3)
                        {
                            var rad = deg * Math.PI / 180.0;
                            rot.Observe(new Gamepad
                            {
                                LX = (short)Math.Round(Math.Cos(rad) * short.MaxValue),
                                LY = (short)Math.Round(Math.Sin(rad) * short.MaxValue),
                                RX = (short)Math.Round(Math.Cos(rad) * short.MaxValue * rightStickScale),
                                RY = (short)Math.Round(Math.Sin(rad) * short.MaxValue * rightStickScale)
                            });
                        }
                        LeftPlot.ShowPrecision(rot.Left);
                        RightPlot.ShowPrecision(rot.Right);
                    }
                }
                else if (precisionActive)
                {
                    CircularityButton.IsChecked = true;
                    RotationTestButton.IsChecked = false;
                    LeftPlot.Recording = true;
                    RightPlot.Recording = true;
                    for (int i = 0; i < 180; i++)
                    {
                        var rad = i * Math.PI / 90.0;
                        LeftPlot.Update(Math.Cos(rad), Math.Sin(rad));
                        RightPlot.Update(Math.Cos(rad) * 0.92, Math.Sin(rad) * 0.92);
                    }
                }
                else
                {
                    CircularityButton.IsChecked = false;
                    RotationTestButton.IsChecked = false;
                    LeftPlot.Recording = false;
                    RightPlot.Recording = false;
                    LeftPlot.ShowPrecision(null);
                    RightPlot.ShowPrecision(null);
                }

                MotorButtons.IsEnabled = activeCount > 0;
                if (MotorButtons.Children.Count >= 3)
                {
                    ((ProgressButton)MotorButtons.Children[0]).Progress = leftMotor;
                    ((ProgressButton)MotorButtons.Children[1]).Progress = rightMotor;
                    ((ProgressButton)MotorButtons.Children[2]).Progress = (leftMotor > 0 && rightMotor > 0) ? Math.Max(leftMotor, rightMotor) : 0;
                }

                UpdateStickTestButtons();
                UpdateLayout();
            }

            async Task Capture(string path)
            {
                UpdateLayout();
                await Task.Delay(140);
                SaveWindowImage(path);
            }

            foreach (var lang in new[] { ("pt-BR", ptDir), ("en-US", enDir) })
            {
                SetLang(lang.Item1);
                SetTheme("light-blue");
                ShowSettings(false);

                var pad01 = new Gamepad { Buttons = Buttons.A | Buttons.LS, LT = 128, LX = 16384, LY = -8192 };
                ApplyState(2, 0,
                    [lang.Item1 == "en-US" ? "GameSir Nova Lite • Dongle 2.4 GHz" : "GameSir Nova Lite • Dongle 2,4 GHz",
                     lang.Item1 == "en-US" ? "GameSir Nova Lite • USB Cable" : "GameSir Nova Lite • Cabo USB"],
                    pad01, PhysicalConnection.Dongle24Ghz, BatteryLevel.Full, leftMotor: 0.5);
                await Capture(Path.Combine(lang.Item2, "01-diagnostico-principal.png"));

                var pad02 = new Gamepad { Buttons = Buttons.LB | Buttons.RB, RT = 192, LX = 0, LY = short.MaxValue, RX = (short)(short.MaxValue * 0.9), RY = 0 };
                ApplyState(2, 0,
                    [lang.Item1 == "en-US" ? "GameSir Nova Lite • Dongle 2.4 GHz" : "GameSir Nova Lite • Dongle 2,4 GHz",
                     lang.Item1 == "en-US" ? "GameSir Nova Lite • USB Cable" : "GameSir Nova Lite • Cabo USB"],
                    pad02, PhysicalConnection.Dongle24Ghz, BatteryLevel.Full, leftMotor: 0.5, rightMotor: 0.5, rotationActive: true);
                await Capture(Path.Combine(lang.Item2, "02-teste-precisao-circularidade.png"));

                var pad03 = new Gamepad { Buttons = Buttons.X | Buttons.Y | Buttons.RS, RT = 255 };
                ApplyState(3, 1,
                    [lang.Item1 == "en-US" ? "GameSir Nova Lite • Dongle 2.4 GHz" : "GameSir Nova Lite • Dongle 2,4 GHz",
                     lang.Item1 == "en-US" ? "GameSir Nova Lite • USB Cable" : "GameSir Nova Lite • Cabo USB",
                     "GameSir Nova Lite • Bluetooth"],
                    pad03, PhysicalConnection.UsbCable, BatteryLevel.Full);
                await Capture(Path.Combine(lang.Item2, "03-suporte-multi-controle.png"));

                ShowSettings(true);
                await Capture(Path.Combine(lang.Item2, "04-configuracoes-temas.png"));
                ShowSettings(false);

                var pad05 = new Gamepad();
                ApplyState(1, 0,
                    [lang.Item1 == "en-US" ? "GameSir Nova Lite • Dongle 2.4 GHz" : "GameSir Nova Lite • Dongle 2,4 GHz"],
                    pad05, PhysicalConnection.Dongle24Ghz, BatteryLevel.Medium);
                await Capture(Path.Combine(lang.Item2, "05-combinacoes-botoes.png"));

                ApplyState(0, 0, [], null, PhysicalConnection.Unknown, null);
                await Capture(Path.Combine(lang.Item2, "06-estado-desconectado.png"));
            }

            SetLang("pt-BR");
            ShowSettings(false);

            var themeConfigs = new (string Key, Gamepad Pad, int ActiveCount, int SelIndex, string[] Names, PhysicalConnection Conn, BatteryLevel Battery, double LeftMotor, double RightMotor, bool RotActive, double RightScale)[]
            {
                ("stellar-white", new Gamepad { Buttons = Buttons.A | Buttons.LS, LT = 128, LX = 16384, LY = -8192 },
                 2, 0, ["GameSir Nova Lite • Dongle 2,4 GHz", "GameSir Nova Lite • Cabo USB"],
                 PhysicalConnection.Dongle24Ghz, BatteryLevel.Full, 0.5, 0, false, 0.9),

                ("space-purple", new Gamepad { Buttons = Buttons.B | Buttons.Y, RT = 192, RX = 24000, RY = 16000 },
                 3, 0, ["GameSir Nova Lite • Bluetooth", "GameSir Nova Lite • Dongle 2,4 GHz", "GameSir Nova Lite • Cabo USB"],
                 PhysicalConnection.Bluetooth, BatteryLevel.Medium, 0, 0.5, true, 0.902),

                ("black", new Gamepad { Buttons = Buttons.Up | Buttons.Right | Buttons.A, LT = 255 },
                 2, 0, ["GameSir Nova Lite • Cabo USB", "GameSir Nova Lite • Dongle 2,4 GHz"],
                 PhysicalConnection.UsbCable, BatteryLevel.Full, 0, 0, false, 0.9),

                ("gray", new Gamepad { Buttons = Buttons.View | Buttons.Menu, LT = 128, RT = 128 },
                 2, 0, ["GameSir Nova Lite • Cabo USB", "GameSir Nova Lite • Dongle 2,4 GHz"],
                 PhysicalConnection.UsbCable, BatteryLevel.Full, 0, 0, false, 0.9),

                ("pink", new Gamepad { Buttons = Buttons.X | Buttons.LS, LX = -20000, LY = -14000 },
                 2, 0, ["GameSir Nova Lite • Bluetooth", "GameSir Nova Lite • Dongle 2,4 GHz"],
                 PhysicalConnection.Bluetooth, BatteryLevel.Medium, 0, 0, true, 0.88),

                ("light-blue", new Gamepad { Buttons = Buttons.LB | Buttons.RB, RT = 192, LX = 0, LY = short.MaxValue, RX = (short)(short.MaxValue * 0.9), RY = 0 },
                 2, 0, ["GameSir Nova Lite • Dongle 2,4 GHz", "GameSir Nova Lite • Cabo USB"],
                 PhysicalConnection.Dongle24Ghz, BatteryLevel.Full, 0.5, 0.5, true, 0.902),

                ("golden-yellow", new Gamepad { Buttons = Buttons.A | Buttons.X | Buttons.RS, RT = 255 },
                 2, 1, ["GameSir Nova Lite • Dongle 2,4 GHz", "GameSir Nova Lite • Cabo USB"],
                 PhysicalConnection.UsbCable, BatteryLevel.Full, 0, 0, false, 0.9),

                ("midnight-blue", new Gamepad { Buttons = Buttons.LB | Buttons.RB, LT = 255, RT = 255 },
                 3, 0, ["GameSir Nova Lite • Dongle 2,4 GHz", "GameSir Nova Lite • Cabo USB", "GameSir Nova Lite • Bluetooth"],
                 PhysicalConnection.Dongle24Ghz, BatteryLevel.Full, 0.75, 0.75, false, 0.9),

                ("light-green", new Gamepad { Buttons = Buttons.Down | Buttons.Left, LT = 200, RT = 200 },
                 2, 0, ["GameSir Nova Lite • Dongle 2,4 GHz", "GameSir Nova Lite • Cabo USB"],
                 PhysicalConnection.Dongle24Ghz, BatteryLevel.Full, 0, 0, true, 0.95)
            };

            foreach (var cfg in themeConfigs)
            {
                SetTheme(cfg.Key);
                ApplyState(cfg.ActiveCount, cfg.SelIndex, cfg.Names, cfg.Pad, cfg.Conn, cfg.Battery,
                    cfg.LeftMotor, cfg.RightMotor, rotationActive: cfg.RotActive, rightStickScale: cfg.RightScale);
                await Capture(Path.Combine(themesDir, $"{cfg.Key}.png"));
            }

            SetTheme("light-blue");
            var batteryConfigs = new (string Name, PhysicalConnection Conn, BatteryLevel? Level, string Model)[]
            {
                ("01_dongle_full", PhysicalConnection.Dongle24Ghz, BatteryLevel.Full, "GameSir Nova Lite • Dongle 2,4 GHz"),
                ("02_dongle_medium", PhysicalConnection.Dongle24Ghz, BatteryLevel.Medium, "GameSir Nova Lite • Dongle 2,4 GHz"),
                ("03_dongle_low", PhysicalConnection.Dongle24Ghz, BatteryLevel.Low, "GameSir Nova Lite • Dongle 2,4 GHz"),
                ("04_usb_cable", PhysicalConnection.UsbCable, BatteryLevel.Full, "GameSir Nova Lite • Cabo USB"),
                ("05_bluetooth", PhysicalConnection.Bluetooth, BatteryLevel.Medium, "GameSir Nova Lite • Bluetooth"),
                ("06_unknown", PhysicalConnection.UsbUnresolved, null, "GameSir Nova Lite • USB não identificado")
            };

            var batPad = new Gamepad { Buttons = Buttons.A, LT = 100, LX = 12000, LY = 8000 };
            foreach (var bcfg in batteryConfigs)
            {
                ApplyState(2, 0, [bcfg.Model, "GameSir Nova Lite • Cabo USB"], batPad, bcfg.Conn, bcfg.Level);
                await Capture(Path.Combine(batteryDir, $"{bcfg.Name}.png"));
            }

            File.WriteAllText(Path.Combine(directory, "showcase-manifest.json"), "{\"status\":\"success\"}");
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "showcase-error.txt"), ex.ToString());
        }
        finally
        {
            _allowExit = true;
            Close();
        }
    }
}



