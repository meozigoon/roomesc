using System.Text.RegularExpressions;

namespace ThirteenthBell.Core;

public static partial class NicknameRules
{
    public const int MinimumLength = 2;
    public const int MaximumLength = 16;

    public static bool TryNormalize(string? value, out string normalized, out string? error)
    {
        normalized = WhitespaceRegex().Replace(value?.Trim() ?? string.Empty, " ");
        if (normalized.Length < MinimumLength || normalized.Length > MaximumLength)
        {
            error = $"닉네임은 {MinimumLength}자 이상 {MaximumLength}자 이하로 입력하세요.";
            return false;
        }

        if (normalized.Any(char.IsControl))
        {
            error = "닉네임에는 제어 문자를 사용할 수 없습니다.";
            return false;
        }

        error = null;
        return true;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
