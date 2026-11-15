# 사가정역 디오라마 제작 방식 추출과 지역 재사용

[기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r50]

- 요청: 2026-10-29. 다른 지역에 적용하기 전에 사가정이 어떻게 만들어졌는지 먼저 추출하고 반복 조사 비용을 줄인다.
- 상태: `ConstructionRecipeExtracted / SourceAndCatalogVerified / ExtractionOnly / NoNewUnityExecution`.
- 범위: 현재 Unity 작업 트리의 코드·Resource·카탈로그와 기존 결과 문서의 정적 대조. 이번에 화면 실행·새 구현·자산 수집은 하지 않았다.
- 빠른 재사용 입력: [기계 판독용 제작 명세](sagajeong-construction-recipe.r1.json). 이 문서와 JSON의 source SHA를 먼저 비교하고, 같으면 원본 전체를 다시 탐색하지 않는다. 달라진 파일과 해당 제작 단계만 재확인한다.

## 먼저 밝혀진 차이

**사가정은 단일 Synty 화면이 아니다.** `사가정운영디오라마View`가 입력·높이·공간층을 읽은 뒤 다음 두 경로 중 하나를 고른다. 실제 실행 중 선택된 모드는 이번 정적 추출만으로 확정하지 않는다.

| 구현 경로 | 어떻게 만드는가 | 적용 상한 |
| --- | --- | --- |
| Synty 건물 경로 | 원문 건물에 맞는 `PolygonCity` Prefab을 골라 축척·회전·높이를 맞추고, 불일치 건물은 원형 Mesh로 남긴다. | `requested && !spatialOverlayApplied`일 때만 사용. 최신 공간층과 무조건 함께 켜지지 않음 |
| 절차적 모형 경로 | 원본/공간층에서 바탕→도로→건물→선택적 녹지·그림자·근접 상세를 결합 Mesh로 만든다. 창 상당수는 shader 격자다. | 최신 공간층을 보존하는 경로. 장식·축소·창/도로 폭은 실측 사실과 구분 |
| 세 점포 사진 참조 연구 | 별도 opt-in Editor 연구에서 확인된 세 대상의 차양·전면·문틀·재질을 비교한다. | [r114](../PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/detailed-building-representation.r114.md)와 후속 r115의 좁은 비공개 범위. 사가정 정본 전체에 적용됐다고 합치지 않음 |

다른 행정동의 기본 제작기는 `행정동디오라마검토MeshBuilder`다. 2×2 결합 Mesh·2.4m 기호 도로·벽/평지붕 압출 중심이며 사가정의 창 shader·도로 접속·차양·근접 상세가 자동 연결되지 않는다. [r49](common-base-layer.private-prototype.r49.md)는 여기에 공통 색/면 방향 음영만 시험한 첫 층이다. **건물 수가 많아도 화면의 표현 층은 적을 수 있다.**

## 입력과 제작 순서

| 순서 | 기존 입력/처리 | 실제 역할 |
| --- | --- | --- |
| 1. 지역 원본 | `SagajeongReference`, 장소 배치, 높이 보완, 도로명, 역 방향 기준점 | 기본 r3 Resource는 반경표현의 halfExtent500m·offset(550,8), 건물602·도로 선분2397. 이는 기본 입력 수량이며 공간층 적용 뒤의 현재 화면 수량은 아님 |
| 2. 검증·공간층 합성 | `사가정공간표현OverlayPipeline` / `OverlayComposer` | 원문 SHA·좌표계·중심·기준일·권리/사용 조건을 검사하고 높이/건물/표면을 합성. Composer에는 사가정의 원본 hash·좌표가 고정돼 있어 그대로 다른 동에 호출할 수 없음 |
| 3. 모드 선택 | `사가정공간표현ModePolicy.UseLegacySynty` | 공간층이 적용되면 legacy Synty 건물 교체를 끄고 모형 경로를 사용 |
| 4. 공통 조립 | `사가정모형MeshBuilder.BuildBatches` | 4×4 공간 묶음, opaque/shadow/station-detail 분리. renderer 상한128·material 상한12를 검사; 상한과 실제 실행 수량은 다름 |
| 5. 표시 | `사가정모형명암.shader`, 그림자 shader, 정사영 카메라 | 벽/지붕 구분·격자 창·접촉 음영·표면 변화를 표시. 원본 건물의 실제 창 개수·실제 층수 정보는 아님 |
| 6. 선택·관찰 | H공간 선택·역 방향 표식·역할 카드·합성Actor | 건물별 근거와 관찰 상태를 결속. 합성 기사는 실제 기사 위치가 아니며 legacy Synty 경로에서는 H건물 선택이 보류됨 |

공개 Resource 이름은 `SagajeongSpatialPresentationOverlay`/`...Approval`이지만, 현재 loader의 승인 hash 목록은 비어 있다. Editor 비공개 입력 경로 키는 `SSALDDEL_SAGAJEONG_PRIVATE_OVERLAY`다. 이 때문에 공간층이 소스에 있다는 사실을 현재 화면·배포 앱에서 활성화됐다는 사실로 바꾸지 않는다.

## 화면 밀도를 높이는 공통 제작 규칙

