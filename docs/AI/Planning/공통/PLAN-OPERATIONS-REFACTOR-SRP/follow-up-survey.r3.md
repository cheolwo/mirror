# 운영 앱·서버 단일 책임 후속 조사 r3

2026-10-21 · 상태: `ProposedOnly` · 이번 요청은 **조사와 우선순위 제안만**이다. 제품 코드·시험·설정은 변경하지 않는다.

추가로 정리할 후보는 **14개**다. 새 기능보다 **게시/알림의 작은 공통 경계 → 기사 보류 상태 → 정산·입고 계산 → 확정 후 후속 실행** 순서로 권고한다. 긴 파일을 잘게 쪼개는 작업이 아니라, 변경 이유·실패 경계·상태 수명이 다른 책임을 분리하는 작업이다.

## 현재 기준과 조사 한계

- 소스 **8,723개**, 프로젝트 **85개**, 총 **8,808개** 경로와 소스 **1,348,410줄**을 구조 색인했다. 시험 파일은 **1,838개**다. 생성물/bin/obj/vendor/private artifacts 등은 제외한다.
- 선언된 프로젝트 참조는 **214개**이며 이 그래프에서 순환을 찾지 못했다. 조건부 참조의 실제 적용이나 런타임 의존성을 모두 평가한 결과는 아니다.
- 이전 r1 **22개**, r2 **21개 첫 책임 경계**는 완료 이력으로 보존한다. r2 완료 당시 소스 해시와 현재 색인을 대조한 추가/삭제/변경은 **각각 0개**다. 새 후보는 그 완료 범위 밖의 책임이다.
- 중복 탐색의 30줄 창 5개는 이미 공통 클라이언트에 위임하는 두 Kakao 호환 facade의 겹침이다. 새 중복 문제 5건으로 세지 않는다.
- 전체 파일 목록·구조 탐색 뒤 서버·역할 앱·공통의 후보 및 관련 시험 본문을 직접 대조했다. **모든 소스의 모든 메서드·모든 화면/생명주기를 의미·실행 전수 검증한 것은 아니다.**
- 이번에는 build/test·DB/API·모델·APK/기기·운영 통신을 실행하지 않았다. 이전 Task 13,967개 통과는 2026-10-20 r2 증거이며 이번 조사 결과로 재사용하지 않는다.

기준선 HEAD: `fd55a1c4bca4dffb4752ac34de64c62d07caced3`, branch: `dev/mirror-integration`. 기존 dirty tree는 보존한다. 파일 해시·선정 근거·시험 경로와 보존 조건은 [14개 작업 명세](follow-up-work-order.r3.json)에 기록했다.

## 다음 순서

P1/P2/P3는 리팩토링 순서이며 운영 장애의 심각도 판정이 아니다. 첫 두 개의 작은 책임부터 닫고, 상태·금전·저장 순서가 중요한 다음 경계를 하나씩 진행한다.

| 순서 | 구간 | 대상 | 명세 ID |
| --- | --- | --- | --- |
| 1 | P1 | 공통 글쓰기: 첨부파일 선택·업로드 | `UI-R3-02` |
| 2 | P1 | 공통 푸시: 설치 HTTP 전송 | `SHARED-R3-01` |
| 3 | P1 | 마트 기사: 미확정 명령·결과 확인 | `UI-R3-01` |
| 4 | P1 | 정산: 주간 명세 계산·묶음 계획 | `SERVER-R3-02` |
| 5 | P1 | 입고: 완료 품목·재고 이력 구성 | `SERVER-R3-01` |
| 6 | P2 | 화물 수락: 확정 후 후속 처리 | `SERVER-R3-03` |
| 7 | P2 | 음식 배달: 수행 시도 구성 | `SERVER-R3-04` |
| 8 | P2 | 음식점: 수신함 페이지 수집 | `UI-R3-03` |
| 9 | P2 | 통합 지도: 레이어 설명 모델 조립 | `UI-R3-04` |
| 10 | P2 | AI 검토: 판단 사례 요청 구성 | `ROOT-AI-01` |
| 11 | P3 | 공동구매: Mongo 조회 응답 구성 | `SERVER-R3-05` |
| 12 | P3 | 회원 앱: Android 알림 표시 | `SHARED-R3-02` |
| 13 | P3 | 기사 지도: 위치 신선도 타이머 | `UI-R3-05` |
| 14 | P3 | 배포 검사: 압축물 해석 | `SHARED-R3-03` |

## 항목별 범위와 보존 조건

### 1. 공통 글쓰기: 첨부파일 선택·업로드 — UI-R3-02

게시·초안 수명과 미디어 실패 규칙을 분리한다. 공통 소비 범위가 넓고 기존 시험으로 작은 경계를 먼저 고정할 수 있다.

첫 경계: 내부 첨부 선택 policy와 순차 uploader를 분리한다. SelectedFiles snapshot은 게시 저장 await와 null 응답 확인 뒤에 만들고, 업로드마다 현행 Draft.Password를 읽는 delegate 또는 업로드 delegate를 전달한다. 앞서 잡은 submissionPassword는 반환 결과용이며 업로드 비밀번호 snapshot으로 대체하지 않는다. 파일별 시도·성공·실패 결과만 반환하고 게시/초안/닫기 수명은 facade에 남긴다.

유지할 조율: 기존 Composer public facade·SelectedFiles 목록·PropertyChanged·IsSaving·상태 문구·Create/Update/Schedule 선택과 ClearLocalDraftAsync/Reset/닫기 순서는 유지한다. 초안 gate/generation과 비밀번호 보관 정책을 함께 뜯지 않는다.

본문 근거:

- [CommunityPostComposerViewModel.cs](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/CommunityPostComposerViewModel.cs) 34–45줄: browser 미디어의 형식·수량·크기 규칙이 글쓰기/예약/초안 조립 VM이 소유한다.
- [CommunityPostComposerViewModel.cs](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/CommunityPostComposerViewModel.cs) 226–268줄: 선택 파일의 필터·원순서·5개 상한과 UI 경고/알림을 동시에 관리한다.
- [CommunityPostComposerViewModel.cs](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/CommunityPostComposerViewModel.cs) 414–525줄: 글 생성/수정/예약 이후 파일별 업로드·취소 시 나머지 실패 집계·초안삭제·성공경고까지 한 본문이다.

특히 보존할 조건:

- JPG/PNG/WebP/GIF/MP4/WebM 대소문자 비교, 이미지 5MB·동영상 15MB, 원순서 선착순 5개
- 형식/크기 탈락과 개수 상한을 합친 기존 rejectedCount·문구
- 게시글 저장 성공 후 파일 실패는 Succeeded=true 경고이며 글을 다시 POST하지 않음
- 업로드 취소 때 현재 파일부터 나머지 이름 모두 실패 집계 후 loop 중지
- 개별 일반 예외는 해당 파일만 실패 집계하고 다음 파일은 이어서 시도
- 업로드 후 CancellationToken.None으로 로컬 초안을 삭제하고 Reset·닫기 수행
- 업로드 호출에는 현행 Draft.Password 사용; 반환 SubmissionPassword와 혼동 금지
- 실패 파일 원순서·중복 수와 표시용 Distinct(StringComparer.OrdinalIgnoreCase)의 차이 유지
- 글 저장 전 인증/403/validation/예약 실패의 기존 결과·초안 보존 유지
- SelectedFiles.ToArray는 게시 저장 await 및 saved null 확인 뒤(472줄), 각 업로드의 Draft.Password 읽기는 482줄의 현행 시점을 보존한다.

