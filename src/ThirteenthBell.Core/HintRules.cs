namespace ThirteenthBell.Core;

public static class HintRules
{
    public static TimeSpan UnlockDelay { get; } = TimeSpan.FromMinutes(3.5);

    public static bool IsUnlocked(TimeSpan elapsed)
    {
        return elapsed >= UnlockDelay;
    }
}
