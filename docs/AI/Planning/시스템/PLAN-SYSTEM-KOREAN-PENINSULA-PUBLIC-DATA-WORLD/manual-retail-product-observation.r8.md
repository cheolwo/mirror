# 검색 상단 대표 상품 수동 관찰 r8

- 상위 기획: `PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD`
- 관찰일: `2026-09-21`
- 상태: `Observed / ManualBrowserSample / PaidPlacementSeparated / MySqlLoadDeferred / UnityDistributionNotApproved`
- 목적: 자동 API나 대량 수집 없이 커피류·바나나류·냉동 새우류의 검색 상단 상품을 소량 관찰하고, 향후 상품 관찰 원장에 필요한 최소 필드와 오인 방지 규칙을 확인한다.

## 관찰 방법

- 네이버 가격비교와 쿠팡의 기본 정렬 상태에서 품목별 검색어를 한 번씩 조회했다.
- 화면에서 가장 먼저 노출된 상품 하나를 기록했다. 두 플랫폼 모두 세 검색어의 첫 상품은 광고였으므로 `PaidPlacement`로 분리했다.
- 추적 parameter는 보존하지 않고 상품 식별에 필요한 기본 URL만 기록했다.
- 로그인·구매·장바구니·제휴 링크 생성·페이지 순회·대량 수집·이미지 복사는 하지 않았다.
- 이 결과는 같은 시점·세션의 화면 관찰이며 인기·판매 순위·추천·최저가·재고의 근거가 아니다.

## 수동 관찰값

| 플랫폼 | 검색어 | 첫 노출 상품 | 관찰 가격 | 배송 표시 | 배치 | 원산지 확인 | 정리한 링크 |
| --- | --- | --- | ---: | ---: | --- | --- | --- |
| 네이버 가격비교 | `원두 커피 1kg` | 모두의커피 1kg 맛있는 당일로스팅원두 | 21,900원 최대 할인가, 정상가 22,900원 | 3,500원 | `PaidPlacement / 광고` | 미확인 | <https://smartstore.naver.com/modoocoffee/products/2437210047> |
| 네이버 가격비교 | `바나나` | 휴대하기 좋은 낱개 포장 바나나 10개입(봉) | 11,900원, 쿠폰 적용 표시 10,115원 | 무료 | `PaidPlacement / 광고` | 판매 페이지 문구상 필리핀산, 공공 원산지 근거와 미결속 | <https://8dogam.com/product/3b0d3de53a62e481f186bf22207d69cd> |
| 네이버 가격비교 | `냉동 새우` | 노바시 새우 300g (40미) 튀김용 손질 냉동새우 | 7,900원, 정상가 9,900원 | 5,000원 | `PaidPlacement / 슈퍼적립광고` | 미확인 | <https://brand.naver.com/thecrab44/products/6437313204> |
| 쿠팡 | `원두 커피 1kg` | [대용량] 1kg x 3봉 베트남 로부스타 G1 홀빈 당일로스팅 사업자원두 | 41,100원, 표시 정상가 51,000원 | 3,000원, 조건부 무료 | `PaidPlacement / 광고` | 상품명 주장만 확인, 공공 원산지 근거와 미결속 | <https://www.coupang.com/vp/products/8816133749?itemId=25688929741&vendorItemId=92678174569> |
| 쿠팡 | `바나나` | 원시인농산 고당도 정품 돌(DOLE) 바나나 3kg내 2송이 | 11,170원, 표시 정상가 15,900원 | 무료 | `PaidPlacement / 광고` | 수입과일 표기만 확인, 국가 미확인 | <https://www.coupang.com/vp/products/7715803614?itemId=20694548865&vendorItemId=73110066035> |
| 쿠팡 | `냉동 새우` | 찐신선 껍질없는 냉동 흰다리 새우살 31/40미 900g 2개 | 38,860원, 표시 정상가 45,860원 | 무료 | `PaidPlacement / 광고` | 미확인 | <https://www.coupang.com/vp/products/9302779215?itemId=27559242008&vendorItemId=94523317480> |

