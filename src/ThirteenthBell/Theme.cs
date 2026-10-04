namespace ThirteenthBell;

internal static class Theme
{
    private static System.Drawing.Text.PrivateFontCollection? _fontCollection;
    private static FontFamily? _fontFamily;

    public static readonly Color Night = Color.FromArgb(12, 22, 35);

    public static readonly Color Panel = Color.FromArgb(24, 35, 46);

    public static readonly Color PanelLight = Color.FromArgb(37, 51, 63);

    public static readonly Color Gold = Color.FromArgb(222, 177, 82);

    public static readonly Color PaleGold = Color.FromArgb(246, 222, 163);

    public static readonly Color Snow = Color.FromArgb(238, 247, 250);

    public static readonly Color Cranberry = Color.FromArgb(145, 38, 50);

    public static readonly Color Pine = Color.FromArgb(37, 94, 73);

    public static string FontFamilyName => _fontFamily?.Name ?? FontFamily.GenericSerif.Name;

    public static void Initialize()
    {
        if (_fontFamily is not null)
        {
            return;
        }

        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSerifKR-VF.ttf");
        if (!File.Exists(fontPath))
        {
            throw new FileNotFoundException($"게임 글꼴 파일을 찾을 수 없습니다: {fontPath}", fontPath);
        }

        _fontCollection = new System.Drawing.Text.PrivateFontCollection();
        _fontCollection.AddFontFile(fontPath);
        _fontFamily = _fontCollection.Families.FirstOrDefault(family => family.Name.Equals("Noto Serif KR", StringComparison.OrdinalIgnoreCase))
            ?? _fontCollection.Families.First();
    }

    public static Font Font(float size, FontStyle style = FontStyle.Regular)
    {
        Initialize();
        FontStyle resolvedStyle = _fontFamily!.IsStyleAvailable(style) ? style : FontStyle.Regular;
        return new Font(_fontFamily, size, resolvedStyle, GraphicsUnit.Point);
    }

    public static Font ControlFont(Control control, float size, FontStyle style = FontStyle.Regular)
    {
        // Keep the equation's emoji glyphs when adaptive layout resizes its label.
        return control.Name == "OrnamentEquationText"
            ? new Font("Segoe UI Emoji", size, FontStyle.Regular, GraphicsUnit.Point)
            : Font(size, style);
    }

    public static void Shutdown()
    {
        _fontFamily?.Dispose();
        _fontFamily = null;
        _fontCollection?.Dispose();
        _fontCollection = null;
    }

    public static Button CreateButton(string text, EventHandler onClick, int tabIndex = 0)
    {
        RoundedButton button = new()
        {
            Text = text,
            Font = Font(11, FontStyle.Bold),
            ForeColor = Snow,
            BackColor = Panel,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabIndex = tabIndex,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = Padding.Empty,
            CornerRadius = 7,
            AccessibleName = text.Replace("&", string.Empty, StringComparison.Ordinal)
        };
        button.FlatAppearance.BorderColor = Gold;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Pine;
        button.FlatAppearance.MouseDownBackColor = Cranberry;
        button.Click += onClick;
        return button;
    }

    public static Label CreateLabel(string text, float size, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            Font = Font(size, style),
            ForeColor = Snow,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
    }
}
