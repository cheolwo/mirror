# 화물 연락처·상하차 시간창·창고 인계 r1

기존 화주 작성, 기사 홈·상하차와 창고 출고 업무 사이에 필요한 현장 정보를 연결한다. 새 업무 페이지를 만들지 않고 기존 route와 stable 운송·의뢰 ID를 유지한다. 실제 소스 기반 신규50건·관련132/132와 전체 build를 통과했고 Android 국내 표시를 확인했다. 전체 시험의 기존7실패는 동일하며 운송의 실제 완주는 미검증이다. 현재 판본의 결과와 실제 캡처는 [변경 기록](../../Changes/2026-10-03-cargo-contact-window-r1.md)에 있다.

## 화면 책임

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
