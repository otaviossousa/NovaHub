using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NovaLite.Core;

namespace NovaLite.Desktop;

public sealed record PnpNode(
    string InstanceId,
    string? ParentInstanceId,
    string? FriendlyName,
    string? Description,
    string? Manufacturer,
    string? ClassName,
    string? Service,
    string? Enumerator,
    Guid? BusType,
    Guid? ContainerId,
    string[] HardwareIds,
    string? Location,
    DateTimeOffset? LastArrival);

public sealed record HidCollection(
    string DevicePath,
    string InstanceId,
    ushort? VendorId,
    ushort? ProductId,
    ushort? VersionNumber,
    string? Manufacturer,
    string? Product,
    string? SerialNumber,
    ushort? UsagePage,
    ushort? Usage,
    ushort? InputReportBytes,
    ushort? OutputReportBytes,
    ushort? FeatureReportBytes,
    ushort? InputValueCapabilities,
    ushort? FeatureValueCapabilities,
    byte[] InputReportIds,
    HidBatteryObservation[] StandardBattery,
    HidPowerCapability[] BatterySystemCapabilities,
    string? OpenError,
    string[] Ancestors,
    ConnectionAssessment Transport);

public sealed record HidBatteryObservation(
    string ReportType,
    ushort UsagePage,
    ushort Usage,
    byte ReportId,
    int LogicalMinimum,
    int LogicalMaximum,
    uint? RawValue,
    string Status);

public sealed record HidPowerCapability(
    string ReportType,
    ushort UsagePage,
    ushort UsageMinimum,
    ushort UsageMaximum,
    byte ReportId,
    ushort BitSize,
    ushort ReportCount,
    int LogicalMinimum,
    int LogicalMaximum);

public sealed record NativeTopologySnapshot(
    DateTimeOffset CreatedAt,
    string Scope,
    PnpNode[] RelevantNodes,
    HidCollection[] HidCollections,
    string[] Limitations);

