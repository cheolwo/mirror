# DriverApp-P07 - 지도 홈, 추천 배너, 현재 운송 진입

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/DriverApp/DriverApp-P07.png" alt="DriverApp-P07 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | DriverApp |
| 페이지 ID / 제목 | DriverApp-P07 - 지도 홈, 추천 배너, 현재 운송 진입 |
| 라우트 | /driver/home |
| 소스 파일 | [DriverApp/Components/Pages/Home.razor](../../../../../DriverApp/Components/Pages/Home.razor) |
| 분류 | 필수 |
| 2.0 운송 필수 연결 | [DriverApp-P07 - 지도 홈, 추천 배너, 현재 운송 진입](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-03 국내 운행·현장 정보 연결)

| 항목 | 기록 |
| --- | --- |
| 주 사용자·소스 | 화물 기사, 기존 `/driver/home`과 NativeDriverHomePage |
| 대상·진입 문맥 | 현재 로그인에 따른 국내 운행과 추천/현재 운송 요약 |
| 한 문장 목적 | 기사가 국내 운행 상태를 보고 현재 운송 또는 메뉴로 들어간다. |
| 완료 결과·주 행동 | 현재 운송은 `/driver/transports/current`, 메뉴는 `/driver/menu`로 인계 |
| 기본·보조 정보 | 대한민국 운행·NAVER provider와 기존 지도/추천/현재 운송. 연락처·시간창 상세는 상하차 화면에서 확인 |
| 독립 업무·제외 정보 | 상하차·정산은 기존 업무 화면이 소유. 국내만 지원하므로 한국/미국 선택 버튼은 두지 않음 |
| 진입·실패·복귀 | 기존 MainPage가 있으면 먼저 PopAsync로 복원한 뒤 목적 경로 전달. 새 화면은 StartPath. 익명 하단 요약의 미확인/정상 빈 상태 혼동은 이전 결손으로 남음 |
| 코드·API·DB | NativeDriverHomePage → DriverOperatingProfileService의 Korea 고정·저장 KR 정규화. 기존 추천/운송 서버 조회와 stable ID 유지 |
| 책임 판정·검증 | 같은 목적의 진입 허브. 전체 build·관련132/132, 최종 APK의 실제 국내 운행/NAVER 표시·선택 버튼 부재 확인. 지도 타일/경로·연락처/시간창 UI·운송 완주는 미검증 |

[이번 현장 연결·국내 운행](../../cargo-contact-window-r1.md) · [최신 변경 기록](../../../../Changes/2026-10-03-cargo-contact-window-r1.md) · [이전 조회 복귀](../../cargo-workflow-recovery-r1.md).

## 왜 필요한가

이 화면은 지도 홈, 추천 배너, 현재 운송 진입을 담당하므로, 1.0 업무 흐름이 실제 사용자 행동으로 닫히기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 기사 / 보조 참여자: 화주, 관리자, 창고 또는 현장 담당자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 지도 홈, 추천 배너, 현재 운송 진입 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 지도 홈, 추천 배너, 현재 운송 진입
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [DriverApp-P06-1 - 운행 조건과 선호 설정](../DriverApp-P06-1/)
- 다음 화면: [DriverApp-P07-1 - 기사 업무 허브/요약](../DriverApp-P07-1/)
- 상위 화면: 없음
- 하위 화면: [DriverApp-P07-1 - 기사 업무 허브/요약](../DriverApp-P07-1/)

상호작용 관점에서는 다음 흐름을 우선 봅니다. 기사의 수락, 거절, 상차, 하차, 증빙, 정산 관련 조작은 화주 상세와 관리자 원장에 상태 변경으로 반영됩니다.

## API 경로와 코드 연결

- 화면 소스: [DriverApp/Components/Pages/Home.razor](../../../../../DriverApp/Components/Pages/Home.razor)
- 클라이언트 서비스/계약: [DriverApp/Services/AuthSession.cs](../../../../../DriverApp/Services/AuthSession.cs), [DriverApp/Services/DriverAppProfile.cs](../../../../../DriverApp/Services/DriverAppProfile.cs), [DriverApp/Services/IAuthSession.cs](../../../../../DriverApp/Services/IAuthSession.cs), [DriverApp/Services/IDriverSampleDataService.cs](../../../../../DriverApp/Services/IDriverSampleDataService.cs), [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs), [DriverApp/Services/Samples/기사샘플데이터Service.cs](../../../../../DriverApp/Services/Samples/기사샘플데이터Service.cs), [SsalddelApp/Services/AuthSession.cs](../../../../../SsalddelApp/Services/AuthSession.cs), [SsalddelApp/Services/IAuthSession.cs](../../../../../SsalddelApp/Services/IAuthSession.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 클라이언트 서비스 | - | `api/v1/driver/recommendations` | [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs) | `GET api/v1/driver/recommendations` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs)<br>`GET api/v1/driver/recommendations/idle` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs)<br>`GET api/v1/driver/recommendations/driving` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs)<br>`GET api/v1/driver/recommendations/search` [Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs](../../../../../Ssalddel/Controllers/Driver/02_Recommendation/기사배차추천Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/driver/reservations` | [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs) | `GET api/v1/driver/reservations` [Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs](../../../../../Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs)<br>`POST api/v1/driver/reservations` [Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs](../../../../../Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs)<br>`POST api/v1/driver/reservations/{id:long}/cancel` [Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs](../../../../../Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs)<br>`GET api/v1/driver/reservations/{id:long}` [Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs](../../../../../Ssalddel/Controllers/Driver/04_Reservation/기사예약Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/driver/settlements/current-month` | [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs) | `GET api/v1/driver/settlements` [Ssalddel/Controllers/Driver/06_Settlement/기사정산Controller.cs](../../../../../Ssalddel/Controllers/Driver/06_Settlement/기사정산Controller.cs)<br>`GET api/v1/driver/settlements/current-month` [Ssalddel/Controllers/Driver/06_Settlement/기사정산Controller.cs](../../../../../Ssalddel/Controllers/Driver/06_Settlement/기사정산Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/driver/transports` | [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs) | `GET api/v1/driver/transports` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/current` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/{id:long}` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`POST api/v1/driver/transports/{id:long}/arrive-pickup` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/driver/work/current` | [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs) | `GET api/v1/driver/work/current` [Ssalddel/Controllers/Driver/01_Work/기사운행Controller.cs](../../../../../Ssalddel/Controllers/Driver/01_Work/기사운행Controller.cs) |
| 클라이언트 서비스 | - | `api/v1/driver/work/status` | [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs) | `GET api/v1/driver/work/status` [Ssalddel/Controllers/Driver/01_Work/기사운행Controller.cs](../../../../../Ssalddel/Controllers/Driver/01_Work/기사운행Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

위치, 주소, 운행 상태는 최소 공개와 최신성 표시가 필요합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
