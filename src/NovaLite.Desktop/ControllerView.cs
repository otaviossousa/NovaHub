using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NovaLite.Core;

namespace NovaLite.Desktop;

public sealed class ControllerView : FrameworkElement
{
    private Gamepad? _input;
    private readonly Queue<Point> _left = new(), _right = new();
    private Brush Cyan => ThemeManager.Brush(this, "ControllerAccentBrush", "#A6E7F2");
    private Brush Active => ThemeManager.Brush(this, "ActiveBrush", "#76DABD");
    private Brush Surface => ThemeManager.Brush(this, "ControllerSurfaceBrush", "#0B1B26");
    private Brush Body => ThemeManager.Brush(this, "ControllerBodyBrush", "#0F2634");
    private Brush ActiveInk => ThemeManager.Brush(this, "ActiveInkBrush", "#07191F");
    private Brush Detail => ThemeManager.Brush(this, "ControllerDetailBrush", "#375362");
    private static readonly ImageSource HomeLogo = LoadHomeLogo();
    private static readonly ImageSource BrandNameLogo = LoadBrandNameLogo();

    public void SetInput(Gamepad? input)
    {
        _input = input;
        if (input is { } pad)
        {
            Add(_left, new Point(Normalize.Axis(pad.LX), Normalize.Axis(pad.LY)));
            Add(_right, new Point(Normalize.Axis(pad.RX), Normalize.Axis(pad.RY)));
        }
        else
        {
            _left.Clear();
            _right.Clear();
        }
        InvalidateVisual();
    }

    private static void Add(Queue<Point> points, Point point)
    {
        points.Enqueue(point);
        while (points.Count > 90) points.Dequeue();
    }

