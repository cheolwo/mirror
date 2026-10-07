# 상태별 업무 카드와 창고 연결 보완 r18

사용자가 OS·업무 상태 카드 조사에서 제안한 순서에 따라 보완을 계속하도록 승인했다. [책임 기준](../ProjectOverview/page-docs/role-map-workspace-r11.md#상태별-업무-연결과-예외-복원-r18)과 [조사 snapshot](../ProjectOverview/page-docs/os-state-card-audit-r17.md)을 바탕으로 기존 상태·화면을 연결한다. 새로운 OS 단계나 역할·자동 업무를 만들지 않는다.

## 바뀐 동작

- 화물 신고 후 다른 조회 수명에서도 최근 신고·관련 사건·운영 검토 상태를 복원한다. 관련 사건 전부의 보류를 반영하고 최신 사건 종료가 다른 전체 보류를 지우지 않는다. 운영 확인, 정산 보류, 전체 운송 보류를 구별하며 전체 보류는 직접 POST도 제한한다. 영향 수량 보류는 전체 중단으로 바꾸지 않는다.
- 서버의 가능한 행동과 카드·독립 입력을 맞춘다. 빈 행동 목록에서는 신고도 비활성이다. 이미 완료가 저장되고 재조회로 확인되면 이후 새 보류가 생겨도 이전 완료를 실패로 표시하지 않는다. 생활 배송 정보 제공이 보류되면 새 사건 상세와 행동도 제거한다.
- 창고 수령 요청과 새 재고의 검수 관계, 인계 준비와 출고예정 관계를 확인해 다음 업무를 연결한다. 기존 검수·출고 검토·운송 의뢰 폼을 같은 창고 역할 세션에서 재사용하며 지도와 선택 업무로 복귀한다. 출고 준비와 실제 기사 인계를 구별하고 운송 의뢰는 명시 확인 후 생성한다.
- 음식점은 픽업 완료 뒤 전달/수령 확인을 안내한다. 재조리는 이번 음식의 준비를 기다리며 과거 준비 기록으로 픽업을 지시하지 않는다. 기사 배정 대기·배차 예외·실제 픽업 허용 여부에 맞춰 음식점·기사·주문자 안내를 연결한다.
- 화물 현재 목적지의 시간창·필수 증빙은 기본 카드에, 사건 처리 상태·최근 변경은 상세에 표시한다. 검수와 현장 입력은 독립 페이지에 유지한다.
- 창고 독립 화면에서는 중복 제목과 내부 기술 설명을 줄이고 수량·포장·보관 조건·현장 확인을 안내한다. 실제 인계 버튼은 기사 신원·차량·물품 확인이 모두 끝난 뒤 활성화한다. 배정 기사 식별번호는 현장 대조에 필요하므로 유지한다. 음식 기사의 상단 안내도 현재 수행·신규 제안·대기를 구별한다.

API/JSON의 기존 필드·stable ID·오류·원장 상태를 보존하고 additive 화물 상태 필드만 추가한다. DB migration·정산 정책·지도 SDK·네이버/Google 설정·운영 자동 배차를 변경하지 않는다.

## 검증

`Implemented / ValidatedLocal / CardReviewPending / DeviceRuntimePending`.

- Fast `20261005-224311` 집중 320/320, Task `20261005-224540` 전수 7,625/7,625 통과. 새 창고 회귀 21개와 음식 기사 상단 안내 회귀가 전수 결과에 포함됐다. 최종 제품 3.5 빌드 오류 0·경고 58이며 다른 앱의 기존 패키지 버전·nullable/미사용 필드 경고는 남아 있다.
- 실제 공통 UI를 사용하는 로컬 예시 웹에서 화물 전체 보류/운송 재개 상세, 음식 재조리·준비 완료·픽업 불허, 음식점 픽업 이후·이번 재조리, 주문자 기사 대기, 창고 검수·출고 검토·운송 요청·실제 인계와 지도 복귀를 확인했다. 마지막 19개 화면/행동 확인에서 320/390px 가로 넘침과 새 검증 탭의 브라우저 오류가 모두 0이었다. 인계 버튼은 두 체크까지 비활성, 세 체크 뒤 활성이고 실제 제출은 하지 않았다.
- 미리보기와 통합 앱 Android 빌드 오류/경고 0. 이는 Android 컴파일 확인이며 새 APK 설치·휴대폰 실행 결과가 아니다.
- 서버 예외 저장·새 조회 수명·후속 관계·권한·현재 기사 대조 회귀는 자동시험으로 검증했다. EF InMemory/fake API 결과를 실제 운영 DB/HTTP 업무 완료로 확대하지 않는다. 예시 웹의 쓰기는 차단하며 현장 체크 UI만 확인한다.
- 이번 실행에는 지도 키를 공급하지 않았으므로 지도 설정 안내/목록 대체와 카드·페이지 이동을 확인했다. r14의 실제 Google 타일 확인 범위는 그대로이며 이번 캡처를 실제 위치·도로 경로 검증으로 보지 않는다.

원시 근거는 Git 제외 `artifacts/local/state-card-polish-r18/`의 `fast-final-ui.log`, `task-final-ui.log`, `preview-build-verified.log`, `android-build-verified.log`, `browser-verified.json`, `final-source-review.md`, `preservation-final.json`에 보관한다. 실제 휴대폰·GPS·Azure·운영 서버/DB 업무 완료·입금·commit/push는 수행하지 않았다. 본 작업 밖 기존 변경 파일 850개 해시와 HEAD·branch·빈 stage를 보존했다.

## 화면 확인

모두 실제 공통 UI의 예시 자료 화면이다. 현장 업무 수행·최종 카드 디자인 승인과 구별한다.

| 확인 내용 | 캡처 |
|---|---|
| 전체 운송 보류·행동 제한 | [화물 보류](../assets/changes/2026-10-05-state-card-polish-r18/cargo-held.jpg) |
| 운영 검토 후 운송 재개 | [예외 처리 상세](../assets/changes/2026-10-05-state-card-polish-r18/cargo-reviewed.jpg) |
| 이번 음식 준비 전 픽업 대기 | [음식 기사 재조리](../assets/changes/2026-10-05-state-card-polish-r18/food-driver-recooking.jpg) |
| 준비 기록이 있어도 픽업 권한 없음 | [픽업 불허](../assets/changes/2026-10-05-state-card-polish-r18/food-driver-ready-without-permission.jpg) |
| 이번 준비 완료·픽업 허용 | [픽업 가능](../assets/changes/2026-10-05-state-card-polish-r18/food-driver-valid-ready.jpg) |
| 음식점의 픽업 이후/이번 재조리 | [픽업 이후](../assets/changes/2026-10-05-state-card-polish-r18/restaurant-pickupdone.jpg), [재조리](../assets/changes/2026-10-05-state-card-polish-r18/restaurant-recooking.jpg) |
| 주문자 기사 배정 대기 | [주문자](../assets/changes/2026-10-05-state-card-polish-r18/orderer-awaiting-driver.jpg) |
| 수령한 물품의 검수 입력 | [검수 화면](../assets/changes/2026-10-05-state-card-polish-r18/warehouse-inspection-form.jpg) |
| 출고 검토·별도 운송 요청 | [출고 검토](../assets/changes/2026-10-05-state-card-polish-r18/warehouse-outbound-review.jpg), [운송 요청](../assets/changes/2026-10-05-state-card-polish-r18/warehouse-transport-draft.jpg) |
| 기사·차량·물품 확인 후 실제 인계 | [인계 화면](../assets/changes/2026-10-05-state-card-polish-r18/warehouse-handoff.jpg) |
