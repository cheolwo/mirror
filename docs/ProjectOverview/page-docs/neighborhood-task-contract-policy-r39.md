# 건별 계약 정책 준비 · 페이지·서버·DB 책임 r39

기준 정책은 [승인 r33](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/task-contract-policy-preparation.implementation.r33.md)다. 현재는 인증된 서버 조회와 꺼진 설정 준비만 제공한다.

| 단계 | 현재 책임 |
| --- | --- |
| 페이지 | 새 화면·버튼·앱 연결 없음. 정책 정보의 추후 사용만 준비 |
| API | `GET /api/v1/common/commerce/task-contract-policy`: 인증과 계정 식별자 확인, 저장 금지 캐시 설정, 준비 정책 조회 |
| 서비스 | `NeighborhoodTaskContractPolicyService`: 계약 기본값과 비활성 상태 반환. 직접 생성도 설정 검증 |
| 설정 | `NeighborhoodTaskContractOptions`: 준비 후보 바인딩. `Enabled=true`와 잘못된 후보는 거부 |
| DB | 새 테이블·migration·개인/대리인 동의 원장 없음. 기존 거래·사진·정산·HR 원장 변경 없음 |

핵심 코드는 [공유 DTO](../../../Ssalddel.Contracts/Common/Commerce/NeighborhoodTaskContractPolicyDtos.cs), [Controller](../../../Ssalddel/Controllers/Common/통신판매보호Controller.cs), [정책 서비스](../../../Ssalddel/Services/Commerce/NeighborhoodTaskContractPolicyService.cs), [설정·검증기](../../../Ssalddel/Services/Commerce/NeighborhoodTaskContractOptions.cs), [서비스 등록](../../../Ssalddel/Extensions/ServiceCollectionExtensions.NeighborhoodTaskContractPolicy.cs)이다. 기존 통신판매 서비스 등록에 이 준비 정책을 연결했다.

일반 계정은 준비 기본값·현재 비활성 여부·미래 활성화에 필요한 조건의 종류만 조회한다. 미래 정책 참조와 나이/업무/시간 후보의 실제 설정은 응답에 포함하지 않는다. 조회 성공은 참여 승인이나 계약 완료가 아니다. `OperationalMinimumAgeYears=19`는 기존 통신판매 신규 거래 기준을 설명하며 전체 기사·조수 자격 검증을 대신하지 않는다.

[전용 시험](../../../Ssalddel.Tests/Services/Commerce/NeighborhoodTaskContractPolicyTests.cs)은 실제 로컬 Kestrel/MVC 정책 조회를 검증한다. 시험 인증은 전용 대역이며, 기존 Controller의 다른 거래·신원기관·DB 액션과 실운영 인증을 시험한 것으로 확대하지 않는다. 실제 반환 결과는 [변경 기록](../../Changes/2026-10-08-neighborhood-task-contract-policy-r39.md)을 따른다.
