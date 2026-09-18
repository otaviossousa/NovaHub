using System.Text.Json;

namespace NovaLite.Core;

public sealed record CaptureSample(DateTimeOffset At, Gamepad Input);
public sealed class StickCapture
{
    private readonly List<CaptureSample> _samples = new();
    public IReadOnlyList<CaptureSample> Samples => _samples;
    public void Add(DateTimeOffset at, Gamepad input) { if (_samples.Count < 3000) _samples.Add(new(at, input)); }
    public object Summary() => new { Count = _samples.Count, Left = Stats(true), Right = Stats(false) };
    public AxisStatistics? Stats(bool left)
    {
        if (_samples.Count == 0) return null;
        var xs = _samples.Select(s => Normalize.Axis(left ? s.Input.LX : s.Input.RX)).ToArray();
        var ys = _samples.Select(s => Normalize.Axis(left ? s.Input.LY : s.Input.RY)).ToArray();
        var mx = xs.Average(); var my = ys.Average();
        return new(mx, my, xs.Min(), xs.Max(), ys.Min(), ys.Max(),
            Math.Sqrt(xs.Zip(ys).Average(p => Math.Pow(p.First - mx, 2) + Math.Pow(p.Second - my, 2))),
            xs.Zip(ys).Max(p => Math.Sqrt(p.First * p.First + p.Second * p.Second)));
    }
}
public sealed record AxisStatistics(double MeanX, double MeanY, double MinX, double MaxX,
    double MinY, double MaxY, double RadialDispersion, double MaxRadius);

public static class Reports
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    public static void Write(string path, ControllerMonitor monitor, string transport = "Não informado",
        string mode = "XInput observado pela API; modo físico não confirmado", StickCapture? capture = null,
        uint? captureSlot = null, Guid? captureSession = null, string captureState = "Não realizada", uint? selectedSlot = null)
    {
        var report = new
        {
            SchemaVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            Application = "NovaHub • 1.0",
            Evidence = "Observações reais da API; não certifica o modelo nem o funcionamento físico",
            Api = "XInput 1.4",
            Model = "Não identificado automaticamente",
            SelectedSlot = selectedSlot,
            TransportDeclaredByUser = transport,
            TransportScope = "Declaração sobre o slot selecionado; não identifica automaticamente as demais conexões",
            Mode = mode,
            FirmwareController = "Não foi possível consultar",
            FirmwareDongle = "Não foi possível consultar",
            OS = Environment.OSVersion.VersionString,
            Runtime = Environment.Version.ToString(),
            Slots = monitor.Snapshot(),
            Events = monitor.Observations(),
            Capture = capture == null ? null : new
            {
                Slot = captureSlot,
                Session = captureSession,
                Status = captureState,
                Method = "Repouso: 10 s; amostras recebidas pela interface, sem filtro local. Não mede latência nem sensor elétrico.",
                Summary = capture.Summary(),
                capture.Samples
            },
            Limitations = new[] {
                "Slot XInput não é uma identidade física persistente; troca entre consultas pode ser indetectável.",
                "O receptor pode continuar enumerado com o controle desligado; requer ensaio físico.",
                "Bateria categórica observada ainda requer validação no Nova Lite por conexão e firmware.",
                "Este relatório usa XInput; a fonte Windows tem exportação própria. Sem consultas proprietárias, escrita de configuração ou atualização de firmware.",
                "Eventos limitados aos 2000 mais recentes; nenhuma validação física automática."
            }
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
    }
}






