using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal sealed class RoundedButton : Button
{
    private int _cornerRadius = 8;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(1, value);
            UpdateRegion();
            Invalidate();
        }
    }

    public RoundedButton()
    {
        TextAlign = ContentAlignment.MiddleCenter;
        Padding = Padding.Empty;
        Resize += (_, _) => UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        int radius = Math.Min(_cornerRadius, Math.Min(Width, Height) / 2);
        int diameter = Math.Max(2, radius * 2);
        Rectangle bounds = new(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        Region? oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }
}

internal sealed class InvisibleHotspotButton : Button
{
    public InvisibleHotspotButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.Opaque, true);
        BackColor = Color.Transparent;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        // Opaque hotspot painting includes the exact scene region in OnPaint.
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        if (Parent is SceneCanvas scene)
        {
            scene.PaintBackdropRegion(eventArgs.Graphics, Bounds);
        }
        if (Focused && ShowFocusCues)
        {
            Rectangle focus = Rectangle.Inflate(ClientRectangle, -3, -3);
            ControlPaint.DrawFocusRectangle(eventArgs.Graphics, focus, Theme.PaleGold, Color.Transparent);
        }
    }
}

internal sealed class EnvelopeLetterAnimation : Control
{
    private float _revealProgress;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float RevealProgress
    {
        get => _revealProgress;
        set
        {
            _revealProgress = Math.Clamp(value, 0f, 1f);
            Invalidate();
        }
    }

    public EnvelopeLetterAnimation()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Animation;
        AccessibleName = "봉투에서 편지가 나오는 애니메이션";
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        Graphics graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        int envelopeWidth = Math.Min(520, Math.Max(240, Width - 80));
        int envelopeHeight = Math.Max(170, envelopeWidth * 5 / 12);
        int envelopeX = (Width - envelopeWidth) / 2;
        int envelopeY = Height - envelopeHeight - 45;
        int letterTravel = Math.Min(280, envelopeY - 30);
        int letterY = envelopeY + 25 - (int)Math.Round(letterTravel * RevealProgress);
        Rectangle letter = new(envelopeX + 38, letterY, envelopeWidth - 76, envelopeHeight + 105);
        Rectangle envelope = new(envelopeX, envelopeY, envelopeWidth, envelopeHeight);

        using SolidBrush shadow = new(Color.FromArgb(100, 0, 0, 0));
        using SolidBrush paper = new(Color.FromArgb(248, 235, 202));
        using SolidBrush paperShade = new(Color.FromArgb(218, 190, 142));
        using Pen paperEdge = new(Color.FromArgb(129, 88, 48), 2f);
        using Pen ink = new(Color.FromArgb(95, 62, 45), 2f);

        Rectangle letterShadow = letter;
        letterShadow.Offset(9, 10);
        graphics.FillRectangle(shadow, letterShadow);
        graphics.FillRectangle(paper, letter);
        graphics.DrawRectangle(paperEdge, letter);
        for (int line = 0; line < 6; line++)
        {
            int y = letter.Top + 40 + (line * 28);
            graphics.DrawLine(ink, letter.Left + 45, y, letter.Right - 45, y);
        }

        Rectangle envelopeShadow = envelope;
        envelopeShadow.Offset(10, 12);
        graphics.FillRectangle(shadow, envelopeShadow);
        graphics.FillRectangle(paperShade, envelope);
        Point[] leftFold = [new(envelope.Left, envelope.Top), new(envelope.Left, envelope.Bottom), new(envelope.Left + (envelope.Width / 2), envelope.Top + (envelope.Height / 2))];
        Point[] rightFold = [new(envelope.Right, envelope.Top), new(envelope.Right, envelope.Bottom), new(envelope.Left + (envelope.Width / 2), envelope.Top + (envelope.Height / 2))];
        graphics.FillPolygon(paper, leftFold);
        graphics.FillPolygon(paper, rightFold);
        graphics.DrawRectangle(paperEdge, envelope);

