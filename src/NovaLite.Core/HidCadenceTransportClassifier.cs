namespace NovaLite.Core;

public static class HidCadenceTransportClassifier
{
    public static PhysicalConnection? Assess(int intervalCount, double? averageMilliseconds)
    {
        if (intervalCount < 40 || averageMilliseconds is not { } average ||
            !double.IsFinite(average) || average <= 0) return null;
        if (average <= 2.0) return PhysicalConnection.UsbCable;
        if (average >= 3.0) return PhysicalConnection.Dongle24Ghz;
        return null;
    }
}
