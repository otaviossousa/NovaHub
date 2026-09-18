using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NovaLite.Core;
using DrawingPen = System.Drawing.Pen;

namespace NovaLite.Desktop;

internal sealed class TrayBatteryIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu = new();
    private string _lastState = "";
    private bool _disposed;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public TrayBatteryIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "NovaHub",
            Icon = CreateBrandIcon()
        };
        _notifyIcon.ContextMenuStrip = _menu;
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        RebuildMenu([], []);
    }

    public void Update(SlotSnapshot[] slots, WindowsDevice[] windowsDevices)
    {
        if (_disposed) return;
        var connected = slots.Where(s => s.Input != null).ToArray();
        // A mesma conexão XInput também pode aparecer como um Gamepad padrão
        // no Windows.Gaming.Input. Não conte essa segunda representação.
        var independentWindows = WindowsDeviceFilter.VisibleDevices(windowsDevices, connected.Length > 0);
        var totalConnected = connected.Length + independentWindows.Length;
        var summary = totalConnected switch
        {
            0 => LanguageManager.Get("NoControllersConnected"),
            1 => LanguageManager.Get("OneControllerConnected"),
            _ => LanguageManager.Format("ManyControllersConnected", totalConnected)
        };
        _notifyIcon.Text = summary.Length <= 63 ? summary : summary[..63];
        var state = LanguageManager.CurrentKey + "|" + string.Join("|", connected.Select(s => $"x{s.Slot}:{s.Battery.Status}:{s.Battery.RawLevel}:{s.Battery.Label}")) +
            "|" + string.Join("|", independentWindows.Select(d => $"w{d.Key}:{d.Battery.Status}:{d.Battery.Label}"));
        if (state != _lastState)
        {
            _lastState = state;
            RebuildMenu(connected, independentWindows);
        }
    }

    private void RebuildMenu(SlotSnapshot[] connected, WindowsDevice[] windowsDevices)
    {
        foreach (ToolStripItem item in _menu.Items)
            item.Image?.Dispose();
        _menu.Items.Clear();
        var title = new ToolStripMenuItem("NovaHub") { Enabled = false };
        _menu.Items.Add(title);
        _menu.Items.Add(new ToolStripSeparator());
        if (connected.Length == 0 && windowsDevices.Length == 0)
            _menu.Items.Add(new ToolStripMenuItem(LanguageManager.Get("NoControllersConnected")) { Enabled = false });
        else
        {
            var levels = connected.Select(slot => slot.Battery.Level is { } level ? (int?)level : null)
                .Concat(windowsDevices.Select(device => WindowsBatteryLevel(device.Battery)));
            foreach (var (level, number) in levels.Select((level, number) => (level, number + 1)))
            {
                var item = new ToolStripMenuItem($"{LanguageManager.Get("Controller")} {number}")
                {
                    ImageAlign = ContentAlignment.MiddleRight,
                    TextImageRelation = TextImageRelation.TextBeforeImage,
                    Padding = new Padding(4, 0, 24, 0)
                };
                item.Image = CreateBatteryBitmap(level);
                _menu.Items.Add(item);
            }
        }
        _menu.Items.Add(new ToolStripSeparator());
        var open = new ToolStripMenuItem(LanguageManager.Get("OpenNovaHub"));
        open.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        _menu.Items.Add(open);
        var exit = new ToolStripMenuItem(LanguageManager.Get("Exit"));
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _menu.Items.Add(exit);
    }

    public void InvalidateLanguage() => _lastState = "";

    private static int? WindowsBatteryLevel(WindowsBattery battery)
    {
        if (battery.Percent is { } percent) return percent switch { <= 5 => 0, <= 20 => 1, <= 60 => 2, _ => 3 };
        return battery.Category is { } category ? (int)category : null;
    }

    private static Bitmap CreateBatteryBitmap(int? level)
    {
        var bitmap = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var outline = new DrawingPen(Color.DimGray, 1.2f);
        using var pole = new SolidBrush(Color.White);
        graphics.DrawRectangle(outline, 1, 3, 11, 9);
        graphics.FillRectangle(pole, 12, 5, 2, 4);
        if (level is null)
        {
            using var red = new DrawingPen(Color.Firebrick, 1.5f);
            graphics.DrawLine(red, 2, 4, 11, 11);
            graphics.DrawLine(red, 11, 4, 2, 11);
        }
        else
        {
            var color = level.Value switch { <= 0 => Color.FromArgb(104, 115, 125), 1 => Color.Firebrick, 2 => Color.DarkOrange, _ => Color.SeaGreen };
            using var fill = new SolidBrush(color);
            var width = Math.Clamp((level.Value + 1) * 3, 2, 10);
            graphics.FillRectangle(fill, 2, 5, width, 6);
        }
        return bitmap;
    }

    private static Icon CreateBrandIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/NovaHub_White_Icon.png"));
            if (resource != null)
            {
                using var stream = resource.Stream;
                using var bitmap = new Bitmap(stream);
                return CloneIcon(bitmap);
            }
        }
        catch { }

        using var fallback = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(fallback);
        graphics.Clear(Color.FromArgb(25, 70, 75));
        using var pen = new DrawingPen(Color.FromArgb(144, 227, 200), 3);
        graphics.DrawString("N", new Font("Segoe UI", 18, FontStyle.Bold), Brushes.White, 5, 3);
        return CloneIcon(fallback);
    }

    private static Icon CloneIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}
