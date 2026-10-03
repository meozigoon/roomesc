namespace ThirteenthBell;

internal sealed class ImageBank : IDisposable
{
    private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private bool _disposed;

    public Image this[string fileName]
    {
        get
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_images.TryGetValue(fileName, out Image? cached))
                {
                    return cached;
                }

                string path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"게임 원화 파일을 찾을 수 없습니다: {path}", path);
                }

                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Image loaded = Image.FromStream(stream);
                Bitmap copy = new(loaded);
                _images.Add(fileName, copy);
                return copy;
            }
        }
    }

    public void Preload(params string[] fileNames)
    {
        foreach (string fileName in fileNames)
        {
            _ = this[fileName];
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (Image image in _images.Values)
            {
                image.Dispose();
            }

            _images.Clear();
        }
    }
}
