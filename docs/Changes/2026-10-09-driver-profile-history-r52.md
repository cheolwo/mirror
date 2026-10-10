# 기사 내 정보 메뉴와 배달 내역 r52

## 변경

[승인 기획](../AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-profile-history.r52.md)에 따라 내 정보와 완료 배달 목록을 먼저 다듬었다. 내 정보는 **배달 내역 → 설정 메뉴 → 로그아웃 → 운행 종료** 순서다. 복귀 권역·알림 설정·배달 경험 공유·합의 요금표는 기존 Fluent 아이콘이 있는 행 전체 버튼으로 연다.

길게 펼쳐져 있던 복귀 권역과 경험 공유 입력은 각각 `food-return-area-settings`, `food-delivery-experience-settings`로 옮겼다. 기존 singleton 모델·조회/저장 명령·판본과 계정 변경 보호를 재사용한다. 읽어 둔 설정과 미저장 입력은 단순 왕복으로 덮어쓰지 않고, 미인증 상태에는 폼을 숨긴다. 새 페이지가 운행·GPS·배차 수신 수명을 변경하지 않는다.

완료 배달 목록은 상호·배달료·완료 시각·요금 기준 거리를 유지하면서 큰 `상세 보기` 버튼 대신 행 전체 선택과 화살표를 사용한다. 상세에서 돌아올 때 같은 계정·날짜·페이지의 **첫 표시 항목**으로 복귀하며 픽셀 단위 위치 복원은 아니다. 날짜·페이지·새 조회·계정 변경 후 이전 항목 복원은 막는다. 행 선택은 기존 상세 명령과 중복 진입 보호를 재사용한다.

[페이지 책임 r15](../ProjectOverview/page-docs/driver-workspace-sections-r1.md)·[완료 목록 책임 r4](../ProjectOverview/page-docs/driver-completed-delivery-detail-r1.md)·[작업 범위](../AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-profile-history.r52.work-order.json)에 책임과 경계를 기록한다. 요금 상세 재배치·현재 수행 카드 문구는 이번 범위에 포함하지 않는다.

## 검증

| 확인 | 결과 |
| --- | --- |
| scoped Fast | 162/162 통과. 새 목록 복귀 회귀 6사례 포함 |
| scoped Task | 서버·공통 UI 9,786/9,786 통과. 별도 Mongo 환경 시험 15개 미실행 |
| 컴파일·탐색 | FDriverApp 및 시험 프로젝트, `Ssalddel.v3.5.slnx` 빌드와 음식 기사 코드 지도 `--check` 통과. 생성 지도 변경 없음 |
| 소유 범위 | 소스/시험 14개·문서 8개. 시작 시점 대비 범위 밖 기존 dirty 파일 변경 없음. 기존 AppStyles와 시험 프로젝트 파일은 동일 |
| 문서 | 기획·변경 기록·페이지 책임과 새 목차 항목의 최종 로컬 링크 76개 유효. XAML 5개 파싱·기획/작업 범위 해시 일치 확인 |
| ARM64 APK | Debug `kr.ssalddel.fdriver`, 0.1.0/1, 91,794,132 bytes. 최종 컴파일 오류 0·기존 Android 버전 호환 경고 35개, v2/v3 서명 검증 통과. 기존 로컬 네이버 설정과 생성 리소스 일치 확인(값 비공개) |

페이지 변경 시 예약된 맨 위 이동에도 화면 활성·등장 판본·날짜·페이지·첫 항목의 동일성 검사를 추가했다. 화면 이탈이나 목록 교체 뒤 실행되는 이전 예약이 현재 목록을 옮기지 않게 한다. 이 마지막 네이티브 코드부 보완은 최종 Android 컴파일로 확인하며 실제 CollectionView 이벤트는 단말에서 별도 확인한다. Task에서 실행한 모델·시험 파일은 이후 변경하지 않는다.

Fast 상세는 `artifacts/local/validation/20261009-232100/`, Task 상세는 `artifacts/local/validation/20261009-232300/`, 이번 작업의 범위·해시·APK 근거는 `artifacts/local/driver-profile-history-r52/`에 보관한다. Fast와 Task의 중복 시험을 별도 신규 시험 수로 합산하지 않는다.

실제 Android 화면·터치·뒤로 이동·TalkBack·GPS/지도·원격 배차/알림은 별도 단말 검증이 필요하다. 공통 Razor 화면이나 XML 파싱을 네이티브 화면 검증으로 보고하지 않는다. 이번에는 휴대폰 설치·운영 활성화·DB/서버 변경·Azure·Unity·영상 편집·commit/push를 수행하지 않는다. 기존 dirty 변경을 보존한다.

APK 경로는 `FDriverApp/bin/Debug/net10.0-android/android-arm64/kr.ssalddel.fdriver-Signed.apk`, SHA-256은 `DCB2140E2FC479D66C2214928B7477A57C21CD30C0D22615DBF0676F682222BA`이다. 전체 앱 코드를 포함한 ARM64 Debug 설치 패키지이며 휴대폰 설치·실행 결과와 구분한다.
