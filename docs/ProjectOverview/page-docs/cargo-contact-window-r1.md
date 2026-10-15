# 화물 연락처·상하차 시간창·창고 인계 r1

기존 화주 작성, 기사 홈·상하차와 창고 출고 업무 사이에 필요한 현장 정보를 연결한다. 새 업무 페이지를 만들지 않고 기존 route와 stable 운송·의뢰 ID를 유지한다. 실제 소스 기반 신규50건·관련132/132와 전체 build를 통과했고 Android 국내 표시를 확인했다. 전체 시험의 기존7실패는 동일하며 운송의 실제 완주는 미검증이다. 현재 판본의 결과와 실제 캡처는 [변경 기록](../../Changes/2026-10-03-cargo-contact-window-r1.md)에 있다.

## 화면 책임

### 국내 화물 한 건 마감 r69 · 신규 작성과 본인 동의 (2026-10-14)

[승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/cargo-closeout.implementation.r69.md)에 따른 구현과 로컬 회귀 시험·서버 HTML 검증을 완료했다. 아래 책임은 기존 `/shipper/request/review`의 보완이며 새 독립 페이지가 아니다. 실행 결과는 [r69 변경 기록](../../Changes/2026-10-14-cargo-closeout-r69.md)이 소유한다. 아래의 과거 r1 시험·Android 기록을 r69의 검증 근거로 재사용하지 않는다.

| 항목 | 현재 책임 |
| --- | --- |
| 주 사용자·한 문장 목적 | 화주가 작성한 화물·장소·시간·금액과 본인 개인정보 동의를 확인해 같은 의뢰를 접수하는가 |
| 기본 정보 | 기존 작성 요약과 필수 누락, 운송대행 개인정보 정책의 목적·항목·기간, 명시적 수집·이용 및 본인·연령 체크, 서버 동의 기록 결과 |
| 보조 정보 | 기존 정책의 동의 거부·제3자 제공·국외 이전 안내와 판본은 접힌 안내에서 확인한다. 금융 시점·수납 주체가 불명확한 선택은 접수 보류 이유를 표시한다. |
| 완료·다음 행동 | 두 체크 후 명시적으로 동의 기록을 요청하고, 본인 API의 유효한 증적 ID·업무·출처·판본을 확인한 뒤 등록한다. 접수의 번호·원문·증적을 보호 저장하고 생성 결과가 확인되면 기존 상세로 이동한다. |
| 실패·복귀 | 동의 실패·체크 해제·계정 변경은 새 접수를 막는다. 접수 응답 유실은 같은 번호의 결과 조회부터 재개한다. 구판 보호 원문에 동의 증적이 없으면 조회만 허용하며 가짜 증적을 붙여 재전송하지 않는다. |
| 독립 업무 | 기사 수락·상하차·화주 인수·지급 준비·실제 송금은 이 검토 화면의 완료가 아니다. |
| 책임 판정 | 같은 목적의 필수 선행 조건 조립. 공통 Screen/동의 Panel은 표시·콜백만, MAUI 작성 ViewModel은 계정·접수·동의 수명을, 서버는 유효성·업무 상태 확정을 소유한다. |

입력한 설명·수량·중량·부피·온도는 기존 `CargoDTO` 필드로 전달하고 미확인을 수량 1이나 중량 0으로 만들지 않는다. 적재 형태는 전용 계약 필드가 없어 기존 요금 `요청사항`의 명시된 작업 조건으로 보존하며 적재 알고리즘의 확정 입력으로 승격하지 않는다. 기존 추가요금·서비스 조건·알선 메모를 전송하고 기존 수정의 독립 원값 복사를 유지한다. 카드·가상계좌·계좌이체는 선택한 enum을 보존한다. `인수증`·`현금성 정산`은 금융 시점이나 수납 주체가 불명확해 임의의 카드·현장·후불 계약으로 변환하지 않는다.

동의는 기존 `api/v1/common/application-privacy-consents`와 `transport-proxy`/`community-map` 계약을 재사용한다. [검토 route](../../../SsalddelApp/Components/Pages/ShipperRequestReviewPage.razor) → [작성 ViewModel](../../../SsalddelApp/ViewModels/Shipper/ShipperRequestAuthoringPageViewModel.cs) → [서버 adapter](../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs) → 기존 신청 동의 증적 및 운송 의뢰 원장으로 연결한다. [공통 동의 Panel](../../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperPrivacyConsentPanel.razor)에는 API·계정 권위를 두지 않는다. 체크는 기본 미선택이고 모의 동의 응답은 시험·loopback 표본에서만 제공한다.

