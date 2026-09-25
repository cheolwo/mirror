# [기획 · 월드·공간·배치 · PLAN-SYSTEM-GEOSPATIAL-TILE-SEMANTIC-ZOOM · 구현 r2]

- 기획 ID: `PLAN-SYSTEM-GEOSPATIAL-TILE-SEMANTIC-ZOOM`
- 기획 분야: 월드·공간·배치
- 기획 판본: `r2`
- 상태: `Approved / ExistingSemanticZoomReused / ExistingStreamingLifecycleReused / AdminStationTransitionImplemented / SagajeongPreparedChildPlayModeVerified / CurrentAdministrativeBoundaryBlocked / LogicalTileStreamingNotImplemented`
- 상위 기획: `PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD`
- 관련 기획: `PLAN-SYSTEM-ADMIN-DONG-DIORAMA`, `PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES`
- Graph Map 영향: `ReviewRequired` — 타일은 Graph 관계를 만들지 않고 기존 공간·객체 고유 식별자를 참조한다.
- 구현 기록: [행정동·역세권·사가정 디오라마 전환 r2](admin-station-diorama-transition.implementation.r2.md)

## 목표

지구본에서 한반도·지역·행정동·역세권·디오라마까지 확대할 때 화면은 연속적으로
가까워지되, 정보는 그 거리에서 읽을 수 있고 출처가 준비된 수준만 단계적으로
드러나게 한다.

타일은 건물·NPC·업무 의미를 생성하는 단위가 아니다. 타일은 다음 책임만 가진다.

- 일정한 지리 범위를 식별한다.
- 같은 범위의 지형·경계·도로·건물 윤곽 등 자료층을 재생성·검증·캐시한다.
- 카메라에 보이는 범위만 지연 적재하고 화면 밖 자료를 회수한다.
- 상위 저정밀 표현과 하위 고정밀 표현을 안전하게 교체한다.

사용자는 타일 경계를 보는 것이 아니라 하나의 연속 공간을 본다. 타일 경계와
상관없이 같은 공간 객체는 같은 고유 식별자를 유지한다.

## 서로 섞지 않는 세 축

| 축 | 질문 | 기존 체계 | 금지하는 해석 |
| --- | --- | --- | --- |
| 의미 확대 | 지금 어느 범위의 어떤 정보를 보여 주는가? | `Z0~Z5` | `Z2`가 타일 4개라는 식의 고정 결속 |
| 자료 타일 | 자료를 어떤 좌표·해상도·범위로 자르고 불러오는가? | `WorldCRS84Quad`, `kr5186` | 타일이 H 공간이나 업무 객체를 자동 생성 |
| 공간 포함 | 그 공간에서 어떤 행동·블록·경관·지역이 성립하는가? | `H1~H5` | 카메라 확대만으로 H 단계 성립·승격 |

현재 Unity의 1·4·16개 Mesh는 이 구조를 비교하기 위한 모판이다. 정식 타일 주소,
가지별 선택, 지연 생성·회수 또는 캐시를 증명하지 않으므로 최종 타일 체계로
승격하지 않는다.

## 좌표와 타일 주소 체계

### 1. 세계·대륙·한반도: `WorldCRS84Quad`

지구 곡면의 논리 주소는 OGC `WorldCRS84Quad`를 기준으로 한다.

```text
worldcrs84quad:{tileMatrix}:{tileRow}:{tileCol}
```

- `TileMatrixSetId`, `TileMatrix`, `TileRow`, `TileCol`을 별도 필드로 보존한다.
- `CRS84`의 축 순서를 계약에 명시하고 EPSG:4326 배열 순서와 암묵적으로 섞지 않는다.
- 한반도 범위와 실제 자료가 존재하는 `TileMatrixSetLimits`만 선언한다.
- 화면 오차가 작아져도 원본 자료의 `SourceMaximumTileMatrix`보다 더 내려가지 않는다.
- 빈 바다·자료 결손 tile은 가짜 지형이나 객체로 채우지 않는다.

### 2. 대한민국 지역 상세: 기존 `kr5186`

대한민국의 준비된 지역 공간은 기존 EPSG:5186 고정 격자를 재사용한다.

```text
kr5186:l0:{x}:{y}  // 8km
kr5186:l1:{x}:{y}  // 2km
kr5186:l2:{x}:{y}  // 500m
```

`WorldCRS84Quad` 주소를 이 키로 바꾸거나 같은 타일이라고 간주하지 않는다. 지역
진입 관문은 두 체계의 범위·좌표 변환·출처 판본을 결속한
`SpatialReferenceTransition`을 사용한다.

### 3. 디오라마: 지역 ENU·배치맵

