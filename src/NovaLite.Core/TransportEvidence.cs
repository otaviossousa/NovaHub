namespace NovaLite.Core;

public enum PhysicalConnection
{
    Unknown,
    Bluetooth,
    UsbUnresolved,
    UsbCable,
    Dongle24Ghz
}

public sealed record TopologyHop(string InstanceId, string? Enumerator, Guid? BusType);

public sealed record VerifiedUsbSignature(string Id, PhysicalConnection Connection);

public sealed record ConnectionAssessment(
    PhysicalConnection Connection,
    string Confidence,
    string Evidence,
    string? VerifiedSignature = null);

/// <summary>
/// Classifies only evidence that has an explicit Windows bus or a previously validated
/// Nova Lite signature. A USB bus alone cannot distinguish a cable from a USB receiver.
/// </summary>
public static class TransportClassifier
{
    private static readonly Guid UsbBus = new("9D7DEBBC-C85D-11D1-9EB4-006008C3A19A");
    private static readonly Guid BluetoothBus = new("E0CBF06C-CD8B-4647-BB8A-263B43F0F974");

    public static ConnectionAssessment Assess(
        IEnumerable<TopologyHop> chain,
        VerifiedUsbSignature? verifiedSignature = null)
    {
        var hops = chain.ToArray();
        if (hops.Any(IsBluetooth))
            return new(PhysicalConnection.Bluetooth, "alta",
                "A cadeia PnP contém um enumerador ou barramento Bluetooth informado pelo Windows.");

        if (hops.Any(IsUsb))
        {
            if (verifiedSignature?.Connection is PhysicalConnection.UsbCable or PhysicalConnection.Dongle24Ghz)
                return new(verifiedSignature.Connection, "alta",
                    "A cadeia PnP é USB e corresponde a uma assinatura do Nova Lite previamente validada no hardware.",
                    verifiedSignature.Id);

            return new(PhysicalConnection.UsbUnresolved, "alta para o barramento; insuficiente para cabo versus receptor",
                "O host vê um dispositivo USB. Sem assinatura física validada, isso pode ser cabo ou dongle 2,4 GHz.");
        }

        return new(PhysicalConnection.Unknown, "insuficiente",
            "A cadeia PnP não contém evidência de barramento Bluetooth ou USB reconhecida.");
    }

    private static bool IsBluetooth(TopologyHop hop) =>
        hop.BusType == BluetoothBus ||
        hop.Enumerator?.StartsWith("BTH", StringComparison.OrdinalIgnoreCase) == true ||
        hop.InstanceId.StartsWith("BTH", StringComparison.OrdinalIgnoreCase);

    private static bool IsUsb(TopologyHop hop) =>
        hop.BusType == UsbBus ||
        string.Equals(hop.Enumerator, "USB", StringComparison.OrdinalIgnoreCase) ||
        hop.InstanceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase);
}
