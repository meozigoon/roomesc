using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Diagnostics;

namespace ThirteenthBell;

internal sealed class SceneCanvas : Panel
{
    private Image? _sceneImage;
    private Bitmap? _scaledBackdrop;
    private SnowfallEffect? _snowfall;
    private System.Windows.Forms.Timer? _snowfallTimer;
    private long _snowfallLastTimestamp;
    private bool _snowfallEnabled;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SnowfallEnabled
    {
        get => _snowfallEnabled;
        set
        {
            if (_snowfallEnabled == value)
            {
                return;
            }
            _snowfallEnabled = value;
            if (value)
            {
                _snowfall ??= new SnowfallEffect();
                if (_snowfallTimer is null)
                {
                    _snowfallTimer = new System.Windows.Forms.Timer { Interval = 33 };
                    _snowfallTimer.Tick += HandleSnowfallTick;
                }
            }
            UpdateSnowfallTimer();
            Invalidate(true);
        }
    }

    private void UpdateSnowfallTimer()
    {
        if (_snowfallTimer is null)
        {
            return;
        }
        _snowfallLastTimestamp = Stopwatch.GetTimestamp();
        _snowfallTimer.Enabled = _snowfallEnabled && Visible && IsHandleCreated;
    }

    private void HandleSnowfallTick(object? sender, EventArgs eventArgs)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = Stopwatch.GetElapsedTime(_snowfallLastTimestamp, now).TotalSeconds;
        _snowfallLastTimestamp = now;
        if (FindForm()?.WindowState == FormWindowState.Minimized)
        {
            return;
        }
        _snowfall?.Advance(elapsed);
        Invalidate();
        // Repaint transparent text with its parent; opaque buttons do not need another draw.
        foreach (Control control in Controls)
        {
            if (control.Visible && control.BackColor.A == 0)
            {
                control.Invalidate(true);
            }
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateSnowfallTimer();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateSnowfallTimer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _snowfallTimer?.Stop();
        base.OnHandleDestroyed(e);
    }

    public event PaintEventHandler? ScenePaint;

    public SceneCanvas()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Black;
        AccessibleRole = AccessibleRole.Pane;
        AccessibleName = "게임 장면";
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? SceneImage
    {
        get => _sceneImage;
        set
        {
            if (ReferenceEquals(_sceneImage, value))
            {
                return;
            }

            _sceneImage = value;
            ClearBackdrop();
            Invalidate(true);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        PaintBackdrop(e.Graphics);
    }

    private void ClearBackdrop()
    {
        _scaledBackdrop?.Dispose();
        _scaledBackdrop = null;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        ClearBackdrop();
        base.OnSizeChanged(e);
    }

    internal void PaintBackdrop(Graphics graphics)
    {

        if (_sceneImage is null)
        {
            if (_snowfallEnabled)
            {
                _snowfall?.Draw(graphics, ClientSize);
            }
            return;
        }

        if (_scaledBackdrop is null && Width > 0 && Height > 0)
        {
            _scaledBackdrop = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using Graphics buffer = Graphics.FromImage(_scaledBackdrop);
            buffer.InterpolationMode = InterpolationMode.HighQualityBicubic;
            buffer.PixelOffsetMode = PixelOffsetMode.HighQuality;
            buffer.DrawImage(_sceneImage, ClientRectangle);
        }
        if (_scaledBackdrop is not null)
        {
            graphics.DrawImageUnscaled(_scaledBackdrop, Point.Empty);
        }
        ScenePaint?.Invoke(this, new PaintEventArgs(graphics, ClientRectangle));
        if (_snowfallEnabled)
        {
            _snowfall?.Draw(graphics, ClientSize);
        }
    }

    internal void PaintBackdropRegion(Graphics graphics, Rectangle region)
    {
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform(-region.X, -region.Y);
        PaintBackdrop(graphics);
        graphics.Restore(state);
    }

    internal Bitmap CopyBackdrop()
    {
        Bitmap copy = new(Math.Max(1, Width), Math.Max(1, Height));
        using Graphics graphics = Graphics.FromImage(copy);
        PaintBackdrop(graphics);
        return copy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearBackdrop();
            _snowfallTimer?.Dispose();
            _snowfall?.Dispose();
        }
        base.Dispose(disposing);
    }
}
