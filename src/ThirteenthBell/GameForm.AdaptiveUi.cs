using ThirteenthBell.Core;

namespace ThirteenthBell;

internal sealed partial class GameForm
{
    private const float CompactFontScale = 0.82f;
    private bool _arrangingText;
    private readonly HashSet<string> _inspectedLocations = new(StringComparer.Ordinal);
    private readonly Dictionary<Label, Rectangle> _textCardBounds = [];
    private readonly Dictionary<Control, (string Text, Size Size, Padding Padding, float Preferred, float Applied)> _textFitCache = [];

    private void ArrangeAdaptiveText()
    {
        if (_arrangingText || _currentScale <= 0)
        {
            return;
        }

        _arrangingText = true;
        try
        {
            ArrangeNarrativeText();
            foreach ((Label label, Rectangle bounds) in _textCardBounds)
            {
                if (!label.IsDisposed && _baseLayout.TryGetValue(label, out LayoutSnapshot snapshot))
                {
                    using Font font = Theme.Font(Math.Max(6.5f, snapshot.FontSize * _currentScale), snapshot.FontStyle);
                    int natural = label.Text.Split('\n').Max(line => TextRenderer.MeasureText(line, font).Width);
                    int width = Math.Clamp((int)Math.Ceiling(natural / _currentScale) + 64, Math.Min(320, bounds.Width), bounds.Width);
                    int height = (int)Math.Ceiling(MeasureWrapped(label.Text, font, (int)Math.Round((width - 48) * _currentScale)).Height / _currentScale) + 38;
                    SetBaseBounds(label, new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y, width, height));
                }
            }
            if (_screen == GameScreen.Menu && _actions.TryGetValue("menu_back", out Button? back)
                && _baseLayout.TryGetValue(back, out LayoutSnapshot backLayout))
            {
                int bottom = _textCardBounds.Keys.Where(label => !label.IsDisposed && label.Parent == _menuScene)
                    .Select(label => _baseLayout[label].Bounds.Bottom).DefaultIfEmpty(600).Max();
                SetBaseBounds(back, backLayout.Bounds with { Y = bottom + 24 });
            }
            foreach ((Control control, LayoutSnapshot snapshot) in _baseLayout)
            {
                if (control.IsDisposed || control is InvisibleHotspotButton)
                {
                    continue;
                }

                if (control is Label or Button or TextBoxBase or NumericUpDown)
                {
                    FitControlText(control, Math.Max(6.5f, snapshot.FontSize * _currentScale));
                }
            }
        }
        finally
        {
            _arrangingText = false;
        }
    }

    private void PrepareCompactControl(Control control)
    {
        Rectangle original = control.Bounds;
        if (control is AtmosphereCard card)
        {
            using Font title = Theme.Font(card.TitleLabel.Font.SizeInPoints * CompactFontScale, FontStyle.Bold);
            using Font body = Theme.ControlFont(card.BodyLabel, card.BodyLabel.Font.SizeInPoints * CompactFontScale);
            int natural = Math.Max(TextRenderer.MeasureText(card.TitleLabel.Text, title).Width,
                card.BodyLabel.Text.Split('\n').Max(line => TextRenderer.MeasureText(line, body).Width));
            int width = Math.Clamp(natural + 90, Math.Min(360, original.Width), original.Width);
            int titleHeight = MeasureWrapped(card.TitleLabel.Text, title, width - 70).Height + 6;
            int bodyHeight = MeasureWrapped(card.BodyLabel.Text, body, width - 70).Height + 8;
            int height = titleHeight + bodyHeight + 78;
            if (card.Controls.Find("ConfirmationError", false).FirstOrDefault() is Label error)
            {
                error.Bounds = new Rectangle(35, 44 + titleHeight + bodyHeight, width - 70, 60);
                height += 70;
            }
            control.Bounds = new Rectangle(original.X + (original.Width - width) / 2, original.Y, width, height);
            card.Arrange(new Rectangle(35, 22, width - 70, titleHeight),
                new Rectangle(35, 32 + titleHeight, width - 70, bodyHeight));
        }
        else if (control is Label label && label.BackColor.A > 0 && label.Padding.Horizontal > 0)
        {
            _textCardBounds.Add(label, original);
            using Font font = Theme.Font(label.Font.SizeInPoints * CompactFontScale, label.Font.Style);
            int natural = label.Text.Split('\n').Max(line => TextRenderer.MeasureText(line, font).Width);
            int width = Math.Clamp(natural + 64, Math.Min(320, original.Width), original.Width);
            label.Padding = new Padding(24, 16, 24, 16);
            int height = MeasureWrapped(label.Text, font, width - 48).Height + 38;
            label.Bounds = new Rectangle(original.X + (original.Width - width) / 2, original.Y, width, height);
        }
        else if (control is RoundedButton || control is TextBoxBase || control is NumericUpDown)
        {
            int width = Math.Max(40, (int)Math.Round(original.Width * 0.88f));
            int height = Math.Max(28, (int)Math.Round(original.Height * 0.76f));
            control.Bounds = new Rectangle(original.X + (original.Width - width) / 2,
                original.Y + (original.Height - height) / 2, width, height);
        }
    }

    private static Size MeasureWrapped(string text, Font font, int width)
    {
        return TextRenderer.MeasureText(text, font, new Size(Math.Max(1, width), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
    }

    private void FitControlText(Control control, float preferredSize)
    {
        if (string.IsNullOrEmpty(control.Text) || control.ClientSize.Width <= 1 || control.ClientSize.Height <= 1)
        {
            return;
        }
        if (_textFitCache.TryGetValue(control, out var cached)
            && cached.Text == control.Text && cached.Size == control.ClientSize && cached.Padding == control.Padding
            && Math.Abs(cached.Preferred - preferredSize) < 0.05f && Math.Abs(cached.Applied - control.Font.SizeInPoints) < 0.05f)
        {
            return;
        }

        int width = Math.Max(1, control.ClientSize.Width - control.Padding.Horizontal - (control is Label ? 0 : 8));
        int height = Math.Max(1, control.ClientSize.Height - control.Padding.Vertical - (control is Label ? 0 : 6));
        FontStyle style = control.Font.Style;
        float minimum = Math.Min(preferredSize, 6.5f);
        float size = preferredSize;
        Font fitted;
        while (true)
        {
            fitted = Theme.ControlFont(control, size, style);
            Size measured = control is TextBoxBase or NumericUpDown
                ? TextRenderer.MeasureText(control.Text, fitted, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix)
                : MeasureWrapped(control.Text, fitted, width);
            if (measured.Width <= width && measured.Height <= height || size <= minimum)
            {
                break;
            }

            fitted.Dispose();
            size = Math.Max(minimum, size - 0.25f);
        }

        if (Math.Abs(control.Font.SizeInPoints - fitted.SizeInPoints) < 0.05f)
        {
            _textFitCache[control] = (control.Text, control.ClientSize, control.Padding, preferredSize, fitted.SizeInPoints);
            fitted.Dispose();
            return;
        }

        Font oldFont = control.Font;
        control.Font = fitted;
        oldFont.Dispose();
        _textFitCache[control] = (control.Text, control.ClientSize, control.Padding, preferredSize, fitted.SizeInPoints);
    }

    private void ArrangeNarrativeText()
    {
        if (_screen == GameScreen.Menu)
        {
            return;
        }

        bool shortText = _notebookText.Text.Length <= 65 && string.IsNullOrEmpty(_hintText.Text);
        float bodySize = shortText ? 8.2f : 9f;
        float scale = _currentScale;
        using Font bodyFont = Theme.Font(Math.Max(6.5f, bodySize * scale));
        using Font titleFont = Theme.Font(Math.Max(6.5f, 10f * scale), FontStyle.Bold);
        string content = string.Join("\n", new[] { _chapterLabel.Text, _notebookText.Text, _hintText.Text }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        int naturalWidth = content.Split('\n').Max(line => TextRenderer.MeasureText(line, bodyFont).Width);
        int width = Math.Clamp((int)Math.Ceiling(naturalWidth / scale) + 60, 340, 920);
        int innerWidth = Math.Max(1, (int)Math.Round((width - 52) * scale));
        int titleHeight = string.IsNullOrEmpty(_chapterLabel.Text) ? 0 : (int)Math.Ceiling(MeasureWrapped(_chapterLabel.Text, titleFont, innerWidth).Height / scale) + 3;
        int bodyHeight = string.IsNullOrEmpty(_notebookText.Text) ? 0 : (int)Math.Ceiling(MeasureWrapped(_notebookText.Text, bodyFont, innerWidth).Height / scale) + 4;
        int hintHeight = string.IsNullOrEmpty(_hintText.Text) ? 0 : (int)Math.Ceiling(MeasureWrapped(_hintText.Text, bodyFont, innerWidth).Height / scale) + 4;
        int height = 36 + titleHeight + bodyHeight + hintHeight;
        int bottom = _screen is GameScreen.PostalRoom or GameScreen.PostalLedger or GameScreen.PostalBells ? 810 : 790;
        SetBaseBounds(_sidebar, new Rectangle((BaseWidth - width) / 2, bottom - height, width, height));
        SetBaseBounds(_chapterLabel, new Rectangle(26, 14, width - 52, Math.Max(1, titleHeight)));
        SetBaseBounds(_notebookText, new Rectangle(26, 14 + titleHeight, width - 52, Math.Max(1, bodyHeight)));
        SetBaseBounds(_hintText, new Rectangle(26, 14 + titleHeight + bodyHeight, width - 52, Math.Max(1, hintHeight)));
        SetPreferredFont(_chapterLabel, 10f);
        SetPreferredFont(_notebookText, bodySize);
        SetPreferredFont(_hintText, bodySize);
        _hintText.Visible = hintHeight > 0;
    }

    private void SetPreferredFont(Control control, float size)
    {
        if (_baseLayout.TryGetValue(control, out LayoutSnapshot snapshot))
        {
            _baseLayout[control] = snapshot with { FontSize = size };
        }
    }

    private void MarkInspected(string id)
    {
        _inspectedLocations.Add(id);
    }

    private string NextGlobalHint()
    {
        if (!_postal.DoorOpened)
        {
            (string Id, string Text, bool Found)[] places =
            [
                ("postal_move_suitcase", "우편실 바닥 왼쪽의 가죽 가방을 눌러 보세요.", _postal.SuitcaseMoved),
                ("postal_move_parcel", "우편실 바닥 가운데의 붉은 선물 상자를 눌러 보세요.", _postal.ParcelMoved),
                ("postal_move_blanket", "우편실 바닥 오른쪽의 초록 담요를 눌러 보세요.", _postal.BellClueFound),
                ("postal_take_key", "짐을 옮긴 자리에서 드러난 작은 황동 열쇠를 눌러 보세요.", _postal.KeyFound || !_postal.KeyVisible),
                ("postal_drawer", "우편실 왼쪽 벽의 황동 서랍을 눌러 보세요.", _postal.DrawerOpened),
                ("postal_bells", "우편실 오른쪽 벽의 네 개의 종을 눌러 보세요.", _postal.BellsSolved),
                ("postal_door", "우편실 중앙의 공방 문을 눌러 보세요.", _postal.DoorOpened)
            ];
            foreach ((string id, string text, bool found) in places)
            {
                if (!found && !_inspectedLocations.Contains(id))
                {
                    return text;
                }
            }

            if (!_postal.KeyFound)
            {
                return "열쇠 찾기: 서로 겹쳐 놓인 짐 아래의 바닥을 확인해 보세요.";
            }
            if (!_postal.DrawerOpened)
            {
                return "서랍: 손잡이 아래의 각인과 가지고 있는 물건을 비교해 보세요.";
            }
            if (!_postal.BellClueFound)
            {
                return "종의 기록: 부드러운 천이 가리고 있는 자리를 확인해 보세요.";
            }
            if (_screen == GameScreen.PostalBells && !_postal.BellsSolved || _postal.RouteSolved && !_postal.BellsSolved)
            {
                return "종의 봉인: 이동이 끝난 종을 다음 이동의 출발점으로 생각해 보세요.";
            }
            if (!_postal.RouteSolved)
            {
                return "배송 봉인: 서로 바로 이웃해야 하는 두 장소부터 묶어서 생각해 보세요.";
            }
            return "두 봉인이 모두 풀렸습니다. 중앙 문을 눌러 공방으로 들어가세요.";
        }

        (string Id, string Text, PuzzleId? Puzzle)[] workshopPlaces =
        [
            ("hotspot_lantern", "공방 왼쪽 벽의 별등 금고를 눌러 보세요.", PuzzleId.Lanterns),
            ("hotspot_desk", "공방 왼쪽 아래의 마리의 책상을 눌러 가까이 살펴보세요.", null),
            ("hotspot_melody", "공방 가운데 아래의 스노글로브 계산대를 눌러 보세요.", PuzzleId.Melody),
            ("hotspot_loom", "공방 오른쪽 벽난로 앞의 양말 장치를 눌러 보세요.", PuzzleId.RibbonLoom),
            ("desk_letter", "마리의 책상 가운데에 놓인 봉투를 눌러 보세요.", PuzzleId.LetterAcrostic),
            ("desk_toys", "마리의 책상 위쪽 장난감 선반을 눌러 보세요.", PuzzleId.ToyCipher),
            ("desk_chart", "마리의 책상 왼쪽 아래 별자리 도면을 눌러 보세요.", PuzzleId.StarChart),
            ("hotspot_clock", "공방 중앙 벽의 별시계를 눌러 보세요.", null)
        ];
        foreach ((string id, string text, PuzzleId? puzzle) in workshopPlaces)
        {
            if (!_inspectedLocations.Contains(id) && (puzzle is null || !PuzzleDone(puzzle.Value)))
            {
                return text;
            }
        }

        (PuzzleId Puzzle, GameScreen Screen, string Hint)[] puzzles =
        [
            (PuzzleId.Lanterns, GameScreen.Lanterns, "별등 금고: 이웃한 숫자끼리 어떤 관계인지 비교해 보세요."),
            (PuzzleId.Melody, GameScreen.Melody, "스노글로브 계산대: 같은 장식은 모든 줄에서 같은 값을 가집니다."),
            (PuzzleId.RibbonLoom, GameScreen.RibbonLoom, "양말 장치: 바로 이웃해야 하는 두 색을 한 묶음으로 생각해 보세요."),
            (PuzzleId.LetterAcrostic, GameScreen.LetterAcrostic, "마리의 편지: 각 줄의 시작 부분에 주목해 보세요."),
            (PuzzleId.ToyCipher, GameScreen.ToyCipher, "장난감 암호: 제목에 등장하는 종의 번호를 다시 읽어 보세요."),
            (PuzzleId.StarChart, GameScreen.StarChart, "별자리 도면: 기록에 적힌 걸음의 방향을 생각해 보세요.")
        ];
        foreach ((PuzzleId puzzle, GameScreen screen, string hint) in puzzles.OrderBy(item => item.Screen == _screen ? 0 : 1))
        {
            if (!PuzzleDone(puzzle))
            {
                return hint;
            }
        }

        return _state.ClockRestored
            ? "별시계가 열렸습니다. 선물을 전할지, 태엽으로 쓸지 선택하세요."
            : "별시계: 획득한 기억을 펼쳐 숫자가 새겨진 조각과 다이얼의 이름을 비교해 보세요.";
    }
}
