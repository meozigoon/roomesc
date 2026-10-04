namespace ThirteenthBell.Core;

public static class HintRules
{
    public static TimeSpan UnlockDelay { get; } = TimeSpan.Zero;

    public static bool IsUnlocked(TimeSpan elapsed)
    {
        return elapsed >= UnlockDelay;
    }
}
