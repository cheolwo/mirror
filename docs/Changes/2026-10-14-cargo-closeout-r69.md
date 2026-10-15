# 국내 화물 한 건 마감 r69

2026-10-14. 사용자 `진행` 승인에 따른 로컬 구현 작업이다. [수용 명세](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/cargo-closeout.implementation.r69.md)와 [작업 경로](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/cargo-closeout.r69.work-order.json)를 따른다.

현재 상태: 네 가지 보완 구현과 로컬 검증 완료. 운영 활성화와 물리 기기 검증은 보류한다. 기존 주문 참조·화주 의뢰·운송·증빙·지급 준비 원장을 연결하며 음식·마트·분산 물류의 절차로 합치지 않는다. 무관한 기존 변경은 보존했다.

## 1. 화주 작성·접수 원문 보존

화주가 입력한 수량·중량·부피·온도·설명을 기존 `CargoDTO`로 전달한다. 미확인 값은 `null`로 유지하며 수량 1이나 측정값 0을 만들지 않는다. 추가요금·서비스 조건·요청사항·알선 메모도 보존하고, 수정 시 원본 화물의 독립 복사를 유지한다. 적재 형태는 전용 계약 필드가 없어 기존 요청사항에 명시된 작업 조건으로 남기며 적재 알고리즘의 확정 입력으로 바꾸지 않는다.

카드·가상계좌·계좌이체는 선택값을 그대로 전달한다. `인수증`·`현금성 정산`처럼 금융 시점과 수납 주체가 불명확한 선택은 임의의 카드·후불·현장 계약으로 변환하지 않고 접수를 보류한다.

검토 화면에는 기존 운송대행 개인정보 정책과 기본 미선택 체크를 둔다. 사용자가 명시적으로 동의 기록을 요청하면 기존 본인 동의 API의 유효한 증적·업무·출처·판본을 확인해 접수 원문에 연결한다. 동의 실패·체크 해제·계정 변경·늦은 응답은 새 접수를 막는다. 접수 응답 유실은 같은 요청 번호와 보호 저장 원문의 결과 조회부터 복구한다. 구판 복구 자료에 증적이 없으면 조회만 허용한다.

- 화면 → [실제 모바일 검토 페이지](../../SsalddelApp/Components/Pages/ShipperRequestReviewPage.razor)·[공통 동의 표시](../../Ssalddel.Ui.Common/Areas/App/Components/Transport/ShipperPrivacyConsentPanel.razor)
- 코드 → [작성 수명·접수 복구](../../SsalddelApp/ViewModels/Shipper/ShipperRequestAuthoringPageViewModel.cs) → [서버 요청 변환](../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs)
- 기존 원장 → `api/v1/common/application-privacy-consents`의 `transport-proxy`/`community-map` 증적 → `POST api/v1/shipper/requests`
- 시험 → [입력 왕복](../../Ssalddel.Tests/Clients/ShipperContactWindowApiTests.cs)·[동의 반례](../../Ssalddel.Tests/Clients/ShipperRequestPrivacyConsentTests.cs)·[같은 접수 복구](../../Ssalddel.Tests/Clients/ShipperRequestSubmissionRecoveryTests.cs)

## 2. 삭제·완료 인수·사진의 서버 보호

접수된 화주 의뢰를 물리 삭제해 하위 원장 연결을 없애지 않도록 막고 기존 취소·운영 검토로 안내한다. 화주 인수증은 실제 연결 운송의 인수 완료·도착 시각과 완료 결과 인계, 요청자 권한이 확인돼야 등록한다. 같은 번호의 재요청은 기존 결과를 반환하고 다른 번호로 덮어쓰지 못한다. 청구·입금·정산으로 이미 진행된 상태에서 인수증을 등록해도 금융 상태를 이전 단계로 되돌리지 않는다.

일반 화물 상하차 완료 사진은 현재 기사·운송·단계에 연결된 서버 업로드 영수증과 비공개 실제 파일을 대조한다. 내부 영수증은 상세 응답에서 제외하되 기존 첨부 원문은 보존한다. 과거 문자열만 있는 증빙, 다른 계정·운송·단계, 파일 변경을 완료 근거로 자동 승격하지 않는다.

