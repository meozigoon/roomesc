using NVorbis;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: AudioAssetBuilder <Kenney Audio directory> <output directory>");
    return 2;
}

string sourceDirectory = Path.GetFullPath(args[0]);
string outputDirectory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outputDirectory);

SoundAsset[] assets =
[
    new("ui-click.wav", "click_004.ogg", 0.20f),
    new("locked.wav", "toggle_002.ogg", 0.22f),
    new("wrong.wav", "error_006.ogg", 0.20f),
    new("puzzle-item.wav", "confirmation_002.ogg", 0.24f),
    new("clock-restored.wav", "maximize_006.ogg", 0.24f),
    new("ending-bell.wav", "bong_001.ogg", 0.23f),
    new("note-moon.wav", "select_002.ogg", 0.18f),
    new("note-tree.wav", "select_004.ogg", 0.18f),
    new("note-bell.wav", "select_006.ogg", 0.18f),
    new("note-star.wav", "select_008.ogg", 0.18f)
];

foreach (SoundAsset asset in assets)
{
    string sourcePath = Path.Combine(sourceDirectory, asset.SourceName);
    if (!File.Exists(sourcePath))
    {
        throw new FileNotFoundException($"Source audio was not found: {sourcePath}", sourcePath);
    }

    string outputPath = Path.Combine(outputDirectory, asset.OutputName);
    WaveAsset wave = DecodeAndPrepare(sourcePath, asset.Gain);
    WritePcm16Wave(outputPath, wave);
    Console.WriteLine($"{asset.OutputName}: {wave.Samples.Length / (double)wave.SampleRate:0.000}s, {wave.SampleRate} Hz, source={asset.SourceName}");
}

return 0;

static WaveAsset DecodeAndPrepare(string sourcePath, float gain)
{
    using VorbisReader reader = new(sourcePath);
    int channels = reader.Channels;
    int sampleRate = reader.SampleRate;
    float[] interleavedBuffer = new float[Math.Max(4096, channels * 2048)];
    List<float> monoSamples = [];

    while (true)
    {
        int samplesRead = reader.ReadSamples(interleavedBuffer, 0, interleavedBuffer.Length);
        if (samplesRead <= 0)
        {
            break;
        }

        int completeFrames = samplesRead / channels;
        for (int frame = 0; frame < completeFrames; frame++)
        {
            float sum = 0f;
            for (int channel = 0; channel < channels; channel++)
            {
                sum += interleavedBuffer[(frame * channels) + channel];
            }

            monoSamples.Add(Math.Clamp((sum / channels) * gain, -1f, 1f));
        }
    }

    if (monoSamples.Count == 0)
    {
        throw new InvalidDataException($"Decoded audio is empty: {sourcePath}");
    }

    int thresholdStart = FindFirstAudibleSample(monoSamples, 0.0008f);
    int thresholdEnd = FindLastAudibleSample(monoSamples, 0.0008f);
    int leadingMargin = sampleRate / 100;
    int trailingMargin = sampleRate / 20;
    int start = Math.Max(0, thresholdStart - leadingMargin);
    int end = Math.Min(monoSamples.Count - 1, thresholdEnd + trailingMargin);
    float[] trimmed = monoSamples.GetRange(start, (end - start) + 1).ToArray();

    int fadeSamples = Math.Min(trimmed.Length, sampleRate / 50);
    for (int index = 0; index < fadeSamples; index++)
    {
        int sampleIndex = trimmed.Length - fadeSamples + index;
        float multiplier = 1f - (index / (float)fadeSamples);
        trimmed[sampleIndex] *= multiplier;
    }

    return new WaveAsset(sampleRate, trimmed);
}

static int FindFirstAudibleSample(IReadOnlyList<float> samples, float threshold)
{
    for (int index = 0; index < samples.Count; index++)
    {
        if (Math.Abs(samples[index]) >= threshold)
        {
            return index;
        }
    }

    return 0;
}

static int FindLastAudibleSample(IReadOnlyList<float> samples, float threshold)
{
    for (int index = samples.Count - 1; index >= 0; index--)
    {
        if (Math.Abs(samples[index]) >= threshold)
        {
            return index;
        }
    }

    return samples.Count - 1;
}

static void WritePcm16Wave(string outputPath, WaveAsset wave)
{
    const short channels = 1;
    const short bitsPerSample = 16;
    int byteRate = wave.SampleRate * channels * (bitsPerSample / 8);
    short blockAlign = (short)(channels * (bitsPerSample / 8));
    int dataLength = checked(wave.Samples.Length * sizeof(short));

    using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
    using BinaryWriter writer = new(stream);
    writer.Write("RIFF"u8.ToArray());
    writer.Write(checked(36 + dataLength));
    writer.Write("WAVE"u8.ToArray());
    writer.Write("fmt "u8.ToArray());
    writer.Write(16);
    writer.Write((short)1);
    writer.Write(channels);
    writer.Write(wave.SampleRate);
    writer.Write(byteRate);
    writer.Write(blockAlign);
    writer.Write(bitsPerSample);
    writer.Write("data"u8.ToArray());
    writer.Write(dataLength);

    foreach (float sample in wave.Samples)
    {
        writer.Write((short)Math.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
    }
}

internal sealed record SoundAsset(string OutputName, string SourceName, float Gain);

internal sealed record WaveAsset(int SampleRate, float[] Samples);
