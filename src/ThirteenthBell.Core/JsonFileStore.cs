using System.Text;
using System.Text.Json;

namespace ThirteenthBell.Core;

internal static class JsonFileStore
{
    internal static JsonSerializerOptions Options { get; } = new() { WriteIndented = true };
    private static readonly UTF8Encoding Encoding = new(false);

    internal static void Save<T>(string filePath, T data)
    {
        string temporaryPath = filePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, Options), Encoding);
            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Cleanup failure must not invalidate a completed save or hide its original error.
            }
        }
    }
}
