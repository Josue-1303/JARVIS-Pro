using System.Windows;
using System.Windows.Media;

namespace Jarvis.Desktop;

/// <summary>Subtle side instrumentation that fills the home stage around the animated core.</summary>
public sealed class StageHudView : FrameworkElement
{
    private static readonly Color[] Colors =
    [
        Color.FromRgb(74, 140, 255),
        Color.FromRgb(155, 114, 255),
        Color.FromRgb(225, 77, 99),
        Color.FromRgb(217, 164, 65)
    ];
    private static readonly Pen[] Pens = CreatePens();
    private static readonly Pen[] DimPens = CreateDimPens();
    private static readonly SolidColorBrush[] Brushes = CreateBrushes();
    private static readonly Speck[] Specks = CreateSpecks();
    private readonly DateTime startedAt = DateTime.UtcNow;
    private bool rendering;

    public StageHudView()
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
        double panelWidth = Math.Min(196, width * 0.19);
        double top = 25;
        double bottom = height - 36;
        double leftCenterX = panelWidth * 0.48;
        double rightCenterX = width - panelWidth * 0.48;
        double upperY = height * 0.32;
        double lowerY = height * 0.68;

        // Very faint structural guides keep the black field intentional without boxing it in.
        for (int i = 1; i <= 4; i++)
        {
            double y = height * i / 5;
            dc.DrawLine(DimPens[i % DimPens.Length], new Point(panelWidth + 4, y), new Point(width - panelWidth - 4, y));
        }
        DrawSideFrame(dc, 10, top, panelWidth - 8, bottom, 0);
        DrawSideFrame(dc, width - panelWidth + 8, top, width - 10, bottom, 1);

        DrawDial(dc, new Point(leftCenterX, upperY), 48, time * 0.055, 1);
        DrawDial(dc, new Point(leftCenterX, lowerY), 24, -time * 0.07, 3);
        DrawDial(dc, new Point(rightCenterX, upperY), 31, time * 0.045, 0);
        DrawSpectrum(dc, 20, height * 0.49, panelWidth - 28, height * 0.12, time * 0.10, 2);
        DrawSpectrum(dc, width - panelWidth + 18, height * 0.56, panelWidth - 28, height * 0.16, -time * 0.09, 1);
        DrawSignalTrace(dc, 20, height * 0.84, panelWidth - 28, time * 0.07, 3);
        DrawSignalTrace(dc, width - panelWidth + 18, height * 0.84, panelWidth - 28, -time * 0.06, 0);