검증 공백: 복수 파일의 중간 일반 실패·중간 취소·중복 파일명 결과와 upload→draft clear→Reset event 순서는 추출 직전에 동작 표로 고정할 가치가 있다. 현재 시험 본문은 확인했으며 이번 조사에서 실행하지 않았다.

기존 회귀 참조(이번 실행 안 함): [CommunityPostComposerViewModelTests.cs](../../../../../Ssalddel.Tests/Ui/Common/CommunityPostComposerViewModelTests.cs) 183줄 · [CommunityPostComposerViewModelTests.cs](../../../../../Ssalddel.Tests/Ui/Common/CommunityPostComposerViewModelTests.cs) 205줄.

### 2. 공통 푸시: 설치 HTTP 전송 — SHARED-R3-01

같은 HTTP 계약만 공유하고 역할별 계정·철회 정책은 유지한다. 전체 알림 상태머신 공통화보다 범위가 작다.

첫 경계: 기존 Ssalddel.Client.Infrastructure/Notifications 안에 작은 concrete installation HTTP helper를 두고 명시적 DTO·installation ID·현재 access token·CancellationToken을 받아 PUT/DELETE 요청 한 번만 수행하게 한다. 먼저 원본 요청/응답 소유·예외 전파 의미를 고정하고 두 facade의 요청 생성/단일 전송만 위임한다. 새 assembly·범용 authenticated client·공통 등록 상태머신은 만들지 않는다.

유지할 조율: SsalddelMobilePushInstallationClient의 두 public 생성자, EnsureRegisteredAsync, DeactivateCurrentOwnerAsync / FDriverPushRegistrationService의 공개 메서드와 InstallationForOwner / 세션 restore/refresh·역할 확인·gate·owner/revision 재확인·로컬 저장 순서는 각 기존 facade

본문 근거:

- [SsalddelMobilePushInstallationClient.cs](../../../../../Ssalddel.Client.Infrastructure/Notifications/SsalddelMobilePushInstallationClient.cs) 64–101줄: 토큰 공급자 조회, owner/revision 검사, 토큰 없음에 따른 철회, RevocationPending 복구를 조율한다.
- [SsalddelMobilePushInstallationClient.cs](../../../../../Ssalddel.Client.Infrastructure/Notifications/SsalddelMobilePushInstallationClient.cs) 103–137줄: 동일 endpoint와 upsert DTO/Authorization를 직접 조립한 직후 토큰 회전 CAS 및 등록 완료 로컬 상태를 갱신한다.
- [SsalddelMobilePushInstallationClient.cs](../../../../../Ssalddel.Client.Infrastructure/Notifications/SsalddelMobilePushInstallationClient.cs) 163–194줄: 철회대기/계정 알림 설정을 먼저 저장하고 DELETE·404 수용·현재 계정 재검사 후 등록 상태를 지운다.

특히 보존할 조건:

- api/v1/mobile/push/installations와 DTO, AppKey, platform 대소문자, AppVersion/DeviceModel의 현재 선택적 포함을 그대로 유지
- FDriver InstallationForOwner(deviceId,owner)와 회원 DeviceInstallationId를 합치지 않음
- DELETE 404는 기존 성공 의미, PUT 상태/timeout/취소/예외 및 오류 문구는 facade별 현재 의미 유지
- RevocationPending 저장 → DELETE 확인 → 등록 상태 삭제/PUT 재등록 순서 및 gate를 유지
- 회원은 OS 권한 철회 시 같은 계정의 소리/진동 선택을 보존하고 명시적 로그아웃은 정리; 음식기사는 권한 OFF가 서버 등록 실패가 되지 않는 현재 차이 유지
- 계정 변경·revision·토큰 회전의 늦은 응답 차단, credentials 확보/refresh 위치 유지
- 새 자동 재전송·새 실패 복구·새 계정/보안 정책을 추가하지 않음
- PUT·공통 DELETE는 현재 caller의 로컬 저장까지 응답 수명을 유지한다. FDriver DELETE는 기존 DeleteAsync 내부 해제→bool 반환→계정 재확인→로컬 저장 순서를 유지한다. 새 wire helper는 각 호출자가 현행 위치에서 응답을 해제할 수 있게 반환한다.
- 회원 DTO의 AppVersion/DeviceModel 선택값과 음식 기사 DTO의 현행 기본값 차이를 보존한다.

검증 공백: 기존 fake HTTP/state 시험 경로를 확인했을 뿐 실행하지 않았다. 실제 SecureStorage·FCM·네트워크/기기 등록은 별도다. 추출 시 요청 bytes/headers/status/disposal 순서를 두 facade별로 대조해야 한다.

기존 회귀 참조(이번 실행 안 함): [SsalddelMobilePushInstallationClientTests.cs](../../../../../Ssalddel.Tests/ClientInfrastructure/SsalddelMobilePushInstallationClientTests.cs) 25줄 · [MemberDeliveryPushTests.cs](../../../../../Ssalddel.Tests/ClientInfrastructure/MemberDeliveryPushTests.cs) 148줄.

### 3. 마트 기사: 미확정 명령·결과 확인 — UI-R3-01

화면 조립과 오래 유지되는 보류 상태의 소유자를 나눈다. 깊은 기존 회귀가 있지만 계정·늦은 응답 위험 때문에 앞 두 경계 뒤에 단독 진행한다.

첫 경계: 마트 전용 PendingCommandCoordinator 한 개에 pending·retryVerified 및 저장/명령/결과확인의 현재 흐름을 옮긴다. owner·active·token 비교는 명시적 수명 context로 전달하고 현재 시점마다 확인한다. 목록/원장 적용은 좁은 callback 또는 결과 DTO로 돌려준다. 음식 PendingCoordinator를 재활용하기 위해 전문 오류/권한 규칙을 공통화하지 않는다.

유지할 조율: 기존 살뜰마트배송PageViewModel public/RelayCommand facade·Items·SelectedItem·PickupCode·ByNumber·문구·CanExecute 및 화면 활성/해제 진입은 유지한다. 이후 목록 ReadSession이나 표현 Projector를 한 번에 새로 만들지 않는다.

본문 근거:

