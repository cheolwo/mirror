# 음식점 메뉴명 충돌 r7 — 구현·검증 결과

2026-09-27. [승인 범위](restaurant-menu-conflict.r7.md), [로컬 수용 명세](restaurant-menu-conflict.r7.work-order.json).

## 보완한 기존 동작

- 같은 음식점의 다른 메뉴 이름으로 수정하면 저장 전 거절하고 Controller가409를 반환한다. 소유 음식점·revision 검사를 먼저 수행하며, 자기 이름 유지와 다른 음식점에서만 쓰는 이름은 허용한다.
- `음식점메뉴명충돌Exception`으로 확인된 이름 충돌만 분류한다. 등록의 기존 중복 거절도 같은 예외로 구분하며, 일반 내부 `InvalidOperationException`을409로 감추지 않는다.
- 변경 제품 파일은 `음식점메뉴관리UseCase.cs`와 `음식점메뉴관리Controller.cs` 두 개다. 신규 `음식점메뉴충돌Tests.cs`에10건을 추가했다. 새 화면·API·DB 구조·운영 정책은 추가하지 않았다.

## 실행 증거

| 실행 | 결과 | 기록 |
| --- | --- | --- |
| 수정 전 실패 재현 | 8건 중5통과·3실패. 중복 이름 수정2건과 Controller409 시험에서 SQLite 고유 제약 예외 재현 | `artifacts/local/validation/restaurant-menu-conflict-r7-red/menu-conflict-red.trx` |
| 중간 Fast | 빌드·집중검사 통과. 이후 전용 예외와 일반 오류 구분 시험을 추가했으므로 최종 증거는 아래 실행을 사용 | `artifacts/local/validation/20260927-182359/` |
| 최종 Task | `Ssalddel.v0.0.slnx` build 성공. 전체5464건 중5457통과·7실패·skip0 | `artifacts/local/validation/20260927-182717/` |
| 최종 집중검사 | 충돌10 + 메뉴 입력15 + 화면 구성25 = 50/50 통과·실패0·skip0. 최종 Task 빌드 산출물로 실행 | `artifacts/local/validation/restaurant-menu-conflict-r7-final/menu-conflict-final.trx` |

전체 실패7건의 testName 집합은 이전 r6 `20260927-180721/Ssalddel.Tests.trx`와 차이0이다. 명명 문서1건·WebApp capability 분류1건·역할 API metadata3건·Controller 명명1건·공식 재료 화면1건이며, 전체 시험 통과는 아니다. 이 범위 밖 결손과 기존 build 경고를 이번에 수정하지 않았다.

문서12경로 Fast·diff 검사도 통과했다(`20260927-183337`, 제품 build/test 생략). 생성 현황표 Write/Validate가 일치하며 기획106개·문서529개·업무7개·경고9개다. 경고에는 기존 미검토/참조 구분과 이전 증거 결속 변경, 메뉴 입력의 출처 hash4개 변경이 포함된다. 이를 앱 미구현 수나 전체 실패로 해석하지 않는다. 승인 기획과 작업 명세의 SHA256도 일치한다.

## 확인한 범위와 남은 경계

- 실제 EF 모델의 메모리 SQLite를 사용해 저장·고유 제약·독립 Context 재조회를 확인했다. 거절 전후 메뉴 필드·revision·갱신 시각 불변, 자기 이름 수정, 다른 음식점 범위, 오래된 revision 우선 거절, 등록 재시도, 실제 Controller의409/404 및 일반 오류 전파를 확인했다.
- Controller 직접 호출은 인증 HTTP가 아니다. 실제 인증/오류 미들웨어·MySQL/MongoDB·Android 화면·알림·장치·APK·실결제는 실행하지 않았다.
- 사전 중복 조회와 저장 사이 동시 요청 경쟁은 해결하지 않았다. DB 고유 제약이 최종 방어이며 경쟁 시409를 보장하지 않는다. DB 동시성 토큰·요청 ID 영속 멱등 원장·앱 종료 후 복구도 이번 범위가 아니다.
- 기존 r9 입력 초안의 출처 hash와 검토 상태는 보존한다. 과거 승인을 갱신해 재검토 경고를 지우지 않는다.

## 앱 제작 절차로 환류

| 적용 조건 | 관측 문제 | 보완 방법 | 근거와 한계 |
| --- | --- | --- | --- |
| 이름 등 기존 업무 제약으로 입력을 거절해야 하는 저장 작업 | DB 예외가 정상적인 업무 충돌 안내를 대신함 | 변경 전 검사 → 특정 업무 오류 → 응답 분류 → 별도 조회로 저장 불변 확인 | SQLite·직접 Controller10건. 다른 업무의 오류 의미·DB 비교 규칙·동시성은 별도 대조 |

[보완 순환 r11](../PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-repair-cycle.r11.md)에 조건부 후보로 연결했다. 이번 성공만으로 모든 앱의 보편 규칙이나 공통 코드로 승격하지 않는다.

다음 작은 보완 후보는 메뉴 Client가 빈 성공 응답을 저장 완료로 받아들이는지 확인하고, 결과 미확인과 확정 성공을 구분하는 것이다. 실제 인증 HTTP·MySQL 경합 검증은 별도 환경 범위로 남긴다. 후보 발견은 구현 완료나 자동 착수 승인이 아니다.

기존 dirty 작업 보존. commit·push·배포·운영 DB 변경 없음.
