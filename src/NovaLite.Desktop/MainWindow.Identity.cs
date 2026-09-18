using NovaLite.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace NovaLite.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<(uint Slot, Guid Session), PhysicalConnection> _sessionConnections = [];

    private PhysicalControllerDevice? SelectedPhysicalDevice()
    {
        if (WindowsKey != null)
            return SelectedWindowsDevice is { } windowsDevice ? ResolvePhysical(windowsDevice) : null;
        return ResolveXInputPhysical(_monitor.Snapshot());
    }

    private void ChooseConnection(object sender, RoutedEventArgs e)
    {
        var physical = SelectedPhysicalDevice();
        if (physical?.Transport.Connection is not (PhysicalConnection.Unknown or PhysicalConnection.UsbUnresolved))
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = ConnectionButton,
            Placement = PlacementMode.Bottom
        };
        menu.Items.Add(ConnectionChoice(LanguageManager.Get("UsbCable"), PhysicalConnection.UsbCable));
        menu.Items.Add(ConnectionChoice("Dongle 2,4 GHz", PhysicalConnection.Dongle24Ghz));
        menu.IsOpen = true;
    }

    private MenuItem ConnectionChoice(string label, PhysicalConnection connection)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => SetSessionConnection(connection);
        return item;
    }

    private void SetSessionConnection(PhysicalConnection connection)
    {
        if (WindowsKey != null) return;
        var slots = _monitor.Snapshot();
        var slot = Selected;
        if (slots[slot].Session is not { } session)
        {
            MessageBox.Show(this,
                LanguageManager.Get("NoActiveSession"), LanguageManager.Get("ConnectionIdentification"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _sessionConnections[(slot, session)] = connection;
        Render();
    }

    private WindowsDevice? ResolveUniqueWindowsIdentity(SlotSnapshot[] slots)
    {
        if (slots.Count(IsLiveXInput) != 1) return null;
        var inventory = _windows.Snapshot;
        if (DateTimeOffset.UtcNow - inventory.At >= TimeSpan.FromSeconds(2)) return null;
        // A Switch-compatible Bluetooth controller is a separate physical device,
        // not the Windows identity of an unrelated live XInput slot.
        var candidates = inventory.Devices.Where(d => d.HasStandardGamepad &&
            !ControllerProfiles.IsNovaLiteSwitchMode(d.VendorId, d.ProductId)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private PhysicalControllerDevice? ResolveXInputPhysical(SlotSnapshot[] slots, uint? requestedSlot = null)
    {
        var physical = _topology.Snapshot;
        if (DateTimeOffset.UtcNow - physical.At >= TimeSpan.FromSeconds(8)) return null;
        var slot = requestedSlot ?? Selected;
        PhysicalControllerDevice? resolved = null;
        var xinputDevices = physical.Devices.Where(d => d.XInputInterfaceIndex != null).ToArray();
        var mappedIndex = XInputInterfaceAssociation.MapSlotToInterfaceIndex(slot,
            slots.Where(IsLiveXInput).Select(s => s.Slot),
            xinputDevices.Select(d => d.XInputInterfaceIndex!.Value));
        if (mappedIndex is { } interfaceIndex)
        {
            var mapped = xinputDevices.Where(d => d.XInputInterfaceIndex == interfaceIndex).ToArray();
            if (mapped.Length == 1) resolved = mapped[0];
        }

        if (resolved == null)
        {
            var indexed = xinputDevices.Where(d => d.XInputInterfaceIndex == slot).ToArray();
            if (indexed.Length == 1) resolved = indexed[0];
        }

        if (resolved == null && slots.Count(IsLiveXInput) == 1)
        {
            var identity = ResolveUniqueWindowsIdentity(slots);
            resolved = identity != null ? ResolvePhysical(identity) :
                physical.Devices.Length == 1 ? physical.Devices[0] : null;
        }
        return ApplySessionConnection(resolved, slot, slots);
    }

    private PhysicalControllerDevice? ApplySessionConnection(PhysicalControllerDevice? device, uint slot,
        SlotSnapshot[] slots)
    {
        if (device == null || slots[slot].Session is not { } session ||
            !_sessionConnections.TryGetValue((slot, session), out var connection)) return device;
        return device with
        {
            Transport = new(connection, "informada pelo usuário nesta sessão",
                "Escolha temporária; não vinculada nem gravada em porta USB.", "escolha-da-sessao"),
            Model = ControllerModes.DisplayName(device.VendorId, device.ProductId, connection) ?? device.Model
        };
    }

    private PhysicalControllerDevice? ResolvePhysical(WindowsDevice device)
    {
        var physical = _topology.Snapshot;
        if (DateTimeOffset.UtcNow - physical.At >= TimeSpan.FromSeconds(8)) return null;
        return physical.UniqueMatch(device.VendorId, device.ProductId);
    }

    private static bool IsLiveXInput(SlotSnapshot slot) =>
        slot.Input != null && DateTimeOffset.UtcNow - slot.SampleAt < TimeSpan.FromSeconds(1);

    private static BatteryReading SelectBattery(BatteryReading xinput, PhysicalControllerDevice? device)
    {
        if (xinput.HasLevel)
            return ControllerModes.Identify(device?.VendorId ?? 0, device?.ProductId ?? 0) == NovaLiteProtocolMode.XInput
                ? xinput with { Label = $"{xinput.Label} • informado pelo XInput" }
                : xinput;
        if (device?.Battery is not { } hid) return xinput;
        return new(BatteryStatus.Valid, $"{hid.Percent}% informado por HID", hid.ReadAt, hid.ReadAt,
            RawType: 3, RawLevel: (byte)hid.Level);
    }

}
