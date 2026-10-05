using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static readonly int[] IncorrectRepeatedBells = [0, 0, 0, 0, 0, 0];
    private static readonly int[] IncorrectStockingOrder = [3, 1, 2, 0];
    private static void CheckRunChanges(GameForm form)
    {
        GameState state = new();
        Check(state.AvailableHints(TimeSpan.Zero) == 1, "one initial hint");
        Check(state.TryUseHint(TimeSpan.Zero) && !state.TryUseHint(TimeSpan.Zero), "only one initial hint usable");
        Check(!state.TryUseHint(TimeSpan.FromSeconds(179.999)) && state.HintCount == 1, "no hint before three minutes");
        Check(state.AvailableHints(TimeSpan.FromSeconds(180)) == 1 && state.TryUseHint(TimeSpan.FromSeconds(180)), "one hint exactly at three minutes");
        Check(state.AvailableHints(TimeSpan.FromSeconds(359.999)) == 0, "second recharge waits until six minutes");
        Check(state.AvailableHints(TimeSpan.FromMinutes(12)) == 3, "unused recharges accumulate");
        Check(GameState.Restore([], 3, 0, false).AvailableHints(TimeSpan.FromMinutes(12)) == 2, "restored hints retain earned and spent balance");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            state.RecordFailure();
        }
        Check(!state.ClearFailed, "ten failures may continue");
        state.RecordFailure();
        Check(state.ClearFailed, "eleventh failure ends run");
        state.RecordFailure();
        Check(state.FailedAttempts == 11 && !state.TryUseHint(TimeSpan.FromDays(1)), "terminal failure cannot add attempts or use hints");

        Call(form, "StartNewGame");
        Click(form, "continue");
        Get<System.Diagnostics.Stopwatch>(form, "_gameStopwatch").Stop();
        Call(form, "ShowHint");
        Call(form, "ShowHint");
        Check(Get<GameState>(form, "_state").HintCount == 1 && !Get<Button>(form, "_hintButton").Enabled, "hint button disables after one use");
        Call(form, "ShowHint");
        Check(Get<GameState>(form, "_state").HintCount == 1, "keyboard cannot spend unavailable hint");
        Set(form, "_elapsedBeforeSession", TimeSpan.FromSeconds(180));
        Call(form, "UpdateHintAvailability");
        Check(Get<Button>(form, "_hintButton").Enabled, "hint button re-enables on recharge");
        Call(form, "ShowHint");
        Check(Get<GameState>(form, "_state").HintCount == 2 && Get<TimeSpan>(form, "_narrativeVisibleDuration").TotalSeconds == 7, "hint uses charge and remains seven seconds");
        Set(form, "_state", new GameState());
        Call(form, "ShowLanternPuzzle");
        string instructions = Get<Label>(form, "_notebookText").Text;
        Call(form, "ShowHint");
        Set(form, "_narrativeShownAt", DateTimeOffset.UtcNow.AddSeconds(-6));
        Call(form, "HandleNarrativeFadeTick", form, EventArgs.Empty);
        Check(Get<Label>(form, "_hintText").Text.Length > 0, "puzzle hint remains for six seconds");
        Set(form, "_narrativeShownAt", DateTimeOffset.UtcNow.AddSeconds(-8));
        Call(form, "HandleNarrativeFadeTick", form, EventArgs.Empty);
        Check(Get<Label>(form, "_hintText").Text.Length == 0 && Get<Label>(form, "_notebookText").Text == instructions
            && Get<Control>(form, "_sidebar").Visible, "hint disappears after seven seconds while puzzle instructions remain");
        Call(form, "ShowPostalRoom");
        Call(form, "ShowNarrativeMessage", "배경 반응");
        Check(Get<TimeSpan>(form, "_narrativeVisibleDuration").TotalSeconds == 3, "insignificant reaction retains three seconds");
        Call(form, "MovePostalProp", 2);
        Check(Get<TimeSpan>(form, "_narrativeVisibleDuration").TotalSeconds == 7, "discovered bell clue stays seven seconds");
        Set(form, "_narrativeShownAt", DateTimeOffset.UtcNow.AddSeconds(-6));
        Cursor.Position = new Point(0, 0);
        Call(form, "HandleNarrativeFadeTick", form, EventArgs.Empty);
        Check(Get<Control>(form, "_sidebar").Visible, "progression message still visible after six seconds");
        Set(form, "_narrativeShownAt", DateTimeOffset.UtcNow.AddSeconds(-9));
        Call(form, "HandleNarrativeFadeTick", form, EventArgs.Empty);
        Check(!Get<Control>(form, "_sidebar").Visible, "progression message fades after seven seconds");

        int[] centers = [354, 584, 818, 1046];
        foreach (Size size in new[] { new Size(1400, 820), new Size(1000, 600), new Size(1680, 1000) })
        {
            form.ClientSize = size;
            Set(form, "_inventoryExpanded", false);
            Set(form, "_postal", new PostalRoomProgress { BellClueFound = true, KeyFound = true });
            Call(form, "ShowPostalBells");
            SceneCanvas scene = Get<SceneCanvas>(form, "_scene");
            Label record = (Label)scene.Controls.Find("PostalBellRecord", false).Single();
            Check(record.TextAlign == ContentAlignment.MiddleCenter && Get<Label>(form, "_notebookText").TextAlign == ContentAlignment.MiddleCenter, "bell instructions centered at " + size.Width);
            for (int bell = 0; bell < 4; bell++)
            {
                Control hotspot = scene.Controls.Find("postal_bell_" + bell, false).Single();
                Label number = (Label)scene.Controls.Find("PostalBellNumber" + bell, false).Single();
                Check(Math.Abs((hotspot.Left + hotspot.Width / 2) - (number.Left + number.Width / 2)) <= 1, "bell number aligned with hit region " + bell + " at " + size.Width);
                Check(number.Text == $"{bell + 1}번" && number.TextAlign == ContentAlignment.MiddleCenter, "bell number correct and centered " + bell);
                Check(Math.Abs((hotspot.Left + hotspot.Width / 2) - centers[bell] * scene.Width / 1400f) <= 1, "bell hit region matches artwork center " + bell);
                Click(form, "postal_bell_" + bell);
                Check(Get<List<int>>(form, "_postalBellInput")[bell] == bell, "bell click records displayed number " + bell);
            }
            CheckTextFits(form, "PostalBellRecord");
            Call(form, "ToggleInventoryExpanded");
            Control inventory = Get<Control>(form, "_inventory");
            Label timer = Get<Label>(form, "_timerLabel");
            Check(Math.Abs(inventory.Right - timer.Right) <= 1 && inventory.Top > timer.Bottom, "acquired records under time and right aligned " + size.Width);
            Check(!inventory.Bounds.IntersectsWith(record.Bounds), "expanded records do not cover bell instructions " + size.Width);
            Check(Get<Label>(form, "_inventoryTitle").TextAlign == ContentAlignment.MiddleRight
                && Get<Label>(form, "_inventoryText").TextAlign == ContentAlignment.MiddleRight, "acquired record text right aligned");
            CheckTextFits(form, "InventoryText");
            Capture(form, "run-bells-inventory-" + size.Width);
        }
        Panel toolbar = Get<Panel>(form, "_header");
        Check(toolbar.Width < Get<Control>(form, "_stage").Width * 0.35, "hover toolbar compact");
        Button[] buttons = toolbar.Controls.OfType<Button>().OrderBy(button => button.Left).ToArray();
        Check(buttons.Length == 3 && buttons.Zip(buttons.Skip(1), (left, right) => right.Left - left.Right).All(gap => gap >= 0 && gap < 40), "toolbar has three evenly spaced short buttons");
        Cursor.Position = Get<Control>(form, "_stage").PointToScreen(new Point(10, 5));
        Call(form, "UpdateHeaderRevealFromPointer");
        Check(toolbar.Visible, "hovering top edge reveals compact menu");
        Capture(form, "run-compact-toolbar");
        if (Form.ActiveForm == form)
        {
            using Bitmap desktop = new(form.ClientSize.Width, form.ClientSize.Height);
            using Graphics graphics = Graphics.FromImage(desktop);
            graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
            desktop.Save(Path.Combine(_output, "run-desktop-toolbar.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        form.ClientSize = new Size(1400, 820);
        foreach (string kind in new[] { "text", "bells", "stockings", "clock" })
        {
            Call(form, "StartNewGame");
            Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
            Set(form, "_state", GameState.Restore(kind == "clock" ? Enum.GetValues<PuzzleId>() : [], 0, 10, false));
            if (kind == "text")
            {
                Call(form, "ShowLanternPuzzle");
                ((TextBox)form.Controls.Find("FrostVaultCode", true).Single()).Text = "001";
                Click(form, "frost_vault_confirm");
            }
            else if (kind == "bells")
            {
                Get<PostalRoomProgress>(form, "_postal").BellsSolved = false;
                Call(form, "ShowPostalBells");
                Get<List<int>>(form, "_postalBellInput").AddRange(IncorrectRepeatedBells);
                Call(form, "CheckPostalBells");
            }
            else if (kind == "stockings")
            {
                Call(form, "ShowStockingPuzzle");
                Set(form, "_stockingPlacement", IncorrectStockingOrder);
                Call(form, "EvaluateStockingArrangement");
            }
            else
            {
                Call(form, "ShowClockPuzzle");
                Click(form, "clock_confirm");
            }
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Ending && Get<GameState>(form, "_state").FailedAttempts == 11, kind + " eleventh failure displays terminal screen");
            Check(!Get<bool>(form, "_gameInProgress") && !Get<System.Diagnostics.Stopwatch>(form, "_gameStopwatch").IsRunning, kind + " failed run stops clock");
            Check(!Get<ProgressBackupStore>(form, "_progressBackupStore").TryLoad(out ProgressBackup? ended, out _) || ended is null, kind + " failed run has no recoverable backup");
            Check(form.Controls.Find("FailureDocument", true).Length == 1 && !Get<Button>(form, "_hintButton").Visible, kind + " terminal controls replace puzzle");
            CheckVisibleText(form, kind + " failure screen");
            if (kind == "clock")
            {
                Capture(form, "run-eleventh-failure");
            }
        }
        Set(form, "_gameInProgress", false);
        OfflineLeaderboard rankingService = (OfflineLeaderboard)Get<ILeaderboardService>(form, "_leaderboardService");
        rankingService.EntryCount = 4;
        rankingService.FailedCount = 2;
        Call(form, "ShowMainMenu", false);
        Label ranking = (Label)form.Controls.Find("LeaderboardText", true).Single();
        Check(ranking.Controls.Find("RankingRow4Column0", false).Single().Text == "6위"
            && ranking.Controls.Find("RankingRow4Column1", false).Single().Text == "Failed1"
            && ranking.Controls.Find("RankingRow4Column2", false).Single().Text == "실패", "menu failure has last rank and failure status");
        Check(!ranking.Controls.Cast<Control>().Any(cell => cell.Text == "Failed2"), "menu still limits ranking to five rows");
        Click(form, "leaderboard_all");
        ListView full = (ListView)form.Controls.Find("FullLeaderboard", true).Single();
        Check(full.Items.Count == 6 && full.Items[4].SubItems[0].Text == "6위" && full.Items[5].SubItems[0].Text == "6위", "failed rows share last rank in full list");
        Check(full.Items[4].SubItems[2].Text == "실패" && full.Items[5].SubItems[2].Text == "실패", "full ranking replaces failed time with status");
        Capture(form, "run-failed-ranking");
        rankingService.EntryCount = 0;
        rankingService.FailedCount = 0;
        Call(form, "ShowMainMenu", false);
        CheckOutcomePersistence();
        using (GameForm titleForm = new(new OfflineLeaderboard(), animationsEnabled: true, audioEnabled: false))
        {
            titleForm.WindowState = FormWindowState.Normal;
            titleForm.ClientSize = new Size(1000, 600);
            System.Diagnostics.Stopwatch hold = System.Diagnostics.Stopwatch.StartNew();
            titleForm.Show();
            PumpUntil(() => hold.ElapsedMilliseconds >= 3200, TimeSpan.FromSeconds(5));
            Check(!Get<bool>(titleForm, "_startupSequenceCompleted")
                && Get<StartupTitleOverlay?>(titleForm, "_startupTitleOverlay") is { FadeProgress: 0 }, "title and creator remain fully visible after three seconds");
            Capture(titleForm, "run-startup-title");
            PumpUntil(() => Get<bool>(titleForm, "_startupSequenceCompleted"), TimeSpan.FromSeconds(10));
            Check(hold.ElapsedMilliseconds >= 4000, "title hold lasts four seconds before menu");
            Set(titleForm, "_allowClose", true);
            titleForm.Close();
        }
        File.WriteAllText(Path.Combine(_output, "run-changes-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks }, ResultJsonOptions));
        Console.WriteLine($"RUN_CHANGES_OK checks={_checks}");
    }

    private static void CheckOutcomePersistence()
    {
        string? previous = Environment.GetEnvironmentVariable("THIRTEENTH_BELL_DATA_DIR");
        string directory = Path.Combine(_output, "outcome-player-data");
        Environment.SetEnvironmentVariable("THIRTEENTH_BELL_DATA_DIR", directory);
        PlayerDataStore store = new(directory);
        ProgressBackupStore backups = new(directory);
        try
        {
            Check(store.TrySave(new PlayerData { TutorialSeen = true, Nickname = "OutcomeTest", OnlineClaimToken = "run-one" }, out _), "outcome test identity saved");
            TrackingOutcomeLeaderboard tracker = new();
            using (GameForm active = CreateOutcomeForm(tracker))
            {
                Call(active, "StartNewGame");
                Click(active, "continue");
                Call(active, "RequestReturnToMainMenu");
                Click(active, "confirmation_cancel");
                Check(tracker.Tokens.Count == 0 && Get<bool>(active, "_gameInProgress"), "canceling abandonment keeps active run");
                Call(active, "RequestReturnToMainMenu");
                Click(active, "confirmation_accept");
                Check(tracker.Tokens.SequenceEqual(["run-one"]) && !Get<bool>(active, "_gameInProgress"), "abandoning menu queues failure for original run");
                Check(store.TryLoad(out PlayerData queued, out _) && queued.PendingFailureTokens.SequenceEqual(["run-one"]), "offline failure persists on disk");
                queued.OnlineClaimToken = "run-two";
                Check(store.TrySave(queued, out _), "new identity preserves old pending failure");
                Set(active, "_allowClose", true);
                active.Close();
            }
            tracker.Succeed = true;
            using (GameForm retry = CreateOutcomeForm(tracker))
            {
                Check(tracker.Tokens.Last() == "run-one", "startup retries original failed claim after identity changes");
                Check(store.TryLoad(out PlayerData saved, out _) && saved.PendingFailureTokens.Count == 0, "acknowledged failure removes outbox item");
                Call(retry, "StartNewGame");
                Call(retry, "ConfirmCloseWithBackup");
                Check(backups.TryLoad(out ProgressBackup? backup, out _) && backup?.OnlineClaimToken == "run-two", "backup exit saves run identity");
                Check(tracker.Tokens.Count == 2, "backup exit does not register failure");
            }
            using (GameForm discard = CreateOutcomeForm(tracker))
            {
                Click(discard, "resume_discard");
                Check(tracker.Tokens.Last() == "run-two" && tracker.Tokens.Count == 3, "declining recovery submits failure");
                Set(discard, "_allowClose", true);
                discard.Close();
            }
            PlayerData last = new() { TutorialSeen = true, Nickname = "ExitTest", OnlineClaimToken = "run-three" };
            store.TrySave(last, out _);
            using (GameForm exit = CreateOutcomeForm(tracker))
            {
                Call(exit, "StartNewGame");
                Call(exit, "ConfirmCloseWithoutBackup");
                PumpUntil(() => !exit.Visible, TimeSpan.FromSeconds(5));
                Check(tracker.Tokens.Last() == "run-three", "exit without backup submits failure before closing");
                Check(backups.TryLoad(out ProgressBackup? backup, out _) && backup is null, "exit without backup removes recoverable progress");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("THIRTEENTH_BELL_DATA_DIR", previous);
        }
    }

    private static GameForm CreateOutcomeForm(TrackingOutcomeLeaderboard tracker)
    {
        GameForm form = new(tracker, animationsEnabled: false, audioEnabled: false);
        form.WindowState = FormWindowState.Normal;
        form.ClientSize = new Size(1000, 600);
        form.Show();
        PumpUntil(() => Get<bool>(form, "_startupSequenceCompleted"), TimeSpan.FromSeconds(10));
        return form;
    }

    private sealed class TrackingOutcomeLeaderboard : ILeaderboardService
    {
        internal List<string> Tokens { get; } = [];
        internal bool Succeed { get; set; }
        public Task<LeaderboardLoadResult> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new LeaderboardLoadResult([], null));
        public Task<NicknameReservationResult> ReserveNicknameAsync(string nickname, CancellationToken cancellationToken) => Task.FromResult(new NicknameReservationResult(NicknameReservationStatus.Reserved, nickname, null));
        public Task<ScoreSubmissionResult> SubmitClearAsync(EndingChoice ending, CancellationToken cancellationToken) => Task.FromResult(new ScoreSubmissionResult(true, 1000, 1, null));
        public Task<ScoreSubmissionResult> SubmitFailureAsync(string claimToken, CancellationToken cancellationToken)
        {
            Check(!cancellationToken.CanBeCanceled, "committed failure independent of screen cancellation");
            Tokens.Add(claimToken);
            return Task.FromResult(new ScoreSubmissionResult(Succeed, 0, Succeed ? 13 : 0, Succeed ? null : "offline fixture"));
        }
        public void Dispose() { }
    }
}
