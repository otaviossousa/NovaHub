namespace NovaLite.Core;

/// <summary>Decodes the common input section of a DualShock 4 Bluetooth report 0x11.</summary>
public static class Ds4InputDecoder
{
    public static bool TryDecode(ReadOnlySpan<byte> report, out Gamepad gamepad)
    {
        gamepad = default;
        if (report.Length < 12 || report[0] != 0x11) return false;

        var buttons0 = report[7];
        var buttons1 = report[8];
        var buttons = (Buttons)0;

        if ((buttons0 & 0x20) != 0) buttons |= Buttons.A; // Cross
        if ((buttons0 & 0x40) != 0) buttons |= Buttons.B; // Circle
        if ((buttons0 & 0x10) != 0) buttons |= Buttons.X; // Square
        if ((buttons0 & 0x80) != 0) buttons |= Buttons.Y; // Triangle
        if ((buttons1 & 0x01) != 0) buttons |= Buttons.LB;
        if ((buttons1 & 0x02) != 0) buttons |= Buttons.RB;
        if ((buttons1 & 0x10) != 0) buttons |= Buttons.View; // Share
        if ((buttons1 & 0x20) != 0) buttons |= Buttons.Menu; // Options
        if ((buttons1 & 0x40) != 0) buttons |= Buttons.LS;
        if ((buttons1 & 0x80) != 0) buttons |= Buttons.RS;

        buttons |= (buttons0 & 0x0F) switch
        {
            0 => Buttons.Up,
            1 => Buttons.Up | Buttons.Right,
            2 => Buttons.Right,
            3 => Buttons.Down | Buttons.Right,
            4 => Buttons.Down,
            5 => Buttons.Down | Buttons.Left,
            6 => Buttons.Left,
            7 => Buttons.Up | Buttons.Left,
            _ => 0
        };

        gamepad = new Gamepad
        {
            Buttons = buttons,
            LX = RawMapping.DisplayUnsignedByteAxis(report[3]),
            LY = RawMapping.DisplayUnsignedByteAxis(report[4], invert: true),
            RX = RawMapping.DisplayUnsignedByteAxis(report[5]),
            RY = RawMapping.DisplayUnsignedByteAxis(report[6], invert: true),
            LT = report[10],
            RT = report[11]
        };
        return true;
    }
}
