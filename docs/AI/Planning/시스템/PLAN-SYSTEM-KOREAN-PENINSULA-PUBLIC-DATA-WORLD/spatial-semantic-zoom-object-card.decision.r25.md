# [기획 · 월드·공간·관찰 · 공간 의미 확대·객체 카드 · 확정 r25]

## 결정

지구본부터 지역 디오라마까지의 기본 정보 구조를 다음과 같이 확정한다.

```text
공간을 먼저 본다
  → 확대 수준에 맞는 자료층이 열린다
    → 공간 안의 거점·경로·건물·NPC·업무 객체가 나타난다
      → 객체를 선택한다
        → 같은 고유 식별자의 상세 카드를 연다
          → 근거·관계·현재 상태·가능한 다음 관찰로 이어진다
```

카드는 월드와 분리된 목록 화면의 대체물이 아니다. 사용자가 공간에서 무엇을 보고 선택했는지 설명하는 상세층이다. 확대만으로 업무 명령·NPC 상태·운행·통행·Simulation 결과를 만들지 않는다.

한반도 지형 첫 기준 자료는 다음 조합으로 확정한다.

- 표고·산지: NASA/USGS SRTM 1 Arc-Second Global.
- 주요 하천: HydroRIVERS Asia에서 한반도 범위를 절단한 선형 자료.
- 광역 해안·호수 보조: Natural Earth `1:10m` Physical.
- 평지·분지: SRTM의 표고·경사에서 생성한 `DerivedLowlandCandidate`.
- Copernicus GLO-30: 접근 권한과 승인 사본이 확인될 때 비교·후속 후보로 유지하고 첫 구현을 차단하지 않는다.

## 확대 수준별 자료 교체

한 개의 거대한 장면에 모든 자료를 켜고 끄지 않는다. 각 확대 수준은 자신의 정밀도에 맞는 별도 읽기 전용 상태 사본을 읽으며, 전환 시 인접 수준만 잠시 함께 유지한다.

| 수준 | 사용자가 보는 공간 | 기본 자료 | 선택 가능한 객체 | 상세 카드 |
| --- | --- | --- | --- | --- |
| `Z0 World` | 지구 | 해양·대륙·저해상 국가 경계 | 국가·광역 자료 가용성 | 국가 개요·출처·최대 확대 수준 |
| `Z1 EastAsia` | 동아시아 | 광역 해안·국가·주요 지형 음영 | 국가·반도·광역 교류 방향 | 지역 개요·자료 품질 |
| `Z2 PeninsulaTerrain` | 한반도 | 표고·산지·주요 하천·해안·파생 저지대 | 산지권·하천·저지대·주요 도시권 | 지형 근거·범위·파생 방식 |
| `Z2.5 PeninsulaHubs` | 한반도 거점망 | Z2 지형과 단순화한 교류축 | 부산·서울·도라산·개성·평양·신의주 등 | 거점·교류 상태·출처·결손 |
| `Z3 Region` | 시도·시군구·생활권 | 행정 경계·광역 도로·철도·하천 | 도시·역·항만·물류 거점 | 지역 통계·관계·하위 관찰 창 |
| `Z4 Neighborhood` | 행정동·역세권 | 도로·건물 윤곽·시설·집계 상태 | 건물·시설·업무 장소·집계 Actor | 주소 범위·역할·업무 요약 |
| `Z5 LocalDiorama` | 승인된 로컬 디오라마 | 검증된 배치·표현·상태 사본 | 건물·NPC·차량·작업 객체 | 현재 단계·관계·출처·허용 상호작용 |

상위 집계 객체를 확대해서 하위 객체처럼 꾸미지 않는다. 예를 들어 Z3의 `서울 음식점 12,000개` 집계를 확대해 임의 음식점 12,000개를 만들지 않고, Z4에서 별도로 검증된 시설 투영이 있을 때만 개별 객체를 표시한다.

## 공통 공간 상태 사본

서버와 자료 도구는 확대 수준마다 다음 공통 계약을 가진 상태 사본을 만든다.

