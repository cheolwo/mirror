# 기사 새 추천의 단발 진동·홍달배달 안내음 r18

상태: Approved / ReadyToDispatch. 2026-10-27 사용자는 새 배차 추천에 진동과 음성을 각각 한 번만 제공하도록 로컬 구현을 지시했고, 안내 문구를 **홍달배달**로 정정했다. 현재 휴대폰의 소리·진동 확인은 할 수 없으므로 구현·자동시험·APK 포장과 실제 청취·진동·FCM 수신 검증을 구분한다.

## 작업지시와 수용 기준

- 기준: `dev/mirror-integration`, commit `d3a19163ba96a7dbed31028f639f3bf0c534ae79`. 같은 로컬 checkout이며 기존 dirty 변경을 보존하고 이번 경로의 수정 전 사본·SHA를 기록한다. 다른 계정/개발실로의 Git 인계가 아니다.
- 대상은 화물 기사 `DriverApp`. 새 서버 정본 추천 → 본인·세션·현재 근무·추천 유효기간·배차 수신·알림 설정 확인 → 기존 Android 알림 경로로 한 번 게시한다. 지도 전경 재조회와 기존 Push 복구가 동일 중복 방지 이력을 사용한다.
- 음성은 Windows 한국어 음성을 스피커 대신 WAV 파일로 합성한 **홍달배달**. 알림 하나의 짧은 음원과 단일 진동 패턴을 사용한다. 반복 재생·자동 수락·별도 runtime TTS를 추가하지 않는다.
- 소리/진동 독립 설정·야간 제한·Android 권한/앱 차단·기존 채널 무음/차단을 보존한다. 새 음원 채널로 기존 사용자 제한을 우회하지 않는다.
- 같은 설치의 본인/의뢰/추천 라운드 이력을 유지하고, 전경/Push 중복·재시작·계정 왕복의 반복 게시를 억제한다. OS 게시 전 이력을 확인/저장하며 저장 실패 때 소리를 먼저 내지 않는다. OS와 단말 저장 간 원자적 전달, 앱 데이터 삭제/재설치, 실제 전달 성공은 보장으로 주장하지 않는다.
- 전경의 늦은 응답은 페이지 이탈·백그라운드·계정/세션 변경 뒤 게시하지 않는다. 알림 실패가 업무 지도 새로고침을 실패로 만들지 않는다.
- 서버/배차 엔진/계약/FCM 자격/운영 플래그/음식 기사 앱은 변경하지 않는다. 실기기에 시험 알림·소리·진동을 발생시키지 않는다. commit/push·외부 배포를 하지 않는다.

## 허용 writePaths와 담당

- 전경 담당: `DriverApp/NativeDriverHomePage.xaml.cs`, 필요한 새 순수 전경 수명 helper `DriverApp/Services/DriverForegroundRecommendationSignal.cs`, 직접 회귀시험.
- Android 담당: `DriverApp/Platforms/Android/DriverCargoRecommendationAlertPublisher.cs`, `DriverApp/Platforms/Android/AndroidManifest.xml`, 필요한 기존 Preferences용 순수 helper `DriverApp/Services/DriverCargoAlertPublicationHistory.cs`, 직접 회귀시험.
- 루트 담당: `DriverApp/Platforms/Android/Resources/raw/hongdal_dispatch.wav`, 음원 생성·검증 도구와 manifest(`artifacts/local/driver-recommendation-signal-r18/`), `Ssalddel.Tests/Ssalddel.Tests.csproj`의 필요한 소스 링크, 관련 기존 회귀와 이번 구조 검증.
- 루트의 서비스 최소 보완: `DriverApp/Services/DriverCargoRecommendationAlertService.cs`의 취소된 전경 조회 예외나 거절 응답이 다른 정상 알림을 전역 취소하지 않도록 기존 catch/거절 네 곳에서 토큰·세션·generation 확인과 실제 취소를 같은 게시 gate 안에 묶고 관련 회귀를 추가한다. 교차검토에서 발견한 검사→취소 사이의 경쟁도 이 범위에 포함한다.
- 문서: 이 PLAN, `docs/AI/CURRENT_WORK.md`, `docs/AI/PLANNING.md`, `docs/Changes/README.md`, 이번 변경 보고. 필요 음원 근거는 보고와 같은 비공개 아닌 기술 자산으로 보존한다.
- `artifacts/local/driver-recommendation-signal-r18/`의 사본·해시·검증·APK 자료.

## 검증

1. 단발 게시 이력·중복/동시 요청·새 라운드·계정 왕복·취소/만료·전경 수명 경계와 기존 설정/Push 복구 회귀를 수행한다.
2. 음원 문구/PCM 헤더·길이·SHA·단일 합성 문장과 코드의 이름 기반 리소스 URI, 채널 이전 시 무음/차단 유지, Manifest 권한을 대조한다. 오프라인 합성/디코딩은 실제 발음 청취 검증이 아니다.
3. 영향 Android 빌드 및 `validate-changes.ps1 -Paths`의 Fast/Task를 실제 선택 범위·pass/fail/skip과 함께 기록한다. APK를 포장하면 최종 raw WAV·병합 Manifest·해시를 확인한다.
4. UI 배치 변경·서버 업무 규칙 변경이 없으므로 기존 r17 실기기/격리 서버 근거를 이번 알림의 실제 수신/청취/진동 증거로 승계하지 않는다. 이후 기기에서 첫 추천·중복·다음 추천·무음·진동 OFF·DND·배경 FCM을 따로 확인한다.
