using System.Media;

namespace ThirteenthBell;

internal enum GameSound
{
    Click,
    Locked,
    Wrong,
    PuzzleItem,
    ClockRestored,
    EndingBell,
    NoteMoon,
    NoteTree,
    NoteBell,
    NoteStar
}

internal sealed class SoundBank : IDisposable
{
    private static readonly IReadOnlyDictionary<GameSound, string> FileNames =
        new Dictionary<GameSound, string>
        {
            [GameSound.Click] = "ui-click.wav",
            [GameSound.Locked] = "locked.wav",
            [GameSound.Wrong] = "wrong.wav",
            [GameSound.PuzzleItem] = "puzzle-item.wav",
            [GameSound.ClockRestored] = "clock-restored.wav",
            [GameSound.EndingBell] = "ending-bell.wav",
            [GameSound.NoteMoon] = "note-moon.wav",
            [GameSound.NoteTree] = "note-tree.wav",
            [GameSound.NoteBell] = "note-bell.wav",
            [GameSound.NoteStar] = "note-star.wav"
        };

    private readonly Dictionary<GameSound, SoundPlayer> _players = [];
    private readonly Dictionary<GameSound, long> _lastPlayedAt = [];
    private readonly HashSet<GameSound> _reportedPlaybackFailures = [];
    private bool _disposed;

    public SoundBank()
    {
        string soundDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
        foreach ((GameSound sound, string fileName) in FileNames)
        {
            string path = Path.Combine(soundDirectory, fileName);
            try
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("효과음 파일을 찾을 수 없습니다.", path);
                }

                SoundPlayer player = new(path);
                _players.Add(sound, player);
            }
            catch (Exception exception) when (exception is FileNotFoundException or InvalidOperationException or TimeoutException)
            {
                ErrorReporter.Report(exception, $"Loading sound effect '{fileName}'", false);
            }
        }
    }

    public bool Play(GameSound sound)
    {
        if (_disposed || !_players.TryGetValue(sound, out SoundPlayer? player))
        {
            return false;
        }

        long now = Environment.TickCount64;
        int minimumInterval = sound == GameSound.Click ? 35 : 55;
        if (_lastPlayedAt.TryGetValue(sound, out long previous) && now - previous < minimumInterval)
        {
            return false;
        }

        try
        {
            _lastPlayedAt[sound] = now;
            player.Play();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            if (_reportedPlaybackFailures.Add(sound))
            {
                ErrorReporter.Report(exception, $"Playing sound effect '{sound}'", false);
            }

            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (SoundPlayer player in _players.Values)
        {
            player.Dispose();
        }

        _players.Clear();
    }
}
