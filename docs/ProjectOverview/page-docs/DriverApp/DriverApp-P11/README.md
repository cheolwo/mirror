# DriverApp-P11 - 진행 중 운송과 다음 행동

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/DriverApp/DriverApp-P11.png" alt="DriverApp-P11 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | DriverApp |
| 페이지 ID / 제목 | DriverApp-P11 - 진행 중 운송과 다음 행동 |
| 라우트 | /driver/transports/current |
| 소스 파일 | [DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor](../../../../../DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor) |
| 분류 | 필수 |
| 2.0 운송 필수 연결 | [DriverApp-P11 - 진행 중 운송과 다음 행동](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-03 조회 복귀)

기사가 현재 세션의 진행 운송을 확인하고 같은 운송 ID의 상차·하차 업무로 이동한다. 기존 상태·주소·수익·다음 행동과 정상 빈 상태의 추천/운행 시작을 유지한다. 실패는 재조회, 인증 종료는 현재 운송 returnUrl로 로그인한다. 초기 조회부터 페이지 취소를 전달하고 이탈 뒤 폴링·렌더를 시작하지 않는다. 다른 사용자 또는 같은 사용자 재로그인 뒤 캐시를 숨기고 강제 조회하며 정상 토큰 갱신은 같은 문맥으로 둔다. 완료·원장 권위는 기존 서버에 있고 월 이용료와 지급 수령액을 합치지 않는다. [구현·검증 범위](../../cargo-workflow-recovery-r1.md).

[이번 화물 보완·검증 범위](../../cargo-workflow-recovery-r1.md) · [최신 변경 기록](../../../../Changes/2026-10-03-cargo-workflow-recovery-r1.md).

## 왜 필요한가

이 화면은 진행 중 운송과 다음 행동을 담당하므로, 1.0 업무 흐름이 실제 사용자 행동으로 닫히기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 기사 / 보조 참여자: 화주, 관리자, 창고 또는 현장 담당자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 진행 중 운송과 다음 행동 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 진행 중 운송과 다음 행동
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [DriverApp-P10 - 추천 수락/거절/보류 처리](../DriverApp-P10/)
- 다음 화면: [DriverApp-P12 - 상차 증빙, 상차 예외](../DriverApp-P12/)
- 상위 화면: 없음
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 기사의 수락, 거절, 상차, 하차, 증빙, 정산 관련 조작은 화주 상세와 관리자 원장에 상태 변경으로 반영됩니다.

## API 경로와 코드 연결

- 화면 소스: [DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor](../../../../../DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor)
- 클라이언트 서비스/계약: [DriverApp/Services/IDriverSampleDataService.cs](../../../../../DriverApp/Services/IDriverSampleDataService.cs), [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs), [DriverApp/Services/Samples/기사샘플데이터Service.cs](../../../../../DriverApp/Services/Samples/기사샘플데이터Service.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 2.0 운송 문서 | - | `api/v1/driver/transports` | [docs/ProjectOverview/ssalddel-v1-required-pages.md](../../../ssalddel-v1-required-pages.md) | `GET api/v1/driver/transports` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/current` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/{id:long}` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`POST api/v1/driver/transports/{id:long}/arrive-pickup` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs) |
| 워크플로우 문서 | - | `api/v1/driver/transports` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `GET api/v1/driver/transports` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/current` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/{id:long}` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`POST api/v1/driver/transports/{id:long}/arrive-pickup` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs) |
| 워크플로우 문서 | - | `api/v1/files` | [docs/ProjectOverview/workflow-app-screen-map.md](../../../workflow-app-screen-map.md) | `POST api/v1/files/upload` [Ssalddel/Controllers/Platform/파일업로드Controller.cs](../../../../../Ssalddel/Controllers/Platform/파일업로드Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

기사 개인정보, 위치, 운행 상태, 추천 수락/거절 기록은 필요한 범위 안에서만 노출해야 합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