행정동·역세권 디오라마에 진입하면 검증된 지역 원점을 가진 로컬 ENU 좌표와
기존 배치맵을 사용한다. Floating Origin은 Unity 표현 좌표만 옮기며 원본 지리
좌표·H 계보·배치 hash를 바꾸지 않는다.

## 확대 단계별 정보 공개

| 의미 수준 | 공간 | 기본 표시 | 선택 또는 다음 확대에서 표시 | 열지 않는 정보 |
| --- | --- | --- | --- | --- |
| `Z0 World` | 지구 | 대륙·해양·저해상 국가 경계 | 국가 개요·자료 준비도 | 도시·건물·NPC |
| `Z1 EastAsia` | 동아시아 | 국가·광역 해안·낮은 정밀도 지형 | 국가별 자료 범위·반도 표식 | 지역 시설·업무 객체 |
| `Z2 PeninsulaTerrain` | 한반도 | 해안·지형 음영·검증 하천 | 산지권·하천·저지대 근거 | 개별 건물·사람 |
| `Z2.5 PeninsulaHubs` | 한반도 거점망 | Z2 지형·단순 교류축·대표 거점 | 거점 관계·출처·결손 | 현재 운행·통행 추정 |
| `Z3 Region` | 시도·시군구·생활권 | 행정 경계·광역 교통·하천·집계 | 도시·역·항만·물류 거점 | 집계를 개별 업체로 분해 |
| `Z4 Neighborhood` | 행정동·역세권 | 도로·건물 윤곽·시설·공개 집계 | 준비된 건물·시설·업무 장소 카드 | 미검증 주소·출입구·정밀 GPS |
| `Z5 LocalDiorama` | 승인된 로컬 공간 | 검증된 배치·표현·상태 사본 | NPC·차량·작업 객체의 비식별 관찰 | Unity의 업무 완료 확정 |

확대는 위 수준의 후보를 고를 뿐이다. 실제 진입은 `MaximumObservationLevel`,
자료층 준비 상태, 출처·권리, 개인정보 공개 범위와 하위 공간 승인까지 통과해야
한다.

## 타일과 자료층 계약

```text
SpatialTileManifest
├─ TileStableId
├─ TileMatrixSetId / TileMatrix / TileRow / TileCol
├─ ParentTileStableId / ChildTileStableIds[]
├─ Bounds / CoordinateReferenceSystem
├─ GeometricErrorOrSampleSpacing
├─ SourceMaximumTileMatrix
├─ AvailableObservationLevels[]
├─ LayerDescriptors[]
│  ├─ LayerStableId / LayerKindCode
│  ├─ MinObservationLevel / MaxObservationLevel
│  ├─ ContentKindCode
│  ├─ Revision / ContentHash / ByteLength
│  ├─ SourceReceiptIds[] / LicenseCode
│  └─ Readiness / MissingReason
├─ LocalProjectionOrigin?
├─ GeneratedAt / ObservedAt
└─ PresentationOnly = true
```

자료층은 실패와 수명을 분리한다.

- 기반 정적층: 해안, 표고, 지형 Mesh, 토지피복.
- 선형 정적층: 하천, 행정 경계, 도로, 철도.
- 시설 정적층: 건물 윤곽, 공개 시설, 검증된 기준점.
- 집계 관찰층: 인구·업종·활동 밀도와 별도 `AggregateStableId`.
- 동적 관찰층: 비식별 NPC·차량·업무 상태 사본. 메모리에만 보관하고 짧은 TTL을 쓴다.
- 상세 카드: 선택한 객체의 근거·관계·상태를 필요할 때 별도 조회한다.

정적 지형 tile 실패가 동적 운영 상태를 되감거나, 운영 조회 실패가 지형을
사라지게 해서는 안 된다.

## 카메라에서 화면까지의 처리 순서

```text
마우스 휠·드래그·핀치
→ 카메라는 즉시 반응하고 기존 검증 화면 유지
→ 기존 0.3초 안정 관문 통과
→ SpatialObservationContext 생성
→ 의미 수준과 허용된 최대 관찰 수준 판정
→ 절두체 안 타일 가지 후보 계산
→ 화면 오차 8px 임시 목표 + 자료별 SourceMaximumTileMatrix 적용
→ cache hit는 즉시 사용, 나머지만 제한 병렬 요청
→ revision·hash·범위·출처·공개 범위 검증
→ 비활성 staging root에서 조립
→ 준비된 자식은 부모를 REPLACE, 독립 자료층은 정책에 따라 ADD
→ 다음 프레임에 원자적으로 교체
→ 화면 밖 상세 자식부터 풀·캐시로 회수
```

