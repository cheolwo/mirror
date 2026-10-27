# 건물별 상세 외형 교체와 세 점포 사진 참조 모델

2026-10-26. [직접 사용자 요청에 따른 구현 단위 r114](../AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/detailed-building-representation.r114.md).

## 구조와 실제 만든 자산

기존 건물 ID·공간 근거·외관 Profile·영상 SHA/PTS를 재사용하고 상세 건물 표현을 소형 부품 Overlay와 분리했다. `사가정상세건물표현Renderer`는 정확 대상별 전용 원형 root와 기존 visualKey/판본을 연결한다. 새 자산의 정적 Mesh/Material/Transform 사본을 모두 검사한 다음 교체하며 source의 스크립트·Collider·LODGroup은 실행하거나 복제하지 않는다. 원래 `사가정외관OverlayRenderer`의 제한은 유지한다.

표시 단계는 건물 경계에서 관찰점까지 50m 이내 상세, 140m 이내 중간, 그 밖 원형이다. 세 단계는 상호 배타적이며 준비 실패·출력 유실·제거·Dispose 시 원래 원형의 active 상태로 돌아간다. 원형과 중간 모델은 같은 읽기 원본을 재사용할 수 있다. 현재는 메모리에 준비한 표시의 전환이며 Addressables·비동기 다운로드·거리별 메모리 해제까지 구현한 것은 아니다.

같은 0111 실제 PTS 사진 5개와 기존 외관 근거를 재사용해 Synty 없이 상세 전면 모델을 새로 만들었다. 와플의 주황 벽·얇은 청색 차양·녹색 문턱, 맥주의 벽돌 상부·미색 하부·청록 띠·갈색 차양, 뚜레의 녹색 곡면 프레임·큰 유리를 구분했다. 기하를 재질별로 합치고 미터 UV·normal/tangent를 제공한다. 상호는 직접 관측한 기존 정적 문자 PNG이며 원본 사진·사람·메뉴·실내를 텍스처로 복사하지 않았다.

사진 참조 형상의 폭·창 구획·문턱·문 위치는 비실측 시안이다. 원래 GIS 몸체와 4m 기호 높이·가설 정면을 유지하고 건물 전체 사진측량·보이지 않는 옆면/뒷면·실측 층수/높이로 승격하지 않았다. 기존 주변 건물과 fallback 부품에는 앞선 자산을 계속 사용한다.

## 표면 재질 출처와 적용 경계

