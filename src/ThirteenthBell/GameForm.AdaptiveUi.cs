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
            if (_gameChromeVisible && _inventory.SolvedCount > 0)
            {
                UpdateInventoryPresentation();
            }
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
            if (_screen == GameScreen.Menu && _actions.TryGetValue("leaderboard_all", out Button? all)
                && _menuScene.Controls.Find("LeaderboardText", false).FirstOrDefault() is Label ranking
                && _baseLayout.TryGetValue(ranking, out LayoutSnapshot rankingLayout))
            {
                Rectangle bounds = rankingLayout.Bounds;
                SetBaseBounds(all, new Rectangle(bounds.X + (bounds.Width - 180) / 2, bounds.Bottom + 18, 180, 42));
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

                if (control is AlignedTextLabel { AutoEllipsis: true })
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
        if (control.Name is "LeaderboardText" or "LetterAcrosticRecord")
        {
            return;
        }
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
        ContentAlignment alignment = _screen == GameScreen.PostalBells
            ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
        _chapterLabel.TextAlign = alignment;
        _notebookText.TextAlign = alignment;
        _hintText.TextAlign = alignment;
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

    private bool HasPersistentInstructions()
    {
        return _screen is GameScreen.PostalLedger or GameScreen.PostalBells
            or GameScreen.Lanterns or GameScreen.Melody or GameScreen.RibbonLoom
            or GameScreen.LetterAcrostic or GameScreen.ToyCipher or GameScreen.StarChart or GameScreen.Clock or GameScreen.Ending;
    }

    private string NextGlobalHint()
    {
        const string none = "제공할 힌트가 없습니다";
        if (_screen == GameScreen.PostalRoom)
        {
            (string Id, string Text, bool Done)[] places =
            [
                ("postal_move_suitcase", "바닥 왼쪽의 가죽 가방을 눌러 보세요.", _postal.SuitcaseMoved),
                ("postal_move_parcel", "바닥 가운데의 붉은 선물 상자를 눌러 보세요.", _postal.ParcelMoved),
                ("postal_move_blanket", "바닥 오른쪽의 초록 담요를 눌러 보세요.", _postal.BellClueFound),
                ("postal_take_key", "짐 아래에서 드러난 황동 열쇠를 눌러 보세요.", _postal.KeyFound || !_postal.KeyVisible),
                ("postal_drawer", "왼쪽 벽의 황동 서랍을 눌러 보세요.", _postal.RouteSolved),
                ("postal_bells", "오른쪽 벽의 네 개의 종을 눌러 보세요.", _postal.BellsSolved),
                ("postal_door", "중앙의 공방 문을 눌러 보세요.", _postal.DoorOpened)
            ];
            return places.Where(place => !place.Done).OrderBy(place => _inspectedLocations.Contains(place.Id))
                .Select(place => place.Text).FirstOrDefault() ?? none;
        }
        if (_screen == GameScreen.PostalLedger)
        {
            return _postal.RouteSolved ? none : "서로 바로 이웃해야 하는 두 장소부터 묶어서 생각해 보세요.";
        }
        if (_screen == GameScreen.PostalBells)
        {
            return _postal.BellsSolved || !_postal.BellClueFound ? none : "방금 울린 종에서 다음 이동을 시작하세요. 오른쪽 끝을 넘으면 왼쪽부터 이어서 셉니다.";
        }
        if (_screen == GameScreen.Room)
        {
            (string Id, string Text, bool Done)[] places =
            [
                ("hotspot_lantern", "왼쪽 책상 아래의 서리 낀 황동 금고를 눌러 보세요.", PuzzleDone(PuzzleId.Lanterns)),
                ("hotspot_desk", "왼쪽 작업대를 눌러 마리의 책상을 가까이 살펴보세요.", PuzzleDone(PuzzleId.LetterAcrostic) && PuzzleDone(PuzzleId.ToyCipher) && PuzzleDone(PuzzleId.StarChart)),
                ("hotspot_melody", "아래쪽의 스노글로브를 눌러 보세요.", PuzzleDone(PuzzleId.Melody)),
                ("hotspot_loom", "양말이 걸린 오른쪽 벽난로를 눌러 보세요.", PuzzleDone(PuzzleId.RibbonLoom)),
                ("hotspot_clock", "중앙의 별시계를 눌러 획득한 기억의 숫자를 맞추세요.", !_state.CanOpenClock || _state.ClockRestored)
            ];
            return places.Where(place => !place.Done).OrderBy(place => _inspectedLocations.Contains(place.Id))
                .Select(place => place.Text).FirstOrDefault() ?? none;
        }
        if (_screen == GameScreen.Desk)
        {
            (PuzzleId Puzzle, string Text)[] places =
            [
                (PuzzleId.LetterAcrostic, "책상 가운데의 봉투를 눌러 마리의 편지를 읽어 보세요."),
                (PuzzleId.ToyCipher, "책상 위쪽 장난감 선반을 눌러 보세요."),
                (PuzzleId.StarChart, "책상 왼쪽 아래의 별자리 도면을 눌러 보세요.")
            ];
            return places.Where(place => !PuzzleDone(place.Puzzle)).Select(place => place.Text).FirstOrDefault() ?? none;
        }
        return _screen switch
        {
            GameScreen.Lanterns when !PuzzleDone(PuzzleId.Lanterns) => "이웃한 숫자끼리 어떤 관계인지 비교해 보세요.",
            GameScreen.Melody when !PuzzleDone(PuzzleId.Melody) => "두 식에서 같은 항을 없애면 장식 사이의 관계를 찾을 수 있습니다.",
            GameScreen.RibbonLoom when !PuzzleDone(PuzzleId.RibbonLoom) => "초록(2)을 가운데 고리에 걸고, 파랑(4)을 빨강(3)보다 오른쪽에 배치해 보세요.",
            GameScreen.Letter when !PuzzleDone(PuzzleId.LetterAcrostic) => "편지를 읽은 뒤 아래의 ‘편지의 봉인 풀기’를 눌러 종잇조각을 확인해 보세요.",
            GameScreen.LetterAcrostic when !PuzzleDone(PuzzleId.LetterAcrostic) => "바늘땀 수는 글자를 고를 위치이고, 조각 번호는 고른 글자를 읽을 순서입니다.",
            GameScreen.ToyCipher when !PuzzleDone(PuzzleId.ToyCipher) => "제목에 등장하는 종의 번호를 다시 읽어 보세요.",
            GameScreen.StarChart when !PuzzleDone(PuzzleId.StarChart) => "기록에 적힌 걸음의 방향을 생각해 보세요.",
            GameScreen.Clock when _state.CanOpenClock && !_state.ClockRestored => "획득한 기억을 펼쳐 숫자가 새겨진 조각과 다이얼의 이름을 비교해 보세요.",
            GameScreen.Choice => "선물을 전달할지, 별시계의 태엽으로 쓸지 선택해 주세요.",
            _ => none
        };
    }
}
