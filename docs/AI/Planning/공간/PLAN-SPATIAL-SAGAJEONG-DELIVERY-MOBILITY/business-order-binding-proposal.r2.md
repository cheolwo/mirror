# [기획 · 자료·월드·음식배달 · PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY · 승인 r2]

상태: `Approved / PrivateDirectoryAndSyntheticLoopImplemented / E1E3AutomatedChecksPassed / FormalEvidenceE0 / RealBusinessNamesPrivateReviewOnly`

## 제안 결론

사가정역 1km 디오라마에 실제 공개 사업장 관측을 배경·선택 정보로 표시하고, 그중 별도로 승인된 음식점만 합성 주문 시나리오와 결속하는 것은 가능하다. 현재 부족한 것은 음식 주문 상태 기계가 아니라 `공개 사업장 관측 → 건물 단일 후보 → Entrance/CurbStop → 검토된 이동망 → 합성 음식점 역할`의 결속과 승인 관문이다.

공개자료에 상호가 있다는 사실만으로 해당 업체가 게임 주문·광고·후원에 참여한다고 표현하지 않는다. 공개 사실 표시, 업체 Claim, 광고 캠페인, 합성 gameplay는 각각 다른 원장과 기능 플래그를 유지한다.

## 2026-09-13 승인 범위

사용자는 실제 음식점 자료를 로컬 DB에 상호 원문·출처·판본·공간 연결 후보·검토 상태와 함께 정리해 이후 이름을 그대로 사용할 수 있게 준비하되, 현재 첫 주문 폐루프는 가상 명칭 음식점 3곳으로 구현하는 안을 승인했다.

- 실제 상호는 `PendingHumanReview`, `distributionApproved=false`, `orderScenarioEligible=false`로 저장한다.
- 가상 음식점 3곳은 실제 사업장 관측·Claim·광고와 연결하지 않고 `SyntheticFixture`로 명시한다.
- 기존 음식 주문 상태 전이와 수령 확인·Save/Replay를 재사용하며, 사가정 차로·신호·골목 상태 사본은 주문 상태를 변경하지 않는다.
- 이번 구현 상한은 자동 시험으로 확인하는 `Logic E3`다. 실제 Entrance/CurbStop·통행 승인·저장 Scene·플레이어 입력·새 Game View 증거는 후속 관문이다.

## 2026-09-13 읽기 전용 재조사

이번 조사는 기존 동결 사본만 읽었으며 새 다운로드·DB 쓰기·Unity 적용은 수행하지 않았다. 사가정 지도 `sagajeong-reference.r3`의 역 기준점 `(550, 8)`, 반경 500m 정사각형을 사용했다.

| 항목 | 결과 | 해석 |
| --- | ---: | --- |
| 1km 안 상가 관측 | 1,987 | 소상공인시장진흥공단 2026-06-30 판본의 위치 관측 |
| 음식 업종 관측 | 599 | 상호 587종, 현재 영업·배달 가능 확정 아님 |
| 중랑구 음식점 원장과 상호+도로명주소 일치 | 334 | 서로 다른 판본의 교집합이며 동일 업체 자동 병합 아님 |
| 현재 602건물 지도에 단일 주소 후보로 연결된 음식 관측 | 180 / 115건물 | 입주·출입구 확정 아님 |
| 두 원천 일치와 단일 건물 후보를 모두 충족 | 93 / 74건물 | 첫 사람 검토 후보군 |
| 위 후보 중 역 250m 안 | 79 | 첫 소규모 시나리오를 고를 수 있는 밀도 |

주요 음식 세부 업종은 백반·한정식 107, 요리주점 73, 카페 62, 돼지고기 구이·찜 42, 김밥·만두·분식 38, 치킨 36, 국·탕·찌개 31, 빵·도넛 27건이다. 이 수치는 게임 수요나 실제 주문량이 아니다.