정적 렌더 시험은 정책 원문·미선택·등록 선행 조건을, 작성/HTTP 대역 시험은 입력 보존·같은 복구 원문·동의 실패·늦은 응답·계정 변경을 다룬다. 실제 소비 Razor/레이아웃을 링크한 `artifacts/local/cargo-closeout-r69/ui-preview/`는 표본 준비이며 빌드·브라우저·APK·실기기·실제 동의·운영 준수 완료를 뜻하지 않는다.

화주 인수증의 입력·등록 UI는 후속 미연결 항목이다. 현재 모바일 상세는 저장된 인수증 번호·등록 시각을 읽기 전용으로 표시하며, Web 결제·정산 서비스의 등록 API 호출을 실제 화주 화면에 연결한 것으로 보지 않는다. r69 서버 보완은 실제 운송의 인수 완료·도착 시각과 완료 결과 인계, 요청자 권한을 다시 검사한다. 허용된 후불 승인·인수증 대기뿐 아니라 이미 청구·입금·정산으로 진행된 원장에도 인수증을 기록할 수 있지만 금융 상태를 이전 단계로 되돌리지 않는다. 이번에는 새 수령 완료 폼이나 앱 등록 행동을 추가하지 않으며 API 검증과 앱 화면 완료를 구분한다.

2026-10-10 예외 지속 보완의 책임: 화주 검토 화면은 고정된 접수 번호의 결과 확인과 명시 재전송을 구분하며 초기 차량 조회만 재시도할 수 있다. 하차 화면은 사진 전송/완료 요청 이후 같은 운송 정본을 확인하고 확인된 완료에서는 이전 완료 입력을 닫는다. 조회 실패는 미저장 단정 대신 결과 확인 상태로 안내한다. [승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.implementation.r1.md).

| 화면 | 한 가지 업무 질문과 결과 | 기본 표시·입력 | 다른 업무로 인계 |
| --- | --- | --- | --- |
| [화주 작성 P02](SsalddelApp/SsalddelApp-P02/README.md) | 화주가 운송 현장의 조건을 확인해 의뢰를 등록하는가 | 실제 상하차 담당자·연락처, 상차 시간창 시작/종료, 선택 하차 시간창 시작/종료 | 서버 등록 후 기존 의뢰 상세·기사 배차로 인계 |
| [기사 홈 P07](DriverApp/DriverApp-P07/README.md) | 기사가 국내 운행 상태를 보고 현재 운송 또는 메뉴로 들어가는가 | 대한민국 운행·NAVER provider 표시, 기존 추천·현재 운송 | 상하차·정산은 기존 업무 화면이 소유 |
| [상차 P12](DriverApp/DriverApp-P12/README.md) | 기사가 같은 운송의 상차 시간과 담당자를 확인하고 현장 인계를 마치는가 | 서버 상차 시간창·담당자·연락처, 기존 현장·사진·인수증 조건 | 창고 연계면 출고 인계가 선행하며 완료 뒤 기존 하차 업무 |
| [하차 P13](DriverApp/DriverApp-P13/README.md) | 기사가 같은 운송의 하차 조건을 확인하고 인수·사진으로 완료를 요청하는가 | 서버 하차 시간창·수령자·연락처와 기존 증빙 | 완료 내역·정산은 기존 업무로 이동 |

상하차 시간창은 네 개의 시작/종료 시각이다. 4시간 고정 길이라는 뜻이 아니다. 상차의 시작·종료는 기존 서버 계약에 맞게 필수로 입력하고, 하차는 둘 다 미지정할 수 있다. 한쪽만 입력하거나 종료가 시작과 같거나 이르면 등록할 수 없다. 한국시간으로 입력한 값을 UTC로 전달하고 기사 화면에서는 다시 한국시간으로 표시한다. 연락처와 시간 미지정을 임의 전화번호·현재 시각·0으로 대체하지 않는다.

## 페이지 → 코드 → 기존 원장

