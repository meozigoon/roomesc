using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static readonly List<string> Findings = [];

    private static void Observe(bool condition, string label)
    {
        if (condition)
        {
            Check(true, label);
        }
        else
        {
            Findings.Add(label);
            Console.WriteLine("FINDING " + label);
        }
    }

    private static void CheckComprehensive(GameForm form)
    {
        CheckPersistence();
        CheckCore();
        CheckRecovery(form);
        Call(form, "ShowMainMenu", false);
        Click(form, "menu_credits");
        CheckVisibleText(form, "credits");
        Click(form, "menu_back");
        Call(form, "ShowFirstRunGuide");
        CheckVisibleText(form, "first run guide");
        Click(form, "tutorial_continue");
        Check(Get<PlayerData>(form, "_playerData").TutorialSeen, "tutorial persisted");

        foreach (EndingChoice ending in new[] { EndingChoice.DeliverTheGift, EndingChoice.FeedTheClock })
        {
            Call(form, "StartNewGame");
            Click(form, "continue");
            Call(form, "MovePostalProp", 0);
            Call(form, "MovePostalProp", 1);
            Call(form, "TakePostalKey");
            Call(form, "MovePostalProp", 2);
            Click(form, "postal_drawer");
            EnterCode(form, "PostalRouteCode", "postal_route_submit", "83614");
            Click(form, "completion_return");
            Click(form, "postal_bells");
            foreach (int bell in new[] { 0, 1, 3, 2, 2, 3 })
            {
                Click(form, $"postal_bell_{bell}");
            }
            Click(form, "postal_bell_check");
            Click(form, "completion_return");
            Click(form, "postal_door");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Room, "full route reaches workshop " + ending);
            Call(form, "ShowClockPuzzle");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Room, "clock locked before six puzzles");
            int beforeHint = Get<GameState>(form, "_state").HintCount;
            Call(form, "ProcessCmdKey", new Message(), Keys.F1);
            Check(Get<GameState>(form, "_state").HintCount == beforeHint + 1, "F1 records hint");
            Call(form, "ShowLanternPuzzle");
            EnterCode(form, "FrostVaultCode", "frost_vault_confirm", "000");
            Check(Get<GameState>(form, "_state").FailedAttempts == 1, "wrong vault counted");
            EnterCode(form, "FrostVaultCode", "frost_vault_confirm", "161", enter: true);
            CompleteReturn(form, PuzzleId.Lanterns, GameScreen.Room);
            Call(form, "ShowSnowglobePuzzle");
            EnterCode(form, "OrnamentEquationCode", "ornament_equation_confirm", "44");
            CompleteReturn(form, PuzzleId.Melody, GameScreen.Room);
            Call(form, "ShowStockingPuzzle");
            StockingPiece[] pieces = Get<StockingPiece[]>(form, "_stockingPieces");
            StockingHookSlot[] slots = Get<StockingHookSlot[]>(form, "_stockingSlots");
            Check(!pieces.OrderBy(piece => piece.Left).Select(piece => piece.StockingId).SequenceEqual([0, 1, 2, 3]), "stockings initial order shuffled");
            int fails = Get<GameState>(form, "_state").FailedAttempts;
            int[] wrongOrder = [3, 1, 2, 0];
            for (int i = 0; i < 4; i++)
            {
                DropPiece(pieces[wrongOrder[i]], slots[i]);
            }
            Check(Get<GameState>(form, "_state").FailedAttempts == fails + 1, "wrong full stocking order counts once");
            Call(form, "EvaluateStockingArrangement");
            Check(Get<GameState>(form, "_state").FailedAttempts == fails + 1, "unchanged wrong order not counted twice");
            // Drop outside a hook, then complete with mouse event handlers at this window scale.
            DropPiece(pieces[0], null);
            Check(Get<int[]>(form, "_stockingPieceSlots")[0] == -1, "outside drop returns stocking home");
            for (int i = 0; i < 4; i++)
            {
                DropPiece(pieces[i], slots[i]);
            }
            CompleteReturn(form, PuzzleId.RibbonLoom, GameScreen.Room);
            Call(form, "ShowDesk");
            Call(form, "ShowMarieLetter");
            Capture(form, "letter-reveal-" + ending);
            Call(form, "ShowLetterAcrosticPuzzle");
            EnterCode(form, "LetterAcrosticCode", "letter_acrostic_confirm", "CHIMNEY", enter: true);
            CompleteReturn(form, PuzzleId.LetterAcrostic, GameScreen.Desk);
            Call(form, "ShowToyCipherPuzzle");
            EnterCode(form, "ToyCipherCode", "toy_cipher_confirm", "bad");
            TextBox badInput = (TextBox)form.Controls.Find("ToyCipherCode", true).Single();
            Check(badInput.Text.Length == 0 && badInput.ContainsFocus, "wrong cipher clears and focuses input");
            EnterCode(form, "ToyCipherCode", "toy_cipher_confirm", "MIDNIGHT", enter: true);
            CompleteReturn(form, PuzzleId.ToyCipher, GameScreen.Desk);
            Call(form, "ShowStarChartPuzzle");
            EnterCode(form, "StarChartCode", "star_chart_confirm", "aurora");
            CompleteReturn(form, PuzzleId.StarChart, GameScreen.Desk);
            Check(Get<GameState>(form, "_state").CanOpenClock, "six actual solutions unlock clock");
            Call(form, "ShowClockPuzzle");
            Click(form, "clock_confirm");
            Check(!Get<GameState>(form, "_state").ClockRestored, "wrong dials leave clock locked");
            ((NumericUpDown)form.Controls.Find("ClockHour", true).Single()).Value = 12;
            ((NumericUpDown)form.Controls.Find("ClockMinute", true).Single()).Value = 0;
            ((NumericUpDown)form.Controls.Find("ClockDate", true).Single()).Value = 25;
            Click(form, "clock_confirm");
            Check(Get<GameScreen>(form, "_screen") == GameScreen.Choice, "correct dials open final choice");
            Click(form, ending == EndingChoice.DeliverTheGift ? "choice_gift" : "choice_clock");
            Check(Get<GameState>(form, "_state").Ending == ending, "actual final button chooses ending " + ending);
            Check(Get<Label>(form, "_hintText").Text.Contains("현재 순위", StringComparison.Ordinal), "ending displays stub score result");
            Check(!File.Exists(Get<ProgressBackupStore>(form, "_progressBackupStore").FilePath), "ending discards progress backup");
            Capture(form, "full-ending-" + ending);
            Click(form, "ending_menu");
        }

        Call(form, "StartNewGame");
        Click(form, "continue");
        Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        Call(form, "ShowRoom");
        for (int i = 0; i < 5; i++)
        {
            Call(form, "InspectRoomPoint", new Point(1300, 80));
            Check(Get<bool>(form, "_treeEasterEggFound") == (i == 4), "tree easter egg click " + (i + 1));
        }
        Check(Get<GameState>(form, "_state").SolvedPuzzles.Count == 0, "easter egg does not solve progression");
        Call(form, "ShowNarrativeMessage", "페이드 점검");
        Point oldPointer = Cursor.Position;
        try
        {
            Cursor.Position = form.PointToScreen(new Point(950, 100));
            Set(form, "_narrativeShownAt", DateTimeOffset.UtcNow.AddSeconds(-5));
            Call(form, "HandleNarrativeFadeTick", form, EventArgs.Empty);
            Check(!Get<Control>(form, "_sidebar").Visible, "normal narrative fades");
            Call(form, "UpdateHeaderRevealFromPointer");
            Check(!Get<Control>(form, "_header").Visible, "toolbar hidden away from top");
            Cursor.Position = Get<Control>(form, "_stage").PointToScreen(new Point(600, 5));
            Call(form, "UpdateHeaderRevealFromPointer");
            Check(Get<Control>(form, "_header").Visible, "toolbar revealed by actual pointer");
        }
        finally
        {
            Cursor.Position = oldPointer;
        }
        Call(form, "ToggleSound");
        Check(Get<bool>(form, "_soundEnabled"), "sound toggle on");
        Call(form, "ToggleSound");
        Check(!Get<bool>(form, "_soundEnabled"), "sound toggle off");
        Set(form, "_fullscreen", false);
        Call(form, "ProcessCmdKey", new Message(), Keys.F11);
        Check(Get<bool>(form, "_fullscreen"), "F11 full screen");
        Call(form, "ProcessCmdKey", new Message(), Keys.F11);
        Check(!Get<bool>(form, "_fullscreen"), "F11 window mode");
        form.ClientSize = new Size(1000, 600);
        Call(form, "ShowLanternPuzzle");
        Call(form, "ProcessCmdKey", new Message(), Keys.Control | Keys.N);
        Check(Get<Control>(form, "_confirmationOverlay").Visible, "Ctrl+N confirms abandonment");
        Check(!Get<Control>(form, "_scene").Enabled, "modal disables scene");
        Capture(form, "modal-before-escape");
        Call(form, "ProcessCmdKey", new Message(), Keys.Escape);
        Observe(Get<GameScreen>(form, "_screen") == GameScreen.Lanterns, "Escape must not navigate underlying screen while confirmation is open");
        Observe(Get<Control?>(form, "_confirmationOverlay") is null, "Escape cancels confirmation");
        Check(!Get<Dictionary<string, Button>>(form, "_actions").ContainsKey("confirmation_cancel"), "closed confirmation removes its actions");
        Call(form, "RequestNewGame");
        int hintCount = Get<GameState>(form, "_state").HintCount;
        Call(form, "ProcessCmdKey", new Message(), Keys.F1);
        Check(Get<GameState>(form, "_state").HintCount == hintCount, "confirmation blocks hint shortcut");
        Click(form, "confirmation_cancel");
        Call(form, "RequestReturnToMainMenu");
        Click(form, "confirmation_cancel");
        Check(Get<bool>(form, "_gameInProgress"), "cancel preserves session");
        Call(form, "RequestReturnToMainMenu");
        Click(form, "confirmation_accept");
        Check(!Get<bool>(form, "_gameInProgress"), "menu abandonment ends session");
        Check(!File.Exists(Get<ProgressBackupStore>(form, "_progressBackupStore").FilePath), "abandon deletes backup");
        Check(!Get<bool>(form, "_treeEasterEggFound"), "abandon clears easter egg");
        Call(form, "StartNewGame");
        Click(form, "continue");
        form.Close();
        Check(!form.IsDisposed && Get<Control>(form, "_confirmationOverlay").Visible, "close active game shows prompt");
        Click(form, "confirmation_cancel");
        Call(form, "TryPeriodicProgressBackup");
        Check(File.Exists(Get<ProgressBackupStore>(form, "_progressBackupStore").FilePath), "periodic autosave exists");
        form.Close();
        Click(form, "confirmation_accept");
        Check(form.IsDisposed, "backup and close disposes window");
        File.WriteAllText(Path.Combine(_output, "findings.json"), JsonSerializer.Serialize(Findings, ResultJsonOptions));
    }

    private static void CheckRecovery(GameForm form)
    {
        ProgressBackupStore store = Get<ProgressBackupStore>(form, "_progressBackupStore");
        ProgressBackup backup = new()
        {
            Nickname = "복구점검", Screen = "PostalRoom", Postal = new PostalRoomProgress(),
            ElapsedMilliseconds = 15000, HintCount = 2, FailedAttempts = 3,
            InspectedLocations = ["postal_drawer"]
        };
        Check(store.TrySave(backup, out _), "resume UI fixture saves");
        Set(form, "_pendingBackup", backup);
        Set(form, "_resumePromptHandled", false);
        Call(form, "ShowResumePromptIfAvailable");
        Check(form.Controls.Find("ResumePrompt", true).Single().Visible, "resume prompt visible");
        Click(form, "resume_load");
        Check(Get<GameScreen>(form, "_screen") == GameScreen.PostalRoom, "resume load button restores postal room");
        Check(!File.Exists(store.FilePath), "resume load button consumes original file");
        Check(Get<GameState>(form, "_state").HintCount == 2 && Get<GameState>(form, "_state").FailedAttempts == 3, "resume restores counters");
        Check(Get<HashSet<string>>(form, "_inspectedLocations").Contains("postal_drawer"), "resume restores inspection history");
        Check(Get<TimeSpan>(form, "_elapsedBeforeSession") == TimeSpan.FromSeconds(15), "resume restores elapsed duration");
        Set(form, "_gameInProgress", false);
        Call(form, "ShowMainMenu", false);
        Check(store.TrySave(backup, out _), "resume discard fixture saves");
        Set(form, "_pendingBackup", backup);
        Set(form, "_resumePromptHandled", false);
        Call(form, "ShowResumePromptIfAvailable");
        Click(form, "resume_discard");
        Check(Get<GameScreen>(form, "_screen") == GameScreen.Menu && !File.Exists(store.FilePath), "resume discard returns menu and deletes file");
        foreach (GameScreen screen in Enum.GetValues<GameScreen>().Where(screen => screen is not GameScreen.Menu and not GameScreen.Ending))
        {
            backup.Screen = screen.ToString();
            backup.Postal = screen is GameScreen.Intro or GameScreen.PostalRoom or GameScreen.PostalLedger or GameScreen.PostalBells
                ? new PostalRoomProgress { KeyFound = true, DrawerOpened = true, BellClueFound = true }
                : PostalRoomProgress.CompletedLegacyRoom();
            backup.SolvedPuzzles = screen is GameScreen.Clock or GameScreen.Choice ? [.. Enum.GetValues<PuzzleId>()] : [];
            backup.ClockRestored = screen == GameScreen.Choice;
            Call(form, "RestoreProgress", backup);
            Check(Get<GameScreen>(form, "_screen") == (screen == GameScreen.Letter ? GameScreen.Desk : screen), "restore screen " + screen);
        }
        Call(form, "StartNewGame");
    }

    private static void DropPiece(StockingPiece piece, StockingHookSlot? target)
    {
        typeof(StockingPiece).GetMethod("OnMouseDown", PrivateInstance)!.Invoke(piece, [new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0)]);
        Point destination = target is null ? new Point(90, 250) : new Point(target.Left + target.Width / 2, target.Top + target.Height / 2);
        piece.Location = new Point(destination.X - piece.Width / 2, destination.Y - piece.Height / 2);
        typeof(StockingPiece).GetMethod("OnMouseUp", PrivateInstance)!.Invoke(piece, [new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0)]);
        Pump();
    }

    private static void EnterCode(GameForm form, string name, string action, string text, bool enter = false)
    {
        TextBox editor = (TextBox)form.Controls.Find(name, true).Single();
        editor.Text = text;
        if (enter)
        {
            typeof(Control).GetMethod("OnKeyDown", PrivateInstance)!.Invoke(editor, [new KeyEventArgs(Keys.Enter)]);
            Pump();
        }
        else
        {
            Click(form, action);
        }
    }

    private static void CompleteReturn(GameForm form, PuzzleId id, GameScreen parent)
    {
        Check(Get<GameState>(form, "_state").SolvedPuzzles.Contains(id), "actual answer solves " + id);
        Click(form, "completion_return");
        Check(Get<GameScreen>(form, "_screen") == parent, "actual puzzle return " + id);
    }

    private static void CheckCore()
    {
        Check(PuzzleRules.MatchesFrostVaultCode("161") && !PuzzleRules.MatchesFrostVaultCode(null), "vault core boundaries");
        Check(PuzzleRules.MatchesOrnamentEquationCode("44") && !PuzzleRules.MatchesOrnamentEquationCode("60"), "equation core answer");
        Check(PuzzleRules.MatchesLetterAcrostic(" c-h_i m n e y ") && !PuzzleRules.MatchesLetterAcrostic("굴뚝"), "letter normalized and Korean answer rejected");
        Check(!PuzzleRules.MatchesToyCipher("자정") && !PuzzleRules.MatchesStarChart("오로라"), "other Korean answers rejected");
        List<int[]> validOrders = [];
        foreach (int a in Enumerable.Range(0, 4))
        {
            foreach (int b in Enumerable.Range(0, 4))
            {
                foreach (int c in Enumerable.Range(0, 4))
                {
                    foreach (int d in Enumerable.Range(0, 4))
                    {
                        int[] candidate = [a, b, c, d];
                        if (PuzzleRules.MatchesStockingOrder(candidate))
                        {
                            validOrders.Add(candidate);
                        }
                    }
                }
            }
        }
        Check(validOrders.Count == 1 && validOrders[0].SequenceEqual([0, 1, 2, 3]), "original stocking answer preserved among 256 candidates");
        Check(!PuzzleRules.MatchesStockingOrder([-1, 1, 2, 3]), "stocking invalid id rejected");
        Check(!NicknameRules.TryNormalize(null, out _, out _) && !NicknameRules.TryNormalize("x", out _, out _), "nickname missing and short rejected");
        Check(NicknameRules.TryNormalize("  한글   이름  ", out string normalized, out _) && normalized == "한글 이름", "nickname whitespace normalization");
        Check(!NicknameRules.TryNormalize(new string('a', 17), out _, out _), "nickname max length enforced");
        GameState state = new();
        Check(!state.ChooseEnding(EndingChoice.DeliverTheGift), "core ending locked before restored clock");
        foreach (PuzzleId id in Enum.GetValues<PuzzleId>())
        {
            Check(state.Solve(id) && !state.Solve(id), "core solve idempotent " + id);
        }
        Check(state.RestoreClock(12, 0, 25) && state.ChooseEnding(EndingChoice.DeliverTheGift), "core clock and ending");
    }

    private static void CheckPersistence()
    {
        string dir = Path.Combine(_output, "store-boundaries-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        PlayerDataStore player = new(dir);
        Check(player.TryLoad(out PlayerData empty, out _) && empty.Nickname.Length == 0, "missing player data defaults");
        Check(player.TrySave(new PlayerData { Nickname = "저장점검", TutorialSeen = true }, out _), "player save Korean text");
        Check(player.TryLoad(out PlayerData loaded, out _) && loaded.Nickname == "저장점검" && loaded.TutorialSeen, "player round trip");
        File.WriteAllText(player.FilePath, "{broken");
        Check(!player.TryLoad(out _, out string? error) && error is not null, "corrupt player JSON rejected");
        ProgressBackupStore store = new(dir);
        Check(store.TryLoad(out ProgressBackup? missing, out _) && missing is null, "missing backup is safe");
        ProgressBackup valid = new() { Nickname = "저장점검", Postal = new PostalRoomProgress(), Screen = "PostalRoom", ElapsedMilliseconds = 12000 };
        Check(store.TrySave(valid, out _), "valid postal backup saves");
        Check(store.TryConsume(out ProgressBackup? consumed, out _) && consumed!.ElapsedMilliseconds == 12000, "backup consumes and restores time");
        Check(!File.Exists(store.FilePath) && !store.TryConsume(out _, out _), "backup can be consumed only once");
        foreach (string json in new[] { "{broken", "null", "{\"Nickname\":\"QA\",\"Version\":2}", "{\"Nickname\":\"QA\",\"SolvedPuzzles\":[0,0]}", "{\"Nickname\":\"QA\",\"ClockRestored\":true}", "{\"Nickname\":\"QA\",\"ElapsedMilliseconds\":-1}" })
        {
            File.WriteAllText(store.FilePath, json);
            Check(!store.TryLoad(out _, out _), "invalid backup rejected " + json);
        }
        Check(store.TryDiscard(out _) && !File.Exists(store.FilePath), "corrupt backup discard");
        string blocked = Path.Combine(dir, "file-instead-of-folder");
        File.WriteAllText(blocked, "block");
        Check(!new PlayerDataStore(blocked).TrySave(new PlayerData(), out _), "player IO failure returned safely");
        Check(!new ProgressBackupStore(blocked).TrySave(valid, out _), "backup IO failure returned safely");
    }
}
