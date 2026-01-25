# 🚀 CatchCapture Microsoft Store 제출 통합 가이드

이 파일은 앱 제출을 위한 **최종 체크리스트 및 가이드**입니다.

---

## 📋 1. 스토어 식별 정보 (매니페스트 설정 완료)
이 정보는 설정이 완료되었으며, `Package.appxmanifest` 파일에 반영되어 있습니다.

- **Package Name**: `Ezupsoft.8936E99798B`
- **Publisher**: `CN=B5EA7C81-B497-441C-8B15-483FD9F7C76A`
- **Publisher Display Name**: `Ezupsoft`
- **Reserved App Name**: `CatchCapture`

---

## 🛠️ 2. 최종 패키징 방법 (Visual Studio)
제출 준비가 완료되면 다음 순서로 패키지 파일을 만드세요.

1.  **솔루션 구성**: 상단 툴바에서 **`Release`** 와 **`x64`** (또는 x86/ARM64)를 선택합니다.
2.  **프로젝트 선택**: `CatchCapture.Package` 프로젝트를 마우스 오른쪽 버튼으로 클릭합니다.
3.  **메뉴 선택**: **[게시(Publish)]** -> **[앱 패키지 만들기(Create App Packages)]** 클릭.
4.  **배포 유형**: "Microsoft Store 콘텐츠" 선택 후 로그인하여 진행.
5.  **결과물**: 빌드가 완료되면 `.msixupload` 파일이 생성됩니다. 이 파일을 파트너 센터에 업로드하세요.

---

## ✅ 3. 제출 전 최종 체크리스트

### 앱 준비 및 자산
- [x] 모든 크기의 공식 로고 생성 및 배치 완료 (`icons/store img/Assets/`)
- [x] 16개국 언어별 스크린샷 준비 완료 (`icons/store img/Screenshots/`)
    *   파일명 예시: `MAIN1_ar.png`, `MAIN1_kor.png` 등
- [x] **개인정보 처리방침(Privacy Policy) URL** 등록 완료
    *   URL: [https://ezupsoft.com/catchcapture/privacy-policy.html](https://ezupsoft.com/catchcapture/privacy-policy.html)

### 패키징 설정
- [x] `Package.appxmanifest` 식별 정보(Identity) 업데이트 완료
- [x] `runFullTrust` 권한 설정 완료
- [ ] **Release/x64** 빌드 및 `.msixupload` 생성

### 파트너 센터 (Store Listing)
- [ ] 16개국 언어별 스토어 설명 입력 (엑셀 파일 참고)
- [ ] 언어별 스크린샷 업로드 (파일명의 국가 코드 확인)
- [ ] 가격 설정 (무료) 및 연령 등급(3+) 설정
- [ ] 패키지 파일 업로드 및 최종 [제출] 클릭

---

## 🎯 현재 진행 상황
1. **완료**: 아이콘 및 스크린샷 자산 준비, 매니페스트 식별 정보 동기화.
2. **진행 중**: 파트너 센터에 다국어 정보 입력 및 이미지 업로드.
3. **남은 과제**: 개인정보 처리방침 외부 링크 확보, 최종 패키지 빌드 및 제출.

---

## 📞 연락처 및 참조
- **ID**: 이지업소프트 (Ezupsoft) / `ezupsoft@gmail.com`
- **웹사이트**: [https://ezupsoft.com](https://ezupsoft.com)
- **도움말**: 마이크로소프트 파트너 센터 대시보드에서 검수 상태를 확인하세요.
