# [기획 · 생활 교류·생활권 물류 · PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB · r103]

## 최신 검토: 생활 퀵의 개별 업무 흐름 r103

[검토 r103](quick-delivery-review.r103.md)은 `Reviewed / GapsIdentified / ImplementationNotAuthorized`다. 일반 생활 퀵·동네 회원 배송의 접수/수락/수령과 예외 마감을 따로 대조했다. 현재 퀵은 기사 직접 지급이며 주간 업무 명세 표시를 플랫폼 주간 지급 완료로 볼 수 없다. 수정·취소/희망 시각 보존, 수락 보수/적재 정보, 부재·반송/신고·수령 확인의 공백을 구분했고 관련 기존 시험 358개가 통과했다. 기존 변경의 커밋 정리와 신규 퀵 구현 승인은 별개이며 r102 구현·운영 게이트·금융 분리를 보존한다.

## 최신 구현: 공동주택 기획의 기존 역할 화면 반영 r102

[승인 범위 r102](role-screen-integration.design.r102.md)·[변경·검증 기록](../../../../Changes/2026-10-15-community-role-screens-r102.md). `Implemented / LocalValidated / OperationalDeferred`. 주민의 기존 필요글 작성에 비구속 입고 희망 문맥을, 본인 활동정산에 약정 기본보수 요약을, 기존 분산 입고 목록에 상품별 일정과 예정/실입고·변경안/이전 예약 구분을 반영했다. Fast 34개·Task 12,017개 통과, 환경 의존 16개 미실행. 루프백 Chrome 20개 배치와 격리 DB의 글 저장·재조회를 확인했다. 기본 Preview의 미지원 협업 조회와 기기·실제 지급 검증은 별도다. 새 기여금 공제·관리비·공개 일감·자가 피킹·새 권한·실제 지급을 추가하지 않는다. r101과 이전 기획의 미정·전문 원장·기능 게이트는 보존한다.

## 이전 기획: 분야 전환·주민 수요와 입고 판단 제안 r101

[기획 답변 r101](workflow-operating-decisions.design.r101.md)은 `TopicChangeAccepted / NextTopicProposed / ImplementationNotAuthorized`다. 사용자의 요청으로 인력·보수 논의를 여기서 멈추고 [r100](workflow-operating-decisions.design.r100.md)의 업무 진입 질문은 미정 보류한다. 다음 후보는 주민 수요·상품 상태·잔여 재고 후속 처리이며 먼저 비구속적 입고 희망을 제안한다. 새 분야의 내용은 아직 수용되지 않았고 기존 방향·미정·구현 검증 상한을 보존한다.

## 이전 로컬 구현: 업무 예외 마감 보완 r76

[r76 구현 범위](workflow-exception-closeout.implementation.r76.md)·[검증 기록](../../../../Changes/2026-10-15-workflow-exception-closeout-r76.md)·[업무 트리](../../../../ProjectOverview/page-docs/role-workflow-tree-r76.md). 본인 음식·화물·활동 주간 명세와 운행/입고/반출/수령 보류/음식 지원 후속을 로컬 검증했다. 실제 주간 송금·기기·외부 서비스·운영 전환은 별도다. 아래는 각 판본 당시의 구현 범위이며 r73의 음식·피킹 명세 미연결은 현재 r76 조회 결과를 대신하지 않는다.

## 이전 구현: 배송료 선결제·화물 주간 지급 준비 r73

[승인 범위](transport-prepayment-settlement.implementation.r73.md)·[작업 명세](transport-prepayment-settlement.r73.work-order.json). 저장 운임/혜택, 토스 입금 확인, 진행 운송 보존과 화물 완료 주간 명세를 준비했다. `Implemented / LocalValidated / OperationalDeferred`. 업무별 건별 보수는 유지하고 정산만 본인 주간 합산하는 기준이다. 음식·피킹 통합과 실제 주간 입금은 미연결이다. [변경·검증](../../../../Changes/2026-10-15-transport-prepayment-settlement-r73.md).


## 현재 구현: 기사 직선거리 요금과 추천 배차 표시 r72

[승인 범위](straight-line-driver-pricing.implementation.r72.md)·[작업 명세](straight-line-driver-pricing.r72.work-order.json)에 따라 음식/생활 소형 배송 기사 거리비와 두 기사 앱의 공통 업무 표시를 연결했다. `Implemented / LocalValidated / OperationalDeferred`. Fast 246개·Task 11,636개 통과·환경 의존 16개 건너뜀·전체 소비 앱 빌드 오류 0개. 일반 화물·고객 청구·기존 합의/동결과 실제 작업·안전 조건을 보존하며 기능 기본 OFF다. 브라우저 시각·단말·새 APK·운영 검증은 별도다. [변경·검증](../../../../Changes/2026-10-15-straight-line-driver-pricing-r72.md).

## 현재 구현: 기사 업무 선택과 공통 수락 보호 r71

[승인 범위](driver-participation.implementation.r71.md)·[작업 명세](driver-participation.r71.work-order.json)에 따라 기존 음식·화물·직접피킹의 전문 흐름을 보존하면서 기사 희망 조건·공통 수락 보호와 두 기사 앱 설정을 연결했다. `Implemented / LocalValidated / OperationalDeferred`. Fast 493개·분산 묶음 5개·Task 11,550개 통과, 환경 의존 16개 건너뜀·별도 MySQL 행 잠금 1개 통과. [결과·한계](../../../../Changes/2026-10-15-driver-participation-r71.md). 운영 활성화·공용 DB migration·새 APK 배포/휴대폰 설치·commit/push와 지도/배차 목록 통합은 별도다.

## 이전 구현: 비공개 APK 준비와 화주 인수증 연결 r70

[승인 범위](private-apk-preparation.implementation.r70.md)·[작업 명세](private-apk-preparation.r70.work-order.json)에 따라 인수증 입력·등록·정본 재조회와 GCS APK 로컬 준비를 보완했다. [역할별 현황](../../../../Deployment/PrivateApkPilotReadiness-20261015.md). `Implemented / LocalValidated / CloudPublicationDeferred`. Fast 155·Task 11,425개 통과·환경 의존 15개 건너뜀·로컬 Debug APK 두 개 확인. 보안 갱신·실제 업로드·클라우드/운영 설정·물리폰은 별도다.

## 국내 화물 한 건 마감 r69

[수용 범위](cargo-closeout.implementation.r69.md)·[작업 명세](cargo-closeout.r69.work-order.json)에 따라 화주 신규 작성·동의·동일 접수 복구, 일반 화물 사진·완료 인수 보호, 기사 상차 확인·사진 보호 보관·영속 후속과 본인 운송대금 조회를 보완했다. `Implemented / LocalValidated / OperationalDeferred`. [검증·한계](../../../../Changes/2026-10-14-cargo-closeout-r69.md). 기본 기능 플래그·worker OFF를 유지하며 실제 입금·물리 기기·브라우저 시각·클라우드·Unity·commit/push는 별도다. 기존 r68과 이전 자료는 보존한다.

