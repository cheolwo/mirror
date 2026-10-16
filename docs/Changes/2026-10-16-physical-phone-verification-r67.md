# 새 개발 휴대폰 역할 앱·배차 수행 검증 r67

2026-10-16. 현재 검증 범위를 마감했다. 상태 `PhysicalDevicePartiallyValidated / FoodJourneyValidated / LocalSoundHeard / CargoTransportCompletedOnDevice / CameraCaptureAndUploadValidated / UiFixDeviceValidated / LocalSimulationOnly`.

새 개발 휴대폰에서 역할 앱7개를 설치하고 음식 기사 추천 배너→수락→음식점 도착→픽업→전달 완료→내역 조회, 주문자의 별도 수령 확인을 실제 화면으로 진행했다. 소리도 사용자가 직접 들었다고 확인했다. 화물은 사례04 수락 화면의 가로 넘침을 보존한 뒤180에서 배치를 수정하고 별도 사례05의 추천→수락→실제 카메라 촬영2회→상차/하차 완료를 확인했다. 서버 운송은 인수완료이며 결제·정산은 대기 상태다. 지도 경고 정리는179에서 확인했다. 이 과정은 합성 의뢰의 실제 APK/카메라 검증이며 실물 운송·전체 주문·송금 완료는 아니다.

이 문서는 실제 확인한 결과와 미검증·잔여 작업을 구분한다. 앱 설치·실제 화면 조작·API 저장·원장 재조회·소리 청취·실물 배송·지급을 같은 완료로 합치지 않는다. 이전 [운영 흐름 감사 r66](../ProjectOverview/operator-workflow-review-r66.md)에서 발견한 취소/복구/관리자 미보완 사항은 이번 정상 경로 성공으로 해소됐다고 표시하지 않는다.

## 환경과 실행 경계

- 단말: 새 Samsung Galaxy A34, USB 디버깅 승인. 잠금/보안 설정의 영구 변경이나 기기 초기화 없이 설치·조작했다. 상세 OS/API·기기 식별값은 로컬 실행 기록을 따른다.
- 서버: `http://127.0.0.1:5363`, USB reverse를 이용한 `Development + Simulation` 전용 호스트. 실행 식별자는 `usb-r67:fcb4817eec02430cbde19ff422355450`다.
- 실제 MySQL/Mongo·인증 API·합성 계정/주문/물품을 사용한다. 경로 자료는 명시적인 합성 어댑터이고 사진 저장은 로컬이다. 실제 주행 경로·배송 상품·고객을 사용하지 않았다.
- `hostedServicesRemoved=true`, 음식 자동 배차 OFF, 클라우드 푸시 OFF다. 제안 생성의 직접 통제와 후보 정책 실행은 각각의 실제 호출 근거로 분류한다. 전경에서 추천을 수신했다고 상주 자동 워커/Ollama 선정/FCM 전달을 인증하지 않는다.
- 최신 모델 `EnsureCreated`와 DB 연결 성공은 운영 migration 적용이 아니다. readiness는 main63·traditional-markets2·agricultural-fisheries17·public-data-ingestion7 pending migration을 보고해 `Unhealthy`다. health 검사나 migration history를 성공처럼 변경하지 않았다.
- 한 휴대폰에서 역할 앱을 순차 전환했다. 다기기 동시 사용자·동시 수락·실제 영업 운영은 별도다.
- 화물 사례05의 화면 순차 검증은 전용 프로필의 제안 TTL300초를 사용했다. 제품 기본60초 응답 창을 그대로 기기에서 완주한 결과나 운영 TTL 변경으로 기록하지 않는다. 앞선 만료 사례는 보존했다.

## APK 설치 대조

기본7개 APK는 모두 `0.1.178 / 178`이며 기기 설치본과 준비 APK의 SHA-256·버전 일치를 확인했다. 로그인·역할 첫 화면도 각 앱에서 확인했지만 전체 생명주기 완료 수준은 서로 다르다.

