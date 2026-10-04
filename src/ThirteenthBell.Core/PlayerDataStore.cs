using System.Text;
using System.Text.Json;

namespace ThirteenthBell.Core;

public sealed class PlayerData
{
    public bool TutorialSeen { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public string OnlineClaimToken { get; set; } = string.Empty;
}

public sealed class PlayerDataStore
{
    private const string DataDirectoryEnvironmentVariable = "THIRTEENTH_BELL_DATA_DIR";

    public PlayerDataStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        DataDirectory = Path.GetFullPath(dataDirectory);
        FilePath = Path.Combine(DataDirectory, "player-data.json");
    }

    public string DataDirectory { get; }

    public string FilePath { get; }

    public static PlayerDataStore CreateDefault()
    {
        string? overriddenDirectory = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        string directory = string.IsNullOrWhiteSpace(overriddenDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThirteenthBell")
            : overriddenDirectory;
        return new PlayerDataStore(directory);
    }

    public bool TryLoad(out PlayerData data, out string? error)
    {
        data = new PlayerData();
        error = null;
        if (!File.Exists(FilePath))
        {
            return true;
        }

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            PlayerData? loaded = JsonSerializer.Deserialize<PlayerData>(json, JsonFileStore.Options);
            if (loaded is null)
            {
                error = "저장된 사용자 데이터가 비어 있어 기본값으로 시작합니다.";
                return false;
            }

            loaded.Nickname ??= string.Empty;
            loaded.OnlineClaimToken ??= string.Empty;

            data = loaded;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            error = $"사용자 데이터를 읽지 못해 기본값으로 시작합니다: {exception.Message}";
            return false;
        }
    }

    public bool TrySave(PlayerData data, out string? error)
    {
        ArgumentNullException.ThrowIfNull(data);
        error = null;

        try
        {
            JsonFileStore.Save(FilePath, data);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = $"사용자 데이터를 저장하지 못했습니다: {exception.Message}";
            return false;
        }
    }
}