- API·코드 → [의뢰 삭제 보호](../../Ssalddel/Application/Shipper/Request/Handlers/의뢰삭제CommandHandler.cs)·[화주 인수증 등록](../../Ssalddel/Application/Shipper/Request/Handlers/화주운송의뢰인수증등록CommandHandler.cs)·[사진 증빙 검사](../../Ssalddel/Application/Driver/Transport/Services/화물운송사진증빙Policy.cs)
- 기존 원장 → 화주 의뢰·운송 완료 시각·서버 사진 업로드 기록·[완료 결과 인계](../../Ssalddel/Services/Operations/화물운송완료화주인수인계Service.cs)
- 한계 → 모바일 상세는 인수증 번호·등록 시각을 읽기 전용으로 표시한다. 화주의 인수증 입력·전송 UI는 후속 미연결이며 이번 서버 API 보호를 앱의 수령 완료 화면 검증으로 표현하지 않는다.

## 3. 기사 상차·사진·완료 후속 복구

상차 완료 응답이 끊기면 같은 운송의 서버 상태를 먼저 조회한다. 이미 완료된 요청을 즉시 반복하지 않고, 미확정 상태에서 같은 업무의 확인 입력을 보존한다. 로그아웃·계정 변경·다른 운송 선택·페이지 이탈 뒤 도착한 응답은 현재 화면에 반영하지 않는다.

사진과 업로드 결과는 계정·운송·상하차 단계별 AES-GCM 캐시에 보관한다. 확인된 완료나 명시 폐기 시 제거하고, 활동 중 조회·저장 때 3일 만료 자료를 정리한다. 앱이 실행되지 않는 동안에도 정리가 보장되는 상시 작업은 아니다. 키 오류를 새 키 생성이나 기존 사진 덮어쓰기로 우회하지 않는다.

운송 완료와 함께 저장된 신규 영속 이벤트에서 화주 인계와 입금 준비를 재개한다. 같은 완료 근거를 재검사하며 조건부 갱신·임대와 제한 재시도로 중복 처리를 막는다. 기본 worker는 OFF다. 정상 저장 뒤 같은 운송을 한 번 처리하고, 외부 트랜잭션이 남아 있으면 그 안에서 후속 작업을 실행하지 않는다. 커밋 뒤 선택적 스윕이 필요하며 과거 이벤트 전체를 자동 재생하지 않는다.

- 화면·코드 → [상차 페이지](../../DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor) → [상차 완료·재조회](../../DriverApp/ViewModels/Driver/Transport/기사상차PageViewModel.cs)·[보호 사진 캐시](../../DriverApp/Services/DriverCargoPhotoRecoveryStore.cs)
- 기존 원장 → [완료 이벤트 저장](../../Ssalddel/Application/Driver/Transport/Services/기사운송상태변경CommandExecutor.cs) → [영속 후속 재개](../../Ssalddel/Application/Driver/Transport/Services/CargoCompletionRecoveryService.cs) → 완료 인계·입금 준비
- 시험 → [상차 응답 유실·수명](../../Ssalddel.Tests/Clients/CargoTransportPickupCompletionTests.cs)·[사진 캐시](../../Ssalddel.Tests/Clients/CargoPhotoRecoveryStoreTests.cs)·[사진 복구 연결](../../Ssalddel.Tests/Clients/CargoPhotoRecoveryIntegrationTests.cs)

## 4. 기사 운송대금 목록·상세

기존 `api/v1/driver/payout-preparations`의 본인 월별 응답을 사용한다. 누락된 실행 분류를 기존 `DomesticTransportWorkflow` 관문에 연결했으며 기본 OFF와 기사 인증은 유지한다. 목록은 완료 운송과 지급 준비 상태를, 상세는 선택 운송의 예정 운임·보류 이유·수납 및 정산 상태를 보여 준다. 운임 누락은 0원이 아닌 미확정으로 표시하고 다른 계정·기간·중복 운송 응답을 받지 않는다. 조회 오류·로그아웃·계정 변경은 이전 금액을 남기지 않는다.

기존 월 정산의 플랫폼 이용료 의미는 유지한다. 지급 준비·승인·Outbox와 실제 은행 입금, 공제 후 수령액을 합치지 않는다.

