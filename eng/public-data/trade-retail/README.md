# 무역 통계·소매 상품 관찰 수집기

대한민국의 2025년 연간 수입 통계에서 HS6 `090121`, `080390`, `030617`의 세계 합계와 품목별 상위 5개 상대국을 수집하고, 수동 검토한 네이버·쿠팡 상품 관찰 6건을 별도 사실로 저장한다.

## 자료 경계

- 무역 통계: [UN Comtrade](https://comtradeplus.un.org/) 공개 API, `reporterCode=410`, `flowCode=M`, `period=2025`, `classification=H6`
- HS 분류: UN Comtrade `H6 (HS 2022)` 참고 목록
- 한국 세번 교차 확인: [관세법령정보포털 CLIP](https://unipass.customs.go.kr/clip/hsinfosrch/openULS0201007Q.do)
- 상품 관찰: `representative-products.reviewed.json`의 2026-09-21 수동 검색 결과

상품 관찰의 `hs6Candidate`는 `PendingHumanReview`다. 상품명이나 판매자 원산지 문구를 무역 상대국 통계와 직접 연결하지 않는다. 여섯 상품은 모두 검색 상단 광고였으며 인기·추천·판매 순위를 뜻하지 않는다.

## 반복 배치

`batches/<batchId>.manifest.json`은 HS6·한국 HSK10·동결 세계 합계·내부 소매 검토와 검색어를 한 배치로 묶는다. v2는 HS6 하나의 여러 HSK10 자식, 무역 응답 결손, 대표 상품 0~3개를 보존한다. `batches/<batchId>.products.reviewed.json`은 코드별 검토 결과와 선택적인 대표 상품 관찰을 분리한다. 쿠팡 숫자 `displayCategoryCode`는 WING 전체 카테고리 파일 또는 인증된 판매자 API로 확인하기 전까지 `null`과 `PendingSellerCategoryVerification`을 유지하며, 판매 제한 품목에는 억지 카테고리나 상품을 만들지 않는다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-acquire . beverage-raw-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-self-test . beverage-raw-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-apply . beverage-raw-01
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-batch-verify . beverage-raw-01
```

첫 반복 배치 `beverage-raw-01`은 `090111`, `090210`, `090230`, `180500`을 담고 총 60행을 별도로 저장한다. 쿠팡 직접 검색이 403으로 거절되어 우회하지 않았으며, 공개 색인 상세 페이지 결과는 쿠팡 내부 순위가 아닌 제한된 관찰로 기록한다.

두 번째 반복 배치 `ground-spices-01`은 `090412`, `090422`, `090620`, `090932`의 분쇄 후추·고추류·계피·커민을 담고 총 60행을 별도로 저장한다. 실행 명령의 마지막 배치 ID만 `ground-spices-01`로 바꾸면 동일한 수집·자체시험·미리보기·저장·독립 재조회 절차를 수행한다. 두 반복 배치는 합계 120행이며 각각 독립된 원본 hash와 중복 방지 키를 가진다.

세 번째 반복 배치 `whole-spices-01`은 `090411`, `090421`, `090611`, `090931`의 통후추·건고추·실론 시나몬 스틱·커민 씨를 담고 총 60행을 별도로 저장한다. 세 반복 배치는 합계 180행이며 상품 후보는 모두 법적 HS 확정이 아닌 `PendingHumanReview`다.

네 번째 반복 배치 `coffee-vanilla-clove-ginger-01`은 볶은 커피·바닐라·정향·생강의 가공 전후 HS6 8개를 담고 총 120행을 별도로 저장한다. 네 반복 배치는 합계 300행이고 같은 입력의 독립 재조회와 중복 방지를 각각 확인했다.

다섯 번째 반복 배치 `chapter09-retail-consumer-02`는 마테·육두구·카다멈·코리앤더·강황·카레·월계수류 HS6 10개를 담고 총 150행을 별도로 저장한다. 다섯 반복 배치는 합계 450행, 관찰 코드 30개이며 동일 입력 신규 0행을 확인했다. 공식 무역 원본에서 순중량이 비어 있는 사프란은 0으로 대체하지 않고 결손 수치 계약의 후속 대상으로 남겼다.

여섯 번째부터 여덟 번째 반복 배치 `chapter01-live-animals-01`~`03`은 제1류 법정 HS6 `010121`~`010690` 34개와 관세청 HSK10 자식 69개를 빠짐없이 담는다. 쿠팡 공식 정책상 살아있는 동물은 온라인 판매 불가이므로 무관한 상품을 붙이지 않고 34개 모두 `RestrictedOrSensitive`로 닫았다. 대표 상품은 0행이며 세 배치 정규화 자료는 94·80·124행, 합계 298행이다. UN Comtrade 응답이 없는 코드는 0으로 바꾸지 않고 `NoReportedRows`로 남겼다.

아홉 번째 반복 배치 `chapter02-meat-01`은 다음 오름차순 HS6 `020110`~`020329` 12개와 관세청 HSK10 자식 16개를 담는다. 소비자 소매 단위가 아닌 소·돼지 도체 4개는 `ChannelInapplicable`로 상품 0건을 기록하고, 냉장·냉동과 뼈 유무가 검색 문구에서 확인되는 8개에는 색인 후보 19건을 `PendingHumanReview`로 기록했다. 뼈사태 후보 두 묶음은 제0206호 식용 설육과의 경계를 별도 검토해야 한다. 이 배치는 155행이다.

열 번째 반복 배치 `chapter02-meat-02`는 `020410`~`020621` 12개와 HSK10 자식 12개를 담는다. 면양 도체 4개는 채널 부적합, 나머지 8개는 색인 후보 17건이다. 냉동 소 혀 코드에는 제목에서 냉동을 확인할 수 있는 후보 한 건만 남기고 `Fresh/생` 표기 후보는 제외했다. 이 배치는 109행이며 열 반복 배치는 합계 1,012행이다.

열한 번째 반복 배치 `chapter02-meat-03`은 `020622`~`020724` 12개와 HSK10 자식 22개를 담는다. 정확한 축종·원물·보관 상태가 확인된 8개 코드에는 색인 후보 13건을 남겼고, 나머지 4개는 무관한 가공품·반려동물용 상품을 붙이지 않고 `SearchNoCandidate`로 기록했다. 이 배치는 121행이며 열한 반복 배치는 합계 1,133행이다.

열두 번째 반복 배치 `chapter02-meat-04`는 `020725`~`020754` 12개와 HSK10 자식 22개를 담는다. 냉동 통칠면조와 냉장·냉동 통오리·오리 절단육 5개 코드에는 색인 후보 8건을 남겼고, 정확한 형태·보관 상태를 확인하지 못한 나머지 7개는 `SearchNoCandidate`로 기록했다. 이 배치는 78행이며 열두 반복 배치는 합계 1,211행이다.

열세 번째 반복 배치 `chapter02-meat-05`는 `020755`~`021012` 12개와 HSK10 자식 24개를 담는다. 염장·숙성·훈연 삼겹살 `021012`에 색인 후보 3건을 남겼고 형태·가공 조건이 맞지 않거나 정확한 후보가 없는 나머지 11개는 `SearchNoCandidate`로 기록했다. 이 배치는 87행이며 열세 반복 배치는 합계 1,298행이다.

열네 번째 반복 배치 `chapter02-meat-06`은 `021019`~`030194` 12개와 HSK10 자식 22개를 담는다. 염장·건조·훈연 육류 3개 코드에는 공개 색인 후보 3건을 `PendingHumanReview`로 남겼고, 정확한 후보가 없는 육류 3개는 `SearchNoCandidate`로 기록했다. 생체 어류 6개는 쿠팡 공식 생체동물 온라인 판매 제한 근거에 따라 상품을 붙이지 않고 `RestrictedOrSensitive`로 닫았다. 이 배치는 131행이며 열네 반복 배치는 합계 1,429행이다.

열다섯 번째 반복 배치 `chapter04-dairy-daily-01`은 P0 생활 밀착 우선순위에 따라 `040110`, `040120`, `040140`, `040150`, `040320`, `040510`, `040520`, `040610`, `040630`, `040690`, `040721`, `040900`과 HSK10 자식 23개를 담는다. 공개 색인 후보 9건을 `PendingHumanReview`로 남겼고, 유지방 범위 또는 데어리 스프레드 여부를 정확히 확인하지 못한 3개는 `SearchNoCandidate`로 보존했다. 이 배치는 193행이며 열다섯 반복 배치는 합계 1,622행이다.

열여섯 번째 반복 배치 `chapter07-everyday-vegetables-01`은 P0 생활 채소·뿌리류 `070190`, `070200`, `070310`, `070320`, `070410`, `070490`, `070511`, `070610`, `070690`, `070700`, `070810`, `070820`과 HSK10 자식 23개를 담는다. 품목·형태·가공·보관 상태를 확인한 공개 색인 후보 7건을 `PendingHumanReview`로 남겼고, 안정적인 상세 후보가 없는 5개는 `SearchNoCandidate`로 보존했다. 이 배치는 161행이며 열여섯 반복 배치는 합계 1,783행이다.

열일곱 번째 반복 배치 `chapter10-11-everyday-grains-milling-01`은 P0 생활 곡물·일상 제분품 HS6 12개와 HSK10 자식 24개를 담는다. 정확한 품목·형태를 확인한 공개 색인 후보 9건은 `PendingHumanReview`, 원곡 메밀과 곡물의 거친 분쇄물 3개는 `SearchNoCandidate`로 보존했다. 이 배치는 201행이며 열일곱 반복 배치는 합계 1,984행이다. 긴 배치 ID는 MySQL 사용자 잠금 64자 상한을 넘지 않도록 업무 접두사와 SHA-256 축약값으로 잠근다.

열여덟 번째 반복 배치 `chapter15-household-edible-oils-01`은 P0 가정용 식용유 HS6 10개와 HSK10 자식 19개를 담는다. 품목·형태·가공 여부가 확인된 공개 색인 후보 6건은 `PendingHumanReview`, 올리브유 세부 코드와 기타 유채유·겨자유 4개는 `SearchNoCandidate`로 보존했다. 관세청 성질별 분류는 선택 HSK10 모두를 `원자재`로 분류하므로 소매 관찰과 분리해 유지한다. 이 배치는 166행이며 열여덟 반복 배치는 합계 2,150행이다.

열아홉 번째 반복 배치 `chapter17-household-sugars-sweeteners-01`은 P0 가정용 설탕·감미료·당과류 HS6 12개와 HSK10 자식 25개를 담는다. 공개 색인 후보 6건은 5개 HS6에 `PendingHumanReview`로 남기고, 원당·향미/착색 자당·세부 과당 비율·전화당 등 7개 코드는 `SearchNoCandidate`로 보존했다. 이 배치는 198행이며 열아홉 반복 배치는 합계 2,348행이다.

스무 번째 반복 배치 `chapter16-everyday-prepared-meat-seafood-01`은 P0 일상 가공 육류·수산 식품 HS6 12개와 HSK10 자식 31개를 담는다. 품목·가공·보관 형태를 확인한 공개 색인 후보 5건은 `PendingHumanReview`, 법정 품목 경계를 정확히 확인하지 못한 7개는 `SearchNoCandidate`로 보존했다. 이 배치는 193행이며 스무 반복 배치는 합계 2,541행이다.

스물한 번째 반복 배치 `chapter19-everyday-pasta-bakery-01`은 P0 일상 면·곡물·빵류 HS6 8개와 HSK10 자식 27개를 담는다. 공개 색인 상세에서 품목과 형태를 확인한 후보 7건은 `PendingHumanReview`, 직접 상품 상세 근거가 없는 스위트 비스킷 1개는 `SearchNoCandidate`로 보존했다. 이 배치는 135행이며 스물한 반복 배치는 합계 2,676행이다.

스물두 번째 반복 배치 `chapter19-remaining-everyday-preparations-01`은 제19류의 남은 HS6 9개와 HSK10 자식 22개를 담는다. 영유아 오트밀·계란 파스타·속 채운 파스타·쿠스쿠스·크리스프브레드 공개 색인 후보 5건은 `PendingHumanReview`, 정확한 상세 근거가 부족한 나머지 4개는 `SearchNoCandidate`로 보존했다. 이 배치는 149행이며 스물두 반복 배치는 합계 2,825행이다.

스물세 번째 반복 배치 `chapter20-everyday-preserved-vegetable-fruit-01`은 제20류의 일상 보존 채소·과일 조제품 HS6 12개와 HSK10 자식 18개를 담는다. 공개 색인 후보 7건은 `PendingHumanReview`, 정확한 상세 근거가 부족한 5개는 `SearchNoCandidate`로 보존했다. 이 배치는 199행이며 스물세 반복 배치는 합계 3,024행이다.

스물네 번째 반복 배치 `chapter20-everyday-canned-fruit-vegetable-02`는 제20류 통조림 과일·가정용 조제 채소 HS6 12개를 담는다. 체리·황도·후르츠칵테일 공개 색인 후보 3건은 `PendingHumanReview`, 정확한 상세 근거가 부족한 9개는 `SearchNoCandidate`로 보존했다. 이 배치는 195행이며 스물네 반복 배치는 합계 3,219행이다.

스물다섯 번째 반복 배치 `chapter20-everyday-fruit-juices-03`은 제20류 일상 과실·채소 주스 HS6 12개를 담는다. 냉장 오렌지주스 공개 색인 후보 1건은 `PendingHumanReview`, 브릭스·농축도나 세부 품목 근거가 부족한 11개는 `SearchNoCandidate`로 보존했다. 이 배치는 193행이며 스물다섯 반복 배치는 합계 3,412행이다.

스물여섯 번째 반복 배치 `chapter20-remaining-fruit-nut-preparations-04`는 제20류 남은 일상 견과·과실 조제품과 사과·혼합 주스 HS6 10개를 담는다. 땅콩버터·건크랜베리·냉장 사과주스 공개 색인 후보 3건은 `PendingHumanReview`, 정확한 품목·브릭스·농축도 근거가 부족한 7개는 `SearchNoCandidate`로 보존했다. 이 배치는 155행이며 스물여섯 반복 배치는 합계 3,567행이다.

스물일곱 번째 반복 배치 `chapter21-household-sauces-seasonings-soups-01`은 제21류 가정용 소스·조미료·수프·일상 조제품 HS6 8개를 담는다. 커피믹스·베이킹파우더·간장·케첩·겨자·혼합조미료·크림스프·아이스밀크 공개 색인 후보 8건을 `PendingHumanReview`로 보존했다. 이 배치는 136행이며 스물일곱 반복 배치는 합계 3,703행이다.

스물여덟 번째 반복 배치 `chapter21-22-household-coffee-tea-water-beverages-01`은 제21·22류 가정용 커피·차 조제품과 물·비알코올 음료 HS6 8개를 담는다. 공개 색인 상세 후보 5건은 `PendingHumanReview`, 세번 결속에 충분한 상세 근거가 없는 3개는 `SearchNoCandidate`로 보존했다. 이 배치는 133행이며 스물여덟 반복 배치는 합계 3,836행이다.

스물아홉 번째 반복 배치 `chapter08-everyday-fruit-01`은 제8류 일상 과실 HS6 11개를 담는다. 공개 색인 상세 후보 10건은 `PendingHumanReview`, 정확한 오렌지 상품 상세 근거가 없는 1개는 `SearchNoCandidate`로 보존했다. 이 배치는 166행이며 스물아홉 반복 배치는 합계 4,002행이다.

서른 번째 반복 배치 `chapter08-remaining-everyday-fruit-02`는 제8류 남은 일상 과실 HS6 9개를 담는다. 공개 색인 상세 후보 8건은 `PendingHumanReview`, 신선 품목 상세 근거가 없는 라즈베리 1개는 냉동 후보와 혼동하지 않고 `SearchNoCandidate`로 보존했다. 이 배치는 132행이며 서른 반복 배치는 합계 4,134행이다.

서른한 번째 반복 배치 `chapter09-remaining-household-coffee-tea-spices-03`은 제9류에 남아 있던 가정용 커피·차·향신료 HS6 10개를 담는다. 디카페인 생두·통계피·팔각·사프란 공개 색인 후보 4건은 `PendingHumanReview`, 법정 품목·포장·가공 형태를 정확히 확인하지 못한 6개는 `SearchNoCandidate`로 보존했다. 이 배치는 150행이며 서른한 반복 배치는 합계 4,284행이다.

서른두 번째 반복 배치 `chapter03-household-fresh-chilled-fish-01`은 제3류 가정용 신선·냉장 어류 HS6 10개를 담는다. 대서양 연어 원물·생물 고등어·생물 대구 공개 색인 후보 3건은 `PendingHumanReview`, 제0302호의 정확한 품목·형태·보관 상태를 확인하지 못한 7개는 `SearchNoCandidate`로 보존했다. 이 배치는 89행이며 서른두 반복 배치는 합계 4,373행이다.

서른세 번째 반복 배치 `chapter03-household-frozen-fish-02`는 제3류 가정용 일반 냉동 어류 HS6 12개를 담는다. 러시아산 선동 냉동 동태 공개 색인 후보 1건은 `PendingHumanReview`, 정확한 제0303호 어종·형태·보관 상태를 확인하지 못한 11개는 `SearchNoCandidate`로 보존했다. 이 배치는 151행이며 서른세 반복 배치는 합계 4,524행이다.

서른네 번째 반복 배치 `chapter03-remaining-household-frozen-fish-03`은 제3류에 남아 있던 가정용 일반 냉동 어류 HS6 12개를 담는다. 참다랑어 색인 결과도 냉동 선택지의 가격·보관 상태를 함께 확정할 수 없어 후보를 만들지 않았으며, 정확한 어종·형태·보관 상태·가격 근거가 없는 12개 모두 `SearchNoCandidate`로 보존했다. 이 배치는 178행이며 서른네 반복 배치는 합계 4,702행이다.

서른다섯 번째 반복 배치 `chapter03-household-frozen-shellfish-04`는 제3류 가정용 냉동 갑각류·연체동물 HS6 12개를 담는다. 바닷가재 꼬리·킹크랩 절단·흰다리새우살·냉동 굴·가리비살·홍합살·갑오징어·자숙 문어 공개 색인 후보 8건은 `PendingHumanReview`, 정확한 품목·형태·보관 상태를 확인하지 못한 4개는 `SearchNoCandidate`로 보존했다. 이 배치는 188행이며 서른다섯 반복 배치는 합계 4,890행이다.

서른여섯 번째 반복 배치 `chapter03-household-frozen-fish-fillets-05`는 제3류 가정용 냉동 어류 필레 HS6 12개를 담는다. 틸라피아·대구·명태·연어 공개 색인 후보 4건은 `PendingHumanReview`, 정확한 품목·필레 형태·냉동 상태를 확인하지 못한 8개는 `SearchNoCandidate`로 보존했다. 이 배치는 138행이며 서른여섯 반복 배치는 합계 5,028행이다.

서른일곱 번째 반복 배치 `chapter03-remaining-household-frozen-fish-fillets-meat-06`은 제3류 남은 가정용 냉동 어류 필레·어육 HS6 8개를 담는다. 냉동 순살 고등어 필레 공개 색인 후보 1건은 `PendingHumanReview`, 정확한 품목·형태·냉동 상태를 확인하지 못한 7개는 `SearchNoCandidate`로 보존했다. 이 배치는 95행이며 서른일곱 반복 배치는 합계 5,123행이다.

서른여덟 번째 반복 배치 `chapter03-household-dried-salted-smoked-fish-07`은 제3류 가정용 건조·염장·훈제 어류 HS6 8개를 담는다. 훈제 연어·건조 명태·건조 멸치 공개 색인 후보 3건은 `PendingHumanReview`, 정확한 가공·보관 상태를 확인하지 못한 5개는 `SearchNoCandidate`로 보존했다. 이 배치는 101행이며 서른여덟 반복 배치는 합계 5,224행이다.

서른아홉 번째 반복 배치 `chapter03-remaining-household-dried-salted-smoked-fish-08`은 제3류 남은 가정용 건조·염장·훈제 어류 HS6 10개를 담는다. 건조 굴비·멸치젓 공개 색인 후보 2건은 `PendingHumanReview`, 정확한 품목·가공·보관 상태를 확인하지 못한 8개는 `SearchNoCandidate`로 보존했다. 이 배치는 132행이며 서른아홉 반복 배치는 합계 5,356행이다.

마흔 번째 반복 배치 `chapter12-household-edible-nuts-seeds-01`은 제12류 가정용 식용 견과·씨앗과 가루 HS6 9개를 담는다. 생알땅콩·생 해바라기씨·생 참깨 공개 색인 후보 3건은 `PendingHumanReview`, 정확한 품목·비가공 상태를 확인하지 못한 6개는 `SearchNoCandidate`로 보존했다. 이 배치는 147행이며 마흔 반복 배치는 합계 5,503행이다.

마흔한 번째 반복 배치 `chapter12-household-culinary-seeds-plants-02`는 제12류 가정용 조리 씨앗·식용 식물 재료 HS6 8개와 HSK10 자식 49개를 담는다. 건조 미역 공개 색인 후보 1건은 `PendingHumanReview`, 정확한 원물·소매 형태를 확인하지 못한 7개는 `SearchNoCandidate`로 보존했다. UN Comtrade의 겨자씨·양귀비씨 세계 합계 순중량 결측은 0으로 바꾸지 않는다. 이 배치는 117행이며 마흔한 반복 배치는 합계 5,620행이다.

마흔두 번째 반복 배치 `chapter33-34-household-hygiene-cleaning-01`은 제33·34류 가정용 개인위생·세정 제품 HS6 10개와 HSK10 자식 19개를 담는다. 샴푸·치약·분말 세탁세제 공개 색인 후보 3건은 `PendingHumanReview`, 정확한 품목 결속을 확인하지 못한 7개는 `SearchNoCandidate`로 보존했다. 이 배치는 163행이며 마흔두 반복 배치는 합계 5,783행이다.

마흔세 번째 반복 배치 `chapter33-household-personal-care-02`는 제33류 가정용 개인관리 제품 HS6 9개와 HSK10 자식 19개를 담는다. 향수·립스틱·페이셜크림 후보 3건은 `PendingHumanReview`, 정확한 품목 결속을 확인하지 못한 6개는 `SearchNoCandidate`로 보존했다. 이 배치는 147행이며 마흔세 반복 배치는 합계 5,930행이다.

마흔네 번째 반복 배치 `chapter33-34-household-deodorizing-maintenance-03`은 제33·34류 가정용 실내 방향·생활 관리 제품 HS6 8개와 HSK10 자식 10개를 담는다. 실내 방향제·가구 광택제·양초 후보 3건은 `PendingHumanReview`, 정확한 상품 결속을 확인하지 못한 5개는 `SearchNoCandidate`로 보존했다. 관세청 성질 분류는 소비재 3개·원자재 7개지만 개별 소매 적합성을 확정하지 않는다. 이 배치는 131행이며 마흔네 반복 배치는 합계 6,061행이다.

마흔다섯 번째 반복 배치 `chapter48-household-paper-hygiene-01`은 제48류 가정용 종이 위생·생활재 HS6 8개와 HSK10 자식 10개를 담는다. 화장지·미용티슈·종이접시 후보 3건은 `PendingHumanReview`, 정확한 상품 결속을 확인하지 못한 5개는 `SearchNoCandidate`로 보존했다. 관세청 성질 분류는 소비재 2개·원자재 8개이며 개별 소매 적합성을 확정하지 않는다. 이 배치는 131행이며 마흔다섯 반복 배치는 합계 6,192행이다.

마흔여섯 번째 반복 배치 `chapter48-household-paper-stationery-02`는 제48류 가정용 종이 문구·정리용품 HS6 8개와 HSK10 자식 8개를 담는다. 봉투·스프링 노트·D링 바인더 후보 3건은 `PendingHumanReview`, 정확한 상품 결속을 확인하지 못한 5개는 `SearchNoCandidate`로 보존했다. 관세청 성질 분류는 모두 소비재지만 개별 소매 적합성을 확정하지 않는다. 이 배치는 131행이며 마흔여섯 반복 배치는 합계 6,323행이다.

마흔일곱 번째 반복 배치 `chapter61-everyday-knitted-tops-01`은 제61류 일상 편물 상의 HS6 8개와 HSK10 자식 18개를 담는다. 면 100% 반팔 티셔츠 후보 1건은 `PendingHumanReview`, 정확한 품목·섬유·편물 여부를 확인하지 못한 7개는 `SearchNoCandidate`로 보존했다. 관세청 성질 분류는 모두 소비재지만 개별 소매 적합성을 확정하지 않는다. 이 배치는 129행이며 마흔일곱 반복 배치는 합계 6,452행이다.

마흔여덟 번째 반복 배치 `chapter61-everyday-underwear-nightwear-02`는 제61류 일상 편물 속옷·잠옷 HS6 12개와 HSK10 자식 16개를 담는다. 남성 면 팬티·남성 면 파자마·여성 면 팬티 후보 3건은 `PendingHumanReview`, 정확한 품목·섬유·편물 여부를 확인하지 못한 9개는 `SearchNoCandidate`로 보존했다. 관세청 성질 분류는 모두 소비재지만 개별 소매 적합성을 확정하지 않는다. 이 배치는 195행이며 마흔여덟 반복 배치는 합계 6,647행이다.

마흔아홉 번째 후보 배치 `chapter61-everyday-hosiery-03`은 제61류 일상 양말·스타킹 HS6 8개와 HSK10 자식 8개를 담는다. 합성섬유 팬티스타킹·면 기본 양말 후보 2건은 `PendingHumanReview`, 정확한 품목·섬유·단사 굵기 근거가 부족한 6개는 `SearchNoCandidate`로 보존했다. 자체시험 12/12·정규화 130행은 통과했지만 로컬 MySQL compose 불일치로 저장·재조회하지 못했으므로 저장 완료 반복 배치는 48개·6,647행을 유지한다.

## 조사 우선순위

전수 대장의 완전성과 별개로 상세 상품 조사는 [`review-priority-policy.v1.json`](review-priority-policy.v1.json)의 `P0 식품·필수 생필품 → P1 위생·의류·주거 생활재 → P2 일반 소매·지역 운영재 → P3 산업재·특수 품목` 순서를 따른다. 이 순서는 실제 인기·검색량·판매량 주장이 아니라 관세청 소비재 분류, 일상 사용성, 디오라마 관찰 가치와 수입액 보조 정렬을 이용한 조사 효용 순서다. 같은 우선순위에서는 생활 상품군별 8~12개를 묶고, 생체·희귀 종·산업 원료는 뒤로 보낸다.

첫 우선 묶음 우유·요구르트·버터·치즈·식용란·꿀 12개부터 P0 생활 식품군과 P1 제33·34류 생활 관리 제품, 제48류 종이 생활재, 제61류 일상 편물 상의·속옷·잠옷까지 생활 상품군 중심 배치를 완료했다. 산업 원료와 전문 용도는 후순위로 보낸다. 제61류 일상 양말류는 파일 생성과 자체시험까지 마쳤으며 DB 저장 검증 후 제62류 일상 직물 셔츠·블라우스·바지를 검토한다.

## HS6 전수 조사 대장

WCO HS 2022 법정 HS6 `5,612개`와 UN Comtrade 통계 특수코드 `999999`를 분리하면서, 모든 UN H6 leaf에 공식 분류 행과 소매 조사 상태 행을 하나씩 둔다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-census-acquire .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-census-self-test .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-census-apply .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-census-verify .
```

현재 전수 대장은 공식 분류 `5,613행`과 조사 대기열 `5,613행`, 합계 `11,226행`이다. 법정 HS6 중 대표 상품 후보를 관찰한 코드는 `210개`, 공식 정책으로 제한 판정을 마친 코드는 `40개`, 채널 부적합 판정은 `8개`, 검색 무결과는 `221개`, 대기는 `5,133개`이며 `999999`는 소매 조사 부적합 특수행이다. 관세청 HSK10 성질 분류 `11,327행`은 이 HS6 대기열과 단위가 다른 보조 분류다. 원본은 `artifacts/local/public-data/trade-retail/census/hs-2022-h6`에 비공개로 둔다.

## 관세청 HS 계층·성질별 전수 자료

관세청 2026 HS 부호 총대장 `12,469행`과 말단 HSK10의 2026 성질별 분류 `11,327행`을 별도 계보로 저장한다. 총대장은 말단 10단위 11,327행과 7~9단위 중간 계층 1,142행을 구분한다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-acquire .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-self-test .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-apply .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-verify .
```

합계 `23,796행`을 기존 MySQL 원장에 저장했고 독립 재조회와 동일 입력 신규·수정 0행을 확인했다. 성질별 대분류는 소비재 3,261·원자재 5,183·자본재 2,883이다. 국내 특수 `2424000000`은 WCO 법정 HS6가 아니며, 성질 분류는 개별 상품의 법적 세번이나 쿠팡 판매 적합성을 확정하지 않는다. 원본은 `artifacts/local/public-data/trade-retail/census/kcs-2026`에 비공개로 둔다.

## 실행

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-acquire .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-self-test .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-preview .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-apply .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- trade-retail-verify .
```

원본은 `artifacts/local/public-data/trade-retail/kr-imports-2025-h6-r1`에 비공개로 보존한다. MySQL은 기존 `public_data_ingestion_runs`, `public_data_raw_snapshots`, `public_data_normalized_records`를 재사용한다.

## 동결 결과

- 원본 응답: 131행
- 저장 통계: 36행 — HS6 3개 × 세계 합계·상대국 상위 5개 × 금액·순중량
- HS 분류: 3행
- 상품 관찰: 6행
- 총 정규화 자료: 45행
- 배포 승인: 모두 `false` 또는 비공개 검토 상태
