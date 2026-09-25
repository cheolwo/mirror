# [기획 · 월드·공간·관찰 · 한반도 지형 의미 확대 · 제안 r24]

## 요청 해석

현행 `지구본 → 한반도 교류축 → 거점 카드` 흐름에서 카드가 한반도의 공간을 대신하지 않게 한다. 대한민국 또는 한반도를 선택하고 확대하면 먼저 산지·주요 하천·해안·저지대의 관계가 보이는 **한반도 지형판**으로 전환하고, 더 확대했을 때 부산·서울·도라산·개성·평양·신의주 같은 거점과 교류축을 그 지형 위에 표시한다. 기존 카드는 선택한 지형·거점의 상세 설명으로 유지한다.

```text
Z0 세계 지구본
  → Z1 동아시아
    → Z2 한반도 지형판
       ├─ 표고·산지 음영
       ├─ 해안선·주요 하천
       └─ 파생 저지대 후보
         → Z2.5 한반도 거점·교류축
            ├─ 부산
            ├─ 서울
            ├─ 도라산
            ├─ 개성
            ├─ 평양
            └─ 신의주
              → Z3 교류축
                → Z4 역·도시 정보 카드
                  → Z5 승인된 지역 디오라마
```

## 현재 기준선과 결손

- 현행 Unity는 Natural Earth `1:110m` 국가 경계를 사용한 구체 지구본과 대한민국 선택을 제공한다.
- 현행 `한반도교류축View`는 8개 거점·7개 구간과 정보 카드를 상대적 순서로 표현한다.
- 실제 `SimulationWorldShell` Game View에서 교류축과 가상 물자 이동 표식은 관찰했으나, 한반도 DEM·하천·해안 투영 자료가 결속된 지형 mesh는 아직 없다.
- 기존 공간 파이프라인에는 Copernicus DEM 절단·hash·CRS·NoData 계약이 있으나 평창 표본용이다. 이를 한반도 전체 자료가 준비됐다는 증거로 재사용하지 않는다.
- 따라서 현재 카드를 지형 위에 얹는 것은 가능하지만, 근거 자료를 받기 전에 산맥·강·평지를 손으로 그려 확정 지형처럼 표시해서는 안 된다.

## 자료층 제안

| 자료층 | 첫 후보 | 표현 책임 | 한계·주의 |
| --- | --- | --- | --- |
| 표고·산지 | NASA/USGS SRTM 1 Arc-Second Global | 한반도 표고 mesh, 산지 음영, 경사 파생 | 약 30m 표본이며 건물·도로 정밀 배치 근거가 아님. void·수직 기준을 manifest에 기록한다. |
| 표고 대안 | Copernicus DEM GLO-30 | 기존 공간 파이프라인과 같은 형식의 DSM 절단 | 2026-07-28 이후 30m View Service는 허가 사용자 범주와 계정 설정을 요구한다. 기존 승인 사본이나 접근 권한을 확인하기 전 기본 자료로 단정하지 않는다. |
| 주요 하천 | HydroRIVERS Asia | 한강·대동강·압록강·두만강 등 축척에 맞는 하천 선 | 15 arc-second 기반이며 집수면적 10km² 이상 또는 평균 유량 0.1m³/s 이상 중심이다. 모든 소하천을 표현하지 않는다. |
| 해안·광역 물리 | Natural Earth `1:10m` Physical | Z1~Z2 해안선·호수·광역 보조선 | 국가·지역 조망용이다. 역세권·건물·법적 경계 정밀 자료로 사용하지 않는다. |
| 평지·분지 | DEM 파생 저지대 후보 | 낮은 표고와 완만한 경사를 단순 색면으로 표시 | 공식 평야 경계가 아니라 계산 결과다. 임계값·알고리즘·원본 hash를 함께 표시하고 `DerivedLowlandCandidate`로 제한한다. |

SRTM은 60°N~56°S의 육지를 1 arc-second 간격으로 제공하므로 한반도 광역 표고의 첫 접근 가능한 기준선으로 사용할 수 있다. Copernicus GLO-30은 30m 전 세계 DSM이지만 현재 접근 조건을 별도로 확인해야 한다. 두 자료를 섞어 한 판본을 만들지 않고, 하나를 선택해 원본·판본·hash를 동결한다.

## 서버·동결 투영 계약 후보

```text
한반도지형ProjectionManifest
├─ SchemaVersion
├─ ProjectionStableId
├─ BoundsWgs84
├─ LocalProjection
├─ GeneratedAt
├─ SourceReceipts[]
│   ├─ Provider
│   ├─ DatasetVersion
│   ├─ LicenseAndAttribution
│   ├─ SourceHash
│   └─ RetrievedAt
├─ ElevationGrid
│   ├─ Width / Height
│   ├─ NoData
│   ├─ VerticalDatum
│   └─ PresentationExaggeration
├─ CoastlinePaths[]
├─ MajorRiverPaths[]
├─ DerivedLowlandCells[]
├─ HubMarkers[]
│   ├─ StableId
│   ├─ Latitude / Longitude
│   ├─ MaximumObservationLevel
│   └─ SourceReceiptIds[]
└─ Boundaries
    ├─ ObservationPresentationOnly = true
    ├─ TraversalReady = false
    └─ GameplayReady = false
```

