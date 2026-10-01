[기획 · 시스템·앱 제작 관리 · PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST · r8 결과]

# 기획에서 역할 앱까지 — 구현·검증 기록

- 기준일: 2026-09-27
- 기획: [승인 범위 r8](app-production-tracking.r8.md)
- 작업 명세: [허용 경로·검증 상한](app-production-tracking.r8.work-order.json)
- 상태: `Implemented / ToolingVerified / ActualBrowserVerificationBlocked`
- 기준선: `dev/mirror-integration` / `ca3b6e65d742bc5c00ebfb7f316ecca706061dc9` + 시작 당시 수정·미추적 152항목. 이 결과는 로컬 작업 사본 기준이며 push된 인계 기준선이 아니다.

## 구현한 연결

`기존 PLAN → 명시적 업무 연결 → 코드·시험 소스 → 실행 전 입력 고정 → 실행 결과 → 현황표·GPT 인계 요약`

1. [목록 생성기](../../../../../eng/planning-inquiries/app-production/catalog.mjs)는 기획 목차·기획 폴더·기존 문답 자료 등록을 함께 읽고 정본/판본/결과/호환 참조를 구분한다. 가장 큰 revision을 임의로 승인 판본으로 고르지 않는다. 이름 유사성만으로 제품 코드나 완료를 판정하지 않는다.
2. [관계 대장](../../../../../eng/planning-inquiries/app-production/role-app-links.json)에 역할 앱 업무 6개와 관리 도구 시험 업무 1개를 결속했다. 메뉴·조리시간·결제승인·주문자 주문내역·기사 업무·운영자 모바일이 첫 범위다. 각 역할의 실제 경로를 기록하고 웹 화면 존재를 모바일 결속으로 오인하지 않는다.
3. [실행 증거 도구](../../../../../eng/planning-inquiries/app-production/evidence.mjs)는 실행 전에 입력·계획 hash·범위·환경·Git 기준을 기록한다. 수정·추가·삭제·산출물 변조를 감지하고 결과와 현재성을 별도로 판정한다. 과거 보고 3건은 `HistoricalReport / Unknown`이며 현재 시험 성공으로 소급하지 않는다.
4. [기존 검증](../../../../../eng/validate-changes.ps1)과 [모바일 시험 패키지 도구](../../../../../eng/release/publish-mobile-field-test.ps1)에 선택적 `-PlanningWorkItemId`를 연결했다. 생략 시 기존 동작이고 `PlanOnly`는 기록하지 않는다. HTTPS·외부 서명 키·운영 금지 경계는 유지한다.
5. [읽기 전용 HTML](../../../generated/planning-app-production.html), [Markdown](../../../generated/planning-app-production.md), [JSON](../../../generated/planning-app-production.json)을 생성한다. 역할/분야/검색/근거 필터, 판본·hash·코드·시험·실행 범위·남은 확인을 조회한다. 로컬 서버는 loopback과 GET/HEAD·명시된 참조만 허용한다.
6. 선택 기획을 `handoff.md/json`으로 내보내며 원문/소스/로그/DB/비밀값은 자동 첨부하지 않는다. 기획 승인이나 제작실 자동 실행을 발생시키지 않는다. 사용 절차는 [도구 안내](../../../../../eng/planning-inquiries/app-production/README.md)에 있다.

## 조사 범위와 한계

- 목차 등록 기획 91개, 관련 자료에서 발견한 PLAN ID 106개, 기획·관련 Markdown 519개를 구분한다. 발견 수는 106개 모두 독립 승인 기획이라는 뜻이 아니다.
- 역할 앱 업무 6개의 명시적 제품 코드와 시험 소스가 존재한다. 나머지 기획은 목록화했을 뿐 세부 의미 충족이나 코드 부재를 전수 판정하지 않았다.
- `.csproj` 1개는 빌드 설정이므로 제품 구현 근거로 인정하지 않는 경고를 유지한다. 누락 오류가 아니며 임의로 제품 코드라고 분류해 경고를 숨기지 않는다.
- `Current`는 명시 파일과 등록 디렉터리 범위(`DeclaredInputsOnly`)의 일치다. 전체 전이 의존성 검증·전체 제품 완료·출시 승인과 다르다.
- 증거 파일은 로컬 `artifacts/`에 남고 대장/현황에는 경로·hash·검토 요약만 들어간다. 다른 PC에 증거가 없으면 `Missing`으로 보이며 현재 통과로 승격하지 않는다.

