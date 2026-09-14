# [기획 · 자료·월드·운영표현 · PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY · 제안 r3]

상태: `ApprovedAsInput / SampleMenuDirectionConfirmed / ContinuedByObservableOperationsR4 / ExternalPlatformMenuIngestionBlocked / LifecycleGameViewProofPending`

> 2026-09-13 후속 확정: 실제 음식점 후보 12곳의 로컬 검토 아이콘에 업종 기반 샘플 메뉴를 명확한 비실제 표시와 함께 결속하고, 음식·화물·창고·마트 OS별 생명주기를 검증하는 방향은 [다중 OS 생명주기 재생 r4](../../시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/multi-os-lifecycle-playback.r4.md)가 이어받는다.

## 제안 결론

사가정역 1km 디오라마에 음식점 아이콘을 표시하고, 음식 주문·조리·배차·이동·전달·회복의 생명주기를 같은 공간에서 관찰하는 것은 가능하다. 다만 현재 저장소의 화면 증거는 `완료된 운영 결과 표식`과 `합성 배달 이동`을 각각 확인한 수준이며, 실제 음식점별 진행 중 운영 생명주기가 Game View에서 연결됐다는 증거는 아니다.

사용자가 혼자 실행하는 로컬 Game View에는 `LocalPrivateReview / 비배포 검토용` 표시를 전제로 실제 음식점 후보 일부를 먼저 띄울 수 있다. 이 모드는 Steam 공개 허용과 무관하며, 화면에 보였다는 사실로 권리·현재 영업·주문 참여가 승인되지 않는다.

첫 구현은 다음 네 층을 분리해야 한다.

1. 실제 음식점 공개 관측은 정적인 지역 사실 아이콘이다.
2. 검증된 상인 Claim은 상호·메뉴·영업 정보의 공개 권위를 가진다.
3. 광고·후원은 별도 표시 계층이며 주문·배차·검색 순위를 바꾸지 않는다.
4. 진행 중 음식배달은 서버가 만든 비식별 상태 사본이고 Unity는 읽기 전용으로 표현한다.

실제 음식점 원장을 또 하나 만들 필요는 없다. 기존 `공개인허가사업장Record`, `공개사업장건축물Assignment`, `지역사업장표시Claim`, `지역디오라마후원Campaign`과 운영용 `음식점공개프로필`·`음식점메뉴`를 재사용한다. 다만 향후 권리가 확인된 외부 메뉴를 반입하려면 운영 메뉴와 분리된 **메뉴 관측 전용 원장**이 필요하다. 관측 자료를 검토 없이 운영 `음식점메뉴`로 복사하지 않는다.

이번 r3는 조사·제안만 수행한다. 외부 서비스 수집, DB migration·쓰기, API·Unity 코드 변경, Scene 저장, Play Mode·Game View 실행은 승인하지 않는다.

## 현재 구조 감사

### 음식점과 메뉴

| 책임 | 현재 원장 | 보유 의미 | 현재 결손 |
| --- | --- | --- | --- |
| 공공 사업장 사실 | `공개인허가사업장Record` | 상호·업종·주소·영업 상태·출처 | 사가정 로컬 DB의 전용 table migration 정합성 확인 필요 |
| 건물 결속 | `공개사업장건축물Assignment` | 사업장 하나와 건물 후보의 연결 | 건물·출입구·승하차 지점 사람 검토 |
| 업체 공개 동의 | `지역사업장표시Claim` | 신청·검토·승인·철회 | 메뉴별 권리·기준일·만료 범위 |
| 광고·후원 | `지역디오라마후원Campaign` | 승인 기간·배지·카드·광고 고지 | gameplay와 독립인 화면 결속 |
| 주문 가능한 음식점 | `음식점공개프로필` | 공개명·분류·공개 주소·주문 가능 상태 | 공공 관측에서 자동 생성하면 안 됨 |
| 주문 메뉴 | `음식점메뉴` | 메뉴명·설명·가격·이미지·공개·품절 | 외부 원천·권리·관측 시각·hash 필드가 없음 |

