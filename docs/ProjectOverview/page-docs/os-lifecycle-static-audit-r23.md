# 업무 OS 생명주기·절차·상태 정적 조사 r23

2026년 10월 6일 Mirror의 운영 업무를 현재 코드·계약·문서와 대조했다. 기준은 `OperatingSystemIds.All`의 **10개 OS**다. 정의된 **41개 책임 단계**, 조사한 **112개 절차 연결**과 **101개 상태묶음**을 소스·담당·선행 조건·인계·회복·관련 시험에 연결했다.

공통 단계 카탈로그 5개 OS는 모두 조사했고, 단계가 없는 나머지 5개는 실제 코드에서 관찰한 절차를 별도로 기록했다. 공통 단계 부재를 기능 전체의 미구현으로 해석하지 않는다. 조사 묶음 수는 중복 소비·조회 투영을 포함하므로 고유 enum 수나 실행 완료율이 아니다. 모든 코드 분기·페이지·상태의 조합을 전수 실행한 결과는 아니다.

역할 화면은 누가 이용하는지를, OS는 어떤 업무 책임을 처리하는지를 나눈다. 한 역할이 여러 OS의 절차를 이용하거나 한 OS를 여러 역할이 함께 처리할 수 있으므로 두 분류를 일대일로 맞추지 않는다.

기준 작업본: `dev/mirror-integration` / `b1b7cfc39b5183ed56246080c141d63965bf2704`의 dirty worktree. 기계 판독 조사 원장은 [JSON](os-lifecycle-static-audit-r23.json), 재검사는 [조사 도구](../../../eng/Ssalddel.OperatingSystemAudit/README.md)다. 근거·시험 후보 파일 518개를 SHA-256에 결속했고, source anchor와 줄을 대조했다. 후보 파일 전부의 모든 분기를 깊게 검토했다는 뜻은 아니다.

## 조사 범위와 생명주기 대장

| OS | 안정 식별자 | 공통 단계 | 조사 절차 연결 | 상태묶음 |
| --- | --- | --- | --- | --- |
| 화주 운송 관리 | `ShipperTransportManagementOS` | 8 | 9 | 8 |
| 국내 화물 운송 | `DomesticCargoTransportOS` | 8 | 8 | 14 |
| 창고·커머스 이행 | `WarehouseCommerceFulfillmentOS` | 9 | 12 | 14 |
| 공동구매 수요·모집 | `GroupPurchaseDemandOS` | 공통 단계 미정의 | 6 | 6 |
| 같이 주문 수입 | `GroupPurchaseImportOS` | 공통 단계 미정의 | 7 | 6 |
| 음식 배달 OS | `FoodDeliveryOS` | 8 | 42 | 31 |
| 살뜰마트 도심 물류 | `SsalddelMartUrbanLogisticsOS` | 8 | 9 | 9 |
| 커뮤니티 신뢰 | `CommunityTrustOS` | 공통 단계 미정의 | 8 | 6 |
| 플랫폼 운영 | `PlatformOperationsOS` | 공통 단계 미정의 | 4 | 2 |
| 교육 현장 체험 지원 | `EducationFieldExperienceOS` | 공통 단계 미정의 | 7 | 5 |

주요 기준 소스는 [생명주기·식별자](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs), [인계 카탈로그](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemInteractionCatalog.cs), [API의 업무·참여자·화면 대장](../../../Ssalddel/ApiMetadata/SsalddelApiVersionAttribute.cs), [업무 실행 책임 모델](../../Architecture/BusinessWorkflowResponsibilityModel.md)이다.

API의 OS 선언은 9개이고 공통 ID는 10개다. 교육 OS는 공통 ID·교육 원장에 존재하며 현재 API 메타데이터는 CommunityTrust workflow를 사용한다. 엔진 결속은 8개, 스케줄 정책 선언은 27개 중 구현 카탈로그의 Active 표시는 3개다. Declared 표시는 해당 알고리즘이 어디에서도 작동하지 않는다는 증거가 아니다. 업무 관계 15개·참여자 연결 25개·화면 선언 28개는 명시적 OS 인계 계약 4개와 다른 대장이다.

## 상태를 읽고 검증하는 기준

| 층위 | 확인 대상 | 검증 근거와 한계 |
| --- | --- | --- |
| OS 생명주기 | 업무 전체에서 어떤 책임을 다루는가 | Stage ID·Sequence·책임 설명. Sequence 자체는 실행 가능한 전이표가 아니다. |
| 절차 | 현재 건을 누가 어떤 조건으로 처리하는가 | API → UseCase/Command → Policy/Store → DB/Event/Outbox 경로. |
| 업무 상태축 | 주문·배차·준비·증빙·동의·정산이 각각 어디까지 진행됐는가 | 서로 독립된 상태를 하나의 완료값으로 합치지 않는다. |
| 다음 행동 | 현재 actor가 무엇을 할 수 있는가 | 현재 소유권·상태·revision·동의·증빙·기한에 대한 서버 Guard와 AvailableActions. |
| 화면 상태 | 지금 필요한 카드와 목적지는 무엇인가 | 서버 상태의 읽기 투영. 로그인·역할 선택·앱 수명·지도 도구는 업무 완료를 확정하지 않는다. |
| 후속 처리 | 저장한 결과가 다른 원장/역할에 연결됐는가 | 멱등 요청, 영속 Outbox, 재시도, 재조회. 원본 완료와 알림·투영·지급 성공을 구분한다. |

음식점의 신규 조리는 유효한 기사 배정 뒤 시작한다. 카탈로그의 조리 30·배차 40을 그대로 실행 순서로 읽으면 이 선행 조건과 어긋난다. 이미 준비된 음식·재조리도 현재 준비 회차와 유효 배달 시도를 함께 확인한다. 배차대기열 생성, 기사 제안, 기사 수락, 픽업, 전달, 고객 수령, 정산 후보, 실제 지급은 서로 다른 결과다.

## 먼저 보완할 부분

| 순서 | 범위 | 확인한 부족 | 완료 기준 |
| --- | --- | --- | --- |
| 1 | 동의·상태 무결성 | 타인 명의 연락처 공개 동의 저장, 음식 Outbox 재시도의 배차상태 회귀, 교육 worker의 최종 결정 회귀 | 인증 actor 결속·진행/종결 상태 보존·실패 후 재시도 회귀시험을 각각 닫는다. 실제 유출·운영 사고는 미재현. |
| 2 | 절차 우회·조건 변경 | 기존 창고 put-away/pack과 새 UseCase의 선행 상태·수량 관문 차이, 화주 일부 조건 변경과 새 판본/재동의 차이 | 동일 업무의 모든 공개 진입점에서 같은 관문을 적용한다. 화주의 가격 변경 잠금은 이미 있으며 그 외 분기를 확인한다. |
| 3 | 저장·후속 처리 복구 | 교육 원장/제출 큐, 친구 응답/알림 Outbox의 분리 저장, 수입 선적 과거 사건의 현재상태 덮기 | 일부 저장 성공 후 실패·응답 유실·지연 사건을 재처리해도 앞선 결과를 보존한다. |
| 4 | 선언과 실행 연결 | 창고 수량 이상 회복, 마트 인계 이후 기사 실행·전달 결과 반환 경로의 확인 공백 | 기존 경로를 먼저 찾거나 보완하고 같은 건의 원본·시도·인계·반환 증거를 연결한다. 조사범위 미발견을 글로벌 부재로 단정하지 않는다. |
| 5 | 공통 대장·문서 | 공통 생명주기 없는 5개 OS, 화면/참여자 역할 3개 누락, 교육 API 대장 범위·오래된 서술 | 관찰한 실제 절차를 공통 대장에 결속한다. 새 기능·권한을 자동 추가하지 않는다. |

P1/P2는 후속 보완 우선순위이며 실제 운영 실패의 심각도를 측정한 값이 아니다. 외부 결제·환불·식당 판매대금은 기존 선언과 연결 여부를 조사한 항목이다. 현재 사용자 방향의 당사자 간 교류·자율 거래를 유지하며, 플랫폼 내 결제나 자동 환불을 새 필수 절차로 확정하지 않는다.

## OS 간 인계

| 인계 | 출발 단계 | 도착 단계 | 의미 |
| --- | --- | --- | --- |
| shipper-request-to-cargo | shipper.transport-handoff | cargo.request | 확정된 화주 운송의뢰의 실행 책임을 화물운송 OS에 넘기되 배차나 기사 상태를 직접 변경하지 않습니다. |
| cargo-completion-to-shipper-acceptance | cargo.evidence-settlement | shipper.delivery-acceptance | 하차 증빙이 있는 운송 완료 결과를 화주 인수·검수로 반환하며 정산 완료를 자동 확정하지 않습니다. |
| warehouse-outbound-to-cargo | warehouse.outbound-handoff | cargo.request | 완료된 창고 출고를 이미 존재하는 화물 운송의뢰에 결속하며 새 운송 의뢰나 배차를 확정하지 않습니다. |
| mart-last-mile-to-food-delivery | mart.last-mile-handoff | food.dispatch | 마트 주문 전체가 아니라 라스트마일 자식 업무만 음식배달 OS에 인계합니다. |

공통 인계 상태는 Requested·Held·Accepted·Rejected·Expired다. 수락 전 책임은 출발 OS에 남고 수락 때 이동한다. 도착 OS, 예상 revision, UTC 만료, 도착 업무 Stable ID 또는 보류/거절 사유를 확인하며 Accepted·Rejected·Expired는 종결이다. 책임 인계 수락은 배송·인수·정산 완료가 아니다. [Coordinator](../../../Ssalddel/Services/Operations/운영체제업무인계Coordinator.cs)와 [공통 계약](../../../Ssalddel.Contracts/Common/Operations/운영체제업무인계Contracts.cs)이 기준이다.

## OS별 절차와 상태

### 1. 화주 운송 관리

`ShipperTransportManagementOS` · 공통 단계 8 · 조사 절차 연결 9 · 상태묶음 8.

| 공통 책임 단계 | 정의된 책임 |
| --- | --- |
| 10 · `shipper.party-context` · 화주 당사자 확정 | 이번 운송에서 물건을 맡기고 비용을 부담할 당사자와 권한을 확인합니다. |
| 20 · `shipper.cargo-definition` · 화물 정의 | 품목·수량·중량·부피·포장·온도와 차량 요구 조건을 기록합니다. |
| 30 · `shipper.terms-quote` · 운송 조건·운임 검토 | 상하차 위치·시간창·기준운임·추가 비용과 지급 조건을 검토합니다. |
| 40 · `shipper.request-commitment` · 운송의뢰 확정 | 검증된 운송 조건과 정산 조건을 판본화하여 화주 의뢰로 확정합니다. |
| 50 · `shipper.transport-handoff` · 화물운송 인계 | 확정된 의뢰의 실행 책임을 화물운송 OS에 명시적으로 인계합니다. |
| 60 · `shipper.progress-change` · 진행·조건 변경 | 운송 진행을 조회하고 핵심 조건 변경은 새 판본과 재동의 대상으로 분리합니다. |
| 70 · `shipper.delivery-acceptance` · 인수·검수 | 인수증·수량 부족·파손과 반품·재위탁 여부를 확인합니다. |
| 80 · `shipper.settlement-recovery` · 정산·비정상 운송 처리·업무 회복 | 운임 지급·정산과 수량 부족·파손·인수 보류 같은 비정상 운송의 검토·해결·재처리를 관리합니다. |

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 등록 주체·소유자·관리자/업무담당자 권한 확정<br>`shipper.party-context` | 의뢰생성CommandHandler / 화주운송업무담당자UseCase | 조건: 현재 로그인 사용자; 공급자 자격; administrator/owner 별 Resolve / 변경 시 화물조건변경/위치연락처변경/배차조정/정산확인 요청권한 | [의뢰생성CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰생성CommandHandler.cs) `의뢰생성CommandHandler.Handle` 38행<br>[의뢰생성CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰생성CommandHandler.cs) `의뢰생성CommandHandler.Handle` 89행 |
| 2. 품명·수량·중량·부피·포장·온도·차량 요건 등록/수정<br>`shipper.cargo-definition` | 의뢰생성CommandHandler / 의뢰수정CommandHandler | 조건: 필수 화물 및 주소/전화/시간 순서 검증; cargo requirement upsert / 화물 변경은 화물조건변경 권한 | [의뢰생성CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰생성CommandHandler.cs) `의뢰생성CommandHandler.Handle` 57행<br>[의뢰수정CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰수정CommandHandler.cs) `의뢰수정CommandHandler.PersistAsync` 68행 |
| 3. 서버 운임 견적과 장소·시간·결제/정산 조건 검증<br>`shipper.terms-quote` | 화주운송의뢰UseCase / 의뢰수정CommandHandler | 조건: pricingChanged 경로는 생성됨+결제대기+배차미시작+미승인정산, transport 대기/미배정일 때만 통과(FreightPricingLocked) / 서버 기준운임/좌표/거리/음수금액 검증 | [화주운송의뢰UseCase.cs](../../../Ssalddel/Application/Shipper/Request/화주운송의뢰UseCase.cs) `화주운송의뢰UseCase.BuildServerPricingAsync` 384행<br>[의뢰수정CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰수정CommandHandler.cs) `의뢰수정CommandHandler.PersistAsync` 97행 |
| 4. Mongo 원본과 RDB 의뢰·운임구성·화물요건 저장<br>`shipper.request-commitment` | 의뢰생성CommandHandler | 조건: user scoped clientRequestId 중복 재전송 / Mongo sync 후 원본/RDB 존재 확인<br>전이: 의뢰 생성됨 / 배차 미시작 / 결제 및 정산 상태는 결제조건에 따라 초기화 | [의뢰생성CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰생성CommandHandler.cs) `의뢰생성CommandHandler.Handle` 127행<br>[의뢰생성CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰생성CommandHandler.cs) `의뢰생성CommandHandler.Handle` 184행 |
| 5. 생성된 의뢰를 화물 OS에 명시적으로 인계<br>`shipper.transport-handoff` | 화주운송의뢰화물운송인계Service | 조건: 저장된 의뢰 필수; deterministic request/accept Guid; 기존 계약·OS·원본 Revision 검증 / 거절/만료 인계 재사용 불가<br>전이: Requested→Accepted; cargo-transport-request:{id} 연결; 이 단계 자체로 기사 배차 확정하지 않음 | [화주운송의뢰화물운송인계Service.cs](../../../Ssalddel/Services/Operations/화주운송의뢰화물운송인계Service.cs) `화주운송의뢰화물운송인계Service.인계Async` 65행 |
| 6. 현재 운송 조회 및 권한 있는 변경<br>`shipper.progress-change` | 의뢰수정CommandHandler / ShipperWorkspaceAdapter | 조건: 변경분류별 권한; pricingChanged에는 FreightPricingLocked / 수량·중량·온도/시간창 등 pricingChanged=false 분기는 직접 저장 가능<br>전이: cargo/time/settlement fields mutate; UpdatedAt만 갱신하는 분기 존재 | [의뢰수정CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰수정CommandHandler.cs) `의뢰수정CommandHandler.PersistAsync` 57행<br>[CargoWorkspaceAdapters.cs](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceAdapters.cs) `ShipperWorkspaceAdapter.Map` 44행 |
| 7. 기사 완료 증빙 인계를 화주 수락하고 인수증 등록<br>`shipper.delivery-acceptance` | 화물운송완료화주인수인계Service / 화주운송의뢰인수증등록CommandHandler | 조건: 인수확인 권한; 인수증 번호 필수; 정산 후불승인완료 또는 인수증대기 / 연결 transport/return handoff 없으면 인계 수락 결과가 null일 수 있음<br>전이: 인수증등록완료 저장은 정산완료와 별도 | [화물운송완료화주인수인계Service.cs](../../../Ssalddel/Services/Operations/화물운송완료화주인수인계Service.cs) `화물운송완료화주인수인계Service.화주인수Async` 130행<br>[화주운송의뢰인수증등록CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/화주운송의뢰인수증등록CommandHandler.cs) `화주운송의뢰인수증등록CommandHandler.Handle` 24행 |
| 8. 현장지급 확인 / 후불 승인 / 운송 후 입금 요청<br>`shipper.settlement-recovery` | 현장지급처리·후불승인 handlers / 운송완료입금요청Service | 조건: 정산확인 권한; 열린 비정상사건 보류; 허용 정산시점/조건 / 현장지급 처리는 현장수금예정 및 매칭중이며 실제 현금 수금 증거가 아님<br>전이: 후불승인완료+매칭중; 준비조건 만족하면 배차대기 생성 | [화주운송의뢰현장지급처리CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/화주운송의뢰현장지급처리CommandHandler.cs) `화주운송의뢰현장지급처리CommandHandler.Handle` 32행<br>[화주운송의뢰후불승인CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/화주운송의뢰후불승인CommandHandler.cs) `화주운송의뢰후불승인CommandHandler.Handle` 33행 |
| 9. 비정상 검토 및 시뮬레이션 취소/환불<br>`shipper.settlement-recovery` | 비정상운송사건Service / 관리자운송의뢰취소환불CommandHandler | 조건: admin+Simulation 모드+확인 및 취소정책 / 사건 예상Revision/clientRequestId 검증<br>전이: 의뢰 취소 / 정산취소 / 배차취소 / 결제취소 또는 환불됨; 실제 PG 환불 증거 아님 | [비정상운송사건Service.cs](../../../Ssalddel/Services/Operations/비정상운송사건Service.cs) `비정상운송사건Service.검토Async` 114행<br>[관리자운송의뢰취소환불CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/관리자운송의뢰취소환불CommandHandler.cs) `관리자운송의뢰취소환불CommandHandler.Handle` 30행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 의뢰 생명 상태 | 관찰 상태축 | 생성됨 / 취소 | 해당 선언의 값 전수 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `의뢰상태` 48행 |
| 결제 상태 | 관찰 상태축 | 결제대기 / 결제완료 / 결제취소 / 환불됨 | PG 승인/환불 실효와 이 문자열의 저장은 별도<br>해당 선언의 값 전수 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `결제상태` 54행 |
| 배차 진행 투영 | 관찰 상태축 | 미시작 / 대기 / 매칭중 / 배차확정 / 상차중 / 상차완료 / 운송중 / 하차완료 / 인수완료 / 취소 | 기사 실행 코드 집합과 동일하지 않음<br>해당 선언의 값 전수 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `배차상태` 64행 |
| 정산시점 | 관찰 상태축 | 선결제 / 현장지급 / 운송완료후정산 / 월말정산 | 해당 선언의 값 전수 | [화주운송의뢰Dtos.cs](../../../Ssalddel.Contracts/Shipper/Request/화주운송의뢰Dtos.cs) `정산시점` 6행 |
| 결제수단 | 관찰 상태축 | 카드 / 가상계좌 / 계좌이체 / 현금 / 별도정산 | 해당 선언의 값 전수 | [화주운송의뢰Dtos.cs](../../../Ssalddel.Contracts/Shipper/Request/화주운송의뢰Dtos.cs) `결제수단` 14행 |
| 증빙방식 | 관찰 상태축 | 없음 / 인수증 / 현금영수증 / 세금계산서 | 해당 선언의 값 전수 | [화주운송의뢰Dtos.cs](../../../Ssalddel.Contracts/Shipper/Request/화주운송의뢰Dtos.cs) `증빙방식` 23행 |
| 수납주체 | 관찰 상태축 | 플랫폼 / 기사 / 화주직접 | 해당 선언의 값 전수 | [화주운송의뢰Dtos.cs](../../../Ssalddel.Contracts/Shipper/Request/화주운송의뢰Dtos.cs) `수납주체` 31행 |
| 운임정산 상태 | 관찰 상태축 | 정산조건작성됨 / 결제대기 / 결제완료 / 현장수금예정 / 현장수금완료 / 후불승인대기 / 후불승인완료 / 인수증대기 / 인수증등록완료 / 청구대기 / 입금대기 / 입금확인완료 / 비정상운송검토보류 / 정산완료 / 정산취소 / 미수발생 | 모든 선언값의 도달성과 모든 write 경로를 전수 검증한 것은 아님<br>해당 선언의 값 전수 | [화주운송의뢰Dtos.cs](../../../Ssalddel.Contracts/Shipper/Request/화주운송의뢰Dtos.cs) `운임정산상태` 38행 |

