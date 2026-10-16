[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\GerberPanelizer.App\Assets\gerber-panelizer-icon.png'),
    [string]$Output = (Join-Path $PSScriptRoot '..\src\GerberPanelizer.App\Assets\gerber-panelizer.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
    throw "Icon source image was not found: $Source"
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source).Path)
$frames = [System.Collections.Generic.List[byte[]]]::new()

try {
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()

        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally {
            $stream.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $sourceImage.Dispose()
}

$outputDirectory = Split-Path -Parent $Output
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$outputPath = [System.IO.Path]::GetFullPath($Output)
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($outputPath))

try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$sizes.Count)

    $frameOffset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = $sizes[$index]
        $frame = $frames[$index]
        $writer.Write([byte]$(if ($dimension -eq 256) { 0 } else { $dimension }))
        $writer.Write([byte]$(if ($dimension -eq 256) { 0 } else { $dimension }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Length)
        $writer.Write([UInt32]$frameOffset)
        $frameOffset += $frame.Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame)
    }
}
finally {
    $writer.Dispose()
}
