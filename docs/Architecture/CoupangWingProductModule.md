# Coupang WING Product Module

## 조사 기준

- 확인일: 2026-07-03
- 공식 문서: Coupang Open API 개발자 문서
- API 성격: WING에서 발급한 OpenAPI Key를 사용하는 판매자 상품 연동 API

## 연동 준비

쿠팡 Open API는 WING 판매자 계정이 필요하며, WING에서 OpenAPI Key를 직접 발급받아 사용한다. 발급 후 실제 접근 권한 반영에 시간이 걸릴 수 있고, 공식 문서상 별도 테스트 환경은 제공되지 않는다. 키는 Access Key, Secret Key 형태이며 유출에 주의해야 한다.

## 인증 방식

쿠팡 Open API는 OAuth 토큰 발급이 아니라 매 요청의 `Authorization` 헤더에 HMAC Signature를 포함하는 방식이다.

- 알고리즘: `HmacSHA256`
- signed-date 형식: `yyMMdd'T'HHmmss'Z'`
- 서명 메시지: `signedDate + method + path + query`
- Authorization 형식: `CEA algorithm=HmacSHA256, access-key={accessKey}, signed-date={signedDate}, signature={signature}`

## 공식 API 경계

상품 API는 등록상품 ID인 `sellerProductId`를 중심으로 동작한다.

- 상품 생성: `POST /v2/providers/seller_api/apis/api/v1/marketplace/seller-products`
- 상품 조회: `GET /v2/providers/seller_api/apis/api/v1/marketplace/seller-products/{sellerProductId}`
- 상품 수정, 승인필요: `PUT /v2/providers/seller_api/apis/api/v1/marketplace/seller-products`
- 상품 수정, 승인불필요: `PUT /v2/providers/seller_api/apis/api/v1/marketplace/seller-products/{sellerProductId}/partial`
- 상품 삭제: `DELETE /v2/providers/seller_api/apis/api/v1/marketplace/seller-products/{sellerProductId}`

쿠팡 상품 삭제는 상태 제약이 있다. 공식 문서 기준으로 승인대기중 상태가 아니며 상품에 포함된 옵션이 모두 판매중지된 경우 삭제 가능하다.

## 구현 방향

현재 구현은 `HongdalApp.Services.Commerce.Coupang` 아래에 외부 API 어댑터로 둔다.

- `ICoupangWingProductClient`: 상품 생성, 조회, 수정, 부분수정, 삭제 클라이언트 계약
- `CoupangWingProductClient`: 쿠팡 WING 상품 엔드포인트 호출 구현
- `ICoupangWingSignatureGenerator`: HMAC Authorization 생성 계약
- `HmacCoupangWingSignatureGenerator`: HMAC-SHA256 서명 구현
- `CoupangWingOptions`: BaseUrl, AccessKey, SecretKey, VendorId 설정

상품 payload는 네이버 모듈과 동일하게 우선 `JsonNode`로 받는다. 쿠팡 상품 생성 payload는 카테고리, 출고지, 반품지, 고시정보, 옵션 정보 등이 크고 카테고리별 변화가 있으므로, 내부 판매상품 모델과 쿠팡 payload 사이의 변환은 별도 mapper 계층으로 분리한다.

## 다음 확장 지점

- 내부 판매상품을 쿠팡 생성 payload로 변환하는 mapper 추가
- WING 채널 계정별 AccessKey/SecretKey/VendorId 저장소 추가
- `sellerProductId`, `vendorItemId`, `externalVendorSku`를 내부 채널출품 레코드에 저장
- 상품 승인 요청, 등록 현황 조회, 목록 조회, 가격/재고/판매상태 변경 API 추가
- 쿠팡 오류 메시지별 사용자 안내와 재시도 정책 분리
