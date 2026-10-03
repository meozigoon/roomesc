using System.Text;

namespace ThirteenthBell;

internal static class ErrorReporter
{
    private static int _isShuttingDown;

    public static bool IsShuttingDown => Volatile.Read(ref _isShuttingDown) != 0;

    public static void BeginShutdown()
    {
        Interlocked.Exchange(ref _isShuttingDown, 1);
    }

    public static void Report(Exception exception, string context, bool showMessage)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThirteenthBell",
                "Logs");
            Directory.CreateDirectory(directory);
            string entry = $"[{DateTimeOffset.Now:O}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(directory, "error.log"), entry, new UTF8Encoding(false));
        }
        catch
        {
            // Error reporting must never trigger another application failure.
        }

        if (!showMessage || IsShuttingDown)
        {
            return;
        }

        try
        {
            MessageBox.Show(
                "예기치 않은 문제가 발생했습니다. 게임을 안전하게 종료한 뒤 다시 실행해 주세요.\n오류 기록은 로컬 앱 데이터의 ThirteenthBell\\Logs에 저장됩니다.",
                "13번째 종",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // The UI may already be unavailable during a fatal failure.
        }
    }
}
