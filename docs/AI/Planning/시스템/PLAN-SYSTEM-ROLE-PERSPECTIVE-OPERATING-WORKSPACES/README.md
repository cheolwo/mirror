[기획 · 시스템·역할별 운영 UI · PLAN-SYSTEM-ROLE-PERSPECTIVE-OPERATING-WORKSPACES · r26]

# 하나의 운영 원장과 역할별 OS 작업공간

> 화면 이미지 경계: 이 문서에 연결된 모바일 이미지는 모두 `SampleOnly / NotApprovedVisual / NotImplementationSpec`인 탐색용 샘플이다. Git 커밋은 시안 승인이나 구현 지시를 뜻하지 않는다. [샘플 이미지 대장](../../../../assets/planning/README.md)

- 기획 ID: `PLAN-SYSTEM-ROLE-PERSPECTIVE-OPERATING-WORKSPACES`
- 기획 분야: 시스템·역할별 운영 UI
- 기획 판본: `r26`
- 상태: `Draft / UnifiedPlatformAdminAppConfirmed / OsOperatorDailyWorkspacePrimaryConfirmed / CrossOsGovernanceSecondaryConfirmed / PlatformOperationsOsShellProposed / PlatformOperationsLifecycleNotInvented / CrossOsFinancialProjectionProposed / RolePerspectiveWorkspacesConfirmed / CanonicalLedgersShared / ExistingRoleAppsMapped / MobileGlancePartiallyImplemented / AdminAndroidBuildPassed / PhysicalDeviceUiProofMissing / PhysicalDeviceReviewDeferredByUser / ImageDrivenMobileReviewConfirmed / ExistingFigmaReferencesMapped / FoodDeliveryOperatorVisualDraftGenerated / OperatorFieldParticipationNotImplemented / ExplicitRoleSwitchRequired / FoodDriverWorkflowReusable / ExceptionFirstHomeAsked / FieldParticipationEntryPlacementDeferred / FirstParticipationModePending / FoodDriverMapFirstConfirmed / FoodDriverMyInfoAndHistoryConfirmed / FoodDriverFrozenPayoutBreakdownConfirmed / PayoutBreakdownDefaultExpandedCollapsible / StandardPayoutRowsAlwaysVisibleConfirmed / GrossDeductionNetSeparated / StatutoryDeductionEligibilityServerOwned / MonthlyInsuranceFinalizationConfirmed / PlatformShareNeverDeductedFromDriver / PlatformContributionSecondaryDisclosureConfirmed / DriverHoldLabelSimplifiedConfirmed / CustomerCenterInquiryConfirmed / InternalHoldReasonOperatorOnly / HistoricalNoRecalculationConfirmed / ExistingPricingLedgerPartiallyMapped / StatutoryDeductionLedgerMissing / PayoutBreakdownContractRequired / FoodRestaurantDriverPairRelationshipConfirmed / BilateralVisibilityControlConfirmed / PreferenceFailureFailClosed / RelationshipNoDispatchInfluenceConfirmed / FoodRelationshipProjectionRequired / RelationshipVisibilityPreferenceRequired / FoodDriverHistoryProjectionRequired / ExistingMartEntryScreenMapped / UiCoverageAuditR2Completed / FinanceDetailUiNext / PhysicalAppSplitDeferred / UnityReadOnlyExcluded`
- 사용자 요구: 일상 모바일 업무는 총괄 관리자가 아니라 음식배달·화물·마트·창고 등 각 OS 운영자가 자기 흐름과 예외를 보는 작업공간을 중심으로 한다. 총괄 관점은 정책·권한·재무·OS 간 최종 충돌에만 사용하는 후순위 관리층이며, 처음에는 한 앱 안에서 OS 작업공간을 전환하고 사람이 늘면 같은 계약을 유지한 채 역할별 앱으로 분리할 수 있어야 한다.
- 상위 기획: [지역 운영 생명주기 E2E·전국 확장 뼈대](../PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/README.md)
- 첫 역할 화면: [음식 판매 OS 주문 처리 화면](../../공통/PLAN-OPERATIONS-FOOD-SALES-ORDER-DESK/README.md)
- 주문자 첫 화면: [진행 주문 우선 홈과 거리 제한 상점 탐색](../../공통/PLAN-OPERATIONS-ORDERER-ACTIVE-ORDER-HOME/README.md)
- 음식 배달 기사 첫 화면: [지도 중심 첫 화면](../../공통/PLAN-OPERATIONS-FOOD-DRIVER-MAP-HOME/README.md)
- 음식점-기사 관계: [함께한 배달 관계](../../공통/PLAN-OPERATIONS-FOOD-RESTAURANT-RIDER-RELATIONSHIP/README.md)
- 기사 지급 상세: [완료 배달 지급 구성 상세](../../공통/PLAN-OPERATIONS-FOOD-DRIVER-PAYOUT-DETAIL/README.md)
- UI 존재 여부 조사: [운영 기획·코드·UI 존재 여부 조사 r2](ui-coverage-audit.r1.md)
- 운영자 모바일·현장 참여: [OS별 이동 중 운영 확인과 현장 참여 r10](operator-mobile-field-participation.r1.md)
- 음식 배달 OS 운영자 시안: [운영 홈·주문 흐름 상세·현장 참여](../../../../assets/planning/food-delivery-os-operator-workspace-r1.png)
- OS별 운영자 화면 목차: [자주색 OS 운영자·황색 총괄 관리자](../../../../assets/planning/os-operator-screen-catalog-r1.png)
- 배차·기사 수급 시안: [현재 관측 통계·예외 상세·동결 지급 구성](../../../../assets/planning/food-delivery-os-dispatch-supply-r1.png)
- 내비게이션 기준: [통합 클라이언트 3단계 내비게이션](../../../../Architecture/ThreeStageClientNavigation.md)
- 현행 판정: `배차대기 주문 원장`과 `대기·제안 가능 기사 수급`을 분리하고 정상은 관측 통계, 예외만 건별 상세로 연다. 미래 수급·비용 예측은 제외한다.
- 통계 기본 범위: `최근 60분 실제 관측 + 오늘 누적 요약`. 더 긴 기간은 통계 상세에서 선택한다.
- 예외 정렬: `안전 위험 → 업무중단·회복 실패 → 판단자료 부족 → 장기 대기`, 같은 단계에서는 대기시간이 긴 순서로 표시한다.
- 예외 상세 첫 행동: 서버 정본과 최신 revision·`AvailableActions` 재조회. 성공 전에는 변경 행동을 열지 않는다.
- 다음 질문: 일반 예외는 앱 내 메시지, 안전 위험·현장 진행 불가는 전화 우선으로 구분할지 정한다.

