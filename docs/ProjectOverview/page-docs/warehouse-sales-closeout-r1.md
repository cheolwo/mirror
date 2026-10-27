# 창고 재고 정리와 판매 주문 보류 마감 r1

2026-10-17. 기존 모든 역할 앱을 함께 공개 준비하는 작업 중 창고·판매의 전문 원장 연결을 보완했다. 소스와 집중 시험을 준비한 상태이며 이 문서 작성 시점에 이번 전체 소스의 최종 빌드·시험·실제 화면·기기·운영 검증은 통합 담당에서 진행 전이다. 이전 작업의 시험 통과를 이번 변경의 결과로 사용하지 않는다.

## 담당과 완료 결과

| 업무 | 주 사용자·질문 | 기본 정보·주 행동 | 다른 책임으로 인계 |
| --- | --- | --- | --- |
| 기존 잔여 재고 정리 | 공간 제공자·물품 소유자·지정 담당자: 공간 종료를 막는 물품을 어떤 근거로 정리했나? | 정상 반출/불량 폐기/실사 소실 종류, 합의 수량·담당자·양측 동의, 바코드·사진·실제 처리 기록 | 손실 배상·보수·지급은 기존 정산, 공간 종료는 잔여·예약·기사 보관·미수령 입고를 다시 확인하는 별도 명령 |
| 기존 판매 주문 목록 | 판매자: 외부 주문이 왜 아직 출고 후보가 되지 않았나? | 채널·본인 계정·표준 보류 사유·시점·비민감 확인 번호, 기존 채널/상품 연결 화면으로 이동 | 실제 채널 연결·주문 수집은 권한 있는 Operational 동기화 담당; 목록 갱신은 조회만 수행 |

책임 기준은 [공통 페이지 원칙](../../Architecture/WholeRoadmapPagePrinciple.md), 상세 카드는 [분산 이행](distributed-fulfillment-r36.md)과 [판매 주문 목록](SsalddelApp/SsalddelApp-P06-2-1/README.md)을 따른다.

## 창고: 기존 반출 계획을 확장

```text
공간 제공 동의 철회
└─ 가용·예약·불량·기사 보관량을 확인해 보관 책임 유지
   └─ 기존 재고 정리 계획
      ├─ 정상 물품 반출
      ├─ 불량 물품 폐기 확인
      └─ 가용/불량 재고의 실사 소실 정정
         └─ 공간 제공자·물품 소유자 양측 동의
            └─ 지정 담당자: 바코드·수량·사진·처리 근거 확인
               └─ 합의한 재고 종류만 감소·이력/영수증 보존
                  └─ 남은 보관 책임 검사 후 공간 제공자의 별도 종료
```

새 반출 원장이나 테이블을 만들지 않았다. 기존 공간의 보호된 `PrivateDetailsJson.stockRelease`, `입고상품`, `재고이력`, 사진 결속과 명령 영수증을 재사용한다. 불량 정리는 불량 수량만, 가용 재고의 소실 정정은 가용 수량만 감소시킨다. 예약·기사 보관량은 우회하지 못하며 입고 누계와 실제 수령 이력, 기수행 보수·지급 근거를 변경하지 않는다. 실사 소실은 반환·재입고가 아니다. 합의만으로 재고가 줄거나 금전이 발생하지 않는다.

처리 근거와 사진은 같은 RequestId·revision·처리 종류·수량에 결속된다. 같은 완료 요청 재생은 이미 저장한 영수증을 반환하며 수량을 두 번 줄이지 않는다. 과거 정상 반출 요청의 새 필드는 null 직렬화를 생략해 이전 JSON/사진 결속을 유지한다. 연결 근거 없는 불량 재고는 여전히 확인 보류다.

코드: [종류와 요청](../../../Ssalddel.Contracts/Common/Warehouse/DistributedStockReleaseDtos.cs) → [기존 정리 UseCase](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.StockRelease.cs)·[보호 메타데이터](../../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.StockReleaseMetadata.cs) → [기존 정리 화면](../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/DistributedFulfillment/분산잔여재고반출Screen.razor).

## 판매: 부분 주문 누락을 성공으로 처리하지 않음