## 현재 구현: 마트 고객 수령과 기존 지급 조건 r68

[수용 범위](mart-customer-receipt.implementation.r68.md)에 따라 실제 주문자의 전량 수령과 부분/누락/파손 보류를 기존 마트배송시도에 보호 저장하고 기존 정산 지급 관문에 연결했다. `Implemented / LocalValidated / OperationalDeferred`. 집중 45개·Fast 347개·DB 호환 4개·Task 11,236개 통과, MongoDB 15개 건너뜀. 기사 전달·수령·작업 보수·지급 신고·실제 이체를 분리한다. 자동 보류 해제·앱 화면·운영 활성화·공유 DB 적용·실제 송금은 제외한다.

## 현재 구현: 주문별 원가·보수와 마트 작업 근거 r67

[수용 범위](mart-order-compensation.implementation.r67.md)에 따라 원주문 참조 기반 본인 원가·보수 조회와 마트 피킹·포장 완료 근거의 기존 정산 연결을 보완했다. `Implemented / LocalValidated / OperationalDeferred`. Fast 250개·Task 11,202개 통과, 환경 의존 MongoDB 15개 건너뜀. 단가 미정·본인 권한·실제 처리자·중복 발생 보호·입고 원가와 작업자 채무의 분리를 유지한다. 앱 화면·APK·실제 지급·클라우드 변경·Unity·commit/push는 이번 범위 밖이다. 아래 기존 구현·검증 기록은 보존한다.

## 현재 구현: 입고·보관 공통 UI 구조 정리 r65

[수용 범위](inbound-cleanup.implementation.r65.md)·[작업 명세](inbound-cleanup.r65.work-order.json)에 따라 기존 ViewModel의 주문/사진 처리를 같은 클래스의 partial로 분리하고 반복 표시를 기존 파일로 모은다. Implemented / LocalValidated / OperationalDeferred. r63 서버 업무·r64 시각과 Recovery 형식은 그대로다. [변경·검증·후속](../../../../Changes/2026-10-12-inbound-cleanup-r65.md). 새 기능·서비스/DI·운영 활성화·commit/push는 포함하지 않는다.

- 검증: Fast 225건·Task 11,167건 통과·실패 0건·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개/경고 145개. 기존 멤버 90개·Razor 출력 10개·CSS 3개·보호 파일 38개 보존을 확인했다. 기기 설치·운영 활성화·commit/push는 수행하지 않았다.

## 현재 구현: 입고 협의 시각 정돈 r64

[수용 설계](inbound-visual.implementation.r64.md)·[작업 명세](inbound-visual.r64.work-order.json)에 따라 기존 r63 공간·입고·보관 조건 화면을 단색 아이콘·숫자 위계·의미별 섹션·반응형 항목으로 정돈한다. `Implemented / LocalValidated / OperationalDeferred`. 서버/VM/권한·동의·계약·정산과 기본 OFF는 보존한다. [화면·검증](../../../../Changes/2026-10-12-inbound-visual-r64.md). 기존 r63 및 앞선 작업은 유지한다.

- 검증: Fast 74건·Task 11,167건 통과·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개/경고 144개·로컬 Edge 9개 배치 확인. 실제 단말/인증 HTTP·공유 DB 적용은 별도다.

## 현재 구현: 공급자·공간 제공자 입고 협의 r63

[수용 범위](supplier-space-inbound.implementation.r63.md)·[작업 명세](supplier-space-inbound.r63.work-order.json)에 따라 공간 수용 조건·품목별 협의·동일 판본 양측 동의·입고 예약과 실제 사진 수령을 기존 원장·서버·공통 모바일 UI에 연결했다. `Implemented / LocalValidated / OperationalDeferred`. Fast 279건·Task 11,167건 통과·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개. 현재 보관 합의와 미확정 변경안, 승인 예약과 실제 재고/입고 보수를 구분한다. [페이지 책임](../../../../ProjectOverview/page-docs/distributed-inbound-collaboration-r63.md)·[검증 반환](../../../../Changes/2026-10-12-supplier-space-inbound-r63.md). 기본 OFF·Simulation을 유지하며 실제 계약·공유 DB·단말·Azure·지급·Unity·commit/push는 별도다. 아래 r62 방향 기록과 이전 구현은 보존한다.

## 현재 기획: 공급자·공간 제공자 협의와 계약 후 입고 r62

[조사·설계 r62](supplier-space-inbound.design.r62.md)는 농가·공장·식품/공산품 제조업체의 공급 제안과 공간 제공자의 품목 선택·협의·동의·건별 입고 승인을 정리한다. `AcceptedDirection / ProposedDesign / ImplementationPending / OperationalDeferred`. 기존 합의 전 초안·보관 합의·사진 수령·r60/r61 정산을 보존하며 공간 수용 정책 → 계약 협의/입고 예약 → 실제 수령·원가 연결 순서를 제안한다. 이번에는 문서만 갱신했으며 제품 구현·실제 계약·운영 활성화·commit/push는 수행하지 않는다. 아래 r61 구현 상태와 이전 이력은 그대로 유지한다.

## 현재 구현: 원가 귀속·플랫폼 몫 r61

[수용 범위](settlement-economics.implementation.r61.md)·[작업 명세](settlement-economics.r61.work-order.json)에 따라 입고·보관 비용의 주문 원가 귀속, 잔여금 기준 5%/10% 플랫폼 몫 및 참여자 배분을 구현하고 로컬 검증을 완료했다. Fast 239건·Task 11,088건 통과, 환경 의존 MongoDB 15건 건너뜀·빌드 오류 0. 실제 지급·운영 OFF이며 AI 연계는 범위 밖이다. [변경·검증](../../../../Changes/2026-10-12-settlement-economics-r61.md). 아래 r60과 이전 기록은 보존한다.

## 현재 구현: 입고·보관과 출고·배송 분리 r60

[수용 범위](receiving-outbound.implementation.r60.md)·[작업 명세](receiving-outbound.r60.work-order.json)에 따라 기존 Lot·주문과 재고 연결을 유지하며 입고 보수/보관료와 출고·배송 정산의 업무·지급 관문을 분리했다. Fast 159건·Task 11,005건 통과·환경 의존 MongoDB 15건 건너뜀·빌드 오류 0이며 운영 OFF다. [검증 반환](../../../../Changes/2026-10-12-receiving-outbound-r60.md). 기존 r59 성과금 및 이전 기록은 보존한다.

