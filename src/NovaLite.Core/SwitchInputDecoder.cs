namespace NovaLite.Core;

public sealed record SwitchInputReading(Gamepad Input, BatteryLevel Battery, byte RawBatteryLevel);

/// <summary>Decodes the standard full input report 0x30 used by Switch Pro-compatible controllers.</summary>
public static class SwitchInputDecoder
{
    public static bool TryDecode(ReadOnlySpan<byte> report, out SwitchInputReading? reading)
    {
        reading = null;
        if (report.Length < 12 || report[0] != 0x30) return false;

        var right = report[3];
        var shared = report[4];
        var left = report[5];
        var buttons = (Buttons)0;
        // Preserve physical positions in the Xbox-style drawing.
        if ((right & 0x04) != 0) buttons |= Buttons.A; // Switch B, bottom
        if ((right & 0x08) != 0) buttons |= Buttons.B; // Switch A, right
        if ((right & 0x01) != 0) buttons |= Buttons.X; // Switch Y, left
        if ((right & 0x02) != 0) buttons |= Buttons.Y; // Switch X, top
        if ((right & 0x40) != 0) buttons |= Buttons.RB;
        if ((left & 0x40) != 0) buttons |= Buttons.LB;
        if ((shared & 0x01) != 0) buttons |= Buttons.View;
        if ((shared & 0x02) != 0) buttons |= Buttons.Menu;
        if ((shared & 0x04) != 0) buttons |= Buttons.RS;
        if ((shared & 0x08) != 0) buttons |= Buttons.LS;
        if ((left & 0x01) != 0) buttons |= Buttons.Down;
        if ((left & 0x02) != 0) buttons |= Buttons.Up;
        if ((left & 0x04) != 0) buttons |= Buttons.Right;
        if ((left & 0x08) != 0) buttons |= Buttons.Left;

        static int Axis(byte low, byte packed, bool highNibble) => highNibble
            ? (packed >> 4) | (low << 4)
            : low | ((packed & 0x0F) << 8);
        static short Display(int raw, bool invert = false)
        {
            raw = Math.Clamp(raw, 0, 4095);
            var centered = raw switch
            {
                2047 or 2048 => 0,
                < 2047 => (raw - 2047) / 2047d,
                _ => (raw - 2048) / 2047d
            };
            return RawMapping.DisplayAxis(invert ? -centered : centered);
        }

        var rawBattery = (byte)((report[2] >> 4) & 0x0F);
        var battery = rawBattery switch
        {
            0 => BatteryLevel.Empty,
            <= 4 => BatteryLevel.Low,
            6 => BatteryLevel.Medium,
            _ => BatteryLevel.Full
        };
        var input = new Gamepad
        {
            Buttons = buttons,
            LT = (left & 0x80) != 0 ? (byte)255 : (byte)0,
            RT = (right & 0x80) != 0 ? (byte)255 : (byte)0,
            LX = Display(Axis(report[6], report[7], highNibble: false)),
            LY = Display(Axis(report[8], report[7], highNibble: true)),
            RX = Display(Axis(report[9], report[10], highNibble: false)),
            RY = Display(Axis(report[11], report[10], highNibble: true))
        };
        reading = new(input, battery, rawBattery);
        return true;
    }
}
