# Simulation World 파생 관계 DB

## 목적

운영 서버와 Simulation 서버는 같은 공공데이터 DB를 사실 자료로 읽는다. Simulation 서버는 공공데이터를 Unity 공간 구조로 바꾸는 공간 실행과, 그 불변 공간 실행을 입력으로 받는 Synty 경관 실행을 서로 독립된 원장과 fingerprint로 축적한다.

```text
공식 원본
  → 공공데이터 수집기
  → 공유 공공데이터 DB
       ├─ 운영 서버: 필요한 관점별 조회 결과로 읽음
       └─ Simulation 서버: SELECT 전용으로 읽음
             → 공간·통계·시나리오 규칙 적용
             → 공간 실행 SchemaVersion 2
                  ├─ 원본 계보와 공간 출력 SHA-256
                  ├─ Area·건물·사업장·Tile 파생 node
                  ├─ 포함·인접·연결·역할 관계
                  └─ Unity 좌표·타일·Terrain·Mask·배치 기준점
                              │ 읽기 전용 입력
                              ▼
                       Synty 경관 Job Shell
                  ├─ Synty·URP 구성 대장 개정
                  ├─ 그래픽 표현 계획
                  ├─ VisualKey 시각 배치 계획
                  └─ 배치 보류·거부 기록
```

공유 공공데이터 DB는 공식 자료와 정규화 결과의 권위다. 파생 관계 DB는 동일 원본·규칙·seed에서 다시 만들 수 있는 Simulation 해석 원장이며 운영 계약·주문·재고·배차의 권위가 아니다.

## 행정동·법정동 우선 가공

월드 API는 공공데이터 원문을 요청마다 직접 결합하지 않는다. 공간 파생 실행이 먼저 건축물–법정동 Assignment, 법정동–행정동 관할 관계와 행정동별 건물 분류 집계를 읽어 다음 지역 Projection을 만든다.

```text
공유 공공데이터 DB
├─ 건축물–법정동·행정동 Assignment
├─ 법정동–행정동 기준시점 관계
└─ 행정동별 건물 Category 집계
   ↓ 공간 파생 실행
Simulation World 파생 관계 DB
├─ LegalRegion / AdministrativeRegion node
├─ LegalAdministrativeRegionCrosswalk
├─ LocatedInLegalRegion / AggregatedInAdministrativeRegion
└─ HasBuildingCategoryAggregate
   ↓ 읽기 전용 지역 Projection
GET /api/simulation/v1/world-stream/regions/{regionStableId}
   ↓ 후속 타일 조립기
Tile·Area의 건물 표현 후보와 VisualKey 입력
```

법정동과 행정동은 별도 고유 식별자로 유지한다. 파생 실행의 입력 fingerprint와 출력 hash가 같으면 같은 지역 Projection을 재사용한다. 현재 뼈대는 기존 `SchemaVersion 2` 실행·node·relation 표를 재사용하므로 별도 DB migration이 필요하지 않다.

행정구역 경계 geometry가 파생 실행에 연결되기 전에는 지역을 임의 타일에 배정하지 않는다. 이 경우 API는 건물 분류 집계와 교차 관계는 반환하되 `WaitingForRegionGeometry`와 빈 `TileKeys`를 반환한다. 지역 Projection은 Unity 표현용 읽기 결과이며 운영 시설 상태나 실제 업무 완료를 뜻하지 않는다.

## DB 책임

| DB | 쓰기 책임 | 읽기 책임 | 포함하지 않는 것 |
| --- | --- | --- | --- |
| 운영 업무 DB | 운영 서버 Command·UseCase | 권한 있는 운영 API | Simulation 결과의 자동 승격 |
| 공유 공공데이터 DB | 운영 공공데이터 수집기와 migration | 운영 서버, SELECT 전용 Simulation 연결 | 가상 재고·시나리오 결과·Prefab 경로 |
| Simulation World 파생 관계 DB | Simulation 파생 생성기 | Simulation Runtime, 검증·관리 관점별 조회 결과 | 운영 업무 확정, 개인 연락처, Synty 원본 자산 |
| Simulation Session DB | Simulation 저장 자료 adapter | 저장 식별자 기반 복원과 검증 | 공공데이터 원본, 공간 파생 사실, 운영 업무 상태 |

