# 음식 배달 경험 공유 — 페이지·코드·DB 책임

기준은 [승인 구현 r47](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/delivery-experience-sharing.implementation.r47.md)이다. 상태는 구현·검증 완료 후 [변경 기록](../../Changes/2026-10-09-delivery-experience-sharing-r47.md)에서 확인한다.

## 페이지가 보여 주는 정보

| 역할·상태 | 표시 위치 | 필요한 정보 | 행동 |
| --- | --- | --- | --- |
| 기사, 픽업 전 | 현재 음식점 카드 | 해당 지점의 픽업 완료 건수 | 기존 가게 도착/픽업 완료 |
| 기사, 픽업 후 | 현재 전달지 카드 | 확인된 도로·단지의 전달 완료 건수 | 기존 전달 완료 |
| 기사, 내 정보 | 배달 경험 공유 | 기능 준비 여부·현재 공유 선택·저장 결과 | 선택 공유 저장 |
| 음식점, 기사 배정 | 주문 진행 영역 | 공유에 동의한 현재 기사의 자기 지점 경험 | 기존 조리/픽업 준비 |
| 주문자, 전달 중 | 배달 진행 영역 | 공유에 동의한 현재 기사의 자기 전달 지역 경험 | 기존 수령 확인/문제 접수 |

새 업무 카드·별도 경험 목록·신뢰 점수는 만들지 않는다. 기사 수행 지도와 하단 카드의 독립 배치는 유지한다. 타인 요약은 종료/취소/재배정/철회 뒤 숨기며 다른 고객 기록으로 연결하지 않는다.

## 코드의 책임

- 공통 계약은 완료 **건수**와 확인 상태를 전달한다. 화면이나 알림 수신이 수치를 올리지 않는다. DTO의 기존 필드·라우트·업무 명령은 유지한다.
- 서버는 현재 수행, 기사 계정, 매장/주문자 소유권과 공유 선택을 검증한 뒤 필요한 장소의 요약만 조합한다.
- 전문 기사 앱과 공용 역할 화면은 동일한 요약 문구를 사용한다. null·미확인·조회 실패를 0으로 바꾸지 않고 조회 세대·계정 전환에 따라 기존 요약을 지운다.
- 음식점 상태 알림과 주문자 진행 주문 재조회는 정본 갱신의 신호다. 별도 원격 푸시/소리/인박스로 보고하지 않는다.
- 주소 검색에서 선택한 공식 식별자는 현재 수령지 기본주소와 결속한다. 기본주소가 변경되면 선택을 해제하고 상세 동·호수 입력은 유지한다. plain 주소 주문은 기존처럼 처리한다.

## DB의 책임

음식 주문·배달 시도가 업무 원본이다. 경험은 기존 원본에 연결된 후속 집계이며 주문·기사·완료 종류별 중복 처리 방지를 갖춘다. 매장·도로·단지는 독립 식별자를 사용하고 실행 모드를 구분한다. 기사 공유 설정은 서버에 저장하며 앱별 로컬 설정이나 알림 설정으로 대신하지 않는다.

집계용 원장과 Outbox는 원본 완료 시도에 연결하지만 API에는 다른 고객, 과거 전달 시각, 상세 주소·좌표·경로를 반환하지 않는다. 기존 고객 상세 열람 3일 제한을 유지한다. 로컬 모델·migration 검증은 운영 DB 적용이나 실제 배달 이력 확인과 다르다.

## 페이지에서 원장까지의 연결

| 페이지 | 코드의 처리 경계 | DB 근거 |
| --- | --- | --- |
| [기사 현재 업무·내 정보](../../../FDriverApp/Pages/MainPage.xaml) | [현재 단계·공유 저장](../../../FDriverApp/PageModels/MainPageModel.DeliveryExperience.cs), [공유 UseCase](../../../Ssalddel/Application/Driver/Settings/기사배달경험공유UseCase.cs) | 음식배달경험공유설정의 본인 설정·Revision, 음식배달경험Fact의 본인 완료 건수 |
| [음식점 주문 상세](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor) | [음식점 조회 UseCase](../../../Ssalddel/Application/Food/음식점음식주문조회UseCase.cs) | 현재 배차·자기 지점·기사 공유 동의 |
| [주문자 진행 주문](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderDetailPanel.razor) | [주문자 조회 UseCase](../../../Ssalddel/Application/Food/주문자음식주문조회UseCase.cs) | 주문 소유권·현재 전달 기사·독립된 도로/단지 근거 |
| [주문 입력](../../../Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererFoodOrderComposer.razor) | [주소 선택 결속](../../../Ssalddel.Ui.Common/Areas/App/ViewModels/음식주문작성ViewModel.DestinationIdentity.cs), [공식 주소 대조](../../../Ssalddel/Services/DeliveryExperience/FoodDeliveryDestinationResolver.cs), [등록 transaction](../../../Ssalddel/Application/Food/Handlers/음식주문등록CommandHandler.cs) | 음식배달전달지역Snapshot, 상세주소를 복제하지 않는 주문 시점 근거 |
| 기존 픽업·전달 완료 버튼 | [기사 업무 서비스](../../../Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs), [완료 수집·투영](../../../Ssalddel/Services/DeliveryExperience/FoodDeliveryExperienceService.cs) | 음식배달시도 → 같은 transaction의 음식배달경험완료Outbox → 멱등 음식배달경험Fact |

[모델·설정](../../../Ssalddel.Domain/음식/음식배달경험.cs)과 [추가 마이그레이션](../../../Ssalddel/Migrations/20261009150000_AddFoodDeliveryExperienceR47.cs)은 배포 전 스키마 준비 대상이다. 기능 OFF의 공유·집계 접근 차단만으로 기존 음식배달시도에 추가된 컬럼 없이 새 서버를 배포할 수 있다는 뜻은 아니다.
