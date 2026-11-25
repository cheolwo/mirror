# 행정동 디오라마 공통 바탕 첫 층 — 비공개 Unity 비교

2026-10-29. [실행 범위 r49](../AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/common-base-layer.private-prototype.r49.md), [전체 품질의 층별 누적 r48](../AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/area-wide-layered-quality.decision.r48.md).

## 실제 변경

사용자가 시안을 본 뒤 전반적으로 층위를 올리는 제작을 진행하도록 요청했다. 새 Editor-only helper와 shader를 만들어 기존 행정동 도형의 **표시 사본 전체**에 벽·지붕·도로·바탕의 공통 팔레트와 면 방향 음영을 적용했다. 특정 건물 ID별 예외·전면 모델·창 패턴·새 지붕 구조·도로 폭·보도·실제 사진/매장 정보를 추가하지 않았다.

같은 원본 Mesh를 쓰는 필터는 후보 Mesh 하나를 공유한다. 모든 입력을 먼저 검사하고 사본을 준비한 뒤 참조를 교체하며, 실패 시 정리하고 중복/겹친 적용은 거절한다. `Dispose` 뒤 원래 Mesh/Material 참조와 표시 결과로 돌아간다. 원본 정점·triangle·법선·접선·UV·색·Transform은 보존한다. 고정 스튜디오 명암이며 실제 재질·현장 일조·물리 그림자 비교가 아니다.

## 적용한 비교 범위

| 기존 역사 모듈 | 건물 | 도로 중심선 | 결합 Mesh | 정점 | triangle | 기존 내부 ring 보완 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 면목제3·8동 `1126057500` | 2,700 | 291 | 4 | 81,272 | 43,000 | r40의 1개 유지 |
| 면목본동 `1126056500` | 3,499 | 237 | 4 | 108,969 | 58,111 | 없음 |

공통 규칙이 두 모듈 전체의 건물6,199개에 적용됐고 geometry hash·정점/triangle 수와 원본 참조를 검증했다. 면목제3·8동의 전체·250m 연구 중심 근접·주변부, 면목본동의 전체·근접 비교까지5시점에서 동일 카메라의 before/after/restored **15PNG**를 만들었다. 250m 연구 중심은 원본 ENU를 기존 검토 사본의 중심/단위 변환에 맞춰 배치했다. 화면에 포함되는 건물 수를 연구 정사각형과 동일하다고 주장하지 않는다.

## 실행·시각 검토

최종 Unity CLI EditMode `editmode-r4.xml`은 **71/71, 실패0·생략0**이다. 신규 공통 바탕4개와 기존 행정동44개·내부 ring23개를 포함한다. 형상 속성·공유 관계·원본 보존, 중복/겹친 적용, 잘못된 뒤쪽 입력의 rollback, 복구와 반복 해제를 검증했다.

최종 graphics-r3는 종료0이며 C#/shader 오류·처리 예외 검색0건이다. source 입력3개와 기존 Scene/URP/Quality/package/dirty source 등12파일 지문이 보존됐다. 실행한 helper2개·shader1개의 지문도 결과 JSON과 일치한다. 모든 PNG의 크기1600×1000·SHA·CRC·전경/분산을 재검사했고5쌍 모두 before/restored SHA가 같다. PreviewScene 종료와 기존 active Scene·주변광 보존, 후보 Mesh/Material 제거도 확인했다.

root는 두 지역의 근접 화면을, 별도 읽기 검토는 전체·주변부를 직접 확인했다. 회색 바탕·따뜻한 지붕·밝은 벽과 방향별 음영이 분리되어 건물 높이차와 틈이 읽히며 두 동과 중심/주변의 표현 수준이 일관된다. 큰 누락·새 잘림·국소 상세 섬은 발견하지 않았다. **첫 공통 바탕층의 시각 후보로 수용**하며 사용자 승인이나 정본/게임 적용으로 확대하지 않는다.

## 초기 실패와 보정