Simulation 서버의 공유 공공데이터 연결은 `AgriculturalFisheriesDbContext`와 `PublicDataIngestionDbContext`를 모두 읽기 전용으로 등록한다. `SaveChanges`는 차단하며 수집기·migration·초기화 작업은 Simulation host에서 실행하지 않는다.

Simulation Session DB에는 현재 실행 중 aggregate를 매 Command마다 투영하지 않는다. 사용자가 명시적으로 만든 `simulation-save.v1` 저장 자료와 재생 hash를 보존하고, 재시작 뒤 Command를 재생해 활성 Session을 복원한다. 역할 카드 장착·활동처럼 Session에 속한 가변 상태도 이 저장 자료에 포함되지만 World 파생 관계 DB에는 복제하지 않는다.

## 파생 실행본

`SimulationWorld파생원장`의 `SchemaVersion 2`는 한 번의 재현 가능한 공간 생성 결과다. 기존 `SchemaVersion 1` 결합 실행본은 읽기 호환 대상으로 보존한다.

```text
BuildStableId
AreaSetStableId
RecipeRevision
RuleRevision
Seed
InputFingerprintSha256
GeneratedAtUtc
Sources[]
Nodes[]
Relations[]
BuildingPlacements[]
OutputHashSha256
```

- `Sources`: 원본 DB 코드, 자료 코드, 원본 개정 번호, SHA-256과 기준 시각
- `Nodes`: Tile·Area·법정동·건물·공개 사업장·도로 회랑처럼 파생 관계의 대상
- `Relations`: 포함·인접·접근·연결·시나리오 역할과 `Observed / Derived / StatisticallyAllocated / Scenario / Decorative` 근거
- `BuildingPlacements`: 영역에서 표현할 건물, 관측 도형·관측 대표점·영역 구성·시나리오 배치 근거, 건물 분류·표현 층수·시각 Family와 위치
- 공간 실행의 fingerprint에는 Synty 구성 대장, URP 표현 대장, 그래픽 계획과 `VisualKey`가 포함되지 않는다.

첫 수직 구현에서는 `completion-area:sim:pyeongchang:daegwallyeong-farm.v1`을 `LandscapeCompletionArea` node로 저장한다. 이 node는 대관령 Farm Area에 포함되고, 1km×1km 범위를 이루는 L2 500m 타일 네 개를 `ContainsSpatialTile` 관계로 참조한다. 전체 평창군 Manifest에 대상 네 타일이 있으면 공간 실행은 이 네 타일과 필요한 L1·L0 상위 타일만 선택한다. 이는 DB schema를 늘리는 기능이 아니라 기존 SchemaVersion 2의 node·relation·Unity 타일 Manifest를 좁은 완결 범위로 사용하는 규칙 개정이다.

별도 `SimulationWorldSynty경관실행원장`은 다음 값을 가진다.

```text
VisualBuildStableId
JobStableId
SpatialBuildStableId
SpatialOutputHashSha256
ScopeKindCode + ScopeStableId
LandscapeRuleRevision
VisualCatalogRevision
UrpProfileCatalogRevision
Seed + TargetPlatformCode + QualityTierCode
GraphicsPlans[]
VisualPlacements[]
Rejections[]
InputFingerprintSha256 + OutputHashSha256
```

`VisualKey`에는 `Assets/...` 같은 Prefab 경로를 저장할 수 없다. 실제 Synty 원본 Prefab은 Unity의 구성 대장에서 해석하며 원본 `.meta` GUID와 Material을 이 DB가 소유하지 않는다.

## 멱등성과 재생성

같은 `BuildStableId`, 입력 fingerprint와 출력 hash는 기존 실행본을 재사용한다. 같은 `BuildStableId`에 다른 입력이나 결과가 들어오면 `SimulationWorldDerivationConflict`로 거부한다.

새 공간 실행본이 필요한 조건은 다음과 같다.

- 공공데이터 원본 개정 또는 SHA-256 변경
- 좌표·공간·분류·관계 규칙 개정
- AreaSet·Recipe·seed 변경

