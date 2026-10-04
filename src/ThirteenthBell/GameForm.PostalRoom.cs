using System.Drawing.Drawing2D;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal sealed partial class GameForm
{
    private PostalRoomProgress _postal = new();
    private readonly List<int> _postalBellInput = [];
    private int _postalDraggedProp = -1;
    private Point _postalDragStart;
    private Point _postalDragOffset;
    private Rectangle _postalDragBounds;
    private bool _postalDragMoved;
    private Label? _postalBellProgress;

    private static readonly Rectangle[] PostalPropRestingBounds =
    [
        new(350, 495, 270, 210),
        new(470, 530, 200, 170),
        new(935, 550, 255, 150)
    ];

    private static readonly Rectangle[] PostalPropMovedBounds =
    [
        new(60, 490, 270, 210),
        new(710, 530, 200, 170),
        new(1140, 535, 255, 150)
    ];

    private static readonly Rectangle PostalKeyBounds = new(505, 615, 120, 65);

    private const string PostalBellClue = "첫 종은 가장 왼쪽. 오른쪽으로 1칸, 2칸, 3칸, 4칸, 5칸 이동하며 울려라.\n끝을 넘으면 왼쪽으로 돌아온다. 출발 종을 포함해 모두 여섯 번.";

    private void InitializePostalInteractions()
    {
        _scene.ScenePaint += PaintPostalProps;
        _scene.MouseDown += HandlePostalMouseDown;
        _scene.MouseMove += HandlePostalMouseMove;
        _scene.MouseUp += HandlePostalMouseUp;
        _scene.MouseCaptureChanged += (_, _) =>
        {
            if (!_scene.Capture && _postalDraggedProp >= 0)
            {
                _postalDraggedProp = -1;
                _scene.Invalidate();
            }
        };
    }

    private void ResetPostalTransientState()
    {
        _postalDraggedProp = -1;
        _postalDragMoved = false;
        _postalBellInput.Clear();
        _postalBellProgress = null;
        _scene.Capture = false;
    }

    private static void ResetPostalNarrativeLayout()
    {
        // Narrative geometry is measured from its current text by ArrangeAdaptiveText.
    }

    private void UsePostalNarrativeLayout()
    {
        ArrangeAdaptiveText();
    }

    private void ShowPostalRoom()
    {
        SetScreen(GameScreen.PostalRoom, "서막: 수취인 없는 우편실",
            "배달부 엘리아스의 짐만 남아 있다. 가방과 상자, 담요를 끌거나 눌러 옮겨 보자. 공방 문에는 두 봉인이 걸려 있다.",
            "서랍과 네 개의 종, 중앙 문을 조사하세요. 발견한 물건과 기록은 상단에 남습니다.");
        UsePostalNarrativeLayout();
        _scene.SceneImage = _images["postal-room.png"];
        _scene.Cursor = Cursors.Hand;
        AddHotspot("postal_drawer", "우편실의 황동 서랍 조사", new Rectangle(275, 440, 255, 110), (_, _) => InspectPostalDrawer(), 1, _postal.DrawerOpened);
        AddHotspot("postal_bells", "네 개의 황동 종 조사", new Rectangle(1040, 240, 320, 195), (_, _) => ShowPostalBells(), 2, _postal.BellsSolved);
        AddHotspot("postal_door", "공방으로 이어지는 문 조사", new Rectangle(620, 135, 305, 375), (_, _) => TryOpenPostalDoor(), 3, _postal.DoorOpened);
        // Keyboard access follows the same interactions as the illustrated props.
        AddHotspot("postal_move_suitcase", "바닥의 가죽 가방 옮기기", new Rectangle(0, 0, 1, 1), (_, _) => MovePostalProp(0), 4, false);
        AddHotspot("postal_move_parcel", "바닥의 선물 상자 옮기기", new Rectangle(1, 0, 1, 1), (_, _) => MovePostalProp(1), 5, false);
        AddHotspot("postal_move_blanket", "바닥의 초록 담요 옮기기", new Rectangle(2, 0, 1, 1), (_, _) => MovePostalProp(2), 6, false);
        if (_postal.KeyVisible)
        {
            AddHotspot("postal_take_key", "드러난 황동 열쇠 줍기", new Rectangle(3, 0, 1, 1), (_, _) => TakePostalKey(), 7, false);
        }
        UpdatePostalInventory();
    }

    private Rectangle PostalPropBounds(int prop)
    {
        bool moved = prop switch
        {
            0 => _postal.SuitcaseMoved,
            1 => _postal.ParcelMoved,
            2 => _postal.BlanketMoved,
            _ => throw new ArgumentOutOfRangeException(nameof(prop))
        };
        return moved ? PostalPropMovedBounds[prop] : PostalPropRestingBounds[prop];
    }

    private void PaintPostalProps(object? sender, PaintEventArgs eventArgs)
    {
        if (_screen != GameScreen.PostalRoom)
        {
            return;
        }

        Graphics graphics = eventArgs.Graphics;
        GraphicsState saved = graphics.Save();
        graphics.ScaleTransform(_scene.Width / (float)BaseWidth, _scene.Height / (float)BaseHeight);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        Image atlas = _images["postal-props.png"];
        if (_postal.KeyVisible)
        {
            RectangleF source = new(atlas.Width * 0.52f, atlas.Height * 0.59f, atlas.Width * 0.47f, atlas.Height * 0.23f);
            graphics.DrawImage(atlas, PostalKeyBounds, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel);
        }

        for (int prop = 0; prop < 3; prop++)
        {
            if (prop == _postalDraggedProp)
            {
                continue;
            }
            DrawPostalProp(graphics, atlas, prop, PostalPropBounds(prop));
        }
        if (_postalDraggedProp >= 0)
        {
            DrawPostalProp(graphics, atlas, _postalDraggedProp, _postalDragBounds);
        }
        graphics.Restore(saved);
    }

    private static void DrawPostalProp(Graphics graphics, Image atlas, int prop, Rectangle bounds)
    {
        Rectangle source = new((prop % 2) * atlas.Width / 2, (prop / 2) * atlas.Height / 2, atlas.Width / 2, atlas.Height / 2);
        graphics.DrawImage(atlas, bounds, source, GraphicsUnit.Pixel);
    }

    private Point PostalPoint(MouseEventArgs eventArgs)
    {
        return new Point(
            (int)Math.Round(eventArgs.X * (double)BaseWidth / Math.Max(1, _scene.Width)),
            (int)Math.Round(eventArgs.Y * (double)BaseHeight / Math.Max(1, _scene.Height)));
    }

    private bool CanInteractWithPostalScene => _screen == GameScreen.PostalRoom
        && _confirmationOverlay is null && _transitionOverlay is null;

    private void HandlePostalMouseDown(object? sender, MouseEventArgs eventArgs)
    {
        if (!CanInteractWithPostalScene || eventArgs.Button != MouseButtons.Left)
        {
            return;
        }
        Point point = PostalPoint(eventArgs);
        for (int prop = 2; prop >= 0; prop--)
        {
            Rectangle bounds = PostalPropBounds(prop);
            if (!bounds.Contains(point))
            {
                continue;
            }
            _postalDraggedProp = prop;
            _postalDragStart = point;
            _postalDragOffset = new Point(point.X - bounds.X, point.Y - bounds.Y);
            _postalDragBounds = bounds;
            _postalDragMoved = false;
            _scene.Capture = true;
            break;
        }
    }

    private void HandlePostalMouseMove(object? sender, MouseEventArgs eventArgs)
    {
        if (_postalDraggedProp < 0 || !CanInteractWithPostalScene)
        {
            return;
        }
        Point point = PostalPoint(eventArgs);
        _postalDragMoved |= Math.Abs(point.X - _postalDragStart.X) + Math.Abs(point.Y - _postalDragStart.Y) > 15;
        _postalDragBounds.Location = new Point(
            Math.Clamp(point.X - _postalDragOffset.X, 10, BaseWidth - _postalDragBounds.Width - 10),
            Math.Clamp(point.Y - _postalDragOffset.Y, 450, 705 - _postalDragBounds.Height));
        _scene.Invalidate();
    }

    private void HandlePostalMouseUp(object? sender, MouseEventArgs eventArgs)
    {
        if (!CanInteractWithPostalScene || eventArgs.Button != MouseButtons.Left)
        {
            return;
        }
        if (_postalDraggedProp >= 0)
        {
            int prop = _postalDraggedProp;
            Rectangle original = PostalPropBounds(prop);
            bool moved = !_postalDragMoved
                || Math.Abs(_postalDragBounds.X - original.X) + Math.Abs(_postalDragBounds.Y - original.Y) >= 70;
            _postalDraggedProp = -1;
            _scene.Capture = false;
            if (moved)
            {
                MovePostalProp(prop);
            }
            _scene.Invalidate();
            return;
        }
        Point point = PostalPoint(eventArgs);
        if (_postal.KeyVisible && PostalKeyBounds.Contains(point))
        {
            TakePostalKey();
            return;
        }
        ShowNarrativeMessage(point.X < 480
            ? "봉투의 수취인들이 희미해지고 있다. 엘리아스는 명단이 지워지는 것을 막으려 이곳에서 배달을 멈춘 듯하다."
            : "문틈 너머에서 불빛이 새어 나온다. 멈춘 공방에 들어가려면 우편실의 봉인부터 풀어야 한다.");
        PlayClick();
    }

    private void MovePostalProp(int prop)
    {
        MarkInspected(prop switch
        {
            0 => "postal_move_suitcase",
            1 => "postal_move_parcel",
            2 => "postal_move_blanket",
            _ => throw new ArgumentOutOfRangeException(nameof(prop))
        });
        string message;
        switch (prop)
        {
            case 0:
                _postal.SuitcaseMoved = !_postal.SuitcaseMoved;
                message = _postal.SuitcaseMoved ? "가죽 가방을 옆으로 밀었다. 짐에 가려졌던 바닥이 보이기 시작한다." : "가방을 원래 자리에 내려놓았다.";
                break;
            case 1:
                _postal.ParcelMoved = !_postal.ParcelMoved;
                message = _postal.ParcelMoved ? "선물 상자를 조심스럽게 옮겼다. 이름이 지워진 명찰만 리본에 남아 있다." : "선물 상자를 원래 자리에 놓았다.";
                break;
            case 2:
                _postal.BlanketMoved = !_postal.BlanketMoved;
                if (_postal.BlanketMoved)
                {
                    _postal.BellClueFound = true;
                    message = "담요 안쪽에 엘리아스가 꿰맨 종의 기록이 있다.\n" + PostalBellClue;
                }
                else
                {
                    message = "담요를 다시 접었다. 종의 기록은 이미 옮겨 적어 두었다.";
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(prop));
        }
        if (_postal.KeyVisible)
        {
            message += "\n바닥에 별 모양 황동 열쇠가 드러났다. 직접 눌러 주울 수 있다.";
            if (!_actions.ContainsKey("postal_take_key"))
            {
                AddHotspot("postal_take_key", "드러난 황동 열쇠 줍기", new Rectangle(3, 0, 1, 1), (_, _) => TakePostalKey(), 7, false);
            }
        }
        else if (_actions.Remove("postal_take_key", out Button? keyAction))
        {
            keyAction.Dispose();
        }
        ShowNarrativeMessage(message);
        UpdatePostalInventory();
        _scene.Invalidate();
        PlayClick();
        SaveProgressBackup(reportFailure: false);
    }

    private void TakePostalKey()
    {
        if (!_postal.KeyVisible)
        {
            return;
        }
        _postal.KeyFound = true;
        MarkInspected("postal_take_key");
        if (_actions.Remove("postal_take_key", out Button? keyAction))
        {
            keyAction.Dispose();
        }
        ShowNarrativeMessage("별 모양 황동 열쇠를 주웠다. 왼쪽 서랍과 중앙 문에 같은 별 각인이 있다.");
        UpdatePostalInventory();
        _scene.Invalidate();
        PlaySound(GameSound.PuzzleItem);
        SaveProgressBackup(reportFailure: false);
    }

    private void InspectPostalDrawer()
    {
        if (!_postal.KeyFound)
        {
            ShowNarrativeMessage("황동 서랍은 잠겨 있다. 손잡이 아래의 별 모양 구멍에는 작은 열쇠가 필요하다. 바닥의 짐을 살펴보자.");
            PlaySound(GameSound.Locked);
            return;
        }
        _postal.DrawerOpened = true;
        ShowPostalLedger();
        SaveProgressBackup(reportFailure: false);
    }

    private Label AddPostalText(string name, string text, Rectangle bounds, float size, Color color)
    {
        Label label = Theme.CreateLabel(text, size);
        label.Name = name;
        label.Bounds = bounds;
        label.ForeColor = color;
        label.BackColor = Color.Transparent;
        _scene.Controls.Add(label);
        return label;
    }

    private void ShowPostalLedger()
    {
        if (!_postal.DrawerOpened)
        {
            ShowPostalRoom();
            return;
        }
        SetScreen(GameScreen.PostalLedger, "우편실: 마지막 배송 순서",
            "다섯 장소에 한 번씩 배달한 순서를 찾고, 봉투에 찍힌 숫자를 그 순서대로 입력하세요.",
            "순서와 봉투 숫자는 서로 다른 정보입니다. 이미 푼 배송 봉인은 다시 풀 필요가 없습니다.");
        UsePostalNarrativeLayout();
        _scene.SceneImage = _images["postal-ledger.png"];
        AddPostalText("PostalRouteRecord",
            "엘리아스의 마지막 배송 기록\n\n다리는 교회보다 정확히 두 번째 뒤에 배달한다.\n창가는 다리 바로 전에 배달한다.\n빵집은 창가 뒤에 배달하며 마지막 장소는 아니다.\n공방은 빵집보다 뒤에 배달한다.\n\n봉투의 숫자:  공방 4 / 다리 6 / 교회 8 / 빵집 1 / 창가 3",
            new Rectangle(335, 150, 755, 310), 11.5f, Color.FromArgb(64, 38, 24));
        if (_postal.RouteSolved)
        {
            AddPostalText("PostalRouteSolved", "배송 봉인 해제 완료: 지워진 마지막 수취인은 노엘 애스터였다.", new Rectangle(260, 600, 900, 50), 14, Theme.PaleGold);
        }
        else
        {
            TextBox editor = CreatePuzzleCodeEditor("PostalRouteCode", new Rectangle(490, 590, 220, 62), 5, "배송 순서에 따른 다섯 자리 봉투 숫자");
            _scene.Controls.Add(editor);
            void Submit()
            {
                if (!PostalPuzzleRules.MatchesRouteCode(editor.Text))
                {
                    RejectTextPuzzle(editor, "배송 순서가 기록과 맞지 않는다. 다섯 장소를 한 번씩 놓고, 그 순서대로 봉투 숫자를 읽어 보자.");
                    return;
                }
                _postal.RouteSolved = true;
                ShowPostalRoom();
                ShowNarrativeMessage("배송 봉인이 풀렸다. ‘노엘 애스터, 기다리고 있음.’ 지워진 수취인의 이름이 다시 떠오른다. 종의 봉인도 확인하자.");
                PlaySound(GameSound.PuzzleItem);
                SaveProgressBackup(reportFailure: false);
            }
            AddAction("postal_route_submit", "배송 봉인 풀기", new Rectangle(740, 590, 220, 62), (_, _) => Submit(), 2);
            editor.KeyDown += (_, eventArgs) => SubmitOnEnter(eventArgs, Submit);
            editor.Focus();
        }
    }

    private void ShowPostalBells()
    {
        SetScreen(GameScreen.PostalBells, "우편실: 배달부의 여섯 울림",
            _postal.BellsSolved ? "종의 봉인은 이미 풀렸다. 우편실로 돌아가 중앙 문을 확인하자."
                : "종은 왼쪽부터 1, 2, 3, 4번이다. 배달부의 기록에 따라 여섯 번 울리고 울림을 확인하자.",
            _postal.BellClueFound ? "담요에서 찾은 기록을 위쪽 명판에 펼쳤습니다." : "울림 규칙이 적힌 기록이 없다. 바닥의 짐에서 단서를 찾아야 합니다.");
        UsePostalNarrativeLayout();
        _scene.SceneImage = _images["postal-bells.png"];
        AddPostalText("PostalBellRecord", _postal.BellClueFound ? PostalBellClue : "배달부의 기록을 먼저 찾아야 한다.\n담요에는 무엇이 남아 있을까?", new Rectangle(385, 64, 645, 105), 9.5f, Color.FromArgb(64, 38, 24));
        int[] bellCenters = [350, 575, 805, 1040];
        for (int bell = 0; bell < 4; bell++)
        {
            int selected = bell;
            AddHotspot($"postal_bell_{bell}", $"{bell + 1}번 종 울리기", new Rectangle(bellCenters[bell] - 90, 280, 180, 220), (_, _) => RingPostalBell(selected), bell + 1, _postal.BellsSolved);
            AddPostalText($"PostalBellNumber{bell}", $"{bell + 1}번", new Rectangle(bellCenters[bell] - 35, 490, 85, 35), 13, Theme.PaleGold);
        }
        _postalBellProgress = AddPostalText("PostalBellProgress", _postal.BellsSolved ? "종의 봉인 해제 완료" : "기록한 울림: 0 / 6", new Rectangle(400, 548, 680, 40), 14, Theme.PaleGold);
        if (!_postal.BellsSolved)
        {
            AddAction("postal_bell_check", "울림 확인", new Rectangle(470, 604, 220, 58), (_, _) => CheckPostalBells(), 6);
            AddAction("postal_bell_reset", "울림 지우기", new Rectangle(715, 604, 220, 58), (_, _) => ResetPostalBellInput(), 7);
        }
    }

    private void RingPostalBell(int bell)
    {
        if (_postal.BellsSolved)
        {
            ShowNarrativeMessage("네 개의 종이 이제 자유롭게 울린다. 종의 봉인은 이미 풀렸다.");
            PlayMechanismTone(bell);
            return;
        }
        if (!_postal.BellClueFound)
        {
            ShowNarrativeMessage("종의 태엽이 걸려 있다. 배달부의 기록을 찾으면 울림 장치가 움직일 것이다. 우편실의 담요를 조사하자.");
            PlaySound(GameSound.Locked);
            return;
        }
        if (_postalBellInput.Count >= 6)
        {
            ShowNarrativeMessage("여섯 번의 울림이 기록되었다. 울림을 확인하거나 지우고 다시 시작하자.");
            return;
        }
        _postalBellInput.Add(bell);
        UpdatePostalBellProgress();
        PlayMechanismTone(bell);
    }

    private void UpdatePostalBellProgress()
    {
        if (_postalBellProgress is not null)
        {
            _postalBellProgress.Text = $"기록한 울림: {_postalBellInput.Count} / 6    {string.Join(" → ", _postalBellInput.Select(bell => bell + 1))}";
        }
    }

    private void ResetPostalBellInput()
    {
        _postalBellInput.Clear();
        UpdatePostalBellProgress();
        ShowNarrativeMessage("울림을 지웠다. 가장 왼쪽 종에서 다시 시작할 수 있다.");
    }

    private void CheckPostalBells()
    {
        if (_postalBellInput.Count != 6)
        {
            ShowNarrativeMessage("출발 종을 포함해 여섯 번의 울림이 필요하다. 아직 완성되지 않은 기록은 실패로 세지 않는다.");
            return;
        }
        if (!PostalPuzzleRules.MatchesBellSequence(_postalBellInput))
        {
            _state.RecordFailure();
            _postalBellInput.Clear();
            UpdatePostalBellProgress();
            UpdateHeader();
            ShowNarrativeMessage("종이 서로 어긋나며 봉인이 남았다. 지금 울린 종에서 오른쪽으로 이동하고, 끝을 넘으면 왼쪽으로 이어서 세어 보자.");
            PlaySound(GameSound.Wrong);
            SaveProgressBackup(reportFailure: false);
            return;
        }
        _postal.BellsSolved = true;
        ShowPostalRoom();
        ShowNarrativeMessage("여섯 번째 울림과 함께 종의 봉인이 풀렸다. 엘리아스가 남긴 말이 떠오른다. ‘멈춘 것은 배달이 아니라, 기다림을 지우려는 시간이다.’");
        PlaySound(GameSound.ClockRestored);
        SaveProgressBackup(reportFailure: false);
    }

    private void TryOpenPostalDoor()
    {
        if (!_postal.CanOpenDoor)
        {
            List<string> remaining = [];
            if (!_postal.KeyFound)
            {
                remaining.Add("별 모양 열쇠가 필요하다");
            }
            if (!_postal.RouteSolved)
            {
                remaining.Add("배송 봉인이 남아 있다");
            }
            if (!_postal.BellsSolved)
            {
                remaining.Add("종의 봉인이 남아 있다");
            }
            ShowNarrativeMessage("공방 문이 잠겨 있다. " + string.Join(". ", remaining) + ".");
            PlaySound(GameSound.Locked);
            return;
        }
        _postal.DoorOpened = true;
        ShowRoom();
        ShowNarrativeMessage("별 모양 열쇠를 돌리자 공방 문이 열렸다. 엘리아스가 지키려던 노엘의 이름을 기억하며, 마리의 멈춘 공방으로 들어선다.");
        PlaySound(GameSound.PuzzleItem);
        SaveProgressBackup(reportFailure: false);
    }

    private void UpdatePostalInventory()
    {
        List<string> found = [];
        if (_postal.KeyFound)
        {
            found.Add("별 모양 황동 열쇠");
        }
        if (_postal.DrawerOpened)
        {
            found.Add("엘리아스의 배송 기록");
        }
        if (_postal.BellClueFound)
        {
            found.Add("종의 기록: 오른쪽 1, 2, 3, 4, 5칸, 끝에서 순환");
        }
        if (_postal.RouteSolved)
        {
            found.Add("배송 봉인 해제");
        }
        if (_postal.BellsSolved)
        {
            found.Add("종의 봉인 해제");
        }
        _inventory.SolvedCount = found.Count;
        UpdateInventoryPresentation();
        _inventoryTitle.Text = _inventoryExpanded ? "우편실 기록, 눌러서 접기" : "발견한 물건과 기록";
        _inventoryText.Text = string.Join("\n", found);
        if (_inventoryExpanded)
        {
            SetBaseBounds(_inventory, new Rectangle(30, 78, 1040, 205));
            SetBaseBounds(_inventoryText, new Rectangle(18, 48, 1000, 145));
        }
        _inventory.Visible = _gameChromeVisible && found.Count > 0;
        _roomButton.Visible = _screen is GameScreen.PostalLedger or GameScreen.PostalBells;
    }
}