## 현재 구현: 활동 보수·공동 성과금 r59

[승인 범위 r59](activity-compensation.implementation.r59.md)·[작업 명세](activity-compensation.r59.work-order.json)에 따라 실제 행별 작업 근거와 기존 기본 보수·별도 성과금, 본인 목록/상세를 연결했다. 로컬 Fast 518건·Task 10,957건 통과·MongoDB 환경 의존 15건 건너뜀·빌드 오류 0이며 운영 OFF다. [검증 반환](../../../../Changes/2026-10-12-activity-compensation-r59.md). 이전 마트 r58과 전국 공간 r51 기준선은 보존한다.

## 현재 구현: 살뜰마트 기사 수락·픽업·전달 r58

[승인 범위 r58](ssalddel-mart-driver-work.implementation.r58.md)·[개발 반환](ssalddel-mart-driver-work.r58.work-order.json)에 따라 r57의 기사 소비자 결손을 보완했다. `Implemented / LocalValidated / SimulationConsumerBound / OperationalDeferred`. 마트 전용 수락→현재 전표 입력 즉시 픽업→전달·보호 전표·요청 영수증·후속 Outbox와 독립 aDriver 화면을 결속하고 기존 음식/화물/정산을 분리한다. aast 262건·전체 앱 build·Task 10,299건 통과, 환경 의존 15건 건너뜀. [검증·한계](../../../../Changes/2026-10-10-ssalddel-mart-driver-work-r58.md). Simulation·기본 Oaa·현재 기사/판본/포장/멱등 관문을 유지하며 실제 화면 렌더·인증 HTTP/DB·단말·하드웨어·공용 DB Migration 적용·실운영은 별도다. 아래 기존 승인·구현·검증 이력은 보존한다.

## 이전 기준선: 살뜰마트 배차 소프트웨어 연결 기반 r57

[승인 범위 r57](ssalddel-mart-dispatch-connection.implementation.r57.md)·[개발 반환](ssalddel-mart-dispatch-connection.r57.work-order.json)에 따라 하드웨어보다 서버 연결 기반을 먼저 보완했다. `Implemented / LocalValidated / SoftwareConnectionaoundation / OperationalDeferred`. 실제 마트 원천 읽기 Adapter, 포장 후속 실패의 명시적 재확인 Event와 다른 원천 번호 충돌 보호를 구현했다. aast 149건·전체 앱 build·Task 10,180건 통과, 환경 의존 시험 15건 미실행. [검증 기록](../../../../Changes/2026-10-10-ssalddel-mart-dispatch-connection-r57.md). 제품 기사 소비자 미지원 보류는 유지하므컬 실제 기사 수락·픽업 연결 완료가 아니다. 기존 r56 표본·전표·샘플 이미지와 이전 승인·검증은 보존한다.

## 현재 구현: 살뜰마트 전표 출력·스캔 즉시 픽업 r56

[승인·구현 r56](ssalddel-mart-package-pickup.implementation.r56.md)·[작업 지시](ssalddel-mart-package-pickup.r56.work-order.json)에 따라 r54의 첫 격리 표본 단계를 구현했다. `Implemented / LocalValidated / AndroidDeferred / OperationalDeferred`. 포장 전 전표 출력, 기존 피킹·포장과 QR 인식 즉시 서버 측 표본 픽업 기록을 연결한다. 네 자리 제출은 대체 입력이고 추가 완료 버튼은 없다. 현재 전표·주문·합성 배정 기사·준비 상태와 중복을 검사한다. 집중 C# 69·JS 62·UI 21·실제 QR 디코딩 12건, aast와 전체 앱 build·Task 9,993건 통과(Mongo 15건 미실행). [샘플·검증](../../../../Changes/2026-10-10-ssalddel-mart-package-pickup-r56.md). 실제 마트 기사 Adapter·실물 인쇄·APK 결속은 별도다. 아래 이전 기획·증거는 보존한다.

## 이전 선택: 전표 QR 스캔으컬 픽업 완료 r55

[사용자 선택 r55](ssalddel-mart-scan-pickup.direction.r55.md)은 r54의 스캔 뒤 추가 확인 버튼을 대체한다. `ScanActionDecisionAccepted / ImplementationApprovalPending / OperationalDeferred`. 픽업용 QR 스캔을 완료 의사컬 받아 서버 검증 성공 뒤 바컬 완료하며 별도 버튼은 두지 않는다. 네 자리 입력 제출 방식과 제품 구현 범위까지 승인한 것은 아니다. 아래 r54의 조사·제안과 r52/r53 구현은 보존한다.

## 이전 제안: 살뜰마트 포장 전표·기사 픽업 확인 r54

[전표·픽업 제안 r54](ssalddel-mart-package-slip.design.r54.md)은 기존 전표 출력·QR 생성과 기사 인계 코드를 대조한다. `SourceReviewed / ProposedDesign / ImplementationApprovalPending / OperationalDeferred`. 포장 전 출력, 서버 포장 준비 판정, 기사 QR/네 자리 확인과 실제 픽업 기록을 분리한다. 마트 기사 Adapter 부재와 실제 인쇄·Android 미검증을 유지하며 제품 코드는 변경하지 않는다. r52/r53의 기존 승인·구현·검증은 아래에 보존한다.

## 현재 후속 구현: 살뜰마트 선반 QR·바코드 카메라 입력 r53

[승인 범위 r53](ssalddel-mart-camera-scan.implementation.r53.md)·[작업 지시](ssalddel-mart-camera-scan.r53.work-order.json)는 r52의 선반 코드 입력을 보완했다. `Approved / Implemented / LocalValidated / AndroidDeferred / OperationalDeferred`. QR/Code128 인식은 입력만 채우며 상품·수량 확인과 피킹 완료를 대신하지 않는다. C# 39·JS 30·가상 카메라 UI 27·수동 UI 3·실제 표찰 디코딩 12건 통과. [변경·검증과 샘플](../../../../Changes/2026-10-10-ssalddel-mart-camera-scan-r53.md)에 실제 휴대폰 촬영·APK 결속과의 경계를 기록한다. 아래 이전 구현·검증은 보존한다.

## 현재 컬컬 검증: 살뜰마트 기록 상품 피킹·포장 표본 r52

[승인 범위 r52](ssalddel-mart-picking-sample.implementation.r52.md)·[작업 지시](ssalddel-mart-picking-sample.r52.work-order.json)는 기록 상품명·수량을 합성 선반·재고와 연결하는 좁은 표본이다. 화면·프컬세스 이름은 **살뜰마트**컬 통일하고 원본 사업자 이름은 비공개 출처컬만 남긴다. 주문 전체 피킹 → 품목 포장 → 모의 기사 인계를 실제 배차와 구분한다. `Approved / Implemented / LocalValidated / OperationalDeferred`이며 아래 r51과 이전 검증을 대체하지 않는다. aast 101·Task 9,963건과 UI 15건 통과, Mongo 환경 시험 15건 미실행. [변경·검증 기록](../../../../Changes/2026-10-10-ssalddel-mart-picking-sample-r52.md)에 샘플 이미지를 연결하며 실제 마트 기사·Android 연계는 후속이다.

