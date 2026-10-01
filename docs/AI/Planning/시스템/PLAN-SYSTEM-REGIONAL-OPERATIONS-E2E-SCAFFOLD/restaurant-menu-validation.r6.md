[기획 · 음식점 메뉴 검증 · PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD · r6]

# 메뉴 입력과 서버 검증 정합성

- 상태: Approved / ScopedImplementation. 2026-09-27 메뉴 불일치 정리·등록/저장/재조회 확인 제안에 대한 사용자 “어, 진행해봐” 승인.
- 선행: [메뉴 r2](restaurant-menu-mobile.r2.md), [탐색 r4](restaurant-navigation.r4.md). 기존 로컬 변경을 보존한다.
- 허용 범위: `Ssalddel.Contracts/Food/음식점메뉴입력Policy.cs`, `RestaurantDeskApp/Components/Pages/Menus.razor`, `Ssalddel/Application/Food/음식점메뉴관리UseCase.cs`, `Ssalddel.Tests/Application/Food/음식점메뉴관리UseCaseTests.cs`와 관련 기획/목차/현재 작업 문서.
- 기존 저장 모델을 기준으로 trim 후 메뉴명200자·소개1000자·사진 URL1000자를 공유 검사한다. 사진 URL은 빈 값 또는 절대 HTTPS 주소다. 자동 절단하지 않는다. 기존 자료를 일괄 수정하지 않으며 과거 비HTTPS 값의 새 저장은 거절한다.
- 등록/수정 모두 영속 변경 전에 검사한다. 기존 인증·음식점 범위·revision·메뉴명 기반 재등록 처리는 유지한다. 공개 route/JSON 필드 변경, migration, 운영 DB 접근, 배포는 제외한다.
- 검증: 제한 경계/초과·잘못된 URL·거절 시 불변·등록/수정 후 재조회·같은 요청 재시도·다른 음식점 범위·stale revision을 집중시험한다. 관련 build와 Fast/Task 결과를 별도 기록한다.
- 한계: InMemory 시험은 실제 DB 동시 경합·HTTP 인증·Android UI·앱 종료 후 복구·요청 ID 영속 멱등성의 증거가 아니다.
- r9 입력 예제/승인 hash는 소급 수정하지 않는다. 제품 코드가 바뀐 입력 근거는 후속 재검토 대상이다.