- [살뜰마트배송PageViewModel.cs](../../../../../FDriverApp/PageModels/살뜰마트배송PageViewModel.cs) 54–80줄: 계정/화면 활성 수명·취소·이전 소유자 pending 삭제가 화면 속성과 함께 존재한다.
- [살뜰마트배송PageViewModel.cs](../../../../../FDriverApp/PageModels/살뜰마트배송PageViewModel.cs) 85–120줄: 보호 pending 복원과 목록 조회/표시 반영, 이어지는 복구를 함께 수행한다.
- [살뜰마트배송PageViewModel.cs](../../../../../FDriverApp/PageModels/살뜰마트배송PageViewModel.cs) 145–191줄: 저장 후 전송, 네 종류 요청 조립, 확정 거절의 내구 저장, 현재 원장 재조회와 표시 오류를 함께 조율한다.

특히 보존할 조건:

- 보호 저장 실패 때 POST 0회; 저장 후 현재 owner 검사와 필요 삭제 위치
- 원문 PIN/코드는 메모리에서만 쓰고 requestId 결합 SHA만 저장; 저장 직후 PickupCode 초기화
- 불확실 POST 자동 재전송 0회; 복원과 결과 확인은 조회만; 명시 재시도에 같은 requestId/revision/round 사용
- 결과 404를 미접수 성공으로 간주하지 않고 현재 offer의 기대 조건을 대조
- 거절 뒤 다른 기사 업무 GET을 하지 않고 본인 결과 영수증+목록만 확인
- 확정 거절 사실의 재시작 보존, 후속 현재 원장 확인 전 pending 삭제 금지
- 계정 변경·명시 logout·권한실패·화면 비활성·늦은 응답의 기존 차이
- 동일 offer·목록 고유성·revision 역행 검사와 예외/Status 문구
- 전달 완료와 수령확인·정산·지급의 구분, feature Disabled/Empty 구분

검증 공백: 기존 fake API/store 회귀가 깊다. 추출 전후 동일 call trace·상태 event 순서 및 owner/token 전환을 대조해야 한다. 실제 MAUI 보호저장·화면/기기 실행은 별도 미검증이다.

기존 회귀 참조(이번 실행 안 함): [FDriverMartDeliveryWorkspaceTests.cs](../../../../../Ssalddel.Tests/Clients/FDriverMartDeliveryWorkspaceTests.cs) 77줄 · [FDriverMartDeliveryWorkspaceTests.cs](../../../../../Ssalddel.Tests/Clients/FDriverMartDeliveryWorkspaceTests.cs) 85줄.

### 4. 정산: 주간 명세 계산·묶음 계획 — SERVER-R3-02

주간 계산·후보 분류와 실제 DB 진행 저장의 변경 이유가 다르다. 기존 지급 규칙을 고정한 뒤 계산 책임만 추출한다.

첫 경계: typed 승인채무 snapshot으로 항목별 검토 사유와 완료주를 판정하고, 유효 목록의 입력 순서를 보존해 주간 명세 요청/키를 만드는 Planner를 추출한다. WeekStart·주기 검증·예정시각은 같은 계산 책임으로 공유할 수 있다. 각 후보의 검토 기록 호출 위치와 chunk 사이 취소 확인은 facade에 남기며 전체를 선계산해 실패/취소 시점을 옮기지 않는다.

유지할 조율: 기존 활성화/권한·시계·options 읽기 시점, SQL query/order/Take, 기존 명세 재검증, RecordAutomaticReviewAsync의 실행/예외, PrepareCoreAsync/RevalidateCoreAsync transaction·Save·revision·재조회·결과 집계를 소유한다.

본문 근거:

- [배송료정산배치UseCase.Automatic.cs](../../../../../Ssalddel/Application/Admin/Settlement/배송료정산배치UseCase.Automatic.cs) 20–62줄: 이중 활성화 gate·일정 설정 검사·cutoff 취득·기존 배치 재검증과 후보 SQL 정렬/Take를 실행한다.
- [배송료정산배치UseCase.Automatic.cs](../../../../../Ssalddel/Application/Admin/Settlement/배송료정산배치UseCase.Automatic.cs) 64–105줄: 기사/정수금액/완료일 분류와 검토 저장, 기사+완료주 GroupBy, Id 정렬·Chunk(200), SHA256 key·합계 request 구성과 실제 PrepareCoreAsync가 같은 반복문 안에 섞인다.
- [배송료정산배치UseCase.Automatic.cs](../../../../../Ssalddel/Application/Admin/Settlement/배송료정산배치UseCase.Automatic.cs) 110–132줄: SQL 검토 진행표시·DB contention 처리와 한국 수요일 날짜 계산이 함께 있다.

특히 보존할 조건:

- Enabled와 AutomaticPreparationEnabled 및 기본 OFF
- 한국 수요일 자정·정확히 7일·완료일 귀속·수/목 예정시각·휴일 보정 없음
- 기존 batch 우선 재검증→SQL 후보 순서→후보별 invalid 검토 저장→유효 GroupBy/Id sort/200 chunk 순서
- mode|policy|driver|Start:O|ids 문자열·문화권·SHA256 바이트·weekly-auto 접두어
- 새 승인/채무/지급 Outbox/실송금 생성 없음, readiness 조회 순서·첫 보류사유 유지
- 각 시계/옵션 읽기와 취소 확인 위치·DB contention 처리·변경 추적 해제 유지

검증 공백: 주간 키를 원형과 직접 대조하고 후보 중간 취소/실패의 진행 저장 위치를 고정할 필요가 있다. 기존 시험은 실행하지 않았다.

기존 회귀 참조(이번 실행 안 함): [배송료정산배치R73Tests.Automatic.cs](../../../../../Ssalddel.Tests/Application/Admin/Settlement/배송료정산배치R73Tests.Automatic.cs) 29줄 · [배송료정산배치R73Tests.Automatic.cs](../../../../../Ssalddel.Tests/Application/Admin/Settlement/배송료정산배치R73Tests.Automatic.cs) 62줄.

### 5. 입고: 완료 품목·재고 이력 구성 — SERVER-R3-01

품목 구성과 원자 저장을 나눈다. 실제 ID를 얻는 두 저장 사이의 경계가 있어 정산 계산 다음에 별도로 검증한다.

첫 경계: 입고완료 품목 Factory/Policy에 Validate·Matches와 신규 입고상품 구성을 추출한다. inbound snapshot, 요청 품목, 이미 취득한 completedAt을 입력으로 받고 DB/시계/사용자 접근 없이 값을 반환하게 한다. 이력/이동 factory는 실제 ID를 얻은 뒤 호출할 수 있지만 첫 추출에서 두 Save 사이의 위치를 바꾸지 않는다.

유지할 조율: RequireUserId·접근가능입고Query·분할/현재 상태 guard·transaction·기존 상품 조회·tracked 상태 적용·Add·첫 Save·ID 기반 이력/이동 Add·두 번째 Save·commit·응답 반환을 같은 순서로 소유한다.

본문 근거:

