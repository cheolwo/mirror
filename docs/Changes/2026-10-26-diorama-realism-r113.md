# 디오라마 재질·조명 현실감 비교

2026-10-26. [직접 사용자 요청에 따른 비공개 표현 범위 r113](../AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/diorama-realism-preview.r113.md).

## 구현

기존 영상 0111의 선분 후보 3개·외관 연구 3곳과 지도·겹침 mask를 그대로 사용한다. 새 `사가정비공개현실감Style`과 전용 PBR shader를 `realism-r1`에서만 실행한다. 기존 shader의 정점색·surface code·창 격자를 유지하고 메모리에서 만든 저대비 미세질감/normal 6개, 거칠기·유리 반사·주광·보조광·로컬 반사 프로브를 새 재질 사본에 연결했다. 원본 사진·HDRI·실측 재질을 사용한 것처럼 표시하지 않는다.

카메라는 같은 시선축·정사영 크기를 유지하며 기존 화면에 걸친 실제 삼각형과 읽을 수 없는 mesh의 보수 bounds를 검사한다. 그림자 범위에 들어갈 수 있는 65m 이동이 화면을 자르면 원래 1,600m 위치를 유지한다. 요청한 그림자와 실제 화면에서 확인한 그림자를 구분하며 기존 URP asset을 바꾸지 않는다. 투명 간판 PNG와 후보선은 재질 교체에서 제외했다.

기본 12장에 외관 활성 상태의 스타일 4장과 스타일 제거 복구 4장을 더해 비교한다. 이 때문에 재질 변화 비교는 외관이 없는 before가 아니라 기존 외관이 있는 after와 style-after를 사용한다. 최종 정본 Scene·원본 Mesh/Material·지도/승인·운영 데이터는 보존한다.

## 검증

- 첫 실행은 새 helper의 `.meta` GUID 33자리 오류로 asset이 무시돼 컴파일에 실패했다. 시험 0건인 실패 판본을 보존하고 해당 GUID를 32자리로 고친 뒤 신규 meta 3개의 형식과 전체 Assets의 중복 없음을 확인했다.
- 새 실제 Unity graphics EditMode r2 XML은 **110/110**, 실패·생략 0이다. 신규 질감/카메라/원참조 복구 4개와 기존 통합 Demo·외관·공간·Synty·H선택 106개를 포함한다. `ssalddel/artifacts/local/diorama-realism-r2/editmode.xml`을 따른다.
- 첫 graphics 실행은 20 PNG 생성과 두 종류의 exact SHA 복구를 통과한 뒤 결과의 `Vector3.normalized`를 JSON.NET이 따라가며 순환참조 예외로 종료했다. 이미지 생성과 전체 실행 성공을 구분하고 실패 출력은 보존했다. Unity 저장 필드만 직렬화하는 경계로 고치고 실제 카메라 위치의 왕복 시험을 추가했다. 첫 시각 검토에서 일부 지붕이 과하게 밝아 주광을 1.7→1.15로 낮췄다. 가는 그림자는 기존 기호형 geometry의 한계로 남기며 없다고 표시하지 않는다.
- 수정 뒤 새 graphics EditMode r3는 **111/111**, 실패·생략 0이다. 직렬화 회귀 1개가 추가됐으며 최종 XML은 `ssalddel/artifacts/local/diorama-realism-r3/editmode.xml`이다.
- 최종 graphics r2는 종료 0, 20 PNG의 전경/밝기·전후 차이·기본/스타일 두 종류 제거 복구의 정확한 SHA 동일·preview 장면 닫힘·원래 active Scene 보존을 통과했다. 실제 shader/컴파일/처리 예외는 최종 로그에서 발견하지 않았다. 초기 실패와 로그 경고는 별도 보존한다.
- 실제 재질 사본 renderer 78개·투명 간판 보존 3개·코드 생성 질감 6개·프로브 1개다. Dispose 뒤 renderer 원참조·주광·카메라 복원 true다. 기존 Scene·지도·shader·URP/Graphics/Quality·package·대표 Synty Material 등 **14파일 hash 불변**, 같은 원본의 현재 증빙/보호 재검사를 통과했다.
- 실제 거리 검토에서 전체·뚜레 카메라는 화면 자름을 피하기 위해 1,600m를 유지하고, 와플·맥주만 같은 시선축·정사영 화면 크기의 65m 위치로 옮겼다. 가까운 두 화면에서는 cast shadow가 보이며 먼 두 화면은 URP 80m 그림자 범위 밖이다. 전체 그림자를 해결했다고 표시하지 않는다.
- 네 시점의 기존 after/개선 이미지를 직접 보았고 같은 구도의 합성 4장과 480프레임·16초 무음 비교 MP4를 만들었다. MP4 전체 프레임 디코딩과 대표 합성의 한글/배치, HTML 자원 경로 17개를 확인했다. 실제 브라우저 slider 조작·Game View는 별도다.

최신 비공개 포인터는 배달 `edit-kit/private/diorama-realism/current-review.json`이다. 최종 비교 자료는 `20261026-r1/comparison-r2/디오라마-현실감-비교.html`과 `디오라마-현실감-전후비교.mp4`, 결과 `result.private.json` SHA `fe61aea04e95d906688980d208ac246a8b4e9649b7dc7c061f1486c307c103ad`를 따른다. MP4 SHA는 `58293b75c4b89390a927c4eacf9d3ac2764521e13fb006e2833c9709e6cbd372`다. 원본 사진은 새 텍스처로 복사하지 않았으며 자료는 로컬 비공개 경로에 보존한다.

## 범위와 다음 작업

이번은 실제 Unity PreviewScene 카메라의 비공개 그래픽 시안이며 Game View·Play Mode·조작감·FPS·운영/공개 검증과 구분한다. 건물 높이 4m와 정면 방향은 가설이며 정본 적용 0건이다. 사진 기반 외관 재구성, 실측 층수·높이·지붕·실제 창문 배치·실내는 아직 없다. 새 공통 디오라마 규칙 후보 없음. commit/push는 요청하거나 실행하지 않았다.

밝기 과다는 첫 판본보다 줄었으나 일부 지붕의 가는 줄그림자와 기존 교차로의 겹침 줄무늬가 남아 있다. 가까운 그림자로 부피감은 커졌지만 점포 유리는 아직 평평하고 상호도 작다. 다음 현실감 보완은 광량을 계속 높이기보다 실측 건물 높이/형태, 근거 있는 창틀·차양·간판 깊이, 기호형 geometry의 shadow 표현을 각각 나눠 다루는 편이 맞다.
