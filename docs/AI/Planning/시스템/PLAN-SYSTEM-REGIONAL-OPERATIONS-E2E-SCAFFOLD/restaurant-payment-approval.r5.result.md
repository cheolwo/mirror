# 음식 주문 결제 승인 반영 r5 — 구현·검증 기록

- 기준일: 2026-09-27. [승인 기획](restaurant-payment-approval.r5.md), [로컬 수용 명세](restaurant-payment-approval.r5.work-order.json).
- 범위: operations의 음식 주문 승인 연결 한 구간. 실결제·환불 실행·정산·지급·운영 활성화·앱 화면·Unity 변경 없음.

## 연결한 코드

1. 기존 `토스결제승인CommandHandler`와 `I결제Provider` 경계는 유지한다. 시험에서는 Provider만 외부 통신 없는 모의 구현으로 주입한다. 화물 전용 FakePG API를 음식용으로 오용하지 않는다.
2. 기존 결제 승인 Outbox가 MediatR로 `음식주문결제승인완료EventHandler`를 호출하면 기존 `Command알림Outbox`에 승인 의도가 남는다.
3. 새 `음식주문결제승인OutboxService`는 그 대상·종류의 의도만 소비하고 결제 정본의 대상·소유자·승인 상태·금액·통화·시각 및 음식 주문을 대조한다. 취소/거절, 다른 승인 충돌은 자동 해결하지 않는다.
4. 음식 주문의 승인 내역 4필드와 의도 성공을 관계형 트랜잭션으로 함께 저장한다. 재처리는 기존 승인 내역을 덮어쓰지 않고 업무 상태·배차·상태 이력 revision을 변경하지 않는다.
5. 기존 음식점 수신함/상세와 주문자 상세 조회에 nullable `결제승인`이 추가된다. 실제 음식점 조회에서 사용하는 `FoodOrderSampleData.Clone`도 이 승인 내역을 복사한다. PG 키/원문 응답은 추가 공개하지 않는다.
6. 기존 배치 주기를 사용하는 Quartz Job/DI에 결속한다. `FoodOrderPaymentApproval:Enabled` 기본값은 false이고, 운영 설정은 바꾸지 않았다.

## 시험의 경계

- 합성 음식 주문과 결제 준비 레코드를 SQLite에 마련한 뒤 실제 승인 Handler·콘텐츠혜택 Service·승인 Outbox·음식 승인 EventHandler·새 소비자·기존 조회를 통과한다. 시험용 MediatR에는 해당 음식 승인 구독자만 연결한다.
- 실제 Controller HTTP·모든 Event 구독자·SignalR·MySQL/MongoDB·앱 결제 준비/승인 조작을 통째로 검증한 것은 아니다.
- 새 시험 30건: 정상 승인·양쪽 역할 조회, PG 실패 후 재시도, 중복 의도/새 DbContext 재처리, 정본 불일치 15종, 잘못된 payload 4종, 다른 승인 충돌, 트랜잭션 rollback/재시도, lease 재처리, 최대 재시도, 비활성/타 업무 격리, 부분 실패 격리, 취소 주문의 기존 승인 재전달, 취소 토큰을 다룬다.
- 기존 음식 정상 생명주기 시험도 회귀 범위에 포함한다. 이 시험의 NoOpSettlement나 새 시험의 과거 승인 내역을 실제 환불·음식점 정산·기사 지급 증거로 확대하지 않는다.
- 실제 PG의 동시 승인/응답 유실, 여러 MySQL worker의 동시 경쟁은 이번 시험 상한 밖이다.

## 실행 결과

