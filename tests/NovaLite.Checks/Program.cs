using System.Runtime.InteropServices;
using NovaLite.Core;

if (args.Length == 2 && args[0] == "--probe")
{
    using var monitor = new ControllerMonitor(new XInputApi());
    monitor.Start();
    await Task.Delay(3000);
    Reports.Write(args[1], monitor);
    foreach (var slot in monitor.Snapshot()) Console.WriteLine($"Slot {slot.Slot}: retorno {slot.ReturnCode}; bateria {slot.Battery.Label}");
    Console.WriteLine("Sondagem somente de leitura. Nenhum motor acionado; modelo não confirmado.");
    return;
}

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FALHOU: " + name);
    Console.WriteLine("OK: " + name); count++;
}
Check(Marshal.SizeOf<Gamepad>() == 12 && Marshal.SizeOf<InputState>() == 16 && Marshal.SizeOf<Capabilities>() == 20 && Marshal.SizeOf<BatteryRaw>() == 2, "layout nativo XInput");
var btTransport = TransportClassifier.Assess([new("BTHENUM\\DEV_TEST", "BTHENUM", null)]);
Check(btTransport.Connection == PhysicalConnection.Bluetooth, "topologia Bluetooth usa evidência de barramento");
var unresolvedUsb = TransportClassifier.Assess([new("USB\\VID_3537&PID_1040", "USB", null)]);
Check(unresolvedUsb.Connection == PhysicalConnection.UsbUnresolved, "USB sem assinatura não inventa cabo ou dongle");
var verifiedDongle = TransportClassifier.Assess([new("USB\\VID_3537&PID_1040", "USB", null)],
    new("nova-lite-dongle-validado", PhysicalConnection.Dongle24Ghz));
Check(verifiedDongle.Connection == PhysicalConnection.Dongle24Ghz && verifiedDongle.VerifiedSignature != null,
    "assinatura USB validada pode identificar dongle");
Check(XInputInterfaceAssociation.MapSlotToInterfaceIndex(0, [0u, 1u], [0, 2]) == 0 &&
      XInputInterfaceAssociation.MapSlotToInterfaceIndex(1, [0u, 1u], [0, 2]) == 2,
    "slots XInput compactados preservam a ordem das interfaces IG presentes");
Check(XInputInterfaceAssociation.MapSlotToInterfaceIndex(1, [0u, 1u], [0]) == null,
    "associação por ordem exige conjuntos completos para não inventar dispositivo");
Check(HidCadenceTransportClassifier.Assess(500, 1.01) == PhysicalConnection.UsbCable &&
      HidCadenceTransportClassifier.Assess(150, 3.99) == PhysicalConnection.Dongle24Ghz,
    "cadência HID distingue USB direto de receptor 2,4 GHz");
Check(HidCadenceTransportClassifier.Assess(39, 1.0) == null &&
      HidCadenceTransportClassifier.Assess(100, 2.5) == null,
    "cadência insuficiente ou ambígua não inventa transporte");
Check(Normalize.Axis(short.MinValue) == -1 && Normalize.Axis(short.MaxValue) == 1 && Normalize.Axis(0) == 0, "extremos e centro sem assimetria de normalização");
Check(Normalize.Trigger(255) == 1 && Normalize.Trigger(0) == 0, "gatilhos preservam extremos");
Check(RawMapping.DisplayUnsignedByteAxis((byte)127) == 0 &&
      RawMapping.DisplayUnsignedByteAxis((byte)128) == 0 &&
      RawMapping.DisplayUnsignedByteAxis((byte)0) == short.MinValue &&
      RawMapping.DisplayUnsignedByteAxis((byte)255) == short.MaxValue,
    "eixo HID de 8 bits aceita os dois valores centrais 127 e 128");
var firstCircle=new CircularitySession(); var secondCircle=new CircularitySession();
firstCircle.SetActive(true);
for(int i=0;i<1810;i++) { firstCircle.Observe(new Gamepad { LX=32767 }); secondCircle.Observe(new Gamepad { RX=-32768 }); }
Check(firstCircle.Left.Count==1800 && firstCircle.Right.Count==1800 && secondCircle.Left.Count==0,"circularidade isolada e memória limitada por controle");
firstCircle.SetActive(false); firstCircle.Observe(default);
Check(firstCircle.Left.Count==0 && firstCircle.Right.Count==0 && !firstCircle.Active,
    "encerrar circularidade apaga os dois rastros");
