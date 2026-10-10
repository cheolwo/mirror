# 살뜰마트 포장 전표·기사 픽업 표본 페이지 책임 r56

- 기획: [승인 r56](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-package-pickup.implementation.r56.md)
- 기존 route: `/workspace/warehouse/ssalddel-mart-sample`
- 실제 표시 범위: 전용 loopback `Development + Simulation` 표본. 운영 인증·권한 화면이 아니다.
- 페이지 원칙: [한 가지 업무 질문·결과](../../Architecture/WholeRoadmapPagePrinciple.md), [역할 앱 시각 기준](../../Architecture/RoleAppVisualDesignStandard.md).

| 책임 | 포장 담당자 관점 | 기사 픽업 관점 |
| --- | --- | --- |
| 사용자·대상 | 합성 담당자와 선택 주문 | 배정된 합성 기사와 같은 주문 |
| 질문·결과 | 어떤 상품을 모아 어떤 전표를 붙여 준비할까? 전표 준비·상품 포장 | 이 포장이 내 배정 건인가? QR 인식 또는 번호 제출로 인수 |
| 기본 정보 | 주문 표시번호·상품/수량·선반·현재 작업·전표 준비·인쇄 | 매장·주문 표시번호·포장 1/1·현재 준비·QR 스캔·번호 대체 |
| 주 행동 | 현재 단계의 피킹/포장 완료. 전표 출력은 독립 보조 행동 | QR 스캔 시작. 유효 인식이 즉시 인수 요청이며 추가 완료 버튼 없음 |
| 상세 정보 | 전표 QR·번호·재출력/포장 전 재발급·전체 상품 진행 | 카메라 제한·현재 결과와 실패 이유 |
| 실패 | 미피킹·전표 없음·인쇄 창 실패는 해당 작업만 재시도 | 틀린 코드·미준비·무효·번호 제한·통신 실패는 인수를 완료하지 않음 |
| 복귀·수명 | 선택 주문과 기존 작업을 보존. 표본 초기화는 명시 확인 | 관점 전환/주문 변경 시 카메라·번호·대기 입력 폐기, 확정 원장 상태 유지 |

같은 물리 페이지의 상호 배타적 관점 모드로 입력과 주 행동을 분리한다. 이 전환은 표본 전용이며 제품 역할 권한을 부여하지 않는다. 정산·지급·실제 배차는 이 페이지의 독립 업무로 추가하지 않는다.

코드 연결은 기존 `SsalddelMartSamplePage` → 전표/카메라 컴포넌트 → `SsalddelMartSampleRuntime` → 기존 피킹/포장 UseCase와 표본 전표·인수 SQLite다. 전표 HTML은 공통 문서 출력 서비스를 사용하며 QR에 개인정보를 담지 않는다. 인쇄 Adapter의 결과는 출력 대화상자 요청이지 종이 출력 성공이 아니다.

실제 렌더·자동 시험·물리 인쇄·실제 인증 API·APK는 별도 결과다. [개발 반환·샘플 화면](../../Changes/2026-10-10-ssalddel-mart-package-pickup-r56.md)에 새 구현과 집중 C# 69건·JS 62건·3폭 UI 21건·실제 QR 픽셀 디코딩 12건을 기록했다. 카메라·인쇄 창은 모의이며 80mm 전표 HTML과 표본 Runtime만 검증했다. 실제 인증 API·APK·물리 인쇄·영속 운영 복구는 미검증이다.