- 화주 입력: [작성 시작](../../../SsalddelApp/Components/Pages/ShipperRequestWizard.razor) → [공통 운송 입력](../../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperRequestTransportScreen.razor) → [작성 ViewModel](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/운송의뢰작성ViewModel.cs)·[Draft](../../../Ssalddel.Ui.Common/Areas/App/Models/운송모델작성Draft.cs) → [MAUI 등록 서비스](../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs) → 기존 `POST api/v1/shipper/requests`와 `LocationContactDTO`/`TimeWindowDTO`. 화면 시작만으로 배차·결제는 실행되지 않는다.
- 서버 투영: 기존 [기사 운송 DTO](../../../Ssalddel.Contracts/Driver/Transport/기사운송Dtos.cs)와 [목록](../../../Ssalddel/Application/Driver/Transport/Handlers/운송목록조회QueryHandler.cs)·[현재](../../../Ssalddel/Application/Driver/Transport/Handlers/운송현재조회QueryHandler.cs)·[상세](../../../Ssalddel/Application/Driver/Transport/Handlers/운송상세조회QueryHandler.cs) 조회에서 같은 의뢰의 연락처·시간창을 전달한다. 조회는 의뢰 ID를 우선하고 기존 운송번호 fallback을 유지하며, 기존 화주 의뢰·운송 원장의 연결을 사용한다.
- 기사 표시: [표시 mapper](../../../DriverApp/Services/기사운송표시Mapper.cs) → [운송 모델](../../../DriverApp/Models/Driver/Samples/기사샘플모델.cs) → 기존 [상차](../../../DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor)·[하차](../../../DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor)의 필요한 현장 정보. 시간 미지정은 `시간 미지정`, 연락처 누락은 `연락처 미등록`으로 표시한다. 이전 [조회 복귀 r1](cargo-workflow-recovery-r1.md)의 인증·선택·이탈 경계를 유지한다.
- 창고 인계: 기존 [출고운송인계완료UseCase](../../../Ssalddel/Application/Warehouse/출고운송인계완료UseCase.cs)와 [기사 상차 완료](../../../Ssalddel/Application/Driver/Transport/Handlers/운송상차완료CommandHandler.cs)를 연결한다. 같은 의뢰의 창고 연계 출고예정이 있으면 현장 도착·상품 인계의 선행 조건을 서버 원장에서 확인한다. 창고 미인계 상태의 상차 완료는 차단하고, 재고 차감과 인계 멱등 처리는 기존 출고 책임에 둔다. 일반 비창고 운송에 창고 인계를 새 필수조건으로 붙이지 않는다.

## 국내 기사 설정과 공개 범위

[네이티브 기사 홈](../../../DriverApp/NativeDriverHomePage.xaml)에는 지역 선택 버튼을 두지 않고 `대한민국 운행`과 현재 NAVER provider만 표시한다. [DriverOperatingProfileService](../../../DriverApp/Services/DriverOperatingProfileService.cs)는 국내 Korea 프로필만 사용하며 과거 저장된 US/기타 값과 기존 `SetMarket` 진입점도 KR로 정규화한다. 기존 Preference key·공용 프로필 stable 계약은 보존한다. 공용 미국 프로필·서버·공공데이터·provider 호환 기능을 삭제하는 작업은 아니다.

상세 주소와 현장 연락처는 운송 준비에 필요한 역할 정보다. 기존 공개 커뮤니티 초안에는 포함하지 않고, 기사 조회의 권한과 필요한 현장 표시 범위를 유지한다. 연락처 조회가 통화 성공이나 상대방 확인의 증거는 아니다.

## 실행·검증 경계

기존 API route와 `DomesticTransportWorkflow`/`CargoYongdalV1`의 기본 비활성 경계를 유지한다. `Simulation`을 실제 배차·계약·결제·은행 지급으로 승격하지 않는다. 이번50건은 실제 소스와 mock HTTP·EF InMemory 시험이며 실제 작성 UI·실 HTTP/DB 왕복이나 화주 등록→기사 수락→창고 도착/출고→상차→하차의 완주 증거가 아니다. 실제 UI는 최종 Driver APK의 국내 운행/NAVER 문구와 선택 버튼 부재만 확인했다.

Fast `20261003-161156`은 전체3.5 build·132/132, Task `20261003-161453`은 전체 build·5,794/5,801이다. 실패7개는 baseline `20261003-150321`과 이름·메시지·stack 동일하고 새 실패0이지만 전체 게이트는 미통과다. 코드39경로와 소스/APK 지문은 Git 제외 `artifacts/local/cargo-contact-window-r1/`에 보관한다.

기존5321 서버는 이번 소스로 재배포하지 않았다. Controller의 feature on/off는 시험 증거이며 이전 API404 해소나 실제 `FeatureDisabled` 응답 확인이 아니다. 창고 재고 의뢰의 실제 하차 담당자·전화 연결, 네이티브 익명 요약의 미확인/빈 상태 구분, 이전 Android 입력 ANR 원인·해소는 남은 결손이다.

후속 검증은 모바일 날짜 입력과 연락처 표시, 실제 동일 ID 왕복·창고 실패/재시도·재고 이동·완주, 과거 US 기기 저장 값의 복원 및 로그인 복귀를 다룬다. 지도 타일·주행 경로·카메라·서명·전화·물리 기기·운영 운송·실결제/은행 지급은 별도다.