    public void ClearTrails()
    {
        _left.Clear();
        _right.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        const double designWidth = 560;
        const double designHeight = 390;
        var scale = Math.Min(ActualWidth / designWidth, ActualHeight / designHeight);
        dc.PushTransform(new TranslateTransform((ActualWidth - designWidth * scale) / 2,
            (ActualHeight - designHeight * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new ScaleTransform(1, .95, designWidth / 2, designHeight / 2));

        var fine = new Pen(Cyan, 1.55) { LineJoin = PenLineJoin.Round };
        var bodyOutline = new Pen(Cyan, 2.35) { LineJoin = PenLineJoin.Round };
        var body = Geometry.Parse(
            "M 157,69 C 112,68 90,93 75,135 C 52,188 22,294 37,340 " +
            "C 43,372 61,396 78,386 C 101,373 126,338 149,320 " +
            "C 165,309 185,306 210,306 L 350,306 " +
            "C 375,306 395,309 411,320 C 434,338 459,373 482,386 " +
            "C 499,396 517,372 523,340 C 538,294 508,188 485,135 " +
            "C 470,93 448,68 403,69 Z");
        dc.DrawGeometry(Body, bodyOutline, body);

        var centerPanel = Geometry.Parse(
            "M 185,69 L 215,138 Q 220,160 240,161 L 320,161 " +
            "Q 340,160 345,138 L 375,69");
        dc.DrawGeometry(null, new Pen(Detail, 1.35) { LineJoin = PenLineJoin.Round }, centerPanel);
        DrawTintedImage(dc, BrandNameLogo, new Rect(230, 166, 100, 28.5), Cyan);

        // Mantém o conjunto gatilho/bumper unido e cria uma pequena folga até o corpo.
        dc.PushTransform(new TranslateTransform(0, -4));
        Trigger(dc, 154, _input?.LT, "LT");
        Trigger(dc, 406, _input?.RT, "RT");
        Shoulder(dc, 154, Buttons.LB, "LB");
        Shoulder(dc, 406, Buttons.RB, "RB");
        dc.Pop();

        Stick(dc, new Point(135, 168),
            _input is { } l ? new Point(Normalize.Axis(l.LX), Normalize.Axis(l.LY)) : null,
            Buttons.LS, "LS");
        Stick(dc, new Point(351, 250),
            _input is { } r ? new Point(Normalize.Axis(r.RX), Normalize.Axis(r.RY)) : null,
            Buttons.RS, "RS");

        dc.PushTransform(new ScaleTransform(.74, .74, 207, 250));
        DrawDpad(dc, new Point(207, 250), fine);
        dc.Pop();
        DrawFaceButton(dc, "Y", Buttons.Y, 426, 141);
        DrawFaceButton(dc, "X", Buttons.X, 397, 170);
        DrawFaceButton(dc, "B", Buttons.B, 455, 170);
        DrawFaceButton(dc, "A", Buttons.A, 426, 199);

        DrawSystemButton(dc, new Point(220, 109), 65, Buttons.View, SystemIcon.View);
        DrawSystemButton(dc, new Point(340, 109), -65, Buttons.Menu, SystemIcon.Menu);
        dc.DrawEllipse(Surface, fine, new Point(280, 101), 20, 20);
        dc.DrawImage(HomeLogo, new Rect(266, 87, 28, 28));
        dc.DrawRoundedRectangle(Surface, fine, new Rect(260, 135.5, 40, 17), 6, 6);
        dc.DrawEllipse(Cyan, null, new Point(280, 144), 3, 3);
        dc.DrawRoundedRectangle(Surface, fine, new Rect(260, 277, 40, 17), 6, 6);
        Text(dc, "M", 280, 285.5, Cyan, 10);

        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private void Trigger(DrawingContext dc, double x, byte? value, string label)
    {
        var amount = Math.Clamp((value ?? 0) / 255d, 0, 1);
        var trigger = Geometry.Parse(
            $"M {x - 20},8 C {x - 20},-2 {x - 11},-9 {x},-9 " +
            $"C {x + 11},-9 {x + 20},-2 {x + 20},8 " +
            $"C {x + 22},16 {x + 23},28 {x + 22},36 " +
            $"Q {x + 22},40 {x + 18},40 L {x - 18},40 " +
            $"Q {x - 22},40 {x - 22},36 " +
            $"C {x - 23},28 {x - 22},16 {x - 20},8 Z");
        dc.DrawGeometry(Surface, null, trigger);
        if (amount > 0)
        {
            dc.PushClip(trigger);
            dc.DrawRectangle(Active, null,
                new Rect(x - 24, 40 - 49 * amount, 48, 49 * amount));
            dc.Pop();
        }
        dc.DrawGeometry(null, new Pen(Cyan, 1.55) { LineJoin = PenLineJoin.Round }, trigger);
        Text(dc, label, x, 19.5, amount >= .55 ? ActiveInk : Cyan, 14);
    }

    private void Shoulder(DrawingContext dc, double x, Buttons button, string label)
    {
        var pressed = IsPressed(button);
        var shoulder = Geometry.Parse(
            $"M {x - 19},46 L {x + 19},46 Q {x + 23},46 {x + 23},50 " +
            $"C {x + 23},58 {x + 20},64 {x + 15},66 " +
            $"C {x + 7},68 {x - 7},68 {x - 15},66 " +
            $"C {x - 20},64 {x - 23},58 {x - 23},50 " +
            $"Q {x - 23},46 {x - 19},46 Z");
        dc.DrawGeometry(pressed ? Active : Surface,
            new Pen(Cyan, 1.45) { LineJoin = PenLineJoin.Round }, shoulder);
        Text(dc, label, x, 55, pressed ? ActiveInk : Cyan, 10.5);
    }

    private void DrawFaceButton(DrawingContext dc, string label, Buttons button, double x, double y)
    {
        var pressed = IsPressed(button);
        dc.DrawEllipse(pressed ? Active : Surface, new Pen(Cyan, 1.45), new Point(x, y), 17, 17);
        Text(dc, label, x, y, pressed ? ActiveInk : Cyan, 15);
    }

    private void Stick(DrawingContext dc, Point center, Point? position, Buttons click, string label)
    {
        var pen = new Pen(Cyan, 1.45);
        dc.DrawEllipse(Surface, pen, center, 38, 38);
        var cap = position is { } p
            ? new Point(center.X + p.X * 14, center.Y - p.Y * 14)
            : center;
        var pressed = IsPressed(click);
        dc.DrawEllipse(pressed ? Active : Surface, pen, cap, 23, 23);
        Text(dc, label, cap.X, cap.Y, pressed ? ActiveInk : Cyan, 13);
    }

    private void DrawDpad(DrawingContext dc, Point center, Pen outline)
    {
        var x = center.X;
        var y = center.Y;
        var shape = Geometry.Parse(
            $"M {x - 12},{y - 45} L {x + 12},{y - 45} Q {x + 18},{y - 45} {x + 18},{y - 38} " +
            $"L {x + 18},{y - 18} L {x + 38},{y - 18} Q {x + 45},{y - 18} {x + 45},{y - 11} " +
            $"L {x + 45},{y + 11} Q {x + 45},{y + 18} {x + 38},{y + 18} L {x + 18},{y + 18} " +
            $"L {x + 18},{y + 38} Q {x + 18},{y + 45} {x + 11},{y + 45} L {x - 11},{y + 45} " +
            $"Q {x - 18},{y + 45} {x - 18},{y + 38} L {x - 18},{y + 18} L {x - 38},{y + 18} " +
            $"Q {x - 45},{y + 18} {x - 45},{y + 11} L {x - 45},{y - 11} Q {x - 45},{y - 18} {x - 38},{y - 18} " +
            $"L {x - 18},{y - 18} L {x - 18},{y - 38} Q {x - 18},{y - 45} {x - 12},{y - 45} Z");
        dc.DrawGeometry(Surface, outline, shape);
        Direction(dc, Buttons.Up, Geometry.Parse($"M {x - 17},{y - 18} L {x + 17},{y - 18} L {x + 17},{y - 38} Q {x + 17},{y - 44} {x + 11},{y - 44} L {x - 11},{y - 44} Q {x - 17},{y - 44} {x - 17},{y - 38} Z"));
        Direction(dc, Buttons.Down, Geometry.Parse($"M {x - 17},{y + 18} L {x + 17},{y + 18} L {x + 17},{y + 38} Q {x + 17},{y + 44} {x + 11},{y + 44} L {x - 11},{y + 44} Q {x - 17},{y + 44} {x - 17},{y + 38} Z"));
        Direction(dc, Buttons.Left, Geometry.Parse($"M {x - 18},{y - 17} L {x - 38},{y - 17} Q {x - 44},{y - 17} {x - 44},{y - 11} L {x - 44},{y + 11} Q {x - 44},{y + 17} {x - 38},{y + 17} L {x - 18},{y + 17} Z"));
        Direction(dc, Buttons.Right, Geometry.Parse($"M {x + 18},{y - 17} L {x + 38},{y - 17} Q {x + 44},{y - 17} {x + 44},{y - 11} L {x + 44},{y + 11} Q {x + 44},{y + 17} {x + 38},{y + 17} L {x + 18},{y + 17} Z"));

        DpadArrow(dc, Buttons.Up,
            Geometry.Parse($"M {x},{y - 34} L {x - 4},{y - 28} L {x + 4},{y - 28} Z"));
        DpadArrow(dc, Buttons.Down,
            Geometry.Parse($"M {x},{y + 34} L {x - 4},{y + 28} L {x + 4},{y + 28} Z"));
        DpadArrow(dc, Buttons.Left,
            Geometry.Parse($"M {x - 34},{y} L {x - 28},{y - 4} L {x - 28},{y + 4} Z"));
        DpadArrow(dc, Buttons.Right,
            Geometry.Parse($"M {x + 34},{y} L {x + 28},{y - 4} L {x + 28},{y + 4} Z"));
    }

    private void Direction(DrawingContext dc, Buttons button, Geometry geometry)
    {
        if (IsPressed(button)) dc.DrawGeometry(Active, null, geometry);
    }

    private void DpadArrow(DrawingContext dc, Buttons button, Geometry geometry)
    {
        dc.DrawGeometry(IsPressed(button) ? ActiveInk : Cyan, null, geometry);
    }

    private void DrawSystemButton(DrawingContext dc, Point center, double angle, Buttons button, SystemIcon icon)
    {
        var pressed = IsPressed(button);
        var ink = pressed ? ActiveInk : Cyan;
        dc.PushTransform(new RotateTransform(angle, center.X, center.Y));
        dc.DrawRoundedRectangle(pressed ? Active : Surface, new Pen(Cyan, 1.45),
            new Rect(center.X - 20, center.Y - 8.5, 40, 17), 6, 6);
        var iconPen = new Pen(ink, 1.15) { LineJoin = PenLineJoin.Round };
        if (icon == SystemIcon.View)
        {
            dc.DrawRectangle(null, iconPen, new Rect(center.X - 6, center.Y - 4, 7, 6));
            dc.DrawRectangle(null, iconPen, new Rect(center.X - 1, center.Y - 1, 7, 6));
        }
        else
        {
            dc.DrawLine(iconPen, new Point(center.X - 6, center.Y - 4), new Point(center.X + 6, center.Y - 4));
            dc.DrawLine(iconPen, new Point(center.X - 6, center.Y), new Point(center.X + 6, center.Y));
            dc.DrawLine(iconPen, new Point(center.X - 6, center.Y + 4), new Point(center.X + 6, center.Y + 4));
        }
        dc.Pop();
    }

    private bool IsPressed(Buttons button) => _input is { } input && input.Buttons.HasFlag(button);

    private void Text(DrawingContext dc, string text, double x, double y, Brush brush, double size)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"),
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(x - formatted.Width / 2, y - formatted.Height / 2));
    }

    private static void DrawTintedImage(DrawingContext dc, ImageSource image, Rect rect, Brush color)
    {
        var mask = new ImageBrush(image)
        {
            Stretch = Stretch.Fill,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, image.Width, image.Height),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = rect
        };
        dc.PushOpacityMask(mask);
        dc.DrawRectangle(color, null, rect);
        dc.Pop();
    }

