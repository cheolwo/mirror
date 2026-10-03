# DriverApp-FoodDeliveries - 음식 배달 업무

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [전체 route](../../current-pages.md) / [이번 보완 결과](../../implementation-r1.md)

기존 전용 캡처 없음. 소스/자동 시험과 실제 단말 조작·캡처는 별도로 확인한다.

## 1. 페이지: 제안 총액 확인 → 수락 → 픽업 → 전달

| 항목 | 현재 연결 |
| --- | --- |
| route / 소스 | `/driver/food-deliveries` / [음식배달업무Page](../../../../../DriverApp/Components/Pages/Driver/03_Progress/음식배달업무Page.razor) |
| 역할/기능 | 기사 / V3.0 음식배달, FoodDeliveryWorkflow feature gate |
| 표시 | 공제 전 `DriverPayout` 총액 한 개, 픽업/전달 장소·거리·추천 이유·만료·현재 상태, 월 이용료 요약 |
| 행동 | 제안 수락/거절, 묶음 수락, 픽업/전달 완료. 제안별 중복 클릭 차단 |
| 조회/오류 | 로딩·빈 목록·오류 구별, 실패 시 workspace=null. 동작 후 workspace 재조회가 성공해야 성공 안내 |
| 화면 간 인계 | 음식점은 주문 인지 뒤 유효한 기사 배정이 있어야 명시적으로 조리 시작. 기사 수락을 조리 완료로 취급하지 않음 |

단건/묶음의 경로 제안과 사적 배달 영상 분류는 별개다. 사적 영상·주소·실제 보험료를 이 화면에 넣지 않았다. 현재 이 route의 소스 포함 호스트는 DriverApp이며 별도 Web 역할 호스트의 기사업무 화면을 이 MAUI 화면의 실행 증거로 취급하지 않는다.

## 2. 코드: Client → Controller → 업무/경로 Service

| 단계 | 코드와 책임 |
| --- | --- |
| Client | [DriverFoodDeliveryApiService](../../../../../DriverApp/Services/DriverFoodDeliveryApiService.cs): 기존 인증 [DriverApiClient](../../../../../DriverApp/Services/DriverApiClient.cs) 이용 |
| 계약 | [FoodDeliveryDriverWorkspaceDtos](../../../../../Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs): 제안/활성 배달/묶음 후보·가용 행동·이용료 |
| Controller | [음식배달기사업무Controller](../../../../../Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs): token의 기사 ID·역할, FoodDeliveryWorkflow 게이트 |
| 읽기 | [FoodDeliveryDriverWorkspaceUseCase](../../../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs): 추천/활성 업무·최근 배달시도·월 이용료 조합, [경로 Service](../../../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverRouteService.cs) |
| 쓰기 | [음식배달기사업무Service](../../../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs): 소유자·제안 만료·현재 상태·유효 시도·활성 건수 검증 후 수락/완료 |
| 제안 금액 | [음식배달기사제안요금Service](../../../../../Ssalddel/Services/Dispatch/Recommendation/음식배달기사제안요금Service.cs) → [Domain Policy](../../../../../Ssalddel.Domain/음식/음식배달기사제안요금Policy.cs): 배차 시 계산, 재추천/완료에서 소급 계산하지 않음 |

모든 경로의 접두사는 `api/v1/driver/food-deliveries`다.

| HTTP | suffix | 효과 |
| --- | --- | --- |
| GET | `/workspace` | 인증 기사 추천/활성/이용료 조회 |
| POST | `/offers/{offerId}/accept`, `/reject` | 배차 수락/거절 |
| POST | `/bundles/accept` | 명시한 OfferIds 묶음의 현재 조건 검증·수락 |
| POST | `/offers/{offerId}/pickup-complete`, `/delivery-complete` | 현재 시도 픽업/전달 상태 변경 |

UI가 버튼을 보여주는 것만으로 상태 전이 가능성을 확정하지 않는다. 인증 실패/만료/상태 충돌은 서버 오류로 표시하고 재조회한다. 자동 추천은 Operational 모드와 기능 활성화가 함께 필요하며 Simulation 표현을 실제 배차 완료로 해석하지 않는다.

## 3. DB·원장: 주문·실행 투영·시도의 결속

| 자료 | 연결/조건 | 읽기·쓰기 |
| --- | --- | --- |
| [운송원장 Configuration](../../../../../Ssalddel.Infrastructure/Persistence/Configurations/Transport/운송원장Configuration.cs) → `운송실행투영` | Id PK, 의뢰Id=`request_id` unique. 음식배달 업무 유형, 제안Id·기사/확정기사 일치 | 추천 총액·정책판본·판정시각·`driver_offer_calculation_json` 동결, 수락/픽업/완료 단계 기록 |
| 음식주문·상태이력 | 제안의 원본 주문번호로 조회. 조리 시작 이력·현재 배차 상태를 유지 | 수락은 기사배정, 픽업은 픽업완료, 전달은 전달완료. 다른 기사/종료된 시도 차단 |
| [음식배달시도 Configuration](../../../../../Ssalddel.Infrastructure/Persistence/Configurations/Food/음식조리배달운영Configuration.cs) | 주문번호 + 제안Id + 기사Id + 시도순번/시도StableId. 시도StableId와 주문번호/시도순번 각각 unique | 수락 새 시도, 가게 도착/픽업/전달 UTC시각·중단 사유·revision. 이전 시도와 현재 시도 구별 |
| 운송이벤트·운영배차활동사건 | 제안·주문·기사·시도 ID | 상태/위치 감사·수락/중단/완료 활동. 주문/투영과 정합 처리 후 별도 알림·커뮤니티 환류 |
| 기사월정산 | 기사Id·년도·월 | 음식 배차의 **플랫폼 이용료**. 세전 기사 수입·법정 공제·지사 지급과 별도 |

저장/원장 동기화·음식점 알림·커뮤니티 발행은 Service의 처리 순서를 따른다. UI 성공은 동작 응답 후 GET workspace로 다시 확인한 상태이며 Mongo/SignalR 구독자·실단말 전체 통합을 뜻하지 않는다. 월 이용료 오류 Result의 기본 응답 대체는 기존 소스 한계로 남기며 실정산 확인으로 승격하지 않는다.

## 결손과 검증

이번에 누락된 상세 문서를 추가했다. 제안 총액 표시·원장 동결·저장 후 재조회는 기존 [r8 검증](../../../../AI/Planning/공통/PLAN-OPERATIONS-FOOD-DRIVER-PAYOUT-DETAIL/dispatch-calculation.integration.r8.md)을 유지하고 새 보험/공제 화면은 만들지 않았다. 과거 시도·동결 가격·새 요율 schema는 r8 migration을 검토한 뒤 운영 DB에 적용해야 한다. 실제 제품 HTTP·운영 DB·기기·송금은 미검증이다.
