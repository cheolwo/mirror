# 지도 우선 추천 비교 r71

2026-10-16. Fast·APK187 설치와 일부 실제 화면을 확인했다. 전체 Task는 알림 콜백 시간 초과 1개가 실패했고 해당 시험만 재실행해 통과했다. 전체 재시험·최종 기기 마감은 완료로 처리하지 않는다. 이후 사용자가 상세 없는 카드로 다시 지정해 r72로 보완 중이며 이 문서는 APK187의 구현·부분 검증 이력이다. [책임 카드 r21](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#지도-우선-추천-비교-r21--r71)이 당시 화면 책임을 소유한다. [r70](2026-10-16-independent-recommendation-cards-r70.md)의 독립 카드·좌우 넘김·명령 보호는 유지했다.

## 구현 범위

작은 기본 카드는 높이 96 DIP, 본문 13·금액 16 크기로 순번·배달료·픽업지·전달지와 상세 진입을 제공한다. 긴 주소는 한 줄로 줄여 표시하고 같은 추천의 상세에서 전체 주소·조건·경고 확인과 수락/보류/거절을 처리한다. 상세 진입과 응답 버튼의 높이는 48 DIP이며 상세 패널은 화면 높이에 맞추되 최대 520 DIP다. 6~7:1은 시각 목표이고 실제 기기의 비율·가독성은 별도 확인한다.

[추천 페이지](../../DriverApp/NativeDriverRecommendationPage.cs)의 고정 소스 SHA-256은 `cd0495061b2670836246501b6f61c7cda2c56de4ce2fc099fa5147b2364b1a35`다. 상세는 선택한 `(의뢰Id, 추천라운드)`에 고정하고 열린 동안 카드 넘김을 막는다. 만료·선택 변경·계정 변경·배경 전환 시 상세를 닫으며, 경고 확인과 응답 직전 최신 원장 재조회·응답 불명확 보호를 유지한다. 선택은 검토 대상만 바꾸며 추천 수락·현재 수행 변경과 구분한다.

별도 추천 지도에는 좌표가 확인된 모든 유효 추천의 핀을 함께 표시한다. 선택한 카드의 픽업은 주황, 전달은 파랑이며 나머지는 회색이다. [지도 control](../../DriverApp/Controls/DriverNativeMapView.cs)의 `HighlightSelectedRecommendation` 기본값은 `false`, `SelectedRecommendationRequestId`는 `null`로 기존 소비자의 색상과 현재 위치 표시는 유지한다. 비교 모드에서 선택 ID가 없으면 모든 추천 핀을 회색으로 표시한다.

[순수 표현 policy](../../DriverApp/Models/Driver/Samples/DriverRecommendationMarkerPresentationPolicy.cs)는 각 픽업·전달 좌표를 독립 검사하고 0·비정상·범위 밖 점을 생략한다. 한쪽만 확인됐으면 그 점은 남긴다. 순번 라벨은 페이지의 카드 순서에서 받아 그대로 보존하고 선택 핀을 마지막·높은 ZIndex로 배치한다. [공통 mapper](../../DriverApp/Handlers/DriverNativeMapViewHandler.cs)와 [Android handler](../../DriverApp/Handlers/DriverNativeMapViewHandler.Android.cs)가 Naver/Google에 같은 선택 기준을 적용한다. Google의 회색 핀은 handler 내부 비트맵으로 재사용하며 현재 위치 마커·방향 센서 코드는 바꾸지 않았다.

전체 지도 SDK를 카드 높이에 맞추어 축소하거나 여백을 바꾸지 않는다. 카드 이동으로 지도가 반복 이동하지 않도록 전체 추천 좌표 집합이 달라질 때만 지도 범위를 갱신한다. 읽기 검토에서 실패 뒤 지도만 비우고 이전 핀 캐시를 남기면 동일 추천을 다시 받아도 핀이 복구되지 않는 반례를 찾아 `ClearMapProjection`으로 실제 지도·캐시·선택 ID·경로를 함께 비우도록 보완했다. 만료된 핀만 제거하고 다른 유효 추천은 유지한다.

서버 배차 정책·음식 기사 앱·요금/정산·Unity는 이번 범위가 아니다. 원래 의뢰 ID·추천 차수·만료·경고 확인·불명확한 수락 결과 재조회 보호를 유지한다. 알려지지 않은 좌표를 임의로 생성하지 않는다.

## 검증 상태

[Fast](../../artifacts/local/validation/20261016-map-first-r71/fast.log)는 기사 앱·시험 프로젝트 빌드와 **69개 통과·실패 0개·생략 0개**다. [상세 시험 로그](../../artifacts/local/validation/20261016-184837/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-184837/Ssalddel.Tests.trx)를 보존한다. [새 표현 policy 집중 시험](../../Ssalddel.Tests/Clients/Cargo/DriverRecommendationMarkerPresentationR71Tests.cs) 16개는 기존 색·라벨·순서, 선택 없음/불일치의 회색, 겹친 선택 핀 우선 순서, 한쪽 좌표만 확인된 경우와 잘못된 좌표 생략을 검증하며 69개에 포함된다. 별도로 합산하지 않는다. [시험 프로젝트의 Compile 연결](../../Ssalddel.Tests/Ssalddel.Tests.csproj)도 앱 소스와 함께 확인했다.

[Task](../../artifacts/local/validation/20261016-map-first-r71/task.log)는 솔루션 빌드를 통과했으나 전체 시험에서 **12,273개 통과·1개 실패·환경 의존 16개 생략**이었다. 실패는 `FDriverFoodPushTests.Receiver_CallbackTimeoutCannotPublishAfterDelayedLocalRestore`의 콜백 종료 2초 대기 `TimeoutException`이다. [상세 시험 로그](../../artifacts/local/validation/20261016-185540/tests-01.log)와 [TRX](../../artifacts/local/validation/20261016-185540/Ssalddel.Tests.trx)를 보존한다. r69에서 같은 시험의 시간 초과 이력이 있으나 원인을 확정하거나 늦은 알림 게시가 확인됐다고 해석하지 않는다.

[실패 시험 단일 재실행](../../artifacts/local/validation/20261016-map-first-r71/recheck-single.log)은 **1개 통과·실패/생략 0개**이며 [TRX](../../artifacts/local/validation/20261016-map-first-r71/recheck/single-push.trx)를 보존한다. 전체 재시험은 아직 실행하지 않았고 최초 실패 기록을 덮어쓰지 않는다.

| 단계 | 현재 상태 |
| --- | --- |
| 제품 구현·읽기 검토·Fast | 완료, 위 고정 페이지 소스와 69개 시험 결과 |
| 전체 Task | 빌드 성공·12,273개 통과·1개 실패·16개 생략, 단일 재실행 1개 통과 |
| APK187 | [제작 로그](../../artifacts/local/validation/20261016-map-first-r71/apk-build.log)와 [제작 원장](../../artifacts/local/usb-device-verification-r74/apk/phone-r67-map-first/cargo-driver/manifest.json) 확인, 0.1.187 / 187 Debug APK 서명·정렬 검사 통과 |
| 실제 기기 설치 | 0.1.187 / 187 버전·해시 일치 확인 |
| 핀 색·작은 카드·상세 | 아래 APK187 부분 확인 이력 |
| 전체 재시험 | 미실행, 단일 재시험 통과로 전체 성공을 주장하지 않음 |
| 수신 복원 | S17 정착 PNG·UI에서 OFF 확인, S16 PNG는 이전 변경 중 화면 |

APK187 제작 원장과 [기기 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r187.json)의 APK SHA-256은 `9e43c3885d2df0abe8eaa972e4c74c331f85cb50d1f836bf1caa8dade8c04e0c`로 일치한다. 제작 원장의 빌드 시점 기기·서버 통신 미검증 상태는 소급 변경하지 않는다. 최초 캡처 시도는 앱 전경이 준비되기 전 launcher여서 helper가 저장을 거부했고 실제 앱을 다시 관측한 [S01](../../artifacts/local/physical-phone-r67/screens/S01-r187-home.png)과 구분한다. 설치 실패로 기록하지 않는다.

[수정 전 소스 사본](../../artifacts/local/physical-phone-r67/.private/source-backups/recommendation-card-r71-before-compact/manifest.json)과 r70 APK186의 소스·설치·스크린샷·시험 원장·실패 이력은 보존한다. r70 기기 성공을 이번 변경의 기기 완료로 옮기지 않는다.

## APK187 실제 기기 부분 확인

| 단계 | 확인한 결과 | 근거 |
| --- | --- | --- |
| 두 건 전체 핀·작은 카드 | 첫 카드 1/2·6,000원과 네 픽업/전달 핀을 확인했다. 선택 핀은 주황/파랑, 다른 추천 핀은 회색이다. | [S08](../../artifacts/local/physical-phone-r67/screens/S08-r187-small-first-card.png) |
| 좌우 넘김·선택 색 | 두 번째 카드 2/2·6,200원으로 넘기자 네 핀은 남고 강조 색이 바뀌었다. 지도 범위는 유지됐다. | [S09](../../artifacts/local/physical-phone-r67/screens/S09-r187-second-card-colors.png) |
| 상세·주의 확인 | 추천 2 상세에서 확인 전 수락 비활성, 경고 체크 후 수락 활성을 UI 관측으로 확인했다. 수락은 실행하지 않았다. | [S10](../../artifacts/local/physical-phone-r67/screens/S10-r187-second-detail.png), [S10 UI](../../artifacts/local/physical-phone-r67/.private/ui/S10-r187-second-detail.xml), [S11](../../artifacts/local/physical-phone-r67/screens/S11-r187-second-warning-confirmed.png), [S11 UI](../../artifacts/local/physical-phone-r67/.private/ui/S11-r187-second-warning-confirmed.xml) |
| 세 건 추가 | O 추가 후 2/3의 작은 카드 화면을 확인했다. | [S12](../../artifacts/local/physical-phone-r67/screens/S12-r187-three-small-cards.png) |
| 세 번째 카드·여섯 핀 | O 3/3·6,400원·주황/파랑 두 핀과 M/N 회색 네 핀을 확인했다. 지도 범위는 세 건을 담은 S12 범위와 같다. | [S13](../../artifacts/local/physical-phone-r67/screens/S13-r187-third-card-colors.png) |
| 수신 복원 | S17 정착 PNG·UI에서 `꺼짐`·switch false를 확인했다. 앞선 S16의 후속 UI도 OFF지만 그 PNG는 `변경 중`이므로 완료 화면으로 쓰지 않는다. | [S17](../../artifacts/local/physical-phone-r67/screens/S17-r187-restored-off-settled.png), [S17 UI](../../artifacts/local/physical-phone-r67/.private/ui/S17-r187-restored-off-settled.xml), [S16 UI](../../artifacts/local/physical-phone-r67/.private/ui/S16-r187-reception-restored-off.xml), [S16 PNG](../../artifacts/local/physical-phone-r67/screens/S16-r187-reception-restored-off.png) |

S13 캡처 작업은 사용자 중단으로 끝났지만 저장된 PNG를 후속으로 직접 열어 위 결과를 확인했다. 캡처 도구의 종료 상태와 실제 파일·화면 확인을 구분한다. S16도 PNG와 UI 관측의 시각이 같다고 가정하지 않는다.

S08 원본 1080×2340 화면에서 header 끝 y=244부터 순번 안내 시작 y=1796까지를 지도 노출 높이로, 카드 본 테두리 y=1910~2170을 카드 높이로 정하면 `(1796-244)/(2170-1910)=1552/260≈5.97:1`이다. 상단 header·순번 안내·시스템 영역을 제외한 한 화면의 픽셀 측정이며 전체 SDK viewport의 크기 변경이나 모든 기기의 동일 비율을 뜻하지 않는다.

[마지막 M/N/O 원장 조회](../../artifacts/local/recommendation-map-r69/.private/20261016-100649-477024-r187-status.json)에서 세 건 모두 확정 기사·동결 기사 보수는 없고 결제는 `결제대기`다. 이 snapshot은 M의 queue/exposure 20/210, N/O의 30/310을 기록하므로 세 건의 만료 상태를 같다고 단정하지 않는다. 수락·거절은 실행하지 않았으며 실제 주행·배송·지급 완료로 확대하지 않는다.

## 격리 서버 준비와 남은 확인

시험 host의 [첫 빌드](../../artifacts/local/validation/20261016-map-first-r71/host-build.log)는 실행 중인 apphost의 파일 잠금으로 `MSB3027`·`MSB3021` 오류가 났다. listener의 실행 경로를 확인하고 원본 사본을 보존한 뒤 해당 host를 중지해 [두 번째 빌드](../../artifacts/local/validation/20261016-map-first-r71/host-build-r2.log)를 **경고 0개·오류 0개**로 완료했다. 기존 DB·실행 식별자·계정과 ready 범위를 유지해 재시작했다. 이를 제품 컴파일 실패나 새 업무 완료로 합치지 않는다.

[M/N/O 준비 절차](../../artifacts/local/recommendation-map-r69/prepare-r187.md)와 [host 변경 전 사본·해시](../../artifacts/local/recommendation-map-r69/.private/source-before-r187/source-sha256.json)를 보존한다. [별도 준비 호출](../../artifacts/local/recommendation-map-r69/.private/20261016-094331-785508-r187-prepare.json)로 세 의뢰 생성·후불 승인을 준비했고 [M/N 두 건 추천](../../artifacts/local/recommendation-map-r69/.private/20261016-095835-841995-r187-offer.json) 뒤 [O 추가·세 건 정본 확인](../../artifacts/local/recommendation-map-r69/.private/20261016-100058-959476-r187-offer.json)을 실행했다. 기존 A~L은 되돌리거나 추천 만료를 연장하지 않는다. 화면 구분을 위해 여섯 점을 서로 다르게 선언한 합성 좌표를 사용하며 실제 매장/고객·주행 경로로 설명하지 않는다.

APK187의 부분 화면과 시험 실패는 위 이력으로 보존하며 [후속 r72](2026-10-16-direct-recommendation-card-actions-r72.md) 검증을 대신하지 않는다. 시험용 유효시간 300초는 제품 기본 60초 수락 검증이 아니다. 시험용 좌표/예상 연결선과 실제 위치·실제 주행·FCM·금융 결과는 구분한다. 자동 worker·FCM·금융 효과는 OFF다. 위치가 포함된 원본 화면은 비공개 로컬에 보관하고 문서 자산으로 복사하지 않는다. 이번 요청은 commit/push가 아니다.
