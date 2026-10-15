# USB 연결 APK 검증 r74 — 기기 검증 결과

2026-10-15. USB 휴대폰에서 일곱 앱을 같은 서명으로 설치하고 파일 해시를 대조했다. 로그인·지도·목록과 상세·설정 저장·연결 복구·운행 시작과 종료를 실제로 확인하고 발견한 핵심 오류를 수정했다. 전체 배송 완주·사진 전송·실제 푸시·결제·운영 배포는 완료하지 않았다. 아래에서 통과와 차단을 구분한다.

## 확인 범위와 환경

- 전용 로컬 `Simulation` 서버는 `http://127.0.0.1:5363`, SQL/Mongo DB는 `mirror_usb_r74`다. 휴대폰 연결과 조작은 root 담당자가 수행한다.
- MySQL은 PC `127.0.0.1:13310`, MongoDB는 `127.0.0.1:27021`에 연결한다. 기존 검증 DB·운영 DB와 분리하고 새 DB에 최신 모델의 `EnsureCreated`를 사용했다. 실제 migration 적용은 아니다. 로컬 DB 연결은 별도 gateway 설정으로 연결했다.
- 기존 CargoJourney의 격리 호스트·합성 창고 자료와 Identity/MobileFieldTest의 합성 계정·메뉴를 재사용한다. 합성 계정 9개, 음식점 3곳·메뉴 9개와 창고 출고 자료를 준비했다. 실제 사용자 권한을 새로 부여하지 않았다.
- 서버는 `Local` 사진 저장, 메모리 transient state, HostedService 제거, HttpClientFactory 외부 HTTP 차단을 사용한다. 휴대폰 지도 SDK를 포함한 모든 네트워크가 차단됐다는 뜻은 아니다.
- 이 호스트의 `SharedDriverWork`·`StraightLineDriverPricing`은 ON이다. 수행 자격과 현재 약속 검사는 유지한다. r73 선결제·입금 webhook·지급 배치, 클라우드 푸시는 OFF다. 실제 결제·환불·계좌 송금은 실행하지 않는다.
- 기존 음식 자동배차 시험의 Staging/container/고정 DB 관문은 완화하지 않았다. r74의 `MobileFieldTest:FoodDispatch`는 OFF이며 HostedService가 제거된 호스트에서 자동 배차 워커 실행을 주장하지 않는다.

시작 기준선은 HEAD `08931c2371f29de615f4a4f133b535e5df76bf7f`, 2026-10-15 05:12 UTC에 기록한 파일 해시 439개다. 같은 HEAD 위의 기존 변경을 보존하며 작업했다. HEAD 하나를 모든 미커밋 소스의 판본으로 간주하지 않는다. r74 검증 대상은 [changed-paths.txt](../../artifacts/local/usb-device-verification-r74/changed-paths.txt)의 18개 경로이며 APK별 build manifest·설치 기록을 별도로 대조한다.

## 로컬 자동 시험과 실제 HTTP

최종 Task는 운행 시작 후 추천 실패 분리와 지원 화면 높이 수정을 포함한다. Fast와 역할 API smoke는 앞선 판본의 근거이며 실행 시점을 구분한다.

| 검증 | 현재 결과 | 근거와 한계 |
| --- | --- | --- |
| 앞선 Fast | 통과 237, 실패 0, 건너뜀 0 | `Ssalddel.v3.5.slnx` 빌드와 집중 시험. [로그](../../artifacts/local/usb-device-verification-r74/validation-final-fast.log), [TRX](../../artifacts/local/validation/20261015-145519/Ssalddel.Tests.trx) |
| 최종 Task | 통과 11,779, 실패 0, 환경 의존 16개 건너뜀 | 전체 11,795개. [로그](../../artifacts/local/usb-device-verification-r74/validation-after-p1-task.log), [TRX](../../artifacts/local/validation/20261015-153448/Ssalddel.Tests.trx). 최신 전체 앱 빌드도 오류 0개다. |
| 갱신한 기기 검증 서버 | 빌드 오류 0·경고 0, 재실행 후 liveness 200 | [빌드](../../artifacts/local/usb-device-verification-r74/host-build-after-p1-r2.log). Windows 실행 파일 잠금 때문에 첫 복사 실패 후 해당 시험 서버만 종료하고 재빌드했다. 운영 배포는 아님. |
| 최신 역할 API smoke | 통과 35, 새 실패 0, 기능 경계 차단 2, 설정 필요 한계 2, 상태 관측 1, 건너뜀 0 | [2026-10-15 06:00 UTC 고유 결과](../../artifacts/local/usb-device-verification-r74/api/role-smoke-results-20261015-060009-356656.json). 로그인 8회 모두 200이며 이후 업무 변경 명령을 보내지 않았다. |
| Liveness | `/health/live` 200 | 프로세스 응답 확인이다. 운영 준비 완료와 다르다. |
| Readiness | `/health/ready` 503 | 아래 migration history 한계가 남아 있다. `Observed`이며 통과로 집계하지 않았다. |