- [WarehouseOperationService.cs](../../../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) 572–611줄: 입력 검사·현재 사용자·Serializable·접근 범위·분할 원천 거부·완료 replay·현재 상태 guard를 실행한다.
- [WarehouseOperationService.cs](../../../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) 614–657줄: 별도 변경 이유인 입고 품목/계약 사본과 가용수량·초기 상태를 구성하면서 tracked entity를 Add한다.
- [WarehouseOperationService.cs](../../../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) 659–701줄: 첫 Save로 얻은 실제 item.Id를 사용해 이력·이동을 만들고 두 번째 Save 뒤 commit한다.

특히 보존할 조건:

- 검사→사용자→Serializable→접근조회 순서와 기존 예외 종류·문구
- DistributedSplitInbound/분할계획원천을 일반 완료에서 거부
- 완료 재호출의 상품 multiset 비교·Trim/null 빈값·중복 품목 개수
- completedAt 한 번 취득과 계약 nullable 의미·가용수량 Math.Max
- 상품 Save→실제 ID 이력/이동→Save→commit, 기존 caller 감사/Event 중복 방지

검증 공백: 현재 시험의 존재를 확인했다. 새 경계 승인 후에는 첫 Save와 이력 Save 실패 시 기존 rollback/ID 의미를 특성화할 필요가 있으며 이번 조사에서 실행하지 않았다.

기존 회귀 참조(이번 실행 안 함): [WarehouseOperationDetailTests.cs](../../../../../Ssalddel.Tests/Services/LogisticsProcessing/Warehouse/WarehouseOperationDetailTests.cs) 189줄 · [WarehouseOperationDetailTests.cs](../../../../../Ssalddel.Tests/Services/LogisticsProcessing/Warehouse/WarehouseOperationDetailTests.cs) 212줄.

### 6. 화물 수락: 확정 후 후속 처리 — SERVER-R3-03

수락 확정과 commit 뒤의 관계 기록·추천 재검증·사건 발행을 나눈다. 현재 복구와 오류 전파를 보존하는 좁은 adapter부터 진행한다.

첫 경계: 현재 PublishAfterCommitAsync/ResumeAcceptanceAsync를 순서를 가진 내부 수락후속 실행 객체로 추출하고 좁은 수락 결과 context를 전달한다. 첫 추출에서는 새 EventHandler·Outbox나 retry를 추가하지 않고 기존 CargoAcceptanceRecoveryService를 호출하는 adapter 역할만 옮긴다.

유지할 조율: 모든 수락 권한/자격/요금·근거 재검증·shared lease/gate·PersistAcceptanceAsync·tracking clear·Serializable·Save/commit·성공 결과를 소유하고, 잠금 해제 뒤에만 후속 실행 객체를 호출한다. 기존 본인 재수락은 복구만 재개하고 관계 관찰/사건을 재발행하지 않는 분기를 유지한다.

본문 근거:

- [배차수락CommandHandler.cs](../../../../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs) 95–167줄: 권한·본인 수락·추천 갱신, shared lease/기사 gate, execution-strategy transaction 재시도와 gate 해제 뒤 후속 실행을 조율한다.
- [배차수락CommandHandler.cs](../../../../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs) 359–371줄: 동일 SQL transaction의 현재 배정/복구 의도 저장→연속배차 수락완료→commit이 수락 권위다.
- [배차수락CommandHandler.cs](../../../../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs) 374–434줄: 관계형 복구/비관계형 시간약속·추천 재검증, 친구 후보 관계 관찰 구성, MediatR 사건 발행이 각기 다른 실패 정책으로 함께 있다.

특히 보존할 조건:

- 기능/worker OFF, 음식/화물/회원 배송 격리와 수락 자격/요금 정책
- 추천 갱신→shared lease→기사 gate→저장→gate/lease 해제→후속 순서
- 관계형은 기존 복구, 비관계형은 시간약속→추천 재검증, 그 뒤 관계 관찰→Publish 순서
- 시간약속/추천 재검증의 OperationCanceledException 재전파와 사건 Publish의 모든 Exception 흡수를 각각 유지
- 관계 collector.Add는 현재 별도 catch가 없음; 이번에 성공 강제나 예외 흡수를 추가하지 않음
- 실제 acceptedAt·traceId 취득 시점·재수락 분기와 원래 수락 결과 유지

검증 공백: 단계별 취소와 collector 실패의 현재 서로 다른 전파를 특성화해야 한다. 본문을 읽었으나 이번에 시험 실행/실패 재현은 하지 않았다.

기존 회귀 참조(이번 실행 안 함): [배차수락CommandHandlerTests.cs](../../../../../Ssalddel.Tests/Application/Driver/DispatchAction/배차수락CommandHandlerTests.cs) 40줄 · [배차수락CommandHandlerTests.cs](../../../../../Ssalddel.Tests/Application/Driver/DispatchAction/배차수락CommandHandlerTests.cs) 238줄.

### 7. 음식 배달: 수행 시도 구성 — SERVER-R3-04

신규·유산 시도의 필드 계산과 SQL 조회·저장을 나눈다. 음식 전문 상태를 화물과 합치지 않는다.

첫 경계: 이미 조회된 이전 시도 정보와 order/queue snapshot·sequence·명시적 시각/모드를 받아 신규 또는 유산 음식배달시도를 구성하는 Factory를 추출한다. StableId, 표시준비시각 하한, 유산 픽업시각/표식을 포함하되 DB 조회·시계 취득·Add/Save는 넣지 않는다.

유지할 조율: 기존 SQL 질의와 정렬/Max, 현재 시도 있으면 즉시 반환, 시각 fallback과 개별 DateTime.UtcNow 호출, DB Add, 수락/도착/중단/픽업/전달 권한·현재 상태 검증, 활동/Outbox 예약·transaction·commit 뒤 후속 순서를 소유한다.

본문 근거:

- [FoodDeliveryDriverWorkService.StatePersistence.cs](../../../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.StatePersistence.cs) 79–122줄: 이전 시도 SQL조회와 순번/조리지연 재배차 표시준비시각 10분 하한·경험모드·시도 entity 구성/Add가 결합한다.
- [FoodDeliveryDriverWorkService.StatePersistence.cs](../../../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.StatePersistence.cs) 125–177줄: 현재 시도 SQL조회 뒤 없으면 Max 순번·주문이력/queue 시각 fallback·상태/유산표식 구성과 Add를 수행한다.
- [FoodDeliveryDriverWorkService.StatePersistence.cs](../../../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.StatePersistence.cs) 252–280줄: 순수 SHA256 identity 구성과 실제 Save/commit/rollback 오류 전파가 같은 partial class에 있다.

특히 보존할 조건:

- 주문별 시도순번과 기존 OrderByDescending/Max 경로, 같은 ID SHA256 입력/접두어
- 한 번이라도 조리지연 중단이 있으면 표시준비시각 acceptedAt+10분 하한
- 신규 경험실행모드와 유산 경로 필드 차이, 유산추정여부=true
- 기사배정 이력→queue UpdatedAt→UtcNow fallback, DateTime.SpecifyKind 의미
- CreatedAtUtc/UpdatedAtUtc 개별 UtcNow 호출을 하나의 now로 합치지 않음
- 시도/활동/후속 예약의 동일 transaction, SQL 성공과 월 정산/원장/알림 후속 구분