## 1. 세 가지를 분리한다

사용자가 부르는 `OS`를 모두 같은 서버 시스템으로 만들지 않는다. 다음 세 축을 분리한다.

| 축 | 의미 | 예시 | 소유하는 것 |
| --- | --- | --- | --- |
| 운영 도메인 OS | 서버에서 업무 규칙·상태 전이·원장 권위를 갖는 체계 | `FoodDeliveryOS`, `WarehouseCommerceFulfillmentOS`, `SsalddelMartUrbanLogisticsOS` | 상태, revision, Command, Event, Outbox, 권한 판정 |
| 역할별 OS 작업공간 | 같은 원장을 특정 참여자의 질문과 행동으로 투영한 화면 묶음 | 주문자 주문 OS, 음식 판매 OS, 기사 수행 OS | 관점별 조회, 마스킹, 알림, `AvailableActions`, 화면 이동 |
| 실제 앱 Host | 작업공간을 담아 실행하는 물리 앱 | `SsalddelAdminApp`, `OrdererApp`, `RestaurantDeskApp`, `FDriverApp` | 로그인, 장치 기능, navigation, 렌더링 |

따라서 `음식 주문 OS`, `음식 판매 OS`, `음식 배달 OS`라는 사용자-facing 이름을 쓸 수 있지만, 역할마다 주문 원장과 서버 상태기를 복제하지 않는다. 한 원장의 서로 다른 역할 투영을 사용한다.

## 2. 공통 처리 흐름

```text
역할 로그인
  → 역할·업무 관계 확인
  → 같은 원장의 역할별 조회 결과
  → 마스킹된 상태 + 서버 AvailableActions
  → 사용자가 허용 행동 선택
  → 기존 Controller·UseCase·Command
  → 서버 권한·상태·revision 재검사
  → 원장 변경·Event·Outbox
  → 같은 원장 역할별 정본 재조회
```