secondCircle.SetActive(true); secondCircle.Observe(new Gamepad { RY=32767 });
Check(secondCircle.Right.Single().Y==1 && firstCircle.Left.Count==0,"segundo controle não altera o primeiro");
var rotationCircle=new RotationCircularitySession();
rotationCircle.SetActive(true);
for(int i=0;i<=360;i++)
{
    var angle=i*Math.PI/180;
    rotationCircle.Observe(new Gamepad { LX=(short)Math.Round(Math.Cos(angle)*short.MaxValue), LY=(short)Math.Round(Math.Sin(angle)*short.MaxValue) });
}
Check(rotationCircle.Left.Radii.Count(r=>r.HasValue)==72 && rotationCircle.Left.AverageErrorPercent is < .01 && rotationCircle.Right.CoveredSectors==0,
    "cobertura angular e erro radial médio são medidos por analógico");
var maximumCircle=new RotationCircularitySession();
maximumCircle.SetActive(true);
maximumCircle.Observe(new Gamepad { LX=(short)(short.MaxValue*.8) });
maximumCircle.Observe(new Gamepad { LX=(short)(short.MaxValue*.6) });
maximumCircle.Observe(new Gamepad { LX=(short)(short.MaxValue*.9) });
Check(maximumCircle.Left.Radii[0] is > .89 and < .91,"setor preenchido preserva somente o maior alcance observado");
var interpolatedCircle=new RotationCircularitySession();
interpolatedCircle.SetActive(true);
for(int degrees=0;degrees<=360;degrees+=20)
{
    var angle=degrees*Math.PI/180;
    interpolatedCircle.Observe(new Gamepad { LX=(short)Math.Round(Math.Cos(angle)*short.MaxValue), LY=(short)Math.Round(Math.Sin(angle)*short.MaxValue) });
}
Check(interpolatedCircle.Left.CoveredSectors==72,"interpolação do percurso não deixa setores entre amostras sem preenchimento");
rotationCircle.SetActive(false);
rotationCircle.Observe(default);
Check(rotationCircle.Left.CoveredSectors==0 && rotationCircle.Left.AverageErrorPercent==null && !rotationCircle.Active,
    "encerrar teste de circularidade limpa preenchimento e erro médio");
rotationCircle.SetActive(true);
Check(rotationCircle.Left.CoveredSectors==0 && rotationCircle.Left.AverageErrorPercent==null,"novo teste de precisão começa limpo");
var now = DateTimeOffset.UtcNow;
for (byte level = 0; level < 4; level++)
    Check(BatteryDecoder.Decode(0, new() { Type = 3, Level = level }, now).Level == (BatteryLevel)level,
        $"categoria {level} preservada sem porcentagem");
Check(BatteryDecoder.Decode(0, new() { Type = 1, Level = 3 }, now).Level == BatteryLevel.Full,
    "controle compatível preserva FULL mesmo quando informa WIRED");
