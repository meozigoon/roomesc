using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace ThirteenthBell;

internal sealed class SceneCanvas : Panel
{
    private Image? _sceneImage;

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
            _sceneImage = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_sceneImage is null)
        {
            return;
        }

        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_sceneImage, ClientRectangle);
    }
}