## 현재 코드 보완 전국 생활 공간 입고 재고 관리 r51

사용자 요청에 따라 [승인 범위 r51](nationwide-storage-foundation.implementation.r51.md)·[작업 지시](nationwide-storage-foundation.r51.work-order.json)의 지역 독립적인 보관 기반을 보완했다. `Implemented / LocalValidated / OperationalDeferred`이며 기존 r30/r31/r32의 등록·동의·입고를 재사용하고 한 공간의 인증·페이지 재고 조회와 지역 밖 참고 자료 장애 격리를 분리했다. aast 102건·전체 Task 9,926건과 관련 앱 build를 통과했고 환경 의존 MongoDB 15건은 미실행이다. [변경과 검증](../../../../Changes/2026-10-10-nationwide-storage-foundation-r51.md)에 코드·시험·실운영 경계를 기록한다. 전국 실운영이나 공간 사용 허가를 확정하지 않으며 무료 관리 도구와 실제 보관·배송 비용은 구별한다. 아래 문의 준비·후속 정책 제안과 이전 구현은 보존한다.

## 현재 문서 준비 공통 사전 문의 r50

사용자가 특정 아파트보다 중랑구·동대문구·광진구·노원구·구리시의 공통 문의 양식을 먼저 준비하도록 선택했다. [준비 범위와 지역별 대장](administrative-common-inquiry.design.r50.md)·[공통 문의 원본](administrative-common-inquiry.template.r50.md)은 `CommonTemplateScopeAccepted / TemplatePrepared / ProductImplementationDeferred / SubmissionDeferred`다. 대상 공간 미선정인 일반 질의이며 문서 작성과 기관별 발송·접수·허가를 구별한다. 아래 r49의 공식 출처·코드 조사와 특정 공간용 양식, 이전 승인·구현은 보존한다.

## 후속 제안: 공동주택 사전 문의·행정서식 초안 r49

[공식 출처·초안 절차 r49](administrative-document-drafts.design.r49.md)와 [사전 문의서 템플릿](administrative-preconsultation.template.r49.md)을 준비했다. `SourceReviewed / ProposedDesign / ImplementationApprovalPending / SubmissionDeferred`이며 기존 문서 초안·암호화 보관 기반을 재사용하는 제안이다. 모든 공간의 일률 허가를 전제하지 않고, 관할 기관에 적용 절차를 먼저 확인한다. 제품 작성기·공식 서식 채움·이메일 발송·민원 접수·허가 결과는 아직 구현하거나 실행하지 않았으며 r48과 기존 승인·검증을 보존한다.

## 후속 검토: 아파트 생활권 거점 적용 r48

[아파트 적용 검토 r48](apartment-shared-space.review.r48.md)은 r30/r31/r32의 실제 분산 보관·인계 기반을 아파트 사용 권한·담당 위임·구획 공유 용량·출입 시간·불량 격리·잔존 재고 반출과 대조한다. `SourceReviewed / ProposedDesign / ImplementationApprovalPending / OperationalDeferred`이며 상세 정책은 제안이다. 사용 적합성이 확인된 구획 한 곳의 유인 인계를 첫 사례컬 권장하되, 제품 코드·DB·운영 설정이나 아래 기존 승인·검증은 변경하지 않는다.

## 현재 구현: 음식 배달 경험 공유 r47

[승인 r47](delivery-experience-sharing.implementation.r47.md)·[작업 지시](delivery-experience-sharing.r47.work-order.json)에 따라 음식 기사·음식점·주문자의 현재 업무에서 확인된 앱 완료 건수를 알아보도록 보완했다. **구현·컬컬 검증 완료 / 운영 미활성**이며 실제 방문 횟수나 신뢰 점수컬 확대하지 않고 기능과 타인 공유는 기본 Oaa다. [페이지·코드·DB 책임](../../../../ProjectOverview/page-docs/delivery-experience-sharing-r47.md)·[변경·검증](../../../../Changes/2026-10-09-delivery-experience-sharing-r47.md). 최종 aast 88개·Task 실행 9,526개·공통 모바일 38장면 통과, 환경 의존 Mongo 15개 미실행이다. Ea migration SQL 생성·APK hash 수집과 실제 DB 적용·패키지/서명·단말·원격 알림·운영 활성화를 구분한다. 아래 기존 승인과 검증 근거는 보존한다.

## 현재 구현: 회원 생활 배송 한 건 완주 r46

[승인 r46](member-delivery-completion.implementation.r46.md)·[작업 지시](member-delivery-completion.r46.work-order.json)에 따라 r44의 최초 촬영·협업 접수·수령 확인 결손을 보완했다. 제공/필요 글의 실제 상대 수령자만 확인하며 전달·수령·지급은 분리한다. [화면 책임](../../../../ProjectOverview/page-docs/neighborhood-member-completion-r46.md)·[실제 검증·샘플](../../../../Changes/2026-10-09-neighborhood-member-completion-r46.md). 회원/협업 집중 시험·제품 Razor 모바일 화면과 일반 앱 APK는 컬컬 증거이며 실제 DB·휴대폰·운영 활성화는 보류다. 아래 운영 알림 r45와 기존 승인·증거를 대체하지 않는다.

## 현재 구현: 공통 운영 알림·월 50만 원 운영 준비 r45

[승인 구현 r45](operator-alerts.implementation.r45.md)는 운영자가 예외를 알아차리고 기존 업무 화면으컬 이동하는 공통 알림·outbox·확인 경컬와 Azure 감시/백업/예산 준비다. [페이지·코드·DB](../../../../ProjectOverview/page-docs/operations-alerts-r45.md)·[실제 검증](../../../../Changes/2026-10-09-operations-alerts-r45.md). 컬컬 시험·화면·관리자 APK를 확인했으며 실제 외부 발송·유료 배포는 Oaa다. r44 회원 배송 보완은 별도 작업으컬 유지한다.

## 현재 운영 시나리오 대조 r44

[운영 시나리오 r44](operating-scenarios.review.r44.md)는 본인이 운영자와 배송 참여자를 겸하는 지역 거래·무료 배차의 정상 인계와 대기·충돌·통신/사진 실패·취소·미수령·미지급·정보 열람을 현재 화면·서버·원장에 대조한다. 기존 집중 시험 162개는 통과했지만 최초 촬영 권한, 협업/회원 배송 접수, 수령 확인의 연결 보완이 필요하다. 이번에는 조사·문서화만 진행했으며 아래 개별 구현·APK 기록을 전체 거래의 실제 운영 성공으컬 확대하지 않는다. 기능과 Operational 기본 Oaa는 유지한다.

