# DriverApp-P13 - 하차 증빙, POD, 하차 예외

## 2026-10-21 재실행 복구 책임 r3

하차 완료 요청의 보호 기록은 사진·서명 캐시와 구분한다. 같은 계정·서버의 미확정을 복원해 정본 완료 여부를 먼저 확인한다. 기록을 읽거나 지운 결과가 확인되기 전에는 재제출을 막고, 다른 운송·계정의 늦은 결과는 반영하지 않는다. [승인 범위](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/durable-workflow-recovery.r3.md).

## 2026-10-21 단계별 예외·복구 책임 r2

기사는 같은 운송의 하차·인수 결과와 지금 확인할 증빙을 판단한다. [단계·복구 대응표](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/domain-workflow-recovery.matrix.r2.md)를 따르며 사진/인수 근거 누락, 운송 보류, 미확정 요청 결과를 구별한다. 공통 창은 기존 요청/사진을 초기화하거나 완료를 재실행하지 않는다. 서버가 확정한 상태/가능 행동으로 복구하며 조회 실패를 전달 완료로 대체하지 않는다. 소스·렌더·APK/단말 증거는 각각 구분한다.

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/DriverApp/DriverApp-P13.png" alt="DriverApp-P13 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | DriverApp |
| 페이지 ID / 제목 | DriverApp-P13 - 하차 증빙, POD, 하차 예외 |
| 라우트 | /driver/transports/{운송Id:long}/dropoff |
| 소스 파일 | [DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor](../../../../../DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor) |
| 분류 | 필수 |
| 2.0 운송 필수 연결 | [DriverApp-P13 - 하차 증빙, POD, 하차 예외](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-03 보완)

### 2026-10-24 현장 신고 수명 보완

운송 변경·로그아웃·화면 종료 뒤 이전 신고 결과를 적용하지 않는다. 미확정 신고는 같은 화면의 중복 전송을 막고 운영자 확인을 안내하며, 기존 하차 사진·인계·결제/증빙 조건은 유지한다. [범위·책임](../../cargo-app-closeout-r1.md)을 따른다.

### 2026-10-21 오류 안내·업무 복귀 전 책임 확인

하차 조회 실패는 공통 오류 창과 같은 운송 GET 재조회/현재 운송 복귀로 안내한다. 완료 결과 미확정은 결과 확인 행동과 사진·입력 보관 수명을 유지하며, 임의 홈 이동이나 완료 POST 재실행을 하지 않는다. [공통 계획](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/role-error-recovery.r1.md)을 따른다.

[이번 구현·통신·시험 범위](../../../../Changes/2026-10-21-role-error-recovery-r1.md)에서 조회 오류 안내와 실제 하차 수행을 구분한다. 기존 캡처 상태를 이번 APK·실기기 검증 완료로 사용하지 않는다.

### 2026-10-21 핵심 문구 정리 전 책임 확인

완료: [변경 전후·시험·APK201 실제 재조회](../../../../Changes/2026-10-21-cargo-concise-ui-r1.md). 기존 인수완료 건의 짧은 완료 화면을 확인했으며 새 하차 사진·완료 요청은 반복하지 않았다.

기사가 같은 운송의 하차 도착·수령자 인계·결제/증빙 방식·사진을 확인하고 하차 완료를 요청한다. 주소·화물·현장 시간·수령자·필수 확인/촬영·다음 행동은 유지하고 내부 번호와 반복 설명만 줄인다. 미확정 결과는 재요청 대신 결과 확인, 사진/확인 내용 보존을 안내한다. 배송 완료·결제·정산은 별개이며 해당 상태/권한·사진/인수 관문·API는 바꾸지 않는다. [이번 범위](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-concise-ui.r1.md).

