# Commerce Channel Integration Module

## 목적

화주 앱의 판매상품/채널출품 흐름을 외부 판매채널 API와 직접 결합하지 않고, 채널별 어댑터를 조합하는 구조로 확장한다.

현재 지원 대상으로 잡은 채널은 다음과 같다.

- SmartStore: Naver Commerce API
- Coupang: Coupang WING Open API
- Amazon: Amazon Selling Partner API, export readiness skeleton first
- ElevenStreet: 후속 연동 후보

## 모듈 경계

공통 채널 계층은 `HongdalApp.Services.Commerce` 아래에 둔다.

- `ICommerceChannelCatalog`: 앱에서 지원하는 판매채널 목록과 채널 식별자 해석
- `CommerceChannelDescriptor`: 채널명, 외부 제공자명, 상품 CRUD 지원 여부, 연동 상태
- `ICommerceChannelListingService`: 내부 판매상품과 판매채널 계정을 받아 출품 연동 준비
- `IProductListingPayloadBuilder`: 내부 판매상품을 채널별 상품 payload 초안으로 변환

채널별 저수준 API 클라이언트는 제공자별 하위 네임스페이스에 둔다.

- `HongdalApp.Services.Commerce.Naver`
- `HongdalApp.Services.Commerce.Coupang`
- `HongdalApp.Services.Commerce.Amazon`

## 현재 동작

`ShipperSalesService.CreateListingAsync`는 내부 채널출품 레코드를 만든 뒤, 연결된 판매채널 계정의 `채널종류`를 기준으로 공통 채널 계층에 출품 준비를 요청한다.

현재는 실제 외부 상품 등록 호출을 자동 실행하지 않는다. 대신 다음 상태를 내부 출품 레코드에 반영한다.

- 지원 채널 + payload builder 존재: `동기화상태 = 연동준비`
- 지원 채널 + payload builder 없음: `동기화상태 = 연동대기`
- 미지원 채널: `동기화상태 = 수동관리`

외부 API 실제 호출은 네이버/쿠팡의 저수준 클라이언트에 이미 분리되어 있으며, 실제 운영 credential과 상품 payload mapper가 준비된 뒤 상위 출품 서비스에서 호출하도록 확장한다.

Amazon은 실제 SP-API 호출 전에 `AmazonExportReadinessPlanner`로 수출 준비 게이트를 먼저 확인한다. Amazon 출품은 단순 채널출품이 아니라 수입 참여자 자격, 국내 물류 이력, 후기 사용 동의, 이미지형 상세페이지/광고 소재, 수출 HS 검토, 관세사 수임과 비용, 상업송장/패킹리스트, 재고 예약, 출고 배치, 국제배송, 반품/정산 정책까지 함께 확인해야 하므로 별도 readiness 골격을 둔다.

## 왜 실제 호출을 아직 자동화하지 않는가

네이버와 쿠팡 모두 상품 생성 payload가 단순 상품명/가격/SKU만으로 완성되지 않는다.

- 네이버: 카테고리, 배송, A/S, 원상품 상세정보, 고시정보 등 필요
- 쿠팡: vendorId, 카테고리, 출고지, 반품지, 고시정보, 옵션/아이템 구조 등 필요

따라서 현재 단계에서 반쪽 payload로 외부 API를 호출하면 실패 응답만 만드는 구조가 된다. 지금은 내부 판매상품을 채널별 payload 초안으로 변환하는 builder를 두고, 필수 항목 매핑을 채우는 다음 단계로 넘긴다.

## 다음 구현 순서

1. 채널 계정 credential 저장 구조 분리
2. 내부 판매상품/재고/창고 정보를 채널별 필수 payload로 변환하는 mapper 추가
3. 출품 생성 시 `연동준비` 상태에서 사용자가 payload를 보정하고 `외부 등록`을 실행하는 워크플로 추가
4. 외부 등록 성공 시 네이버 `originProductNo/channelProductNo`, 쿠팡 `sellerProductId/vendorItemId`를 내부 채널출품에 저장
5. 가격, 재고, 판매상태 변경 API를 채널별 커맨드로 추가
6. Amazon은 `AmazonExportReadinessPlanner`의 게이트가 닫힌 뒤 marketplaceId, sellerId, productType, Product Type Definitions JSON Schema를 이용해 Listings Items API payload를 완성
