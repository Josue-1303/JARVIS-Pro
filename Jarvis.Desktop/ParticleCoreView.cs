using System.Windows;
using System.Windows.Media;

namespace Jarvis.Desktop;

/// <summary>Continuously rendered golden holographic particle sphere for the J.A.R.V.I.S. home screen.</summary>
public sealed class ParticleCoreView : FrameworkElement
{
    private const int ParticleCount = 360;
    private static readonly Particle[] Particles = CreateParticles();
    private static readonly Link[] Links = CreateLinks();
    private SolidColorBrush[] particleBrushes;
    private Pen[] linkPens;
    private readonly ProjectedParticle[] projected = new ProjectedParticle[ParticleCount];
    private readonly DateTime startedAt = DateTime.UtcNow;
    private readonly Pen orbitPen = CreatePen(Color.FromArgb(42, 255, 190, 73), 0.7);
    private Color accentColor = Color.FromRgb(217, 164, 65);
    private Brush accentGlow = CreateAccentGlow(Color.FromRgb(217, 164, 65));
    private bool rendering;
    private double targetCursorX;
    private double targetCursorY;
    private double cursorX;
    private double cursorY;

    public ParticleCoreView()
    {
        particleBrushes = CreateParticleBrushes(accentColor);
        linkPens = CreateLinkPens(accentColor);
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => UpdateSubscription();
    }

    public Color AccentColor
    {
        get => accentColor;
        set
        {
            if (accentColor == value) return;
            accentColor = value;
            accentGlow = CreateAccentGlow(value);
            particleBrushes = CreateParticleBrushes(value);
            linkPens = CreateLinkPens(value);
            InvalidateVisual();
        }
    }

    public void TrackCursor(double x, double y)
    {
        targetCursorX = Math.Clamp(x, -1, 1);
        targetCursorY = Math.Clamp(y, -1, 1);
    }
    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateSubscription();

    private void UpdateSubscription()
    {
        if (IsVisible && !rendering)
        {
            CompositionTarget.Rendering += OnRendering;
            rendering = true;
        }
        else if (!IsVisible) Unsubscribe();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Unsubscribe();

    private void Unsubscribe()
    {
        if (!rendering) return;
        CompositionTarget.Rendering -= OnRendering;
        rendering = false;
    }

    private void OnRendering(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double diameter = Math.Min(ActualWidth, ActualHeight);
        if (diameter < 1) return;

        double time = (DateTime.UtcNow - startedAt).TotalSeconds;
        double centerX = ActualWidth / 2;
        double centerY = ActualHeight / 2;
        double radius = diameter * 0.435;
        cursorX += (targetCursorX - cursorX) * 0.055;
        cursorY += (targetCursorY - cursorY) * 0.055;
        double yaw = time * 0.075 - cursorX * 0.27;
        double pitch = 0.16 + Math.Sin(time * 0.065) * 0.055 - cursorY * 0.23;
        double cosYaw = Math.Cos(yaw);
        double sinYaw = Math.Sin(yaw);
        double cosPitch = Math.Cos(pitch);
        double sinPitch = Math.Sin(pitch);

        drawingContext.DrawEllipse(accentGlow, null, new Point(centerX, centerY), radius * 0.72, radius * 0.72);

        drawingContext.PushTransform(new RotateTransform(time * 7.5, centerX, centerY));
        drawingContext.DrawEllipse(null, orbitPen, new Point(centerX, centerY), radius * 0.94, radius * 0.34);
        drawingContext.Pop();
        drawingContext.PushTransform(new RotateTransform(-time * 5.2 + 64, centerX, centerY));
        drawingContext.DrawEllipse(null, orbitPen, new Point(centerX, centerY), radius * 0.86, radius * 0.28);
        drawingContext.Pop();

        for (int i = 0; i < Particles.Length; i++)
        {
            Particle particle = Particles[i];
            double drift = time * particle.Drift + particle.Phase;
            double breathing = 1 + Math.Sin(drift) * 0.018 + Math.Cos(drift * 0.59 + particle.X * 13) * 0.01;
            double x = particle.X * breathing + Math.Sin(drift * 0.77) * 0.007;
            double y = particle.Y * breathing + Math.Cos(drift * 0.69 + particle.Z * 9) * 0.006;
            double z = particle.Z * breathing + Math.Sin(drift * 0.53 + particle.Y * 11) * 0.007;

            double rotatedX = x * cosYaw + z * sinYaw;
            double rotatedZ = -x * sinYaw + z * cosYaw;
            double rotatedY = y * cosPitch - rotatedZ * sinPitch;
            double depth = y * sinPitch + rotatedZ * cosPitch;
            double depthFront = Math.Clamp((depth + 1) * 0.5, 0, 1);
            double perspective = 0.91 + depthFront * 0.12;

            double screenX = centerX + rotatedX * radius * perspective;
            double screenY = centerY - rotatedY * radius * perspective;
            double pull = particle.IsSeeker ? 0.08 + 0.12 * (0.5 + 0.5 * Math.Sin(time * 0.34 + particle.Phase)) : 0;
            if (particle.IsSeeker)
            {
                double targetX = centerX + cursorX * radius * 0.88 + Math.Cos(particle.Phase * 1.7) * radius * 0.055;
                double targetY = centerY + cursorY * radius * 0.88 + Math.Sin(particle.Phase * 1.7) * radius * 0.055;
                screenX += (targetX - screenX) * pull;
                screenY += (targetY - screenY) * pull;
                depthFront = Math.Clamp(depthFront + pull * 0.32, 0, 1);
            }

            projected[i] = new ProjectedParticle(
                screenX,
                screenY,
                depthFront,
                particle.Size * (particle.IsSeeker ? 0.78 + depthFront * 0.9 : 0.52 + depthFront * 0.78),
                centerX + rotatedX * radius * perspective,
                centerY - rotatedY * radius * perspective,
                particle.IsSeeker);
        }

        foreach (Link link in Links)
        {
            ProjectedParticle a = projected[link.A];
            ProjectedParticle b = projected[link.B];
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > diameter * diameter * 0.009 || distanceSquared < 1) continue;

            int strength = Math.Clamp((int)(((a.Depth + b.Depth) * 0.5) * (linkPens.Length - 1)), 0, linkPens.Length - 1);
            drawingContext.DrawLine(linkPens[strength], new Point(a.X, a.Y), new Point(b.X, b.Y));
        }

        for (int i = 0; i < projected.Length; i++)
        {
            ProjectedParticle particle = projected[i];
            int brightness = Math.Clamp((int)(particle.Depth * (particleBrushes.Length - 1)), 0, particleBrushes.Length - 1);
            double size = particle.Size;
            if (particle.IsSeeker)
            {
                double trailX = particle.X - particle.SourceX;
                double trailY = particle.Y - particle.SourceY;
                if (trailX * trailX + trailY * trailY > 4)
                    drawingContext.DrawLine(linkPens[5], new Point(particle.SourceX, particle.SourceY), new Point(particle.X, particle.Y));
                brightness = Math.Max(brightness, particleBrushes.Length - 2);
            }
            drawingContext.DrawEllipse(particleBrushes[brightness], null, new Point(particle.X, particle.Y), size, size);
            if (particle.IsSeeker || (brightness >= particleBrushes.Length - 2 && i % 9 == 0))
                drawingContext.DrawEllipse(particleBrushes[brightness], null, new Point(particle.X, particle.Y), size * (particle.IsSeeker ? 2.7 : 2.3), size * (particle.IsSeeker ? 2.7 : 2.3));
        }
    }

