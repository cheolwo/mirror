# 메뉴→재료 수요 Simulation 첫 수직 조각

배달 기록에 남은 메뉴 세 건을 기존 식약처 공공 레시피 **참고 후보**와 연결하고, 계획 판매량에서 재료 중량 범위와 구매 단위 수를 역산한다. 실제 매장 배합, 구매 원가, 공급 계약 또는 발주를 확정하지 않는다.

## 상태 경계

| 층 | 상태 | 이 조각에서 뜻하는 것 |
| --- | --- | --- |
| 배달 관찰 | `ObservedFact` | 배달 기록에 매장명과 메뉴명이 있었다는 사실 |
| 공공 레시피 | `ReferenceCandidate` / `PendingReview` | 조리 유형 비교 후보이며 실제 지점 레시피가 아님 |
| 1인분 범위 | `SimulationEstimate` | 공공 레시피 수량을 중심으로 둔 수요 민감도 가정 |
| 합산 수요 | `SimulationDerivedEstimate` | 계획 인분 수를 곱한 계산 결과 |
| 운영 효과 | `None` | 계약·재고·발주·배차를 만들거나 수정하지 않음 |

`catalog.r1.json`은 기존 12개 재료 기반의 ID를 재사용한다. 레시피 원문의 다른 재료, 단위 환산이 불가능한 `개·줌·봉`, 매장 사용이 확인되지 않은 변형 재료는 `unresolvedRecipeComponents`에 남긴다. 쌀 리조또 참고자료는 인분 수가 없으므로 `RecipeYieldUnstated_AssumedOnePlanningServing`과 낮은 신뢰 상태를 함께 반환한다.

`product:ingredient:*` 값은 이 공급망 예시 안에서만 쓰는 `simulationProductStableId`다. 기존 재료 기반이 아직 연결하지 않은 전역 상품 ID를 만들지 않으며 `canonicalProductStableId=null`, `productIdentityStateCode=SimulationScopedCandidate`를 함께 반환한다.

## 실행

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- menu-demand-self-test .
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- menu-demand-analyze .
```

분석 결과는 `artifacts/local/public-data/menu-ingredient-demand/<sha256>.json`에 내용 주소 방식으로 저장된다. 같은 입력은 같은 JSON을 만들며, 메뉴별 조회 `menuDemand`와 재료별 역조회 `ingredientDemand`를 함께 제공한다.

## 공급망 연결 경계

- 닭고기: `product:ingredient:chicken`, `BOX`, 상자당 `10 KGM`
- 소고기: `product:ingredient:beef`, `BOX`, 상자당 `5 KGM`
- 쌀: `product:ingredient:rice`, `BAG`, 포대당 `20 KGM`
- 계산 근거: `source:menu-ingredient-estimate:r1` 및 메뉴 관찰·식약처 레시피 후보 stable ID

구매 단위 수는 `ceil(예상 KGM / 구매 단위 함량 KGM)`으로 계산한다. 결과는 공급망 Simulation에 넣을 수 있는 후보 입력일 뿐 `StoreOrder`, 실제 상품, 실제 계약 품목으로 자동 전환하지 않는다.

샘플 시나리오는 세 메뉴 총 480인분이다. 닭고기 9.9~12.1 KGM은 10 KGM 상자 1~2개, 소고기 7.2~8.8 KGM은 5 KGM 상자 2개, 쌀 27~33 KGM은 20 KGM 포대 2개로 계산된다.