HTTP 시험은 다음 결과를 분리해 기록했다.

- 실제 공개 권역 API에서 서울특별시 중랑구 `bjd-sigungu:11260`을 선택해 반경 7km 조회가 200인 것을 확인했다. 권역을 선택하지 않은 조회는 정확한 공개 행정권역 선택 안내와 함께 400을 반환했다. 후자를 서버 오류로 취급하지 않았다.
- 공개 공간 지도는 최신 호스트에서 200이다. 앞선 조회 실패 기록을 지우지 않고 고유 실행 파일을 추가했다.
- 익명 창고 조회는 401, 일반 회원의 음식점 메뉴·수신함·운영자 알림 및 음식점 계정의 창고 자료 조회는 403이다.
- 다른 창고 소유자의 합성 재고 조회는 404 `NotFound`다. `FOOD-20261015054354101`과 `warehouse-outbound-910004`는 소유자 조회 200을 먼저 확인한 뒤 다른 합성 화주로 조회해 각각 404 `NotFound`를 확인했다. 존재하지 않는 임의 ID의 404를 소유권 검사 근거로 사용하지 않았다.
- 마트 OFF 조회의 404 `FeatureDisabled`, 미분류 API의 404 `FeatureBoundaryUnclassified`, 권한상 비공개인 자원의 404 `NotFound`를 서로 다른 결과로 보존한다.
- 앞선 다중 로그인 중 429가 관측됐다. 로그인 정책은 동일 제한 범위에서 5분당 10회인 sliding window다. 제한을 우회하지 않았으며 최신 실행은 호스트 재시작 뒤 root의 기기 로그인과 겹치지 않게 계정 8개를 한 번씩만 사용했다. 인증 POST에 따른 감사·토큰 저장과 업무 변경 명령은 구분한다.

readiness 상세에는 main 63개, traditional-markets 2개, agricultural-fisheries 17개, public-data-ingestion 7개의 pending migration이 기록돼 있다. `EnsureCreated` 호스트는 migration history를 채우지 않으므로 연결·시드 성공과 운영 readiness가 분리된다. 이를 해결한 것처럼 health check를 변경하거나 가짜 migration history를 넣지 않았다. [상세 상태](../../artifacts/local/usb-device-verification-r74/host/health-details.json)는 로컬 검증 자료다.

## APK 제작과 실제 설치는 별도

APK는 현재 Debug 검증용이다. 서명·정렬 검사는 Release 배포 적합성이나 전체 화면 검증을 대신하지 않는다. 아래 표는 작성 시점에 존재하는 manifest와 `installation/` 기록만 반영했다.

| 앱 | APK 제작 근거 | 실제 기기 설치 대조 | 후속 |
| --- | --- | --- | --- |
| 화물 기사 `cargo-driver` | 0.1.74 / 174 및 175 수정본 manifest | 174·175 각각 APK 해시·버전 일치. 같은 서명 교체 설치 확인 | 175 지도·현재 운송 화면 확인, 업무 완주는 차단 |
| 음식 기사 `driver` | 174·175·176 manifest | 세 판본 각각 APK 해시·버전 일치 | 최종 176: 저장 버튼 전체 노출·저장·다시 열기 확인 |
| 주문자 `orderer` | 0.1.74 / 174 manifest | 174, APK 해시·버전 일치 | 상태별 화면·다음 행동 후속 기록 |
| 일반/화주 `shipper` | 0.1.74 / 174 manifest | 174, APK 해시·버전 일치 | 의뢰·인수증·복귀 후속 기록 |
| 음식점 `restaurant` | 0.1.74 / 174 manifest | 174, APK 해시·버전 일치 | 주문 상세·알림 권한 거부 화면 확인 |
| 운영자 `admin` | 0.1.74 / 174 manifest | 174, APK 해시·버전 일치 | 음식 주문·오프라인 복구 화면 확인 |
| 창고 `warehouse` | 0.1.74 / 174 manifest | 174, APK 해시·버전 일치 | 세로 화면·재고 목록/상세를 SQL과 대조 |

