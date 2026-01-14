# CatchCapture - Microsoft Store 패키징 가이드

## 📋 준비 사항

### 필수 요구사항
- ✅ Windows 10/11
- ✅ Visual Studio 2022 (Community 이상)
- ✅ "Windows Application Packaging" 워크로드 설치됨
- ✅ 개발자 계정 (Microsoft Partner Center)

---

## 🎯 방법 1: Visual Studio 사용 (권장)

### 1단계: Windows Application Packaging Project 추가

1. Visual Studio에서 솔루션 열기
2. 솔루션 탐색기에서 솔루션 우클릭 → **추가** → **새 프로젝트**
3. "Windows Application Packaging Project" 검색 및 선택
4. 프로젝트 이름: `CatchCapture.Package`
5. 대상 버전: Windows 10, version 1809 (10.0; Build 17763) 이상
6. 최소 버전: Windows 10, version 1809 (10.0; Build 17763)

### 2단계: 참조 추가

1. `CatchCapture.Package` 프로젝트에서 **Applications** 폴더 우클릭
2. **참조 추가** 선택
3. `CatchCapture` 프로젝트 체크
4. **확인** 클릭

### 3단계: Package.appxmanifest 편집

1. `Package.appxmanifest` 더블클릭
2. **패키징** 탭:
   - 패키지 이름: `com.ezupsoft.catchcapture`
   - 패키지 표시 이름: `CatchCapture`
   - 버전: `1.0.0.0`
   - 게시자: `CN=EzUpSoft` (나중에 Partner Center에서 받은 것으로 변경)

3. **응용 프로그램** 탭:
   - 표시 이름: `CatchCapture`
   - 설명: `강력한 화면 캡처 및 편집 도구`
   - 로고 설정 (아래 참조)

4. **기능** 탭:
   - `runFullTrust` 체크 (필수!)

### 4단계: 로고 이미지 준비

Microsoft Store는 다양한 크기의 로고가 필요합니다:

**필수 이미지:**
- Square 44x44 Logo: 44x44 픽셀
- Square 150x150 Logo: 150x150 픽셀
- Wide 310x150 Logo: 310x150 픽셀 (선택)
- Store Logo: 50x50 픽셀

**현재 사용 가능한 이미지:**
- `icons/icon_main.png` (79728 bytes) - 크기 확인 필요
- `icons/catcha.ico` (15678 bytes)

**이미지 생성 방법:**
1. 기존 `icon_main.png`를 다양한 크기로 리사이즈
2. 온라인 도구 사용: https://www.appicon.co/
3. 또는 Photoshop/GIMP 사용

### 5단계: 패키지 빌드

1. `CatchCapture.Package` 프로젝트를 시작 프로젝트로 설정
2. 빌드 구성: **Release** / **x64** (또는 x86, ARM64)
3. **빌드** → **솔루션 빌드**
4. 성공하면 `.msix` 파일이 생성됨

**출력 위치:**
```
CatchCapture.Package\bin\x64\Release\net8.0-windows10.0.19041.0\
```

---

## 🎯 방법 2: 명령줄 도구 사용

### 1단계: Windows SDK 도구 확인

```powershell
# MakeAppx.exe 위치 확인
where.exe makeappx
```

일반적인 위치:
```
C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe
```

### 2단계: 릴리스 빌드

```powershell
cd c:\yonghoprogram\Catchcapture
dotnet publish -c Release -r win-x64 --self-contained false
```

### 3단계: 패키지 생성

```powershell
# MakeAppx.exe 경로 설정
$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe"

# MSIX 패키지 생성
& $makeappx pack /d "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish" /p "CatchCapture_1.0.0.0_x64.msix"
```

### 4단계: 서명 (개발 테스트용)

```powershell
# 인증서 생성 (테스트용)
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=EzUpSoft" -KeyUsage DigitalSignature -FriendlyName "CatchCapture Dev" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# PFX로 내보내기
$pwd = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath "CatchCapture_Dev.pfx" -Password $pwd

# 서명
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
& $signtool sign /fd SHA256 /a /f "CatchCapture_Dev.pfx" /p "YourPassword" "CatchCapture_1.0.0.0_x64.msix"
```

