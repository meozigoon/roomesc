using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace ThirteenthBell;

internal sealed class SceneCanvas : Panel
{
    private Image? _sceneImage;
    private Bitmap? _scaledBackdrop;

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
        }
        base.Dispose(disposing);
    }
}
