namespace NovaLite.Core;

/// <summary>Perfis de entrada validados para dispositivos que o Windows expõe apenas como HID genérico.</summary>
public static class ControllerProfiles
{
    public static bool IsNovaLiteSwitchMode(ushort vendorId, ushort productId) =>
        ControllerModes.Identify(vendorId, productId) == NovaLiteProtocolMode.Switch;

    public static RawMapping? Create(ushort vendorId, ushort productId, int buttonCount,
        int axisCount, int switchCount)
    {
        // GameSir Nova Lite em modo Bluetooth, observado no Windows como 3537:1041:
        // 16 botões HID, quatro eixos de stick, dois gatilhos e um hat switch.
        if (vendorId != 0x3537 || productId != 0x1041 || buttonCount < 10 ||
            axisCount < 6 || switchCount < 1) return null;

        return new RawMapping
        {
            Buttons = new()
            {
                [Buttons.A] = 0,
                [Buttons.B] = 1,
                [Buttons.X] = 3,
                [Buttons.Y] = 4,
                [Buttons.LB] = 6,
                [Buttons.RB] = 7,
                [Buttons.View] = 10,
                [Buttons.Menu] = 11,
                [Buttons.LS] = 13,
                [Buttons.RS] = 14
            },
            Axes = new()
            {
                ["LX"] = 0,
                ["LY"] = 1,
                ["RX"] = 2,
                ["RY"] = 3,
                ["LT"] = 5,
                ["RT"] = 4
            },
            Inverted = new() { "LY", "RY" },
            Hat = 0
        };
    }
}