| 앱 | 패키지 | 설치 대조 | 실제 업무 범위 |
| --- | --- | --- | --- |
| 음식 기사 | `kr.ssalddel.fdriver` | 178 해시·버전 일치 | 추천·수락·도착·준비 대기·픽업·전달·내 정보 내역·소리 |
| 화물 기사 | `kr.hongdal.driver` | 178, 후속179·180 각각 일치 | 운행·신규 수신·추천 배너·180 수락/촬영/상하차 완료,179 지도 경고 정리 |
| 주문자 | `com.ssalddel.ordererapp` | 178 해시·버전 일치 | 메뉴/지역·주문 진입, 기존 시험 주문의 별도 수령 확인 |
| 음식점 | `com.ssalddel.restaurantdeskapp` | 178 해시·버전 일치 | 주문 목록·상세·실제 접수, 자동 인쇄창 관찰 |
| 생활·화주 | `com.companyname.ssalddelapp` | 178 해시·버전 일치 | 로그인·화주 역할 화면·지도 설정 안내/의뢰 카드 |
| 창고 | `com.companyname.warehousemanagerapp` | 178 해시·버전 일치 | 로그인·출고 검토 목록 진입 |
| 운영자 | `com.ssalddel.adminapp` | 178 해시·버전 일치 | 로그인·음식 주문 목록 |

화물179 설치본 SHA-256은 `5e68109cd323ca6b34bc591991c6a620525b1d809a7a2489e21dcb3df2cef003`이며 [별도 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r179.json)에 남겼다. 180 설치본 SHA-256은 `6d5a03ec01a56444dddd0530b5679ea6b20d47b3b0d95a42ca324209c0d3228b`이며 [180 설치 기록](../../artifacts/local/physical-phone-r67/installation/cargo-driver-r180.json)도 해시·버전 일치다. 이전178·179 기록을 덮어180 근거로 재사용하지 않는다. **180은 사례05의 실제 수락·촬영·운송 완료까지 확인했으며 전체 주문/화주 별도 수령/금융 완료는 별도다.**

설치 근거는 [installation 폴더](../../artifacts/local/physical-phone-r67/installation/)다. 지도 Client ID·시험 자격 증명은 문서에 기록하지 않는다.

## 음식: 같은 사례 안에서 연결한 결과

### 음식점 접수 사례01

`food-phone-01` / `FOOD-20261016055510654`는 API로 생성하고 음식점 앱에서 실제 접수했다. 접수 뒤 Android 자동 인쇄창이 나타났으며 원래 화면 복귀와 필요 여부는 별도 UX 관찰로 남겼다. 이 사례의 기사 완료를 아래 사례02로 대신하지 않는다.

### 기사 정상 수행 사례02

`food-phone-02` / `FOOD-20261016055947070`의 주문 생성·음식점 수락·조리·준비 완료는 API로 처리했다. 기사와 주문자 부분은 실제 기기로 수행했다. 주문자가 휴대폰에서 새 주문을 처음부터 제출했다고 표시하지 않는다.

| 실제 휴대폰 단계 | 화면/행동 결과 | 완료와 구별할 사항 |
| --- | --- | --- |
| 추천 도착 | 상단 음식 배달 추천 배너·수락 행동 표시 | 전경 수신이며 실제 FCM·자동 워커 아님 |
| 기사 수락 | 현재 음식점 카드·총 보수2,500원·가게 도착 행동 | 제안 수락과 실제 픽업은 별도 |
| 가게 도착 | 음식 준비 전에는 픽업 완료 버튼 비활성, 대기 이유 표시 | 준비 완료를 임의 통과하지 않음 |
| 준비 완료 뒤 | 픽업 버튼이 활성화됨 | 음식점 변경은 API 처리 |
| 픽업 완료 | 음식점 카드에서 전달지·고객 요청사항 카드로 전환 | 합성 수령지임을 명시, 실제 고객 배송 아님 |
| 전달 완료 | 현재 업무가 해제되고 배차 대기로 돌아옴 | 주문자 수령과 별도 결과 |
| 기사 내역 | 내 정보→배달 내역→상세,1건·2,500원·요금 기준 거리0.25km | 공제·수령액/입금은 미확정·미지급과 구별 |
| 주문자 별도 수령 | 실제 휴대폰에서 수령 확인, 서버의 별도 수령 상태도 root가 재조회 | 기사 완료와 다른 행동/시각, 실제 상품 수령 아님 |

대표 화면은 [추천](../../artifacts/local/physical-phone-r67/screens/F03-food-recommendation-round2.png), [준비 전 픽업 제한](../../artifacts/local/physical-phone-r67/screens/F05-food-store-arrival.png), [준비 후](../../artifacts/local/physical-phone-r67/screens/F05-food-ready-to-pickup.png), [픽업 후 전달 카드](../../artifacts/local/physical-phone-r67/screens/F06-food-picked-up.png), [내역](../../artifacts/local/physical-phone-r67/screens/F09-food-history.png), [별도 수령 확인](../../artifacts/local/physical-phone-r67/screens/F08-orderer-receipt-confirmed.png)이다. 화면 원본은 아래 개인정보 이유로 로컬에만 둔다.

