using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Jarvis.Desktop;

/// <summary>A low-motion, particle-only neural cloud with one-shot save/delete pulses.</summary>
public sealed class MemoryBrainView : FrameworkElement
{
    private sealed class Particle(double x, double y, double phase, double size, int colorIndex, bool ambient = false, double depth = 0)
    {
        public readonly double X = x;
        public readonly double Y = y;
        public readonly double Phase = phase;
        public readonly double Size = size;
        public readonly int ColorIndex = colorIndex;
        public readonly bool Ambient = ambient;
        public readonly double Depth = depth;
    }

    private static readonly Color[] ParticleColors =
    {
        Color.FromRgb(177, 139, 255), Color.FromRgb(104, 177, 255),
        Color.FromRgb(218, 178, 255), Color.FromRgb(225, 185, 105), Color.FromRgb(238, 91, 117)
    };

    private readonly Particle[] particles;
    private readonly Stopwatch transferClock = new();
    private TaskCompletionSource? transferCompletion;
    private bool deleting;
    private bool subscribed;

    public MemoryBrainView()
    {
        Focusable = false;
        var random = new Random(80317);
        var points = new List<Particle>(126);
        int attempts = 0;
        while (points.Count < 104 && attempts++ < 5000)
        {
            double x = random.NextDouble() * 0.72 + 0.12;
            double y = random.NextDouble() * 0.72 + 0.10;
            if (!InsideBrain(x, y)) continue;
            double edgeBias = DistanceFromCenter(x, y) > 0.68 ? 1.25 : 1.0;
            int color = points.Count % 29 == 4 ? 3 : points.Count % 37 == 7 ? 4 : points.Count % 4 == 0 ? 1 : 0;
            points.Add(new Particle(x, y, random.NextDouble() * Math.PI * 2,
                (random.NextDouble() * 1.1 + 0.8) * edgeBias, color, depth: random.NextDouble() * 0.38 - 0.19));
        }
        // A few loose motes keep the silhouette airy instead of closing into a perfect outline.
        for (int i = 0; i < 22; i++)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            double radius = random.NextDouble() * 0.10 + 0.32;
            double x = 0.50 + Math.Cos(angle) * radius * 0.84;
            double y = 0.45 + Math.Sin(angle) * radius * 0.77;
            points.Add(new Particle(x, y, random.NextDouble() * Math.PI * 2, random.NextDouble() * 0.65 + 0.35,
                i % 7 == 0 ? 3 : i % 4 == 0 ? 1 : 0, ambient: true, depth: random.NextDouble() * 0.26 - 0.13));
        }
        particles = points.ToArray();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => UpdateSubscription();
    }

    public Task PlayTransferAsync(bool deleting)
    {
        if (transferCompletion is not null) return transferCompletion.Task;
        this.deleting = deleting;
        transferClock.Restart();
        transferCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        InvalidateVisual();
        return transferCompletion.Task;
    }

    private static double DistanceFromCenter(double x, double y)
    {
        double dx = (x - 0.51) / 0.31;
        double dy = (y - 0.44) / 0.30;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool InEllipse(double x, double y, double cx, double cy, double rx, double ry)
    {
        double dx = (x - cx) / rx;
        double dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1;
    }

    // Overlapping, intentionally uneven lobes create an approximate lateral brain profile.
    private static bool InsideBrain(double x, double y)
    {
        bool cortex = InEllipse(x, y, 0.45, 0.39, 0.25, 0.25) ||
                      InEllipse(x, y, 0.63, 0.40, 0.20, 0.22) ||
                      InEllipse(x, y, 0.72, 0.47, 0.13, 0.15) ||
                      InEllipse(x, y, 0.44, 0.54, 0.22, 0.14) ||
                      InEllipse(x, y, 0.62, 0.57, 0.16, 0.13) ||
                      InEllipse(x, y, 0.57, 0.68, 0.075, 0.12);
        bool notch = InEllipse(x, y, 0.50, 0.65, 0.08, 0.055) ||
                     InEllipse(x, y, 0.28, 0.53, 0.05, 0.045);
        return cortex && !notch;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateSubscription();
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unsubscribe();
        CompleteTransfer();
    }
    private void UpdateSubscription() { if (IsVisible) Subscribe(); else Unsubscribe(); }
    private void Subscribe()
    {
        if (subscribed) return;
        CompositionTarget.Rendering += OnRendering;
        subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!subscribed) return;
        CompositionTarget.Rendering -= OnRendering;
        subscribed = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        InvalidateVisual();
        if (transferCompletion is not null && transferClock.Elapsed >= TimeSpan.FromMilliseconds(1750))
            CompleteTransfer();
    }

    private void CompleteTransfer()
    {
        transferClock.Stop();
        TaskCompletionSource? finished = transferCompletion;
        transferCompletion = null;
        finished?.TrySetResult();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 1 || h < 1) return;
        double scale = Math.Min(w, h);
        Point P(double x, double y) => new((w - scale) / 2 + x * scale, (h - scale) / 2 + y * scale);
        double elapsed = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        // Soft, localized glow; no fixed silhouette, filled geometry, or background image.
        var aura = new RadialGradientBrush();
        aura.GradientStops.Add(new GradientStop(Color.FromArgb(29, 105, 82, 191), 0));
        aura.GradientStops.Add(new GradientStop(Color.FromArgb(15, 68, 101, 206), 0.56));
        aura.GradientStops.Add(new GradientStop(Color.FromArgb(0, 8, 10, 18), 1));
        dc.DrawEllipse(aura, null, P(0.51, 0.45), scale * 0.35, scale * 0.34);

        var positions = new Point[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            Particle particle = particles[i];
            double speed = particle.Ambient ? 0.42 : 0.48;
            double drift = particle.Ambient ? 0.052 : 0.030;
            double yaw = elapsed * 0.24;
            double pitch = Math.Sin(elapsed * 0.11) * 0.08;
            double dx = particle.X - 0.51;
            double dy = particle.Y - 0.45;
            double rotatedX = dx * Math.Cos(yaw) + particle.Depth * Math.Sin(yaw);
            double rotatedDepth = -dx * Math.Sin(yaw) + particle.Depth * Math.Cos(yaw);
            double rotatedY = dy * Math.Cos(pitch) - rotatedDepth * Math.Sin(pitch);
            double perspective = 1 + rotatedDepth * 0.30;
            double x = 0.51 + rotatedX * perspective
                + Math.Sin(elapsed * speed + particle.Phase) * drift
                + Math.Sin(elapsed * 0.19 + particle.Phase * 0.73) * drift * 0.34;
            double y = 0.45 + rotatedY * perspective
                + Math.Cos(elapsed * speed * 0.83 + particle.Phase * 1.31) * drift * 0.78
                + Math.Sin(elapsed * 0.16 + particle.Phase * 0.51) * drift * 0.32;
            positions[i] = P(x, y);
        }

        // Sparse nearest-neighbor filaments reveal the lobes but leave generous dark gaps.
        for (int i = 0; i < particles.Length; i++)
        {
            int links = 0;
            for (int j = i + 1; j < particles.Length && links < 2; j++)
            {
                double dx = particles[i].X - particles[j].X;
                double dy = particles[i].Y - particles[j].Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > 0.056 || particles[i].Ambient || particles[j].Ambient) continue;
                byte alpha = (byte)(8 + (0.056 - distance) / 0.056 * 20);
                var color = Color.FromArgb(alpha, 142, 125, 228);
                dc.DrawLine(new Pen(new SolidColorBrush(color), 0.65), positions[i], positions[j]);
                links++;
            }
        }

        // Slow, non-synchronous twinkle and drift make this read as floating particles.
        for (int i = 0; i < particles.Length; i++)
        {
            Particle particle = particles[i];
            Color color = ParticleColors[particle.ColorIndex];
            double pulse = 0.60 + (Math.Sin(elapsed * 0.34 + particle.Phase) + 1) * 0.16;
            double radius = particle.Size * (particle.Ambient ? 0.0015 : 0.0024) * scale;
            byte alpha = (byte)((particle.Ambient ? 76 : 118) * pulse);
            var glow = new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.19), color.R, color.G, color.B));
            var core = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            dc.DrawEllipse(glow, null, positions[i], radius * 4.8, radius * 4.8);
            dc.DrawEllipse(core, null, positions[i], radius, radius);
        }

        if (transferCompletion is null) return;
        double progress = Math.Clamp(transferClock.Elapsed.TotalMilliseconds / 1750.0, 0, 1);
        // Ease in/out once; the trail fades completely and does not loop.
        double eased = progress * progress * (3 - 2 * progress);
        Point from = deleting ? P(0.52, 0.48) : P(0.44, 0.94);
        Point to = deleting ? P(0.86, 0.26) : P(0.54, 0.48);
        Color transferColor = deleting ? Color.FromRgb(238, 91, 117) : Color.FromRgb(228, 188, 108);
        for (int i = 0; i < 6; i++)
        {
            double tail = Math.Clamp(progress - i * 0.075, 0, 1);
            double travel = tail * tail * (3 - 2 * tail);
            Point position = new(from.X + (to.X - from.X) * travel, from.Y + (to.Y - from.Y) * travel);
            double fade = Math.Sin(Math.PI * progress) * (1 - i * 0.10);
            byte alpha = (byte)(Math.Clamp(fade, 0, 1) * 205);
            double radius = Math.Max(0.8, (3.8 - i * 0.35) * (0.45 + fade * 0.45));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.22), transferColor.R, transferColor.G, transferColor.B)),
                null, position, radius * 2.7, radius * 2.7);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, transferColor.R, transferColor.G, transferColor.B)),
                null, position, radius, radius);
        }
        byte trailAlpha = (byte)(Math.Sin(Math.PI * progress) * 40);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(trailAlpha, transferColor.R, transferColor.G, transferColor.B)), 1.0), from, to);
    }
}
