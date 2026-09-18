using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NovaLite.Desktop;

public sealed class CombinationGlyph : FrameworkElement
{
    public string Kind { get; set; } = "Vibration";

    private static Brush Cyan => ThemeManager.Brush("ControllerAccentBrush", "#A6E7F2");
    private static Brush Surface => ThemeManager.Brush("ControllerSurfaceBrush", "#0B1B26");
    private static Pen Outline => new(Cyan, 1.55) { LineJoin = PenLineJoin.Round };

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        const double width = 180;
        const double height = 72;
        var scale = Math.Min(ActualWidth / width, ActualHeight / height);
        dc.PushTransform(new TranslateTransform((ActualWidth - width * scale) / 2,
            (ActualHeight - height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));

        switch (Kind)
        {
            case "Connection": DrawConnection(dc); break;
            case "Deadzone": DrawDeadzone(dc); break;
            case "Layout": DrawLayout(dc); break;
            case "Turbo": DrawTurbo(dc); break;
            default: DrawVibration(dc); break;
        }

        dc.Pop();
        dc.Pop();
    }

    private static void DrawVibration(DrawingContext dc)
    {
        DrawM(dc, 34, 36);
        Text(dc, "+", 68, 36, 18);
        DrawDpad(dc, new Point(116, 36));
    }

    private static void DrawConnection(DrawingContext dc)
    {
        DrawSystemButton(dc, new Point(52, 31), 55, false);
        Text(dc, "+", 90, 36, 18);
        DrawSystemButton(dc, new Point(128, 31), -55, true);
        Text(dc, "View", 52, 62, 9);
        Text(dc, "Menu", 128, 62, 9);
    }

    private static void DrawDeadzone(DrawingContext dc)
    {
        DrawM(dc, 28, 36);
        Text(dc, "+", 60, 36, 18);
        DrawStick(dc, new Point(100, 36), "LS");
        Text(dc, "/", 130, 36, 14);
        DrawStick(dc, new Point(160, 36), "RS");
    }

    private static void DrawLayout(DrawingContext dc)
    {
        DrawM(dc, 48, 36);
        Text(dc, "+", 86, 36, 18);
        DrawFaceButton(dc, new Point(126, 36), "A", 17);
    }

    private static void DrawTurbo(DrawingContext dc)
    {
        DrawM(dc, 30, 36);
        Text(dc, "+", 64, 36, 17);

        DrawTopButton(dc, 84, "LT");
        DrawTopButton(dc, 108, "LB");
        DrawTopButton(dc, 132, "RB");
        DrawTopButton(dc, 156, "RT");

        var center = new Point(120, 48);
        DrawFaceButton(dc, new Point(center.X, center.Y - 12), "Y", 8);
        DrawFaceButton(dc, new Point(center.X - 16, center.Y), "X", 8);
        DrawFaceButton(dc, new Point(center.X + 16, center.Y), "B", 8);
        DrawFaceButton(dc, new Point(center.X, center.Y + 12), "A", 8);

    }

    private static void DrawTopButton(DrawingContext dc, double x, string label)
    {
        Geometry shape;
        if (label is "LT" or "RT")
        {
            shape = Geometry.Parse(
                $"M {x - 9},22 L {x + 9},22 Q {x + 11},22 {x + 11},20 " +
                $"C {x + 11},15 {x + 10},10 {x + 7},7 Q {x},3 {x - 7},7 " +
                $"C {x - 10},10 {x - 11},15 {x - 11},20 Q {x - 11},22 {x - 9},22 Z");
        }
        else
        {
            shape = Geometry.Parse(
                $"M {x - 9},8 L {x + 9},8 Q {x + 11},8 {x + 11},10 " +
                $"C {x + 11},16 {x + 9},20 {x + 6},21 " +
                $"C {x + 3},22 {x - 3},22 {x - 6},21 " +
                $"C {x - 9},20 {x - 11},16 {x - 11},10 Q {x - 11},8 {x - 9},8 Z");
        }
        dc.DrawGeometry(Surface, Outline, shape);
        Text(dc, label, x, 15, 7.5);
    }

