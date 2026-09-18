namespace NovaLite.Core;

public static class XInputInterfaceAssociation
{
    public static int? MapSlotToInterfaceIndex(uint requestedSlot,
        IEnumerable<uint> activeSlots, IEnumerable<int> physicalInterfaceIndexes)
    {
        var slots = activeSlots.Distinct().Order().ToArray();
        var interfaces = physicalInterfaceIndexes.Distinct().Order().ToArray();
        if (slots.Length == 0 || slots.Length != interfaces.Length) return null;
        var position = Array.IndexOf(slots, requestedSlot);
        return position < 0 ? null : interfaces[position];
    }
}
