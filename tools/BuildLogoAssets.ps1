param(
    [Parameter(Mandatory = $true)]
    [string]$InputPng,

    [Parameter(Mandatory = $true)]
    [string]$AssetsDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$resolvedInput = (Resolve-Path -LiteralPath $InputPng).Path
$resolvedAssets = (Resolve-Path -LiteralPath $AssetsDirectory).Path
$source = [System.Drawing.Bitmap]::FromFile($resolvedInput)

function New-LogoBitmap
{
    param(
        [System.Drawing.Image]$Source,
        [int]$Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try
    {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage($Source, 0, 0, $Size, $Size)
    }
    finally
    {
        $graphics.Dispose()
    }

    return $bitmap
}

try
{
    $logo = New-LogoBitmap -Source $source -Size 512
    try
    {
        $logo.Save((Join-Path $resolvedAssets 'wall-clock-logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $logo.Save((Join-Path $resolvedAssets 'app-icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    {
        $logo.Dispose()
    }

    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $frames = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes)
    {
        $frame = New-LogoBitmap -Source $source -Size $size
        $stream = [System.IO.MemoryStream]::new()
        try
        {
            $frame.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally
        {
            $stream.Dispose()
            $frame.Dispose()
        }
    }

    $iconPath = Join-Path $resolvedAssets 'app-icon.ico'
    $file = [System.IO.File]::Open($iconPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = [System.IO.BinaryWriter]::new($file)
    try
    {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$frames.Count)
        $offset = 6 + (16 * $frames.Count)
        for ($index = 0; $index -lt $frames.Count; $index++)
        {
            $size = $sizes[$index]
            $dimension = if ($size -eq 256) { 0 } else { $size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$index].Length
        }

        foreach ($bytes in $frames)
        {
            $writer.Write($bytes)
        }
    }
    finally
    {
        $writer.Dispose()
        $file.Dispose()
    }
}
finally
{
    $source.Dispose()
}
