# 일반 이용자 지도 작업공간 r10

2026-10-05. `Implemented / ValidatedLocal / PublicDeploymentPending`. 사용자 승인: 일반용 앱을 먼저 지도 중심으로 정리하고 등록·수정은 입력 전용 페이지로 유지한다.

Android SsalddelApp, 통합 Web, 독립 01 Community Web을 우선 적용한다. 주변 보기 / 내 할 일 / 등록을 기본 진입으로 두고 표시 설정은 별도 펼침으로 둔다. 지도에서는 선택한 글·협업·공간·배송의 조회와 현재 단계 행동을 기존 업무 컴포넌트로 처리한다. 입력·인증은 독립 화면으로 이동하고 저장/취소 뒤 같은 지도 맥락으로 복귀한다. 기존 direct route, PageKey, API/DB, 권한과 기능 관문, 전문 역할 앱은 유지한다.

[구현 전 책임 카드](../../../../ProjectOverview/page-docs/neighborhood-map-workspace-r1.md)가 기본·보조 정보, 입력 수명, 복귀, 개인정보, 렌더 책임을 소유한다. 계정별 메모리 세션과 기존 영구 표시 설정을 구별하며 정확 주소·위치와 업무 DTO를 영구 캐시하지 않는다. 카메라 이동은 메모리 세션에 전달하고 개인 위치는 앱 중단 때 제거한다. 재진입/재개는 현재 원장을 다시 조회한다.

독립 데이터 조회와 지도 장면 구성, 플랫폼 어댑터, 패널 조립을 분리한다. Android native 지도는 HTML 위의 별도 native view이므로 CSS 겹침에 기대지 않고 지도와 패널 영역을 나눈다. 키가 없으면 목록과 명확한 안내를 제공하며 Web 키로 native 제한을 우회하지 않는다.

검증은 focused → 전체 Task → shared UI의 실제 HTTP/보호 원장과 화면 → 내장 assemblies APK 순서다. 실제 휴대폰, Azure, Android 지도 타일, 외부 경로/입금, Figma 동기화는 별도 결과로 보고한다. 기준 r9 및 unrelated dirty 작업은 보존한다. 커밋·푸시는 현재 요청 범위에 포함하지 않는다.

[변경·검증 기록](../../../../Changes/2026-10-05-map-workspace-r1.md)에 집중 321/321, Task `20261005-155118` 전수 7,297/7,297, 공통 UI·독립 01 WASM 실제 화면과 최신 내장 Debug APK를 결속했다. UI 실행은 실제 HTTP/SQLite/보호 Mongo와 검토용 인증·배송을 구분한다. 기존 업무 입력 수명도 유지하므로 배송 작성의 화면 이탈·401 후 비공개 초안 삭제를 지도 복귀가 해결한 것으로 보고하지 않는다.