Check((BatteryDecoder.Decode(0, new() { Type = 3, Level = 2 }, now) with { Status = BatteryStatus.Stale }).Level == null, "leitura antiga não destaca categoria");
var mapping = new RawMapping { Buttons = new() { [Buttons.A] = 1 }, Axes = new() { ["LX"] = 0, ["LY"] = 1, ["LT"] = 2 }, Inverted = new() { "LY" }, Hat = 0 };
var mapped = mapping.Convert([false, true], [1, 0, .5], ["UpRight"]);
Check(mapped.Buttons == (Buttons.A | Buttons.Up | Buttons.Right) && mapped.LX == 32767 && mapped.LY == 32767 && mapped.LT == 128, "mapeamento explícito de botão, diagonal, eixo invertido e gatilho");
var bluetoothProfile = ControllerProfiles.Create(0x3537, 0x1041, 16, 6, 1)!;
var bluetoothButtons = new bool[16]; bluetoothButtons[0] = true; bluetoothButtons[6] = true;
bluetoothButtons[10] = true; bluetoothButtons[13] = true;
var bluetoothPad = bluetoothProfile.Convert(bluetoothButtons, [.5, 0, .75, 1, 1, .5], ["DownLeft"]);
Check(bluetoothPad.Buttons.HasFlag(Buttons.A) && bluetoothPad.Buttons.HasFlag(Buttons.LB) &&
      bluetoothPad.Buttons.HasFlag(Buttons.View) && bluetoothPad.Buttons.HasFlag(Buttons.LS) && bluetoothPad.Buttons.HasFlag(Buttons.Down) &&
      bluetoothPad.Buttons.HasFlag(Buttons.Left) && bluetoothPad.LY == 32767 &&
      bluetoothPad.RY == -32768 && bluetoothPad.LT == 128 && bluetoothPad.RT == 255,
    "perfil automático do Nova Lite Bluetooth");
Check(ControllerProfiles.Create(0x3537, 0x1040, 16, 6, 1) == null,
    "perfil Bluetooth não é aplicado a outro PID");
Check(ControllerProfiles.IsNovaLiteSwitchMode(0x057E, 0x2009) &&
      !ControllerProfiles.IsNovaLiteSwitchMode(0x3537, 0x1041),
    "identidade Bluetooth do modo Switch é separada dos demais modos");
Check(ControllerModes.Identify(0x3537, 0x1040) == NovaLiteProtocolMode.XInput &&
      ControllerModes.Identify(0x3537, 0x1041) == NovaLiteProtocolMode.AndroidHid &&
      ControllerModes.Identify(0x054C, 0x09CC) == NovaLiteProtocolMode.DualShock4 &&
      ControllerModes.Identify(0x057E, 0x2009) == NovaLiteProtocolMode.Switch &&
      ControllerModes.DisplayName(0x3537, 0x1040, PhysicalConnection.Dongle24Ghz) == "GameSir Nova Lite • Dongle 2,4 GHz" &&
      ControllerModes.DisplayName(0x054C, 0x09CC, PhysicalConnection.Bluetooth) == "GameSir Nova Lite • DualShock" &&
      ControllerModes.DisplayName(0x3537, 0x1041, PhysicalConnection.Bluetooth) == "GameSir Nova Lite • Android" &&
      ControllerModes.DisplayName(0x057E, 0x2009, PhysicalConnection.Bluetooth) == "GameSir Nova Lite • Switch" &&
      ControllerModes.DisplayName(0x3537, 0x1040, PhysicalConnection.UsbUnresolved) == "GameSir Nova Lite • USB não identificado",
    "modos do Nova Lite são identificados explicitamente por protocolo");
var switchReport = new byte[49];
switchReport[0] = 0x30; switchReport[2] = 0x40;
switchReport[3] = 0x04 | 0x40 | 0x80; switchReport[4] = 0x01 | 0x08;
switchReport[5] = 0x02 | 0x04 | 0x40 | 0x80;
switchReport[6] = 0x00; switchReport[7] = 0x08; switchReport[8] = 0x80;
switchReport[9] = 0xFF; switchReport[10] = 0x0F; switchReport[11] = 0x00;
Check(SwitchInputDecoder.TryDecode(switchReport, out var switchReading) && switchReading != null &&
      switchReading.Battery == BatteryLevel.Low && switchReading.RawBatteryLevel == 4 &&
      switchReading.Input.LX == 0 && switchReading.Input.LY == 0 &&
      switchReading.Input.RX == short.MaxValue && switchReading.Input.RY == short.MinValue &&
      switchReading.Input.LT == 255 && switchReading.Input.RT == 255 &&
      switchReading.Input.Buttons.HasFlag(Buttons.A) && switchReading.Input.Buttons.HasFlag(Buttons.LB) &&
      switchReading.Input.Buttons.HasFlag(Buttons.RB) && switchReading.Input.Buttons.HasFlag(Buttons.LS) &&
      switchReading.Input.Buttons.HasFlag(Buttons.View) && switchReading.Input.Buttons.HasFlag(Buttons.Up) &&
      switchReading.Input.Buttons.HasFlag(Buttons.Right),
    "relatório Switch 0x30 mapeia bateria, botões, gatilhos e analógicos");