- **도로:** 종류별 표현 폭, 양옆 포장 띠, 주요도로 표시, node 접합 원판으로 분리 선분의 틈을 메운다. 실제 보도·도로 실폭 검증과는 다르다.
- **건물:** 높이대·용도·고유 ID hash로 벽/지붕 색을 결정한다. 24m 이상 모형은 상부를0.84배로 줄이는 표현도 있다. 이 축소를 실제 건물 형상으로 이식하지 않는다.
- **창:** 외벽 `surface.x=1`에 월드 좌표 격자로 창 무늬를 그린다. 창마다 Mesh/Renderer를 만들지 않아 반복 표현 비용이 낮다.
- **지붕·차양:** 역 중심135m 안에 테두리·조건부 차양·소형 옥상 표현을 보탠다. 현재 사용자의 [전체 품질 층별 누적 r48](area-wide-layered-quality.decision.r48.md)에 적용할 때 이 역 중심 전용 상세 반경을 그대로 복제하지 않는다.
- **그림자:** 높이에 비례한 분석적 그림자 길이를 최대8m로 제한하고 본그림자/가장자리를 나눠 표시한다. 실제 일조·현장 그림자 측정값이 아니다.

## 실제 Synty 연결

현재 `SagajeongSyntyVisualCatalog.asset`의4개 Prefab GUID를 설치된 원본 `.meta`와 대조해 모두 일치함을 확인했다. 경로는 `Assets/Synty/PolygonCity/Prefabs/Buildings/`다.

| VisualKey의 역할 | 실제 Prefab |
| --- | --- |
| apartment | `SM_Bld_OfficeSquare_01` |
| shop | `SM_Bld_Shop_03` |
| office | `SM_Bld_OfficeOld_Small_01` |
| civic | `SM_Bld_Shop_05` |

이름의 apartment/civic은 프로젝트의 표시 역할이며 실제 아파트/공공청사 동일성 증명이 아니다. Planner는 중심·긴 변 방향·외곽 크기·표시 높이에 맞춰 후보를 검사한다. 균등 축척0.8~1.25, 높이 오차10% 이내, 세로 복제 최대24라는 기존 기준을 쓰며 View의 일반 호출은 후보 최대120개다. 150m/13개 전용 시범 분기도 별도로 있다. 이 수치들은 추출한 현행 정책이며 다른 지역의 승인 기본값이 아니다.

Ready 후보만 생성하고 공유 Prefab/Material·색 보정을 사용한다. 복제물의 Collider를 제거해 표현 전용으로 둔다. 이 건물 카탈로그는 차량·인물·실제 상호를 공급하지 않는다.

## 다른 지역에 재사용할 최소 단위

1. **공통 바탕 유지:** r49의 전체 재질/명암 비교 위에 다음 층을 더한다.
2. **표면 계약과 창 shader:** 행정동 제작기에 벽/지붕의 명시적 표면 구분과 표현용 UV를 연결하는 작은 Adapter부터 검토한다. 사가정 `View` 전체 복사와 source hash 교체로 시작하지 않는다.
3. **공통 건물·거리 표현:** 지붕/벽·도로 접합을 같은 범위 전체에 고르게 적용할 후보로 분리한다. 실제 형상·실폭과 장식을 구분한다.
4. **선택적 Synty:** 외곽/높이 적합 검사를 재사용하되 잘 맞는 곳만 사용하고 원형 복귀를 유지한다. 전체 건물을 Prefab으로 강제 대체하지 않는다.

지역 입력으로 바꿀 항목은 **고유 ID, 경계/창, 중심·offset·CRS, 원문/Resource hash, 건물·도로·표면·높이와 결손, 장소/역 기준점, 대응 키, 사용 조건·공개 상태, 자산/성능 예산**이다. 공통 알고리즘과 지역 값의 분리가 재사용 작업의 핵심이다.

## 원문 탐색 위치

모두 Unity `Assets/Ssalddel/` 기준이며 정확한 작업 트리 SHA는 JSON에 있다.

| 파일:행 | 확인할 제작 단계 |
| --- | --- |
| `Presentation/World/사가정운영디오라마View.cs:135` | 입력과 두 모드의 결속 |
| `Presentation/World/사가정공간표현ModePolicy.cs:8` | Synty/공간층 배타 조건 |
| `Runtime/World/사가정공간표현OverlayLoader.cs:147` | 비공개/승인 Resource의 사용 조건 |
| `Presentation/World/사가정모형MeshBuilder.cs:188` | 조립 순서·4×4 묶음 |
| 같은 파일 `:339`, `:441`, `:525` | 도로 접합, 건물/그림자, 역 주변 상세 |
| `Resources/사가정모형명암.shader:85` | 개별 Mesh 없는 창 무늬 |
| `Editor/사가정Synty표현CatalogBuilder.cs:24` | Synty4종 매핑 |
| `Presentation/World/사가정Synty표현Planner.cs:171` | 원형/높이 적합·fallback |
| `Presentation/World/행정동디오라마검토MeshBuilder.cs:260` | 다른 행정동의 기본 도로/건물 |

## 확정·미정·다음 질문

- 확정: 현재 제작 방식을 두 경로·입력·밀도 규칙·재사용 단위로 추출했다. 새로운 렌더/기능/지역 적용은 이번 요청에 포함하지 않는다.
- 미정: 사용자가 비교한 특정 실행 화면의 활성 모드·공간층 판본, 다음 공통 Adapter의 구현 명세·자료/성능 검증.
- 다음 질문 하나: 지금은 추출 결과를 전달한다. 다음 지역 적용을 시작할 때 이 명세에서 작은 공통 층 하나를 선택한다.

기존 자료/표현 분리·지역 값 소유·교차지역 fallback 금지 원칙을 지지하며 새 공통 규칙으로 승격하지 않는다. **새 디오라마 규칙 후보 없음.**

검증: 명세 JSON 재조회·25원문 SHA25/25 일치·Synty Prefab GUID4/4·새 로컬 링크4개·문서5경로 Fast/diff·규칙 r5 Validate 통과. Fast 로그는 `artifacts/local/validation/20261029-175916/`이며 문서 작업으로 build/test를 생략했다. 새 Unity 실행 근거는 만들지 않았다.
