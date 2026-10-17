using System.Windows;
using System.Windows.Media;

namespace Jarvis.Desktop;

/// <summary>Animated particle telemetry strip that frames the desktop assistant header.</summary>
public sealed class HudParticleView : FrameworkElement
{
    private static readonly Color[] AccentColors =
    [
        Color.FromRgb(74, 140, 255),
        Color.FromRgb(155, 114, 255),
        Color.FromRgb(225, 77, 99),
        Color.FromRgb(217, 164, 65)
    ];
    private static readonly Pen[] TracePens = CreateTracePens();
    private static readonly Pen[] DimTracePens = CreateDimTracePens();
    private static readonly SolidColorBrush[] ParticleBrushes = CreateParticleBrushes();
    private static readonly HudParticle[] Particles = CreateParticles();
    private readonly DateTime startedAt = DateTime.UtcNow;
    private bool rendering;

    public HudParticleView()
    {
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (rendering) return;
        CompositionTarget.Rendering += OnRendering;
        rendering = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!rendering) return;
        CompositionTarget.Rendering -= OnRendering;
        rendering = false;
    }

    private void OnRendering(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 1 || height < 1) return;

        double time = (DateTime.UtcNow - startedAt).TotalSeconds;
        double midY = height * 0.5;
        double inset = 8;
        double mainX = width * 0.56;
        double mainRadius = Math.Min(22, height * 0.39);

        // Two fine rails and small corner marks give the strip a technical frame.
        dc.DrawLine(DimTracePens[0], new Point(inset, 5), new Point(width - inset, 5));
        dc.DrawLine(DimTracePens[1], new Point(inset, height - 5), new Point(width - inset, height - 5));
        DrawCorner(dc, inset, 5, 1, 1);
        DrawCorner(dc, width - inset, 5, -1, 1);
        DrawCorner(dc, inset, height - 5, 1, -1);
        DrawCorner(dc, width - inset, height - 5, -1, -1);

        // Circuit traces flow through the strip, with intentional gaps around the round instruments.
        double leftGaugeX = width * 0.28;
        double rightGaugeX = width * 0.84;
        dc.DrawLine(DimTracePens[0], new Point(inset + 3, midY), new Point(leftGaugeX - 17, midY));
        dc.DrawLine(DimTracePens[1], new Point(leftGaugeX + 17, midY), new Point(mainX - mainRadius - 5, midY));
        dc.DrawLine(DimTracePens[0], new Point(mainX + mainRadius + 5, midY), new Point(rightGaugeX - 14, midY));
        dc.DrawLine(DimTracePens[1], new Point(rightGaugeX + 14, midY), new Point(width - inset - 3, midY));
        dc.DrawLine(DimTracePens[0], new Point(width * 0.39, 7), new Point(width * 0.39, height - 7));
        dc.DrawLine(DimTracePens[1], new Point(width * 0.73, 7), new Point(width * 0.73, height - 7));
        DrawNode(dc, new Point(width * 0.39, 7), 0);
        DrawNode(dc, new Point(width * 0.39, height - 7), 1);
        DrawNode(dc, new Point(width * 0.73, 7), 2);
        DrawNode(dc, new Point(width * 0.73, height - 7), 3);

        // Compact circular instruments and a larger central instrument echo the reference HUD.
        DrawInstrument(dc, new Point(leftGaugeX, midY), 13, 12, time * 0.08, 1);
        DrawInstrument(dc, new Point(mainX, midY), mainRadius, 28, -time * 0.055, 0);
        DrawInstrument(dc, new Point(rightGaugeX, midY), 10, 10, time * 0.09, 3);

        // Small bars and pulse dots add activity while keeping the metadata legible above them.
        DrawMeter(dc, width * 0.46, height - 11, time * 0.12, 2);
        DrawMeter(dc, width * 0.66, 11, -time * 0.11, 1);

        foreach (HudParticle particle in Particles)
        {
            double progress = (particle.Phase + time * particle.Speed) % 1;
            if (progress < 0) progress += 1;
            double x = inset + progress * (width - inset * 2);
            double y = particle.Lane switch
            {
                0 => height * 0.22,
                1 => midY + Math.Sin(time * 0.12 + particle.Phase * 8) * 1.5,
                _ => height * 0.78
            };
            double size = particle.Size * (0.78 + 0.22 * Math.Sin(time * 0.34 + particle.Phase * 9));
            dc.DrawEllipse(ParticleBrushes[particle.Color], null, new Point(x, y), size, size);
            if (particle.IsSpark)
            {
                dc.DrawEllipse(ParticleBrushes[particle.Color], null, new Point(x, y), size * 2.7, size * 2.7);
                dc.DrawLine(DimTracePens[particle.Color], new Point(x - 5, y), new Point(x + 5, y));
            }
        }
    }

    private static void DrawCorner(DrawingContext dc, double x, double y, int directionX, int directionY)
    {
        dc.DrawLine(DimTracePens[0], new Point(x, y + directionY * 8), new Point(x, y));
        dc.DrawLine(DimTracePens[0], new Point(x, y), new Point(x + directionX * 13, y));
    }

    private static void DrawNode(DrawingContext dc, Point point, int color)
    {
        dc.DrawEllipse(ParticleBrushes[color], null, point, 2.1, 2.1);
        dc.DrawEllipse(null, DimTracePens[color], point, 4.2, 4.2);
    }

    private static void DrawInstrument(DrawingContext dc, Point center, double radius, int tickCount, double angle, int accent)
    {
        dc.DrawEllipse(null, DimTracePens[accent], center, radius, radius);
        dc.DrawEllipse(null, DimTracePens[(accent + 1) % AccentColors.Length], center, radius * 0.76, radius * 0.76);
        dc.DrawEllipse(ParticleBrushes[accent], null, center, 2.2, 2.2);
        for (int i = 0; i < tickCount; i++)
        {
            double theta = angle + Math.PI * 2 * i / tickCount;
            double innerRadius = radius * (i % 4 == 0 ? 0.79 : 0.88);
            double outerRadius = radius * 1.04;
            var start = new Point(center.X + Math.Cos(theta) * innerRadius, center.Y + Math.Sin(theta) * innerRadius);
            var end = new Point(center.X + Math.Cos(theta) * outerRadius, center.Y + Math.Sin(theta) * outerRadius);
            dc.DrawLine(TracePens[(i + accent) % TracePens.Length], start, end);
        }
        double markerAngle = angle * 1.7;
        var marker = new Point(center.X + Math.Cos(markerAngle) * radius * 0.57, center.Y + Math.Sin(markerAngle) * radius * 0.57);
        dc.DrawEllipse(ParticleBrushes[(accent + 2) % ParticleBrushes.Length], null, marker, 2.1, 2.1);
    }

    private static void DrawMeter(DrawingContext dc, double x, double y, double phase, int accent)
    {
        for (int i = 0; i < 9; i++)
        {
            double pulse = 0.25 + 0.75 * Math.Abs(Math.Sin(phase + i * 0.54));
            double barHeight = 3 + pulse * 7;
            double barX = x + i * 5;
            dc.DrawLine(TracePens[(accent + i / 3) % TracePens.Length], new Point(barX, y + barHeight / 2), new Point(barX, y - barHeight / 2));
        }
    }

    private static Pen[] CreateTracePens()
    {
        byte[] alpha = [118, 142, 150, 156];
        var result = new Pen[AccentColors.Length];
        for (int i = 0; i < result.Length; i++) result[i] = CreatePen(Color.FromArgb(alpha[i], AccentColors[i].R, AccentColors[i].G, AccentColors[i].B), i == 0 ? 1 : 0.8);
        return result;
    }

    private static Pen[] CreateDimTracePens()
    {
        byte[] alpha = [56, 62, 61, 68];
        var result = new Pen[AccentColors.Length];
        for (int i = 0; i < result.Length; i++) result[i] = CreatePen(Color.FromArgb(alpha[i], AccentColors[i].R, AccentColors[i].G, AccentColors[i].B), 0.65);
        return result;
    }

    private static SolidColorBrush[] CreateParticleBrushes()
    {
        byte[] alpha = [178, 187, 202, 214];
        var result = new SolidColorBrush[AccentColors.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new SolidColorBrush(Color.FromArgb(alpha[i], AccentColors[i].R, AccentColors[i].G, AccentColors[i].B));
            result[i].Freeze();
        }
        return result;
    }

    private static HudParticle[] CreateParticles()
    {
        var random = new Random(904);
        var result = new HudParticle[48];
        for (int i = 0; i < result.Length; i++)
            result[i] = new HudParticle(random.NextDouble(), 0.015 + random.NextDouble() * 0.045, i % 3, i % AccentColors.Length, 0.7 + random.NextDouble() * 1.8, i % 7 == 0);
        return result;
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private readonly record struct HudParticle(double Phase, double Speed, int Lane, int Color, double Size, bool IsSpark);
}