Synty·URP 구성 대장이나 경관 규칙만 바뀌면 공간 실행을 재생성하지 않고 새 Synty 시각 실행만 만든다. Synty 입력 fingerprint는 공간 출력 SHA-256, Tile·Area·AreaSet 범위, 경관 규칙, Synty·URP 대장, seed, 플랫폼과 품질 단계를 포함한다.

파생 실행본은 수정 덮어쓰기보다 새 `BuildStableId`로 생성한다. 운영 DB나 공유 공공데이터 DB를 파생 결과로 역갱신하지 않는다.

## 첫 migration과 한글 물리 스키마

`시뮬레이션월드공간건물그래픽원장추가`는 다음 표를 만든다.

- `시뮬레이션월드_파생실행`
- `시뮬레이션월드_원본계보`
- `시뮬레이션월드_파생노드`
- `시뮬레이션월드_파생관계`
- `시뮬레이션월드_건물배치계획`
- `시뮬레이션월드_그래픽표현계획`
- `시뮬레이션월드_시각배치계획`
- `시뮬레이션월드_UI기획대장`
- `시뮬레이션월드_UI설계근거`
- `시뮬레이션월드_UI화면영역기획`
- `시뮬레이션월드_UI정보항목기획`
- `시뮬레이션월드_UI상태표현기획`
- `시뮬레이션월드_UI행동후보기획`
- `시뮬레이션월드_UI업무규칙연결`

위 그래픽·시각 배치 표는 `SchemaVersion 1` 호환을 위해 보존한다. 증분 migration `Synty경관JobShell분리`는 공간 실행의 `시각자산대장개정번호`를 선택 사항으로 바꾸고 다음 독립 표를 추가한다.

- `시뮬레이션월드_Synty경관실행`
- `시뮬레이션월드_Synty그래픽표현계획`
- `시뮬레이션월드_Synty시각배치계획`
- `시뮬레이션월드_Synty배치거부`

물리 열 이름도 `파생실행고유식별자`, `영역묶음고유식별자`, `근거종류코드`, `행정구역코드`, `시각키`, `표현전용여부`처럼 한국어 업무 의미를 먼저 사용한다. `SHA256`, `UTC`, `DB`, 좌표축 `X/Y/Z`처럼 외부 표준이나 기술 단위를 식별하는 약어는 의미 손실을 막기 위해 유지한다. EF migration 이력 표는 `__EF마이그레이션이력_시뮬레이션월드파생`으로 분리한다.

C# 속성명과 설정 키, API·JSON 계약은 기존 소비 코드와 저장 자료의 호환성을 위해 이 물리 명명 변경에 포함하지 않는다. 새 DB에 아직 적용하지 않은 첫 migration 단계에서만 물리 이름을 확정했으며, 이미 운영 중인 업무 DB와 공유 공공데이터 DB의 기존 표·열 이름은 일괄 변경하지 않는다.

## 영역·건물·공개 인허가 사업장 연결

공유 공공데이터 DB는 건축물대장 레코드와 공개 인허가 사업장 레코드를 계속 사실 원본으로 보존한다. 파생 DB는 원본 값을 복제해 또 다른 사실 원장을 만들지 않고 `원본계보고유식별자 + 원본레코드고유식별자`로 정확한 레코드를 참조한다.

```text
Area node
  ── Contains ──> Building node
                        ── HostsPublicLicensedBusiness ──> PublicLicensedBusiness node
  └─ 건물배치계획 ──> Building node
                           └─ VisualPlacement ──> VisualKey
```

- 건물 node는 건축물대장·GIS 건물도형의 원본 레코드를 참조한다.
- 공개 사업장 node는 공개 상호명·업종·업태·영업상태가 있는 지방행정 인허가 원본 레코드를 참조한다.
- 사업장–건물 관계는 정확한 주소 또는 건물도형 포함 판정의 파생 관계이며 실제 입주·소유 관계를 확정하지 않는다.
- 대표자명·전화번호·사업자등록번호·상세 호수는 World 파생 원장에 투영하지 않는다.
- `건물배치계획`은 `ObservedFootprint`, `ObservedRepresentativePoint`, `AreaComposition`, `Scenario`를 구분한다. 따라서 실제 위치 건물과 자료 부족 시 만든 대표 경관 건물을 같은 사실처럼 노출하지 않는다.
- 건물 분류·층수·바닥면적·높이와 `시각Family코드`는 Synty Base·Middle·Roof 구성의 입력이다. 실제 Prefab 경로는 저장하지 않는다.