### 별도 알림음 사례03

`food-sound-03` / `FOOD-20261016060959642`는 처음 앱의 배차 알림 설정 OFF로 소리가 들리지 않았다. 휴대폰에서 ON 저장 뒤 새 제안으로 다시 시험했고, 사용자가 `소리가 들렸어.`라고 확인했다.

[청취 확인](../../artifacts/local/physical-phone-r67/audio-confirmation.json)은 `confirmedBy=user`, `secondAttempt=heard`, 채널 `food_dispatch_alerts.v2.s1v1`, 중요도4를 기록한다. 실제 경로는 **전경 polling + Android 로컬 알림**이고 `realFcmVerified=false`다. 음원 패키지 포함·OS 알림 게시·사용자 청취는 각각 다른 근거다. 정확한 음절·억양·모든 무음/권한 조합·잠금/백그라운드 수신까지 확인한 결과로 확대하지 않는다. 이 사례는 알림 확인용이며 정상 배송 사례02와 합쳐 수행 완료로 세지 않는다.

## 화물·창고: 사례05의 촬영·운송 완료와 다른 업무의 경계

- `cargo-phone-01`의 창고 연계 의뢰는 상품9개 예약·모의 후불 승인까지 진행했지만 제안 생성이 `PickupCoordinatesMissing`로 차단됐다. 기존 예약과 승인을 보존하고 임의 좌표/초기화로 성공을 만들지 않았다.
- 별도 퀵 의뢰 사례02/03은 접수·동의·승인·제안 생성 흐름을 분리했다. 사례03에서 실제 화물 추천 배너·응답 기한이 표시되는 것을 확인했다. [대표 배너](../../artifacts/local/physical-phone-r67/screens/Q03-cargo-banner-pixels.png)는 빨강 종류 표시와 남은 응답 시간을 보여준다.
- 사례04에서는 추천 상세→수락 검토까지 갔지만 경고와 확인 체크박스를 한 가로행에 두어 경고 글자가 세로로 눌리고 확인 문구가 가로로 잘렸다. 실제 수락 버튼 접근과 다음 수행으로 이어지지 못한 [오류 화면](../../artifacts/local/physical-phone-r67/screens/Q04-cargo-accept-horizontal-overflow.png)을 보존했다.
- 사례04는 제안 만료를 보존했다. 같은 만료 제안을 다시 유효하게 바꾸지 않고 별도 사례05를 생성해180에서 추천→경고 확인→수락했다.
- 사례05 의뢰 `d72367d7-6511-ce9b-cd86-e1973ece8bc6` / 운송8은 실제 휴대폰 버튼 수락이다. 기사 API 보조 수락 스크립트는 사용하지 않았다. root의06:52:26 UTC 서버 정본 대조는 지정 기사·운송 확정·큐90/900·동결 보수6,000원·제안 TTL 해제를 확인했다. 그 뒤 카메라와 상하차 업무를 별도로 수행했다.
- [추천 배너](../../artifacts/local/physical-phone-r67/screens/Q05-recommended-list-180.png), [경고 미확인 수락 제한](../../artifacts/local/physical-phone-r67/screens/Q05-visible-accept-disabled-180.png), [확인 뒤 활성화](../../artifacts/local/physical-phone-r67/screens/Q05-visible-accept-enabled-180.png), [수락 후 현재 운송](../../artifacts/local/physical-phone-r67/screens/Q06-current-actions-180.png)을 직접 픽셀로 확인했다. 현재 운송에는6,000원과 `상차 처리하기` 행동이 보인다.
- `Q05-accept-submitted-180.png`는 클릭 직후 원본 캡처여서 `아직 처리 전입니다` 안내가 남아 있다. 후속 hierarchy의 `수락되었습니다` 안내·서버 정본 확정·다음 현재 운송 화면과 시점이 다르다. 클릭 직후 PNG 자체를 성공 안내 화면으로 바꾸어 설명하지 않는다.
- 사례05의 기사 상하차 촬영·완료는 확인했다. 다른 창고 사례01의 실제 피킹·포장·출고 인계와 화주의 별도 수령 확인은 아직 미완료다. 합성 재고/포장 fixture는 실제 현장 작업 근거가 아니다.

