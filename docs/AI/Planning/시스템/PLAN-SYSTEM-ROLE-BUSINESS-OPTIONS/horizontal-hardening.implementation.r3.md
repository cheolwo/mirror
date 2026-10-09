# 기존 역할 옵션의 수평 보완 후 단일 업무 수직 검증 r3

- 기획 ID: `PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS`, 판본: `r3`, 기준일: 2026-10-07.
- 상태: `Approved / ImplementationInProgress / LocalOnly`.
- 승인 근거: 사용자가 전반적인 보완을 먼저 진행하고, 나중에 하나의 업무를 정해 수직으로 다시 보완하는 순서를 수용했다.
- 선행: [재조사 r2](role-options.review.r2.md), [승인 요금표 r1](README.md). 두 기존 판본을 보존한다.
- 주 책임: `operations`, 작업: `WORK-ROLE-OPTIONS-HORIZONTAL-R3`.

## 확정한 진행 순서

1. 지금은 기존 옵션의 의미·적용 범위·확인 절차를 역할 전반에서 일관되게 보완한다.
2. 새 옵션을 모두 한 번에 만들거나 모든 계약·요율을 활성화하지 않는다. 기존 구현을 재사용하고 실제로 혼동되는 항목부터 수정한다.
3. 그다음 한 업무를 선택해 입력→조회→조건 확인→명령→저장/Event→역할 인계→결과 재조회→실패·복구를 수직 검증한다. 대표 업무의 선택은 후속이며 지금 임의로 정하지 않는다.

## 이번 첫 구현 범위

서버 조건·금액·권한을 바꾸지 않는 공통 역할 화면의 범위 표시부터 진행한다.

| 화면 | 이번 직접 결과 | 유지할 경계 |
| --- | --- | --- |
| 주문자 음식점 탐색 | 조회 반경과 주문·배송 허용 범위를 구별 | 기존 필터·조회·위치 기준 보존 |
| 음식점 주문 행동 | 조리시간이 이번 주문에 적용됨을 표시 | 개인/가게 기본값을 저장하지 않음 |
| 음식 기사·운영자 문제 행동 | 사유·판정은 이번 건의 처리임을 표시 | 신고와 귀책 판정, 현재 권한·행동 보존 |
| 화주 절차 입력 | 이번 의뢰 금액·추가 작업 조건, 예정액과 완료 상태 구별 | 계산·입력 바인딩·서버 검증·확정 잠금 보존 |
| 화물 기사 제안 | 의뢰 결제 예정액을 기사 실지급액처럼 표시하지 않음 | 해당 제안의 건별 수락·재조회 보존 |
| 창고 현장 작업 | 실제 작업 기록과 개인 설정·계약 단가 구별 | 수량·위치·포장·인계 상태 보존 |

알림의 실제 앱 연결, 계정/가게 기본값 영속화, 사전 요금표 합의 저장은 별도 구현 경로로 남긴다. 이번 문구 보완을 이 기능들의 완료로 보고하지 않는다. r26/r27 제품 변경에 겹치는 파일은 쓰지 않는다.

## 현 작업트리 수용·소유권

이번 사용자의 직접 보완 요청을 같은 로컬 작업트리에서 수용한다. 다른 계정·데스크톱으로 Git 작업실을 교대하는 인계는 아니며 승인 인계 commit을 발행했다고 주장하지 않는다. r1의 미발행 인계와 금전 구현 관문은 보존하고, 아래 비금전 표시 범위만 별도 수용한다.

- 기준 branch: `dev/mirror-integration`, HEAD: `9336637bde31ec97b5740e2a82a802dd490127ca`.
- 수용 전 변경 경로: 186개. 관련 없는 변경은 보존하며 시작 시점의 숫자를 최종 불변값으로 쓰지 않는다.
- 아래 제품 파일은 수용 시 clean을 확인하고 patch 직전에 재확인한다.
- 개발 통합 담당은 root, 음식 표시/검증과 화물 표시 담당의 정확 경로를 나눈다. 같은 파일 동시 편집은 하지 않는다.

허용 `writePaths`:

- root: `Ssalddel.Ui.Common/Areas/App/Components/Food/OrdererRestaurantSearchPanel.razor`, `Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodRoleActionForm.razor`.
- 화물 담당: `Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperRequestProcedureScreen.razor`, `Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionScreen.razor`, `Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/WarehouseTaskActionScreen.razor`.
- 검증: 신규 `Ssalddel.Tests/Ui/Common/RoleOptionScopeR3Tests.cs`, 신규 `eng/RoleWorkspacePreview/RoleOptionScopePreviewPage.razor`, 필요한 신규 `eng/RoleWorkspacePreview/RoleOptionScopePreviewApi.cs` 및 기존 `PreviewInputApi.cs`의 예시 GET 조립만.
- 문서: 이 파일, 신규 `development-work-order.r3.md`, 신규 `docs/Changes/2026-10-07-role-option-scope-r3.md`, `docs/ProjectOverview/page-docs/role-map-workspace-r11.md`의 이번 책임 카드, `docs/AI/PLANNING.md`의 이번 항목, `docs/AI/CURRENT_WORK.md`의 이번 snapshot.

## 검증 상한과 제외

- 집중 회귀·Fast/Task의 이번 경로 검증·공통 UI/소비 preview build를 수행한다.
- 실제 공통 Razor를 합성 자료로 렌더해 320/390px에서 문구·기존 입력·확인·복귀·오류/비활성 상태를 확인한다. 화면 예시는 실제 DB/API·네이티브 앱 검증이 아니다.
- API·DB·요율·상태 전이·알림 채널·외부 계약/결제·Unity·APK·실기기·운영 활성화·commit/push·배포는 변경/실행하지 않는다.

## 미정·다음 질문 하나

기본값의 저장 범위와 실제 소비 공백은 후속이다. 모든 r2 후보를 구현 승인한 것으로 확대하지 않는다. 수평 보완 반환 후 다음 질문은 수직 검증할 대표 업무 하나의 선택이며 지금 미리 묻지 않는다.