검증 공백: 현재시도 없는 유산 경로의 필드/다중 시계 fallback 및 조리지연 10분 하한을 원형과 비교할 직접 특성화가 필요하다. 관련 생명주기 시험은 실행하지 않았다.

기존 회귀 참조(이번 실행 안 함): [음식배달정상수직생명주기Tests.cs](../../../../../Ssalddel.Tests/Application/Food/음식배달정상수직생명주기Tests.cs) 245줄 · [음식배달정상수직생명주기Tests.cs](../../../../../Ssalddel.Tests/Application/Food/음식배달정상수직생명주기Tests.cs) 444줄.

### 8. 음식점: 수신함 페이지 수집 — UI-R3-03

페이지 수집의 완결성 판정과 수신함 반영을 나눈다. 부분 결과를 완전한 목록으로 오인하지 않는 경계다.

첫 경계: 내부 InboxSnapshotReader가 page reader delegate·각 await 직후 actor guard callback·token을 받아 수집한 원문 주문과 fullSnapshotCaptured를 반환하도록 추출한다. 생성/확정/가공을 하지 않는다. facade가 현재 actor 캡처→pending 복원→serverInboxGate 획득→Reader→pending resolve/Upsert→CaptureSnapshot→gate finally 해제 순서를 소유한다.

유지할 조율: I음식점주문DeskService와 기존 public 메서드, _serverInboxGate·ProgressSession·Projection의 적용 순서, 전표/알림/권한 조립을 유지한다. Reader를 새 authoritative cache나 일반 retry 서비스로 만들지 않는다.

본문 근거:

- [음식점주문DeskService.cs](../../../../../RestaurantDeskApp/Services/음식점주문DeskService.cs) 32–35줄: InboxProjection·ProgressSession·알림 Coordinator의 기존 완료 경계가 현재 존재한다.
- [음식점주문DeskService.cs](../../../../../RestaurantDeskApp/Services/음식점주문DeskService.cs) 60–108줄: 100개 단위 미처리 목록의 페이지 반복, OrdinalIgnoreCase 병합, TotalCount 및 빈/반복 페이지 종료와 fullSnapshotCaptured 판정이 facade 본문에 남는다.
- [음식점주문DeskService.cs](../../../../../RestaurantDeskApp/Services/음식점주문DeskService.cs) 110–124줄: 모든 페이지 수집 뒤 pending 확인·Upsert 및 완결일 때만 누락 항목 제거를 적용한다.

특히 보존할 조건:

- 미처리 filter, page=1, pageSize=100, 각 응답의 TotalCount로 현재 기대 수 갱신
- OrdinalIgnoreCase 주문번호 병합과 뒤 페이지 중복 overwrite
- 수집 count>=TotalCount일 때만 fullSnapshotCaptured=true
- 빈 페이지·새 주문이 없는 반복 페이지 종료는 부분 결과이며 기존 수신함 누락 삭제 금지
- 후속 페이지 조회 예외 전까지 projection에 부분 적용하지 않음
- 각 API await 직후 동일 actor 확인
- pending Restore와 serverInboxGate WaitAsync의 순서·finally release
- 모든 페이지 수집 뒤 각 주문의 pending resolve→Upsert 순서

검증 공백: 조회 중 actor 변경·gate 대기 취소·실패 페이지 이후 projection 미적용과 API 호출 trace를 추가 비교해야 한다. 현재 시험은 실제 네트워크·프린터 실행 증거가 아니다.

기존 회귀 참조(이번 실행 안 함): [RestaurantDeskResponsibilityBoundaryTests.cs](../../../../../Ssalddel.Tests/Clients/RestaurantDeskResponsibilityBoundaryTests.cs) 13줄 · [RestaurantDeskResponsibilityBoundaryTests.cs](../../../../../Ssalddel.Tests/Clients/RestaurantDeskResponsibilityBoundaryTests.cs) 36줄.

### 9. 통합 지도: 레이어 설명 모델 조립 — UI-R3-04

지도 화면의 다이어그램 표시값 조립만 옮긴다. 이미 분리한 SDK·갱신 세션은 다시 만들지 않는다.

첫 경계: WebApp 내부 CommunityMapLayerDiagramPresentation에 layer·isNightLearning·selectedCountryCode를 명시 입력으로 넘겨 현재 record를 그대로 반환한다. Build/Consolidate/FormatBlockRelation만 추출하며 map marker·ApplySnapshot·딥링크·질문·동적 이미지·navigation은 이번 후보에서 제외한다. 다른 앱 사용 근거 없이 Ui.Common으로 올리지 않는다.

유지할 조율: Razor의 동일 markup·ActiveLayerDiagrams property·선택/접기 상태·Query 파라미터·버튼/접근성·render 호출은 유지하고 기존 helper 이름 facade를 얇게 남길 수 있다.

본문 근거:

- [CommunityRoleHomePage.razor](../../../../../Ssalddel.WebApp/Pages/CommunityRoleHomePage.razor) 1233–1255줄: polling·조회·질문작성·SDK 책임은 이미 이전 구현의 별도 객체에 위임한다.
- [CommunityRoleHomePage.razor](../../../../../Ssalddel.WebApp/Pages/CommunityRoleHomePage.razor) 1846–1987줄: 데이터셋·국가·레이어에 따른 template 선택과 block/relation·공개자료 설명 문구를 Razor 내부에서 생성한다.
- [CommunityRoleHomePage.razor](../../../../../Ssalddel.WebApp/Pages/CommunityRoleHomePage.razor) 1989–2017줄: template key 기준 그룹화·레이어명 병합과 관계 라벨 조립이 route/selection/SDK 갱신 코드와 같은 owner에 있다.

특히 보존할 조건:

- 낮/밤·NewsPublisher 분기 순서와 기존 공개자료 안내 문구
- 한국/해외 도매시장·공공가격의 template 선택 및 layer.LedgerTemplateKey 우선
- LedgerBlocks 원순서와 다음 block에 대한 첫 관계 찾기
- 관계 Required·RelationType의 정확한 라벨
- BestLedgerPatternSummary 빈값 fallback
- TemplateKey null일 때 layer별 그룹, 그 외 ledger별 그룹 및 Ordinal 비교
- 그룹 first 모델과 레이어명 Distinct 원순서, '+' 결합 문구
- 읽기/참고와 가입·거래·신뢰 보증을 구분하는 현행 경계 문구
- route·query·접기/펼치기·자동 diagram 진입·포커스 복귀를 바꾸지 않음

검증 공백: 구조 문자열 시험만으로 결과 동일성을 입증하지 못한다. 실제 catalog의 레이어/국가/데이터셋 조합과 중복 template 그룹의 record 값 비교가 다음 구현의 좁은 검증 대상이다. 실제 브라우저 렌더는 이번 조사 미실행.

