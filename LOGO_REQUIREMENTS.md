# CatchCapture - 필요한 로고 이미지 목록

## 📐 Microsoft Store 필수 이미지

### 1. App Icon (앱 아이콘)
**Square 44x44 Logo**
- 크기: 44 x 44 픽셀
- 형식: PNG (투명 배경 권장)
- 용도: 작업 표시줄, 시작 메뉴 작은 타일
- 파일명: `Square44x44Logo.png`

**Square 150x150 Logo**
- 크기: 150 x 150 픽셀
- 형식: PNG (투명 배경 권장)
- 용도: 시작 메뉴 중간 타일
- 파일명: `Square150x150Logo.png`

### 2. Wide Tile (넓은 타일) - 선택사항
**Wide 310x150 Logo**
- 크기: 310 x 150 픽셀
- 형식: PNG
- 용도: 시작 메뉴 넓은 타일
- 파일명: `Wide310x150Logo.png`

### 3. Store Logo (스토어 로고)
**Store Logo**
- 크기: 50 x 50 픽셀
- 형식: PNG
- 용도: Microsoft Store 목록
- 파일명: `StoreLogo.png`

### 4. Splash Screen (시작 화면) - 선택사항
**Splash Screen**
- 크기: 620 x 300 픽셀
- 형식: PNG
- 용도: 앱 시작 시 표시
- 파일명: `SplashScreen.png`

---

## 📸 스크린샷 (필수)

### Store 목록용 스크린샷
- **최소**: 1개
- **권장**: 3-5개
- **크기**: 1366 x 768 픽셀 이상
- **형식**: PNG 또는 JPG
- **내용**: 주요 기능을 보여주는 화면

**추천 스크린샷:**
1. 영역 캡처 화면
2. 편집 도구 사용 화면
3. OCR 기능 화면
4. 간편 모드 화면
5. 트레이 모드 화면

---

## 🎨 현재 사용 가능한 이미지

### 프로젝트 내 아이콘
```
icons/
├── icon_main.png (79,728 bytes) - 크기 확인 필요
├── catcha.ico (15,678 bytes) - ICO 형식
├── catcha1.ico (6,605 bytes)
└── catcha2.ico (8,702 bytes)
```

### 필요한 작업
1. `icon_main.png`의 실제 크기 확인
2. 필요한 크기로 리사이즈
3. 투명 배경 PNG로 저장

---

## 🛠️ 이미지 생성 방법

### 방법 1: 온라인 도구 (가장 쉬움)
1. https://www.appicon.co/ 방문
2. `icon_main.png` 업로드
3. "Windows" 선택
4. 모든 크기 자동 생성
5. 다운로드

### 방법 2: Photoshop/GIMP
1. `icon_main.png` 열기
2. 이미지 → 크기 조정
3. 각 크기로 저장:
   - 44x44
   - 50x50
   - 150x150
   - 310x150
   - 620x300

### 방법 3: PowerShell 스크립트
```powershell
# 이미지 리사이즈 스크립트
Add-Type -AssemblyName System.Drawing

$source = "icons\icon_main.png"
$sizes = @(
    @{Width=44; Height=44; Name="Square44x44Logo.png"},
    @{Width=50; Height=50; Name="StoreLogo.png"},
    @{Width=150; Height=150; Name="Square150x150Logo.png"},
    @{Width=310; Height=150; Name="Wide310x150Logo.png"}
)

$img = [System.Drawing.Image]::FromFile((Resolve-Path $source))

foreach ($size in $sizes) {
    $newImg = New-Object System.Drawing.Bitmap($size.Width, $size.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($newImg)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($img, 0, 0, $size.Width, $size.Height)
    $newImg.Save("Assets\$($size.Name)", [System.Drawing.Imaging.ImageFormat]::Png)
    $newImg.Dispose()
    $graphics.Dispose()
}

$img.Dispose()
Write-Host "이미지 생성 완료!"
```

---

## 📁 권장 폴더 구조

```
CatchCapture/
├── Assets/                          # 새로 만들 폴더
│   ├── Square44x44Logo.png
│   ├── Square150x150Logo.png
│   ├── Wide310x150Logo.png
│   ├── StoreLogo.png
│   └── SplashScreen.png
├── Screenshots/                     # 새로 만들 폴더
│   ├── screenshot1.png
│   ├── screenshot2.png
│   ├── screenshot3.png
│   ├── screenshot4.png
│   └── screenshot5.png
└── icons/                           # 기존 폴더
    └── (기존 아이콘들)
```

---

## ✅ 체크리스트

### 로고 이미지
- [ ] Square44x44Logo.png (44x44)
- [ ] Square150x150Logo.png (150x150)
- [ ] Wide310x150Logo.png (310x150)
- [ ] StoreLogo.png (50x50)
- [ ] SplashScreen.png (620x300) - 선택사항

### 스크린샷
- [ ] 스크린샷 1 (영역 캡처)
- [ ] 스크린샷 2 (편집 도구)
- [ ] 스크린샷 3 (OCR 기능)
- [ ] 스크린샷 4 (간편 모드) - 선택사항
- [ ] 스크린샷 5 (트레이 모드) - 선택사항

---

## 🎯 다음 단계

1. **이미지 생성**: 위 방법 중 하나로 필요한 크기의 로고 생성
2. **Assets 폴더 생성**: 프로젝트에 `Assets` 폴더 만들기
3. **이미지 복사**: 생성된 이미지를 `Assets` 폴더에 복사
4. **스크린샷 촬영**: 앱 실행 후 주요 기능 스크린샷 촬영
5. **Package.appxmanifest 업데이트**: 이미지 경로 수정

준비되셨으면 Visual Studio에서 Packaging Project를 추가하세요!