/// <summary>
/// Read-only SetupAPI/Configuration Manager/HID inventory. It does not send output or
/// vendor feature reports. Only the standard HID Battery Strength usage (06:20) is read.
/// </summary>
public static class NativeDeviceTopology
{
    private const uint DigcfPresent = 0x2;
    private const uint DigcfAllClasses = 0x4;
    private const uint DigcfDeviceInterface = 0x10;
    private const uint SpdrpDeviceDesc = 0;
    private const uint SpdrpHardwareId = 1;
    private const uint SpdrpService = 4;
    private const uint SpdrpClass = 7;
    private const uint SpdrpManufacturer = 11;
    private const uint SpdrpFriendlyName = 12;
    private const uint SpdrpLocationInformation = 13;
    private const uint SpdrpBusTypeGuid = 19;
    private const uint SpdrpEnumeratorName = 22;
    private const uint ErrorNoMoreItems = 259;
    private const uint FileShareRead = 1;
    private const uint FileShareWrite = 2;
    private const uint OpenExisting = 3;
    private const int HidpInput = 0;
    private const int HidpFeature = 2;
    private const int HidpStatusSuccess = 0x00110000;
    private static readonly Guid HidInterface = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    private static readonly DevPropKey ContainerIdKey = new(new("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);
    private static readonly DevPropKey LastArrivalKey = new(new("83DA6326-97A6-4088-9453-A1923F573B29"), 102);

    public static NativeTopologySnapshot Capture()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("A sondagem PnP exige Windows.");
        var nodes = EnumerateNodes();
        var hids = EnumerateHidCollections(nodes);
        var relevant = new HashSet<string>(hids.Select(h => h.InstanceId), StringComparer.OrdinalIgnoreCase);
        foreach (var hid in hids)
            foreach (var ancestor in hid.Ancestors) relevant.Add(ancestor);
        foreach (var node in nodes.Values.Where(IsControllerCandidate)) relevant.Add(node.InstanceId);
        var selected = relevant.Select(id => nodes.GetValueOrDefault(id)).Where(n => n != null).Cast<PnpNode>()
            .OrderBy(n => n.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(DateTimeOffset.UtcNow,
            "Interfaces HID presentes, seus ancestrais PnP e nós candidatos a controle/XInput",
            selected, hids.OrderBy(h => h.InstanceId, StringComparer.OrdinalIgnoreCase).ToArray(),
            [
                "Bluetooth é confirmado apenas por barramento/ancestral PnP Bluetooth.",
                "Um barramento USB não distingue cabo do receptor 2,4 GHz sem assinatura física previamente validada.",
                "XInput não fornece Device Instance ID; este relatório não associa automaticamente uma interface HID a um slot XInput.",
                "Somente o uso HID padronizado Battery Strength (Usage Page 06, Usage 20) é interpretado como candidato de bateria.",
                "Feature reports proprietários não são enviados nem interpretados. Device paths podem conter identificadores locais; revise antes de compartilhar."
            ]);
    }

    private static Dictionary<string, PnpNode> EnumerateNodes()
    {
        var result = new Dictionary<string, PnpNode>(StringComparer.OrdinalIgnoreCase);
        var set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = NewDeviceInfoData();
                if (!SetupDiEnumDeviceInfo(set, index, ref data))
                {
                    if ((uint)Marshal.GetLastWin32Error() == ErrorNoMoreItems) break;
                    continue;
                }
                var id = GetInstanceId(set, ref data);
                if (id == null) continue;
                result[id] = new(id, GetParentId(data.DevInst),
                    GetStringProperty(set, ref data, SpdrpFriendlyName),
                    GetStringProperty(set, ref data, SpdrpDeviceDesc),
                    GetStringProperty(set, ref data, SpdrpManufacturer),
                    GetStringProperty(set, ref data, SpdrpClass),
                    GetStringProperty(set, ref data, SpdrpService),
                    GetStringProperty(set, ref data, SpdrpEnumeratorName),
                    GetGuidProperty(set, ref data, SpdrpBusTypeGuid),
                    GetDeviceGuidProperty(set, ref data, ContainerIdKey),
                    GetMultiStringProperty(set, ref data, SpdrpHardwareId),
                    GetStringProperty(set, ref data, SpdrpLocationInformation),
                    GetDeviceFileTimeProperty(set, ref data, LastArrivalKey));
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    private static HidCollection[] EnumerateHidCollections(Dictionary<string, PnpNode> nodes)
    {
        var result = new List<HidCollection>();
        var hidGuid = HidInterface;
        var set = SetupDiGetClassDevsW(ref hidGuid, null, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = NewInterfaceData();
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                {
                    if ((uint)Marshal.GetLastWin32Error() == ErrorNoMoreItems) break;
                    continue;
                }
                _ = SetupDiGetDeviceInterfaceDetailW(set, ref interfaceData, IntPtr.Zero, 0, out var required, IntPtr.Zero);
                if (required == 0) continue;
                var detail = Marshal.AllocHGlobal((int)required);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    var deviceData = NewDeviceInfoData();
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref interfaceData, detail, required, out _, ref deviceData)) continue;
                    var path = Marshal.PtrToStringUni(detail + 4);
                    var id = GetInstanceId(set, ref deviceData);
                    if (path == null || id == null) continue;
                    result.Add(InspectHid(path, id, BuildAncestors(id, nodes), nodes));
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return result.ToArray();
    }

    private static HidCollection InspectHid(string path, string instanceId, string[] ancestors,
        Dictionary<string, PnpNode> nodes)
    {
        var chainIds = new[] { instanceId }.Concat(ancestors).ToArray();
        var assessment = TransportClassifier.Assess(chainIds.Select(id => nodes.TryGetValue(id, out var node)
            ? new TopologyHop(id, node.Enumerator, node.BusType)
            : new TopologyHop(id, null, null)));
        using var handle = CreateFileW(path, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
            return new(path, instanceId, null, null, null, null, null, null, null, null, null, null, null, null, null,
                [], [], [], new Win32Exception(Marshal.GetLastWin32Error()).Message, ancestors, assessment);

        var attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        ushort? vid = null, pid = null, version = null;
        if (HidD_GetAttributes(handle, ref attributes)) { vid = attributes.VendorId; pid = attributes.ProductId; version = attributes.VersionNumber; }
        string? manufacturer = GetHidString(handle, HidD_GetManufacturerString);
        string? product = GetHidString(handle, HidD_GetProductString);
        string? serial = GetHidString(handle, HidD_GetSerialNumberString);
        if (!HidD_GetPreparsedData(handle, out var preparsed))
            return new(path, instanceId, vid, pid, version, manufacturer, product, serial, null, null, null, null, null, null, null,
                [], [], [], "HidD_GetPreparsedData falhou", ancestors, assessment);
        try
        {
            if (HidP_GetCaps(preparsed, out var caps) != HidpStatusSuccess)
                return new(path, instanceId, vid, pid, version, manufacturer, product, serial, null, null, null, null, null, null, null,
                    [], [], [], "HidP_GetCaps falhou", ancestors, assessment);
            var battery = ReadStandardBattery(handle, preparsed, caps);
            var powerCapabilities = ReadBatterySystemCapabilities(preparsed, caps);
            var inputReportIds = ReadInputReportIds(preparsed, caps);
            return new(path, instanceId, vid, pid, version, manufacturer, product, serial,
                caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength,
                caps.NumberInputValueCaps, caps.NumberFeatureValueCaps, inputReportIds, battery, powerCapabilities, null, ancestors, assessment);
        }
        finally { HidD_FreePreparsedData(preparsed); }
    }

    private static byte[] ReadInputReportIds(IntPtr preparsed, HidpCaps caps)
    {
        if (caps.NumberInputValueCaps == 0) return [];
        var found = new HidpValueCaps[caps.NumberInputValueCaps];
        ushort count = caps.NumberInputValueCaps;
        return HidP_GetValueCaps(HidpInput, found, ref count, preparsed) == HidpStatusSuccess
            ? found.Take(count).Select(c => c.ReportId).Distinct().Order().ToArray()
            : [];
    }

    private static HidPowerCapability[] ReadBatterySystemCapabilities(IntPtr preparsed, HidpCaps caps)
    {
        var result = new List<HidPowerCapability>();
        Read(HidpInput, "Input", caps.NumberInputValueCaps);
        Read(HidpFeature, "Feature", caps.NumberFeatureValueCaps);
        return result.ToArray();

        void Read(int reportType, string label, ushort maximumCaps)
        {
            if (maximumCaps == 0) return;
            var found = new HidpValueCaps[maximumCaps];
            ushort count = maximumCaps;
            if (HidP_GetValueCaps(reportType, found, ref count, preparsed) != HidpStatusSuccess) return;
            foreach (var cap in found.Take(count).Where(c => c.UsagePage is 0x84 or 0x85 || c.LinkUsagePage is 0x84 or 0x85))
            {
                var minimum = cap.Values.UsageMinOrUsage;
                var maximum = cap.IsRange != 0 ? cap.Values.UsageMaxOrReserved : minimum;
                result.Add(new(label, cap.UsagePage, minimum, maximum, cap.ReportId, cap.BitSize, cap.ReportCount,
                    cap.LogicalMin, cap.LogicalMax));
            }
        }
    }

    private static HidBatteryObservation[] ReadStandardBattery(SafeFileHandle handle, IntPtr preparsed, HidpCaps caps)
    {
        var result = new List<HidBatteryObservation>();
        Read(HidpFeature, "Feature", caps.NumberFeatureValueCaps, caps.FeatureReportByteLength, HidD_GetFeature);
        Read(HidpInput, "Input", caps.NumberInputValueCaps, caps.InputReportByteLength, HidD_GetInputReport);
        return result.ToArray();

        void Read(int reportType, string label, ushort maximumCaps, ushort reportBytes, HidReportReader reader)
        {
            if (maximumCaps == 0 || reportBytes == 0) return;
            var found = new HidpValueCaps[maximumCaps];
            ushort count = maximumCaps;
            if (HidP_GetSpecificValueCaps(reportType, 0x06, 0, 0x20, found, ref count, preparsed) != HidpStatusSuccess) return;
            foreach (var cap in found.Take(count))
            {
                var report = new byte[reportBytes];
                report[0] = cap.ReportId;
                if (!reader(handle, report, report.Length))
                {
                    result.Add(new(label, 0x06, 0x20, cap.ReportId, cap.LogicalMin, cap.LogicalMax, null,
                        $"capacidade declarada; leitura falhou: {new Win32Exception(Marshal.GetLastWin32Error()).Message}"));
                    continue;
                }
                var status = HidP_GetUsageValue(reportType, 0x06, cap.LinkCollection, 0x20, out var raw,
                    preparsed, report, (uint)report.Length);
                result.Add(new(label, 0x06, 0x20, cap.ReportId, cap.LogicalMin, cap.LogicalMax,
                    status == HidpStatusSuccess ? raw : null,
                    status == HidpStatusSuccess ? "valor padronizado observado; não convertido em porcentagem" : $"HidP_GetUsageValue: 0x{status:X8}"));
            }
        }
    }

    private static string[] BuildAncestors(string id, Dictionary<string, PnpNode> nodes)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        var current = id;
        while (nodes.TryGetValue(current, out var node) && node.ParentInstanceId is { } parent && seen.Add(parent))
        {
            result.Add(parent); current = parent;
        }
        return result.ToArray();
    }

    private static bool IsControllerCandidate(PnpNode node)
    {
        var text = string.Join('|', node.InstanceId, node.FriendlyName, node.Description, node.Service, node.ClassName,
            string.Join('|', node.HardwareIds));
        return text.Contains("IG_", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("XINPUT", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("XUSB", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("GAMESIR", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("NOVA", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("GAMEPAD", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("CONTROLADOR", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("CONTROLLER", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetInstanceId(IntPtr set, ref SpDevinfoData data)
    {
        var buffer = new StringBuilder(1024);
        return SetupDiGetDeviceInstanceIdW(set, ref data, buffer, buffer.Capacity, out _) ? buffer.ToString() : null;
    }

    private static string? GetParentId(uint devInst)
    {
        if (CM_Get_Parent(out var parent, devInst, 0) != 0) return null;
        if (CM_Get_Device_ID_Size(out var size, parent, 0) != 0) return null;
        var buffer = new StringBuilder((int)size + 1);
        return CM_Get_Device_IDW(parent, buffer, buffer.Capacity, 0) == 0 ? buffer.ToString() : null;
    }

    private static byte[]? GetRegistryProperty(IntPtr set, ref SpDevinfoData data, uint property, out uint type)
    {
        type = 0; var buffer = new byte[4096];
        if (!SetupDiGetDeviceRegistryPropertyW(set, ref data, property, out type, buffer, (uint)buffer.Length, out var required))
            return null;
        if (required < buffer.Length) Array.Resize(ref buffer, (int)required);
        return buffer;
    }

    private static string? GetStringProperty(IntPtr set, ref SpDevinfoData data, uint property)
    {
        var bytes = GetRegistryProperty(set, ref data, property, out _);
        return bytes == null ? null : Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private static string[] GetMultiStringProperty(IntPtr set, ref SpDevinfoData data, uint property)
    {
        var value = GetStringProperty(set, ref data, property);
        return value?.Split('\0', StringSplitOptions.RemoveEmptyEntries) ?? [];
    }

    private static Guid? GetGuidProperty(IntPtr set, ref SpDevinfoData data, uint property)
    {
        var bytes = GetRegistryProperty(set, ref data, property, out _);
        return bytes is { Length: >= 16 } ? new Guid(bytes.AsSpan(0, 16)) : null;
    }

    private static Guid? GetDeviceGuidProperty(IntPtr set, ref SpDevinfoData data, DevPropKey key)
    {
        var buffer = new byte[16]; var local = key;
        return SetupDiGetDevicePropertyW(set, ref data, ref local, out _, buffer, (uint)buffer.Length, out _, 0)
            ? new Guid(buffer) : null;
    }

    private static DateTimeOffset? GetDeviceFileTimeProperty(IntPtr set, ref SpDevinfoData data, DevPropKey key)
    {
        var buffer = new byte[8]; var local = key;
        if (!SetupDiGetDevicePropertyW(set, ref data, ref local, out _, buffer, (uint)buffer.Length, out _, 0))
            return null;
        var fileTime = BitConverter.ToInt64(buffer, 0);
        if (fileTime <= 0) return null;
        try { return new DateTimeOffset(DateTime.FromFileTimeUtc(fileTime), TimeSpan.Zero); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private delegate bool HidStringReader(SafeFileHandle handle, StringBuilder buffer, int length);
    private delegate bool HidReportReader(SafeFileHandle handle, byte[] buffer, int length);
    private static string? GetHidString(SafeFileHandle handle, HidStringReader reader)
    {
        var value = new StringBuilder(256);
        return reader(handle, value, value.Capacity * 2) ? value.ToString() : null;
    }

    private static SpDevinfoData NewDeviceInfoData() => new() { Size = Marshal.SizeOf<SpDevinfoData>() };
    private static SpDeviceInterfaceData NewInterfaceData() => new() { Size = Marshal.SizeOf<SpDeviceInterfaceData>() };

    [StructLayout(LayoutKind.Sequential)] private struct SpDevinfoData { public int Size; public Guid ClassGuid; public uint DevInst; public UIntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct SpDeviceInterfaceData { public int Size; public Guid InterfaceClassGuid; public uint Flags; public UIntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct DevPropKey { public Guid Fmtid; public uint Pid; public DevPropKey(Guid fmtid, uint pid) { Fmtid = fmtid; Pid = pid; } }
    [StructLayout(LayoutKind.Sequential)] private struct HiddAttributes { public int Size; public ushort VendorId; public ushort ProductId; public ushort VersionNumber; }
    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct HidpValueCapsUnion
    {
        [FieldOffset(0)] public ushort UsageMinOrUsage;
        [FieldOffset(2)] public ushort UsageMaxOrReserved;
        [FieldOffset(4)] public ushort StringMinOrIndex;
        [FieldOffset(6)] public ushort StringMaxOrReserved;
        [FieldOffset(8)] public ushort DesignatorMinOrIndex;
        [FieldOffset(10)] public ushort DesignatorMaxOrReserved;
        [FieldOffset(12)] public ushort DataIndexMinOrIndex;
        [FieldOffset(14)] public ushort DataIndexMaxOrReserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HidpValueCaps
    {
        public ushort UsagePage;
        public byte ReportId, IsAlias;
        public ushort BitField, LinkCollection, LinkUsage, LinkUsagePage;
        public byte IsRange, IsStringRange, IsDesignatorRange, IsAbsolute, HasNull, Reserved;
        public ushort BitSize, ReportCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public ushort[] Reserved2;
        public uint UnitsExp, Units;
        public int LogicalMin, LogicalMax, PhysicalMin, PhysicalMax;
        public HidpValueCapsUnion Values;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SpDevinfoData data);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr data, ref Guid guid, uint index, ref SpDeviceInterfaceData interfaceData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SpDeviceInterfaceData interfaceData, IntPtr detail, uint detailSize, out uint required, IntPtr deviceInfoData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SpDeviceInterfaceData interfaceData, IntPtr detail, uint detailSize, out uint required, ref SpDevinfoData deviceInfoData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SpDevinfoData data, StringBuilder id, int size, out int required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SpDevinfoData data, uint property, out uint type, byte[] buffer, uint size, out uint required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref SpDevinfoData data, ref DevPropKey key, out uint type, byte[] buffer, uint size, out uint required, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Parent(out uint parent, uint devInst, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Device_ID_Size(out uint length, uint devInst, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_IDW(uint devInst, StringBuilder buffer, int length, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HiddAttributes attributes);
    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool HidD_GetManufacturerString(SafeFileHandle handle, StringBuilder buffer, int length);
    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool HidD_GetProductString(SafeFileHandle handle, StringBuilder buffer, int length);
    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool HidD_GetSerialNumberString(SafeFileHandle handle, StringBuilder buffer, int length);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out HidpCaps caps);
    [DllImport("hid.dll")] private static extern int HidP_GetSpecificValueCaps(int reportType, ushort usagePage, ushort linkCollection, ushort usage, [Out] HidpValueCaps[] caps, ref ushort count, IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetValueCaps(int reportType, [Out] HidpValueCaps[] caps, ref ushort count, IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint value, IntPtr data, byte[] report, uint reportLength);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetFeature(SafeFileHandle handle, byte[] report, int length);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetInputReport(SafeFileHandle handle, byte[] report, int length);
}