    private static void DrawM(DrawingContext dc, double x, double y)
    {
        dc.DrawRoundedRectangle(Surface, Outline, new Rect(x - 20, y - 8.5, 40, 17), 6, 6);
        Text(dc, "M", x, y, 10);
    }

    private static void DrawFaceButton(DrawingContext dc, Point center, string label, double radius)
    {
        dc.DrawEllipse(Surface, Outline, center, radius, radius);
        Text(dc, label, center.X, center.Y, radius < 15 ? 9 : 15);
    }

    private static void DrawStick(DrawingContext dc, Point center, string label)
    {
        dc.DrawEllipse(Surface, Outline, center, 24, 24);
        dc.DrawEllipse(Surface, Outline, center, 15, 15);
        Text(dc, label, center.X, center.Y, 10);
    }

    private static void DrawDpad(DrawingContext dc, Point center)
    {
        var x = center.X;
        var y = center.Y;
        var shape = Geometry.Parse(
            $"M {x - 7},{y - 27} L {x + 7},{y - 27} Q {x + 11},{y - 27} {x + 11},{y - 23} " +
            $"L {x + 11},{y - 11} L {x + 23},{y - 11} Q {x + 27},{y - 11} {x + 27},{y - 7} " +
            $"L {x + 27},{y + 7} Q {x + 27},{y + 11} {x + 23},{y + 11} L {x + 11},{y + 11} " +
            $"L {x + 11},{y + 23} Q {x + 11},{y + 27} {x + 7},{y + 27} L {x - 7},{y + 27} " +
            $"Q {x - 11},{y + 27} {x - 11},{y + 23} L {x - 11},{y + 11} L {x - 23},{y + 11} " +
            $"Q {x - 27},{y + 11} {x - 27},{y + 7} L {x - 27},{y - 7} Q {x - 27},{y - 11} {x - 23},{y - 11} " +
            $"L {x - 11},{y - 11} L {x - 11},{y - 23} Q {x - 11},{y - 27} {x - 7},{y - 27} Z");
        dc.DrawGeometry(Surface, Outline, shape);
        dc.DrawGeometry(Cyan, null, Geometry.Parse($"M {x},{y - 21} L {x - 3},{y - 16} L {x + 3},{y - 16} Z"));
        dc.DrawGeometry(Cyan, null, Geometry.Parse($"M {x},{y + 21} L {x - 3},{y + 16} L {x + 3},{y + 16} Z"));
    }

    private static void DrawSystemButton(DrawingContext dc, Point center, double angle, bool menu)
    {
        dc.PushTransform(new RotateTransform(angle, center.X, center.Y));
        dc.DrawRoundedRectangle(Surface, Outline, new Rect(center.X - 20, center.Y - 8.5, 40, 17), 6, 6);
        var iconPen = new Pen(Cyan, 1.15) { LineJoin = PenLineJoin.Round };
        if (menu)
        {
            dc.DrawLine(iconPen, new Point(center.X - 6, center.Y - 4), new Point(center.X + 6, center.Y - 4));
            dc.DrawLine(iconPen, new Point(center.X - 6, center.Y), new Point(center.X + 6, center.Y));
            dc.DrawLine(iconPen, new Point(center.X - 6, center.Y + 4), new Point(center.X + 6, center.Y + 4));
        }
        else
        {
            dc.DrawRectangle(null, iconPen, new Rect(center.X - 6, center.Y - 4, 7, 6));
            dc.DrawRectangle(null, iconPen, new Rect(center.X - 1, center.Y - 1, 7, 6));
        }
        dc.Pop();
    }

    private static void Text(DrawingContext dc, string text, double x, double y, double size)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"),
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Cyan, 1);
        dc.DrawText(formatted, new Point(x - formatted.Width / 2, y - formatted.Height / 2));
    }

}
