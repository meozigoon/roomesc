using System.Drawing.Drawing2D;

namespace ThirteenthBell;

internal sealed class SnowfallEffect : IDisposable
{
    private readonly record struct Flake(float X, float Y, float Speed, float Size, float Phase, int Layer);
    private readonly Flake[] _flakes = new Flake[120];
    private readonly SolidBrush[] _brushes =
    [
        new(Color.FromArgb(48, 237, 247, 255)),
        new(Color.FromArgb(78, 243, 249, 255)),
        new(Color.FromArgb(112, 250, 252, 255))
    ];
    private double _elapsedSeconds;

    public SnowfallEffect()
    {
        for (int index = 0; index < _flakes.Length; index++)
        {
            int layer = index % _brushes.Length;
            _flakes[index] = new Flake(Random.Shared.NextSingle(), Random.Shared.NextSingle(),
                0.025f + layer * 0.025f + Random.Shared.NextSingle() * 0.02f,
                (1.2f + layer * 1.1f + Random.Shared.NextSingle()) * 1.45f,
                Random.Shared.NextSingle() * MathF.Tau, layer);
        }
    }

    internal void Advance(double elapsedSeconds)
    {
        // Skip catch-up after a busy frame so snow never leaps across the screen.
        _elapsedSeconds += Math.Clamp(elapsedSeconds, 0, 0.065);
    }

    internal void Draw(Graphics graphics, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        SmoothingMode previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = Math.Max(0.65f, Math.Min(size.Width / 1400f, size.Height / 820f));
        foreach (Flake flake in _flakes)
        {
            float time = (float)_elapsedSeconds;
            float x = (flake.X + time * 0.003f + MathF.Sin(time * 0.65f + flake.Phase) * 0.012f) % 1.04f;
            float y = (flake.Y + time * flake.Speed) % 1.06f - 0.03f;
            float diameter = flake.Size * scale;
            graphics.FillEllipse(_brushes[flake.Layer], x * size.Width - diameter / 2,
                y * size.Height, diameter, diameter);
        }
        graphics.SmoothingMode = previous;
    }

    public void Dispose()
    {
        foreach (SolidBrush brush in _brushes)
        {
            brush.Dispose();
        }
    }
}
