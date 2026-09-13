[기획 · 월드·공간·배치 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r8]

# 실제 외관 근거 기반 건물 표현 교체 제안

- 상태: `ProposalPrepared / ApprovalPending / DataAndRightsFirst / BlenderAndUnityWorkNotStarted`
- 상위 기획: [역세권 디오라마 모듈 표준 r8](README.md)
- 관련 기획: [사가정시장 입구·대표 골목 수집 명세 r6](sagajeong-market-landmark-collection.r6.md)
- 기획 근거: 2026-09-13 사용자가 기존 사가정 디오라마의 도로·건물 뼈대를 보존하면서, 도로명주소와 역 주변 자료·실제 건물 사진을 수집하고 Blender로 대표 건물을 제작해 기존 모형을 점진적으로 교체하는 방안을 요청함.

## 제안 요약

현재 절차적 건물 전체를 없애지 않는다. 역세권 1km의 공간적 연속성은 기존 건물 도형·높이 기반 모형이 계속 책임지고, 근거와 이용권을 갖춘 대표 건물만 별도 **랜드마크 표현 계층**으로 올린다.

```text
공공 공간자료
├─ 도로명주소 건물관리번호·건물 도형·출입구
├─ 건축물대장 층수·높이·구조·주용도
└─ 항공·정사영상의 지붕과 주변 배치
        ↓ 식별·좌표·치수 대조
랜드마크 근거 묶음
├─ 공간 근거
├─ 외관 시각 근거
├─ 개별 이용권·출처·hash
└─ 모델 제작 브리프
        ↓ 권리·정합성 관문
Blender 원본 → LOD FBX → Unity 표현 자원 대장
        ↓ Prefab·bounds·hash 검증 성공 뒤
해당 건물의 절차적 매스만 교체
```

교체 자원이 없거나 판본·hash·권리·크기가 맞지 않으면 기존 절차적 건물을 그대로 표시한다. 실패 때문에 도시에 빈 구멍이 생기지 않게 하는 것이 가장 중요한 안전 조건이다.

## 현재 뼈대에서 재사용할 것

- [사가정 공간 밀도 표현 r18](../PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/spatial-density-presentation.r18.md)의 건물 고유 식별자, polygon, 실제 높이와 상징적 fallback 구분을 보존한다.
- 역 중심 1km 표현 창, 4×4 chunk, 카메라와 기존 저채도 도형 표현을 유지한다.
- Graph Map은 랜드마크·역·도로·건물의 의미 관계를, 배치맵은 실제 도형·좌표·방향·높이와 Unity XZ 변환을 계속 소유한다.
- 기존 Blender 작업의 `source / exports / validation / workflows` 구조와 원본 재열기·FBX 왕복·hash 검사를 재사용한다.
- Unity에는 새 Scene이나 별도 Map Manager를 만들지 않고 canonical `SimulationWorldShell`의 기존 역세권 모듈에 표현 계층만 추가한다.

현재 자료는 **실제 건물의 위치와 질량**을 표현하지만 실제 외관·출입 가능성·현 영업을 증명하지 않는다. 새 사진을 구했다는 이유만으로 기존 건물 고유 식별자, 업무 장소, 통행 또는 gameplay 권위를 바꾸지 않는다.

## 표현 정밀도 세 층

| 층 | 대상 | 표현 방식 | 교체 여부 |
| --- | --- | --- | --- |
| 1. 배경 매스 | 역세권 전체 건물 | 현재 polygon·높이 기반 저채도 절차적 모형 | 항상 유지 가능한 안전 fallback |
| 2. 동네 외관 문법 | 반복되는 주택·근린상가·아파트 | 창호·옥상·차양·외장재를 유형별 모듈로 재사용 | 원본 매스 위의 비권위 표현 보강 |
| 3. 대표 랜드마크 | 역 출구에서 읽히는 독립 건물·시장 입구·공원 시설 | 개별 근거로 만든 Blender LOD 자원 | 검증된 대상 건물만 선택 교체 |

