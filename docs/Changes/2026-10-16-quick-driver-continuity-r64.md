# 퀵 기사 후속 콜·현재 업무 유지 r64

- 사용자 요청: 이동 중에도 후속 콜을 받고 기존 배송을 계속 처리할 수 있도록 r63에서 확인한 네 가지를 보완한다.
- 상태: `Implemented / LocalValidated / StandaloneApkBuilt / SignatureVerified / DeviceDeferred / OperationalConfigurationUnchanged`.
- 기준선: `dev/mirror-integration`, `b02f541aa00156376e6e33525753cd8a983d6671`. 관련 없는 미커밋 작업을 보존했고 commit/push는 하지 않았다.
- 정본: [승인 명세](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/quick-driver-continuity.implementation.r64.md) · [작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/quick-driver-continuity.r64.work-order.json) · [화면 책임](../ProjectOverview/page-docs/cargo-driver-native-continuity-r64.md).

## 바뀐 업무 흐름

```text
운행 시작·정본 확인
├─ 최초 수신 ON: 시작 전에 본 판본과 같을 때만 저장
│  └─ 기사님이 그사이 OFF로 바꾸었거나 자동중지됨 → 최신 의사·중지 보존
└─ 신규 배차받기 ON + 서버 제한 없음
   ├─ 유휴 기사 → 현재 GPS에서 픽업 가능성 검토
   └─ 수행 기사 → 이미 맡은 전체 업무의 마지막 목적지·완료 ETA에서 검토
      ├─ 실제 경로/일정/차량/적재/시간창 근거 부족 → 새 제안 보류
      └─ 적격 B 제안 → 실제 화면 표시 ACK → 기사 수락
         ├─ A 카드·경로 유지 → A 처리 → 서버의 현재 업무가 B이면 B로 전환
         └─ 수락 응답 미확인 → 기존 요청 보존·정본 재조회

신규 수신 OFF / 연속 3회 표시된 제안 미응답
├─ 신규 후보·제안·수락·연속 탐색 중지
├─ 이미 수락한 A 조회·복구·완료 유지
└─ 기사 본인이 지도 좌측 상단 토글 ON → 명시 재개
```

## 구현과 책임

1. **서버 수신 관문**: 기존 공통 수신 Policy를 화물 후보·추천 조회/저장·신규 수락·연속 탐색에 연결했다. 조회 실패는 신규 허용으로 바꾸지 않는다. 진행/완료한 본인 A의 동일 수락 결과는 먼저 확인하며 새 상태나 시각을 쓰지 않는다.
2. **전체 수행 종료 근거**: 기존 운송 원장·일정 구성·실제 경로 평가를 재사용하는 `화물후속픽업기준Service`를 추가했다. 현재 GPS 후보 최대 100명과 제한된 활성 기사 후보 최대 300명을 합쳐 검토한다. 전체 남은 업무 최대 10건, 좌표·기한·정차 연결·경로 옵션·미반납 책임을 확인하고, 오래 걸리는 경로/적격성 조회 뒤 원장 판본과 수신 상태를 다시 읽는다. 직접 지정 추천은 자동 1등 여부 대신 해당 기사의 적격성을 검사한다.
3. **기사 화면**: 네이티브 지도 좌측 상단 신규 수신 토글과 중지 안내를 연결했다. 현재 업무 A와 신규 추천 B를 분리하고, 전경 30초 보완 조회·복귀 즉시 조회·중복 합류·계정/세션/화면 수명 보호를 적용했다. 예외 및 실패 slice는 마지막 확인 업무를 남기고 변경 행동을 막는다. 기기 만료 타이머가 거절 POST를 만들지 않으며, HTTP 408/429·응답 유실도 확정 거절로 처리하지 않는다.
4. **최초 ON 경합 보호**: 시작 전에 읽은 의사 판본을 서버의 Serializable 비교·추가 경계에서 확인한다. 최신 수동 OFF·동일 시각 변경·같은 요청 재생을 보존하고, 자동중지 해제는 기사 본인의 토글 ON만 수행한다. 기존 원장 기록 시각을 내부 정렬에 사용하며 새 테이블·migration을 만들지 않았다.
5. **연속 API 분류**: 기존 국내 화물 실행 Feature `DomesticTransportWorkflow`로 연속배차 API를 분류했다. 음식/공통 Feature만 켜서 화물 실행이 열리거나 미분류 오류가 되는 경로를 구분한다.

## 검증