- 화면에 행동 후보가 보인다는 사실은 실행 권한을 부여하지 않는다.
- 앱은 다른 역할의 Command를 대신 실행하지 않는다.
- 역할별 화면은 원장을 합치지 않고, 연결 원장 묶음에서 필요한 참조만 보여 준다.
- 개인정보·연락처·정밀 위치·금액은 역할과 업무 관계에 필요한 범위에서만 연다.
- Unity 관찰 화면은 이 작업공간에 포함하지 않는다. Unity는 비식별 읽기 전용 상태 사본을 보는 후순위 소비자다.

## 3. 총괄 관리자 통합 앱

첫 물리 Host는 기존 `SsalddelAdminApp`이다. 한 앱에서 여러 OS를 보되 다음 정보 계층을 유지한다.

```text
전체 운영 홈
  → OS별 정상·주의·긴급 집계
  → OS 중점 화면
  → 업무 대기열
  → 개별 원장 상세
  → 허용 행동·영향 미리보기
  → 확정 Command
  → 정본 재조회
```

총괄 관리자는 전체 상황과 책임 경계를 조정한다. 정상 업무를 매번 승인하거나 주문자·판매자·기사·창고 담당자의 역할을 대신하지 않는다. 금전 확정, 할증 금액 변경, 보상·환불·정산 승인은 별도 권한과 미리보기를 유지한다.

OS별 관리자가 생기면 같은 통합 앱에서 권한에 따라 자기 OS 모듈만 볼 수 있다. 조직과 업무량이 커지면 해당 모듈을 얇은 별도 앱 Host로 분리하되 서버 원장·계약·공통 UI를 복제하지 않는다.

### 3.1 총괄 화면과 `PlatformOperationsOS`

재무 영향 화면의 상단을 `음식배달 OS`로 고정하지 않는다. 저장소에는 이미 안정 식별자 `PlatformOperationsOS`와 표시명 `플랫폼 운영 OS`가 있으므로 새 `총괄 OS` 식별자나 중복 원장을 만들지 않고 이를 총괄 화면 셸로 재사용한다.

```text
총괄 운영 (`PlatformOperationsOS`)
  → 범위: 전체 / 음식배달 / 화물운송 / 마트 / 창고
  → 현재 원장: OS별 읽기 전용 재무 사본 집계
  → 시뮬레이션: 같은 범위의 가정 기반 Preview
  → OS 상세: 원래 책임 OS의 업무·원장으로 내려가기
```

- `PlatformOperationsOS`는 전체 집계, 정책 비교, 예외 연결과 재무 관점을 제공한다.
- 음식배달·화물·마트·창고의 상태·지급·정산 원장을 흡수하거나 대신 확정하지 않는다.
- 현재 공통 생명주기 대장이 없는 `PlatformOperationsOS`에 주문·배차·정산 단계를 임의로 만들지 않는다.
- `음식배달`은 화면 제목이 아니라 범위 필터 또는 상세 진입 대상이다.
- 기존 운영자 통합 회계 원칙대로 별도 회계사 앱을 만들지 않고 `SsalddelAdminApp`의 총괄 운영 화면 안에서 연다.

### 3.2 총괄은 일상 홈이 아니라 후순위 교차 관리층이다

정상 자동화가 동작하면 총괄 관리자의 일상 조작이 적은 것이 바람직하다. 기본 모바일 진입은 각 OS 운영자의 작업공간이며, `PlatformOperationsOS`는 다음 범위에만 사용한다.

- 여러 OS가 동시에 관련된 정책·권한·기능 활성화
- 한 OS 운영자가 해결할 수 없는 최종 실패와 책임 충돌
- 금전 승인·재무 영향·감사·보험·법률 검토
- 운영자 배정과 조직 관리

한 사람이 여러 OS 운영 권한을 가지면 `내 운영 업무`에서 음식배달·화물·마트·창고 작업공간을 명시적으로 전환한다. OS가 하나뿐이면 선택 화면을 거치지 않고 해당 작업공간으로 바로 들어갈 수 있다.

## 4. 역할별 작업공간

