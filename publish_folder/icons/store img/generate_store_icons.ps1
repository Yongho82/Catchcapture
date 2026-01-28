Add-Type -AssemblyName System.Drawing

# 경로를 현재 폴더에 맞춰 수정 (img vs icon 확인)
$sourcePath = "C:\yonghoprogram\Catchcapture\icons\store img\icon_orignal.png"
$outputDir = "C:\yonghoprogram\Catchcapture\icons\store img\output_store"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir }

try {
    $srcImg = [System.Drawing.Image]::FromFile($sourcePath)
} catch {
    Write-Error "Could not load image: $sourcePath"
    exit
}

# 기존 generate_icons.ps1의 고품질 리사이징 로직 그대로 사용
function Resize-Image {
    param($width, $height, $name)
    $destImg = New-Object System.Drawing.Bitmap($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($destImg)
    
    # 깨짐 방지를 위한 고품질 설정
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    
    # 비율 유지 및 중앙 정렬 로직 (기존 스크립트 기반)
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
    Write-Host "생성 완료: $name ($width x $height)"
}

Write-Host "고품질 아이콘 생성 시작..."

# 요청하신 스토어 등록용 사이즈
Resize-Image 300 300 "icon_300.png"
Resize-Image 150 150 "icon_150.png"
Resize-Image 71 71 "icon_71.png"

$srcImg.Dispose()
Write-Host "작업이 완료되었습니다. 위치: $outputDir"