[소상공인시장진흥공단 상가(상권)정보](https://www.data.go.kr/data/15083033/fileData.do)는 상호명·업종·주소·경위도를 제공하고 분기 갱신 자료다. [중랑구 음식점현황](https://www.data.go.kr/data/15035759/fileData.do)은 일반음식점·휴게음식점·제과점영업의 업소명·주소·업태를 제공하는 연간 자료다. 두 자료 모두 이용허락범위 제한 없음으로 표시되지만, 자료 이용조건은 업체의 게임 참여·광고 계약이나 실제 메뉴·영업시간·배달 가능성의 증거가 아니다.

[서울시 상권분석서비스](https://golmok.seoul.go.kr/introduce.do)의 점포수·추정매출·유동인구는 분기·행정동·상권 집계로 별도 분석층에 쓸 수 있다. 이를 개별 음식점의 실제 주문량이나 합성 NPC 수요로 배분하지 않는다.

## 권위와 원장 분리

```text
PublicBusinessObservation
  └─ BuildingBusinessAssignment
       ├─ PublicFactPresentation
       ├─ VerifiedLocalBusinessClaim ── LocalDioramaSponsorship
       └─ SyntheticRestaurantScenarioBinding ── FoodDeliveryJourneySnapshot
```

1. `PublicBusinessObservation`은 상호·업종·공개 주소·좌표·원천 판본·hash·기준일만 소유한다. 대표자·전화·사업자등록번호는 Unity 투영에서 제외한다.
2. `BuildingBusinessAssignment`는 정확 주소와 단일 건물 후보, 선택적인 좌표 교차 확인, 연결 방법·신뢰도·판본을 소유한다. 복수·충돌·미연결은 `Unresolved`로 유지한다.
3. `VerifiedLocalBusinessClaim`은 실제 업체가 공개 이름·로고·메뉴·영업시간·게임 내 참여를 신청하고 운영자가 검토한 상태다. 공개자료 관측으로 자동 생성하지 않는다.
4. `SyntheticRestaurantScenarioBinding`은 게임 메뉴·조리시간·조리 슬롯·합성 재고·주문 가능 시간만 소유한다. 실제 업체 Claim이 없으면 실제 상호와 결합하지 않고 명확한 가상 명칭을 사용한다.
5. 광고·후원은 기존 Claim과 캠페인 원장을 사용하며 주문 노출 순위·배차·NPC 활동·조리시간·평판을 바꾸지 않는다.

## Unity 표현 제안

```text
RegionExperienceRoot
├─ Geography
├─ PlacesAndBusinesses
│  ├─ BusinessDensityCells
│  ├─ BuildingBusinessAnchors
│  └─ SelectedBusinessCard
├─ CommercialDisplayOverlays
├─ Mobility
│  ├─ ApprovedMobilityGraph
│  ├─ EntranceAndCurbStops
│  └─ CourierActors
├─ OperationalSnapshot
└─ Gameplay
   └─ SyntheticFoodOrderScenario
```

- 전체 1km 보기: 599개 상호 라벨을 띄우지 않고 업종 밀도와 선택 가능한 건물만 표시한다.
- 중간 확대: 화면당 표시 수를 제한하고, 검토된 건물 표식만 보여 준다. 실제 상호는 `공개자료 기준일`과 `주문 불가/참여 확인`을 함께 표시한다.
- 건물 선택: 한 건물에 여러 업체가 있으면 최대 몇 개만 우선 표시하고 나머지는 목록으로 연다. 층 정보는 원천값이 있을 때만 보여 주며 호실·출입구를 추정하지 않는다.
- 주문 추적: `ScenarioEnabled`인 합성 음식점 또는 유효한 Claim과 결속된 음식점만 픽업 표식·조리 진행·기사 경로를 만든다.
- 광고: 선택 카드와 배지에만 `광고/후원`을 표시하고 배경 건물 크기나 gameplay 결과를 바꾸지 않는다.

## 합성 주문 폐루프

```text
합성 주문자가 메뉴 선택
→ Simulation 주문 등록
→ 음식점 수락/거절
→ 조리시간·슬롯에 따라 조리
→ 기사 배정과 음식점 접근을 병행
→ 조리 완료 + 기사 도착 뒤 픽업
→ 검토된 차로·생활도로·골목 경로 이동
→ 합성 목적지 Entrance에서 전달
→ 주문자의 별도 수령 확인
→ 기사 대기 위치 귀환
```

조리시간은 상가 업종이나 공개 상호로 추정하지 않는다. 합성 시나리오는 명시 profile을 사용하고, 실제 참여 업체는 업체가 입력하고 검토된 상품·시간대 설정을 사용한다. 도착 애니메이션이나 좌표 도달만으로 픽업·전달·수령을 확정하지 않는다.

## 단계별 권고

1. **내부 관찰층**: 1,987개 상가와 599개 음식 관측을 500m 타일·LOD로 읽되 기본은 밀도 표시, 개별 상호는 비공개 검토에서만 연다.
2. **첫 12곳 검토**: 93개 교집합 중 역 250m 안의 업종이 다른 12곳을 고른다. 최신 판본·주소·건물·층·중복·상호 변경을 사람 검토하되 아직 주문 버튼은 만들지 않는다.
3. **세 경로 공간 결속**: 짧은 대로형, 생활도로형, 골목형 음식점 각 1곳과 합성 목적지의 Entrance/CurbStop, 접근 간선을 검토한다. 현재 `TraversalReady=false`인 간선은 사용하지 않는다.
4. **가상 주문 첫 공개 전 검증**: 실제 상호를 숨긴 세 합성 음식점으로 조리·신호대기·픽업·전달·수령·귀환을 같은 revision에서 Game View로 검증한다.
5. **실제 상호 참여 선택지**: 업체 Claim과 공개 동의를 받은 곳만 실제 상호·메뉴·카드와 결합한다. 후원 여부와 주문 참여 여부는 각각 켜고 끌 수 있게 한다.

## 완료 조건과 차단

- 같은 원천 판본으로 1km 선택·중복 교집합·건물 연결 결과가 결정적이다.
- 원천 기준일과 연결 신뢰도, 실제 상호의 `참여 확인/주문 불가` 상태가 화면에서 판독된다.
- 실제 상호가 합성 주문·배차·추천·광고로 자동 승격되지 않는다.
- Entrance/CurbStop 또는 검토된 경로가 없으면 `RouteUnresolved`로 멈추며 건물 중심 직선 이동을 만들지 않는다.
- 실제 고객 주소·연락처·GPS·결제·운영 주문은 이 제안 범위 밖이다.
- 실제 상호의 공개·주문 참여·광고 결속, 원격 운영 DB·Steam 게시와 저장 Scene 변경은 승인하지 않는다. 로컬 개발 DB의 비공개 검토 원장은 승인 범위다.

## 승인된 첫 구현

**실제 상호를 주문 버튼에 쓰지 않는 3개 합성 음식점 폐루프**를 구현한다. 실제 상호 후보는 비공개 자료 원장에만 보존하고, 업체 Claim과 공개 동의가 생긴 뒤 별도 승인으로 하나씩 실제 참여점으로 바꾼다.
