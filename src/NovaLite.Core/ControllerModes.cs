namespace NovaLite.Core;

public enum NovaLiteProtocolMode
{
    Unknown,
    XInput,
    AndroidHid,
    DualShock4,
    Switch
}

public static class ControllerModes
{
    public static NovaLiteProtocolMode Identify(ushort vendorId, ushort productId) =>
        (vendorId, productId) switch
        {
            (0x3537, 0x1040) => NovaLiteProtocolMode.XInput,
            (0x3537, 0x1041) => NovaLiteProtocolMode.AndroidHid,
            (0x054C, 0x09CC) => NovaLiteProtocolMode.DualShock4,
            (0x057E, 0x2009) => NovaLiteProtocolMode.Switch,
            _ => NovaLiteProtocolMode.Unknown
        };

    public static string? DisplayName(ushort vendorId, ushort productId, PhysicalConnection connection) =>
        Identify(vendorId, productId) switch
        {
            NovaLiteProtocolMode.XInput when connection == PhysicalConnection.Dongle24Ghz =>
                "GameSir Nova Lite • Dongle 2,4 GHz",
            NovaLiteProtocolMode.XInput when connection == PhysicalConnection.UsbCable =>
                "GameSir Nova Lite • Cabo USB",
            NovaLiteProtocolMode.XInput => "GameSir Nova Lite • USB não identificado",
            NovaLiteProtocolMode.AndroidHid => "GameSir Nova Lite • Android",
            NovaLiteProtocolMode.DualShock4 => "GameSir Nova Lite • DualShock",
            NovaLiteProtocolMode.Switch => "GameSir Nova Lite • Switch",
            _ => null
        };
}