```text
SpatialObservationSnapshot
├─ SnapshotStableId
├─ SchemaVersion
├─ Revision
├─ GeneratedAt / ObservedAt
├─ ObservationLevelCode
├─ FocusSpatialStableId
├─ ParentSpatialStableId
├─ Bounds
│  ├─ CoordinateReferenceSystem
│  ├─ Wgs84Envelope
│  └─ LocalProjectionOrigin
├─ VisibleLayerDescriptors[]
├─ SpatialObjects[]
├─ Relations[]
├─ SourceReceipts[]
├─ Completeness
├─ MaximumObservationLevel
└─ PresentationOnly = true
```

`VisibleLayerDescriptors`는 지형·하천·행정 경계·거점·건물·NPC·업무 상태처럼 서로 다른 자료층을 독립적으로 설명한다. 각 층은 최소한 다음을 가진다.

- `LayerStableId`, `LayerKindCode`, `Revision`.
- 표시 가능한 `MinObservationLevel`, `MaxObservationLevel`.
- 자료 기준 시각, 출처 영수증, 원본/파생 구분, 품질·결손 상태.
- 확대 수준별 단순화 판본과 hash.
- 사용자가 끌 수 있는지, 선택 가능한지, 상세 카드가 있는지.

## 공통 공간 객체

거점·건물·NPC를 완전히 다른 화면 체계로 만들지 않고 다음 공통 껍질을 사용한다.

```text
SpatialObservationObject
├─ ObjectStableId
├─ ObjectKindCode
│  ├─ SpatialArea
│  ├─ TerrainFeature
│  ├─ Hub
│  ├─ Route
│  ├─ Facility
│  ├─ Actor
│  ├─ Vehicle
│  └─ WorkObject
├─ DisplayName
├─ SpatialAnchor
├─ ParentSpatialStableId
├─ RelationStableIds[]
├─ MinObservationLevel / MaxObservationLevel
├─ RepresentationKey
├─ SelectionState
├─ DetailCardRef
├─ SourceReceiptIds[]
├─ DataQualityCode
└─ AuthorityBoundary
```

- `ObjectStableId`는 확대·표현 변경에도 유지한다. 저해상 표식, 중간 거리 아이콘, 로컬 3D GameObject가 같은 객체라면 같은 ID를 사용한다.
- 여러 객체를 하나로 묶은 집계 표식은 개별 객체 ID를 빌려 쓰지 않고 별도 `AggregateStableId`를 가진다.
- `SpatialAnchor`는 점·선·면·로컬 기준점 중 하나이며 좌표계·정밀도·출처를 포함한다.
- 정적 지리 객체와 동적 운영 객체를 구분한다. 건물 위치와 NPC 현재 업무 상태는 서로 다른 revision과 자료원 실패 경계를 가진다.
- NPC·차량은 서버가 공개한 비식별 관찰 사본만 사용한다. 정밀 GPS·실명·연락처·상세 개인 이동 이력은 Unity 카드에 포함하지 않는다.

## 상세 카드 계약

카드 모양은 공통으로 유지하되 객체 종류별 항목을 붙인다.

```text
SpatialObjectDetailCard
├─ Header
│  ├─ DisplayName
│  ├─ ObjectKind
│  ├─ ObservationLevel
│  └─ DataStatus
├─ Where
│  ├─ ParentSpace
│  ├─ ApproximateLocation
│  └─ SpatialAccuracy
├─ What
│  ├─ Summary
│  ├─ RoleCodes[]
│  └─ CurrentPresentationState
├─ Relations
│  ├─ RelatedObjects[]
│  └─ RelatedRoutes[]
├─ Evidence
│  ├─ Source
│  ├─ ReferenceTime
│  ├─ DerivedOrObserved
│  └─ MissingOrLimitedReason
└─ Navigation
   ├─ FocusObject
   ├─ OpenParentSpace
   ├─ OpenPreparedChildSpace
   └─ OpenOperationalAppLink
```

카드는 다음 경계를 지킨다.