현재 1km 조사 결과인 음식 관측 599행·원문 상호 587종은 `PendingHumanReview`, `distributionApproved=false`, `orderScenarioEligible=false`인 비공개 후보이다. 이는 아이콘 후보 수이지 현재 영업, 배달 가능, 메뉴 정확성 또는 게임 참여의 증거가 아니다. 세 가상 음식점만 `SyntheticFixture` 주문 폐루프에 사용할 수 있다.

### 운영 서버 생명주기

음식배달 서버에는 `주문대기 → 조리중 → 픽업대기 → 기사배정 → 픽업완료 → 전달완료 → 수령확인`과 `거절/취소`, 픽업 전후 기사 중단에 대한 회복 전이가 있다. 주문자·음식점·기사 API와 상태 전이 권위도 이미 존재한다.

반면 지역 장면 `GET api/v1/world/areas/{areaStableId}/scene-snapshots`의 음식배달 항목은 현재 정상 완료된 `CompletedLifecycle / ReceiptConfirmed`만 제공한다. 기본 의미 위치도 실제 음식점 건물이 아니라 영역 대표 위치이다. 따라서 진행 중 조리·배차·픽업·이동을 공용 디오라마에 보이려면 주문번호·사용자·기사·상세 주소·GPS를 제거한 새 지역 투영이 선행되어야 한다.

| 운영 묶음 | 서버 자료 | 현재 지역 장면 표현 상한 | r3 판정 |
| --- | --- | --- | --- |
| 음식배달 | 전체 상태 기계·거절·취소·중단·회복 | 수령 확인 완료 결과만 | 첫 진행 중 투영 대상 |
| 창고 | 작업·Actor 상태 | task/actor 표식 | 사가정 의미 위치 결속 뒤 후보 |
| 화물 | 운송·완료 인계 | 완료 handoff 중심 | 활성 경로 공개 계약 전 보류 |
| 마트·도심물류 | 거점·검증 자료 일부 | 지역 대표 표식 수준 | 실제 점포·거점 결속 전 보류 |
| 그 밖의 OS | 개별 업무 API·원장 | 지역 projection 없음 | 목록·진단만, 임의 표식 금지 |

Unity의 현재 해석기는 낮은 revision, TTL 만료, 자료원 부분 실패와 개인정보성 필드명을 거부하고 메모리에서만 상태를 보관한다. 그러나 현재 음식배달 Adapter 역시 완료 결과만 수용하고 실제 Scene에서 음식점 건물과 결속하는 Presenter는 없다.

## 외부 음식점·메뉴 자료 조사

조사 기준일은 2026-09-13이다.

| 출처 | 확인한 공식 경로 | 얻을 수 있는 값 | 판정 |
| --- | --- | --- | --- |
| NAVER API HUB 지역검색 | 업체·기관 검색 API | 상호, 분류, 설명, 주소, 좌표, 상세 URL | 메뉴가 없고 전수 원장·광고 결합 용도로 부적합 |
| 네이버 지도 화면 | 지도·장소 서비스 | 화면상 장소 정보 | 화면 수집·별도 DB화를 승인한 경로가 아님 |
| 배달의민족 | 입점 업주의 셀프서비스 메뉴 관리 | 업주가 관리하는 가격·이미지·옵션 | 제3자용 공개 메뉴 조회 API를 확인하지 못함 |
| 일반음식점 인허가·중랑구 음식점 | 공공데이터 | 상호·주소·업태·영업 상태 | 음식점 존재 원장에 적합, 전체 메뉴는 없음 |
| 중랑구 모범음식점 | 중랑구 Open API | 업소명·주소·업태·`주된음식` | 일부 대표 음식 보강 후보, 전수 메뉴 아님 |
| 상인 직접 제출 | Claim 뒤 CSV/JSON 또는 관리 화면 | 메뉴명·가격·옵션·이미지·영업시간 | 실제 메뉴의 우선 권위 후보 |
| 합성 자료 | 저장소 fixture | 가상 상호·메뉴·조리시간 | 첫 폐루프와 Game View 검증에 적합 |