        Point[] flap =
        [
            new(envelope.Left, envelope.Top),
            new(envelope.Right, envelope.Top),
            new(envelope.Left + (envelope.Width / 2), envelope.Top + (int)Math.Round((1f - RevealProgress) * envelope.Height * 0.72f))
        ];
        graphics.FillPolygon(paper, flap);
        graphics.DrawPolygon(paperEdge, flap);

        int sealX = envelope.Left + (envelope.Width / 2);
        int sealY = envelope.Top + (envelope.Height / 2);
        using SolidBrush wax = new(Color.FromArgb(143, 24, 33));
        using Pen waxEdge = new(Color.FromArgb(221, 120, 84), 2f);
        graphics.FillEllipse(wax, sealX - 26, sealY - 26, 52, 52);
        graphics.DrawEllipse(waxEdge, sealX - 26, sealY - 26, 52, 52);
        graphics.DrawLine(waxEdge, sealX - 13, sealY, sealX + 13, sealY);
        graphics.DrawLine(waxEdge, sealX, sealY - 13, sealX, sealY + 13);
    }
}

internal sealed class WallClockLogo : Control
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? LogoImage { get; set; }

    public WallClockLogo()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "13번째 종 벽시계 로고";
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        Graphics graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (LogoImage is not null)
        {
            float scaleToFit = Math.Min(Width / (float)LogoImage.Width, Height / (float)LogoImage.Height);
            int imageWidth = Math.Max(1, (int)Math.Round(LogoImage.Width * scaleToFit));
            int imageHeight = Math.Max(1, (int)Math.Round(LogoImage.Height * scaleToFit));
            Rectangle destination = new((Width - imageWidth) / 2, (Height - imageHeight) / 2, imageWidth, imageHeight);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(LogoImage, destination);
            return;
        }

        float scale = Math.Min(Width / 230f, Height / 280f);
        float ox = (Width - (230f * scale)) / 2f;
        float oy = (Height - (280f * scale)) / 2f;
        graphics.TranslateTransform(ox, oy);
        graphics.ScaleTransform(scale, scale);

        using SolidBrush shadow = new(Color.FromArgb(105, 0, 0, 0));
        using SolidBrush wood = new(Color.FromArgb(61, 34, 22));
        using SolidBrush woodLight = new(Color.FromArgb(104, 61, 31));
        using Pen gold = new(Theme.Gold, 5f);
        using Pen fineGold = new(Theme.PaleGold, 2f);
        using SolidBrush face = new(Color.FromArgb(232, 219, 185));
        using SolidBrush navy = new(Color.FromArgb(17, 41, 66));

        graphics.FillEllipse(shadow, 22, 10, 196, 196);
        using GraphicsPath casePath = RoundedPath(new Rectangle(12, 4, 206, 270), 28);
        graphics.FillPath(wood, casePath);
        graphics.DrawPath(gold, casePath);
        graphics.FillRectangle(woodLight, 34, 172, 162, 82);
        graphics.DrawRectangle(fineGold, 34, 172, 162, 82);

        graphics.FillEllipse(navy, 33, 24, 164, 164);
        graphics.DrawEllipse(gold, 33, 24, 164, 164);
        graphics.FillEllipse(face, 47, 38, 136, 136);
        graphics.DrawEllipse(fineGold, 47, 38, 136, 136);

        PointF center = new(115, 106);
        for (int index = 0; index < 12; index++)
        {
            double angle = (index * Math.PI / 6d) - (Math.PI / 2d);
            float outerX = center.X + ((float)Math.Cos(angle) * 59f);
            float outerY = center.Y + ((float)Math.Sin(angle) * 59f);
            float innerX = center.X + ((float)Math.Cos(angle) * 51f);
            float innerY = center.Y + ((float)Math.Sin(angle) * 51f);
            graphics.DrawLine(fineGold, innerX, innerY, outerX, outerY);
        }

        using Pen hand = new(Color.FromArgb(58, 39, 28), 5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(hand, center, new PointF(91, 73));
        graphics.DrawLine(hand, center, new PointF(115, 58));
        graphics.FillEllipse(navy, center.X - 7, center.Y - 7, 14, 14);

        graphics.DrawLine(gold, 115, 188, 115, 224);
        graphics.FillEllipse(woodLight, 88, 216, 54, 54);
        graphics.DrawEllipse(gold, 88, 216, 54, 54);
        using SolidBrush pendulumHighlight = new(Theme.PaleGold);
        graphics.FillEllipse(pendulumHighlight, 106, 234, 18, 18);
        graphics.ResetTransform();
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class StockingHookSlot : Control
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Occupied { get; set; }

    public StockingHookSlot()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Grouping;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle glow = Rectangle.Inflate(ClientRectangle, -8, -8);
        using SolidBrush fill = new(Color.FromArgb(Occupied ? 34 : 18, Theme.PaleGold));
        using Pen edge = new(Color.FromArgb(Occupied ? 150 : 90, Theme.Gold), 1.5f)
        {
            DashStyle = DashStyle.Dash
        };
        eventArgs.Graphics.FillEllipse(fill, glow);
        eventArgs.Graphics.DrawEllipse(edge, glow);
    }
}

