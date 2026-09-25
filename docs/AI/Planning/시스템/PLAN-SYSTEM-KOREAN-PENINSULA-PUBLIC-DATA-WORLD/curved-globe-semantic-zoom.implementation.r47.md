# 지구 곡면 연속 의미 확대 구현 r47

## 목적

지구본 확대 중 별도의 직사각형 사진판이나 권역판이 튀어나오지 않게 하고, 같은 지구 곡면에서 한반도 자료층이 단계적으로 드러나도록 기본 확대 경로를 교체한다.

## 구현

- `한반도지형미리보기View`를 `GlobeVisualRoot`의 자식으로 결속했다. 지구본 회전과 한반도 지형층은 같은 구체 좌표계를 사용한다.
- 카메라 거리로 계산한 `ZoomBlend`에 따라 해안 윤곽, 지형 음영, 검증 하천의 투명도를 연속 조절한다.
- Natural Earth 지형 이미지를 평면 Quad에 표시하지 않고 위경도 격자를 구면 좌표로 변환한 곡면 Mesh에 투영한다.
- 지형 음영 Mesh는 동결된 세계 국가 경계 Catalog에서 `KR` 또는 `KP`로 판독되는 셀만 삼각형을 생성한다. 따라서 원본 이미지의 직사각형 경계와 주변 중국·해양 부분을 한반도 지형으로 노출하지 않는다.
- 투명 배경의 하천층은 국경에서 잘리지 않도록 별도의 곡면층으로 유지한다.
- 기존 `RegionalTerrainBoard`와 0.75초 평면판 전환은 현행 기본 경로에서 제거했다. r42·r44는 시도 이력과 회귀 근거로만 보존한다.
- 카메라·선택·정보 패널은 읽기 전용 Presentation이며 운영·Simulation·이동 가능 지형 권위를 만들지 않는다.

## 검증

- Unity 재컴파일: 오류 없음.
- 관련 EditMode: `세계지구본Tests` 21/21 통과.
- 실제 `SimulationWorldShell` Play Mode: `Hero=True / Stage=Rivers / Blend=0.75 / Curved=True / Vertices=7081`.
- 전용 지구본 카메라 Game View에서 한반도 지형 음영이 남북한 육지 곡면에만 표시되고 직사각형 지형판이 생성되지 않는 것을 확인했다.
- 대표 캡처: `Assets/Documentation/Changes/2026-09-21-korean-peninsula-terrain-preview/game-view-curved-semantic-zoom-r45.png` (별도 Unity 저장소의 파일명은 화면 증거 생성 순서를 보존한다.)
- 기존 Scene의 서버 세션·Replay·비활성 부모 변경 오류가 남아 있어 Console 0 또는 통합 E5 이상은 주장하지 않는다.

## 남은 범위

- 현재 지형은 음영 텍스처의 곡면 투영이며 실제 DEM 높이 Mesh가 아니다.
- 지구 곡면에서 대한민국 행정 권역 tile, 로컬 ENU 창, 사가정 아이소메트릭 디오라마로 넘어가는 다음 확대 관문은 아직 연결하지 않았다.
- 현행 국가 경계 정밀도보다 더 부드러운 해안 마스크와 거리별 LOD는 후속 최적화 후보다.
- Scene 저장, commit, push는 수행하지 않았다.
- 새 디오라마 규칙 후보 없음.