[APK manifest 폴더](../../artifacts/local/usb-device-verification-r74/apk/)와 [실제 설치 대조 폴더](../../artifacts/local/usb-device-verification-r74/installation/)는 별도 증거다. 제작 당시 manifest의 `verification.physicalDeviceVerified=false`를 설치 성공만으로 소급 변경하지 않았다. 일곱 앱 174의 설치 대조는 각각 `hashMatches=true`, `versionMatches=true`다. 175의 [화물 기사 설치 기록](../../artifacts/local/usb-device-verification-r74/installation/cargo-driver-viewport-map-fix.json)과 [음식 기사 설치 기록](../../artifacts/local/usb-device-verification-r74/installation/driver-viewport-map-fix.json)도 두 항목이 일치한다. 음식 기사의 최종 [176 설치 기록](../../artifacts/local/usb-device-verification-r74/installation/driver-support-viewport-r2.json)도 일치한다. 이 사실만으로 업무 전체가 정상이라고 판정하지 않는다.

화면 원본은 로컬 비공개 검증 폴더에 유지하고 아래에서 확인한 화면만 개별 참조한다. 합성 인증 입력이 보인 미검토 캡처는 문서·공유 대상에서 제외한다. 화면 캡처의 존재·실제 버튼 조작·서버 저장·재조회·업무 완료를 하나의 결과로 합치지 않는다. 이 기록은 원본 캡처를 공개 문서 자산으로 복사하거나 화면 전체 검수를 대신 인증하지 않는다.

## 실제 휴대폰에서 확인한 화면

아래 결과는 기기 담당자의 실제 화면 조작과 해당 실행 자료에 근거한다. API 시험의 성공을 화면 확인으로 대신 기록하지 않았다.

| 역할·상황 | 확인한 결과 | 근거와 남은 구분 |
| --- | --- | --- |
| 주문자 | 합성 주문자 로그인 뒤 음식 주문 1건·10,000원과 상세를 확인 | [내역](../../artifacts/local/usb-device-verification-r74/screens/orderer-food-history.png), [내역 하단](../../artifacts/local/usb-device-verification-r74/screens/orderer-food-history-bottom.png), [상세](../../artifacts/local/usb-device-verification-r74/screens/orderer-detail.png). 메뉴 수량 조작·새 주문 제출은 별도 후속 확인 |
| 음식점 | 같은 주문은 기사 배정 대기이며 조리 시작 버튼이 노출되지 않음 | [주문 목록](../../artifacts/local/usb-device-verification-r74/screens/restaurant-home.png), [주문 상세](../../artifacts/local/usb-device-verification-r74/screens/restaurant-order-details.png). 접수 시각의 UTC `05:43` 표시가 남아 있음 |
| 음식점 알림 권한 거부 | 기기 설정 안내가 나오며 기존 주문 목록을 유지 | [권한 거부 뒤 화면](../../artifacts/local/usb-device-verification-r74/screens/restaurant-notification-denied.png). 실제 푸시 수신 성공과 다름 |
| 운영자 | 로그인 전 안내를 거쳐 운영자 로그인 후 같은 음식 주문·다음 담당인 기사 배정을 확인 | [주문 목록](../../artifacts/local/usb-device-verification-r74/screens/admin-food-list.png), [상세](../../artifacts/local/usb-device-verification-r74/screens/admin-food-details.png). 기사 배정 명령 수행 완료는 아님 |
| 운영자 연결 복구 | USB reverse 연결을 20초 제거한 동안 이전 자료를 표시하고 연결 복구 후 최신 조회 회복 | [오프라인 대기 뒤](../../artifacts/local/usb-device-verification-r74/screens/admin-offline-after-wait.png), [복구 뒤](../../artifacts/local/usb-device-verification-r74/screens/admin-online-recovered.png). 임의 업무 재전송이나 운영 서버 장애 시험은 아님 |
| 창고 | 세로 화면의 재고 목록/상세에서 재고 항목 1건·가용 0·예약 9·재위탁 대기를 SQL과 대조 | [세로 화면](../../artifacts/local/usb-device-verification-r74/screens/warehouse-portrait.png), [목록](../../artifacts/local/usb-device-verification-r74/screens/warehouse-inventory-list.png), [상세](../../artifacts/local/usb-device-verification-r74/screens/warehouse-inventory-detail.png). 피킹·촬영·인계 완료와 구분 |
| 화물 기사 175 | 네이티브 지도에서 교통 속도 색 선이 사라지고 현재 운송 조회 실패 안내를 확인 | [네이티브 지도](../../artifacts/local/usb-device-verification-r74/screens/cargo-175-native-map.png). 기본 도로의 주황색 표현은 남으며 현재 운송 정상 조회·업무 완주는 아님 |

