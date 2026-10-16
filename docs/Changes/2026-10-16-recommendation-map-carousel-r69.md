# 화물 기사 추천 지도와 좌우 카드 r69

2026-10-16. 구현·로컬 검증·최종 APK184 설치와 추천 화면 확인을 완료했다. APK183에서는 추천 두 건의 좌우 카드·선택 수락과 사진을 첨부한 수행 명령을 확인했다. APK184는 새 E/F 추천으로 자동 배너·별도 지도·양방향 카드 넘김·복귀를 별도로 확인했다.

사용자는 운행 지도에 추천 픽업·도착 핀을 먼저 표시하는 대신, 추천 배너를 누르면 새 지도 화면을 열고 하단에서 건별 카드를 좌우로 넘겨 확인하도록 요청했다. 이번 대상은 현재 확인 중인 화물 기사 앱이다. [페이지 책임 카드 r19](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#화물-기사-추천-지도와-좌우-카드-r19--r69)가 화면별 책임과 복귀 기준을 소유한다.

## 구현 범위

- 수행 지도는 현재 업무의 지도와 새 추천 배너를 유지한다.
- 독립 추천 지도는 전체 지도 위 하단 카드 한 장을 표시하고 추천 두 건이면 좌우로 두 장을 넘겨 본다. 수직 접기/펼치기를 추가하지 않는다.
- 선택한 카드의 픽업은 주황, 도착은 파랑 핀으로 표시한다. 좌표 미확인을 임의 좌표로 채우지 않는다.
- 기존 만료·수신 상태·경고 확인·한 건 수락·서버 재조회와 현재 업무 보존을 유지한다. 서버·음식 기사·Unity·정산 변경은 이번 범위가 아니다.

## 검증 상태

[새 추천 지도](../../DriverApp/NativeDriverRecommendationPage.cs)는 `CarouselView`의 실제 좌우 이동과 선택 카드 한 건의 지도·응답을 소유한다. 기존 수행 지도에서 추천 상세/수락 UI를 옮겼으며, 서버 상태나 배차 기준은 변경하지 않았다. [선택 상태](../../DriverApp/Models/Driver/DriverRecommendationSelectionState.cs)는 순서 변경·만료·동일 의뢰의 새 추천 라운드를 구분한다.

홈과 추천 지도는 기존 `DriverNativeHomeWorkspaceState`를 같은 기사 작업공간으로 공유하고 화면 이동 시 `Pause`한다. 현재 업무 A와 수락 결과가 불명확한 B를 유지하고 정본을 확인한다. 다른 기사/세션으로 변경하면 이전 미확정 상태를 버린다. 이는 동일 프로세스 내 화면 이동 보존이며 앱 프로세스 종료 후 영속 복구 구현으로 확대하지 않는다.

[UTC 수정 판본의 Fast 검증](../../artifacts/local/validation/20261016-recommendation-map-r69/fast-r3.log)에서 앱·시험 프로젝트 빌드와 **53개 통과, 실패·생략 0개**를 확인했다. [선택·경로 시험](../../Ssalddel.Tests/Clients/Cargo/DriverRecommendationSelectionR69Tests.cs)과 [화면 이동·현재 업무 회귀](../../Ssalddel.Tests/Clients/CargoNativeHomeWorkflowTests.cs)를 포함한다. 현재 위치 또는 현재 업무 끝점이 추천 픽업과 같을 때 전달 구간이 사라지는 반례를 발견해 [독립 경로 builder](../../DriverApp/Services/DriverHomeRoutePlanningService.cs)로 수정했고, 잘못된 좌표·미확인 좌표와 함께 시험했다.

카드의 내부 API 설명 한 줄을 제거한 최종 판본은 [Fast-r4](../../artifacts/local/validation/20261016-recommendation-map-r69/fast-r4.log)에서 앱·시험 프로젝트 빌드와 **43개 통과, 실패·생략 0개**를 확인했다. 방향 시험 클래스 필터 이름이 맞지 않아 이전 53개 중 방향 시험 10개는 이 집중 실행에 포함되지 않았다. 해당 10개는 이전 Fast-r3와 아래 최종 전체 시험에 포함된다. 두 실행의 수치를 합산하지 않는다.

예시 추천 두 건은 기존 격리 서버/DB/실행 식별자를 유지한 별도 A/B 케이스로 준비했다. 실제 작업자 로그인이나 기존 완료 주문·사진 원장을 되감지 않는다. [준비 절차](../../artifacts/local/recommendation-map-r69/prepare-plan.md)와 별도 비공개 작업 기록을 보존한다. 기기 표시에 쓰는 추천 유효시간은 기존 격리 host의 300초로, 제품 기본 60초 수락 검증을 뜻하지 않는다. 자동 worker·FCM·금융 효과는 OFF를 유지한다.

UTC 수정 전 [Task-r2](../../artifacts/local/validation/20261016-recommendation-map-r69/task-r2.log)의 **12,256개 통과·실패 0개·환경 의존 16개 생략**과 [시험 결과](../../artifacts/local/validation/20261016-170039/Ssalddel.Tests.trx)를 보존한다. UTC 수정 후 [Task-r3](../../artifacts/local/validation/20261016-recommendation-map-r69/task-r3.log)는 솔루션 전체 빌드와 **12,258개 통과·실패 0개·환경 의존 16개 생략**이며, [시험 결과](../../artifacts/local/validation/20261016-171151/Ssalddel.Tests.trx)와 [시험 로그](../../artifacts/local/validation/20261016-171151/tests-01.log)를 보존한다.

최종 판본의 [Task-r4](../../artifacts/local/validation/20261016-recommendation-map-r69/task-r4.log)는 솔루션 빌드를 통과했지만 전체 시험에서 **12,257개 통과·1개 실패·환경 의존 16개 생략**이었다. 실패는 `FDriverFoodPushTests.Receiver_CallbackTimeoutCannotPublishAfterDelayedLocalRestore`의 콜백 종료 2초 대기이며, [당시 로그](../../artifacts/local/validation/20261016-172540/tests-01.log)를 보존한다. 시간 의존성 가능성은 있으나 원인을 확정하지 않았다. 늦은 알림 게시가 확인된 실패로 해석하지 않는다.

동시 빌드 종료 후 같은 소스·산출물로 [실패 시험 1개 재확인](../../artifacts/local/validation/20261016-recommendation-map-r69/timeout-recheck.log) **1개 통과**, [음식 푸시 시험 그룹](../../artifacts/local/validation/20261016-recommendation-map-r69/notification-group-recheck.log) **21개 통과**를 확인했다. 이어 [독립 전체 시험 재실행](../../artifacts/local/validation/20261016-recommendation-map-r69/final-full-recheck.log)은 **12,258개 통과·실패 0개·환경 의존 16개 생략**으로 마쳤다. [최종 TRX](../../artifacts/local/validation/20261016-recommendation-map-r69/recheck/final-full-r69.trx)를 보존한다. Task-r4의 빌드 성공·최초 전체 시험 실패·후속 독립 전체 시험 통과를 구분하며, 최초 실패 기록을 덮어쓰지 않는다.

[초기 APK 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-recommendation-map/cargo-driver/manifest.json)의 **0.1.182 / 182**를 기기에 업데이트했고 [설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r182.json)에서 버전·해시 일치를 확인했다. 그러나 유효한 예시 추천 A/B 두 건과 수신 ON에도 배너가 없었다. 이를 기기 검증 완료로 처리하지 않았다.

원인은 기존 서버 추천 시각이 MySQL/DTO에서 `DateTime.Kind=Unspecified`·UTC 의미의 `noZ` 값으로 전달되는데 새 선택/남은 시간/표시 확인의 `ToUniversalTime`이 기기 현지 시각으로 해석한 것이다. 서버 원장·DTO·원시 값은 유지하고 클라이언트에서 같은 `NormalizeServerUtc`를 적용했다. [수정 전 재현 실패](../../artifacts/local/validation/20261016-recommendation-map-r69/utc-reproduction.log)와 최종 Fast의 선택/표시 ACK 회귀 통과를 보존한다. A/B는 만료를 늘리거나 재추천하지 않고 남겼으며, [새 C/D 준비](../../artifacts/local/recommendation-map-r69/prepare-r183.md)는 별도 식별자·별도 journal만 사용한다.

수정한 **0.1.183 / 183**은 [APK 제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-recommendation-map-utc/cargo-driver/manifest.json)과 [기기 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r183.json)의 버전·해시가 일치한다. SHA-256은 `dbfd43739cb2f1e18f2c2a0ab79d1565410631d6409bd7ed37d49a4f8266486a`다. APK182의 실패 기록은 그대로 보존한다.

## APK183 실제 기기 결과

| 단계 | 확인한 결과 | 화면 근거 |
| --- | --- | --- |
| 추천 배너 | 추천 두 건 표시 | [E17](../../artifacts/local/physical-phone-r67/screens/E17-r183-map-reentry-settled.png) |
| 좌우 카드 | 첫 카드 6,000원에서 둘째 카드 6,200원으로 이동 | [E19](../../artifacts/local/physical-phone-r67/screens/E19-r183-first-card.png), [E20](../../artifacts/local/physical-phone-r67/screens/E20-r183-second-card.png) |
| 선택 지도 | 지도 이동 후 선택 건의 주황 픽업·파랑 도착 핀과 예상 구간 확인 | [E21](../../artifacts/local/physical-phone-r67/screens/E21-r183-second-card-map-pan.png) |
| 경고 확인·수락 | 확인 전 수락 비활성, 확인 후 활성 상태를 UI 관측으로 확인하고 둘째 건 수락 | [E22](../../artifacts/local/physical-phone-r67/screens/E22-r183-selected-confirmed.png) |
| 운행 지도 복귀 | 6,200원·전달 장소 2·다음 행동 상차지 도착 표시 | [E24](../../artifacts/local/physical-phone-r67/screens/E24-r183-accepted-home.png) |
| 상차 사진 | 카메라 촬영·사진 첨부 후 상차 완료 명령 | [E38](../../artifacts/local/physical-phone-r67/screens/E38-r183-pickup-photo-selected.png), [E39](../../artifacts/local/physical-phone-r67/screens/E39-r183-pickup-complete-enabled.png), [E40](../../artifacts/local/physical-phone-r67/screens/E40-r183-pickup-submitted.png) |
| 하차 사진 | 카메라 촬영·사진 첨부 후 하차 완료 명령 | [E45](../../artifacts/local/physical-phone-r67/screens/E45-r183-delivery-photo-selected.png), [E46](../../artifacts/local/physical-phone-r67/screens/E46-r183-delivery-complete-enabled.png), [E47](../../artifacts/local/physical-phone-r67/screens/E47-r183-delivery-submitted.png) |

[후속 원장 조회](../../artifacts/local/recommendation-map-r69/.private/20261016-083322-982031-r183-status.json)에서 선택한 D는 `인수완료`, 정산은 `입금대기`, 결제는 `결제대기`, 동결 기사 보수는 6,200원이다. C는 미확정으로 남았다. 실제 금융 확인은 `false`다. E47은 결과를 다시 확인하라는 처리 중 화면이므로 완료 확정 화면으로 사용하지 않으며, 완료 판정은 이 후속 원장 조회와 구분한다.

원본 PNG와 계층 관측은 시각이 다를 수 있다. E18은 화면 진입 중, E23은 수락 처리 중 PNG이며 이후 UI 관측을 해당 PNG의 동시 상태로 합치지 않는다. E13~E16의 이전 케이스 메타데이터도 C/D 확인 근거로 재사용하지 않는다. 처음에는 핀이 고정 카드에 가려졌고 E21의 지도 이동 후 확인했다. 카드 높이에 맞춰 지도 크기나 여백을 바꾸지는 않았다. 위치·사진을 포함한 원본은 비공개 로컬 증거로 유지한다.

## 최종 APK184와 남은 범위

카드의 내부 API 설명 한 줄을 제거한 **0.1.184 / 184**는 [제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-recommendation-map-final/cargo-driver/manifest.json)과 [설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r184.json)의 버전·해시가 일치한다. SHA-256은 `d48809f8c2f0f5dc7fd0a9ff490063ef50390b0164b239c68b1558b51192a8df`다.

| 최종 APK184 확인 | 실제 기기 결과 | 근거 |
| --- | --- | --- |
| 자동 배너 | 홈 전경을 유지한 채 새 E/F 추천 두 건이 표시됐다. 홈을 다시 열거나 수동 갱신하지 않았다. 추천 핀은 홈에 추가되지 않았다. | [추천 생성 원장](../../artifacts/local/recommendation-map-r69/.private/20261016-083613-013344-r184-offer.json), [E53](../../artifacts/local/physical-phone-r67/screens/E53-r184-automatic-banner.png) |
| 별도 지도·첫 카드 | 배너를 눌러 독립 지도에 진입하고 1/2·픽업 장소 1·전달 장소 1·6,000원을 확인했다. 내부 API 설명은 표시되지 않는다. | [E57](../../artifacts/local/physical-phone-r67/screens/E57-r184-first-card-settled.png) |
| 좌우 넘김·선택 지도 | 오른쪽에서 왼쪽으로 넘겨 2/2·전달 장소 2·6,200원으로 바뀌었다. 지도 이동 후 주황 픽업·파랑 도착 핀과 예상 구간을 확인했다. | [E58](../../artifacts/local/physical-phone-r67/screens/E58-r184-second-card-swipe.png), [E59](../../artifacts/local/physical-phone-r67/screens/E59-r184-second-card-map.png) |
| 역방향 넘김·복귀 | 반대 방향으로 넘겨 첫 카드로 돌아가고 운행 지도로 복귀했다. 현재 수행 운송이 없는 상태를 유지했다. | [E60](../../artifacts/local/physical-phone-r67/screens/E60-r184-return-first-card.png), [E62](../../artifacts/local/physical-phone-r67/screens/E62-r184-home-settled.png) |
| 시험 설정 복원 | 시험을 위해 켰던 신규 배차 받기를 원래 OFF로 돌렸다. 기존 방향 화살표와 화면 유지 값 15는 보존했다. | [E63](../../artifacts/local/physical-phone-r67/screens/E63-r184-reception-off.png) |

[APK184 기기 증거](../../artifacts/local/recommendation-map-r69/.private/device-evidence-r184.json)는 각 PNG 해시와 설치·화면·동작 범위를 연결한다. E/F는 수락하지 않았으며 [마지막 원장](../../artifacts/local/recommendation-map-r69/.private/20261016-083944-222492-r184-status.json)에서도 둘 다 미확정이다. 수락·상하차 사진 명령은 별도로 검증한 APK183의 D 사례이며, 그 결과를 APK184에서 같은 명령을 반복 실행했다고 표시하지 않는다. APK184에서도 최초 핀은 카드에 가릴 수 있고 E59는 지도 이동 후의 화면이다. 지도 크기는 카드 높이에 맞춰 조정하지 않았다.

추천은 격리 호스트의 합성 좌표·300초 조건으로 준비했다. 실제 주행 경로·기본 60초 조건·자동 배차 worker·FCM·PG·송금·공개 운영을 검증한 것은 아니다.

기존 다른 미커밋 작업은 보존하며 이번 요청은 commit/push·외부 배포를 포함하지 않는다. 기존 거절 API는 추천 라운드 CAS를 제공하지 않으므로 UI 변경만으로 서버의 거절 동시 변경 보호까지 완료했다고 보지 않는다. 수락 취소의 기존 API·전문 VM은 보존했고 취소 화면의 개선은 이번 범위가 아니다.

원본 화면은 비공개로 유지하며, 검토한 대표 파생 화면과 판본별 설명은 [휴대폰 UI 진행 요약](2026-10-16-phone-ui-progress-summary.md)에서 확인한다.