기존 회귀 참조(이번 실행 안 함): [CommunityWorldMapHomeCompositionTests.cs](../../../../../Ssalddel.Tests/Architecture/CommunityWorldMapHomeCompositionTests.cs) 496줄 · [CommunityWorldMapHomeCompositionTests.cs](../../../../../Ssalddel.Tests/Architecture/CommunityWorldMapHomeCompositionTests.cs) 555줄.

### 10. AI 검토: 판단 사례 요청 구성 — ROOT-AI-01

검토용 문구·검증·요청 조립과 저장 포트를 나눈다. 실제 배차나 모델 학습을 바꾸는 작업은 아니다.

첫 경계: 순수 내부 JudgmentCaseRequestFactory로 현재 입력 정규화/검증 및 DispatchAIJudgmentCaseCreateRequest만 옮긴다. public RecordDecisionAsync는 factory→기존 CreateAsync→같은 응답만 조율한다.

유지할 조율: 기존 public RecordDecisionAsync의 factory→CreateAsync 1회→같은 응답 조율을 유지한다. 계약·저장 예외·token/admin 전달도 유지한다.

본문 근거:

- [FoodDeliveryDispatchAIReviewService.cs](../../../../../Ssalddel/Services/Dispatch/Coordination/FoodDeliveryDispatchAIReviewService.cs) 88–88줄: RecordDecisionAsync가 입력 정규화/검증과 문구/키워드/사례 요청을 조립한 뒤 저장 port를 호출
- [FoodDeliveryDispatchAIReviewService.cs](../../../../../Ssalddel/Services/Dispatch/Coordination/FoodDeliveryDispatchAIReviewService.cs) 116–116줄: 수동/AI·승인/보류·주문 묶음·관리자 메모 표시 규칙
- [FoodDeliveryDispatchAIReviewService.cs](../../../../../Ssalddel/Services/Dispatch/Coordination/FoodDeliveryDispatchAIReviewService.cs) 123–123줄: 전문 RAG 사례 payload 생성과 CreateAsync 호출의 결합

특히 보존할 조건:

- ArgumentNullException→주문번호 필터/Trim/Ordinal Distinct→빈 주문 검사→기사 검사 순서
- 주문번호 입력 순서, BundleKey/메모 fallback, null 필드의 현행 예외
- Title/Keywords/SituationSummary/Source/Active와 DecisionType 원문
- adminUser와 cancellationToken을 같은 저장 호출에 전달, 예외를 삼키거나 자동 재시도하지 않음
- CreateAsync 1회와 응답 CaseId/Message, 실제 배차·학습 적용 없음

검증 공백: 실제 저장 없이 수동/AI×승인/보류 전체 payload 고정 / Trim·공백·중복·대소문자·명시/기본 메모·묶음명과 입력 무변경 / 검증 실패에서 저장 0회, token/admin 전달과 저장 실패 전파

기존 회귀 참조(이번 실행 안 함): [FoodDeliveryDispatchAIReviewServiceTests.cs](../../../../../Ssalddel.Tests/Services/Dispatch/Coordination/FoodDeliveryDispatchAIReviewServiceTests.cs) 38줄.

### 11. 공동구매: Mongo 조회 응답 구성 — SERVER-R3-05

저장과 조회 시점 계산은 다른 책임이나 직접 Mongo projection 회귀가 약하다. 현행 응답 비교 자료를 먼저 확보한다.

첫 경계: 먼저 문서의 응답 전용 집단/수요/이벤트/운영상태 변환을 Projector로 추출한다. 호출자가 기존 위치에서 취득한 응답 기준시각과 기존 Engine을 전달하고 저장 재계산/인계 승인/큐·다음 점검 변경은 옮기지 않는다. 공유 문서를 복제하거나 CAS callback 밖에서 미리 계산한 값으로 영속 상태를 바꾸지 않는다.

유지할 조율: Mongo index/collection, 수요 등록/철회/원장 연결 권한과 멱등·CAS, 변경 callback 재실행, 영속 재계산/인계/운영큐·다음 점검 기록, 시각 취득·조회 선택/페이징과 response Projector 호출을 소유한다.

본문 근거:

- [공동구매자동집단화저장소.cs](../../../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) 452–546줄: CAS callback 안 상태/큐/멱등 이력·다음 점검을 기록하고 밖에서 집단/운영 상태 응답을 만든다.
- [공동구매자동집단화저장소.cs](../../../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) 819–878줄: Mongo read→변경 callback 재실행→버전 증가→Insert/Replace CAS와 최대 8회 충돌 재시도를 소유한다.
- [공동구매자동집단화저장소.cs](../../../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) 1025–1066줄: 영속 현재 상태와 집계/event를 변경한다. 조회 projection과 다른 수명이다.

특히 보존할 조건:

- 집단 조회마다 별도 UtcNow 취득과 조회 중 모집 종료 표시가 영속 상태/event를 바꾸지 않는 차이
- 활성수요 내림차순과 사건 내림차순 Take(20), 동률의 기존 안정성
- 기존 문서/Engine/DTO·BSON 명칭과 null/default·Simulation·운영체제/인계 fallback
- CAS callback별 최신 원본 재계산과 최대 8회 재시도·버전·멱등키 보존
- 현재 큐는 저장값 우선·없을 때만 fallback, 9999 점검시각은 응답 null
- 후속 기능/worker 기본 OFF와 사람 인계 승인·실제 1.5 실행 경계

검증 공백: 위 UseCase/ProcessManager 시험은 fake port 중심이고 실제 Mongo 문서 projection을 직접 검증하는 시험으로 간주할 수 없다. 현행 문서/시각/Engine 입력으로 원형 DTO와 동일한 결과를 고정한 뒤 추출할 필요가 있다. 이번에는 실행하지 않았다.

기존 회귀 참조(이번 실행 안 함): [공동구매자동집단화UseCaseTests.cs](../../../../../Ssalddel.Tests/Services/Orderer/공동구매자동집단화UseCaseTests.cs) 11줄 · [공동구매수요모집ProcessManagerTests.cs](../../../../../Ssalddel.Tests/Services/Orderer/공동구매수요모집ProcessManagerTests.cs) 43줄.

### 12. 회원 앱: Android 알림 표시 — SHARED-R3-02

계정·동의 조율과 OS 표시를 나눈다. 기존 fake 시험으로 native 표시를 입증할 수 없어 기기 검증 여건과 함께 진행한다.

첫 경계: 같은 SsalddelApp에 concrete MemberDeliveryNotificationPlatform adapter 하나를 두고 NotificationsAllowed·권한 요청·Publish·Cancel의 현재 native 본문을 옮긴다. Publish의 동기 bool 반환은 Coordinator의 현재 store.UpdateAsync 잠금 안 호출 위치를 유지한다. 계정/동의 HTTP·Coordinator·설치등록·Changed는 현재 MemberDeliveryPushService가 조율한다. 타 역할 앱의 문구/기한/receiver를 합치거나 공통 알림 프레임워크/assembly를 추가하지 않는다.