## 검증 증거

| 구분 | 결과 | 상한 |
| --- | --- | --- |
| 관리 도구 자동시험 | **54/54 통과**, 실패/건너뜀 0 | 목록 14 + 증거 28 + CLI/순수 DOM 12 |
| 실행 전·후 증거 | `TOOL-APP-PRODUCTION / AutomatedTest / Passed / Current` 생성 | [최종 manifest](../../../../../artifacts/local/planning-app-production/r8-20260927-03/tool-tests.json), [최종 로그](../../../../../artifacts/local/planning-app-production/r8-20260927-03/tool-tests.log); 음식 업무 시험으로 합산하지 않음 |
| 범위 Fast/Task | 23경로 diff 검사 통과 | 도구·문서 범위이므로 제품 build/test는 생략됨. Node 시험은 별도 실행 |
| 생성·인계 | 동일 입력 생성/재검사·선택 기획 실제 export 통과 | [로컬 인계 사본](../../../../../artifacts/local/exports/app-production-r8-20260927-final/handoff.md), 원시 자료 미포함 |
| PowerShell 연결 | 임시 Git fixture에서 실제 scoped 검증과 manifest 재조회 통과 | 제품 build/test를 생략하는 문서 범위; 두 기존 스크립트 PlanOnly 확인 |
| 로컬 HTTP | 생성물·허용 원문 조회, 비밀 파일/상위 경로/쓰기/외부 Host 거부 시험 통과 | 실제 브라우저 렌더링 아님 |
| 화면 스크립트 | 검색·역할·미검토 필터·선택·빈 결과 순수 DOM 계약 시험 통과 | 실제 브라우저·390px 레이아웃·키보드·캡처 아님 |
| 실제 브라우저 | **차단** | 브라우저 목록이 비었고 `cua.createBrowserTab("iab", ...)`가 `Browser is not available: iab` 반환 |
| 제품·외부 효과 | 미실행 | 앱/서버 build·DB·실결제·APK·실기기·Unity·배포 없음 |

독립 코드 검토에서 검증 종류 바꿔치기와 끊어진 심볼릭 링크 출력 우회를 발견해 수정하고 회귀시험을 추가했다. `Validation` 기록을 `Device`로 연결해도 장치 통과로 표시하지 않는다. 출력 경로의 끊어진 링크도 파일 생성 전에 거부한다. 서버와 화면의 파일 제공 목록도 통일했다. 작업지시서 JSON·시험 MJS는 허용 원문으로 제공하고 원시 artifacts는 클릭 링크 대신 로컬 경로로 표시한다.

초기 실행 `r8-20260927-01/02`는 같은 로컬 상위 폴더에 보존했고 최신 참조는 최종 `03`이다. 도구 수정 뒤 초기 실행을 현재 통과로 쓰지 않는다. 선택적 검증 기록에는 고유 실행 ID를 붙여 같은 초의 Fast/Task가 서로 덮어쓰지 않도록 했다. 실제 선택 인자 실행에서도 범위 검증 manifest 생성·현재성 조회를 확인했다.

## 남은 확인과 재개

1. 브라우저 사용이 가능한 세션에서 데스크톱/390px 검색·필터·기획 선택·원문 링크·접힘 패널과 실제 화면 캡처를 확인한다. 임의 이미지로 실제 검증을 대신하지 않는다.
2. 다음 역할 앱 업무부터 기획→행동→API→원장→재조회→UI/패키지 범위를 하나씩 연결한다. 테스트를 실행할 때 새 실행 전 기준선을 남긴다.
3. 관리 도구 작업은 제품 완성도를 높였다는 판정이 아니다. 기존 7건 제품 전체시험 실패·실제 DB/HTTP·모바일 검증 결손은 기존 작업의 책임으로 유지한다.

새 Goal/WI·DB 상태·운영 기능 활성화·commit·push·배포는 수행하지 않았다.