- Fast: `artifacts/local/validation/20261016-124550/`, 관련 시험 **474개 통과**, 실패·건너뜀 0개. `Ssalddel.v3.5.slnx` 빌드와 정적 차이 검사도 통과했다.
- Task: `artifacts/local/validation/20261016-124719/`, 제품 전체 **12,226개 통과**, 실패 0개·환경 의존 **16개 미실행**(MySQL 1개·MongoDB 15개). 같은 제품 solution 빌드와 정적 차이 검사를 통과했다.
- Android 컴파일: `android-driver-build.log`, 오류 0개·기존 종속성 경고 27개. `EmbedAssembliesIntoApk=true` 단독 APK 패키징은 `android-driver-standalone-apk.log`에서 오류 0개·경고 33개로 완료했다. 기본 Debug build/Sign target은 빠른 배포 때문에 이전 APK가 그대로 남을 수 있어, 이를 최신 단독 APK 완료로 사용하지 않았다.
- 새 APK: `DriverApp/bin/Debug/net10.0-android/kr.hongdal.driver-Signed.apk`, **161,067,645 bytes**, SHA-256 `11bb2c81e8c5949e3e1f62ed94361afecf6e8af971faf84e59da8a85a1b3a080`. 최신 생성 시각·Manifest·DEX·arm64/x64 앱/계약 DLL 포함을 확인했고 `apksigner verify --verbose`의 v1/v2/v3 서명 검사가 통과했다. Debug 서명이며 실제 단말 설치·실행은 하지 않았다. `apk-evidence.json`·`apk-signature-final.log`에 근거가 있다.
- APK의 arm64/x64 각각 DriverApp·Contracts·UI DLL ELF payload **6개 모두** 현재 빌드 DLL과 크기·SHA-256이 일치했다. `apk-managed-payload-comparison.json`과 재현 스크립트 `verify-apk-managed-payloads.ps1`에 남겼다. APK 원본은 변경하지 않았다.
- 기존 AndroidX 종속성 범위 경고와 APK의 META-INF 부가자료에 대한 v1 서명 경고는 남아 있다. 서명 검사는 exit 0·v2/v3 true였으며 이번에 의존성 버전이나 APK 원본을 임의로 바꾸지 않았다.
- 코드 지도 생성과 r64 새 문서·목차 링크 15개 확인을 통과했다. 정책 메타데이터 회귀에 새 종료 기준·신규 수신 관문을 명시했고, 56개 직접 관련 제품/시험 경로의 SHA-256을 `source-hashes.json`에 남겼다.
- 로그: `artifacts/local/validation/20261016-quick-driver-continuity-r64/`. `owned-product-paths.txt`는 이번 보완에 직접 참여한 경로이며 같은 파일의 기존 미커밋 변경까지 새 작업으로 소유한다는 뜻은 아니다.
- 첫 집중 검사는 컴파일 문제와 기존 시험 입력의 신규 수신 동의 누락을 드러냈고 이를 수정했다. 예비 전체 검사는 당시 DLL에서 12,198개 통과·13개 실패·16개 환경 의존 미실행이었다. 이 숫자를 최종 검증으로 사용하지 않는다.
- 정책·앱 상태·인증 client·실제 API Feature 필터·SQLite 원장/경합 검증을 실제 물리폰 UI·푸시·주행·알림음 확인과 구분한다.

## 남은 실제 환경 확인

- 새 종료 기준의 정확성 옵션은 기본 true이다. 기존 `FreightContinuity.Enabled=false`, `ShadowMode=true`, SharedDriverWork·자동 운영 worker·요금 정책의 기존 기본 비활성은 변경하지 않았다.
- 완료 ETA는 기존에 기록된 남은 일정과 경로로 계산한다. 미등록 피킹·대기·하역 작업 시간을 새로 추정하거나 실제 도착을 보장하지 않는다. 후보 상한 밖 전체 기사에 대한 전수 최적화도 아니다.
- 단말 설치·실제 지도/GPS·전경/백그라운드 푸시·배너와 알림음·실제 주행은 후속 USB 검증 대상이다. 브라우저 미리보기의 기존 보안 차단은 재시도하거나 우회하지 않았다. 이 변경에 대한 픽셀 화면 검증을 완료했다고 표시하지 않는다.
- 운영 MySQL 동시성·외부 서버·실제 배차/송금·모델 학습·배포·commit/push는 이번 결과에 포함하지 않는다.
