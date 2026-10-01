[기획 · 음식점 메뉴 보완 · PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD · r7]

# 기존 이름으로 수정할 때의 충돌 처리

- 상태: Approved / ScopedImplementation. 2026-09-27 사용자: 새 기능을 늘리기보다 계속 보완하고 개발에서 앱 제작의 보편 절차를 추출해 달라는 요청.
- 선행: [입력 정합성 r6](restaurant-menu-validation.r6.md). 기존 메뉴명 고유 인덱스의 의미는 변경하지 않는다.
- 결손: 수정 시 같은 음식점의 다른 메뉴 이름을 쓰면 DB 고유 제약 예외가 발생하며, 기존 PUT은 이를 업무 충돌로 안내하지 않는다.
- 이번 범위: 소유 음식점·현재 revision 검사 뒤, 변경 전에 동일 음식점의 다른 메뉴가 이름을 사용 중인지 검사한다. 같은 파일의 `음식점메뉴명충돌Exception : InvalidOperationException`으로 알려진 충돌만 POST/PUT에서409로 반환한다. 일반 내부 오류를 업무 거절로 바꾸지 않는다. 자기 이름 유지/다른 음식점의 동일 이름은 허용한다.
- 허용 제품 경로: `Ssalddel/Application/Food/음식점메뉴관리UseCase.cs`, `Ssalddel/Controllers/Food/음식점메뉴관리Controller.cs`. 검증 경로: `Ssalddel.Tests/Application/Food/음식점메뉴충돌Tests.cs`. 관련 PLAN/결과/목차/현재작업/생성 현황표만 함께 갱신한다.
- 검증: 기존 DB 모델을 사용한 메모리 SQLite에서 정상 등록/수정/독립 재조회, 이름 충돌 전후 값·revision 불변, 다른 음식점 범위, stale revision, Controller409를 확인한다. 시험 코드를 먼저 추가해 실패를 재현한 후 보완한다.
- 제외: DB 구조/migration/운영 DB, 동시 쓰기 경쟁 조건의 완전 해결, 요청 ID 영속 원장, 신규 UI, 배포/commit/push. 사전 중복 조회 뒤에 생기는 동시 중복은 여전히 DB 제약이 최종 방어이며 이번409 보장의 범위 밖이다. Controller 직접 호출 시험은 실제 인증 HTTP 시험이 아니다.
- 제작 규칙은 체크리스트 후속 판본에 조건·근거·한계를 갖는 후보로 기록한다. 새 규칙 엔진이나 앱 생성기를 만들지 않는다.
