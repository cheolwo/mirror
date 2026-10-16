# 음식 기사 업무 코드 탐색 트리

> 이 문서는 `SsalddelCodeMetadataAttribute`와 `eng/work-areas/food-driver.json`에서 자동 생성된다. 직접 수정하지 않는다.

소스 결속은 검토한 파일·심볼의 탐색 연결이다. reflection 단계와 분리하며 실제 호출·실행 성공이나 운영 검증을 인증하지 않는다. MAUI 앱 assembly를 로드하지 않는다.

```text
음식 기사 업무
└─ 음식 기사 운행·현재 업무·수행·정산 경계 [food-driver-work]
   ├─ 010 contract.work-start-request · Contract · Definition
   ├─ 011 contract.work-start-response · Contract · Definition
   ├─ 012 contract.work-status · Contract · Definition
   ├─ 013 contract.offer · Contract · Definition
   ├─ 014 contract.active-delivery · Contract · Definition
   ├─ 015 contract.current-work · Contract · Definition
   ├─ 016 contract.legacy-workspace · Contract · Definition
   ├─ 017 contract.driver-followup-intent · Contract · Definition
   ├─ 018 contract.driver-followup-port · Contract · Definition
   ├─ 019 contract.work-start-followup-port · Contract · Definition
   ├─ 020 contract.food-workspace-port · Contract · Definition
   ├─ 030 domain.driver-shift · Domain · Definition
   ├─ 031 domain.work-start-followup · Domain · Definition
   ├─ 040 infrastructure.shift-mapping · Infrastructure · Definition
   ├─ 041 domain.straight-line-delivery-pricing · Domain · Definition
   ├─ 041 infrastructure.work-start-followup-mapping · Infrastructure · Definition
   ├─ 050 application.work-transition-lock · Application · Confirm
   ├─ 052 application.driver-action-policy · Application · Preview
   ├─ 053 application.current-bundle-policy · Application · Preview
   ├─ 053 application.driver-offer-evidence · Application · Projection
   ├─ 054 application.current-work-projector · Application · Projection
   ├─ 055 application.driver-offer-query · Application · Query
   ├─ 060 application.shared-driver-admission · Application · Confirm
   ├─ 070 application.work-start · Application · Confirm
   ├─ 075 application.work-status · Application · Query
   ├─ 080 application.work-stop · Application · Confirm
   ├─ 085 application.work-location · Application · Projection
   ├─ 110 application.current-work · Application · Query
   ├─ 115 application.legacy-workspace · Application · Query
   ├─ 120 application.driver-actions · Application · Confirm
   ├─ 125 application.driver-state-persistence · Application · Persistence
   ├─ 130 application.daily-settlement · Application · Query
   ├─ 135 application.completed-detail · Application · Query
   ├─ 140 application.driver-followups · Application · Projection
   ├─ 150 infrastructure.work-start-followup · Infrastructure · Projection
   ├─ 160 infrastructure.driver-followup-outbox · Infrastructure · Persistence
   ├─ 170 infrastructure.work-start-worker · Infrastructure · Tick
   ├─ 180 api.food-driver · Api · Confirm
   ├─ 190 contract.workspace-port · Contract · Definition
   ├─ 191 contract.recovery-port · Contract · Definition
   ├─ 200 ui.recovery-policy · Domain · Definition
   ├─ 203 ui.food-presentation · ViewModel · Presentation
   ├─ 205 ui.stage-presentation · ViewModel · Presentation
   ├─ 206 ui.workspace-presentation · ViewModel · Presentation
   ├─ 210 ui.food-driver-adapter · ClientAdapter · Confirm
   ├─ 215 ui.food-action-view-model · ViewModel · Confirm
   ├─ 220 ui.workspace-view-model · ViewModel · Presentation
   └─ 230 ui.role-map-view · View · Presentation
     소스 결속 · source.contract.current-work
     소스 결속 · source.contract.work-start-request
     소스 결속 · source.domain.driver-shift
     소스 결속 · source.domain.work-start-followup
     소스 결속 · source.infrastructure.shift-mapping
     소스 결속 · source.infrastructure.work-start-followup-mapping
     소스 결속 · source.ui.role-map-view
     소스 결속 · source.ui.workspace-view-model
     소스 결속 · source.ui.food-driver-adapter
     소스 결속 · source.ui.stage-presentation
     소스 결속 · source.ui.recovery-policy
     소스 결속 · source.ui.workspace-presentation
     소스 결속 · source.ui.food-action-view-model
     소스 결속 · source.ui.food-presentation
     소스 결속 · source.contract.workspace-port
     소스 결속 · source.application.current-bundle-policy
     소스 결속 · source.application.completed-detail
     소스 결속 · source.contract.food-workspace-port
     소스 결속 · source.application.current-work-projector
     소스 결속 · source.application.daily-settlement
     소스 결속 · source.application.work-transition-lock
     소스 결속 · source.application.work-status
     소스 결속 · source.application.work-start
     소스 결속 · source.application.work-stop
     소스 결속 · source.application.work-location
     소스 결속 · source.api.food-driver
     소스 결속 · source.contract.driver-followup-intent
     소스 결속 · source.infrastructure.driver-followup-outbox
     소스 결속 · source.contract.work-start-followup-port
     소스 결속 · source.infrastructure.work-start-worker
     소스 결속 · source.application.driver-actions
     소스 결속 · source.application.driver-followups
     소스 결속 · source.application.driver-action-policy
     소스 결속 · source.application.driver-offer-query
     소스 결속 · source.application.driver-state-persistence
     소스 결속 · native.current-work
     소스 결속 · native.automatic-start
     소스 결속 · native.pending-recovery
     소스 결속 · native.work-screen
     소스 결속 · native.profile-settlement
     소스 결속 · native.foreground-notification
     소스 결속 · source.application.shared-driver-admission
     소스 결속 · source.domain.straight-line-delivery-pricing
     소스 결속 · source.application.driver-offer-evidence
```

기능 하나만 보려면 `dotnet run --project eng/Ssalddel.CodeMap -- --work-area food-driver --feature <기능키>`를 사용한다.

## 음식 기사 운행·현재 업무·수행·정산 경계 (`food-driver-work`)

