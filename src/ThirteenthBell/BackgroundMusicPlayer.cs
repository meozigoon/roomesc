using NAudio.Wave;
using System.Runtime.InteropServices;

namespace ThirteenthBell;

internal enum BackgroundMusicKind
{
    None,
    Menu,
    Game,
    PostalRoom
}

internal sealed class BackgroundMusicPlayer : IDisposable
{
    private const float TargetVolume = 0.34f;
    private const float FadeStep = 0.028f;
    private readonly bool _playbackAvailable;
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 30 };
    private WasapiPlayer? _output;
    private AudioFileReader? _reader;
    private LoopingWaveStream? _loop;
    private bool _enabled = true;
    private bool _fadingOut;
    private bool _disposed;
    private BackgroundMusicKind _requestedKind;
    private BackgroundMusicKind _playingKind;
    private bool _opening;

    public BackgroundMusicPlayer(bool playbackAvailable = true)
    {
        _playbackAvailable = playbackAvailable;
        _fadeTimer.Tick += HandleFadeTick;
    }

    public BackgroundMusicKind CurrentKind => _requestedKind;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_disposed || _enabled == value)
            {
                return;
            }

            _enabled = value;
            if (!_playbackAvailable)
            {
                return;
            }

            if (_enabled)
            {
                if (_reader is null)
                {
                    StartRequestedTrack(0f);
                }
                else
                {
                    _fadingOut = false;
                    _fadeTimer.Start();
                }
            }
            else
            {
                BeginFadeOut();
            }
        }
    }

    public string? LastError { get; private set; }

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    public void Play(BackgroundMusicKind kind)
    {
        if (_disposed)
        {
            return;
        }

        if (_requestedKind == kind
            && (!_playbackAvailable
                || !_enabled
                || _opening
                || (_playingKind == kind && _output?.PlaybackState == PlaybackState.Playing && !_fadingOut)))
        {
            return;
        }

        _requestedKind = kind;
        if (!_playbackAvailable || !_enabled)
        {
            return;
        }

        if (_reader is null)
        {
            StartRequestedTrack(0f);
            return;
        }

        BeginFadeOut();
    }

    private void StartRequestedTrack(float initialVolume)
    {
        if (_opening)
        {
            return;
        }
        _ = StartRequestedTrackAsync(initialVolume);
    }

    private async Task StartRequestedTrackAsync(float initialVolume)
    {
        StopPlayback();
        if (_disposed || !_enabled || _requestedKind == BackgroundMusicKind.None)
        {
            _fadeTimer.Stop();
            return;
        }

        BackgroundMusicKind kind = _requestedKind;
        string fileName = kind switch
        {
            BackgroundMusicKind.Menu => "menu-bgmusic.mp3",
            BackgroundMusicKind.Game => "game-bgmusic.mp3",
            BackgroundMusicKind.PostalRoom => "first_game-bgmusic.mp3",
            _ => throw new InvalidOperationException("지원하지 않는 배경 음악 종류입니다.")
        };
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Music", fileName);
        _opening = true;
        try
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("배경음악 파일을 찾을 수 없습니다.", path);
            }

            var prepared = await Task.Run(() => PrepareTrack(path, initialVolume)).ConfigureAwait(true);
            if (_disposed || !_enabled || _requestedKind != kind)
            {
                prepared.Output.Dispose();
                prepared.Loop.Dispose();
                prepared.Reader.Dispose();
                return;
            }
            _reader = prepared.Reader;
            _loop = prepared.Loop;
            _output = prepared.Output;
            _output.Play();
            _playingKind = kind;
            _fadingOut = false;
            _fadeTimer.Start();
            LastError = null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException
            or COMException
            or ArgumentException)
        {
            LastError = exception.Message;
            if (!_disposed)
            {
                _fadeTimer.Stop();
            }
            StopPlayback();
        }
        finally
        {
            _opening = false;
            if (!_disposed && _enabled && _requestedKind != kind)
            {
                StartRequestedTrack(0f);
            }
        }
    }

    private static (AudioFileReader Reader, LoopingWaveStream Loop, WasapiPlayer Output) PrepareTrack(string path, float initialVolume)
    {
        AudioFileReader reader = new(path) { Volume = Math.Clamp(initialVolume, 0f, TargetVolume) };
        LoopingWaveStream loop = new(reader);
        WasapiPlayer? output = null;
        try
        {
            output = new WasapiPlayerBuilder().WithSharedMode().WithLatency(120).Build();
            output.Init(loop);
            return (reader, loop, output);
        }
        catch
        {
            output?.Dispose();
            loop.Dispose();
            reader.Dispose();
            throw;
        }
    }

    private void BeginFadeOut()
    {
        if (_reader is null)
        {
            StopPlayback();
            return;
        }

        _fadingOut = true;
        _fadeTimer.Start();
    }

    private void HandleFadeTick(object? sender, EventArgs eventArgs)
    {
        if (_disposed)
        {
            _fadeTimer.Stop();
            return;
        }

        AudioFileReader? reader = _reader;
        if (reader is null)
        {
            _fadeTimer.Stop();
            if (_enabled && _requestedKind != BackgroundMusicKind.None)
            {
                StartRequestedTrack(0f);
            }

            return;
        }

        if (_fadingOut || !_enabled || _requestedKind == BackgroundMusicKind.None || _playingKind != _requestedKind)
        {
            reader.Volume = Math.Max(0f, reader.Volume - FadeStep);
            if (reader.Volume > 0f)
            {
                return;
            }

            StopPlayback();
            if (_enabled && _requestedKind != BackgroundMusicKind.None)
            {
                StartRequestedTrack(0f);
            }
            else
            {
                _fadeTimer.Stop();
            }

            return;
        }

        reader.Volume = Math.Min(TargetVolume, reader.Volume + FadeStep);
        if (reader.Volume >= TargetVolume)
        {
            _fadeTimer.Stop();
        }
    }

    private void StopPlayback()
    {
        WasapiPlayer? output = _output;
        LoopingWaveStream? loop = _loop;
        AudioFileReader? reader = _reader;
        _output = null;
        _loop = null;
        _reader = null;
        _playingKind = BackgroundMusicKind.None;

        try
        {
            output?.Stop();
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            ErrorReporter.Report(exception, "Stopping background audio", false);
        }

        DisposeAudioResource(output, "Releasing the audio output");
        DisposeAudioResource(loop, "Releasing the looping audio stream");
        DisposeAudioResource(reader, "Releasing the audio reader");
    }

    private static void DisposeAudioResource(IDisposable? resource, string context)
    {
        try
        {
            resource?.Dispose();
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            ErrorReporter.Report(exception, context, false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _requestedKind = BackgroundMusicKind.None;
        _fadeTimer.Stop();
        _fadeTimer.Dispose();
        StopPlayback();
    }

    private sealed class LoopingWaveStream : WaveStream
    {
        private readonly WaveStream _source;

        public LoopingWaveStream(WaveStream source)
        {
            _source = source;
        }

        public override WaveFormat WaveFormat => _source.WaveFormat;

        public override long Length => long.MaxValue;

        public override long Position
        {
            get => _source.Position;
            set => _source.Position = value;
        }

        public override int Read(Span<byte> buffer)
        {
            int totalRead = 0;
            bool restarted = false;
            while (totalRead < buffer.Length)
            {
                int read = _source.Read(buffer[totalRead..]);
                if (read == 0)
                {
                    if (restarted)
                    {
                        break;
                    }
                    _source.Position = 0;
                    restarted = true;
                    continue;
                }

                restarted = false;
                totalRead += read;
            }

            return totalRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

    }
}