---

## 📤 Microsoft Store 제출

### 1단계: Partner Center 계정 생성

1. https://partner.microsoft.com/dashboard 방문
2. 개발자 계정 등록 (개인: $19, 회사: $99)
3. 계정 인증 완료

### 2단계: 앱 예약

1. Partner Center → **앱 및 게임** → **새 제품**
2. 제품 이름: `CatchCapture`
3. 이름 예약

### 3단계: 앱 정보 입력

**제품 설정:**
- 제품 이름: CatchCapture
- 카테고리: 생산성 도구
- 하위 카테고리: 스크린샷 및 화면 녹화

**속성:**
- 개인정보 처리방침 URL: `https://ezupsoft.com/catchcapture/privacy-policy.html`
- 웹사이트: `https://ezupsoft.com`
- 지원 연락처: `ezupsoft@gmail.com`

**연령 등급:**
- 모든 연령 (3+)

**가격 및 가용성:**
- 무료 (광고 없음)
- 모든 시장에서 사용 가능

### 4단계: Store 목록 작성

**설명 (한국어):**
```
CatchCapture - 강력한 화면 캡처 및 편집 도구

주요 기능:
✓ 영역 캡처 - 원하는 영역만 정확하게 캡처
✓ 스크롤 캡처 - 긴 웹페이지도 한 번에
✓ OCR 텍스트 인식 - 이미지에서 텍스트 추출
✓ 실시간 편집 - 펜, 도형, 텍스트 추가
✓ 다양한 모드 - 일반, 간편, 트레이 모드 지원

개인정보 보호:
CatchCapture는 개인정보를 수집하지 않습니다. 
모든 데이터는 사용자의 컴퓨터에만 저장됩니다.
```

**스크린샷 (필수 1개, 권장 3-5개):**
- 해상도: 1366x768 이상
- 주요 기능을 보여주는 화면

**키워드:**
```
스크린샷, 화면캡처, OCR, 스크롤캡처, 편집
```

### 5단계: 패키지 업로드

1. **패키지** 섹션으로 이동
2. `.msix` 또는 `.msixupload` 파일 업로드
3. 자동 검증 대기
4. 오류 없으면 다음 단계

### 6단계: 제출

1. 모든 섹션 완료 확인
2. **제출** 버튼 클릭
3. 인증 대기 (보통 1-3일)

---

## 🔍 인증 체크리스트

Microsoft Store 인증을 통과하려면:

- ✅ 앱이 크래시 없이 실행됨
- ✅ 개인정보 처리방침 URL이 유효함
- ✅ 모든 기능이 정상 작동함
- ✅ 앱 설명이 정확함
- ✅ 스크린샷이 실제 앱과 일치함
- ✅ 광고가 있다면 명시되어 있음
- ✅ 연령 등급이 적절함

---

## 🚨 자주 발생하는 문제

### 문제 1: "Publisher 불일치"
**해결:** Partner Center에서 받은 정확한 Publisher 값을 `Package.appxmanifest`에 입력

### 문제 2: "로고 크기 오류"
**해결:** 정확한 크기의 PNG 이미지 준비 (44x44, 150x150, 310x150, 50x50)

### 문제 3: "runFullTrust 권한 필요"
**해결:** `Package.appxmanifest`에서 `runFullTrust` capability 추가

### 문제 4: "앱이 시작되지 않음"
**해결:** 
- 모든 DLL이 패키지에 포함되었는지 확인
- `--self-contained true`로 빌드 시도

---

## 📞 도움이 필요하면

- Microsoft Store 문서: https://docs.microsoft.com/windows/uwp/publish/
- Partner Center 지원: https://partner.microsoft.com/support
- 이메일: ezupsoft@gmail.com

---

## ✅ 다음 단계

1. Visual Studio 2022 설치
2. Windows Application Packaging 워크로드 추가
3. 로고 이미지 준비 (44x44, 150x150, 310x150, 50x50)
4. 패키징 프로젝트 생성
5. 테스트 빌드
6. Partner Center 계정 생성
7. 앱 제출

**준비되셨으면 시작하세요!** 🚀
