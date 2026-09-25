# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r17]

## 범위

HS 2022 법정 HS6 조사 대장의 미처리 코드를 오름차순으로 이어가기 위해 `010121`부터 `010392`까지 첫 12개를 `chapter01-live-animals-01` 배치로 닫았다. 이 묶음은 모두 살아있는 말·당나귀·노새·소·버펄로·돼지류다.

쿠팡의 [판매 불가 품목 안내](https://marketplace.coupang.com/information-center/blog-news7?rf=MARKETPLACE)는 살아있는 동물의 온라인 판매가 불가하다고 명시한다. 따라서 이 배치는 무관한 완구·축산물·사료를 대표 상품으로 붙이지 않고, 12개 모두 `RestrictedOrSensitive / LiveAnimalOnlineSaleProhibited`로 검토 완료했다. 상품·가격 관찰은 0건이고 배포 승인은 꺼져 있다.

## 계약 보완

- 기존 `trade-retail-batch.v1`과 `representative-product-observation.v2` 배치 다섯 개는 그대로 읽고 같은 행·키를 재생성한다.
- 신규 `trade-retail-batch.v2`는 HS6 하나에 여러 HSK10 자식을 보존한다. 이번 12개 HS6에는 관세청 HSK10 23개가 연결된다.
- 신규 `representative-product-observation.v3`는 코드별 검토 결과를 정확히 하나씩 두고 대표 상품은 0~3개를 허용한다.
- 검토 결과는 `ProductCandidateObserved`, `SearchNoCandidate`, `ChannelInapplicable`, `RestrictedOrSensitive` 중 하나다. 상품 0건도 명시적 근거가 있으면 완료 상태다.
- UN Comtrade 응답에 세계 합계가 없는 코드는 `NoReportedRows`로 남긴다. 이를 무역액·순중량 0으로 바꾸지 않으며, 응답 결손은 무역이 없다는 증명이 아님을 제한에 기록한다.
- 상대국이 다섯 개보다 적어도 세계 합계와 실제 반환된 상대국까지만 저장한다. 순중량 `null`도 0으로 바꾸지 않는 계약을 마련했다.

## 수집·저장 결과

- UN Comtrade 대한민국 2025년 수입 공개 응답: 28행.
- 무역 세계 합계가 있는 HS6: `010121`, `010129`, `010221`, `010239`, `010310`, `010392` 6개.
- 응답에 행이 없는 HS6: `010130`, `010190`, `010229`, `010231`, `010290`, `010391` 6개.
- 정규화: 무역 금액·순중량·가용 상태 58행, HS6 분류 12행, 소매 채널 검토 12행, 대표 상품 0행, 상품 검토 결과 12행, 합계 94행.
- MySQL 최초 적용: 신규 94, 기존 0. 독립 재조회 94행과 원본 hash 결속을 확인했다.
- 동일 입력 반복 적용: 신규 0, 기존 94.
- 전수 대장: 대표 상품 후보 관찰 30개, 제한 품목 검토 완료 12개, 대기 5,570개, UN 통계 특수 1개.
- 전수 대장 최초 갱신은 12행 수정, 반복 적용은 신규·수정 0행이었다.

## 출처와 제한

- HS6 분류: [UN Comtrade H6 참고 목록](https://comtradeapi.un.org/files/v1/app/reference/H6.json), HS 2022.
- 대한민국 2025년 수입: [UN Comtrade 공개 API](https://comtradeapi.un.org/public/v1/preview/C/A/HS), 연간·수입·보고국 410.
- HSK10과 성질별 분류: [관세청 HS부호](https://www.data.go.kr/data/15049722/fileData.do), [관세청 HSK별 성질별 분류](https://www.data.go.kr/data/15049720/fileData.do), 2026 판본.
- 온라인 판매 제한: [쿠팡 판매 불가 품목 안내](https://marketplace.coupang.com/information-center/blog-news7?rf=MARKETPLACE), 2026-09-21 확인.

관세청의 `소비재` 성질 분류는 통계 분류일 뿐 소매 판매 가능성을 뜻하지 않는다. 이번 결과는 비공개 검토 원장이며 개별 상품의 법적 세번, 현재 가격·재고, 인기·추천·판매 순위, Unity 표현을 확정하지 않는다.

읽기 API, Unity 투영·Game View, 외부 게시, commit·push는 수행하지 않았다.