### 사진 필수 관문과 실제 카메라·전송

| 단계 | 실제 기기에서 확인한 결과 | 원본 화면/정본 구분 |
| --- | --- | --- |
| 상차 사진 전 | 확인 항목을 체크해도 사진 없이는 상차 완료 비활성 | [사진 필수](../../artifacts/local/physical-phone-r67/screens/Q06-photo-required-180.png) |
| 첫 촬영 | Android 실제 카메라로 개인정보 없는 필통/어두운 표면을 촬영 | [카메라](../../artifacts/local/physical-phone-r67/screens/Q06-camera-ready.png), 실제 화물 상차 사진이 아님 |
| 상차 첨부 | 촬영 사진 미리보기·파일명과 상차 완료 버튼 활성 | [첨부](../../artifacts/local/physical-phone-r67/screens/Q06-photo-attached-180.png), 첨부와 완료는 별도 |
| 상차 제출 | 원본 PNG는 `상차 완료를 처리하는 중`과 동일 요청 반복 금지 안내 | [처리 중](../../artifacts/local/physical-phone-r67/screens/Q06-pickup-completion-submitted-180.png), 후속06:56:29 UTC 서버 상태는 상차완료 |
| 하차 사진 전 | 사진이 없으면 하차 완료 비활성 | [하차 사진 필수](../../artifacts/local/physical-phone-r67/screens/Q07-delivery-photo-required-180.png) |
| 두 번째 촬영·첨부 | 실제 카메라 재진입·촬영·미리보기 뒤 하차 완료 활성 | [카메라](../../artifacts/local/physical-phone-r67/screens/Q07-camera-ready.png), [첨부](../../artifacts/local/physical-phone-r67/screens/Q07-delivery-photo-attached-180.png) |
| 하차 완료 | 최종 화면에 `하차 완료를 확인했습니다` 안내 | [완료 화면](../../artifacts/local/physical-phone-r67/screens/Q08-cargo-delivery-confirmed-180.png),06:59:45 UTC 서버 운송 인수완료와 대조 |

최종 서버 정본은 운송8 `인수완료`, 동결 보수6,000원, 결제 `결제대기`, 정산 `입금대기`를 확인했다. 상위 주문은 생성됐지만 개별 운송 완료가 전체 주문·화주 별도 수령·송금 완료를 확정하지 않는다. 카메라 취소/업로드 실패·응답 유실의 모든 경우를 이 정상 두 사진으로 검증했다고 표시하지 않는다.

실기기 담당의 읽기 전용 파일/DB 대조는 상차·하차 각각 사진 receipt1개·완료 참조1개, JPEG4000×3000 두 파일·합계6,661,145 bytes, 파일 해시/크기 결속 일치를 확인했다. 로컬 근거는 `artifacts/local/physical-phone-r67/.private/20261016-070021-497155-cargo-photo-storage-verification.json`이다. 파일 위치와 원본 이미지는 비공개로 유지한다. 보조 도구의 `actualCameraCaptureEstablishedByThisHelper=false`는 유지하며 실제 카메라 조작 근거는 별도 기기 기록과 위 화면이다. 파일 검사가 기기 촬영을 대신 인증하거나 실제 경로/금융을 검증하지 않는다.

## 실기기에서 발견한 UI 보완

### 지도에는 현재 업무의 조회 오류만 표시 —179에서 확인

화물 지도 상단에 월 정산/예약 조회 실패가 계속 나와 현재 운송을 가렸다. [DriverNativeHomeWorkspaceState](../../DriverApp/Services/DriverNativeHomeWorkspaceState.cs)의 `MapWarnings`를 운행·현재 운송·상세·추천·실시간 현재 업무 범위로 제한했고, 예약 실패는 [예약 전용 화면](../../DriverApp/Components/Pages/Driver/04_Reservation/예약Page.razor)의 재조회로 다루게 했다. 실제 실패를 성공이나 빈 자료로 바꾸는 변경이 아니다.

관련 `CargoNativeHomeWorkflowTests` **23개 통과·실패/생략0개**를 [로그](../../artifacts/local/validation/20261016-physical-phone-r67/cargo-native-map-tests.log)와 [TRX](../../artifacts/local/validation/20261016-physical-phone-r67/test-results/cargo-native-map-r67.trx)에서 확인했다. 전체 회귀 시험을 이번에 새로 모두 실행한 결과는 아니다.

