using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace NovaLite.Desktop;

public sealed class StickPlot : FrameworkElement
{
    private Point? _point;
    private readonly Queue<Point> _trail = new();
    private IReadOnlyList<double?> _precisionRadii = Array.Empty<double?>();
    public double? PrecisionAverageErrorPercent { get; private set; }
    public bool Recording { get; set; }
    public void Clear() { _trail.Clear(); InvalidateVisual(); }
    public void Update(double? x, double? y)
    {
        _point = x is { } a && y is { } b ? new Point(a, b) : null;
        if (_point is { } p && Recording) { _trail.Enqueue(p); while (_trail.Count > 1800) _trail.Dequeue(); }
        if (_point == null) _trail.Clear();
        InvalidateVisual();
    }
    public void Show(double? x, double? y, IEnumerable<NovaLite.Core.StickPoint> trail)
    {
        _point = x is { } a && y is { } b ? new Point(a, b) : null;
        _trail.Clear();
        if (_point != null) foreach (var p in trail) _trail.Enqueue(new Point(p.X, p.Y));
        InvalidateVisual();
    }
    public void ShowPrecision(NovaLite.Core.RotationCircularityResult? result)
    {
        _precisionRadii = result?.Radii ?? Array.Empty<double?>();
        PrecisionAverageErrorPercent = result?.AverageErrorPercent;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        var c = new Point(ActualWidth / 2, 67); const double r = 54;
        var muted = ThemeManager.Brush(this, "ControllerDetailBrush", "#435B67");
        var mint = ThemeManager.Brush(this, "AccentBrush", "#90E3C8");
        var surface = ThemeManager.Brush(this, "WindowBackgroundBrush", "#10181F");
        var within = ThemeManager.Brush(this, "CircularityWithinBrush", "#68DDA3");
        var beyond = ThemeManager.Brush(this, "CircularityBeyondBrush", "#A991FF");
        var panel = ThemeManager.Brush(this, "PanelBrush", "#19232C");
        var border = ThemeManager.Brush(this, "BorderStrongBrush", "#506673");
        dc.DrawEllipse(surface, new Pen(muted, 1), c, r, r);
        if (_precisionRadii.Count > 0)
        {
            var step = Math.PI * 2 / _precisionRadii.Count;
            dc.PushOpacity(.82);
            for (var i = 0; i < _precisionRadii.Count; i++)
            {
                if (_precisionRadii[i] is not { } measured) continue;
                var shown = Math.Min(measured, 1);
                var start = i * step + .012;
                var end = (i + 1) * step - .012;
                var p1 = new Point(c.X + Math.Cos(start) * r * shown, c.Y - Math.Sin(start) * r * shown);
                var p2 = new Point(c.X + Math.Cos(end) * r * shown, c.Y - Math.Sin(end) * r * shown);
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(c, true, true);
                    context.LineTo(p1, true, false);
                    context.LineTo(p2, true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(measured >= 1 ? beyond : within, null, geometry);
            }
            dc.Pop();
        }
        dc.DrawLine(new Pen(muted, 1), new(c.X - r, c.Y), new(c.X + r, c.Y));
        dc.DrawLine(new Pen(muted, 1), new(c.X, c.Y - r), new(c.X, c.Y + r));
        dc.DrawEllipse(null, new Pen(border, 1.5), c, r, r);
        Point Map(Point p) => new(c.X + p.X * r, c.Y - p.Y * r);
        Point? previous = null;
        foreach (var p in _trail) { var q = Map(p); if (previous is { } old) dc.DrawLine(new Pen(mint, 1), old, q); previous = q; }
        if (_point is { } point) dc.DrawEllipse(mint, null, Map(point), 4, 4);
        if (PrecisionAverageErrorPercent is { } error)
        {
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var textBrush = ThemeManager.Brush(this, "TextPrimaryBrush", "#FFFFFF");
            dc.PushOpacity(.95);
            dc.DrawEllipse(panel, new Pen(border, 1), c, 28, 25);
            dc.Pop();
            var label = new FormattedText("Erro médio", CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 9, textBrush, dpi);
            var value = new FormattedText($"{error:F1}%", CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 17, textBrush, dpi);
            dc.DrawText(label, new Point(c.X - label.Width / 2, c.Y - 16));
            dc.DrawText(value, new Point(c.X - value.Width / 2, c.Y - 4));
        }
        var text = _point is { } v ? $"X {v.X:F3}   Y {v.Y:F3}" : "X —   Y —";
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, new Typeface("Consolas"), 11, ThemeManager.Brush(this, "TextSecondaryBrush", "#D3D3D3"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(c.X - formatted.Width / 2, 134));
    }
}

