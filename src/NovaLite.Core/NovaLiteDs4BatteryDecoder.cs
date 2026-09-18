namespace NovaLite.Core;

public sealed record NovaLiteDs4BatteryEstimate(int PercentForCategory, string Label,
    bool LowBatteryWarning, bool IsCalibratedPoint);

/// <summary>
/// Interprets the DS4-compatible nibble emitted by the Nova Lite. The physical
/// warning LED was observed before the report moved from level 2 to level 1,
/// so the report transition, rather than the earlier LED blink, drives the UI category.
/// </summary>
public static class NovaLiteDs4BatteryDecoder
{
    public static NovaLiteDs4BatteryEstimate Decode(int step)
    {
        step = Math.Clamp(step, 0, 10);
        var midpoint = step < 10 ? step * 10 + 5 : 100;
        return step switch
        {
            0 => new(midpoint, "Crítica • cerca de 5%", true, false),
            1 => new(midpoint, "Baixa • cerca de 15%", true, true),
            2 => new(midpoint, "Cerca de 25% • alerta físico pode começar antes", false, true),
            _ => new(midpoint, $"Cerca de {midpoint}% • estimativa DS4", false, false)
        };
    }
}