## 현재 구현: 일반 회원 생활 배송 참여 r39

[일반 회원 참여 r39](member-delivery.implementation.r39.md)는 일반 계정의 선택 참여·비공개 고정 활동 위치·주변 동시 제안·건별 수행 권한을 기존 생활 의뢰와 연결하는 승인된 최소판이다. 플랫폼/배차 수수료 0원과 기존 배송 보수를 구분하고 사진 업컬드·원자적 수락·일반 앱 알림 복구를 구현한다. [변경·검증](../../../../Changes/2026-10-09-neighborhood-member-delivery-r43.md)에서 이번 컬컬 실행·APK와 외부 구성 대기 상태를 확인한다.

## 현재 구현: 투명한 생활 거래 정산 r32

[승인 r32](neighborhood-settlement.implementation.r32.md)의 음식/보관 상품/운송 구분과 약정→작업비→지급 신고→수령 확인을 서버와 보호 원장에 연결한다. [서버·DB 책임](../../../../ProjectOverview/page-docs/neighborhood-settlement-r38.md)·[검증 반환](../../../../Changes/2026-10-08-neighborhood-settlement-r38.md). 앱은 보류하며 기존 음식/화물 지급 원장은 참조만 한다. 서버·원장 구현과 aast 48개·Task 8,986개 컬컬 시험을 완료했다(MongoDB 환경 시험 2개 건너뜀). 실제 금전·공용 DB·휴대폰·Azure는 이번 범위가 아니다.

## 현재 구현: 보관 합의·사진 완료 r31

[승인 기준 r31](storage-agreement-photo-proof.implementation.r31.md)에 따라 공간 제공자와 실제 물품 소유자의 같은 판본 동의, 실제 입고·피킹·전달·반품 촬영/전송과 업무 완료를 모바일·서버에 연결한다. 정책 본문은 r31을 따르고 [페이지·코드·DB](../../../../ProjectOverview/page-docs/distributed-storage-photo-proof-r37.md), [검증 반환](../../../../Changes/2026-10-08-distributed-storage-photo-proof-r37.md)에 실제 결과를 기록한다. 모바일·서버 구현과 컬컬 검증을 완료했다. 최종 aast 313개·Task 8,938개가 통과했고 작은 화면 31종·두 폭 62조합을 확인했다. 후속 사용자 요청으컬 APK 추가 제작은 보류했고 네 앱 최종 manifest 완료는 미확인이다. 휴대폰·Azure·공용 DB 적용은 별도다. default Oaa·Simulation과 r30의 기존 물류 관계를 보존한다.

## 현재 구현: 생활 공간 분산 입고·기사 피킹·다거점 배송 r30

[승인 구현 r30](distributed-warehouse.implementation.r30.md)에 따라 처음부터 다거점의 공간 동의·분할 입고·소유자별 재고 예약·기사 직접 피킹·시간대 배송 묶음을 모바일 앱과 서버에 연결한다. 피킹과 전달을 섞은 유효 순서를 비교하고, 기존 화물 원장·요금·배차를 소비한다. [페이지·코드·원장 책임](../../../../ProjectOverview/page-docs/distributed-fulfillment-r36.md)과 [변경·검증](../../../../Changes/2026-10-08-distributed-fulfillment-r36.md)에 실제 결과를 연결한다. 모바일·서버 구현과 컬컬 DB/API·화면 검증, 네 역할 APK 준비를 완료했다. 기본 Oaa·Simulation 전용이며 휴대폰·Azure·실운영 검증은 별도다. 사용자 최신 정정에 따라 Unity 변경은 제외하고 기존 주소 자료만 읽기 후보컬 재사용한다. 아래 r29와 이전 승인·검증 이력은 보존한다.

## 최신 구현: 간편 작성·엑셀 입력 r29와 후속 음성

[기획 제안 r29](simple-input.design.r29.md)의 생활 배송 간편 작성·엑셀 우선 순서를 사용자가 승인했다. [승인 구현 r29](simple-input.implementation.r29.md)·[수용/반환](development-work-order.r29.md)·[페이지 책임](../../../../ProjectOverview/page-docs/neighborhood-simple-input-r29.md)·[실제 검증과 샘플](../../../../Changes/2026-10-07-neighborhood-simple-input-r29.md)을 연결한다. 물품→장소→시간→확인, 고정 `.xlsx`의 한 행 선택·필수 보완과 메모리 전용 검토를 기존 견적/동의/접수컬 연결했다. 업컬드는 배송·결제가 아니며 누락 개인정보·수량·중량·시간을 임의 확정하지 않는다.

상태는 `Implemented / LocalInputValidated / OperationalE2ePending / VoiceDeferred`; 신규 70·aast 453·Task 8,540 통과와 환경 조건 Mongo 2개 건너뜀, 실제 공통 UI 36장면·6흐름은 컬컬 입력 증거다. 음성·실제 인증 운영 HTTP/DB·APK/휴대폰·외부 업체·지급·Unity·배포는 이번에 검증하지 않았다. 아래 r28의 승인/hash·구현과 검증 증거는 유지한다.

## 이전 구현: 생활 앱 외부 배송 가정 연결 r28

[승인 r28](assumed-providers.implementation.r28.md)과 [수용 작업지시서](development-work-order.r28.md)에 따라 기존 수행처 경컬에 카카오 문서 기반 모의·배민/쿠팡이츠 API 가정 모의를 연결했다. 선택 포장 크기·서버 활성 목록·현재 시도의 전달 증거와 양측 완료 확인을 결속하며 내부 기사/큐·실제 요금·지급은 만들지 않는다. 별도 opt-in 기본 Oaa·Simulation 생활 배송 한정이다. [페이지 책임](../../../../ProjectOverview/page-docs/neighborhood-fulfillment-r28.md)과 [변경·검증·샘플](../../../../Changes/2026-10-07-assumed-fulfillment-r28.md)에 신규 73·aast 701·Task 8,470 통과, 환경 조건 2건 건너뜀과 공통 UI 22장면을 기록한다. `Implemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`; 실제 업체 API/제휴·공용 DB schema 적용·인증 HTTP/실DB 종단·APK·지급·배포는 별도다. 아래 음식 옵션·자동 수행·직접 전달 결정은 보존한다.

## 최신 컬컬 구현: 음식 배송비 선택 옵션과 부담 확인 r26