유지할 조율: MemberDeliveryPushService의 공개 메서드·Changed와 OpenAction/ChannelPrefix 상수 / MauiMemberPushTokenProvider가 사용하는 정적 NotificationsAllowed는 호환 위임을 유지 / MainActivity·Firebase callback의 현재 진입점과 시점 / MemberDeliveryPushCoordinator 및 NotificationAlertPolicy의 현재 권위

본문 근거:

- [MemberDeliveryPushService.cs](../../../../../SsalddelApp/Services/MemberDeliveryPushService.cs) 24–91줄: 공통 Coordinator 구성, 세션 변경 구독, 동의·설치 등록/복귀 수명과 사용자 Changed를 소유한다.
- [MemberDeliveryPushService.cs](../../../../../SsalddelApp/Services/MemberDeliveryPushService.cs) 93–135줄: 참여 동의 GET와 account/revision/PreferencesCheckedAtUtc 보호, 계정 변경 정리를 소유한다.
- [MemberDeliveryPushService.cs](../../../../../SsalddelApp/Services/MemberDeliveryPushService.cs) 40–49줄: Android CurrentActivity 권한 요청을 같은 서비스가 직접 실행한다.

특히 보존할 조건:

- OpenAction, ChannelPrefix, 알림 ID 42091, notificationId tag, PendingIntent requestCode/data URI/extras/Immutable|UpdateCurrent/SingleTop|ClearTop 유지
- API 26/33 분기, 기존 채널 mute/block·소리/진동 사용자 선택, PreserveLegacy 호출 시점과 채널 생성 순서 유지
- ssalddel_member_delivery 소리·아이콘·제목/문구·Private visibility·timeout 값과 단위 유지
- Publish는 native Notify 실패/권한 거부를 false로 반환하고 성공을 동기 확인한 현재 bool 의미 유지
- Cancel은 42091 알림만 취소; 계정 변경/동의 OFF 취소의 기존 순서와 SecurityException 처리 유지
- 회원 동의/PreferencesCheckedAtUtc·owner/revision late-response guard 및 계정 상태 정리 의미 유지
- Android가 아닌 target의 false/no-op 의미 유지; 새 권한 요청·알림·서버 호출을 추가하지 않음

검증 공백: 검색한 기존 시험에는 MemberDeliveryPushService의 실제 Android NotificationManager/Intent/channel API를 직접 실행하는 검사가 없다. 따라서 Coordinator/Policy 시험 통과를 native adapter/기기 완료로 볼 수 없다. 추출 전후 본문·상수·분기/호출 순서 대조와 소비 Android 컴파일, 별도 실제 기기 알림/클릭/권한 검증을 구분해야 한다. 이번 조사에서는 아무 시험/빌드/기기도 실행하지 않았다.

기존 회귀 참조(이번 실행 안 함): [MemberDeliveryPushTests.cs](../../../../../Ssalddel.Tests/ClientInfrastructure/MemberDeliveryPushTests.cs) 51줄 · [MemberDeliveryPushTests.cs](../../../../../Ssalddel.Tests/ClientInfrastructure/MemberDeliveryPushTests.cs) 84줄.

### 13. 기사 지도: 위치 신선도 타이머 — UI-R3-05

타이머 구독 수명만 옮긴다. 이미 분리한 지도 제공자·compass 소유권을 건드릴 이익보다 실행 위험이 크므로 후순위다.

첫 경계: 현재 Dispatcher에서 timer를 만드는 내부 freshness pump에 Start/Stop·Tick 구독 해제만 옮긴다. Handler는 현재의 시작 guard와 Disconnect 순서, tick callback의 RefreshLocationPresentation→RefreshDirectionPresentation을 소유한다. pump가 provider를 고르거나 Compass·지도 상태·계정 상태를 소유하지 않게 한다.

유지할 조율: Mapper·Handler·provider surface·_mapLifecycleGeneration·위치 policy와 direction sensor 구현은 유지한다. 세 메서드의 얇은 facade를 남겨 existing call sites/해제 순서를 보존한다.

본문 근거:

- [DriverNativeMapViewHandler.Android.cs](../../../../../DriverApp/Handlers/DriverNativeMapViewHandler.Android.cs) 55–76줄: provider resource 해제와 timer/compass 중단 순서를 Handler가 조립한다.
- [DriverNativeMapViewHandler.Android.cs](../../../../../DriverApp/Handlers/DriverNativeMapViewHandler.Android.cs) 201–237줄: SDK 자원 surface 분리 뒤에도 1초 dispatcher timer 생성·구독·중단과 freshness tick 수명이 Handler에 남는다.
- [DriverNativeMapViewHandler.방향.Android.cs](../../../../../DriverApp/Handlers/DriverNativeMapViewHandler.방향.Android.cs) 25–35줄: 같은 tick이 위치 불변 때에도 방향 freshness를 갱신해야 한다. 방향 sensor 자체는 이 최소 후보의 추출 범위가 아니다.

특히 보존할 조건:

- timer interval=1초, 기존 Dispatcher 사용과 timer 중복 생성 방지
- CurrentLocation null 또는 양쪽 map null일 때 시작하지 않는 기존 guard
- 매 tick 위치→방향 순서, 위치가 동일해도 방향 freshness 평가
- Stop→Tick 구독해제→참조 null의 현재 순서
- Disconnect의 generation 증가→compass 중단→timer 중단→overlay→view/listener 해제 순서
- provider 전환만으로 비활성 view 파괴/전역 generation 변경을 추가하지 않음
- ready callback이 숨긴 provider여도 현행 generation이면 수용하는 현재 의미
- 다른 화면이 시작한 compass를 중단하지 않는 공유 소유권·예외 처리를 변경하지 않음

검증 공백: timer 생성/Start/Stop/dispose callback seam의 동일 호출 trace를 고정할 수 있다. 실기기의 map/GPS/provider 전환·센서 권한·늦은 callback은 별도 실행 검증이 필요하며 이번 조사에서 하지 않는다.

기존 회귀 참조(이번 실행 안 함): [DriverNativeMapSurfaceCompositionTests.cs](../../../../../Ssalddel.Tests/Architecture/DriverNativeMapSurfaceCompositionTests.cs) 67줄 · [DriverNativeMapSurfaceCompositionTests.cs](../../../../../Ssalddel.Tests/Architecture/DriverNativeMapSurfaceCompositionTests.cs) 38줄.

### 14. 배포 검사: 압축물 해석 — SHARED-R3-03

압축 스트림과 배포 조건 판정을 나눈다. 운영 앱의 직접 책임 다음에 도구 입력·출력 계약을 보존하며 진행한다.

첫 경계: 같은 eng/staging의 작은 IntegratedMobileArchiveInspection.ps1로 ReadWeb·ReadImage와 필요한 경로/entry/metadata 검사 함수 묶음을 옮겨 기존 entry에서 같은 scope로 불러온다. entry는 params·실행 순서·프로필/manifest/hash·반환 schema를 소유한다. bundle 파일 경로 검사도 현재 PrivatePath 의미를 계속 사용한다. 새 module/package·공통 배포 framework·새 탐지 규칙을 만들지 않는다.

