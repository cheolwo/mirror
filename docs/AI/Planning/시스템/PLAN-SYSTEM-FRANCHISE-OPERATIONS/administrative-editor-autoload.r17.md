# 행정 경계 Editor 자동 준비 r17

2026-09-23 사용자 속행 요청. r16 로컬 검토 파일을 매번 선택해야 하는 단계를 줄였다. 배포 승인이나 전국 자료 확보는 포함하지 않는다.

## 구현

- `서울중간공간Loader`는 지정한 v3 파일 경로와 기대 SHA256을 프로젝트별 EditorPrefs에 보관한다. 이 PC에는 r16의 seoul-globe-preview.json을 지정했다. 데이터 자체를 Assets/Resources나 빌드에 복사하지 않는다.
- SimulationWorldShell Play 진입 시 지구본 준비를 최대 15초 기다려 상태 사본을 검증/로드한다. 카메라는 이동시키지 않는다. 이미 로드된 검토 View는 보존한다.
- 파일/sidecar hash와 별도로 기억한 기대 hash를 대조한다. 실패하면 자동 준비를 중단하고 경고하며 더미 자료로 대체하지 않는다.
- 메뉴 `Tools/Mirror/행정 경계 자동 준비 파일 지정`과 `행정 경계 자동 준비 해제`를 제공한다. 새 PC의 기본값은 미설정이다.
- 지구본 휠의 서울 진입은 서울이 카메라 앞쪽 중앙 범위에 있을 때만 허용한다. 다른 지역 확대 시 서울로 이동하지 않는다.

## 확인한 결과

- EditMode 서울중간공간Tests 14/14 통과, 관련 코드 diff 검사 통과.
- 실제 Editor에서 잘못된 기대 hash 거절 확인.
- Play 진입 두 번 모두 별도 Load 호출 없이 AutomaticStatus=Ready, View 1개/89개 영역 준비. 시작 시 IsVisible=false로 지구본 유지.
- 한반도 초기 자세/거리32에서 CanEnterFromGlobe=true, 지구본 180도 회전 후 false 확인. 자동 회전으로 서울이 중앙을 벗어나면 false가 되는 것이 정상이다.
- 자동 준비된 View에서 서울→중랑구 함수 호출 후 Game View 캡처: Unity `Documentation/Changes/2026-09-23-admin-auto-r17/jungnang.png`. 물리 휠/클릭 검증은 아니다.

## 남은 범위

Windows 빌드 자동 로드, 전국 경계 수집, 지도 시각 마감, 사가정 왕복 및 Console 오류 0 검증은 아직 없다. 파일 경로 기억은 로컬 개발 편의이며 배포 가능한 데이터 파이프라인 완성으로 해석하지 않는다. 자료/출처는 r16과 동일하다. 새 디오라마 규칙 후보 없음. Play 종료, Scene 저장·commit·push 없음.
