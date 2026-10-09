# 주문 한 건의 역할·조건·다음 행동 리팩토링 r40

[승인 r34](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/order-ledger-role-refactor.implementation.r34.md)에 따라 기존 주문 루트와 독립 하위 업무를 보존하면서 서버·공유 계약·주문 상세를 보완하고 로컬 검증했다. Unity·영상·운영 설정·실제 금전·APK·단말·Azure·commit/push는 이번 대상이 아니다.

## 변경 결과

| 업무 | 보완 | 그대로 유지한 경계 |
| --- | --- | --- |
| 분산 주문 연결 | 선택한 루트의 실제 판매자/주문자·판본·조건 확인 → SQL 예약과 영수증 저장 → 기존 출고/운송 Mongo 동기화 → 루트 포함 참조 연결. 같은 요청은 연결만 재개 | 표시 `OrderReference`와 고유 `OrderId` 분리. 구형 출고 ID·독립 상태·물품 소유권 보존 |
| 원장 연결 편집 | 진행된 루트/자녀의 삭제·역할 변경·필수 해제 차단. 동일 연결은 저장하지 않음 | 부모 완료로 자녀 완료를 덮어쓰지 않음. 필수 미지정·누락·진행·완료 별도 판정 |
| 조건 변경 | 표시의 `CanRenewConsent`/잠금 이유와 실제 저장을 같은 정책에서 판정 | 조리 시작·활성 수행·종료·미확인 상태 차단. 철회가 현재 수행 취소·환불이 되지 않음 |
| 주문 상세 | 현재 단계·담당·대기 이유·전문 업무 이동을 우선 표시. 연결 ID·판본·조회 근거 접기 | 숨겨진 자녀 상태/담당자/위치로 안내를 만들지 않음. 안내 링크는 명령이 아님 |
| 문제 접수 | 음식점 수락 후 취소·변경을 기존 보호된 협의 페이지로 인계 | 요청·사건 종결·수령·정산·실지급을 하나의 완료로 합치지 않음 |

원주문 연결 `Pending`은 SQL 배송 저장 성공과 Mongo 연결 확인 중을 구분한다. 권한 문제 `Blocked`는 주문 ID·판본·재시도 행동을 숨긴다. 현재 root 판본을 확보했을 때만 분산 상세에서 연결 재확인을 제공하고, `Connected`는 원주문으로 복귀한다. 입고 당시 Lot 보관 합의는 변경하지 않고 예약 당시 합의/양측 동의 판본을 순회 근거에 별도로 동결한다.

## 페이지 → 코드 → 저장

- [주문 상세 책임](../ProjectOverview/page-docs/OrdererApp/OrdererApp-P05/README.md) → `OrdererIndividualOrderLedgerDetail`/`주문원장업무안내Panel` → `주문조회ViewModel`/역할별 조회 → 기존 Mongo `community_ledgers`/공개 요청 경계.
- [분산 주문 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md) → `분산주문Screen`/`분산이행PageViewModel`/`분산이행Client` → 기존 `POST waves/{waveId}/orders` 및 `POST waves/{waveId}/orders/{orderId}/ledger` → 기존 SQL 주문·예약·출고·운송·명령 영수증 + Mongo 포함 참조.
- 원주문 검증/연결은 `분산주문원장연결Adapter`, 연결 계획과 예약 합의는 기존 보호된 wave `RouteEvidenceJson.orderLedgerConnections`/`reservationTerms`에 저장한다. 새 테이블·열·migration은 없다.
- 음식 조건은 `AutoFulfillmentConsentRenewalPolicy` → `AutoFulfillmentSnapshotProjection`/`AutoFulfillmentCoordinator`; 음식 전문 진행 API와 동결 요금·부담·증빙은 유지한다.

## 검증

