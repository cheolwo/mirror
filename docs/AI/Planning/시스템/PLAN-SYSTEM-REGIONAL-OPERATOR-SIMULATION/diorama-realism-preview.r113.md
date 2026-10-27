# 기존 디오라마의 재질·조명 현실감 비교

[기획 · 비공개 디오라마 그래픽 개선 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r113]

- 날짜: 2026-10-26
- 상태: `ApprovedByDirectUser / PrivateRealismPreviewRendered / Focused111Passed / CanonicalScenePreserved / GameViewNotVerified / OperationalDisabled`
- 직접 요청: 기존 디오라마를 그래픽적으로 더 현실적으로 만들고 싶다는 사용자 요청.
- 기준: [같은 영상의 선분·건물 비교 r112](video-segment-diorama-visual-demo.r112.md)의 0111 원본 근거·후보 도로 3개·GIS 연구 건물 3개·미리보기 겹침 mask를 그대로 사용한다.

## 좁은 구현 범위

새 opt-in Editor 표현 helper와 집중 시험, 필요한 별도 shader, 기존 통합 시안 Capture의 최소 연결만 허용한다. 기본 시안과 원본 Mesh/Material/Prefab/Scene은 보존한다. 새 사본의 노면·외벽 미세 질감, 유리 반사, 조명·그림자·주변광으로 개선한다. 설치된 Synty 재질은 실제 현장 사진 기반 자산으로 표시하지 않으며 자료에 없는 새 건물 높이·창문 개수·뒷면을 실측 사실로 만들지 않는다.

미터 기준 UV·surface code·vertex color와 기존 창 패턴을 유지한다. 같은 정사영 시선·화면 크기로 기존/개선/제거 복구를 비교한다. 물리 그림자의 80m 범위 때문에 카메라 거리를 바꾸면 전후 거리·정사영 투영 조건을 기록하고 '동일 카메라 위치'라고 보고하지 않는다. 기존 글로벌 URP 설정은 바꾸지 않고 필요한 임시 자원·RenderSettings는 finally에서 복구한다.

허용 쓰기 경로는 Unity `Assets/Ssalddel/Editor/사가정비공개현실감Style*`, 같은 기능의 새 shader·EditMode 집중 시험 및 `.meta`, 기존 `사가정영상선분디오라마DemoCapture.cs`의 opt-in 연결, 배달 `edit-kit/private/diorama-realism/20261026-r1/`의 파생 근거·실행 입력·렌더·비교 자료, 본 PLAN·최신 문서 포인터와 Changes다.

## 검증과 보존

관련 EditMode의 실제 비어 있지 않은 XML, Unity 그래픽 CLI PreviewScene Camera.Render의 대표 4시점 이미지, 밝기/전경·전후 차이·자원 제거·원래 장면/설정 복구와 오류 로그를 확인한다. 원본 근거·분석·승인·Premiere·운영 DB·실제 지도·원본 URP/품질 설정·공개 상태는 변경하지 않는다. 현재 Editor/Pipeline 연결은 없으므로 실제 Game View·Play Mode·FPS와 offscreen 렌더를 구분한다.

실제 높이/정면·사진 기반 외관 재구성·전체 경로/GPS 확정은 별도다. 새 패키지 설치·다운로드·새 공식 Scene·자동 게시·운영·commit/push는 포함하지 않는다. 이번 결과는 실제 Unity가 만든 전후 이미지로 보여주며 AI 생성 컨셉 이미지를 실행 결과로 대체하지 않는다.

## 표현 근거

기존 shader·설치된 URP 17.5.0의 Lighting/SurfaceData·현재 PC renderer를 먼저 대조했다. Unity 공식 [Lit 재질의 표면·반사 설정](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lit-shader.html)과 [화면 공간 주변광 차폐](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing-ssao.html)를 참고한다. 새 shader의 실제 컴파일·그림자 보임은 공식 설명만으로 성공 처리하지 않고 이번 실행 이미지와 로그로 확인한다. 기존 Synty 자산은 stylized이고 HDRI는 없으므로 범용 코드 생성 미세질감·낮 환경 반사를 사진 기반 자산으로 표현하지 않는다.

## 마감

실제 Unity 집중 시험 111/111과 graphics r2 종료 0, 20 PNG의 전후 차이·두 종류 복구 동일 SHA·장면 정리·원본 14파일 불변을 확인했다. 같은 구도의 전후 합성 4장·16초 무음 MP4 전체 디코딩·비교 HTML의 17개 자원 경로를 검사했다. 와플·맥주 카메라만 안전하게 65m로 옮겨 실제 그림자를 확인했고 전체·뚜레는 앞쪽 도형이 잘리지 않도록 1,600m를 유지했다. 이 두 시점은 주광 그림자 80m 범위 밖이며 네 시점 모두 같은 그림자 품질을 보인다고 보고하지 않는다.

결과와 초기 실패·수정·남은 가는 줄그림자/노면 겹침/실측 결손은 [r113 변경 기록](../../../../Changes/2026-10-26-diorama-realism-r113.md)을 따른다. 새 공통 디오라마 규칙 후보 없음. 실제 Game View·FPS·정본/운영·공개·commit/push는 수행하지 않았다.
