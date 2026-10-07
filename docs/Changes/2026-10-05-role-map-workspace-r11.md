# 역할 선택 홈과 역할별 지도 작업공간 r11

사용자가 승인한 메인 홈의 여덟 역할 선택을 통합 Web와 SsalddelApp Android에 구현했다. 선택한 역할은 같은 앱 안의 `/workspace/{role}`로 이동하며 기존 전용 앱은 유지한다. 생활 지도는 기존 교류·협업·보관·배송 화면을 재사용한다.

공통 지도 화면은 현재 업무 카드와 접힌 상세·추가 작업을 제공한다. 주문자·음식점·음식 기사·운영자는 기존 음식 주문 API를, 화주·화물 기사·창고는 기존 운송·창고 API를 사용한다. 주문 작성, 조리 조건, 중단 검토, 상하차 사진 증빙, 추천 수락, 창고 수량 입력은 독립 화면에 둔다. 입력·로그인 뒤 선택 대상과 지도 맥락으로 복귀한다.

역할 선택은 권한을 부여하지 않는다. 기존 일반 계정은 재사용하고 음식점·음식 기사·운영자는 역할별 인증 저장과 요청을 분리한다. 계정·역할 변경과 화면 중단에는 비공개 카드·지도와 늦은 응답을 제거한다. 실패한 상태 명령을 자동 재전송하지 않으며 사람이 재시도할 때 같은 요청 ID를 사용한다. 음식·화물 서버 계약과 원장의 권위는 그대로 유지했다.

실제 좌표만 핀으로 표시한다. 음식 기사 경로는 기존 API의 비추정 `NaverDirections5` 응답만 사용하고 픽업은 주황색, 전달은 파란색으로 표현한다. 좌표·위치 권한·지도 설정이 없으면 카드로 업무를 계속 확인할 수 있다. 화물의 직선 연결은 회색 점선과 ‘직선 후보선 · 실제 주행 경로 아님’ 안내로 구분한다.

상태는 `Implemented / ValidatedLocal / PublicDeploymentPending`이다. 역할 화면의 소스·서버 계약·로컬 렌더·설치 파일 검증과 실제 휴대폰/공개 서버 검증을 구별한다.

## 검증 결과

- Fast `20261005-171452`를 통과했다. 최종 Task `20261005-173938`는 전체 `Ssalddel.v3.5.slnx` 빌드(오류 0·경고 98)와 전수 시험 **7,402/7,402**를 통과했다. 기존 코드의 nullable 및 패키지 경고는 남아 있다.
- 실제 통합 WASM을 로컬에서 실행해 여덟 역할 홈, 일곱 보호 역할의 미로그인 경계, 로그인 취소 후 같은 역할 복귀, 생활 지도 wrapper의 query 연결을 확인했다. 다섯 신규 입력 경로의 직접 열기와 새로고침도 통과했다. 음식 행동 ID는 query로 전달해 점이 포함된 ID의 직접 접근 오류를 해결했고 기존 path alias는 유지했다.
- 위 WASM 확인은 모든 API 요청을 로컬 403 응답으로 가로채 외부 서버에 업무를 기록하지 않았다. 역할별 인증을 거친 새 UI→실제 서버 업무 완료, GPS 및 지도 타일 실행 증거로 확대하지 않는다. 원시 결과는 `wasm-ui-checks.json`이다.
- 공통 Razor 화면은 별도 예시 자료로 여덟 역할의 기본/펼친 카드, 처리 확인, 역할 홈 복귀, 로그아웃 시 비공개 카드 제거, 320/390/1100px 화면 폭과 가로 넘침 없음, 실행 오류 없음을 확인했다. `preview-ui-checks.json`에 기록했다. 이 예시 생활 화면은 제품의 생활 지도 실행 증거가 아니다.
- 입력·허용 행동·계정 변경·늦은 응답·같은 요청 ID 재시도·사진 증빙·완료 후 재조회·실패 복구는 새 역할 adapter/ViewModel/인증 시험에서 확인했다. 음식·화물·창고의 이전 서버 업무 검증은 기존 기록으로 보존한다.

## 화면 기록

아래 첫 두 화면은 실제 통합 WASM의 모바일 폭 화면이다. 이후 일곱 화면은 예시 자료와 미설정 지도 host를 사용한 공통 카드 배치 미리보기다. 지도 타일을 촬영한 화면은 아니다.

| 구분 | 화면 |
| --- | --- |
| 실제 통합 앱 | [역할 선택 홈](../assets/changes/2026-10-05-role-map-workspace-r11/home-mobile.png) · [음식 기사 로그인 안내](../assets/changes/2026-10-05-role-map-workspace-r11/driver-login-required.png) |
| 예시 카드 배치 | [주문자](../assets/changes/2026-10-05-role-map-workspace-r11/orderer-expanded.png) · [음식점](../assets/changes/2026-10-05-role-map-workspace-r11/restaurant-expanded.png) · [음식 기사](../assets/changes/2026-10-05-role-map-workspace-r11/food-driver-expanded.png) |
| 예시 카드 배치 | [화주](../assets/changes/2026-10-05-role-map-workspace-r11/shipper-expanded.png) · [화물 기사](../assets/changes/2026-10-05-role-map-workspace-r11/cargo-driver-expanded.png) · [창고](../assets/changes/2026-10-05-role-map-workspace-r11/warehouse-expanded.png) · [운영자](../assets/changes/2026-10-05-role-map-workspace-r11/operator-expanded.png) |

## 설치 파일과 남은 실행 확인

내장 Android Debug APK는 `artifacts/local/role-map-workspace-r11/SsalddelApp-role-map-workspace-r11-debug.apk`에 보관한다. 최종 Android 빌드(오류 0·기존 nullable 경고 2), APK v2/v3 서명, ZIP 정렬·CRC, ARM64/x86_64 런타임·앱·공통 UI 어셈블리 포함, 현재 빌드와 공통 CSS 일치, 원본과 사본의 SHA-256 일치를 확인했다. `apk-verification.json`에 결속하며 버전의 기준 소스는 같은 폴더의 `source-hashes.json`이다.

이 APK는 현재 Debug 기본값인 Android 에뮬레이터 서버 `http://10.0.2.2:5104/`를 사용하며 공개 Azure 주소를 내장하지 않았다. 개인 휴대폰 설치/통신, 실제 운영 로그인과 새 역할 화면의 서버 업무 완료, Android 지도 키·타일·위치 권한·외부 경로는 별도 실행 확인이 남는다. 지도 실패와 좌표 누락에서는 카드/목록을 제공한다. 창고 목록은 공정별 첫 50건, 화주 목록은 첫 200건이며 선택한 업무 ID의 상세 조회는 별도로 가능하다.

관련: [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md), [구현 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-map-workspace.implementation.r11.md). 로컬 원시 근거는 `artifacts/local/role-map-workspace-r11/`에 보관한다. 기존 r10 증거와 다른 dirty 작업은 보존했다.