[NAVER API HUB 지역검색 명세](https://api.ncloud-docs.com/docs/naver-api-hub-search-local)는 한 번에 1~5건을 반환하고 응답 필드도 상호·분류·주소·좌표 중심이며 메뉴명·가격·옵션·이미지를 제공하지 않는다. [NAVER API 서비스 이용약관](https://developers.naver.com/products/intro/terms/terms.md)은 네이버 지역정보를 광고 영업에 이용하거나 별도 DB로 관리하는 행위, API 결과와 광고를 함께 노출하는 행위를 금지한다. 별도 제휴·서면 허용이 없는 한 네이버 결과를 사가정 음식점/메뉴 원장이나 지역 광고 화면에 반입하지 않는다.

배달의민족은 조사 범위에서 제3자가 소비자용 가게·메뉴를 읽는 공식 공개 API를 확인하지 못했다. 공식 [배민 메뉴 관리 안내](https://ceo.baemin.com/guide/11832)는 가격·이미지 등의 메뉴 정보를 입점 업주가 셀프서비스에서 관리하는 구조를 설명한다. 이 사실은 화면 크롤링 권한을 뜻하지 않는다. 배민·네이버 화면의 메뉴, 리뷰, 평점, 이미지, 순위를 자동 수집하지 않고 제휴 또는 권리 확인을 별도 관문으로 둔다.

[중랑구 모범음식점 Open API](https://data.jungnang.go.kr/openinf/openapiview.jsp?infId=OA-10313)는 `MAIN_EDF` 주된음식과 행정동을 제공하고 공공누리 제1유형으로 안내한다. 다만 모범음식점 일부의 대표 음식이므로 전체 음식점의 현재 메뉴·가격·배달 가능 상태로 확대 해석하지 않는다.

## 권장 원장과 전용 테이블

### 중복 음식점 master를 만들지 않는다

```text
공공 사업장 관측
  └─ 건물·행정동 귀속
       ├─ 정적 음식점 아이콘
       ├─ 검증된 업체 Claim ── 공개 프로필 ── 운영 메뉴
       └─ 광고·후원 Campaign

메뉴 원천 관측
  └─ 권리·판본·사람 검토
       └─ 검증된 Claim과 일치할 때만 운영 메뉴 후보
```

전용 테이블을 만든다면 이름과 책임은 `공개음식점메뉴관측Record` 같은 **수집·검토 원장**이어야 한다. 주문 가격과 판매 상태를 확정하는 운영 `음식점메뉴`를 대신하지 않는다.

권장 필드는 다음과 같다.

- `StableId`, `PublicBusinessRecordStableId`
- `SourceId`, `SourceDatasetId`, `SourceItemId`, `SourceRevision`, `SourceHashSha256`, `RawSnapshotId`
- `ObservedAt`, `ValidFrom`, `ExpiresAt`
- 원문 `MenuName`, 선택적인 `MenuCategory`, `PriceAmount`, `CurrencyCode`, `AvailabilityCode`
- `RightsStatusCode`, `ReviewStatusCode`, `DistributionApproved`
- `ClaimStableId`, `ReviewerEvidenceRef`, `RevokedAt`
- 이미지가 있으면 본문과 별도의 `ImageRightsStatusCode`, 원본 배포 금지 여부

동일 입력은 `(SourceId, SourceDatasetId, SourceItemId, SourceRevision, row hash)`로 멱등 처리한다. 가격이 없거나 오래되면 `Unknown/Stale`로 남기며 0원이나 현재 판매로 보정하지 않는다. 리뷰·별점·검색 순위는 첫 원장 범위에서 제외한다.

## Unity 표시 계층 제안

공식 `SimulationWorldShell` 안에서 새 Scene을 만들지 않고 다음처럼 조립한다.

```text
SimulationWorldShell
└─ OperationalOsWorldRoot                 # 기존 읽기 전용 관찰 Root
   ├─ GeographyRoot                       # 지형·도로·건물
   ├─ PlacesAndBusinessesRoot             # 정적 공개 사실
   │  ├─ BusinessDensityCells             # 전체 보기 밀도
   │  ├─ RestaurantBuildingAnchors        # 검토된 건물 결속
   │  ├─ RestaurantIconPool               # 거리별 재사용 아이콘
   │  └─ SelectedRestaurantCard           # 선택 시 상호·출처·기준일
   ├─ CommercialDisplayOverlayRoot         # 광고·후원, gameplay와 독립
   ├─ OperationalLifecycleRoot             # 서버 상태 사본
   ├─ MobilityRoot                         # 기사·차량·신호·경로 표현
   └─ DiagnosticsRoot                      # 미결속·만료·자료원 장애
```

표시 밀도는 다음으로 고정하는 안을 추천한다.

- 전체 1km: 음식점 수를 모두 라벨로 펼치지 않고 업종 밀도와 소수 대표 아이콘만 보인다.
- 중간 확대: 사람 검토를 통과한 건물별 아이콘을 보여 주며 겹치면 건물 단위로 묶는다.
- 근접/선택: 상호·업태·출처·기준일·`주문 참여 확인/주문 불가`를 카드로 보여 준다.
- 미결속: 건물이나 의미 위치가 불명확한 음식점은 지도에서 숨기고 진단 집계에만 남긴다.
- 주문 진행: `SyntheticFixture` 또는 유효한 Claim으로 주문 참여가 승인된 음식점만 조리 pulse, 픽업 표식, 기사 경로를 가진다.
- 로컬 검토: `LocalPrivateReview` 아이콘에는 항상 `검토용·비배포` 상태를 판독 가능하게 표시하고 일반 사용자/Steam 빌드에서는 제외한다.

현재 표시 overlay 계약은 `SemanticPlaceStableId`·상호·업종·배지·카드는 가지지만 실제 건물 ID와 좌표 anchor가 없다. 실제 아이콘 배치 전 다음 결속 자료를 디오라마 타일 또는 별도 판본화된 공간 결속 사본에 추가해야 한다.

- `RestaurantObservationStableId`
- `SemanticPlaceStableId`, `BuildingStableId`
- `AnchorKind`: `BuildingPointOnSurface`, `VerifiedEntrance`, `VerifiedCurbStop`
- 경계·건물·결속 revision/hash, 방법, 신뢰도
- 공개·검토·배포 허용 상태

상호와 광고 카드는 `display-overlays`, 기하 anchor는 공간 타일이 소유하도록 분리하고 `SemanticPlaceStableId`로 결합한다. 건물 중심이나 가까운 도로로 임의 보정하지 않는다.

## 진행 중 운영 생명주기 투영

공용 디오라마에는 주문자 전용 lifecycle 응답이나 원시 운송 Event를 직접 보내지 않는다. 그 자료에는 실제 주문·사용자·기사·주소·GPS가 포함될 수 있다. 대신 서버가 다음 최소 필드만 가진 `FoodDeliveryLifecycle` 지역 상태 사본을 만든다.

- 합성 또는 비식별 `WorkStableId`
- `LifecycleStageCode`, `AttentionStateCode`
- 검토된 `SemanticPlaceStableId`
- `Revision`, `ObservedAt`, `ExpiresAt`, `SourceKind`
- 선택적인 `JourneyStableId`, `RouteRevision`, `ProgressBucket`
- 공개 가능한 합성 표시명 또는 Claim 표시명 참조

포함 금지 값은 실제 주문번호, 사용자/업주/기사 ID, 연락처, 상세 배송 주소, 원시 GPS, 결제값, 비공개 메뉴·메모이다. `Active`, `RecoveryPending`, `Recovered`, `Completed`, `Cancelled`를 화면 판독 상태로 쓰되 Unity가 전이를 확정하거나 서버에 Command를 보내지 않는다.

## 검증 제안

### G0. 동결 기준선

- 현재 r2의 음식 관측 599행, 실제 이름 587종, 세 가상 음식점과 다섯 합성 주문의 입력 hash를 기록한다.
- 기존 디오라마·차로·신호·골목의 revision/hash와 canonical Scene을 고정한다.
- 다른 작업트리 변경과 별도 Unity 저장소의 변경 소유자를 먼저 확인한다.

### G1. 자료·권리 관문

- 각 상호·주된음식·메뉴·이미지의 출처, 판본, 기준일, 이용조건, 철회 가능성을 행 단위로 보존한다.
- 네이버·배민 화면 수집 자료가 원장·Steam 빌드·광고 overlay에 들어가지 않는 음성 시험을 둔다.
- `distributionApproved=false` 자료는 인증된 개발자 진단과 로컬 검토 View에서만 허용하고 일반 API 응답·Steam 빌드에서는 거부한다.

### G2. DB 관문

- 기존 공공 사업장·건물 Assignment·Claim·Campaign migration을 실제 로컬 MySQL에 적용하고 독립 `DbContext`로 재조회한다.
- 메뉴 관측 원장이 필요한 합법적 원천이 생긴 뒤에만 migration을 추가한다.
- 동일 입력 재처리 시 중복 0, source/hash 불일치 시 게시 거절을 확인한다.
- 공공 관측이 운영 `음식점공개프로필`·`음식점메뉴`로 자동 승격되지 않는 시험을 둔다.

### G3. 서버 생명주기 관문

- 합성 주문 하나를 등록부터 수령확인·기사 귀환까지 통과시키고 거절, 취소, 픽업 전 중단, 픽업 후 중단·재조리·재배차를 각각 재현한다.
- 모든 상태 변경은 원장 revision과 Outbox 멱등성을 유지한다.
- 광고·아이콘 표시 활성화 여부가 주문, 조리, 배차, 신호, 퀘스트, 평판 결과를 바꾸지 않는 회귀 시험을 둔다.

### G4. 지역 projection/API 관문

- 진행 중 상태 사본은 PII denylist, TTL, tombstone, cursor/ETag, 낮은 revision 거절과 자료원 부분 실패 격리를 통과한다.
- 건물·Entrance·CurbStop이 없는 업무는 `SpatialBindingUnresolved`로 집계하고 배치하지 않는다.
- 완료 사본과 진행 중 사본의 안정 ID가 같은 업무를 중복 GameObject로 만들지 않는다.

### G5. Unity 메모리·표현 관문

- Decoder·Interpreter에서 판본/hash 불일치, TTL 만료, 취소·철회, 행정동/지역 전환 시 메모리 삭제를 시험한다.
- 아이콘은 카메라 거리별 LOD, 화면당 상한, 건물 단위 묶음, object pool 반환을 검증한다.
- 실제 관측 아이콘에는 주문 pulse나 주문 버튼이 생기지 않고, 합성/승인 참여점만 생명주기 표현을 받는다.

### G6. Play Mode·Game View 관문

첫 화면 검증은 세 가상 음식점과 주문 한 건을 대상으로 다음 연속 상태를 재생한다.

```text
주문대기 → 조리중 → 픽업대기 → 기사배정
→ 신호대기 → 픽업완료 → 대로·생활도로·골목 이동
→ 전달완료 → 수령확인 → 기사 귀환
```

다음 세 화면과 그때의 `WorkStableId`, lifecycle stage, revision, 지도·경로 hash를 함께 보존한다.

1. 전체 1km: 음식점 밀도·대표 아이콘·활성 주문 위치
2. 음식점 근접: 건물 anchor, 선택 카드, 조리/픽업 상태
3. 이동 근접: 기사·주변 차량·신호대기·골목 진입과 현재 상태

Play Mode 화면은 Unity 표현 증거이고 실제 주문 처리나 영업 상태의 증거가 아니다. 실제 로컬 서버 HTTP 연결, 저장 Scene, Steam 배포도 각각 별도 증거로 보고한다.

### G7. 실패·부하·귀환 관문

- 12개 실제 관측 아이콘과 3개 합성 음식점, 동시 합성 주문 1/5/20건에서 프레임·할당·pool 수를 비교한다.
- API 일시 실패 때 마지막 유효 상태를 TTL까지만 유지하고 이후 제거한다.
- 취소·철회·지역 전환·Play Mode 종료 뒤 아이콘·기사·카드·경로가 남지 않는다.
- 완료 뒤 같은 기사가 대기 위치로 귀환하고 다음 주문을 받을 수 있는지 확인한다.

## 추천 첫 구현 절편

1. 실제 음식점 검토 후보 12곳을 개발자 전용 `LocalPrivateReview`의 **정적 카테고리 아이콘**으로만 준비한다. 기본 화면에는 상호를 펼치지 않고 선택 카드에서 출처·기준일·`검토용·비배포·주문 불가`와 함께 표시한다. Steam 후보로 승격하려면 별도의 사람 검토와 `distributionApproved=true`가 필요하다.
2. 세 가상 음식점은 현재 합성 메뉴와 다섯 주문을 재사용하고, 그중 주문 하나를 전체 생명주기 Game View 검증 대상으로 고정한다.
3. 실제 음식점 12곳과 합성 음식점 3곳 모두 건물 anchor fixture를 가지되, 검토되지 않은 Entrance/CurbStop은 통행에 사용하지 않는다.
4. 진행 중 음식배달의 비식별 지역 사본과 Unity Presenter를 먼저 닫고, 창고·화물·마트 OS는 같은 계약이 안정된 뒤 하나씩 추가한다.
5. 외부 플랫폼 메뉴는 반입하지 않는다. 첫 실제 메뉴는 검증된 상인 Claim과 직접 제출 자료로 한 곳만 수직 완결한다.

이 절편은 화면 밀도, 실제/가상 판독, 서버 생명주기, 기사 이동, 메모리 삭제를 한 번에 검증하면서도 실제 업체가 주문에 참여하는 것으로 오인되는 위험을 제한한다.

## 완료 조건

- 실제 음식점 관측, 업체 Claim, 광고 Campaign, 운영 메뉴, 합성 시나리오가 서로 다른 권위와 상태로 유지된다.
- 실제 관측 아이콘은 검토된 건물 하나 또는 `Unresolved`에만 속하며 임의 배치되지 않는다.
- 권리·판본·기준일이 없는 메뉴와 이미지는 DB 공개 투영과 Unity 응답에 포함되지 않는다.
- 진행 중 음식배달 사본에 실제 식별자·상세 주소·GPS가 없고 서버 상태와 동일 revision을 따른다.
- 주문의 정상·거절·취소·중단·회복·귀환이 서버 시험으로 닫히며 Unity 표시가 서버 결과를 변경하지 않는다.
- 전체·음식점 근접·이동 근접 Game View와 상태/revision/hash 기록이 같은 실행에서 보존된다.
- 실제 로컬 DB 재조회, 실제 로컬 서버 HTTP, Unity Play Mode, 저장 Scene, Steam 공개 여부를 각각 분리해 보고한다.

## 제외

- 네이버·배민 화면 크롤링, 메뉴·리뷰·평점·이미지 전사
- 제휴 없는 외부 플랫폼 API 자료의 DB 영속화·광고 결합
- 실제 고객 주문·주소·GPS·연락처·결제
- 광고에 따른 추천·배차·NPC·퀘스트·평판 변화
- 모든 OS의 동시 표현, 새 공식 Scene, 자동 Steam 게시

## 확정

- 실제 음식점 아이콘과 운영 생명주기 표시는 가능하지만 정적 관측·상인 참여·광고·운영 상태를 분리한다.
- 기존 음식점·메뉴·공공사업장 원장을 재사용하고, 합법적인 외부 메뉴 원천이 생길 때만 메뉴 관측 전용 테이블을 추가한다.
- 첫 생명주기 Game View는 실제 업체가 아닌 세 가상 음식점 중 한 주문으로 검증한다.
- 네이버·배민 자료는 현재 확인된 경로로 수집·저장·광고 결합하지 않는다.

## 미정

- 첫 실제 음식점 12곳의 사람 검토 완료 여부와 공개 상호 표시 방식
- 진행 중 지역 상태 사본의 polling 주기·TTL·동시 표시 상한
- 상인 직접 제출 형식과 메뉴별 권리·철회 UI
- 첫 실제 참여 음식점 Claim과 Steam 공개 시점

## 다음 질문 하나

첫 r3 로컬 검토 화면에서 실제 음식점 후보 12곳을 어떻게 보일지 정해야 한다.

**추천 A:** 전체·중간 보기에는 카테고리 아이콘만 표시하고, 선택했을 때 상호·출처·기준일·`검토용·비배포·주문 불가`를 보여 준다. 세 가상 음식점만 주문 pulse와 생명주기를 가진다.

- B: 실제 상호를 아이콘 옆에 항상 표시한다. 지역감은 강하지만 화면 혼잡과 영업 상태 오인이 커진다.
- C: 첫 화면은 가상 음식점 3곳만 표시한다. 가장 안전하지만 실제 지역 디오라마라는 감각 검증이 늦어진다.
