# HS·소매 카테고리 반복 수집 첫 배치 r10

- 상위 기획: `PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD`
- 구현일: `2026-09-21`
- 배치: `beverage-raw-01`
- 상태: `Implemented / LocalMySqlStoredAndIndependentlyVerified / IdempotentReplayVerified / CategoryBindingPendingReview / PrivateReviewOnly / UnityProjectionDeferred`

## 목적과 반복 단위

HS 품목을 한꺼번에 무제한 수집하지 않고 다음 단계를 배치 하나로 고정했다.

`HS6·한국 HSK10 확인 → 2025년 대한민국 수입 집계 → 내부 소매 카테고리 → 쿠팡 말단 카테고리 후보 → 대표 상품 관찰 → MySQL 저장·재조회`

다음 배치는 새 `*.manifest.json`과 검토된 상품 관찰 파일을 추가한 뒤 같은 실행기를 사용한다. HS 분류, 내부 카테고리, 쿠팡 카테고리와 개별 상품은 서로 다른 사실이며 후보 관계로만 연결한다.

## 첫 배치

| HS6 / HSK10 | 품목 | 내부 소매 카테고리 | 쿠팡 말단 카테고리 후보 | 2025 수입금액 | 순중량 |
| --- | --- | --- | --- | ---: | ---: |
| `090111` / `0901110000` | 카페인을 제거하지 않은 커피 생두 | 커피 생두 | 생두 | USD 1,212,437,445 | 167,421,062.365kg |
| `090210` / `0902100000` | 3kg 이하 소포장 녹차 | 소포장 녹차 | 녹차 | USD 730,918 | 18,615.638kg |
| `090230` / `0902300000` | 3kg 이하 소포장 홍차·부분 발효차 | 소포장 홍차 | 홍차 | USD 9,836,649 | 512,945.338kg |
| `180500` / `1805000000` | 무가당 코코아 가루 | 무가당 코코아 가루 | 코코아/핫초코분말 | USD 90,764,518 | 10,928,965.619kg |

한국 HSK10 명칭은 관세법령정보포털 2026년 분류표로 교차 확인했다. 무역값은 UN Comtrade 공개 preview의 대한민국 2025년 수입 세계 합계다. 개별 상품의 실제 통관 세번을 확정하는 근거로 사용하지 않는다.

## 쿠팡 분류·상품 관찰

- 쿠팡 공식 상품등록 가이드는 `displayCategoryCode`를 WING 전체 카테고리 파일 또는 인증된 카테고리 API로 확인하도록 안내한다.
- 이 작업에는 판매자 업체코드·API 키가 없으므로 숫자 `displayCategoryCode`를 만들지 않았다. 공개 상품 제목의 `생두`, `녹차`, `홍차`, `코코아/핫초코분말`을 `PendingSellerCategoryVerification` 후보로 저장했다.
- 쿠팡 소비자 검색은 자동 브라우저 요청을 403으로 거절했다. 우회하지 않고 검색엔진에 공개 색인된 쿠팡 상세 페이지 한 건씩만 관찰했다.
- 대표 상품은 검색엔진 첫 관련 결과일 뿐 쿠팡 인기·판매·검색 순위가 아니다. 가격·재고도 색인 시점 관찰이며 현재값으로 게시하지 않는다.
- 네 상품 모두 `PendingHumanReview`, `affiliate=false`, `distributionApproved=false`다. 코코아 후보 하나는 URL의 `sourceType=srp_product_ads` 근거로 광고 관찰을 별도 표시했다.

## 구현과 실행

- [반복 배치 실행기](../../../../../eng/Ssalddel.PublicDataPortalImport/무역소매Batch.cs)
- [첫 배치 명세](../../../../../eng/public-data/trade-retail/batches/beverage-raw-01.manifest.json)
- [첫 상품 관찰](../../../../../eng/public-data/trade-retail/batches/beverage-raw-01.products.reviewed.json)

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-acquire . beverage-raw-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-self-test . beverage-raw-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-preview . beverage-raw-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-apply . beverage-raw-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-verify . beverage-raw-01
```

## 저장·검증 결과

| 항목 | 결과 |
| --- | ---: |
| 공식 무역 원본 응답 | 208행 |
| 무역 금액·중량 정규화 | 48행 |
| HS6 분류 | 4행 |
| HS–내부–쿠팡 카테고리 후보 | 4행 |
| 쿠팡 상품 관찰 | 4행 |
| MySQL 최초 삽입 | 60행 |
| 독립 재조회 | 60행 |
| 동일 입력 재실행 | 신규 0행, 기존 60행 |

저장 대상은 `hongdal-mysql-1 / hongdal_dev`이며 기존 공공자료 원장 세 테이블을 재사용했다. 새 migration은 없다. 무역 원본 SHA-256은 `e552c8d271eae0a9a9da6f463d9e24fd86a435ea794a8c219483439bd841576d`다.

## 검증 상한

- 실제 외부 수집, 로컬 MySQL 저장, 독립 재조회와 중복 방지는 검증했다.
- 판매자 카테고리 숫자 코드, 개별 상품 HS 확정, 가격·재고 자동 갱신, 외부 게시, 서버 읽기 API와 Unity 투영은 검증하지 않았다.
- 상품 링크나 판매자 원산지 문구는 무역 상대국과 동일시하지 않는다.
- 이번 자료는 국가 단위 무역·소매 관찰이므로 새 역세권 디오라마 규칙 후보는 없다.
