Add-Type -AssemblyName System.Drawing
$assets = Join-Path $PSScriptRoot '..\src\IracingSetupManager.App\Assets'
$original = Join-Path $assets 'AboutEmblem.png'
$logo = [Drawing.Image]::FromFile($original)
$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($size in $sizes) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($logo, 0, 0, $size, $size)
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    }
    finally {
        $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    }
}
$logo.Dispose()
Copy-Item -LiteralPath $original -Destination (Join-Path $assets 'AppLogo.png')
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $assets 'AppLogo.ico')))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = [byte]($frame.Size % 256)
        $writer.Write($dimension); $writer.Write($dimension)
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
}
finally { $writer.Dispose() }