영역 화면은 이 관계를 이용해 `관측 건물 수`, `대표 건물 수`, `건물 분류별 수`, `연결된 공개 사업장 수`, `주소 연결 미해결 수`를 별도로 보여준다. 폐업 사업장은 기록과 검증에는 남기되 기본 활성 간판이나 업무 객체로 표현하지 않는다.

## 그래픽 표현 계획

새 실행에서는 `시뮬레이션월드_Synty그래픽표현계획`이 공간·건물 사실과 분리된 Presentation 설정이다. 기존 `시뮬레이션월드_그래픽표현계획`은 결합형 `SchemaVersion 1` 호환 자료만 담는다.

| 열 | 용도 |
| --- | --- |
| `대상노드고유식별자` | Area·건물·도로·지형 등 적용 대상 |
| `표현범위코드` | 배경·건물 외관·지면·도로·수목 등 |
| `질감세트키` | 실제 Texture 파일이 아닌 Unity 구성 대장 해석 키 |
| `재질변형키` | 원본 Synty Material을 보존하는 프로젝트 소유 variant 키 |
| `색조팔레트키` | Farm·Town·Hub·City 색조 계열 |
| `배경Profile키` | 산림 능선·수목 완충대·도시 silhouette 구성 |
| `조명Profile키` | 공통 태양·환경광·Volume 설정 |
| `시간대Profile키` | 낮·Golden Hour·밤 같은 시간 표현 |
| `그림자정책코드` | `None / Blob / Realtime / Mixed / HlodBaked` |
| `그림자투사여부`·`그림자수신여부` | 대상별 shadow caster/receiver 정책 |
| `접지그림자강도`·`그림자거리미터` | 건물 접지감과 카메라 거리 예산 |
| `주변광차폐강도` | 모서리·접합부 깊이 표현 |
| `세부표현단계코드`·`품질단계코드` | L0/L1/L2 및 PC/Mobile 품질 분기 |

키에는 `Assets/...`, `.png`, `.mat` 같은 파일 경로를 넣지 않는다. Unity의 `VisualRoot → VisualKey/Catalog → Texture/Material/Volume/Renderer`가 실제 자산을 해석한다. 원본 Synty Prefab과 Material은 수정하지 않고 wrapper, 프로젝트 소유 Material variant와 `MaterialPropertyBlock`을 사용한다.

첫 평창군 기본 프로필은 다음 방향으로 둔다.

| Area | 색조·질감 | 배경 | 그림자 |
| --- | --- | --- | --- |
| 대관령면 Farm | 따뜻한 흙·초록, 밭고랑 질감 | 산림 능선·수목대 | L2 건물·차량 `Mixed`, 작물 군집 `Blob` |
| Farm–Hub 회랑 | 흙길·잔디 가장자리 | 드문 농가·산림 전환 | 근거리만 실시간, 원거리는 HLOD 통합 |
| 진부면 Hub | 콘크리트·주황 안전 강조 | 완충 수목·물류 silhouette | 건물·Van 실시간, Pallet은 수신 위주 |
| 평창읍 Town | 크림·벽돌·생활 도로 | 낮은 주택군과 상업 landmark | 건물 혼합, 작은 소품은 접지 그림자 |

모든 Area는 같은 시간대와 태양 방향을 공유한다. 그래픽 프로필 변경은 법정동, 건물 원본, 인허가 사업장, Simulation 업무 상태를 변경하지 않는다.

설정은 `SimulationWorldDerivationDatabase:Enabled`와 `ConnectionStrings:SimulationWorldDerived`를 사용한다. 기본값은 비활성이며 migration 자동 적용은 하지 않는다. 별도 DB와 최소 권한 계정을 준비한 환경에서 명시적으로 적용한다.

## 다음 세로 단위

첫 실제 생성기는 평창군 `pyeongchang-farm-hub-town-v1`을 대상으로 한다. 공간 실행과 Synty 시각 실행은 다음처럼 나뉜다.