기존 동기화는 한 품목의 상품 연결을 찾지 못하면 그 품목을 건너뛰고 나머지만 예약할 수 있었다. 이제 연결 미확인/중복, 판매 불가 계약, 배정 미완료, 재고 예약 경합이면 해당 주문의 새로운 출고를 보류하고 수집 커서를 유지한다. 일부 품목만 새로 성공시켜 누락을 숨기지 않는다. 연결 보완 후 기존 주문 참조·SKU와 이미 저장한 출고를 확인해 반복 조회에 새 예약이 늘지 않게 하는 기존 보호를 유지한다.

현재 등록된 `EmptySalesChannelOrderFeedClient`는 실제 채널 구현이 아니므로 정상 주문 0건으로 처리하지 않는다. `ProviderUnavailable`을 기록하고 커서를 유지한다. 외부 조회 실패는 표준 코드만 보관하며 취소 토큰은 정상 취소로 전파한다. 실제 채널 구현·인증·대량 주문 수집·외부 주문 변경은 추가하지 않았다.

기존 `판매채널계정`에 nullable `동기화진단_json` 한 컬럼만 추가한다. 기존 개인정보 보호 변환을 적용하고 표준 원인 코드·시점·보완 대기/해결 상태·해시 확인키만 보관한다. 고객 이름·주소·토큰·외부 주문 원문·외부 예외 문자열은 넣지 않는다. 조회는 기존 판매자 소유 범위를 재사용한다. 해결 관측도 삭제하지 않으며 진단 200개 한도에 도달하면 운영자 검토 안내와 커서 유지로 남긴다. 자동 삭제·정상 처리로 초기화하지 않는다.

진단은 주문의 정본이나 취소·수집 성공의 대용이 아니다. 과거 보류 주문이 현재 수집 응답에 없다는 이유만으로 해결 상태로 만들지 않는다. 성공한 동일 주문의 출고 저장/기존 출고 재확인 뒤에만 해당 진단을 해결로 표시한다. 채널 읽기 실패와 상품 연결·재고 준비의 복구는 각각 기존 담당 화면으로 인계한다.

코드: [공유 진단 계약](../../../Ssalddel.Contracts/Common/Sales/SalesChannelSyncDiagnostics.cs) → [수집·전체 주문 보류](../../../Ssalddel/Services/LogisticsProcessing/SalesOrders/SalesChannelOrderSyncService.cs)·[보호 진단](../../../Ssalddel/Services/LogisticsProcessing/SalesOrders/SalesChannelSyncDiagnostics.cs) → [소유자 조회](../../../Ssalddel/Services/LogisticsProcessing/SalesOrders/SalesChannelOrderReadService.cs) → [기존 공통 목록](../../../Ssalddel.Ui.Common/Areas/App/Components/Sales/ShipperSalesOrderWorkspace.razor).

## 검증 준비와 남은 운영 조건

- [창고 집중 시험](../../../Ssalddel.Tests/Application/Warehouse/DistributedStockResolutionTests.cs): 세 종류별 수량/누계/이력·재생, 양측 동의/지정자/사진/근거 필수, 촬영 후 입력 변경 차단, 불량만 남은 공간 종료, 예약 우회 차단과 구형 JSON을 준비했다.
- [판매 집중 시험](../../../Ssalddel.Tests/Services/LogisticsProcessing/SalesOrders/SalesChannelSyncRecoveryTests.cs): 부분 연결 실패의 전체 주문 보류, 연결 보완·반복 예약 방지, 자리표시자, 외부 조회 실패와 취소, 배정 보류, 소유자 격리·보호 저장을 준비했다.
- [컬럼 migration](../../../Ssalddel/Migrations/20261017040000_AddSalesChannelSyncDiagnostics.cs)은 준비만 했다. 공용 DB 적용·snapshot 통합·최종 빌드/회귀 시험은 통합 작업에서 별도 확인한다. 기록을 지우는 자동 Down은 제공하지 않는다.
- 분산 물류의 기본 OFF·Simulation/profile 관문과 판매 동기화의 기존 feature/Operational/관리자 경계를 유지한다. 이 보완으로 운영 배포 권한이나 공급자·공간 소유자의 실제 계약/폐기 허가를 자동 인증하지 않는다.
- 실제 MySQL 동시 작업·계정 간 동기화 경합, 외부 채널 구현/원문 대사, 진단 보관 한도 이후 보관 정책, 기존 부분 출고의 수량 변경 조정, 물리 카메라·폐기·소실 조사·지급·외부 공개는 후속이다.

이번 변경은 기존 dirty 작업을 보존하며 운영 호출·클라우드 배포·commit/push를 수행하지 않는다.
