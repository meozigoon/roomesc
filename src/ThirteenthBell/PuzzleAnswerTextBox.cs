using System.Runtime.InteropServices;

namespace ThirteenthBell;

internal sealed class PuzzleAnswerTextBox(bool digitsOnly) : TextBox
{
    private bool _filtering;

    private bool Allowed(char value)
    {
        return digitsOnly ? value is >= '0' and <= '9' : value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private string Normalize(string text)
    {
        return new string(text.Where(Allowed).Select(char.ToUpperInvariant).ToArray());
    }

    protected override void OnKeyPress(KeyPressEventArgs eventArgs)
    {
        if (!char.IsControl(eventArgs.KeyChar) && !Allowed(eventArgs.KeyChar))
        {
            eventArgs.Handled = true;
        }
        base.OnKeyPress(eventArgs);
    }

    protected override void OnTextChanged(EventArgs eventArgs)
    {
        if (_filtering)
        {
            return;
        }
        string original = Text;
        string normalized = Normalize(original);
        if (MaxLength > 0 && normalized.Length > MaxLength)
        {
            normalized = normalized[..MaxLength];
        }
        if (original != normalized)
        {
            int caret = original.Take(SelectionStart).Count(Allowed);
            _filtering = true;
            try
            {
                Text = normalized;
                SelectionStart = Math.Min(caret, normalized.Length);
            }
            finally
            {
                _filtering = false;
            }
        }
        base.OnTextChanged(eventArgs);
    }

    protected override void WndProc(ref Message message)
    {
        const int paste = 0x0302;
        if (message.Msg == paste)
        {
            try
            {
                string text = Normalize(Clipboard.GetText(TextDataFormat.UnicodeText));
                int capacity = Math.Max(0, MaxLength - (Text.Length - SelectionLength));
                SelectedText = text[..Math.Min(capacity, text.Length)];
            }
            catch (ExternalException)
            {
                // A clipboard temporarily locked by another application can be retried.
            }
            return;
        }
        base.WndProc(ref message);
    }
}