유지할 조율: Test-IntegratedMobileFieldPackage.ps1의 인자/default·AsObject 또는 JSON 반환 / deploy/azure-vm/package-mobile-field-test.ps1의 기존 검사 호출 경로 / profile 검사 → bundle/manifest/hash 검사 → ReadWeb → ReadImage → 결과 조립 순서 / 기존 failure 문구와 결과 schemaVersion/status/inspectedImageLayers 및 false 범위 주장

본문 근거:

- [Test-IntegratedMobileFieldPackage.ps1](../../../../../eng/staging/Test-IntegratedMobileFieldPackage.ps1) 16–66줄: entry 경로/크기 및 metadata 문자열 검사를 같은 entry script에 둔다.
- [Test-IntegratedMobileFieldPackage.ps1](../../../../../eng/staging/Test-IntegratedMobileFieldPackage.ps1) 67–89줄: gzip/tar stream 수명과 Web publish payload/production same-origin 형식을 검사한다.
- [Test-IntegratedMobileFieldPackage.ps1](../../../../../eng/staging/Test-IntegratedMobileFieldPackage.ps1) 90–142줄: docker-save manifest를 두 번 순회하고 metadata와 plain/gzip 중첩 layer를 1GiB 한도 안에서 검사하며 stream을 해제한다.

특히 보존할 조건:

- 추출은 disk 해제나 Docker/DB/네트워크 실행 없이 같은 stream 검사만 수행
- entry text 10MiB와 layer 1GiB bound, plain/gzip 감지, 두 번 manifest/config/layer 순회와 Dispose 순서 유지
- 원형 tag/RepoTags count·declared config exactly once·모든 layer 검사 및 기존 CA 허용/앱 link/경로/metadata 판정 그대로 유지
- 나중 layer에서 지워진 원래 layer의 현행 검사와 원형 오류 순서 유지
- Simulation/feature/worker/금전/DB/volume/storage·Caddy·hash/file set 판정, profile 기본값을 구조 변경과 함께 바꾸지 않음
- schemaVersion integrated-mobile-field-package-check.r1, LocalProfileChecked/LocalArchiveAndProfileChecked, networkCalls/filesWritten/운영·기기·push false 의미 유지
- 현재 두 대상 스크립트는 기존 dirty tree의 untracked 파일이다. 구현 착수 시 소유자·현행 SHA를 다시 확인하고 기존 작업을 대체/삭제하지 않음

검증 공백: 기존 synthetic fixture script의 실제 경로·Check 이름을 읽었을 뿐 실행하지 않았다. 새 파일이 tool/source fingerprint를 소유하는 caller에 빠지지 않는지도 구현 시 확인해야 한다. offline 검사는 실제 VM 배포/rollback/DB/푸시 수신 성공을 대신하지 않는다.

기존 회귀 참조(이번 실행 안 함): [Test-IntegratedMobileFieldPackage.Tests.ps1](../../../../../eng/staging/Test-IntegratedMobileFieldPackage.Tests.ps1) 76줄 · [Test-IntegratedMobileFieldPackage.Tests.ps1](../../../../../eng/staging/Test-IntegratedMobileFieldPackage.Tests.ps1) 87줄.

## 이번 후보에서 제외하거나 늦출 것

- r1/r2에서 끝낸 토큰 저장, 음식 기사 메인 세션, 음식점 projection/진행·알림 세션, 공통 pending 저장, DI/CLI, Kakao HTTP, SDK 제공자 surface는 재구현하지 않는다.
- 선언형 `CommunityLedgerTemplateCatalog`와 준수 카탈로그는 길이만으로 위반이라 판단하지 않는다. DI 조립 진입점도 조립 책임을 가진다는 이유만으로 분해하지 않는다.
- 개발용 `InMemoryShipperStore`의 자료량과 Unity 전시 projection은 현행 운영 책임 정리보다 우선하지 않는다.
- 새 공통 복구 엔진, 플랫폼별 알림 규칙 통합, 배차/요금/주간 정산 정책 변경, 자동 재전송·새 Outbox·새 retry, 새로운 권한은 별도 기능 변경이다.
- r1에서 확인한 요금 오류 시 원복, 재위탁 SQL 성공 뒤 Mongo replay, 늦은 응답 보호 등 동작 개선 후보는 기존 기록에 보존한다. 구조 이동과 섞지 않는다.

## 이후 구현할 때의 닫는 순서

1. 대상 파일과 dirty 소유를 다시 확인하고 현행 입력/출력·호출 순서·실패/취소 위치를 고정한다. 기능 변경이 필요한 발견은 별도 목록에 남긴다.
2. 현재 프로젝트 안에서 한 책임만 추출하고 기존 facade는 조율을 유지한다. 단순 partial 파일 이동이나 공통 framework 추가를 완료 기준으로 삼지 않는다.
3. 해당 항목의 기존 회귀와 명시한 공백을 비교 검증하고 소비 프로젝트를 빌드한다. 공통 사용자는 함께 확인하되 UI와 업무 정책은 유지한다.
4. 소스 결속 검사·코드 지도·도구 fingerprint가 있다면 공식 생성 경로로 새 책임을 포함한다. 기존 검사를 지우거나 약화해 통과시키지 않는다.
5. 로컬 회귀, Android 컴파일, 실제 화면/기기, 외부 푸시·운영·실제 정산 증거를 나눠 기록한다. 이번 제안은 구현/활성화 승인이나 실운영 검증 완료가 아니다.

## 조사 원자료와 정적 검증

- [structural-summary.json](../../../../../artifacts/local/refactoring-survey/20261021-r3/structural-summary.json)
- [delta-from-r2-completed.json](../../../../../artifacts/local/refactoring-survey/20261021-r3/delta-from-r2-completed.json)
- [ui-audit.json](../../../../../artifacts/local/refactoring-survey/20261021-r3/ui-audit.json)
- [server-audit.json](../../../../../artifacts/local/refactoring-survey/20261021-r3/server-audit.json)
- [shared-audit.json](../../../../../artifacts/local/refactoring-survey/20261021-r3/shared-audit.json)
- [root-audit.json](../../../../../artifacts/local/refactoring-survey/20261021-r3/root-audit.json)
- [proposal-validation.json](../../../../../artifacts/local/refactoring-survey/20261021-r3/proposal-validation.json)

원자료는 로컬 산출물이며 Git에 포함되는 문서와 구분한다. 항목별 소스 해시·핵심 줄·회귀 경로는 작업 명세에도 남긴다. 최종 정적 검증은 소스/프로젝트 해시 무변경, 과거 계획/구현 기록 보존, JSON 및 참조 경로·줄, 새 보고서와 이번에 추가한 목차 링크, 문서 diff를 확인한다. 기존 목차 전체의 링크 건전성이나 제품 시험 실행을 검증한 것은 아니다.