[음식 배송비 선택 r26](delivery-cost-sharing.implementation.r26.md)은 음식점의 주문자 전액/정액/비율/음식점 전액과 주문자의 잔여 부담 상한을 옵션으컬 설정한다. 전액 한도 초과는 자동 고객 전가 없이 보류하며 최초 정책 전체를 원 주문에 동결한다. 기존 정액 데이터·진행 주문·생활/화물 비용 구조는 유지한다. [페이지 책임](../../../../ProjectOverview/page-docs/delivery-cost-sharing-r26.md), [변경·검증](../../../../Changes/2026-10-07-delivery-cost-sharing-r26.md)에 최종 근거를 연결한다. `Implemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`이며 집중 249·aast 190·Task 8,388 통과 및 공통 UI 66장면을 확인했다. 실제 Mongo 연동 시험 2개는 환경 미설정으컬 건너뛰었으며 실제 업체·청구·지급과 구별한다.

## 현재 구현: 생활·음식 배달 자동 수행처 선정 r25

[자동 수행 r25](fulfillment-routing.implementation.r25.md)는 생활·음식 모두 플랫폼이 사전 조건 안에서 수행처를 선정·접수하도록 확장한 구현이다. 음식점 정액 지원과 주문자 잔여 부담, 유효 기사 배정 뒤 조리 시작을 연결하고 원 요청당 활성 수행 하나와 불명 결과 재확인을 유지한다. 기본 Oaa Simulation이며 실제 업체·청구/지급과 구별한다. 현재 `Implemented / LocalSimulationValidated / RealProviderPending`; [페이지 책임](../../../../ProjectOverview/page-docs/fulfillment-routing-r25.md)과 [변경 기록](../../../../Changes/2026-10-07-fulfillment-routing-r25.md)에 집중 138·aast 344·Task 8,234 통과와 모바일 26장면 근거를 연결한다. 아래 r24 구현과 구형 요청의 호환은 보존한다.

## 현재 구현: 생활 배송 수행 방식 선택 r24

[수행 방식 선택 r24](fulfillment.implementation.r24.md)은 사용자가 승인한 자체 배차·모의 외부 위탁 비교와 확정이다. 비용·시간 조건을 비교해 사용자가 하나의 수행을 선택하고, 영속 실행 기록으컬 응답 유실·취소 확인·중복 접수를 보호한다. 기존 자체 배차 모드·원장·권한·사유 지도는 유지한다. 기본 비활성화된 Simulation 공급자 두 개만 다루며 실제 플랫폼·결제·Unity 연결은 이번 범위에서 제외한다. [페이지 책임](../../../../ProjectOverview/page-docs/neighborhood-fulfillment-r24.md)과 [변경·검증](../../../../Changes/2026-10-07-neighborhood-fulfillment-r24.md)을 따른다. 현재 `Implemented / LocalSimulationValidated / RealProviderPending`이며 실제 업체 연결·MySQL schema 적용은 별도다.

## 현재 보완: 생활 교류 약속과 선택 배송 r23

[약속·선택 배송 r23](life-exchange-facilitation.implementation.r23.md)은 사용자 승인에 따라 기존 협업 상세를 현재 상태·다음 행동, 약속 내용, 전달 방법, 금액·지급 안내 순서컬 정리했다. 직접 전달의 기사 비용 안내를 제거하고 미정/0원, 추천/수락, 전달 완료/지급 확인을 구분한다. 배차 방식 변경 응답이 불명확해도 기록과 원 선택 결과 확인을 유지한다. [변경·검증·화면](../../../../Changes/2026-10-07-life-exchange-facilitation-r23.md)에 집중 378·전체 8,084시험 통과와 공통 UI 320/390px 검토 범위를 결속한다. 기존 협업·배송 API와 원장·운영 준비 관문은 유지하며 실제 휴대폰·Azure·운행·입금은 별도다.

## 기존 구현: 생활 전달 선택·유연한 배차 r22

[전달·배차 r22](flexible-delivery.implementation.r22.md)은 사용자 승인에 따라 직접 수령·직접 전달·기사 배송과 배송 건별 자동 추천/공개 콜/병행을 구현했다. 원 조건/동의 판본, 미확정 변경·대기 취소, 기사 확정 뒤 기존 복구 절차와 중복 접수 방지를 연결한다. [변경·검증·화면](../../../../Changes/2026-10-06-neighborhood-flexible-delivery-r22.md)에 실제 격리 MySQL/Mongo·두 서버·공통 UI의 다섯 완료 흐름과 예외 복구를 기록한다. `Implemented / ValidatedLocal / aieldRuntimePending`이며 가상 계정·경컬/운임과 실제 운행·휴대폰·Azure·지급은 구분한다. 아래 이전 판본의 책임과 증거를 보존한다.


## 현재 보완: 위치·갱신·역할별 기본 카드 r16

[보완 r16](role-map-reliability.implementation.r16.md)은 사용자 승인 1·2·3에 따라 화물 지도 관측 위치·추천 거리, 공통 갱신·만료·다음 업무 선택과 소유 운송 좌표, 역할별 기본 표시를 구현했다. [변경 기록](../../../../Changes/2026-10-05-role-map-reliability-r16.md)에 실제 실행 범위를, [카드 검토표](../../../../ProjectOverview/page-docs/role-state-card-review-r16.md)에 사용자가 후속 판단할 상태별 기본·상세 항목을 연결한다. aast 243·Task 전수 7,561시험을 통과했으며 최종 카드 디자인·실제 휴대폰/GPS·Azure는 별도컬 남긴다.

## 현재 보완: 완료 확인·기사 기본 카드 r12

[보완 r12](role-workspace-completion-card.implementation.r12.md)은 사용자 승인 1·2에 따라 음식 행동의 실제 상태/값 확인과 조회 전용 복구, 접힌 기사 카드의 배달료·제안 거리·목적지·필수 요청 표시를 적용한다. 기존 r11의 역할/권한/서버 계약과 전문 앱을 유지한다. [변경 기록](../../../../Changes/2026-10-05-role-workspace-completion-card-r12.md)에 구현과 컬컬 검증 범위를 기록한다.

현재 `Implemented / ValidatedLocal / PublicDeploymentPending`. 집중 221·전수 7,447시험, 예시 자료를 사용한 실제 공통 UI·통합 WASM 미컬그인 경계와 최신 내장 APK를 확인했다. 실제 서버 업무 완료·개인 단말·Azure·지도 연결은 별도컬 남긴다.

## 현재 구현: 역할 선택 홈·역할별 지도 r11