- 첫 컴파일은 Unity6000.5의 폐기 API `GetInstanceID` 때문에 시험0건으로 실패했다. 객체 참조 집합으로 교체했고 실패 로그는 보존했다.
- 현재 Unity CLI는 `run`의 `-quit`을 직접 관리하므로 전달 인자에 넣은 첫 명령을 실행 전에 거절했다. 해당 전달 인자를 제거했다.
- graphics-r1은 shader의 정수 half literal `1h`를 거절했다. `1.0h`로 고치고 shader 오류 사전 검사를 추가했다. 빈/오류 결과 검사는 이 실행을 실패로 남겼다.
- 그래픽 확인 중 연구 ENU 중심을 검토 사본 좌표로 변환하지 않은 카메라를 발견해 기존 `SourceCenterX/Z·MetersPerUnit` 계약에 맞췄다. 최종 전후는 모두 같은 수정 카메라다.
- graphics-r2는 형상·복구 검사가 통과했으나 실제 화면에서 대비가 너무 낮아 건물 볼륨이 적용 전보다 평평해졌다. 이 시각 후보는 거절했다. 전체 팔레트와 고정 명암을 한 번 보정했고 graphics-r3를 다시 검증했다. 자동 이미지 유효성 통과와 시각 수용을 구분했다.

## 결과와 코드 위치

Unity 새 파일은 `Assets/Ssalddel/Editor/행정동공통바탕Style.cs`, `행정동공통바탕PreviewCapture.cs`, `Assets/Ssalddel/Shaders/행정동공통바탕.shader`, `Assets/Ssalddel/Tests/EditMode/행정동공통바탕StyleTests.cs`와 각각의 `.meta`다. 기존 제품/Unity source는 수정하지 않았다.

실행 출력은 Unity `artifacts/local/common-base-layer-r49/`다. 최종 결과는 `graphics-r3/result.private.json` SHA `3151367f99c7ca40082a17641d60db2c1918cf1c8aec508a2cc50c474723ffcf`, 시험은 `editmode-r4.xml` SHA `ce4a169e444288ca7732caa9508ba0118f3301d56b43aa78393d402e17ae0868`다. raw Unity 로그에는 실행 환경 인증 정보가 포함될 수 있어 비교 묶음에 복사하지 않았다.

Hongdal의 `artifacts/local/store-diorama-binding-20261029/common-base-layer-r49/`에는 입력·보존 원문 지문·검증/조립 script, `verification-graphics-r3.private.json`과 최종 `review-r2/index.html`을 보관한다. HTML은 PNG pixel을 수정하지 않고 동일 카메라 전후 슬라이더·제거 복구·모듈/시점 선택을 제공한다. 기존 거절 후보 `review-r1`은 덮어쓰지 않았다. 원본 권리의 비공개 상한에 따라 PNG는 public 문서 자산으로 옮기지 않았다.

로컬 비교 URL은 `http://127.0.0.1:8769/`다. HTML·대표 PNG의 HTTP200과 image/png를 확인했다. 이 HTTP는 사용자 PC의 이미지 비교용이며 게임/운영 API 검증이 아니다. HTML UI의 실제 브라우저 입력 자동 검증은 수행하지 않았다.

## 남은 범위

연결된 Editor/Pipeline가 없어 CLI `PreviewScene Camera.Render`를 사용했다. 실제 Play Mode·Game View·선택 입력·실시간 FPS·메모리 스트리밍·정본 반영·설치 파일은 이번 결과가 아니다. 측정한 render/readback 시간은 offscreen 실행 기록이며 게임 FPS로 쓰지 않는다.

균일한 지붕색과 단순 몸체의 첫 층이다. 실제 높이 결손·외관·창/지붕 구조·보행면·DEM·현재 매장 동일성은 그대로 남는다. 전체 조망의 큰 빈 바탕도 실제 공터나 현행 토지 상태로 확정하지 않는다. 다음은 이 공통 바탕 위에 전체에 재사용할 건물 표현 층의 후보를 준비하는 순서다. 새 WI/권위·E/G 승격·운영 DB·현재 공간 pointer·공개·commit/push는 없다.

기존 원천/표현 분리, 표시가 권위를 변경하지 않는 원칙, 절차적 매스 보존과 실제 화면 비교를 지지한다. graphics-r2 대비 문제는 이번 후보의 반례로 기록했고 기존 공통 규칙을 무효화하지 않는다. **새 디오라마 규칙 후보 없음.**

문서5경로 Fast/diff 검사(`artifacts/local/validation/20261029-174431/`), 새 문서 로컬 링크4개와 규칙 r5 Validate도 통과했다. 이 Fast는 문서 검사로 build/test를 생략했으며 위의 실제 Unity71시험·graphics-r3 검증과 별도다.