    private static ImageSource LoadHomeLogo()
    {
        const string png = "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";
        using var stream = new MemoryStream(Convert.FromBase64String(png));
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var image = decoder.Frames[0];
        image.Freeze();
        return image;
    }

    private static ImageSource LoadBrandNameLogo()
    {
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAZsAAAB1CAYAAACYoUy+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAACcGSURBVHhe7d17fFxlnT/wz+c5M9Ok7WRyT5OmmaYXqAVlsYjVRVBBcF0pLDcvyyoCslpc1tu6urpbKqD+FF0BQcDFRRGQi6AuUlBx7c/LTy5Fhd1Aaeg1zaXppLm0uc2c5/v7Y5I2OZkzc87pTJJJn/c/ec2cZDIz5znP83zP93yfAxiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGYRiGkR90PhFEVcPS40eGRUUiwOgocORnBKOjoxMeO3/Oju2hCAd7O3fuAaCdn62Q6uvXzO/oaAfQMQRAnNunV/38+voGdHRsmQXvpZByf86KhuVLkiOpBZFwhF7az9Tni297T+fgHqD7kNt3MlFDw6qqgZHh6nA4rLy+/mzb3tO56xUAtvOzHdFYWtUQasrcr2V+/VJrwZ7u7hbX77ChYVXVgH3ozYrhMGwblmXBtm3AsoAMP2fjdkmO7mHI6u7r2rXbb38ZaLCJNjRU0Q6/Q4DVCmgCsCjoa80GIni0v3rBd9HSMurcVkixmvgFSjF8oGvnIwCSzu3TqaJu2YkaqffKkP5Gf3/bAbcDprjVz4/VRT4r1D/t71z7R+ChjJ1NtCZ+nVJcA4jl3DYXCTAUTun1icSeDrf9XlEbf5NNWQNRawCUkbKgaI95YU8Jy67s6nrhkHPTuIq65SfYSN1AoNS5zY0WbBjYt+tZt0EsVh0/GRZvFcgCjn3RRfdTcADAQSHaBfh9JKV/nq3dTETnE9nU16+ZfzC1/xJlqb8SrU8gWQ+gwu/rzCYikgjDek9i3/ZfuzWSQohGG6rU/Mh/AWApQ+s6O1v3e9lhhbKwZslplrLuF+DhUDh8Y0/btvaZfD/5Vz8/VlfyWYG+SkFd0du140kAKedvAUCsduljAjmHZMi5bS4i8WMZ0pf39e0+4NxWXrP0JLHkfaLxJhBxAksAKufvFRMC3+jtSn0BaBtybhu3sGbJaUpZjxOIOre5Ibiut2vHJrd2VVG/7DRb248T9Pyas5WIJEnsEOAly8IPD7TvegRA1sm650ZTUbF8yaFU93VK4bMQuYjkCQAqi3mgAQCSD0hKuc5GCoUl4feI4GRA1gzayYuxenXY+TvTKRwKQ0QqAFxmj45+PVrdvNJP+5jNKisXN5bVzPuiQF9FsBawCZzh/LXDBAKyqJu1H0Paxrf7+poHnBuiNUs/IEq+KoKPkHwLwXixDzQAtinqu4C2EeeGicIhBJhrpXDGGe7tCqlksXeXh5EMAzyO4Hk6hS+ULYpfF402VGX7gJ4aTkXD8iV2OHUDFK8AeJzXvysCPUmdvLPnH/72oHNDITU0rKoCcRWJCMAwFa4p7xmsFxHXHVVoyVQSBEGgHOQFpP2V6obmFcW+r0srFzfa4dD1IK5IDzQgYAHY7PzVwwhCxG9HU5xE5D4rqp4GNk+ajUdrlnxAEZ8RwVkEY9k6kWIikP+MzuP2XPmGZCrdDnyxQti82b1dIRQG5mK7Ik+A5lVqfvhfo9EG1wAkZ0cSbWioslOp6wleOJcaHQCI1v/ZECt9GRs3Zm14+XYwNbiewKoj3z+P1yLvJ0+YsegmHApPnMeFqfjuUVu+HGuIv85LO5mNKisXN4bDoetFcBGJ8iNt1wayzECPmchG0MMQbj2wffukqCZW03QRaf0TiNeQRR/JHCGyTULWw7t27cp6ugeHjwefA4ONrO0KSM6h3nMyEuUCfpClkfVobCxxboeXToSp0HoqXgDKfOe2YiYi28Ih/kdra+u0Juaj1Q3HkeqDAkQmPi8aV8bqR070sk8KIR3ZTMQwgXPFxteqGpa9DUBR5S8ORzSCi0gumLTRsoAsM9BjJrIhHuxT9ssTZ/nR6sUrodRnAHnNTLXFQhHFrw/sLdmVK6rBhEjftyztCpixueS0IFAOyN9XpKwzMvUXWRtTtLr5OFBdBsGCOTckE99NdCzIGU7nm1LhyyGyhI6ps1JcBtu+Mh6PTxqEpovLTC5M8KyUra+vqanJOFuZjaK18bWRUOgbIrh4ykADpC/jPMZzNgLshS13ou2KSbkLZYWuhsjrSM6tK/FEnk6l9E+BFk+Ty3Ao3Q78SWWNbJJzKGfjhuRirXlNVdXxU67iyzrYqJC+HCKNc/Ab2gatH4L8r6eGly/li5eepAUXg3Sb4lzYe0hOwIYNWfdLIeSYya0dwYK/POOMM6bMVmabaG18LcnrBPgbki7RuMnZQPCd8gV4CThyCrl68bKVIjgf5IxMeApJQ761tHZhwmvWP2jOJltkE56rOZupTk9heI2zv3Dt1BobG0shuDB91cHcogW39tdE94Cc1j2vk/IJpbDEdfAmaiWkPoa77prn3FRoLpHNYVTY8MeWnR/G6tWztiOK1sbXKuKLhLw1+2XLx3jORmSbhNV9ztxFMmmvO3IRxRwi2FQK6xctLd6iGng4HjLKlbNJzd2czSTEArHUX29+5ZVJfYXrYNM/hBMhbJhrDU+0PB0Oh34EHw0vH6K18bUE/grI1gkC0Div0g69ZWz6PW1yRDYA8CYofrqs+9CVq2fhgBNdFH8XyesgeFvO7/gYz9nQUncP7N0+pQJciNMFMqcmlyIYUkpu3rdvh+eoBkdzyitLu0JoTn21WQn02ypKSyd9YNfBBmFrjWTbXqyobunZu7jTT8M7Wo2NjaWKvMZLASyJCtuWa+rr66c1uvEykyOwDIqfaus+dOVsinCiNUv/Tmn8GyFvQ9aIZsyxnLMReVqnkg9mXrGCJ861XA2V/EIPydNuhZZuTJ3N0SG4yt4/OqktuQ8mggrmmOoWGyF+bXPwF8DmaS3gHLCtc0TkLE8dIQCAbx2yQ+9wnvMsJA+RDTBhwIkmDm6INTXlHDwLLVrbdKkiPgPgDYDXjvLYzdlokVtXNtW5XZE14/szz3rEtm/7i7+YWrCaS9CczTFZZ5MBiQWycOGkL9D12yyvj39ObGwA6XmGLYIhEI8DeidIDT02nI3/HOd8frq22/xlf3f8V84CtkJqbGwsHUhaPwH4dn+nxuQ39qHRCw4e7PAV/gdVUb/sNNvWT5CYevVWBgIcgNbf5Shu6Ovb3Tsd79EpWtt0KcF/Jrk668TJgdDn9q5qfgKbM7eDstr4YyTPyXT5ZiYC2UvgYeHYch3Odue1fRZ4O20M2aF5txzseCVjmyqrXdpDosL5fDYCeZEavxelBkCdfk2X/3+Y8/lCbRfZE06V3p1IbPU92ARZWobgut5VTZvc2lVF/ZLTbK18viYe1dDbQerDTzo/t9vnd/70u124ApAzx96v61jhRluj1QPt7Ynxx64vUFYX/xyBDYD3wYbgjaL1vaGSkn1DQ0Myfz4wOAjMnz8fg4ODmOnHDeWRntbW1qzLVORbrDZ+Psi7AYll+bqnEJFRpfjh3s6d92c+5ZFfC2uWnGbRegIeBxuMDzgid5WXlH1p9+4Xp23AaWxcW9o3vPcqpawPg/Iav0uo0OK5ve07nnA7tRJgbbRnRafeHylVh7K1v5l+bEXmjw60v9zjtp/K6pb2MB3deNWqBZ8qRfh/DuqRIef/m+nHpVWlBxNbtx50+7zZjK0V+DhmeG00alzGsPWrkdFkyvn5Cv04HGEdkvIOKHUtAJcrO91pq6R6oP3l3INNeV38c1qwgT4iG20njxvYv7c1yM6di8oaGyuZDP0EwJv8RTVjRJ62rXnvdpuJ5lM6srGfyFiXkkV6wMGjotVXBvZvf3VsXlQwjY2Npf2joU8DcgXJJY45mSeEPre3q/kJtwjXd2Qj8rt5suCd3d0t07rsUb75jWxE5HsLrNH1HR0dg85txS5YFGKv6121zDWyCbS4p+C83n1Vm4AtBZ9wZhJraqrAMO8HcWbOC28c9OBo9cDAkcjG9UDVgO8kabi8tKvQnWJRSYUuFsEpgQYaACBPZnLk0tXTsEin15yNE4EKEpcqZd9QvXjZ8mxt6mg1Nq4t7R9VnybxUZJNwf9XfnM2JNGNbufTxcfn7rcs9duOjo6MHWuxC5qzyXY1WpA6Gxs21qxxPjt9+nbvPgCqZ0Toez9Ho2WTHrserErB9xeDHucTx67KypWLofGR9GKbgUWUxY+19wzW++8K/PFyNVoWERDnJZP29dHqwgw46Yim/dOk+ujR3z8pv3U2IoIa1DifLj4+d79odAFnFDSSnSmBjocC1NlYsLBlyxbn09NKIIfou3UAA+if9Ni1U9A6wOFc6Xzi2JUKjVxJ5mV9qeUieE+hF1YKGtkcwQjI82nZN1YtXn5G4Ggug8rGlYv7k9aXSbX+6Aea/NfZHKuRTZr791jMZkudTTqymcHQBoCl0hMw/7xGNgECGxPZpJXVLF9B8lIgX8t+yIdjDfETsu2voxVoJjdVhOC6VMr+YqyuKS8DTmXlysWp5Oi1EF6Zl4EG+a+zOVYjG1AqV69eXbA2OZNmS51NOrJxPju9UlpKA01EB7xGNmMzNl9MZAMAoLI/JIIm3y3LFVfoFC4v5CKdRx/ZTHKaCDfGFi87qgGnsnHl4lR49FoK3+f1kmxvTM4mI7+7X3BSS0t/4P07mwXN2eS7zmamczZAY6kSrhGIr4sDAAAmZ1NYlXXNpwLynnyvKafIi3qTyMdpuYwc97M5aiRPk5S9MVa79HLA/2oD5bXNr02Njm6E8H1+Lsf2xuRsMvLZAARybnld+CyvV+0Vk0CRfq6cTYD72cxkzqassbGyrNb6JCBv9FEGMMHkyMb1oweps7GSoVhPT+uk/1BW13gqGfo7bUtYqfGCIQWtdXpAmxWP+Wpf186bct1D24uymvg9VCxIjkWIu/pDqX9Am/u904MKUmfjhRZpJXhj/76d/+n1+62saz41pfXnqXhWkOv7cylAnU2niH6cZDJz+5o9j0nef9Lx8d9tznB5boA6G0DkGZIv2FrsTP9v2h6LfUApldC2JEIha9uBjr4/A4lANTaYRXU2EDxBco+ttc76+QvymAsFOJNAXbaxwk1B62ysZH+sp6dn0mBTvXjZymTKfgzgymz/byaJyO9KOXxOV1fXIec2P8rqmk8l9E9RqJVzRXoA65K+fdt/PTaPypugdTbeSCuEX+vbt/PuXANOZV3zqTbkWgDvKNSMOd91NsVCBAeg1Nn9ndv/mKn9+K2zmU0EOAjBACkDIugg8TIET1qp8FM9Pa0Dfged2VJnA8FzoPyF3xqX2aCgdTaonJq02b93+zaQ3xGRrJ3MTCKJLueTvjWWKshnRFBVkIEGAMhKoX1Nff0azxMAr/Kcs3HgCg35p7K6+CdjsfiE2zNPVrWo+Q0psQs60KTlN2dTLJTCj/o7h1oyDTQAQGLYb6c8WxBYSKIe4HEkzxDBh0j+q20lv1q+uPm1fnOHQXM22a5GC1JnoyH3A+z0/YczTEQOqIoqPfE518FGqfRUyBeXnE0ype8FMen2s7OJiKDO+aRP5bWhd2jgTB+nXgLi2wel+6+R50U6A52j9kGRKwD+I+bxn2OxpikDTqw2fn5K9HWkKvBAg7znbIrEq0J9O9DhulyTCF6arceoXyQjApwMhQ9JUr4Sq4uf7qddBToecuVsAtXZsBWCeyB03W+zE/9sHRyaNKlxHWzyWWczuH93B225NdcplJlytJFNY+PaUht6va/wOCACCwH18YVbXpnSYR+NwkY2aQQWAXIVStVnJg44ZXXL3quBfwE4PcnmPNfZFAMR/f3yCP/XLaoBAFB+IVL4dfimFyMg/koEGytq42/wGuHMpjqbyLzQXRriGpHOSlp+GY0mJ/X3roNNkMDGLbIBAB1JPSKC/zcbv7CjjWz6k53nklzrtSGPE5H/AeQV37NJkVNC88N5zSkEmskFQVZC4yop4YaymiXLymrj7wfszypyjd/vL7A819kUgVclpB5w3pnTSYl6nJD+Yj2Vlg3Jt9jAv1Q0LPd0Q8jZVGfTvSe2m+TdIiiK6EYEB0j8zNneXAebYDkb5xNHDLS3J2jJLbMxd3M0kU1ZWWMlgY9TsNC5LRdavFlr+Y7/iI8RTa6vaFienyLHaYpsDiMqIbhCKetrAD5P8LXZ2mL+HXM5mzsG2kM7c01qGqpLXxbi525XUxU7kmenksn3rlixIuel+EFzNoWqs0nZ+mESfxSRWTdZd1IKj/ap4W3O9uZ6gOczZzMuNFryS5CPAzKrGnM6sgkW27AkdKEITobfOxwKnkum5L8iWu4LEvEROCWVSl2CPC3S6b/ORn4qEN9X+YwjuVCAv/F7L5opBM/B9wF4DOVsBM8B8hDQmnNC09LSMkol/y6Q7c6OYo6IKMUPdw8kc641GCjSz5WzOYo6m8H9uztA3gQy72UP+STAdg3ejq6uYec214M8UM4mh0Ri64DW9jcB9gXsowqCJLq6/Mc2VVXHNwgkyGKbw1rsb72mqTqRSOxpp6VuERGfITIjivz7io4Di0TkqPdUOrLxTmv9I0XeKIK+oAPOUaM8IKK/BzJnRzrJMZSz0SK3La5a0O51H/V17H5eifqiiBzw+jfFhSsJ9W4g+zEbONLP0q6Cld4dWRstYpduEi1/mMWRZ4e29ZdrF1ovZJo8uw42QQKbnKENgKaa6DPA7ArVRQR1AQKbpDV0OQn/a5ZRntIMPbFly5YUAIRGw78E+Af/M3SstMPzPsCVK7MeOF74nclZiomk5h0AvyaY3gFHBMMQeQA2vqIs9aJkaNhZHSM5GxH57bxSeaKlpcVX0r93X+nDIL4ukB1zMcIR6HU1NdnPCIRD6XbgTyprZBPsooMja6N1d7ccVMStEJm2GxV6JUCHaLl+npQ+4HaDStdOMljOJkvSZkxLS8uoUuo2Ecyae98EydmU1SxZTqoPQPxNV0T0sIJ126F9Ow7fEC2R2DoAka+B7HP+fm5yZWwwedTL2PidydkATnlNU0KGRu5QwhtFJGdOIB9EMEwlNxGpL318/WUvpGzb9v6ux839nE36e7L+ff/u3fv9H2ctozKYvFNRbdAiWzLNUosb14yOHsp6cU3QnE22yCZInY1zbbQ+++BTIJ8UEV8TiEIi8EvR9ufCuuSebLfgdu2gCpGzGXego+QZRT6EWfKFBbkajUp9ECJNoM9bEiv1lB6yf++M7BprFvwKwJPpE7vekYxLCh862kU6fUc2sLB582YMDLQn7KGR25VwAwo8ExbBMCA3W6P2Lb379r64ceNG7fd9p839nA2JX8ng6K/9tqdxAwPtid7O0gcp/IJAf1OA5+fQoFMpUTvrcRuoXeXI2VBSPaC/42PK2miJxIDS+tsEZjq6GUZ6InIDtb1xYPWye7MNNMgW0wVaGy0aivW0Tl4bzU20quF4KxT5sghKQACi07eSl/S7EtHpfnzssc/tp4hINb33GL/rw9A58LhcTdWiZacktX0fSV83ChPgACEXnn36qf/3oYcemnLgVtQvO03b9o9BVjm35dCR1PKuwfWXvYCNG3015nF+10bT0O8eWNX85ISlOSLltc2XCPW1AJv9fC9eiOhhKN6cHLVvHkzsaScpOLKG1SbA+9WA+V4bbWy/Pgcw5bF9Fn67Tt108urlT2VaA82v6qZV9aNDQyeR6gShXkJyhWitsv7/XO8vD9tBNlBwou+LcwDIUKqqv7/NdXocZGmZXGujVVUdH01aw/eRfKfXsgUt9nnHLandtGXLkdtCr169OtK2/9D1VFwNDeX2/bh/f2wQ0SfS//c2LCIPKqW6tK13ANyOkdSzfX17Dowfj9m4dsbB1kabuhBnNmWLlpwSEsuaOPUKO6ZiQR6ngG8QeKPXug2/a6OV1cbvJvF+wN/KziL6u2UR/bE214U0V0fKFw3eLiKX+n1tQL7fF7Y/EnSRTr9roxH63b1du5+cfGCtjpTXDL5HFDYAyNuAMx7RJFP2zUM9bZOS3WOLG24i6H2wyfPaaAJ5gcA/anAoU3uciccL1ciLHR0dgxM25cHqSNWSgepkMrRYRBSy/P9peWxLlWXxTBG5Gj76KQCQcKqqv819sAm6NtpJq5ZtyjbAx+qa3g7hD0FWZ+t/x1FwXu++qk3AkcEGAGJ18aVCuyYkIeX6/bg9plRR43aSjV7ewzgRGVVQV4S0/dT+/bs7Jx6HXrj+o3Rkww0APO9EP5FNIcVql/5sbGbqabDxE9mkZ9LqYb+LbYrgQFipsxOd25/PdqppQW3ja0MM/QyA34YwwLB1ft/e7ZuDnO4IEtm8flXzkxkOrEhsUfM60foakm/2OuC7EZEOkDclU/Y9g/t3dzhnUIEiG/Dc3q78RTYAftenD70T3d0HnRuMwiqtWtIQCVnfEZGzfewvT5GN71WfLa7rbXePbMZEYouWfle0XEzmvrlipsgmH8pqmm6kUlcDKHFuy0rkwYgsuKK7u8V3W3edeRYyZ1No6UjRcz/to86msTSkrE8B/hfbJHB/Yp79P9kGGgA4tK/tRQF+BPjLZ5GMSkp/rL6+3vPkYCK/dTbjOZsMRvs6dzxMYoOI/C7IwDdOgA5QbginBm4bmnDqbKJA59bznLOZM/ezKUJDiT3tWvT9hM+1wybf12uKQO0qR85mzKiy7dvp8QrOKTmbPImErTtF4Ds6EfJdozy4NsjajK6DTSHqbKYLme4AvPJaZ1NeF367FrzV73LfAmxPIXU7dl3mqR4kbOF2EezKNTBlcNZgKvLOMwI0BL91NjbsrEtz9HXt+m8S1wYdcESkQ4E39KcOfT+RSLgmHv1eRQfkv86Gc+VOnUUqZIV2CsTfsZLj/EugdoVcdTZp9TXRZzTwIOBlNZUjdTb5tL99xysKuCt9itq79Ora6uqFW7ZW+B0hXAebIIHNbAltRAJENjkCm8bGxlIRez3p/XTNYYLvHyoreQXwlrxPtO/cCvIuv5c3EohC8RNbXvG/SKffmZwFuEU2h40POIA85mfViPGIxkr1fx9ZBhoEiMiA/NfZmMhmpvmvzM8d2aTbgT/Z62zGtbS0jCLEW7xFFkfqbPJtxLa/O7Yav6/JoIacGZofORvIXqvk5DrYFKrOZjpwrAPwykudTd+QulDIv/SaNB6ntewIh/BDtLb6GjiSKfsekltF/M3YROTUsB3xvUy/35lc+oxB7gOrr2vXf4eobhDhDxy5yoy0yPMgr88V0YzzG5Gl5bfOxkQ2M4t2qMTv5Cp3ZJNuB77kqLOZaKB951aS9+ZaXNNZZ5NPQ4k97SL4HnK8ByeCUU1+tK5u2Nek1nWwKeqcjaDCx2XPOetsYrGmCmWpa/xcmTLOIm9KtM/3XX8ylNjTDpG7CX9LsZCM2MT6ysUrfN3K1X9k45qzmSLRuePZsCX/RwQ/yJaLEsHzFrixXwa/lyuiGef3faeZnM1ckqK9iuL5YqC0nJFNgHblLWczgf6PXPf5KlTOZlxknv2QEH/yG90QeMOwpNb5WZvRdbAJkrOx5yXP9DujzreFNU0Xk1juZ72wnJFNifU3Au17ZWIBnh/V+kHI/7p2sNlYkciDAnnaf0PgqcmR5EWrfTQE/5FN9g7bKdGx6+WwJV8V4b2ZIhwt8rwCvti7b+fjXq4KHOf3fQMmZzOXzK9uqifUOoF4buuAl8gm6b8DhLeczbi+rt07ROR7gHveJB3ZFCi0AZDYs6fdUupbAvFZMsGIUF0TSxxq8NrXuv5SkDobiDwrxCMW8YJN2kilI0t77MSFPeExkD7FmdfttqwBcYnfJeuz1dnEapuWAXwA5Ov9vCYAaK3vs5R1v1aS9PT+M2yHwkUU+SDos+5G8LJKjp594EB7W+7zwvmqs8mtqj6+KmXzA0JZT7AMAEUwPtD8zO/rzYo6G0ELLfm8pDjk3H+59u9MbD/QUfUrZ92GU0VN05ttqGimv8/1+tO2PSkVSvFsTbnYz/4HZq7OxqlqyZKG5Ij1GImTMvUvbnU2+VRVdXw0GRp5lCJnwMfl4wAA4af6SvVt2LXLdcAc5zrYBKmzQbrj3k+FVyHpy1QFGhz/Djm128vndgFWEYhl+1wuXOtsojXxa5XCZ/2spIDxmTopAp30+v4zbheJkgy49pl8sa9r15eA3Odk81hnk1N9/XHVh+zhq0F+AsBzSnhz776dj/sdaDBL6mwEGKDIS1DUU/Zfrv07zdu11s8vtGr+qaNji2vBZ+WiFattPXojqMoFmn5ef1q3C6ICWeWjnu6wGayzmSJWF79MBN/KNNErVJ2N08Ka+AWW4h1+yzpEZGvYwvmJjl05bwLp+qLl9fHPiY0Nfitzi5EWeSgWsT/orOyP1TU1Q7gJ5Eo/nf1YxfsVIC4geJ7XGXK+iaBdCd7V273zxVwNIR3Z6CfocbAh+O7erh2+I5txC+uPq6Yefb+l+Mfejh2/x4QJqx+BI5tVzU9MWGpnEr+RTVERfrhvX+n3gRbXXGCsdumtoFwJ5C46LFa5I5tlp9na9hnZcF3vqqZNbu3KTVVVVTRpRX9MyunOsop0ZLNzU6ZTz/lUVXV8NBUauQOQC/3udxH5alnEvtbZfzq5dqBaYwfIrB3UXKHA1ra2tqmfleoqCbDsColfc0Q2KfIWiARYyTk/SDRo6A96WaTT71VduepscjnY8cr+umjojt6OHb8JOtBgluRsigWJXyW1/TPJkkOMVcdPBnG++FzNvMh09g9YU4/3CQK1K/jL2YxLJBIDWvRtADOciipszmZcIrF1gIrfDnJ/KpKXDgxbq3DxxVkjTNdONBS2fwORKaeV5iICf3B2eLH6pteLlvfRb64EGCZ4W19f80BvZ+gPIB7LdgVWoVGpS3qGedLYaW9Xfq++sTzU2eTidt8LP2ZDnU2RGIbGTYMnNHdnWokhLV4iilfD3yK2RUdEnq2JLMgafRSyziaTebr05wL986kRTOHqbJx6O6xnlOJjAVbjbxCLH8Bvf5v1LJjrYNPT1rYXZFHc8/poiJYd1rx5zzkHG7FxNcmGbKcaM9OPSon+TTr53DoCjW8CnMl79yxWwDVAY9boxu9MzvZYZ1NofiOyNCuvdTbFQTYvjMz/bbZTPBWL8FZA3uX7YpQioxQf7u52P42IaaizcUoktg4oqpvEcWO0QtbZTNU6Ajt5kwAdvvsrkUtiev4bs516dh1sAACa3/Fb51FsqHBPYo99+EZmABCtaXozqd6V7YvLRAS9Guqmvl27Dl9Y2de9608QeWQmoxuBnFu+KHRqtujGf2Tjvc6mkPy+77Tsl23PtchGBL0U3tbW1uJ6wW88Hi+xBR8lWeN/glU8BPjTyGjqqakRxGSB2lV6BuZ81rPeztDTBJ+CHFlto9B1Nk693Xv/DPJRb0vpHEGyQSR1dU3NateFPbMONlbMelIovwiaBC4CL+kU79uw4W8nNLx4iaXUJ0XE0xLgE5HywECX/NmZjLdU5HbRyHm1RqEQjGotHytrXO16pZ7/yCZ7hz1d/L5v4NjL2ZD8qYzo32Q7jg8M4q0ET/M7wSoywxR9W/Oisu5cM/fpqLOZqnWEGt8G2SljDbDQdTaZhBi+UwT+77xLnj0sh053W5sx62DT09raHwK/BGBXrp1TbATo1Fq+NrC0avvGCTccq1iEt2rgTK+XvY4TwU5qfhuYuthmT+e2lwh+TwRTtk0b4hwkB89as2ZNxs/ldyZn4WgPrPzw+76BYytnI0CvFn17X99u16gmFouXk1gPkRx19UVO5CfhEjzW0tKSNarBWM7Gf5eXOupTy9XloacBeYRI9xXpyMb5W4XV07ntJRB351pKx4lAlBY/sWXr3oyLdGYdbACgp2vn0wC/PLYKsd9vf1YSoFe03BG2wz/ChOvX4/F4ia1xNX3UbIwTkXuqY6GX3RbbHLWTPySxze9aZ/lCMErI+lde6SjL1BD8RgizK2fj/X2nHUM5Gy0PLrSq/+zMSU7EUp4n4Gm+C/qKiAA/Din19f27d+/z0o8Fzdkc7anl1tbWEYu4Q5BepHN6czZHJJP2PSB8r80IkdPDWp+F9H3aJsk52ABAX5f1A5LXC+B76ZTZJh2B4MthO/Kdnp7WSetvHTiE80H/pxJE8HIkhPtasyy2OdSzt020vnMmc2AiWMv5kTMz5W78XtVlcjaznwh2UnB7R8eWDJfUpsXi8XIt+Hsy12phRWsYgh+C6oZE547nvfZfgdrVUeZsxvV07nqJwA9FMDLdOZtxQz1teylyDzNeju0uvTaj/ZEFdcumLNLpabABWkf6unb8ABrXQuR2YOZm6EGJ6F4Cj4jof4tg/m09Pdv2TpzhxGLxcip8nAFOJSjyW4mOXdtzneOcb0UeEMizXht8vpGcR5FPVi5escjZEPxe1XW0dTb5EiiyOUZyNiTu7duPl7K1Sz2o1xHIeWl8MRLgTyL6S4rW9f2d2//o57gL1K6Qz1PLcicoW21qe7pzNuOsSOQBAZ7JluvLhOBaS9sXrlixYtIVsB4HGwDASH/3zietcPgrAnwB5DcB+TmAVgEm3Rd+NhCgVwQ7APwekB+A+IJouW6g+0P3ZrqlqY7wPAFfB9LPdwIB/mSlUo/muroFADo7W7tBfmtmczd8YzKVusC5WqvfmVw+6mzywW9EBhwbORsR7LSp7wN2uba1+TXxRUpZH/W7JNVsJSK7BHh+7E6314lgwzxZ+O8Hul5t8TPQYAbqbJz69u3eTsE9yrJGZiCwAQD0tG3bS8XbBOIrugEQAfkP+w+mJk1qAx9RCxcuqgmXLVhpj6aaaKEUWmpEKUKPDWEz/lP6FVVfEva+kpDak2jfuW1sa0ZlNY3nUIVeJxoq8+tl/ikpu+X1J/pYfK+ysiwWiV0htkQyvd50/FSKLb1dk9dwKq1c3BiJRC4RW8LO38/80374E+sv3zHx4oqZ4P99A/NKQg9372l1ve1DbFHzRbDtZlFKZfr7YvgpovcuqV74YEuLez3JokUrag7p5KUkI86/L8afyuJeO2X3WvPUnt69I9uAjqGgk+CxdnWxn+N03jzrR91tr+Y8w+FV5eIVjYODh9TwNVe1YaaOs8rKsvJQ2Ye0oMT5eXP9LCHv3rdvh6ccmWEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYhmEYfvx/MaBDdgIzxPYAAAAASUVORK5CYII=";
        using var stream = new MemoryStream(Convert.FromBase64String(png));
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var image = decoder.Frames[0];
        image.Freeze();
        return image;
    }

    private enum SystemIcon { View, Menu }
}