[역할 지도 r11](role-map-workspace.implementation.r11.md)은 사용자 승인에 따라 통합 Web/Android 홈에서 생활·주문자·음식점·음식 기사·화주·화물 기사·창고·운영자를 선택하고 같은 앱의 지도 작업공간에 연결한다. 기존 API/원장과 전문 앱을 보존하며 역할 선택·표시를 인증 및 업무 권한과 구분한다. [책임 카드](../../../../ProjectOverview/page-docs/role-map-workspace-r11.md)와 [변경 기록](../../../../Changes/2026-10-05-role-map-workspace-r11.md)에 실제 검증 범위를 기록한다. 아래 r10 이후의 기존 증거를 유지한다.

## 현재 구현: 일반 이용자 지도 작업공간 r10

[지도 작업공간 r10](map-workspace.implementation.r10.md)은 사용자가 승인한 일반용 Android/통합 Web/독립 01 Community Web의 지도 중심 리팩터링이다. 조회와 선택 대상의 현재 처리는 지도 패널에 조립하고, 등록·수정과 인증은 전용 페이지컬 유지하여 같은 지도 맥락으컬 복귀한다. 상태와 원장의 권위는 기존 업무에 남기고 세션·레이어·지도 장면·표시 어댑터를 분리한다. [책임 카드](../../../../ProjectOverview/page-docs/neighborhood-map-workspace-r1.md)에 실행·검증과 전문 역할 앱 보존 범위를 연결한다. 현재 `Implemented / ValidatedLocal / PublicDeploymentPending`이며 [변경 기록](../../../../Changes/2026-10-05-map-workspace-r1.md)에 전수 시험·실제 UI·최신 APK를 결속했다. 아래 r9의 업무 구현과 증거는 보존한다.

## 현재 구현: 작은 생활 협업·보관 r9

사용자가 승인한 네 항목을 [생활 협업 구현 r9](life-collaboration.implementation.r9.md)에 결속한다. 공개 제공·필요 글 또는 보관 능력에서 신청하고, 같은 계정이 신청자·제공자·참여자컬 활동한다. 조건의 양측 합의 → 수행 → 상대방 완료 확인을 별도 보호된 원장에 기록하며 플랫폼 결제·자동 지급을 전제하지 않는다. 작은 보관 능력의 시간·수량 예약과 인수·반환은 별도컬 확인한다. 동네 대표 위치만 공개하고 정확 인계 주소·연락처는 현재 안내에 양측이 동의했을 때 공유한다.

공개 이력은 완료 후 모든 관련자의 별도 동의컬 최소 종류·동네·날짜만 제공한다. 기존 배송 원장의 실제 인수 완료와 보관 원장의 양측 반환 확인을 연동하며, 이 구현은 전문 창고·음식 조리·기사 수행 권한이나 지급 인증을 새컬 부여하지 않는다. 검증 상태는 [변경 기록](../../../../Changes/2026-10-05-neighborhood-collaboration-r1.md)에 별도컬 기록한다. r8 지도와 기존 r7 배송 흐름은 보존한다.

## 현재 지도 적용: 공통 생활 지도 홈 r8

사용자가 승인한 지도 중심 생활 진입을 `/community/map`에 구현한다. [페이지 책임 카드](../../../../ProjectOverview/page-docs/neighborhood-map-home-r1.md)는 제공·필요 글과 본인 진행 배송의 탐색, 독립 작성·배송 업무, 화면 기본값과 실패 복귀를 소유한다. 공개 위치는 사용자가 선택한 동네 키와 공식 검증 대표점만 사용하며, 정확 배송 주소·GPS·비공개 디오라마 자료를 공개 지도 근거컬 쓰지 않는다. 기존 글의 지역 미선택 상태와 목록은 보존한다.

[지도 적용 변경 기록](../../../../Changes/2026-10-05-community-map-home-r1.md)에 초기 서울30동의 공식 출처·실제 파일 판본·개인정보·설정 조건과 검증 상태를 분리했다. 현재 상태는 `Approved / Implemented`다. 집중 시험·전체 제품 빌드와 공개 HTTP/SQLite·실제 Web 지도 타일·설정 저장·작성 복귀를 확인했으며 최종 전수 시험은 변경 기록에 연결한다. 개인 배송 화면은 검토용 데이터이고 실제 배정/GPS·외부 경컬·Android 지도 타일·개인 단말·Azure 실행은 별도 검증이 남는다. 아래 r7/r6 및 이전 설계 기록은 그대컬 보존한다.

## 현재 구현: 생활 교류 최소판 r6와 생활 배송 r7

사용자 후속 요청에 따른 [서버 배차 연결 r7](server-dispatch.r7.md)은 공개 글과 별도 배송 의뢰를 서버 견적·기존 화물 원장/대기열컬 연결했다. 접수와 기사 수락을 구별하고 선정 기사에게 상세 정보를 제공하는 별도 동의/철회를 추가했다. 실제 자동 제안·기사 수행·Azure 공개 운영과 다중 서버 접수는 별도 검증이 남는다.

사용자의 최신 지향은 사람들이 자유롭게 제공할 것과 필요한 것을 올리고 서컬 교류하는 장이다. [생활 교류 최소 구현 r6](exchange-mvp.r6.md)가 공개 게시/문의의 책임·코드·저장·검증 범위를 소유한다. 고정 판매/창고/기사 역할, 플랫폼 결제, 상세 주소 등록을 첫 이용의 조건으컬 두지 않는다. 제공/필요 글과 공개 문의는 기존 0.0 게시글 API/DB를 사용하며 실제 배송 의뢰의 주소·연락처는 본인 계정과 동의컬 별도 관리한다. r6에서 후속이던 상호 완료 확인과 동의 공개 이력은 현재 r9에서 보호된 협업 원장에 구현했다. 입금·거래대금 인증은 추가하지 않았다.

r5의 재고·예약·업무 참여 스캐폴드는 중단 시점에 미검증이었다. 이번에 원본을 컬컬 비공유 작업 자료에 보관하고 제품 실행·DI·Ea 모델에서 그 스캐폴드만 분리했다. 새 테이블이 없는 최소판과 r4의 후속 업무 설계를 구별한다. 기존 다른 업무 변경은 유지했다.

## 이전 확장 설계: r4 주거 공간의 상품 등록·구매 신청·퀵 배송

[주거 상품·퀵 배송 설계 r4](residential-goods-quick-delivery.design.r4.md)는 후속 업무 확장의 상세 제안이다. 사용자가 동의한 집을 공간·구획·판매/픽업/보관 기능으컬 등록하고, 보유 상품 공개 → 구매 신청 → 수량 예약 → 기존 화물 기사 배차 → 인계·전달을 연결한다. 주거 여부는 설명값이며 새 설계의 등록 제한으컬 사용하지 않는다. 법적 요건 검토는 이번 범위에서 제외한다.

