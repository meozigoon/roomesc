using System.Drawing.Imaging;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static void CheckRequirements(GameForm form)
    {
        Call(form, "StartNewGame");
        Click(form, "continue");
        Button hint = Get<Button>(form, "_hintButton");
        Check(hint.Enabled && hint.Visible && hint.Parent == Get<SceneCanvas>(form, "_scene"), "global hint immediate and outside narrative");
        Call(form, "ShowHint");
        Check(Get<Label>(form, "_notebookText").Text.Contains("가죽 가방", StringComparison.Ordinal), "first hint points to unseen suitcase");
        Call(form, "MovePostalProp", 0);
        Call(form, "ShowHint");
        Check(Get<Label>(form, "_notebookText").Text.Contains("붉은 선물", StringComparison.Ordinal), "hint skips inspected suitcase");
        HashSet<string> inspected = Get<HashSet<string>>(form, "_inspectedLocations");
        inspected.UnionWith(["postal_move_suitcase", "postal_move_parcel", "postal_move_blanket", "postal_drawer", "postal_bells", "postal_door"]);
        Call(form, "ShowHint");
        Check(Get<Label>(form, "_notebookText").Text.StartsWith("열쇠 찾기:", StringComparison.Ordinal), "all inspected gives subtle unresolved clue");
        Call(form, "SaveProgressBackup", false);
        Check(Get<ProgressBackupStore>(form, "_progressBackupStore").TryLoad(out ProgressBackup? backup, out _) && backup!.InspectedLocations.Contains("postal_drawer"), "inspection history persisted");
        Call(form, "RestoreProgress", backup!);
        Check(Get<HashSet<string>>(form, "_inspectedLocations").Contains("postal_drawer"), "inspection history restored");

        Call(form, "ShowNarrativeMessage", "서랍은 잠겨 있다.");
        Size shortSize = Get<Control>(form, "_sidebar").Size;
        float shortFont = Get<Label>(form, "_notebookText").Font.SizeInPoints;
        Capture(form, "adaptive-short-message");
        string longText = string.Join(" ", Enumerable.Repeat("배달부의 기록에는 잊힌 수취인과 멈춰 버린 시계의 이야기가 적혀 있다. 방 안의 물건을 차례로 조사하고 발견한 기록을 확인하자.", 5));
        Call(form, "ShowNarrativeMessage", longText);
        Check(Get<Control>(form, "_sidebar").Width > shortSize.Width && Get<Control>(form, "_sidebar").Height > shortSize.Height, "long narrative grows in both dimensions");
        Check(Get<Label>(form, "_notebookText").Font.SizeInPoints > shortFont, "short narrative uses smaller font");
        CheckTextFits(form, "NotebookText");
        Capture(form, "adaptive-long-message");
        Get<Control>(form, "_sidebar").Visible = false;
        Check(hint.Visible && hint.Enabled, "global hint survives hidden narrative");
        CheckHotspotBackdrop(form, "postal_door", "postal-hotspot-focused");

        Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        Set(form, "_state", new GameState());
        Get<HashSet<string>>(form, "_inspectedLocations").Clear();
        Call(form, "ShowRoom");
        CheckHotspotBackdrop(form, "hotspot_lantern", "workshop-hotspot-focused");
        Call(form, "ShowHint");
        Check(Get<Label>(form, "_notebookText").Text.Contains("별등 금고", StringComparison.Ordinal), "workshop exploration hint first");
        Get<HashSet<string>>(form, "_inspectedLocations").UnionWith(["hotspot_lantern", "hotspot_desk", "hotspot_melody", "hotspot_loom", "desk_letter", "desk_toys", "desk_chart", "hotspot_clock"]);
        Call(form, "ShowStarChartPuzzle");
        Call(form, "ShowHint");
        Check(Get<Label>(form, "_notebookText").Text.StartsWith("별자리 도면:", StringComparison.Ordinal) && !Get<Label>(form, "_notebookText").Text.Contains("AURORA", StringComparison.Ordinal), "all workshop locations inspected gives current unsolved small hint");

        string[] screens = ["ShowLanternPuzzle", "ShowSnowglobePuzzle", "ShowStockingPuzzle", "ShowLetterAcrosticPuzzle", "ShowToyCipherPuzzle", "ShowStarChartPuzzle", "ShowPostalLedger", "ShowPostalBells"];
        PostalRoomProgress postal = PostalRoomProgress.CompletedLegacyRoom();
        postal.DoorOpened = false;
        postal.RouteSolved = false;
        postal.BellsSolved = false;
        Set(form, "_postal", postal);
        foreach (Size window in new[] { new Size(1400, 820), new Size(1000, 600) })
        {
            form.ClientSize = window;
            foreach (string method in screens)
            {
                Call(form, method);
                Check(Get<Label>(form, "_hintText").Text.Length == 0, method + " no solution footer");
                if (method == "ShowToyCipherPuzzle")
                {
                    Check(!((AtmosphereCard)form.Controls.Find("ToyCipherRecord", true).Single()).BodyLabel.Text.Contains("A ↔ N", StringComparison.Ordinal), "no solved cipher lookup table");
                }
                if (method == "ShowStarChartPuzzle")
                {
                    Check(!((AtmosphereCard)form.Controls.Find("StarChartRecord", true).Single()).BodyLabel.Text.Contains("각 글자를", StringComparison.Ordinal), "no star chart solution footer");
                }
                CheckVisibleText(form, method + "-" + window.Width);
                Capture(form, method + "-" + window.Width);
            }
            Call(form, "ShowNarrativeMessage", longText);
            CheckTextFits(form, "NotebookText");
        }
        form.ClientSize = new Size(1400, 820);
        Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        Set(form, "_state", GameState.Restore(Enum.GetValues<PuzzleId>(), 0, 0, false));
        Call(form, "ShowClockPuzzle");
        Call(form, "ToggleInventoryExpanded");
        CheckTextFits(form, "InventoryText");
        Check(Get<Label>(form, "_hintText").Text.Length == 0, "clock no settings solution footer");
        CheckVisibleText(form, "clock");
        Capture(form, "clock-compact");
        Call(form, "ShowMainMenu", false);
        Call(form, "ShowGuideMenu");
        CheckVisibleText(form, "guide");
        CheckGuideButton(form);
        Capture(form, "guide-compact");
        form.ClientSize = new Size(1000, 600);
        CheckVisibleText(form, "guide-small");
        CheckGuideButton(form);
        Capture(form, "guide-small");
        form.ClientSize = new Size(1400, 820);
        Set(form, "_gameInProgress", true);
        Call(form, "ShowRoom");
        Call(form, "RequestReturnToMainMenu");
        Call(form, "ShowConfirmationError", "진행 백업을 저장하지 못했습니다. 폴더의 쓰기 권한을 확인한 뒤 다시 시도하세요.");
        Label error = (Label)form.Controls.Find("ConfirmationError", true).Single();
        Check(error.Parent!.ClientRectangle.Contains(error.Bounds), "confirmation error stays inside resized card");
        Check(Get<Control>(form, "_stage").Controls.GetChildIndex(Get<Control>(form, "_confirmationOverlay")) == 0, "confirmation overlay remains above game after deferred text layout");
        CheckVisibleText(form, "confirmation");
        Capture(form, "confirmation-compact-error");
        Click(form, "confirmation_cancel");
        foreach (EndingChoice ending in new[] { EndingChoice.DeliverTheGift, EndingChoice.FeedTheClock })
        {
            Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
            Set(form, "_state", GameState.Restore(Enum.GetValues<PuzzleId>(), 0, 0, true));
            Call(form, "ShowEnding", ending);
            CheckVisibleText(form, "ending");
            Capture(form, "ending-" + ending);
            Click(form, "ending_menu");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Menu && Get<SceneCanvas>(form, "_menuScene").Visible, "ending returns to main menu " + ending);
        }
        CheckNicknameUi();
    }

    private static void CheckGuideButton(GameForm form)
    {
        Button button = Get<Dictionary<string, Button>>(form, "_actions")["menu_back"];
        Control menu = Get<Control>(form, "_menuScene");
        int bottom = menu.Controls.OfType<Label>().Max(label => label.Bottom);
        Check(button.Top > bottom && button.Bottom < menu.Height, "guide back button below dynamic text card and inside screen");
    }

    private static void CheckHotspotBackdrop(GameForm form, string id, string capture)
    {
        Get<Control>(form, "_sidebar").Visible = false;
        SceneCanvas scene = Get<SceneCanvas>(form, "_scene");
        Button hotspot = Get<Dictionary<string, Button>>(form, "_actions")[id];
        foreach (Control control in scene.Controls)
        {
            control.Visible = false;
        }
        using Bitmap expected = new(scene.Width, scene.Height);
        scene.DrawToBitmap(expected, scene.ClientRectangle);
        hotspot.Visible = true;
        hotspot.Focus();
        typeof(Control).GetMethod("OnMouseEnter", PrivateInstance)!.Invoke(hotspot, [EventArgs.Empty]);
        using Bitmap actual = new(scene.Width, scene.Height);
        scene.DrawToBitmap(actual, scene.ClientRectangle);
        int differences = 0;
        for (int y = hotspot.Top + 12; y < hotspot.Bottom - 12; y += 9)
        {
            for (int x = hotspot.Left + 12; x < hotspot.Right - 12; x += 9)
            {
                if (expected.GetPixel(x, y).ToArgb() != actual.GetPixel(x, y).ToArgb())
                {
                    differences++;
                }
            }
        }
        Check(differences == 0, id + " transparent hotspot matches original scene pixels under focus and hover");
        actual.Save(Path.Combine(_output, capture + ".png"), ImageFormat.Png);
        Call(form, "UpdateHintAvailability");
    }

    private static void CheckVisibleText(GameForm form, string screen)
    {
        Audit(Get<Control>(form, "_stage"));
        void Audit(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Visible && control is Label label && !string.IsNullOrEmpty(label.Text))
                {
                    Size measured = TextRenderer.MeasureText(label.Text, label.Font,
                        new Size(Math.Max(1, label.Width - label.Padding.Horizontal), int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
                    Check(measured.Height <= label.Height - label.Padding.Vertical && measured.Width <= label.Width - label.Padding.Horizontal,
                        screen + " fits " + label.Name);
                }
                if (control.Visible)
                {
                    Audit(control);
                }
            }
        }
    }

    private static void CheckNicknameUi()
    {
        using NicknameFixture service = new();
        using GameForm form = new(service, animationsEnabled: false, audioEnabled: false);
        form.WindowState = FormWindowState.Normal;
        form.ClientSize = new Size(1400, 820);
        form.Show();
        PumpUntil(() => Get<bool>(form, "_startupSequenceCompleted"), TimeSpan.FromSeconds(30));
        Call(form, "ShowNicknameSetup");
        CheckVisibleText(form, "nickname setup");
        TextBox editor = (TextBox)form.Controls.Find("NicknameEditor", true).Single();
        Label status = (Label)form.Controls.Find("NicknameStatus", true).Single();
        editor.Text = "Existing";
        Click(form, "nickname_confirm");
        Check(Get<GameScreen>(form, "_screen") == GameScreen.Menu && status.Text.Contains("이미", StringComparison.Ordinal), "taken nickname keeps setup screen and reports error");
        Check(editor.Enabled && Get<Dictionary<string, Button>>(form, "_actions")["nickname_confirm"].Enabled, "duplicate check re-enables controls");
        Check(editor.Text.Length == 0 && editor.ContainsFocus, "duplicate nickname clears input and returns keyboard focus");
        service.Status = NicknameReservationStatus.Unavailable;
        editor.Text = "Retry Name";
        Click(form, "nickname_confirm");
        Check(Get<GameScreen>(form, "_screen") == GameScreen.Menu && editor.Enabled, "network failure cannot start game");
        editor.Text = "x";
        int before = service.Calls;
        Click(form, "nickname_confirm");
        Check(service.Calls == before, "invalid short nickname rejected before network call");
        service.Pending = new TaskCompletionSource<NicknameReservationResult>();
        editor.Text = "Pending Name";
        Click(form, "nickname_confirm");
        Check(!editor.Enabled && !Get<Dictionary<string, Button>>(form, "_actions")["nickname_confirm"].Enabled, "verification in flight disables repeated submission");
        Click(form, "nickname_back");
        service.Pending.SetResult(new NicknameReservationResult(NicknameReservationStatus.Reserved, "Pending Name", null));
        Pump();
        Check(Get<GameScreen>(form, "_screen") == GameScreen.Menu, "late nickname response cannot start game after leaving setup");
        service.Pending = null;
        Call(form, "ShowNicknameSetup");
        editor = (TextBox)form.Controls.Find("NicknameEditor", true).Single();
        service.Status = NicknameReservationStatus.Reserved;
        editor.Text = "  New   Name ";
        Click(form, "nickname_confirm");
        Check(service.LastNickname == "New Name" && Get<GameScreen>(form, "_screen") == GameScreen.Intro, "normalized available nickname starts game");
        Set(form, "_gameInProgress", false);
        Set(form, "_allowClose", true);
        form.Close();
    }

    private sealed class NicknameFixture : ILeaderboardService
    {
        public NicknameReservationStatus Status { get; set; } = NicknameReservationStatus.Taken;
        public int Calls { get; private set; }
        public string? LastNickname { get; private set; }
        public TaskCompletionSource<NicknameReservationResult>? Pending { get; set; }
        public Task<LeaderboardLoadResult> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new LeaderboardLoadResult([], null));
        public Task<NicknameReservationResult> ReserveNicknameAsync(string nickname, CancellationToken cancellationToken)
        {
            Calls++;
            LastNickname = nickname;
            if (Pending is not null)
            {
                return Pending.Task;
            }
            return Task.FromResult(new NicknameReservationResult(Status, Status == NicknameReservationStatus.Reserved ? nickname : null,
                Status == NicknameReservationStatus.Taken ? "이미 사용 중인 닉네임입니다." : "서버에 연결할 수 없습니다."));
        }
        public Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, CancellationToken cancellationToken) => Task.FromResult(new ScoreSubmissionResult(true, 1000, 1, null));
        public void Dispose()
        {
        }
    }
}