var ds4Report = new byte[78];
ds4Report[0] = 0x11; ds4Report[3] = 128; ds4Report[4] = 128; ds4Report[5] = 255; ds4Report[6] = 0;
ds4Report[7] = 0x20 | 0x01; ds4Report[8] = 0x01 | 0x20 | 0x80; ds4Report[10] = 64; ds4Report[11] = 192;
Check(Ds4InputDecoder.TryDecode(ds4Report, out var ds4Pad) && ds4Pad.LX == 0 && ds4Pad.LY == 0 &&
      ds4Pad.RX == short.MaxValue && ds4Pad.RY == short.MaxValue && ds4Pad.LT == 64 && ds4Pad.RT == 192 &&
      ds4Pad.Buttons.HasFlag(Buttons.A) && ds4Pad.Buttons.HasFlag(Buttons.Up) &&
      ds4Pad.Buttons.HasFlag(Buttons.Right) && ds4Pad.Buttons.HasFlag(Buttons.LB) &&
      ds4Pad.Buttons.HasFlag(Buttons.Menu) && ds4Pad.Buttons.HasFlag(Buttons.RS),
    "relatório Bluetooth DualShock 4 mapeia botões, eixos e gatilhos");
var novaWarning = NovaLiteDs4BatteryDecoder.Decode(2);
var novaLow = NovaLiteDs4BatteryDecoder.Decode(1);
var novaUncalibrated = NovaLiteDs4BatteryDecoder.Decode(3);
Check(!novaWarning.LowBatteryWarning && novaWarning.IsCalibratedPoint &&
      novaWarning.PercentForCategory == 25 && novaWarning.Label.Contains("pode começar antes"),
    "nível bruto 2 permanece médio apesar do alerta físico antecipado");
Check(novaLow.LowBatteryWarning && novaLow.IsCalibratedPoint && novaLow.PercentForCategory == 15 &&
      novaLow.Label.Contains("Baixa"),
    "transição para nível bruto 1 ativa a categoria baixa");
Check(!novaUncalibrated.LowBatteryWarning && !novaUncalibrated.IsCalibratedPoint &&
      novaUncalibrated.PercentForCategory == 35 && novaUncalibrated.Label.Contains("estimativa"),
    "níveis DS4 ainda não calibrados são identificados como estimativa");
Check(mapping.Convert([], [], []).Buttons == 0 && mapping.Convert([], [], []).LX == 0, "índices ausentes não reutilizam entradas");
Check(mapping.Convert([], [double.NaN], []).LX == 0, "entrada não finita não contamina desenho");
Check(BatteryDecoder.Decode(0, new() { Type = 1, Level = 3 }, now).Status == BatteryStatus.Valid,
    "sucesso XInput torna o nível a fonte de verdade, como no monitor de referência");
Check(BatteryDecoder.Decode(0, new() { Type = 255, Level = 3 }, now).Status == BatteryStatus.Valid,
    "tipo desconhecido não descarta um nível XInput válido");
Check(BatteryDecoder.Decode(0, new() { Type = 2, Level = 9 }, now).Status == BatteryStatus.Unknown, "valor fora do contrato não produz nível");
Check(BatteryDecoder.Decode(0, new() { Type = 3, Level = 1 }, now).Label == "Baixa", "categoria documentada preservada");
Check(BatteryDecoder.Decode(5, new() { Type = 2, Level = 3 }, now).RawLevel == null, "erro não reaproveita bytes de saída");
Check(BatteryDecoder.Decode(XInputApi.MissingApi, default, now).Status == BatteryStatus.Unsupported, "API ausente é distinta de bateria desconhecida");
Check(BatteryDecoder.Decode(XInputApi.Disconnected, default, now).ValidAt == null, "desconexão sem leitura válida");