- 8px는 Windows 1080p 비교를 시작하기 위한 임시 Profile 값이지 제품 상수가 아니다.
- 부모는 필요한 자식이 모두 준비되기 전까지 유지한다. 자식 하나가 실패하면 그
  구역의 부모를 남기고 다른 자식만 정상적으로 세분화할 수 있다.
- 같은 객체가 부모·자식 자료에 함께 있으면 `ObjectStableId`와 표현 우선순위로
  한 번만 보인다. 집계 객체는 개별 객체 ID를 사용하지 않는다.
- 현재 수준과 인접 수준 하나만 전환에 참여한다. 모든 정밀도를 동시에 유지하지 않는다.
- 늦은 응답은 기존 `RequestGeneration`으로 거절한다.

## Graph Map·배치맵·H 계층 결속

- Graph Map은 공간·거점·경로·업무 객체의 고유 식별자와 `Contains`, `LocatedIn`,
  `ConnectedTo`, `PreparedChildSpace` 관계를 소유한다.
- 배치맵은 좌표 변환, tile 교차 범위, 가시성, 겹침, 단순화, 로컬 ENU 배치를
  소유한다.
- tile은 Graph 노드와 H 정의를 참조할 수 있지만 새 관계·H1·건물·NPC를 만들지 않는다.
- 하나의 H 객체가 여러 tile에 걸릴 수 있고 하나의 tile이 여러 H 경계를 참조할 수 있다.
- `H1~H5`는 디오라마 공간 의미이고 `TileMatrix`나 `kr5186 L0~L2`와 같은 단계가 아니다.

## 구현 우선순위

### P0 — 논리 주소와 동결 Manifest

1. `WorldCRS84QuadTileKey`와 위경도 bounds 계산을 UnityEngine 비의존 순수 코어로 만든다.
2. 현재 한반도 범위에 필요한 tile limits와 source maximum level을 동결 Manifest로 만든다.
3. 동일 입력의 tile key·bounds·manifest hash 결정성을 시험한다.

### P1 — 화면 가지 선택 모판

1. 기존 화면 오차 선택기의 1·4·16 고정 결과를 논리 tile 가지 선택으로 교체한다.
2. 절두체, 카메라 표면 거리, viewport, FOV, 임시 8px 목표를 함께 계산한다.
3. 기존 의미 단계와 독립적으로 visible·adjacent tile만 선택한다.
4. 이 단계에서는 현재 곡면 Mesh renderer를 Adapter로 재사용하고 새 Scene을 만들지 않는다.

### P2 — 지연 생성·취소·회수

1. 기존 `공간TileStreamingController`의 repository→manifest→slot/pool→load/cancel→reconcile
   생명주기를 일반화하되 `kr5186` 주소 계약은 유지한다.
2. 부모 유지, 자식 staging, 원자 교체, 화면 밖 회수와 제한 병렬 요청을 구현한다.
3. 메모리·요청 수·cache hit·실패 tile을 진단 패널에 표시한다.

### P3 — 의미 자료층과 객체 카드

1. Z2 해안·지형·검증 하천·거점 공개 시점을 tile 상세도와 분리한다.
2. `SpatialObservationObject`의 동일 고유 식별자를 저정밀 표식→아이콘→3D 표현까지 유지한다.
3. 객체 선택은 상세 카드를 열 뿐 Actor·Simulation·운영 revision을 바꾸지 않는다.

### P4 — 대한민국 지역 좌표 인계

1. 선택한 대한민국 지역의 CRS84 범위와 기존 `kr5186` coverage를 대조한다.
2. 검증된 `SpatialReferenceTransition`이 있을 때만 Z3/Z4 상세 창으로 넘어간다.
3. 행정동 경계·도로·건물 윤곽이 없는 곳은 집계 또는 결손 상태로 남긴다.

### P5 — 행정동·역세권 디오라마 인계

1. Z4의 준비된 행정동·역세권에서만 로컬 ENU 원점과 배치맵을 연다.
2. 사가정역·면목제3·8동 기존 디오라마를 첫 Z5 Adapter로 재사용한다.
3. H1~H5와 Graph Map이 승인한 객체만 3D로 조립하고, 공개자료 집계를 임의 NPC로 분해하지 않는다.

## 첫 개발 수직 단위

```text
지구본의 대한민국 선택
→ 카메라 확대와 0.3초 안정
→ 한반도 CRS84 visible tile 가지 계산
→ 8px 임시 목표와 source maximum level로 선택
→ 현재 곡면 지형·검증 하천을 tile root별 지연 조립
→ 부모/자식 중복 없이 교체
→ 서울 거점 선택 카드
→ 축소 시 자식 회수·부모 및 선택 문맥 복원
```

