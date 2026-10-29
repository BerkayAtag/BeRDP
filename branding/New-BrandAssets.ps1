# Regenerates the red BeRDP brand assets (ICO, PNG and setup wizard bitmaps).
# Windows PowerShell 5.1+ with System.Drawing; run from any directory.
param([string]$OutputRoot = (Split-Path $PSScriptRoot -Parent))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$red = [Drawing.Color]::FromArgb(255, 240, 32, 43)
$redDark = [Drawing.Color]::FromArgb(255, 176, 0, 11)

function New-RoundedRectangle([single]$x, [single]$y, [single]$width, [single]$height, [single]$radius) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Add-CenteredLetter($graphics, [string]$text, [Drawing.RectangleF]$box, [Drawing.Brush]$brush) {
    $family = New-Object Drawing.FontFamily 'Segoe UI Black'
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddString($text, $family, [int][Drawing.FontStyle]::Regular, 100, (New-Object Drawing.PointF 0, 0), [Drawing.StringFormat]::GenericTypographic)
    $bounds = $path.GetBounds()
    $scale = [Math]::Min($box.Width / $bounds.Width, $box.Height / $bounds.Height)
    $matrix = New-Object Drawing.Drawing2D.Matrix
    $matrix.Translate($box.X + ($box.Width - $bounds.Width * $scale) / 2, $box.Y + ($box.Height - $bounds.Height * $scale) / 2)
    $matrix.Scale($scale, $scale)
    $matrix.Translate(-$bounds.X, -$bounds.Y)
    $path.Transform($matrix)
    $graphics.FillPath($brush, $path)
    $path.Dispose(); $family.Dispose()
}

# Small sizes show a bold "B"; larger sizes show a monitor carrying the "B".
function New-LogoBitmap([int]$size) {
    $bitmap = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.PixelOffsetMode = 'HighQuality'
    $graphics.Clear([Drawing.Color]::Transparent)
    $s = [single]$size
    $tile = New-RoundedRectangle 0 0 $s $s ($s * 0.22)
    $gradient = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.PointF 0, 0), (New-Object Drawing.PointF 0, $s), $red, $redDark
    $graphics.FillPath($gradient, $tile)
    $white = New-Object Drawing.SolidBrush ([Drawing.Color]::White)
    $redBrush = New-Object Drawing.SolidBrush $redDark
    if ($size -le 24) {
        Add-CenteredLetter $graphics 'B' (New-Object Drawing.RectangleF ($s * 0.25), ($s * 0.18), ($s * 0.5), ($s * 0.64)) $white
    } else {
        $screen = New-RoundedRectangle ($s * 0.17) ($s * 0.2) ($s * 0.66) ($s * 0.47) ($s * 0.07)
        $graphics.FillPath($white, $screen)
        Add-CenteredLetter $graphics 'B' (New-Object Drawing.RectangleF ($s * 0.39), ($s * 0.28), ($s * 0.22), ($s * 0.31)) $redBrush
        $graphics.FillRectangle($white, $s * 0.455, $s * 0.66, $s * 0.09, $s * 0.1)
        $base = New-RoundedRectangle ($s * 0.32) ($s * 0.75) ($s * 0.36) ($s * 0.07) ($s * 0.035)
        $graphics.FillPath($white, $base)
        $screen.Dispose(); $base.Dispose()
    }
    $graphics.Dispose(); $gradient.Dispose(); $white.Dispose(); $redBrush.Dispose(); $tile.Dispose()
    return $bitmap
}

function Get-PngBytes($bitmap) {
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    return $stream.ToArray()
}

# ICO entries <= 64 px use 32-bit DIBs for maximum shell compatibility; 128/256 use PNG.
function Get-DibBytes($bitmap) {
    $size = $bitmap.Width
    $stream = New-Object IO.MemoryStream
    $writer = New-Object IO.BinaryWriter $stream
    $maskRow = [int]([Math]::Ceiling($size / 32.0) * 4)
    $writer.Write([uint32]40); $writer.Write([int32]$size); $writer.Write([int32]($size * 2))
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]0)
    $writer.Write([uint32]($size * $size * 4 + $maskRow * $size))
    $writer.Write([int32]0); $writer.Write([int32]0); $writer.Write([uint32]0); $writer.Write([uint32]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $c = $bitmap.GetPixel($x, $y)
            $writer.Write([byte]$c.B); $writer.Write([byte]$c.G); $writer.Write([byte]$c.R); $writer.Write([byte]$c.A)
        }
    }
    $writer.Write((New-Object byte[] ($maskRow * $size)))
    $writer.Flush()
    return $stream.ToArray()
}

$brandingDirectory = Join-Path $OutputRoot 'branding'
$siteAssets = Join-Path $OutputRoot 'docs\assets'
$consoleAssets = Join-Path $OutputRoot 'src\BeRdp.AdminConsole\wwwroot\img'
foreach ($directory in @($brandingDirectory, $siteAssets, $consoleAssets)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = foreach ($size in $sizes) {
    $bitmap = New-LogoBitmap $size
    $bytes = if ($size -ge 128) { Get-PngBytes $bitmap } else { Get-DibBytes $bitmap }
    $bitmap.Dispose()
    [pscustomobject]@{ Size = $size; Bytes = $bytes }
}
$icon = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter $icon
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
$writer.Flush()
[IO.File]::WriteAllBytes((Join-Path $brandingDirectory 'BeRDP.ico'), $icon.ToArray())

foreach ($target in @(@{ Path = (Join-Path $siteAssets 'berdp-logo.png'); Size = 256 },
                      @{ Path = (Join-Path $consoleAssets 'berdp-logo.png'); Size = 128 },
                      @{ Path = (Join-Path $brandingDirectory 'berdp-logo-512.png'); Size = 512 })) {
    $bitmap = New-LogoBitmap $target.Size
    $bitmap.Save($target.Path, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

# Inno Setup wizard header images (100%, 150%, 200% DPI), white background BMP.
foreach ($pair in @(@(55, 'setup-small.bmp'), @(83, 'setup-small-150.bmp'), @(110, 'setup-small-200.bmp'))) {
    $size = $pair[0]
    $canvas = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $graphics.Clear([Drawing.Color]::White)
    $graphics.InterpolationMode = 'HighQualityBicubic'
    $logo = New-LogoBitmap ([int]($size * 0.86))
    $graphics.DrawImage($logo, [int](($size - $logo.Width) / 2), [int](($size - $logo.Height) / 2), $logo.Width, $logo.Height)
    $canvas.Save((Join-Path $brandingDirectory $pair[1]), [Drawing.Imaging.ImageFormat]::Bmp)
    $graphics.Dispose(); $logo.Dispose(); $canvas.Dispose()
}
Write-Host "Brand assets written to $brandingDirectory, $siteAssets and $consoleAssets"