    private static Particle[] CreateParticles()
    {
        var random = new Random(1303);
        var result = new Particle[ParticleCount];
        for (int i = 0; i < result.Length; i++)
        {
            // Random directions and uneven radii avoid the perfect latitude/longitude symmetry
            // of a mathematical sphere and create a cloud with a broken, organic silhouette.
            double vertical = random.NextDouble() * 2 - 1;
            double angle = random.NextDouble() * Math.PI * 2;
            double ring = Math.Sqrt(1 - vertical * vertical);
            double x = Math.Cos(angle) * ring;
            double y = vertical;
            double z = Math.Sin(angle) * ring;

            double lobes = 0.11 * Math.Sin(x * 6.1 + z * 3.4) * Math.Cos(y * 5.3 - angle)
                         + 0.07 * Math.Sin(z * 8.2 - y * 4.7 + 1.2)
                         + 0.045 * Math.Cos(x * 10.1 + y * 7.3);
            double wisps = random.NextDouble() < 0.13 ? random.NextDouble() * 0.48 : 0;
            double radius = 0.72 + random.NextDouble() * 0.46 + lobes + wisps;

            result[i] = new Particle(
                x * radius,
                y * radius,
                z * radius,
                random.NextDouble() * Math.PI * 2,
                0.025 + random.NextDouble() * 0.075,
                0.58 + random.NextDouble() * 0.78,
                random.NextDouble() < 0.11);
        }
        return result;
    }
    private static Link[] CreateLinks()
    {
        var links = new List<Link>();
        for (int i = 0; i < Particles.Length; i++)
        for (int j = i + 1; j < Particles.Length; j++)
        {
            Particle a = Particles[i];
            Particle b = Particles[j];
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            if (dx * dx + dy * dy + dz * dz < 0.16) links.Add(new Link(i, j));
        }
        return links.ToArray();
    }

    private static SolidColorBrush[] CreateParticleBrushes(Color color)
    {
        byte[] alpha = [24, 38, 58, 84, 116, 151, 190, 235];
        var brushes = new SolidColorBrush[alpha.Length];
        for (int i = 0; i < brushes.Length; i++)
        {
            byte r = (byte)Math.Min(255, color.R + i * (255 - color.R) / (alpha.Length - 1));
            byte g = (byte)Math.Min(255, color.G + i * (255 - color.G) / (alpha.Length - 1));
            byte b = (byte)Math.Min(255, color.B + i * (255 - color.B) / (alpha.Length - 1));
            brushes[i] = new SolidColorBrush(Color.FromArgb(alpha[i], r, g, b));
            brushes[i].Freeze();
        }
        return brushes;
    }

    private static Pen[] CreateLinkPens(Color color)
    {
        byte[] alpha = [8, 13, 20, 30, 43, 61, 82, 105];
        var pens = new Pen[alpha.Length];
        for (int i = 0; i < pens.Length; i++) pens[i] = CreatePen(Color.FromArgb(alpha[i], color.R, color.G, color.B), 0.55);
        return pens;
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private static Brush CreateAccentGlow(Color color)
    {
        var glow = new RadialGradientBrush();
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(34, color.R, color.G, color.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(15, color.R, color.G, color.B), 0.58));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        glow.Freeze();
        return glow;
    }

    private readonly record struct Particle(double X, double Y, double Z, double Phase, double Drift, double Size, bool IsSeeker);
    private readonly record struct Link(int A, int B);
    private readonly record struct ProjectedParticle(double X, double Y, double Depth, double Size, double SourceX, double SourceY, bool IsSeeker);
}