**LOG-SHIP-01 · P2 · provenCatalogImplementationMismatch**

카탈로그의 핵심조건 변경→새 판본·재동의 경계와 달리, 권한을 통과한 pricingChanged=false 수정분기는 수량·중량·온도·시간창 및 정산조건을 직접 수정하며 핵심조건 재동의 호출을 확인할 수 없다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `OperatingSystemLifecycleStageIds.ShipperProgressChange` 187행<br>[OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `OperatingSystemLifecycleStageIds.CargoTermsAgreement` 196행<br>[의뢰수정CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰수정CommandHandler.cs) `의뢰수정CommandHandler.PersistAsync` 81행<br>[의뢰수정CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰수정CommandHandler.cs) `의뢰수정CommandHandler.PersistAsync` 100행<br>[의뢰수정CommandHandler.cs](../../../Ssalddel/Application/Shipper/Request/Handlers/의뢰수정CommandHandler.cs) `의뢰수정CommandHandler.PersistAsync` 148행

조사 한계: 견적/현장지급예정/후불승인/인수증등록/배차/실제수납은 별도 상태 축. 공식 stage sequence를 runtime 자동 순서로 사용하지 않음

### 2. 국내 화물 운송

`DomesticCargoTransportOS` · 공통 단계 8 · 조사 절차 연결 8 · 상태묶음 14.

| 공통 책임 단계 | 정의된 책임 |
| --- | --- |
| 10 · `cargo.request` · 운송 의뢰 | 화주의 의뢰와 최초 운송 조건을 기록합니다. |
| 20 · `cargo.terms-agreement` · 조건 합의 | 기사 수락과 핵심 조건 변경 재동의를 관리합니다. |
| 30 · `cargo.dispatch` · 배차 | 후보·제안·예약·수락·거절을 조율합니다. |
| 40 · `cargo.pickup` · 상차 | 상차 준비·도착·적재와 출발 가능 상태를 관리합니다. |
| 50 · `cargo.transport` · 운송 | 경유와 운송 약속, 다음 콜 연속성을 관리합니다. |
| 60 · `cargo.dropoff` · 하차 | 도착·하차·인수 결과를 관리합니다. |
| 70 · `cargo.evidence-settlement` · 증빙·정산 | 완료 증빙과 이동·대기 보전, 정산 후보를 관리합니다. |
| 80 · `cargo.interruption-recovery` · 비정상 운송 처리·업무 회복 | 사고·고장·지연·수량 부족·파손의 검토와 안전한 재개·재배차·종료를 조율합니다. |

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 원본 의뢰 및 RDB 운송 원장 조회/연결<br>`cargo.request` | 운송의뢰배차대기Service / 화주운송의뢰화물운송인계Service | 조건: 의뢰 원천·원본 ID·업무유형을 구분; 인계 stable ID로 재전송 검증 | [화주운송의뢰화물운송인계Service.cs](../../../Ssalddel/Services/Operations/화주운송의뢰화물운송인계Service.cs) `화주운송의뢰화물운송인계Service.인계Async` 65행<br>[운송원장.cs](../../../Ssalddel.Domain/운송/운송원장.cs) `운송원장` 8행 |
| 2. 기사 수락 전 정산/예약/업무 경계 확인 및 시간약속 잠금<br>`cargo.terms-agreement` | 배차수락CommandHandler / 화물연속배차UseCase | 조건: 운송정산준비/자기수락 경계/예약Revision/TTL/추천round·공개노출/기사자격 검증 / 기존 시간약속 보존; 실제 수락 후 잠금 시도 | [배차수락CommandHandler.cs](../../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs) `배차수락CommandHandler.PersistAcceptanceAsync` 114행<br>[화물연속배차UseCase.cs](../../../Ssalddel/Services/Dispatch/Continuity/화물연속배차UseCase.cs) `화물연속배차UseCase.수락예약검증Async` 280행 |
| 3. 서버 수락으로 배차 원장/화주투영 확정<br>`cargo.dispatch` | 배차수락CommandHandler | 조건: transaction/idempotent reservation/eligible driver / 권한 및 수락가능 상태 재조회<br>전이: 배차상태 확정; 큐단계90·노출900; 화주 배차확정; postcommit 알림 | [배차수락CommandHandler.cs](../../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs) `배차수락CommandHandler.Handle` 52행<br>[배차수락CommandHandler.cs](../../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs) `배차수락CommandHandler.PersistAcceptanceAsync` 198행 |
| 4. 상차지 도착 → 상차완료 및 사진/인수증 검증<br>`cargo.pickup` | 기사운송상태전이Service / 운송상차완료CommandHandler | 조건: 도착 허용: 배차대기/매칭중/배차확정/확정/이동중 / 상차완료 허용: 상차지도착; 사진필수; 인수증 조건일 때 정책검증<br>전이: 상차지도착→상차완료; pickup departure time 설정 | [기사운송상태전이Service.cs](../../../Ssalddel/Application/Driver/Transport/Services/기사운송상태전이Service.cs) `기사운송상태전이Policy` 12행<br>[기사운송상태전이Service.cs](../../../Ssalddel/Application/Driver/Transport/Services/기사운송상태전이Service.cs) `기사운송상태전이Service.상태변경` 40행 |
| 5. 현재 운행/가능행동 투영 및 상태 변경 권한·보류 검증<br>`cargo.transport` | 기사운송상태변경CommandExecutor / 기사운송업무상태Projector | 조건: 현재 기사 업무 권한과 transport scope; 전체보류 사건이 있으면 진행차단 / 알림/표현은 server allowed actions와 재조회로 투영 | [기사운송상태변경CommandExecutor.cs](../../../Ssalddel/Application/Driver/Transport/Services/기사운송상태변경CommandExecutor.cs) `기사운송상태변경CommandExecutor.실행Async` 63행<br>[기사운송업무상태Projector.cs](../../../Ssalddel/Application/Driver/Transport/Services/기사운송업무상태Projector.cs) `기사운송업무상태Projector` 9행 |
| 6. 하차지 도착 및 사진 기반 인수완료<br>`cargo.dropoff` | 기사운송상태전이Service / 운송인수완료CommandHandler | 조건: 하차지도착은 상차완료/운송중에서만 / 인수완료는 하차지도착에서만; 하차 사진필수<br>전이: 하차지도착→인수완료; delivery time 설정; shipper projection sync | [기사운송상태전이Service.cs](../../../Ssalddel/Application/Driver/Transport/Services/기사운송상태전이Service.cs) `기사운송상태전이Policy` 25행<br>[운송인수완료CommandHandler.cs](../../../Ssalddel/Application/Driver/Transport/Handlers/운송인수완료CommandHandler.cs) `운송인수완료CommandHandler.Handle` 19행 |
| 7. 기사 증빙 완료 반환 인계 / 입금요청 / 지급준비 조회<br>`cargo.evidence-settlement` | 화물운송완료화주인수인계Service / 운송완료입금요청정책 / 기사지급준비UseCase | 조건: 완료상태·증빙·source request 및 정산시점 검증; 반환 인계만으로 정산완료 금지 / 지급준비는 예상지급운임/화주수납/사건보류/계좌/계좌검증 각각 판정<br>전이: 완료 returned→화주 acceptance 별도; ReadyForPayoutPreparation은 실제 지급 아님 | [화물운송완료화주인수인계Service.cs](../../../Ssalddel/Services/Operations/화물운송완료화주인수인계Service.cs) `화물운송완료화주인수인계Service.완료결과요청Async` 59행<br>[운송완료입금요청정책.cs](../../../Ssalddel/Application/Driver/Transport/Services/운송완료입금요청정책.cs) `운송완료입금요청정책` 9행 |
| 8. 현장문제 접수 → 비정상사건 검토 → 부분/전체 보류·재개·종료<br>`cargo.interruption-recovery` | 운송현장예외정책 / 비정상운송사건Service / 비정상운송사건운영UseCase | 조건: clientRequestId payload replay / expected Revision / operator reviewer / reason / 정상분인수+영향분보류는 수량분리와 업무불가 조건검증 / 전체보류는 진행차단; 해결 후 다른 열린 hold 없을 때 shipper settlement 복원<br>전이: OperationsReviewPending→ActionDecided/Closed / ContinueWithCaution→PartiallyHeld/FullyHeld/Resumed/Closed; settlement hold 독립 | [운송현장예외정책.cs](../../../Ssalddel/Application/Driver/Transport/Services/운송현장예외정책.cs) `운송현장예외정책.정리` 17행<br>[비정상운송사건Service.cs](../../../Ssalddel/Services/Operations/비정상운송사건Service.cs) `비정상운송사건Service.접수Async` 27행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 기사 실행 코드 | 관찰 상태축 | 배차대기 / 매칭중 / 배차확정 / 이동중 / 운송중 / 상차지도착 / 상차완료 / 하차지도착 / 인수완료 | 전이표에는 legacy '확정'도 허용; shipper 배차투영과 다른 집합<br>해당 선언의 값 전수 | [기사운송상태코드.cs](../../../Ssalddel/Application/Driver/Transport/Services/기사운송상태코드.cs) `기사운송상태코드` 3행 |
| 배차 큐 단계 | 관찰 상태축 | {"name": "계획배차", "value": 10} / {"name": "배차추천", "value": 20} / {"name": "공개배차", "value": 30} / {"name": "확정", "value": 90} / {"name": "종료", "value": 99} | 해당 선언의 값 전수 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `배차큐단계` 11행 |
| 배차 노출 상태 | 관찰 상태축 | {"name": "계획대기", "value": 100} / {"name": "계획시도중", "value": 110} / {"name": "계획실패", "value": 120} / {"name": "추천대기", "value": 200} / {"name": "추천중", "value": 210} / {"name": "추천만료", "value": 220} / {"name": "추천거절", "value": 230} / {"name": "추천후보없음", "value": 240} / {"name": "공개대기", "value": 300} / {"name": "공개중", "value": 310} / {"name": "확정", "value": 900} / {"name": "종료", "value": 990} | 해당 선언의 값 전수 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `배차노출상태` 20행 |
| 연속배차 수락예약 | 관찰 상태축 | Held / Accepted / Expired / Released / Invalidated | 해당 선언의 값 전수 | [화물연속배차Contracts.cs](../../../Ssalddel.Contracts/Common/Dispatch/화물연속배차Contracts.cs) `화물연속배차예약상태Code` 3행 |
| 시간약속 위험 | 관찰 상태축 | Normal / Watch / Critical / EvidenceMissing | 해당 선언의 값 전수 | [화물연속배차Contracts.cs](../../../Ssalddel.Contracts/Common/Dispatch/화물연속배차Contracts.cs) `화물시간위험Code` 12행 |
| 핵심조건 재동의 | 관찰 상태축 | Pending / Accepted / Rejected | 선언 전수. 검색상 실제 producer/transition 미발견<br>해당 선언의 값 전수 | [화물연속배차Contracts.cs](../../../Ssalddel.Contracts/Common/Dispatch/화물연속배차Contracts.cs) `화물조건재동의상태Code` 20행 |
| 비정상 사건 유형 | 관찰 상태축 | QuantityMismatch / CargoDamage / DropoffRecipientUnavailable | 해당 선언의 값 전수 | [비정상운송사건.cs](../../../Ssalddel.Domain/운송/비정상운송사건.cs) `비정상운송사건유형Codes` 66행 |
| 사건 검토 상태 | 관찰 상태축 | OperationsReviewPending / ActionDecided / Closed | 해당 선언의 값 전수 | [비정상운송사건.cs](../../../Ssalddel.Domain/운송/비정상운송사건.cs) `비정상운송사건상태Codes` 73행 |
| 사건 운영 제어 | 관찰 상태축 | ContinueWithCaution / PartiallyHeld / FullyHeld / Resumed / Closed | 해당 선언의 값 전수 | [비정상운송사건.cs](../../../Ssalddel.Domain/운송/비정상운송사건.cs) `비정상운송업무통제상태Codes` 83행 |
| 사건 보류 범위 | 관찰 상태축 | None / AffectedQuantity / EntireTransport | 해당 선언의 값 전수 | [비정상운송사건.cs](../../../Ssalddel.Domain/운송/비정상운송사건.cs) `비정상운송보류범위Codes` 92행 |
| 운영 해결 조치 | 관찰 상태축 | NormalAcceptedAffectedHeld / EntireTransportHeld / TransportResumed / IncidentClosed | 해당 선언의 값 전수 | [비정상운송사건.cs](../../../Ssalddel.Domain/운송/비정상운송사건.cs) `비정상운송해결결과Codes` 99행 |
| 보험 검토 | 관찰 상태축 | NotRequested / EligibilityReviewPending | 보험 승인/청구 성공 아님<br>해당 선언의 값 전수 | [비정상운송사건.cs](../../../Ssalddel.Domain/운송/비정상운송사건.cs) `비정상운송보험검토상태Codes` 107행 |
| 기사지급 준비 | 관찰 상태축 | SourceRequestMissing / ExpectedPayoutMissing / OnSiteCollectionPending / OnSiteCollectionConfirmed / ShipperCollectionPending / AbnormalTransportReviewHeld / SettlementAccountMissing / SettlementAccountUnverified / ReadyForPayoutPreparation | 해당 선언의 값 전수 | [기사지급준비Dtos.cs](../../../Ssalddel.Contracts/Driver/Settlement/기사지급준비Dtos.cs) `기사지급준비상태코드` 53행 |
| 입금 요청 종류 | 관찰 상태축 | 운송완료후정산 / 상차완료조기정산 | 해당 선언의 값 전수 | [운송완료입금요청정책.cs](../../../Ssalddel/Application/Driver/Transport/Services/운송완료입금요청정책.cs) `운송입금요청종류` 126행 |

전이·관문 대조표는 JSON의 `legalTransitions` 또는 `transitionTable`에 별도로 보존했다. 정상 경로의 표가 모든 예외·교차 상태의 자동 증명은 아니다.

조사 한계: 실행상태·배차노출·예약·조건동의·증빙·정산·사건보류 상태는 서로 독립. 운송원장은 RDB 실행 투영이며 Mongo 원본과 구분.

### 3. 창고·커머스 이행

`WarehouseCommerceFulfillmentOS` · 공통 단계 9 · 조사 절차 연결 12 · 상태묶음 14.

| 공통 책임 단계 | 정의된 책임 |
| --- | --- |
| 10 · `warehouse.inbound-plan` · 입고 예정 | 확정된 입고 요청과 운송 인계 근거를 기록하며 실제 도착이나 재고 반영을 뜻하지 않습니다. |
| 20 · `warehouse.receiving` · 도착·수령 | 입고 바코드와 도착·수령을 기록하고 검수 대기 재고 후보를 만듭니다. |
| 30 · `warehouse.inspection` · 검수 | 예정·수령 수량과 상태를 대조하고 가용·불량 수량을 명시적으로 판정합니다. |
| 40 · `warehouse.put-away-inventory` · 적재·재고 | 검수된 상품에 보관 위치를 배정하고 가용·예약 재고에 결속합니다. |
| 50 · `warehouse.outbound-allocation` · 출고 할당 | 확정된 주문 수량을 재고·출고 예정·피킹 작업에 결속하며 미할당 수량을 숨기지 않습니다. |
| 60 · `warehouse.picking` · 피킹 | 대기·진행중·완료 상태에서 위치·상품·수량 확인을 거쳐 출고 대상을 집품합니다. |
| 70 · `warehouse.packing` · 포장 | 확인된 재고나 피킹 결과를 포장하고 출고 묶음과 준비 상태를 기록합니다. |
| 80 · `warehouse.outbound-handoff` · 출고·운송 인계 준비 | 출고 결과와 기존 운송 의뢰 참조를 결속합니다. 실제 화물 OS 책임 인계나 배차 완료를 자동 확정하지 않습니다. |
| 90 · `warehouse.exception-recovery` · 수량 이상 보류·재검수 회복 | 수량 불일치 단위를 보호 보류하고 재검수·재계수 뒤 승인된 수량으로 검수 단계에 재진입시킵니다. |

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 계약/주문 또는 미예정 입고 요청 작성·수정·취소<br>`warehouse.inbound-plan` | WarehouseOperationService / 주문결제완료물류예정생성EventHandler | 조건: 계획 작성은 실물 도착·검수·재고 증거 아님; client/notice와 duplicate replay / 수정은 입고예정; 취소는 입고예정에서만, 이미취소 멱등<br>전이: 입고예정 생성; 주문결제 event는 예정 원장만 생성 | [WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.CreateInboundAsync` 379행<br>[WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.CreateUnplannedInboundRequestAsync` 430행 |
| 2. 실물 도착/입고 수량 등록 후 재고 후보 생성<br>`warehouse.receiving` | WarehouseOperationService.CompleteInboundAsync | 조건: 로그인·창고scope·Serializable transaction; 상태 입고예정 또는 운송중 / 이미 입고완료면 동일 item payload 재전송만 허용<br>전이: 입고완료 / receivedAt; 입고상품 보관중 및 수량·available·defect 초기 저장 | [WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.CompleteInboundAsync` 571행 |
| 3. 수령 수량·불량 수량 검사 및 가용 계산<br>`warehouse.inspection` | 창고작업UseCase / WarehouseOperationService | 조건: qty>0 및 <=100000; defect 유효; memo길이; warehouseScope / 상태 보관중만 최초검수; 정상수량은 기존예약보다 작을 수 없음 / 이미검수완료이면 같은qty/defect/memo만 멱등, 다른 결과 재검수는 거절<br>전이: 보관중→검수완료 또는 검수완료-불량포함; available=normal-reserved | [창고작업UseCase.cs](../../../Ssalddel/Application/Warehouse/창고작업UseCase.cs) `창고작업UseCase.입고검수Async` 264행<br>[WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.InspectInboundItemAsync` 897행 |
| 4. 새 작업 API의 검수완료 재고 적재<br>`warehouse.put-away-inventory` | 적재작업UseCase | 조건: 현장확인·위치·memo; 로그인·warehouseScope / 검수완료 prefix필수; 이미적재완료는 동일위치만 멱등<br>전이: 검수완료*→적재완료 | [적재작업UseCase.cs](../../../Ssalddel/Application/Warehouse/적재작업UseCase.cs) `적재작업UseCase.완료Async` 129행 |
| 5. 기존 적재 API의 위치 배정<br>`warehouse.put-away-inventory` | 창고작업UseCase / WarehouseOperationService | 조건: 운영사용자전용 + WarehouseManager/InventoryOperator HR 권한 및 inventoryScope / 위치필수; 현재 재고상태에 대한 검수완료 gate 없음<br>전이: 어떤 기존 inventory state든 위치를 저장하고 적재완료로 변경 가능 | [창고작업Controller.cs](../../../Ssalddel/Controllers/Common/창고작업Controller.cs) `창고작업Controller.입고검수` 299행<br>[창고작업UseCase.cs](../../../Ssalddel/Application/Warehouse/창고작업UseCase.cs) `창고작업UseCase.적재위치배정Async` 288행 |
| 6. 확정 주문의 출고예정/예약과 피킹 task 생성<br>`warehouse.outbound-allocation` | 주문결제완료물류예정생성EventHandler / 출고피킹작업생성Service | 조건: 결제완료 event의 주문상품; 기존 예정 중복확인 / 출고예정 상태 및 아직 task 없는 상품만 대상 / engine complete assignment 부족하면 저장하지 않음; SKU·위치·입고상품 결속<br>전이: 출고예정 및 피킹대기 task 투영 | [주문결제완료물류예정생성EventHandler.cs](../../../Ssalddel/Application/Warehouse/Handlers/주문결제완료물류예정생성EventHandler.cs) `주문결제완료물류예정생성EventHandler.Handle` 54행<br>[출고피킹작업생성Service.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/출고피킹작업생성Service.cs) `출고피킹작업생성Service.대기출고처리Async` 57행 |
| 7. 대기→시작→확인완료<br>`warehouse.picking` | 피킹작업UseCase | 조건: scope+currentuser; 시작은 대기(이미진행중 멱등) / 완료는 진행중; 상품/수량 확인; rackcode필수+server stored rack 일치 / 완료 재전송은 멱등<br>전이: 대기→진행중→완료 | [피킹작업UseCase.cs](../../../Ssalddel/Application/Warehouse/피킹작업UseCase.cs) `피킹작업UseCase.시작Async` 142행<br>[피킹작업UseCase.cs](../../../Ssalddel/Application/Warehouse/피킹작업UseCase.cs) `피킹작업UseCase.완료Async` 178행 |
| 8. 새 작업 API의 전체 가용재고 포장<br>`warehouse.packing` | 포장작업UseCase | 조건: 재고/포장라벨확인; 유효포장유형; login+scope / 적재완료필수; 포장수량==entire available / 이미포장완료는 같은유형+이력수량만 멱등<br>전이: 적재완료→포장완료-{type} | [포장작업UseCase.cs](../../../Ssalddel/Application/Warehouse/포장작업UseCase.cs) `포장작업UseCase.완료Async` 100행 |
| 9. 기존 포장 API<br>`warehouse.packing` | 창고작업UseCase / WarehouseOperationService | 조건: 운영사용자전용+WarehouseManager/DispatchOperator 및 inventoryScope / qty>0<=available+reserved; 적재완료/전체가용수량/라벨확인 gate 없음<br>전이: 부분수량으로도 aggregate status 포장완료-{type} 저장 | [창고작업Controller.cs](../../../Ssalddel/Controllers/Common/창고작업Controller.cs) `창고작업Controller.적재위치배정` 309행<br>[창고작업UseCase.cs](../../../Ssalddel/Application/Warehouse/창고작업UseCase.cs) `창고작업UseCase.포장작업Async` 306행 |
| 10. 포장 재고의 인계 준비 및 기존 운송의뢰 연결<br>`warehouse.outbound-handoff` | 출고인계준비UseCase / 출고예정검토UseCase / WarehouseOperationService.ReconsignmentAsync | 조건: packed prefix 및 qty==entireavailable; sameplan request/qty replay / 운송의뢰 연결은 출고준비중·해당 inventory/warehouse·qty 일치·가용재고·주소/차량/연락/시간 검증<br>전이: 출고준비중 계획과 예약; 재고부족분 없는 경우만 연결 | [출고인계준비UseCase.cs](../../../Ssalddel/Application/Warehouse/출고인계준비UseCase.cs) `출고인계준비UseCase.완료Async` 102행<br>[출고예정검토UseCase.cs](../../../Ssalddel/Application/Warehouse/출고예정검토UseCase.cs) `출고예정검토UseCase.목록Async` 32행 |
| 11. 실제 기사 인계 확인 후 원장 출고완료와 cargo child handoff<br>`warehouse.outbound-handoff` | 출고운송인계완료UseCase / 출고화물운송운영체제인계Service | 조건: 3개 현장확인 flags; login+scope; linked request; driver+registered vehicle/type; allocated quantity/reserved 검증 / 출고준비중 conditional update; 이미완료면 인계 재조회<br>전이: 출고준비중→출고완료+processedAt; reserved 차감; existing cargo request로 ChildWork accepted | [출고운송인계완료UseCase.cs](../../../Ssalddel/Application/Warehouse/출고운송인계완료UseCase.cs) `출고운송인계완료UseCase.완료Async` 39행<br>[출고운송인계완료UseCase.cs](../../../Ssalddel/Application/Warehouse/출고운송인계완료UseCase.cs) `출고운송인계완료UseCase.PersistHandoffAsync` 89행 |
| 12. 취소/멱등 충돌/수량 보호 및 공식 회복검증 정의 대조<br>`warehouse.exception-recovery` | WarehouseOperationService / catalog fixture | 조건: 입고예정 취소 및 검수결과 중복/변경 충돌; 정상수량>=예약수량 / 공식 수량불일치→hold→재검수/재계수→inspection 재진입 fixture는 실행 명령과 구분 | [WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.CancelInboundAsync` 551행<br>[WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.InspectInboundItemAsync` 929행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 입고 요청 상태 | 관찰 상태축 | 입고예정 / 운송중 / 입고완료 / 입고취소 | 해당 선언의 값 전수 | [창고분류.cs](../../../Ssalddel.Domain/창고/창고분류.cs) `입고상태` 27행 |
| 출고 계획 상태 | 관찰 상태축 | 출고예정 / 출고준비중 / 출고완료 / 출고취소 | 해당 선언의 값 전수 | [창고분류.cs](../../../Ssalddel.Domain/창고/창고분류.cs) `출고상태` 19행 |
| 피킹·포장 작업 실행 상태 | 관찰 상태축 | 대기 / 진행중 / 완료 / 취소 | 해당 선언의 값 전수 | [피킹포장작업.cs](../../../Ssalddel.Domain/창고/피킹포장작업.cs) `피킹포장작업상태` 74행 |
| 피킹·포장 작업 유형 | 관찰 상태축 | 피킹 / 포장 | 해당 선언의 값 전수 | [피킹포장작업.cs](../../../Ssalddel.Domain/창고/피킹포장작업.cs) `피킹포장작업유형` 68행 |
| 피킹 조회 필터 | 관찰 상태축 | 대기 / 진행중 / 완료 / 전체 | 전체는 조회 필터이며 실행 상태 아님<br>해당 선언의 값 전수 | [PickingTaskDtos.cs](../../../Ssalddel.Contracts/Common/Warehouse/PickingTaskDtos.cs) `피킹작업조회상태코드` 3행 |
| 검수 조회 필터 | 관찰 상태축 | 대기 / 완료 / 전체 | 해당 선언의 값 전수 | [InboundInspectionDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/InboundInspectionDtos.cs) `입고검수조회상태코드` 4행 |
| 적재 조회 필터 | 관찰 상태축 | 대기 / 완료 / 전체 | 해당 선언의 값 전수 | [PutAwayTaskDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/PutAwayTaskDtos.cs) `적재작업조회상태코드` 3행 |
| 포장 조회 필터 | 관찰 상태축 | 대기 / 완료 / 전체 | 해당 선언의 값 전수 | [PackingTaskDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/PackingTaskDtos.cs) `포장작업조회상태코드` 3행 |
| 출고 인계 준비 조회 필터 | 관찰 상태축 | 대기 / 완료 / 전체 | 해당 선언의 값 전수 | [OutboundHandoffTaskDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/OutboundHandoffTaskDtos.cs) `출고인계준비조회상태코드` 3행 |
| 출고예정 검토 조회 필터 | 관찰 상태축 | 검토 대기 / 운송 연결 / 전체 | 해당 선언의 값 전수 | [OutboundPlanReviewDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/OutboundPlanReviewDtos.cs) `출고예정검토조회상태코드` 3행 |
| 출고 검토 항목 판정 | 관찰 상태축 | 확인 완료 / 입력 필요 / 차단 | 해당 선언의 값 전수 | [OutboundPlanReviewDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/OutboundPlanReviewDtos.cs) `출고예정검토항목상태코드` 15행 |
| 재고 조회 필터 | 관찰 상태축 | 전체 / 가용 / 예약 / 위치미배정 | 해당 선언의 값 전수 | [WarehouseInventoryOverviewDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/WarehouseInventoryOverviewDtos.cs) `창고재고조회상태코드` 3행 |
| 포장 유형 | 관찰 상태축 | 일반포장 / 냉장포장 / 완충포장 | 해당 선언의 값 전수 | [PackingTaskDtos.cs](../../../Ssalddel.Contracts/Common/Inventory/PackingTaskDtos.cs) `포장유형코드` 13행 |
| 입고상품 재고 업무 상태 문자열 | 관찰 상태축 | 보관중 / 검수완료 / 검수완료-불량포함 / 적재완료 / 포장완료-{포장유형} / 재위탁대기 | 별도 enum 없는 aggregate 문자열 / 수량(입고/가용/예약/불량), 위치, 작업원장 상태와 구분<br>조사한 writer의 literal/prefix 목록; 모든 동적 문자열/전체 repo writer 전수 아님 | [WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.InspectInboundItemAsync` 897행<br>[적재작업UseCase.cs](../../../Ssalddel/Application/Warehouse/적재작업UseCase.cs) `적재작업UseCase.완료Async` 129행 |

**LOG-WH-01 · P2 · provenParallelRouteGuardMismatch**

권한 있는 기존 put-away/pack API는 새 적재/포장 UseCase의 검수완료→적재완료 및 전체가용수량 제약을 적용하지 않고 재고 aggregate 상태를 완료로 바꾼다. 기존 pack은 부분수량으로도 aggregate 포장완료를 저장한다.

근거: [창고작업Controller.cs](../../../Ssalddel/Controllers/Common/창고작업Controller.cs) `창고작업Controller` 33행<br>[창고작업Controller.cs](../../../Ssalddel/Controllers/Common/창고작업Controller.cs) `창고작업Controller.입고검수` 299행<br>[창고작업Controller.cs](../../../Ssalddel/Controllers/Common/창고작업Controller.cs) `창고작업Controller.적재위치배정` 309행<br>[창고작업UseCase.cs](../../../Ssalddel/Application/Warehouse/창고작업UseCase.cs) `창고작업UseCase.적재위치배정Async` 288행<br>[창고작업UseCase.cs](../../../Ssalddel/Application/Warehouse/창고작업UseCase.cs) `창고작업UseCase.포장작업Async` 306행

**LOG-WH-02 · P2 · sourceCoverageGap**

formal stage9가 요구하는 수량불일치의 보류→재검수/재계수→검수 재진입은 catalog 및 scripted observation fixture에 있으나 조사한 실제 warehouse command 연결을 찾지 못했다. 기존 검수완료 재고의 다른 결과는 거절된다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `OperatingSystemLifecycleStageIds.WarehouseExceptionRecovery` 229행<br>[OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `WarehouseLifecycleValidationCaseCodes.QuantityMismatchRecovery` 305행<br>[WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.InspectInboundItemAsync` 929행<br>[WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.InspectInboundItemAsync` 963행<br>[관찰운영검증Runner.cs](../../../Ssalddel/Services/Development/ObservableOperations/관찰운영검증Runner.cs) `관찰운영검증Runner.Definitions` 1014행

조사 한계: 예정·수령·검수·재고수량·작업완료·출고완료·cargo execution은 독립 원장/상태. fixture timeline은 실제 작업완료 증거 아님

### 4. 공동구매 수요·모집

`GroupPurchaseDemandOS` · 공통 단계 0 · 조사 절차 연결 6 · 상태묶음 6.

공통 stage ID는 미정의다. 아래 번호는 현재 코드에서 관찰한 절차를 읽기 위한 순서이며 새 canonical lifecycle을 등록한 것이 아니다.

등록 OS에 formal stage 정의가 없다는 사실은 기능 부재를 뜻하지 않습니다. 아래 절차와 상태 묶음은 실제 구현 관찰이며 canonical lifecycle ID를 새로 만들지 않습니다.

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 본인 비구속 수요 등록·변경 후 집단 재조율 | 주문자 UseCase → ProcessManager → Mongo 저장소 | 조건: 원천 수요 소유자 결속 / 같은 요청 fingerprint 재실행/다른 내용 충돌 / 수량·상품·배송권·예약 결제 출처 조건 검증<br>저장: Mongo 단일 집단 문서에 수요·이벤트·명령 기록; CAS/Version filter로 갱신 | [공동구매수요모집ProcessManager.cs](../../../Ssalddel/Services/Orderer/공동구매수요모집ProcessManager.cs) `수요등록조율Async` 125행<br>[공동구매자동집단화저장소.cs](../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) `수요등록Async` 84행 |
| 2. 수요 철회 후 집단 재조율 | 본인 주문자 → 저장소 | 조건: 확정 집단의 활성 수요 철회 금지 / 결제·개별 주문 원장에 연결된 수요 철회 금지 | [공동구매수요모집ProcessManager.cs](../../../Ssalddel/Services/Orderer/공동구매수요모집ProcessManager.cs) `수요철회조율Async` 139행<br>[공동구매자동집단화저장소.cs](../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) `수요철회Async` 282행 |
| 3. 모집 마감·장기 정체 점검 | Feature/설정 조건의 worker 또는 운영자 수동 재조율 | 조건: worker는 GroupPurchaseDemandWorkflow와 OS 옵션 활성 조건 / B2C 최소 인원·예약 수량·모집 종료 정책을 함께 평가 | [공동구매수요모집ProcessManager.cs](../../../Ssalddel/Services/Orderer/공동구매수요모집ProcessManager.cs) `모집마감스캔Async` 188행<br>[공동구매자동집단화저장소.cs](../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) `운영조율Async` 452행 |
| 4. 운영자 인계 승인 | 서버관리자 endpoint → ProcessManager → Mongo 저장소 | 조건: ReadyToConfirm 또는 Confirmed에서만 / actor·approval request·idempotency key 필요<br>저장: Confirmed + ApprovedAwaitingGroupPurchaseImport + 승인 actor/time + 후속 canonical OS를 기록<br>경계: 1.5 원장 생성·결제·계약·외부 실행을 자동 시작하지 않음 | [공동구매수요모집ProcessManagerAdminController.cs](../../../Ssalddel/Controllers/Admin/Orderer/공동구매수요모집ProcessManagerAdminController.cs) `인계승인` 70행<br>[공동구매자동집단화저장소.cs](../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) `인계승인Async` 584행 |
| 5. 승인된 집단의 후속 수입 준비 원장 결속 | Import 준비 서비스의 명시적 연결 → ProcessManager | 조건: CustomsAndTradeDataWorkflow 활성 / 승인 요청 일치 / 다른 원장으로 재결속 금지<br>저장: 후속 원장 ID를 기존 집단 문서에 결속 | [공동구매수요모집ProcessManager.cs](../../../Ssalddel/Services/Orderer/공동구매수요모집ProcessManager.cs) `후속원장연결Async` 283행<br>[공동구매자동집단화저장소.cs](../../../Ssalddel/Services/Orderer/공동구매자동집단화저장소.cs) `후속원장연결Async` 664행 |
| 6. OS 배치 등록 상태 조회 | 운영자 읽기 전용 catalogue | 경계: 공공 가격/기업 근거 수집 배치 등록은 모집 승인·주문 생성 완료가 아님 | [공동구매수요모집BatchCatalog.cs](../../../Ssalddel/Services/Orderer/공동구매수요모집BatchCatalog.cs) `공동구매수요모집BatchCatalog` 104행<br>[공동구매수요모집OsDtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매수요모집OsDtos.cs) `공동구매수요모집Os배치상태코드` 117행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 개별 수요 | 관찰 상태축 | Active / Withdrawn |  | [공동구매자동집단화Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매자동집단화Dtos.cs) `공동구매자동수요상태코드` 209행 |
| 수요에 연관된 결제 | 관찰 상태축 | NotPaid / Reserved / Captured | 결제 상태 표시는 이 OS에서 지급을 수행했다는 뜻이 아님 | [공동구매자동집단화Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매자동집단화Dtos.cs) `공동구매자동결제상태코드` 215행 |
| 모집 집단 | 관찰 상태축 | CollectingDemand / ReadyToConfirm / Confirmed / RecruitmentClosedTargetNotReached |  | [공동구매자동집단화Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매자동집단화Dtos.cs) `공동구매자동집단상태코드` 222행 |
| 점검/인계 큐 | 관찰 상태축 | Recruiting / ConfirmationReview / RecruitmentClosed / HandoffReady |  | [공동구매수요모집OsDtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매수요모집OsDtos.cs) `공동구매수요모집Os큐코드` 23행 |
| 인계 | 관찰 상태축 | NotRequested / AwaitingApproval / ApprovedAwaitingGroupPurchaseImport |  | [공동구매수요모집OsDtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매수요모집OsDtos.cs) `공동구매수요모집인계상태코드` 31행 |
| 배치 활성 여부 | 관찰 상태축 | ActiveInOS / RegisteredOSInactive / DisabledByConfiguration |  | [공동구매수요모집OsDtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매수요모집OsDtos.cs) `공동구매수요모집Os배치상태코드` 117행 |

조사 한계: 표준 lifecycle 미정의와 실제 Mongo 운영 상태·큐 존재를 구분 / Clustering engine Active는 외부 실행·계약 완료 증거가 아님 / 공개 투표/의사 표시와 본인 수요/개별 원함/확정 주문의 단계·소유자를 섞지 않음 / 정상 승인 후에는 후속 수입 준비 원장으로 명시적으로 인계

세부 확인이 남은 범위: 전체 주문자 UI 분기 / KAMIS/USDA/기업 근거 수집 배치 내부 / 개별 원함·개별 주문 원장의 모든 상세 전이

### 5. 같이 주문 수입

`GroupPurchaseImportOS` · 공통 단계 0 · 조사 절차 연결 7 · 상태묶음 6.

공통 stage ID는 미정의다. 아래 번호는 현재 코드에서 관찰한 절차를 읽기 위한 순서이며 새 canonical lifecycle을 등록한 것이 아니다.

등록 OS에 formal stage 정의가 없다는 사실은 기능 부재를 뜻하지 않습니다. 아래 절차와 상태 묶음은 실제 구현 관찰이며 canonical lifecycle ID를 새로 만들지 않습니다.

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 승인 수요 집단을 기존 수입 준비 원장에 결속 | 운영자 준비 원장 UseCase/Service → 커뮤니티 원장 저장소 | 조건: 1.0 인계 승인 actor/time 필수 / 기존 원장 갱신은 기대Revision 필수 / 멱등키와 요청 fingerprint 확인 / 원천집단 중복·다른 원장 연결·템플릿 충돌 검증<br>저장: 기존 community ledger의 원천/공급자/견적/도착원가/포워더/분류/규제/책임 블록 저장<br>경계: 원장의 기존 상태를 유지; 계약·결제·신고·운송 자동 실행 없음 | [같이수입준비원장Service.cs](../../../Ssalddel/Services/Orderer/같이수입준비원장Service.cs) `저장Async` 132행 |
| 2. 원장 준비 상태 조회/재평가 | 읽기/수동 요청 → 준비 ProcessManager | 조건: CustomsAndTradeDataWorkflow / manual work code·기대Revision·명령 receipt fingerprint<br>저장: trade-readiness-os-state 블록과 workload attempts/receipts 저장 | [같이수입준비ProcessManager.cs](../../../Ssalddel/Services/Orderer/같이수입준비ProcessManager.cs) `운영상태조회Async` 199행<br>[같이수입준비ProcessManager.cs](../../../Ssalddel/Services/Orderer/같이수입준비ProcessManager.cs) `작업실행Async` 210행 |
| 3. 정기 근거 freshness/견적/분류·규제/책임 점검 | 활성 옵션 worker → ProcessManager | 저장: 원장 판본에 결속한 OS state block | [같이수입준비ProcessManager.cs](../../../Ssalddel/Services/Orderer/같이수입준비ProcessManager.cs) `정기점검Async` 338행 |
| 4. 전문가 검토 인계 기록 | 사람이 검토자를 정한 뒤 운영자 → ProcessManager | 조건: 수신자·범위·메모 / 현재 revision / 준비 요건 충족<br>저장: 인계기록/actor/time/명령receipt | [같이수입준비ProcessManager.cs](../../../Ssalddel/Services/Orderer/같이수입준비ProcessManager.cs) `전문검토인계Async` 271행 |
| 5. 사람의 포워더 전달·회신 기록 | 집계 기본 패키지·명시 동의 후 최소 개인정보를 사람이 인계 | 경계: 자동 포워더 선정·외부 자동전송·계약·결제·신고·운송 지시 없음 | [같이수입준비원장Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/같이수입준비원장Dtos.cs) `같이수입준비포워더전달정보범위코드` 133행<br>[같이수입준비원장Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/같이수입준비원장Dtos.cs) `정보제공동의확인여부` 241행 |
| 6. 해외 선적 사건 등록·통관 조회 반영 | 관리자 보호 endpoint → 선적 UseCase/외부조회Service → Mongo 추적저장소 | 저장: 문서관리번호로 Mongo replace/upsert 및 event push | [공동구매해외선적추적UseCase.cs](../../../Ssalddel/Application/Orderer/공동구매해외선적추적UseCase.cs) `저장Async` 156행<br>[공동구매해외선적추적UseCase.cs](../../../Ssalddel/Application/Orderer/공동구매해외선적추적UseCase.cs) `이벤트추가Async` 171행 |
| 7. 항구·공항·보세구역 정규화 후보 조회 | Simulation helper | 경계: 조회/시뮬레이션 결과를 실제 통관·반출·배송 전이로 기록하지 않음 | [공동구매해외선적추적UseCase.cs](../../../Ssalddel/Application/Orderer/공동구매해외선적추적UseCase.cs) `수입물류정규화시뮬레이션` 106행<br>[공동구매해외선적추적UseCase.cs](../../../Ssalddel/Application/Orderer/공동구매해외선적추적UseCase.cs) `원장기반정규화시뮬레이션Async` 111행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 수입 준비 OS | 관찰 상태축 | EvidenceCollecting / EvidenceRefreshRequired / ReadyForQualifiedReview / QualifiedReviewInProgress / ReadyForNextStageHandoff |  | [같이수입준비OsDtos.cs](../../../Ssalddel.Contracts/Common/Orderer/같이수입준비OsDtos.cs) `같이수입준비Os상태코드` 6행 |
| 준비 작업 | 관찰 상태축 | Pending / Ready / Blocked / AwaitingHumanReview / InProgress / Completed / DisabledByConfiguration / Failed |  | [같이수입준비OsDtos.cs](../../../Ssalddel.Contracts/Common/Orderer/같이수입준비OsDtos.cs) `같이수입준비Os작업상태코드` 38행 |
| 근거 전문 검토 | 관찰 상태축 | Unverified / EvidenceCollected / QualifiedReviewRequired / ReviewedByQualifiedProfessional / NotApplicable |  | [같이수입준비원장Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/같이수입준비원장Dtos.cs) `같이수입준비검토상태코드` 49행 |
| 준비 원장 | 관찰 상태축 | Draft / ReadyForQualifiedReview |  | [같이수입준비원장Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/같이수입준비원장Dtos.cs) `같이수입준비원장상태코드` 84행 |
| 포워더 인계·회신 | 관찰 상태축 | Draft / ReadyForHandoff / HandoffRecorded / ResponseRecorded |  | [같이수입준비원장Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/같이수입준비원장Dtos.cs) `같이수입준비포워더인계상태코드` 117행 |
| 선적 추적 | 관찰 상태축 | DocumentRegistered / OverseasPacked / LoadedOnVesselOrFlight / InTransit / ArrivedAtPort / CustomsInProgress / CustomsCleared / LogisticsProxyInboundReady / LogisticsProxyInboundCompleted / SalesListingReady / SalesChannelListed / OutboundBatchReady / DomesticWarehouseReceived / DomesticCarrierPickup / ApartmentDropoff / DistributionInProgress / Completed / Exception | 서로 다른 하위 업무 관찰 코드들; 전체 GroupPurchaseImport canonical 순서로 승격 금지 | [공동구매해외선적추적Dtos.cs](../../../Ssalddel.Contracts/Common/Orderer/공동구매해외선적추적Dtos.cs) `공동구매선적상태코드` 15행 |

**OTHER-IMPORT-001 · P2 · TrackingCurrentStateRegression**

선적 AppendEventAsync는 발생시각과 현 상태·종료 여부를 비교하지 않고 event code를 곧바로 현재상태로 덮습니다. 늦게 접수한 과거 사건 또는 완료 후 통관 재조회가 추적 현재상태를 이전 단계로 되돌릴 수 있습니다.

근거: [공동구매해외선적추적저장소.cs](../../../Ssalddel/Services/Orderer/공동구매해외선적추적저장소.cs) `AppendEventAsync` 151행<br>[공동구매해외선적추적저장소.cs](../../../Ssalddel/Services/Orderer/공동구매해외선적추적저장소.cs) `현재상태코드` 166행<br>[공동구매해외선적통관동기화Service.cs](../../../Ssalddel/Services/Orderer/공동구매해외선적통관동기화Service.cs) `SyncAsync` 27행

조사 한계: 1.5는 기존 원장 준비 조율이며 신고·계약·결제·운송 자동실행 아님 / 근거의 전문 검토와 실제 통관 승인·현장 이행 분리 / 명시적인 aggregate/minimum-disclosure 범위와 근거 유지 / 선적 tracking status와 실제 Warehouse/Cargo/판매채널 권위 분리

세부 확인이 남은 범위: 개별/공동 수입·수출 원장 전체 조합 / 멤버십/사업자 검증 전체 / 국내 공동구매 협상·생산자 연결·fulfillment 상세 / 모든 외부 통관·가격 데이터 어댑터 / 전문가 검토 결과의 실제 현장/기관 증거

### 6. 음식 배달 OS

`FoodDeliveryOS` · 공통 단계 8 · 조사 절차 연결 42 · 상태묶음 31.

| 공통 책임 단계 | 정의된 책임 |
| --- | --- |
| 10 · `food.order` · 음식 주문 | 주문자의 주문과 주문 원장을 관리합니다. |
| 20 · `food.restaurant-response` · 음식점 응답 | 음식점 수락·거절과 조리시간 선택을 관리합니다. |
| 30 · `food.cooking` · 조리 | 조리 진행과 픽업 준비 예정·완료를 관리합니다. |
| 40 · `food.dispatch` · 배차 | 기사 후보·제안·수락·거절과 균형을 조율합니다. |
| 50 · `food.pickup` · 픽업 | 가게 도착·현장 대기·픽업을 관리합니다. |
| 60 · `food.delivery` · 전달 | 고객 전달과 수령 완료를 관리합니다. |
| 70 · `food.cancellation-compensation` · 취소·보상 | 취소·환불·음식점 보상과 사고 손실 대응을 관리합니다. |
| 80 · `food.interruption-recovery` · 중단·회복 | 조리 지연·사고·재조리·재배차와 운영자 검토를 조율합니다. |

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 공개 음식점·메뉴 조회와 주문 초안 작성<br>`food.order` | Orderer UI / 음식점탐색조회UseCase | 범위: 공개 음식점 조회 UseCase source 확인; 원본 메뉴 공개·초안 UI의 전 상태 전수 감사는 범위 밖 | [음식점탐색조회UseCase.cs](../../../Ssalddel/Application/Food/음식점탐색조회UseCase.cs) `음식점탐색조회UseCase` 34행 |
| 2. 인증 주문자·클라이언트 요청 ID·메뉴/수령인 입력 검증<br>`food.order` | 음식주문Controller → 음식주문등록CommandHandler | 조건: 서버가 주문자UserId를 인증 ID로 덮어쓴다 / Guid.Empty·음식점Id·메뉴Id·수량·필수 수령인 입력 차단 / 메뉴 검증 서비스가 서버 기준 가격/표시 스냅샷을 생성한다 | [음식주문Controller.cs](../../../Ssalddel/Controllers/Food/음식주문Controller.cs) `등록` 47행<br>[음식주문등록CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식주문등록CommandHandler.cs) `Handle` 18행 |
| 3. 주문·상품·첫 상태이력 멱등 저장 및 음식점 유효 제안 사건 기록<br>`food.order` | EfSsalddelFoodOrderStore / 음식주문등록CommandHandler | 결과: 상태=주문대기, 배차상태=미요청; 동일 주문자+요청ID는 기존 원장 반환 | [EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `멱등등록` 69행<br>[음식주문등록CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식주문등록CommandHandler.cs) `등록과음식점제안기록Async` 44행 |
| 4. 접수 결과 유실 복구와 소유 주문 재조회<br>`food.order` | 음식주문Controller / 주문자음식주문조회UseCase | 조건: 기존 요청ID 재사용 / 소유권으로 결과 조회 / 신규 등록과 조회 실패를 성공/미접수로 단정하지 않는다 | [음식주문Controller.cs](../../../Ssalddel/Controllers/Food/음식주문Controller.cs) `접수결과조회` 30행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `접수주문조회` 54행 |
| 5. 신규 주문 Event를 알림·공동 원장 후속 관심사에 전달<br>`food.order` | 음식주문등록CommandHandler | 경계: DB 저장 뒤 Publish; 영속 주문과 알림 성공은 별개 | [음식주문등록CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식주문등록CommandHandler.cs) `Handle` 18행 |
| 6. 본인 매장 수신함·상세·조리시간 참고 조회<br>`food.restaurant-response` | 음식주문Controller / 음식점접근범위Resolver | 조건: 음식점운영자전용 정책 / restaurant claim scope로 주문 대상 확인 | [음식주문Controller.cs](../../../Ssalddel/Controllers/Food/음식주문Controller.cs) `음식점수신함` 154행<br>[음식주문Controller.cs](../../../Ssalddel/Controllers/Food/음식주문Controller.cs) `음식점상세` 173행 |
| 7. 주문 수락 및 조리 계획 선택<br>`food.restaurant-response` | 음식점주문수락CommandHandler / EfSsalddelFoodOrderStore | 조건: 주문대기만 수락 / 클라이언트요청ID·인증 처리사용자 필요 / 실제 조리 시작과 음식점 수락 구분<br>결과: 주문대기→주문확인; 계획조리분 보존; 일반 주문은 조리시작/완료 시각이 아직 없다 | [음식점주문수락CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식점주문수락CommandHandler.cs) `Handle` 19행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `음식점수락멱등` 148행 |
| 8. 이미 준비된 음식의 즉시 픽업 사실 기록<br>`food.restaurant-response` | EfSsalddelFoodOrderStore.음식점수락멱등 / 음식주문현재조리Policy | 결과: 주문확인 + '음식점 주문 확인 · 기존 준비 완료' 이력; 배차 확정이나 신규 조리 시작을 뜻하지 않는다 | [EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `음식점수락멱등` 148행<br>[음식주문현재조리Policy.cs](../../../Ssalddel/Services/Food/음식주문현재조리Policy.cs) `계산` 20행 |
| 9. 수락과 배차요청 Outbox를 같은 관계형 transaction에 기록<br>`food.restaurant-response` | 음식점주문수락CommandHandler / 음식배차요청OutboxService | 결과: 수락 사건·조리 선택·배차 요청 의도 저장; 후속 큐 생성 실패를 재시도한다 | [음식점주문수락CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식점주문수락CommandHandler.cs) `AcceptAndRecordAsync` 62행<br>[음식배차요청OutboxService.cs](../../../Ssalddel/Services/Food/음식배차요청OutboxService.cs) `예약Async` 40행 |
| 10. 미수락 주문 거절과 이유·활동 사건 기록<br>`food.restaurant-response` | 음식점주문진행변경CommandHandler / 음식점주문진행Policy | 조건: 주문대기 상태 / 거절 사유 필수<br>결과: 주문대기→거절; 환불 실행과 별개 | [음식점주문진행변경CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/음식점주문진행변경CommandHandler.cs) `Handle` 19행<br>[음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) `Reject` 57행 |
| 11. 유효 기사 배정 뒤 최초 조리 시작<br>`food.cooking` | 음식점주문진행Policy / EfSsalddelFoodOrderStore | 조건: 기사배정 상태 / 배차상태=기사배정 / 큐 업무유형/원본의뢰/확정기사/현재 시도 연결 일치 / 현재 시도가 중단·완료·픽업되지 않음 / 주문확인 이력 존재 / 현재 음식 조리/준비 미시작 / 예상Revision 필수·일치<br>결과: 기사배정→조리중; 현재 시도 표시준비예정시각·Revision 갱신 | [음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) `StartCooking` 43행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `진행변경Core` 229행 |
| 12. 조리 예상 시간 변경<br>`food.cooking` | 음식점주문진행Policy.ChangePreparationTime | 조건: 주문확인·조리중·기사배정만 허용 / 분 입력 필수·정책 Clamp / 제공된 revision 충돌 차단<br>결과: 동일 주문상태 self-transition 이력; 이미 시작된 조리의 예정시각 갱신 | [음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) `ChangePreparationTime` 74행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `진행변경Core` 229행 |
| 13. 현재 음식 픽업 준비 완료 기록<br>`food.cooking` | 음식점주문진행Policy.MarkPickupReady / EfSsalddelFoodOrderStore | 조건: 현재 조리 시작/기존 준비 사실 필요 / 조리중 또는 기사배정<br>결과: 조리중→픽업대기; 기사배정일 때 주문상태를 보존하고 준비완료 이력만 기록 | [음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) `MarkPickupReady` 95행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `진행변경Core` 229행 |
| 14. 재조리 요청 이전의 준비 완료를 무효화하고 현재 음식 차수 계산<br>`food.cooking` | 음식주문현재조리Policy | 결과: Round·CookingStartedAtUtc·ReadyAtUtc·RecookingRequestedAtUtc를 불변 이력에서 파생; 픽업 전 기사 변경은 같은 음식 보존; 픽업 후 중단만 새 차수 | [음식주문현재조리Policy.cs](../../../Ssalddel/Services/Food/음식주문현재조리Policy.cs) `계산` 20행 |
| 15. 수락 의도를 배차 큐에 멱등 생성/결속<br>`food.dispatch` | 음식배차요청OutboxService / 운송의뢰배차대기Service | 조건: 원본유형=음식점주문 / 업무유형=음식배달 / 큐 의뢰ID=음식 주문번호<br>결과: 계획배차/계획대기 큐 + 주문 배차대기Id; 현재 재시도 회귀 finding 참조 | [음식배차요청OutboxService.cs](../../../Ssalddel/Services/Food/음식배차요청OutboxService.cs) `ProcessItemAsync` 147행<br>[운송의뢰배차대기Service.cs](../../../Ssalddel/Services/Dispatch/Queue/운송의뢰배차대기Service.cs) `생성또는조회Async` 70행 |
| 16. 업무 출처와 배차 엔진 분류<br>`food.dispatch` | 음식배달배차흐름Resolver / 음식배달배차엔진 | 경계: 음식점주문과 마트 라스트마일은 출처/선행 준비가 다르다; 후보 Engine은 영속 확정 권위가 아니다 | [음식배달배차흐름.cs](../../../Ssalddel/Services/Dispatch/Engine/음식배달배차흐름.cs) `Resolve` 32행<br>[음식배달배차엔진.cs](../../../Ssalddel/Services/Dispatch/Engine/음식배달배차엔진.cs) `다음후보선정Async` 34행 |
| 17. 배달권·신선한 위치·수신의사·거리·준비 시간·부하로 후보 선정<br>`food.dispatch` | 음식배달배차업무정책 | 조건: FoodDeliveryDriverApp / 운행중 / 좌표/신선도 / On + 서버 Eligible / 거절·제외기사 제외 / 허용 반경/후보 상한<br>결과: 후보 반환; 실배차 확정 없음 | [음식배달배차업무정책.cs](../../../Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs) `다음후보선정Async` 44행 |
| 18. 음식 배달 거리·요금 근거 동결 뒤 기사 제안/알림 기록<br>`food.dispatch` | 배차대기원장전환Service.시작Async | 조건: 음식 추천 기록 Port 필요 / 거리·동결 요금 확인<br>결과: 배차추천/추천중, 추천차수·대상기사·만료시각; 알림 Outbox | [배차대기원장전환Service.Recommendation.cs](../../../Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs) `시작Async` 9행 |
| 19. 참여 기사 본인 수락과 묶음 수락<br>`food.dispatch` | 음식배달기사업무Service | 조건: 실행 경계 CanConfirmDispatch / 수신 On + Eligible / 유효 대상/만료 전 / 최대 활성 배달 수 / 서로 다른 묶음2~3건과 동선/준비시각 기준 / 종료 주문 거부 / Serializable transaction<br>결과: 큐 확정·확정기사 + 시도 Accepted; 최초 주문확인→기사배정; 진행 중 재배차는 조리중/픽업대기를 보존 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `ApplyFoodOrderState` 311행 |
| 20. 제안 거절/만료 후 다음 후보 탐색<br>`food.dispatch` | 배차대기원장전환Service / 음식배달기사업무Service | 결과: 음식은 후보 없음/최대차수에서도 화물 공개배차로 승격하지 않고 재탐색 대기 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `거절Async` 387행<br>[배차대기원장전환Service.cs](../../../Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.cs) `추천만료처리Async` 184행 |
| 21. 음식 전용 큐 스캔·만료 정리·추천 알림 발송<br>`food.dispatch` | FoodDeliveryDispatch workload jobs | 경계: FoodDeliveryWorkflow/FoodDeliveryDispatchJobs 설정·업무유형 필터; 화물 workload와 분리 | [음식배달배차큐스캔Job.cs](../../../Ssalddel/Infrastructure/BackgroundJobs/FoodDeliveryDispatch/음식배달배차큐스캔Job.cs) `Execute` 21행<br>[음식배달추천만료정리Job.cs](../../../Ssalddel/Infrastructure/BackgroundJobs/FoodDeliveryDispatch/음식배달추천만료정리Job.cs) `Execute` 19행 |
| 22. 확정 기사 가게 도착·시도 revision·위치 감사 기록<br>`food.pickup` | 음식배달기사업무Service.가게도착Async | 조건: 확정기사 일치 / 클라이언트 요청ID / 제공된 시도 revision 일치<br>경계: 위치 감사는 AdvisoryEvidenceOnly; 단독 위치로 실물 인계 인증하지 않는다 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `가게도착Async` 445행 |
| 23. 현장 대기 시간 산정<br>`food.pickup` | 음식배달기사업무Service.진행상태변경Async | 결과: 픽업완료시각−가게도착시각; 별도 도착이 없으면 픽업 시각을 도착으로 대입하고 0초 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `진행상태변경Async` 665행 |
| 24. 현재 음식 준비 확인 후 픽업 완료<br>`food.pickup` | 음식배달기사업무Service.픽업완료Async | 조건: 확정기사·큐 확정/종료 경계 / 주문 기사배정 또는 픽업대기 / 현재 차수 ReadyAtUtc 존재<br>결과: 주문 픽업완료 + 배차 기사배정 + 운송 상차완료 + 시도 PickedUp; 배차상태 배달중 값은 이 경로에서 사용하지 않는다 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `픽업완료Async` 637행<br>[FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `진행상태변경Async` 665행 |
| 25. 동일 상태 픽업 재전송과 타 기사 차단<br>`food.pickup` | 음식배달기사업무Service.진행상태변경Async | 결과: 현재 목표 상태면 변경 없이 기존 응답; 현재 확정기사 외는 차단 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `진행상태변경Async` 665행 |
| 26. 픽업된 음식 고객 전달 완료<br>`food.delivery` | 음식배달기사업무Service.전달완료Async | 조건: 확정기사 소유 / 현재 주문 픽업완료<br>결과: 주문 전달완료·배차 배달완료·운송 인수완료·큐/노출 종료·시도 Completed | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `전달완료Async` 651행<br>[FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `진행상태변경Async` 665행 |
| 27. 최종 유효 배달시도 동결 대금을 완료 transaction에 기록<br>`food.delivery` | 음식주문기사정산Recorder | 조건: 음식 큐·종료·최종 유효 기사/시도 / 완료시도이며 중단 아님 / 기존 다른 시도의 정산 덮어쓰기 거부<br>결과: 주문별 단일 기사 정산; 요금 근거 없으면 BlockedMissingQuote; 기본 AwaitingReceipt | [음식주문기사정산Recorder.cs](../../../Ssalddel/Application/Food/음식주문기사정산Recorder.cs) `완료기록Async` 16행 |
| 28. 주문 소유자 수령 확인<br>`food.delivery` | 주문자음식주문수령확인CommandHandler / EfSsalddelFoodOrderStore | 조건: 주문 소유자 / 전달완료 선행 / 동일 요청/수령확인 재시도는 no-op<br>결과: 전달완료→수령확인; 같은 저장으로 정산·완료 World Projection Outbox 반영 | [주문자음식주문수령확인CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/주문자음식주문수령확인CommandHandler.cs) `Handle` 14행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `주문자수령확인` 296행 |
| 29. 완료·진행 업무 상태 사본 제공<br>`food.delivery` | 음식배달수명주기조회UseCase / 음식배달수명주기SnapshotFactory | 경계: SourceCode=OperationalServer; 주문 소유자 읽기; Unity 관찰은 상태 전이 권한 없음 | [음식배달수명주기조회UseCase.cs](../../../Ssalddel/Application/Food/음식배달수명주기조회UseCase.cs) `상세Async` 27행<br>[음식배달수명주기SnapshotFactory.cs](../../../Ssalddel/Application/Food/음식배달수명주기SnapshotFactory.cs) `FromOperationalOrder` 8행 |
| 30. 기사 본인 완료 내역·당일 정산 조회<br>`food.delivery` | FoodDeliveryDriverWorkspaceUseCase | 경계: 최종 기사 및 제한된 완료 증거 접근; 입금 완료와 별개 | [FoodDeliveryDriverWorkspaceUseCase.DailySettlement.cs](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.DailySettlement.cs) `GetDailySettlementAsync` 11행<br>[FoodDeliveryDriverWorkspaceUseCase.CompletedDetail.cs](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.CompletedDetail.cs) `GetCompletedDeliveryDetailAsync` 12행 |
| 31. 명시적 시험 공제 근거로 모의 실패·재시도·지급 성공 검증<br>`food.delivery` | 음식주문기사정산UseCase | 조건: 서버관리자 / 현재 서버 Simulation / 수령확인·최종 기사/시도·동결금액 일치 / ExpectedSettlementRevision / 멱등키/입력 일치 / 동일 근거 보존 / 기존 성공 뒤 중복 성공 차단<br>경계: 은행/PG 실송금 없음; 정산·지급 catalog stage는 아직 별도 등록되지 않음 | [음식주문기사정산UseCase.cs](../../../Ssalddel/Application/Admin/Food/음식주문기사정산UseCase.cs) `모의지급검증Async` 31행<br>[음식주문기사정산Recorder.cs](../../../Ssalddel/Application/Food/음식주문기사정산Recorder.cs) `상태반영` 71행 |
| 32. 수락 전 본인 주문 취소<br>`food.cancellation-compensation` | 주문자음식주문취소CommandHandler / EfSsalddelFoodOrderStore | 조건: 주문 소유자 / 주문대기만 / 요청ID·유효 사유 / 제공된 revision 일치<br>결과: 취소·활동 사건·Event; 음식점 수락 이후 직접 취소는 차단 | [주문자음식주문취소CommandHandler.cs](../../../Ssalddel/Application/Food/Handlers/주문자음식주문취소CommandHandler.cs) `Handle` 19행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `주문자취소` 371행 |
| 33. 음식점 거절에 따른 업무 종료<br>`food.cancellation-compensation` | 음식점주문진행Policy.Reject | 결과: 거절은 주문 종료이며 자동 환불 완료가 아니다 | [음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) `Reject` 57행 |
| 34. 기사 중단 책임 초안과 사람 검토<br>`food.cancellation-compensation` | 음식배달기사업무Service / 음식배달중단검토UseCase | 경계: 책임·균형 사건은 금전 보상·손실 지급 원장이 아니다 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `중단Async` 519행<br>[음식배달중단검토UseCase.cs](../../../Ssalddel/Application/Admin/Food/음식배달중단검토UseCase.cs) `검토Async` 27행 |
| 35. 취소/거절 후 결제 환불·음식점 보상·사고 손실 정산<br>`food.cancellation-compensation` | 연결 확인 필요(조사한 Food/payment 경로) | 경계: 범용 결제의 취소/환불 상태 상수만으로 Food lifecycle 완료를 주장하지 않는다<br>범위: 조사한 Food/payment 경로에서 실행 연결을 확인하지 못함; 외부 서비스 전체의 부재 확정 아님 | [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `FoodCancellationCompensation` 214행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `주문자취소` 371행 |
| 36. 기사 중단 사유·현재 시도·재전송 내용 확인<br>`food.interruption-recovery` | 음식배달기사업무Service.중단Async | 조건: 사유 허용 목록·메모500자 / 현재 확정기사 / 종료시도 거부 / 제공된 시도 revision / 같은 요청ID에 다른 사유/메모 거부 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `중단Async` 519행 |
| 37. 조리 지연 중단의 시간·도착·현재 준비 guard<br>`food.interruption-recovery` | 음식배달기사업무Service.중단Async | 조건: 가게 도착 후 픽업 전 / 시도 표시준비예정시각+10분 이상 / 현재 차수 준비 완료면 차단 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `중단Async` 519행 |
| 38. 픽업 전 중단의 같은 음식·조리 상태 보존<br>`food.interruption-recovery` | 음식배달기사업무Service / 음식주문현재조리Policy | 결과: 시도 Interrupted; 기사 해제·배차 재추천; 미조리→주문확인, 조리중 유지 또는 픽업대기 유지 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `중단Async` 519행<br>[음식주문현재조리Policy.cs](../../../Ssalddel/Services/Food/음식주문현재조리Policy.cs) `계산` 20행 |
| 39. 픽업 후 중단의 재조리·재배차<br>`food.interruption-recovery` | 음식배달기사업무Service / 음식주문현재조리Policy | 결과: 새 recook stable ID·현재 조리 차수 증가·이전 Ready 무효; 주문 픽업완료→조리중·배차대기; 즉시 추천 실패는 영속 큐 재시도 | [FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `중단Async` 519행<br>[음식주문현재조리Policy.cs](../../../Ssalddel/Services/Food/음식주문현재조리Policy.cs) `계산` 20행 |
| 40. 중단 책임 재검토와 균형 지표 재구성<br>`food.interruption-recovery` | 음식배달중단검토UseCase | 조건: 중단 시도만 / 요청ID/입력 일치 / 제공된 revision / Protected→Driver은 사람의 악용확정 필요<br>결과: 책임 판정 사건·revision; 단기 지표 재조회 실패는 로그 | [음식배달중단검토UseCase.cs](../../../Ssalddel/Application/Admin/Food/음식배달중단검토UseCase.cs) `검토Async` 27행 |
| 41. 업무 지연·자동 회복·기술 이상 분리 조회<br>`food.interruption-recovery` | 음식주문운영추적UseCase / 음식배달운영생명주기조화Projector | 결과: 조리/배달 지연 vs 추천만료/중단 자동회복 vs 원장 연결·Outbox·알림 기술 이상<br>경계: 지연 경고는 자동 취소/보상/배차 확정 명령 아님 | [음식주문운영추적UseCase.cs](../../../Ssalddel/Application/Admin/Food/음식주문운영추적UseCase.cs) `조회Async` 27행<br>[음식배달운영생명주기조화Projector.cs](../../../Ssalddel/Application/Admin/Food/음식배달운영생명주기조화Projector.cs) `판정` 26행 |
| 42. Outbox 재시도·lease 만료·최대 실패 상태 표시<br>`food.interruption-recovery` | 음식배차요청OutboxService / OutboxProcessingPolicy | 결과: Pending→Processing→Succeeded 또는 Pending/Failed;30초 retry·5분 lease·5회 최대<br>경계: Failed 뒤 자동 재활성화/수동 재처리 API는 이 audit에서 확인되지 않음 | [음식배차요청OutboxService.cs](../../../Ssalddel/Services/Food/음식배차요청OutboxService.cs) `ProcessItemsAsync` 93행<br>[OutboxProcessingPolicy.cs](../../../Ssalddel/Services/Outbox/OutboxProcessingPolicy.cs) `OutboxProcessingPolicy` 11행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| order | PersistedOrderState | 주문대기 / 주문확인 / 조리중 / 픽업대기 / 기사배정 / 픽업완료 / 전달완료 / 수령확인 / 거절 / 취소 | '주문접수' alias는 주문대기로 normalize. 미지원 값도 주문대기로 normalize되는 defensive risk 존재.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문Dtos.cs](../../../Ssalddel.Contracts/Food/음식주문Dtos.cs) `음식주문상태코드` 6행 |
| orderDispatch | PersistedOrderDispatchState | 미요청 / 배차대기 / 추천중 / 기사배정 / 배달중 / 배달완료 / 배차불가 | 상수 존재와 현재 모든 경로 사용을 구분. 현재 픽업완료는 기사배정을 유지하고 전달완료에서 배달완료.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문Dtos.cs](../../../Ssalddel.Contracts/Food/음식주문Dtos.cs) `음식주문배차상태코드` 58행 |
| restaurantInbox | DerivedInboxFilter | 미처리 / 완료 / 전체 | 독립 음식점 domain 상태기 아님. 전달완료·수령확인·거절·취소이면 완료; 전체는 조회 filter.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문Dtos.cs](../../../Ssalddel.Contracts/Food/음식주문Dtos.cs) `음식점주문수신함처리상태코드` 69행 |
| restaurantOperations | CommandCode | 조리시작 / 거절 / 조리시간변경 / 픽업준비 | 주문 수락은 별도 route/Command.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문Dtos.cs](../../../Ssalddel.Contracts/Food/음식주문Dtos.cs) `음식점주문진행작업코드` 93행 |
| currentPreparation | DerivedPreparationState | Round >= 1 / CookingStartedAtUtc:null\|value / ReadyAtUtc:null\|value / RecookingRequestedAtUtc:null\|value | 조리중 문자열 하나로 음식 첫차수/재조리·준비완료를 확정하지 않음. 이력과 재조리 경계 사용.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문현재조리Policy.cs](../../../Ssalddel/Services/Food/음식주문현재조리Policy.cs) `음식주문현재조리상태` 6행<br>[음식주문현재조리Policy.cs](../../../Ssalddel/Services/Food/음식주문현재조리Policy.cs) `계산` 21행 |
| preparationTimeSource | DecisionProvenance | RestaurantExplicit / PlatformObservedAverage / RestaurantSetting / SystemDefault / ImmediatePickup | 실제 음식점 선택·관측·설정·기본·즉시준비의 근거 출처.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식조리배달운영Dtos.cs](../../../Ssalddel.Contracts/Food/음식조리배달운영Dtos.cs) `음식조리시간결정출처Code` 3행 |
| deliveryAttempt | PersistedAttemptState | Accepted / RestaurantArrived / PickedUp / Interrupted / Completed | 주문번호 아래 시도순번·기사·제안/라운드·stable ID·revision을 따로 보존.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식조리배달운영Dtos.cs](../../../Ssalddel.Contracts/Food/음식조리배달운영Dtos.cs) `음식배달시도상태Code` 45행 |
| interruptionReason | CommandCode | Accident / RestaurantPreparationDelay / BatteryLow / VehicleBreakdown / UnsafeWeather / PersonalUrgency / Other | 조리지연은 픽업전/도착/예정+10분/미준비 guard; 기타 사유는 보호 정책으로 책임 초안 판정.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식조리배달운영Dtos.cs](../../../Ssalddel.Contracts/Food/음식조리배달운영Dtos.cs) `음식배달중단사유Code` 54행 |
| interruptionReview | ReviewCommandCode | Protected / DriverResponsible / RestaurantResponsible / PlatformResponsible | 검토 입력 code는 영속 책임 code와 다름.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식조리배달운영Dtos.cs](../../../Ssalddel.Contracts/Food/음식조리배달운영Dtos.cs) `음식배달중단검토판정Code` 101행 |
| interruptionResponsibility | PersistedResponsibilityCode | Driver / Restaurant / Orderer / Platform / System / Protected / Unresolved / NotApplicable | 공유 책임 code 전체; 각 값의 음식 사용 여부는 별도.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [운영배차공통Contracts.cs](../../../Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs) `운영배차책임Code` 52행 |
| dispatchQueueStage | PersistedSharedQueueStage | {"name": "계획배차", "value": 10} / {"name": "배차추천", "value": 20} / {"name": "공개배차", "value": 30} / {"name": "확정", "value": 90} / {"name": "종료", "value": 99} | 음식은 공개배차30로 전환하지 않는다. 실제 음식 subset과 공유 전체 정의 구분.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `배차큐단계` 11행 |
| dispatchExposure | PersistedSharedExposureState | {"name": "계획대기", "value": 100} / {"name": "계획시도중", "value": 110} / {"name": "계획실패", "value": 120} / {"name": "추천대기", "value": 200} / {"name": "추천중", "value": 210} / {"name": "추천만료", "value": 220} / {"name": "추천거절", "value": 230} / {"name": "추천후보없음", "value": 240} / {"name": "공개대기", "value": 300} / {"name": "공개중", "value": 310} / {"name": "확정", "value": 900} / {"name": "종료", "value": 990} | 공유 상태 전체12개. Food 후보없음은 추천후보없음240·배차추천20에서 재탐색.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `배차노출상태` 20행 |
| transportProgress | PersistedSharedTransportState | 미시작 / 대기 / 매칭중 / 배차확정 / 상차중 / 상차완료 / 운송중 / 하차완료 / 인수완료 / 취소 | 공유 문자열 전체; Food driver 실제 경로는 큐 '확정'(별도 배차대기상태), 픽업 '상차완료', 전달 '인수완료'.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `배차상태` 64행 |
| queueWait | PersistedSharedQueueWaitState | 대기 / 확정 | 운송원장.상태는 초기/확정과 운송 상태 문자열을 함께 사용한다.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `배차대기상태` 42행 |
| driverOfferProjection | ReadProjection | Recommended / Accepted / MovingToPickup / PickupConfirmed / MovingToDropoff / Completed / Rejected | 공유 DTO 상태; Food 중단 response에는 catalog에 없는 literal 'Interrupted'도 사용되므로 DTO 그룹과 배달시도 그룹 혼동 금지.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [DriverWorkDtos.cs](../../../Ssalddel.Contracts/Common/Drivers/DriverWorkDtos.cs) `DriverWorkOfferStatus` 6행 |
| driverReceivingIntent | PersistedOperationalIntent | On / Off | Off는 새 추천수락 차단; 이미 수락된 업무·도착·중단은 유지.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [운영배차공통Contracts.cs](../../../Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs) `운영배차수신의사Code` 5행 |
| driverReceivingEffective | PersistedOperationalEligibility | Eligible / PausedByServer / Ineligible / ConnectionUnavailable | 기사 On만으로 배차확정 권한 생기지 않음.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [운영배차공통Contracts.cs](../../../Ssalddel.Contracts/Common/Dispatch/운영배차공통Contracts.cs) `운영배차실효상태Code` 11행 |
| driverStageUi | UiProjectionEnum | Waiting / PickupTravel / PickupWaiting / Delivery | 서버 가능행동/현재 시도/도착/전달 상태를 해석하는 화면 단계; 새 domain 상태 권위 아님.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [FoodDriverStagePresentation.cs](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverStagePresentation.cs) `FoodDriverStage` 8행 |
| paymentNumeric | PersistedSharedPaymentState | {"name": "요청생성", "value": 10} / {"name": "결제창진입", "value": 20} / {"name": "승인대기", "value": 30} / {"name": "승인완료", "value": 40} / {"name": "실패", "value": 50} / {"name": "취소요청", "value": 60} / {"name": "취소완료", "value": 70} / {"name": "환불완료", "value": 80} | Food 승인 소비자는 승인완료40+legacy 결제완료+취소일시 없음 정본 요구. 정의된 취소/환불 값이 Food 실행 연결을 증명하지 않음.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [결제공통정의.cs](../../../Ssalddel.Domain/결제/결제공통정의.cs) `결제상태` 25행 |
| paymentLegacy | PersistedSharedPaymentState | 결제대기 / 결제완료 / 결제취소 / 환불됨 | 허용값 배열은 결제대기·결제완료·환불됨3개만 포함하여 정의값4개와 다름. 이 감사에서는 공통 결제 정책 전수를 검토하지 않음.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [상태값.cs](../../../Ssalddel.Domain/공통/상태값.cs) `결제상태` 54행 |
| paymentApprovalLink | HistoricalApprovalFacts | 결제승인Id:null\|value / 결제승인금액:null\|value / 결제승인통화:null\|value / 결제승인시각Utc:null\|value | 음식주문의 과거 승인사실. 환불잔액·현재결제상태·음식점정산·기사실지급을 뜻하지 않음.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문.cs](../../../Ssalddel.Domain/음식/음식주문.cs) `결제승인Id` 89행<br>[음식주문결제승인OutboxService.cs](../../../Ssalddel/Services/Payments/음식주문결제승인OutboxService.cs) `승인반영Async` 127행 |
| driverSettlement | PersistedOrderSettlementState | AwaitingReceipt / AwaitingDeductions / ReadyForSimulation / BlockedMissingQuote | 배송 종료와 금융종료 별개. 근거없는 금액은 null; 완료 당시 동결 요금 사용.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문기사정산.cs](../../../Ssalddel.Domain/음식/음식주문기사정산.cs) `음식주문기사정산상태Code` 52행 |
| driverPayout | PersistedSimulatedPayoutState | NotRequested / SimulationSucceeded / SimulationFailed | 실송금 완료 상태 없음. 실패 증빙을 보존하고 같은 공제 근거로 재시도.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문기사정산.cs](../../../Ssalddel.Domain/음식/음식주문기사정산.cs) `음식주문기사지급상태Code` 60행 |
| operationalStage | AdminReadProjection | AwaitingDriverAssignment / RestaurantDecision / CookingAndDispatch / PickupHandoff / Delivery / ReceiptConfirmation / Closed | catalog food.* stage IDs와 다른 조회용 단계. 기사배정은 PickupHandoff로 매핑되므로 조리전/조리후는 다른 조리 facts를 함께 읽어야 한다.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문운영추적Dtos.cs](../../../Ssalddel.Contracts/Admin/Food/음식주문운영추적Dtos.cs) `음식배달운영생명주기단계Codes` 15행 |
| operationalAttention | AdminReadProjection | Normal / Attention / AutomaticRecovery / OperatorReviewRequired | 업무상태/자동회복/통신·저장 기술 이상을 구분.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문운영추적Dtos.cs](../../../Ssalddel.Contracts/Admin/Food/음식주문운영추적Dtos.cs) `음식배달운영주의상태Codes` 35행 |
| operationalDelay | AdminReadProjection | None / Attention / OperatorReviewRequired | 조리 예정 또는 픽업+42분 기준 초과5분 주의/10분 운영자 검토; 계약된 개별 고객 전달 마감은 아직 별도 동결되지 않음.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문운영추적Dtos.cs](../../../Ssalddel.Contracts/Admin/Food/음식주문운영추적Dtos.cs) `음식배달운영지연상태Codes` 43행 |
| operationalDelayReason | AdminReadProjection | RestaurantPreparationDelay / DeliveryProgressDelay | 해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문운영추적Dtos.cs](../../../Ssalddel.Contracts/Admin/Food/음식주문운영추적Dtos.cs) `음식배달운영업무지연Codes` 50행 |
| automaticRecoveryReason | AdminReadProjection | DriverRecommendationExpired / LatestDeliveryAttemptInterrupted | 해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문운영추적Dtos.cs](../../../Ssalddel.Contracts/Admin/Food/음식주문운영추적Dtos.cs) `음식배달운영자동회복Codes` 56행 |
| technicalExceptionReason | AdminReadProjection | DispatchLedgerMissing / DispatchCorrelationMismatch / SharedLedgerOutboxReviewRequired / DriverNotificationOutboxReviewRequired | 해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문운영추적Dtos.cs](../../../Ssalddel.Contracts/Admin/Food/음식주문운영추적Dtos.cs) `음식배달운영기술이상Codes` 62행 |
| outbox | PersistedAsyncProcedureState | Pending / Processing / Succeeded / Failed | Command processing/알림/원장 투영 상태는 업무 주문 상태와 독립.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [OutboxProcessingPolicy.cs](../../../Ssalddel/Services/Outbox/OutboxProcessingPolicy.cs) `OutboxProcessingStatuses` 3행 |
| locationTracking | PrivacyBoundedReadProjection | 추적전 / 추적중 / 갱신지연 / 종료 | 주문 소유자만; 완료/취소 이후 현재 좌표 제거.<br>해당 명명된 상수 그룹의 값은 전부 포함; 전체 저장소 모든 enum/string 값 전수는 아님 | [음식주문Dtos.cs](../../../Ssalddel.Contracts/Food/음식주문Dtos.cs) `음식배달위치추적상태코드` 302행 |

전이·관문 대조표는 JSON의 `legalTransitions` 또는 `transitionTable`에 별도로 보존했다. 정상 경로의 표가 모든 예외·교차 상태의 자동 증명은 아니다.

**FOOD-R23-001 · P1 · ProvenStaticRegressionPath_RuntimeReproductionNotPerformed**

배차 요청 Outbox의 후속 동기화 실패 후 재시도가 이미 확정/진행/완료된 주문의 배차상태를 배차대기로 회귀시킬 수 있다.

조건: 첫 ProcessItemAsync에서 큐 생성 저장(191)와 주문 연결 저장(193→store447)이 완료된다. / transportLedgerSync(195) 또는 foodLedgerOutbox(196)에서 예외가 발생한다. ProcessItemsAsync는 전체 transaction 없이 item을 Pending으로 남긴다(129~131). / 30초 retry 이전에 기사 본인이 제안을 수락해 큐와 주문 배차상태를 확정한다. / 재시도는 과거 payload.Order를 사용하고 current order/queue 상태 early-return 없이 기존 큐를 재조회한다(운송의뢰배차대기Service79~85). / EfSsalddelFoodOrderStore.배차대기반영은 상태 guard 없이 주문 배차상태를 배차대기로 덮어쓴다(442).

근거: [음식배차요청OutboxService.cs](../../../Ssalddel/Services/Food/음식배차요청OutboxService.cs) `ProcessItemsAsync` 93행<br>[음식배차요청OutboxService.cs](../../../Ssalddel/Services/Food/음식배차요청OutboxService.cs) `ProcessItemAsync` 147행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `배차대기반영` 428행<br>[운송의뢰배차대기Service.cs](../../../Ssalddel/Services/Dispatch/Queue/운송의뢰배차대기Service.cs) `생성또는조회Async` 70행<br>[FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `수락목록Async` 193행

보완: 주문-큐 결속 자체를 멱등하게 만들고 이미 같은 큐가 결속된 경우 진행 상태·최초 배차 요청시각을 보존한다. 후속 동기화 재시도는 업무 상태 변경과 분리한다. 후속 sync 한 번 실패→새 Context 기사 수락→새 Context retry→주문/큐 상태 보존 회귀시험을 추가한다.

실행 증거: 미수행; 코드의 조건·저장 순서로 가능한 경로를 확인

**FOOD-R23-002 · P1 · CatalogProcedureConnectionConfirmationRequired**

취소·보상 단계의 환불·음식점 보상·사고 손실 조정은 조사한 Food 경로에서 실행 연결 확인이 필요하다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `FoodCancellationCompensation` 214행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `주문자취소` 371행<br>[음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) `Reject` 57행<br>[음식배달중단검토UseCase.cs](../../../Ssalddel/Application/Admin/Food/음식배달중단검토UseCase.cs) `검토Async` 27행<br>[음식주문결제승인OutboxService.cs](../../../Ssalddel/Services/Payments/음식주문결제승인OutboxService.cs) `승인반영Async` 127행

보완: catalog가 선언한 책임과 실제 지원 절차를 나눈 상태표를 만들고 수락 후 중지·환불·보상·손실 조정·사람 검토의 owner/원장/guard/멱등키를 명세한다. 현재 직접취소 제한을 제품 버그로 확대해 해제하지 않는다.

실행 증거: 실제 금융·환불 실행 검증 없음

**FOOD-R23-003 · P2 · CatalogGap_FinancialLifecycle**

Food lifecycle8단계에는 결제·기사 정산·지급의 독립 책임 단계/종료 의미가 없다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `FoodDelivery` 213행<br>[OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `FoodInterruptionRecovery` 215행<br>[음식주문결제승인OutboxService.cs](../../../Ssalddel/Services/Payments/음식주문결제승인OutboxService.cs) `승인반영Async` 127행<br>[음식주문기사정산Recorder.cs](../../../Ssalddel/Application/Food/음식주문기사정산Recorder.cs) `완료기록Async` 16행<br>[음식주문기사정산UseCase.cs](../../../Ssalddel/Application/Admin/Food/음식주문기사정산UseCase.cs) `모의지급검증Async` 31행

보완: 기존 stable ID를 보존하며 결제 승인 사실·환불/조정·기사정산·음식점정산·지급 책임을 명시적으로 분리하거나 정의되지 않은 책임으로 표시한다. Closed는 주문 종료로 한정하고 금융종료와 구별한다.

**FOOD-R23-004 · P2 · CatalogOrderingAmbiguity_NotTransitionBug**

FoodCooking30→FoodDispatch40라는 카탈로그 Order는 실제 운영 실행 선후행을 표현하지 않는다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `FoodCooking` 210행<br>[OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `FoodDispatch` 211행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `음식점수락멱등` 148행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `유효한배차확정` 554행<br>[음식점주문진행Policy.cs](../../../Ssalddel/Services/Food/음식점주문진행Policy.cs) `StartCooking` 43행

보완: catalog Order를 책임 표시 순서로 정의하고 실제 transition graph·선행 guard·병행 state axes를 별도 명시한다. catalog8단계를 직선 실행기나 완성률로 소비하지 않는다.

**FOOD-R23-005 · P2 · AuthenticatedFoodPaymentPreparationConnectionConfirmationRequired**

Food 승인 연결 consumer는 구현됐지만 공통 준비 API는 용달운송의뢰만 지원하여 인증 Food 결제 준비 연결 확인이 필요하다.

근거: [음식주문결제승인OutboxService.cs](../../../Ssalddel/Services/Payments/음식주문결제승인OutboxService.cs) `승인반영Async` 127행<br>[음식주문결제승인완료EventHandler.cs](../../../Ssalddel/Application/Shipper/Payment/Handlers/음식주문결제승인완료EventHandler.cs) `Handle` 18행<br>[공통결제준비CommandHandler.cs](../../../Ssalddel/Application/Shipper/Payment/Handlers/공통결제준비CommandHandler.cs) `Handle` 15행<br>[토스결제준비CommandHandler.cs](../../../Ssalddel/Application/Shipper/Payment/Handlers/토스결제준비CommandHandler.cs) `Handle` 30행<br>[음식주문결제승인OutboxServiceTests.cs](../../../Ssalddel.Tests/Services/Payments/음식주문결제승인OutboxServiceTests.cs) `SeedAsync` 355행

보완: 기존 approved-payment 소비자를 재사용하되 음식 주문 소유권·현재 payable 상태·서버 정본금액·provider·금액 변경/중복 준비·취소 경쟁을 검증하는 독립 결제 준비 절차를 정의한다. 현 상태를 운영 PG 결제 완료 기능으로 보고하지 않는다.

실행 증거: 외부 PG·환불/실입금 실행 없음; 조사한 API 경로의 지원 범위와 fixture seed만 정적 확인. 다른 provider/service 또는 별도 배포 연결의 전수 부재를 확정하지 않음.

**FOOD-R23-006 · P2 · DefensiveValidationRisk_StoredCorruptionNotObserved**

미지원 주문 상태 문자열이 주문대기로 normalize되어 가능행동·수락/취소 guard의 유효 초기 상태로 읽힌다.

근거: [음식주문Dtos.cs](../../../Ssalddel.Contracts/Food/음식주문Dtos.cs) `Normalize` 33행<br>[음식배달업무상태전이Guard.cs](../../../Ssalddel/Services/Food/음식배달업무상태전이Guard.cs) `판정` 9행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `음식점수락멱등` 148행<br>[EfSsalddelFoodOrderStore.cs](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs) `주문자취소` 371행<br>[음식배달가능행동Projector.cs](../../../Ssalddel/Application/Food/음식배달가능행동Projector.cs) `음식배달가능행동Projector` 7행

보완: 읽기 alias 호환과 잘못된 상태 fail-closed를 분리하고 mutation 진입에서 지원여부를 검사한다. 미지원 raw 상태를 초기상태로 변경하지 않는 회귀시험 후보.

실행 증거: 실제 손상 상태의 발생/주문 변조를 관찰하지 않았으므로 현재 사용자 버그로 단정하지 않음

**FOOD-R23-007 · P3 · HistoricalDocumentSupersessionGap**

2026-09-27 음식점 생명주기 감사 r3의 결제 소비자 없음·기사 배달대금 정산 없음 서술은 현재 코드와 달라졌다.

근거: [restaurant-lifecycle-audit.r3.md](../../../docs/AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/restaurant-lifecycle-audit.r3.md) `결제` 15행<br>[app-gap-improvements-r1.md](../../../docs/ProjectOverview/page-docs/app-gap-improvements-r1.md) `주문별 정산·모의 지급` 26행<br>[음식주문결제승인OutboxService.cs](../../../Ssalddel/Services/Payments/음식주문결제승인OutboxService.cs) `승인반영Async` 127행<br>[음식주문기사정산Recorder.cs](../../../Ssalddel/Application/Food/음식주문기사정산Recorder.cs) `완료기록Async` 16행<br>[음식주문기사정산UseCase.cs](../../../Ssalddel/Application/Admin/Food/음식주문기사정산UseCase.cs) `모의지급검증Async` 31행

보완: 이력 문서를 덮어쓰지 말고 최신 감사/구현 문서로의 superseded/current 포인터를 추가하여 재사용 시 과거 결손을 현행 결손으로 읽지 않도록 한다.

조사 한계: 제품 코드·설정·DB·운영 원장·영상은 수정하지 않은 정적 소스·문서 조사다. 관련 시험과 새 정적 조사 도구의 중앙 검증은 validation에 별도로 기록했다. / 분담 소스 조사에서 제품 API·DB 실행은 수행하지 않았다. 중앙에서 기존 관련 시험을 실행했으며 실제 제품 UI·HTTP·DB 업무 완주, Android/휴대폰/GPS·PG/입금·Unity·운영 자동배차는 미실행이다. / 공유 dirty worktree dev/mirror-integration의 현재 작업 사본 기준. HEAD-only 구현으로 주장하지 않으며 관련 없는 변경을 보존. / 카탈로그 Food8단계는 OS 전생명주기 책임 분류이며 단일 executor·실행 graph·완성률이 아님. / 명명된 주요31개 상태/코드/조회그룹의 각 상수는 포함했지만 저장소 모든 enum/string literal·전 legacy endpoint·전 UI 상태의 절대 전수 감사는 아님. / Food restaurant flow를 집중 검토. Mart last-mile은 별도 OS 책임과 자식 업무 인계 경계만 대조; 실제 Mart driver adapter/원장 closure는 다른 감사에서 통합. / 주문 delivery terminal, 음식점 inbox complete, queue terminal, delivery attempt terminal, payment approval facts, driver settlement, simulated payout, UI runtime/auth/network 상태를 각각 독립 취급. / OperationalServer source는 운영 주문 원장 소유권 표시이며 execution mode=Operational 실업무 운영 완료를 뜻하지 않음. Development/Simulation 호스트에서도 운영 API 계층의 fixture 원장을 사용할 수 있음. / 음식배달후보 Engine·scheduler metadata는 후보·정책 책임; UseCase/Service+transaction이 영속 권위. Unity/Simulation 표현이나 공유 상태규칙은 실제 주문·배차 확정 권위 없음. / 실행 증거가 명시된 기존 문서는 날짜/fixture/API/기기/PG 경계를 보존하며 이번 static 감사의 신규 실행 결과로 합산하지 않음. / 실제 금융 법률 적용·보험료·요율·시장 운영 적법성 검토는 범위 밖. 사용자 배달영상 정산 기준을 제품 정산 정책으로 이식하지 않음. / 외부 결제·환불·음식점 판매대금/보상 연결은 명시한 Food/payment 검색 경로에서 확인하지 못한 연결 확인 필요 항목. 외부 시스템 전체 또는 저장소 모든 서비스의 절대 부재로 확정하지 않음.

### 7. 살뜰마트 도심 물류

`SsalddelMartUrbanLogisticsOS` · 공통 단계 8 · 조사 절차 연결 9 · 상태묶음 9.

| 공통 책임 단계 | 정의된 책임 |
| --- | --- |
| 10 · `mart.supply-agreement` · 공급계약 이용 | 플랫폼 공급계약 중 각 마트가 이용할 계약과 품목 범위를 등록합니다. |
| 20 · `mart.replenishment-order` · 점포별 입고 발주 | 마트별 필요 수량과 납기 요청을 기록하며 공급자의 수락 수량을 보존합니다. |
| 30 · `mart.inbound-receiving` · 입고·검수 | 도착 수량과 상태를 검수하고 확인된 재고 후보를 만듭니다. |
| 40 · `mart.putaway-inventory` · 적재·재고 | 검수된 상품을 적재 위치와 가용 재고에 결속합니다. |
| 50 · `mart.customer-order-allocation` · 고객 주문·할당 | 확정된 고객 주문을 점포 재고와 출고 작업에 결속합니다. |
| 60 · `mart.picking-packing` · 피킹·포장 | 적재 위치에 근거한 피킹과 포장, 픽업 준비 시각을 관리합니다. |
| 70 · `mart.last-mile-handoff` · 라스트마일 인계 | 마트 주문과 분리된 배송 자식 업무를 음식배달 OS에 명시적으로 인계합니다. |
| 80 · `mart.completion-recovery` · 완료·회복 | 배송 증거를 주문에 반영하고 품절·지연·재배차·취소 복구를 관리합니다. |

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 공급 플랫폼 중개계약 작성/활성 및 매장 참여<br>`mart.supply-agreement` | 플랫폼공급계약관리UseCase / 조직개별공급발주UseCase | 조건: platformoperator/organization scope; current document noticeconsent; active usableagreement+item범위 / 활성은 suppliercontractconfirmed+brokerconfirmed 및 Draft expectedstate<br>전이: Draft→Active; 조직 participation Active | [플랫폼공급계약관리UseCase.cs](../../../Ssalddel/Application/ContractManagement/플랫폼공급계약관리UseCase.cs) `플랫폼공급계약관리UseCase.등록Async` 40행<br>[플랫폼공급계약관리UseCase.cs](../../../Ssalddel/Application/ContractManagement/플랫폼공급계약관리UseCase.cs) `플랫폼공급계약관리UseCase.활성화Async` 148행 |
| 2. 매장 개별 발주와 공급자 수락/부분수락/거절<br>`mart.replenishment-order` | 조직개별공급발주UseCase / 플랫폼공급계약관리UseCase | 조건: logged organization scope/activeparticipation/currentdocument/itemscope/minmaxqty / clientGuid payload replay; response onlySubmittedToSupplier, expectedstatus·evidence·confirmed flag / full acceptedQty==requested; partial 0<acceptedQty<requested; rejectQty0<br>전이: SubmittedToSupplier→SupplierAccepted/SupplierPartiallyAccepted/SupplierRejected; submitted only→Withdrawn | [조직개별공급발주UseCase.cs](../../../Ssalddel/Application/ContractManagement/조직개별공급발주UseCase.cs) `조직개별공급발주UseCase.발주등록Async` 185행<br>[플랫폼공급계약관리UseCase.cs](../../../Ssalddel/Application/ContractManagement/플랫폼공급계약관리UseCase.cs) `플랫폼공급계약관리UseCase.공급자응답기록Async` 213행 |
| 3. 마트 창고 입고 수령과 수량 검수<br>`mart.inbound-receiving` | WarehouseOperationService / 창고작업UseCase | 조건: warehouseScope/입고예정 또는 운송중/동일payload replay / 보관중 검수; defect/예약수량 보호<br>전이: 입고완료→재고후보 보관중→검수완료* | [WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.CompleteInboundAsync` 571행<br>[WarehouseOperationService.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs) `WarehouseOperationService.InspectInboundItemAsync` 897행 |
| 4. 검수 재고 적재/위치·가용·예약 투영<br>`mart.putaway-inventory` | 적재작업UseCase / 재고현황UseCase | 조건: 새작업 검수완료 gate/warehouseScope; location 확인 / 가용/예약/불량 수량 구분<br>전이: 검수완료*→적재완료 | [적재작업UseCase.cs](../../../Ssalddel/Application/Warehouse/적재작업UseCase.cs) `적재작업UseCase.완료Async` 129행<br>[재고현황UseCase.cs](../../../Ssalddel/Application/Warehouse/재고현황UseCase.cs) `재고현황UseCase` 31행 |
| 5. 커뮤니티/주문 원장의 마트·출고상품 투영<br>`mart.customer-order-allocation` | FoodMartLedgerMongoSyncService / 주문결제완료물류예정생성EventHandler | 조건: Mongo source와 order/ledger reference 결속; confirmed payment event가 예정 생성 / 실제 재고배분은 inventory/outbound/picking 원장 조건에 따름 | [FoodMartLedgerMongoSyncService.cs](../../../Ssalddel/Services/Community/FoodMartLedgerMongoSyncService.cs) `음식마트원장Mongo동기화Service.마트주문투영Async` 108행<br>[주문결제완료물류예정생성EventHandler.cs](../../../Ssalddel/Application/Warehouse/Handlers/주문결제완료물류예정생성EventHandler.cs) `주문결제완료물류예정생성EventHandler.Handle` 21행 |
| 6. 일반 사용자 마트 주문 요청 접수(비구속 의향)<br>`mart.customer-order-allocation` | 마트주문요청작성UseCase | 조건: loggeduser/clientGuid/noticeconsent/product salepermission/qty1..100/표시available 확인 / 수량변경/철회는 owner+expectedstate; Submitted만 변경<br>전이: Submitted→Withdrawn; 결제/재고예약/출고 자동 실행 없음 | [마트주문요청작성UseCase.cs](../../../Ssalddel/Application/Mart/마트주문요청작성UseCase.cs) `마트주문요청작성UseCase.등록Async` 37행<br>[마트주문요청작성UseCase.cs](../../../Ssalddel/Application/Mart/마트주문요청작성UseCase.cs) `마트주문요청작성UseCase.수량변경Async` 147행 |
| 7. warehouse 피킹/포장 작업과 마트 주문 조회·공간투영<br>`mart.picking-packing` | 피킹작업UseCase / 포장작업UseCase / 마트피킹조회UseCase / 마트피킹포장WorldProjector | 조건: 작업scope+확인상품/수량/랙; packing newroute는 적재완료+전체가용수량 / WorldProjector는 완료된 작업 상태 읽기만; 실제 작업수행 아님 | [피킹작업UseCase.cs](../../../Ssalddel/Application/Warehouse/피킹작업UseCase.cs) `피킹작업UseCase.완료Async` 178행<br>[포장작업UseCase.cs](../../../Ssalddel/Application/Warehouse/포장작업UseCase.cs) `포장작업UseCase.완료Async` 100행 |
| 8. 포장완료 판정 후 별도 음식배달 child queue와 명시 인계<br>`mart.last-mile-handoff` | 알뜰살뜰마트배차대기Service / 살뜰마트라스트마일배차Policy / 살뜰마트라스트마일배차인계Service | 조건: 주문참조/출고예정/포장준비/상차주소/배송지 필요; acceptedassignment 보호 / sourceRevision>=0·품목수량>0·pure policy candidate; 제안대기이면 handoff만 만들고 queue 없음 / 제안시 음식배달 업무유형+SsalddelMartPackedOrder 원본으로 운송원장 생성/조회<br>전이: 마트주문 포장 완료 / 출고준비중; ChildWork accepted→food-delivery-dispatch-request:{id} | [알뜰살뜰마트배차대기Service.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/알뜰살뜰마트배차대기Service.cs) `알뜰살뜰마트배차대기Service.주문포장완료후배차대기생성Async` 98행<br>[살뜰마트라스트마일배차Policy.cs](../../../Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs) `살뜰마트라스트마일배차Policy.Evaluate` 14행 |
| 9. 배차 보류 사유·수락배정 보존 및 마트 출고투영 대조<br>`mart.completion-recovery` | 알뜰살뜰마트배차대기Service / 살뜰마트라스트마일배차Policy / FoodMartLedgerMongoSyncService | 조건: acceptedassignment 상태는 전략/배정 유지; 미준비/주소/목적지 누락은 보류 결과 / 주문 반영 완료 기준이 outbound allcomplete인지 실제 전달증빙인지 구분 | [알뜰살뜰마트배차대기Service.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/알뜰살뜰마트배차대기Service.cs) `알뜰살뜰마트배차대기Service.주문포장완료후배차대기생성Async` 98행<br>[살뜰마트라스트마일배차Policy.cs](../../../Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs) `살뜰마트라스트마일배차Policy.Evaluate` 24행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 공급 계약 | 관찰 상태축 | Draft / Active / Suspended / Terminated | 해당 선언의 값 전수 | [플랫폼공급중개Dtos.cs](../../../Ssalddel.Contracts/Common/ContractManagement/플랫폼공급중개Dtos.cs) `플랫폼공급계약상태코드` 20행 |
| 조직 참여 | 관찰 상태축 | Active / Suspended / Cancelled | 해당 선언의 값 전수 | [플랫폼공급중개Dtos.cs](../../../Ssalddel.Contracts/Common/ContractManagement/플랫폼공급중개Dtos.cs) `공급계약이용상태코드` 28행 |
| 개별 공급 발주 | 관찰 상태축 | SubmittedToSupplier / SupplierAccepted / SupplierPartiallyAccepted / SupplierRejected / Withdrawn | 해당 선언의 값 전수 | [플랫폼공급중개Dtos.cs](../../../Ssalddel.Contracts/Common/ContractManagement/플랫폼공급중개Dtos.cs) `개별공급발주상태코드` 35행 |
| 마트 주문 요청 의향 | 관찰 상태축 | Submitted / Withdrawn | formal 확정주문 및 재고배분 상태와 구분<br>해당 선언의 값 전수 | [마트주문요청Dtos.cs](../../../Ssalddel.Contracts/Mart/마트주문요청Dtos.cs) `마트주문요청상태코드` 3행 |
| 라스트마일 준비 단계 | 관찰 상태축 | PickingPending / PickingStarted / PickingCompleted / PackingInProgress / ReadyForPickup | 해당 선언의 값 전수 | [살뜰마트라스트마일배차Policy.cs](../../../Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs) `살뜰마트라스트마일준비단계Codes` 166행 |
| 라스트마일 정책 전략 | 관찰 상태축 | PeakImmediate / Balanced / Consolidation / OldestFirstRecovery / CourierScarcity / PreparationDelayProtection / AcceptedAssignmentPreserved | 순수 판정값; 배차 확정 아님<br>해당 선언의 값 전수 | [살뜰마트라스트마일배차Policy.cs](../../../Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs) `살뜰마트라스트마일배차전략Codes` 181행 |
| 라스트마일 정책 판정 사유 | 관찰 상태축 | PromiseDeadlineAtRisk / ExpectedReadyAfterInternalTarget / ExpectedReadyUnknown / CourierSupplyScarce / PendingOrderBacklog / ConsolidationSlackAvailable / BalancedDefault / AcceptedAssignmentMustNotChange | 해당 선언의 값 전수 | [살뜰마트라스트마일배차Policy.cs](../../../Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs) `살뜰마트라스트마일배차사유Codes` 192행 |
| 라스트마일 생성/보류 결과 | 관찰 상태축 | 생성또는조회됨 / 주문참조번호없음 / 출고예정없음 / 포장대기 / 기사제안대기 / 상차주소없음 / 배송목적지없음 | 해당 선언의 값 전수 | [알뜰살뜰마트배차대기Service.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/알뜰살뜰마트배차대기Service.cs) `알뜰살뜰마트배차대기결과코드` 473행 |
| 마트 주문 및 상품의 원장 투영 문자열 | 관찰 상태축 | 출고 예정 / 출고 준비중 / 출고 완료 / 입고 예정 / 입고 운송중 / 입고 완료 / 포장 완료 | 출고 완료는 실제 고객 전달완료 증빙과 다른 의미<br>조사한 producer literals만; 전체 dynamic stage key/string 전수 아님 | [FoodMartLedgerMongoSyncService.cs](../../../Ssalddel/Services/Community/FoodMartLedgerMongoSyncService.cs) `음식마트원장Mongo동기화Builder.ResolveOutboundStage` 552행<br>[FoodMartLedgerMongoSyncService.cs](../../../Ssalddel/Services/Community/FoodMartLedgerMongoSyncService.cs) `음식마트원장Mongo동기화Builder.ResolveInboundStage` 572행 |

**LOG-MART-01 · P2 · unconfirmedCrossOsAdapterGap**

마트 child handoff는 SsalddelMartPackedOrder 음식배달 운송원장만 만들고 Accepted를 저장한다. 공통 음식배달 기사 행동 consumer는 주문번호로 db.음식주문을 반드시 조회하므로, 마트 주문→음식주문 adapter 또는 binding 없으면 행동 대상이 null이다. 조사한 연결 경로에서 binding을 확인하지 못했다.

근거: [살뜰마트라스트마일배차인계Service.cs](../../../Ssalddel/Services/Operations/살뜰마트라스트마일배차인계Service.cs) `살뜰마트라스트마일배차인계Service.인계Async` 107행<br>[살뜰마트라스트마일배차인계Service.cs](../../../Ssalddel/Services/Operations/살뜰마트라스트마일배차인계Service.cs) `살뜰마트라스트마일배차인계Service.인계Async` 120행<br>[FoodDeliveryDriverWorkService.cs](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) `음식배달기사업무Service.LoadForActionAsync` 810행<br>[음식배달배차흐름.cs](../../../Ssalddel/Services/Dispatch/Engine/음식배달배차흐름.cs) `음식배달배차흐름Resolver.Resolve` 44행

**LOG-MART-02 · P2 · sourceCoverageGap**

formal stage8의 음식배달 전달증빙을 마트 주문 완료와 부족/지연/재배정/취소 회복으로 되돌리는 생산 경로는 조사범위에서 확인하지 못했다. 현재 조사된 마트 완료 투영은 출고예정 전부 출고완료를 기준으로 한다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `OperatingSystemLifecycleStageIds.MartCompletionRecovery` 242행<br>[FoodMartLedgerMongoSyncService.cs](../../../Ssalddel/Services/Community/FoodMartLedgerMongoSyncService.cs) `음식마트원장Mongo동기화Builder.ResolveOutboundLedgerState` 537행<br>[FoodMartLedgerMongoSyncService.cs](../../../Ssalddel/Services/Community/FoodMartLedgerMongoSyncService.cs) `음식마트원장Mongo동기화Builder.ResolveOutboundStage` 552행

**LOG-MART-03 · P3 · integrationCoverageGap**

순수 policy는 PickingStarted/Completed/PackingInProgress의 후보 등록·임박 조기제안을 표현하지만 production packaging 연결은 ReadyForPickup으로만 policy를 호출한다. 준비중 후보 기능의 end-to-end 연결 근거가 부족하다.

근거: [살뜰마트라스트마일배차Policy.cs](../../../Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs) `살뜰마트라스트마일배차Policy.Evaluate` 40행<br>[살뜰마트라스트마일배차Policy.cs](../../../Ssalddel.Domain/운영/살뜰마트라스트마일배차Policy.cs) `살뜰마트라스트마일배차Policy.Evaluate` 96행<br>[알뜰살뜰마트배차대기Service.cs](../../../Ssalddel/Services/LogisticsProcessing/Warehouse/알뜰살뜰마트배차대기Service.cs) `알뜰살뜰마트배차대기Service.주문포장완료후배차대기생성Async` 187행

조사 한계: 플랫폼 공급중개 계약·공급자 응답은 공급매매 당사자/실물입고/재고를 대신하지 않음. 마트주문요청은 비구속 의향. pure lastmile strategy·queue·인계수락·기사수락·고객전달증빙은 별도.

### 8. 커뮤니티 신뢰

`CommunityTrustOS` · 공통 단계 0 · 조사 절차 연결 8 · 상태묶음 6.

공통 stage ID는 미정의다. 아래 번호는 현재 코드에서 관찰한 절차를 읽기 위한 순서이며 새 canonical lifecycle을 등록한 것이 아니다.

등록 OS에 formal stage 정의가 없다는 사실은 기능 부재를 뜻하지 않습니다. 아래 절차와 상태 묶음은 실제 구현 관찰이며 canonical lifecycle ID를 새로 만들지 않습니다.

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 글 탐색·게시·관심/투표 시작 | 회원 read/publishing/participation UseCase | 조건: 비구속 의미 명시 확인<br>경계: 관심 시작은 주문·계약·배차·원장을 자동 생성하지 않음 | [CommunityPostOpportunityParticipationUseCase.cs](../../../Ssalddel/Services/Community/CommunityPostOpportunityParticipationUseCase.cs) `StartParticipationAsync` 29행 |
| 2. 관심에서 가원장으로 승격 | 원글 작성자 + distinct 관심자 ≥2 → participation UseCase | 조건: 별도 가원장 생성 확인·비구속 근거·알림 확인 / 원글 author 결속 / 서로 다른 관심자 최소2 / 기존 원장·거래 조건 충돌 검증<br>저장: 초안 원장 + 투표 결속<br>경계: 가원장 초안은 법적 계약/결제/자동 실행 아님 | [CommunityPostOpportunityParticipationUseCase.cs](../../../Ssalddel/Services/Community/CommunityPostOpportunityParticipationUseCase.cs) `PromoteParticipationAsync` 66행 |
| 3. 조건·양측 동의·공개 동의가 분리된 생활 협업 신청/변경 | 본인 당사자 또는 허용 참가자 → 생활협업UseCase → MongoStore | 조건: actor/party·current revision·client request fingerprint / 서버 AllowedActions / 조건 변경 시 TermsRevision 증가 후 양측 재합의 / 인계정보 제공 notice와 공개 이력 consent 분리<br>저장: Mongo 협업 원장 revision CAS, request receipts/history | [생활협업UseCase.cs](../../../Ssalddel/Services/Community/생활협업UseCase.cs) `신청Async` 21행<br>[생활협업UseCase.cs](../../../Ssalddel/Services/Community/생활협업UseCase.cs) `변경Async` 69행 |
| 4. 양측 업무 시작·제공자 완료 제안·상대 완료 확인 | 협업 당사자 → UseCase | 조건: 완료 제안자와 확인자 분리 / storage 반환 완료/linked delivery 실제 완료 확인 | [생활협업UseCase.cs](../../../Ssalddel/Services/Community/생활협업UseCase.cs) `AllowedActions` 382행<br>[생활협업UseCase.cs](../../../Ssalddel/Services/Community/생활협업UseCase.cs) `CompletionAuthorityAsync` 330행 |
| 5. r22 직접 수령·직접 전달·기사 배송 및 확정 전 배차 방식 선택 | 현재 합의 당사자 / SQL 의뢰 소유자 | 조건: SQL Serializable transaction + ExpectedDispatchRevision + receipt replay / 기사 확정/queue 종료/진행중이면 선택 lock / 방식/조건 변경 전 대기 배송 취소 확인, 합의/인계 notice 초기화<br>저장: 협업 Mongo TermsRevision와 SQL delivery choice/event는 다른 저장소; fenced intent로 연결<br>경계: 기사 수락·인수 후 취소/반환은 기존 배송 상세 권위에서 처리 | [생활전달선택.cs](../../../Ssalddel.Contracts/Common/Community/생활전달선택.cs) `NeighborhoodTransferMethods` 3행<br>[생활배송배차선택Service.cs](../../../Ssalddel/Services/Community/생활배송배차선택Service.cs) `선택Async` 23행 |
| 6. 생활 보관 공간 등록·시간/수량 예약·양측 인수/반환 확인 | 제공자 또는 합의 당사자 → 생활보관공간Service | 조건: 현재 양측 조건·비공개 인계동의 / 공간 revision/OfferRevision / 시간·단위·최대 동시 수량 / reservation intent guard / 양측 confirm-intake/confirm-return | [생활보관공간Service.cs](../../../Ssalddel/Services/Community/생활보관공간Service.cs) `등록Async` 160행<br>[생활보관공간Service.cs](../../../Ssalddel/Services/Community/생활보관공간Service.cs) `예약Async` 223행 |
| 7. 커뮤니티 원장 저장·상태 변경·privacy-minimized 완료 게시 | 원장 UseCase → Mongo community ledger → 이벤트 투영 | 저장: revision CAS, ledger blocks/history; event publication after source storage<br>경계: 완료 신호는 상세 주소·연락처·계좌·증빙을 공개하는 권한 아님 | [CommunityLedgerStore.cs](../../../Ssalddel/Services/Community/CommunityLedgerStore.cs) `원장저장Async` 34행<br>[CommunityLedgerStore.cs](../../../Ssalddel/Services/Community/CommunityLedgerStore.cs) `원장상태변경Async` 158행 |
| 8. 업무 관계 친구 요청 수락/거절·연락처 항목 동의 기록 | 인증된 수신자 → 친구요청응답CommandHandler | 조건: 친구 요청의 수신자=current user / pending 상태에서만 응답<br>저장: RDB 요청·연락처공개동의 저장 후 Command알림Outbox 저장 | [친구요청Controller.cs](../../../Ssalddel/Controllers/Common/친구요청Controller.cs) `요청응답` 67행<br>[친구요청응답CommandHandler.cs](../../../Ssalddel/Application/Connections/Handlers/친구요청응답CommandHandler.cs) `Handle` 22행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 가원장/일반 원장 | 관찰 상태축 | 초안 / 진행중 / 보류 / 완료 / 닫힘 |  | [CommunityLedgerContracts.cs](../../../Ssalddel.Community/Ledgers/CommunityLedgerContracts.cs) `커뮤니티원장상태` 190행 |
| 생활 협업 | 관찰 상태축 | requested / agreed / in-progress / completion-proposed / completed / cancelled / rejected / expired |  | [생활협업Dtos.cs](../../../Ssalddel.Contracts/Common/Community/생활협업Dtos.cs) `NeighborhoodCollaborationStates` 21행 |
| 생활 보관 공간 | 관찰 상태축 | draft / published / paused / closed |  | [생활보관공간Dtos.cs](../../../Ssalddel.Contracts/Common/Community/생활보관공간Dtos.cs) `NeighborhoodStorageStatus` 17행 |
| 생활 보관 예약 | 관찰 상태축 | reserved / in-custody / returned / cancelled |  | [생활보관공간Dtos.cs](../../../Ssalddel.Contracts/Common/Community/생활보관공간Dtos.cs) `NeighborhoodStorageReservationStatus` 25행 |
| 친구 요청 | 관찰 상태축 | 대기 / 수락 / 거절 / 취소 |  | [친구요청.cs](../../../Ssalddel.Domain/사용자/친구요청.cs) `친구요청상태` 59행 |
| r22 전달 방식/배차 모드 | ChoiceValues; not lifecycle status | recipient-pickup / provider-delivery / driver-delivery / automatic / public-call / hybrid |  | [생활전달선택.cs](../../../Ssalddel.Contracts/Common/Community/생활전달선택.cs) `NeighborhoodTransferMethods` 3행<br>[생활전달선택.cs](../../../Ssalddel.Contracts/Common/Community/생활전달선택.cs) `NeighborhoodDispatchModes` 12행 |

**OTHER-TRUST-001 · P1 · ConsentActorBinding**

친구 요청 수락은 수신자를 인증하지만 연락처공개동의입력.동의자참여자Id를 인증 actor와 결속하지 않아 수신자가 다른 참여자 명의의 공개 동의 기록을 저장할 수 있습니다.

근거: [친구요청응답CommandHandler.cs](../../../Ssalddel/Application/Connections/Handlers/친구요청응답CommandHandler.cs) `Handle` 22행<br>[친구요청응답CommandHandler.cs](../../../Ssalddel/Application/Connections/Handlers/친구요청응답CommandHandler.cs) `동의자참여자Id` 52행<br>[친구요청응답CommandHandler.cs](../../../Ssalddel/Application/Connections/Handlers/친구요청응답CommandHandler.cs) `동의자참여자Id` 59행<br>[친구요청Controller.cs](../../../Ssalddel/Controllers/Common/친구요청Controller.cs) `요청응답` 67행<br>[연락처공개동의Configuration.cs](../../../Ssalddel.Infrastructure/Persistence/Configurations/User/연락처공개동의Configuration.cs) `Configure` 9행

**OTHER-TRUST-002 · P2 · PersistenceRecoveryBoundary**

친구 요청·동의 저장과 알림 Outbox 저장이 두 SaveChanges로 분리돼 두 번째 실패 시 응답은 이미 처리됐고 동일 Command는 '이미 처리'로 거절됩니다.

근거: [친구요청응답CommandHandler.cs](../../../Ssalddel/Application/Connections/Handlers/친구요청응답CommandHandler.cs) `SaveChangesAsync` 82행<br>[친구요청응답CommandHandler.cs](../../../Ssalddel/Application/Connections/Handlers/친구요청응답CommandHandler.cs) `Command알림Outbox.Add` 84행<br>[친구요청응답CommandHandler.cs](../../../Ssalddel/Application/Connections/Handlers/친구요청응답CommandHandler.cs) `SaveChangesAsync` 106행

조사 한계: CommunityTrust는 linear 주문 OS가 아니라 탐색/관심/합의/참가/동의/신뢰와 원장 조율의 여러 절차 / 공개 신호와 비공개 상세·정보 제공 동의·기사 제공 동의 분리 / Food/Cargo/Warehouse 실제 완료 권위를 협업 원장의 완료 표시가 대체하지 않음 / 새 r22 선택 기능은 확정 전 선택을 제공하며 서버 수락/인수 조건은 유지

세부 확인이 남은 범위: 모든 글 편집/신고/숨김/번역/자동게시 상세 전이 / 모든 public-data·지도 신청·보상·꾸미기·메시징 절차 / 커뮤니티 원장 모든 템플릿과 복합 사업 흐름 / 모든 개인정보 보존·삭제 구현

### 9. 플랫폼 운영

`PlatformOperationsOS` · 공통 단계 0 · 조사 절차 연결 4 · 상태묶음 2.

공통 stage ID는 미정의다. 아래 번호는 현재 코드에서 관찰한 절차를 읽기 위한 순서이며 새 canonical lifecycle을 등록한 것이 아니다.

등록 OS에 formal stage 정의가 없다는 사실은 기능 부재를 뜻하지 않습니다. 아래 절차와 상태 묶음은 실제 구현 관찰이며 canonical lifecycle ID를 새로 만들지 않습니다.

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 운영 재무 사건·계정 잔액·대사 예외·현금 흐름 조회 | 관리자 endpoint → 운영재무조회UseCase | 조건: currency/date 범위 검증<br>저장: 기존 RDB 사건·관리계정·대사 projection 읽기 전용<br>경계: 운영 전표 생성·지급·세금 신고를 수행하지 않음 | [운영재무조회UseCase.cs](../../../Ssalddel/Application/Admin/Finance/운영재무조회UseCase.cs) `사건목록조회Async` 88행<br>[운영재무조회UseCase.cs](../../../Ssalddel/Application/Admin/Finance/운영재무조회UseCase.cs) `관리계정잔액조회Async` 122행 |
| 2. 가정 또는 원장 기반 경제성 계산 | 서버관리자 → 계산 UseCase/finance query | 저장: 읽기/계산 결과; 기존 원장 snapshot hash 결속<br>경계: 확정 정산/은행 입금/실현 수익 아님 | [플랫폼운영경제성Controller.cs](../../../Ssalddel/Controllers/Admin/Operations/플랫폼운영경제성Controller.cs) `플랫폼운영경제성Controller` 32행<br>[플랫폼운영경제성UseCase.cs](../../../Ssalddel/Application/Admin/Operations/플랫폼운영경제성UseCase.cs) `플랫폼운영경제성UseCase` 23행 |
| 3. 음식·마트 동기화 및 OS 인계 후속처리 복구 목록 | 서버관리자 → 운영후속처리복구UseCase | 조건: Succeeded/완료는 기본목록 제외 / lease 초과 Processing을 운영자확인필요로 표시<br>저장: RDB 원천 두 종류의 Pending/Processing/Failed/Succeeded 읽기 | [운영후속처리복구UseCase.cs](../../../Ssalddel/Application/Admin/Operations/운영후속처리복구UseCase.cs) `목록조회Async` 34행 |
| 4. 실패한 후속처리의 안전 재시도 예약 | 서버관리자 → recovery UseCase → 각 Outbox service | 조건: Failed에 한정 / 예상처리시도수 일치/상태 conflict<br>경계: 이미 완료된 primary 주문/운송/원장을 되돌리지 않음 | [운영후속처리복구UseCase.cs](../../../Ssalddel/Application/Admin/Operations/운영후속처리복구UseCase.cs) `재시도예약Async` 92행<br>[FoodMartLedgerSyncOutboxService.cs](../../../Ssalddel/Services/Community/FoodMartLedgerSyncOutboxService.cs) `실패항목재시도예약Async` 107행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 운영 복구 표시 | 관찰 상태축 | AutoRetryPending / Processing / OperatorReviewRequired / Completed |  | [운영후속처리복구Dtos.cs](../../../Ssalddel.Contracts/Admin/Operations/운영후속처리복구Dtos.cs) `운영후속처리복구상태Codes` 9행 |
| 원천 Outbox 상태 | 관찰 상태축 | Pending / Processing / Failed / Succeeded / 대기 / 처리중 / 재시도대기 / 완료 / 실패 | 두 원천의 다른 저장 코드와 운영 표시를 구별; canonical Platform lifecycle 아님 | [운영후속처리복구UseCase.cs](../../../Ssalddel/Application/Admin/Operations/운영후속처리복구UseCase.cs) `MapLedger` 160행<br>[CommunityLedgerContracts.cs](../../../Ssalddel.Community/Ledgers/CommunityLedgerContracts.cs) `커뮤니티원장투영상태` 58행 |

조사 한계: 시뮬레이션 경제성 결과·운영 전표 허용·지급/세무 완료는 서로 다른 증거 / 복구 목록은 현재 두 Outbox 원천을 다루며 전체 플랫폼 실패 통합이라고 주장하지 않음 / 통계/재무 read와 실승인·정산/보험/수수료 변경 분리 / stale Processing은 자동 lease 회수 경로가 있으므로 retry 버튼 부재 자체를 결함으로 판단하지 않음

세부 확인이 남은 범위: 인력/참여/근로계약/보험 신고 전체 / 역할 승인·운영 설정·모든 관리 controller / 정산 지급·대사 작업의 전체 종료/재처리 / 현재 DB에 기록된 lease/실패 row

### 10. 교육 현장 체험 지원

`EducationFieldExperienceOS` · 공통 단계 0 · 조사 절차 연결 7 · 상태묶음 5.

공통 stage ID는 미정의다. 아래 번호는 현재 코드에서 관찰한 절차를 읽기 위한 순서이며 새 canonical lifecycle을 등록한 것이 아니다.

등록 OS에 formal stage 정의가 없다는 사실은 기능 부재를 뜻하지 않습니다. 아래 절차와 상태 묶음은 실제 구현 관찰이며 canonical lifecycle ID를 새로 만들지 않습니다.

| 절차 · 연결 책임 단계 | 담당/변경 경계 | 선행 조건·결과·한계 | 소스 근거 |
| --- | --- | --- | --- |
| 1. 학생·보호자·학교·활동 계획의 비공개 원장 생성 | 인증 사용자 → 현장체험활동UseCase | 조건: 학생·학교·보호자·활동 계획 필수<br>저장: Mongo community ledger 교육 template; EducationFieldExperienceOS 결속<br>경계: 원장 작성은 출석 인정 아님 | [현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `생성Async` 76행<br>[현장체험활동Dtos.cs](../../../Ssalddel.Contracts/Common/Education/현장체험활동Dtos.cs) `대상OsCode` 103행 |
| 2. 활동 기록과 지정 지도자 현장 확인 | 학생/참여자 + 별도 지도자 역할 endpoint | 조건: 활동 기록은 학생 owner / 시작/종료 시간/활동 내용 검증 / 확인은 지정 지도자 + role<br>저장: 기록·확인 블록을 원장에 저장<br>경계: 지도자는 학교 출석 결정 권한 없음 | [현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `활동기록Async` 174행<br>[현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `현장지도자확인Async` 233행 |
| 3. 등록 보호자의 승인 또는 거절 | 원장에 등록된 보호자 → UseCase | 조건: 지정 보호자 actor 일치<br>저장: bool 승인/거절·의견 블록과 overall 보호자확인 상태<br>경계: 보호자확인이라는 overall 상태는 승인 bool=true와 동일하지 않음; 제출 readiness는 실제 bool 검사 | [현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `보호자승인Async` 298행 |
| 4. 문서/이메일/API 학교 제출 예약 | 참여자 → UseCase → Mongo 제출대기열 | 조건: 활동≥1 + 보호자 승인 + 지정 지도자 확인 / API URL은 body에 없고 서버 destination key / email 유효성<br>저장: 원장 제출블록 Save 후 별도 queue 예약; 문서면 수동제출준비 | [현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `학교제출Async` 349행<br>[교육기관제출대기열.cs](../../../Ssalddel/Services/Education/교육기관제출대기열.cs) `예약Async` 62행 |
| 5. 설정 조건의 비동기 전송·재시도 | 교육기관제출Worker → 서버 전송 adapter | 조건: 자동전송활성화 옵션 / atomic claim +5분 lease / 최대 시도/지연<br>저장: 설정대기는30분, 실패는 bounded retry, 성공 queue완료 후 ledger 학교심사중<br>경계: 전송 완료는 기관 수신 확인/출석 인정 아님 | [교육기관제출Worker.cs](../../../Ssalddel/Services/Education/교육기관제출Worker.cs) `ExecuteAsync` 27행<br>[교육기관제출Worker.cs](../../../Ssalddel/Services/Education/교육기관제출Worker.cs) `ProcessPendingAsync` 48행 |
| 6. 학교 출석 인정/미인정 결정 기록 | school claim가 원장과 일치하는 선생님 또는 서버관리자 | 조건: teacher/admin role / 학교 claim 결속(관리자 예외) / 결정기관/결정자 이름 / 제출블록 존재<br>저장: 학교결정 블록과 출석인정/미인정 상태 | [현장체험활동Controller.cs](../../../Ssalddel/Controllers/Common/현장체험활동Controller.cs) `학교결정` 98행<br>[현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `학교결정Async` 433행 |
| 7. 별도 교육과정 신청·심사·과제·참석·개인정보 삭제 | 교육과정참여Service/RDB; 현장 체험 원장과 별도 절차 | 조건: 활성 중복 신청 방지 / 등록된 신청은 승인 이외로 변경 불가 / 진행중 등록에서만 과제 제출 / 확인된 과제 재제출 금지 / 신청 본인/관리자 개인정보 삭제<br>저장: RDB 암호화 개인정보·신청·등록·과제·참석 | [교육과정참여Service.cs](../../../Ssalddel/Services/Education/교육과정참여Service.cs) `신청Async` 38행<br>[교육과정참여Service.cs](../../../Ssalddel/Services/Education/교육과정참여Service.cs) `심사Async` 157행 |

| 상태묶음 | 성격 | 관찰한 값/필드 | 해석과 범위 | 정의/판정 근거 |
| --- | --- | --- | --- | --- |
| 현장 체험 원장 | 관찰 상태축 | 계획작성 / 활동진행 / 보호자확인 / 제출대기 / 학교심사중 / 출석인정 / 출석미인정 |  | [현장체험활동Dtos.cs](../../../Ssalddel.Contracts/Common/Education/현장체험활동Dtos.cs) `현장체험활동상태` 114행 |
| 학교 제출 queue | 관찰 상태축 | 전송대기 / 전송중 / 설정대기 / 수동제출준비 / 전송완료 / 전송실패 |  | [현장체험활동Dtos.cs](../../../Ssalddel.Contracts/Common/Education/현장체험활동Dtos.cs) `교육기관제출상태` 135행 |
| 인접 교육과정 신청 | 관찰 상태축 | 검토대기 / 보류 / 승인 / 거절 / 철회 |  | [교육과정.cs](../../../Ssalddel.Domain/Education/교육과정.cs) `교육과정신청상태` 3행 |
| 인접 교육과정 등록 | 관찰 상태축 | 진행중 / 수료심사대기 / 수료 / 중지 |  | [교육과정.cs](../../../Ssalddel.Domain/Education/교육과정.cs) `교육과정등록상태` 15행 |
| 인접 교육과정 제출 | 관찰 상태축 | 제출 / 확인 / 보완요청 |  | [교육과정.cs](../../../Ssalddel.Domain/Education/교육과정.cs) `교육과정제출상태` 23행 |

**OTHER-EDU-001 · P1 · TerminalStateRegression**

학교결정은 제출블록 존재만 요구하고 pending 이메일/API 전송 완료를 요구하지 않습니다. 결정된 원장을 worker가 이후 읽고 전송에 성공하면 원장 최종상태를 학교심사중으로 낮출 수 있습니다.

근거: [현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `학교결정Async` 433행<br>[현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `학교제출Block` 463행<br>[교육기관제출Worker.cs](../../../Ssalddel/Services/Education/교육기관제출Worker.cs) `원장조회Async` 61행<br>[교육기관제출Worker.cs](../../../Ssalddel/Services/Education/교육기관제출Worker.cs) `완료Async` 76행<br>[CommunityLedgerStore.cs](../../../Ssalddel/Services/Community/CommunityLedgerStore.cs) `원장상태변경Async` 158행

**OTHER-EDU-002 · P2 · LostUpdateConcurrency**

현장 체험 SaveAsync가 읽은 ledger.Revision을 기대Revision으로 전달하지 않습니다. 다른 명령이 먼저 저장된 뒤 기존 block 목록을 다시 저장하면 앞선 기록·동의를 덮을 수 있습니다.

근거: [현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `SaveAsync` 505행<br>[CommunityLedgerStore.cs](../../../Ssalddel/Services/Community/CommunityLedgerStore.cs) `원장저장Async` 34행<br>[CommunityLedgerStore.cs](../../../Ssalddel/Services/Community/CommunityLedgerStore.cs) `EnsureExpectedRevision` 535행

**OTHER-EDU-003 · P2 · QueueLedgerRecoveryGap**

학교 제출 원장블록→큐예약과 전송성공 큐완료→원장상태변경이 별도 쓰기입니다. 중간 실패를 재결속하는 명령/outbox를 확인하지 못했습니다.

근거: [현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `SaveAsync` 407행<br>[현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `예약Async` 415행<br>[교육기관제출Worker.cs](../../../Ssalddel/Services/Education/교육기관제출Worker.cs) `완료Async` 76행<br>[교육기관제출대기열.cs](../../../Ssalddel/Services/Education/교육기관제출대기열.cs) `다음작업확보Async` 94행

**OTHER-EDU-004 · P3 · CatalogMetadataMismatch**

교육 OS는 canonical registry와 원장 template에 존재하지만 API metadata OS enum/catalog는 9개이고 교육 UseCase는 CommunityTrust workflow로 표시됩니다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `OperatingSystemIds.EducationFieldExperience` 18행<br>[현장체험활동Dtos.cs](../../../Ssalddel.Contracts/Common/Education/현장체험활동Dtos.cs) `대상OsCode` 103행<br>[SsalddelApiVersionAttribute.cs](../../../Ssalddel/ApiMetadata/SsalddelApiVersionAttribute.cs) `SsalddelOperatingSystem` 133행<br>[현장체험활동UseCase.cs](../../../Ssalddel/Services/Education/현장체험활동UseCase.cs) `SsalddelApiWorkflow` 61행

조사 한계: 플랫폼은 기관 결정을 기록/지원하며 현장 활동·학교 인정 자체를 자동 결정하지 않음 / 보호자 확인 상태와 실제 승인 bool 분리 / 문서 수동준비·email/API 전송완료·기관 인정은 서로 다른 단계 / 교육과정 RDB 절차는 현장체험 Mongo 원장과 별도 / 학교 PDF 서식·전자서명·기관 API/보존기간은 문서에 다음 수직 보완으로 명시

세부 확인이 남은 범위: 실제 SMTP/API 전송·기관 수신 / 모든 기관별 서식/전자서명 / 모든 교육과정 controller authorization/종료 writer / 실제 보호자/학교 인증 신원 강도

## 공통 대장과 문서의 불일치

**OTHER-DOC-001 · P3 · DocumentationDrift**

주문 중심 업무망 기획 문서는 순서형 lifecycle OS를 4개로 적지만 현행 공통 catalogue는 Warehouse를 포함해 5개를 정의합니다.

근거: [README.md](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK/README.md) `생명주기` 35행<br>[OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `OperatingSystemLifecycleCatalog` 173행

**META-R23-001 · P2 · VerifiedMetadataCoverageMismatch**

화면 대장에 존재하는 3개 업무·역할 조합이 해당 업무의 참여자 대장에는 없습니다.

| 업무 | 화면의 역할 | 화면 선언 |
| --- | --- | --- |
| CustomsAndTradeData | ShipperOrSeller | /shipper/customs/hs-reviews |
| FoodDelivery | PlatformOperator | /food/order-trace |
| SsalddelMart | Orderer | /food/mart |

근거: [SsalddelApiVersionAttribute.cs](../../../Ssalddel/ApiMetadata/SsalddelApiVersionAttribute.cs) `SsalddelWorkflowParticipants` 649행<br>[SsalddelApiVersionAttribute.cs](../../../Ssalddel/ApiMetadata/SsalddelApiVersionAttribute.cs) `SsalddelWorkflowScreens` 829행

**META-R23-002 · P2 · CanonicalLifecycleCoverageGap**

공동구매 수요·공동수입·커뮤니티 신뢰·플랫폼 운영·현장체험 교육의 실제 절차는 존재하지만 공통 생명주기 stage ID가 아직 없습니다.

근거: [OperatingSystemIdentityCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemIdentityCatalog.cs) `OperatingSystemLifecycleCatalog` 173행<br>[OperatingSystemInteractionCatalog.cs](../../../Ssalddel.Contracts/Common/Versioning/OperatingSystemInteractionCatalog.cs) `GetLifecycleCoverage` 216행

## 검증 결과와 재검사

상태: `SourceAuditComplete / StaticConsistencyGapsFound / RelatedTestsPassed / ProductRuntimeNotVerified`.

| 검증 | 결과 |
| --- | --- |
| 카탈로그·OS 인계 기존 시험 | 38개 통과 |
| 음식 생명주기·Guard·정산/표시 관련 기존 시험 | 415개 통과 |
| 나머지 OS 절차·상태·동의·복구 관련 기존 시험 | 1129개 통과 |
| 카탈로그/근거 정적 검사 | 4628개 통과 / 3개 불일치 탐지 |
| 정적 검사 오류 입력 대조 | 21개 오류 입력을 탐지 |
| 소스 근거 | 파일 SHA-256·anchor·줄 대조, 현재 카탈로그의 10 OS·41 stage 조사 연결 확인 |

정적 검사에서 실패한 3개 항목은 위 META-R23-001의 화면/참여자 대장 누락이다. 빌드 오류나 단위시험 실패로 바꾸어 설명하지 않는다. 기존 시험은 확인된 정적 결함 경로의 실패 순서·동시성·재시도를 모두 다루지 않으므로 통과를 근거로 결함이 없다고 판단하지 않는다.

```powershell
dotnet run --project eng/Ssalddel.OperatingSystemAudit -- `
  --root . `
  --audit docs/ProjectOverview/page-docs/os-lifecycle-static-audit-r23.json
```

현재는 3개 metadata 불일치를 기록하며 종료 코드 1을 반환한다. 근거 파일이 바뀌면 추가로 SourceCurrent 검사가 실패하므로 변경된 절차를 다시 조사한 후 새 판본을 남겨야 한다. 고정 JSON을 읽었다는 사실이나 해시만 다시 계산했다는 사실을 의미 검증으로 승격하지 않는다.

원시는 Git 제외 `artifacts/local/os-lifecycle-audit-r23/`의 카탈로그 결과·선정 시험 목록·TRX·로그·오류 입력 검사·소스 참조 대조·범위 보존 확인에 있다. UI·화면을 바꾸지 않았으며 새 화면 캡처가 필요한 작업은 아니다. 휴대폰/APK 설치·지도/GPS·Azure·외부 제출/입금·여러 계정의 실제 업무 완주는 미검증이다. 제품 API·DB·상태 전이·요금·정산·Unity·배달 영상을 수정하거나 commit/push·배포를 수행하지 않았다.
