# 공통 바탕 첫 층의 비공개 Unity 그래픽 연구 구현

[기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r49]

- 직접 실행 요청: 2026-10-29 — r48 시안을 본 사용자가 전반적으로 층위가 올라가는 방식으로 진행하도록 요청했다.
- 선행: [범위 전체의 층별 상향 r48](area-wide-layered-quality.decision.r48.md).
- 역할과 범위: Unity 비공개 그래픽 연구 구현. 기존 도형의 메모리 사본에 공통 바탕 표현을 시험하며 게임 행위·Simulation·정본 Scene을 구현하는 작업이 아니다.
- 상태: `PrivateLayer1Implemented / Focused71Passed / UnityPreviewRendered / RestoreVerified / RuntimeAndCanonicalNotApplied`.

## 이번에 실제 만드는 결과

면목제3·8동 기존 모듈 전체와 면목본동 비교 표본에 같은 첫 층을 적용한 **실제 Unity PreviewScene 렌더**를 만든다. 창·지붕 구조·새 건물·도로 폭·출입구·상호·사진은 추가하지 않는다. 첫 층은 공통 기본 팔레트, 벽/지붕/도로/바탕 구분과 면 방향에 따른 스튜디오 음영이다. 이 음영을 현장 일조·실제 물리 그림자로 부르지 않는다.

기존 생성기의 Mesh·정점/triangle·법선/UV·Transform·ID·높이와 숫자/기호 출처는 유지한다. 표시용 Mesh의 색·Material만 사본으로 바꾸고 제거하면 원래 참조·재질로 돌아간다. 전체·근접·주변부 카메라는 전후/복구에서 동일하게 둔다. 공통 helper에는 지역 ID별 예외나 특정 점포의 상세 외형을 넣지 않는다.

## 허용 쓰기 경로와 보존

- Unity 새 Editor helper: `Assets/Ssalddel/Editor/행정동공통바탕Style.cs`, `행정동공통바탕PreviewCapture.cs`와 각 `.meta`.
- Unity 새 shader: `Assets/Ssalddel/Shaders/행정동공통바탕.shader`와 `.meta`.
- Unity 집중 시험: `Assets/Ssalddel/Tests/EditMode/행정동공통바탕StyleTests.cs`와 `.meta`.
- 비공개 실행 입력·원문 지문·결과: Hongdal `artifacts/local/store-diorama-binding-20261029/common-base-layer-r49/`와 Unity `artifacts/local/common-base-layer-r49/`.
- 현재 PLAN/기획 목차·snapshot·변경 기록과 이전 문서의 현행 포인터.

기존 dirty source와 Scene/Prefab/Material/URP/Quality/package·운영 DB·현재 공간 pointer·공개 상태를 보존한다. 다운로드·패키지 설치·새 공식 Scene·commit/push는 없다. 같은 Unity checkout을 사용하는 Editor가 없는지 먼저 확인했고 CLI `status`는 `STATUS_NO_INSTANCES`다. 연결된 Pipeline 도구가 없으므로 graphics CLI 경로를 쓴다.

## 연구와 제품 구현의 경계

사용자 요청으로 이번 **첫 층 연구 시제품의 제작과 비교**를 진행한다. 미완성 건물·공간 연구 전체를 `Accepted`로 올리거나 WI/E1~E7·정본 구현 관문을 우회하지 않는다. 현재 결과는 연구용 Editor 코드/새 shader와 격리된 PreviewScene의 이미지다. 실제 Play Mode·Game View·선택 입력·실시간 FPS·제품 배포·E 승격은 별도다.

## 검증 계획

- source Mesh/Material 공유·오류 rollback·중복 적용 거절·Dispose 복구의 집중 EditMode 시험과 관련 기존 행정동 시험.
- 원문 입력 SHA, 전체 건물·batch/triangle 수와 전후 geometry hash 불변, 원본 Mesh/색·재질 불변.
- 동일 카메라 before/after/restored PNG, 전경/명암 분산·이미지 변화와 before/restored SHA 동일성.
- 새 자원 제거, PreviewScene 종료, 원래 active Scene·원본 Scene/설정/source 입력의 지문 보존.
- 실행 시간은 CLI/offscreen 측정으로 기록하고 실시간 FPS로 표시하지 않음.

## 미정과 다음 질문

- 확정: 시안에 따른 공통 바탕 첫 층의 비공개 연구 제작을 진행한다.
- 미정: 실제 렌더의 판독 품질·팔레트 조정과 원형 보존/복구 검증 결과. 다음 표현 층과 정본 적용은 이번 범위가 아니다.
- 다음 질문 하나: 먼저 실행 결과를 만든다. 시안과 실제 렌더를 비교한 뒤 구체적인 차이가 있으면 그 선택만 검토한다.

새 디오라마 규칙 후보 없음. [최종 구현·시험·전후/복구 결과](../../../../Changes/2026-10-29-administrative-dong-common-base-layer-r49.md)에 실행 근거와 남은 범위를 기록했다.

## 실제 진행과 발견

새 helper·shader·집중 시험을 구현했고 최신 `editmode-r4.xml`은 신규4개와 기존67개, 총71/71·실패/생략0이다. 첫 컴파일의 `GetInstanceID` 폐기 API 오류는 객체 참조 집합으로 바꿨고, 첫 graphics의 half literal 문법 오류도 수정했다. 실패 실행은 별도로 보존했다.

graphics-r2는 두 모듈의 형상·복구·15PNG 무결성은 통과했지만, 직접 화면 검토에서 새 팔레트의 대비가 약해 건물 볼륨이 적용 전보다 평평하게 보였다. 이 판본은 시각 개선으로 수용하지 않았다. 바탕/벽/지붕의 공통 색과 방향별 음영을 전역 보정한 graphics-r3에서 높이·건물 틈·면 방향이 판독되는 것을 root/독립 검토로 확인했다. 대상별 예외·새 형상·광역 품질 섬은 추가하지 않았다.

최종 결과는 두 모듈6199건물·결합Mesh8개·5시점15PNG다. 원형/원문12파일·입력3개·실행코드3개 지문과 모든 before/restored PNG 일치를 독립 재조회했다. 비교 HTML은 `artifacts/local/store-diorama-binding-20261029/common-base-layer-r49/review-r2/index.html`, 로컬 URL은 `http://127.0.0.1:8769/`다. 첫 층의 연구 시각 후보로 수용하며 실제 Game View·제품/운영 적용·FPS는 별도다.
