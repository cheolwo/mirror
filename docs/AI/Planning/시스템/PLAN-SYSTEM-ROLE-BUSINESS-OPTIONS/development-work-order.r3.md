# 역할 옵션 수평 표시 보완 작업지시서 r3

- 작업: `WORK-ROLE-OPTIONS-HORIZONTAL-R3`, 주 책임: `operations`.
- [승인 기획](horizontal-hardening.implementation.r3.md): r3, SHA256 `96725BEE35968CAAE7C91865B2843E578BF6BC55841BCE57765E8ED8425A5B75`.
- 현재 결과: `AcceptedLocal / FirstSliceImplemented / LocalPreviewValidated`. 최초 수용 상태는 `ReadyToDispatch / AcceptedLocal / ImplementationInProgress`였다.
- 승인·수용 근거: 사용자의 전반적 보완 우선·단일 업무 수직 보완 후속 요청, 같은 현 작업트리에서 이 기획의 비금전 표시 범위만 수용.
- 기준 branch/HEAD, 정확 `writePaths`, 담당·실행 상한은 승인 기획을 따른다. r1의 별도 금전 구현 관문이나 다른 Git 작업실 인계를 대신하지 않는다.

## 조립과 검증

1. 대상 clean 여부와 기획 hash를 대조하고 현재 입력·바인딩·서버 행동을 보존한다.
2. 음식·화물 표시를 비중첩 파일로 병렬 보완하고 root가 소스와 결과를 통합한다.
3. 신규 표시 회귀와 기존 상태·입력·금액 보존 시험, 공통 UI·소비 preview를 검증한다. Fast/Task는 이번 경로와 관련 filter로 제한한다.
4. 예시 GET만 제공하는 Preview로 실제 공통 Razor를 320/390px에서 렌더한다. 실제 업무 HTTP·외부 요청·저장·PG·지급은 사용하지 않는다.
5. [변경 결과](../../../../Changes/2026-10-07-role-option-scope-r3.md)와 현재 작업으로 통합·미검증 범위를 반환한다.

개인 기본값 저장·기상/소리 알림 실제 수신·합의 요금표·DB migration·APK/실기기·Unity·운영 활성화는 이번 완료 주장이 아니다. 코드·시험·실제 렌더를 각각 기록한다. commit/push·배포는 하지 않는다.

## 구현 반환

- 공통 제품 Razor 5파일의 표시 의미와 예시 GET 조립, 신규 회귀 9개를 반영했다. 기존 입력·금액·권한·가능 행동·상태 전이·API/DB는 변경하지 않았다.
- Fast 집중 187개 통과, Task 전체 솔루션 build 오류 0(경고 111)·시험 8,397개 통과·실제 Mongo 2개 건너뜀, preview build 오류/경고 0.
- 실제 공통 Razor 320/390px 12개 장면과 입력/확인/복귀/미리보기 저장 차단 6개 흐름을 통과했다. 화면은 합성 예시이며 실운영·네이티브·휴대폰 검증이 아니다.
- 승인 기획 hash와 r1을 보존하고 [변경 결과](../../../../Changes/2026-10-07-role-option-scope-r3.md)에 원시 검증 위치·경고·미검증 범위를 결속했다. 로컬 preview는 종료했다.
- 이번 첫 보완 뒤 나머지 옵션 연결을 이어갈 수 있다. 후속 단일 업무 수직 검증의 대상은 미정이며 지금 임의 선택하지 않았다.