검색 진입 주소는 각각 다음과 같다.

- 네이버 가격비교: [원두 커피 1kg](https://search.shopping.naver.com/search/all?query=%EC%9B%90%EB%91%90%20%EC%BB%A4%ED%94%BC%201kg), [바나나](https://search.shopping.naver.com/search/all?query=%EB%B0%94%EB%82%98%EB%82%98), [냉동 새우](https://search.shopping.naver.com/search/all?query=%EB%83%89%EB%8F%99%20%EC%83%88%EC%9A%B0)
- 쿠팡: [원두 커피 1kg](https://www.coupang.com/np/search?q=%EC%9B%90%EB%91%90+%EC%BB%A4%ED%94%BC+1kg), [바나나](https://www.coupang.com/np/search?q=%EB%B0%94%EB%82%98%EB%82%98), [냉동 새우](https://www.coupang.com/np/search?q=%EB%83%89%EB%8F%99+%EC%83%88%EC%9A%B0)

## 확인된 원장 필드

첫 MySQL 적재가 승인되면 기존 출처 계보와 별도의 상품 관찰 원장에 다음 최소값을 보존한다.

```text
ObservationStableId
PlatformCode
SearchQuery
ResultPosition
PlacementCode
ProductTitle
CanonicalExternalUrl
ObservedPrice
ReferencePrice
ShippingPrice
CurrencyCode
ObservedAt
OriginClaim
OriginEvidenceCode
LinkStatus
Affiliate
DistributionApproved
```

- `ObservedPrice`, 정상가·쿠폰가·배송비를 한 금액으로 합치지 않는다.
- `PlacementCode`는 `PaidPlacement`, `Organic`, `Unknown`을 구분한다.
- `OriginClaim`은 판매 페이지의 주장이고 관세청 품목·국가 통계와 직접 결속되지 않는다.
- URL은 허용 도메인과 HTTPS를 확인하고 추적 parameter를 제거한다. 쿠팡의 옵션 식별용 `itemId`, `vendorItemId`는 유지한다.
- 최초 저장 시 `Affiliate=false`, `DistributionApproved=false`로 둔다.

## 품목 연결 상태

| 관찰 묶음 | 소매 카테고리 후보 | 관세 품목 결속 | 현재 판정 |
| --- | --- | --- | --- |
| 커피류 | 원두·볶은 커피 | 정확한 HS 세번과 가공 상태 미확인 | `Pending` |
| 바나나류 | 신선 바나나 | 정확한 HS 세번·기간·상대국 미확인 | `Pending` |
| 냉동 새우류 | 냉동·손질 새우 | 종·냉동·조제 상태별 HS 세번 미확인 | `Pending` |

## 제한과 사용 금지

- 첫 노출이 모두 광고였으므로 `대표 인기 상품`, `판매 1위`, `추천 상품`으로 표시하지 않는다.
- 표시 가격과 배송 조건은 관찰 시점 이후 바뀔 수 있다. 자동 갱신 전까지 현재 가격처럼 노출하지 않는다.
- 상품명·판매자 설명만으로 제조국·원산지·수입 상대국을 확정하지 않는다.
- 상품 이미지와 상세 페이지 본문은 저장하거나 Unity에 배포하지 않는다.
- 이 문서만으로 MySQL 적재, 외부 게시, Unity 카드 공개, 구매 연결을 승인하지 않는다.

## 다음 관문

관세청 현행 품목표와 확정 수출입 기간에서 세 묶음의 정확한 HS 세번·가공 상태·상대국 집계를 먼저 확인한다. 이후 `Pending` 카테고리 연결을 사람이 검토하고, 같은 입력의 중복 방지와 독립 재조회를 포함한 제한 MySQL 적재 여부를 별도로 승인한다.
