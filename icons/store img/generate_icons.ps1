Add-Type -AssemblyName System.Drawing

$sourcePath = "c:\yonghoprogram\CatchCapture\icons\store icon\icon_orignal.png"
$outputDir = "c:\yonghoprogram\CatchCapture\icons\store icon\output"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir }

try {
    $srcImg = [System.Drawing.Image]::FromFile($sourcePath)
} catch {
    Write-Error "Could not load image: $sourcePath"
    exit
}

function Resize-Image {
    param($width, $height, $name)
    $destImg = New-Object System.Drawing.Bitmap($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($destImg)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)
    
    # Calculate scaling to fit within the destination while maintaining aspect ratio
    $ratioX = $width / $srcImg.Width
    $ratioY = $height / $srcImg.Height
    $ratio = [Math]::Min($ratioX, $ratioY)
    
    $newWidth = [int]($srcImg.Width * $ratio)
    $newHeight = [int]($srcImg.Height * $ratio)
    
    $posX = [int](($width - $newWidth) / 2)
    $posY = [int](($height - $newHeight) / 2)
    
    $g.DrawImage($srcImg, $posX, $posY, $newWidth, $newHeight)
    $destImg.Save((Join-Path $outputDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $destImg.Dispose()
    Write-Host "Created: $name ($width x $height)"
}

Write-Host "Starting icon generation..."

# Standard sizes (100% scale)
Resize-Image 44 44 "Square44x44Logo.png"
Resize-Image 150 150 "Square150x150Logo.png"
Resize-Image 310 150 "Wide310x150Logo.png"
Resize-Image 50 50 "StoreLogo.png"
Resize-Image 620 300 "SplashScreen.png"
Resize-Image 24 24 "BadgeLogo.png"

# Scale 200 (Common for high DPI)
Resize-Image 88 88 "Square44x44Logo.scale-200.png"
Resize-Image 300 300 "Square150x150Logo.scale-200.png"
Resize-Image 620 300 "Wide310x150Logo.scale-200.png"
Resize-Image 100 100 "StoreLogo.scale-200.png"
Resize-Image 1240 600 "SplashScreen.scale-200.png"

# Target sizes for Square44x44Logo (Start menu, taskbar icons)
Resize-Image 16 16 "Square44x44Logo.targetsize-16.png"
Resize-Image 24 24 "Square44x44Logo.targetsize-24.png"
Resize-Image 32 32 "Square44x44Logo.targetsize-32.png"
Resize-Image 48 48 "Square44x44Logo.targetsize-48.png"
Resize-Image 256 256 "Square44x44Logo.targetsize-256.png"

# Altform unplated (Without background plate)
Resize-Image 16 16 "Square44x44Logo.targetsize-16_altform-unplated.png"
Resize-Image 24 24 "Square44x44Logo.targetsize-24_altform-unplated.png"
Resize-Image 32 32 "Square44x44Logo.targetsize-32_altform-unplated.png"
Resize-Image 48 48 "Square44x44Logo.targetsize-48_altform-unplated.png"
Resize-Image 256 256 "Square44x44Logo.targetsize-256_altform-unplated.png"

$srcImg.Dispose()
Write-Host "Generation complete. Files are in $outputDir"
