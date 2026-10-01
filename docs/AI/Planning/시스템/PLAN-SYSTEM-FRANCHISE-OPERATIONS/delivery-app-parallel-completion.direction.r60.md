# [기획 · 운영 원칙·앱 완성 · PLAN-SYSTEM-FRANCHISE-OPERATIONS · r60]

- 기준일: 2026-09-28.
- 상태: `FourPercentOperatingProfitTargetRetained / DeliveryAndAppCompletionParallelDirectionConfirmed`.
- 선행: [4% 기준 대조 r59](four-percent-policy-basis-review.r59.md), [공동 참여·정산 우선 r57](participatory-settlement-transparency.direction.r57.md), [참여자 수수료 표시 r58](participant-platform-fee-visibility.decision.r58.md).

## 확정

사용자는 기존 기준을 유지하겠다고 답했다. `four-percent-fee-or-margin-basis`는 `ConfirmedExistingOperatingProfitTarget`으로 종결한다. **플랫폼 보유 매출에서 운영비를 뺀 영업이익을 약 4% 목표로 삼는 기존 정책**을 유지하며, 거래마다 4%를 공제하는 새 수수료율로 바꾸지 않는다. 분모·통과자금·유보금·순환 가능 현금은 [기존 사용량·구독 기획](../PLAN-SYSTEM-USAGE-COST-SUBSCRIPTION-METERING/README.md#영업이익-4-목표와-자금-순환)을 따른다.

배달 활동은 배달 활동대로 지속하고, 앱도 별도의 작업으로 완성해 나간다. 현장 경험과 기록을 앱 개선에 참고하되, 새로운 촬영·쇼츠·방문 자료가 더 쌓여야 이미 정한 앱 기능을 완성할 수 있다는 의존성을 만들지 않는다. 쇼츠 제작 개선은 이번 범위에서 계속 제외한다.

이는 자동화 예약·새 스레드 실행·실영업·정산 지급·배포 승인이 아니다. 현재 문답에서는 방향과 기존 완성 계획의 연결만 기록한다.

## 재사용할 앱 완성 기준

첫 역할 앱은 [음식점 우선 기존 결정](../PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/restaurant-mobile-workflow.r1.md)의 `RestaurantDeskApp`을 유지한다. 앱 선택을 새로 질문하거나 운영자·기사·주문자 앱을 동시에 전면 개편하지 않는다.

| 구간 | 이어갈 범위 | 완료를 구분할 근거 |
| --- | --- | --- |
| 첫 마무리 단위 | 메뉴 등록·수정·재조회·오류 회복 | 기존 r12의 집중 시험, 격리 인증 API/DB, 실제 화면, 시험 APK를 각각 확인 |
| 음식점 업무 연결 | 주문 알림·수락/거절·조리시간·픽업 준비 | 같은 주문의 서버 상태와 음식점 화면이 이어지는지 확인 |
| 정산 투명성 | 본인 거래의 금액 구성·수수료 근거·보류·지급 상태 | r57/r58을 기존 재무 계약에 연결한 별도 명세와 시험 필요. 모의 계산과 실제 지급을 구분 |
| 전달·출시 | 사용자가 검토할 화면과 시험 설치 파일 | APK 생성, 기기 설치, 로그인·복귀, 스토어 배포를 별도 단계로 유지 |

이는 기존 승인 잔여 작업을 완성하기 위한 연결 순서의 제안이다. r57의 정산 우선을 취소하거나 새 기능 개발의 순서를 메뉴 우선으로 바꾸는 확정이 아니다. 이후 신규 기능은 본인 정산 상세의 투명성을 우선 검토한다. 정산을 제외한 앱을 전체 완성으로 부르지 않으며, 메뉴 검증을 끝내기 위해 다른 앱 전체나 Unity 디오라마의 완성을 선행 조건으로 추가하지 않는다.

## 기존 승인 작업과 현재 확인

[음식점 메뉴 대표 실행 r12](../PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-reference-run.r12.md)와 [작업 명세](../PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-reference-run.r12.work-order.json)가 이미 존재한다. 승인 상태·정확한 쓰기 경로·검증 상한·제외는 그 문서가 소유하며 이번 방향 문서로 확장하지 않는다.

이번 읽기 확인에서 r12 본문 SHA-256과 작업 명세가 일치했고 현재 HEAD도 명세의 기준 commit과 같았다. 다만 작업트리는 기존 변경이 많으며 `app-production-reference-run.r12.result.md`는 없다. 승인된 계획의 존재를 실제 API·화면·APK 실행 완료로 해석하지 않는다. 현재 작업 소유권·입력 판본·환경 준비와 실행 증거는 개발 재개 때 다시 확인한다.

부분 코드는 이미 있다. [메뉴 Preview](../../../../../eng/RestaurantMenuPreview/Program.cs)에 명시적 API 연결 선택과 실제 Client를 사용하는 소스가 있다. 반면 [역할 앱 검증 Runner](../../../../../eng/Ssalddel.RoleAppHeadlessE2E/Program.cs)에는 r12의 메뉴 전용 시나리오 선택이 확인되지 않고 새 r2 입력3개·r12 결과 문서도 없다. 현재 기본 Runner는 전체 배달 흐름을 실행하므로 메뉴 검증 목적으로 그대로 실행하지 않는다. 이 대조는 소스 존재 확인이며 실제 연결·시험·화면 완료 증거는 아니다. r12 전체를 미착수로 표시하지 않는다.

다음 개발 진입 후보는 새 기획을 늘리는 것이 아니라 **기존 r12 메뉴 대표 실행의 남은 연결과 검증**이다. r12에는 정산 정책·제품 API/DB schema 변경이 허용되어 있지 않으므로 정산 보완은 별도 범위로 명세화해야 한다. 이번 문답에서는 실제 실행이나 추가 제품 변경을 하지 않았다.

## 미정 / 다음 질문

- 미정: 정산 상세의 세부 계약·이의 처리, 실제 실행 환경 준비·기기/출시 증거. 기획 선택과 기술 확인을 구별한다.
- `orderer-own-platform-fee-visibility`는 앱 완성에 필요한 시점까지 보류한다. 4% 목표 유지 답변을 주문자 공개 범위 승인으로 확대하지 않는다.
- 다음 질문: **없음.** 4%의 의미와 첫 역할 앱·메뉴 대표 실행의 완료 범위는 이미 정해져 있어 반복 질문하지 않는다. 기술 결손은 기존 작업 명세에 따라 조사하고 사용자 선택이나 외부 권한이 필요한 때만 후속 질문한다.

## 작업 범위

이번에는 기획·목차·현재 상태만 변경했다. 제품 코드·DB·서버 실행·테스트 앱·실결제·지급·Unity·영상·외부 게시·자동화·commit·push 변경 없음. 문서 검사와 제품 실행·배포는 별도 증거다.