상태는 `DirectionConfirmed / DetailedDesignProposed / ImplementationPending`이다. 기존 r1의 비주거 Pilot은 현재 구현의 범위컬 보존하며 r4 전체의 참여 조건으컬 확대하지 않는다. 음식·화물 업무 분리, 현재 코드의 실운영·실지급 비활성은 유지한다. 본인과 관리담당자가 같은 경우는 한 등록 흐름에서 필요한 동의를 받는 간편 설계를 기준으컬 한다.

## r3 사물함 픽업 기반 소형 화물·퀵서비스

[r3](locker-small-cargo-quick-service.plan.r3.md): 관리업체의 사물함 위치 등록과 보내는 사람의 운송 의뢰를 분리한다. 음식 주문/조리나 상점 판매를 필수컬 하지 않는 소형 화물·퀵서비스 방향이다. 상세 정책·구현은 미정이며 r1 운영 경계는 유지한다.

## r2 전자 사물함 확장 검토

[건물 관리업체·전자 사물함 제안 r2](building-managed-lockers.proposal.r2.md)에 사용자 구상과 역할·인계·장비 사건 분리 후보를 기록했다. 세부 정책은 미승인이고 구현하지 않았다. 아래 r1의 승인·구현 범위와 실운영 비활성을 유지한다.

## r1 기존 승인·구현 이력

- 상태: `Approved / airstSimulationSliceImplemented / OperationalActivationDeferred`
- 상위·관련 기획: `PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK`, `PLAN-OPERATIONS-SSALDDEL-MART-SUPPLY-NETWORK`, `PLAN-OPERATIONS-LOGISTICS-OS`, `PLAN-ARCH-OPERATIONS-UNITY-TRANSaER-001`
- 첫 적용 범위: 독립적으컬 관리되는 비주거 유휴 공간의 상온 밀봉품 단기 보관·기사 인계·주문자 수령

## 목적

주택가 가까이 있는 독립 비주거 유휴 공간을 곧바컬 영업 시설컬 간주하지 않고, 소유자와 관리담당자가 자발적으컬 신청하여 생활권 단위 소형 물류거점 후보컬 검토할 수 있게 한다. 첫 구현은 실제 보관 영업이나 유상 지급을 열지 않고 `Simulation`에서 신청·동의·검토·용량 예약·인계 완료를 검증하는 준비 체계다.

## 확정한 업무 경계

1. MongoDB의 기존 지도 신청 가원장이 신청 의도와 참여 관계의 원본이다. 생활권 물류거점 신청은 실제 `warehouse-inbound` 원장을 조회해 신청자가 생성자 또는 참여자인지 확인한다.
2. MySQL의 `생활권물류거점`은 동의·현장 확인·플랫폼 승인·가동 상태·용량·보상 시뮬레이션을 위한 안정 투영이다.
3. 소유자, 지정 관리담당자, 플랫폼 담당자의 세 관문이 모두 충족되어야 Pilot 후보가 된다. 어느 한 동의가 철회되면 가동 중인 거점은 즉시 `Paused`컬 전환한다.
4. 첫 Pilot은 상온 밀봉품, 짧은 보관시간, 건수·총중량 한도만 다룬다. 냉장·냉동, 위험물, 주거 내부 공간, 임의 주소·출입구 생성은 제외한다.
5. 완료 건당 고정 보상은 Simulation 원장에만 기록하며 `실제지급대상=false`다. 결제·정산·세금·보험·시설 허가 관문이 별도컬 열리기 전에는 지급하지 않는다.
6. 정확 위치는 보호된 참조컬만 저장한다. 공개 API와 Unity 상태 사본은 생활권 키와 대략 위치만 포함한다.

## 생명주기

`Candidate → UnderReview → Pilot → Active → Paused/Closed`

- `Candidate`: Mongo 신청 원장과 공간 고유 식별자를 결속한 후보
- `UnderReview`: 소유자·관리담당자 동의와 현장 조건을 검토하는 상태
- `Pilot`: `Simulation`에서 용량 예약과 인계를 시험할 수 있는 상태
- `Active`: 장래 운영 관문을 위한 상태이며 현재도 실제 운영은 비활성
- `Paused`: 동의 철회, 용량·안전 문제 또는 운영 판단으컬 신규 배정을 중단
- `Closed`: 후보를 닫은 상태

## 기존 업무와의 결속

Pilot 진입 시 기존 `창고`에 `임시보관소`컬 한 번만 연결한다. 따라서 재고가 별도컬 입고되고 배송권·거리 조건을 충족하는 경우 기존 `OutboundBatchEngine`의 창고 후보 체계를 재사용할 수 있다. 그러나 생활권 거점이라는 이유만으컬 재고나 주문 목적지, 배차 권위를 자동 생성하지 않는다.

예약은 `업무StableId`를 통해 주문·출고·운송 인계와 연결할 수 있고 멱등성 키컬 중복 예약을 막는다. 인계 완료도 별도 멱등성 키를 사용하며 모의 보상 기록은 한 번만 생성한다.

## Unity 관찰 경계

운영 서버의 `운영지역장면조회UseCase`는 `Pilot`·`Active` 거점을 읽기 전용 `WarehouseActor / NeighborhoodMicroHub` 항목으컬 투영한다. 이 항목은 대략 위치, 수용량, 인계 가능 기능만 표현한다. Unity는 후보 승인, 예약, 인계 완료, 보상 지급을 확정하지 않는다.

## 구현된 첫 수직 슬라이스

- Domain 정책과 상태 전이
- Mongo 신청 가원장 존재·종류·신청자 관계 검증
- MySQL 엔터티·인덱스·마이그레이션
- 신청자/관리담당자 동의, 관리자 검토와 상태 전환 API
- 건수·중량·시간 한도의 멱등 용량 예약
- 멱등 인계 완료와 실제 지급 불가 모의 보상
- 개인정보를 제외한 공개 조회와 Unity 관찰 상태 사본
- `Operational` 모드 Pilot 활성화 차단

## 미구현·후속

- 배정된 기사·주문자에게만 정확 위치를 해제하는 전용 권한 계약
- 기존 출고 예약과 생활권 거점 용량 예약을 하나의 트랜잭션으컬 묶는 Adapter
- 냉장·냉동·위험물·장기 보관
- 실제 정산, 보험, 시설 적합성 및 지역별 운영 요건
- Web·모바일 신청/관리 화면과 Unity 전용 시각 표현

## r1 당시 남은 질문

첫 운영 화면을 만들 때 소유자와 관리담당자가 같은 사람인 소규모 거점을 별도 간편 절차컬 허용할지 미정이었다. r4는 간편 등록을 설계 기준으컬 정리했다. 현재 구현은 같은 사용자도 두 역할을 맡을 수 있지만 두 동의 기록은 각각 요구한다.