기기 가로/세로 전환은 사용자가 준비 중 실제 휴대폰을 회전한 결과다. 가로 방향에서는 메뉴 하단 안내와 링크가 겹쳐 일부 이동이 어려웠으며, 세로로 전환하자 같은 링크가 정상 작동했다. 가로 메뉴는 별도 잔여다. 임시 회전 설정은 원래 값과 사용자 후속 변경을 대조해 정리한다.

## 발견한 문제와 반영한 범위

1. **기사 API 실행 기능 분류**: 기사 홈은 국내 운송, 공통 업무 선택·알림은 음식 또는 화물이 켜졌을 때 사용하는 `OperationalDispatchCore`, 화물 추천·상세·공개콜·요약은 국내 운송으로 명시했다. 제품 버전·기존 route·기사 권한을 유지하며 gate 우회나 판본 하향을 하지 않았다. [회귀 시험](../../Ssalddel.Tests/ApiMetadata/DriverHomeAndParticipationApiBoundaryR74Tests.cs)은 21개 조합에서 실제 필터와 기존 계약을 검사한다.
2. **공간 지도 조회의 의존성**: 내부 연결 조회를 배송·보호지원 쓰기 UseCase에서 [생활협업연결Query](../../Ssalddel/Services/Community/생활협업연결Query.cs)로 분리했다. 기존 합의 만료 CAS와 보관 예약 보호는 유지하며 새 공개 권한을 부여하지 않았다. 최신 공간 지도 HTTP 200은 확인했으나 모든 공간 업무 완주를 뜻하지 않는다.
3. **화물 지도 교통 정보**: 기본 교통 레이어를 OFF로 변경했다. 175 실제 네이티브 지도에서 교통 속도 색 선이 제거된 것을 확인했다. 기본 도로 색과 기사 업무 핀·현재 경로의 상태 색을 없애는 변경이 아니다.
4. **운행 시작 요청과 후속 실패**: 즉시 운행 버튼은 조회 화면의 표시 문구를 복사하지 않고 서버 계약의 `바로시작`을 전달하도록 맞췄다. 요청 DTO 기본값도 일치시켰고 175 요청에서 정상 mode를 확인했다. 그러나 운행 저장 뒤 추천 알림 단계가 Opinet 설정 부재로 예외를 내면서 HTTP 400이 반환됐다. 후속 정본 조회는 운행 중·Shift 2를 확인했다. 저장 성공과 후속 알림 실패가 앱에서 전체 실패로 보이는 P1을 재현했다. Handler에서 확정 후 추천·알림의 비취소 예외를 경고로 분리하고 취소 의미는 보존했다. SQLite 실제 commit·동일 요청/구형 null 재생·취소 회귀 시험 3개와 전체 시험이 통과했다. 갱신 서버에서 175 앱으로 새 운행 시작→위치 송신 시작→운행 종료까지 성공했다. 알림 전달 성공을 인증하거나 재시도 예약을 만든 것은 아니다. 실제 중복 근무는 입증되지 않았다. [수정 후 시작](../../artifacts/local/usb-device-verification-r74/screens/cargo-after-p1-start.png), [종료](../../artifacts/local/usb-device-verification-r74/screens/cargo-after-p1-stopped.png), [회귀 시험](../../Ssalddel.Tests/Application/Driver/Work/CargoWorkStartRecommendationFailureR74Tests.cs). [기기 결과 화면](../../artifacts/local/usb-device-verification-r74/screens/cargo-175-start-result.png).
5. **음식 기사 설정 화면**: 174에서 맨 아래 저장 버튼 일부가 가려지는 현상을 확인했다. 175의 여백 수정만으로 해결되지 않은 것을 확인하고, 176에서 viewport를 WebView의 고정 영역으로 맞춰 높이 중복을 없앴다. 저장 버튼 전체가 보이고 음식 배달·오토바이 설정 저장 및 화면 재진입 유지가 확인됐다. [저장 버튼](../../artifacts/local/usb-device-verification-r74/screens/food-176-save-reachable.png), [저장 확인](../../artifacts/local/usb-device-verification-r74/screens/food-176-work-settings-saved.png), [재진입](../../artifacts/local/usb-device-verification-r74/screens/food-176-settings-reopened.png).

