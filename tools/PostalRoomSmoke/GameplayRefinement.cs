using System.Diagnostics;
using System.Text.Json;
using ThirteenthBell.Core;

namespace ThirteenthBell;

internal static partial class PostalRoomSmoke
{
    private static readonly int[] RefinedBellSequence = [0, 1, 3, 2, 2, 3];
    private static readonly string[] RefinedStockingNames = ["노랑", "초록", "빨강", "파랑"];
    private static readonly int[] RefinedWindowWidths = [1400, 1000, 1680];
    private static readonly int[] OriginalStockingOrder = [0, 1, 2, 3];

    private static void CheckGameplayRefinement(GameForm form)
    {
        Call(form, "StartNewGame");
        Stopwatch timer = Get<Stopwatch>(form, "_gameStopwatch");
        Thread.Sleep(220);
        Pump();
        Check(!timer.IsRunning && timer.Elapsed == TimeSpan.Zero, "opening letter excludes reading time");
        Check(!Get<System.Windows.Forms.Timer>(form, "_elapsedTimer").Enabled, "opening letter has no active elapsed timer");
        Check(Get<GameState>(form, "_state").AvailableHints(TimeSpan.Zero) == 1, "one starting hint");
        Call(form, "SaveProgressBackup", false);
        ProgressBackupStore backups = Get<ProgressBackupStore>(form, "_progressBackupStore");
        Check(backups.TryLoad(out ProgressBackup? opening, out _) && opening!.ElapsedMilliseconds == 0, "opening backup contains zero gameplay time");
        Call(form, "RestoreProgress", opening!);
        Check(!timer.IsRunning && !Get<System.Windows.Forms.Timer>(form, "_elapsedTimer").Enabled, "restored opening letter stays paused");
        Click(form, "continue");
        Thread.Sleep(180);
        Pump();
        Check(timer.IsRunning && timer.ElapsedMilliseconds >= 150, "postal-room entry starts elapsed time");
        long before = timer.ElapsedMilliseconds;
        Call(form, "ShowPostalRoom");
        Check(timer.ElapsedMilliseconds >= before, "returning to room preserves elapsed time");
        timer.Stop();

        GameState hints = new();
        Check(hints.TryUseHint(TimeSpan.Zero), "first hint can be spent");
        Check(!hints.TryUseHint(TimeSpan.FromSeconds(179.999)), "recharge is unavailable before 180 seconds");
        Check(hints.AvailableHints(TimeSpan.FromSeconds(180)) == 1, "one charge arrives at 180 seconds");
        Check(hints.AvailableHints(TimeSpan.FromSeconds(360)) == 2, "one more charge arrives at 360 seconds");
        Check(hints.TryClaimCreatorHint() && hints.AvailableHints(TimeSpan.Zero) == 1 && hints.HintCount == 1, "creator reward adds availability without counting as usage");
        Check(!hints.TryClaimCreatorHint() && hints.AvailableHints(TimeSpan.Zero) == 1, "creator reward can only be claimed once");
        Check(GameState.Restore([], 1, 0, false, true).AvailableHints(TimeSpan.Zero) == 1, "restored reward and usage balance");

        Set(form, "_state", new GameState());
        Set(form, "_postal", PostalRoomProgress.CompletedLegacyRoom());
        foreach (Size size in new[] { new Size(1400, 820), new Size(1000, 600), new Size(1680, 1000) })
        {
            form.ClientSize = size;
            Call(form, "ShowDesk");
            SceneCanvas scene = Get<SceneCanvas>(form, "_scene");
            float scale = scene.Width / 1400f;
            Check(scene.GetChildAtPoint(new Point((int)(1060 * scale), (int)(545 * scale)))?.Name == "desk_creator", "pen paper click matches restored art at " + size.Width);
            Click(form, "desk_creator");
            GameState current = Get<GameState>(form, "_state");
            Check(current.CreatorEasterEggFound && current.AvailableHints(TimeSpan.Zero) == 2 && current.HintCount == 0, "paper awards exactly one unused hint at " + size.Width);
            Check(Get<Label>(form, "_notebookText").Text.Contains("제작자 정보", StringComparison.Ordinal), "paper reveals creator information");
            Check(Get<TimeSpan>(form, "_narrativeVisibleDuration") == TimeSpan.FromSeconds(7), "creator reward shown seven seconds");
            CheckVisibleText(form, "creator-" + size.Width);
            Capture(form, "gameplay-creator-" + size.Width);
            Click(form, "desk_creator");
            Check(current.AvailableHints(TimeSpan.Zero) == 2, "repeated paper cannot add hints");
            Call(form, "SaveProgressBackup", false);
            Check(backups.TryLoad(out ProgressBackup? saved, out _) && saved!.CreatorEasterEggFound, "creator reward saved");
            Call(form, "RestoreProgress", saved!);
            Click(form, "desk_creator");
            Check(Get<GameState>(form, "_state").AvailableHints(TimeSpan.Zero) == 2, "restoring cannot claim reward twice");

            Set(form, "_state", new GameState());
            Call(form, "ShowStockingPuzzle");
            StockingPiece[] pieces = Get<StockingPiece[]>(form, "_stockingPieces");
            for (int index = 0; index < pieces.Length; index++)
            {
                Check(pieces[index].TagText == (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), "unique numeric stocking identity " + index);
                Check(pieces[index].AccessibleName!.Contains(RefinedStockingNames[index], StringComparison.Ordinal), "stocking accessible color and number");
                Check(Get<Label>(form, "_notebookText").Text.Contains($"{RefinedStockingNames[index]}({index + 1})", StringComparison.Ordinal), "instructions include color and number " + index);
            }
            CheckVisibleText(form, "stockings-" + size.Width);
            Capture(form, "gameplay-numbered-stockings-" + size.Width);
            for (int index = 0; index < pieces.Length; index++)
            {
                Call(form, "PlaceStockingInSlot", index, index);
            }
            Call(form, "EvaluateStockingArrangement");
            Check(Get<GameState>(form, "_state").SolvedPuzzles.Contains(PuzzleId.RibbonLoom), "numbered stockings solve original logic");
            Call(form, "ShowStockingPuzzle");
            string answer = ((AtmosphereCard)scene.Controls.Find("MemoryTag", false).Single()).BodyLabel.Text;
            Check(answer.Contains("정답", StringComparison.Ordinal) && answer.Contains("25", StringComparison.Ordinal)
                && !answer.Contains("써 있는", StringComparison.Ordinal) && !answer.Contains("양말의 숫자", StringComparison.Ordinal), "solved message gives answer directly");
            Capture(form, "gameplay-stockings-solved-" + size.Width);
            Set(form, "_state", new GameState());
        }
        form.ClientSize = new Size(1400, 820);
        CheckRemainingStockingConditions(form);
        Set(form, "_postal", new PostalRoomProgress { BellClueFound = true });
        Call(form, "ShowPostalBells");
        SoundBank sounds = Get<SoundBank>(form, "_sounds");
        Dictionary<GameSound, long> played = (Dictionary<GameSound, long>)typeof(SoundBank).GetField("_lastPlayedAt", PrivateInstance)!.GetValue(sounds)!;
        Get<List<int>>(form, "_postalBellInput").AddRange(RefinedBellSequence);
        Check(!played.ContainsKey(GameSound.EndingBell), "completion bell silent before success");
        Set(form, "_soundEnabled", true);
        Call(form, "CheckPostalBells");
        Check(Get<PostalRoomProgress>(form, "_postal").BellsSolved && played.ContainsKey(GameSound.EndingBell), "correct bells trigger single completion bell");
        long playedAt = played[GameSound.EndingBell];
        Thread.Sleep(100);
        Call(form, "CheckPostalBells");
        Call(form, "ShowPostalBells");
        Call(form, "CheckPostalBells");
        Check(played[GameSound.EndingBell] == playedAt, "success recheck and revisit never repeat completion bell");
        Set(form, "_soundEnabled", false);
        Call(form, "StartNewGame");
        Check(!Get<GameState>(form, "_state").CreatorEasterEggFound && Get<GameState>(form, "_state").AvailableHints(TimeSpan.Zero) == 1, "new game resets creator reward and starting hint");
        File.WriteAllText(Path.Combine(_output, "gameplay-refinement-result.json"), JsonSerializer.Serialize(new { passed = true, checks = _checks, audio = "real SoundPlayer one-shot request; no listening test", sizes = RefinedWindowWidths }, ResultJsonOptions));
        Console.WriteLine($"GAMEPLAY_REFINEMENT_OK checks={_checks}");
    }