179 APK를 실제 설치한 뒤 [지도 화면](../../artifacts/local/physical-phone-r67/screens/M04-cargo-clean-map-179.png)에서 월 정산/예약 오류 문구가 제거되고 신규 수신 토글·최근 위치·현재 운송 카드가 남는 것을 확인했다. 초기178 오류 화면을 지우지 않았다. 이 화면은 실제 GPS 위치 맥락을 포함하지만 이동 중 연속 갱신·실제 도로 경로를 검증한 결과가 아니다.

같은179의 [예약 화면](../../artifacts/local/physical-phone-r67/screens/M08-reservation-page-179.png)에서는 예약 조회 실패와 새 `다시 시도` 행동을, [월 정산 화면](../../artifacts/local/physical-phone-r67/screens/M10-monthly-page-179.png)에서는 월 정산을 불러오지 못했다는 안내와 `다시 시도`를 실제 픽셀로 확인했다. 지도에서 보조 오류를 분리했어도 전용 화면의 오류는 그대로 보존한다. 조회 장애의 서버 원인은 이번 보완에서 고치지 않았고 재조회 성공으로 기록하지 않는다.

### 화물 수락 경고/버튼 가로 넘침 —180 기기 수락까지 확인

수락 경고·확인 체크박스를 독립된 세로 배치로 나누고 수락/보류/거절 버튼을 줄바꿈 가능한 행으로 보완했다. 이번 배치 변경 대상은 기존 [배차 처리](../../DriverApp/Components/Pages/Driver/02_Recommendation/배차처리Page.razor) 한 화면이다. 권한·기한·경고 확인·수락 API를 완화하지 않는다.

180 APK를 실제 설치하고 사례05의 경고 전체·확인 체크박스·수락/보류/거절 버튼을 한 화면에서 확인했다. 체크 전 수락 비활성·체크 후 활성화를 거쳐 실제 수락·서버 확정, 촬영·상하차 완료까지 진행했다. 초기 사례04의 오류/만료는 보존한다. 이는 사례05의 정상 경로이며 모든 예외/기한/다기기 경합의 성공이 아니다.

## 화면 자료·개인정보 검토

[manifest](../../artifacts/local/physical-phone-r67/screens/manifest.jsonl)는 각 원본 PNG의 SHA-256·전경 패키지·실기기 관측·실행 식별자를 연결한다. [개인정보 검토](../../artifacts/local/physical-phone-r67/privacy-review.json)는 실제 픽셀을 연 화면만 기록하며 업무 성공을 자동 판정하지 않는다.

선택 화면에는 실제 GPS 지도 맥락이 있고 관리자 캡처에는 로그인 식별자가 보인다. 따라서 **원본은 Git 제외 로컬 검증 폴더에 유지하고 `docs/assets/changes/`에 복사하지 않는다.** 관리자 식별자 캡처는 갤러리 제외를 권장했다. 합성 수령인·시험 연락처·원장 ID를 실제 고객 정보와 혼동하지 않는다. 원본 변경·마스킹·공개 게시를 이번 결과에 포함하지 않는다.

갤러리 생성기는 [build_gallery.py](../../artifacts/local/physical-phone-r67/build_gallery.py)이며 root가 확인한 `result.json`의 명시 단계/검증/개인정보 판정만 사용한다. 로딩·진단·실제 업무 단계·확인된 완료 범위를 구별하고, 파일명에 완료가 있어도 자동 성공으로 바꾸지 않는다. 180의 수락/사진 첨부는 업무 단계, 상차 제출 직후는 로딩, 최종 하차 화면은 정본이 확인된 `transport` 완료로 분리한다. 전체 주문/금융 완료를 생성기가 인증하지 않는다.

## 남은 업무와 다음 확인

```text
실기기 설치·로그인7앱 [확인]
├─ 음식 사례02
│  ├─ 추천→수락→도착→픽업→전달→내역 [기기 확인]
│  └─ 주문자 별도 수령→서버 재조회 [기기·서버 확인]
├─ 알림음 사례03
│  └─ OFF 원인→ON 저장→새 제안→사용자 청취 [확인]
├─ 화물 사례03/04
│  ├─ 추천 배너 [확인]
│  └─ 수락 화면 가로 넘침→180 수정 [기기 확인]
│     └─ 별도 사례05→실제 수락·서버 확정 [확인]
│        ├─ 실제 촬영2회→사진 첨부→상차/하차→서버 운송 인수완료 [확인]
│        └─ 상위 주문 마감·화주 별도 수령·결제/정산 [별도]
└─ 운영 마감
   ├─ 기사 신규 수신OFF·화면 유지 임시 설정 복원 [확인]
   ├─ 다른 사례의 미처리 대기·예약 상태 [보존·아래 기록]
   └─ FCM/백그라운드·Azure HTTPS·실물 배송·실송금 [별도]
```

