[기획 · 시스템·앱 제작 입력 · PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST · r9 결과]

# 앱 제작 입력 표준 — 구현 반환

기준: [승인 r9](app-production-intake.r9.md), [허용 경로](app-production-intake.r9.work-order.json). 기존 r8와 다른 작업트리 변경을 보존한다.

상태: `ToolingImplementedAndTested / PilotNeedsReview / ActualBrowserBlocked`. 사용자 승인 범위는 관리 도구이며 음식점 새 업무 구현 승인으로 전용하지 않았다. 시작 당시 dirty 165항목을 보존하고 허용 경로만 수정했다. push된 원격 인계 수용이나 깨끗한 Git 기준선으로 표현하지 않는다.

## 반영

- `intake.mjs`와 양식3종: 공통 프로필·여섯 입력 묶음·검토 기록을 출처/hash와 결속한다. 기존 미연결 업무는 `NotReviewed`다. 사람 확인, 실행 승인 선언, 결과물별 환경 준비는 독립 상태이며 실행 권한을 생성하지 않는다.
- `catalog/cli/viewer`: 입력 상태 필터·상세·환경 차단·출처 hash와 안전한 인계 사본. 원시 입력 JSON·비밀 설정을 HTTP로 제공하지 않는다. 명백한 민감 패턴 차단은 개인정보 전수 탐지 보증이 아니다.
- `evidence`: 선택한 입력과 중첩 출처 변경을 증거 현재성에 반영한다. 이전 형식의 결속 hash는 그대로 유지한다. r8 도구 성공 기록은 보존하되 현재 r9 입력에서는 `Stale`이다.
- `publish-mobile-field-test.ps1`: `-AppNames restaurant` 등 명시 선택, 생략 시 기존 네 앱 전체 유지. HTTPS·버전·서명 조건과 저장소 내부/재분석 경로 키 차단을 시험했다. 실제 포장하지 않았다.
- [음식점 메뉴 초안](restaurant-menu-input-review.md): 기존 r2 메뉴와 r4 탐색을 재사용한다. 현재 `NeedsInformation / Incomplete / Pending / NotGranted / Blocked`. 길이 제한과 서버 URL 검증 차이를 발견 사항으로 남겼고, 사람 검토·제품 승인·환경 준비를 대기한다. 기존 탐색·비공개 기본값을 다시 결정하라고 묻지 않는다.

## 검증

| 범위 | 결과 | 근거·한계 |
| --- | --- | --- |
| 최종 자동시험 | 93/93, 실패·건너뜀0 | 목록15·CLI/순수 DOM16·증거36·입력20·APK 계획6. [로그](../../../../../artifacts/local/planning-app-production/r9-20260927-01/tool-tests.log) |
| 실행 전/후 입력 고정 | `Passed / Current` | [manifest](../../../../../artifacts/local/planning-app-production/r9-20260927-01/tool-tests.json). 선언된 관리 도구 범위만 |
| 독립 검토 | 환경 false-Ready 1건 발견·수정·재시험 | 잘못된 환경 항목을 버리지 않고 해당/필수 결과물을 보수적으로 차단. 승인·중첩 hash·민감 경로도 검토 |
| 저장소 범위 Fast/Task | 통과 | 각각 `20260927-173122`, `20260927-173123`. docs/eng 범위라 제품 build·시험 생략 |
| 생성/인계 | 동일 입력 생성/재검사·선택 기획 export 통과 | 입력 hash를 포함한 현황표와 검토용 인계. `artifacts/local/exports/app-production-r9-20260927-final`에 요약2파일만 저장. 원문 파일 자동 복사 없음 |
| 문서·diff | 로컬 링크28개와 git diff 검사 통과 | 전체 기획의 의미 충족이나 제품 시험 통과가 아님 |
| 실제 브라우저·캡처 | 미검증 | 현재 CUA inventory apps/browsers 비어 있음, 실제 열기 `Browser is not available: iab`. 순수 DOM 시험은 화면 렌더·390px 조작·캡처가 아님 |

검토자 신원 인증이나 source anchor의 실제 제목 존재를 이 도구가 보증하지 않는다. 기획·작업지시서·실제 수용 절차는 별도로 확인한다. 다음 제작 전에 메뉴 기술 충돌과 격리 시험/서명 환경을 확인하고, 새 업무 실행 승인을 받아야 한다.

[입력 안내](app-production-input-guide.md) · [생성 현황표](../../../generated/planning-app-production.md) · [읽기 전용 HTML](../../../generated/planning-app-production.html)

제품 코드·DB·실결제·Unity·실제 APK 생성·장치 설치·배포·commit·push는 이 작업에서 제외한다.
