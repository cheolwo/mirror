# Naver Commerce SmartStore Product Module

## 조사 기준

- 확인일: 2026-07-03
- 공식 문서: Naver Commerce API 2.81.0
- API 성격: 스마트스토어 기능을 HTTP API로 호출하는 서버 간 연동 API

## 공식 API 경계

상품 CRUD는 Naver Commerce API의 상품 v2 엔드포인트를 기준으로 분리한다.

- 상품 등록: `POST /external/v2/products`
- 채널 상품 조회: `GET /external/v2/products/channel-products/{channelProductNo}`
- 채널 상품 수정: `PUT /external/v2/products/channel-products/{channelProductNo}`
- 채널 상품 삭제: `DELETE /external/v2/products/channel-products/{channelProductNo}`
- 원상품 조회: `GET /external/v2/products/origin-products/{originProductNo}`
- 원상품 수정: `PUT /external/v2/products/origin-products/{originProductNo}`
- 원상품 삭제: `DELETE /external/v2/products/origin-products/{originProductNo}`

인증은 OAuth2 Client Credentials 방식이며, 토큰 요청 시 `client_id`, `timestamp`, `client_secret_sign`, `grant_type`, `type` 값을 사용한다. `client_secret_sign`은 `clientId_timestamp` 값을 bcrypt로 해싱한 뒤 Base64 인코딩한다.

## 구현 방향

현재 구현은 `HongdalApp.Services.Commerce.Naver` 아래에 외부 API 어댑터로 둔다.

- `INaverSmartStoreProductClient`: 상품 등록, 조회, 수정, 삭제 클라이언트 계약
- `NaverSmartStoreProductClient`: 네이버 상품 v2 엔드포인트 호출 구현
- `INaverCommerceTokenProvider`: 인증 토큰 공급 계약
- `NaverCommerceTokenProvider`: Client Credentials 토큰 발급 및 짧은 캐시
- `INaverCommerceSignatureGenerator`: 전자서명 생성 계약
- `BCryptNaverCommerceSignatureGenerator`: bcrypt/Base64 전자서명 구현
- `NaverCommerceOptions`: BaseUrl, ClientId, ClientSecret, TokenType 설정

상품 payload는 우선 `JsonNode`로 받는다. 네이버 상품 구조체가 넓고 변경 가능성이 높기 때문에, 내부 판매상품 모델과 네이버 payload 매핑은 별도 매퍼 계층에서 다루는 것이 낫다.

## 다음 확장 지점

- 내부 `판매상품저장요청`에서 네이버 상품 등록 payload로 변환하는 mapper 추가
- 스마트스토어 채널 계정별 credential 저장소 추가
- 실제 호출 결과의 `originProductNo`, `channelProductNo`를 내부 `채널출품` 레코드에 저장
- 네이버 오류 응답 코드별 사용자 메시지/재시도 정책 분리
