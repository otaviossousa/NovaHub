using System.Runtime.InteropServices;

namespace NovaLite.Core;

[Flags]
public enum Buttons : ushort
{
    Up = 1, Down = 2, Left = 4, Right = 8, Menu = 16, View = 32,
    LS = 64, RS = 128, LB = 256, RB = 512, A = 4096, B = 8192, X = 16384, Y = 32768
}

[StructLayout(LayoutKind.Sequential)]
public struct Gamepad
{
    public Buttons Buttons;
    public byte LT, RT;
    public short LX, LY, RX, RY;
}

[StructLayout(LayoutKind.Sequential)]
public struct InputState { public uint Packet; public Gamepad Gamepad; }

[StructLayout(LayoutKind.Sequential)]
public struct Motors { public ushort Left, Right; }

[StructLayout(LayoutKind.Sequential)]
public struct BatteryRaw { public byte Type, Level; }

[StructLayout(LayoutKind.Sequential)]
public struct Capabilities
{
    public byte Type, Subtype;
    public ushort Flags;
    public Gamepad Gamepad;
    public Motors Vibration;
}

public interface IControllerApi
{
    uint Read(uint slot, out InputState state);
    uint Battery(uint slot, out BatteryRaw battery);
    uint Capabilities(uint slot, out Capabilities capabilities);
    uint Vibrate(uint slot, ushort left, ushort right);
}

public sealed class XInputApi : IControllerApi
{
    public const uint Disconnected = 1167;
    public const uint MissingApi = 0xE0000001;
    public uint Read(uint slot, out InputState state)
    {
        state = default;
        try { return XInputGetState(slot, out state); }
        catch (DllNotFoundException) { return MissingApi; }
        catch (EntryPointNotFoundException) { return MissingApi; }
    }
    public uint Battery(uint slot, out BatteryRaw battery)
    {
        battery = default;
        try { return XInputGetBatteryInformation(slot, 0, out battery); }
        catch (DllNotFoundException) { return MissingApi; }
        catch (EntryPointNotFoundException) { return MissingApi; }
    }
    public uint Capabilities(uint slot, out Capabilities capabilities)
    {
        capabilities = default;
        try { return XInputGetCapabilities(slot, 0, out capabilities); }
        catch (DllNotFoundException) { return MissingApi; }
        catch (EntryPointNotFoundException) { return MissingApi; }
    }
    public uint Vibrate(uint slot, ushort left, ushort right)
    {
        var motors = new Motors { Left = left, Right = right };
        try { return XInputSetState(slot, ref motors); }
        catch (DllNotFoundException) { return MissingApi; }
        catch (EntryPointNotFoundException) { return MissingApi; }
    }

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    private static extern uint XInputGetState(uint index, out InputState state);
    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    private static extern uint XInputGetBatteryInformation(uint index, byte type, out BatteryRaw battery);
    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    private static extern uint XInputGetCapabilities(uint index, uint flags, out Capabilities capabilities);
    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    private static extern uint XInputSetState(uint index, ref Motors motors);
}

public static class Normalize
{
    public static double Axis(short value) => value < 0 ? value / 32768.0 : value / 32767.0;
    public static double Trigger(byte value) => value / 255.0;
}