모든 일반 건물을 개별 모델링하지 않는다. 이 구분을 쓰면 사실감은 랜드마크와 거리의 반복 문법에서 얻고, 1km 전체의 제작비·메모리·권리 위험은 제한할 수 있다.

## 자료 수집 우선순위

### P0 · 위치와 질량을 정하는 공식 자료

1. [주소기반산업지원서비스](https://business.juso.go.kr/)
   - 건물관리번호, 도로명주소, 건물 도형과 제공되는 경우 출입구 좌표를 결속한다.
   - 주소·출입구는 건물 식별과 배치 근거이며 외관 사진 이용권은 아니다.
2. [국토교통부 건축HUB 건축물대장정보](https://www.data.go.kr/data/15134735/openapi.do)
   - 층수·높이·구조·주용도·면적을 도형과 대조한다.
   - 데이터 페이지의 이용조건과 개별 건물 외관·간판·설계·사진의 권리를 동일시하지 않는다.
3. [국토정보플랫폼 항공사진](https://www.data.go.kr/data/15059918/fileData.do)
   - 지붕 형태, 필지 관계, 골목 폭, 주변 수목과 공터를 위에서 대조한다.
   - 정면 입면의 근거를 대신하지 않으며 내려받은 상품별 판본·이용조건을 별도로 보존한다.

### P1 · 외관을 만드는 시각 자료

- 최우선은 직접 촬영하거나 건물 소유자·촬영자에게 명시적으로 허락받은 사진이다.
- 공공 사진은 개별 항목에 [공공누리 제1유형](https://www.kogl.or.kr/static/html/opencode.html)이 실제로 표시되어 상업적 이용과 변경이 가능한지 다시 확인하고 출처를 표시한다.
- S-MAP·브이월드·카카오 로드뷰·네이버 거리뷰는 촬영 방향과 누락 면을 확인하는 로컬 참고 도구로만 쓴다. 별도 허락이 없으면 화면 캡처, 사진 파일, 3D mesh를 텍스처·모델 제작 자산으로 추출하거나 배포하지 않는다.
- 인물 얼굴, 차량 번호판, 세대 정보, 연락처는 제거한다. 간판·상표·벽화는 별도 허락이 없으면 읽을 수 없는 일반화 표현으로 바꾼다.

### 자료를 두 묶음으로 분리

```text
위치·치수 근거                  외관·색·재료 근거
도로명주소·건물도형             직접 촬영·명시적 허락
건축물대장                      개별 공공누리 제1유형 사진
항공·정사영상                   권리가 확인된 도면·사진
        │                               │
        └──────── 같은 건물 대조 ────────┘
```

주소가 맞는다고 사진 이용권이 생기지 않고, 사진 속 건물이 비슷하다고 정확한 polygon에 결속하지 않는다. 두 관문을 모두 통과해야 배포 가능한 모델 후보가 된다.

## 서버·Graph Map·배치맵 자료 계약 제안

### `StationLandmarkEvidence` 후보

- `LandmarkStableId`, `TransitStationStableId`, 공식 표시명과 분류
- 대상 `BuildingStableIds[]`와 건물관리번호 후보
- 각 원천 URL·수집 시각·기준일·원본 SHA-256
- 사진별 촬영자·촬영 위치/방향·권리 유형·상업 이용·변형·게임 배포 가능 여부
- 관측 치수와 추정 치수, 근거 없는 면과 `MissingVisualEvidence`
- 간판·얼굴·번호판 일반화 방침
- `PendingHumanReview / ApprovedForModelBrief / RightsBlocked` 상태와 검토자

### Graph Map 관계

- `CandidateLandmarkFor`: 해당 역세권의 대표 후보
- `LocatedIn`: 검증된 건물·법정동·행정동 관계
- `DerivedFrom`: 공간·사진·건축물대장 근거
- `PresentationOf`: Blender/Unity 자원이 표현하는 건물
- `ScenarioOverlayOf`: 판타지·업무 표현이 현실 공간 기반 위에 얹히는 경우

Graph Map은 “대표 건물”이라는 의미와 자료 계보를 소유한다. 사진만 보고 새로운 건물·출입구·업무 장소를 만들지 않는다.

### `StationLandmarkBinding` 후보

- `TransitStationStableId`, `LandmarkStableId`
- 공간 overlay revision·content hash
- 교체 대상 `BuildingStableIds[]`
- `VisualKey` 또는 기존 공간 조립 자원 키
- 위치·회전·균일 배율 보정과 예상 footprint bounds
- Prefab content hash와 `FallbackMode=KeepProceduralMass`

서버는 Unity 파일 경로를 보내지 않는다. Unity의 기존 표현 자원 대장이 `VisualKey`를 Prefab으로 해석한다.

## Blender 제작 규격 제안

```text
LM_<stableId>_LOD0
LM_<stableId>_LOD1
LM_<stableId>_LOD2
COL_<stableId>
ANCHOR_FOOTPRINT
ANCHOR_ENTRANCE   # 실제 근거가 있을 때만
```

- `1 Unity unit = 1m`, 지면 pivot, 북쪽/정면 방향과 적용 좌표계를 manifest에 고정한다.
- LOD0는 역 확대 관찰, LOD1은 중거리, LOD2는 실루엣 판독용이다.
- 실제로 확인하지 못한 뒤편·옥상·재질은 디오라마 문법으로 일반화하고 `Estimated`로 기록한다.
- 사진을 그대로 벽 텍스처로 붙이는 것을 기본값으로 삼지 않는다. 저폴리 재질과 반복 가능한 창호·차양 모듈로 재해석한다.
- `.blend`, FBX, 재질, 원천 영수증, 모델 manifest의 hash를 한 묶음으로 남긴다.
- 첫 슬라이스는 관찰 전용이므로 내부·NavMesh·정밀 collider를 만들지 않는다. 필요하면 시각 mesh와 분리한 단순 proxy만 둔다.

## Unity 교체 방식

```text
binding·overlay revision·hash 확인
→ Prefab 존재·Renderer·bounds·LOD 확인
→ LandmarkRoot 아래에 먼저 생성
→ 생성 결과가 정상일 때만 대상 building.id 매스 숨김

하나라도 실패
→ 랜드마크 자원 제거
→ 기존 절차적 building.id 매스 유지 또는 즉시 복귀
```

- 랜드마크는 기존 geography root와 나란한 `LandmarkRoot`에 둔다. chunk mesh에 합쳐 개별 해제·LOD·fallback을 어렵게 만들지 않는다.
- 가까운 거리는 LOD0, 중거리는 LOD1, 먼 거리는 LOD2 또는 기존 절차적 매스를 쓴다.
- 현재 역세권 renderer/material 예산 안에 랜드마크 비용을 포함한다.
- 카메라·건물 선택은 표현 동작이다. 모델 교체가 NPC·주문·통행·저장 상태를 바꾸지 않는다.

## 첫 두 절편 제안

### A. 사가정시장 입구·대표 골목

기존 승인 대상을 버리지 않는다. 시장은 여러 필지·점포·차양의 집합일 수 있으므로 첫 단계에서는 입구 경계와 반복 골목 모듈을 **overlay**로 만든다. 시장 전체의 정확한 건물 범위가 확인되기 전에는 특정 건물 매스를 숨기지 않는다.

### B. 첫 실제 건물 교체

사가정역에서 보이는 후보 중 다음 조건을 만족하는 독립 건물 한 동을 고른다.

1. 건물 polygon 하나와 건물관리번호 하나가 명확히 대응한다.
2. 역 관찰 카메라에서 실루엣이 읽힌다.
3. 앞·옆·지붕 근거가 확보되거나 결손을 일반화해도 오해가 적다.
4. 직접 촬영 또는 개별 권리 확인된 외관 자료를 쓸 수 있다.
5. 실제 상호·인물·차량번호를 복제하지 않아도 장소성이 남는다.

이 한 동에서 근거 묶음 → Blender → FBX → 표현 자원 대장 → Prefab → 안전 교체 → fallback의 전 과정을 닫은 뒤에만 3~5개 대표 건물로 늘린다.

## 구현 우선순위

1. 사가정 1km 건물 후보에서 독립 polygon과 관찰 가시성을 기준으로 3개를 추린다.
2. 각 후보의 도로명주소·건물관리번호·건축물대장·항공사진을 대조한다.
3. 사진 권리가 없는 후보는 `VisualReferenceOnly`로 남기고 모델 제작 후보에서 제외한다.
4. 승인 가능한 후보 한 동의 `StationLandmarkEvidence`와 모델 브리프를 동결한다.
5. Blender LOD 원본과 FBX를 만들고 재열기·scale·axis·hash를 검증한다.
6. 기존 Unity 표현 자원 대장에 `VisualKey`를 추가하고 Prefab을 만든다.
7. 대상 건물 하나만 안전하게 교체하고 세 가지 실패 조건—자원 누락, hash 불일치, overlay 판본 변경—에서 매스 복귀를 검증한다.
8. 실제 Play Mode·Game View에서 전체 보기, 역 확대, LOD 왕복, 선택, 해제 후 자원 회수를 따로 확인한다.

## 완료 조건

- 공식 공간 근거와 개별 시각 권리가 같은 안정 건물 식별자에 결속된다.
- 확인되지 않은 면·치수·재질이 `Estimated` 또는 `MissingVisualEvidence`로 남는다.
- Blender 원본과 FBX의 scale·axis·LOD·hash가 결정적으로 검증된다.
- Unity는 검증 성공 후에만 특정 절차적 건물 매스를 숨긴다.
- 자원 누락·hash/판본 불일치·bounds 오류 시 기존 매스가 다시 표시된다.
- 전체 도시를 재생성하지 않고 대상 안정 식별자의 자원만 생성·갱신·제거한다.
- 실제 Play Mode·Game View 증거와 코드·자동 시험 증거를 구분해 남긴다.

## 확정

- 기존 사가정 디오라마와 절차적 건물 뼈대는 보존한다.
- 사가정시장 입구·대표 골목은 첫 랜드마크 **공간 모듈** 후보로 유지한다.
- 공공 공간자료와 외관 사진의 권리·정밀도는 서로 다른 관문으로 관리한다.
- Unity는 읽기 전용 표현만 담당하며 모델 교체가 공간·업무·통행 권위를 만들지 않는다.

## 미정

- 첫 실제 건물 교체 표본의 정확한 건물과 `BuildingStableId`
- 직접 촬영을 할지, 개별 공공누리 제1유형 자료가 있는 후보를 먼저 택할지
- 첫 표본의 삼각형·재질·텍스처 해상도 상한
- 랜드마크 자원 대장을 기존 `WorldVisualCatalog`와 공간 조립 catalog 중 어느 adapter에 결속할지

## 다음 질문 하나

사가정시장은 입구·골목 overlay로 계속 두고, **첫 실제 건물 교체 표본은 polygon 하나에 명확히 대응하고 사진 권리를 확보할 수 있는 독립 건물 한 동으로 별도 선정할까?**

추천은 그렇게 하는 것이다. 시장의 장소성 작업은 보존하면서도, 여러 필지와 점포 권리 문제가 첫 Prefab 교체 파이프라인 검증을 막는 일을 피할 수 있다. 대가는 시장 자체가 첫 완전 교체 건물이 되지는 않는다는 점이다.

## 현재 검증 상한

현재 저장소의 사가정 건물·높이·도로 표현과 별도 Unity 저장소의 Blender 검증 구조를 정적으로 대조하고, 공식 주소정보·건축HUB·항공사진·공공누리 이용조건을 조사해 제안으로 정리했다. 새 사진 다운로드, 도로명주소/건축물대장 수집, 후보 건물 확정, Graph Map·배치맵 수정, Blender 모델링, FBX·Prefab 생성, Unity Scene·Play Mode·Game View 검증은 수행하지 않았다.
