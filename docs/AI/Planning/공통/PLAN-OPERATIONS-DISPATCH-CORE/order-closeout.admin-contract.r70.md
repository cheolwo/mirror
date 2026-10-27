# r70 동일 의뢰 운영자 조회 계약 보완

2026-10-19, 같은 스레드의 ‘막는 문제부터 보완하고 화주·기사·운영자의 한 주문 처리를 검증’ 승인에 포함되는 구현 범위 보강이다. 원 승인 설계와 SHA는 유지한다. 로컬 역할 조회 중 운영자 응답을 읽다가 실제 계약 차이로 실패하여 기록한다.

- 실제 서버는 `정산시점`·`증빙방식`·`수납주체`를 숫자 열거값으로 보낸다. 기존 관리자 표시 DTO는 문자열이므로 운영자 단건 HTTP 조회와 같은 계약의 목록·취소환불 성공 응답 파싱이 실패할 수 있다.
- 서버/JSON 공개 계약과 관리자 표시 DTO를 변경하지 않는다. 기존 공유 화주 응답으로 먼저 읽고 관리자 표시 DTO로 명시 투영하는 제품 경로를 공통으로 둔다. 검증 도구도 그 제품 투영을 사용한다.
- 알려진 열거값은 이름, null은 미확인으로 표시하고 정의되지 않은 숫자는 안전하게 실패한다. 금액·원래 의뢰 ID·상태를 보존한다. 취소환불 명령 자체·권한·재시도·회계·실입금 정책은 변경하지 않는다.
- 단건·목록·취소환불 성공 응답 파싱 회귀와 금액 보존·알 수 없는 값 차단을 확인한다. 실제 관리자 GET과 기존 Factory/카드 렌더 결과를 별도로 대조한다. 과거 실패 출력은 보존한다.

추가 제품 경로는 `SsalddelAdmin/Services/AdminShipperRequestProjection.cs`, `SsalddelAdmin/Services/백오피스조회Service.cs`, `Ssalddel.Tests/Ui/BackOffice/AdminShipperRequestContractR70Tests.cs`로 한정한다. 기존 r70 검증 도구 경로를 재사용하며 새 운영 권한·상위 주문 연결·배포·기기 조작·commit/push는 추가하지 않는다.

## 같은 운영자 화면의 읽기 경계 누락

실제 GET 사전검사에서 운송 원장 이벤트·배차 대기 목록·POD 목록·결제 목록·기사 월정산 목록이 모두 `404 FeatureBoundaryUnclassified`를 반환했다. 이 화면이 기존에 조합하는 **5개 조회 액션에만** 기존 `DomesticTransportWorkflow` 메타데이터를 명시한다. 기존 기능 플래그 값·정책·권한·컨트롤러 판본은 유지한다. 컨트롤러 전체를 활성화하지 않고 결제 준비/승인·POD 업로드·배차 생성 등 다른 액션은 이번 수정으로 개방하지 않는다. 기능 꺼짐 차단과 기존 미분류 쓰기 경계 보존을 회귀 검사한다.

추가 경로: `Ssalddel/Controllers/Common/운송원장Controller.cs`, `Ssalddel/Controllers/Admin/02_유입/배차대기Controller.cs`, `Ssalddel/Controllers/Admin/04_증빙/파일POD관리Controller.cs`, `Ssalddel/Controllers/Admin/05_정산/기사월정산관리Controller.cs`, `Ssalddel/Controllers/Shipper/02_Payment/화주결제Controller.cs`, `Ssalddel.Tests/Controllers/TransportRequestLedgerBoundaryR70Tests.cs`. 같은 사용자 요청의 한 건 마감을 막는 읽기 경계 보완이며 권한 완화나 실금융 활성화 승인이 아니다.
