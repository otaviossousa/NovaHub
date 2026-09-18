namespace NovaLite.Core;

// This profile affects the diagnostic drawing only. It never remaps games or writes to a device.
public sealed class RawMapping
{
    public Dictionary<Buttons, int> Buttons { get; set; } = new();
    public Dictionary<string, int> Axes { get; set; } = new();
    public HashSet<string> Inverted { get; set; } = new();
    public int Hat { get; set; } = -1;
    public Gamepad Convert(bool[] buttons, double[] axes, string[] hats)
    {
        var result = new Gamepad();
        foreach (var (button, index) in Buttons)
            if (index >= 0 && index < buttons.Length && buttons[index]) result.Buttons |= button;
        if (Hat >= 0 && Hat < hats.Length)
        {
            result.Buttons |= hats[Hat] switch
            {
                "Up" => global::NovaLite.Core.Buttons.Up,
                "UpRight" => global::NovaLite.Core.Buttons.Up | global::NovaLite.Core.Buttons.Right,
                "Right" => global::NovaLite.Core.Buttons.Right,
                "DownRight" => global::NovaLite.Core.Buttons.Down | global::NovaLite.Core.Buttons.Right,
                "Down" => global::NovaLite.Core.Buttons.Down,
                "DownLeft" => global::NovaLite.Core.Buttons.Down | global::NovaLite.Core.Buttons.Left,
                "Left" => global::NovaLite.Core.Buttons.Left,
                "UpLeft" => global::NovaLite.Core.Buttons.Up | global::NovaLite.Core.Buttons.Left,
                _ => 0
            };
        }
        double? Value(string name)
        {
            if (!Axes.TryGetValue(name, out var i) || i < 0 || i >= axes.Length || !double.IsFinite(axes[i])) return null;
            var value = Math.Clamp(axes[i], 0, 1);
            return Inverted.Contains(name) ? 1 - value : value;
        }
        short Stick(string name) => Value(name) is { } value ? DisplayUnsignedByteAxis(value) : (short)0;
        result.LX = Stick("LX"); result.LY = Stick("LY"); result.RX = Stick("RX"); result.RY = Stick("RY");
        result.LT = DisplayTrigger(Value("LT") ?? 0); result.RT = DisplayTrigger(Value("RT") ?? 0);
        return result;
    }
    public static short DisplayAxis(double value) => !double.IsFinite(value) ? (short)0 :
        (short)Math.Round(Math.Clamp(value, -1, 1) * (value < 0 ? 32768 : 32767));
    public static short DisplayUnsignedByteAxis(double normalized, bool invert = false)
    {
        if (!double.IsFinite(normalized)) return 0;
        var raw = (byte)Math.Round(Math.Clamp(normalized, 0, 1) * 255,
            MidpointRounding.AwayFromZero);
        return DisplayUnsignedByteAxis(raw, invert);
    }
    public static short DisplayUnsignedByteAxis(byte raw, bool invert = false)
    {
        // HID axes with an even 0..255 range have two middle samples. Some
        // firmware rests on 127 and other firmware/axes rest on 128.
        var centered = raw switch
        {
            127 or 128 => 0,
            < 127 => (raw - 127) / 127d,
            _ => (raw - 128) / 127d
        };
        return DisplayAxis(invert ? -centered : centered);
    }
    public static byte DisplayTrigger(double value) => !double.IsFinite(value) ? (byte)0 : (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}