- Fast: 소비 앱을 포함한 `Ssalddel.v3.5.slnx` 빌드와 관련 323개 시험 통과, 실패·건너뜀 0개. 원 로그는 `artifacts/local/validation/20261008-185105/`다.
- Task: 같은 solution 빌드와 전체 9,078개 시험 통과, 실패 0개. 실제 격리 MongoDB를 요구하는 기존 통신판매·보호지원 시험 2개는 환경 미지정으로 건너뛰었다. 원 로그는 `artifacts/local/validation/20261008-185534/`다. 전체 빌드에는 기존 Android 의존성 범위 등 경고 64개가 남아 있다.
- 주문 상세 검토 호스트는 제품 Razor 파일을 그대로 링크하고 실제 공유 ViewModel을 사용한다. 예시 응답만 제공하며 업무 쓰기 API·실제 사용자 자료·실제 DB를 연결하지 않는다. 음식·분산·비공개 하위 기록·조회 실패 4상황을 390×844/1280×900 두 폭에서 확인했다. 가로 넘침이 없고, 연결 기록을 펼친 뒤 새로고침해도 펼침 상태가 유지된다. 별도 일시 오류 상황은 두 폭에서 실제 ‘다시 시도’ 클릭 후 안내로 복구됐다. 전문 업무의 링크 주소만 확인했으며 실제 명령 화면으로 이동·접수하지 않았다.
- 검토 호스트 최종 빌드는 경고·오류 0개다. 브라우저 기록에는 검토 서버를 재시작하는 동안의 연결 실패가 있으며 재시작 후 새 페이지와 재조회 동작은 복구됐다. 이 재시작을 제품의 업무 통신 실패 시험으로 보고하지 않는다. 화면·DOM 관찰은 `ui-verification.json`과 `order-*.jpg`에 남겼다.
- 분산 회귀 시험은 격리 SQLite의 실제 분산 UseCase·기존 출고/운송 동기화·주문 포함 서비스와 CAS 저장소를 사용한다. 이 CAS 저장소는 실제 MongoDB 연결 시험을 대체하지 않는다. SQLite의 지원 격리 수준만 Serializable로 선택하며 MySQL의 기존 RepeatableRead는 유지한다.
- 이번 파일 목록·검증 로그·소스 지문·화면은 Git 제외 `artifacts/local/order-ledger-role-refactor-r40/`에 보관한다. 소유 50경로 중 컴파일 대상 42소스의 검증 전후 SHA-256이 동일하고 기존 다른 변경 15,118개의 지문을 보존했다. 변경한 8문서의 내부 링크 834개는 대상이 존재한다. 결과는 `verification.json`/`doc-links.json`에 기록한다. 이전 실패 빌드 로그는 보존하되 최종 통과 근거는 위 두 실행이다.

## 남은 경계

아래 1·2는 r40 당시의 잔여 기록이다. 후속 [r41 원주문 선택·동시 변경 보호](2026-10-08-root-order-selection-concurrency-r41.md)에서 구현하고 로컬 검증했다. 당시 시험 수와 미실행 환경 기록은 그대로 보존한다. 실제 업무·휴대폰·운영 효과는 계속 별도다.

1. 신규 분산 주문에서 원주문을 목록으로 고르는 양식은 아직 추가하지 않았다. 기존 요청의 명시적 root 선택/권한 검증/연결 API와 연결된 상세의 복귀·복구부터 구현했다. 내부 ID 직접 입력 양식을 새로 요구하지 않는다.
2. 연결 편집은 현재 읽은 자녀 상태와 루트 CAS를 검사한다. 검사 뒤 별개 자녀가 동시에 진행하는 경쟁까지 원자적으로 차단하려면 자녀 전이와 관계 편집의 공유 잠금 또는 다문서 트랜잭션을 추가해야 한다. DB 간 전체 원자성을 이번에 확보했다고 보고하지 않는다.
3. 기존 지원 화면의 사건 처리는 유지하고 주문 개요에 모든 사건·비용 분담을 자동 집계하지 않았다. 새로운 환불 정책·실제 계좌 지급은 추가하지 않았다.
4. 실제 보호 HTTP·공용 SQL/Mongo·휴대폰 APK·Azure·실입고/운행·실금전·비활성 정책 운영 전환은 별도 검증이다.