    private static void CheckRemainingStockingConditions(GameForm form)
    {
        byte[] oracle = Convert.FromHexString("0000000880000000000000000000000080000000000010000000000000000000");
        bool allOutcomesMatch = true;
        bool allMessagesMatch = true;
        bool allInstructionsMatch = true;
        for (int index = 0; index < 256; index++)
        {
            if ((oracle[index / 8] & (1 << (index % 8))) == 0)
            {
                continue;
            }
            Set(form, "_state", new GameState());
            Call(form, "ShowStockingPuzzle");
            string instructions = Get<Label>(form, "_notebookText").Text;
            allInstructionsMatch &= instructions.Contains('①') && instructions.Contains('②') && instructions.Contains('③')
                && !instructions.Contains('④') && !instructions.Contains("바로 왼쪽", StringComparison.Ordinal);
            string hint = (string)typeof(GameForm).GetMethod("NextGlobalHint", PrivateInstance)!.Invoke(form, null)!;
            allInstructionsMatch &= !hint.Contains("바로 이웃", StringComparison.Ordinal)
                && hint.Contains("초록(2)", StringComparison.Ordinal) && hint.Contains("파랑(4)", StringComparison.Ordinal);
            for (int slot = 0; slot < 4; slot++)
            {
                int pieceId = (index >> ((3 - slot) * 2)) & 3;
                Call(form, "PlaceStockingInSlot", pieceId, slot);
            }
            bool originalAnswer = Get<int[]>(form, "_stockingPlacement").SequenceEqual(OriginalStockingOrder);
            Call(form, "EvaluateStockingArrangement");
            GameState state = Get<GameState>(form, "_state");
            allOutcomesMatch &= state.SolvedPuzzles.Contains(PuzzleId.RibbonLoom) == originalAnswer;
            allOutcomesMatch &= state.FailedAttempts == (originalAnswer ? 0 : 1);
            if (!originalAnswer)
            {
                Call(form, "EvaluateStockingArrangement");
                allOutcomesMatch &= state.FailedAttempts == 1;
                allOutcomesMatch &= Get<Label>(form, "_hintText").Text.Contains("다른 배치를 시도해 보세요", StringComparison.Ordinal);
                allInstructionsMatch &= Get<Label>(form, "_notebookText").Text == instructions;
                continue;
            }
            SceneCanvas scene = Get<SceneCanvas>(form, "_scene");
            AtmosphereCard? memory = scene.Controls.Find("MemoryTag", false).OfType<AtmosphereCard>().SingleOrDefault();
            allMessagesMatch &= memory?.BodyLabel.Text == "정답: 노랑(1), 초록(2), 빨강(3), 파랑(4).\n기억 조각의 정답: 25";
            Call(form, "ShowStockingPuzzle");
            memory = scene.Controls.Find("MemoryTag", false).OfType<AtmosphereCard>().SingleOrDefault();
            allMessagesMatch &= memory?.BodyLabel.Text == "정답: 노랑(1), 초록(2), 빨강(3), 파랑(4).\n기억 조각의 정답: 25";
        }
        Check(allInstructionsMatch, "stocking instructions retain only remaining conditions");
        Check(allOutcomesMatch, "original answer completes while alternative clue-matching attempts count one failure");
        Check(allMessagesMatch, "stocking completion and revisit messages match the accepted arrangement");
    }
}
