[기획 · 앱 제작 표준화 · PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST · r12]

# 전체 제작 절차 연결과 음식점 메뉴 대표 실행

- 상태: Approved / ReadyToDispatch / AcceptedInCurrentThread.
- 승인: 2026-09-27 사용자가 화면·시험 APK 완료선을 선택한 뒤 제시된 r12에 `Implement the proposed plan.` 요청.
- 기준: `dev/mirror-integration`, `ca3b6e65d742bc5c00ebfb7f316ecca706061dc9`, 시작 dirty183항목. 기존 작업을 보존하며 commit/push/배포하지 않는다.
- 선행: [r9 입력](app-production-intake.r9.md), [r10 문답](app-production-guided-intake.r10.md), [r11 보완](app-production-repair-cycle.r11.md). 기존 schemaVersion v1과 업무 ID를 유지한다.

## 제작 절차와 자료

기존 입력 안내를 공통 진입점으로 확장해 자료 접수→대조/문답→승인/환경 준비→기존 기능 구현→시험/화면/포장→결과 반환을 연결한다. 각 단계는 입력, 결과물, 통과 조건, 실패 시 돌아갈 곳을 명시한다. 새 생성기·점수·업무 엔진·운영 DB는 만들지 않는다.

이전 메뉴 입력3개는 보존하고 r6 입력 정책/r7 이름 충돌과 한계를 반영한 새 `.r2.json` 입력3개를 같은 `APP-RESTAURANT-MENU`에 연결한다. 새 입력의 제품 선택은 기존 메뉴 범위를 재사용하고 이번 승인 이상의 동시성·앱 종료 복구·금전 정책을 만들지 않는다. 확인한 입력과 이번 승인 근거는 새 검토 자료에 결속하되 환경 준비는 실제 확인한 단계만 Ready로 둔다.

최신 기획·작업지시서·시험·결과를 관계 대장에 연결한다. 과거 결과는 HistoricalReport이며 새 실행 전 기준선과 종료 증거만 EvidenceManifest로 남긴다. 옛 입력의 Changed 충돌은 HTML/Markdown/인계에서 입력 당시 기록이라고 표시한다. 다른 과거 미연결 업무를 미구현으로 판정하지 않는다.

## 대표 실행 범위

- 기존 `RestaurantDeskApp` 메뉴 Razor/Client/인증과 `eng/RestaurantMenuPreview`를 재사용한다. 합성 화면은 유지하고 명시적인 격리 API 연결 선택을 추가한다. 연결 실패를 합성 자료로 대체하지 않는다. 계정/토큰은 메모리에만 두며 로그/화면/저장소로 내보내지 않는다.
- 기존 `eng/Ssalddel.RoleAppHeadlessE2E`에 `--scenario restaurant-menu` 선택을 추가한다. 이 선택은 메뉴 인증/GET/POST/PUT만 실행하고 주문/기사/결제/Unity 전이를 하지 않는다. 기존 기본 실행은 보존하며 누락 메뉴 인터페이스 링크를 보완한다.
- 전용 food-observer `Development + Simulation`과 기존 전용 MySQL/Mongo만 사용한다. 현재 실행 중인 Hongdal/unity-review 환경을 변경하거나 볼륨 삭제/자동 migration으로 해결하지 않는다. 스키마/환경 불일치는 Blocked로 반환한다. 전용 서버 Start(배달 시나리오)는 호출하지 않는다.
- 업무시험은 정상 등록/수정/재조회, 동일 내용 재요청, 잘못된 길이/URL, 이름/오래된 revision 충돌, 비인증/다른 역할 거절과 저장 불변을 확인한다. 다른 가게 소유권은 기존 SQLite 시험과 구분하며 실제 MySQL 경합·앱 종료 복구는 제외한다.
- 390px 모바일 폭에서 목록→등록→수정→충돌→재조회 화면을 확인하고 캡처한다. API 연결 브라우저와 Android 실제 화면은 별도다. 접근 불가 시 합성/정적 근거로 API UI 통과를 대신하지 않는다.
- 기존 포장 도구에서 `-AppNames restaurant`만 선택한다. HTTPS·외부 키 요구를 유지하며 준비 전 APK는 Blocked다. 가짜 주소·Release 보안 완화·임의 키 생성으로 통과시키지 않는다. 장치 설치/스토어 업로드는 제외한다.

## 인수와 반환

실행 전에 승인 PLAN/hash·쓰기 경로·공유 파일 소유·입력 판본을 확인하고 새 증거를 연결한다. 관리 도구 시험과 생성물 검사, 메뉴 집중시험, 실제 API/DB, 화면, APK를 따로 반환한다. 코드 수정 뒤 정본 재조회·거절 데이터 불변과 기존 전체시험 실패를 분리한다. 입력/공유 의존성 변경 시 해당 실행을 재검증한다.

제품 route/DTO/DB schema 변경은 없다. 범위 밖 결손은 별도 후보로 반환한다. 환경 차단이 남으면 절차 정리/부분 검증 결과만 완료로 표시하고 대표 앱 전 과정 완주를 주장하지 않는다. 결과는 같은 폴더의 `app-production-reference-run.r12.result.md`와 CURRENT_WORK에 기록한다.