var api = new FakeApi();
using (var monitor = new ControllerMonitor(api))
{
    api.Connected[0] = true; api.Connected[1] = true;
    monitor.Poll(now);
    var session = monitor.Snapshot()[0].Session!.Value;
    Check(monitor.Snapshot()[0].Battery.HasLevel, "bateria consultada na conexão");
    Check(monitor.StartVibration(0, session, .25, .25, TimeSpan.FromMilliseconds(50), continuous: true), "vibração contínua exige início explícito");
    await Task.Delay(150);
    Check(monitor.ContinuousVibration && api.Commands.Last().Left != 0, "vibração contínua não expira no prazo do teste temporário");
    monitor.StopVibration();
    Check(!monitor.ContinuousVibration && api.Commands.Last() == (0u, (ushort)0, (ushort)0), "parar cancela modo contínuo e envia zero");
    Check(!monitor.StartVibration(0, Guid.NewGuid(), .2, .2, TimeSpan.FromSeconds(1)), "sessão errada não aciona motores");
    Check(!monitor.StartVibration(0, session, .2, .2, TimeSpan.FromSeconds(6)), "duração acima do limite recusada");
    Check(!monitor.StartVibration(0, session, double.NaN, .2, TimeSpan.FromSeconds(1)), "intensidade inválida recusada");
    Check(monitor.StartVibration(0, session, .2, 0, TimeSpan.FromMilliseconds(50)), "teste temporário aceito na sessão correta");
    await Task.Delay(250);
    Check(api.Commands.Last() == (0u, (ushort)0, (ushort)0), "temporizador para sem depender da interface");
    monitor.StartVibration(0, session, .2, .2, TimeSpan.FromSeconds(2));
    api.Connected[0] = false; monitor.Poll(now.AddSeconds(1));
    Check(monitor.Snapshot()[0].Input == null && monitor.Snapshot()[0].Battery.ValidAt == null, "desconexão elimina entradas e bateria atuais");
    Check(api.Commands.Last() == (0u, (ushort)0, (ushort)0), "desconexão tenta parar vibração");
    api.Connected[0] = true; monitor.Poll(now.AddSeconds(2));
    Check(monitor.Snapshot()[0].Session != session, "reconexão cria nova sessão");
    Check(!monitor.StartVibration(0, session, .2, .2, TimeSpan.FromSeconds(1)), "sessão antiga não controla slot reutilizado");
    var second = monitor.Snapshot()[1].Session!.Value;
    monitor.StartVibration(1, second, .4, .3, TimeSpan.FromSeconds(2));
    Check(api.Commands.Last().Slot == 1, "seleção isola destino do comando");
    monitor.StopVibration();
    api.BatteryCode = 5; monitor.RefreshBattery(1); monitor.Poll(now.AddSeconds(3));
    Check(monitor.Snapshot()[1].Battery.Status == BatteryStatus.Error, "falha de atualização não mantém nível como atual");
    monitor.StartVibration(1, second, .1, .1, TimeSpan.FromSeconds(1));
}
Check(api.Commands.Last() == (1u, (ushort)0, (ushort)0), "encerramento envia zero");
var capture = new StickCapture(); capture.Add(now, new Gamepad { LX = 0 }); capture.Add(now.AddSeconds(1), new Gamepad { LX = 32767 });
Check(capture.Stats(true) is { MeanX: .5, MinX: 0, MaxX: 1, RadialDispersion: .5 }, "métricas reproduzíveis com dados conhecidos");
Console.WriteLine($"{count} verificações aprovadas. São testes de lógica com API simulada, não homologação no Nova Lite.");

sealed class FakeApi : IControllerApi
{
    public bool[] Connected = new bool[4];
    public uint BatteryCode;
    public readonly List<(uint Slot, ushort Left, ushort Right)> Commands = new();
    public uint Read(uint slot, out InputState state) { state = default; return Connected[slot] ? 0 : XInputApi.Disconnected; }
    public uint Battery(uint slot, out BatteryRaw battery) { battery = new() { Type = 2, Level = 2 }; return BatteryCode; }
    public uint Capabilities(uint slot, out Capabilities capabilities) { capabilities = default; return 0; }
    public uint Vibrate(uint slot, ushort left, ushort right) { Commands.Add((slot, left, right)); return Connected[slot] ? 0 : XInputApi.Disconnected; }
}