- 원본 래스터·벡터 전체는 Unity 배포 자원에 넣지 않는다. 서버/자료 도구가 한반도 범위로 절단·단순화한 동결 투영만 Unity가 읽는다.
- Unity는 높이 과장과 색상만 표현할 수 있으며 물리 표고·경사·하천 위치·거점 좌표를 다시 판정하지 않는다.
- 거점은 출처가 있는 위경도로 투영한다. 현재 상대 순서로만 배치된 거점의 정확 좌표를 새 지형판의 사실 좌표처럼 승격하지 않는다.
- 남북한 경계와 군사분계선은 별도 법적·역사 자료층이다. DEM·해안선으로 추정하지 않는다.

## Unity 표현 제안

새 공식 Scene이나 별도 Map Manager를 만들지 않는다. `SimulationWorldShell/WorldMapRoot` 아래에서 기존 지구본과 `한반도교류축View` 사이에 읽기 전용 `한반도지형판View`를 둔다.

```text
WorldMapRoot
├─ GlobeRoot
├─ KoreanPeninsulaTerrainRoot
│  ├─ TerrainMesh
│  ├─ CoastlineLayer
│  ├─ MajorRiverLayer
│  ├─ LowlandCandidateLayer
│  └─ HubMarkerLayer
└─ KoreanPeninsulaCorridorRoot
   ├─ CorridorSegments
   └─ SelectedHubInformationCard
```

첫 화면은 북쪽이 위인 납작한 지도 대신, 현재 디오라마와 조화를 이루는 낮은 각도의 3D 지형판으로 만든다. 지형 높이는 판독을 위해 시각적으로 과장할 수 있지만 실제 표고와 과장 배율을 분리한다.

확대 동작은 다음과 같이 한다.

1. 대한민국 선택 뒤 확대하면 지구본 곡면에서 한반도 지형판으로 교차 전환한다.
2. Z2에서는 산지·해안·주요 하천만 우선 읽히고 거점 표시는 최소화한다.
3. 더 확대하면 출처가 결속된 부산·서울·도라산·개성·평양·신의주 표식과 교류축을 표시한다.
4. 거점을 선택하면 기존 카드를 오른쪽 또는 하단 상세 패널로 연다.
5. 준비된 거점만 Z4/Z5로 진입한다. 자료가 없으면 같은 지형판에서 결손 이유를 보인다.
6. 축소하면 거점·교류축을 먼저 회수하고, 지형판을 거쳐 지구본으로 돌아간다.

## 첫 수직 슬라이스

첫 구현은 한반도 전체를 고정밀로 만들지 않고 다음까지만 닫는다.

- SRTM 또는 접근이 확인된 Copernicus DEM 중 하나를 선택해 한반도 범위의 단순 높이 격자를 생성한다.
- Natural Earth `1:10m` 해안선과 HydroRIVERS의 한반도 주요 하천을 같은 투영으로 절단한다.
- 저지대 후보는 표고·경사 임계값을 명시한 파생 자료로 생성한다.
- 부산·서울·도라산·개성·평양·신의주의 위경도 출처를 별도 영수증으로 결속한다.
- 기존 `한반도교류축View`의 카드·상태·가상 물자 표식은 지형판 위 거점 선택 이후에만 나타나게 한다.
- 카메라 확대와 화면 전환은 관찰 상태만 바꾸며 Simulation·운영 원장·실제 통행 상태를 변경하지 않는다.

## 검증과 상한

- 같은 원본과 설정으로 동결 투영의 byte와 SHA-256이 동일해야 한다.
- 한반도 범위 밖 자료, NoData, 좌표계·수직 기준 결손, 출처 없는 거점은 생성 단계에서 거절한다.
- 해안·하천·거점이 같은 투영에서 허용 오차 안에 겹치는지 표본 검사한다.
- 평지 표현은 `DerivedLowlandCandidate`로 보이고 공식 평야 경계처럼 표시하지 않는다.
- Unity EditMode에서 projection hash, 좌표 변환, 확대 단계, 낮은 revision 거절, 지구본 복귀를 검증한다.
- 실제 Play Mode·Game View에서 `지구본 → 한반도 지형 → 거점 → 카드 → 복귀`를 확인하기 전에는 표현 E5 이상을 주장하지 않는다.
- 지형판은 NPC 이동 가능 구역, 철도 운행 가능성, 군사·법적 경계, 실제 교류 가능성을 증명하지 않는다.

## 확정

- 카드보다 한반도 공간을 먼저 보여 주는 지형 우선 확대가 사용자의 목표다.
- 산지·하천·해안·평지를 하나의 임의 그림으로 만들지 않고 출처와 파생 수준을 분리한다.
- 기존 지구본·교류축·거점 카드와 canonical `SimulationWorldShell`을 보존한다.
- 부산·서울·도라산·개성·평양·신의주 표식은 지형판의 다음 확대 수준에서 나타난다.

## 미정

- 첫 DEM을 SRTM으로 고정할지, 접근 권한이 확인된 Copernicus GLO-30 사본을 사용할지.
- 지형판의 최종 격자 크기, 단순화 수준, 높이 과장 배율.
- 주요 하천의 첫 표시 목록과 저지대 후보 임계값.
- 북측 거점 위경도의 최종 권위 출처와 공개 범위.

## 다음 질문 하나

첫 구현 기준 자료를 **NASA/USGS SRTM 30m + HydroRIVERS + Natural Earth 1:10m** 조합으로 확정할까? 권한 확인 없이 Copernicus GLO-30에 종속되지 않고 시작할 수 있어 이 조합을 추천한다.

