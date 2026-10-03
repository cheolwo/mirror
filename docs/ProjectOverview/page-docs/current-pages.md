# 현재 페이지 소스·호스트 목록

이 파일은 `eng/page-docs/catalog.py --write`로 생성한다. 소스와 MSBuild Content/RazorComponent 평가 결과이며 실제 사용자 조작·캡처·출시 완료를 뜻하지 않는다.

물리 페이지 파일 **466개**, route 선언 **538개**. 별칭 route는 같은 페이지에 묶고, 여러 호스트에서 재사용하는 소스는 한 번만 센다.

기존 P ID와 캡처는 보존한다. 새 파일에는 경로 기반 ID를 부여하고 `page-identities.json`에 고정한다. 과거 문서가 있다는 사실과 이번 페이지→코드→DB 상세 검토 완료는 별개다.

## 실행 호스트의 소스 포함

| 호스트 | 페이지 파일 | route 선언 |
| --- | ---: | ---: |
| `DriverApp` | 25 | 25 |
| `FDriverApp` | 2 | 2 |
| `HumanResourcesManagerApp` | 1 | 1 |
| `OrdererApp` | 46 | 49 |
| `RestaurantDeskApp` | 12 | 13 |
| `SellerApp` | 13 | 13 |
| `Ssalddel.Web.CommunityApp` | 50 | 58 |
| `Ssalddel.Web.DriverApp` | 22 | 23 |
| `Ssalddel.Web.OrdererApp` | 32 | 39 |
| `Ssalddel.Web.ShipperApp` | 49 | 51 |
| `Ssalddel.Web.UnityReviewApp` | 3 | 5 |
| `Ssalddel.Web.WarehouseApp` | 36 | 58 |
| `Ssalddel.WebApp` | 156 | 195 |
| `SsalddelAdmin` | 50 | 65 |
| `SsalddelAdminApp` | 11 | 12 |
| `SsalddelApp` | 97 | 104 |
| `WarehouseManagerApp` | 48 | 52 |

호스트 표는 컴파일 입력이다. 기능 플래그·인증·실행 모드의 실제 접근 가능 여부는 각 화면에서 확인한다. `eng/web-role-app`의 공통 진입 화면은 제품 역할 호스트에 포함되므로 목록에 남긴다.

## DriverApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `DriverApp-P06` | `/driver/work/start` | [운행시작Page.razor](../../../DriverApp/Components/Pages/Driver/01_Work/운행시작Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P06/README.md) |
| `DriverApp-Components-Pages-Driver-01_Work-커뮤니티개별의뢰Page` | `/driver/work/community-inquiries` | [커뮤니티개별의뢰Page.razor](../../../DriverApp/Components/Pages/Driver/01_Work/커뮤니티개별의뢰Page.razor) | DriverApp | 미검토 |
| `DriverApp-P10` | `/driver/recommendations/{의뢰Id}/decision` | [배차처리Page.razor](../../../DriverApp/Components/Pages/Driver/02_Recommendation/배차처리Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P10/README.md) |
| `DriverApp-P08` | `/driver/recommendations` | [추천목록Page.razor](../../../DriverApp/Components/Pages/Driver/02_Recommendation/추천목록Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P08/README.md) |
| `DriverApp-P09` | `/driver/recommendations/{의뢰Id}` | [추천상세Page.razor](../../../DriverApp/Components/Pages/Driver/02_Recommendation/추천상세Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P09/README.md) |
| `DriverApp-P04` | `/driver/exploration/campaigns` | [탐색캠페인Page.razor](../../../DriverApp/Components/Pages/Driver/02_Recommendation/탐색캠페인Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P04/README.md) |
| `DriverApp-P05` | `/driver/transports/history` | [배달내역Page.razor](../../../DriverApp/Components/Pages/Driver/03_Progress/배달내역Page.razor) | DriverApp | [페이지→코드→DB](DriverApp/DriverApp-P05/README.md) |
| `DriverApp-P12` | `/driver/transports/{운송Id:long}/pickup` | [상차Page.razor](../../../DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P12/README.md) |
| `DriverApp-FoodDeliveries` | `/driver/food-deliveries` | [음식배달업무Page.razor](../../../DriverApp/Components/Pages/Driver/03_Progress/음식배달업무Page.razor) | DriverApp | [페이지→코드→DB](DriverApp/DriverApp-FoodDeliveries/README.md) |
| `DriverApp-P11` | `/driver/transports/current` | [진행중운송Page.razor](../../../DriverApp/Components/Pages/Driver/03_Progress/진행중운송Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P11/README.md) |
| `DriverApp-P13` | `/driver/transports/{운송Id:long}/dropoff` | [하차Page.razor](../../../DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P13/README.md) |
| `DriverApp-P03` | `/driver/reservations` | [예약Page.razor](../../../DriverApp/Components/Pages/Driver/04_Reservation/예약Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P03/README.md) |
| `DriverApp-P02` | `/driver/menu` | [메뉴Page.razor](../../../DriverApp/Components/Pages/Driver/04_Settings/메뉴Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P02/README.md) |
| `DriverApp-P15-1` | `/driver/notifications/settings` | [알림설정Page.razor](../../../DriverApp/Components/Pages/Driver/04_Settings/알림설정Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P15-1/README.md) |
| `DriverApp-P06-1` | `/driver/work/settings` | [운행설정Page.razor](../../../DriverApp/Components/Pages/Driver/04_Settings/운행설정Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P06-1/README.md) |
| `DriverApp-P02-1` | `/driver/settings/views` | [화면설정Page.razor](../../../DriverApp/Components/Pages/Driver/04_Settings/화면설정Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P02-1/README.md) |
| `DriverApp-P14-2` | `/driver/account/bank` | [계좌정보Page.razor](../../../DriverApp/Components/Pages/Driver/05_Settlement/계좌정보Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P14-2/README.md) |
| `DriverApp-P14` | `/driver/settlements/current-month` | [월정산Page.razor](../../../DriverApp/Components/Pages/Driver/05_Settlement/월정산Page.razor) | DriverApp | [페이지→코드→DB](DriverApp/DriverApp-P14/README.md) |
| `DriverApp-P14-1` | `/driver/settlements/info` | [이용료안내Page.razor](../../../DriverApp/Components/Pages/Driver/05_Settlement/이용료안내Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P14-1/README.md) |
| `DriverApp-P15` | `/driver/notifications` | [알림함Page.razor](../../../DriverApp/Components/Pages/Driver/06_Notification/알림함Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P15/README.md) |
| `DriverApp-P15-2` | `/driver/notifications/push` | [푸시설정Page.razor](../../../DriverApp/Components/Pages/Driver/06_Notification/푸시설정Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P15-2/README.md) |
| `DriverApp-P07-1` | `/driver/home/summary` | [기사홈Page.razor](../../../DriverApp/Components/Pages/Driver/Home/기사홈Page.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P07-1/README.md) |
| `DriverApp-P07` | `/driver/home` | [Home.razor](../../../DriverApp/Components/Pages/Home.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P07/README.md) |
| `DriverApp-P01` | `/login` | [Login.razor](../../../DriverApp/Components/Pages/Login.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P01/README.md) |
| `DriverApp-P00` | `/` | [RootRedirect.razor](../../../DriverApp/Components/Pages/RootRedirect.razor) | DriverApp | [이전 화면 문서](DriverApp/DriverApp-P00/README.md) |

## FDriverApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `FDriverApp-Components-Pages-FDriverHome` | `/` | [FDriverHome.razor](../../../FDriverApp/Components/Pages/FDriverHome.razor) | FDriverApp | 미검토 |
| `FDriverApp-Components-Pages-FDriverWorkspaceLaunchPage` | `/food-delivery/open/{Focus}` | [FDriverWorkspaceLaunchPage.razor](../../../FDriverApp/Components/Pages/FDriverWorkspaceLaunchPage.razor) | FDriverApp | 미검토 |

## HumanResourcesManagerApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `HumanResourcesManagerApp-P01` | `/` | [Home.razor](../../../HumanResourcesManagerApp/Components/Pages/Home.razor) | HumanResourcesManagerApp | [이전 화면 문서](HumanResourcesManagerApp/HumanResourcesManagerApp-P01/README.md) |

## OrdererApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `OrdererApp-P03` | `/cargo` | [CargoOrder.razor](../../../OrdererApp/Components/Pages/CargoOrder.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P03/README.md) |
| `OrdererApp-Components-Pages-FoodOrderHistory` | `/orders/food` | [FoodOrderHistory.razor](../../../OrdererApp/Components/Pages/FoodOrderHistory.razor) | OrdererApp | 미검토 |
| `OrdererApp-P04` | `/food` | [FoodOrderHome.razor](../../../OrdererApp/Components/Pages/FoodOrderHome.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P04/README.md) |
| `OrdererApp-Components-Pages-GroupExportLedger` | `/trade/group-export` | [GroupExportLedger.razor](../../../OrdererApp/Components/Pages/GroupExportLedger.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupImportReadinessClassification` | `/group-purchase/imports/{LedgerId}/classification` | [GroupImportReadinessClassification.razor](../../../OrdererApp/Components/Pages/GroupImportReadinessClassification.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupImportReadinessConsent` | `/group-purchase/imports/{LedgerId}/consent` | [GroupImportReadinessConsent.razor](../../../OrdererApp/Components/Pages/GroupImportReadinessConsent.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupImportReadinessCosts` | `/group-purchase/imports/{LedgerId}/costs` | [GroupImportReadinessCosts.razor](../../../OrdererApp/Components/Pages/GroupImportReadinessCosts.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupImportReadinessHandoff` | `/group-purchase/imports/{LedgerId}/handoff` | [GroupImportReadinessHandoff.razor](../../../OrdererApp/Components/Pages/GroupImportReadinessHandoff.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupImportReadinessLogisticsReview` | `/group-purchase/imports/{LedgerId}/logistics-review` | [GroupImportReadinessLogisticsReview.razor](../../../OrdererApp/Components/Pages/GroupImportReadinessLogisticsReview.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupImportReadinessOverview` | `/group-purchase/imports/{LedgerId}` | [GroupImportReadinessOverview.razor](../../../OrdererApp/Components/Pages/GroupImportReadinessOverview.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupImportReadinessSuppliers` | `/group-purchase/imports/{LedgerId}/suppliers` | [GroupImportReadinessSuppliers.razor](../../../OrdererApp/Components/Pages/GroupImportReadinessSuppliers.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseDeliveryScopeDetail` | `/group-purchase/delivery-scopes/{DeliveryScopeKey}` | [GroupPurchaseDeliveryScopeDetail.razor](../../../OrdererApp/Components/Pages/GroupPurchaseDeliveryScopeDetail.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseDeliveryScopes` | `/group-purchase/delivery-scopes` | [GroupPurchaseDeliveryScopes.razor](../../../OrdererApp/Components/Pages/GroupPurchaseDeliveryScopes.razor) | OrdererApp | 미검토 |
| `OrdererApp-P02-3` | `/group-purchase/demands/new/{ProductId}` | [GroupPurchaseDemandCreate.razor](../../../OrdererApp/Components/Pages/GroupPurchaseDemandCreate.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseGroupDetail` | `/group-purchase/groups/{AutoGroupId}` | [GroupPurchaseGroupDetail.razor](../../../OrdererApp/Components/Pages/GroupPurchaseGroupDetail.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseGroups` | `/group-purchase/groups` | [GroupPurchaseGroups.razor](../../../OrdererApp/Components/Pages/GroupPurchaseGroups.razor) | OrdererApp | 미검토 |
| `OrdererApp-P02` | `/group-purchase` | [GroupPurchaseHome.razor](../../../OrdererApp/Components/Pages/GroupPurchaseHome.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P02/README.md) |
| `OrdererApp-P02-4` | `/group-purchase/import-review/{ProductId}` | [GroupPurchaseImportReview.razor](../../../OrdererApp/Components/Pages/GroupPurchaseImportReview.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseOrderModeComparison` | `/group-purchase/compare/{ProductId}` | [GroupPurchaseOrderModeComparison.razor](../../../OrdererApp/Components/Pages/GroupPurchaseOrderModeComparison.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchasePractice` | `/group-purchase/practice` | [GroupPurchasePractice.razor](../../../OrdererApp/Components/Pages/GroupPurchasePractice.razor) | OrdererApp | 미검토 |
| `OrdererApp-P02-2` | `/group-purchase/products/{ProductId}` | [GroupPurchaseProductDetail.razor](../../../OrdererApp/Components/Pages/GroupPurchaseProductDetail.razor) | OrdererApp | 미검토 |
| `OrdererApp-P02-1` | `/group-purchase/products` | [GroupPurchaseProducts.razor](../../../OrdererApp/Components/Pages/GroupPurchaseProducts.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseRecipeUse` | `/group-purchase/recipe-uses/{ProductId}` | [GroupPurchaseRecipeUse.razor](../../../OrdererApp/Components/Pages/GroupPurchaseRecipeUse.razor) | OrdererApp | 미검토 |
| `OrdererApp-P02-5` | `/group-purchase/shipments` | [GroupPurchaseShipments.razor](../../../OrdererApp/Components/Pages/GroupPurchaseShipments.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseSupplierMembership` | `/group-purchase/supplier-relationships/{SupplierKey}/membership` | [GroupPurchaseSupplierMembership.razor](../../../OrdererApp/Components/Pages/GroupPurchaseSupplierMembership.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseSupplierRelationship` | `/group-purchase/supplier-relationships/{SupplierKey}` | [GroupPurchaseSupplierRelationship.razor](../../../OrdererApp/Components/Pages/GroupPurchaseSupplierRelationship.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseTogetherOrderDetail` | `/group-purchase/together-orders/{AutoGroupId}` | [GroupPurchaseTogetherOrderDetail.razor](../../../OrdererApp/Components/Pages/GroupPurchaseTogetherOrderDetail.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseTogetherOrders` | `/group-purchase/together-orders` | [GroupPurchaseTogetherOrders.razor](../../../OrdererApp/Components/Pages/GroupPurchaseTogetherOrders.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseUrgentHarvestOffer` | `/group-purchase/urgent-harvest-offers/{SupplyOfferDraftId}` | [GroupPurchaseUrgentHarvestOffer.razor](../../../OrdererApp/Components/Pages/GroupPurchaseUrgentHarvestOffer.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseUrgentHarvestReview` | `/group-purchase/urgent-harvest-offers/{SupplyOfferDraftId}/review` | [GroupPurchaseUrgentHarvestReview.razor](../../../OrdererApp/Components/Pages/GroupPurchaseUrgentHarvestReview.razor) | OrdererApp | 미검토 |
| `OrdererApp-P02-1-2` | `/group-purchase/wishes/new` | [GroupPurchaseWishCreate.razor](../../../OrdererApp/Components/Pages/GroupPurchaseWishCreate.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseWishDetail` | `/group-purchase/wishes/{WishLedgerId}` | [GroupPurchaseWishDetail.razor](../../../OrdererApp/Components/Pages/GroupPurchaseWishDetail.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-GroupPurchaseWishEdit` | `/group-purchase/wishes/{WishLedgerId}/edit` | [GroupPurchaseWishEdit.razor](../../../OrdererApp/Components/Pages/GroupPurchaseWishEdit.razor) | OrdererApp | 미검토 |
| `OrdererApp-P02-1-1` | `/group-purchase/wishes` | [GroupPurchaseWishes.razor](../../../OrdererApp/Components/Pages/GroupPurchaseWishes.razor) | OrdererApp | 미검토 |
| `OrdererApp-P01` | `/` | [Home.razor](../../../OrdererApp/Components/Pages/Home.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P01/README.md) |
| `OrdererApp-Components-Pages-IndividualExportLedger` | `/trade/individual-export`<br>`/orders/{OrderLedgerId}/individual-export` | [IndividualExportLedger.razor](../../../OrdererApp/Components/Pages/IndividualExportLedger.razor) | OrdererApp | 미검토 |
| `OrdererApp-Components-Pages-IndividualImportLedger` | `/trade/individual-import`<br>`/orders/{OrderLedgerId}/individual-import` | [IndividualImportLedger.razor](../../../OrdererApp/Components/Pages/IndividualImportLedger.razor) | OrdererApp | 미검토 |
| `OrdererApp-P04-2` | `/food/mart` | [MartOrder.razor](../../../OrdererApp/Components/Pages/MartOrder.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P04-2/README.md) |
| `OrdererApp-P04-3` | `/food/mart/order`<br>`/food/mart/order/{ProductId:long}` | [MartOrderRequest.razor](../../../OrdererApp/Components/Pages/MartOrderRequest.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P04-3/README.md) |
| `OrdererApp-P04-2-1` | `/food/mart/products/{ProductId:long}` | [MartProductDetail.razor](../../../OrdererApp/Components/Pages/MartProductDetail.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P04-2-1/README.md) |
| `OrdererApp-P04-2-2` | `/food/mart/reviews/{ProductId:long}` | [MartProductReview.razor](../../../OrdererApp/Components/Pages/MartProductReview.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P04-2-2/README.md) |
| `OrdererApp-P99` | `/not-found` | [NotFound.razor](../../../OrdererApp/Components/Pages/NotFound.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P99/README.md) |
| `OrdererApp-Components-Pages-OrderDetail` | `/orders/{OrderLedgerId}` | [OrderDetail.razor](../../../OrdererApp/Components/Pages/OrderDetail.razor) | OrdererApp | 미검토 |
| `OrdererApp-P05` | `/orders` | [OrderHistory.razor](../../../OrdererApp/Components/Pages/OrderHistory.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P05/README.md) |
| `OrdererApp-Components-Pages-ProducePriceComparison` | `/group-purchase/produce-prices` | [ProducePriceComparison.razor](../../../OrdererApp/Components/Pages/ProducePriceComparison.razor) | OrdererApp | 미검토 |
| `OrdererApp-P04-1` | `/food/restaurants` | [RestaurantOrder.razor](../../../OrdererApp/Components/Pages/RestaurantOrder.razor) | OrdererApp | [이전 화면 문서](OrdererApp/OrdererApp-P04-1/README.md) |

## RestaurantDeskApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `RestaurantDeskApp-P04` | `/dispatch/address-form` | [DispatchAddressForm.razor](../../../RestaurantDeskApp/Components/Pages/DispatchAddressForm.razor) | RestaurantDeskApp | [이전 화면 문서](RestaurantDeskApp/RestaurantDeskApp-P04/README.md) |
| `RestaurantDeskApp-P01` | `/store` | [Home.razor](../../../RestaurantDeskApp/Components/Pages/Home.razor) | RestaurantDeskApp | [이전 화면 문서](RestaurantDeskApp/RestaurantDeskApp-P01/README.md) |
| `RestaurantDeskApp-Components-Pages-IngredientSupplyRequest` | `/ingredients/supply-request` | [IngredientSupplyRequest.razor](../../../RestaurantDeskApp/Components/Pages/IngredientSupplyRequest.razor) | RestaurantDeskApp | 미검토 |
| `RestaurantDeskApp-Components-Pages-Login` | `/login` | [Login.razor](../../../RestaurantDeskApp/Components/Pages/Login.razor) | RestaurantDeskApp | 미검토 |
| `RestaurantDeskApp-Components-Pages-Menus` | `/menus` | [Menus.razor](../../../RestaurantDeskApp/Components/Pages/Menus.razor) | RestaurantDeskApp | 미검토 |
| `RestaurantDeskApp-P02` | `/restaurants/nearby` | [NearbyRestaurants.razor](../../../RestaurantDeskApp/Components/Pages/NearbyRestaurants.razor) | RestaurantDeskApp | [이전 화면 문서](RestaurantDeskApp/RestaurantDeskApp-P02/README.md) |
| `RestaurantDeskApp-Components-Pages-NotFound` | `/not-found` | [NotFound.razor](../../../RestaurantDeskApp/Components/Pages/NotFound.razor) | RestaurantDeskApp | 미검토 |
| `RestaurantDeskApp-Components-Pages-OrderDetail` | `/orders/{OrderNo}` | [OrderDetail.razor](../../../RestaurantDeskApp/Components/Pages/OrderDetail.razor) | RestaurantDeskApp | 미검토 |
| `RestaurantDeskApp-Components-Pages-OrderInbox` | `/orders`<br>`/` | [OrderInbox.razor](../../../RestaurantDeskApp/Components/Pages/OrderInbox.razor) | RestaurantDeskApp | 미검토 |
| `RestaurantDeskApp-P02-1` | `/restaurants/popular` | [PopularRestaurants.razor](../../../RestaurantDeskApp/Components/Pages/PopularRestaurants.razor) | RestaurantDeskApp | [이전 화면 문서](RestaurantDeskApp/RestaurantDeskApp-P02-1/README.md) |
| `RestaurantDeskApp-Components-Pages-PreparationTimeSettings` | `/settings/preparation-times` | [PreparationTimeSettings.razor](../../../RestaurantDeskApp/Components/Pages/PreparationTimeSettings.razor) | RestaurantDeskApp | 미검토 |
| `RestaurantDeskApp-P03` | `/reviews/moderation` | [ReviewModeration.razor](../../../RestaurantDeskApp/Components/Pages/ReviewModeration.razor) | RestaurantDeskApp | [이전 화면 문서](RestaurantDeskApp/RestaurantDeskApp-P03/README.md) |

## SellerApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `SellerApp-Components-Pages-ForeignFoodFacilities` | `/seller/foreign-food-facilities` | [ForeignFoodFacilities.razor](../../../SellerApp/Components/Pages/ForeignFoodFacilities.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-Home` | `/` | [Home.razor](../../../SellerApp/Components/Pages/Home.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-Inventory` | `/seller/inventory` | [Inventory.razor](../../../SellerApp/Components/Pages/Inventory.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-ListingCreate` | `/shipper/sales/listings/new` | [ListingCreate.razor](../../../SellerApp/Components/Pages/ListingCreate.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-Listings` | `/shipper/sales/listings` | [Listings.razor](../../../SellerApp/Components/Pages/Listings.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-Login` | `/login` | [Login.razor](../../../SellerApp/Components/Pages/Login.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-OrderDetail` | `/shipper/sales/orders/{OrderId:long}` | [OrderDetail.razor](../../../SellerApp/Components/Pages/OrderDetail.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-OrdererDemand` | `/seller/orderer-demand` | [OrdererDemand.razor](../../../SellerApp/Components/Pages/OrdererDemand.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-Orders` | `/shipper/sales/orders` | [Orders.razor](../../../SellerApp/Components/Pages/Orders.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-ProductCreate` | `/shipper/sales/products/new` | [ProductCreate.razor](../../../SellerApp/Components/Pages/ProductCreate.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-Products` | `/shipper/sales/products` | [Products.razor](../../../SellerApp/Components/Pages/Products.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-SalesChannels` | `/shipper/sales/channels` | [SalesChannels.razor](../../../SellerApp/Components/Pages/SalesChannels.razor) | SellerApp | 미검토 |
| `SellerApp-Components-Pages-SalesPageComposer` | `/shipper/sales/pages/new` | [SalesPageComposer.razor](../../../SellerApp/Components/Pages/SalesPageComposer.razor) | SellerApp | 미검토 |

## Ssalddel.Web.UnityReviewApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `Ssalddel.Web.UnityReviewApp-Pages-LoginPage` | `/login` | [LoginPage.razor](../../../Ssalddel.Web.UnityReviewApp/Pages/LoginPage.razor) | Ssalddel.Web.UnityReviewApp | 미검토 |
| `Ssalddel.Web.UnityReviewApp-Pages-NotFound` | `/404` | [NotFound.razor](../../../Ssalddel.Web.UnityReviewApp/Pages/NotFound.razor) | Ssalddel.Web.UnityReviewApp | 미검토 |
| `Ssalddel.Web.UnityReviewApp-Pages-Synty공간조립Web검토Page` | `/`<br>`/reviews/compositions`<br>`/world/review/compositions` | [Synty공간조립Web검토Page.razor](../../../Ssalddel.Web.UnityReviewApp/Pages/Synty공간조립Web검토Page.razor) | Ssalddel.Web.UnityReviewApp | 미검토 |

## Ssalddel.WebApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `Ssalddel.WebApp-Pages-CommunityBaguaTransitionPage` | `/community/bagua/{SourceTrigramKey}/{TargetTrigramKey}`<br>`/community/bagua/{RoleCode}/{SourceTrigramKey}/{TargetTrigramKey}` | [CommunityBaguaTransitionPage.razor](../../../Ssalddel.WebApp/Pages/CommunityBaguaTransitionPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityBoardManagementPage` | `/community/boards/manage` | [CommunityBoardManagementPage.razor](../../../Ssalddel.WebApp/Pages/CommunityBoardManagementPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityBoardPage` | `/community/boards` | [CommunityBoardPage.razor](../../../Ssalddel.WebApp/Pages/CommunityBoardPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityCollectiveActionsPage` | `/community/actions`<br>`/community/actions/{PageKey}` | [CommunityCollectiveActionsPage.razor](../../../Ssalddel.WebApp/Pages/CommunityCollectiveActionsPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityDecorationCheckoutPage` | `/community/decorations/checkout/{ProductKey}` | [CommunityDecorationCheckoutPage.razor](../../../Ssalddel.WebApp/Pages/CommunityDecorationCheckoutPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityDecorationLegacyCheckoutPage` | `/community/decorations/{ProductKey}/checkout` | [CommunityDecorationLegacyCheckoutPage.razor](../../../Ssalddel.WebApp/Pages/CommunityDecorationLegacyCheckoutPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityDecorationLegacyProductPage` | `/community/decorations/{ProductKey}` | [CommunityDecorationLegacyProductPage.razor](../../../Ssalddel.WebApp/Pages/CommunityDecorationLegacyProductPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityDecorationProductPage` | `/community/decorations/products/{ProductKey}` | [CommunityDecorationProductPage.razor](../../../Ssalddel.WebApp/Pages/CommunityDecorationProductPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityDecorationStorePage` | `/community/decorations` | [CommunityDecorationStorePage.razor](../../../Ssalddel.WebApp/Pages/CommunityDecorationStorePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityDirectoryPage` | `/community/categories`<br>`/community/boards/directory` | [CommunityDirectoryPage.razor](../../../Ssalddel.WebApp/Pages/CommunityDirectoryPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityFoodDiscoveryPage` | `/community/discover/food` | [CommunityFoodDiscoveryPage.razor](../../../Ssalddel.WebApp/Pages/CommunityFoodDiscoveryPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGlobalTradeThreadPage` | `/community/global-trade/{ThreadId:long}` | [CommunityGlobalTradeThreadPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGlobalTradeThreadPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupImportPage` | `/community/group-import` | [CommunityGroupImportPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupImportPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseCreatePage` | `/community/group-purchase/new` | [CommunityGroupPurchaseCreatePage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseCreatePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseDeliveryOptionsPage` | `/community/group-purchase/{CampaignId:guid}/delivery-options` | [CommunityGroupPurchaseDeliveryOptionsPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseDeliveryOptionsPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseDemandPage` | `/community/group-purchase/demand` | [CommunityGroupPurchaseDemandPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseDemandPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseDetailPage` | `/community/group-purchase/{CampaignId:guid}` | [CommunityGroupPurchaseDetailPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseDetailPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseFulfillmentDraftPage` | `/community/group-purchase/{CampaignId:guid}/fulfillment-draft` | [CommunityGroupPurchaseFulfillmentDraftPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseFulfillmentDraftPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseNegotiationPage` | `/community/group-purchase/{CampaignId:guid}/negotiation` | [CommunityGroupPurchaseNegotiationPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseNegotiationPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseObjectionsPage` | `/community/group-purchase/{CampaignId:guid}/objections` | [CommunityGroupPurchaseObjectionsPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseObjectionsPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchasePage` | `/community/group-purchase` | [CommunityGroupPurchasePage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchasePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseParticipationPage` | `/community/group-purchase/{CampaignId:guid}/participation` | [CommunityGroupPurchaseParticipationPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseParticipationPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchasePracticePage` | `/community/group-purchase/practice` | [CommunityGroupPurchasePracticePage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchasePracticePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseResolutionPage` | `/community/group-purchase/{CampaignId:guid}/resolution` | [CommunityGroupPurchaseResolutionPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseResolutionPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseSignaturePage` | `/community/group-purchase/{CampaignId:guid}/signature` | [CommunityGroupPurchaseSignaturePage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseSignaturePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityGroupPurchaseSuppliersPage` | `/community/group-purchase/{CampaignId:guid}/suppliers` | [CommunityGroupPurchaseSuppliersPage.razor](../../../Ssalddel.WebApp/Pages/CommunityGroupPurchaseSuppliersPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityLedgerDraftPage` | `/community/ledgers/new` | [CommunityLedgerDraftPage.razor](../../../Ssalddel.WebApp/Pages/CommunityLedgerDraftPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityMapApplicationChooserPage` | `/community/map-application-chooser` | [CommunityMapApplicationChooserPage.razor](../../../Ssalddel.WebApp/Pages/CommunityMapApplicationChooserPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityPage` | `/community`<br>`/{LanguageSegment}/community` | [CommunityPage.razor](../../../Ssalddel.WebApp/Pages/CommunityPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityPersonalPage` | `/community/me`<br>`/community/me/{SectionKey}` | [CommunityPersonalPage.razor](../../../Ssalddel.WebApp/Pages/CommunityPersonalPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityPostComposePage` | `/community/write` | [CommunityPostComposePage.razor](../../../Ssalddel.WebApp/Pages/CommunityPostComposePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityPostDetailPage` | `/community/posts/{PostId:long}` | [CommunityPostDetailPage.razor](../../../Ssalddel.WebApp/Pages/CommunityPostDetailPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityRecommendedPostDetailPage` | `/community/posts/recommended/detail` | [CommunityRecommendedPostDetailPage.razor](../../../Ssalddel.WebApp/Pages/CommunityRecommendedPostDetailPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityRecommendedPostsPage` | `/community/posts/recommended` | [CommunityRecommendedPostsPage.razor](../../../Ssalddel.WebApp/Pages/CommunityRecommendedPostsPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityRoleApplicationPage` | `/community/roles/apply` | [CommunityRoleApplicationPage.razor](../../../Ssalddel.WebApp/Pages/CommunityRoleApplicationPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityRoleHomePage` | `/community/home` | [CommunityRoleHomePage.razor](../../../Ssalddel.WebApp/Pages/CommunityRoleHomePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunitySafetyCenterPage` | `/community/safety` | [CommunitySafetyCenterPage.razor](../../../Ssalddel.WebApp/Pages/CommunitySafetyCenterPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-CommunityWorkspacePage` | `/community/workspace` | [CommunityWorkspacePage.razor](../../../Ssalddel.WebApp/Pages/CommunityWorkspacePage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DiagramWorkbenchPage` | `/diagram` | [DiagramWorkbenchPage.razor](../../../Ssalddel.WebApp/Pages/DiagramWorkbenchPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverBankAccountPage` | `/driver/account/bank` | [DriverBankAccountPage.razor](../../../Ssalddel.WebApp/Pages/DriverBankAccountPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverCommunityRequestsPage` | `/driver/community-requests` | [DriverCommunityRequestsPage.razor](../../../Ssalddel.WebApp/Pages/DriverCommunityRequestsPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverCurrentMonthSettlementPage` | `/driver/settlements/current-month` | [DriverCurrentMonthSettlementPage.razor](../../../Ssalddel.WebApp/Pages/DriverCurrentMonthSettlementPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverCurrentTransportPage` | `/driver/transports/current` | [DriverCurrentTransportPage.razor](../../../Ssalddel.WebApp/Pages/DriverCurrentTransportPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverExplorationCampaignsPage` | `/driver/exploration/campaigns` | [DriverExplorationCampaignsPage.razor](../../../Ssalddel.WebApp/Pages/DriverExplorationCampaignsPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverHome` | `/driver`<br>`/driver/home` | [DriverHome.razor](../../../Ssalddel.WebApp/Pages/DriverHome.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverMyInfoPage` | `/driver/me` | [DriverMyInfoPage.razor](../../../Ssalddel.WebApp/Pages/DriverMyInfoPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverNotificationSettingsPage` | `/driver/notifications/settings` | [DriverNotificationSettingsPage.razor](../../../Ssalddel.WebApp/Pages/DriverNotificationSettingsPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverNotificationsPage` | `/driver/notifications` | [DriverNotificationsPage.razor](../../../Ssalddel.WebApp/Pages/DriverNotificationsPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverRecommendationDecisionPage` | `/driver/dispatch-decisions/{RequestId}` | [DriverRecommendationDecisionPage.razor](../../../Ssalddel.WebApp/Pages/DriverRecommendationDecisionPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverRecommendationMapPage` | `/driver/recommendations/{RequestId}` | [DriverRecommendationMapPage.razor](../../../Ssalddel.WebApp/Pages/DriverRecommendationMapPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverRecommendations` | `/driver/recommendations` | [DriverRecommendations.razor](../../../Ssalddel.WebApp/Pages/DriverRecommendations.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverReservationsPage` | `/driver/reservations` | [DriverReservationsPage.razor](../../../Ssalddel.WebApp/Pages/DriverReservationsPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverTransportDropoffPage` | `/driver/transports/{TransportId:long}/dropoff` | [DriverTransportDropoffPage.razor](../../../Ssalddel.WebApp/Pages/DriverTransportDropoffPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverTransportHistoryPage` | `/driver/transports/history` | [DriverTransportHistoryPage.razor](../../../Ssalddel.WebApp/Pages/DriverTransportHistoryPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverTransportPickupPage` | `/driver/transports/{TransportId:long}/pickup` | [DriverTransportPickupPage.razor](../../../Ssalddel.WebApp/Pages/DriverTransportPickupPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverTransportProofPage` | `/driver/transport/proof` | [DriverTransportProofPage.razor](../../../Ssalddel.WebApp/Pages/DriverTransportProofPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverViewSettingsPage` | `/driver/settings/views` | [DriverViewSettingsPage.razor](../../../Ssalddel.WebApp/Pages/DriverViewSettingsPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverWorkSettingsPage` | `/driver/work/settings` | [DriverWorkSettingsPage.razor](../../../Ssalddel.WebApp/Pages/DriverWorkSettingsPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-DriverWorkStartPage` | `/driver/work/start` | [DriverWorkStartPage.razor](../../../Ssalddel.WebApp/Pages/DriverWorkStartPage.razor) | Ssalddel.Web.DriverApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ExpectedItemDocumentWorkbenchPage` | `/tools/expected-item-documents` | [ExpectedItemDocumentWorkbenchPage.razor](../../../Ssalddel.WebApp/Pages/ExpectedItemDocumentWorkbenchPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-GlobalImportOrderLedgerPage` | `/global/orders/{OrderCode}` | [GlobalImportOrderLedgerPage.razor](../../../Ssalddel.WebApp/Pages/GlobalImportOrderLedgerPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-GlobalImportRequestsPage` | `/global/import-requests` | [GlobalImportRequestsPage.razor](../../../Ssalddel.WebApp/Pages/GlobalImportRequestsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-GlobalProductDetailPage` | `/global/products/{Slug}` | [GlobalProductDetailPage.razor](../../../Ssalddel.WebApp/Pages/GlobalProductDetailPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-GlobalSupplierApplyPage` | `/global/suppliers/apply` | [GlobalSupplierApplyPage.razor](../../../Ssalddel.WebApp/Pages/GlobalSupplierApplyPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-GlobalTradeHome` | `/global` | [GlobalTradeHome.razor](../../../Ssalddel.WebApp/Pages/GlobalTradeHome.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-GroupExportLedgerPage` | `/orderer/ledgers/group-export` | [GroupExportLedgerPage.razor](../../../Ssalddel.WebApp/Pages/GroupExportLedgerPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-Home` | `/` | [Home.razor](../../../Ssalddel.WebApp/Pages/Home.razor) | Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-IdentifierCodeWorkbenchPage` | `/tools/identifier-codes` | [IdentifierCodeWorkbenchPage.razor](../../../Ssalddel.WebApp/Pages/IdentifierCodeWorkbenchPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-IndividualExportLedgerPage` | `/orderer/ledgers/individual-export` | [IndividualExportLedgerPage.razor](../../../Ssalddel.WebApp/Pages/IndividualExportLedgerPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-IndividualImportLedgerPage` | `/orderer/ledgers/individual-import` | [IndividualImportLedgerPage.razor](../../../Ssalddel.WebApp/Pages/IndividualImportLedgerPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-KamisDomesticPriceComparisonPage` | `/information/kamis-domestic-price-comparison` | [KamisDomesticPriceComparisonPage.razor](../../../Ssalddel.WebApp/Pages/KamisDomesticPriceComparisonPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-KoreaAgriculturalMapPage` | `/information/korea-agricultural-map`<br>`/information/regional-agricultural-map` | [KoreaAgriculturalMapPage.razor](../../../Ssalddel.WebApp/Pages/KoreaAgriculturalMapPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-LoginPage` | `/login` | [LoginPage.razor](../../../Ssalddel.WebApp/Pages/LoginPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-NotFound` | `/not-found` | [NotFound.razor](../../../Ssalddel.WebApp/Pages/NotFound.razor) | Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OfficialFoodIngredientPage` | `/information/food-ingredients` | [OfficialFoodIngredientPage.razor](../../../Ssalddel.WebApp/Pages/OfficialFoodIngredientPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OrdererFclSharedGoalPage` | `/orderer/group-import/fcl-goal` | [OrdererFclSharedGoalPage.razor](../../../Ssalddel.WebApp/Pages/OrdererFclSharedGoalPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OrdererHome` | `/orderer` | [OrdererHome.razor](../../../Ssalddel.WebApp/Pages/OrdererHome.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OrdererMartCatalogPage` | `/food/mart`<br>`/orderer/mart` | [OrdererMartCatalogPage.razor](../../../Ssalddel.WebApp/Pages/OrdererMartCatalogPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OrdererMartOrderRequestPage` | `/food/mart/order`<br>`/food/mart/order/{ProductId:long}`<br>`/orderer/mart/order`<br>`/orderer/mart/order/{ProductId:long}` | [OrdererMartOrderRequestPage.razor](../../../Ssalddel.WebApp/Pages/OrdererMartOrderRequestPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OrdererMartProductDetailPage` | `/food/mart/products/{ProductId:long}`<br>`/orderer/mart/products/{ProductId:long}` | [OrdererMartProductDetailPage.razor](../../../Ssalddel.WebApp/Pages/OrdererMartProductDetailPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OrdererMartProductReviewPage` | `/food/mart/reviews/{ProductId:long}`<br>`/orderer/mart/reviews/{ProductId:long}` | [OrdererMartProductReviewPage.razor](../../../Ssalddel.WebApp/Pages/OrdererMartProductReviewPage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-OrdererWorkspacePage` | `/orderer/workspace` | [OrdererWorkspacePage.razor](../../../Ssalddel.WebApp/Pages/OrdererWorkspacePage.razor) | Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ProduceRegionalPriceComparisonPage` | `/information/produce-price-comparison`<br>`/information/apple-price-comparison` | [ProduceRegionalPriceComparisonPage.razor](../../../Ssalddel.WebApp/Pages/ProduceRegionalPriceComparisonPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-PublicDataInformationPage` | `/information/public-data`<br>`/information/agricultural-fisheries-price-comparison` | [PublicDataInformationPage.razor](../../../Ssalddel.WebApp/Pages/PublicDataInformationPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-RegionalCultureSpecialtyDetailPage` | `/community/regions/{RegionKey}` | [RegionalCultureSpecialtyDetailPage.razor](../../../Ssalddel.WebApp/Pages/RegionalCultureSpecialtyDetailPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-RegionalCultureSpecialtyPage` | `/community/regions` | [RegionalCultureSpecialtyPage.razor](../../../Ssalddel.WebApp/Pages/RegionalCultureSpecialtyPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-RegionalProductCandidatesPage` | `/information/regional-products` | [RegionalProductCandidatesPage.razor](../../../Ssalddel.WebApp/Pages/RegionalProductCandidatesPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.OrdererApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperBulkImportPage` | `/shipper/request/bulk` | [ShipperBulkImportPage.razor](../../../Ssalddel.WebApp/Pages/ShipperBulkImportPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperCustomsHsReviewsPage` | `/shipper/customs/hs-reviews` | [ShipperCustomsHsReviewsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperCustomsHsReviewsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperDispatchAddressFormPage` | `/dispatch/address-form` | [ShipperDispatchAddressFormPage.razor](../../../Ssalddel.WebApp/Pages/ShipperDispatchAddressFormPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperExplorationInboxPage` | `/shipper/exploration/inbox` | [ShipperExplorationInboxPage.razor](../../../Ssalddel.WebApp/Pages/ShipperExplorationInboxPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperFclLclPlannerPage` | `/shipper/international/fcl-lcl` | [ShipperFclLclPlannerPage.razor](../../../Ssalddel.WebApp/Pages/ShipperFclLclPlannerPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperHome` | `/shipper` | [ShipperHome.razor](../../../Ssalddel.WebApp/Pages/ShipperHome.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperInboundDashboardPage` | `/shipper/inbound/dashboard` | [ShipperInboundDashboardPage.razor](../../../Ssalddel.WebApp/Pages/ShipperInboundDashboardPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperInboundRequestCompletePage` | `/shipper/inbound/requests/{InboundId:long}/complete` | [ShipperInboundRequestCompletePage.razor](../../../Ssalddel.WebApp/Pages/ShipperInboundRequestCompletePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperInboundRequestCreatePage` | `/shipper/inbound/requests/new` | [ShipperInboundRequestCreatePage.razor](../../../Ssalddel.WebApp/Pages/ShipperInboundRequestCreatePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperInboundRequestDetailPage` | `/shipper/inbound/requests/{InboundId:long}` | [ShipperInboundRequestDetailPage.razor](../../../Ssalddel.WebApp/Pages/ShipperInboundRequestDetailPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperInboundRequestsPage` | `/shipper/inbound/requests` | [ShipperInboundRequestsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperInboundRequestsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperProductListingsPage` | `/shipper/sales/listings` | [ShipperProductListingsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperProductListingsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperProfileSettingsPage` | `/shipper/settings/profile` | [ShipperProfileSettingsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperProfileSettingsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperPublicCargoPage` | `/shipper/public-cargo` | [ShipperPublicCargoPage.razor](../../../Ssalddel.WebApp/Pages/ShipperPublicCargoPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperReconsignmentOrdersPage` | `/shipper/reconsignment/orders` | [ShipperReconsignmentOrdersPage.razor](../../../Ssalddel.WebApp/Pages/ShipperReconsignmentOrdersPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestCargoPage` | `/shipper/request/cargo` | [ShipperRequestCargoPage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestCargoPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestDetailPreview` | `/shipper/request/detail`<br>`/shipper/request/{RequestId}` | [ShipperRequestDetailPreview.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestDetailPreview.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestPage` | `/shipper/request` | [ShipperRequestPage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestPaymentPage` | `/shipper/request/{RequestId}/payment` | [ShipperRequestPaymentPage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestPaymentPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestProcedurePage` | `/shipper/request/procedure` | [ShipperRequestProcedurePage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestProcedurePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestProofsPage` | `/shipper/request/{RequestId}/proofs` | [ShipperRequestProofsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestProofsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestReviewPage` | `/shipper/request/review`<br>`/shipper/request/summary` | [ShipperRequestReviewPage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestReviewPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestTimelinePage` | `/shipper/request/{RequestId}/timeline` | [ShipperRequestTimelinePage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestTimelinePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperRequestTransportPage` | `/shipper/request/transport` | [ShipperRequestTransportPage.razor](../../../Ssalddel.WebApp/Pages/ShipperRequestTransportPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSalesChannelsPage` | `/shipper/sales/channels` | [ShipperSalesChannelsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperSalesChannelsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSalesListingCreatePage` | `/shipper/sales/listings/new` | [ShipperSalesListingCreatePage.razor](../../../Ssalddel.WebApp/Pages/ShipperSalesListingCreatePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSalesOrderDetailPage` | `/shipper/sales/orders/{OrderId:long}` | [ShipperSalesOrderDetailPage.razor](../../../Ssalddel.WebApp/Pages/ShipperSalesOrderDetailPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSalesOrdersPage` | `/shipper/sales/orders` | [ShipperSalesOrdersPage.razor](../../../Ssalddel.WebApp/Pages/ShipperSalesOrdersPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSalesPageComposerPage` | `/shipper/sales/pages/new` | [ShipperSalesPageComposerPage.razor](../../../Ssalddel.WebApp/Pages/ShipperSalesPageComposerPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSalesProductCreatePage` | `/shipper/sales/products/new` | [ShipperSalesProductCreatePage.razor](../../../Ssalddel.WebApp/Pages/ShipperSalesProductCreatePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSalesProductsPage` | `/shipper/sales/products` | [ShipperSalesProductsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperSalesProductsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperSettlementStatusPage` | `/shipper/request/payment-status` | [ShipperSettlementStatusPage.razor](../../../Ssalddel.WebApp/Pages/ShipperSettlementStatusPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperViewSettingsPage` | `/shipper/settings/views` | [ShipperViewSettingsPage.razor](../../../Ssalddel.WebApp/Pages/ShipperViewSettingsPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperWarehouseInventoryPage` | `/shipper/warehouse/inventory` | [ShipperWarehouseInventoryPage.razor](../../../Ssalddel.WebApp/Pages/ShipperWarehouseInventoryPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperWarehouseRegistrationPage` | `/shipper/warehouses/new` | [ShipperWarehouseRegistrationPage.razor](../../../Ssalddel.WebApp/Pages/ShipperWarehouseRegistrationPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperWarehouseScanPage` | `/shipper/warehouse/scan` | [ShipperWarehouseScanPage.razor](../../../Ssalddel.WebApp/Pages/ShipperWarehouseScanPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperWarehouseWorkspacePage` | `/shipper/warehouse/workspace` | [ShipperWarehouseWorkspacePage.razor](../../../Ssalddel.WebApp/Pages/ShipperWarehouseWorkspacePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperWarehouseWorkStartPage` | `/shipper/warehouse/work/{ProcessCode}` | [ShipperWarehouseWorkStartPage.razor](../../../Ssalddel.WebApp/Pages/ShipperWarehouseWorkStartPage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-ShipperWorkspacePage` | `/shipper/workspace` | [ShipperWorkspacePage.razor](../../../Ssalddel.WebApp/Pages/ShipperWorkspacePage.razor) | Ssalddel.Web.ShipperApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-UsdaUnitedStatesPriceComparisonPage` | `/information/usda-us-price-comparison` | [UsdaUnitedStatesPriceComparisonPage.razor](../../../Ssalddel.WebApp/Pages/UsdaUnitedStatesPriceComparisonPage.razor) | Ssalddel.Web.CommunityApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseBondedCustomsPage` | `/warehouse/bonded-customs` | [WarehouseBondedCustomsPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseBondedCustomsPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseExceptionsPage` | `/warehouse/exceptions` | [WarehouseExceptionsPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseExceptionsPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseExpectedInboundsPage` | `/warehouse/inbounds/expected` | [WarehouseExpectedInboundsPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseExpectedInboundsPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseHistoryPage` | `/warehouse/history` | [WarehouseHistoryPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseHistoryPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseHome` | `/warehouse` | [WarehouseHome.razor](../../../Ssalddel.WebApp/Pages/WarehouseHome.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseInboundInspectionDetailPage` | `/work/inbound/inspection/{InboundItemId:long}`<br>`/warehouse/work/inbound/inspection/{InboundItemId:long}` | [WarehouseInboundInspectionDetailPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseInboundInspectionDetailPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseInboundInspectionPage` | `/warehouse/work/inbound/inspection`<br>`/work/inbound/inspection` | [WarehouseInboundInspectionPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseInboundInspectionPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseInboundInspectionRecordPage` | `/work/inbound/inspection/{InboundItemId:long}/record`<br>`/warehouse/work/inbound/inspection/{InboundItemId:long}/record` | [WarehouseInboundInspectionRecordPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseInboundInspectionRecordPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `WarehouseManagerApp-P03-1` | `/warehouse/work/inbound/products`<br>`/work/inbound/products` | [WarehouseInboundProductScanPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseInboundProductScanPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P03-1/README.md) |
| `Ssalddel.WebApp-Pages-WarehouseInventoryPage` | `/warehouse/general/inventory`<br>`/warehouse/inventory` | [WarehouseInventoryPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseInventoryPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseMartHomePage` | `/warehouse/mart`<br>`/mart` | [WarehouseMartHomePage.razor](../../../Ssalddel.WebApp/Pages/WarehouseMartHomePage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseMartPickingOrderDetailPage` | `/warehouse/mart/picking/orders/{OrderId:long}`<br>`/mart/picking/orders/{OrderId:long}` | [WarehouseMartPickingOrderDetailPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseMartPickingOrderDetailPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseMartPickingPackingPage` | `/warehouse/mart/picking`<br>`/mart/picking` | [WarehouseMartPickingPackingPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseMartPickingPackingPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseMartWorkBoardPage` | `/warehouse/mart/work-board`<br>`/mart/work-board` | [WarehouseMartWorkBoardPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseMartWorkBoardPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseMartWorkStartPage` | `/warehouse/mart/work/{ProcessCode}`<br>`/mart/work/{ProcessCode}` | [WarehouseMartWorkStartPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseMartWorkStartPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseOutboundHandoffPage` | `/warehouse/general/transport-handoff`<br>`/work/outbound/handoff` | [WarehouseOutboundHandoffPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseOutboundHandoffPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseOutboundPlanReviewPage` | `/warehouse/general/outbound-plan-review`<br>`/work/outbound/plans` | [WarehouseOutboundPlanReviewPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseOutboundPlanReviewPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehousePackingTaskPage` | `/warehouse/work/outbound/packing`<br>`/work/outbound/packing` | [WarehousePackingTaskPage.razor](../../../Ssalddel.WebApp/Pages/WarehousePackingTaskPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehousePickingBatchDetailPage` | `/work/picking-batch/{TaskKey}`<br>`/warehouse/work/picking-batch/{TaskKey}` | [WarehousePickingBatchDetailPage.razor](../../../Ssalddel.WebApp/Pages/WarehousePickingBatchDetailPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehousePickingBatchExecutePage` | `/work/picking-batch/{TaskKey}/execute`<br>`/warehouse/work/picking-batch/{TaskKey}/execute` | [WarehousePickingBatchExecutePage.razor](../../../Ssalddel.WebApp/Pages/WarehousePickingBatchExecutePage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehousePickingBatchPage` | `/warehouse/work/picking-batch`<br>`/work/picking-batch` | [WarehousePickingBatchPage.razor](../../../Ssalddel.WebApp/Pages/WarehousePickingBatchPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehousePutAwayTaskPage` | `/warehouse/work/inbound/put-away`<br>`/work/inbound/put-away` | [WarehousePutAwayTaskPage.razor](../../../Ssalddel.WebApp/Pages/WarehousePutAwayTaskPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseScanPage` | `/warehouse/scan`<br>`/scan` | [WarehouseScanPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseScanPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseSettingsPage` | `/warehouse/settings` | [WarehouseSettingsPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseSettingsPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseTransportRequestDraftPage` | `/warehouse/general/transport-request-draft`<br>`/work/outbound/transport-request-draft` | [WarehouseTransportRequestDraftPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseTransportRequestDraftPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseWorkbenchScanPage` | `/warehouse/work/{ProcessCode}/workbench`<br>`/work/{ProcessCode}/workbench` | [WarehouseWorkbenchScanPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseWorkbenchScanPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseWorkBoardPage` | `/warehouse/work-board`<br>`/work-board` | [WarehouseWorkBoardPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseWorkBoardPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseWorkspaceCatalogPage` | `/warehouse/workspace` | [WarehouseWorkspaceCatalogPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseWorkspaceCatalogPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |
| `Ssalddel.WebApp-Pages-WarehouseWorkStartPage` | `/warehouse/work/{ProcessCode}`<br>`/work/{ProcessCode}` | [WarehouseWorkStartPage.razor](../../../Ssalddel.WebApp/Pages/WarehouseWorkStartPage.razor) | Ssalddel.Web.WarehouseApp, Ssalddel.WebApp | 미검토 |

## SsalddelAdmin

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `SsalddelAdmin-P23` | `/activity-logs` | [ActivityLogs.razor](../../../SsalddelAdmin/Components/Pages/ActivityLogs.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P23/README.md) |
| `SsalddelAdmin-P00-1` | `/login` | [AdminLogin.razor](../../../SsalddelAdmin/Components/Pages/AdminLogin.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P00-1/README.md) |
| `SsalddelAdmin-P35` | `/auxiliary-feature-settings` | [AuxiliaryFeatureSettings.razor](../../../SsalddelAdmin/Components/Pages/AuxiliaryFeatureSettings.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P35/README.md) |
| `SsalddelAdmin-Components-Pages-BusinessPackageAdmin` | `/admin/food-delivery`<br>`/admin/freight-delivery`<br>`/admin/order-warehouse` | [BusinessPackageAdmin.razor](../../../SsalddelAdmin/Components/Pages/BusinessPackageAdmin.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-P25` | `/common-contents` | [CommonContents.razor](../../../SsalddelAdmin/Components/Pages/CommonContents.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P25/README.md) |
| `SsalddelAdmin-Components-Pages-CommunityAdminDashboard` | `/community` | [CommunityAdminDashboard.razor](../../../SsalddelAdmin/Components/Pages/CommunityAdminDashboard.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-Components-Pages-CommunityUserManagement` | `/community/users` | [CommunityUserManagement.razor](../../../SsalddelAdmin/Components/Pages/CommunityUserManagement.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-P36` | `/contact-search` | [ContactSearch.razor](../../../SsalddelAdmin/Components/Pages/ContactSearch.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P36/README.md) |
| `SsalddelAdmin-P91` | `/counter` | [Counter.razor](../../../SsalddelAdmin/Components/Pages/Counter.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P91/README.md) |
| `SsalddelAdmin-P16` | `/dashboard`<br>`/admin/order-warehouse/dashboard` | [Dashboard.razor](../../../SsalddelAdmin/Components/Pages/Dashboard.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P16/README.md) |
| `SsalddelAdmin-P39` | `/dispatch-ai-judgment-cases` | [DispatchAIJudgmentCases.razor](../../../SsalddelAdmin/Components/Pages/DispatchAIJudgmentCases.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P39/README.md) |
| `SsalddelAdmin-P19` | `/dispatch/wait`<br>`/admin/freight-delivery/dispatch-wait` | [DispatchWait.razor](../../../SsalddelAdmin/Components/Pages/DispatchWait.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P19/README.md) |
| `SsalddelAdmin-P27-4` | `/documents/logs` | [DocumentLogs.razor](../../../SsalddelAdmin/Components/Pages/DocumentLogs.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P27-4/README.md) |
| `SsalddelAdmin-P27-2` | `/documents/policies` | [DocumentPolicies.razor](../../../SsalddelAdmin/Components/Pages/DocumentPolicies.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P27-2/README.md) |
| `SsalddelAdmin-P27-3` | `/documents/policies/{DocumentCode}` | [DocumentPolicyDetail.razor](../../../SsalddelAdmin/Components/Pages/DocumentPolicyDetail.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P27-3/README.md) |
| `SsalddelAdmin-Components-Pages-DocumentRelationships` | `/documents/relationships` | [DocumentRelationships.razor](../../../SsalddelAdmin/Components/Pages/DocumentRelationships.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-P27` | `/documents`<br>`/admin/order-warehouse/documents` | [Documents.razor](../../../SsalddelAdmin/Components/Pages/Documents.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P27/README.md) |
| `SsalddelAdmin-P27-1` | `/documents/upload` | [DocumentUpload.razor](../../../SsalddelAdmin/Components/Pages/DocumentUpload.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P27-1/README.md) |
| `SsalddelAdmin-P37` | `/dispatch/ai-review`<br>`/admin/freight-delivery/dispatch-ai-review` | [DomesticCargoDispatchAIReview.razor](../../../SsalddelAdmin/Components/Pages/DomesticCargoDispatchAIReview.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P37/README.md) |
| `SsalddelAdmin-P20` | `/drivers/operating`<br>`/admin/freight-delivery/drivers` | [DriverOperatingView.razor](../../../SsalddelAdmin/Components/Pages/DriverOperatingView.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P20/README.md) |
| `SsalddelAdmin-P32` | `/drivers` | [Drivers.razor](../../../SsalddelAdmin/Components/Pages/Drivers.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P32/README.md) |
| `SsalddelAdmin-P00-2` | `/Error` | [Error.razor](../../../SsalddelAdmin/Components/Pages/Error.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P00-2/README.md) |
| `SsalddelAdmin-P31` | `/exploration/campaigns` | [ExplorationCampaigns.razor](../../../SsalddelAdmin/Components/Pages/ExplorationCampaigns.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P31/README.md) |
| `SsalddelAdmin-P40` | `/development/fake-payment-settlement` | [FakePaymentSettlementConsole.razor](../../../SsalddelAdmin/Components/Pages/FakePaymentSettlementConsole.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P40/README.md) |
| `SsalddelAdmin-P27-5` | `/files/pod` | [FilesPod.razor](../../../SsalddelAdmin/Components/Pages/FilesPod.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P27-5/README.md) |
| `SsalddelAdmin-Components-Pages-FollowUpRecovery` | `/operations/follow-up-recovery` | [FollowUpRecovery.razor](../../../SsalddelAdmin/Components/Pages/FollowUpRecovery.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-P38` | `/dispatch/food-ai-review`<br>`/admin/food-delivery/dispatch-ai-review` | [FoodDeliveryDispatchAIReview.razor](../../../SsalddelAdmin/Components/Pages/FoodDeliveryDispatchAIReview.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P38/README.md) |
| `SsalddelAdmin-P30` | `/food/operations`<br>`/admin/food-delivery/operations` | [FoodOperations.razor](../../../SsalddelAdmin/Components/Pages/FoodOperations.razor) | SsalddelAdmin | [페이지→코드→DB](SsalddelAdmin/SsalddelAdmin-P30/README.md) |
| `SsalddelAdmin-Components-Pages-FoodOrderOperationsTrace` | `/food/order-trace`<br>`/admin/food-delivery/order-trace` | [FoodOrderOperationsTrace.razor](../../../SsalddelAdmin/Components/Pages/FoodOrderOperationsTrace.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-Components-Pages-GroupImportTradeReadiness` | `/trade-readiness` | [GroupImportTradeReadiness.razor](../../../SsalddelAdmin/Components/Pages/GroupImportTradeReadiness.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-P00` | `/` | [Home.razor](../../../SsalddelAdmin/Components/Pages/Home.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P00/README.md) |
| `SsalddelAdmin-P29` | `/customs/hs-codes` | [HsCodeOperations.razor](../../../SsalddelAdmin/Components/Pages/HsCodeOperations.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P29/README.md) |
| `SsalddelAdmin-P99` | `/not-found` | [NotFound.razor](../../../SsalddelAdmin/Components/Pages/NotFound.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P99/README.md) |
| `SsalddelAdmin-P33` | `/partners` | [Partners.razor](../../../SsalddelAdmin/Components/Pages/Partners.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P33/README.md) |
| `SsalddelAdmin-P26` | `/payments` | [Payments.razor](../../../SsalddelAdmin/Components/Pages/Payments.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P26/README.md) |
| `SsalddelAdmin-P28` | `/cargo` | [PublicCargo.razor](../../../SsalddelAdmin/Components/Pages/PublicCargo.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P28/README.md) |
| `SsalddelAdmin-P18` | `/requests/{RequestId}` | [RequestDetail.razor](../../../SsalddelAdmin/Components/Pages/RequestDetail.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P18/README.md) |
| `SsalddelAdmin-P17` | `/requests`<br>`/admin/freight-delivery/requests`<br>`/admin/order-warehouse/outbound-requests` | [Requests.razor](../../../SsalddelAdmin/Components/Pages/Requests.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P17/README.md) |
| `SsalddelAdmin-P30-1` | `/restaurant-search-policy` | [RestaurantSearchPolicySettings.razor](../../../SsalddelAdmin/Components/Pages/RestaurantSearchPolicySettings.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P30-1/README.md) |
| `SsalddelAdmin-P34` | `/revenue-policies` | [RevenuePolicies.razor](../../../SsalddelAdmin/Components/Pages/RevenuePolicies.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P34/README.md) |
| `SsalddelAdmin-P26-1` | `/settlements` | [Settlements.razor](../../../SsalddelAdmin/Components/Pages/Settlements.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P26-1/README.md) |
| `SsalddelAdmin-Components-Pages-SpatialCatalog` | `/spatial-catalog` | [SpatialCatalog.razor](../../../SsalddelAdmin/Components/Pages/SpatialCatalog.razor) | SsalddelAdmin | 미검토 |
| `SsalddelAdmin-P21` | `/transports`<br>`/admin/freight-delivery/transports`<br>`/admin/order-warehouse/outbound-transports` | [Transports.razor](../../../SsalddelAdmin/Components/Pages/Transports.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P21/README.md) |
| `SsalddelAdmin-P22` | `/transports/{RequestId}` | [TransportWorkflowDetail.razor](../../../SsalddelAdmin/Components/Pages/TransportWorkflowDetail.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P22/README.md) |
| `SsalddelAdmin-P22-1` | `/transports/{RequestId}/events` | [TransportWorkflowEvents.razor](../../../SsalddelAdmin/Components/Pages/TransportWorkflowEvents.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P22-1/README.md) |
| `SsalddelAdmin-P22-2` | `/transports/{RequestId}/proofs` | [TransportWorkflowProofs.razor](../../../SsalddelAdmin/Components/Pages/TransportWorkflowProofs.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P22-2/README.md) |
| `SsalddelAdmin-P22-3` | `/transports/{RequestId}/settlement` | [TransportWorkflowSettlement.razor](../../../SsalddelAdmin/Components/Pages/TransportWorkflowSettlement.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P22-3/README.md) |
| `SsalddelAdmin-P32-1` | `/vehicle-management`<br>`/admin/freight-delivery/vehicles` | [VehicleManagement.razor](../../../SsalddelAdmin/Components/Pages/VehicleManagement.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P32-1/README.md) |
| `SsalddelAdmin-P24` | `/view-policies` | [ViewPolicies.razor](../../../SsalddelAdmin/Components/Pages/ViewPolicies.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P24/README.md) |
| `SsalddelAdmin-P90` | `/weather` | [Weather.razor](../../../SsalddelAdmin/Components/Pages/Weather.razor) | SsalddelAdmin | [이전 화면 문서](SsalddelAdmin/SsalddelAdmin-P90/README.md) |

## SsalddelAdminApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `SsalddelAdminApp-P03` | `/information-review` | [CommunityInformationReview.razor](../../../SsalddelAdminApp/Components/Pages/CommunityInformationReview.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-P02` | `/community-management` | [CommunityManagement.razor](../../../SsalddelAdminApp/Components/Pages/CommunityManagement.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-Components-Pages-Finance` | `/operations/finance` | [Finance.razor](../../../SsalddelAdminApp/Components/Pages/Finance.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-Components-Pages-FollowUpRecovery` | `/operations/follow-up-recovery` | [FollowUpRecovery.razor](../../../SsalddelAdminApp/Components/Pages/FollowUpRecovery.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-Components-Pages-GroupImportTradeReadiness` | `/trade-readiness` | [GroupImportTradeReadiness.razor](../../../SsalddelAdminApp/Components/Pages/GroupImportTradeReadiness.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-P00` | `/`<br>`/overview` | [Home.razor](../../../SsalddelAdminApp/Components/Pages/Home.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-P01` | `/login` | [Login.razor](../../../SsalddelAdminApp/Components/Pages/Login.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-Components-Pages-Operations` | `/operations` | [Operations.razor](../../../SsalddelAdminApp/Components/Pages/Operations.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-Components-Pages-PageCatalog` | `/page-catalog` | [PageCatalog.razor](../../../SsalddelAdminApp/Components/Pages/PageCatalog.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-P04-1` | `/prajna/hongik-hakdang` | [PrajnaHongikHakdang.razor](../../../SsalddelAdminApp/Components/Pages/PrajnaHongikHakdang.razor) | SsalddelAdminApp | 미검토 |
| `SsalddelAdminApp-P04` | `/prajna` | [PrajnaHub.razor](../../../SsalddelAdminApp/Components/Pages/PrajnaHub.razor) | SsalddelAdminApp | 미검토 |

## SsalddelApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `SsalddelApp-Components-Pages-CommunityBaguaTransition` | `/community/bagua/{SourceTrigramKey}/{TargetTrigramKey}`<br>`/community/bagua/{RoleCode}/{SourceTrigramKey}/{TargetTrigramKey}` | [CommunityBaguaTransition.razor](../../../SsalddelApp/Components/Pages/CommunityBaguaTransition.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityBoardDirectoryPage` | `/community/boards/directory` | [CommunityBoardDirectoryPage.razor](../../../SsalddelApp/Components/Pages/CommunityBoardDirectoryPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityBoardManagementPage` | `/community/boards/manage` | [CommunityBoardManagementPage.razor](../../../SsalddelApp/Components/Pages/CommunityBoardManagementPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityBoardPage` | `/community/boards` | [CommunityBoardPage.razor](../../../SsalddelApp/Components/Pages/CommunityBoardPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityCollectiveActions` | `/community/actions`<br>`/community/actions/{PageKey}` | [CommunityCollectiveActions.razor](../../../SsalddelApp/Components/Pages/CommunityCollectiveActions.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P10-2-1` | `/community/decorations/checkout/{ProductKey}` | [CommunityDecorationCheckoutPage.razor](../../../SsalddelApp/Components/Pages/CommunityDecorationCheckoutPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P10-2-1/README.md) |
| `SsalddelApp-P10-3` | `/community/decorations/create` | [CommunityDecorationCreatePage.razor](../../../SsalddelApp/Components/Pages/CommunityDecorationCreatePage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P10-3/README.md) |
| `SsalddelApp-P10-1` | `/community/decorations/products/{ProductKey}` | [CommunityDecorationDetailPage.razor](../../../SsalddelApp/Components/Pages/CommunityDecorationDetailPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P10-1/README.md) |
| `SsalddelApp-Components-Pages-CommunityDecorationLegacyCheckoutPage` | `/community/decorations/{ProductKey}/checkout` | [CommunityDecorationLegacyCheckoutPage.razor](../../../SsalddelApp/Components/Pages/CommunityDecorationLegacyCheckoutPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityDecorationLegacyProductPage` | `/community/decorations/{ProductKey}` | [CommunityDecorationLegacyProductPage.razor](../../../SsalddelApp/Components/Pages/CommunityDecorationLegacyProductPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P10` | `/community/decorations` | [CommunityDecorationStorePage.razor](../../../SsalddelApp/Components/Pages/CommunityDecorationStorePage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P10/README.md) |
| `SsalddelApp-P10-4` | `/community/decorations/themes/submit` | [CommunityDecorationThemeSubmitPage.razor](../../../SsalddelApp/Components/Pages/CommunityDecorationThemeSubmitPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P10-4/README.md) |
| `SsalddelApp-Components-Pages-CommunityGroupImport` | `/community/group-import` | [CommunityGroupImport.razor](../../../SsalddelApp/Components/Pages/CommunityGroupImport.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchase` | `/community/group-purchase` | [CommunityGroupPurchase.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchase.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseCreatePage` | `/community/group-purchase/new` | [CommunityGroupPurchaseCreatePage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseCreatePage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseDeliveryOptionsPage` | `/community/group-purchase/{CampaignId:guid}/delivery-options` | [CommunityGroupPurchaseDeliveryOptionsPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseDeliveryOptionsPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P00-C1` | `/community/group-purchase/demand`<br>`/community/orders/new` | [CommunityGroupPurchaseDemandPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseDemandPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseDetailPage` | `/community/group-purchase/{CampaignId:guid}` | [CommunityGroupPurchaseDetailPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseDetailPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseFulfillmentDraftPage` | `/community/group-purchase/{CampaignId:guid}/fulfillment-draft` | [CommunityGroupPurchaseFulfillmentDraftPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseFulfillmentDraftPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseNegotiationPage` | `/community/group-purchase/{CampaignId:guid}/negotiation` | [CommunityGroupPurchaseNegotiationPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseNegotiationPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseObjectionsPage` | `/community/group-purchase/{CampaignId:guid}/objections` | [CommunityGroupPurchaseObjectionsPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseObjectionsPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseParticipationPage` | `/community/group-purchase/{CampaignId:guid}/participation` | [CommunityGroupPurchaseParticipationPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseParticipationPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseResolutionPage` | `/community/group-purchase/{CampaignId:guid}/resolution` | [CommunityGroupPurchaseResolutionPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseResolutionPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseSignaturePage` | `/community/group-purchase/{CampaignId:guid}/signature` | [CommunityGroupPurchaseSignaturePage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseSignaturePage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityGroupPurchaseSuppliersPage` | `/community/group-purchase/{CampaignId:guid}/suppliers` | [CommunityGroupPurchaseSuppliersPage.razor](../../../SsalddelApp/Components/Pages/CommunityGroupPurchaseSuppliersPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P00-C` | `/community` | [CommunityHomePage.razor](../../../SsalddelApp/Components/Pages/CommunityHomePage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P00-C2` | `/community/orders` | [CommunityIndividualOrdersPage.razor](../../../SsalddelApp/Components/Pages/CommunityIndividualOrdersPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityLedgerDraftPage` | `/community/ledgers/new` | [CommunityLedgerDraftPage.razor](../../../SsalddelApp/Components/Pages/CommunityLedgerDraftPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityPersonalPage` | `/community/me` | [CommunityPersonalPage.razor](../../../SsalddelApp/Components/Pages/CommunityPersonalPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityPostComposePage` | `/community/write` | [CommunityPostComposePage.razor](../../../SsalddelApp/Components/Pages/CommunityPostComposePage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityPostDetailPage` | `/community/posts/{PostId:long}` | [CommunityPostDetailPage.razor](../../../SsalddelApp/Components/Pages/CommunityPostDetailPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityRecommendedPostDetailPage` | `/community/posts/recommended/detail` | [CommunityRecommendedPostDetailPage.razor](../../../SsalddelApp/Components/Pages/CommunityRecommendedPostDetailPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityRecommendedPostsPage` | `/community/posts/recommended` | [CommunityRecommendedPostsPage.razor](../../../SsalddelApp/Components/Pages/CommunityRecommendedPostsPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityWorkBoardPage` | `/community/work/{GroupKey}` | [CommunityWorkBoardPage.razor](../../../SsalddelApp/Components/Pages/CommunityWorkBoardPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityWorkRelationshipsPage` | `/community/relationships` | [CommunityWorkRelationshipsPage.razor](../../../SsalddelApp/Components/Pages/CommunityWorkRelationshipsPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-CommunityWorkspacePage` | `/community/workspace` | [CommunityWorkspacePage.razor](../../../SsalddelApp/Components/Pages/CommunityWorkspacePage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P91` | `/counter` | [Counter.razor](../../../SsalddelApp/Components/Pages/Counter.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P91/README.md) |
| `SsalddelApp-P07-1` | `/shipper/customs/hs-reviews` | [CustomsHsReviews.razor](../../../SsalddelApp/Components/Pages/CustomsHsReviews.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P07-1/README.md) |
| `SsalddelApp-Components-Pages-DiagramWorkbench` | `/diagram` | [DiagramWorkbench.razor](../../../SsalddelApp/Components/Pages/DiagramWorkbench.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P02-2` | `/dispatch/address-form` | [DispatchAddressForm.razor](../../../SsalddelApp/Components/Pages/DispatchAddressForm.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02-2/README.md) |
| `SsalddelApp-P01-4` | `/shipper/exploration/inbox` | [ExplorationInbox.razor](../../../SsalddelApp/Components/Pages/ExplorationInbox.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P01-4/README.md) |
| `SsalddelApp-P07` | `/shipper/international/fcl-lcl` | [FclLclPlanner.razor](../../../SsalddelApp/Components/Pages/FclLclPlanner.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P07/README.md) |
| `SsalddelApp-P01` | `/shipper` | [Home.razor](../../../SsalddelApp/Components/Pages/Home.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P01/README.md) |
| `SsalddelApp-P04` | `/shipper/inbound/dashboard` | [InboundDashboard.razor](../../../SsalddelApp/Components/Pages/InboundDashboard.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P04/README.md) |
| `SsalddelApp-Components-Pages-InboundRequestCompletePage` | `/shipper/inbound/requests/{InboundId:long}/complete` | [InboundRequestCompletePage.razor](../../../SsalddelApp/Components/Pages/InboundRequestCompletePage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-InboundRequestCreatePage` | `/shipper/inbound/requests/new` | [InboundRequestCreatePage.razor](../../../SsalddelApp/Components/Pages/InboundRequestCreatePage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-InboundRequestDetailPage` | `/shipper/inbound/requests/{InboundId:long}` | [InboundRequestDetailPage.razor](../../../SsalddelApp/Components/Pages/InboundRequestDetailPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P04-1` | `/shipper/inbound/requests` | [InboundRequests.razor](../../../SsalddelApp/Components/Pages/InboundRequests.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P04-1/README.md) |
| `SsalddelApp-Components-Pages-KamisDomesticPriceComparison` | `/information/kamis-domestic-price-comparison` | [KamisDomesticPriceComparison.razor](../../../SsalddelApp/Components/Pages/KamisDomesticPriceComparison.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-KoreaAgriculturalMapPage` | `/information/korea-agricultural-map`<br>`/information/regional-agricultural-map` | [KoreaAgriculturalMapPage.razor](../../../SsalddelApp/Components/Pages/KoreaAgriculturalMapPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-LogisticsServiceContract` | `/shipper/warehouse/logistics-contract` | [LogisticsServiceContract.razor](../../../SsalddelApp/Components/Pages/LogisticsServiceContract.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P99` | `/not-found` | [NotFound.razor](../../../SsalddelApp/Components/Pages/NotFound.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P99/README.md) |
| `SsalddelApp-Components-Pages-OfficialFoodIngredients` | `/information/food-ingredients` | [OfficialFoodIngredients.razor](../../../SsalddelApp/Components/Pages/OfficialFoodIngredients.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P06-2` | `/shipper/sales/fulfillment` | [OrderFulfillment.razor](../../../SsalddelApp/Components/Pages/OrderFulfillment.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2/README.md) |
| `SsalddelApp-P06-2-5` | `/shipper/sales/fulfillment/inventory` | [OrderFulfillmentInventory.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentInventory.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-5/README.md) |
| `SsalddelApp-P06-2-4-1` | `/shipper/sales/fulfillment/orders/{OrderKey}` | [OrderFulfillmentOrderDetail.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentOrderDetail.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-4-1/README.md) |
| `SsalddelApp-P06-2-4` | `/shipper/sales/fulfillment/orders` | [OrderFulfillmentOrders.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentOrders.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-4/README.md) |
| `SsalddelApp-P06-2-7` | `/shipper/sales/fulfillment/packing` | [OrderFulfillmentPacking.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentPacking.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-7/README.md) |
| `SsalddelApp-P06-2-7-1` | `/shipper/sales/fulfillment/packing/{TaskId:long}` | [OrderFulfillmentPackingTask.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentPackingTask.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-7-1/README.md) |
| `SsalddelApp-P06-2-6` | `/shipper/sales/fulfillment/picking` | [OrderFulfillmentPicking.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentPicking.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-6/README.md) |
| `SsalddelApp-P06-2-6-1` | `/shipper/sales/fulfillment/picking/{TaskId:long}` | [OrderFulfillmentPickingTask.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentPickingTask.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-6-1/README.md) |
| `SsalddelApp-P06-2-8` | `/shipper/sales/fulfillment/restock-policy` | [OrderFulfillmentRestockPolicy.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentRestockPolicy.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-8/README.md) |
| `SsalddelApp-P06-2-3` | `/shipper/sales/fulfillment/samples` | [OrderFulfillmentSamples.razor](../../../SsalddelApp/Components/Pages/OrderFulfillmentSamples.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-3/README.md) |
| `SsalddelApp-Components-Pages-ProduceRegionalPriceComparison` | `/information/produce-price-comparison`<br>`/information/apple-price-comparison` | [ProduceRegionalPriceComparison.razor](../../../SsalddelApp/Components/Pages/ProduceRegionalPriceComparison.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P06-1` | `/shipper/sales/listings` | [ProductListings.razor](../../../SsalddelApp/Components/Pages/ProductListings.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-1/README.md) |
| `SsalddelApp-P01-3` | `/shipper/public-cargo` | [PublicCargo.razor](../../../SsalddelApp/Components/Pages/PublicCargo.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P01-3/README.md) |
| `SsalddelApp-Components-Pages-PublicDataInformation` | `/information/public-data`<br>`/information/agricultural-fisheries-price-comparison` | [PublicDataInformation.razor](../../../SsalddelApp/Components/Pages/PublicDataInformation.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P08` | `/shipper/reconsignment/orders` | [ReconsignmentOrders.razor](../../../SsalddelApp/Components/Pages/ReconsignmentOrders.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P08/README.md) |
| `SsalddelApp-Components-Pages-RegionalCultureSpecialtyDetailPage` | `/community/regions/{RegionKey}` | [RegionalCultureSpecialtyDetailPage.razor](../../../SsalddelApp/Components/Pages/RegionalCultureSpecialtyDetailPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-RegionalCultureSpecialtyPage` | `/community/regions` | [RegionalCultureSpecialtyPage.razor](../../../SsalddelApp/Components/Pages/RegionalCultureSpecialtyPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-RegionalProductCandidatesPage` | `/information/regional-products` | [RegionalProductCandidatesPage.razor](../../../SsalddelApp/Components/Pages/RegionalProductCandidatesPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P06` | `/shipper/sales/channels` | [SalesChannels.razor](../../../SsalddelApp/Components/Pages/SalesChannels.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06/README.md) |
| `SsalddelApp-P06-2-2` | `/shipper/sales/orders/{OrderId:long}` | [SalesOrderDetail.razor](../../../SsalddelApp/Components/Pages/SalesOrderDetail.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-2/README.md) |
| `SsalddelApp-P06-2-1` | `/shipper/sales/orders` | [SalesOrders.razor](../../../SsalddelApp/Components/Pages/SalesOrders.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P06-2-1/README.md) |
| `SsalddelApp-Components-Pages-SalesPageComposer` | `/shipper/sales/pages/new` | [SalesPageComposer.razor](../../../SsalddelApp/Components/Pages/SalesPageComposer.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P02-1` | `/shipper/request/bulk` | [ShipperBulkImport.razor](../../../SsalddelApp/Components/Pages/ShipperBulkImport.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02-1/README.md) |
| `SsalddelApp-P01-1` | `/shipper/settings/profile` | [ShipperProfileSettings.razor](../../../SsalddelApp/Components/Pages/ShipperProfileSettings.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P01-1/README.md) |
| `SsalddelApp-P02` | `/shipper/request/cargo` | [ShipperRequestCargoPage.razor](../../../SsalddelApp/Components/Pages/ShipperRequestCargoPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P03` | `/shipper/request/{RequestId}` | [ShipperRequestDetail.razor](../../../SsalddelApp/Components/Pages/ShipperRequestDetail.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P03/README.md) |
| `SsalddelApp-P02` | `/shipper/request/{RequestId}/payment` | [ShipperRequestPaymentPage.razor](../../../SsalddelApp/Components/Pages/ShipperRequestPaymentPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P02` | `/shipper/request/procedure` | [ShipperRequestProcedurePage.razor](../../../SsalddelApp/Components/Pages/ShipperRequestProcedurePage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P02` | `/shipper/request/{RequestId}/proofs` | [ShipperRequestProofsPage.razor](../../../SsalddelApp/Components/Pages/ShipperRequestProofsPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P02` | `/shipper/request/review`<br>`/shipper/request/summary` | [ShipperRequestReviewPage.razor](../../../SsalddelApp/Components/Pages/ShipperRequestReviewPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P02` | `/shipper/request/{RequestId}/timeline` | [ShipperRequestTimelinePage.razor](../../../SsalddelApp/Components/Pages/ShipperRequestTimelinePage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P02` | `/shipper/request/transport` | [ShipperRequestTransportPage.razor](../../../SsalddelApp/Components/Pages/ShipperRequestTransportPage.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P02` | `/shipper/request` | [ShipperRequestWizard.razor](../../../SsalddelApp/Components/Pages/ShipperRequestWizard.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P02/README.md) |
| `SsalddelApp-P01-2` | `/shipper/settings/views` | [ShipperViewSettings.razor](../../../SsalddelApp/Components/Pages/ShipperViewSettings.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P01-2/README.md) |
| `SsalddelApp-P09` | `/shipper/transport` | [TransportWorkspace.razor](../../../SsalddelApp/Components/Pages/TransportWorkspace.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P09/README.md) |
| `SsalddelApp-P00` | `/` | [UnifiedHome.razor](../../../SsalddelApp/Components/Pages/UnifiedHome.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P00/README.md) |
| `SsalddelApp-Components-Pages-UnitedStatesKoreanFoodGroupBuyPage` | `/us/korean-food-group-buy` | [UnitedStatesKoreanFoodGroupBuyPage.razor](../../../SsalddelApp/Components/Pages/UnitedStatesKoreanFoodGroupBuyPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-Components-Pages-UsdaUnitedStatesPriceComparison` | `/information/usda-us-price-comparison` | [UsdaUnitedStatesPriceComparison.razor](../../../SsalddelApp/Components/Pages/UsdaUnitedStatesPriceComparison.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P05-1` | `/shipper/warehouse/inventory` | [WarehouseInventory.razor](../../../SsalddelApp/Components/Pages/WarehouseInventory.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P05-1/README.md) |
| `SsalddelApp-Components-Pages-WarehouseRegistrationPage` | `/shipper/warehouses/new` | [WarehouseRegistrationPage.razor](../../../SsalddelApp/Components/Pages/WarehouseRegistrationPage.razor) | SsalddelApp | 미검토 |
| `SsalddelApp-P05-2` | `/shipper/warehouse/scan` | [WarehouseScanStation.razor](../../../SsalddelApp/Components/Pages/WarehouseScanStation.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P05-2/README.md) |
| `SsalddelApp-P05` | `/shipper/warehouse/workspace` | [WarehouseWorkspace.razor](../../../SsalddelApp/Components/Pages/WarehouseWorkspace.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P05/README.md) |
| `SsalddelApp-P05-3` | `/shipper/warehouse/work/{ProcessCode}` | [WarehouseWorkStart.razor](../../../SsalddelApp/Components/Pages/WarehouseWorkStart.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P05-3/README.md) |
| `SsalddelApp-P90` | `/weather` | [Weather.razor](../../../SsalddelApp/Components/Pages/Weather.razor) | SsalddelApp | [이전 화면 문서](SsalddelApp/SsalddelApp-P90/README.md) |

## WarehouseManagerApp

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `WarehouseManagerApp-Components-Pages-ApartmentAllocation` | `/warehouse/apartment/allocation` | [ApartmentAllocation.razor](../../../WarehouseManagerApp/Components/Pages/ApartmentAllocation.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ApartmentArrivals` | `/warehouse/apartment/arrivals` | [ApartmentArrivals.razor](../../../WarehouseManagerApp/Components/Pages/ApartmentArrivals.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ApartmentHandoff` | `/warehouse/apartment/handoff` | [ApartmentHandoff.razor](../../../WarehouseManagerApp/Components/Pages/ApartmentHandoff.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ApartmentInbound` | `/warehouse/apartment/inbound` | [ApartmentInbound.razor](../../../WarehouseManagerApp/Components/Pages/ApartmentInbound.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ApartmentUnclaimed` | `/warehouse/apartment/unclaimed` | [ApartmentUnclaimed.razor](../../../WarehouseManagerApp/Components/Pages/ApartmentUnclaimed.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityBoardManagementPage` | `/community/boards/manage` | [CommunityBoardManagementPage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityBoardManagementPage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityBoardPage` | `/community/boards` | [CommunityBoardPage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityBoardPage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityHomePage` | `/community` | [CommunityHomePage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityHomePage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityLedgerDraftPage` | `/community/ledgers/new` | [CommunityLedgerDraftPage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityLedgerDraftPage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityPostComposePage` | `/community/write` | [CommunityPostComposePage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityPostComposePage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityPostDetailPage` | `/community/posts/{PostId:long}` | [CommunityPostDetailPage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityPostDetailPage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityRecommendedPostDetailPage` | `/community/posts/recommended/detail` | [CommunityRecommendedPostDetailPage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityRecommendedPostDetailPage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityRecommendedPostsPage` | `/community/posts/recommended` | [CommunityRecommendedPostsPage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityRecommendedPostsPage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-CommunityWorkspacePage` | `/community/workspace` | [CommunityWorkspacePage.razor](../../../WarehouseManagerApp/Components/Pages/CommunityWorkspacePage.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-DiagramWorkbench` | `/diagram` | [DiagramWorkbench.razor](../../../WarehouseManagerApp/Components/Pages/DiagramWorkbench.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ExpectedInbounds` | `/warehouse/inbounds/expected` | [ExpectedInbounds.razor](../../../WarehouseManagerApp/Components/Pages/ExpectedInbounds.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-P03-2` | `/warehouse/general/inventory` | [GeneralInventory.razor](../../../WarehouseManagerApp/Components/Pages/GeneralInventory.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P03-2/README.md) |
| `WarehouseManagerApp-P04-2` | `/warehouse/general/transport-handoff`<br>`/admin/order-warehouse/outbound-handoff` | [GeneralTransportHandoff.razor](../../../WarehouseManagerApp/Components/Pages/GeneralTransportHandoff.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P04-2/README.md) |
| `WarehouseManagerApp-P01` | `/` | [Home.razor](../../../WarehouseManagerApp/Components/Pages/Home.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P01/README.md) |
| `WarehouseManagerApp-Components-Pages-ImportArrival` | `/warehouse/import/arrival` | [ImportArrival.razor](../../../WarehouseManagerApp/Components/Pages/ImportArrival.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ImportCustoms` | `/warehouse/import/customs` | [ImportCustoms.razor](../../../WarehouseManagerApp/Components/Pages/ImportCustoms.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ImportDomesticHandoff` | `/warehouse/import/domestic-handoff` | [ImportDomesticHandoff.razor](../../../WarehouseManagerApp/Components/Pages/ImportDomesticHandoff.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-ImportRelease` | `/warehouse/import/release` | [ImportRelease.razor](../../../WarehouseManagerApp/Components/Pages/ImportRelease.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-P03` | `/work/inbound/inspection` | [InboundInspection.razor](../../../WarehouseManagerApp/Components/Pages/InboundInspection.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P03/README.md) |
| `WarehouseManagerApp-P03` | `/work/inbound/inspection/{InboundItemId:long}` | [InboundInspectionDetail.razor](../../../WarehouseManagerApp/Components/Pages/InboundInspectionDetail.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P03/README.md) |
| `WarehouseManagerApp-P03` | `/work/inbound/inspection/{InboundItemId:long}/record` | [InboundInspectionRecord.razor](../../../WarehouseManagerApp/Components/Pages/InboundInspectionRecord.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P03/README.md) |
| `WarehouseManagerApp-P03-1` | `/work/inbound/products` | [InboundProductScan.razor](../../../WarehouseManagerApp/Components/Pages/InboundProductScan.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P03-1/README.md) |
| `WarehouseManagerApp-P05` | `/mart` | [MartHome.razor](../../../WarehouseManagerApp/Components/Pages/MartHome.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P05/README.md) |
| `WarehouseManagerApp-P05-3` | `/mart/picking` | [MartPickingPacking.razor](../../../WarehouseManagerApp/Components/Pages/MartPickingPacking.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P05-3/README.md) |
| `WarehouseManagerApp-Components-Pages-MartPickingPackingDetail` | `/mart/picking/orders/{OrderId:long}` | [MartPickingPackingDetail.razor](../../../WarehouseManagerApp/Components/Pages/MartPickingPackingDetail.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-P05-1` | `/mart/work-board` | [MartWorkBoard.razor](../../../WarehouseManagerApp/Components/Pages/MartWorkBoard.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P05-1/README.md) |
| `WarehouseManagerApp-P05-2` | `/mart/work/{ProcessCode}` | [MartWorkStart.razor](../../../WarehouseManagerApp/Components/Pages/MartWorkStart.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P05-2/README.md) |
| `WarehouseManagerApp-P99` | `/not-found` | [NotFound.razor](../../../WarehouseManagerApp/Components/Pages/NotFound.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P99/README.md) |
| `WarehouseManagerApp-P04-3` | `/warehouse/general/outbound-plan-review` | [OutboundPlanReview.razor](../../../WarehouseManagerApp/Components/Pages/OutboundPlanReview.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P04-3/README.md) |
| `WarehouseManagerApp-P04-1` | `/work/outbound/packing`<br>`/admin/order-warehouse/packing` | [PackingTask.razor](../../../WarehouseManagerApp/Components/Pages/PackingTask.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P04-1/README.md) |
| `WarehouseManagerApp-P04` | `/work/picking-batch`<br>`/admin/order-warehouse/picking` | [PickingBatch.razor](../../../WarehouseManagerApp/Components/Pages/PickingBatch.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P04/README.md) |
| `WarehouseManagerApp-P04` | `/work/picking-batch/{TaskKey}` | [PickingBatchDetail.razor](../../../WarehouseManagerApp/Components/Pages/PickingBatchDetail.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P04/README.md) |
| `WarehouseManagerApp-P04` | `/work/picking-batch/{TaskKey}/execute` | [PickingBatchExecute.razor](../../../WarehouseManagerApp/Components/Pages/PickingBatchExecute.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P04/README.md) |
| `WarehouseManagerApp-P03-3` | `/work/inbound/put-away` | [PutAwayTask.razor](../../../WarehouseManagerApp/Components/Pages/PutAwayTask.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P03-3/README.md) |
| `WarehouseManagerApp-P02-3` | `/scan` | [ScanStation.razor](../../../WarehouseManagerApp/Components/Pages/ScanStation.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P02-3/README.md) |
| `WarehouseManagerApp-P04-4` | `/warehouse/general/transport-request-draft` | [TransportRequestDraft.razor](../../../WarehouseManagerApp/Components/Pages/TransportRequestDraft.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P04-4/README.md) |
| `WarehouseManagerApp-Components-Pages-WarehouseExceptions` | `/warehouse/exceptions` | [WarehouseExceptions.razor](../../../WarehouseManagerApp/Components/Pages/WarehouseExceptions.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-WarehouseHistory` | `/warehouse/history` | [WarehouseHistory.razor](../../../WarehouseManagerApp/Components/Pages/WarehouseHistory.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-WarehouseSettings` | `/warehouse/settings` | [WarehouseSettings.razor](../../../WarehouseManagerApp/Components/Pages/WarehouseSettings.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-Components-Pages-WarehouseWorkspace` | `/warehouse`<br>`/admin/order-warehouse` | [WarehouseWorkspace.razor](../../../WarehouseManagerApp/Components/Pages/WarehouseWorkspace.razor) | WarehouseManagerApp | 미검토 |
| `WarehouseManagerApp-P02-2` | `/work/{ProcessCode}/workbench` | [WorkbenchScan.razor](../../../WarehouseManagerApp/Components/Pages/WorkbenchScan.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P02-2/README.md) |
| `WarehouseManagerApp-P02` | `/work-board` | [WorkBoard.razor](../../../WarehouseManagerApp/Components/Pages/WorkBoard.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P02/README.md) |
| `WarehouseManagerApp-P02-1` | `/work/{ProcessCode}` | [WorkStart.razor](../../../WarehouseManagerApp/Components/Pages/WorkStart.razor) | WarehouseManagerApp | [이전 화면 문서](WarehouseManagerApp/WarehouseManagerApp-P02-1/README.md) |

## eng

| 페이지 ID | route (별칭 포함) | 소스 | 포함 호스트 | 상세 검토 |
| --- | --- | --- | --- | --- |
| `eng-web-role-app-Pages-RoleNotFound` | `/not-found` | [RoleNotFound.razor](../../../eng/web-role-app/Pages/RoleNotFound.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.DriverApp, Ssalddel.Web.OrdererApp, Ssalddel.Web.ShipperApp, Ssalddel.Web.WarehouseApp | 미검토 |
| `eng-web-role-app-Pages-RoleRoot` | `/` | [RoleRoot.razor](../../../eng/web-role-app/Pages/RoleRoot.razor) | Ssalddel.Web.CommunityApp, Ssalddel.Web.DriverApp, Ssalddel.Web.OrdererApp, Ssalddel.Web.ShipperApp, Ssalddel.Web.WarehouseApp | 미검토 |

## 과거 문서의 비라우트 참조·누락 소스

기존 README가 함께 연결한 공용/접근 제어 컴포넌트는 페이지에서 제외한다. 파일 없음과 파일은 있으나 @page가 없는 경우를 구별하며 기존 ID·문서는 삭제하지 않는다. 삭제/이동의 원인을 확정할 때는 Git 이력을 대조한다.

- `OrdererApp-P02`: `Ssalddel.Ui.Common/Areas/App/Components/Orderer/GroupPurchaseScreenFrame.razor` — 기존 비라우트 컴포넌트
- `WarehouseManagerApp-P03-1`: `Ssalddel.Ui.Common/Areas/App/Components/WarehouseOperations/SsalddelInboundReceivingWorkspace.razor` — 기존 비라우트 컴포넌트
- `SsalddelApp-P01`: `SsalddelApp/Components/Shared/ShipperHomeAppShell.razor` — 기존 비라우트 컴포넌트
- `WarehouseManagerApp-P03`: `WarehouseManagerApp/Components/Pages/InboundInspectionAccessFrame.razor` — 기존 비라우트 컴포넌트
- `WarehouseManagerApp-P04`: `WarehouseManagerApp/Components/Pages/PickingBatchAccessFrame.razor` — 기존 비라우트 컴포넌트