- **010 contract.work-start-request** — [기사운행시작요청](../../../Ssalddel.Contracts/Driver/Work/기사운행Dtos.cs#L11) · 기사의 운행 시작 입력과 선택적 클라이언트 요청 식별자를 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 시작 요청 ID와 배차 수신 변경 ID는 별개다. 계약 자체는 시작 POST 재전송이나 상태 변경을 실행하지 않는다.
  - 먼저 읽을 단계: None
- **011 contract.work-start-response** — [기사운행시작응답](../../../Ssalddel.Contracts/Driver/Work/기사운행Dtos.cs#L11) · SQL 근무 확정 결과와 인덱스 후속 대기 여부를 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: FollowupPending은 SQL 저장 실패가 아니며 응답만으로 배차 수신 ON이나 외부 후속 완료를 확정하지 않는다.
  - 먼저 읽을 단계: contract.work-start-request
- **012 contract.work-status** — [기사운행상태응답](../../../Ssalddel.Contracts/Driver/Work/기사운행Dtos.cs#L11) · 운행 정본의 근무와 시작 요청 소유권 및 위치 투영을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 운행 상태와 시작 요청 ID는 SQL 기준이고 위치·Aging은 인덱스 투영이다. 구형 근무의 null 요청 ID를 현재 요청으로 추정하지 않는다.
  - 먼저 읽을 단계: None
- **013 contract.offer** — [FoodDeliveryDriverOfferDto](../../../Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs#L15) · 수락 전 음식 배달 제안의 조건과 가능한 행동을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 거리 null과 확인된 0을 구별한다. 제안 금액은 음식 주문 금액이나 기사 실지급 완료를 뜻하지 않는다.
  - 먼저 읽을 단계: None
- **014 contract.active-delivery** — [FoodDeliveryDriverActiveDeliveryDto](../../../Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs#L15) · 현재 기사에게 연결된 배달 시도와 Revision 및 허용 행동을 전달한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 주문·제안·시도 식별자는 서로 대체하지 않는다. 고객 상세는 권한 있는 현재 업무에 한정하며 전달 완료와 수령 확인·실지급은 별개다.
  - 먼저 읽을 단계: None
- **015 contract.current-work** — [FoodDeliveryDriverCurrentWorkDto](../../../Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs#L15) · 지도와 현재 수행 업무의 정산 없는 조회 응답을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 월 정산·주문별 정산·완료 상세를 포함하지 않는 현재 업무 계약이다.
  - 먼저 읽을 단계: contract.active-delivery | contract.offer
- **016 contract.legacy-workspace** — [FoodDeliveryDriverWorkspaceDto](../../../Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs#L15) · 기존 정산 포함 음식 기사 workspace 응답을 보존한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 현재 업무 계약과 호환 정산 조회를 구별한다. 정산 기록이나 모의 지급 성공을 실제 이체로 승격하지 않는다.
  - 먼저 읽을 단계: contract.current-work
- **017 contract.driver-followup-intent** — [음식기사후속의도](../../../Ssalddel/Services/Community/FoodDriverFollowupOutbox.cs#L15) · 음식 기사 상태 변경의 후속 원천 식별자와 사건 정보를 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 고객 주소·연락처 사본을 담지 않는다. 원장 동기화·알림 예약은 실제 발송이나 수령·실지급 완료가 아니다.
  - 먼저 읽을 단계: None
- **018 contract.driver-followup-port** — [I음식기사후속OutboxService](../../../Ssalddel/Services/Community/FoodDriverFollowupOutbox.cs#L15) · 상태 변경과 함께 예약하고 저장 후 처리하는 음식 기사 후속 계약을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 예약은 호출자의 SaveChanges와 transaction으로 확정하며 즉시처리는 별도다. 인터페이스 선언 자체는 저장·발송을 실행하지 않는다.
  - 먼저 읽을 단계: contract.driver-followup-intent
- **019 contract.work-start-followup-port** — [I기사운행시작후속Service](../../../Ssalddel/Services/Dispatch/Queue/기사운행시작후속Service.cs#L21) · 근무 식별자 기준 운행 시작 인덱스 재구성 계약을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 운행 시작 정본을 만들거나 수신 의사를 변경하는 계약이 아니다.
  - 먼저 읽을 단계: contract.work-start-response
- **020 contract.food-workspace-port** — [IFoodDeliveryDriverWorkspaceUseCase](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs#L24) · 현재 업무와 호환 workspace 및 정산·완료 상세 조회 계약을 정의한다.
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 현재 조회와 정산 조회의 호출을 구별하며 조회 계약 자체는 업무 상태를 전이하지 않는다.
  - 먼저 읽을 단계: contract.current-work | contract.legacy-workspace
- **030 domain.driver-shift** — [기사근무](../../../Ssalddel.Domain/기사/DriverShift.cs#L14) · 운행 시작 요청의 멱등성과 현재 근무 슬롯을 담는 SQL 전문 상태를 정의한다.
  - 계층/단계: `Domain / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 음식·화물 공용 근무 모델이며 ActiveDriverId·종료 시각·요청 지문은 서버가 검증한다. 타입 정의 자체가 DB를 저장하지 않는다.
  - 먼저 읽을 단계: contract.work-start-request | contract.work-status
- **031 domain.work-start-followup** — [기사운행시작후속](../../../Ssalddel.Domain/기사/DriverWorkStartFollowup.cs#L12) · SQL 근무와 함께 확정할 인덱스 후속의 재시도 상태를 정의한다.
  - 계층/단계: `Domain / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: ShiftId에 결속된 작업 상태이며 고객 정보나 시작 주소를 복제하지 않는다. SQL 근무 성공과 인덱스 처리 성공은 별도다.
  - 먼저 읽을 단계: domain.driver-shift
- **040 infrastructure.shift-mapping** — [기사근무Configuration](../../../Ssalddel.Infrastructure/Persistence/Configurations/Driver/기사근무Configuration.cs#L17) · 근무 시작 식별자와 활성 기사 슬롯의 SQL 매핑·고유 제약을 정의한다.
  - 계층/단계: `Infrastructure / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: EF 모델 매핑이며 migration 실행이나 기존 근무 값 재작성은 수행하지 않는다. nullable 고유 슬롯과 시작 요청 키의 의미를 보존한다.
  - 주 계약: `살뜰.도메인.기사.기사근무`
  - 먼저 읽을 단계: domain.driver-shift
- **041 domain.straight-line-delivery-pricing** — [StraightLineDeliveryPricingPolicy](../../../Ssalddel.Domain/운송/StraightLineDeliveryPricingPolicy.cs#L8) · 픽업에서 전달까지의 원래 직선거리로 비례 거리비를 계산한다.
  - 계층/단계: `Domain / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 거리 표시를 반올림하기 전 원거리로 거리비만 원 단위 반올림한다. 경로·시간·차량·혼적 안전 검사를 대체하지 않는다.
  - 먼저 읽을 단계: None
- **041 infrastructure.work-start-followup-mapping** — [기사운행시작후속Configuration](../../../Ssalddel.Infrastructure/Persistence/Configurations/Driver/DriverWorkStartFollowupConfiguration.cs#L16) · 근무별 후속 작업의 SQL 키와 재시도 조회 인덱스를 정의한다.
  - 계층/단계: `Infrastructure / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: EF 모델 매핑만 정의한다. 실제 DB 적용과 작업 처리 완료는 별도이며 근무 외래 키 결속을 보존한다.
  - 주 계약: `살뜰.도메인.기사.기사운행시작후속`
  - 먼저 읽을 단계: domain.work-start-followup
- **050 application.work-transition-lock** — [기사운행전이Lock](../../../Ssalddel/Application/Driver/Work/기사운행전이Lock.cs#L16) · 운행 시작·종료·GPS·후속의 기사별 전이 순서를 직렬화한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `PersistentWrite | StateMutation`
  - 경계: 프로세스 잠금과 transaction 내부의 SQL 행 잠금을 함께 사용한다. 동일 값을 유지하는 UPDATE도 DB 쓰기이며 잠금 자체가 업무 성공을 확정하지 않는다.
  - 먼저 읽을 단계: infrastructure.shift-mapping
- **052 application.driver-action-policy** — [음식배달기사업무Service](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Policies.cs#L40) · 이미 조회된 업무에 대한 활성 상한과 묶음 동선 조건을 판정한다.
  - 계층/단계: `Application / Preview`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 이 partial의 판정은 영속 상태를 확정하지 않는다. 실제 권한·현재 Revision 재검사와 SQL 저장은 상태 전이 경계가 맡는다.
  - 먼저 읽을 단계: contract.active-delivery | contract.offer
- **053 application.current-bundle-policy** — [FoodDeliveryCurrentBundlePolicy](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.BundlePolicy.cs#L25) · 현재 제안 자료에서 표시할 묶음 후보를 순수 계산한다.
  - 계층/단계: `Application / Preview`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 후보 생성은 자동 수락·배차 확정·영속 저장이 아니다.
  - 먼저 읽을 단계: contract.offer
- **053 application.driver-offer-evidence** — [기사제안표시근거Service](../../../Ssalddel/Services/Dispatch/Recommendation/기사제안표시근거Service.cs#L13) · 저장한 기사 요금 근거와 실제 분산 피킹 책임을 제안 표시로 읽는다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `OperationalState → None`
  - 부수효과: `PersistentRead`
  - 경계: 조회만 수행한다. 고객 견적·희망 옵션·창고 선행 bool로 보수나 포장 배정을 추론하지 않는다.
  - 먼저 읽을 단계: None
- **054 application.current-work-projector** — [FoodDeliveryCurrentWorkProjector](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CurrentWorkProjection.cs#L25) · 확인된 제안·운송·시도 자료를 현재 업무 응답 값으로 변환한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 이 projection은 입력 사본만 변환한다. DB 조회나 상태 변경·외부 호출을 실행하지 않으며 전달·수령·실지급 상태를 합치지 않는다.
  - 먼저 읽을 단계: contract.active-delivery | contract.offer
- **055 application.driver-offer-query** — [음식배달기사업무Service](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Query.cs#L42) · 현재 기사에게 유효한 제안과 수행 업무를 조회한다.
  - 계층/단계: `Application / Query`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite`
  - 경계: SQL 전문 상태는 조회만 한다. 공통 수신 상태 조회는 영속 원장 기준 Redis 투영을 재구성할 수 있으며 수신 의사 자체를 바꾸지 않는다.
  - 주 계약: `살뜰.Services.Dispatch.Recommendation.I음식배달기사업무Service`
  - 먼저 읽을 단계: contract.active-delivery | contract.offer
- **060 application.shared-driver-admission** — [SharedDriverWorkAdmissionService](../../../Ssalddel/Services/Dispatch/Participation/SharedDriverWorkAdmissionService.cs#L35) · 기사 희망 업무와 음식·화물·분산 미완료 약속을 공통 수락 직전에 검사한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `PersistentWrite`
  - 경계: SQL 잠금 행만 갱신한다. 전문 원장·차량 적합성·완료·정산·지급 권위를 대체하지 않는다.
  - 먼저 읽을 단계: None
- **070 application.work-start** — [운행시작CommandHandler](../../../Ssalddel/Application/Driver/Work/Handlers/운행시작CommandHandler.cs#L29) · 권한과 요청 소유권을 검증해 근무와 음식 운행 시작 후속 의도를 함께 확정한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | MayIncurExternalCost | StateMutation`
  - 경계: 동일 요청·동일 입력은 같은 SQL 근무를 반환한다. 음식 시작의 SQL commit과 인덱스 후속 성공은 별도다. 같은 Handler의 화물 분기는 기존 즉시 후속·추천을 통해 외부 경로 API와 푸시를 호출할 수 있으며 음식 시작에는 그 추천 호출을 적용하지 않는다.
  - 주 계약: `Ssalddel.Application.Driver.Work.운행시작Command`
  - 먼저 읽을 단계: application.work-transition-lock | contract.work-start-request | contract.work-start-response | domain.work-start-followup
- **075 application.work-status** — [운행상태조회QueryHandler](../../../Ssalddel/Application/Driver/Work/Handlers/운행상태조회QueryHandler.cs#L12) · SQL 운행·최신 근무 소유권과 위치 인덱스를 함께 재조회한다.
  - 계층/단계: `Application / Query`
  - 읽기/쓰기: `OperationalState → None`
  - 부수효과: `NetworkCall | PersistentRead`
  - 경계: 운행 상태·시작 요청 ID·실행유형의 권위는 SQL이며 위치·Aging은 Redis 또는 설정된 인덱스 사본이다. 인덱스로 SQL 운행을 복원하지 않는다.
  - 주 계약: `Ssalddel.Application.Driver.Work.운행상태조회Query`
  - 먼저 읽을 단계: contract.work-status | domain.driver-shift
- **080 application.work-stop** — [운행종료CommandHandler](../../../Ssalddel/Application/Driver/Work/Handlers/운행종료CommandHandler.cs#L19) · 권한 검증 후 SQL 근무를 종료하고 실행 인덱스를 정리한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite | StateMutation`
  - 경계: 시작·GPS·후속과 같은 기사 잠금을 사용한다. 인덱스 정리 실패는 로그에 남기고 SQL 종료를 보존하며 기존 명시적 수신 OFF와 업무 완료를 합치지 않는다.
  - 주 계약: `Ssalddel.Application.Driver.Work.운행종료Command`
  - 먼저 읽을 단계: application.work-transition-lock | domain.driver-shift
- **085 application.work-location** — [위치갱신CommandHandler](../../../Ssalddel/Application/Driver/Work/Handlers/위치갱신CommandHandler.cs#L24) · 현재 근무·앱 소유권을 검증해 GPS 투영과 접근·위치 후속을 갱신한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | MayIncurExternalCost | StateMutation`
  - 경계: 종료와 같은 기사 잠금으로 늦은 GPS의 운행 부활을 막는다. 외부 역지오코딩은 구 단위 공개 동의가 있을 때만 호출하며 고객 경로나 배달 완료를 확정하지 않는다.
  - 주 계약: `Ssalddel.Application.Driver.Work.위치갱신Command`
  - 먼저 읽을 단계: application.work-transition-lock | contract.work-status
- **110 application.current-work** — [FoodDeliveryDriverWorkspaceUseCase](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs#L24) · 정산을 읽지 않고 현재 제안·수행 시도·조리 상태를 조합한다.
  - 계층/단계: `Application / Query`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite`
  - 경계: GetCurrentWorkAsync는 월 정산·주문별 정산·완료 상세를 호출하지 않는다. SQL 전문 상태는 조회만 하며 위임한 수신 조회가 Redis 투영을 재구성할 수 있다.
  - 주 계약: `Ssalddel.Application.Driver.Food.IFoodDeliveryDriverWorkspaceUseCase`
  - 먼저 읽을 단계: application.current-bundle-policy | application.current-work-projector | application.driver-offer-query | contract.current-work
- **115 application.legacy-workspace** — [FoodDeliveryDriverWorkspaceUseCase](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs#L24) · 기존 workspace 호출에 현재 업무와 월·최근 주문 정산을 함께 반환한다.
  - 계층/단계: `Application / Query`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite`
  - 경계: GetAsync 호환 경계다. 정산 조회 실패는 현재 업무가 없는 정상 응답으로 숨기지 않고 별도 오류로 구분한다. 수령 확인과 실제 지급 완료를 추정하지 않는다.
  - 주 계약: `Ssalddel.Application.Driver.Food.IFoodDeliveryDriverWorkspaceUseCase`
  - 먼저 읽을 단계: application.current-work | contract.legacy-workspace
- **120 application.driver-actions** — [음식배달기사업무Service](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs#L87) · 기사 수락·거절·도착·중단·픽업·전달의 권한·Revision·현재 상태 전이를 조율한다.
  - 계층/단계: `Application / Confirm`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | MayIncurExternalCost | StateMutation`
  - 경계: 수락·도착·픽업·전달은 SQL 변경과 후속 예약을 같은 transaction으로 확정한다. 거절의 기존 두 단계 저장은 별도다. 거절·중단의 다음 후보 진행은 새 제안 요금 산정을 통해 외부 경로 API를 호출할 수 있으며 전달·수령·정산·실지급을 합치지 않는다.
  - 주 계약: `살뜰.Services.Dispatch.Recommendation.I음식배달기사업무Service`
  - 먼저 읽을 단계: application.driver-action-policy | contract.active-delivery | contract.driver-followup-port
- **125 application.driver-state-persistence** — [음식배달기사업무Service](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.StatePersistence.cs#L41) · 기사 동작의 SQL 원천·배달 시도·책임 판정과 transaction 저장을 처리한다.
  - 계층/단계: `Application / Persistence`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `PersistentRead | PersistentWrite | StateMutation`
  - 경계: 동시성 충돌과 DB 저장 실패는 rollback한다. SQL 전문 상태와 Mongo 주문 원장의 관계 권위는 별개이며 저장 성공이 외부 후속 완료를 뜻하지 않는다.
  - 주 계약: `살뜰.Services.Dispatch.Recommendation.I음식배달기사업무Service`
  - 먼저 읽을 단계: application.driver-actions
- **130 application.daily-settlement** — [FoodDeliveryDriverWorkspaceUseCase](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.DailySettlement.cs#L17) · 전달 완료 SQL 정산 원장을 한국 날짜 기준으로 조회한다.
  - 계층/단계: `Application / Query`
  - 읽기/쓰기: `OperationalState → None`
  - 부수효과: `PersistentRead`
  - 경계: 현재 제안·최근 40건의 합계로 대체하지 않는다. 전달 완료일·수령 확인·공제·모의 지급·실제 이체는 별개이며 실제 이체 완료를 생성하지 않는다.
  - 주 계약: `Ssalddel.Application.Driver.Food.IFoodDeliveryDriverWorkspaceUseCase`
  - 먼저 읽을 단계: contract.food-workspace-port
- **135 application.completed-detail** — [FoodDeliveryDriverWorkspaceUseCase](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CompletedDetail.cs#L19) · 기사 소유권·조회 기한·시도 연결이 확인된 완료 상세만 반환한다.
  - 계층/단계: `Application / Query`
  - 읽기/쓰기: `OperationalState → None`
  - 부수효과: `PersistentRead`
  - 경계: 다른 기사의 정산 존재 여부나 고객 정보를 공개하지 않는다. 조회 중 기한 경과도 다시 차단하며 완료 상세 조회는 현재 업무나 실제 지급 판정이 아니다.
  - 주 계약: `Ssalddel.Application.Driver.Food.IFoodDeliveryDriverWorkspaceUseCase`
  - 먼저 읽을 단계: contract.food-workspace-port
- **140 application.driver-followups** — [음식배달기사업무Service](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Followups.cs#L41) · 확정된 기사 상태 변경의 원장 동기화·알림·활동·정산 후속을 전달한다.
  - 계층/단계: `Application / Projection`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite | StateMutation`
  - 경계: 예약과 실제 처리는 별개다. 영속 Outbox가 있으면 재처리하며 기존 fallback·거절·월 정산의 별도 경계를 보존한다. 응답 유실에서 외부 수신의 정확히 한 번 전달을 보장하지 않는다.
  - 주 계약: `살뜰.Services.Dispatch.Recommendation.I음식배달기사업무Service`
  - 먼저 읽을 단계: application.driver-state-persistence | contract.driver-followup-intent
- **150 infrastructure.work-start-followup** — [기사운행시작후속Service](../../../Ssalddel/Services/Dispatch/Queue/기사운행시작후속Service.cs#L21) · SQL의 현재 근무를 실행 인덱스로 재구성하고 실패 재시도를 보존한다.
  - 계층/단계: `Infrastructure / Projection`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite | StateMutation`
  - 경계: 종료·새 근무 뒤의 오래된 작업은 인덱스를 되살리지 않는다. 같은 근무 GPS·Aging을 초기화하지 않으며 수신 의사·추천은 변경하지 않는다. SQL과 Redis는 별도 경계다.
  - 주 계약: `살뜰.Services.Dispatch.Queue.I기사운행시작후속Service`
  - 먼저 읽을 단계: application.work-start | application.work-transition-lock | contract.work-start-followup-port | infrastructure.work-start-followup-mapping
- **160 infrastructure.driver-followup-outbox** — [음식마트원장동기화OutboxService](../../../Ssalddel/Services/Community/FoodMartLedgerSyncOutboxService.cs#L60) · 기사 후속 의도를 기존 SQL Outbox에서 멱등 예약하고 최신 원천으로 처리한다.
  - 계층/단계: `Infrastructure / Persistence`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite | StateMutation`
  - 경계: 예약은 호출자 transaction에 결속하고 처리 시 최신 정본·개인정보 파기 장벽을 다시 확인한다. SQL 전문 상태·Mongo 관계 투영·외부 알림은 별개이며 수령·실지급을 확정하지 않는다.
  - 주 계약: `Ssalddel.Services.Community.I음식기사후속OutboxService`
  - 먼저 읽을 단계: application.driver-followups | contract.driver-followup-port
- **170 infrastructure.work-start-worker** — [기사운행시작후속Worker](../../../Ssalddel/Services/Dispatch/Queue/기사운행시작후속Worker.cs#L11) · 주기적으로 새 scope에서 운행 시작 후속 대기를 재처리한다.
  - 계층/단계: `Infrastructure / Tick`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | PersistentRead | PersistentWrite | StateMutation`
  - 경계: 기존 pending 작업만 처리하며 새 운행 시작이나 자동 수신 ON을 만들지 않는다. worker 반복은 실제 인덱스 성공·배차 수락·운영 활성화 증거가 아니다.
  - 먼저 읽을 단계: infrastructure.work-start-followup
- **180 api.food-driver** — [음식배달기사업무Controller](../../../Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs#L44) · 인증된 음식 기사의 조회·운행·배달 동작을 기존 route와 오류 계약으로 연결한다.
  - 계층/단계: `Api / Confirm`
  - 읽기/쓰기: `OperationalState → OperationalState`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | MayIncurExternalCost | StateMutation`
  - 경계: workspace/current는 정산 없는 조회이며 호환 workspace·정산은 별도다. 하위 SQL·Mongo·Redis·알림·경로 API 효과를 위임하며 서버 권한·Revision·개인정보와 Operational/Simulation 게이트를 보존한다.
  - 주 계약: `Ssalddel.Application.Driver.Food.IFoodDeliveryDriverWorkspaceUseCase`
  - 먼저 읽을 단계: application.completed-detail | application.current-work | application.daily-settlement | application.driver-actions | application.legacy-workspace | application.work-location | application.work-start | application.work-status | application.work-stop
- **190 contract.workspace-port** — [IRoleWorkspaceAdapter](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/RoleWorkspaceContracts.cs#L25) · 공통 역할 셸이 조회·행동·초기화를 위임하는 포트
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 역할별 구현의 내부 저장소·권한을 소유하지 않는 호출 계약이다.
  - 먼저 읽을 단계: None
- **191 contract.recovery-port** — [IRoleWorkspaceRecovery](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/RoleWorkspaceContracts.cs#L25) · 미확정 행동과 정본 조회 복구를 공통 셸에 제공하는 포트
  - 계층/단계: `Contract / Definition`
  - 읽기/쓰기: `None → None`
  - 부수효과: `None`
  - 경계: 구현체별 복구 판단을 노출하며 재전송·영속 저장 자체를 수행하지 않는다.
  - 먼저 읽을 단계: None
- **200 ui.recovery-policy** — [FoodDriverWorkRecoveryPolicy](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkRecoveryPolicy.cs#L13) · 운행 시작의 동일 요청·정본 상태와 응답 유실 복구를 순수 판정
  - 계층/단계: `Domain / Definition`
  - 읽기/쓰기: `ClientPresentation → None`
  - 부수효과: `None`
  - 경계: 전달받은 값만 판정하며 HTTP·GPS·암호화 저장·서버 상태 변경은 호출자가 소유한다.
  - 먼저 읽을 단계: contract.work-start-response | contract.work-status
- **203 ui.food-presentation** — [FoodWorkspacePresentation](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodWorkspacePresentation.cs#L11) · 음식 역할의 금액·시각·가능 행동·지도 표시 값을 순수 변환
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `ClientPresentation → None`
  - 부수효과: `None`
  - 경계: 기사 이외 음식 역할도 공유하는 표시 도구이며 조회·명령·정산 확정은 수행하지 않는다.
  - 먼저 읽을 단계: ui.recovery-policy
- **205 ui.stage-presentation** — [FoodDriverStagePresentation](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverStagePresentation.cs#L14) · 확인된 배달 상태·가능 행동으로 현재 수행 단계와 주 행동 표시를 선택
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `ClientPresentation → None`
  - 부수효과: `None`
  - 경계: 순수 표시 판정이며 실제 픽업·전달 가능 권한을 생성하거나 서버 상태를 변경하지 않는다.
  - 먼저 읽을 단계: contract.active-delivery | ui.food-presentation
- **206 ui.workspace-presentation** — [FoodDriverWorkspacePresentation](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkspacePresentation.cs#L13) · 확정된 현재 배달 응답을 단계별 목적지·요청·주문 카드와 지도 값으로 변환
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `ClientPresentation → None`
  - 부수효과: `None`
  - 경계: 조회·명령·장치·영속 저장을 소유하지 않는다. 미확인 거리와 0을 구분하며 정산을 현재 조회에 섞지 않는다.
  - 먼저 읽을 단계: contract.current-work | ui.food-presentation | ui.stage-presentation
- **210 ui.food-driver-adapter** — [FoodDriverRoleWorkspaceAdapter](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverRoleWorkspaceAdapter.cs#L16) · 음식 기사 현재 조회·운행 시작·수신 의사·수행 명령과 정본 복구를 조립
  - 계층/단계: `ClientAdapter / Confirm`
  - 읽기/쓰기: `OperationalState | ClientPresentation → OperationalState | ClientPresentation`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | UiStateMutation | MayIncurExternalCost | StateMutation`
  - 경계: 서버 명령 효과를 위임한다. 미확정 ID는 이 객체 수명에만 보존하며 네이티브 암호화 저장을 수행하지 않는다. 자동 조회는 수락이나 시작 POST 자동 재전송이 아니다.
  - 주 계약: `Ssalddel.Contracts.Driver.Food.FoodDeliveryDriverCurrentWorkDto`
  - 먼저 읽을 단계: api.food-driver | contract.recovery-port | contract.workspace-port | ui.recovery-policy | ui.workspace-presentation
- **215 ui.food-action-view-model** — [FoodRoleActionViewModel](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodRoleActionViewModel.cs#L15) · 음식 역할의 별도 행동 입력·확인·동일 요청 복구를 계정 수명에 결속
  - 계층/단계: `ViewModel / Confirm`
  - 읽기/쓰기: `OperationalState | ClientPresentation → OperationalState | ClientPresentation`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | UiStateMutation | MayIncurExternalCost | StateMutation`
  - 경계: 기사 중단 등 확인 입력의 클라이언트 조율이며 서버 권한·상태 원장을 대체하지 않는다. 다른 음식 역할과 공유한다.
  - 먼저 읽을 단계: api.food-driver | ui.food-presentation
- **220 ui.workspace-view-model** — [RoleWorkspaceViewModel](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleWorkspaceViewModel.cs#L10) · 역할 포트의 조회·선택·계정 수명·자동 확인과 표시 상태를 조율
  - 계층/단계: `ViewModel / Presentation`
  - 읽기/쓰기: `ClientPresentation → OperationalState | ClientPresentation`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | UiStateMutation | MayIncurExternalCost | StateMutation`
  - 경계: 구체 음식 기사 Adapter에 의존하지 않는 공통 셸이다. 포트로 위임한 명령 효과를 포함하며 업무 권한·영속 저장은 서버가 소유한다.
  - 먼저 읽을 단계: contract.recovery-port | contract.workspace-port
- **230 ui.role-map-view** — [RoleMapWorkspace](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor#L3) · 전체 지도와 독립된 상태별 카드·행동을 표시하는 공통 역할 화면
  - 계층/단계: `View / Presentation`
  - 읽기/쓰기: `ClientPresentation → OperationalState | ClientPresentation`
  - 부수효과: `NetworkCall | ThirdPartyApiCall | PersistentRead | PersistentWrite | UiStateMutation | MayIncurExternalCost | StateMutation`
  - 경계: Model에 조회·명령을 위임한다. 카드 높이로 지도를 변경하지 않으며 서버 권한·실제 GPS·지도 SDK 성공을 이 메타데이터가 증명하지 않는다.
  - 먼저 읽을 단계: ui.workspace-view-model

### 소스 결속

- **source.contract.current-work** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `contract.current-work`, `contract.legacy-workspace`, `contract.offer`, `contract.active-delivery`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs:15](../../../Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs#L15) · `StepKey = "contract.current-work"`
- **source.contract.work-start-request** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `contract.work-start-request`, `contract.work-start-response`, `contract.work-status`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Contracts/Driver/Work/기사운행Dtos.cs:11](../../../Ssalddel.Contracts/Driver/Work/기사운행Dtos.cs#L11) · `StepKey = "contract.work-start-request"`
- **source.domain.driver-shift** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `domain.driver-shift`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Domain/기사/DriverShift.cs:14](../../../Ssalddel.Domain/기사/DriverShift.cs#L14) · `StepKey = "domain.driver-shift"`
- **source.domain.work-start-followup** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `domain.work-start-followup`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Domain/기사/DriverWorkStartFollowup.cs:12](../../../Ssalddel.Domain/기사/DriverWorkStartFollowup.cs#L12) · `StepKey = "domain.work-start-followup"`
- **source.infrastructure.shift-mapping** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `infrastructure.shift-mapping`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Infrastructure/Persistence/Configurations/Driver/기사근무Configuration.cs:17](../../../Ssalddel.Infrastructure/Persistence/Configurations/Driver/기사근무Configuration.cs#L17) · `StepKey = "infrastructure.shift-mapping"`
- **source.infrastructure.work-start-followup-mapping** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `infrastructure.work-start-followup-mapping`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Infrastructure/Persistence/Configurations/Driver/DriverWorkStartFollowupConfiguration.cs:16](../../../Ssalddel.Infrastructure/Persistence/Configurations/Driver/DriverWorkStartFollowupConfiguration.cs#L16) · `StepKey = "infrastructure.work-start-followup-mapping"`
- **source.ui.role-map-view** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.role-map-view`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor:3](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor#L3) · `StepKey = "ui.role-map-view"`
- **source.ui.workspace-view-model** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.workspace-view-model`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleWorkspaceViewModel.cs:10](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleWorkspaceViewModel.cs#L10) · `StepKey = "ui.workspace-view-model"`
- **source.ui.food-driver-adapter** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.food-driver-adapter`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverRoleWorkspaceAdapter.cs:16](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverRoleWorkspaceAdapter.cs#L16) · `StepKey = "ui.food-driver-adapter"`
- **source.ui.stage-presentation** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.stage-presentation`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverStagePresentation.cs:14](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverStagePresentation.cs#L14) · `StepKey = "ui.stage-presentation"`
- **source.ui.recovery-policy** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.recovery-policy`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkRecoveryPolicy.cs:13](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkRecoveryPolicy.cs#L13) · `StepKey = "ui.recovery-policy"`
- **source.ui.workspace-presentation** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.workspace-presentation`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkspacePresentation.cs:13](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverWorkspacePresentation.cs#L13) · `StepKey = "ui.workspace-presentation"`
- **source.ui.food-action-view-model** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.food-action-view-model`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodRoleActionViewModel.cs:15](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodRoleActionViewModel.cs#L15) · `StepKey = "ui.food-action-view-model"`
- **source.ui.food-presentation** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `ui.food-presentation`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodWorkspacePresentation.cs:11](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodWorkspacePresentation.cs#L11) · `StepKey = "ui.food-presentation"`
- **source.contract.workspace-port** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `contract.workspace-port`, `contract.recovery-port`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel.Ui.Common/Areas/App/RoleWorkspace/RoleWorkspaceContracts.cs:25](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/RoleWorkspaceContracts.cs#L25) · `StepKey = "contract.workspace-port"`
- **source.application.current-bundle-policy** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.current-bundle-policy`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.BundlePolicy.cs:25](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.BundlePolicy.cs#L25) · `StepKey = "application.current-bundle-policy"`
- **source.application.completed-detail** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.completed-detail`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CompletedDetail.cs:19](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CompletedDetail.cs#L19) · `StepKey = "application.completed-detail"`
- **source.contract.food-workspace-port** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `contract.food-workspace-port`, `application.current-work`, `application.legacy-workspace`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs:24](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs#L24) · `StepKey = "contract.food-workspace-port"`
- **source.application.current-work-projector** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.current-work-projector`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CurrentWorkProjection.cs:25](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CurrentWorkProjection.cs#L25) · `StepKey = "application.current-work-projector"`
- **source.application.daily-settlement** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.daily-settlement`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.DailySettlement.cs:17](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.DailySettlement.cs#L17) · `StepKey = "application.daily-settlement"`
- **source.application.work-transition-lock** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.work-transition-lock`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Work/기사운행전이Lock.cs:16](../../../Ssalddel/Application/Driver/Work/기사운행전이Lock.cs#L16) · `StepKey = "application.work-transition-lock"`
- **source.application.work-status** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.work-status`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Work/Handlers/운행상태조회QueryHandler.cs:12](../../../Ssalddel/Application/Driver/Work/Handlers/운행상태조회QueryHandler.cs#L12) · `StepKey = "application.work-status"`
- **source.application.work-start** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.work-start`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Work/Handlers/운행시작CommandHandler.cs:29](../../../Ssalddel/Application/Driver/Work/Handlers/운행시작CommandHandler.cs#L29) · `StepKey = "application.work-start"`
- **source.application.work-stop** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.work-stop`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Work/Handlers/운행종료CommandHandler.cs:19](../../../Ssalddel/Application/Driver/Work/Handlers/운행종료CommandHandler.cs#L19) · `StepKey = "application.work-stop"`
- **source.application.work-location** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.work-location`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Application/Driver/Work/Handlers/위치갱신CommandHandler.cs:24](../../../Ssalddel/Application/Driver/Work/Handlers/위치갱신CommandHandler.cs#L24) · `StepKey = "application.work-location"`
- **source.api.food-driver** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `api.food-driver`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs:44](../../../Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs#L44) · `StepKey = "api.food-driver"`
- **source.contract.driver-followup-intent** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `contract.driver-followup-intent`, `contract.driver-followup-port`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Community/FoodDriverFollowupOutbox.cs:15](../../../Ssalddel/Services/Community/FoodDriverFollowupOutbox.cs#L15) · `StepKey = "contract.driver-followup-intent"`
- **source.infrastructure.driver-followup-outbox** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `infrastructure.driver-followup-outbox`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Community/FoodMartLedgerSyncOutboxService.cs:60](../../../Ssalddel/Services/Community/FoodMartLedgerSyncOutboxService.cs#L60) · `StepKey = "infrastructure.driver-followup-outbox"`
- **source.contract.work-start-followup-port** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `contract.work-start-followup-port`, `infrastructure.work-start-followup`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Dispatch/Queue/기사운행시작후속Service.cs:21](../../../Ssalddel/Services/Dispatch/Queue/기사운행시작후속Service.cs#L21) · `StepKey = "contract.work-start-followup-port"`
- **source.infrastructure.work-start-worker** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `infrastructure.work-start-worker`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Dispatch/Queue/기사운행시작후속Worker.cs:11](../../../Ssalddel/Services/Dispatch/Queue/기사운행시작후속Worker.cs#L11) · `StepKey = "infrastructure.work-start-worker"`
- **source.application.driver-actions** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.driver-actions`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs:87](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs#L87) · `StepKey = "application.driver-actions"`
- **source.application.driver-followups** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.driver-followups`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Followups.cs:41](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Followups.cs#L41) · `StepKey = "application.driver-followups"`
- **source.application.driver-action-policy** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.driver-action-policy`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Policies.cs:40](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Policies.cs#L40) · `StepKey = "application.driver-action-policy"`
- **source.application.driver-offer-query** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.driver-offer-query`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Query.cs:42](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.Query.cs#L42) · `StepKey = "application.driver-offer-query"`
- **source.application.driver-state-persistence** · reflection 단계의 실제 선언 파일 및 partial 책임을 명시적으로 결속
  - 관련 단계(탐색): `application.driver-state-persistence`
  - 경계: 탐색 소스 연결이며 런타임 호출·완료 증거가 아니다.
  - [Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.StatePersistence.cs:41](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.StatePersistence.cs#L41) · `StepKey = "application.driver-state-persistence"`
- **native.current-work** · 네이티브 앱의 현재 업무 API와 화면 정본 재조회 진입
  - 관련 단계(탐색): `api.food-driver`, `application.current-work`
  - 경계: 현재 조회는 정산 조회와 분리한다. 네이티브 assembly를 reflection으로 불러오지 않는다.
  - [FDriverApp/Services/FoodDeliveryDriverApiService.cs:77](../../../FDriverApp/Services/FoodDeliveryDriverApiService.cs#L77) · `public Task<FoodDeliveryDriverCurrentWorkDto> GetCurrentWorkAsync`
  - [FDriverApp/PageModels/MainPageModel.cs:719](../../../FDriverApp/PageModels/MainPageModel.cs#L719) · `private async Task LoadWorkspaceAsync`
- **native.automatic-start** · 운행 시작 한 번과 동일 요청 ID를 통한 정본 확인·수신 의사 복구
  - 관련 단계(탐색): `application.work-start`, `ui.recovery-policy`
  - 경계: 불확실한 시작 POST를 자동 재전송하지 않고 사용자의 명시적 수신 OFF를 보존한다.
  - [FDriverApp/PageModels/MainPageModel.AutomaticWork.cs:28](../../../FDriverApp/PageModels/MainPageModel.AutomaticWork.cs#L28) · `private async Task StartAutomaticWorkAsync`
  - [FDriverApp/PageModels/MainPageModel.AutomaticWork.cs:94](../../../FDriverApp/PageModels/MainPageModel.AutomaticWork.cs#L94) · `private async Task ReconcileAutomaticWorkAsync`
- **native.pending-recovery** · 계정별 미확정 요청 ID의 네이티브 암호화 저장·복원
  - 관련 단계(탐색): `ui.recovery-policy`, `contract.recovery-port`
  - 경계: 공통 UI 객체의 일시 보존과 별도다. 저장·복원은 실제 기기 실행 검증을 대신하지 않는다.
  - [FDriverApp/PageModels/MainPageModel.FoodPending.cs:33](../../../FDriverApp/PageModels/MainPageModel.FoodPending.cs#L33) · `private async Task RestorePendingOperationsAsync`
  - [FDriverApp/PageModels/MainPageModel.FoodPending.cs:86](../../../FDriverApp/PageModels/MainPageModel.FoodPending.cs#L86) · `private async Task PersistPendingOperationsAsync`
  - [FDriverApp/Services/FDriverSecurePendingOperationStore.cs:5](../../../FDriverApp/Services/FDriverSecurePendingOperationStore.cs#L5) · `public sealed class FDriverSecurePendingOperationStore`
- **native.work-screen** · 전문 기사 전체 지도·독립 카드 XAML과 코드부
  - 관련 단계(탐색): `ui.stage-presentation`, `ui.workspace-presentation`, `api.food-driver`
  - 경계: x:Class와 namespace/partial class를 함께 검사한다. 네이티브 화면은 reflection View 형식으로 위장하지 않는다.
  - [FDriverApp/Pages/MainPage.xaml:8](../../../FDriverApp/Pages/MainPage.xaml#L8) · `x:Class="FDriverApp.Pages.MainPage"`
  - [FDriverApp/Pages/MainPage.xaml.cs:8](../../../FDriverApp/Pages/MainPage.xaml.cs#L8) · `public partial class MainPage`
- **native.profile-settlement** · 내 정보 진입의 정산 조회와 수행 자동 조회의 분리
  - 관련 단계(탐색): `application.legacy-workspace`, `application.daily-settlement`, `application.completed-detail`
  - 경계: 정산·입금 완료를 현재 배달 상태에서 추론하지 않는다.
  - [FDriverApp/PageModels/MainPageModel.FoodDailySettlement.cs:29](../../../FDriverApp/PageModels/MainPageModel.FoodDailySettlement.cs#L29) · `public async Task RefreshProfileSettlementAsync`
  - [FDriverApp/Services/FoodDeliveryDriverApiService.cs:110](../../../FDriverApp/Services/FoodDeliveryDriverApiService.cs#L110) · `public Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync`
- **native.foreground-notification** · 화면 전경 복귀·배차 알림 뒤 최신 업무 재조회
  - 관련 단계(탐색): `ui.recovery-policy`, `application.current-work`
  - 경계: 백그라운드 위치·실제 푸시 수신·자동 수락 완료의 증거가 아니다.
  - [FDriverApp/PageModels/MainPageModel.cs:193](../../../FDriverApp/PageModels/MainPageModel.cs#L193) · `private async Task ResumeForegroundWorkspaceAsync`
  - [FDriverApp/PageModels/MainPageModel.FoodNotifications.cs:204](../../../FDriverApp/PageModels/MainPageModel.FoodNotifications.cs#L204) · `public async Task ResumeFoodNotificationWorkspaceAsync`
- **source.application.shared-driver-admission** · 기사 업무 선택과 미완료 약속을 수락 직전에 검사하는 공통 관문
  - 관련 단계(탐색): `application.shared-driver-admission`
  - 경계: 전문 원장과 자격·차량·완료·정산을 대체하지 않는 연결
  - [Ssalddel/Services/Dispatch/Participation/SharedDriverWorkAdmissionService.cs:35](../../../Ssalddel/Services/Dispatch/Participation/SharedDriverWorkAdmissionService.cs#L35) · `StepKey = "application.shared-driver-admission"`
- **source.domain.straight-line-delivery-pricing** · 원래 직선거리로 기사 거리비만 계산한다
  - 관련 단계(탐색): `domain.straight-line-delivery-pricing`
  - 경계: 운임 수락·실지급·경로 안전 판정을 대신하지 않는다
  - [Ssalddel.Domain/운송/StraightLineDeliveryPricingPolicy.cs:8](../../../Ssalddel.Domain/운송/StraightLineDeliveryPricingPolicy.cs#L8) · `StepKey = "domain.straight-line-delivery-pricing"`
- **source.application.driver-offer-evidence** · 저장된 기사 요금과 실제 피킹 책임을 제안 표시로 읽는다
  - 관련 단계(탐색): `application.driver-offer-evidence`
  - 경계: 운임 수락·실지급·경로 안전 판정을 대신하지 않는다
  - [Ssalddel/Services/Dispatch/Recommendation/기사제안표시근거Service.cs:13](../../../Ssalddel/Services/Dispatch/Recommendation/기사제안표시근거Service.cs#L13) · `StepKey = "application.driver-offer-evidence"`

## 진단 요약

- 오류: 0
- 경고: 7
- 일반 공개 타입의 미표기는 경고이며, 필수 단계·권위 위반·오래된 생성 파일만 검증을 차단한다.