1. 원본 개정과 SHA-256을 `Sources`에 봉인
2. Area·건물·공개 사업장·회랑 node와 원본 레코드 참조 생성
3. 공간 포함과 정확한 주소 연결을 영역–건물–사업장 관계로 변환
4. 관측 도형·대표점·영역 구성·시나리오 근거별 건물 배치 계획 생성
5. Unity 좌표·타일·Terrain·Mask·배치 기준점 산출
6. 목표·후보·할당·미해결 면적과 공간 출력 hash 저장
7. 별도 Synty Job Shell이 공간 실행 ID와 출력 hash를 검증
8. 경관·Synty·URP 대장으로 그래픽 계획, VisualKey 배치와 거부 사유 저장

실제 공공데이터가 0건이면 빈 관계를 성공 결과로 꾸미지 않고 `자료 부족` node와 실행 결과를 저장한다.

## 공공데이터에서 공간 DB로 가는 적재 Pipeline

`평창군공간파생Pipeline`은 다음 절차를 하나의 멱등 실행으로 묶는다.

```text
공유 공공데이터 DB (SELECT 전용)
→ 평창군 51760 현행 건축물 추출
→ 주용도 분류·형태 Profile·시각 Family 조회
→ 공개 인허가 사업장–건물 연결 조회
→ 원본 레코드 정렬·입력 SHA-256 계산
→ 대관령 Farm·진부 Hub·평창읍 Town Area node 구성
→ Area–건물–공개 사업장 관계 생성
→ 좌표가 확인된 건물만 배치 대상으로 제한
→ 원장 검증·출력 SHA-256 계산
→ SchemaVersion 2 공간 실행본 저장
```

공유 공공데이터 연결에는 읽기 전용 `PublicDataIngestionDbContext`를 사용하고 파생 DB만 쓰기 가능하다. 건물도형 또는 좌표가 없는 건물은 `(0,0)`이나 임의 seed 위치에 놓지 않으며 `미배치건축물수`로 보고한다. 원본이 전혀 없으면 `DataGap` node와 `InsufficientSourceData` 실행 결과를 저장한다. 같은 입력 SHA-256과 출력 hash로 다시 실행하면 기존 실행본을 반환하고 새 행을 만들지 않는다.

### 건물 종류별 1개 대표 공간 표현 Projection

공유 공공데이터 DB의 평창군 건축물 37,383건은 삭제하거나 줄이지 않는다. Unity 공간 실행에는 화면 복잡도와 후속 규칙 편집 범위를 최소화하기 위해 건물 용도 Category마다 대표 건축물 node를 하나만 투영한다.

각 건물 용도 Category에서 이름·면적·층수·높이·주소 자료가 상대적으로 충실한 건물을 먼저 뽑고 같은 조건이면 고정 seed hash로 결정한다. 같은 원본과 규칙에서는 입력 순서가 달라도 같은 대표가 선택된다. 분류되지 않은 건물도 `unresolved` 종류의 대표 하나로 남긴다.

각 대표 node는 `대표군코드`, `대표원본건수`, `대표순위=1`을 가진다. 대표원본건수의 전체 합은 공유 DB의 전체 후보 건물 수와 일치한다. 따라서 대표 Prefab 하나를 실제 건물 한 채 또는 회사 한 곳의 전수 사실로 해석하지 않는다. 공개 인허가 사업장은 선택된 대표 건물과 검증된 연결이 있는 항목만 후속 표현 후보가 되며, 원본 사업장 원장은 그대로 보존한다.

### 지역 정보 축약과 LOD별 표현 요약

종류별 대표 하나만 고르는 평창 전용 단계 뒤에 전국 공용 `지역표현요약Engine`을 둔다. 이 Engine은 공공데이터 원본을 줄이지 않고 파생 node를 지역·타일·LOD별 표현 예산으로 다시 해석한다.

```text
공유 공공데이터 원본
→ Simulation World 파생 node·관계
→ 지역표현요약Profile
→ 지역표현요약실행
   ├─ 지역표현요약항목
   └─ 지역표현요약분류보고서
→ 객체 표현 결합 원장
→ Synty·URP·Unity 표현
```