## 남은 차단과 업무 결과 경계

- 두 기사 계정의 `/api/v1/driver/home`은 400 `BadRequest`, `Opinet:CertKey configuration is required.`다. 홈이 추천을 읽는 과정에서 유가 조회 설정이 필요해 중단된다. 생성자 예외나 SQL 자료 없음으로 판정하지 않았으며, 가짜 키·0원·빈 추천 fallback을 추가하지 않았다.
- 운영자 리뷰와 토스 환경 조회는 404 `FeatureBoundaryUnclassified`다. 최신 smoke의 두 차단으로 남겼으며 정상적인 권한 거부나 기능 OFF 성공으로 집계하지 않았다.
- 추천 API의 오류가 화면에서 빈 추천 0건으로 보이는 부분과 고정 `04.04` 날짜 표시는 발견한 미해결 UI 사항이다. 이번 서버 metadata 보완이 이 화면 표현까지 해결한 것은 아니다.
- 음식 통제 배차 제안 POST는 409이며 후속 GET의 제안 라운드는 0, 동결 요금도 없다. 외부 HTTP 차단·Directions 키 부재·합성 하차 주소의 Kakao 확인 부재가 가능한 제약이지만 최초 실패 원인은 아직 확정하지 않았다. 제안 생성·배너 수신·수락·픽업 완료로 기록하지 않는다.
- 화물 추천은 Opinet 설정 부재로 차단되고 기존 제안은 만료 상태다. 운임 변경 PUT도 409이므로 요금 변경 성공이나 기사 수락을 주장하지 않는다.
- Mongo 직접 조회에서 문서 1건이 있는 반면 typed reader의 간헐 조회 실패가 관측됐다. 조회 실패 또는 `false`를 자료 부재로 단정하지 않고 후속 원인 확인 대상으로 남긴다.
- 화물 기사 174→175 첫 실행에서 다시 로그인이 필요했다. 영구 저장·복원 코드는 있으며 동일 시험 계정의 별도 API 로그인이 갱신 토큰을 교체할 수 있으므로, 업데이트로 저장소가 지워졌다고 단정하지 않는다. 기기 세션 갱신 원인은 아직 미확정이다.
- 운행 시작 응답 유실·후속 실패 때 사용할 클라이언트 stable ID와 불확실 결과의 정본 재조회는 별도 잔여 사항이다. 위 운행 저장 결과를 근거로 자동 재전송하지 않았다.
- 실제 위치 권한 거부 후 재허용, 알림 권한 거부 안내와 음식 기사 알림 설정 저장·다시 조회를 확인했다. 실제 FCM/배차 배너와 소리·진동의 전달은 확인하지 않았다. 모든 권한 조합·모든 앱 연결 복구·모든 업무 상태를 통과로 확대하지 않는다. [알림 설정 저장](../../artifacts/local/usb-device-verification-r74/screens/food-notification-saved.png).
- 사용자는 안전한 촬영 배경과 세로 방향을 준비했지만 정상 배차가 차단돼 업무 카메라 화면에는 아직 진입하지 못했다. 사진 촬영·전송·완료 전이의 실제 기기 검증은 미완료다.

| 결과 | 현재 근거 | 완료로 합치지 않는 후속 |
| --- | --- | --- |
| 음식 주문 존재·본인 조회 | 합성 음식 주문 ID의 소유자 GET 200 및 교차 계정 404 | 음식점 수락·조리·기사 수락·픽업·전달·주문자 수령은 각각 별도 |
| 화물 출고 준비·후불 Simulation 승인·통제 제안 | 합성 의뢰의 `prepare`·`approve`·`offer` 단계 `Succeeded` 기록 | 실제 후보 선택 엔진 증거, 기사 수락·상차·하차·화주 인수증 완료는 별도 |
| 화물 운임 변경 시험 | `cargo-quote-state.json`은 `FailedOrUncertain`, 자동 변경 재시도 없음 | 제안액 확정이나 수락·배송 완료로 승격하지 않음 |
| 창고 인계 | 필요한 API의 기존 실행 기능 분류 및 실제 재고 목록/상세는 확인 | 실제 창고 작업자 인계와 기사 인수 완료는 후속 기기/서버 근거 필요 |
| 수령·정산·지급 | 업무별 원장·수령 확인·지급 조건을 구분 | 전달 완료가 수령 확인·수금·정산 확정·은행 입금을 자동 증명하지 않음 |

