#Requires -Version 7
# 產生 Windows 版的 App 圖示 Windows/Resources/AppIcon.ico：
# 和 macOS 版（scripts/make-icon.swift）相同的四疊半風車排列、深色木地板底座、藺草色的榻榻米與黑色布邊，
# 畫成 16、24、32、48、64、128、256 像素，打包成一個多尺寸的 ICO。
#
# 用法（只在圖示設計改變時執行，並 commit 產生的檔案）：
#   pwsh scripts/make-icon-windows.ps1
param(
    [string]$Output = (Join-Path $PSScriptRoot '..' 'Windows' 'Resources' 'AppIcon.ico')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256

# 和 TatamiIcon 相同的排列（3×3 單位，原點左上）
$mats = @(
    @{ X = 0; Y = 0; W = 2; H = 1 }  # 上方，橫放
    @{ X = 2; Y = 0; W = 1; H = 2 }  # 右方，直放
    @{ X = 1; Y = 2; W = 2; H = 1 }  # 下方，橫放
    @{ X = 0; Y = 1; W = 1; H = 2 }  # 左方，直放
    @{ X = 1; Y = 1; W = 1; H = 1 }  # 中間的半疊
)

function Get-Color([int]$hex, [double]$alpha = 1) {
    [System.Drawing.Color]::FromArgb([int][Math]::Round($alpha * 255), ($hex -shr 16) -band 0xFF, ($hex -shr 8) -band 0xFF, $hex -band 0xFF)
}

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    if ($d -le 0) {
        $path.AddRectangle([System.Drawing.RectangleF]::new($x, $y, $w, $h))
        return $path
    }
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# 以 1024 為設計單位，依實際大小縮放；小圖示省略細節（木紋、藺草紋路、布邊），只留清楚的形狀
function New-IconBitmap([int]$size) {
    $s = $size / 1024.0
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # 底座：圓角方形的深色木地板（Windows 圖示比 macOS 少留邊）
    $margin = [Math]::Max(24 * $s, 0.5)
    $baseSize = $size - $margin * 2
    $base = New-RoundedRect $margin $margin $baseSize $baseSize (180 * $s)
    $baseBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, $margin), [System.Drawing.PointF]::new(0, $margin + $baseSize),
        (Get-Color 0x4A4139), (Get-Color 0x2C2621))
    $g.FillPath($baseBrush, $base)
    if ($size -ge 64) {
        # 木地板的直向木紋
        $g.SetClip($base)
        $pen = [System.Drawing.Pen]::new((Get-Color 0x000000 0.18), [Math]::Max(2 * $s, 1))
        for ($i = 0; $i -lt 14; $i++) {
            $x = $margin + $i * $baseSize / 14
            $g.DrawLine($pen, [float]$x, [float]$margin, [float]$x, [float]($margin + $baseSize))
        }
        $g.ResetClip()
    }

    # 榻榻米
    $inset = 96 * $s
    $areaX = $margin + $inset
    $areaSize = $baseSize - $inset * 2
    $unit = $areaSize / 3
    $gap = [Math]::Max(8 * $s, 1)
    $heri = 22 * $s
    $radius = [Math]::Max(10 * $s, 0.6)

    foreach ($mat in $mats) {
        $x = $areaX + $mat.X * $unit + $gap / 2
        $y = $areaX + $mat.Y * $unit + $gap / 2
        $w = $mat.W * $unit - $gap
        $h = $mat.H * $unit - $gap
        $isHorizontal = $mat.W -gt $mat.H
        $isHalf = $mat.W -eq $mat.H
        $path = New-RoundedRect $x $y $w $h $radius

        # 藺草的底色，帶一點由左上往右下的明暗
        $fill = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.PointF]::new($x, $y), [System.Drawing.PointF]::new($x + $w, $y + $h),
            (Get-Color 0xDCD3A0), (Get-Color 0xC2B783))
        $g.FillPath($fill, $path)
        $g.SetClip($path)

        if ($size -ge 128) {
            # 藺草紋路：細線與疊的短邊平行，深淺交錯
            $step = 6 * $s * 2
            $length = if ($isHorizontal) { $w } else { $h }
            $index = 0
            for ($offset = 0.0; $offset -lt $length; $offset += $step / 2) {
                $color = if ($index % 2 -eq 0) { Get-Color 0x8C8150 0.2 } else { Get-Color 0xF6EFC8 0.16 }
                $pen = [System.Drawing.Pen]::new($color, [Math]::Max(1.5 * $s * 2, 1))
                if ($isHorizontal) {
                    $g.DrawLine($pen, [float]($x + $offset), [float]$y, [float]($x + $offset), [float]($y + $h))
                } else {
                    $g.DrawLine($pen, [float]$x, [float]($y + $offset), [float]($x + $w), [float]($y + $offset))
                }
                $index++
            }
        }

        if ($size -ge 48) {
            # 黑色布邊：整疊縫在兩條長邊上；半疊縫在上下兩邊
            $heriBrush = [System.Drawing.SolidBrush]::new((Get-Color 0x15130F))
            if ($isHorizontal -or $isHalf) {
                $g.FillRectangle($heriBrush, [float]$x, [float]$y, [float]$w, [float]$heri)
                $g.FillRectangle($heriBrush, [float]$x, [float]($y + $h - $heri), [float]$w, [float]$heri)
            } else {
                $g.FillRectangle($heriBrush, [float]$x, [float]$y, [float]$heri, [float]$h)
                $g.FillRectangle($heriBrush, [float]($x + $w - $heri), [float]$y, [float]$heri, [float]$h)
            }
        }
        $g.ResetClip()
    }

    $g.Dispose()
    return $bitmap
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return $stream.ToArray()
}

