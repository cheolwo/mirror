# 역할별 업무조건 개발 작업지시서 r1

- 작업 ID: `WORK-ROLE-BUSINESS-OPTIONS-R1`.
- 주 책임: `operations`; 공통 계약과 소비 앱 회귀는 `integration` 검증으로 구분한다.
- 기획: [PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS r1](README.md), `Approved`.
- 승인 근거: 2026-10-07 사용자의 직전 제안 구현 요청. 단가·수수료율·실운영·commit·push 승인이 아니다.
- 인계: `NotReady / ProductImplementationPending`.
- 저장소: `https://github.com/cheolwo/mirror.git`.
- 조사 기준 branch: `dev/mirror-integration`.
- 조사 기준 HEAD: `9336637bde31ec97b5740e2a82a802dd490127ca`.
- PLAN SHA-256: `7ADD568F46273E3739BC934CB7CA0B151ADEAFA260BCA44C15237451854A47DC` (UTF-8 로컬 파일 바이트 기준, 인계 checkout에서 재검사).
- 인계 commit: 미발행. 위 HEAD에는 이 새 기획이 들어 있지 않으며 승인 인계 commit으로 사용할 수 없다.

## 현재 차단

[저장소 공통 지침](../../../../../AGENTS.md)과 [Git 작업실 교대 체계](../../../../Architecture/GitHub기반기획개발교대운영체계.md)는 `Approved` 기획, 판본/hash, 작업지시서, 정확한 기준 commit과 허용 `writePaths`의 일치를 요구한다. 이 작업의 계획은 직전까지 채팅에만 있었으므로 제품 코드를 먼저 수정하지 않는다.

기존 변경도 이번 작업의 기준선으로 확정되지 않았다. 시작 시 178개 변경 경로가 있었고 다음 경로가 특히 겹친다. 경로 수는 시작 시점의 상태이지 최종 불변 수치가 아니다.

- `Ssalddel/Services/Dispatch/Recommendation/음식배달기사제안요금Service.cs`: 기존 수정 있음.
- `Ssalddel/Extensions/ServiceCollectionExtensions.DomainServices.Operations.cs`: 기존 수정 있음.
- `Ssalddel/Services/Fulfillment/AutoFulfillmentCoordinator.cs`: 기존 미추적 구현 있음.
- `Ssalddel.Contracts/Common/Fulfillment/DeliveryCostSharingPolicy.cs`: r26의 기존 미추적 구현 있음.
- `docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`: 기존 수정 있음. 이번 계획 포인터와 현재 관문 상태만 최소 추가한다.

이 목록은 작업 중인 파일을 되돌리거나 다른 변경을 함께 커밋할 권한이 아니다. r26이 구현 중인 상태에서 음식 분담 정책·저장·선정 경로의 소유권과 최종 판본을 먼저 맞춘다.

## 읽기 기준

- [공용 컨텍스트](../../../../ProjectOverview/GptProjectContext.md), [현재 작업](../../../CURRENT_WORK.md), [확정 결정](../../../DECISIONS.md).
- [업무 실행 책임](../../../../Architecture/BusinessWorkflowResponsibilityModel.md), [Command/Event](../../../../Architecture/CommandEvent리팩토링원칙.md).
- [음식 비용 분담 r26](../../공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/delivery-cost-sharing.implementation.r26.md).
- [음식·화물 계산 분리 r12](../../공통/PLAN-OPERATIONS-FOOD-DRIVER-PAYOUT-DETAIL/pricing-business-separation.refactoring.r12.md).
- [화물 운임 인계](../../../../ProjectOverview/page-docs/cargo-pricing-handoff-r1.md).
- 화면 단계에서는 [페이지 책임](../../../../Architecture/WholeRoadmapPagePrinciple.md), [역할 앱 시각 기준](../../../../Architecture/RoleAppVisualDesignStandard.md)과 실제 대상 페이지의 책임 카드를 읽는다.

## 수정 경로와 소유권

현재 허용된 준비 작업의 `writePaths`는 다음 네 파일이다. 제품 경로는 아직 구현 허용 목록으로 확정하지 않는다.

