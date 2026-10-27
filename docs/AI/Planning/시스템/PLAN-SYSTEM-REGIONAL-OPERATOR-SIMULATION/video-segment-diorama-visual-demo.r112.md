# 같은 영상의 선분 후보와 외관을 함께 보는 비공개 디오라마 시안

[기획 · 영상→선분→디오라마 시각 검증 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r112]

- 날짜: 2026-10-26
- 상태: `ApprovedByDirectUser / PrivateIntegratedDemoRendered / Focused106Passed / CanonicalScenePreserved / GameViewNotVerified / OperationalDisabled`
- 직접 요청: 기존 업로드 영상을 분석한 뒤 선분화하고 디오라마까지 적용한 모습을 한 번 보고 싶다는 사용자 요청.
- 이전 근거: [r109 외관 연구](video-facade-refinement.implementation.r109.md), [r111 실제 시각·결과 연결](video-diorama-daily-connection.implementation.r111.md).

## 이번에 보여줄 한 사례

0111의 사가정 공개 거리 외관 관측과 실제 PTS 사진을 재사용한다. 같은 원본의 선분 후보·지도 판본·노드/도형·공개 건물 3개의 원형·외관 관측·시안 모듈을 새 입력 manifest에 고정한다. 원본 구간과 선분 후보 도식, 주변 건물을 포함한 연구 시안의 전후·제거 복구를 한 검토 자료에서 비교한다.

공통 관측창에 대조된 선분 후보는 빨간 후보선으로 표시하되 선분별 통과 시각·실제 차량 GPS·정밀 주행 재현으로 만들지 않는다. 실측 높이·정면은 미확정이며 4m 기호값과 도로 가까운 면의 정면 가설을 기록한다. 간판은 직접 관측한 상호의 기존 정적 PNG를 쓰고 사진/폰트 원본·보이지 않는 외관을 복제하지 않는다.

## 허용 경로와 권위

- Unity 새 Editor 통합 시안 캡처 class와 `.meta`, 필요한 집중 시험만 변경한다. 기존 공간 MeshBuilder/Composition·외관 Profile/Gate/Renderer·검토 모듈을 재사용한다.
- 배달 `edit-kit/private/video-diorama-demo/20261026-r1/`의 새 근거·입력·파생 비공개 영상/도식·렌더·검증·보고, 원본/선분/보호 검증의 좁은 보조만 허용한다.
- 기존 Overture 2,247건물은 변경하지 않고, 직접 대응이 없는 GIS 3개는 정확 source ID/도형의 별도 연구 레이어에 놓는다. 근접 Overture 건물에 대신 적용하거나 정본 적용으로 보고하지 않는다.
- 현재 실제 Editor/Pipeline 연결·Unity MCP는 없음을 확인했다. 그래픽 CLI의 PreviewScene 카메라 렌더를 먼저 검증하며 Game View·Play Mode 입력 검증과 구분한다. `-nographics`는 쓰지 않는다.

원본·기존 분석/번호순 포인터·사용자 승인/컷·마커·현재 지도·Scene·EditorPrefs·Synty 원본 자산·운영 DB·공개 상태는 보존한다. 새 공식 Scene·운영·모델 학습·자동 게시·commit/push는 범위 밖이다. 이번 직접 요청은 위 시각 시안 범위에서만 Unity 실행을 허용한다.

## 실제 완료 기준

입력·원본 stat/이전 SHA 근거·실제 PTS·프레임·최신 보호창·선분/공개 건물 도형/ID·공간 판본을 실행 전후 대조한다. 새 카메라 렌더의 비어 있음·전후 차이·제거 뒤 복구·자원 정리·Console 오류를 검사하고 대표 이미지를 직접 본다. 원본 구간을 사용할 때 원음은 제외하고 실제 PTS 입출점을 유지하며 파생본의 디코딩·길이·프레임 수를 별도로 검증한다.

## 마감

같은 0111 원본의 기존 실제 PTS 사진 5개·선분 후보 3개·공개 건물 3곳을 고정했다. 실제 PTS로 선택한 9.042초/271프레임 무음 파생 구간과 주변 도로/기존 건물이 포함된 Unity 시안의 전후·실제 제거 복구 12장을 만들었다. 106/106 집중 회귀·12개 비어 있지 않은 이미지·4시점의 before/restored 동일 SHA·cleanup/active Scene 보존을 확인했다. 영상 근거와 시안 7장을 약 32초(헤더 표시 31.97초) 무음 검토 MP4로 묶어 전체 파생 디코딩과 대표 프레임을 검사했다.

첫 그래픽 실행에서 기존 baseline602 중 3개와 연구 GIS 도형이 겹쳐 줄무늬가 생겼고 복구 PNG의 1픽셀 차이도 검증을 막았다. 기하 충돌만 근거로 한 명시적 미리보기 mask로 원본 불변 사본의 legacy3개만 제외하고 카메라 2회 안정화 후 strict 동일 SHA 복구를 통과했다. Overture2247 자체와 원본 baseline은 그대로이며 기하 충돌 처리를 동일 건물 권위로 승격하지 않았다. 일부 교차로 노면의 가는 줄무늬는 남아 있다.

[실제 결과·검증과 한계](../../../../Changes/2026-10-26-video-segment-diorama-demo-r112.md), 비공개 결과는 배달 `edit-kit/private/video-diorama-demo/current-review.json`을 따른다. 연구 외관 반영 3건·정본 적용 0건이다. 실제 Game View·플레이 조작·전체 주행 경로·GPS·실측 높이/정면·공개 검수·배포·commit/push는 수행하지 않았다. 새 공통 디오라마 규칙 후보 없음.