# ICO 裡的 BMP 格式：BITMAPINFOHEADER（高度是兩倍）+ 由下往上的 BGRA 像素 + 1 位元的 AND 遮罩
function Get-DibBytes([System.Drawing.Bitmap]$bitmap) {
    $size = $bitmap.Width
    $rect = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $size * 4
    $pixels = [byte[]]::new($stride * $size)
    for ($row = 0; $row -lt $size; $row++) {
        [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($data.Scan0, $row * $data.Stride), $pixels, $row * $stride, $stride)
    }
    $bitmap.UnlockBits($data)

    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    $writer.Write([int]40)            # biSize
    $writer.Write([int]$size)         # biWidth
    $writer.Write([int]($size * 2))   # biHeight（色彩 + 遮罩）
    $writer.Write([int16]1)           # biPlanes
    $writer.Write([int16]32)          # biBitCount
    $writer.Write([int]0)             # biCompression = BI_RGB
    $writer.Write([int]($stride * $size + $maskStride * $size))
    $writer.Write([int]0); $writer.Write([int]0); $writer.Write([int]0); $writer.Write([int]0)
    for ($row = $size - 1; $row -ge 0; $row--) {
        $writer.Write($pixels, $row * $stride, $stride)
    }
    # AND 遮罩全為 0：透明度由 alpha 通道決定
    $writer.Write([byte[]]::new($maskStride * $size))
    $writer.Flush()
    return $stream.ToArray()
}

$images = foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    # 256 用 PNG 壓縮（檔案小很多），其他用 BMP，相容性最好
    $bytes = if ($size -ge 256) { Get-PngBytes $bitmap } else { Get-DibBytes $bitmap }
    $bitmap.Dispose()
    [pscustomobject]@{ Size = $size; Bytes = $bytes }
}

$stream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([int16]0)                  # reserved
$writer.Write([int16]1)                  # type = icon
$writer.Write([int16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension)      # width（0 代表 256）
    $writer.Write([byte]$dimension)      # height
    $writer.Write([byte]0)               # color count
    $writer.Write([byte]0)               # reserved
    $writer.Write([int16]1)              # planes
    $writer.Write([int16]32)             # bits per pixel
    $writer.Write([int]$image.Bytes.Length)
    $writer.Write([int]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
$writer.Flush()

$directory = Split-Path -Parent $Output
New-Item -ItemType Directory -Force -Path $directory | Out-Null
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath $directory).Path + [System.IO.Path]::DirectorySeparatorChar + (Split-Path -Leaf $Output), $stream.ToArray())
Write-Host "Wrote $Output ($($sizes -join ', ') px)"