| 역할 작업공간 | 사용자의 핵심 질문 | 첫 화면 | 주요 행동 경계 | 현행 Host 후보 |
| --- | --- | --- | --- | --- |
| 주문자 · 음식 주문 OS | 내가 주문한 것이 지금 어디까지 진행됐는가 | 진행 중이면 현재 주문 카드, 없으면 거리 제한 상점 탐색 | 주문, 허용된 취소, 결제 확인, 수령 확인 | `OrdererApp`, `SsalddelApp` |
| 음식점 · 음식 판매 OS | 들어온 주문을 언제 어떻게 준비할 것인가 | 신규·조리 중·픽업 대기 | 수락·거절, 조리시간, 준비 완료, 인계 확인 | `RestaurantDeskApp` |
| 기사 · 음식 배달 수행 OS | 현재 어디에서 운행하며 신규 배차를 받을 것인가 | 지도·왼쪽 상단 수신 토글·하단 운행 시작 | 운행 상태, 수신 의사, 제안 수락·거절, 픽업·전달·사건 보고 | `FDriverApp` |
| 마트 관리자 · 마트 판매·이행 OS | 주문·재고·피킹·포장·인계를 무엇부터 처리할까 | 주문·재고·작업 대기열 | 재고 확인, 피킹·포장, 라스트마일 인계 | `SellerApp`, 마트 모듈 Host 후보 |
| 창고 관리자·작업자 · 창고 운영 OS | 입고·검수·적치·출고와 예외가 어디에 쌓였는가 | 입고·출고·예외 작업함 | 검수, 적치, 피킹, 포장, 수량 차이·격리 보고 | `WarehouseManagerApp` |
| 화주·담당자 · 화주 운송관리 OS | 누가 맡은 운송 의뢰가 어느 단계인가 | 의뢰·계약·운송·인수 목록 | 담당자 위임, 의뢰, 계약 확인, 인수, 문제 보고 | `ShipperApp` 계열 |
| 화물 기사 · 화물 운송 수행 OS | 내가 수락하고 수행할 운송은 무엇인가 | 제안·상차·운송·하차 작업 | 수락, 상하차 확인, 운송, 부분 인수·파손 보고 | `DriverApp` 계열 |

역할 이름과 물리 앱 이름은 일대일일 필요가 없다. 한 사람이 여러 권한을 가졌다면 앱 안에서 명시적으로 역할을 전환하되, 현재 역할과 수행 가능한 행동을 항상 표시한다.

### 4.1 역할별 공통 진입 골격

각 앱의 `/` 또는 역할 홈은 세부 기능을 한꺼번에 펼치는 메뉴판이 아니라 다음 순서의 진입점으로 맞춘다.

```text
로그인·세션 복원
  → 현재 역할과 운영 상태 표시
  → 지금 이어서 해야 하는 업무 1건 또는 가장 중요한 대기열
  → 같은 역할의 OS 작업공간 목록
  → 알림·정산·설정·커뮤니티 보조 진입점
  → 상세에서 서버 AvailableActions 확인
```

- 진행 중 업무가 있으면 새 업무 탐색보다 먼저 보여 준다.
- 진행 중 업무가 없으면 유효 제안·신규 주문·입출고 작업처럼 해당 역할의 다음 후보를 보여 준다.
- 후보도 없으면 근무 시작, 상점 탐색, 새 의뢰 작성처럼 역할의 정상 시작 행동을 보여 준다.
- 첫 화면은 개별 업무를 임의 확정하지 않는다. 상세 진입 뒤 서버가 제공한 상태와 `AvailableActions`로 Command를 연다.
- 커뮤니티·정산·설정은 역할 업무를 가리지 않는 보조 진입점으로 두되 삭제하지 않는다.

### 4.2 현재 진입점·기획 화면 범위

