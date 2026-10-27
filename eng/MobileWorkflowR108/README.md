# r108 로컬 모바일 업무·응답 유실 검증

잘못된 합성 사진을 서버가 확정 거절한 경우에는 기존 수락 업무를 지우거나 새 의뢰로 바꾸지 않는다. `resumeRequestId`와 같은 의뢰의 `previousResultPath`를 private 입력에 추가해 현재 `Assigned`·사진 없음 정본부터 다시 읽는다. 앞선 수락 응답 유실 근거의 ID와 hash를 연결하고, 유효한 새 합성 촬영으로 픽업·전달의 네 성공 응답 유실을 이어 검사한다. 원 실패 결과는 보존하며, 두 실행을 한 번의 무실패 실행으로 합치지 않는다.

현재 통합앱이 사용하는 공통 ViewModel, 생활 배송 typed client와 JSON/protected client를 그대로 소비한다. 보호 메타데이터가 있는 DTO에만 제품 암호화 모듈을 호출하는 기존 정책을 유지하며, 이 실행에서 실제 호출한 횟수도 기록한다. 별도 로컬 API의 합성 제안 한 건을 수락하고 픽업·전달 증빙과 완료를 처리한다. 실제 서버가 성공 응답을 반환한 뒤 클라이언트 경계에서 응답만 한 번씩 버리고, 암호화한 파일의 미확정 요청을 새 VM/세션에서 읽어 정본 GET·사진 영수증으로 복구한다. 수락·픽업 사진·픽업 완료·전달 사진·전달 완료의 다섯 번만 쓰고 조회 복구는 추가 POST를 보내지 않는지 검사한다.

실제 API·원장 저장은 서버 담당의 별도 검증과 대조한다. 현재 위치·촬영은 합성 adapter이며 Android 카메라/GPS·MAUI SecureStorage·앱 프로세스 재시작·FCM·실제 수령/송금 완료는 인증하지 않는다. 전송 암호화가 필요한 경우 제품 모듈을 Node의 WebCrypto에서 실행하며 브라우저 JSInterop 왕복 검증과 구별한다. 해당 DTO의 메타데이터가 암호화를 요구하지 않아 호출하지 않았다면 실제 암호화 검증으로 보고하지 않는다.

서버 담당이 전용 시험 계정과 아직 유효한 제안의 입력 JSON을 Git 제외 `artifacts/local/.../.private/`에 마련한다. 필드는 `baseAddress`(127.0.0.1의 루트 HTTP 주소), `owner`, `accessToken`, `offerId`, `pickupLatitude`, `pickupLongitude`, `samplePhotoPath`, `nodeExecutable`이다. 토큰 원문은 콘솔·공개 문서에 기록하지 않는다. 검증 결과 출력은 아직 없는 `artifacts/local/` 하위 경로여야 한다. 수락부터 시작하는 새 정상 실행은 새 제안을 준비한다. 이미 수락한 업무의 확정 거절 뒤 재촬영을 확인할 때만 상단의 명시적 `resumeRequestId`·`previousResultPath`로 같은 업무를 이어가며, 해당 업무를 삭제하거나 새 정상 실행으로 재포장하지 않는다.

전체 제품 빌드와 동시 실행하지 말고 담당과 순서를 조율하여 빌드한다. 실행 DLL은 격리 사본으로 옮겨 공용 bin 파일 잠금을 피한다. 실행 예시의 private 입력이나 실제 토큰을 Git에 추가하지 않는다.

```powershell
dotnet build eng/MobileWorkflowR108/MobileWorkflowR108.csproj --nologo
dotnet <isolated-output>/MobileWorkflowR108.dll <private-input.json> artifacts/local/local-operations-r108/mobile/http-run-01
```

실제 화면 표본은 기존 `RoleWorkspacePreview`의 생활 배송 Razor·VM 검사를 새 출력에 실행하되, 그 화면의 in-process 모의 client 결과를 이 HTTP 흐름과 동일 실행으로 합치지 않는다.

`verify-ui.ps1`는 기존 상태별 렌더/버튼 검사를 새 출력으로 실행한다. 이 호스트는 컴파일한 출력 전체를 별도 사본으로 옮긴 뒤 실행하며 `--contentRoot`에는 실제 `eng/RoleWorkspacePreview`의 절대 경로를 사용한다. 상대 경로를 DLL 사본 폴더 아래로 해석한 최초 시작 실패와 캡처 검증 실패는 보존한다. 새 출력을 지정해 재시도하며 원 실패 파일을 덮어쓰지 않는다.

## 기존 통합앱의 로컬 APK 갱신

`build-local-apk.ps1`는 기존 `publish-mobile-usb-test.ps1`를 그대로 재사용한다. 저장소 밖에 보존된 동일 Firebase SDK 구성·기존 Debug 서명키·설치된 Android SDK/JDK를 사용하고 새 버전/출력의 standalone Debug APK를 준비한다. 최종 소스·시험이 고정된 뒤 명시적으로 실행한다. `-PlanOnly`는 실제 APK 제작이나 설치가 아니다.

```powershell
pwsh -NoProfile -File eng/MobileWorkflowR108/build-local-apk.ps1 -Version 0.1.193 -VersionCode 193 -PlanOnly
```

버전은 예시다. 기존 완료 manifest가 있는 버전/출력을 덮어쓰지 않는다. APK의 `127.0.0.1:5363` 주소는 기존 USB 로컬 시험 계약이며 이 HTTP 검증의 전용 `5378`과 다르다. 이 제작으로 r108 서버에 휴대폰이 통신했다거나 APK에 r108 전용 주소가 포함됐다고 표시하지 않는다. 설치·USB 조작·GCS 업로드·FCM 발송은 실행하지 않는다.
