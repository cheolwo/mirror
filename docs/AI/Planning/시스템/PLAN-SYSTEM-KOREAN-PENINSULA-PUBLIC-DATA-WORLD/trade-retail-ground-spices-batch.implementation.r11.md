# 분쇄 향신료 무역·소매 반복 수집 r11

- 상위 기획: `PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD`
- 구현일: `2026-09-21`
- 배치: `ground-spices-01`
- 상태: `Implemented / LocalMySqlStoredAndIndependentlyVerified / IdempotentReplayVerified / CategoryBindingPendingReview / PrivateReviewOnly / UnityProjectionDeferred`

## 범위

기존 반복 배치 실행기로 대한민국의 2025년 수입 통계와 분쇄 향신료 네 품목을 수집했다. HS 분류, 한국 HSK10, 내부 소매 카테고리, 쿠팡 말단 카테고리 후보와 개별 상품 관찰은 서로 다른 사실로 저장한다.

| HS6 / HSK10 | 품목 | 내부 소매 카테고리 | 쿠팡 말단 카테고리 후보 | 2025 수입금액 | 순중량 |
| --- | --- | --- | --- | ---: | ---: |
| `090412` / `0904120000` | 부수거나 잘게 부순 후추 | 분쇄 후추 | 기타조미료 | USD 9,296,276 | 1,002,800.870kg |
| `090422` / `0904220000` | 부수거나 잘게 부순 고추류 | 고춧가루 | 고추가루 | USD 10,015,696 | 2,633,942.969kg |
| `090620` / `0906200000` | 부수거나 잘게 부순 계피와 계피나무의 꽃 | 계피·시나몬 가루 | 강황/진저/계피 | USD 1,435,746 | 218,248.177kg |
| `090932` / `0909320000` | 부수거나 잘게 부순 커민 씨 | 커민·큐민 가루 | 허브향신료 | USD 225,468 | 45,237.277kg |

무역값은 UN Comtrade 공개 preview의 대한민국 2025년 수입 세계 합계다. 한국 HSK10은 관세법령정보포털 분류표와 교차 확인했지만, 이것으로 개별 판매 상품의 실제 통관 세번을 확정하지 않는다.

## 대표 상품 관찰 경계

- 쿠팡 공개 색인 결과에서 흑후추 분말·고춧가루·시나몬 파우더·큐민 분말을 각 한 건 관찰했다.
- 가격·배송·재고는 검색 색인 시점의 관찰이며 현재 판매 상태나 인기 순위를 뜻하지 않는다.
- 상품 제목에 나타난 대한민국·베트남·스리랑카는 판매 페이지 주장으로만 기록했다. 무역 상대국이나 법적 원산지 증거로 사용하지 않는다.
- 숫자 `displayCategoryCode`는 WING 전체 카테고리 파일 또는 인증 판매자 API 확인 전까지 `null`이다.
- 네 상품 모두 `PendingHumanReview`, `affiliate=false`, `distributionApproved=false`다.

## 구현 자료

- [배치 명세](../../../../../eng/public-data/trade-retail/batches/ground-spices-01.manifest.json)
- [검토 상품 관찰](../../../../../eng/public-data/trade-retail/batches/ground-spices-01.products.reviewed.json)
- [반복 배치 실행기](../../../../../eng/Ssalddel.PublicDataPortalImport/무역소매Batch.cs)

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-acquire . ground-spices-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-self-test . ground-spices-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-preview . ground-spices-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-apply . ground-spices-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-verify . ground-spices-01
```

## 저장·검증 결과

| 항목 | 결과 |
| --- | ---: |
| 공식 무역 원본 응답 | 111행 |
| 무역 금액·중량 정규화 | 48행 |
| HS6 분류 | 4행 |
| HS–내부–쿠팡 카테고리 후보 | 4행 |
| 쿠팡 상품 관찰 | 4행 |
| MySQL 최초 삽입 | 60행 |
| 독립 재조회 | 60행 |
| 동일 입력 재실행 | 신규 0행, 기존 60행 |

저장 대상은 `hongdal-mysql-1 / hongdal_dev`다. 무역 원본 SHA-256은 `f17bfcf974cc8e7d79d496348af85fc026537566a24ceeb34495dd58d130ab9a`이며 새 migration은 없다.

## 검증 상한

- 외부 원본 수집, 로컬 MySQL 저장, 독립 재조회와 중복 방지는 검증했다.
- 판매자 숫자 카테고리, 개별 상품 HS 확정, 가격·재고 자동 갱신, 서버 읽기 API, Unity 투영과 외부 게시를 검증하지 않았다.
- 국가 무역 자료를 행정동이나 개별 건물에 자동 배분하지 않는다.
- 국가 단위 무역·소매 관찰이므로 새 역세권 디오라마 규칙 후보는 없다.