- 지형 카드는 표고·하천·파생 저지대의 근거를 설명한다.
- 거점 카드는 연결 관계와 관찰 가능한 하위 공간을 설명한다.
- 건물 카드는 확인된 용도·주소 범위·시설 역할만 보여 준다.
- NPC·차량 카드는 비식별 역할, 서버가 확정한 현재 단계, 마지막 갱신과 자료 상태만 보여 준다.
- 업무 객체 카드는 주문·운송·창고 업무의 읽기 전용 단계와 관계를 보여 주며 Unity에서 완료를 확정하지 않는다.
- 자료가 없으면 빈 카드를 임의 서술로 채우지 않고 `Missing`, `Restricted`, `ResearchPending`, `Stale`을 보여 준다.

## 선택과 확대의 분리

카메라 확대와 객체 선택은 서로 다른 상태다.

- 확대는 관찰 수준과 표시 자료층을 바꾼다.
- 선택은 현재 수준 안의 객체를 강조하고 상세 카드를 연다.
- `자세히 보기`는 하위 공간 상태 사본이 준비된 경우에만 다음 수준으로 이동한다.
- 객체 선택만으로 카메라를 강제 이동하거나 Actor를 순간이동시키지 않는다.
- 카드를 닫아도 공간과 카메라 문맥은 유지한다.
- 축소하면 상세 카드부터 닫고, 하위 객체를 회수한 뒤 상위 공간의 선택 문맥을 복원한다.

## Graph Map·배치맵과의 관계

- Graph Map은 `포함`, `위치함`, `연결`, `업무 관계`, `상세 공간으로 이동 가능` 관계와 고유 식별자를 소유한다.
- 배치맵은 각 확대 수준에서 실제 좌표를 화면 좌표·Unity 지역 좌표로 변환한 결과와 가시성·겹침·단순화 규칙을 소유한다.
- 공간 상태 사본은 두 결과를 Unity가 읽을 수 있게 합친 관점별 조회 결과이며 원장이나 새 권위가 아니다.
- 동일 객체의 Graph 관계와 배치 위치가 다른 revision이면 더 상세한 수준으로 진입하지 않고 불일치를 표시한다.

## 첫 구현 인계 범위

첫 개발 인계는 한반도 Z2~Z2.5만 대상으로 한다.

1. SRTM·HydroRIVERS·Natural Earth를 한반도 범위로 절단하고 출처·판본·hash를 기록한다.
2. 한반도 지형 상태 사본 한 판본을 결정적으로 생성한다.
3. 산지·주요 하천·해안·파생 저지대와 6개 대표 거점을 `SpatialObservationObject`로 변환한다.
4. 기존 `한반도교류축View`의 거점 카드를 `DetailCardRef`로 연결한다.
5. `지구본 → Z2 지형 → Z2.5 거점 → 카드 → 지구본 복귀`를 실제 Game View에서 검증한다.

건물·NPC·업무 객체는 이번 한반도 지형 구현에서 생성하지 않는다. 다만 같은 공통 계약을 재사용할 수 있도록 종류 코드와 경계를 열어 둔다.

## 확정

- 한반도 첫 자료 조합은 SRTM 30m, HydroRIVERS, Natural Earth 1:10m으로 한다.
- 확대할수록 같은 장면에 객체를 무한 추가하지 않고 정밀도별 상태 사본을 교체한다.
- 공간을 먼저 보여 주고, 공간 안의 객체 선택으로 상세 카드를 연다.
- 거점·건물·NPC·차량·업무 객체는 공통 선택·카드 틀을 쓰되 자료원과 권위 revision은 분리한다.
- 기존 카드·교류축·지구본과 `SimulationWorldShell`을 보존한다.

## 미정

- Z2 높이 격자 크기와 표시용 높이 과장 배율.
- 첫 주요 하천 표시 목록과 저지대 후보의 표고·경사 임계값.
- 6개 거점의 최종 좌표 출처 영수증과 북한 지역 공개 범위.
- 카드의 최종 시각 디자인과 화면 배치.

## 다음 질문 하나

첫 Z2 화면에서 하천은 **한강·대동강·압록강·두만강처럼 광역 판독에 필요한 주요 하천만 표시**하고, 나머지는 더 확대했을 때 여는 방식으로 할까? 화면 판독성과 자료 단순화를 위해 이를 추천한다.

