# 음식 배송비 선택 옵션과 부담 확인 r26

2026-10-07. [승인 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/delivery-cost-sharing.implementation.r26.md), [페이지 책임](../ProjectOverview/page-docs/delivery-cost-sharing-r26.md)을 따른다.

## 문제와 구현 범위

기존 음식 배송비 지원은 정액 하나만 설정할 수 있어 음식점별 부담 방식과 주문자의 잔여 부담을 설명하기 어려웠다. 네 방식과 양측 조건을 옵션으로 설정하고 공통 계산에서 선정과 화면 금액을 일치시킨다. 기존 정액 설정/진행 주문과 생활·화물의 비용 구조는 보존한다.

## 현재 상태

`Implemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`. 서버·정책·저장과 기존 공통 UI의 구현 및 로컬 검증을 완료했다. 기준선 12,879파일 해시는 `artifacts/local/fulfillment-cost-sharing-r26/preservation-before.json`에 보관한다. 관련 없는 UI csproj·기존 r24/r25 작업·branch·HEAD를 보존한다. 실제 청구/지급·공용 DB 적용·업체·운영 단말·외부 공유/배포·commit/push는 별도다.

## 선택과 계산

| 방식 | 음식점 입력 | 주문 전체 비용의 배분 |
| --- | --- | --- |
| 주문자 전액 부담 | 부담 방식만 선택 | 주문자가 전체 비용 부담 |
| 정액 지원 | 지원액 | 음식점은 비용과 지원액 중 작은 금액, 주문자는 잔액 |
| 비율 분담 | 부담률·한도 유무·선택한 한도 | 누적 비용에 비율을 적용해 원 미만 내림, 한도를 적용한 나머지는 주문자 부담 |
| 음식점 전액 부담 | 한도 유무·선택한 한도 | 음식점이 전액 부담. 한도 초과 후보는 제외하며 초과액을 주문자에게 넘기지 않음 |

주문자의 최대 부담액은 이 결과에 별도로 적용한다. 자동 배송 사용과 비용 분담 방식을 구분하고, 비용 조건과 완료 기한·허용 처리처·정보 동의를 별도 묶음으로 표시한다. 계산 예시는 배송비 6,000원 가정임을 명시한다. 아직 견적이 없는 주문은 견적 대기, 한도 초과 최초 보류는 견적 미확정, 발생비용 미확인은 확인 필요로 표시한다. 유효한 실제 0원은 미확인과 구별한다.

## 저장과 기존 주문

설정·최초 주문 동의에 정책 전체와 판본을 보존한다. 재선정 비용은 확정 기발생 비용과 새 견적을 합쳐 같은 계산기로 배분하고, 동의 갱신도 원 정책을 유지한다. 음식점 설정 변경은 새 주문에만 적용된다. 구형 정액 설정과 동의는 null 정책/명시적 정액 JSON 모두 호환되며, 비율·전액 등에는 정책 전체 확인이 필요하다. 응답 유실은 원 요청 ID와 원 정책 JSON으로 재확인한다.

`20261007180000_AddDeliveryCostSharingPolicies`는 설정의 `CostSharingPolicyJson`과 동의의 `FrozenCostSharingPolicyJson`을 nullable longtext로 추가한다. 기존 값을 덮어쓰거나 backfill하지 않는다. null은 해당 row의 기존 정액으로 해석하고 손상 JSON을 정액으로 대체하지 않는다. 자동 Down은 정책·동의 의미 유실을 막기 위해 실행 전에 거절한다. 서버 연결 전에 선행 r24/r25와 이번 스키마가 필요하며 기본 OFF가 스키마 요건을 생략하지 않는다. SQLite 적용 및 MySQL SQL 생성과 실제 공용 MySQL 적용은 구분한다.

생활/화물 배송의 비용 정책은 이번에 활성화하지 않는다. Unknown·취소 미확정·발생비용 미확인·조리 시작 뒤 전환 제한·중복 수행·판본·권한 관문은 기존 기준을 유지한다. 실제 청구·지원금 지급·은행 입금·기사 정산 기능은 추가하지 않았다.

## 검증 결과