실제 송금·PG 실수납·환불·실제 FCM 수신, 외부 HTTPS 서버와 Azure/GCS 배포, 공개 APK 배포, 운영 활성화·운영 readiness는 미검증이다. Unity, commit/push도 이번 검증 완료 범위에 포함하지 않는다.

## 최종 정리와 다음 검증

- 최종 설치는 화물 기사 175·음식 기사 176·나머지 다섯 앱 174다. 앱 데이터를 지우거나 삭제하지 않았다. 음식 기사 175→176 업데이트 뒤 로그인 복원도 확인했다.
- 두 기사 시험 운행을 종료하고 위치 송신 중지 화면을 확인했다. 전용 MySQL SELECT에서도 음식/화물 모두 대기·미종료 근무 0건·활성 슬롯 0건이다. [SQL 정본 대조](../../artifacts/local/usb-device-verification-r74/.private/driver-shift-stop-sql-20261015T064350188124Z.json).
- USB 화면 유지 설정은 원래 값으로 복원했고, 사용자가 후속 변경한 자동 회전 설정은 보존했다. 잠금·인증 설정은 변경하지 않았다. 사용자가 화면을 이어 볼 수 있도록 격리 로컬 서버·DB와 이 작업의 USB reverse 5363은 유지한다. 이 APK는 PC의 로컬 서버와 USB 연결을 사용하는 시험용이며 독립적인 외부 서비스용 APK가 아니다.
- [최종 감사](../../artifacts/local/usb-device-verification-r74/final-audit.json): 일곱 현재 설치 APK 해시 일치·439개 기존 변경 기준선의 범위 밖 변경 0건·새 범위 밖 Git 경로 0건을 확인했다. 확보 가능한 crash buffer의 현재 작업 크래시 0건이다.
- [앞선 크래시 점검](../../artifacts/local/usb-device-verification-r74/crash-summary.json): 확보 가능한 crash buffer에서 현재 작업 시작 이후 일곱 앱의 새 크래시는 0건이다. 10월 10일의 과거 음식 기사 2건은 별개다. 모든 코드 경로·ANR 부재를 인증하지 않는다.
- 우선 남은 것은 유가·경로 설정을 연결한 정상 추천/수락→카메라 촬영·취소·전송·복구→상하차/전달/수령의 실제 기기 완주다. 현재 배차 차단을 DB 좌표·요금 주입이나 직접 accept로 우회하지 않았다.
- 이어서 화물 앱의 안정된 시작 요청 ID·불확실 응답 정본 조회, 홈의 선택적 조회 실패 표시, 가로 메뉴, 접수 시각 한국 시간, 예시 단계 표기를 다듬어야 한다. 주문자 익명 메뉴 증감 잠금은 읽기상 불일치 후보로 남았으며 기기 재현을 확정하지 않았다.
- 화물 운행 공개 옵션은 현재 기본 true이고 구 단위 위치 공개 동의는 별도다. 시험은 격리 계정·로컬 커뮤니티에서만 수행하고 종료했으며, 공개 옵션의 초기 선택은 실제 운영 전 검토 대상으로 남긴다.
- APK 빌드에서 기존 `Microsoft.AspNetCore.SignalR.Common 10.0.9`의 `NU1903`/`GHSA-334f-vvp4-7g6p` 경고가 관측됐다. 경고를 숨기지 않았으며 공개 배포 전 의존성 보안 보완이 남아 있다.
- 실제 PG·실지급·FCM·Azure/GCS·HTTPS 배포·운영 migration·Release 서명 APK와 외부 배포는 별도 검증이다. 이번에는 commit/push하지 않았다.

비밀·토큰·원본 인증 화면·기기 고유 식별자는 이 문서에 포함하지 않는다. 로컬 비공개 파일과 실제 현장 고객 자료를 공개 문서나 Git에 첨부하지 않는다.