1. `docs/AI/Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/README.md`
2. `docs/AI/Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/development-work-order.r1.md`
3. `docs/AI/PLANNING.md`의 이 기획 목차 항목
4. `docs/AI/CURRENT_WORK.md`의 이 작업 snapshot

제품 인계의 최소 후보는 사전 요금표 contract·권한 검증 UseCase·영속 원장·사건 기록과 당사자 재조회다. 구현 경로를 확정할 때 기존 합의/계약 저장소·조직 담당 권한·공유 Client를 우선 대조하고 실제 파일별 `writePaths`를 기록한다. 새 Controller·Entity·DB를 필요 확인 없이 미리 확정하지 않는다. 음식·화물 요금 서비스 및 r26 파일은 비중첩 수정이 가능한지 patch 직전에 다시 확인한다.

## 통합 단계와 반환 기준

| 단계 | 직접 결과 | 실패·회복 | 완료 증거 |
| --- | --- | --- | --- |
| 합의 원장 | 제안과 상대 수락/거절·철회, 판본 보존 | 권한 거부, 낡은 판본, 같은 키의 다른 내용, 응답 유실 | 영속·동시성·재조회 시험 |
| 음식 계산 연결 | 당사자 요금표를 기존 제안 계산·동결에 연결 | 미합의/만료를 대체 요금으로 숨기지 않음 | 기존 음식 계산·기상·시간대·제안 동결 회귀 |
| 화물 계산 연결 | 합의 운임과 기준 견적·기사 대가 분리 | 확정 의뢰 변경 차단과 원 값 유지 | 기존 차량별 운임·권한·확정 잠금 회귀 |
| 분담 연결 | r26의 동일 정책으로 음식점/주문자 부담 계산 | 한도 초과 보류와 미확정 비용 보호 | 누적 비용·정책 동결·구형 요청 회귀 |
| 역할 화면 | 동일 합의 결과 재조회와 명시적 다음 행동 | loading/error/retry/응답 미확정 보존 | 공통 UI·소비 앱 빌드와 실제 렌더 |

제품 구현 전 수정 경로를 확정한 뒤 `eng/validate-changes.ps1 -Level Fast -Paths <이번 파일들>`과 `-Level Task`를 실행한다. 공유 계약은 소비 앱을 포함하며 실제 DB/HTTP·화면·APK·실지급을 서로 대신하지 않는다. 검증 산출물은 `artifacts/local/validation/role-business-options-r1/` 아래에 두고 장기 결과는 이 작업지시서와 현재 작업에 반환한다.

## 실행 상한

현재는 읽기 조사와 승인 기획·인계 준비까지만 수행한다. 기존 기능 플래그 기본값과 `SsalddelExecution:Mode`를 유지한다. 공용 DB migration 적용, 실제 업체 접수, PG·청구·지급, 외부 알림, Unity 변경, commit·push·배포는 하지 않는다.

원래 승인 범위에서 조직 권한·공유 계약·음식/화물 금액 의미의 추가 판단이 필요하면 `FeedbackRequired`로 반환한다. 현재 인계 관문 해결 전에는 코드 또는 시험 완료로 표시하지 않는다.

## 이번 준비 검증 결과

- 두 신규 문서의 로컬 링크 17개, 새 문서의 줄 끝 공백, PLAN 파일과 작업지시서의 SHA-256 일치를 확인했다. `PLANNING.md` 범위 `git diff --check`도 통과했다.
- 문서 네 경로를 명시한 Fast `20261007-155053`은 공통 script의 전역 `git diff --check`에서 실패했다. `CURRENT_WORK.md`의 이번 추가 구간 밖 마지막 빈 줄, `docs/Changes/README.md`, `docs/ProjectOverview/page-docs/README.md`의 마지막 빈 줄을 보고했다. 다른 변경을 정리하거나 검증 도구를 이 작업에서 수정하지 않았다.
- 원시 기록: `artifacts/local/validation/20261007-155053/diff-check.log`. 문서 작업으로 build/test는 계획상 생략이며 제품 시험 성공으로 보고하지 않는다. 코드·DB·화면·APK·운영 실행·commit·push는 수행하지 않았다.
