using System.Text;
using System.Text.Json;

namespace ThirteenthBell.Core;

public sealed class ProgressBackup
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string Nickname { get; set; } = string.Empty;

    public List<PuzzleId> SolvedPuzzles { get; set; } = [];

    public int HintCount { get; set; }

    public int FailedAttempts { get; set; }

    public bool ClockRestored { get; set; }

    public PostalRoomProgress? Postal { get; set; }

    public List<string> InspectedLocations { get; set; } = [];

    public long ElapsedMilliseconds { get; set; }

    public string Screen { get; set; } = "Room";

    public DateTimeOffset SavedAtUtc { get; set; }
}

public sealed class ProgressBackupStore
{

    public ProgressBackupStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        DataDirectory = Path.GetFullPath(dataDirectory);
        FilePath = Path.Combine(DataDirectory, "progress-backup.json");
    }

    public string DataDirectory { get; }

    public string FilePath { get; }

    public bool TryLoad(out ProgressBackup? backup, out string? error)
    {
        backup = null;
        error = null;
        if (!File.Exists(FilePath))
        {
            return true;
        }

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            ProgressBackup? loaded = JsonSerializer.Deserialize<ProgressBackup>(json, JsonFileStore.Options);
            if (loaded is null || !IsValid(loaded, out error))
            {
                error ??= "진행 백업이 비어 있습니다.";
                return false;
            }

            backup = loaded;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            error = $"진행 백업을 읽지 못했습니다: {exception.Message}";
            return false;
        }
    }

    public bool TrySave(ProgressBackup backup, out string? error)
    {
        ArgumentNullException.ThrowIfNull(backup);
        error = null;
        if (!IsValid(backup, out error))
        {
            return false;
        }

        try
        {
            JsonFileStore.Save(FilePath, backup);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = $"진행 백업을 저장하지 못했습니다: {exception.Message}";
            return false;
        }
    }

    public bool TryConsume(out ProgressBackup? backup, out string? error)
    {
        if (!TryLoad(out backup, out error) || backup is null)
        {
            return false;
        }

        if (!TryDiscard(out error))
        {
            backup = null;
            return false;
        }

        return true;
    }

    public bool TryDiscard(out string? error)
    {
        error = null;
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"진행 백업을 삭제하지 못했습니다: {exception.Message}";
            return false;
        }
    }

    private static bool IsValid(ProgressBackup backup, out string? error)
    {
        error = null;
        backup.Nickname ??= string.Empty;
        backup.SolvedPuzzles ??= [];
        backup.Screen ??= string.Empty;
        backup.InspectedLocations ??= [];

        if (backup.Version != ProgressBackup.CurrentVersion)
        {
            error = "지원하지 않는 진행 백업 버전입니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(backup.Nickname)
            || backup.ElapsedMilliseconds < 0
            || backup.HintCount < 0
            || backup.FailedAttempts < 0
            || backup.SolvedPuzzles.Count > GameState.RequiredPuzzleCount
            || backup.SolvedPuzzles.Distinct().Count() != backup.SolvedPuzzles.Count
            || backup.SolvedPuzzles.Any(puzzle => !Enum.IsDefined(puzzle))
            || (backup.ClockRestored && backup.SolvedPuzzles.Count != GameState.RequiredPuzzleCount)
            || string.IsNullOrWhiteSpace(backup.Screen))
        {
            error = "진행 백업의 내용이 올바르지 않습니다.";
            return false;
        }

        if (backup.Postal is not null && (!backup.Postal.IsValid()
            || (!backup.Postal.DoorOpened && backup.SolvedPuzzles.Count > 0)))
        {
            error = "우편실 진행 백업의 내용이 올바르지 않습니다.";
            return false;
        }

        return true;
    }

}
