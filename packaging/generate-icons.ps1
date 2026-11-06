# Generates the app icon (a green + on black) at every size the MSIX package
# and the .exe need. Re-run after changing the colours or proportions.
#   packaging\Assets\*.png      MSIX logos, tiles and splash screen
#   src\ChainCalculator\app.ico icon embedded in the .exe and shown on the window

Add-Type -AssemblyName System.Drawing

$Background = [System.Drawing.Color]::Black
$Plus = [System.Drawing.ColorTranslator]::FromHtml('#39FF7A')

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $PSScriptRoot 'Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

# Draws a black canvas with a centred +. $scale is the plus arm length as a
# fraction of the shorter side. Edges are snapped to whole pixels so the
# small sizes stay crisp.
function New-PlusBitmap([int]$width, [int]$height, [double]$scale) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($Background)
    $brush = New-Object System.Drawing.SolidBrush $Plus

    $side = [Math]::Min($width, $height)
    $length = [int][Math]::Round($side * $scale)
    $thickness = [Math]::Max(2, [int][Math]::Round($length * 0.22))
    # Matching parity with the canvas keeps the plus exactly centred.
    if (($length % 2) -ne ($side % 2)) { $length++ }
    if (($thickness % 2) -ne ($side % 2)) { $thickness++ }

    $cx = [int](($width - $thickness) / 2)
    $cy = [int](($height - $thickness) / 2)
    $g.FillRectangle($brush, $cx, [int](($height - $length) / 2), $thickness, $length)
    $g.FillRectangle($brush, [int](($width - $length) / 2), $cy, $length, $thickness)

    $brush.Dispose(); $g.Dispose()
    return $bmp
}

function Save-Png([string]$name, [int]$width, [int]$height, [double]$scale) {
    $bmp = New-PlusBitmap $width $height $scale
    $bmp.Save((Join-Path $assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$iconScale = 0.7   # app list, taskbar, store logo, .ico
$tileScale = 0.5   # Start menu tiles and splash leave more breathing room

foreach ($s in 100, 200, 400) {
    $f = $s / 100
    Save-Png "Square44x44Logo.scale-$s.png"   (44 * $f)  (44 * $f)  $iconScale
    Save-Png "Square150x150Logo.scale-$s.png" (150 * $f) (150 * $f) $tileScale
    Save-Png "Wide310x150Logo.scale-$s.png"   (310 * $f) (150 * $f) $tileScale
    Save-Png "StoreLogo.scale-$s.png"         (50 * $f)  (50 * $f)  $iconScale
}
foreach ($s in 100, 200) {
    Save-Png "SplashScreen.scale-$s.png" (620 * $s / 100) (300 * $s / 100) $tileScale
}
foreach ($t in 16, 24, 32, 48, 256) {
    Save-Png "Square44x44Logo.targetsize-$t.png" $t $t $iconScale
    Save-Png "Square44x44Logo.altform-unplated_targetsize-$t.png" $t $t $iconScale
}

# .ico with PNG-compressed entries (supported since Windows Vista).
$sizes = 16, 24, 32, 48, 256
$images = foreach ($size in $sizes) {
    $bmp = New-PlusBitmap $size $size $iconScale
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }   # 0 means 256
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
[System.IO.File]::WriteAllBytes((Join-Path $root 'src\ChainCalculator\app.ico'), $out.ToArray())
$w.Dispose()

"Icons written to $assets and src\ChainCalculator\app.ico"
