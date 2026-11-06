param(
    [string]$OutputPath = "src\ClipboardApp.WinForms\Resources\app.ico"
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()

function New-RoundedPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(1.0, $size * 0.055)
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($size - ($pad * 2)), ($size - ($pad * 2)))
    $radius = [Math]::Max(3.0, $size * 0.22)
    $path = New-RoundedPath $rect $radius

    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 14, 165, 233),
        [System.Drawing.Color]::FromArgb(255, 20, 184, 166),
        45.0)
    $g.FillPath($gradient, $path)

    # Subtle top highlight
    $highlightRect = New-Object System.Drawing.RectangleF($rect.X, $rect.Y, $rect.Width, ($rect.Height * 0.52))
    $highlight = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $highlightRect,
        [System.Drawing.Color]::FromArgb(90, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255),
        90.0)
    $g.FillPath($highlight, $path)

    # Clipboard body
    $bodyW = $size * 0.52
    $bodyH = $size * 0.60
    $bodyX = ($size - $bodyW) / 2
    $bodyY = $size * 0.22
    $bodyRect = New-Object System.Drawing.RectangleF($bodyX, $bodyY, $bodyW, $bodyH)
    $bodyPath = New-RoundedPath $bodyRect ([Math]::Max(1.5, $size * 0.055))
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $g.FillPath($white, $bodyPath)
    $bodyPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 15, 118, 110), [Math]::Max(1.0, $size * 0.012))
    $g.DrawPath($bodyPen, $bodyPath)

    # Clipboard top clip
    $clipW = $size * 0.24
    $clipH = $size * 0.095
    $clipX = ($size - $clipW) / 2
    $clipY = $size * 0.175
    $clipRect = New-Object System.Drawing.RectangleF($clipX, $clipY, $clipW, $clipH)
    $clipPath = New-RoundedPath $clipRect ([Math]::Max(1.0, $size * 0.035))
    $teal = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 118, 110))
    $g.FillPath($teal, $clipPath)

    # Check mark
    $checkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 13, 148, 136), [Math]::Max(1.4, $size * 0.055))
    $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $checkPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $cx = $size * 0.35
    $cy = $size * 0.55
    $points = @(
        (New-Object System.Drawing.PointF($cx, $cy)),
        (New-Object System.Drawing.PointF(($size * 0.45), ($size * 0.65))),
        (New-Object System.Drawing.PointF(($size * 0.66), ($size * 0.42)))
    )
    $g.DrawLines($checkPen, $points)

    # Tiny sparkle
    $sparkPen = $null
    if ($size -ge 24) {
        $sparkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 255, 255, 255), [Math]::Max(1.0, $size * 0.018))
        $sparkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $sparkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $sx = $size * 0.76
        $sy = $size * 0.26
        $g.DrawLine($sparkPen, $sx, ($sy - ($size * 0.045)), $sx, ($sy + ($size * 0.045)))
        $g.DrawLine($sparkPen, ($sx - ($size * 0.045)), $sy, ($sx + ($size * 0.045)), $sy)
    }

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@($size, $ms.ToArray())

    $ms.Dispose()
    if ($sparkPen) { $sparkPen.Dispose() }
    $checkPen.Dispose()
    $teal.Dispose()
    $clipPath.Dispose()
    $bodyPen.Dispose()
    $white.Dispose()
    $bodyPath.Dispose()
    $highlight.Dispose()
    $gradient.Dispose()
    $path.Dispose()
    $g.Dispose()
    $bmp.Dispose()
}

$dir = Split-Path -Parent $OutputPath
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$tempDir = Join-Path $env:TEMP ("clipapp-icon-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir | Out-Null
$pngPaths = @()
foreach ($frame in $frames) {
    $size = [int]$frame[0]
    $path = Join-Path $tempDir ("icon-{0}.png" -f $size)
    [System.IO.File]::WriteAllBytes($path, [byte[]]$frame[1])
    $pngPaths += $path
}

Add-Type -Path (Join-Path (Split-Path -Parent $PSCommandPath) 'IcoBuilder.cs') -ReferencedAssemblies System.Drawing
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[IcoBuilder]::Build($OutputPath, [string[]]$pngPaths)
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Output "Created $OutputPath with $($frames.Count) sizes."
