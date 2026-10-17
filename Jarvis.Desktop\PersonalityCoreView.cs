using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Jarvis.Desktop;

/// <summary>Transparent, continuously drifting green/cyan holographic energy core.</summary>
public sealed class PersonalityCoreView : FrameworkElement
{
    private readonly record struct Mote(double X, double Y, double Z, double Phase, double Size, bool Bright);
    private static readonly Mote[] Motes = CreateMotes();
    private static readonly Brush[] MoteBrushes = CreateMoteBrushes();
    private static readonly Pen[] WebPens = CreateWebPens();
    private static readonly Pen[] ShellPens =
    [
        MakePen(Color.FromArgb(95, 71, 231, 202), 1.25),
        MakePen(Color.FromArgb(105, 89, 196, 255), 1.1),
        MakePen(Color.FromArgb(82, 174, 230, 111), 0.9),
        MakePen(Color.FromArgb(42, 117, 255, 194), 2.1)
    ];
    private readonly Stopwatch lifetime = Stopwatch.StartNew();
    private readonly Point[] screenMotes = new Point[Motes.Length];
    private bool subscribed;
    private double targetX;
    private double targetY;
    private double cursorX;
    private double cursorY;

    public PersonalityCoreView()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => UpdateSubscription();
        Unloaded += (_, _) => Unsubscribe();
        IsVisibleChanged += (_, _) => UpdateSubscription();
    }

    public void TrackCursor(double x, double y)
    {
        targetX = Math.Clamp(x, -1, 1);
        targetY = Math.Clamp(y, -1, 1);
    }

    private void UpdateSubscription()
    {
        if (IsVisible && !subscribed)
        {
            CompositionTarget.Rendering += OnRendering;
            subscribed = true;
        }
        else if (!IsVisible) Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        CompositionTarget.Rendering -= OnRendering;
        subscribed = false;
    }

    private void OnRendering(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double diameter = Math.Min(ActualWidth, ActualHeight);
        if (diameter < 1) return;
        double time = lifetime.Elapsed.TotalSeconds;
        double cx = ActualWidth * 0.5;
        double cy = ActualHeight * 0.5;
        double radius = diameter * 0.405;
        cursorX += (targetX - cursorX) * 0.035;
        cursorY += (targetY - cursorY) * 0.035;
        double yaw = time * 0.045 - cursorX * 0.12;
        double pitch = Math.Sin(time * 0.07) * 0.035 - cursorY * 0.10;
        double cyaw = Math.Cos(yaw), syaw = Math.Sin(yaw);
        double cpitch = Math.Cos(pitch), spitch = Math.Sin(pitch);

        var aura = new RadialGradientBrush();
        aura.GradientStops.Add(new GradientStop(Color.FromArgb(32, 80, 222, 181), 0));
        aura.GradientStops.Add(new GradientStop(Color.FromArgb(18, 40, 140, 182), 0.55));
        aura.GradientStops.Add(new GradientStop(Color.FromArgb(0, 8, 10, 18), 1));
        aura.Freeze();
        dc.DrawEllipse(aura, null, new Point(cx, cy), radius * 1.12, radius * 1.12);

        Point Project(double x, double y, double z)
        {
            double rx = x * cyaw + z * syaw;
            double rz = -x * syaw + z * cyaw;
            double ry = y * cpitch - rz * spitch;
            double depth = y * spitch + rz * cpitch;
            double perspective = 0.88 + Math.Clamp((depth + 1) * 0.5, 0, 1) * 0.16;
            return new Point(cx + rx * radius * perspective, cy - ry * radius * perspective);
        }

        for (int i = 0; i < Motes.Length; i++)
        {
            Mote mote = Motes[i];
            double drift = time * (0.08 + (i % 7) * 0.007) + mote.Phase;
            double breath = 1 + Math.Sin(drift * 0.6) * 0.013;
            screenMotes[i] = Project(
                mote.X * breath + Math.Sin(drift) * 0.009,
                mote.Y * breath + Math.Cos(drift * 0.83) * 0.008,
                mote.Z * breath + Math.Sin(drift * 0.71) * 0.009);
        }

        // Fine filaments remain sparse so the translucent core never becomes a solid ball.
        for (int i = 0; i < Motes.Length; i++)
        {
            int connected = 0;
            for (int j = i + 1; j < Motes.Length && connected < 2; j++)
            {
                double dx = Motes[i].X - Motes[j].X;
                double dy = Motes[i].Y - Motes[j].Y;
                double dz = Motes[i].Z - Motes[j].Z;
                double distance = dx * dx + dy * dy + dz * dz;
                if (distance > 0.025 || distance < 0.0002 || (i + j) % 3 == 0) continue;
                int pen = Math.Clamp((int)((1 - distance / 0.025) * (WebPens.Length - 1)), 0, WebPens.Length - 1);
                dc.DrawLine(WebPens[pen], screenMotes[i], screenMotes[j]);
                connected++;
            }
        }

        DrawRibbon(dc, Project, time, 0, ShellPens[0], 1.0, 0.14, 1.0);
        DrawRibbon(dc, Project, time, 1, ShellPens[1], 0.81, 0.43, -0.66);
        DrawRibbon(dc, Project, time, 2, ShellPens[2], 0.68, -0.59, 0.47);
        DrawRibbon(dc, Project, time, 3, ShellPens[3], 1.11, 0.22, 0.86);

        for (int i = 0; i < Motes.Length; i++)
        {
            Mote mote = Motes[i];
            double pulse = 0.62 + 0.38 * (0.5 + 0.5 * Math.Sin(time * 0.36 + mote.Phase));
            double r = mote.Size * (mote.Bright ? 1.15 : 0.78) * pulse;
            Brush brush = MoteBrushes[mote.Bright ? 1 : 0];
            dc.DrawEllipse(brush, null, screenMotes[i], r * (mote.Bright ? 4.2 : 2.5), r * (mote.Bright ? 4.2 : 2.5));
            dc.DrawEllipse(MoteBrushes[2], null, screenMotes[i], r, r);
        }
    }

    private void DrawRibbon(DrawingContext dc, Func<double, double, double, Point> project, double time,
        int style, Pen pen, double scale, double tilt, double twist)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            const int steps = 108;
            for (int i = 0; i <= steps; i++)
            {
                double angle = Math.PI * 2 * i / steps;
                double irregular = 1 + 0.075 * Math.Sin(angle * 3 + style * 1.7 + time * 0.16)
                                     + 0.035 * Math.Cos(angle * 5 - style + time * 0.11);
                double vertical = Math.Sin(angle) * scale * irregular;
                double horizontal = Math.Cos(angle) * scale * irregular;
                double x = horizontal * Math.Cos(twist) - vertical * Math.Sin(twist) * tilt;
                double y = horizontal * Math.Sin(twist) * 0.37 + vertical * Math.Cos(twist);
                double z = Math.Sin(angle + time * 0.025 + style) * 0.16 + horizontal * tilt;
                Point point = project(x, y, z);
                if (i == 0) ctx.BeginFigure(point, false, true);
                else ctx.LineTo(point, true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static Mote[] CreateMotes()
    {
        var random = new Random(29103);
        var result = new Mote[210];
        for (int i = 0; i < result.Length; i++)
        {
            double y = random.NextDouble() * 2 - 1;
            double angle = random.NextDouble() * Math.PI * 2;
            double ring = Math.Sqrt(1 - y * y);
            double x = Math.Cos(angle) * ring;
            double z = Math.Sin(angle) * ring;
            double asymmetry = 0.88 + 0.13 * Math.Sin(angle * 2.3 + y * 3.1) + random.NextDouble() * 0.15;
            result[i] = new Mote(x * asymmetry, y * asymmetry, z * asymmetry,
                random.NextDouble() * Math.PI * 2, 0.7 + random.NextDouble() * 1.2, random.NextDouble() < 0.16);
        }
        return result;
    }

    private static Brush[] CreateMoteBrushes()
    {
        Brush Make(byte alpha, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
            brush.Freeze();
            return brush;
        }
        return [Make(31, 81, 255, 193), Make(46, 166, 249, 193), Make(210, 179, 255, 214)];
    }

    private static Pen[] CreateWebPens()
    {
        byte[] alpha = [7, 11, 16, 23, 31, 40];
        var result = new Pen[alpha.Length];
        for (int i = 0; i < result.Length; i++) result[i] = MakePen(Color.FromArgb(alpha[i], 82, 223, 197), 0.7);
        return result;
    }

    private static Pen MakePen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
