using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace ThirteenthBell;

internal enum NarrativeVisualStyle
{
    Narration,
    Clue,
    Dialogue,
    Mechanism,
    Result
}

internal enum AtmosphereCardStyle
{
    Letter,
    Speech,
    GiftTag
}

internal sealed class NarrativePanel : Panel
{
    private NarrativeVisualStyle _visualStyle;
    private float _visualOpacity = 1f;

    public NarrativePanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Pane;
        AccessibleName = "하단 이야기 상자";
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NarrativeVisualStyle VisualStyle
    {
        get => _visualStyle;
        set
        {
            if (_visualStyle == value)
            {
                return;
            }

            _visualStyle = value;
            Invalidate(true);
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float VisualOpacity
    {
        get => _visualOpacity;
        set
        {
            _visualOpacity = Math.Clamp(value, 0f, 1f);
            Invalidate(true);
        }
    }

    public Color TitleColor => _visualStyle == NarrativeVisualStyle.Clue
        ? Color.FromArgb(91, 55, 34)
        : Theme.Gold;

    public Color BodyColor => _visualStyle == NarrativeVisualStyle.Clue
        ? Color.FromArgb(55, 38, 28)
        : Theme.Snow;

    public Color SecondaryColor => _visualStyle == NarrativeVisualStyle.Clue
        ? Color.FromArgb(111, 69, 38)
        : Theme.PaleGold;

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle body = new(4, 4, Math.Max(1, Width - 9), Math.Max(1, Height - 15));
        using GraphicsPath path = RoundedRectangle(body, 22);

        switch (_visualStyle)
        {
            case NarrativeVisualStyle.Clue:
                using (SolidBrush paper = new(WithOpacity(Color.FromArgb(226, 242, 226, 190), _visualOpacity)))
                using (Pen edge = new(WithOpacity(Color.FromArgb(190, 131, 82, 42), _visualOpacity), 1.5f))
                using (Pen inner = new(WithOpacity(Color.FromArgb(150, 196, 150, 83), _visualOpacity), 0.8f))
                {
                    eventArgs.Graphics.FillPath(paper, path);
                    eventArgs.Graphics.DrawPath(edge, path);
                    Rectangle innerBounds = Rectangle.Inflate(body, -9, -9);
                    using GraphicsPath innerPath = RoundedRectangle(innerBounds, 15);
                    eventArgs.Graphics.DrawPath(inner, innerPath);
                }
                break;

            case NarrativeVisualStyle.Dialogue:
                using (SolidBrush fill = new(WithOpacity(Color.FromArgb(215, 42, 25, 38), _visualOpacity)))
                using (Pen edge = new(WithOpacity(Color.FromArgb(205, Theme.PaleGold), _visualOpacity), 1.5f))
                {
                    eventArgs.Graphics.FillPath(fill, path);
                    eventArgs.Graphics.DrawPath(edge, path);
                    Point[] tail =
                    [
                        new(body.Left + 82, body.Bottom - 1),
                        new(body.Left + 128, body.Bottom - 1),
                        new(body.Left + 101, Height - 2)
                    ];
                    eventArgs.Graphics.FillPolygon(fill, tail);
                    eventArgs.Graphics.DrawLines(edge, [tail[0], tail[2], tail[1]]);
                }
                break;

            case NarrativeVisualStyle.Mechanism:
                using (SolidBrush fill = new(WithOpacity(Color.FromArgb(208, Theme.Panel), _visualOpacity)))
                using (Pen edge = new(WithOpacity(Color.FromArgb(205, Theme.Gold), _visualOpacity), 1.5f))
                using (Pen inner = new(WithOpacity(Color.FromArgb(155, Theme.PaleGold), _visualOpacity), 0.8f))
                using (SolidBrush rivet = new(WithOpacity(Color.FromArgb(210, Theme.Gold), _visualOpacity)))
                {
                    eventArgs.Graphics.FillPath(fill, path);
                    eventArgs.Graphics.DrawPath(edge, path);
                    eventArgs.Graphics.DrawRectangle(inner, Rectangle.Inflate(body, -10, -10));
                    foreach (Point point in new[]
                    {
                        new Point(body.Left + 18, body.Top + 18),
                        new Point(body.Right - 18, body.Top + 18),
                        new Point(body.Left + 18, body.Bottom - 18),
                        new Point(body.Right - 18, body.Bottom - 18)
                    })
                    {
                        eventArgs.Graphics.FillEllipse(rivet, point.X - 4, point.Y - 4, 8, 8);
                    }
                }
                break;

            case NarrativeVisualStyle.Result:
                using (SolidBrush fill = new(WithOpacity(Color.FromArgb(210, 26, 65, 55), _visualOpacity)))
                using (Pen edge = new(WithOpacity(Color.FromArgb(205, Theme.Gold), _visualOpacity), 1.5f))
                {
                    eventArgs.Graphics.FillPath(fill, path);
                    eventArgs.Graphics.DrawPath(edge, path);
                }
                break;

            default:
                using (SolidBrush fill = new(WithOpacity(Color.FromArgb(205, 18, 30, 43), _visualOpacity)))
                using (Pen edge = new(WithOpacity(Color.FromArgb(200, Theme.Gold), _visualOpacity), 1.5f))
                {
                    eventArgs.Graphics.FillPath(fill, path);
                    eventArgs.Graphics.DrawPath(edge, path);
                    DrawCornerOrnaments(eventArgs.Graphics, body, edge);
                }
                break;
        }
    }

    private static void DrawCornerOrnaments(Graphics graphics, Rectangle bounds, Pen pen)
    {
        const int length = 24;
        graphics.DrawLine(pen, bounds.Left + 10, bounds.Top + length, bounds.Left + 10, bounds.Top + 10);
        graphics.DrawLine(pen, bounds.Left + 10, bounds.Top + 10, bounds.Left + length, bounds.Top + 10);
        graphics.DrawLine(pen, bounds.Right - length, bounds.Top + 10, bounds.Right - 10, bounds.Top + 10);
        graphics.DrawLine(pen, bounds.Right - 10, bounds.Top + 10, bounds.Right - 10, bounds.Top + length);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color WithOpacity(Color color, float opacity)
    {
        return Color.FromArgb((int)Math.Round(color.A * opacity), color.R, color.G, color.B);
    }
}

internal sealed class AtmosphereCard : Panel
{
    private readonly AtmosphereCardStyle _cardStyle;

    public AtmosphereCard(string heading, string body, AtmosphereCardStyle cardStyle)
    {
        _cardStyle = cardStyle;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = heading;

        TitleLabel = Theme.CreateLabel(heading, 20, FontStyle.Bold);
        TitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(TitleLabel);

        BodyLabel = Theme.CreateLabel(body, 13);
        BodyLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(BodyLabel);

        ApplyPalette();
    }

    public Label TitleLabel { get; }

    public Label BodyLabel { get; }

    public void Arrange(Rectangle titleBounds, Rectangle bodyBounds)
    {
        TitleLabel.Bounds = titleBounds;
        BodyLabel.Bounds = bodyBounds;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bounds = new(5, 5, Math.Max(1, Width - 11), Math.Max(1, Height - 11));

        switch (_cardStyle)
        {
            case AtmosphereCardStyle.Letter:
                DrawLetter(eventArgs.Graphics, bounds);
                break;
            case AtmosphereCardStyle.Speech:
                DrawSpeech(eventArgs.Graphics, bounds);
                break;
            case AtmosphereCardStyle.GiftTag:
                DrawGiftTag(eventArgs.Graphics, bounds);
                break;
        }
    }

    private void ApplyPalette()
    {
        if (_cardStyle is AtmosphereCardStyle.Letter or AtmosphereCardStyle.GiftTag)
        {
            TitleLabel.ForeColor = Color.FromArgb(102, 58, 34);
            BodyLabel.ForeColor = Color.FromArgb(54, 37, 27);
            return;
        }

        TitleLabel.ForeColor = Theme.PaleGold;
        BodyLabel.ForeColor = Theme.Snow;
    }

    private static void DrawLetter(Graphics graphics, Rectangle bounds)
    {
        using SolidBrush shadow = new(Color.FromArgb(80, 53, 35, 27));
        using SolidBrush paper = new(Color.FromArgb(224, 244, 229, 194));
        using Pen edge = new(Color.FromArgb(185, 143, 91, 47), 1.5f);
        using Pen inner = new(Color.FromArgb(150, 201, 160, 91), 0.8f);
        graphics.FillRectangle(shadow, bounds.X + 7, bounds.Y + 8, bounds.Width, bounds.Height);
        graphics.FillRectangle(paper, bounds);
        graphics.DrawRectangle(edge, bounds);
        graphics.DrawRectangle(inner, Rectangle.Inflate(bounds, -12, -12));

        int sealSize = Math.Max(26, Math.Min(52, bounds.Height / 8));
        Rectangle seal = new(bounds.Right - sealSize - 24, bounds.Bottom - sealSize - 20, sealSize, sealSize);
        using SolidBrush wax = new(Theme.Cranberry);
        using Pen waxEdge = new(Color.FromArgb(93, 23, 33), 1.5f);
        graphics.FillEllipse(wax, seal);
        graphics.DrawEllipse(waxEdge, seal);
        graphics.DrawLine(waxEdge, seal.Left + (sealSize / 2), seal.Top + 10, seal.Left + (sealSize / 2), seal.Bottom - 10);
        graphics.DrawLine(waxEdge, seal.Left + 10, seal.Top + (sealSize / 2), seal.Right - 10, seal.Top + (sealSize / 2));
    }

    private static void DrawSpeech(Graphics graphics, Rectangle bounds)
    {
        Rectangle bubble = new(bounds.X, bounds.Y, bounds.Width, Math.Max(1, bounds.Height - 28));
        using GraphicsPath path = RoundedRectangle(bubble, 28);
        using SolidBrush fill = new(Color.FromArgb(210, 27, 39, 53));
        using Pen edge = new(Color.FromArgb(205, Theme.PaleGold), 1.5f);
        graphics.FillPath(fill, path);
        graphics.DrawPath(edge, path);
        Point[] tail =
        [
            new(bubble.Left + 150, bubble.Bottom - 2),
            new(bubble.Left + 210, bubble.Bottom - 2),
            new(bubble.Left + 175, bounds.Bottom)
        ];
        graphics.FillPolygon(fill, tail);
        graphics.DrawLines(edge, [tail[0], tail[2], tail[1]]);
    }

    private static void DrawGiftTag(Graphics graphics, Rectangle bounds)
    {
        int cut = Math.Min(28, bounds.Height / 6);
        Point[] tag =
        [
            new(bounds.Left + cut, bounds.Top),
            new(bounds.Right - cut, bounds.Top),
            new(bounds.Right, bounds.Top + cut),
            new(bounds.Right, bounds.Bottom - cut),
            new(bounds.Right - cut, bounds.Bottom),
            new(bounds.Left + cut, bounds.Bottom),
            new(bounds.Left, bounds.Bottom - cut),
            new(bounds.Left, bounds.Top + cut)
        ];
        using SolidBrush paper = new(Color.FromArgb(224, 239, 220, 178));
        using Pen edge = new(Color.FromArgb(205, Theme.Cranberry), 2f);
        using Pen stitch = new(Color.FromArgb(145, 155, 93, 45), 0.8f) { DashStyle = DashStyle.Dash };
        graphics.FillPolygon(paper, tag);
        graphics.DrawPolygon(edge, tag);
        Rectangle stitchBounds = Rectangle.Inflate(bounds, -15, -15);
        graphics.DrawRectangle(stitch, stitchBounds);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