기본 Profile `region-presentation-summary.v1`은 L0 8개, L1 32개, L2 120개 표현 슬롯을 사용한다. 각 LOD는 분포 대표 60%, 지역 특색 25%, 게임 맥락 15%로 나누고 한 분류가 전체 슬롯의 40%를 넘지 않게 한다. 실제 후보가 부족하면 다른 범주를 꾸며 채우지 않고 `PartialDataGap` 또는 `NoSourceCandidate`로 남긴다. L0·L1·L2의 희소 표현 최소 수는 각각 1·1·3이며 이는 Prefab 개수가 아니라 이후 시각 대장이 해석할 표현 예산이다.

요약 항목은 실제 상호명을 포함하지 않고 원본 객체 식별자, 분류, 대표 원본 수·면적, 선정 이유, 근거 수준, 의미 기반 `VisualKey`, 공개 상세 연결 여부만 가진다. 화면에서 선택되지 않은 원본은 `화면생략대표원본수`와 분류 보고서에 계속 합산한다. 따라서 요약 항목의 수와 실제 건물·회사·면적을 동일하게 해석하지 않는다.

파생 원장을 처음 저장할 때 AreaSet과 확인된 법정동·행정동별 L0~L2 요약을 함께 저장한다. 타일에 배치 가능한 객체가 있으면 타일 요약을 만들고, 타일 Manifest만 있고 객체 좌표가 없으면 빈 타일 요약과 `NoSourceCandidate`를 저장한다. 같은 Profile·원본 지문·지역·타일·LOD·seed는 같은 요약 SHA-256을 만든다. 평창 어댑터는 `region:kr:sigungu:51760`을 일반 대표 건물 Selector에 넘기며 일반 Selector와 요약 Engine에는 평창 코드가 고정되어 있지 않다.

조회 경계는 기존 `api/simulation/v1/world-stream` 아래에 둔다.

- `regions/{regionStableId}/summary?lod=L0|L1|L2`: 지역 대표 항목과 생략 집계를 반환한다.
- `tiles/{tileKey}/summary?lod=L0|L1|L2`: 좌표가 확인된 타일 대표 항목 또는 명시적인 자료 부족을 반환한다.
- `objects/{objectStableId}/public-detail`: 검증된 `HostsPublicLicensedBusiness` 관계가 있는 공개 사업장만 상호명·분류·출처·기준일을 반환한다.

일반 요약 응답에는 공개 상호명이 들어가지 않는다. 대표자 이름, 연락처, 사업자등록번호는 공개 상세 응답에도 포함하지 않는다. 기본 지역 요약은 공공데이터와 공간 규칙의 파생 결과이며 현재 Session의 게임 맥락은 별도 상황 표현 오버레이로 결합한다.

로컬·승인된 Simulation 환경에서는 다음 명령 진입점을 사용한다. 연결 문자열은 source가 아니라 환경 변수 또는 서버 측 secret으로 공급한다.

```powershell
dotnet run --project eng/Ssalddel.Simulation.Tools -- `
  --build-pyeongchang-world-derived `
  --tile-manifest=<private-pyeongchang-tile-manifest-json-path>

dotnet run --project eng/Ssalddel.Simulation.Tools -- `
  --build-pyeongchang-synty-landscape `
  --spatial-build=<앞 명령이 반환한 공간 실행 고유 식별자>
```

두 번째 명령은 공간 실행을 수정하지 않는다. `Synty경관JobShell`이 저장된 공간 출력 hash를 요청에 봉인하고 Farm·Hub·Town 그래픽 계획을 별도 Synty 실행에 저장한다. 현재 Terrain·Mask·배치 기준점 산출물이 없으면 임의 위치의 `VisualKey`를 만들지 않고 `UnitySpatialArtifactMissing` 거부 기록과 `Partial` 상태를 남긴다. 실제 Prefab·Material·Shader Graph·HLOD 결합은 후속 Unity BatchMode 작업자가 같은 시각 실행을 이어 받아야 한다.

정적 Synty 실행 이후 현재 Simulation 상태를 URP 표현으로 합성하는 경계는 [Simulation 규칙 기반 Runtime 렌더링 의도 Pipeline](SimulationRuntimeRenderingIntentPipeline.md)을 따른다. Runtime 표현은 공간·Synty 실행을 수정하지 않고 두 출력 hash를 입력으로 참조한다.

