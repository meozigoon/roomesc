namespace ThirteenthBell.Core;

public static class PuzzleRules
{
    public const int FrostVaultAnswer = 161;

    public const int OrnamentEquationAnswer = 44;

    public const string LetterAcrosticAnswer = "CHIMNEY";

    public const string ToyCipherAnswer = "MIDNIGHT";

    public const string StarChartAnswer = "AURORA";

    public static bool MatchesFrostVaultCode(string? input)
    {
        return int.TryParse(
            input,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out int answer) && answer == FrostVaultAnswer;
    }

    public static bool MatchesOrnamentEquationCode(string? input)
    {
        return int.TryParse(
            input,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out int answer) && answer == OrnamentEquationAnswer;
    }

    public static bool MatchesStockingOrder(IReadOnlyList<int> input)
    {
        if (input.Count != 4)
        {
            return false;
        }

        Span<bool> seen = stackalloc bool[4];
        Span<int> positions = stackalloc int[4];
        for (int position = 0; position < input.Count; position++)
        {
            int stocking = input[position];
            if (stocking is < 0 or > 3 || seen[stocking])
            {
                return false;
            }

            seen[stocking] = true;
            positions[stocking] = position;
        }

        int yellow = positions[0];
        int green = positions[1];
        int red = positions[2];
        int blue = positions[3];
        return green is not 0 and not 3
            && blue > red
            && Math.Abs(yellow - blue) != 1
            // The displayed clues intentionally leave alternatives; preserve the original accepted arrangement.
            && green + 1 == red;
    }

    public static bool IsClockSettingCorrect(int hour, int minute, int date)
    {
        return hour == 12 && minute == 0 && date == 25;
    }

    public static bool MatchesLetterAcrostic(string? input)
    {
        string normalized = NormalizeTextAnswer(input);
        return normalized == LetterAcrosticAnswer;
    }

    public static bool MatchesToyCipher(string? input)
    {
        string normalized = NormalizeTextAnswer(input);
        return normalized == ToyCipherAnswer;
    }

    public static bool MatchesStarChart(string? input)
    {
        string normalized = NormalizeTextAnswer(input);
        return normalized == StarChartAnswer;
    }

    private static string NormalizeTextAnswer(string? input)
    {
        return new string((input ?? string.Empty)
            .Where(character => !char.IsWhiteSpace(character) && character is not '-' and not '_')
            .Select(char.ToUpperInvariant)
            .ToArray());
    }
}
