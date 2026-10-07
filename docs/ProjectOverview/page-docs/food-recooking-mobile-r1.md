# 재조리와 새 기사 인계의 모바일 책임

2026-10-04. 기존 음식 배달의 픽업 후 중단 → 재조리 → 새 기사 픽업 → 전달 흐름을 보완한다. [업무 실행 책임](../../Architecture/BusinessWorkflowResponsibilityModel.md), [페이지 단일 책임](../../Architecture/WholeRoadmapPagePrinciple.md), [기존 Android 상태 기준](mobile-workflow-readiness-r1.md)을 재사용한다.

## 페이지 책임

| 화면 | 현재 책임 | 상태별 정보와 행동 |
| --- | --- | --- |
| 음식점 주문 상세 | 이번 음식의 조리·픽업 인계 | 다시 조리 중 / 이번 준비 완료를 구분한다. 서버 AvailableActions의 준비 완료를 명시 요청하고 같은 주문을 다시 조회한다. 첫 음식의 준비 완료를 현재 음식에 적용하지 않는다. |
| 음식 기사 현재 업무 | 본인의 현재 배달 시도 | 새 시도에서 음식점 이동 → 도착·준비 대기 → 픽업 → 전달을 표시한다. 준비 예정과 실제 준비 완료를 구별하며 허용된 픽업만 실행한다. |
| 주문자 선택 상세 | 동일 주문의 진행·수령 확인 | 새 주문을 생성하지 않고 동일 주문번호의 최신 상태를 조회한다. 전달 완료 이후 기존 수령 확인을 사용한다. |
| 운영자 주문 추적 | 시도 이력과 현재 책임 | 중단된 시도와 새 시도를 보존한다. 이전 음식의 픽업 이력을 현재 음식의 배송으로 표시하지 않는다. |

새 페이지나 업무 명령을 만들지 않는다. [역할 앱 시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)의 기존 카드·버튼을 유지하며 재조리 설명은 해당 진행 단계에서만 보인다. 화물의 상하차·취소·재배차 정책에는 적용하지 않는다.

## 페이지 → 코드/API → DB

| 연결 | 코드/API | 저장과 판정 |
| --- | --- | --- |
| 음식점 현재 조리 | [OrderDetail](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor), [DeskModel](../../../RestaurantDeskApp/Models/Restaurant/음식점주문DeskModels.cs) → 기존 `api/v1/food-orders/restaurant/inbox/{orderNo}` 조회·`{orderNo}/restaurant-progress` 변경 | [EF Store](../../../Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs)는 동일 주문과 이력을 읽고 revision·권한을 검증한다. |
| 현재 준비와 행동 | [현재 조리 Policy](../../../Ssalddel/Services/Food/음식주문현재조리Policy.cs) → [행동 Projector](../../../Ssalddel/Application/Food/음식배달가능행동Projector.cs) | 기존 `음식주문상태이력`의 픽업 후 중단 사건을 차수 경계로 삼는다. 픽업 전 기사 교체는 같은 음식의 준비를 유지한다. |
| 새 기사 인계 | [MainPageModel](../../../FDriverApp/PageModels/MainPageModel.cs) → 기존 `GET api/v1/driver/food-deliveries/workspace`·`POST offers/{offerId}/accept\|restaurant-arrival\|interruption\|pickup-complete\|delivery-complete` → [Workspace](../../../Ssalddel/Application/Driver/Food/FoodDeliveryDriverWorkspaceUseCase.cs)·[WorkService](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs) | 주문번호·제안·기사·`음식배달시도`를 결속한다. 조회 행동과 실제 픽업 검증이 같은 현재 준비 판정을 사용한다. 중단 기사에게 새 시도의 권한을 주지 않는다. |
| 운영 상태 사본 | [수명주기 Snapshot](../../../Ssalddel/Application/Food/음식배달수명주기SnapshotFactory.cs) | 현재 재조리 이후의 준비·픽업·전달을 표시한다. 원본 이력은 삭제하지 않는다. |

[응답 DTO](../../../Ssalddel.Contracts/Food/음식주문Dtos.cs)의 `CurrentPreparationRound`, `CurrentCookingStartedAtUtc`, `CurrentPickupReadyAtUtc`, `RecookingRequestedAtUtc`는 조회 투영이다. 기존 첫 조리·첫 준비 시각은 보존한다. [기사 DTO](../../../Ssalddel.Contracts/Driver/Food/FoodDeliveryDriverWorkspaceDtos.cs)는 현재 차수·실제 준비·재조리 요청을 전달하며 `DisplayedPreparationReadyAtUtc`는 예정 시각으로 유지한다. 테이블·migration·가격·공제·기본 공개 기능은 변경하지 않는다.

준비 완료 때 저장한 0분은 새 음식의 조리시간이 아니다. 재조리 예정은 양수인 적용 조리분, 플랫폼 참고 조리분, 기존 기본값 20분 순으로 사용하며 음식점이 기존 시간 변경으로 조정한다. 예정 시각이 지났다는 이유로 준비 완료나 픽업을 허용하지 않는다. 이번 차수의 음식이 먼저 준비됐다면 새 기사의 수락보다 앞선 준비도 유효하다.

## 실행·설치 경계

[역할 Client 검증 도구](../../../eng/Ssalddel.RoleAppHeadlessE2E/Program.cs)의 `--recook`는 같은 주문에서 두 기사·음식점·주문자·관리자의 HTTP 재조회와 종료를 검증한다. `--prepare-recook-ui`는 재조리·새 기사 도착 상태에서 멈춰 APK 조작을 준비한다. 합성 계정·거리 입력의 격리 Development/Simulation 실행이며 실배달·지도·은행 입금이 아니다.

공개 HTTPS 주소 주입·Release 서명·Azure 설정은 [저비용 VM](../../Deployment/AzureLowCostVm.md), [App Service](../../Deployment/AzureAppService.md)와 기존 field-test 도구를 따른다. 서버·DB 실행, APK 생성·설치·화면 조작, 개인 휴대폰 검증, Azure 배포는 별도 결과다. 현재 결과는 [변경 기록](../../Changes/2026-10-04-food-recooking-mobile-r1.md)에 기록한다.