- 화면 → [운송대금 목록](../../DriverApp/Components/Pages/Driver/05_Settlement/운송대금Page.razor)·[운송대금 상세](../../DriverApp/Components/Pages/Driver/05_Settlement/운송대금상세Page.razor)
- 코드 → [본인·월·선택 수명](../../DriverApp/ViewModels/Driver/Settlement/기사운송대금PageViewModel.cs) → [기존 지급 준비 API](../../DriverApp/Services/DriverPayoutPreparationApiService.cs)
- 시험 → [운송대금 조회·표시 반례](../../Ssalddel.Tests/Clients/CargoPayoutPreparationTests.cs)

## 현재 확인된 결과

| 검증 | 실제 확인과 제한 |
| --- | --- |
| 앱 빌드·회귀 시험 | Fast 661개·API 기능 경계 Fast 5개·Task 11,395개 통과, 실패 0개·환경 의존 MongoDB 15개 건너뜀. 최종 전체 소비 앱 빌드 오류 0개·경고 70개. 신규 내부 사진 정보 노출·구형 첨부 호환·기능 분류 누락과 시험 설정 오류를 보완한 최종 소스로 확인했다. |
| 실제 localhost HTML | 제품 Razor·ViewModel과 예시 응답을 연결한 서버 사전 렌더 9/9 통과. 동의 전·기록 완료·오류, 지급 준비·보류·빈 목록·오류, 정상/보류 상세의 문구·체크 및 등록 가능 조건을 확인했다. |
| 브라우저 화면 | 연결 시도 2회 시간 초과로 실제 paint 확인 차단. `browserVerified`·`viewportVerified`·`interactivityVerified`는 모두 false다. HTML 반환은 390px 배치·클릭·스크롤·접근성의 확인이 아니다. |
| 격리 서버 HTTP | 새 격리 MySQL/MongoDB와 Local 비공개 사진을 사용한 합성 창고 연계 시나리오의 `prepare → arrive → handoff → pickup → complete` 5단계 성공. 서버 업로드 뒤 상하차 완료와 같은 원장 재조회까지 확인했다. 기사 선택은 전용 통제 시나리오이며 자동 배차·Ollama 판단 검증이 아니다. 새 화주 작성 UI에서 기사 앱까지 실제 사람이 수행한 완주도 아니다. |
| 화주 인수증 HTTP | 첫 등록 200, 같은 번호 재요청 200, 다른 번호 409 확인. 실제 운송 완료·인계와 금융 상태 보존 검사의 API 증거이며 앱 입력 화면은 미연결이다. |
| 삭제·지급 준비·SQL | 화주의 삭제 404는 원접수자와 다른 계정의 권한 가림이며, 원접수자의 삭제 409로 보호를 확인했다. 본인 월별 지급 준비 GET 200과 해당 운송 항목, SQL의 의뢰·운송 각 1건·수락된 완료 인계 1건·후속 Done 1건·인수 완료·입금 대기 유지·인수증 기록을 확인했다. 최종 서버를 재시작한 뒤 같은 결과를 재확인했다. |

원문 HTML·개별 SHA-256·소스 연결 manifest는 Git 제외 `artifacts/local/cargo-closeout-r69/ui-preview/ssr-evidence/`에 보관한다. 격리 서버 로그와 결과는 `artifacts/local/cargo-closeout-r69/`, 단계별 HTTP 자료는 `artifacts/local/cargo-warehouse-journey-r1/workflow/`에 있다. 이 자료의 합성 계정·입력·모의 응답을 실제 사용자 동의나 현장 증빙으로 사용하지 않는다. 기준선은 `artifacts/local/cargo-closeout-r69/baseline.json`이다.

## 실행하지 않은 범위와 다음 확인

이번 작업에서 실제 휴대폰 설치·카메라·네이티브 SecureStorage·지도·브라우저 시각 검증, 실제 결제·은행 지급, Azure/GCS 설정·배포, Unity, commit/push는 실행하지 않았다. 기본 운영 기능·복구 worker 활성화와 클라우드 설정도 바꾸지 않았다.

로컬 회귀 시험과 본인 삭제 보호·지급 준비·SQL 대조는 마쳤다. 다음에는 별도 기기 연결에서 상하차 촬영·사진 재진입·계정 변경과 실제 화면 흐름을 확인한다. 화주 인수증 입력 UI와 실수납·실지급은 각각 독립된 후속 범위다.