        // Sparse slow particles drift through both side instruments and the center field.
        foreach (Speck speck in Specks)
        {
            double drift = (speck.Position + time * speck.Speed) % 1;
            if (drift < 0) drift += 1;
            double x = speck.Side switch
            {
                0 => 14 + drift * (panelWidth - 22),
                1 => width - panelWidth + 8 + drift * (panelWidth - 22),
                _ => panelWidth + 10 + drift * (width - panelWidth * 2 - 20)
            };
            double y = speck.Y * height + Math.Sin(time * 0.11 + speck.Phase) * 1.4;
            double size = speck.Size * (0.9 + 0.1 * Math.Sin(time * 0.17 + speck.Phase));
            dc.DrawEllipse(Brushes[speck.Color], null, new Point(x, y), size, size);
            if (speck.Glow) dc.DrawEllipse(Brushes[speck.Color], null, new Point(x, y), size * 2.4, size * 2.4);
        }
    }

    private static void DrawSideFrame(DrawingContext dc, double left, double top, double right, double bottom, int accent)
    {
        double corner = 15;
        dc.DrawLine(DimPens[accent], new Point(left, top + corner), new Point(left, top));
        dc.DrawLine(DimPens[accent], new Point(left, top), new Point(left + corner, top));
        dc.DrawLine(DimPens[accent], new Point(right - corner, top), new Point(right, top));
        dc.DrawLine(DimPens[accent], new Point(right, top), new Point(right, top + corner));
        dc.DrawLine(DimPens[(accent + 1) % Colors.Length], new Point(left, bottom - corner), new Point(left, bottom));
        dc.DrawLine(DimPens[(accent + 1) % Colors.Length], new Point(left, bottom), new Point(left + corner, bottom));
        dc.DrawLine(DimPens[(accent + 1) % Colors.Length], new Point(right - corner, bottom), new Point(right, bottom));
        dc.DrawLine(DimPens[(accent + 1) % Colors.Length], new Point(right, bottom), new Point(right, bottom - corner));
        dc.DrawLine(DimPens[accent], new Point(left, top + corner), new Point(left, bottom - corner));
        dc.DrawLine(DimPens[(accent + 2) % Colors.Length], new Point(right, top + corner), new Point(right, bottom - corner));
        for (int i = 1; i <= 5; i++)
        {
            double y = top + (bottom - top) * i / 6;
            dc.DrawLine(DimPens[(accent + i) % Colors.Length], new Point(left - 3, y), new Point(left + 4, y));
            dc.DrawLine(DimPens[(accent + i + 1) % Colors.Length], new Point(right - 4, y), new Point(right + 3, y));
        }
    }

    private static void DrawDial(DrawingContext dc, Point center, double radius, double phase, int accent)
    {
        dc.DrawEllipse(null, DimPens[accent], center, radius, radius);
        dc.DrawEllipse(null, DimPens[(accent + 1) % Colors.Length], center, radius * 0.78, radius * 0.78);
        dc.DrawEllipse(null, DimPens[(accent + 2) % Colors.Length], center, radius * 0.52, radius * 0.52);
        for (int i = 0; i < 32; i++)
        {
            double angle = phase + Math.PI * 2 * i / 32;
            double inner = radius * (i % 4 == 0 ? 0.8 : 0.9);
            var start = new Point(center.X + Math.Cos(angle) * inner, center.Y + Math.Sin(angle) * inner);
            var end = new Point(center.X + Math.Cos(angle) * radius * 1.05, center.Y + Math.Sin(angle) * radius * 1.05);
            dc.DrawLine(Pens[(i + accent) % Pens.Length], start, end);
        }
        double pointerAngle = phase * 1.4;
        var pointer = new Point(center.X + Math.Cos(pointerAngle) * radius * 0.37, center.Y + Math.Sin(pointerAngle) * radius * 0.37);
        dc.DrawLine(Pens[accent], center, pointer);
        dc.DrawEllipse(Brushes[(accent + 3) % Brushes.Length], null, pointer, 2.3, 2.3);
    }

    private static void DrawSpectrum(DrawingContext dc, double x, double y, double width, double height, double phase, int accent)
    {
        dc.DrawLine(DimPens[accent], new Point(x, y), new Point(x + width, y));
        dc.DrawLine(DimPens[(accent + 1) % Colors.Length], new Point(x, y + height), new Point(x + width, y + height));
        int count = 20;
        double gap = width / count;
        for (int i = 0; i < count; i++)
        {
            double energy = 0.18 + 0.82 * Math.Abs(Math.Sin(phase + i * 0.39)) * Math.Abs(Math.Cos(i * 0.17 + phase * 0.7));
            double bar = height * energy;
            double barX = x + i * gap + gap * 0.5;
            dc.DrawLine(Pens[(accent + i / 5) % Pens.Length], new Point(barX, y + height), new Point(barX, y + height - bar));
            if (i % 4 == 0) dc.DrawEllipse(Brushes[(accent + i / 4) % Brushes.Length], null, new Point(barX, y + height - bar), 1.4, 1.4);
        }
    }

    private static void DrawSignalTrace(DrawingContext dc, double x, double y, double width, double phase, int accent)
    {
        Point previous = new(x, y);
        for (int i = 1; i <= 36; i++)
        {
            double px = x + width * i / 36;
            double wave = Math.Sin(i * 0.56 + phase) * 3 + Math.Sin(i * 0.19 - phase * 0.4) * 2;
            Point current = new(px, y + wave);
            dc.DrawLine(Pens[(accent + i / 9) % Pens.Length], previous, current);
            if (i % 9 == 0) dc.DrawEllipse(Brushes[(accent + i / 9) % Brushes.Length], null, current, 1.7, 1.7);
            previous = current;
        }
    }

    private static Pen[] CreatePens()
    {
        byte[] alpha = [94, 105, 96, 102];
        var pens = new Pen[Colors.Length];
        for (int i = 0; i < pens.Length; i++) pens[i] = MakePen(Color.FromArgb(alpha[i], Colors[i].R, Colors[i].G, Colors[i].B), 0.75);
        return pens;
    }

    private static Pen[] CreateDimPens()
    {
        byte[] alpha = [32, 36, 34, 39];
        var pens = new Pen[Colors.Length];
        for (int i = 0; i < pens.Length; i++) pens[i] = MakePen(Color.FromArgb(alpha[i], Colors[i].R, Colors[i].G, Colors[i].B), 0.65);
        return pens;
    }

    private static SolidColorBrush[] CreateBrushes()
    {
        byte[] alpha = [112, 118, 126, 135];
        var brushes = new SolidColorBrush[Colors.Length];
        for (int i = 0; i < brushes.Length; i++)
        {
            brushes[i] = new SolidColorBrush(Color.FromArgb(alpha[i], Colors[i].R, Colors[i].G, Colors[i].B));
            brushes[i].Freeze();
        }
        return brushes;
    }

    private static Speck[] CreateSpecks()
    {
        var random = new Random(662);
        var result = new Speck[56];
        for (int i = 0; i < result.Length; i++)
            result[i] = new Speck(random.NextDouble(), 0.08 + random.NextDouble() * 0.14,
                0.12 + random.NextDouble() * 0.76, i % 3 == 0 ? 2 : i % Colors.Length,
                0.7 + random.NextDouble() * 0.8, random.NextDouble() < 0.2, random.NextDouble() * 6.28, i % 4 == 0 ? 0 : i % 4 == 1 ? 1 : 2);
        return result;
    }

    private static Pen MakePen(Color color, double width)
    {
        var pen = new Pen(new SolidColorBrush(color), width);
        pen.Freeze();
        return pen;
    }

    private readonly record struct Speck(double Position, double Speed, double Y, int Color, double Size, bool Glow, double Phase, int Side);
}