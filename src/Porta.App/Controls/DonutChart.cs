using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Porta.Controls;

public sealed record ChartSlice(string Label, double Value, IBrush Brush);

public static class ChartPalette
{
    private static readonly IBrush[] Brushes =
    [
        new SolidColorBrush(Color.Parse("#2563EB")), // royal blue
        new SolidColorBrush(Color.Parse("#F59E0B")), // amber
        new SolidColorBrush(Color.Parse("#10B981")), // emerald
        new SolidColorBrush(Color.Parse("#8B5CF6")), // violet
        new SolidColorBrush(Color.Parse("#0EA5E9")), // sky
        new SolidColorBrush(Color.Parse("#EC4899")), // pink
        new SolidColorBrush(Color.Parse("#14B8A6")), // teal
        new SolidColorBrush(Color.Parse("#64748B"))  // slate
    ];

    public static IBrush Get(int index) => Brushes[index % Brushes.Length];
}

/// <summary>Lightweight donut chart for the allocation-per-asset-class view (FR-04). No external dependency.</summary>
public sealed class DonutChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ChartSlice>?> SlicesProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<ChartSlice>?>(nameof(Slices));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 28);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(TrackBrush), new SolidColorBrush(Color.Parse("#33808080")));

    static DonutChart()
    {
        AffectsRender<DonutChart>(SlicesProperty, ThicknessProperty, TrackBrushProperty);
    }

    public IReadOnlyList<ChartSlice>? Slices
    {
        get => GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= Thickness * 2) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = (size - Thickness) / 2;

        // Background track (also the empty state).
        context.DrawEllipse(null, new Pen(TrackBrush, Thickness), center, radius, radius);

        var slices = Slices?.Where(s => s.Value > 0).ToList();
        if (slices is null || slices.Count == 0) return;

        var total = slices.Sum(s => s.Value);
        if (slices.Count == 1)
        {
            context.DrawEllipse(null, new Pen(slices[0].Brush, Thickness), center, radius, radius);
            return;
        }

        const double gapDegrees = 1.2;
        var start = -90.0;
        foreach (var slice in slices)
        {
            var sweep = slice.Value / total * 360.0;
            var drawSweep = Math.Max(0.1, sweep - gapDegrees);
            DrawArc(context, center, radius, start, drawSweep, new Pen(slice.Brush, Thickness, lineCap: PenLineCap.Flat));
            start += sweep;
        }
    }

    private static void DrawArc(DrawingContext ctx, Point c, double r, double startDeg, double sweepDeg, IPen pen)
    {
        static Point At(Point c, double r, double deg)
        {
            var rad = deg * Math.PI / 180.0;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(At(c, r, startDeg), false);
            g.ArcTo(At(c, r, startDeg + sweepDeg), new Size(r, r), 0, sweepDeg > 180, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geometry);
    }
}