| 역할 | 현재 코드 진입점 | 첫 화면 코드 | 기획 화면 정리 | 다음 처리 |
| --- | --- | --- | --- | --- |
| 총괄 운영자 | `SsalddelAdminApp /`, `/overview` | 운영 집계·예외·배차대기 | 운영 홈 방향과 예외 상세 시안 존재 | 공통 진입 골격과 권한 표를 후속 대조 |
| 주문자 | `OrdererApp /` | 역할 여정·주문자 모바일 홈 | 진행 주문 홈·주문 상세 지도 시안 존재 | 세부 문답 잠시 보류 |
| 음식점 | `RestaurantDeskApp /` | 주문 수신함 등 빠른 행동 | 주문 판단 모바일 시안 존재 | 세부 문답 잠시 보류 |
| 음식 배달 기사 | `FDriverApp /` | 현재 카드형 홈, 별도 네이티브 지도 업무 화면 | 지도 첫 화면·수신 토글·운행 시작·내 정보/배달 내역 시안 생성 | 완료 이력 Projection·상세 범위 문답 뒤 닫기 |
| 마트 관리자 | `SellerApp /`, `WarehouseManagerApp /mart` 후보 | 판매 흐름 또는 마트 작업 홈 | 마트 역할 첫 화면 미정 | **다음 화면 기획** |
| 창고 관리자·작업자 | `WarehouseManagerApp /` → 창고 화면 | 창고 화면으로 redirect | 역할별 첫 화면 우선순위 미정 | 마트 다음 |
| 화주·담당자 | `SsalddelApp /` 역할 분기, Web Shipper | 화주 역할 홈 코드 존재 | 모바일 첫 화면 시안 미정 | 창고 다음 |
| 화물 기사 | `DriverApp /` → `/driver/home/summary` | 진행 운송·오늘 할 일·추천 | 모바일 첫 화면 시안 미정 | 화주 다음 |

이 표의 `미정`은 코드가 없다는 뜻이 아니다. 기존 코드를 기준으로 역할이 처음 보아야 할 정보와 화면 계층을 아직 기획 시안으로 동결하지 않았다는 뜻이다.

## 5. 화면 공통 계약

각 역할 화면은 최소한 다음 여섯 요소를 같은 방식으로 표현한다.

1. 현재 역할과 현재 보고 있는 원장 또는 연결 원장 묶음
2. 서버가 확정한 현재 상태·revision·마지막 갱신 시각
3. 이 역할에 필요한 핵심 정보와 마스킹 상태
4. 서버가 제공한 `AvailableActions`
5. 행동 전 영향·대가·알림 대상 미리보기
6. 성공·실패 뒤 같은 원장 정본 재조회와 회복 진입점

모바일 목록은 표보다 compact card와 상세 전환을 우선한다. 전체 관계는 2단계 다이어그램에서 보고, 실제 입력·승인·스캔·연락은 3단계 데이터 화면에서 수행한다.

## 6. 현재 코드 존재 대조

| 기획 요소 | 현재 근거 | 판정 |
| --- | --- | --- |
| 서버 운영 OS 안정 ID | `OperatingSystemIdentityCatalog.cs` | `O` |
| 주문자·판매자·창고 관리자·운송 담당자 역할별 주문 관점 | `개별주문역할관점ViewModels.cs` | `O` |
| 역할별 행동 후보와 서버 권한 재검사 경계 | `역할관점업무ViewModels.cs` | `O` |
| 주문자·음식점·기사·창고·관리자 물리 앱 | 기존 앱 프로젝트들 | `O` |
| 총괄 관리자 앱의 OS별 모듈 확장 기반 | `SsalddelAdminApp` 운영 화면 | `부분` |
| 음식 판매 주문 판단 화면과 기존 처리 흐름 대조 | `PLAN-OPERATIONS-FOOD-SALES-ORDER-DESK` | `기획+기존 코드` |
| 모든 역할의 통일된 작업공간 카탈로그 | 이 기획에서 처음 정리 | `기획` |
| 역할 모듈을 나중에 별도 앱으로 안전하게 분리하는 검증 계약 | 미정 | `X` |

`O`는 관련 코드가 존재한다는 뜻이다. 완성도, 운영 준비, 모바일 장치 검증을 뜻하지 않는다.

## 7. 구현 우선순위

1. 기존 코드가 깊은 음식 배달 OS 운영자 화면에서 주문→조리→배차→픽업→전달과 예외 대기열을 먼저 닫는다.
2. 마트 주문→피킹·포장→음식 배달 인계에서 마트와 기사 책임 전환을 QR·주문번호 확인으로 결속한다.
3. 화물과 창고 운영자 관점을 같은 공통 진입 골격으로 확대하되 각 OS 생명주기를 유지한다.
4. 여러 OS를 맡은 사용자의 `내 운영 업무` 선택과 명시적 작업공간 전환을 닫는다.
5. 실제 교차 충돌이 확인된 정책·권한·재무·최종 실패만 총괄 셸에 연결한다.
6. 실제 사용 결과로 역할별 업무량과 권한 경계를 확인한 뒤에만 물리 앱 분리를 결정한다.