| 항목 | 기록 |
| --- | --- |
| 주 사용자·대상 | 화물 기사, 경로로 받은 운송 ID의 하차 작업 |
| 한 문장 목적 | 기사가 최신 운송을 조회하고 인수 확인·사진으로 하차 완료를 요청한다. |
| 기본 정보·주 행동 | 하차지·상태·실제 수령자/연락처·한국시간 하차 시간창, 인수/결제 확인·사진. 조건 충족 후 하차 완료 |
| 보조 정보·미지정 | 기존 화물/POD 상세 유지. 시간 미지정·연락처 미등록·거리 미확인을 임의 정상 값으로 채우지 않음 |
| 독립 업무 | 정산·정책 변경은 다른 화면. 인수/사진 입력만 해당 운송의 화면 수명 안에서 유지 |
| 진입·실패·복귀 | 캐시 없는 상세 조회, 오류·재시도·로그인, 다른 ID/이탈 뒤 응답 차단. 진행 중 운송으로 복귀 |
| 코드·API·DB | 기존 PageViewModel → 기사운송상세조회Service → 기존 상세/완료 API와 서버 운송 원장. DTO/표시 mapper로 같은 의뢰의 연락처·선택 하차 시간창 전달 |
| 책임 판정·검증 | 같은 목적의 구성. 전체 build·관련132/132; 현장 정보는 실제 소스의 mock HTTP·EF InMemory 시험 증거. 하차 현장 UI·실 HTTP/DB·운송 완주는 미검증 |

[이번 현장 정보 연결](../../cargo-contact-window-r1.md) · [최신 변경 기록](../../../../Changes/2026-10-03-cargo-contact-window-r1.md) · [이전 조회 복귀](../../cargo-workflow-recovery-r1.md).

## 왜 필요한가

이 화면은 하차 증빙, POD, 하차 예외을 담당하므로, 1.0 업무 흐름이 실제 사용자 행동으로 닫히기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 기사 / 보조 참여자: 화주, 관리자, 창고 또는 현장 담당자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 하차 증빙, POD, 하차 예외 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 하차 증빙, POD, 하차 예외
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [DriverApp-P12 - 상차 증빙, 상차 예외](../DriverApp-P12/)
- 다음 화면: [DriverApp-P14 - 월정산 확인](../DriverApp-P14/)
- 상위 화면: 없음
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 기사의 수락, 거절, 상차, 하차, 증빙, 정산 관련 조작은 화주 상세와 관리자 원장에 상태 변경으로 반영됩니다.

## API 경로와 코드 연결

- 화면 소스: [DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor](../../../../../DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor)
- 클라이언트 서비스/계약: [DriverApp/Services/DriverTransportCompletionPhotoService.cs](../../../../../DriverApp/Services/DriverTransportCompletionPhotoService.cs), [DriverApp/Services/DriverTransportExceptionService.cs](../../../../../DriverApp/Services/DriverTransportExceptionService.cs), [DriverApp/Services/IDriverSampleDataService.cs](../../../../../DriverApp/Services/IDriverSampleDataService.cs), [DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs](../../../../../DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs), [DriverApp/Services/Samples/기사샘플데이터Service.cs](../../../../../DriverApp/Services/Samples/기사샘플데이터Service.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 2.0 운송 문서 | - | `api/v1/driver/transports` | [docs/ProjectOverview/ssalddel-v1-required-pages.md](../../../ssalddel-v1-required-pages.md) | `GET api/v1/driver/transports` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/current` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`GET api/v1/driver/transports/{id:long}` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs)<br>`POST api/v1/driver/transports/{id:long}/arrive-pickup` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs) |
| 2.0 운송 문서 | - | `api/v1/files` | [docs/ProjectOverview/ssalddel-v1-required-pages.md](../../../ssalddel-v1-required-pages.md) | `POST api/v1/files/upload` [Ssalddel/Controllers/Platform/파일업로드Controller.cs](../../../../../Ssalddel/Controllers/Platform/파일업로드Controller.cs) |
| 클라이언트 서비스 | POST | `api/v1/driver/transports/{transportId}/report-exception` | [DriverApp/Services/DriverTransportExceptionService.cs](../../../../../DriverApp/Services/DriverTransportExceptionService.cs) | `POST api/v1/driver/transports/{id:long}/report-exception` [Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs](../../../../../Ssalddel/Controllers/Driver/05_Settings/기사운송진행Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

사진, POD, 서명, 문서 원본은 접근 권한, 원본 보존, 다운로드 로그가 필요합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