- 최초 30건 중 실패 주입 2건은 시험용 SQL 감지 테이블명이 실제 `Command_알림_Outbox`와 달라 오류 주입이 안 되었다. 조건을 수정한 뒤 새 30건과 기존 음식/승인 Outbox 60건, 합계 90건이 통과했다. `artifacts/local/validation/food-payment-approval/food-payment-approval-regression.trx`.
- 음식점 응답 복사 보완과 첫 Fast build가 겹쳤고, 강화한 수신함 시험 1건이 실패했다. 혼합 산출물 가능성을 배제하기 위해 파일 변경 완료 뒤 다시 build/검증했다. 이 최초 Fast를 통과 증거로 사용하지 않는다.
- Task (`artifacts/local/validation/20260927-134624/`): `Ssalddel.v3.5.slnx` build 성공. 전체 시험 5,441건 중 5,434 통과·7 실패. 새 결제 시험 30건은 모두 통과했다. 과거 `20260927-103125/Ssalddel.Tests.trx`의 실패 이름 집합과 이번 7건이 정확히 일치한다. Task 전체 통과는 아니다.
- 남은 7건: `ConventionalArchitectureNamingTests` 1건, `OfficialFoodIngredientJourneyTests` 1건, `IntegratedBetaCatalogTests` 1건, `SsalddelApiClassificationTests` 4건(주문자·기사·화주 Controller 분류 3건 및 Common/Admin 접두어 1건). 이번 소유 범위 밖이므로 수정하지 않았다.
- 최종 Fast (`artifacts/local/validation/20260927-135000/`): 최종 migration 식별자·음식점 응답 복사를 포함한 `Ssalddel.v3.5.slnx` build 및 집중시험 90/90 통과. Task 이후 migration 식별자만 실제 EF 생성 시각으로 정리했으며 업무 코드는 바꾸지 않았다. 전체 시험을 최종 식별자 기준으로 또 실행하지는 않았다.
- 최종 식별자로 MySQL SQL 재생성 성공. 승인 컬럼 추가 4개·고유 인덱스 1개, 다른 업무 테이블 변경 없음. 기획 hash/기준 HEAD/명세의 파일 경로와 새 문서 상대 링크·공백을 대조했다.

## DB 적용 전 주의

- `20260927043832_음식주문결제승인반영`은 음식 주문에 nullable 승인 필드 4개와 고유 인덱스 1개만 추가한다. 기존 데이터 갱신/삭제나 다른 업무 테이블 변경은 없다. 임시 예정 식별자 대신 EF가 실제 생성한 UTC 식별자를 사용해 후속 migration의 시간순을 보존한다.
- MySQL SQL 생성·검사만 수행했다. `artifacts/local/validation/food-payment-approval/migration.sql`. 연결 불가능한 설계 전용 주소를 일시 주입해 생성했으며 DB 연결·migration 적용은 하지 않았다.
- 기존 모델의 한시 수요 할증 필드 9개(음식운영정책 7개, 운송실행투영 2개)가 기존 정본 migration snapshot에 빠진 별도 차이를 발견했다. 이번 생성물에 섞이지 않도록 제외했고 해당 기존 소스는 바꾸지 않았다. 정본 snapshot의 이번 diff는 승인 필드/인덱스 21줄뿐이다.
- 따라서 **모델과 migration이 완전히 일치하거나 기존 DB에 바로 적용 가능하다고 판정하지 않는다.** DB 적용 전 이 별도 차이의 소유자/선행 migration을 확인해야 한다.
- 최종 `dotnet ef migrations has-pending-model-changes --no-build`도 이 남은 차이로 exit 1을 반환했다. `artifacts/food-payment-approval-model-gap.log`. 검사를 우회하거나 기존 결손을 숨기지 않았다.
- 소비자 비활성은 DB schema 변경 필요를 없애지 않는다. 새 EF 조회 코드를 기존 DB로 실행하려면 먼저 승인 컬럼 migration과 기준선 정합성을 검토해야 한다.

## 다음 범위

1. DB migration 기존 차이를 별도 정리한 격리 DB에서 승인 조회 HTTP를 확인한다.
2. 취소/거절 → 환불 검토 및 상태 연결을 실환불 없이 검증한다. 환불 승인·보상 정책은 임의 확정하지 않는다.
3. 완료 주문 → 음식점 정산 대상·기사 대가를 구분해 연결하고 역할 앱 알림/재조회 및 실제 모바일 화면을 확인한다.

commit·push·배포·운영 DB 변경은 하지 않았다.