### 7.1 운영자 모바일과 현장 참여의 현재선

- `SsalddelAdminApp`에는 이동 중 빠른 확인 문구, 관리자 확인·운송 예외·배차대기 집계, 운송·기사 현황과 30초 보완 조회가 존재한다.
- 관리자 앱 Android Debug 빌드 증거는 있지만 실제 휴대폰 설치·로그인·터치·390px 판독·백그라운드 복귀는 검증되지 않았다.
- 실제 음식 배달 수행 흐름은 `FDriverApp`에 있으나 관리자 앱에서 기사 역할로 안전하게 전환하는 계약·화면·세션은 없다.
- 운영자 현장 참여는 새 기사 기능을 관리자 앱에 복제하지 않고, 같은 자격·계약을 가진 별도 기사 역할로 명시적으로 전환하는 방향을 사용한다.
- 구체적인 현행 대조와 단계는 [OS별 운영자 모바일·현장 참여 r3](operator-mobile-field-participation.r1.md)이 소유한다.

## 확정

- 총괄 운영 관점은 `SsalddelAdminApp` 한 앱에서 OS별 모듈로 통합한다.
- 주문자·판매자·기사·마트·창고·화주 등은 같은 원장을 자기 역할의 질문과 허용 행동으로 본다.
- 역할별 작업공간은 서버 OS와 별개이며 새 원장이나 중복 상태기를 만들지 않는다.
- 처음에는 통합 Host로 시작하고 나중에 같은 계약을 유지한 채 역할 앱으로 분리할 수 있다.
- 음식 판매 OS는 기존 `FoodDeliveryOS`의 음식점 역할 작업공간으로 시작한다.
- 음식 배달 기사 첫 화면은 지도이며 왼쪽 상단에 `신규 배차 받기` 토글, 하단에 `운행 시작` 버튼을 둔다. 운행 상태와 신규 제안 수신 의사는 분리한다.
- 음식 배달 기사 지도 오른쪽 상단의 사람 아이콘은 별도 `내 정보` 화면으로 이동한다. 기본 탭은 기사 관점의 `배달 내역`이며 정산·운행 정보·설정은 같은 원장의 보조 조회로 연결한다.
- 음식 배달 완료 이력은 화물 기사 화면 구조를 참고할 수 있지만 화물 상태·정산을 복제하지 않는다. 월 누적 운행 거리는 단말 GPS 추측값이 아니라 서버가 인정한 거리 근거가 있을 때만 합산한다.
- 음식점과 기사 양쪽은 같은 완료 Event에서 계산한 `함께 완료한 배달 N회`를 자기 역할 화면에서 본다. 이 수치는 배차·지급·평점에 영향을 주지 않고 자동 친구 관계도 만들지 않는다.
- 관계 배지는 클릭해 함께 완료한 배달 목록으로 이동한다. 기사와 음식점은 각각 상대 화면 노출을 기본 ON 상태에서 끌 수 있고, 설정 조회 실패 시에는 배지를 숨긴다. 노출 철회는 완료 원장을 삭제하지 않는다.
- 기사 본인의 완료 배달 상세는 제안 시점에 동결된 기본 지급·거리 추가·기상·수요 할증과 총액·정산 상태를 분리해 보여 준다. 관계 상세에는 이 개인 지급 구성을 공개하지 않는다.
- 기사 지급 상세의 표준 행은 적용 여부와 관계없이 항상 표시한다. 미적용은 `0원 · 미적용`, 해당 판본에 세부 근거가 없는 과거 자료는 `세부 근거 없음`, 조회 실패는 `확인할 수 없음 · 다시 조회`로 구분한다.
- 지급 보류의 내부 사유는 운영자·고객센터 작업공간이 소유하고, 기사 작업공간에는 `지급 보류`와 고객센터 문의 진입점만 제공한다.
- 기사 지급 구성은 기본 펼침·선택 접힘으로 제공하고, 공제 전 지급액·기사 부담 법정 공제·실지급액을 분리한다. 세금·고용보험·산재보험의 적용과 금액은 서버 정산 결과가 소유하며 플랫폼 부담분을 기사 금액에서 빼지 않는다.
- 플랫폼 부담 보험료는 기본 지급 화면에서 숨기고 기사가 `보험료 납부 내역`을 열었을 때만 `플랫폼 부담 · 기사 실지급액에서 차감하지 않음`으로 보여 준다.
- 마트 관리자 홈·업무 흐름·피킹/포장 제품 UI는 이미 존재하므로 새 `마트 첫 화면`을 공백으로 취급하지 않는다. 현행 UI의 서버 데이터 결속 검증으로 전환한다.
- 주문자 홈은 진행 주문을 먼저 보여 주고, 아래의 상점 탐색은 서버의 음식점 탐색 반경 정책을 사용한다.
- 주문자의 `10km` 장거리 탐색에서 배차 지연·미성사와 음식 품질 변화 가능성을 안내할 수 있다는 후보를 보존하되, 세부 화면과 다른 음식 유형으로의 확장은 역할별 진입점 정리 뒤로 미룬다.
- 주문 상세는 활성 배달의 주문 결속 위치 사본을 지도에 표시하되 기사 전용 위치 API·원시 이동 이력·개인 식별정보를 주문자에게 직접 제공하지 않는다.
- Unity는 역할 업무 앱이 아니라 읽기 전용 관찰 소비자다.
- 운영자가 현장에 참여할 때 관리자 권한으로 기사 업무를 우회하지 않고 현재 역할을 명시적으로 전환한다.
- 현장 회고는 비식별 운영 개선 자료이며 개인 귀책·평점·제재를 자동 확정하지 않는다.
- 일상 모바일 운영의 중심은 각 OS 운영자 작업공간이다. 총괄은 정책·권한·재무·OS 간 최종 충돌을 다루는 후순위 관리층이다.
- 각 OS 관리자·운영자 화면은 자주색, 총괄 관리자 화면은 황극 의미의 황색을 사용한다. 주의·지연·위험은 계속 주황·빨강으로 표시한다.
- 배차·기사 수급 화면은 정상 주문·기사의 개별 목록보다 대기시간·제안 가능 기사·수락률·재탐색·지급 영향을 집계하고, 장기 무배차·자료 불완전·지급 이상·회복 실패만 상세로 연다.
- 운영자 화면의 첫 목표는 현재 정본과 관측 통계를 정확히 정리하는 것이다. 미래 추가 지급 전망은 제외하고 예외 상세에는 이미 동결된 지급 구성만 보조 정보로 둔다.
- 기본 통계는 최근 60분 추세와 오늘 누적을 함께 보여 주며 어제·7일·사용자 지정 기간은 별도 상세에서 연다.
- 예외 목록은 안전·업무중단·판단자료 부족·장기 대기 순으로 분류하고 같은 단계에서 오래 기다린 업무를 먼저 보여 준다. 이 순서는 표시 우선순위이며 기사 평가·배차 점수·지급·귀책을 바꾸지 않는다.
- 예외 상세에서는 서버 정본 새로고침이 성공한 뒤 최신 허용 행동만 연다. 실패하면 마지막 정상 자료를 읽기 전용으로 남긴다.

