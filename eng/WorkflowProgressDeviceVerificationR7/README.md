# 업무 진행 알림 실기기 단발 검증 r7

제품의 `FirebaseFcmPushService` 원본을 링크하고 공통 `WorkflowProgressNotification`의 단계 문구·payload 검사를 사용한다. 제품 서버·worker·Outbox 상태나 금융 상태는 바꾸지 않는다. 이 도구는 캠페인이나 자동 재전송 기능이 아니며 명시적으로 선택한 실제 휴대폰 설치 하나에 합성 단계 알림 한 건만 보낸다.

루트 담당자가 실제 서버 DB에서 현재 활성 설치를 조회한 뒤 다음 입력을 `artifacts/local/workflow-progress-device/20261022-r1/.private/` 아래 새 JSON 파일에 저장한다. 원문 토큰·계정·키를 콘솔·스크린샷·tracked 설정에 복사하지 않는다. JSON의 필드는 `Program.cs`의 `DeviceInput`과 같다.

```json
{
  "schemaVersion": "workflow-progress-device-input.r7",
  "sampleId": "cargo-picked-01",
  "ownerUserId": "<합성 계정 ID>",
  "appKey": "ssalddel.shipper",
  "packageId": "com.companyname.ssalddelapp",
  "installationId": "<실제 등록 설치 ID>",
  "installationOwnerUserId": "<같은 합성 계정 ID>",
  "installationAppKey": "ssalddel.shipper",
  "installationPlatform": "Android",
  "installationActive": true,
  "activeInstallationCount": 1,
  "installationAppVersion": "<실제 등록 버전>",
  "registrationVerifiedAtUtc": "<10분 이내 실제 DB 조회 UTC>",
  "token": "<실제 등록 토큰>",
  "tokenSha256": "<토큰 UTF-8 SHA256 소문자 64자리>",
  "firebaseConfigurationPath": "<저장소 밖 일반앱 google-services.json 절대 경로>",
  "sourceKind": "SyntheticDeviceSample",
  "stageCode": "cargo.picked-up",
  "eventId": "<샘플 사건 SHA256 64자리>",
  "sourceId": "<합성 업무 원천 ID>",
  "deepLink": "/workspace/shipper",
  "occurredAtUtc": "<10분 이내 샘플 발생 UTC>"
}
```

서버 자격은 기존 `Ssalddel/appsettings.Local.json`의 `PushNotifications.ServiceAccountJsonPath`로만 읽는다. SDK·서버·기존 지정 ProjectId와 통합앱 패키지 일치를 확인한다. 서비스 계정과 SDK 파일은 저장소 밖에 있어야 하며 경로의 reparse point는 거절한다. 실제 DB 조회·설치 수 재검사는 호출자의 책임이다. 이 도구는 10분 이내 조회 snapshot과 토큰 해시·소유자·앱·활성 상태를 검사하지만 DB에 직접 연결하지 않는다.

빌드는 다른 앱 빌드와 간섭하지 않게 별도 산출물 경로를 지정한다.

```powershell
dotnet build eng/WorkflowProgressDeviceVerificationR7/WorkflowProgressDeviceVerificationR7.csproj --artifacts-path artifacts/local/workflow-progress-device/20261022-r1/tool-build
dotnet <빌드 DLL> --validate <비공개 입력.json> <아직 없는 검증 결과 폴더>
dotnet <빌드 DLL> --send <비공개 입력.json> <아직 없는 발송 결과 폴더>
```

`test-preflight.ps1 -DllPath <빌드 DLL> -FirebaseConfigurationPath <외부 SDK 설정>`는 합성 토큰으로 정상 사전검사와 소유자·설치수·앱·플랫폼·토큰 해시·경로·단계·시각·기존 결과 보호를 검사한다. `--validate`만 호출하며 실제 발송 명령이나 네트워크 요청은 실행하지 않는다.

두 결과 폴더는 `artifacts/local/workflow-progress-device/20261022-r1/` 하위이며 `.private/` 밖이어야 한다. `--validate`는 인증 파일과 payload를 읽기만 하며 네트워크를 호출하지 않는다. `--send`는 `.private/send-attempts/<sampleId>.json`을 `CreateNew`로 저장·flush한 다음에만 Google 인증과 FCM을 한 번 호출한다. 같은 sampleId는 새 출력 폴더에서도 재발송할 수 없다. 응답 유실·오류·제공자 거절 뒤 같은 사건을 자동 재시도하지 않는다. 의도적인 단말 중복 억제 시험만 새 sampleId와 기존 eventId를 명시해 별도 실험으로 구분한다.

메시지는 기존 계약의 8필드 data-only·high-priority다. 임의 제목·본문·연락처·사진·외부 링크는 입력할 수 없다. FCM 메시지 요청은 정확한 프로젝트의 HTTPS `fcm.googleapis.com/v1/projects/.../messages:send` POST로 제한하고 redirect를 막는다. Google 인증 라이브러리의 OAuth 교환은 별도 외부 요청이며 이 도구가 프로세스 전체 방화벽을 구현한 것은 아니다.

`ProviderAccepted`는 Google의 발송 접수다. 실제 OS 알림 게시·전경/배경 수신·잠금화면·소리 청취·알림 클릭·계정 보호·업무 재조회·업무 완료는 각각 별도 증거로 남긴다. 진행 알림의 시스템음과 배송제안의 ‘살뜰 배달’ 음성 채널도 구분한다.
