# 전국 생활 공간 입고 재고 조회 기반 r51

아파트·일반 생활 공간·기존 창고를 지역별로 다른 원장에 복제하지 않고 기존 분산 거점·적재 위치·입고·재고 연결로 관리한다. [승인 범위](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/nationwide-storage-foundation.implementation.r51.md)와 [작업 지시](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/nationwide-storage-foundation.r51.work-order.json)는 코드 기반 보완만 소유한다.

## 코드 연결

- 기존 `POST api/v1/distributed-fulfillment/sites`와 동의·적재 위치·입고 예정·실제 수령 명령을 재사용한다. 특정 구/동이나 공공 건물 후보 없이 본인 관리 공간을 등록할 수 있다.
- 새 `GET api/v1/distributed-fulfillment/sites/{id}/inventory?roleKey=warehouse&skip=0&take=50`은 한 공간의 재고를 배송·배차와 독립적으로 조회한다. `roleKey=shipper`는 본인이 물품 소유자 또는 판매자인 Lot만 반환한다. 기사·주문자 관점은 이 재고 관리 API의 대상이 아니다.
- [조회 계약](../../Ssalddel.Contracts/Common/Warehouse/분산공간재고Dtos.cs)·[UseCase](../../Ssalddel/Application/Warehouse/DistributedFulfillmentUseCase.Inventory.cs)·[기존 Controller](../../Ssalddel/Controllers/Common/DistributedFulfillmentController.cs)를 연결한다. 공간 접근권과 Lot 범위를 SQL에서 제한하고 고유 식별자 순서로 최대 100건과 다음 행 여부를 반환한다. 배송 원장·경로·공공 후보를 불러오지 않는다.
- 예정·실수령·입고 잔여·가용·예약·기존 불량 수량은 별도 값이다. 예정 수량을 실제 보관 재고로 계산하지 않으며 재고 원본/위치 연결 결손은 503으로 거절한다. 조회가 입고·예약·수령 완료를 만들지 않는다.
- 관리자는 빈 공간과 동의 철회 중 남은 재고도 확인한다. 응답에 주소·좌표·연락처·출입 안내·바코드·증빙·사용자 ID는 넣지 않으며 물품 소유자/판매자에게 관리용 위치 이름을 주지 않는다.
- [건물 후보 Provider](../../Ssalddel/Services/WorldProjection/DistributedBuildingCandidateProvider.cs)는 지역 밖 주소를 번들 파일 접근 전에 분리한다. 적용 지역 안의 파일·hash·판본 오류는 기존 503으로 남기고 샘플로 대체하지 않는다.

## 검증

Fast는 계약·서버·시험 프로젝트 build와 집중 시험 **102건 통과, 실패/건너뜀 0건**이다. [신규 재고 시험 25건](../../Ssalddel.Tests/Application/Warehouse/분산공간재고Tests.cs), [인증 HTTP 시험](../../Ssalddel.Tests/Controllers/분산물류HttpTests.cs), [참고 자료 경계 시험](../../Ssalddel.Tests/Services/WorldProjection/DistributedBuildingCandidateTests.cs), 기존 입고·사진 증빙 시험을 함께 실행했다. 실제 격리 SQLite와 loopback Kestrel 인증 HTTP를 사용하며 실운영 로그인·외부 DB의 증거는 아니다. 로그·TRX는 `artifacts/local/validation/20261010-134631`에 있다.

Task는 관련 앱을 포함한 `Ssalddel.v3.5.slnx` build와 서버 시험 **9,926건 통과, 실패 0건, 환경 의존 MongoDB 15건 미실행**이다. 로그·TRX는 `artifacts/local/validation/20261010-134822`에 있다. 기존 앱 코드 경고가 남은 성공 build이며 APK 설치·실제 기기 검증은 아니다. 검증 전후 이번 소스·시험 파일 8개의 SHA-256 변화는 0건이고 작업 지시에 지문을 보존했다.

최초 build에서 신규 시험의 `Driver`가 이웃 namespace와 충돌한 것을 `DistributedTestFixture.Driver`로 고친 후 위 검증을 재실행했다. 최초 실패 로그는 `artifacts/local/validation/20261010-134209`에 보존한다. 기획 hash 일치와 독립 코드·범위 검토를 확인했으며 원천 계보 검사 보완을 반영했다.

문서 범위 Fast(`artifacts/local/validation/20261010-135723`), 이번 문서와 README 및 공용 목차·현재 작업의 r51 연결 88개, 전체 소유 파일의 공백 검사를 통과했다. 관련 없는 기존 작업을 stage하거나 되돌리지 않았다.

응답의 `Site.Revision`과 항목 `Revision`은 각각 기존 공간과 Lot 판본이다. 예약·불량을 포함한 전체 재고의 단일 동시 판본이나 차등 조회 기준으로 사용하지 않으며 필요한 페이지를 다시 조회한다.

## 남은 경계

소프트웨어의 지역 독립성은 전국 실운영 준비 완료가 아니다. 기존 전체 `workspace`는 여전히 폭넓은 배송 후보와 목록을 읽으며 이번 API는 선택한 한 공간의 재고 조회를 분리한다. 전국 규모 용량 시험·인덱스 검토·전체 목록 페이지화는 후속이다. 위치에 재고가 없는 경우에는 기존 공간/위치 조회를 사용한다.

이번에는 분산 입고의 정상/불량 구분 명령·격리 장소·폐기·환불, 공유 공간 용량·출입 시간의 실제 집행, IoT, 신규 피킹·포장, 앱 UI·APK·Unity를 확장하지 않는다. 실제 불량 분리 입고는 기존 증빙·멱등 hash와 함께 후속 검증이 필요하다.

무료 관리 도구 방향과 실제 보관 노동·배송비·기존 당사자 비용 약정은 별개다. 기능·운영 기본 OFF와 Simulation 관문을 유지하며 공용 DB 변경·실입고·정산 지급·행정 문의 발송·현장 허가·commit·push·배포는 수행하지 않는다.