## 미정

- 이름 검색에서 탐색 반경 밖 상점을 숨길지 주문 불가로 표시할지
- 기사 위치 공유를 기사 배정 직후부터 열지 픽업 완료 뒤부터 열지
- 활성 배달 중 위치 전송·조회 주기와 오래된 위치 표시 기준
- 마트 작업공간을 `SellerApp`에 둘지 별도 얇은 Host로 둘지
- 한 사용자가 여러 역할을 가질 때 기본 역할·마지막 역할 복원 방식
- OS별 관리자 권한과 총괄 관리자 권한의 첫 capability 표
- 역할 모듈을 물리 앱으로 분리하는 업무량·보안·조직 기준
- 음식 배달 기사가 운행 전에도 신규 배차 수신 의사를 미리 선택할 수 있는지
- 완료 배달 상세의 단순화 경로를 기본 표시할지 선택적으로 열지
- 첫 운영자 현장 참여를 합성 주문 훈련으로 시작할지 실제 배차로 시작할지
- 운영자↔기사 역할 전환의 Host·세션·감사·복귀 방식

## 다음 질문 하나

일반 예외는 기록이 남는 앱 내 메시지를 기본으로 하고, 안전 위험·현장 진행 불가에만 전화 진입을 앞세울까?

추천은 구분이다. 연락 필요성과 긴급도를 분리하면서 연락처 노출과 불필요한 통화를 줄일 수 있다.
