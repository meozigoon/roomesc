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

            }
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"게임 원화 파일을 찾을 수 없습니다: {path}", path);
            }

            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using Image loaded = Image.FromStream(stream);
            // Decode once into a screen-sized, premultiplied bitmap instead of retaining oversized PNGs.
            float scale = Math.Min(1f, Math.Min(1400f / loaded.Width, 820f / loaded.Height));
            int width = Math.Max(1, (int)Math.Round(loaded.Width * scale));
            int height = Math.Max(1, (int)Math.Round(loaded.Height * scale));
            Bitmap copy = new(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(copy))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(loaded, new Rectangle(0, 0, width, height));
            }
            lock (_sync)
            {
                if (_disposed)
                {
                    copy.Dispose();
                    throw new ObjectDisposedException(nameof(ImageBank));
                }
                if (_images.TryGetValue(fileName, out Image? cached))
                {
                    copy.Dispose();
                    return cached;
                }
                _images.Add(fileName, copy);
                return copy;
            }
        }
    }

    public void Preload(params string[] fileNames)
    {
        foreach (string fileName in fileNames)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
            }
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
