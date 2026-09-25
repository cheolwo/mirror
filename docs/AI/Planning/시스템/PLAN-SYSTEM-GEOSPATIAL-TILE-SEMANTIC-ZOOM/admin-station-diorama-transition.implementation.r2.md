# [기획 · 월드·공간·배치 · PLAN-SYSTEM-GEOSPATIAL-TILE-SEMANTIC-ZOOM · 구현 r2]

## 구현 범위

기존 `SimulationWorldShell`과 곡면 지구본 확대 경로를 유지하면서 다음의 읽기 전용
공간 전환을 추가했다.

```text
Z2.5 한반도 거점
→ Z3 사가정 생활권 참조 범위
→ Z4 면목제3·8동
→ Z4.5 사가정역 생활권
→ Z5 기존 사가정역 1km 디오라마
→ Z4.5 역세권 선택으로 복귀
```

새 Scene, 새 지도 관리자, 새 행정 경계 또는 임의 공간 객체는 만들지 않았다. 기존
고유 식별자를 그대로 사용한다.

- 권역: `world-region:kr:seoul:jungnang:sagajeong.r1`
- 행정동: `region:kr:hjd:1126057500`
- 역세권: `station:kr:kric:s1107:0722`

## 구현 내용

- `공간의미확대전환흐름`이 `Region → AdministrativeArea → StationArea → LocalDiorama`
  순서를 소유한다.
- `공간의미확대전환View`는 한반도 거점 단계에서만 우측 선택 패널을 표시한다.
- 행정동 단계에는 `HistoricalBoundaryCandidate_CurrentPublicationBlocked`를 표시해
  과거 경계 후보를 현재 공개 경계처럼 보이지 않게 했다.
- 역세권 단계에서만 기존 `ActiveStationDioramaHost`와
  `사가정운영디오라마View`를 활성화할 수 있다.
- 디오라마를 열 때 지구본 관찰 카메라만 닫고, Actor·NPC·업무 원장·Simulation
  revision과 위치는 변경하지 않는다.
- 복귀하면 사가정 디오라마를 닫고 `33` 거리의 한반도 거점 화면과 사가정역 선택
  문맥을 복원한다.
- `세계지도표현상태`는 `Region`, `AdministrativeArea`, `StationArea`를 구분한다.

## 사실 경계

- 30개 행정동은 후보 범위이며 이번 구현은 현재 행정 경계 수집 완료를 뜻하지 않는다.
- 면목제3·8동 단계는 기존 역사 경계 후보를 식별하는 선택 문맥일 뿐, 경계 Mesh를
  새로 그리거나 현재 경계라고 주장하지 않는다.
- 사가정역 1km 자료는 `LocalPrivateReview` 동결 자료다. 도로 통행, 실제 건물 용도,
  출입구, NPC 이동과 운영 권위를 확정하지 않는다.
- 현재 1·4·16 타일은 계속 비교 모판이다. `WorldCRS84Quad` 논리 타일 주소와 지연
  생성·회수 구현을 완료했다고 보지 않는다.

## 검증

- Unity 재컴파일: 오류 없음.
- EditMode:
  - `공간의미확대전환Tests` 3/3.
  - `세계지구본Tests` 21/21.
  - `역세권디오라마Module표준Tests` 11/11.
- 실제 `SimulationWorldShell` Play Mode:
  - `Region → AdministrativeArea → StationArea` 전환 확인.
  - 사가정 디오라마 `Ready / ReadyWithOptionalModuleGaps` 활성화 확인.
  - 복귀 뒤 `CameraDistance=33 / TerrainStage=Hubs / StationArea` 문맥 복원 확인.
  - 디오라마 복귀 뒤 `DioramaVisible=False` 확인.
  - Console 오류 0건. 기존 누락 Script와 네트워크 capability 대기 경고는 남아 있다.
- Scene 저장 전후 SHA-256:
  `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 동일하며
  Scene은 dirty가 아니다.
- 로컬 화면 증거:
  `C:/Users/user/ssalddel/artifacts/local/globe-semantic-transition-captures/20260922/`
  아래 `05`~`09` PNG. 임시 검증 자료라 저장소 문서 자산으로 승격하지 않았다.

실제 마우스 버튼 입력, Windows Player 빌드, 현재 행정 경계 원본, 다른 역의 동일
전환은 검증하지 않았다. 따라서 이 결과는 기존 사가정 준비 자식 공간의 Play Mode
관찰 결속 증거이며 통합 E7 또는 공통 역세권 규칙 승격 증거가 아니다.

## 디오라마 증거 규칙 판정

기존 `PreparedChildSpace`와 `ActiveStationDioramaHost` 경계를 재사용했고 새 출처나
다른 역 반례를 추가하지 않았다. 기존 규칙을 약화·무효화하지 않았으며
**새 디오라마 규칙 후보 없음**으로 판정한다.

## 제외

- 현재 행정동 경계·건물·출입구 수집과 공개 승격
- 새 역세권 Profile과 다른 역 fallback
- 지구 타일 주소·캐시·지연 생성·회수 완성
- Actor 순간이동·내비게이션·업무 상태 변경
- Scene 저장, Windows 빌드, commit, push, 배포
