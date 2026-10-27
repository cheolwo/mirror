# 같은 영상의 선분 후보와 세 매장 외관을 함께 보는 디오라마

2026-10-26. [사용자가 승인한 시각 검증 범위 r112](../AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/video-segment-diorama-visual-demo.r112.md).

## 보여준 한 사례

0111 원본 SHA `62d1aff8…`의 기존 실제 PTS 사진 5개(84.017~93.026초)를 재사용했다. 같은 관측창에서 대조한 선분 후보 `JN-S002005`·`JN-S012044`·`JN-S007715`와 배경 도로 2개, 와플대학·맥주아비·뚜레쥬르의 공개 원형·외관 근거를 고정한 입력으로 연결했다. 새 위치 승인이나 선분별 진입/이탈 시각은 만들지 않았다.

현재 동네 모형을 배경으로 공개 GIS 연구 건물 3개에 관측색·상호 PNG·유리면·차양을 표시했다. 전체와 매장별 근접 4시점에서 before/after/모듈 실제 제거 후 restored 12장을 생성했다. 기존 Synty 모듈·외관 Gate/Renderer·공통 MeshBuilder를 재사용했고 새 Editor Capture와 집중시험 각각 `.meta`를 포함한 4파일만 추가했다. 기존 제품/Scene/Prefab/Material은 바꾸지 않았다.

원본 9.042초·271프레임을 정수 PTS로 선택한 무음 파생 구간과 시안 캡처를 7장면 설명 MP4로 조립했다. 재생 시간은 헤더 반올림 31.97초이며 설명 영상 시계와 원본 주행 시계를 구분한다. HTML에는 원본 구간·선분/공개 건물 도식·전후/복구 이미지 선택·설명 MP4를 함께 두었다. HTML 자원 경로를 검사했지만 실제 사용자 버튼 조작 검증은 수행하지 않았다.

## 실제 실행에서 고친 문제

- 첫 컴파일은 새 Capture가 참고도로를 points 배열로 읽어 실패했다. 실제 x1/z1/x2/z2 계약으로 수정했다. 이 실행은 시험 0건이며 이후 성공 XML과 구분한다.
- 첫 그래픽 출력은 원래 baseline602와 새 GIS3 원형이 겹쳐 지붕 줄무늬가 생겼고, 복구 이미지의 1픽셀 차이로 실패했다. 원본 map으로 Overture composition을 검증한 뒤 미리보기용 깊은 사본에서 명시 3개 legacy만 가렸다. 원본 데이터·현재 Overture2247은 보존하며 기하 겹침을 건물 동일성 확정으로 사용하지 않았다. source baseline602→미리보기599와 연구3을 별도로 기록했다.
- 후속 그래픽에서 카메라를 2회 렌더해 안정화하고 before/restored SHA 정확 일치를 통과했다. 대상 지붕의 심한 겹침은 보이지 않았으나 일부 교차로 노면의 가는 줄무늬는 남았다. 전체 지도의 깜빡임/겹침을 해결했다고 보고하지 않는다.
- 첫 파생 검토본은 원본 회전 metadata 자동 적용으로 영상이 옆으로 돌아갔다. 실제 관측된 raw 프레임 방향과 대조해 이 원본에 한해 noautorotate·출력 회전 metadata 초기화 후 새 source-preview-r2/video-r2를 만들었다. 원본과 앞선 실패/검토 판본은 덮지 않았다.

## 검증과 결과 위치

- Unity EditMode **106/106**, 실패·생략 0: 새 Demo 8·기존 외관/공간/Synty/H선택 98. 실제 XML은 `ssalddel/artifacts/local/video-segment-diorama-demo-r3/editmode.xml`이다.
- 실제 graphics r2 종료0, 12개 PNG의 전경/분산·hash 검사, 4시점의 전후 차이·모듈 제거 후 0개·복구 동일 SHA, PreviewScene 닫힘과 원래 active Scene 보존을 확인했다. 처리되지 않은 실행 예외 0건이며 라이선스 경고와 구분했다.
- 실제 Editor/Pipeline 연결 및 Unity MCP는 없어 그래픽 CLI PreviewScene Camera.Render로 실행했다. Game View·Play Mode·실제 입력·FPS 검증은 아니다.
- 원본 stat·기존 SHA 영수증·실제 PTS·프레임/지도/공개 원형/현재 보호·31개 저장 native 프로젝트를 대조했다. 미저장 앱 상태는 미확인이다. 원본 전체 새 해시·복사는 0이며 새 파생 영상만 렌더했다.
- source-preview-r2와 video-r2 전체 파생 영상 디코딩을 통과했고, 대표 파생 프레임에서 방향·한글 제목·시안을 직접 확인했다. 최종 MP4 SHA는 `6650606e38052f554f21d4048cc3bd27d10730ab25db28e1e9a2cd22b38f9a44`이다.

최신 포인터: 배달 `edit-kit/private/video-diorama-demo/current-review.json`.
비교 페이지: `20261026-r1/영상-선분-디오라마-비교-r2.html`.
검토 MP4: `20261026-r1/video-r2/영상-선분-디오라마-검토.mp4`.
검증 묶음: `20261026-r1/result-r2.private.json`(SHA `aa7c1f1af933713ed1467dd3fa8d808d08149a86be03373a68c5996c77a38e10`).

자료는 로컬 비공개 검토용이며 저장소 공개 시각 자산으로 복사하지 않았다. 연구 외관 반영 3건·정본 지도 적용 0건이다. 빨간 점선은 후보이고 운행 애니메이션·GPS·전체 경로가 아니다. 높이 4m·도로 가까운 면 정면은 가설이다. 기존 원본·분석/승인·Premiere 컷/마커·Scene·Prefs·Synty·운영 DB·공개 상태·commit/push는 변경하지 않았다.