공간 node와 아직 발전 중인 Simulation 규칙을 어떤 기본 구성·동적 표현으로 해석할지는 [공간·Simulation 규칙 객체 표현 결합 원장](SimulationWorldObjectRepresentationRuleLedger.md)을 따른다. `Draft` 규칙은 축적하되 활성 해석에서는 제외하고, 공간 실행 hash·선택적 Simulation 개정·적용 규칙을 별도 불변 해석 결과로 저장한다.

시설 의미·시설 기능·업무 Simulation 규칙·객체 연결·Scenario 규칙 묶음의 전체 소속 관계는 [Simulation World 업무 규칙 집결 트리](SimulationWorldBusinessRuleTree.md)를 따른다. 파생 DB에는 규칙 코드 자체나 현재 Session 상태를 복제하지 않고 규칙 식별자·개정·Engine 키·입출력 계약과 공간 객체 연결 계보를 저장한다.

2026-08-13 로컬 검증에서는 `ssalddel_simulation_world` DB를 생성하고 관련 migration을 실제 적용했다. 공유 DB의 VWorld 평창군 건물 37,383건과 WorldCover 기반 타일 Manifest 6,629건을 새 실행본에 저장했고, 공개 사업장·사업장–건물 연결은 원본 미확보로 0건이다. 건물 SHP geometry 투영 전이므로 건물은 모두 미배치로 보존한다. 두 번째 동일 실행은 `새실행본=False`이고 같은 실행 고유 식별자와 출력 SHA-256을 재사용했다.

## 공간 축적과 Unity 변환 원장

공간 의미 원장과 Unity 산출물을 바로 합치지 않고 다음 세 표로 변환 경계를 둔다.

### `시뮬레이션월드_Unity공간변환Profile`

현실 좌표를 Unity 표현 좌표로 변환하는 버전 계약이다. `EPSG:5186`, `Easting→X / Northing→Z / Elevation→Y`, Unity 원점, 기준 표고, 수평 축척률, 높이 과장률, Unity 단위당 미터, 규칙 개정과 Profile SHA-256을 저장한다.

`Ready` 상태에는 원점과 기준 표고가 반드시 필요하다. 실제 타일 Manifest가 없을 때는 이를 추측하지 않고 원점·기준 표고가 비어 있는 `WaitingForTileManifest` 상태로 축적한다. 현재 WorldCover Manifest처럼 평면 원점은 계산할 수 있으나 DEM 기준 표고가 없으면 `InsufficientSourceData`로 보존한다. 경사·수계·배치 가능 판정은 이 Profile의 높이 과장이 아니라 원본 `PhysicalElevation`을 사용한다.

### `시뮬레이션월드_Unity타일Manifest`

L0·L1·L2 타일키, 크기, Halo, EPSG:5186 핵심 경계, 입력 fingerprint, Manifest SHA-256과 생성 상태를 저장한다. 타일 Manifest는 반드시 같은 파생 실행본의 공간 변환 Profile을 참조한다.

### `시뮬레이션월드_Unity산출물`

Terrain Mesh·토지피복 Mask·수계 Mask·건물 배치 Manifest·그래픽 Manifest·HLOD 같은 게임용 산출물을 축적한다. 대용량 파일 자체가 아니라 private object storage의 객체 키, SHA-256, 정점·삼각형·재질 슬롯·예상 Draw Call, 인접 타일 경계 정점 hash와 상태를 저장한다.

산출물 상태는 `Pending / Completed / InsufficientSourceData / Failed / PerformanceBudgetExceeded`다. `Completed`에는 보관 객체 키와 SHA-256이 필수다. 변환 규칙·원본·타일 fingerprint가 같으면 기존 산출물을 재사용하고, 달라진 의존 Layer와 HLOD만 다시 만든다.

2026-08-13에는 증분 migration `Unity공간변환타일산출물원장추가`를 로컬 공간 DB에 실제 적용했다. WorldCover 기반 평창군 Manifest에서 L0 42건·L1 437건·L2 6,150건, 합계 6,629건을 저장했다. 변환 Profile 1건은 기준 표고가 없어 `InsufficientSourceData`, Unity 산출물은 0건이다. 실행 `world-build:pyeongchang:204d2ff358c587c3`의 출력 SHA-256은 `4d70e9658d7013aec391d11f8066991d259d4a15383fb280f77270a62b9aebde`이며 동일 입력 재실행은 새 행을 만들지 않았다.