[Poly Haven 공식 CC0](https://polyhaven.com/license)와 [공식 API 이용 안내](https://polyhaven.com/our-api)를 원문과 hash로 보존했다. Brick Wall 001·Concrete Wall 003·Asphalt 02의 1K diffuse/normal GL/roughness 9파일 **7,698,607bytes**를 공식 metadata 크기·MD5와 수신 SHA-256·1024²로 대조했다. 이 자료는 일반 표면 사진 재질이며 해당 점포를 직접 스캔한 재질이 아니다. 벽돌·콘크리트를 상세 전면 일부에 사용하며 아스팔트는 이번 전면에는 적용하지 않고 준비 자료로 남겼다. 미관측 몸체·지붕은 최종 판본에서 과한 사진 무늬를 제거한 중립 URP 재질로 유지한다.

`사가정상세표면MaterialLibrary`는 고정 영수증·공식 metadata·현재 원문을 다시 검사하고 메모리 URP/Lit 재질만 소유한다. normal은 선형 GL 자료, roughness는 선형 R에서 1−R을 만든 매끈함 alpha로 변환한다. 원래 Material·프로젝트 설정·package는 변경하지 않는다.

## 발견과 보완

- 첫 컴파일은 새 Renderer의 out 매개변수 lambda 캡처와 `Application` 이름 충돌로 실패했다. 로컬 변수와 `UnityEngine.Application` 명시로 고쳤으며 실패 실행은 시험 0건으로 보존했다.
- 142개 중 PropertyBlock 색 시험 1개가 Color literal의 왕복 반올림을 잘못 비교해 실패했다. 색 허용 오차를 늘리는 대신 Unity가 실제 저장한 vector 값을 복사 전후 정확 비교하도록 고쳤다. 새 입력의 색/Material 보존 의미는 유지한다.
- 사용하지 않는 바탕판 정점이 새 몸체 Mesh bounds에 남아 상세 대상 범위 검사를 막을 수 있음을 읽기 감사에서 발견했다. 이번에 새로 생성해 소유한 mesh만 실제 사용 triangle로 bounds를 계산하며 원본 기하/공유 Synty mesh는 보존한다. 적대적 unused-ground 회귀를 추가했다.
- 첫 graphics는 한 거리 카메라가 주변 건물에 가려져 빈 화면 검사가 거절했다. 화면을 성공으로 표시하지 않고 실제 양면 triangle 가림 거리로 카메라 간격·화각을 결정하는 보정을 추가했다. 첫 보정의 API 호출 컴파일 오류도 수정했다. 카메라 위치는 촬영 위치 복원이 아닌 검토 가설이다.

## 현재 검증

새 실제 graphics EditMode r5는 **143/143**, 실패·생략 0이다. 상세 교체 18·전면 모델 7·표면 재질 5·기존 현실감/외관/공간/Synty/H선택/Demo와 추가 경계/카메라 회귀를 포함한다. 실제 XML은 `ssalddel/artifacts/local/detailed-building-study-r5/editmode.xml`이다.

143시험 뒤 후속 그래픽에서 간판 방향과 몸체의 표면 표시만 조정했다. 간판 PNG를 새 기준점에 붙인 첫 비교는 좌우가 반전돼 보여 Quad 방향을 고쳤다. 미관측 몸체 전체에 사진 콘크리트를 반복하던 모습은 위장 무늬처럼 보여 중립 재질로 바꿨다. 이 두 시각 수정은 새 Unity 컴파일·graphics 실행과 직접 이미지 검토로 확인하며 단위 시험이 간판 가독성까지 증명한다고 표현하지 않는다.

최종 graphics r4는 **종료0**, 기존 비교20+상세/조감 비교12장 총32 PNG를 생성했다. 새 거리3/조감1의 before/after 차이와 제거 후 before/restored 정확 SHA 일치를 확인했다. 실제 0/85/500m 관찰점에서 상세/중간/원형 단계와 각 단계의 단일 표시를 확인했다. 거리 화면은 실제 카메라 위치를 관찰점으로 쓰며 조감도는 명시한 중앙 검토점을 쓴다. 상세 source는 새 모델+같은 GIS 몸체이며 원형/중간 부품과 분리한다.

새 controller·모델 원본·재질/텍스처·임시 container의 해제, 원래 study active와 원래 active Scene 복원·PreviewScene 닫힘을 확인했다. 기존 Scene·기반 지도·원 shader·URP/Graphics/Quality·package·대표 기존 Material 등 원문14파일과 재질원문9개 hash가 보존됐다. 원본 영상 stat·기존 SHA 근거·현재 공개 외관 PTS/보호를 다시 대조했다. 원본 영상의 전체 재해시·복사는 0이다. 최종 로그에 shader error·C# error·처리 예외는 발견하지 않았다.

최종 4개 after와 전후 합성을 직접 검토하고 읽기 전용 독립 검토도 받았다. 상호3개가 정상 방향으로 보이고 전면의 물리적 틀·차양·턱이 나타나며 큰 새 가림/잘림·오류 재질은 보이지 않았다. 유리는 여전히 불투명 판에 가까우며 임시 높이·정면·창 구획·문 위치와 전면 한정 모델이라는 상한은 남았다. 전체 거리의 실사 복원이나 실내 표현 완료는 아니다.

같은 카메라 전후 합성4장, **16초·480프레임 무음 비교 MP4**, HTML을 만들고 MP4 전체 프레임 디코딩을 통과했다. 비공개 최신 포인터는 배달 `edit-kit/private/detailed-building-study/current-review.json`이다. 비교 자료는 `20261026-r1/comparison-r1/상세건물-비교.html`과 `상세건물-전후비교.mp4`이며 결과 `result.private.json` SHA는 `f4cf8ad4c801870279239099a9344e8dc8ae7c060e10b25bcf48502684a7c67a`, MP4 SHA는 `5159276c1aafd23b1a8a0d1ad8d6d3a5b32e493401486ae2605fcdb7a50eae2d`다.

실제 Editor/Pipeline 도구 연결이 없어 CLI PreviewScene Camera.Render를 사용했다. Game View·Play Mode·실시간 FPS·비동기 메모리 스트리밍은 미검증/미구현이며 정본 적용0이다. 최종 Scene·원형·사용자 승인·운영 DB·공개 상태·commit/push는 변경하지 않는다. 기존 출처/권위·보존·범위 검사 규칙을 재사용했고 새 공통 디오라마 규칙 후보 없음.