- 종료 시각 정본은 아래 표처럼 보존했다. 기사 화면에 진행 운송이 없다는 것을 전체 원장의 작업0건으로 확대하지 않는다.
- 새 기기에서 안전한 배경의 실제 촬영2회·전송·사진 관문·파일/DB 결속은 확인했다. 카메라 취소·사진 업로드 실패·완료 응답 유실 등 예외는 별도 후속이다.
- 음식 추천의 접힌 카드 수락 전 보수 노출, 음식점 접수 후 자동 인쇄, 자동 배차 OFF와 수신 ON의 문구 혼동은 후속 UI 관찰이다.
- 주간 명세·공제·지급 준비·실제 은행 입금, 개인정보 실제3일 만료, 다기기 동시 수락, 실도로 ETA, 앱 잠금/프로세스 종료 FCM, Azure 운영 배포는 별도다.
- [r65 집중 업무 기간](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-work-focus.design.r65.md)은 미정/구현 전 경계를 유지한다.

## 종료 상태와 남은 문제

| 사례 | 종료 시 정본 | 남긴 의미 |
| --- | --- | --- |
| 음식01·소리03 | 주문확인·배차대기, 큐20/210, 과거 제안 만료시각 | 자동 worker OFF이므로 정리 완료라고 표시하지 않음 |
| 음식02 | 수령확인·배달완료, 큐99/990 | 이 사례의 음식 수행/별도 수령 완료 |
| 창고 연계 화물01 | 예약 재고9·가용0·출고준비중·운송대기 | 좌표 누락으로 미수행, 재고 예약을 임의 해제하지 않음 |
| 화물02·03·04 | 운송대기, 큐30/310 공개대기, 선정/확정 기사 없음 | 만료 후 공개 전환 정본을 보존, 배송 완료 아님 |
| 화물05 | 운송 인수완료, 큐90/900, 동결 보수6,000원·정산 입금대기 | 개별 운송 완료와 전체 주문/금융 마감 분리 |

최종 대조 시각은 음식07:02:41 UTC·창고 연계07:02:42 UTC·화물별07:02:54 UTC다. 현재 호스트의 TTL300 표시를 과거 모든 제안의 생성 설정으로 소급하지 않는다. 원래 화물03은60초,04·05는300초 검증 전용으로 생성됐고 이력을 보존했다.

두 기사 역할의 신규 배차 수신은 본인 설정 OFF로 마감했고, [최종180 지도](../../artifacts/local/physical-phone-r67/screens/M14-clean-map-installed-180.png)에서도 꺼짐·진행 운송 없음·보조 조회 오류 문구 제거를 확인했다. 화면 유지 임시 설정은 복원했다. 운행 종료나 휴대폰 보안 설정 변경은 하지 않았으며 전용 로컬 호스트는 workers OFF로 유지했다.

남은 실제 결함/검토 대상은 전용 월 정산·예약 조회 실패, 창고 상차 좌표 누락, 일부 시스템 하단 영역 겹침과 음식점 자동 인쇄창이다. 예약/정산 Controller의 V2_0 기능 분류 누락으로 전역 `FeatureBoundaryFilter`가 `404 FeatureBoundaryUnclassified`를 반환하는 코드 경로가 원인 후보로 확인됐다. 이번 해당 HTTP 오류 본문은 직접 채취하지 않았으므로 두 실제 화면 실패의 원인으로 확정하지 않는다. 자료가 없어서 정상0건인 결과로도 처리하지 않는다.

관련 없는 기존 미커밋 변경·DB·과거 APK/캡처를 보존했다. 운영 기본 기능·외부 지급·Unity·공개 APK 배포·commit/push는 이번 검증 완료 항목이 아니다. 실제 FCM·자동 운영 worker·Azure HTTPS 업무·실물 배송·금융과 카메라/통신의 예외 검증은 후속으로 남긴다.

원본 화면은 비공개로 유지하며, 검토한 대표 파생 화면과 판본별 설명은 [휴대폰 UI 진행 요약](2026-10-16-phone-ui-progress-summary.md)에서 확인한다.