| 검증 | 결과·근거 |
| --- | --- |
| 집중 시험 | 249 통과 / 실패·건너뜀 0. `artifacts/local/fulfillment-cost-sharing-r26/focused/r26-focused.trx` |
| Fast | 190 통과 / 실패·건너뜀 0. `artifacts/local/validation/20261007-160301/` |
| Task | 8,388 통과 / 실패 0 / 건너뜀 2. `artifacts/local/validation/20261007-163406/` |
| 소비 앱 포함 빌드 | Fast·최종 Task의 `Ssalddel.v3.5.slnx` Debug 빌드 통과. 실제 휴대폰 설치·실행과 구별 |
| 새 공통 UI | 실제 Razor·ViewModel, 20장면 × 320/390px = 40 통과. 소스 변동·차단된 외부 요청 0 |
| 기존 r25 UI 회귀 | 13장면 × 320/390px = 26 통과. 소스 변동·차단된 외부 요청 0 |
| migration | 집중 시험에 포함된 3개 시험으로 기존 값 보존/SQLite 실제 적용/정책 JSON 재조회/MySQL SQL/EF snapshot을 대조 |

Task의 건너뜀 2개는 개인정보 보호지원·통신판매의 실제 격리 Mongo 연동 환경 미설정에 따른 기존 시험이다. 이 두 실제 연동을 통과한 것으로 보고하지 않는다. 빌드에는 NU1608 패키지 제약, 기존 nullability/xUnit/플랫폼 경고와 Naver Map SDK 미설정 안내가 남는다. Fast 빌드는 경고 157 / 오류 0이며 기존 `FoodRoleWorkspaceAdapterTests`의 CS8601도 그대로다.

첫 일반 출력 빌드는 다른 작업의 APK/복원과 충돌했다. 검증 프로세스에만 `UseArtifactsOutput`·절대 `ArtifactsPath`·임시 `DirectoryBuildPropsPath`를 설정해 기존 root/nested props를 가져오고 bin/obj를 `artifacts/local/fulfillment-cost-sharing-r26/isolated-output/`에 분리했다. 저장소의 프로젝트 설정이나 다른 작업의 프로세스는 변경하지 않았다.

첫 격리 Task `20261007-162248`는 8,385 통과 / 경로 탐색 실패 3 / 건너뜀 2였다. 실행 DLL은 16:12 생성본이고, 다른 작업에서 16:28 갱신한 공통 저장소 탐색 helper가 아직 반영되지 않았다. 기존 4단계 상위 폴더 계산이 격리 경로를 저장소로 오인한 것으로 확인했다. 최신 helper를 다시 빌드한 최종 Task에서는 세 시험을 포함해 전체가 통과했다. 배송비 제품 소스 변경 없이 해결했으며 실패 로그와 `task-recovery-reason.json`을 보존한다.

화면 결과와 PNG는 [페이지 책임·화면 기록](../ProjectOverview/page-docs/delivery-cost-sharing-r26.md#로컬-화면-확인)에 연결한다. 실제 소비 앱 빌드·합성 응답 UI·격리 SQLite 시험과 운영 DB/업체/단말 실행은 서로 다른 증거다.

## 범위와 보존

전체 해시 대조는 기준선 대비 이번 관련 50경로와 다른 작업의 18경로를 구별했으며, 파일 누락 0·branch/HEAD·관련 없는 UI csproj 보존을 확인했다. 이후 최종 확인에서도 12,879파일의 존재·r26 제품 소스 해시·보호 csproj·branch/HEAD가 유지됐다. 최종 존재 확인을 전체 파일의 두 번째 해시 대조로 표현하지 않는다. 다른 작업의 새 저장소 탐색 helper 및 세 시험 수정도 보존하며 r26 소유 변경에 포함하지 않는다.

최종 근거는 `artifacts/local/fulfillment-cost-sharing-r26/implementation-verification.json`, `preservation-after.json`, `ownership-classification.json`, `final-preservation.json`에 있다. 공유 DB migration 적용·실제 업체 선정/접수·PG·지원금 출금·은행 지급·기사 정산·휴대폰·Azure·Unity·MYBOX·commit/push는 실행하지 않았다. 운영 사용 전에는 선행 스키마와 실제 업체·업무·지급의 별도 검증이 필요하다.