첫 단위에서는 행정동 건물·NPC·업무 객체와 디오라마 진입을 만들지 않는다. 이
단위가 안정된 뒤 `서울 지역 → 대한민국 EPSG:5186 → 준비된 행정동` 인계를 별도
수직 단위로 연다.

## 검증과 완료 조건

- 같은 좌표·수준은 항상 같은 tile key와 bounds를 만든다.
- 인접 tile의 경계와 부모·자식 범위가 수학적으로 일치한다.
- 화면에 보이는 가지와 인접 사전 적재만 요청하며 모든 21개 Mesh를 선생성하지 않는다.
- 빠른 확대·축소에서 최종 문맥만 적용되고 오래된 응답이 화면을 되돌리지 않는다.
- 자식 준비 전 부모가 사라지지 않고 실패 tile만 상위 표현으로 안전하게 남는다.
- 같은 객체는 확대 전후 같은 ID로 선택·카드 문맥이 복원된다.
- 자료층을 끄면 해당 자료의 요청·렌더·선택 후보가 함께 사라진다.
- 원본보다 높은 상세도를 요청하지 않고 `Partial`, `Missing`, `Restricted`, `Stale`을 구분한다.
- 카메라·tile 적재·카드 선택이 `WorldTick`, 업무 원장과 Simulation revision을 바꾸지 않는다.
- EditMode 순수 계산, Play Mode 지연 적재·회수, 실제 1920×1080 Game View와 Windows
  Player 평균·p95 프레임·최고 메모리를 서로 별도 증거로 남긴다.

## 성능 Profile에서 나중에 확정할 값

- 화면 오차 목표: 현재 PC 후보 `8px`; Mobile·고해상도 Profile은 측정 뒤 분리.
- 동시 요청 수와 준비 tile 수.
- CPU/GPU frame p95, 최고 메모리, cache hit, tile 준비 지연 p95.
- 부모·자식 교차 유지 프레임과 화면 밖 회수 유예 시간.
- 자료층별 `SourceMaximumTileMatrix`와 byte 예산.

이 값은 코드 전역 상수가 아니라 판본화된 `SpatialStreamingProfile`로 관리한다.

## 기준 자료

- [OGC 2D Tile Matrix Set 2.0 — WorldCRS84Quad](https://docs.ogc.org/is/17-083r4/17-083r4.html)
- [OGC API Tiles 1.0](https://docs.ogc.org/is/20-057/20-057.html)
- [OGC 3D Tiles 1.1 — geometric error와 refinement](https://docs.ogc.org/cs/22-025r4/22-025r4.html)
- [기존 확대 기반 공간 자료 적재 r27](../PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/zoom-driven-spatial-streaming.decision.r27.md)
- [기존 공간 의미 확대·객체 카드 r25](../PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/spatial-semantic-zoom-object-card.decision.r25.md)
- [기존 화면 오차 비교 실험](../PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/adaptive-tile-sse-comparison.experiment-2026-09-22.md)
- [공간 Tile·Area·AreaSet 경관 생성 파이프라인](../../../../Architecture/SpatialTileAreaSetLandscapePipeline.md)

## 확정

- 사용자는 연속 공간을 보고, tile은 보이지 않는 자료·캐시·재생성 단위로 사용한다.
- 확대할수록 준비된 자료층과 객체만 단계적으로 드러낸다.
- 의미 확대, 자료 타일, H 공간 포함을 서로 독립된 축으로 유지한다.
- 세계 곡면 주소, 대한민국 고정 격자와 로컬 디오라마 좌표를 관문으로 연결한다.
- 기존 `SimulationWorldShell`, 0.3초 관문, 객체 카드와 로컬 tile 생명주기를 재사용한다.

## 미정

- 자료층별 실제 `SourceMaximumTileMatrix`와 tile content 형식.
- PC·Mobile별 요청·메모리·캐시 예산.
- Z3에서 CRS84 표현을 유지할 범위와 EPSG:5186으로 전환할 정확한 관문.
- 부모·자식 교차 표현 시간과 부분 실패의 최종 시각 디자인.

## 다음 질문 하나

준비된 사가정 디오라마 전환은 r2로 닫았다. 다음 구현은 계획의 P0~P1인
**`WorldCRS84Quad` 논리 tile key·bounds·visible 가지 선택**으로 제한하고, 현재
행정 경계 원본 수집과 다른 역 확장은 별도 관문으로 유지할까? 자료 주소와 선택
결정성을 먼저 닫을 수 있으므로 이 순서를 추천한다.