internal sealed class MemoryStripPanel : Panel
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SolvedCount { get; set; }

    public MemoryStripPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.StatusBar;
        AccessibleName = "기억 조각 상태";
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        Graphics graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle body = new(1, 5, Math.Max(1, Width - 3), Math.Max(1, Height - 10));
        using GraphicsPath path = RoundedPath(body, 11);
        using SolidBrush fill = new(Color.FromArgb(184, 18, 30, 43));
        using Pen edge = new(Color.FromArgb(155, Theme.Gold), 1.2f);
        graphics.FillPath(fill, path);
        graphics.DrawPath(edge, path);

    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class StockingDroppedEventArgs(Point center) : EventArgs
{
    public Point Center { get; } = center;
}

internal sealed class StockingPiece : Control
{
    private bool _dragging;
    private Point _grabOffset;

    public StockingPiece(int stockingId, Color stockingColor, string tagText, Image stockingImage)
    {
        StockingId = stockingId;
        StockingColor = stockingColor;
        TagText = tagText;
        StockingImage = stockingImage;
        SetStyle(ControlStyles.SupportsTransparentBackColor
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.SizeAll;
        AccessibleRole = AccessibleRole.Diagram;
        AccessibleName = $"{tagText} 양말, 마우스로 고리에 끌어 놓으세요";
        TabStop = true;
    }

    public int StockingId { get; }

    public Color StockingColor { get; }

    public string TagText { get; }

    public Image StockingImage { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle HomeBounds { get; set; }

    public event EventHandler<StockingDroppedEventArgs>? Dropped;

    public void ReturnHome()
    {
        float scale = Parent is null || Parent.ClientSize.Width <= 0 ? 1f : Parent.ClientSize.Width / 1400f;
        Bounds = new Rectangle(
            (int)Math.Round(HomeBounds.X * scale),
            (int)Math.Round(HomeBounds.Y * scale),
            Math.Max(1, (int)Math.Round(HomeBounds.Width * scale)),
            Math.Max(1, (int)Math.Round(HomeBounds.Height * scale)));
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _grabOffset = eventArgs.Location;
        Capture = true;
        BringToFront();
        Focus();
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (!_dragging || Parent is null)
        {
            return;
        }

        Point pointer = Parent.PointToClient(PointToScreen(eventArgs.Location));
        int x = Math.Clamp(pointer.X - _grabOffset.X, 0, Math.Max(0, Parent.ClientSize.Width - Width));
        int y = Math.Clamp(pointer.Y - _grabOffset.Y, 0, Math.Max(0, Parent.ClientSize.Height - Height));
        Location = new Point(x, y);
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        if (!_dragging || eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        Capture = false;
        Dropped?.Invoke(this, new StockingDroppedEventArgs(new Point(Left + (Width / 2), Top + (Height / 2))));
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        Graphics graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        float sx = Width / 118f;
        float sy = Height / 154f;
        graphics.ScaleTransform(sx, sy);

        int cellWidth = StockingImage.Width / 4;
        Rectangle source = new(cellWidth * StockingId, 0, cellWidth, StockingImage.Height);
        graphics.DrawImage(StockingImage, new Rectangle(0, 0, 118, 154), source, GraphicsUnit.Pixel);

        using SolidBrush tag = new(Color.FromArgb(231, 213, 170));
        using Pen tagEdge = new(Color.FromArgb(113, 73, 37), 1.5f);
        graphics.FillEllipse(tag, 75, 104, 31, 31);
        graphics.DrawEllipse(tagEdge, 75, 104, 31, 31);
        using Font font = Theme.Font(11, FontStyle.Bold);
        TextRenderer.DrawText(graphics, TagText, font, new Rectangle(75, 103, 31, 31), Theme.Night,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(graphics, new Rectangle(3, 3, 111, 148), Theme.PaleGold, Color.Transparent);
        }

        graphics.ResetTransform();
    }

}

internal sealed class FadeTransitionOverlay : Control
{
    private readonly Bitmap _previousFrame;
    private readonly Bitmap _nextFrame;
    private float _progress;

    public FadeTransitionOverlay(Bitmap previousFrame, Bitmap nextFrame)
    {
        _previousFrame = previousFrame;
        _nextFrame = nextFrame;
        SetStyle(ControlStyles.SupportsTransparentBackColor
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        TabStop = false;
        Enabled = false;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0f, 1f);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.DrawImage(_nextFrame, ClientRectangle);

        using ImageAttributes attributes = new();
        ColorMatrix matrix = new()
        {
            Matrix00 = 1f,
            Matrix11 = 1f,
            Matrix22 = 1f,
            Matrix33 = 1f - _progress,
            Matrix44 = 1f
        };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        eventArgs.Graphics.DrawImage(
            _previousFrame,
            ClientRectangle,
            0,
            0,
            _previousFrame.Width,
            _previousFrame.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previousFrame.Dispose();
            _nextFrame.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class StartupTitleOverlay : Panel
{
    private readonly Label _titleLabel;
    private readonly Label _creditLabel;
    private readonly Label _copyrightLabel;
    private float _fadeProgress;

    public StartupTitleOverlay()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        Enabled = true;
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "13번째 종 시작 제목";

        _titleLabel = Theme.CreateLabel("13번째 종: 잊힌 선물", 34, FontStyle.Bold);
        _titleLabel.Name = "StartupGameTitle";
        _titleLabel.Bounds = new Rectangle(0, 280, 1400, 112);
        _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(_titleLabel);

        _creditLabel = Theme.CreateLabel("Made by HSSH ESC", 14);
        _creditLabel.Bounds = new Rectangle(0, 420, 1400, 45);
        _creditLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(_creditLabel);

        _copyrightLabel = Theme.CreateLabel("Copyright © 2026 HSSH ESC. All rights reserved.", 10.5f);
        _copyrightLabel.Bounds = new Rectangle(0, 475, 1400, 38);
        _copyrightLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(_copyrightLabel);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float FadeProgress
    {
        get => _fadeProgress;
        set
        {
            _fadeProgress = Math.Clamp(value, 0f, 1f);
            int alpha = Math.Clamp((int)Math.Round((1f - _fadeProgress) * 255f), 0, 255);
            _titleLabel.ForeColor = Color.FromArgb(alpha, Theme.Snow);
            _creditLabel.ForeColor = Color.FromArgb(alpha, Theme.PaleGold);
            _copyrightLabel.ForeColor = Color.FromArgb(alpha, Theme.Snow);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        int alpha = Math.Clamp((int)Math.Round((1f - _fadeProgress) * 92f), 0, 92);
        using SolidBrush veil = new(Color.FromArgb(alpha, Theme.Night));
        eventArgs.Graphics.FillRectangle(veil, ClientRectangle);
    }
}
