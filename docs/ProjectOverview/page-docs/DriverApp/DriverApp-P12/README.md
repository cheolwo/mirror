# DriverApp-P12 - 상차 증빙, 상차 예외

## 2026-10-21 재실행 복구 책임 r3

완료 명령 전에 같은 계정·서버·운송·상차 단계의 확인 기록을 보호 저장한다. 재실행하면 운송 결과를 먼저 조회하며, 미완료 응답이나 사진 보관 기한 경과만으로 재제출하지 않는다. 보호 기록 확인·정리에 실패하면 기존 사진을 보존하고 완료 버튼을 잠근다. [승인 범위](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/durable-workflow-recovery.r3.md).

## 2026-10-21 단계별 예외·복구 책임 r2

기사는 같은 운송의 상차 단계에서 사진·인계·보류 등 업무 사유와 처리 결과의 확정 여부를 확인한다. [단계·복구 대응표](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/domain-workflow-recovery.matrix.r2.md)에 따라 기존 서버 guard와 가능 행동을 소비한다. 조회/안내는 완료·재업로드·인계 명령이 아니며 미확정 결과에서 강제 복귀하거나 자동 재전송하지 않는다. 사진/요청과 필수 확인은 기존 소유자에 유지한다. 원시 오류 코드와 기술 응답 대신 상차 문맥의 짧은 안내를 제공하며 실제 단말 검증 범위는 별도로 남긴다.

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/DriverApp/DriverApp-P12.png" alt="DriverApp-P12 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | DriverApp |
| 페이지 ID / 제목 | DriverApp-P12 - 상차 증빙, 상차 예외 |
| 라우트 | /driver/transports/{운송Id:long}/pickup |
| 소스 파일 | [DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor](../../../../../DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor) |
| 분류 | 필수 |
| 2.0 운송 필수 연결 | [DriverApp-P12 - 상차 증빙, 상차 예외](../../../ssalddel-v1-required-pages.md) |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-03 보완)

### 2026-10-24 현장 신고 수명 보완

신고는 선택 운송과 인증 문맥에 묶고 화면 종료 뒤 늦은 응답을 표시하지 않는다. 처리 중·결과 미확정일 때 같은 화면에서 재전송하지 않으며, 미확정이면 운영자 확인을 안내한다. 이 보호를 앱 재실행 뒤 영속 복구로 표현하지 않는다. [범위·책임](../../cargo-app-closeout-r1.md)을 따른다.

### 2026-10-21 오류 안내·업무 복귀 전 책임 확인

상차 조회 실패는 공통 오류 창과 같은 운송 GET 재조회/현재 운송 복귀로 안내한다. 완료 결과가 미확정이면 재전송 대신 결과 확인을 우선하고, 기존 사진·체크·요청 수명을 보존한다. 창을 닫는 행동을 업무 완료나 입력 초기화로 사용하지 않는다. [공통 계획](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/role-error-recovery.r1.md)을 따른다.

[이번 구현·통신·시험 범위](../../../../Changes/2026-10-21-role-error-recovery-r1.md)에서 조회 오류 안내와 실제 상차 수행을 구분한다. 기존 캡처 상태를 이번 APK·실기기 검증 완료로 사용하지 않는다.

### 2026-10-21 핵심 문구 정리 전 책임 확인

완료: [문구·회귀·소비 빌드와 기기 확인 범위](../../../../Changes/2026-10-21-cargo-concise-ui-r1.md). 새 미완료 상차 문구는 소스/시험 범위이며 이번에 새 상차 사진·완료 요청을 반복하지 않았다.

기사가 같은 운송의 상차 도착·화물/현장 확인·인수증·사진을 갖추어 상차 완료를 요청한다. 기본 표시는 상차지·화물·다음 목적지·담당자·시간·확인 항목·촬영/완료 행동이다. 내부 화면 번호·원시 의뢰 ID·반복 설명은 제외하고 현장 확인 라벨은 핵심 단어로 줄인다. 필수 사진/서명·예외 신고·미확정 결과 재조회·권한·인수증 생략 사유는 유지한다. 상태 판정/전송/초안/복구·자동 하차 이동은 바꾸지 않는다. [이번 범위](../../../../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-concise-ui.r1.md).

| 항목 | 기록 |
| --- | --- |
| 주 사용자·대상 | 화물 기사, 경로로 받은 운송 ID의 상차 작업 |
| 한 문장 목적 | 기사가 선택 운송을 서버에서 확인하고 현장 확인·인수증·사진으로 상차 완료를 요청한다. |
| 기본 정보·주 행동 | 최신 운송 상태·상차지·실제 담당자/연락처·한국시간 상차 시간창과 현장 확인·증빙. 서버 조건 충족 후 상차 완료 |
| 보조 정보·미지정 | 긴 화물/인수증 상세는 기존 구성을 유지. 시간 미지정·연락처 미등록을 임의 정보로 채우지 않음 |
| 독립 업무 | 메뉴·정산·다른 운송은 별도 화면. 이 화면에서 운임 정책이나 원장을 직접 변경하지 않음 |
| 진입·실패·복귀 | 캐시 없이도 기존 상세 API 조회. 불러오기·실패·재시도·로그인 복귀, 다른 ID/이탈 뒤 응답 차단. 진행 중 운송으로 복귀 |
| 코드·API·DB | 기존 PageViewModel → 기사운송상세조회Service → 기존 상세 API. DTO/표시 mapper가 같은 의뢰의 연락처·시간창을 전달. 창고 연계는 현장 도착·출고 인계가 서버 원장에서 선행되어야 하며 일반 운송과 구별 |
| 책임 판정·검증 | 같은 목적의 구성. 전체 build·관련132/132; 현장 정보/창고 조건은 실제 소스의 mock HTTP·EF InMemory 시험 증거. 상차 현장 UI·실 HTTP/DB·운송 완주는 미검증 |

[이번 현장 정보·창고 연결](../../cargo-contact-window-r1.md) · [최신 변경 기록](../../../../Changes/2026-10-03-cargo-contact-window-r1.md) · [이전 조회 복귀](../../cargo-workflow-recovery-r1.md).

## 왜 필요한가

이 화면은 상차 증빙, 상차 예외을 담당하므로, 1.0 업무 흐름이 실제 사용자 행동으로 닫히기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 기사 / 보조 참여자: 화주, 관리자, 창고 또는 현장 담당자

이 화면은 살뜰 2.0 국내 화물 운송 워크플로우 안에서 상차 증빙, 상차 예외 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 상차 증빙, 상차 예외
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [DriverApp-P11 - 진행 중 운송과 다음 행동](../DriverApp-P11/)
- 다음 화면: [DriverApp-P13 - 하차 증빙, POD, 하차 예외](../DriverApp-P13/)
- 상위 화면: 없음
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 기사의 수락, 거절, 상차, 하차, 증빙, 정산 관련 조작은 화주 상세와 관리자 원장에 상태 변경으로 반영됩니다.

## API 경로와 코드 연결

- 화면 소스: [DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor](../../../../../DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor)
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
