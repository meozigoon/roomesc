namespace ThirteenthBell;

// Table cells stay on one line so a long nickname cannot shift the other columns.
internal sealed class AlignedTextLabel : Label
{
    protected override void OnPaint(PaintEventArgs e)
    {
        TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping;
        flags |= TextAlign == ContentAlignment.MiddleRight ? TextFormatFlags.Right
            : TextAlign == ContentAlignment.MiddleCenter ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
        if (AutoEllipsis)
        {
            flags |= TextFormatFlags.EndEllipsis;
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
    }
}
