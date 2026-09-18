namespace NovaLite.Core;

public readonly record struct StickPoint(double X, double Y);
public sealed class CircularitySession
{
    private readonly Queue<StickPoint> _left = new(), _right = new();
    public bool Active { get; private set; }
    public IReadOnlyCollection<StickPoint> Left => _left;
    public IReadOnlyCollection<StickPoint> Right => _right;
    public void SetActive(bool active)
    {
        if (active != Active) { _left.Clear(); _right.Clear(); }
        Active = active;
    }
    public void Observe(Gamepad pad)
    {
        if (!Active) return;
        _left.Enqueue(new(Normalize.Axis(pad.LX), Normalize.Axis(pad.LY)));
        _right.Enqueue(new(Normalize.Axis(pad.RX), Normalize.Axis(pad.RY)));
        while (_left.Count > 1800) _left.Dequeue();
        while (_right.Count > 1800) _right.Dequeue();
    }
}

public sealed record RotationCircularityResult(int CoveredSectors, double? AverageErrorPercent, IReadOnlyList<double?> Radii);

public sealed class RotationCircularitySession
{
    private readonly RotationTracker _left = new(), _right = new();
    public bool Active { get; private set; }
    public RotationCircularityResult Left => _left.Result;
    public RotationCircularityResult Right => _right.Result;

    public void SetActive(bool active)
    {
        if (active != Active) { _left.Reset(); _right.Reset(); }
        Active = active;
    }

    public void Observe(Gamepad pad)
    {
        if (!Active) return;
        _left.Observe(Normalize.Axis(pad.LX), Normalize.Axis(pad.LY));
        _right.Observe(Normalize.Axis(pad.RX), Normalize.Axis(pad.RY));
    }

    private sealed class RotationTracker
    {
        private const int SectorCount = 72;
        private const double MinimumRadius = .25;
        private const double MaximumInterpolationStep = Math.PI / 2;
        private readonly double?[] _radii = new double?[SectorCount];
        private double? _previousAngle;
        private double? _previousRadius;

        public RotationCircularityResult Result
        {
            get
            {
                var radii = _radii.ToArray();
                var measured = radii.Where(radius => radius.HasValue).Select(radius => radius!.Value).ToArray();
                return new(measured.Length, measured.Length == 0 ? null : measured.Average(radius => Math.Abs(radius - 1)) * 100, radii);
            }
        }

        public void Reset()
        {
            Array.Clear(_radii);
            _previousAngle = null;
            _previousRadius = null;
        }

        public void Observe(double x, double y)
        {
            var radius = Math.Sqrt(x * x + y * y);
            if (radius < MinimumRadius)
            {
                _previousAngle = null;
                _previousRadius = null;
                return;
            }
            var angle = Math.Atan2(y, x);
            if (angle < 0) angle += Math.PI * 2;
            if (_previousAngle is { } previousAngle && _previousRadius is { } previousRadius)
            {
                var delta = angle - previousAngle;
                if (delta > Math.PI) delta -= Math.PI * 2;
                else if (delta < -Math.PI) delta += Math.PI * 2;
                if (Math.Abs(delta) <= MaximumInterpolationStep)
                {
                    var halfSector = Math.PI / SectorCount;
                    var steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / halfSector));
                    for (var i = 0; i <= steps; i++)
                    {
                        var progress = i / (double)steps;
                        Fill(previousAngle + delta * progress, previousRadius + (radius - previousRadius) * progress);
                    }
                }
                else Fill(angle, radius);
            }
            else Fill(angle, radius);
            _previousAngle = angle;
            _previousRadius = radius;
        }

        private void Fill(double angle, double radius)
        {
            angle %= Math.PI * 2;
            if (angle < 0) angle += Math.PI * 2;
            var sector = Math.Min(SectorCount - 1, (int)(angle / (Math.PI * 2) * SectorCount));
            if (_radii[sector] is not { } current || radius > current) _radii[sector] = radius;
        }
    }
}
