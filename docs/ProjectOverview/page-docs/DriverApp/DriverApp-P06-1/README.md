# DriverApp-P06-1 - 운행 조건과 선호 설정

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

2026-10-15 r71: 아래는 기존 캡처이며 새 업무·적재 설정의 실행 증거가 아닙니다. 새 공통 폼의 정적 렌더·소비 앱 빌드와 실제 모바일 입력·배치는 별도로 검증합니다.

<img src="../../../assets/app-pages/DriverApp/DriverApp-P06-1.png" alt="DriverApp-P06-1 화면 캡처" width="720">

## 업무·적재 설정 r71 책임 카드

| 항목 | 현재 구성 |
| --- | --- |
| 주 사용자·질문 | 같은 기사 계정이 어떤 업무를 받고, 현재 차량에 어느 정도 적재할 수 있는가? |
| 페이지·진입 | DriverApp의 기존 `/driver/work/settings`와 메뉴. FDriverApp 내 정보 → `food-work-preferences`에서 같은 공통 폼을 사용한다. |
| 완료 결과 | 음식·소형 물품 퀵·화물의 희망 업무, 별도 직접 피킹, 차량·중량 kg·부피 L·희망 동시 건수·추가 배차 의사를 서버에 저장하고 정본으로 다시 확인한다. |
| 기본 정보·주 행동 | 받을 업무 → 차량·적재 조건 → 추가 배차의 세 섹션. 주 행동은 설정 저장이며 로딩·오류·저장 결과 미확인은 바로 표시한다. 알 수 없는 제원은 빈칸으로 둔다. |
| 독립 업무 | 운행 시작·종료, 실제 배차 수락, 피킹·전달 수행, 정산은 기존 화면과 전문 원장에서 처리한다. 선택은 수행 자격이나 혼합 적재 승인·권한을 만들지 않는다. |
| 준비 경계 | 공통 수락 기능이 꺼져 있어도 설정은 준비할 수 있다. 실제 제안은 서버의 기존 역할·자격과 운영 준비 상태를 따른다. |
| 실패·복귀 | 판본을 포함한 PUT 한 번 뒤 GET으로 확인한다. 결과가 불명확하면 재전송을 잠그고 정본 조회만 허용한다. 계정/로그인 판본 변경·화면 종료의 늦은 응답은 버린다. 기존 메뉴/내 정보로 돌아간다. |
| page → code → DB | [공통 화면](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/DriverPreferences/DriverWorkParticipationScreen.razor) → [표시·저장 확인 VM](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/DriverPreferences/DriverWorkParticipationViewModel.cs) → 두 앱의 기존 인증 HTTP adapter → `GET/PUT api/v1/driver/work-participation` → 기사 본인 SQL 설정·revision. |
| 검증 범위 | 세션·응답 유실·판본과 실제 공통 Razor 정적 렌더를 집중 시험으로 확인한다. 실제 브라우저 배치, APK, 단말, 운영 DB·배차는 각 별도 증거다. |

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | DriverApp |
| 페이지 ID / 제목 | DriverApp-P06-1 - 운행 조건과 선호 설정 |
| 라우트 | /driver/work/settings |
| 소스 파일 | [DriverApp/Components/Pages/Driver/04_Settings/운행설정Page.razor](../../../../../DriverApp/Components/Pages/Driver/04_Settings/운행설정Page.razor) |
| 분류 | 보조 |
| 2.0 운송 필수 연결 | [DriverApp-P06-1 - 운행 조건과 선호 설정](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 왜 필요한가

이 화면은 운행 조건과 선호 설정을 담당하므로, 주 업무 화면을 보조하고 사용자가 다음 행동으로 이동할 수 있게 합니다.

## 사용자와 참여자

주 사용자: 기사 / 보조 참여자: 화주, 관리자, 창고 또는 현장 담당자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 운행 조건과 선호 설정 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 운행 조건과 선호 설정
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [DriverApp-P06 - 운행 시작, 위치 송신 시작](../DriverApp-P06/)
- 다음 화면: [DriverApp-P07 - 지도 홈, 추천 배너, 현재 운송 진입](../DriverApp-P07/)
- 상위 화면: [DriverApp-P06 - 운행 시작, 위치 송신 시작](../DriverApp-P06/)
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 기사의 수락, 거절, 상차, 하차, 증빙, 정산 관련 조작은 화주 상세와 관리자 원장에 상태 변경으로 반영됩니다.

## API 경로와 코드 연결

- 화면 소스: [DriverApp/Components/Pages/Driver/04_Settings/운행설정Page.razor](../../../../../DriverApp/Components/Pages/Driver/04_Settings/운행설정Page.razor)
- 클라이언트 서비스/계약: [DriverApp/Services/IDriverSampleDataService.cs](../../../../../DriverApp/Services/IDriverSampleDataService.cs), [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs), [DriverApp/Services/Samples/기사샘플데이터Service.cs](../../../../../DriverApp/Services/Samples/기사샘플데이터Service.cs)

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
