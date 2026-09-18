namespace NovaLite.Core;

public enum BatteryStatus { Disconnected, Unknown, Unsupported, Wired, Valid, Error, Stale }
public enum BatteryLevel { Empty, Low, Medium, Full }

public sealed record BatteryReading(BatteryStatus Status, string Label, DateTimeOffset? AttemptAt = null,
    DateTimeOffset? ValidAt = null, uint? ReturnCode = null, byte? RawType = null, byte? RawLevel = null)
{
    public static BatteryReading Disconnected => new(BatteryStatus.Disconnected, "Controle desconectado");
    public bool HasLevel => Status == BatteryStatus.Valid;
    public BatteryLevel? Level => HasLevel && RawLevel is <= 3 ? (BatteryLevel)RawLevel.Value : null;
}

public static class BatteryDecoder
{
    public static BatteryReading Decode(uint result, BatteryRaw raw, DateTimeOffset now)
    {
        if (result == XInputApi.Disconnected) return BatteryReading.Disconnected with { AttemptAt = now, ReturnCode = result };
        if (result == XInputApi.MissingApi) return new(BatteryStatus.Unsupported, "Consulta indisponível nesta API", now, ReturnCode: result);
        if (result != 0) return new(BatteryStatus.Error, "Não foi possível atualizar a bateria", now, ReturnCode: result);

        // Some compatible controllers report BATTERY_TYPE_WIRED even while they keep
        // BATTERY_LEVEL updated. This is also how the proven bateria-8bitdo monitor
        // works: a successful call makes the returned level the source of truth.
        if (raw.Level <= 3)
            return new(BatteryStatus.Valid, new[] { "Vazia", "Baixa", "Média", "Cheia" }[raw.Level],
                now, now, result, raw.Type, raw.Level);

        var (status, label) = raw.Type switch
        {
            0 => (BatteryStatus.Disconnected, "API de bateria informa desconectado"),
            1 => (BatteryStatus.Wired, "Nível não informado (WIRED)"),
            _ => (BatteryStatus.Unknown, "Bateria desconhecida")
        };
        return new(status, label, now, status == BatteryStatus.Valid ? now : null, result, raw.Type, raw.Level);
    }
}
