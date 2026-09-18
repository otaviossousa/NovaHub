using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace NovaLite.Desktop;

public sealed class ProgressButton : Button
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ProgressButton), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRoundedRectangle(ThemeManager.Brush(this, "ButtonBrush", "#2B414F"), null, rect, 6, 6);
        dc.PushClip(new RectangleGeometry(rect, 6, 6));
        dc.DrawRectangle(ThemeManager.Brush(this, "ActiveSurfaceBrush", "#367765"), null, new Rect(0, 0, ActualWidth * Math.Clamp(Progress, 0, 1), ActualHeight));
        dc.Pop();
    }
}
