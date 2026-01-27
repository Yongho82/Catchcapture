---
description: 클라우드(Imgur 개인, Google Drive 등) 연동 및 이미지 업로드 링크 생성 기능 구현 절차
---

# 클라우드 업로드 및 링크 생성 구현 (OAuth 2.0)

이 워크플로우는 사용자가 외부 클라우드 서비스(Imgur Personal, Google Drive) 계정을 연동하고, 캡처된 이미지를 업로드하여 공유 링크를 생성하는 기능을 구현합니다.

## 1. 사전 준비 (개발자 콘솔 등록)
- **공통:** Redirect URI는 `http://127.0.0.1` (Loopback) 사용 권장.
- **Google Drive:** Google Cloud Console에서 `Desktop App`으로 프로젝트 생성 및 Client ID/Secret 발급.
- **Imgur:** Imgur API 등록 페이지에서 `OAuth 2 authorization with a callback URL` 선택 후 Client ID/Secret 발급.

## 2. 의존성 패키지 설치 (NuGet)
OAuth 인증 및 API 통신을 쉽게 하기 위해 검증된 라이브러리를 사용합니다.
- `Google.Apis.Auth`, `Google.Apis.Drive.v3` (구글용)
- `IdentityModel.OidcClient` (범용 OAuth용 - Imgur 등)
- `Newtonsoft.Json` (JSON 처리)

## 3. 설정(Settings) UI 구현
`SettingsWindow.xaml`에 `[링크/공유]` 탭을 추가하고 서비스 선택 UI를 구성합니다.

### UI 구성요소
1.  **서비스 목록 (Radio Group):**
    - **Imgur (Anonymous):** 기본값. 별도 로그인 불필요.
    - **Imgur (Personal):** [로그인] 버튼 필요. 로그인 성공 시 계정명 표시 및 [로그아웃] 버튼으로 변경.
    - **Google Drive:** [로그인] 버튼 필요. 로그인 성공 시 이메일 표시.
2.  **동작 방식:**
    - 라디오 버튼을 선택하면 해당 서비스가 `CurrentProvider`로 설정됨.
    - 단, 로그인이 필요한 서비스는 **로그인이 완료된 상태여야만 선택 가능**하도록 제약.

## 4. 토큰 관리자 (TokenManager) 구현
OAuth 인증 후 발급받은 `Access Token`, `Refresh Token`을 안전하게 저장하고 불러오는 클래스를 만듭니다.
- **저장 위치:** `Properties.Settings.Default`를 사용하거나 로컬 암호화 파일.
- **기능:**
  - `SaveToken(serviceName, tokenData)`
  - `GetToken(serviceName)`
  - `ClearToken(serviceName)`
  - `IsConnected(serviceName)` 확인 메서드

## 5. 인증 로직 (AuthService) 구현
각 서비스별 인증 흐름을 담당하는 서비스를 구현합니다.

### Google Drive
- `GoogleWebAuthorizationBroker.AuthorizeAsync` 메서드를 사용하면 브라우저 오픈 -> 로그인 -> 토큰 저장을 한 번에 처리해줍니다.

### Imgur
- `OidcClient`를 사용하거나 직접 `System.Net.HttpListener`를 사용하여 로컬 서버(127.0.0.1)를 열고, 브라우저를 띄워 인증 코드를 받아옵니다.

## 6. 업로드 로직 (UploadService) 구현
이미지 파일을 받아 실제 업로드를 수행하고 URL을 반환하는 인터페이스를 구현합니다.

```csharp
public interface IUploadProvider
{
    Task<string> UploadImageAsync(string filePath); // 리턴값: 생성된 링크 URL
}
```

- **ImgurProvider:** 기존 익명 업로드 로직 + (토큰이 있으면) Authorization 헤더에 Bearer Token 추가.
- **GoogleDriveProvider:** 구글 API를 통해 파일 업로드 -> '링크 공유 켜기' -> `WebViewLink` 반환.

## 7. 메인 로직 통합
`MainWindow` 또는 캡처 후처리 로직(`Copy to Clipboard` 등)에서 링크 생성 요청이 오면:
1. 설정에서 **현재 선택된 Provider** 확인.
2. 해당 Provider의 `UploadImageAsync` 호출.
3. 반환받은 URL을 클립보드에 복사.
4. **결과 알림:** "링크가 생성되었습니다 (Google Drive)" 트레이 알림 표시.

## 8. 예외 처리
- **토큰 만료:** 업로드 실패 시 401/403 에러가 나면 `Refresh Token`으로 토큰 갱신 시도 -> 재업로드.
- **용량 초과:** Google Drive 용량 부족 등의 경우 사용자에게 알림.
