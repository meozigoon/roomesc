namespace ThirteenthBell.Core;

public enum PuzzleId
{
    Lanterns,
    Melody,
    RibbonLoom,
    LetterAcrostic,
    ToyCipher,
    StarChart
}

public enum EndingChoice
{
    None,
    DeliverTheGift,
    FeedTheClock
}

public sealed class GameState
{
    private readonly HashSet<PuzzleId> _solvedPuzzles = [];

    public IReadOnlySet<PuzzleId> SolvedPuzzles => _solvedPuzzles;

    public int HintCount { get; private set; }

    public int FailedAttempts { get; private set; }

    public bool ClockRestored { get; private set; }

    public EndingChoice Ending { get; private set; }

    public const int RequiredPuzzleCount = 6;

    public const int InitialHints = 1;
    public bool CreatorEasterEggFound { get; private set; }

    public bool TryClaimCreatorHint()
    {
        if (CreatorEasterEggFound || ClearFailed)
        {
            return false;
        }
        CreatorEasterEggFound = true;
        return true;
    }

    public const int HintRechargeSeconds = 180;

    public const int MaximumFailedAttempts = 10;

    public bool ClearFailed => FailedAttempts > MaximumFailedAttempts;

    public int AvailableHints(TimeSpan elapsed)
    {
        long earned = InitialHints + (CreatorEasterEggFound ? 1 : 0)
            + Math.Max(0, elapsed.Ticks) / TimeSpan.FromSeconds(HintRechargeSeconds).Ticks;
        return (int)Math.Clamp(earned - HintCount, 0, int.MaxValue);
    }

    public bool TryUseHint(TimeSpan elapsed)
    {
        if (ClearFailed || AvailableHints(elapsed) == 0)
        {
            return false;
        }
        RecordHint();
        return true;
    }

    public bool CanOpenClock => _solvedPuzzles.Count == RequiredPuzzleCount;

    public static GameState Restore(
        IEnumerable<PuzzleId> solvedPuzzles,
        int hintCount,
        int failedAttempts,
        bool clockRestored,
        bool creatorEasterEggFound = false)
    {
        ArgumentNullException.ThrowIfNull(solvedPuzzles);
        ArgumentOutOfRangeException.ThrowIfNegative(hintCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedAttempts);

        GameState restored = new();
        foreach (PuzzleId puzzle in solvedPuzzles)
        {
            if (!Enum.IsDefined(puzzle) || !restored._solvedPuzzles.Add(puzzle))
            {
                throw new ArgumentException("저장된 퍼즐 목록이 올바르지 않습니다.", nameof(solvedPuzzles));
            }
        }

        if (clockRestored && !restored.CanOpenClock)
        {
            throw new ArgumentException("모든 퍼즐을 풀기 전에는 별시계 복구 상태를 불러올 수 없습니다.", nameof(clockRestored));
        }

        restored.HintCount = hintCount;
        restored.FailedAttempts = failedAttempts;
        restored.ClockRestored = clockRestored;
        restored.CreatorEasterEggFound = creatorEasterEggFound;
        return restored;
    }

    public bool Solve(PuzzleId puzzle)
    {
        if (!Enum.IsDefined(puzzle))
        {
            throw new ArgumentOutOfRangeException(nameof(puzzle));
        }
        return _solvedPuzzles.Add(puzzle);
    }

    public void RecordFailure()
    {
        if (!ClearFailed)
        {
            FailedAttempts++;
        }
    }

    public void RecordHint()
    {
        HintCount++;
    }

    public bool RestoreClock(int hour, int minute, int date)
    {
        if (ClearFailed)
        {
            return false;
        }
        if (!CanOpenClock || !PuzzleRules.IsClockSettingCorrect(hour, minute, date))
        {
            RecordFailure();
            return false;
        }

        ClockRestored = true;
        return true;
    }

    public bool ChooseEnding(EndingChoice choice)
    {
        if (ClearFailed || !ClockRestored || choice == EndingChoice.None || !Enum.IsDefined(choice))
        {
            return false;
        }

        Ending = choice;
        return true;
    }
}
