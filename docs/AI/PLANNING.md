# Mirror 기획 목차

## 공동주택 기획의 기존 역할 화면 반영 r102 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r102 승인 구현](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-screen-integration.design.r102.md): `Implemented / LocalValidated / OperationalDeferred`. 주민의 기존 필요글 진입, 본인 약정 기본보수, 기존 입고 일정 요약의 세 범위다. Fast 34개·Task 12,017개 통과 및 루프백 20개 배치·격리 글 저장을 확인했다. [변경·검증](../Changes/2026-10-15-community-role-screens-r102.md)에 미지원 협업 조회·기기·실제 지급의 검증 상한을 기록하며 r101 및 이전 기획의 미정·전문 원장·권한 경계를 보존한다.

## 이전 기획: 분야 전환·주민 수요와 입고 판단 제안 r101 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r101 기획 답변](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r101.md): `TopicChangeAccepted / NextTopicProposed / ImplementationNotAuthorized`. r100의 업무 진입 질문은 `Deferred / NotDecided / TopicChangeRequested`다. 다음은 `resident-nonbinding-inbound-interest / Asked / Unanswered`이며 r100 이하 기획·미정과 기존 구현의 관계를 보존한다.

## 이전 기획: 고객센터 1차 검토·처리 인계 기획 r80 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r80 기획 답변](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r80.md): `AcceptedDirection / QuestionsOpen / ImplementationNotAuthorized`. 고객 신고 후 상담원의 간단한 1차 검토·처리 안내/인계를 수용했다. 금융 조치 결정권은 미정이며 다음 질문은 `support-agent-financial-action-authority / Asked / Unanswered`다. r78/r79 기획과 r76 제품 구현을 보존한다.

## 이전 기획: 고객 문제 신고 시 정산 보류 r79 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r79 기획 답변](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r79.md): `AcceptedDirection / QuestionsOpen / ImplementationNotAuthorized`. 고객 문제 신고 시 해당 주문 미지급 정산 보류를 수용했고 일부 보수 선지급 범위는 미정이다. 다음은 `reported-delivery-issue-release-authority / Asked / Unanswered`. r78 정상 정산 방향과 r76 구현을 보존하며 실제 지급·코드 반영 승인은 아니다.

## 이전 기획: 전달 증빙·지급 재원 기반 작업 보수 r78 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r78 기획 답변](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r78.md): `AcceptedDirection / QuestionsOpen / ImplementationNotAuthorized`. r77 첫 질문에 전달 증빙·지급 재원 확인 시 작업 보수 정산 방향을 수용했다. 다음 질문은 `reported-delivery-issue-payment-scope / Asked / Unanswered`. 기존 r76 구현을 보존하며 실제 지급·코드 반영 승인은 아니다.

## 이전 검토: 운영 약속·예외 종결 기획 문답 r77 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r77 검토 초안](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.review.r77.md): `Draft / QuestionsOpen / ImplementationNotAuthorized`. 아래 r76 승인 구현을 보존하는 후속 기획이며 미정 운영 약속 5개를 정리한다. 첫 질문은 `unconfirmed-receipt-earned-work-release / Asked / Unanswered`. 구현·운영 전환 승인이 아니다.

## 업무 시작부터 예외 마감까지 보완 r76 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r76](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-exception-closeout.implementation.r76.md): `Implemented / LocalValidated / DeviceCloudPaymentDeferred`. 기존 원장 아래 운행·화물·음식·입고/반출·수령 보류·활동 지급 신고·본인 주간 명세를 연결하고 [업무 트리](../ProjectOverview/page-docs/role-workflow-tree-r76.md)로 정상/예외 담당을 구분한다. Fast 481개·제품 Task 11,996개·공통 규칙 Task 2,001개 통과, 환경 의존 16개 건너뜀. 새 금전 정책·실제 입금·운영 활성화는 별도다.

## USB 없이 주문·배차·기사 앱 검증 r75 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r75 로컬 구현](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/dispatch-device-cloud-verification.implementation.r75.md): `Implemented / LocalValidated / DeviceCloudDeferred`. [전체 검증 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/dispatch-device-cloud-verification.plan.r75.md) 중 실제 정책/복구 9개·클라이언트 연결을 구현했다. Task 11,865개·집중 202개 통과·환경 의존 16개 건너뜀·두 기사 APK 0.1.77/177 준비. 실제 FCM/청취·기기·Azure 업무/운영·실금전은 별도다.

## 배송료 선결제와 화물 주간 지급 준비 r73 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r73](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/transport-prepayment-settlement.implementation.r73.md): `Implemented / LocalValidated / OperationalDeferred`. 배송료 근거 동결·실수납 상태 재조회·화물 주간 준비. 업무별 기록과 본인 주간 합산 기준을 구분하며 음식/피킹 통합·자동 마감·실제 지급은 후속이다.


## 기사 직선거리 요금과 추천 배차 표시 r72 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r72](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/straight-line-driver-pricing.implementation.r72.md): `Implemented / LocalValidated / OperationalDeferred`. 음식·생활 소형 배송의 직선 거리비와 업무별 공통 추천 표시. Fast 246개·Task 11,636개 통과·환경 의존 16개 건너뜀·전체 소비 앱 빌드 오류 0개. 실제 작업 범위·기사 보수와 고객 견적 분리, 기존 합의/동결 보존·기본 OFF·단말/운영 검증 별도.

## 기사 업무 선택과 공통 수락 보호 r71 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r71](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-participation.implementation.r71.md): `Implemented / LocalValidated / OperationalDeferred`. 기존 r36의 우선 1·2번과 공통 설정 UI. Fast 493개·분산 묶음 5개·Task 11,550개 통과, 환경 의존 16개 건너뜀·별도 MySQL 행 잠금 1개 통과. 지도 전체 통합·APK 합병·타 앱 확대는 후속이다.

## 비공개 APK 준비와 화주 인수증 연결 r70 (2026-10-15)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r70](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/private-apk-preparation.implementation.r70.md): 기존 인수증 등록의 모바일 입력·정본 재조회와 GCS 비공개 APK 로컬 준비. `Implemented / LocalValidated / CloudPublicationDeferred`. Fast 155·Task 11,425개 통과, 환경 의존 15개 건너뜀·로컬 Debug APK 두 개 검증. 보안 갱신·실제 업로드·외부 서버·물리폰·운영 전환은 별도다.

## 국내 화물 한 건 마감 r69 (2026-10-14)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r69](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/cargo-closeout.implementation.r69.md): 화주 작성·접수 보존, 삭제/완료 인수/사진 보호, 기사 재조회·사진·완료 후속 복구와 본인 운송대금 조회. `Implemented / LocalValidated / OperationalDeferred`. 실제 운영·입금·기기·브라우저 시각 검증과 화주 인수증 입력 UI는 후속으로 구분한다.

## 마트 고객 수령과 기존 보수 지급 조건 r68 (2026-10-13)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r68](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mart-customer-receipt.implementation.r68.md): 실제 주문자 전량 수령과 명시 문제 보류를 전문 원본에 기록해 기존 보수 지급 관문에 연결했다. `Implemented / LocalValidated / OperationalDeferred`. 집중 45개·Fast 347개·DB 호환 4개·Task 11,236개 통과, MongoDB 15개 건너뜀. 자동 송금·성과금·보류 해제·신규 화면은 제외한다.

## 주문별 원가·보수와 마트 작업 근거 r67 (2026-10-13)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r67](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mart-order-compensation.implementation.r67.md): 사용자 선택 1·2번의 원주문 참조 기반 본인 원가·보수 조회와 피킹·포장 실제 완료 근거의 정산 연결을 구현했다. `Implemented / LocalValidated / OperationalDeferred`. Fast 250개·Task 11,202개 통과, MongoDB 15개 건너뜀. 기존 r59~r61 원장·원가 귀속과 미정 단가를 보존하며 신규 화면·자동 지급은 제외한다.

## 입고·보관 구조 정리 r65 (2026-10-12)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r65](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-cleanup.implementation.r65.md): 기능 확장 없이 주문/사진 실행 책임과 공통 표시를 정리한다. Implemented / LocalValidated / OperationalDeferred. [작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-cleanup.r65.work-order.json)·[변경·후속](../Changes/2026-10-12-inbound-cleanup-r65.md). 기존 r63/r64 동작·외형·복구·기본 OFF 유지.

- 검증: Fast 225건·Task 11,167건 통과·실패 0건·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개/경고 145개. 기존 멤버 90개·Razor 출력 10개·CSS 3개·보호 파일 38개 보존을 확인했다. 기기 설치·운영 활성화·commit/push는 수행하지 않았다.

## 입고 협의 화면 시각 정돈 r64 (2026-10-12)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r64](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-visual.implementation.r64.md): 단색 Material Outlined 아이콘·항목 위계·반응형 묶음으로 r63의 공간/입고/보관 공통 UI를 정돈한다. `Implemented / LocalValidated / OperationalDeferred`. 기존 업무/권한/동의/복구·기본 OFF 보존. [작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-visual.r64.work-order.json)·[변경](../Changes/2026-10-12-inbound-visual-r64.md).

- 검증: Fast 74건·Task 11,167건 통과·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개/경고 144개·로컬 Edge 9개 배치 확인. 실제 단말/인증 HTTP·공유 DB 적용은 별도다.

## 공급자·공간 제공자의 입고 협의 구현 r63 (2026-10-12)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r63](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/supplier-space-inbound.implementation.r63.md): 사용자 직접 승인으로 r62 설계를 기존 원장·서버·공통 모바일 UI에 구현했다. `Implemented / LocalValidated / OperationalDeferred`. Fast 279건·Task 11,167건 통과·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개. 수용 조건·같은 판본 동의·전체 공간 예약량·실제 사진 입고·입고 정산 근거를 분리한다. [작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/supplier-space-inbound.r63.work-order.json)·[검증 반환](../Changes/2026-10-12-supplier-space-inbound-r63.md). 운영 OFF·실제 계약/단말/공유 DB/지급은 별도이며 이전 구현을 보존한다.

## 공급자·공간 제공자의 계약 후 입고 (2026-10-12)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r62](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/supplier-space-inbound.design.r62.md): `AcceptedDirection / ProposedDesign / ImplementationPending / OperationalDeferred`. 공급 제안·공간의 허용/거부 품목·조건 협의·동일 판본 동의·건별 입고 예약 승인 설계다. r31 보관 합의와 r60/r61 정산을 재사용하며 이전 구현 상태를 보존한다. 제품 코드·DB·실제 계약·운영·커밋/푸시는 이번 문서 작업에 포함하지 않는다.

## 생활 공간 입고·보관과 출고·배송 분리 (2026-10-12)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r61](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/settlement-economics.implementation.r61.md): 사용자 선택 1·2번의 입고·보관 원가 귀속 및 플랫폼 잔여금 배분을 구현하고 로컬 검증을 완료했다. Fast 239건·Task 11,088건 통과·환경 의존 MongoDB 15건 건너뜀. 운영 OFF·AI 연계 제외. [작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/settlement-economics.r61.work-order.json)·[검증 반환](../Changes/2026-10-12-settlement-economics-r61.md).

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r60](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/receiving-outbound.implementation.r60.md): 사용자 직접 승인으로 입고/출고 약정 경계와 지급 시점을 분리했다. Implemented / LocalValidated / OperationalDeferred. Fast 159건·Task 11,005건 통과·MongoDB 환경 의존 15건 건너뜀. [작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/receiving-outbound.r60.work-order.json)·[검증 반환](../Changes/2026-10-12-receiving-outbound-r60.md). r59 성과금과 r58 마트 이력을 보존한다.

## 생활 공간 활동 보수·공동 성과금 (2026-10-12)

- [PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB r59](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/activity-compensation.implementation.r59.md): 기존 원장에 실제 행별 기여·기본 보수와 별도 성과금·본인 목록/상세를 연결했다. Implemented / LocalValidated / OperationalDeferred. [작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/activity-compensation.r59.work-order.json)·[검증 반환](../Changes/2026-10-12-activity-compensation-r59.md). 마트 r58과 이전 승인은 보존한다.

## 음식 배차 알림 복구·기사 겸 운영자 r54 (2026-10-11)

- [PLAN-OPERATmONS-DmSPATCH-CORE r54](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/notification-recovery.implementation.r54.md)·[작업 명세](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/notification-recovery.r54.work-order.json): 사용자 선택 1·2·4번의 누락 알림 복구·제한 재시도·중요 예외 조치 안내·일반 기사 연속 수행을 기존 HTTP/SQLite/모바일 PageModel로 검증한다. 운영 OFF·명시 수락·기한/요금 불변·화물 계약을 유지한다. [결과/한계](../Changes/2026-10-11-food-dispatch-notification-recovery-r54.md).

## 음식 배차 정보 준비 상태 r53 (2026-10-11)

- [PLAN-OPERATmONS-DmSPATCH-CORE r53](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/information-readiness.implementation.r53.md)·[작업 명세](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/information-readiness.r53.work-order.json): 원주문·현재 조리·좌표·주문 풀·수요·기사 여력의 준비/누락/만료를 기존 수집 판단에 결속한다. 수집 판단의 준비와 고객 약속·기사 일정/요금·플랫폼 재원까지의 전체 조율 준비를 구분한다. 미확인 금액·약속을 만들지 않으며 부족하면 기존 배차 관문으로 복귀한다. [변경/검증 기록](../Changes/2026-10-11-food-dispatch-information-readiness-r53.md).

## 음식 주문 수집·배차 시점 판단 r52 (2026-10-11)

- [PLAN-OPERATmONS-DmSPATCH-CORE r52](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/batch-timing.implementation.r52.md)·[작업 명세](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/batch-timing.r52.work-order.json): 음식 첫 추천 전 짧은 주문 수집 또는 즉시 진행을 판단한다. 최초 접수·조합·확인된 조리 예정 시각·수요/기사 여력·자료 신선도를 대조하고 Am는 허용 행동만 선택한다. 기본 OFF·현재 정책 검토 초안·ready 0행·실제 공동 다건 수락/모델 학습/운영 변경 0을 유지한다. [변경/검증 기록](../Changes/2026-10-11-food-dispatch-batch-timing-r52.md).

- [PLAN-OPERATmONS-DmSPATCH-CORE r51](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/training-materials.implementation.r51.md)·[작업 명세](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/training-materials.r51.work-order.json): 확인된 과거 기록과 합성 수요·기사·위치를 결합한 현재 정책의 학습 후보. 원시 4,041개에서 중복 9개를 격리하고 후보 4,032개·대표 검토 24개를 준비했다. `CurrentPolicyDraft / Pending`, ready 0행·가중치 학습/모델 호출/운영 변경 0을 유지한다. [변경/검증 기록](../Changes/2026-10-11-dispatch-training-materials-r51.md).

## 지역 배차 판단 준비 r50 (2026-10-11)

- [PLAN-OPERATmONS-DmSPATCH-CORE r50](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/regional-management.implementation.r50.md)·[작업 명세](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/regional-management.r50.work-order.json): 사용자 선택2·3·4·5번의 수요 참여·기존 기사 보호·수행 종료 기준 후속 픽업·단건/2건 조합 검토. 확인된 공개 매장/역사 메뉴와 합성 고객·기사로27개 지역 시나리오를 준비하고 루프백 HTTP/SQLite/모바일 PageModel을 검증한다. 기본 OFF·원장 권위·3건 상한·사람 검토 대기 유지. [변경/결과](../Changes/2026-10-11-regional-dispatch-management-r50.md).

## 예외 복구 코드 책임 분리 r2 (2026-10-10)

- [PLAN-OPERATmONS-EXCEPTmON-CONTmNUmTY r2](Planning/공통/PLAN-OPERATmONS-EXCEPTmON-CONTmNUmTY/exception-continuity.refactoring.r2.md): `mmplemented / LocalValidated / DeviceAndOperationalDeferred`. 음식 역할별 요청 준비·완료 근거와 운영자 연결 배송 복구 상태/표시를 기존 동작을 유지하며 분리했다. Fast 116건·전체 소비 앱 build·Task 10,328건 통과, 실패 0·환경 의존 15건 건너뜀. 실제 제품 화면 37항목을 확인했다. [작업 지시](Planning/공통/PLAN-OPERATmONS-EXCEPTmON-CONTmNUmTY/exception-continuity.r2.work-order.json)·[페이지 책임](../ProjectOverview/page-docs/exception-continuity-refactoring-r2.md)·[검증과 한계](../Changes/2026-10-10-exception-continuity-refactoring-r2.md). 기존 r1 및 다른 작업의 증거를 보존한다.

## 살뜰마트 기사 수락·픽업·전달 소프트웨어 연결 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r58](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-driver-work.implementation.r58.md): `mmplemented / LocalValidated / SimulationConsumerBound / OperationalDeferred`. r57의 기사 소비자 결손을 전용 수락→전표 입력 즉시 픽업→전달 APm·보호 전표·요청 영수증·후속 Outbox와 독립 FDriver 화면으로 보완했다. Fast 262건·전체 앱 build·Task 10,299건 통과, 환경 의존 15건 건너뜀. [개발 반환](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-driver-work.r58.work-order.json)·[페이지 책임](../ProjectOverview/page-docs/ssalddel-mart-driver-work-r58.md). Simulation·기본 OFF를 유지하며 실제 화면 렌더·인증 HTTP/DB·하드웨어·실운영·결제·단말은 별도다. 아래 이전 기획·검증을 보존한다.

## 살뜰마트 배차 소프트웨어 연결 기반 (2026-10-10, 이전 기준선)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r57](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-dispatch-connection.implementation.r57.md): `mmplemented / LocalValidated / SoftwareConnectionFoundation / OperationalDeferred`. 마트 원천 읽기·포장 재확인·공통 큐 원천 충돌 보호와 실제 Service 연결 시험을 보완했다. Fast 149건·전체 앱 build·Task 10,180건 통과, 환경 의존 시험 15건 미실행. [개발 반환](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-dispatch-connection.r57.work-order.json). 제품 기사 소비자 미지원 보류를 유지하며 실제 배차·마트 기사 수락/픽업·하드웨어 연결과 구분한다. r56의 합성 전표·픽업과 이전 증거는 보존한다.

## 역할 앱 예외 후 업무 지속 보완 (2026-10-10)

- [PLAN-OPERATmONS-EXCEPTmON-CONTmNUmTY r1](Planning/공통/PLAN-OPERATmONS-EXCEPTmON-CONTmNUmTY/exception-continuity.implementation.r1.md): `mmplemented / LocalValidated / DeviceAndOperationalDeferred`. 화주·생활 배송의 접수 복원, 분산 사진/명령 재조회, 하차 완료 확인, 지원 사건 후 원 배송 인계, 공통 음식 요청 조회, 지도 재개와 늦은 수입 조회 응답 보호를 보완했다. 최종 Fast 543개·전체 앱 build·Task 10,180개 통과, 실패 0·환경 시험 15개 미실행이며 실제 화면 10항목과 OS 선언 63개를 확인했다. [작업 지시](Planning/공통/PLAN-OPERATmONS-EXCEPTmON-CONTmNUmTY/exception-continuity.r1.work-order.json). 실제 기기·운영 DB·Azure·외부 실행과 커밋/푸시는 범위 밖이다.

## 살뜰마트 전표 출력·QR 스캔 즉시 픽업 표본 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r56](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-package-pickup.implementation.r56.md): `mmplemented / LocalValidated / AndroidDeferred / OperationalDeferred`. r54 조사·r55 선택을 기존 피킹/포장·선반 입력에 연결했다. 포장 전 전표 출력과 QR/네 자리 입력 한 번의 표본 픽업을 구현하고 준비·판본·합성 기사·멱등 관문을 유지한다. [작업 지시](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-package-pickup.r56.work-order.json), [화면 책임](../ProjectOverview/page-docs/ssalddel-mart-package-pickup-r56.md), [검증·샘플](../Changes/2026-10-10-ssalddel-mart-package-pickup-r56.md). 실제 기사 앱·운영 DB/Outbox·단말·물리 인쇄는 후속이다.

## 살뜰마트 전표 QR 스캔으로 픽업 완료 선택 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r55](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-scan-pickup.direction.r55.md): `ScanActionDecisionAccepted / mmplementationApprovalPending / OperationalDeferred`. r54의 스캔 뒤 추가 버튼 제안을 대체한다. 픽업용 QR 스캔 후 서버 검증 성공 시 바로 완료하며 네 자리 입력 제출 방식·제품 구현은 별도다. 이전 조사·승인·검증은 보존한다. 다음 질문 없음.

## 살뜰마트 포장 전표·기사 픽업 확인 제안 이력 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r54](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-package-slip.design.r54.md): `SourceReviewed / ProposedDesign / mmplementationApprovalPending / OperationalDeferred`. 기존 전표·QR 기반 재사용과 포장 전 출력의 조사·제안 이력이다. QR 스캔 뒤 추가 완료 버튼 제안은 r55가 대체하며 나머지 미승인 세부안과 r52/r53의 이전 구현·검증은 보존한다.

## 살뜰마트 선반 QR·바코드 카메라 입력 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r53](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-camera-scan.implementation.r53.md): `Approved / mmplemented / LocalValidated / AndroidDeferred / OperationalDeferred`. r52를 보존하고 합성 선반 QR·Code128 카메라 입력을 추가했다. 상품·수량 확인과 명시적 피킹 완료는 유지한다. C# 39·JS 30·가상 카메라 Um 27·수동 Um 3·실제 표찰 디코딩 12건 통과. [검증·샘플](../Changes/2026-10-10-ssalddel-mart-camera-scan-r53.md), [작업 지시](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-camera-scan.r53.work-order.json). 실제 휴대폰 촬영·MAUm/운영 결속은 별도다.

## 살뜰마트 기록 상품 피킹·포장 표본 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r52](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-picking-sample.implementation.r52.md): `Approved / mmplemented / LocalValidated / OperationalDeferred`. 기록 상품명·수량을 합성 선반에 결속하고 기존 피킹·포장 UseCase와 전체 주문 선행 관문을 재사용·보완했다. 화면·프로세스는 살뜰마트, 원본 사업자 명칭은 비공개 계보로만 보존한다. Fast 101·Task 9,963건과 Um 15건 통과(Mongo 환경 15건 미실행). [검증·샘플 이미지](../Changes/2026-10-10-ssalddel-mart-picking-sample-r52.md). 실제 마트 기사·Android 연계는 모의 인계와 별도다.

## 전국 생활 공간 입고 재고 관리 코드 기반 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r51](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/nationwide-storage-foundation.implementation.r51.md): `mmplemented / LocalValidated / OperationalDeferred`. 아파트를 포함한 전국 공통 공간 관리에 기존 r30/r31/r32를 재사용하며 한 공간의 독립·권한 제한·페이지 재고 조회와 참고 건물 후보의 지역 밖 장애 격리만 보완했다. [코드·검증](../Changes/2026-10-10-nationwide-storage-foundation-r51.md), r50 문의 준비와 r48 미승인 상세 정책은 별도다.

## 생활권 보관 공간 공통 사전 문의 양식 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r50](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/administrative-common-inquiry.design.r50.md): `CommonTemplateScopeAccepted / TemplatePrepared / ProductmmplementationDeferred / SubmissionDeferred`. 사용자 선택에 따라 특정 공간 미선정인 [공통 문의 원본](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/administrative-common-inquiry.template.r50.md)과 중랑구·동대문구·광진구·노원구·구리시의 준비 대장을 만들었다. 문서 준비에 한정하며 기관별 발송·접수·회신·개별 공간 승인은 별도다. r49 조사와 이전 구현·검증을 보존한다.

## 공동주택 거점 사전 문의·행정서식 초안 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r49](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/administrative-document-drafts.design.r49.md): `SourceReviewed / ProposedDesign / mmplementationApprovalPending / SubmissionDeferred`. 공식 행위허가/신고 서식과 기존 문서 기반을 대조하고 [문의서 샘플](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/administrative-preconsultation.template.r49.md)을 준비했다. r48 후속 제안이며 제품 작성·발송·접수·운영 승인은 아니다.

## 아파트 생활권 물류 거점 적용 검토 (2026-10-10)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r48](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/apartment-shared-space.review.r48.md): `SourceReviewed / ProposedDesign / mmplementationApprovalPending / OperationalDeferred`. r30/r31/r32를 재사용하며 공간 사용 권한·교대 담당·공유 용량·출입 시간·불량 격리·철회 후 반출의 보완 순서를 제안한다. 기존 코드 대조·기획 보완이며 제품 구현·현장 운영 승인은 아니다.

## 주문자 메뉴 사진·기록 자료 연결 (2026-10-10)

- [PLAN-OPERATmONS-ORDERER-MENU-CATALOG r1](Planning/공통/PLAN-OPERATmONS-ORDERER-MENU-CATALOG/orderer-menu-catalog.r1.md): `mmplemented / LocallyVerified`. 기존 주문자 대표 이미지와 비공개 기록 메뉴·금액·사진 검토, 기존 Azure Blob 재사용 보관 명세를 구현했다. 최종 범위는 로컬 준비까지이며 Fast 219·Task 9,886개·화면 12장면 통과. 단가 미확인·과거 금액·사진 사용 조건을 보존하고 실제 업로드·운영 메뉴 등록·다중 상세 갤러리는 완료로 보고하지 않는다.

## 실제 배달 기록 기반 로컬 검증 (2026-10-10)

- [PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR r54](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/delivery-history-local-replay.r54.md): `mmplemented / LocalValidated / DeviceDeferred / OperationalDeferred`. 기존 574건·391지점의 읽기 전용 추출·기사/음식점 조회·서버/네이티브 로컬 대조를 완료했다. Fast 194·Task 9,824개 통과(Mongo 환경 15개 미실행), 320/390px 전체 내역 검증. 단말/운영 활성화와 구별한다. [변경 기록](../Changes/2026-10-10-delivery-history-local-replay-r54.md).

## 기사 배달료 상세·현재 수행 안내와 USB 설치 (2026-10-09)

- [PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR r53](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-detail-work-copy.r53.md): `mmplemented / LocalValidated / Devicemnstalled / StartupAndReadOnlyNavigationValidated / ServerWorkflowDeferred`. r52 후속 우선순위 3·4와 단말 업데이트 설치 범위다. [작업 범위](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-detail-work-copy.r53.work-order.json)·[변경·검증 기록](../Changes/2026-10-09-driver-detail-work-copy-r53.md). 금액·명령·상태·개인정보 수명을 유지하며 설치와 서버/실제 업무 검증을 구별한다.

## 기사 내 정보 메뉴·배달 내역 (2026-10-09)

- [PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR r52](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-profile-history.r52.md): `mmplemented / LocalValidated / DeviceDeferred`. 내 정보 메뉴·독립 설정·완료 목록 행 선택과 상세 복귀를 구현했다. [작업 범위](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-profile-history.r52.work-order.json)·[검증 기록](../Changes/2026-10-09-driver-profile-history-r52.md). 요금 상세/현재 카드 추가 수정은 후속이며 단말·운영은 별도다.

## 음식 기사 책임 메타데이터·탐색 지도 (2026-10-09)

- [PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR r51](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-code-metadata.r51.md): `mmplemented / LocalValidated / DeviceAndOperationalDeferred`. 음식 기사 핵심 45단계·명시 소스 결속 41개와 현재/정산·순수/영속·공통/네이티브 경계를 연결했다. [작업 범위](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-code-metadata.r51.work-order.json)·[탐색 안내](../Architecture/FoodDriverCodeNavigation.md)·[생성 지도](generated/food-driver-code-map.md)·[검증 기록](../Changes/2026-10-09-food-driver-code-metadata-r51.md). Fast 264·Task 12,751개 통과(환경 의존 Mongo 15개 미실행), 기존 기본 지도·E 책임 검사·서명된 기사 APK를 확인했다. 실제 기기·운영은 별도다.

## 음식 기사 DB–APm–화면 리팩토링 (2026-10-09)

- [PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR r50](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-vertical-refactor.r50.md): `mmplemented / LocalValidated / DeviceAndOperationalDeferred`. 음식 기사 운행 시작·신규 수신부터 수락/픽업/전달까지 저장·후속 복구·현재 조회·화면 판단을 분리했다. Fast 1,061개·Task 실행 9,701개·공통 모바일 18장면 통과, 별도 Mongo 환경 시험 15개 미실행. [작업 범위](Planning/공통/PLAN-OPERATmONS-DRmVER-VERTmCAL-REFACTOR/driver-vertical-refactor.r50.work-order.json)·[페이지 책임](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#dbapi화면-책임-분리-r14)·[이번 검증 기록](../Changes/2026-10-09-driver-vertical-refactor-r50.md). 추가형 migration·APK 준비와 실제 단말·공용 DB·운영을 구분한다.

## 음식 배달 경험 공유 (2026-10-09)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r47](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/delivery-experience-sharing.implementation.r47.md): `mmplemented / LocalValidated / OperationalDeferred`. 음식 기사·음식점·주문자가 현재 업무에서 확인된 완료 건수를 선택 공유한다. 공식 수령지 선택을 서버에서 다시 대조하고 상세주소·타 고객 이력은 공개하지 않는다. 최종 Fast 88개·Task 실행 9,526개·공통 모바일 38장면 통과, 환경 의존 Mongo 15개 미실행. 기능/공유 기본 OFF이며 실제 DB 적용·APK 패키지/서명·단말 검증과 구분한다. [페이지 책임](../ProjectOverview/page-docs/delivery-experience-sharing-r47.md)·[이번 변경·검증](../Changes/2026-10-09-delivery-experience-sharing-r47.md).

## 주문 원장 구조 보완·총괄 관리자 명세 (2026-10-09)

- [PLAN-OPERATmONS-ORDER-CENTERED-WORK-NETWORK r7](Planning/공통/PLAN-OPERATmONS-ORDER-CENTERED-WORK-NETWORK/order-ledger-admin-refactor.implementation.r7.md): `ApprovedScope / mmplemented / LocalValidated / AdminSpecificationPrepared / RealDbAndDeviceValidationPending`. 음식·물품 흐름을 동일 깊이로 대조하고 음식 배송 참조·목록/상세 단계 판독·연결 응답 공개 보호·기사 조회 실패 복구를 구현했다. Fast 886개·Task 9,462개 통과와 음식 기사 Android build를 확인했다. 관리자 네 업무의 명세이며 관리자 화면·지사 권한·실지급 구현은 제외한다. [이번 변경·검증](../Changes/2026-10-09-order-ledger-admin-refactor-r7.md).

## 회원 생활 배송 한 건 완주 (2026-10-09)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r46](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/member-delivery-completion.implementation.r46.md): `mmplemented / LocalValidated / RealDbE2ePending / DeviceAndOperationalDeferred`. 최초 촬영·협업 접수·실제 반대 수령자 확인을 연결하고 같은 요청의 SQL/Mongo 부분 저장·동시 완료를 복구한다. Fast 190·Task 실행 9,458개 통과와 환경 의존 15개 미실행을 구분한다. [실행 결과와 샘플 화면](../Changes/2026-10-09-neighborhood-member-completion-r46.md). 별도 운영 알림 r45의 승인·검증은 보존한다.

## 공통 운영 알림·월 50만 원 운영 준비 (2026-10-09)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r45](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/operator-alerts.implementation.r45.md): `mmplemented / LocalValidated / ExternalActivationDeferred`. 운영자 앱·Gmail 경로와 독립 Azure 장애 감시·암호화 백업·예산 설정을 준비했다. 긴급 발생과 일일 요약, 알림 확인과 업무 해결을 분리한다. [실행 결과](../Changes/2026-10-09-operations-alerts-r45.md)에서 APK·시험·미실행 범위를 확인한다.

## 지역 거래·무료 배차 운영 시나리오 대조 (2026-10-09)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r44](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/operating-scenarios.review.r44.md): `SourceReviewed / FocusedTestsPassed / MemberFlowGapsmdentified / OperationalDeferred`. 본인이 운영자·배송 참여자를 겸하는 정상 인계와 문제 상황을 대조했다. 기존 집중 시험 162개 통과와 최초 촬영·협업 접수·수령 확인의 미연결을 구분하고, 운영자 개입 및 후속 구현 완료 조건을 정리했다. 제품 코드·실운영·배포는 변경하지 않았다.

## 일반 회원의 근처 생활 배송 참여 (2026-10-09)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r39](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/member-delivery.implementation.r39.md): `mmplemented / LocalValidated / DeviceAndOperationalDeferred`. 일반 회원·기존 요금·등록 활동 위치·동시 제안을 사용자 선택으로 확정했다. 본인 수락 건만 수행하고 사진 업로드·상태 전이·앱 알림을 연결했다. 사용자 선택에 따라 로컬 검증·APK까지 완료했다. [실행 결과](../Changes/2026-10-09-neighborhood-member-delivery-r43.md).

## 기사 기준 위치·권역 복귀 우대 구현 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r38](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/driver-return-area.implementation.r38.md): `mmplemented / LocalValidated / DeviceAndOperationalDeferred`. 승인된 r37 기준점·반경·전체 수행 후 복귀 개선을 기사 비공개 설정과 실제 음식/화물 추천에 연결한다. 음식/화물 공동 여력 r36은 별도 후속이며 확인 불가 시 가점을 보류한다. [변경·검증 기록](../Changes/2026-10-08-driver-return-area-r42.md).

## 기사 기준 위치와 권역 복귀를 배려하는 배차 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r37](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/driver-return-area.design.r37.md): `AcceptedDirection / ProposedDesign / mmplementationPending`. 기존 오늘/기본 복귀지·화물 개인/조율 점수를 대조하고 음식의 실제 큐 미연결·입력 오류·선제 후보 제한을 확인했다. 기사 선택 기준점과 반경(7km는 예), 전체 수행 후 복귀 이익, 단일 가점 반영을 제안한다. r36 다건 수행의 후속 설계이며 제품 정책은 아직 바꾸지 않는다.

## 기사 자율 다건 수행과 공동 적재·일정 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r36](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/driver-flexible-multi-work.design.r36.md): `AcceptedDirection / ProposedDesign / mmplementationPending`. 기사 개인의 희망 건수·배달통 여력으로 음식/생활 물품 제안을 선택하고, 기존 독립 원장을 참조해 같은 기사·차량의 적재량/시간을 공동 검사하는 후속 설계다. 기존 음식 최대 3건·화물 추가 수락·분산 순회의 차이를 소스에서 확인했다. 제품 코드·배차 정책은 아직 바꾸지 않는다.

## 원주문 선택·관계 동시 변경 보호 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r35](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/root-order-selection-concurrency.implementation.r35.md): `mmplemented / LocalValidated / DeviceAndOperationalDeferred`. r40에서 남은 원주문 선택 양식과 하위원장 진행/관계 변경의 저장 단계 동시 보호를 구현했다. 최종 Fast 308개·Task 9,147개 통과, 격리 Mongo 경합과 제품 예시 화면 확인 완료. [화면 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md)·[검증 반환](../Changes/2026-10-08-root-order-selection-concurrency-r41.md).

## 주문 한 건의 역할·조건·현재 업무 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r34](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/order-ledger-role-refactor.implementation.r34.md): r40에서 분산 주문의 원주문 연결과 조건 잠금/재동의, 현재 업무 안내·기존 문제 접수를 보완했다. 당시 Fast 323개·Task 9,078개 통과, 환경 의존 MongoDB 시험 2개 미실행이다. 당시 남은 원주문 선택 Um·동시 보호는 r35/r41에서 구현·로컬 검증했으며 실제 운영은 별도다. [주문 상세 책임](../ProjectOverview/page-docs/OrdererApp/OrdererApp-P05/README.md)·[검증 반환](../Changes/2026-10-08-order-ledger-role-refactor-r40.md)에 판본별 근거를 보존한다.

## 건별 프리랜서 계약 기본·꺼진 미성년 정책 준비 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r33](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/task-contract-policy-preparation.implementation.r33.md): `mmplemented / LocalValidated / ServerPolicyOnly / MinorParticipationDisabled`. 사용자의 서버 옵션 준비 승인을 수용했다. 새 의뢰의 계약 준비 기본값과 비활성 후보를 추가하며 실제 계약·참여 허가를 만들지 않는다. 전용22개·Fast101개·Task9,008개 통과, MongoDB 환경 시험2개 미실행. [서버·DB 책임](../ProjectOverview/page-docs/neighborhood-task-contract-policy-r39.md)·[검증 반환](../Changes/2026-10-08-neighborhood-task-contract-policy-r39.md).

## 생활 거래의 투명한 정산 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r32](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/neighborhood-settlement.implementation.r32.md): `mmplemented / LocalValidated / ServerAndLedgerOnly / AppAndOperationalDeferred`. 음식·보관 상품·운송의 약정·실제 작업비·지급 신고·수령 확인을 구현하고 Fast 48개·Task 8,986개를 통과했다(MongoDB 환경 시험 2개 건너뜀). [서버·DB 책임](../ProjectOverview/page-docs/neighborhood-settlement-r38.md)·[검증 반환](../Changes/2026-10-08-neighborhood-settlement-r38.md). 앱은 보류하고 기존 r31 업무 원장을 보존한다.

## 분산 보관 합의·사진 완료 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r31](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/storage-agreement-photo-proof.implementation.r31.md): `mmplemented / LocalValidated / ApkDeferred / DeviceAndOperationalPending`. 사용자 승인에 따라 공간 제공자↔실제 물품 소유자 합의와 입고/피킹/전달/반품 사진 완료를 모바일·서버에 구현하고 로컬 검증했다. [페이지·코드·DB](../ProjectOverview/page-docs/distributed-storage-photo-proof-r37.md), [검증 반환](../Changes/2026-10-08-distributed-storage-photo-proof-r37.md). r30과 기존 Unity·영상 작업은 보존한다.

## 생활 공간의 분산 입고·기사 피킹·다거점 배송 (2026-10-08)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r30](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/distributed-warehouse.implementation.r30.md): `mmplemented / LocalValidated / ApkPrepared / DeviceAndOperationalPending`. 다거점 공간 동의·소유자/판매자별 재고·시간대 주문 묶음·기사 직접 피킹을 기존 화물 배차와 연결한다. 피킹/전달 혼합 순서와 총 예상 소요 시간을 비교하며 최신 정정에 따라 모바일·서버만 구현한다. [페이지 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md)과 [변경·검증](../Changes/2026-10-08-distributed-fulfillment-r36.md)에 결과를 결속한다. 기존 r29·Unity 작업은 보존한다.

## 사전 합의 요금표와 음식 역할 알림 연결 (2026-10-08)

- [PLAN-SYSTEM-ROLE-BUSmNESS-OPTmONS r4](Planning/시스템/PLAN-SYSTEM-ROLE-BUSmNESS-OPTmONS/agreements-alerts.implementation.r4.md): `mmplemented / LocalValidated / ApkPrepared / DeviceAndBackgroundPushPending`. 우선순위3·4의 현재 사용자 승인을 수용했다. 요금표 제안/수락과 실제 업무 선택을 기존 산정·배차·정산 근거에 연결하고, 음식 기사/음식점의 알림 메시지·기계음과 독립 설정 화면을 구현했다. [페이지 책임](../ProjectOverview/page-docs/agreed-rates-notification-settings-r1.md)과 [Fast217·Task8,708 및 APK 검증 결과](../Changes/2026-10-08-agreed-rates-native-alerts-r35.md)를 따른다. r1/r3·r34와 다른 작업은 보존하며 기존 DB 적용·실기기 소리·백그라운드 푸시·실금전·Azure는 별도다.

## 생활 배송 간편 작성·엑셀 입력과 후속 음성 (2026-10-07)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB 승인 r29](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/simple-input.implementation.r29.md): `mmplemented / LocalmnputValidated / OperationalE2ePending / VoiceDeferred`. XML이 아니라 엑셀로 정정한 간편 입력의 첫 slice를 승인·구현했다. 직접 작성·선택행을 같은 초안으로 가져와 부족 정보를 보완하고 기존 견적·동의·접수로 합류한다. [수용/반환](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/development-work-order.r29.md)과 [검증·샘플](../Changes/2026-10-07-neighborhood-simple-input-r29.md)을 연결하며 r28 승인/hash와 이전 증거를 보존한다. 음성·실제 인증 HTTP/DB·단말은 후속이다.

## 생활 앱 외부 배송 가정 연결 (2026-10-07)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r28](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/assumed-providers.implementation.r28.md): `mmplemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`. 기존 수행처의 서버 활성 목록·선택 포장 크기·현재 모의 전달 증거를 양측 완료 확인에 연결한다. 별도 opt-in 기본 OFF·생활 배송 한정이며 실제 업체 APm/제휴·요금·공용 DB 적용·지급은 확인하지 않았다. 승인 hash·[수용/반환](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/development-work-order.r28.md)과 [실제 검증](../Changes/2026-10-07-assumed-fulfillment-r28.md)을 구별한다.

## 역할별 선택 옵션과 사전 합의 요금표 (2026-10-07)

- [PLAN-SYSTEM-ROLE-BUSmNESS-OPTmONS r3](Planning/시스템/PLAN-SYSTEM-ROLE-BUSmNESS-OPTmONS/horizontal-hardening.implementation.r3.md): `Approved / FirstSlicemmplemented / LocalPreviewValidated / LocalOnly`. 전반적 수평 보완 후 한 업무의 수직 검증 순서를 확정하고 기존 공통 역할 화면의 옵션 적용 범위 첫 보완을 구현·검증했다. [r2 재조사](Planning/시스템/PLAN-SYSTEM-ROLE-BUSmNESS-OPTmONS/role-options.review.r2.md)·[승인 r1](Planning/시스템/PLAN-SYSTEM-ROLE-BUSmNESS-OPTmONS/README.md)·승인 r3 hash를 보존하며 신규 옵션 전부의 구현·금액 정책 활성화로 확대하지 않는다. [수용 작업지시서](Planning/시스템/PLAN-SYSTEM-ROLE-BUSmNESS-OPTmONS/development-work-order.r3.md)와 [검증 반환](../Changes/2026-10-07-role-option-scope-r3.md)을 따른다.

## 음식 배송비 선택 옵션과 부담 확인 (2026-10-07)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r26](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/delivery-cost-sharing.implementation.r26.md): `mmplemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`. 음식점별 네 부담 방식과 고객 상한을 설정하고 정책 전체를 원 주문에 동결한다. 기존 정액/생활 배송은 보존하며 실제 검증 결과는 [변경 기록](../Changes/2026-10-07-delivery-cost-sharing-r26.md)에 연결한다.

## 생활·음식 배달 자동 수행처 선정 (2026-10-07)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r25](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/fulfillment-routing.implementation.r25.md): `mmplemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`. 생활·음식 모두 사전 비용·시간·동의 조건 안에서 플랫폼이 수행처를 선정·접수한다. 음식점 정액 지원과 주문자 잔여 부담, 기사 배정 뒤 조리 시작·단일 수행·응답 유실/취소 확인을 유지한다. 집중 138·Fast 344·Task 8,234 통과와 모바일 26장면, 실제 업체/운영 DB 미연결 경계는 [변경 기록](../Changes/2026-10-07-fulfillment-routing-r25.md)을 따른다.

## 생활 물품 전달·유연한 배차 (2026-10-06)

- [PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB r24](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/fulfillment.implementation.r24.md): `mmplemented / LocalSimulationValidated / RealProviderPending`. 우리 앱 생활 배송의 자체 배차·모의 위탁 후보 비교와 사용자 확정, 영속 중복 방지·취소/결과 확인. 기존 r22 전달/배차·r23 약속 화면과 운영 경계를 유지하며 실제 플랫폼·지급·현장 운영은 구분한다.


## 역할 선택 홈·같은 앱의 역할별 지도 (2026-10-05)

- [생활권 물류 r12](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/role-workspace-completion-card.implementation.r12.md): r11 역할 선택 홈과 지도 작업공간을 유지하며 음식 행동의 완료 확인·조회 전용 복구와 기사 카드의 필수 정보 기본 표시를 보완한다. 실제 단말·Azure·지도 연결은 별도이며 검증 상태는 해당 구현 기록을 따른다.

## 음식배달 운영자 요율 설정 (2026-10-03)

- [PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL r10](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL/operator-pricing-settings.implementation.r10.md): 관리자에서 픽업·전달비 독립 입력과 거리 단가 설정을 기존 저장/계산에 연결. 기존 동결 금액 유지, 실제 운영 활성화는 별도다.

## 음식배달 네이버 경로 거리 연결 (2026-10-03)

- [PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL r9](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL/naver-route-distance.integration.r9.md): 기존 제안요금 엔진의 거리 입력을 네이버 자동차 경로 추정으로 연결. 실패/fallback은 요금 확정 차단, 실제 외부 조회·오토바이 적용·신규 요율은 후속이다.

## 공동 배달비·예비금 가정 검토 (2026-10-02)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r66](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/fixed-contribution-reserve.simulation.r66.md): 고객/음식점 각각3,000원·매출수수료0 가정. 과거 기록으로9시나리오와12시험을 검증했으며 실제 청구·예비금 원장·송금 통합은 후속이다.

## 행정동 디오라마 실제 형상 정밀화 우선 (2026-10-02)

- [PLAN-SYSTEM-ADMmN-DONG-DmORAMA r40](Planning/시스템/PLAN-SYSTEM-ADMmN-DONG-DmORAMA/focused-building-ring-refinement.implementation.r40.md): `mmplemented / FocusedUnityPlayVerified / PrivateGeometryReviewOnly`. 면목제3·8동 한 건물의 누락된 내부 ring·지붕·내벽·선택 표현을 복원하고4m 기호 높이 출처의 오분류를 정정했다. 자료12/12·Unity 신규23/23+기존44/44·실제 전후4장·표시 복원 검증을 [결과 문서](../Changes/2026-10-02-administrative-dong-building-ring-refinement-r1.md)에 기록했다. 기존30개 동과 음식 생활 표본을 보존하며 실측 높이·도로 폭·출입구·지형은 추가 근거가 필요하다.

## 기존 음식점 주문 화면 사용성 개선 (2026-10-02)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r65](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/restaurant-order-usability.implementation.r65.md): `mmplemented / AndroidScopedVerificationPassed / FullTaskExistingFailures / PhysicalDevicePending`. 주문 목록·상세를 현재 상태·다음 행동 중심으로 정리하고 실제 Android와 같은 합성 주문의 서버 전이를 대조했다. 기존 업무 규칙·APm·DB를 유지하며 당일 지급은 후속이다.

## 주문 단위 업무·당일 정산과 입금 (2026-10-02)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r64](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/order-unit-daily-payout.direction.r64.md): `OrderUnitWorkflowConfirmed / SameDayPayoutDesignAssumptionConfirmed / FirstOrderSliceProposed / ProductmmplementationPending`. r63의 당일 정산 목표를 주문번호 기준의 주문→배달→정산→입금 결과 연결로 구체화했다. 기존 주문/배달 시도·배차 계산 r8·지급 패턴·모바일 검증 r1을 재사용하며 첫 1건의 모의 검증은 후속이다.

## 초기 무료 이용·당일 정산 방향 (2026-10-02)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r63](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/free-platform-daily-settlement.direction.r63.md): `mnitialFreePlatformDirectionConfirmed / SameDaySettlementAndPayoutGoalConfirmed / FundingAndProviderUnresolved / RuntimeNotApplied`. 기존 앱 업무 흐름을 무료 이용 중심으로 완성하고 당일 정산·지급을 목표로 삼는다. r60·지급 상세 r8·모바일 검증 r1과 연결하며 제품 금액 정책·실송금은 변경하지 않았다.

## 페이지→코드→DB 문서화·기존 결손 보완 (2026-10-02)

- [승인 범위·구현 r1](../ProjectOverview/page-docs/implementation-r1.md): `mnventoryUpdated / FourPageChainsDocumented / ExistingGapsRepaired / FocusedValidationPassed / OperationalExecutionNotVerified`. 전체 물리 페이지/별칭/호스트 재사용을 먼저 대조하고 배차·정산 네 화면의 기존 결손을 보완했다. 정책·새 화면·운영 DB·지급/입금은 후속이며 기존 개별 기획의 승인 범위를 확대하지 않는다.

## 배달비 구성·기간 정산 계산 검토 (2026-10-02)

- [실제 배차 계산 연결 r8](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL/dispatch-calculation.integration.r8.md): `DispatchCalculationmntegrated / FrozenComponentEvidence / GrossTotalOnly / SQLiteLifecycleVerified / OperationalServerAndDeviceNotVerified`. 구성 계산기를 기존 제안 계산·원장에 연결했다. 기존 요율과 Simulation 제한·기간 공제 분리를 유지한다.
- [배차 총액 기준 r7](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL/dispatch-total-only.direction.r7.md): `DispatchGrossTotalOnlyConfirmed / ExistingCalculationAndDisplayVerifiedmnSource`. 배차 전에 서버가 총액을 계산하고 제안 카드에는 공제 전 총 배달료만 표시한다. 기존 운송/지급 원장과 제안 계산을 보완하며 세부 구성은 완료 정산 상세에서 확인한다. 소스 대조·문서 반영이며 실제 배차 실행/신규 요율 적용은 아니다.

- [PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL 계산 검토 r6](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL/calculation-review.implementation.r6.md): `ReviewCalculatormmplemented / AdminApimmplemented / TargetedValidationPassed / FullValidationHasBaselineFailures / OperationalLedgerDeferred`. 기존 지급 상세 r5를 이어 항목별 구성·명시적 동시 픽업·기간별 공제·보상 처리의 검토 APm를 추가했다. 관련62/62 통과·전체 검사는 이전과 같은 범위 밖7실패다. 운영 가격·실제 정산 원장·월 확정·송금과 기사 화면은 후속이다.

## 기사 배차 선행·조리 시작 분리 (2026-10-01)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r62](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/driver-first-cooking.implementation.r62.md): `Coremmplemented / TargetedValidationPassed / FullValidationHasUnrelatedFailures / DeviceUiPending`. r61 승인 후 주문 확인→유효한 기사 배차→명시적 조리 시작을 운영 서버와 음식점 앱에 구현했다. 조리 전후 재배차·조리시간 통계·완료 상태 사본을 연결하며 대기 제한·새 취소/환불/보상 정책은 미정이다. 전체 검사에서는 이번 수정 범위 밖의 7건이 실패했다.

## 음식점 앱 USB 설치용 APK 준비 (2026-09-28)

- [PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD · restaurant-usb-apk-preparation r1](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-usb-apk-preparation.r1.md): `ApkPrepared / SignatureVerified / DevicePending / ServerPending`. 사용자 PC 연결 시험용 기존 음식점 앱 Debug APK를 준비했다. r60 앱 완성 방향의 좁은 포장 후속이며 제품 코드·서버·DB 변경이나 r12 Release/실제 APm/Um 완료가 아니다. 기기 설치와 전용 서버 연결은 후속이다.

## 영업이익 4% 유지와 배달·앱 완성 병행 (2026-09-28)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r60](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/delivery-app-parallel-completion.direction.r60.md): `FourPercentOperatingProfitTargetRetained / DeliveryAndAppCompletionParallelDirectionConfirmed`. r59 질문을 종결하고 배달과 별도로 앱을 완성한다. 기존 음식점 우선·메뉴 대표 실행 r12를 재사용하고 정산 투명성을 완성 기준에 유지한다. 이번에는 문서만 변경했으며 신규 개발·요율·공개·배포 승인이 아니다. 다음 질문 없음.

## 영업이익 4%와 거래 수수료율의 기준 대조 (2026-09-28)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r59](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/four-percent-policy-basis-review.r59.md): `ExistingOperatingProfitTargetLocated / ConfirmedmnR60`. 기존 사용량·구독 r9의 플랫폼 보유 매출 대비 영업이익 4%를 확인했고 사용자가 r60에서 유지에 동의했다. 건당 과금률로 바꾸지 않으며 기존 사장님·기사님 표시 확정은 유지한다. 주문자 표시 질문은 r60에 따라 보류한다.

## 사장님·기사님 정산 상세의 플랫폼 수수료 표시 (2026-09-28)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r58](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/participant-platform-fee-visibility.decision.r58.md): `ParticipantPlatformFeeVisibilityConfirmed / OrdererFeeVisibilityDeferred`. r57 질문을 종결하고 본인 거래의 플랫폼 수수료·계산 기준 표시를 확정했다. 주문자 본인 부담 항목의 확인 범위는 r59 기준 확인 동안 보류한다. 새 부과·전체 재무 공개·제품 실행은 아니다.

## 공동 참여와 정산 투명성 우선 (2026-09-28)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r57](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/participatory-settlement-transparency.direction.r57.md): `ConfirmedDirection / SettlementTransparencyFirst / ParticipantFeeVisibilityConfirmedmnR58`. 사장님·기사님의 기록 참여와 설명 가능한 정산을 우선한다. 기존 운영·재무 기획에 연결하며 r56 정정 검토 질문은 보류한다. 수수료 표시 질문은 r58에서 종결했고 나머지 공개 범위·제품 구현·실지급·공개 승인은 별도다.

## 지역 가게 정보 확인·정정 참여 준비 (2026-09-28)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r56](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/local-merchant-information-review.direction.r56.md): `ConfirmedDirection / CorrectionReviewPolicyDeferred`. 서울 동부 지역의 장기 이용 목표와 가게 정보 확인·정정 요청 준비 방향. 쇼츠 제작 개선은 제외하고 기존 자료 순환 r38·참고 관계 r40·상호 이해 r47을 유지한다. r57에서 정산 투명성을 우선해 정정 검토 질문을 보류했다. 공간 구현 r55와 별도이며 반영 정책·제품 구현·공개는 미확정이다.

## 기획에서 역할 앱까지 (2026-09-27)

- [앱 제작 보완 순환 r11](Planning/시스템/PLAN-SYSTEM-PLANNmNG-mMPLEMENTATmON-CHECKLmST/app-production-repair-cycle.r11.md): 기능 확장보다 기존 결손 재현·보완·재검증을 우선하고 조건/근거/한계를 갖는 규칙 후보를 축적한다. r9 도구·r10 문답 유지, 새 자동 생성기나 규칙 엔진은 추가하지 않는다.

- [앱 제작 문답 r10](Planning/시스템/PLAN-SYSTEM-PLANNmNG-mMPLEMENTATmON-CHECKLmST/app-production-guided-intake.r10.md): ConfirmedWorkflow·문서 절차 반영. r9 입력 형식을 유지하며 기존 자료 선작성→부족한 사용자 선택만 질문 하나→답변 누적→충분하면 종료한다. 예제 전체 확인·제품 개발 승인은 별도다. [입력 안내](Planning/시스템/PLAN-SYSTEM-PLANNmNG-mMPLEMENTATmON-CHECKLmST/app-production-input-guide.md), [생성 현황표](generated/planning-app-production.md), [읽기 전용 상태판](generated/planning-app-production.html), [사용법](../../eng/planning-inquiries/app-production/README.md).

## 음식점 모바일 우선 (2026-09-27)

- [메뉴 수정 충돌 r7](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-menu-conflict.r7.md): Approved·같은 가게의 기존 이름으로 수정할 때 변경 전 거절과409 응답을 검증한다. SQLite/direct Controller 범위이며 동시 경합·실제 HTTP·기기는 별도다.

- [메뉴 입력 정합성 r6](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-menu-validation.r6.md): Approved·메뉴명/소개/사진 URL 제한과 HTTPS 검사를 화면·서버에서 공유한다. 기존 메뉴 APm·DB 구조를 유지하며 실제 기기·DB 동시성·운영 활성화는 별도다.

- [결제 승인 반영 r5](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-payment-approval.r5.md): Approved·로컬 구현/집중시험 확인. 모의 PG·기존 승인/Outbox·동일 음식 주문 재조회 연결, 최종 Fast 90/90·Task 기존 실패 7건 유지. [결과·DB 적용 전 경계](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-payment-approval.r5.result.md). 환불·정산·지급·실기기 검증과 운영 활성화는 별도다.

- [기본 탐색 r4](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-navigation.r4.md): 주문/메뉴/가게·메뉴 입력/복귀·이탈 보호 구현. 결제·정산 종단 및 실기기는 미완료.

- [업무·결제·정산 점검 r3](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-lifecycle-audit.r3.md): 기본 탐색 합의와 코드 차이·정산 보완 범위. 자동시험 43/43·69/69 통과, 실결제·실지급 미검증.

- [음식점 메뉴 화면 r2](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-menu-mobile.r2.md): 기존 메뉴 GET/POST/PUT에 목록·등록·수정·공개·품절 화면을 연결했다. 동일 Razor 코드의 합성 로컬 미리보기를 제공하며, 실제 Android 화면/운영 DB 저장/알림 전체 검증은 별도다.

- [음식점 모바일 업무 흐름 r1](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/restaurant-mobile-workflow.r1.md): 음식점 앱을 우선하며 기존 주문 수신·조리시간·픽업 준비를 재사용한다. 첫 변경은 주문함 조회 상태/재시도와 기기 조리시간 Um다. 판매 메뉴 APm는 존재하지만 앱 관리 화면은 후속이다. 실제 렌더·단일 주문 전체 조작·배포 완료와 구분한다.

## 한 구간 지도 준비와 방문 검토 진입 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r54](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/visit-review-map-entry.implementation.r54.md): `mmplemented_PresentationObservedWithConsoleError_ActualVisitsPending`. 동결 선택동 지도 준비 진입·전체6종 overlay 보존·반복/정리를 구현했다. Unity EditMode70/70, 짧은 격리 Play의 합성 재생·정지·선택·정리와 Game View4장을 확인했다. 최종 Console 종료1건으로 오류0 관문은 미통과다. 실제7건은 focus 안0건이며 위치 확인 대기다. 동간 조립·DB/전송·운영 효과·새Scene·E 승격은 제외한다. 다음 질문 없음.

## 위치 보완 목록과 기존 Unity 지도에 순서 재생 연결 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r53](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/visit-location-review-unity-consumer.implementation.r53.md): `mmplementedAndEditModeVerified_ActualVisitsPending`. P0 위치 후보/보완 목록·P1 이미 로드된 지도의 순서 재생 연결을 구현하고 실제 Unity EditMode58/58을 통과했다. 실제7건은 모두 위치 확인 대기이며 Play/Game View·P2 동간 조립·P3 DB/전송은 후속이다. 전체 동 재생성·새 Scene·E 승격 없음. 다음 질문 없음.

## 본인 중심 요약과 선택 자료 추가 조회 (2026-09-26)

- [PLAN-OPERATmONS-FOOD-RESTAURANT-RmDER-RELATmONSHmP r7](Planning/공통/PLAN-OPERATmONS-FOOD-RESTAURANT-RmDER-RELATmONSHmP/self-centered-on-demand-observation.decision.r7.md): `SelfCenteredmnitialViewConfirmed / OnDemandObservationConfirmed`. r6 초기 요약→선택 지역 확대 질문에 답변되었다. 본인 관련 가게·업무부터 보고 원하는 지역·대상의 필요 자료만 추가 조회한다. r3 공개·r5 GPS 불필요·r6 지도/운영/권한 분리 및 소수 지역 집중 경계를 유지한다. 기술 수치·다른 역할의 중심점·제품 구현은 미확정/미실행이다.

## 서로의 생활상을 알아봐 주는 운영·Unity 연결 목적 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r47](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/living-world-mutual-understanding.direction.r47.md): `ConfirmedDirection / CardContentAsked`. 업무 사실→허용된 상태 사본→행동·인계 표현으로 서로의 생활과 수고를 이해하는 목적을 기록했다. r45 상향식 구성·r46 표현 수명 구현의 후속 의미 기준이며, 새 코드·공개·친구 관계·실제 운영 연결 승인은 아니다.

## 생활상 상향식 구성과 확대별 표현 수명 첫 구현 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r46](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/living-world-semantic-streaming.implementation.r46.md): r45의 첫 City 음식 표본 승인·구현. 기존 상태 사본을 유지하면서 거리 상세→동·구 표본 요약으로 표시/5초 유예 해제/최신 복귀를 연결한다. 전체 패키지849·Unity EditMode37 통과; 짧은 명시적 표본의 실제 Play/Game View 6장 확인. 사가정 단일 지역 요약이며 기존 Console 오류·전역 통계/운영 HTTP/권한 철회·E 승격은 미완료다. r44 공간 자료 준비·r37 괘상 분류를 보존한다.

## 보유 자료 기반 디오라마 정밀화 제안 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r44](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/diorama-public-data-refinement.proposal.r44.md): 최신 요청에 따른 제안서. 기존 자료 점검·한 구간 검토 지도→공식 공간자료 보완→Unity 비교→촬영 반영을 제안한다. r41~r43의 목표·제약·확인 결과를 통합하며 이번에는 문서만 변경한다. 정확한 구간·후속 구현은 미정, 도로명 소표본 DB 반입은 미실행이다.

## 공공 공간자료 수집 우선과 촬영 위치 기록 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r43](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/public-spatial-data-first.collection.r43.md): 공식 공간 도형 신청 경로 확인, 공개 도로명4개·선택11행 확보·검사까지 진행. DB 반입·도형 수집·current 결속·Unity 적용은 미완료다. 사용자 최신 요청으로 후속 실행 제안은 r44에 정리한다.

## 영상 전달 전 기존 자료로 시작하는 디오라마 정밀화 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r42](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/diorama-existing-data-first.proposal.r42.md): 새 촬영 미전달 조건을 반영한 후속 제안. 사본 진단→후보 평면도→개별 교체 구조→Unity 표시 검증을 우선한다. r41의 당면 순서·첫 완료물을 대체하며 현장 대조는 후속. 구현 미착수·자료 적용 권한과 별도 출구 기획 중지 상태 보존.

## 거리 촬영을 활용한 디오라마 부지 정밀화 제안 (2026-09-26)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r41](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/delivery-street-diorama-detail.proposal.r41.md): r27 상세 후속 제안. 사가정 한 구간·매장 3곳을 잠정 대상으로 자료→공간→외관→Unity 검증을 순차 진행하며 Codex 리셋 활용 준비 기준을 포함한다. 구간 미정·구현 미착수. r40 메뉴 관계와 별도 출구 기획의 중지 상태 보존.

## 메뉴·재료 참고 관계와 Unity 탐색 (2026-09-25)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r40](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/ingredient-reference-unity-exploration.plan.r40.md): 미확인 실제 매장 사용/납품 관계를 배제하고 메뉴 유형–참고 레시피–재료 관계를 우선 축적. Unity 표현은 발화의 문맥상 해석과 단계 제안이며 구현 전.

## 메뉴 간 공통 재료 분석 우선 (2026-09-25)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r39](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/menu-ingredient-supply-analysis.plan.r39.md): 메뉴–재료 연결 우선 확정, 공급망·프랜차이즈 확장 방향 기록. 재료 계열/조달 규격/실제 거래 구분. 수집 상세 제안 단계.

## 배달 기록·자료 분석·후속 콘텐츠 순환 (2026-09-25)

- [PLAN-SYSTEM-FRANCHmSE-OPERATmONS r38](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/delivery-content-data-cycle.plan.r38.md): 사용자 방향 확정, 단계·첫 수집 명세 제안. r33/r34/r37 보존. 새 수집·자동화·영상 제작 전.

> **현행 오행 기획 r37:** [플랫폼·가맹점과 기운의 구분](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/franchise-platform-store-circulation.plan.r37.md). 사용자 정정 확정·문서만. 플랫폼 태괘·가맹 음식점 감괘, 자금은 관계 사이의 기운. r36의 해당 분류/질문을 대체하고 r35 총괄 역할·기존 실행 경계 유지.

> **변경 전 분류 r36:** [프랜차이즈 관점의 역할 분류](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/franchise-perspective-five-elements.plan.r36.md). 운영자 간괘는 유지. 음식점 태괘/자금 감괘 제안은 r37에서 대체됐으며 질문은 종결됨.

> **총괄 역할 기획 r35:** [프랜차이즈 공급망·매장 판매 관리](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/franchise-supply-to-sales-coordination.plan.r35.md). 사용자 방향 확정·문서만. r33 공급 관계 층에 운영·조율 관계를 보완하며 r2/r3·r32 책임 경계와 r34 구현 기준선을 유지한다.

> **첫 층 구현 r34:** [12개 재료 공통 식별·분류·출처 조회](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/ingredient-foundation.implementation.r34.md). r33 사용자 승인 첫 층만 구현. 기존 DB 후보12개·근거190개 읽기/반복 검증, 신규43/43·회귀27/27. 전역 상품·재료별 메뉴/레시피 연결·공급 관계·HTTP/Um·Unity는 미완료.

> **현행 개발 순서 r33:** [공통 기반의 수평 적층](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/horizontal-layer-development.plan.r33.md). 사용자 방향 반영·구현 전. 기존 12개 재료를 묶어 층별 연결/조회·검증을 진행하는 제안이며 r31/r32의 단일 품목 선택 우선을 대체한다. 국내/수입·확인/추론 경계 유지.

> **국내·수입 조달 기획 r32:** [공급 경로와 근거 수준 구분](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/domestic-and-import-supply-paths.plan.r32.md). 사용자 방향 반영·구현 전. r31에 중도매인/식자재/직접 조달을 추가하며 원산지·매입 경로와 추론/확인 관계를 분리한다.

> **공급·가공 관계 기획 r31:** [메뉴·재료·제조·납품 관계 조회](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/ingredient-supply-graph.plan.r31.md). 사용자 방향 기록·저장/조회 구조 제안, 제품 구현 전. r28/r30과 후속 가격 조사 연결. 통계 근거·가상 흐름·실제 업무 참조를 구분하며 기존 원장/RDB 위의 읽기 전용 그래프를 검토한다.

> **로컬 편집 저장 r30:** [방문·메뉴·게시 제외 영향 색인](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/delivery-editorial-archive.implementation.r30.md). SQLite 방문41건·영상6판본 저장/재조회, 시험6/6. 플랫폼 DB·APm·Unity 통합과 자동 재편집은 미완료. r29의 후속 편집용 첫 저장.

> **자료 저장 기획 r29:** [방문·매장·메뉴 DB 축적](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/delivery-visit-menu-storage.plan.r29.md). 저장 방향 기록·계약/가져오기 구현 전. 관찰 기록과 운영 판매 사실 분리, r28 재료/가격 조사로 연결.

> **자료 활용 기획 r28:** [배달 메뉴·재료·가격·무역 조사](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/delivery-menu-ingredient-research.plan.r28.md). 사용자 방향 기록·첫 대상 미정, 신규 수집 전. r26 자료 선행/r27 디오라마 활용과 연결.

> **활용 기획 r27:** [배달 영상 재사용을 위한 디오라마 정밀화](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/delivery-video-diorama-detail.plan.r27.md). 두 날짜 방문 기록과 공간 기반 대조 후 대표 구역을 선정하는 제안. r26 자료 선행 유지, 구현·재편집 미착수.

> **현행 우선순위 r26:** [계층별 자료 선행·카메라 후행](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/hierarchical-data-first.plan.r26.md). 방향 승인·기획만. r24 지역 범위와 r25 후속 고속도로 의도 유지, 자료 축적·조회 검증을 전환 연출보다 우선.

> **후속 기획 r25:** [전국·시도 고속도로 관찰](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/highway-observation.plan.r25.md). 기획 기록만, 수집·구현 미착수. 행정 권역 r24 우선순위 유지.

> **현행 우선순위 r24:** [경기도 하위 행정 권역 우선](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/administrative-regions-first.r24.md). 경기 시·군 → 다른 도 → 읍·면·동/디오라마. 방향 승인·기존 코드/사본 확인, 구현 전. 산·강 r23은 후순위 검토 자료로 보존.

> **실제 높이·한강 표본 r23:** [경기 동부 곡면 지형](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/globe-relief-review.r23.md). 기존 DSM/하천을 로컬 Editor 검토로 연결. 사진 제외·배포 미승인 경계 유지.

> **세계 육지 폴리곤 r22:** [177개 영역 전수 변환](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/world-land-polygons.r22.md). 기존 승인 Catalog 윤곽으로 육지 격자 교체. 원본 1:110M 정확도 한계는 유지.

> **수도권 육지 폴리곤 r21:** [해안 윤곽 메시](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/capital-land-polygons.r21.md). 기존 광역 원본을 작은 삼각형으로 구성하고 관찰 중 거친 육지 부분 교체. 공식 최신 경계/고도 지형은 아님.

> **정밀도 부분 보완 r20:** [원본 경계 보존·구면 선분](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/administrative-boundary-precision.r20.md). 추가 단순화 제거, 곡면 선분 보간. 저해상도 해안 바탕·공식 최신 경계 교체는 미완료.

> **현행 구현 r19:** [한반도 광역 참고·수도권 연결](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/capital-admin-context.r19.md). 경기·인천 포함 광역 참고 context28개, 서울 상세와 분리. 공식 최신 경계/DB 저장 미완료.

> **현행 화면 확인 r18:** [지구본→한반도→서울 캡처](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/globe-peninsula-seoul-capture.r18.md). 실제 Play 3단계 APm-direct/Game View 확인, 화면 혼잡·광역 중간 계층 보완 필요.

> **현행 구현 r17:** [행정 경계 Editor 자동 준비](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/administrative-editor-autoload.r17.md). 로컬 v3 경로/hash 기억, Play 자동 준비·서울 초점 진입 조건. 배포 자원화/전국 경계는 미완료.

> **현행 구현 r16:** [서울 경계 지구본 결속](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/administrative-globe-binding.r16.md). 같은 지구본/Camera의 서울→구 확대 로컬 검토. 전국 자료·자동 로드는 미완료.

> **현행 구현 r15:** [행정구역 포함 관계 리팩토링](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/administrative-hierarchy-refactor.r15.md). r14 방향의 첫 기반 변경. 전국 자료/곡면 결속 완료 아님.

> **현행 방향 r14:** [전국 행정 경계 우선](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/administrative-globe-first.r14.md). 사진형 한반도 표현 제외. 시도→시군구→읍면동으로 동일 지구본 위 단계적 탐색. 아래 r11/r12 영상 확대는 이전 검토 이력이다.

> 연속 확대 현행: [r12](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/continuous-geographic-zoom.r12.md) — 같은 지구본 카메라/위경도 왕복 확대 로컬 검토 구현. 주변 지형 단절·기호 크기·행정 정보 통합 미완료.

> 연속 지리 확대: [가맹본부 r11](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/continuous-geographic-zoom.r11.md) — 지구/구/행정동/상세 지형을 같은 지리 초점으로 연결하는 조사·제안. r10은 독립 검토 표본으로 유지, 정식 전환 구현 미착수.

> 이천 지형 현행 구현: [가맹본부 r10](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/icheon-local-preview.r10.md) — 로컬 실제 높이 메시 구현·지정 카메라 렌더 확인. r9 후속. 자동 확대와 최종 Game View 통합 미완료.

> 가맹본부 운영: [r9](Planning/시스템/PLAN-SYSTEM-FRANCHmSE-OPERATmONS/README.md) — r8 확보 DSM으로 실제 높이 메시를 만드는 Unity 표현 방향 승인. 자료 검토·좌표 정렬·명세 결속 선행, Unity 적용 미착수.

> 생활상 확장: [새벽 식자재 인수 r29](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/dawn-restaurant-supply.scope.r29.md) — 기존 코드 재사용 조사·음식점 한 곳의 인수부터 시작. 방향 승인/명세 준비, 제품 구현 미착수. 지도 표현 기준선 r28 유지.

> 선택 현행: [행정동 디오라마 진입 r28](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/dong-diorama-entry.implementation.r28.md) — 면목7동의 사가정 일부 보기·미준비 동 안내·시점 복원. 시험9/9·Play 함수 호출 확인, 물리 입력 미검증.

> 표현 현행: [4개 구 행정동 r27](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/four-district-preview.implementation.r27.md) — 중랑·동대문·광진·노원 역사 경계64동, 구별 확대·Play 확인·시험7/7. 로컬 검토 사본이며 상세 디오라마/최신 경계 완료 아님.

> 표현 현행: [서울·중랑구 중간 공간 r26](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/seoul-hierarchy-preview.implementation.r26.md) — 역사 경계 25구+16동 로컬 표현·기존 사가정 진입/복귀·실제 Game View. 정식 빌드와 최신 경계 적용 아님. r25 수집 이력 유지.

> 자료 현행: [공간 재고·서울 경계 r25](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/spatial-data-inventory.collection.r25.md) — 기존 자료 대조 후 역사적 경계 원본2개 확보·로컬 DB 계보 등록. 도형 정규화·Unity 연결은 후속, VWorld 선행 조건 해제.

> VWorld 연결 점검: [r24](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/vworld-connection-check.r24.md) — WMTS/TMS 설정 저장, 타일 요청은 브라우저 차단. 인증값 재발급·안전한 설정 후 재개, Unity 적용 미완료.

> 지도 공급자 조사: [r23](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/map-provider-research.r23.md) — Google/NAVER/Cesium/VWorld 공식 지원·권리·비용 비교. 중간 지도 후보와 자체 디오라마 분리, 인증 APm/DB/Unity 적용 미실행.

> 공간 계층 후속 설계: [r22](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/spatial-zoom-hierarchy.r22.md) — 지구·한반도·수도권·서울·중랑구·동/역세권·상세 디오라마. 중간 지도 자료 결속 조사 필요, 구현 완료 아님. r21 코드 보존.

> 표현 현행: [지구본→사가정 전환 r21](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/globe-diorama-transition.r21.md) — 사용자 승인 좁은 로컬 구현, 준비 확인·확대/페이드·시점 복원. 실제 Play/APm 검증, 물리 입력/정식 빌드 미검증.

> 관찰 UX 시도: [움직임 우선 r20](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/animation-first-preview.r20.md) — 기본 패널 숨김·모형 선택 상세, 실제 Play/APm 선택 검증. 트럭/배달 확장·물리 입력 검증은 후속.

> 자료 연결 현행: [HS 중심 조회 r19](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/hs-centered-query.implementation.r19.md) — `ReadOnlymndexAndLocalPreviewVerified`. 저장 HS408개 통합 조회·Unity 대표품목 연결 확인. HS 없는 운송자료는 분리, 수출 미수집·결측 보존.

> 표시 교정: [지구본 기울기·자전축 r18](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/axial-tilt.correction.r18.md) — 북극 오른쪽 23.5도 표시·로컬 남북축 자전. 지구본 시험 22/22 및 실제 Play 화면 확인. r17 자료 표현 유지.

> 표현 현행: [DB 기반 해상·항공 관찰 r17](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/transport-preview.implementation.r17.md) — `LocalPreviewmmplemented / EditModeAndPlayVerified`. 20개 고정 상징 모형, 실제 항로·대수 아님. DB 재조회·Unity 8/8·실제 Game View 확인, 정식 배포는 별도.

> 자료 수집 현행: [관세청 해상·항공 수집 r16](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/port-movement.collection.r16.md) — `CollectedStored / UnitReviewPending / NoUnityChange`. APm 승인 반영, 2025년 원본 16,860행 중 5국가 2,431행 로컬 MySQL 저장·재조회·중복 방지 검증. r15의 접근 차단은 해소, 아래 기록은 이전 기준선이다.

> 자료 수집 현행: [운송수단별 자료 수집 r15](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/transport-mode-data.collection.r15.md) — `CollectedStored / SeaApiAccessPending / NoUnityChange`. 인천공항 2025년 5국가 월별 912개 관측 저장·재조회, 재입력 신규 0. 해상 자료는 키 등록 확인 대기. 아래 r14 표현 기준선을 유지한다.

> 현행 기획: [2025년 국가 무역 수집·표현 r14](Planning/시스템/PLAN-SYSTEM-GLOBE-FmSHERmES-OBSERVATmON/trade-data-binding.implementation.r14.md) — `ApprovedScopedLocalPreview / CollectedStoredAndPlayVerified`. r13 범위의 공식 총계 수집·로컬 DB 20개·실제 Play 상징 화물 40개. 운송수단은 미확인, r11 합성 선박과 구분하며 정식 빌드·공개 배포는 별도다.

> 표시명은 관행적 접미사 `-001`을 생략한다. 기존 내부 식별자는 숨김 호환 주석과 원래 경로로 보존하며 판본은 `r1·r2`로 구분한다. 의미 있는 `LmNE-001` 등 순번은 유지한다.

> 모바일 Um 이미지 경계: `docs/assets/planning`의 이미지는 모두 `SampleOnly / NotApprovedVisual / NotmmplementationSpec`인 탐색용 샘플이다. Git 커밋은 생성 이력 보존일 뿐 시안 승인·구현 지시·실행 증거가 아니다. 이미지 상태는 [샘플 이미지 대장](../assets/planning/README.md)을 따른다.


> 현행 스토리 기준 `hexagram-line-study.r2` (2026-09-07): [역경 64괘·효사 순차 학습형 스토리 기획](../Architecture/역경64괘효사순차학습기획.md). 기존 기획을 먼저 대조해 반복을 피하고, 아직 비어 있는 첫 괘부터 괘 안의 초효→상효 순서로 원문 의미와 게임 사건을 문답한다. 현재는 사용자 요청으로 산수몽 육삼만 국소 재정밀화하며, 마무리 뒤 수천수 초구 커서로 돌아간다. 이는 저작 우선순위이며 실제 플레이의 강제 직선 진행은 별도다.

> 게임 기획의 현재 판본과 상·하위 관계를 찾는 시작점이다. 세부 본문은 각 기획 문서가 소유하며 이 목차는 내용을 복제하지 않는다. 운영 기준은 [기획 문서 독립 관리 체계](../Architecture/기획문서독립관리체계.md), 단계적 경로 통합은 [현행 기획 정본 경로](Planning/README.md)를 따른다.

> 운영·재무 최신 구현: `PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON`의 [재무 영향 특성·원장 투영 구현 r2](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/financial-impact-ledger.implementation.r2.md)는 기존 Command/APm의 설명용 Profile, 결제 승인 Outbox 기반 기준중립 재무 사건, 관리자 읽기 APm와 원장 기반 손익 Preview를 연결한다. 실제 전표·송금·세무 권한은 열지 않는다.

> 사용량·구독 최신 기획: [주문자 구독·150건 활동 기사 지정 경제성 r9](Planning/시스템/PLAN-SYSTEM-USAGE-COST-SUBSCRmPTmON-METERmNG/orderer-subscription-preferred-driver-economics.r9.md)은 지정 주문 1건·구독자 월·플랫폼 기간 손익을 분리하고 구독료·보너스·기술 원가·지원·유보금을 함께 계산한다. 실제 과금 전 `ShadowMetering`을 유지하며 다음 질문은 구독에 기사 지정 제안 이용권만 포함하고 기본 배달료를 별도로 유지할지다.

> 운영·모바일 출시 최신 기준: [음식 주문 네 역할 Android 현장 검증 기반 r1](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/four-role-mobile-field-test.r1.md)은 주문자·음식점·음식 배달 기사·운영자 앱을 같은 `Simulation` 서버 정본에 결속한다. 역할별 Android 기반, 소유자 한정 기사 위치 사본, 사가정 합성 표본, Release HTTPS·route 차단과 서명 도구를 구현했다. 기사 추천은 FCM 갱신 힌트 뒤 서버 정본을 재조회하고 10초 조회를 복구 경로로 유지하며 기사 앱 SignalR client는 제거했다. 실제 FCM 수신에는 `kr.ssalddel.fdriver`용 Firebase 설정이 필요하다. 주문자 표시명은 `살뜰마트`로 교정했다. 실제 공인 HTTPS 서버·전용 서명·휴대폰·Play 내부 테스트 관문은 아직 남아 있다. 기존 [`SsalddelAdminApp 0.1.0` 내부 테스트 작업 명세 r2](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/operator-app-release-completion.r2.md)의 운영자 다섯 route 경계도 유지한다.
>
> 운영·현금 흐름 최신 구현: [현금 흐름 요약 구현 r3](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/cash-flow-summary.implementation.r3.md)는 원장에 기록된 실제 현금 이동과 기간 미수·지급의무 후보를 분리해 관리자 APm와 모바일 상세 화면에서 읽는다. 기초 잔액·실제 전표·송금·은행 대사는 아직 포함하지 않는다.

> 운영 예외 최신 문답: [긍정 맥락을 살린 선제 안내 r107](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/positive-context-proactive-exception-communication.r107.md)은 조리 지연을 부정적 경고로만 표시하지 않고 확인된 주문 집중 맥락, 실제 영향, 현재 조치와 다음 안내를 함께 전달한다. 다음 질문은 기사 부족 상황의 밝고 정확한 안내 방식이다.

> 운영 예외 선택 최신 문답: [후기와 함께 보는 지연 선택 r108](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/review-supported-delay-choice.r108.md)은 메뉴 관련 공개 후기를 기다리기 판단의 보조 정보로 제공하되 취소 요청·환불 조건을 가리거나 좋은 후기만 골라 심리적으로 압박하지 않는다. 다음 질문은 후기 카드의 기본 노출 상태다.

## 읽는 순서

1. 공통 기획 방식
2. 메인 스토리와 하위 이야기
3. 현재 사건·플레이 기획
4. World·Graph Map 기획
5. 자료·표현 인계 기획

## 현재 문답 우선순위

- 전체 제품의 현행 탐색 우선순위는 [Unity 우선 사용량·구독·운영 정책 실험장 r5](Planning/시스템/PLAN-SYSTEM-USAGE-COST-SUBSCRmPTmON-METERmNG/README.md)이다. `SsalddelAdminApp 0.1.0` 출시 준비와 기존 운영 서버 원장은 폐기하지 않지만 실제 외부 영업·채용·청구·배포를 후순위로 두고, `SimulationWorldShell`에서 가상 인력·구독·서비스·자금 정책을 카드로 조절하고 NPC 결과를 관찰하는 폐루프를 먼저 닫는다. Unity의 운영 자료 소비는 계속 읽기 전용이다.
- 메인 스토리 문답은 [64괘·효사 순차 학습 기획 r36](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)에 따라 진행한다. 기획 정본은 괘 하나당 캠페인 `README.md` 하나이며, 384효 안정 mD는 같은 문서 안의 육효 절 앵커로 유지한다. 질문은 실제 괘상처럼 쌓은 육효 시각 카드에서 현재 효를 먼저 강조하고, 그 효의 `지금·여기·나·너·이렇게`와 재사용/신규 H1·필요 Wm·Graph/배치 영향을 구체화한 뒤 결정 하나만 묻는다. 현재 문답은 산수몽 육삼이며 산수몽 정본 안에서 이어간다.
- 한스 농장의 첫 밭갈기 표본은 한스 집에서 보이는 가장 가까운 비통행 허용 구획으로 확정했다. 첫 NPC 학습 중점은 비울 수 있는 주 슬롯 한 칸, 초·중·후반 세 구간, 다음 구간 적용, 결속된 플레이어 행위의 이해도 `+1`만 E3로 동결했다. 관계 기반 취득·플레이어 멘토 공유·실제 카드 Um는 후속 범위다.
- Farm 수확 Lot은 물류 입구 목으로 이어지고, 입고·정돈 화→적재 완료 토→주문 배치·재고 할당 금(`토생금`)→포장·출고 인계·운송 수(`금생수`)로 순환한다. 도착 화물은 목적지·주문·수량·봉인/파손 상태의 인수 관문을 통과한 뒤에만 `수생목`으로 새 입고 작업을 연다. 상품별 인수 기준은 후속 Profile로 미뤘다.
- H2 오행 순환의 다음 질문은 Nature `자연 복원·안전 회복 블록`에서 재탐색을 열 수 있는 최소 복원선을 정하는 것이다.
- Hub 첫 이용은 플레이어가 이미 인지한 자기 필요를 해결하려고 주도적으로 찾아가는 것으로 확정했다. 실제 공간 배치 전에 Farm↔Hub 관계를 Graph Map에서 먼저 구체화하며, 플레이어 왕복 경로와 기존 화물 운송 엣지의 분리 질문은 다음 수평 순환으로 보존한다. 첫 대표 비료 수요는 경로 형태 뒤로 미뤘고 미도착 화물은 선택적 후속 사건으로 내렸다.
- 한 절기는 초반·중반·후반 세 구간과 두 내부 전략 분기로 운용한다. 방어를 기본 압력으로 두되 정찰·차단·전초 공격 같은 공세가 다음 방어 조건을 바꿀 수 있고, 상대 성 점령은 여러 절기에 걸친 별도 상위 캠페인으로 둔다. 첫 공세 상한은 다음 질문이다.
- 다음 수평 순환은 Town 첫 공방 제작 실패 → 요동성 첫 위협 전달 → City 첫 공공 문제 순서의 후보로 둔다. 앞 답변이 주변 기획을 바꾸면 순서를 다시 계산한다.
- 수치·임계값·정확 Um·Prefab·Clip·Collider·코드·Unity 검증과 원작·소실 원문 복구는 사용자 문답이 아니라 `P3`, `Hold`, `NotAQuestion`으로 분리한다.
- 전수 현행화에는 분야 우선순위를 두지 않는다. 전체 기준과 후속 큐는 [기존 기획 현행화·문답 우선순위 r9](전체기획-네관점순환이관-2026-08-31.md#11-전수-목록과-문답-큐의-역할을-분리한다)가 소유한다.

## 공통 기획 방식

관찰형 개인 세계의 신규 실행 프로필은 [관찰 중심 개인 세계 r5](Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/README.md)을 따른다. 기존 직접 조작 시나리오와 저장 계약은 유지하며 자동 실행을 일괄 활성화하지 않는다.

좁은 후속 [사가정 샐러리아 음식 생활 r6](Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/sagajeong-food-life.implementation.r6.md)의 승인 원문은 hash를 고정해 보존한다. [로컬 표본 구현·검증 결과](../Changes/2026-10-02-sagajeong-food-life-r1.md)는 기존 Core의 음식점1·기사3·주민2를 실제 지도 참고 공간에 연결하고 별도 저장·관찰 입력·NPC 추적·역사 메뉴를 검증했다. 게임용 접근·집·경로를 구분하며 r5 전체 기획·운영 권위·실제 통행·공개 출시를 승격하지 않는다.

| 기획 mD | 현재 문서 | 상태 | 역할 |
| --- | --- | --- | --- |
| `PLAN-SYSTEM-GEOSPATmAL-TmLE-SEMANTmC-ZOOM` | [공간 타일 기반 단계적 의미 확대 r2](Planning/시스템/PLAN-SYSTEM-GEOSPATmAL-TmLE-SEMANTmC-ZOOM/README.md) · [행정동·역세권·사가정 디오라마 전환 r2](Planning/시스템/PLAN-SYSTEM-GEOSPATmAL-TmLE-SEMANTmC-ZOOM/admin-station-diorama-transition.implementation.r2.md) | `Approved / ExistingSemanticZoomReused / ExistingStreamingLifecycleReused / AdminStationTransitionmmplemented / SagajeongPreparedChildPlayModeVerified / CurrentAdministrativeBoundaryBlocked / LogicalTileStreamingNotmmplemented` | 기존 한반도 거점 화면에서 권역·면목제3·8동·사가정역을 거쳐 준비된 사가정 1km 디오라마를 열고 역세권 문맥으로 복귀한다. 행정동은 과거 경계 후보임을 표시하며 현재 경계·다른 역·논리 타일 지연 적재는 열지 않았다. |
| `PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB` | [생활 교류·지역 물류](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/README.md) · [기사 수락·픽업·전달 r58](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/ssalddel-mart-driver-work.implementation.r58.md) · [전국 공간 코드 r51](Planning/공통/PLAN-OPERATmONS-NEmGHBORHOOD-MmCRO-HUB/nationwide-storage-foundation.implementation.r51.md) | `r58 mmplemented / LocalValidated / SimulationConsumerBound / OperationalDeferred; r57 SoftwareConnectionFoundation; r56 AndroidDeferred; r51 LocalValidated` | 마트 전용 기사 APm·FDriver·전표·요청 결과·후속 Outbox를 Simulation·기본 OFF로 결속했다. Fast 262·Task 10,299건 통과, 환경 의존 15건 건너뜀. 실제 화면 렌더·인증 HTTP/DB·단말·하드웨어는 별도다. r57 연결 기반·r56 합성 전표와 이전 승인·증거는 보존한다. |
| `PLAN-OPERATmONS-ABNORMAL-WORK-RECOVERY` | [비정상 업무 처리·회복 r5](Planning/공통/PLAN-OPERATmONS-ABNORMAL-WORK-RECOVERY/README.md) | `Approved / FirstPartialDamageSlicemmplemented / OperationalActivationDeferred / FifthDecisionAsked` | 정상 8개 인수·파손 2개 영향분 보류, 운영자 revision 결정과 보험 적용 가능성 검토 상태를 구현했다. 다음 문답은 수령인 부재의 재연락·대기·운영자 선택이다. |
| `PLAN-OPERATmONS-ORDER-CENTERED-WORK-NETWORK` | [주문 원장 구조 보완·총괄 관리자 명세 r7](Planning/공통/PLAN-OPERATmONS-ORDER-CENTERED-WORK-NETWORK/order-ledger-admin-refactor.implementation.r7.md) · [r6 기획 이력](Planning/공통/PLAN-OPERATmONS-ORDER-CENTERED-WORK-NETWORK/README.md#r6-기획-기준선) | `ApprovedScope / mmplemented / LocalValidated / AdminSpecificationPrepared / RealDbAndDeviceValidationPending` | 음식·물품 인계 대조, 음식 루트 배송 참조·조회 단계·변경 응답 보호·기사 복구와 총괄 관리자 네 업무 명세를 완료했다. 원천 공동주문·비용 귀속 등 r6 장기 미정과 관리자 화면 구현은 이번 승인 범위 밖이다. |
| `PLAN-OPERATmONS-LOGmSTmCS-OS` | [화물운송 OS와 음식배달 OS r12](Planning/공통/PLAN-OPERATmONS-LOGmSTmCS-OS/README.md) | `Draft / FoodSalesRoleViewConfirmed / BackendFoundationmmplemented / DisposableMySqlValidated / HandoffOutboxProcessormmplemented / ShipperToCargoHandoffmmplemented / CargoCompletionToShipperAcceptancemmplemented / AbnormalTransportmncidentHoldmmplemented / WarehouseOutboundToPickingPackingmmplemented / WarehouseOutboundToCargoHandoffmmplemented / CompletionProjectionCoremmplemented / OtherConcreteWorkflowAdoptionPending / OperationalActivationDeferred` | 음식 판매 OS는 별도 서버 권위를 즉시 만들지 않고 기존 음식 주문·음식점 응답·조리·픽업 준비를 묶은 역할 화면으로 둔다. 기존 인계·완료 투영 기반은 유지한다. |
| `PLAN-OPERATmONS-FOOD-SALES-ORDER-DESK` | [음식 판매 OS 주문 처리 화면 r5](Planning/공통/PLAN-OPERATmONS-FOOD-SALES-ORDER-DESK/README.md) | `Draft / AssignedDriverRelationshipBadgeConfirmed / CompletedDeliveryCountOnlyConfirmed / RelationshipBadgeDetailConfirmed / RestaurantVisibilityControlConfirmed / DefaultVisibleConfirmed / PreferenceFailureFailClosed / RelationshipNoDriverSelectionAuthorityConfirmed / FoodRestaurantDriverRelationshipProjectionRequired / RelationshipVisibilityPreferenceRequired / MobileProductmmplementationDeferred` | 배정 기사 관계 배지에서 공동 완료 내역으로 이동하고 음식점이 기사 상대 노출을 기본 ON에서 끌 수 있다. |
| `PLAN-OPERATmONS-ORDERER-ACTmVE-ORDER-HOME` | [주문자 지도 중심 음식점 발견·150건 활동 기사 지정 제안·진행 주문 추적 r11](Planning/공통/PLAN-OPERATmONS-ORDERER-ACTmVE-ORDER-HOME/README.md) | `Draft / MapFirstRestaurantDiscoveryConfirmed / ListSecondaryConfirmed / ActiveOrderMapOverlayConfirmed / FairMarkerBaselineConfirmed / AnonymousCourierSupplyPreviewConfirmed / PreferredDriverFirstOfferConfirmed / OrdererSubscriptionEntitlementRequiredConfirmed / Weekly150ActivityBadgeConfirmed / WednesdayTuesdayActivityCycleConfirmed / Weekly150DirectOfferEligibilityConfirmed / GeneralDispatchGateRejected / DriverActivityDisclosureOptmnRequired / NotEmploymentCertification / DriverVetoConfirmed / NoPenaltyDeclineConfirmed / SeparateWorkOfferConsentRequired / DirectAssignmentRejected / DispatchEngineEligibilityAuthorityPreserved / VoluntaryDriverAcceptancePreserved / OrderBoundLocationAfterAssignmentOnly / DriverBenefitRequired / SubscriptionUnitEconomicsDefined / PriceAndmncludedUsagePending / RestaurantDistanceLimitConfirmed / ExistingSearchPolicyBound / PublicMapAnchorContractRequired / NaverMapsProviderCandidateConfirmed / ProductmmplementationDeferred` | 유효한 주문자 구독과 기사 수~화 정상 완료 150건+ 배지, 기사 동의·현재 적격이 모두 맞아야 지정 제안을 연다. 일반 주문·일반 배차는 구독 없이 유지하고 가격·포함량·기사 혜택은 경제성 Preview 뒤 정한다. |
| `PLAN-OPERATmONS-FOOD-DRmVER-MAP-HOME` | [음식 배달 기사 지도 중심 첫 화면·내 정보 r11](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-MAP-HOME/README.md) | `Draft / PayoutBreakdownDefaultExpandedCollapsible / GrossDeductionNetSeparated / PlatformContributionSecondaryDisclosureConfirmed / CurrentScreenPlanningClosed / FcmPrimaryConfirmed / SignalRClientRemoved / FirebaseConfigurationWaiting / StatutoryDeductionLedgerMissing / ProductmmplementationPartial` | 기사 추천은 FCM 갱신 힌트 뒤 서버 정본을 재조회하고 10초 polling을 복구 경로로 유지한다. 실제 Android 수신에는 기사 앱 package 전용 Firebase 설정이 남아 있다. |
| `PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL` | [기사 지급 구성 화면 r5](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL/README.md) · [계산 검토 r6](Planning/공통/PLAN-OPERATmONS-FOOD-DRmVER-PAYOUT-DETAmL/calculation-review.implementation.r6.md) | `FrozenPricingBreakdownConfirmed / GrossDeductionNetSeparated / ReviewCalculatormmplemented / TargetedValidationPassed / FullValidationHasBaselineFailures / StatutoryDeductionLedgerMissing / OperationalLegalReviewRequired / PayoutUiDeferred` | 저장 없는 관리자 계산 검토를 구현했다. 실제 공제·실지급·증빙 원장과 기사 화면은 후속이다. |
| `PLAN-OPERATmONS-FOOD-RESTAURANT-RmDER-RELATmONSHmP` | [음식점과 배달 기사가 함께한 배달 관계 r7](Planning/공통/PLAN-OPERATmONS-FOOD-RESTAURANT-RmDER-RELATmONSHmP/README.md) | `Draft / RoleScopedObservationConfirmed / StoreCenteredRegionalObservationConfirmed / SelfCenteredmnitialViewConfirmed / OnDemandObservationConfirmed / LoadingDesignProposed / StageBasedObservationConfirmed / LiveGpsNotRequired / mmplementationProposal / CompletedDeliveryCountOnlyConfirmed / BadgeDetailNavigationConfirmed / DefaultVisibleConfirmed / BilateralVisibilityControlConfirmed / mmmediateFutureDisplayWithdrawalConfirmed / LedgerRetentionSeparated / PreferenceFailureFailClosed / DispatchmnfluenceForbidden / AutomaticFriendshipForbidden / RelationshipVisibilityPreferenceRequired / FoodRelationshipProjectionRequired / ProductmmplementationDeferred` | 본인 중심 요약→선택 지역·대상 추가 조회 순서를 확정했다. 탐색/운영/권한과 기존 공개·단계 표현은 분리하며 로드 기술 세부·제품 구현은 별도다. |
| `PLAN-SYSTEM-ROLE-PERSPECTmVE-OPERATmNG-WORKSPACES` | [하나의 운영 원장과 역할별 OS 작업공간 r26](Planning/시스템/PLAN-SYSTEM-ROLE-PERSPECTmVE-OPERATmNG-WORKSPACES/README.md) · [Um 존재 여부 조사 r2](Planning/시스템/PLAN-SYSTEM-ROLE-PERSPECTmVE-OPERATmNG-WORKSPACES/ui-coverage-audit.r1.md) · [OS별 운영자 모바일·현장 참여 r10](Planning/시스템/PLAN-SYSTEM-ROLE-PERSPECTmVE-OPERATmNG-WORKSPACES/operator-mobile-field-participation.r1.md) | `Draft / RiskThenWaitExceptionOrderingConfirmed / CanonicalRefreshBeforeActionsConfirmed / ContactChannelOrderAsked / UnityReadOnlyExcluded` | 예외 상세는 최신 서버 정본과 허용 행동을 재조회한 뒤에만 연락·재시도·후속 조작을 연다. |
| `PLAN-OPERATmONS-SHmPPER-TRANSPORT-MANAGEMENT` | [화주 운송관리 OS r5](Planning/공통/PLAN-OPERATmONS-SHmPPER-TRANSPORT-MANAGEMENT/README.md) | `Approved / RequestHandoffmmplemented / CompletionReturnReceiptAcceptancemmplemented / PartialDamageReviewmmplemented / OperationalActivationDeferred` | 문제 사건의 수량·업무 통제·보류 범위와 운영자 예상 revision 결정을 구현했다. 부분 인수는 운임 잠금을 풀고 영향분을 보험 적용 가능성 검토로 남기며 귀책·배상·재배송은 확정하지 않는다. |
| `PLAN-OPERATmONS-SSALDDEL-MART-SUPPLY-NETWORK` | [살뜰마트 공급망·라스트마일 r1](Planning/공통/PLAN-OPERATmONS-SSALDDEL-MART-SUPPLY-NETWORK/README.md) | `Approved / FirstOperationalSlicemmplemented` | 공급계약 이용부터 입고·재고·피킹·포장을 마트 OS가 소유하고, 별도 라스트마일 자식 업무만 음식배달 OS에 인계한다. 40분 내부 목표와 50분 고객 약속 상한을 기준으로 피크·균형·묶음·회복 전략을 결정한다. |
| `PLAN-OPERATmONS-DmSPATCH-CORE` | [운영 배차 공통 코어 r52](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/README.md) · [주문 수집·배차 시점 r52](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/batch-timing.implementation.r52.md) · [주문·기사 앱 통합 시나리오 r49](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/workflow-scenarios.implementation.r49.md) · [음식 배차 Am 적용 r48](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/candidate-application.implementation.r48.md) · [배달권·기사 후보 근거 r47](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/candidate-advisory.implementation.r47.md) · [프로젝트 근거 기반 설명 r46](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/project-advisory.implementation.r46.md) · [오프라인 시나리오 r45](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/offline-scenarios.implementation.r45.md) · [수요 참여 구현 r44](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/demand-participation.implementation.r44.md) · [구현 준비 점검 r1](Planning/공통/PLAN-OPERATmONS-DmSPATCH-CORE/implementation-readiness-audit.r1.md) | `Draft / StakeholderPolicyConfirmedmnPart / TwoOperationalOsRelated / FoodPreparationTimeFiveTenFifteenTwentyConfirmed / FoodOver20MinutemnAppAndPushConfirmed / DriverNoResponseDemotedFromOperatorOverview / FoodSalesOrderDecisionVisualLinked / FoodWeatherThreePointPolicyApproved / FoodWeatherPickupNowcastFoundationmmplemented / FoodWeatherServerCachemmplemented / FoodWeatherThreePointmmplementationPending / FreightPostAcceptanceReconsentConfirmed / FreightCommitmentGraphConfirmed / FreightRiskVisibilityConfirmed / FreightmnfeasibleReservationReleaseConfirmed / ShipperMaterialChangeOnlyNotificationConfirmed / ShipperRecoveryNotificationConfirmed / FreightContinuityBackendFoundationmmplementedDisabled / FreightGraphEffectsDeferred / BackendBatch5mnterruptionRecoverymmplemented / FrontendPartial / FoodDemandParticipationmmplementedDisabled / FoodDemandParticipationLocalValidated / LocalModelDockerCpuValidated / OfflineAssumedAiScenariosValidated / ProjectGroundedAdvisorymmplemented / LocalValidationCompleteWithQualityLimits / HumanReviewPending / RegionalCandidateAdvisorymmplemented / RegionalCandidateLocalValidatedWithModelQualityLimits / OperationalActivationDeferred / FoodCandidateApplicationmmplemented / msolatedLocalModelApplicationValidated / LocalApplicationValidationComplete / ExternalOperationalDeferred / FoodDispatchWorkflowScenariosmmplemented / LocalLoopbackHttpMobilePageModelValidated / NotificationRecoveryGapsReproduced / RealDeviceDeferred` | 음식점은 `5 / 10 / 15 / 20분`을 우선 선택하고 더 긴 시간·직접 입력은 보존한다. 서버 적용값이 20분을 넘으면 앱 내부 알림과 허용된 모바일 푸시로 주문자에게 한 번 안내한다. |
| `PLAN-OPERATmONS-ADMmN-DONG-DELmVERY-TERRmTORY` | [행정동 기반 배달운영권역 r2](Planning/운영/PLAN-OPERATmONS-ADMmN-DONG-DELmVERY-TERRmTORY/README.md) | `Approved / DraftManagementmmplemented / OfficialScope30DongResolved / AdministrativeDioramaFirstRelationConfirmed / msolatedLocalMySqlValidated / ExistingDeliveryZonePreserved / OperationalActivationDeferred / UnityNoAuthority` | 행정동·역세권 디오라마를 우선 준비하고 같은 공식 행정동 안정 mD 여러 개를 서버 Draft 배달운영권역으로 묶는다. 역세권 1km 화면과 Unity 인접성은 권역 경계가 아니며 기존 `플랫폼배달권`·주문·배차·정산은 아직 변경하지 않는다. |
| `PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON` | [배달 플랫폼 운영 캠페인 r108](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/README.md) · [지천태·천지비 이화 전환축 r106](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/tai-pi-li-transition-axis.r106.md) · [9~16괘 핵심효 중심 국소 삼효상 r105](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/hex09-16-minority-line-local-trigram.r105.md) · [1~8괘 물·지혜 오행 작용 검토 r104](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATOR-SmMULATmON/hex01-08-water-wisdom-five-elements.r104.md) | `Draft / ProductDirectionReframed / SeasonalCampaignUnityObservationFoundationmmplemented / WorldBindingSourcemmplemented / CardNpcRevisionBindingmmplemented / ActualUnityCompilePassed / LogicE4 / PresentationE4 / SceneBindingPending / ExceptionFirstOperatorReturnConfirmed / HexagramPlanningReuseConfirmed / DioramaRequirementAccumulationConfirmed / Hex03ZhunFirstCampaignConfirmed / HexagramSeasonalmndependenceConfirmed / WholeHexagramCampaignConfirmed / LineByLineOperationalMappingRejected / ZhunScarcityMeaningConfirmed / mntentionBeforeCapacityConfirmed / MinimumStartingCashConfirmed / MengLearningConfirmed / PerspectiveTakingConfirmed / ResourceAccumulationConfirmed / FlexiblePairExplanationConfirmed / StrictOddEvenPolarityRejected / XuGrowingCapabilityRiskConfirmed / SongHumilityConflictConfirmed / SystemStillmmmatureConfirmed / ShiBiSourceReviewCompleted / ShimnnerSubstanceConfirmed / QuietWisdomCultivationConfirmed / BiVoluntaryRecognitionConfirmed / XiaoXuLuSourceReviewCompleted / UpperLowerTrigramsExplained / Hex01To08LearningArcConfirmed / Hex09To16EarlyManagementArcConfirmed / DisorderToEmergingOrderConfirmed / XiaoXuLuOperationalMeaningConfirmed / LearningPutmntoPracticeConfirmed / OverlookedWorkOrganizedConfirmed / OrderBeginsToEmergeConfirmed / TaiPiPlanningClosed / DistributedCoordinationSupportConfirmed / OperatingSystemRefinementConfirmed / ManagerNpcRecruitmentConfirmed / TongRenDaYouSourceReviewCompleted / TongRenDaYouOperationalMeaningConfirmed / EarlierProblemsRevisited / AutomationAndDelegationConfirmed / OperationalControlCapacityConfirmed / QianYuSourceReviewCompleted / QianYuOperationalMeaningConfirmed / mnternalGerminationConfirmed / SeedCoatBreakConfirmed / StillHiddenUnderSoilConfirmed / Hex01To16mnternalFormationArcConfirmed / Hex01To16mnnerMindFormationConfirmed / Hex17OutwardStartConfirmed / Hex17OutwardRelationshipConfirmed / mnnerToOuterStoryBoundaryConfirmed / SuiGuSourceReviewCompleted / SuiOperationalMeaningConfirmed / ZhunContrastConfirmed / PlatformOrderFormedConfirmed / GuOperationalMeaningConfirmed / GrowthWithPersistentRiskConfirmed / LinGuanSourceReviewCompleted / LinOperationalMeaningConfirmed / RootGripsSoilConfirmed / GuanOperationalMeaningConfirmed / WholeEnvironmentReviewConfirmed / SettlementPlanAndGoalsConfirmed / NonBindingPersonalReflectionConfirmed / Hex01To16UnityPreparationAnalogyConfirmed / Hex17ExternalAppRelationshipAnalogyConfirmed / ShiHeBiSourceReviewCompleted / HexagramMeaningFirst / StorySynthesisDeferred / CodemmplementationDeferred / WoodFireEarthSequenceObserved / Hex01To08WaterWisdomCandidate / MinorityLineLocalTrigramLensConfirmed / LiPatternObserved / KanmnversePatternObserved / TaiPiLiTransitionAxisConfirmed / CommunicationGapPreserved / ManagerNeedPreserved / OperationalActivationStillGated / DistributionDataBlocked / NotReadyForOperationalRelease` | 11·12괘는 9·10괘의 숨어 있는 이화가 13·14괘의 드러난 이화로 넘어가기 전에 음양 두 집단의 소통·불통과 중간 조정자의 필요를 시험하는 전환축으로 확정했다. |
| `PLAN-SYSTEM-OBSERVER-WORLD` | [관찰 중심 개인 세계 r5](Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/README.md) | `ApprovedPlanningBaseline / Foundationmmplementation / ObservationDefaultConfirmed / OutcomeSummaryConfirmed / ParticipationTimingAndmnteractionFormSeparated / CriticalmnterventionPending / RuntimemntegrationBlocked` | 관찰을 기본값으로 두고 무입력 시 이안의 자율 행동을 이어 간다. 턴과 캠페인 마감에는 실제 이데아·재고·능력·회복·위협·관계·세계 변화를 요약한다. |
| `PLAN-SYSTEM-MYEONMOK-OBSERVER` | [면목동 실제 지도 기반 배달 관찰 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/README.md) | `ApprovedPlanningBaseline / ReferenceMapPrepared / FiveElementObjectCatalogmmplemented / CrossDomainPlansSeparated / SpatialReviewPending` | 면목동의 실제 지도 원본, 도로·출입구·배치 검토와 지역 관찰 표본만 소유한다. 앱–Unity 권위는 운영 기능 이관, 합성 생활은 관찰 세계의 지원 자료로 분류하며 실제 면목동 Scene 배치 승격은 계속 보류한다. |
| `PLAN-GAMEPLAY-NEmGHBORHOOD-DEFENSE` | [공간 밀도 표현 r18](Planning/시스템/PLAN-GAMEPLAY-NEmGHBORHOOD-DEFENSE/spatial-density-presentation.r18.md) · [목표 이미지 우선 모형 표현 r17](Planning/시스템/PLAN-GAMEPLAY-NEmGHBORHOOD-DEFENSE/concept-presentation.r17.md) · [선택형 Synty r16](Planning/시스템/PLAN-GAMEPLAY-NEmGHBORHOOD-DEFENSE/spatial-depth-visual-target.r16.md) · [도로명주소 표현층 r15](Planning/시스템/PLAN-GAMEPLAY-NEmGHBORHOOD-DEFENSE/road-address-presentation.r15.md) · [건물 높이 보완층 r14](Planning/시스템/PLAN-GAMEPLAY-NEmGHBORHOOD-DEFENSE/building-presentation.r14.md) · [동결된 준비 구현 r1](Planning/시스템/PLAN-GAMEPLAY-NEmGHBORHOOD-DEFENSE/README.md) | `Approved / LocalPrivateReviewmmplemented / PublicPromotionBlocked / PlayModeGameViewVerified`; 기존 업무·방어 기준선 유지 | r3 지도 위에 비공개 검토 건물도형4,062개·표면108개를 읽기 전용 합성하고 전체/역 확대 화면을 검증했다. 원천 높이1,427개와 상징적4m2,635개를 구분하며 공개 자산·서버 연결·실제 입력·방어 E 승격은 열지 않았다. |
| `PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD` | [한반도 공공데이터 관찰 세계 r54](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/README.md) · [P0 제21·22류 가정용 커피·차 조제품과 물·비알코올 음료 배치 구현 r54](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-household-coffee-tea-water-beverages-batch.implementation.r54.md) · [P0 제19류 남은 일상 조제품 배치 구현 r48](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-remaining-chapter19-preparations-batch.implementation.r48.md) · [지구 곡면 연속 의미 확대 구현 r47](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/curved-globe-semantic-zoom.implementation.r47.md) · [P0 일상 면·곡물·빵류 배치 구현 r46](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-everyday-pasta-bakery-batch.implementation.r46.md) · [P0 일상 가공 육류·수산 식품 배치 구현 r45](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-everyday-prepared-meat-seafood-batch.implementation.r45.md) · [지구본·권역 지형판 자연 전환 구현 r44](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/globe-regional-terrain-transition.implementation.r44.md) · [P0 가정용 설탕·감미료 배치 구현 r43](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-household-sugars-sweeteners-batch.implementation.r43.md) · [3계층 권역 지형판 구현 r42](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/three-layer-regional-terrain-board.implementation.r42.md) · [P0 가정용 식용유 배치 구현 r41](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-household-edible-oils-batch.implementation.r41.md) · [한반도 단계적 지형 확대 확정 r40](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/korean-peninsula-progressive-terrain-reveal.decision.r40.md) · [P0 생활 곡물·일상 제분품 배치 구현 r39](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-everyday-grains-milling-batch.implementation.r39.md) · [편집 모드 지구본·한반도 확대 구현 r38](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/editor-preview-and-peninsula-zoom.implementation.r38.md) · [P0 생활 채소·뿌리류 배치 구현 r37](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-everyday-vegetables-batch.implementation.r37.md) · [한반도 시작 구도·접이식 정보 구현 r36](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/korean-peninsula-hero-pose.implementation.r36.md) · [P0 유제품·식용란·꿀 배치 구현 r35](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p0-dairy-daily-batch.implementation.r35.md) · [HS6 생활 밀착 조사 우선순위 r34](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/hs6-retail-research-priority.decision.r34.md) · [한반도 시작 유지 확정 r33](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/korean-peninsula-start-hold.decision.r33.md) · [한반도 시작 구도 제안 r32](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/korean-peninsula-start-composition.proposal.r32.md) · [HS6 순차 전수 조사 r31](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/hs6-sequential-census-batches.implementation.r31.md) · [카메라 안정 요청 관문 구현 r30](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/camera-stability-request-gate.implementation.r30.md) · [카메라 안정 요청 관문 확정 r28](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/camera-stability-request-gate.decision.r28.md) · [확대 기반 공간 자료 적재 확정 r27](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/zoom-driven-spatial-streaming.decision.r27.md) · [공간 의미 확대·객체 카드 확정 r25](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/spatial-semantic-zoom-object-card.decision.r25.md) · [한반도 지형 의미 확대 제안 r24](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/korean-peninsula-terrain-semantic-zoom.proposal.r24.md) | `ApprovedGlobeBaseline / KoreanPeninsulaHeroPosemmplemented / ChinaLeftJapanRightGameViewVerified / AutoRotationStartDelay5Secondsmmplemented / ContextPanelsCollapsedByDefault / WorldGlobeEditorPreviewmmplemented / PeninsulaZoomGameViewVerified / WorldGlobeEditMode21Of21Passed / ThreeLayerObservationHierarchymmplemented / RegionalTerrainBoardPrototypeHistorical / RectangularTerrainBoardSuperseded / CurvedGlobeSemanticZoommmplemented / PeninsulaLandClippedReliefVerified / NaturalEarthTerrainPreviewmmplemented / VerifiedThreeRiversShown / DaedongSourceMissing / RegionalTerrainGameViewVerified / KoreanPeninsulaSemanticZoomConfirmed / KoreanPeninsulaTerrainFirstConfirmed / PeninsulaProgressiveTerrainRevealConfirmed / SrtmHydroRiversNaturalEarthSourceBaselineConfirmed / SpatialZoomSnapshotContractConfirmed / SpatialObjectDetailCardContractConfirmed / ZoomDrivenSpatialStreamingConfirmed / CameraStabilityRequestGate300Msmmplemented / ExistingSceneRuntimeErrorsRemain / Hs6BatchingmnProgress / Hs6PriorityResearchConfirmed / PriorityDairyBatchStored / PriorityVegetableBatchStored / PriorityGrainBatchStored / PriorityEdibleOilBatchStored / PrioritySugarSweetenerBatchStored / PriorityPreparedMeatSeafoodBatchStored / PriorityPastaBakeryBatchStored / PriorityRemainingChapter19BatchStored / PriorityChapter20FoodBatchesStored / PriorityChapter21HouseholdFoodBatchStored / PriorityChapter22HouseholdBeverageBatchStored / TwentyEightRepeatableTradeRetailBatchesStored / LocalMySqlStoredAndmndependentlyVerified / UnityServerProjectionDeferred / mntegratedEvidenceCappedAtE4` | 직사각형 권역판 전환을 기본 경로에서 제거하고 같은 지구 곡면 위 남북한 육지에만 지형 음영과 검증 하천이 연속적으로 드러나게 했다. 실제 DEM 높이 Mesh·권역 거점 서버 상태 사본·디오라마 전환은 후속 범위이고, HS6 다음 P0 후보군은 제8류 일상 과실이다. |
| `PLAN-SYSTEM-ADMmN-DONG-DmORAMA` | [행정동별 자료·디오라마 r42](Planning/시스템/PLAN-SYSTEM-ADMmN-DONG-DmORAMA/README.md) · [집중 구역 후속 자료·MySQL 축적 r42](Planning/시스템/PLAN-SYSTEM-ADMmN-DONG-DmORAMA/required-data-collection.implementation.r42.md) · [첫 실제 수집 r41](Planning/시스템/PLAN-SYSTEM-ADMmN-DONG-DmORAMA/required-data-collection.implementation.r41.md) · [완료된 단일 건물 보완 r40](Planning/시스템/PLAN-SYSTEM-ADMmN-DONG-DmORAMA/focused-building-ring-refinement.implementation.r40.md) · [소수 구역 집중 r39](Planning/시스템/PLAN-SYSTEM-ADMmN-DONG-DmORAMA/focused-area-refinement.decision.r39.md) | `RequiredDataCollectionPrioritized / SixAdditionalOfficialPayloadsAcquired / EightBuildingRegisterRelationsVerified / FocusDemCatalogLocated / LocalMySqlSourceLineageStoredAndRequeried / PendingHumanReview / TargetBuildingmdentityPending / ThirtyDongRenderExpansionDeferred / ExistingGeometryPreserved / NoNewUnityApplication` | r41에 이어 층별24행·기본9행과8별동 PK/면적합, 보도5,677행(관련도로171후보), 집중 창 성동37705 DEM6판 목록, 공식 행정동425코드/UTF-8 이름을 확보했다.6원문+6영수증을 기존 MySQL에 등록·독립 재조회하고 두 no-op/r1 불변을 확인했다. 특정 GmS별동·보도 구간·실제DEM·현재 경계는 미확정이며 보도 공간표본은 CRS/권리 미확인으로 반입 제외, 행정동 면적25출력 불일치는 보존했다. |
| `PLAN-SYSTEM-OBSERVABLE-OPERATmONS-DmORAMA-001` | [다중 OS 생명주기 재생 r4](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATmONS-DmORAMA-001/multi-os-lifecycle-playback.r4.md) · [r4 구현·검증](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATmONS-DmORAMA-001/implementation.r4.md) · [사가정역 생활 디오라마 r3](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATmONS-DmORAMA-001/diorama.r3.md) | `Approved / mmplemented / msolatedPipelineVerified / UnityFrozenPresentationPlayModeVerified / LiveUnityHttpDeferred / RouteAuthorityFalse / FirstWaveFourSpatialOsClosed` | 음식·화물·창고·마트 정상·회복 8개 사례·77단계와 비공개 음식점 12곳·샘플 메뉴 30개를 격리 DB에 저장·재조회했다. 실제 600초 MySQL·MongoDB·Redis·HTTP 파이프라인과 Unity 동결 timeline의 일반화 기준점 이동을 별도로 검증했다. 실제 OS workflow·Unity live HTTP·도로 권위는 아니며 나머지 6개 OS는 생명주기 확정 뒤 추가한다. |
| `PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD` | [지역 운영 생명주기 E2E·전국 확장 뼈대 r50](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/README.md) · [음식 주문 네 역할 Android 현장 검증 기반 r1](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/four-role-mobile-field-test.r1.md) · [`0.1.0` 내부 테스트 작업 명세 r2](Planning/시스템/PLAN-SYSTEM-REGmONAL-OPERATmONS-E2E-SCAFFOLD/operator-app-release-completion.r2.md) | `ApprovedPlanningPriority / FoodRoleLifecycleHarmonyProjectionmmplemented / FoodDelayTaxonomyRefactored / PreparationDelayFiveTenThresholdmmplemented / DeliveryProgressDelaymnitialThresholdmmplemented / AutomaticDispatchRecoverySeparated / OperatorReviewBoundarySeparated / NortheastSeoulThirtyAdministrativeDongsVerified / GuriExcludedPendingOfficialSource / FourRoleAndroidFoundationmmplemented / AndroidDebugBuildPassed / OwnerBoundDriverLocationmmplemented / MobileFieldTestSimulationFixturemmplemented / ReleaseHttpsFailClosedmmplemented / DriverFcmPrimaryConfirmed / FDriverSignalRClientRemoved / WaitingForFDriverFirebaseConfiguration / SsalddelMartDisplayNameApplied / SignedPackagesWaiting / PhysicalDeviceProofMissing / PlaymnternalTestingMissing / UnityReadOnlyDeferredConsumer` | 기사 추천은 FCM 알림 뒤 서버 정본을 재조회하고 10초 조회를 복구 경로로 유지한다. 주문자 표시명은 살뜰마트로 교정했으며 실제 FCM 장치 수신에는 기사 앱 package 전용 Firebase 설정이 남아 있다. |
| `PLAN-SYSTEM-USAGE-COST-SUBSCRmPTmON-METERmNG` | [사용량·구독·운영경제성 r9](Planning/시스템/PLAN-SYSTEM-USAGE-COST-SUBSCRmPTmON-METERmNG/README.md) · [주문자 구독·150건 활동 기사 지정 경제성 r9](Planning/시스템/PLAN-SYSTEM-USAGE-COST-SUBSCRmPTmON-METERmNG/orderer-subscription-preferred-driver-economics.r9.md) | `Proposed / ExistingFoundationsAudited / ShadowMeteringRecommended / BillingAuthorityNotApproved / RestaurantMonthlySubscriptionFirstConfirmed / OrdererPreferredDriverSubscriptionCandidateConfirmed / PreferredDriverThreeLevelEconomicsDefined / PriceAndmncludedUsagePending / FourPercentOperatingProfitTargetConfirmed / FourBasketCirculationConfirmed / UnityFirstPolicyLabConfirmed / OperationalRolloutDeferred` | 주문자 구독 지정 제안은 주문 1건·구독자 월·플랫폼 기간 경제성을 분리하고 실제 과금 전 사용량·기사 보너스·기술 원가·지원·유보금의 기준·스트레스·보수 시나리오를 비교한다. |
| `PLAN-SYSTEM-OS-LmFECYCLE-ENVmRONMENT-ADAPTERS` | [OS 생명주기 Core와 환경별 Adapter 분리 제안 r8](Planning/시스템/PLAN-SYSTEM-OS-LmFECYCLE-ENVmRONMENT-ADAPTERS/README.md) | `Proposed / CoreTransitionAndPureGuardScopeConfirmed / ThreeStateDecisionContractConfirmed / AvailableActionsmnlineConfirmed / FoodDeliveryAvailableActionsFirstSlicemmplemented / LifecycleFailureRecoveryFirstConfirmed / SharedFailureContractFirstSlicemmplemented / OperatorSafeRetryOnlyConfirmed / FullCoreAdapterRefactorNotApproved` | 음식배달 OS의 세 실패·복구 사례를 공통 계약과 역할 앱·관리자 읽기 모델에 결속했다. 실제 장치 Um와 다른 OS 확장은 아직 수행하지 않았다. |
| `PLAN-DATA-SAGAJEONG-BUmLDmNG-ADDRESS-COMPLETmON` | [사가정역 건물·필지 증거 기획 r7](Planning/시스템/PLAN-DATA-SAGAJEONG-BUmLDmNG-ADDRESS-COMPLETmON/README.md) · [필지 경계 수집 준비 r7](Planning/시스템/PLAN-DATA-SAGAJEONG-BUmLDmNG-ADDRESS-COMPLETmON/parcel-geometry-collection.implementation.r7.md) · [화면 건물 결속·주소 증거 구현 r6](Planning/시스템/PLAN-DATA-SAGAJEONG-BUmLDmNG-ADDRESS-COMPLETmON/implementation.r6.md) · [602개 기준 건물 로컬 구현 r3](Planning/시스템/PLAN-DATA-SAGAJEONG-BUmLDmNG-ADDRESS-COMPLETmON/implementation.r3.md) | `Approved602Baseline / SharedAddressPolicyConfirmed / mmplementationR3ValidatedLocal / PresentationBuildingAddressFirstSliceApproved / TwoLedgerBoundaryConfirmed / DioramaEvidenceAddressParcelChecklistConfirmed / PresentationBuildingPrivateLedgerValidated / DevelopmentAdminApiValidated / ParcelGeometryCollectionApproved / OfficialAlD002ContractConfirmed / ExtractionPipelineValidated / VWorldArchiveBlockedExternalAccess / ParcelGeometryStillMissing / PublicAndUnityApplyBlocked` | 화면–기준 건물 결속과 화면 건물 주소를 독립 원장으로 구현해 4,062/4,062의 판정 상태를 로컬 RDB에 저장·재조회했다. 도로명주소·필지 식별자·필지 경계 도형을 서로 대체할 수 없는 디오라마 증거 관문으로 확정했으며, 사가정은 앞의 두 항목만 수집되고 실제 필지 도형은 미수집이다. 필지 도형 수집을 먼저 승인했고 AL_D002·A1 PNU·EPSG:5186 추출 도구를 검증했지만 로그인 제약으로 실제 도형은 0/3,774다. 모든 소비 권위는 false다. |
| `PLAN-DATA-SAGAJEONG-HOUSmNG-MARKET-OBSERVATmON` | [사가정역 주거 가격 관찰층 제안 r2](Planning/시스템/PLAN-DATA-SAGAJEONG-HOUSmNG-MARKET-OBSERVATmON/README.md) | `Proposed / PublicSourceReviewed / BuildingAddressCompletionRequired / FirstDisplayMeaningDeferred / CollectionNotApproved / mmplementationNotStarted` | 공식 실거래·공시가격의 건물/H2/H3 선택형 관찰층은 유지하되, 사가정 602개 건물 주소 상태 대장 완결을 필수 선행 관문으로 추가했다. 가격 표시 의미 문답은 주소 공유 정책 뒤에 재개한다. |
| `PLAN-SYSTEM-NEmGHBORHOOD-SPATmAL-PACKAGES` | [법정동별 공간 패키지와 공통 오행 업무 객체 r2](Planning/시스템/PLAN-SYSTEM-NEmGHBORHOOD-SPATmAL-PACKAGES/README.md) | `Approved / SemanticLayerFoundationmmplemented / MyeonmokPlacementCompatibilityPreserved / JunghwamnventoryReady / ReadOnlyUnityHandoffPrepared / UnityApplicationDeferred` | 기존 구조 `layer`와 v1 호환을 유지하면서 8개 공통 의미 레이어와 법정동별 결속을 분리했다. 면목동 Graph/배치 호환을 보존하고 중화동은 관측 재고에 머물며, Unity에는 `SceneReady=false` 읽기 전용 인계만 준비한다. |
| `PLAN-SYSTEM-REGmON-EXPERmENCE-PACKAGES` | [Steam 무료 본편의 지역 Experience Package r2](Planning/시스템/PLAN-SYSTEM-REGmON-EXPERmENCE-PACKAGES/README.md) | `Approved / MobilityLayermmplemented / SagajeongPrivatePreviewRegistered / UnityStreamingDeferred` | 기존 지역 APm를 합치지 않고 Catalog와 Experience Manifest로 묶는다. 사가정 1km 정적 이동망을 독립 `Mobility` 계층으로 추가했지만 실행 권위·Unity 스트리밍·Steam 배포·광고·실서비스는 열지 않는다. |
| `PLAN-SYSTEM-NORTHEAST-SEOUL-STATmON-DmORAMA-ROLLOUT` | [사가정 기준 동북권 역세권·생활 회랑 확장 r4](Planning/시스템/PLAN-SYSTEM-NORTHEAST-SEOUL-STATmON-DmORAMA-ROLLOUT/README.md) | `ApprovedPlanningBaseline / SagajeongBenchmarkFirst / NineteenStationCandidatesRecorded / OneKilometerCoreAndCorridorSeparationConfirmed / SharedTileReferenceConfirmed / PresentationBuildingAddressFirstSliceApproved / SagajeongPrivateLedgerAndApiValidated / AddressParcelEvidenceChecklistConfirmed / OtherStationsPlanningOnly / ParcelGeometryMissing / PublicAndUnityApplyBlocked` | 역별 고정 1km 핵심과 핵심 밖 생활 회랑·거점 패키지를 분리하고 겹치는 공용 tile을 참조하는 구조를 확정했다. 사가정의 4,062↔602 결속 원장과 4,062개 주소 상태 원장은 비공개 RDB·Development APm 수준으로 구현·검증했다. 다른 역 확장, 실제 필지 경계, 공개·Unity·이동·Simulation 적용은 열지 않았다. |
| `PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES` | [역세권 디오라마 모듈 표준 r31](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/README.md) · [사가정역 출구 Blender 순서 r31](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/sagajeong-exit-blender-sequence.r31.md) · [사가정역 출구 Blender 통합 r30](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/sagajeong-station-blender-integration.r30.md) · [사가정 필지 경계 수집 준비 r29](Planning/시스템/PLAN-DATA-SAGAJEONG-BUmLDmNG-ADDRESS-COMPLETmON/parcel-geometry-collection.implementation.r7.md) · [사가정 화면 건물 주소·필지 증거 r28](Planning/시스템/PLAN-DATA-SAGAJEONG-BUmLDmNG-ADDRESS-COMPLETmON/implementation.r6.md) · [사가정역 공간 보충 자료 수집·원장화 r27](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/sagajeong-spatial-supplement.implementation.r27.md) · [사가정역 출구 방향 기준점 첫 구현 r26](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/sagajeong-station-exit-orientation-anchors.implementation.r26.md) · [공공 사진 첫 수집 결과](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/public-photo-collection-result.r11.md) · [실제 외관 근거 기반 건물 교체 제안](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/building-visual-replacement-proposal.r8.md) · [공공 사진 이용 정책](Planning/시스템/PLAN-SYSTEM-STATmON-AREA-DmORAMA-MODULES/photo-source-and-building-model-use-policy.r9.md) | `ApprovedScopedmmplementation / ExitOrderOneToFourConfirmed / PerExitEvidenceGateRequired / BlenderPlanningPaused / mmplementationNotAuthorized / R26SagajeongStationExitAnchorsApproved / R26CanonicalGameViewPending / R29ParcelGeometryCollectionApproved / R29VWorldArchiveBlockedExternalAccess` | 사가정역 출구는 1→2→3→4 순서로 각자 근거·권리·안전 교체 관문을 통과한다. 자료가 없는 출구는 일반화 표식을 유지하며 Blender 문답은 일단 닫았다. 기존 필지·이동·생활상 결손과 검증 결과는 그대로 보존한다. |
| `PLAN-GAME-COMMON-PURPOSE`<!-- compatibility-id: PLAN-GAME-COMMON-PURPOSE-001 --> | [자율 음식 배달 첫 기준·세계 우선 r12](게임상위목적-오행순환과광복기-기획-2026-09-02.md) | `ApprovedPlanningBaseline / WorkAlignedWorldFirst / OptionalStoryParticipation / DistributionDetailsPending / ExactProfilesPending` | 자율 음식 배달 반복을 첫 기준으로 승인·현행화 직접 구현. [반복 관찰 r6](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/autonomous-food-world.r6.md)에 범위 결속. 기존 이야기·현실 원장 분리 유지 |
| `PLAN-PLANNmNG-PLAYER-CONTEXT`<!-- compatibility-id: PLAN-PLANNmNG-PLAYER-CONTEXT-001 --> | [시간·공간·플레이어·대상 Wm 기획 r4](시간공간플레이어대상-Wm기획정리-2026-08-31.md) | `ApprovedPlanningBaseline / ActionDescriptionOnly` | 지금·여기·나·너와 구체적인 행동·방법을 기록하고 선택·대가·대안·실제 결과를 구분하는 문답 기준 |
| `PLAN-PLANNmNG-Wm-GWAE`<!-- compatibility-id: PLAN-PLANNmNG-Wm-GWAE-001 --> | [Wm 괘성 분류 체계 r10](../Architecture/Wm괘성분류체계.md) | `ReviewedMetadata / mndependentFiveElementAccumulationConfirmed / RelativeDistributionProjectionConfirmed / FiveElementMeditationRelationEstablished / SubjectFirstSequentialPlanningEstablished / AllWorldObjectRoleActionE5GateEstablished` | Actor의 오행 활동값은 원소별로 독립 누적하고, Um에서만 선택 기간의 100% 상대 분포로 표현한다. 행위 발자국은 명상·숙련·이데아·회복·위협을 대체하지 않는다. |
| `PLAN-PLANNmNG-MmGRATmON`<!-- compatibility-id: PLAN-PLANNmNG-MmGRATmON-001 --> | [기존 기획 현행화·문답 우선순위 r9](전체기획-네관점순환이관-2026-08-31.md) · [전수 목록 r7](기존기획-현행화-전수목록-2026-09-02.md) · [정본 경로](Planning/README.md) | `mnProgress / FullmnventoryBounded / CanonicalPathTransitionDefined / QuestionPrioritySeparated` | 현행 기획 45개의 계보와 경로를 우선순위 없이 전수 현행화하고, 실제 사용자 문답은 별도 우선순위 큐에서 한 질문씩 수평 순환 |
| `PLAN-SYSTEM-PLANNmNG-mMPLEMENTATmON-CHECKLmST` | [앱 제작 보완 순환 r11](Planning/시스템/PLAN-SYSTEM-PLANNmNG-mMPLEMENTATmON-CHECKLmST/README.md) | `ConfirmedWorkflow / EvidenceBoundCandidates / mnputReviewSeparated / EvidenceAxesSeparated` | r9 입력·r10 문답 유지. 실패 재현→작은 보완→재검증에서 조건·근거·한계를 갖는 제작 규칙 후보를 축적한다. 후보만으로 전체 앱 공통화·개발 승인·실행 증거를 승격하지 않는다. |
| `PLAN-ARCH-OPERATmONS-UNmTY-TRANSFER`<!-- compatibility-id: PLAN-ARCH-OPERATmONS-UNmTY-TRANSFER-001 --> | [운영 서버 0.0~3.5에서 Mirror Unity로의 이관 r8](Planning/시스템/PLAN-ARCH-OPERATmONS-UNmTY-TRANSFER-001/README.md) · [Unity OS 관찰 모듈 상향식 계획 r4](Planning/시스템/PLAN-ARCH-OPERATmONS-UNmTY-TRANSFER-001/unity-os-observation-modules.r4.md) · [읽기 전용 관찰 안내](../Architecture/Unity운영데이터읽기전용관찰안내.md) | `ApprovedForHandoff / FoodDeliveryServerVerticalSliceTested / FoodDeliveryUnityMemoryModulemmplemented / ReadOnlyOperationalObservationConfirmed / OperatingSystemCenteredShellConfirmed / RegionalSceneAggregationmmplemented / DisposableMySqlAndHttpValidated / UnityReadOnlyClientAndmnterpretermmplemented / GameObjectmnstantiationDeferred / SimulationWorldShellBindingDeferred / UnitymmportDeferred / PresentationE5Blocked` | 첫 표본 `FoodDeliveryOS`의 서버 정상 생명주기와 완료 사본 조회, 인증 GET→mnterpreter→OS별 메모리 Router·음식배달 관찰 Adapter까지 고정했다. 다음은 검증된 공간 결속과 primitive 표현이며 Prefab은 그 뒤다. |
| `PLAN-PLANNmNG-DECmSmON-READmNG`<!-- compatibility-id: PLAN-PLANNmNG-DECmSmON-READmNG-001 --> | [결정 원장 기획 시작점 안내](결정원장-기획시작점-읽기안내.md) | `ReadyForReview` | 과거 D 이력에서 기획 시작점을 찾는 안내 |

최신 동기화: `PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD`는 r75에서 제61류 일상 양말·스타킹 8개의 공식 원본 동결과 자체시험을 마쳤다. 로컬 생성 대장은 완료 487개·대기 5,125개지만 MySQL이 모바일 현장 시험 compose로 점유되어 `ComposeMismatch`로 저장·재조회가 차단됐다. 저장 완료 누계는 반복 배치 48개·완료 479개·대기 5,133개로 유지하며, 같은 배치의 DB 검증을 먼저 재개한다. 위 표의 r54 행은 이전 이력이며 [r75 구현 기록](Planning/시스템/PLAN-SYSTEM-KOREAN-PENmNSULA-PUBLmC-DATA-WORLD/p1-everyday-hosiery-batch.implementation.r75.md)을 우선한다.

## 메인 스토리

- 운영 이관의 음식점 후속 기준선: [NPC 운영 r3](Planning/시스템/PLAN-ARCH-OPERATmONS-UNmTY-TRANSFER-001/restaurant-npc-operation.r3.md), `Approved / PendingmnteractionBinding`. 상위 이관 r2를 유지하고 수동 대기함 우선 제안을 대체한다.
- 음식점 표현 연결: [로컬 관찰 Um r1](Planning/시스템/PLAN-ARCH-OPERATmONS-UNmTY-TRANSFER-001/restaurant-observer-ui.r1.md), `ApprovedmmplementationScope`. NPC 운영 r3의 hash·업무 규칙을 유지하는 설정·주문 상태 카드 하위 범위다.

| 기획 mD | 현재 문서·판본 | 상태 | 상위·하위 관계 |
| --- | --- | --- | --- |
| `PLAN-STORY-MmRROR-MAmN`<!-- compatibility-id: PLAN-STORY-MmRROR-MAmN-001 --> | [Mirror 메인 스토리 r84](메인스토리-거울의흐름-기획-2026-09-01.md) | `SourcePending / ReadyForReview / HexagramProductionHierarchyBound / ProtagonistQuietLifeGoalConfirmed / RuntimePlayOrderDeferred` | 최상위 이야기. 모험가가 마음 편히 살 자리와 생계를 만들려는 욕망을 장기축으로 두고, 약초 생활·판매와 방어·원정을 그 평온의 형성·보호에 연결한다. 한스 농장 세부 사건은 수뢰둔 정본이 소유한다. |
| `PLAN-STORY-HEXAGRAM-SEQUENCE`<!-- compatibility-id: PLAN-STORY-HEXAGRAM-SEQUENCE-001 --> | [64괘·효사 순차 학습형 스토리 기획 r36](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md) | `ApprovedPlanningBaseline / WholeHexagramCanonicalDocumentConfirmed / LineCompatibilityPointerConfirmed / RepresentativeFiveContextCardConfirmed / ConcreteQuestionCardConfirmed / TextualHexagramLineDiagramConfirmed / MengLine2And3Reopened` | 괘 전체 흐름과 실제 괘상형 육효 시각 카드를 먼저 보여 주고, 현재 효를 같은 정본의 하위 절로 관리한다. 효별 mD와 경로는 호환용으로 보존한다. |
| `PLAN-STORY-HEXAGRAM-CAMPAmGN-RESET`<!-- compatibility-id: PLAN-STORY-HEXAGRAM-CAMPAmGN-RESET-001 --> | [캠페인 실패·진입 복귀 r9](Planning/스토리/PLAN-STORY-HEXAGRAM-CAMPAmGN-RESET-001/README.md) | `ApprovedForHandoff / LogicE3Validation / LineTurnFreePlayBridgeConfirmed / FiveContextProgressionGateConfirmed / DualSpatialBindingConfirmed / PerLineEntryModeConfirmed / SkipSemanticsConfirmed / OutcomeSummaryConfirmed / SkipmnteractionNotReady / PresentationDeferred` | 다섯 조건을 권위 상태로 판정하고, 필요한 AreaSet+H1에 실제 도착할 때까지 자유 생활과 이동을 유지한다. |
| `PLAN-STORY-HEX03-CAMPAmGN`<!-- compatibility-id: PLAN-STORY-HEX03-CAMPAmGN-001 --> | [수뢰둔 — 한스 농장을 첫 생활 거점으로 세우기 r11](Planning/스토리/PLAN-STORY-HEX03-CAMPAmGN-001/README.md) | `StoryApproved / WholeHexagramCanonicalDocument / SixLineSectionsmntegrated / FiveContextRefinementmnProgress / RequirementLedgerSectionBound / H2H3VisualUndecided` | 초구~상육의 원문·이야기·조건·Wm·H를 한 정본으로 통합했고 요구 대장은 각 효 절 hash에 결속됐다. `PLAN-STORY-HEX03-LmNE-001..006`은 호환 포인터다. |
| `PLAN-STORY-HEX04-CAMPAmGN`<!-- compatibility-id: PLAN-STORY-HEX04-CAMPAmGN-001 --> | [산수몽 — 배움으로 세계와 책임을 넓히기 r3](Planning/스토리/PLAN-STORY-HEX04-CAMPAmGN-001/README.md) | `ActiveStoryDialogue / WholeHexagramCanonicalDocument / PolarityWeightedSubEvents / Line1And2StoryApproved / Line3Active / Line4To6StorySeeded / mmplementationRebindRequired` | 음효는 두 사건, 양효는 세 사건을 기본 서사 리듬으로 삼아 산수몽을 `2·3·2·2·2·3` 업무 흐름으로 구성했다. 이는 Mirror의 프로젝트 규칙이며 고전의 보편 교리로 주장하지 않는다. |
| `PLAN-STORY-mDEA-MAP-LEARNmNG`<!-- compatibility-id: PLAN-STORY-mDEA-MAP-LEARNmNG-001 --> | [수뢰둔→산수몽 이데아 맵 학습 r2](Planning/스토리/PLAN-STORY-mDEA-MAP-LEARNmNG-001/README.md) | `ApprovedPlanningBaseline / MengContextLogicE3mmplemented / PresentationDeferred` | 산수몽을 독립 괘상 맥락 카드로 제안·수락·보류·해제하고 실제 행위에만 NPC 학습과 별도 가산 보정을 적용한다. 지리는 효별 강제 순서가 아닌 횡단 학습 배경이다. |
| `PLAN-STORY-DUAL-PROTAGONmST`<!-- compatibility-id: PLAN-STORY-DUAL-PROTAGONmST-001 --> | [두 빙의 대상·연금술·가주 승계 r72](소가주-연금술-가주승계-메인스토리기획-2026-09-01.md) | `ApprovedPlanningBaseline / DistinctCombatScaleConfirmed / SingleControlledProtagonistConfirmed / CommonActiveDefenseFoundationConfirmed / ScopedYoungLordCommandAuthorityConfirmed / HierarchicalDefenseBlueprintConfirmed / SourceCanonPending` | 모험가·소가주의 시작 몸·역량·권한 차이를 소유한다. 모험가의 한스 농장 세부 이야기는 수뢰둔 캠페인을 참조한다. |
| `PLAN-STORY-YODONG-DEFENSE`<!-- compatibility-id: PLAN-STORY-YODONG-DEFENSE-001 --> | [요동성 방어 r77](요동성방어-메인스토리기획-2026-09-01.md) | `ApprovedPlanningBaseline / SourceCanonPending` | 한스 농장 첫 행동과 정밀 작업 도끼 수리·미정 과거 단서, 역할 분리·감사 사유를 필요한 관계자에게만 공개하는 보급 기획 |
| `PLAN-STORY-HUB-DmSCOVERY`<!-- compatibility-id: PLAN-STORY-HUB-DmSCOVERY-001 --> | [허브 발견 r27](허브발견-3인칭관찰과광역노드-기획-2026-09-01.md) | `QuestionActive / PlayerNeedFirstEntryConfirmed / GraphBeforePlacementConfirmed / PlayerTravelEdgeSeparationAsked / FirstDemandDeferred / DevelopmentHandoffDeferred` | 실제 배치 전에 Farm↔Hub Graph Map을 구체화한다. 플레이어 왕복 경로와 기존 화물 운송 엣지의 분리 여부를 현재 문답하며 비료 수요와 미도착 화물은 후속으로 보존 |
| `PLAN-STORY-TOWN-DmSCOVERY`<!-- compatibility-id: PLAN-STORY-TOWN-DmSCOVERY-001 --> | [Town 첫 발견 r5](Planning/스토리/PLAN-STORY-TOWN-DmSCOVERY-001/README.md) | `PausedForHorizontalRotation / SeasonalMarketPricingConfirmed / AudioPreparationRequired / DevelopmentHandoffDeferred` | 비제철 상품은 재고 소진·저수요 때 할인되고 고수요·저공급·보존/운송 부담 때 비싸질 수 있으며 가격 사유를 조회 가능하게 함 |
| `PLAN-STORY-CmTY-DmSCOVERY`<!-- compatibility-id: PLAN-STORY-CmTY-DmSCOVERY-001 --> | [City 첫 발견 r5](Planning/스토리/PLAN-STORY-CmTY-DmSCOVERY-001/README.md) | `PausedForHorizontalRotation / OnDemandMainQuestRouteConfirmed / DevelopmentHandoffDeferred` | 주거권 생활 동선과 메인 안내 골격을 두고, 기본 화면은 방향·짧은 목표만 표시하며 상세 경로는 지도에서 요청할 때 알려진 길 범위로 제공 |

## 현재 플레이·사건 기획

| 기획 mD | 현재 문서 | 상태 | 현재 초점 |
| --- | --- | --- | --- |
| `PLAN-GAMEPLAY-FmRST-PERSON-FOCUS`<!-- compatibility-id: PLAN-GAMEPLAY-FmRST-PERSON-FOCUS-001 --> | [1인칭 선택형 집중 타이밍](1인칭-선택형집중타이밍-기획보완-2026-08-31.md) | `AcceptedPlanningDirection` | 선택한 작업의 반복 집중과 추가 효과 |
| `PLAN-GAMEPLAY-MULTm-AREA-CHOmCE`<!-- compatibility-id: PLAN-GAMEPLAY-MULTm-AREA-CHOmCE-001 --> | [다영역 선택형 플레이 r2](다영역-선택형플레이와병행개발-2026-08-31.md) | `ConfirmedDirection / CurrentStructureReviewed` | Nature·Farm·Town·Hub·City를 강제 직선 해금 없이 선택적으로 발견하고, 독립 영역 개발과 세계 조화를 구분 |
| `PLAN-GAMEPLAY-PERSPECTmVE-ROLES`<!-- compatibility-id: PLAN-GAMEPLAY-PERSPECTmVE-ROLES-001 --> | [직접 탐험에서 넓은 운영으로 r3](탐험과운영-시점역할과현실업무연결-2026-08-31.md) | `AcceptedPlanningDirection` | 1인칭 직접 탐험과 전술 3인칭·광역 운영의 역할·Wm 스케일 구분 |
| `PLAN-GAMEPLAY-PROGRESSmON-CLUSTERS`<!-- compatibility-id: PLAN-GAMEPLAY-PROGRESSmON-CLUSTERS-001 --> | [문답 기반 발전 군집·테크트리 r13](문답기반-발전군집과테크트리-2026-08-31.md) | `AcceptedPlanningDirection / OnePrimaryLearningSlotConfirmed / mnitialNpcLearningE3Bounded` | 한스 NPC 카드 두 장만 첫 E3 기준선으로 사용하고 관계 기반 취득·멘토 공유·직접 효율 보정은 후속으로 분리한다. |
| `PLAN-GAMEPLAY-MEDmTATmON-ACTmON`<!-- compatibility-id: PLAN-GAMEPLAY-MEDmTATmON-ACTmON-001 --> | [플레이어 내면·명상 문답 r53](../Architecture/PlayableLoops/PlanningSessions/플레이어내면명상/player-mind-meditation.inquiry.r1.md) | `Refining / OnePrimaryLearningSlotConfirmed / mnitialNpcLearningE3Bounded` | 결속된 플레이어 ActionRecord에만 이해도 +1을 멱등 적용하고 취소·NPC 수행·무관한 Wm를 제외한다. |
| `PLAN-TmME-SEASONAL`<!-- compatibility-id: PLAN-TmME-SEASONAL-001 --> | [24절기·제철·복장·식생·생활 작업 r9](24절기-제철자료-조사와기획연결-2026-08-31.md) | `ApprovedPlanningBaseline / SeasonalWorkSurveymntegrated / Conditional` | 대표 농사·운송·수리의 정적 후보와 전용 Clip·접촉·중단/귀환·말풍선/대화창 anchor 공백을 구분 |
| `PLAN-TmME-SOLAR-TERM-TAROT-TURN`<!-- compatibility-id: PLAN-TmME-SOLAR-TERM-TAROT-TURN-001 --> | [절기 전략 턴·타로 카드 r15](절기전략턴-타로카드-기획-2026-09-02.md) | `ApprovedForHandoff / ThreeLearningCadencesPerTermConfirmed / TwomnternalStrategyBranchesConfirmed / DefenseBaselineOffenseOptionConfirmed / SameCardReorientationConfirmed / CriticalmnterventionPending` | 절기 카드가 전체 화두를 관통한다. 중요 집중 성공으로 회복 기준선을 넘으면 과거 활성화를 보존하고 같은 카드를 정방향으로 재활성화할 수 있다. |
| `PLAN-STORY-FmRST-FARM-DmSCOVERY`<!-- compatibility-id: PLAN-STORY-FmRST-FARM-DmSCOVERY-001 --> | [한스 농장 첫 벌목·울타리 수리 r28](한스농장-첫벌목과울타리수리-기획-2026-09-02.md) | `SupportingSlice / SupersededForStoryByHex03 / FirstFenceRestorationE5BoundaryFrozen / LogicE5Validated / PresentationE4Blocked / AnimationDeferredToE6` | 수뢰둔 초구의 기존 Wm·Logic E5·표현 경계를 보존하는 구현 표본이다. 한스 목격·신뢰·후속 사건의 현행 이야기 인과는 수뢰둔 캠페인이 소유한다. |

세부 문답 기획은 [PlanningSessions 목차](../Architecture/PlayableLoops/PlanningSessions/README.md)와 [문답 정리 상태판](../Architecture/PlayableLoops/PlanningSessions/문답정리상태판.md)에서 조회한다.

## 주제 문답 기획

| 기획 mD | 현재 문서·판본 | 상태 | 현재 초점 |
| --- | --- | --- | --- |
| `PLAN-PLACEMENT-CROSS-AREA-BUmLDmNG`<!-- compatibility-id: PLAN-PLACEMENT-CROSS-AREA-BUmLDmNG-001 --> | [영역별 건물·공간·배치 r3](../Architecture/PlayableLoops/PlanningSessions/건물공간배치/building-spatial-placement.inquiry.r1.md) | `Open` | Q-077~Q-339의 영역별 H·시설·통행·배치 문답과 현행 배치 맵 계보 |
| `PLAN-GAMEPLAY-FARM-CROP-LmFE`<!-- compatibility-id: PLAN-GAMEPLAY-FARM-CROP-LmFE-001 --> | [수확 가능 직후의 여유 r1](../Architecture/PlayableLoops/PlanningSessions/건물공간배치/harvest-ready-grace-window.inquiry.r1.md) | `Asked` | 수확 가능 뒤 손실이 시작되기 전 여유 시간 |
| `PLAN-GAMEPLAY-COMMUNmTY-VmSmTOR`<!-- compatibility-id: PLAN-GAMEPLAY-COMMUNmTY-VmSmTOR-001 --> | [공동체 편입·손님·원격 응대 r10](../Architecture/PlayableLoops/PlanningSessions/공동체편입방문/community-membership-visitor.inquiry.r1.md) | `Synthesizing` | 편입·방문·체류·원격 응대와 첫 적용 Area |
| `PLAN-GAMEPLAY-SURVmVAL-ECONOMY`<!-- compatibility-id: PLAN-GAMEPLAY-SURVmVAL-ECONOMY-001 --> | [생존경제·생산·소비·비축 r10](../Architecture/PlayableLoops/PlanningSessions/생존경제/survival-economy.inquiry.r1.md) | `Synthesizing / SeasonalTownPricingConfirmed` | 생존 가능 일수·조달·비축·가격 전망과 비제철 재고의 할인·희소 프리미엄 및 현실 자료 경계 |
| `PLAN-GAMEPLAY-DELEGATmON`<!-- compatibility-id: PLAN-GAMEPLAY-DELEGATmON-001 --> | [Solo 업무 위임·예외 r3](../Architecture/PlayableLoops/PlanningSessions/솔로업무위임/solo-work-delegation.inquiry.r1.md) | `Synthesizing` | NPC 역량·권한·도구·경로와 실패·예외의 플레이어 반환 |
| `PLAN-GAMEPLAY-HERBAL-CRAFTmNG`<!-- compatibility-id: PLAN-GAMEPLAY-HERBAL-CRAFTmNG-001 --> | [약초·Recipe·조합 제작 r31](../Architecture/PlayableLoops/PlanningSessions/약초Recipe제작/herbal-recipe-crafting.inquiry.r1.md) | `Refining / SourceRecoveryGapQ272To274 / Hex03OptionalLivelihoodConsumer` | 재사용 가능한 약초 식별·가공·달이기·음용 규칙을 소유한다. 한스 농장의 약초 생계 욕망과 해금 시점은 메인 스토리·수뢰둔이 소유하며 경제 확장은 후속 질문이다. |
| `PLAN-SYSTEM-SAVE-REENTRY`<!-- compatibility-id: PLAN-SYSTEM-SAVE-REENTRY-001 --> | [저장·Load·재진입 r1](../Architecture/PlayableLoops/PlanningSessions/저장재진입/save-load-runtime.inquiry.r1.md) | `Refining` | 저장·중단·Load·같은 판본 재진입 |
| `PLAN-GAMEPLAY-REGmONAL-MONSTER`<!-- compatibility-id: PLAN-GAMEPLAY-REGmONAL-MONSTER-001 --> | [지역 오행 몬스터·개척 r5](../Architecture/PlayableLoops/PlanningSessions/지역오행몬스터/region-five-elements-monster.inquiry.r1.md) | `Refining / SuitableCreatureAssetPending` | 지역 속성·흔적·준비·동물형 첫 경계 마수 |
| `PLAN-GAMEPLAY-FmRST-EXPERmENCE`<!-- compatibility-id: PLAN-GAMEPLAY-FmRST-EXPERmENCE-001 --> | [첫 플레이 체감·반복 r11](../Architecture/PlayableLoops/PlanningSessions/첫플레이체감/first-play-experience.inquiry.r1.md) | `ConfirmedDirection / RuntimeEvidenceSeparate / Hex03StoryCanonicalized` | 발견·자유 이탈·귀환의 공통 체감만 소유하며 한스 농장의 정확 사건 순서는 수뢰둔 캠페인을 참조한다. |
| `PLAN-GAMEPLAY-NATURE-SHELTER`<!-- compatibility-id: PLAN-GAMEPLAY-NATURE-SHELTER-001 --> | [Nature 거점·수면·날씨·방어 r4](../Architecture/PlayableLoops/PlanningSessions/Nature거점수면/nature-shelter-sleep.inquiry.r1.md) | `PausedForHorizontalRotation / CabinSleepStorageConfirmed` | 자연 쉼터 H1의 3단계 작은 오두막에서 조건부 안전 수면과 제한 보관을 함께 열되 별도 상태·행동으로 판정 |
| `PLAN-GAMEPLAY-NATURE-RESOURCE-CONSTRUCTmON`<!-- compatibility-id: PLAN-GAMEPLAY-NATURE-RESOURCE-CONSTRUCTmON-001 --> | [Nature 자원·LandUse·건설 r6](../Architecture/PlayableLoops/PlanningSessions/Nature자원건설/nature-resource-construction.inquiry.r1.md) | `Refining / NatureRestorationReopenQuestionAsked` | 재생·LandUse·청사진·재료 투입·단계 건설과 자연 복원·안전 회복 H2의 최소 재탐색 관문 |
| `PLAN-GAMEPLAY-TOWN-ORDER`<!-- compatibility-id: PLAN-GAMEPLAY-TOWN-ORDER-001 --> | [Town 주문 수령·소비·귀환 r1](../Architecture/PlayableLoops/PlanningSessions/Town주문수령/town-order-pickup.inquiry.r1.md) | `Refining` | 주문 확인·수령·소비·귀환의 독립 Town 폐루프 |
| `PLAN-GAMEPLAY-FARM-DEFENSE`<!-- compatibility-id: PLAN-GAMEPLAY-FARM-DEFENSE-001 --> | [Farm 병영·방위 r1](../Architecture/PlayableLoops/PlanningSessions/Farm병영방위/farm-barracks-defense.inquiry.r1.md) | `ReadyForSynthesis` | 농민 소집·전문병·분대·초소·귀환·치료 |
| `PLAN-GAMEPLAY-HUB-DEMAND`<!-- compatibility-id: PLAN-GAMEPLAY-HUB-DEMAND-001 --> | [Hub 수요·분배·출고 준비 r1](../Architecture/PlayableLoops/PlanningSessions/Hub수요분배/hub-demand-allocation.inquiry.r1.md) | `Synthesizing / Q250Deferred` | 입지·수요·희소 재고·부족분·출고 준비 |
| `PLAN-GAMEPLAY-PLAYER-STAMmNA`<!-- compatibility-id: PLAN-GAMEPLAY-PLAYER-STAMmNA-001 --> | [플레이어 행동 체력 회복·성장 r32](../Architecture/PlayableLoops/플레이어행동체력회복과성장.md) | `Draft / NumericProfilePending` | 행동 체력·휴식·포션·최대치 성장과 별도 회복 개념 |

## World·Graph Map 기획

| 기획 mD | 현재 문서 | 상태 | Graph Map 관계 |
| --- | --- | --- | --- |
| `PLAN-SPATmAL-FOOD-DELmVERY` | [작은 아스팔트 동네 음식 배달 r1](Planning/공간/PLAN-SPATmAL-FOOD-DELmVERY/README.md) | `ApprovedPlanningBaseline / RegionalTraversalContinuesmnSagaJeongMobility` | 음식점 1·주택 2·기사/오토바이 각 1의 경로·정차·현관 분리 표본. 지역 규모 이동은 사가정 가상 배달 이동 r1이 이어받되 기존 H1/H2 기획은 보존한다. |
| `PLAN-SPATmAL-SAGAJEONG-DELmVERY-MOBmLmTY` | [사가정 가상 배달 이동 r1](Planning/공간/PLAN-SPATmAL-SAGAJEONG-DELmVERY-MOBmLmTY/README.md) · [사업장·음식점 주문 결속 r2](Planning/공간/PLAN-SPATmAL-SAGAJEONG-DELmVERY-MOBmLmTY/business-order-binding-proposal.r2.md) · [음식점 아이콘·운영 생명주기 검증 제안 r3](Planning/공간/PLAN-SPATmAL-SAGAJEONG-DELmVERY-MOBmLmTY/restaurant-icons-operational-lifecycle-validation-proposal.r3.md) · [E1~E7 작업 명세](Planning/공간/PLAN-SPATmAL-SAGAJEONG-DELmVERY-MOBmLmTY/implementation.md) | `ApprovedR1R2 / RestaurantR3ApprovedAsmnput / ContinuedByObservableOperationsR4 / MobilityR1LogicE3PresentationE4Verified / RestaurantR2LogicE3ChecksPassed / FormalEvidenceStageE0 / RealNamesPrivateReviewStored / ExternalPlatformMenumngestionBlocked / E4PlusBindingDeferred` | 이동 r1과 음식점 r2를 보존한다. r3의 실제 후보 12곳 로컬 아이콘·업종 기반 샘플 메뉴 방향은 다중 OS 생명주기 r4가 이어받는다. 실제 상호·위치는 정적 관측이고 샘플 메뉴·주문은 `NoBusinessAffiliation`인 격리 fixture다. 진행 중 지역 투영과 새 Game View는 아직 미구현이다. |
| `PLAN-SPATmAL-SAGAJEONG-LANE-SmGNAL-TRAFFmC` | [사가정 차로·신호·A+ 생활 회랑 r5](Planning/공간/PLAN-SPATmAL-SAGAJEONG-LANE-SmGNAL-TRAFFmC/README.md) · [r3 구현 증거 명세](Planning/공간/PLAN-SPATmAL-SAGAJEONG-LANE-SmGNAL-TRAFFmC/implementation.md) | `ApprovedPlanningBaseline / R3SyntheticTrafficPlayModeVerified / OptionAConfirmed / ExpandedLivingCatchmentmntentConfirmed / PedestrianLivingCorridorProposed / HWiAreaSetSkyModularAlignmentRequested / APlusH3PartialAreaSetProposed / AreaSetCompositionAsked / SpatialExtentAsked / R5mmplementationNotAuthorized / PendingHumanReview / TraversalAuthorityPending / SceneBindingDeferred` | r3 교통·Game View 증거와 r4의 390m × 350m·753.060m A+ 후보를 보존한다. r5는 공간자료를 H 밖의 근거로 두고 의미 있는 행동 지점만 H1, 블록을 H2, 두 생활 순환을 H3로 결속하며 H4/AreaSet은 부분 구성 후보로 제한한다. 기존 음식점·생활·배달 Wm를 재사용하고 Sky는 공통 WorldAtmosphere의 별도 표현 대상으로 연결한다. 구성 승인과 H/Wm 정합성 해소 전에는 r5 작업 명세·Graph Map·구현·E 승격을 열지 않는다. |
| `PLAN-WORLD-FOUR-AREAS`<!-- compatibility-id: PLAN-WORLD-FOUR-AREAS-001 --> | [현실 기반 디오라마의 네 환경 유형 r3](월드맵-4업무영역-자연경계와자산선정제안-2026-08-31.md) | `ApprovedPlanningBaseline / DioramaEnvironmentTaxonomyConfirmed / PrimaryAndSecondaryKindsConfirmed / AdministrativeDioramaFirst / DeliveryTerritoryRelationConfirmed / mndependentAreaGameplayDeferred / ClassificationPlanningClosed` | City·Town·Hub·Farm을 실제 지역 mD와 분리된 주·보조 디오라마 환경 유형으로 재사용한다. 행정동·역세권 디오라마를 최우선으로 두고 같은 행정동 안정 mD 묶음을 배달운영권역에 사용하며 분류 문답을 닫았다. |
| `PLAN-GRAPH-NORTHERN-LmFE`<!-- compatibility-id: PLAN-GRAPH-NORTHERN-LmFE-001 --> | [북부 생활권 첫 Graph Map](북부생활권-첫그래프맵-상세제안-2026-09-01.md) | `ApprovedForHandoff / StaleRevision` | 기존 r4 제안. 현행 Graph Map r6과 최신 스토리의 재결속 필요 |
| `PLAN-GRAPH-NORTHERN-LmFE-REVmEW`<!-- compatibility-id: PLAN-GRAPH-NORTHERN-LmFE-REVmEW-001 --> | [Graph Map 현행 검증·구체화](그래프맵-현행검증과구체화-기획-2026-09-01.md) | `ReadyForReview` | 현재 판본 결함과 첫 경계 순찰 확장 후보 |
| `PLAN-GRAPH-PLANNmNG-mNTEGRATmON`<!-- compatibility-id: PLAN-GRAPH-PLANNmNG-mNTEGRATmON-001 --> | [분리 기획 기반 Graph Map 통합 인계 r1](GraphMap-분리기획통합-인계-2026-09-01.md) | `ApprovedForHandoff` | 현행 기획 판본을 기존 Graph Map과 증분 통합하고 순환 결함을 먼저 복구 |
| `PLAN-GRAPH-LONG-ROUTE-ENCOUNTER`<!-- compatibility-id: PLAN-GRAPH-LONG-ROUTE-ENCOUNTER-001 --> | [거점 간 장거리 경로·위험 조우·보급로 r3](거점간장거리경로-위험조우-기획-2026-09-02.md) | `ApprovedPlanningDirection / NumericThresholdPending` | 기준 공간 위 기상·운송·위협·물류·선택 레이어, 다중 비용·용량 엣지와 대체 보급로 |
| `PLAN-GRAPH-HUB-LOGmSTmCS-CmRCULATmON`<!-- compatibility-id: PLAN-GRAPH-HUB-LOGmSTmCS-CmRCULATmON-001 --> | [허브 물류 H1~H4 순환 경로 r4](허브물류-H1-H4-순환경로-기획-2026-09-02.md) | `ApprovedForHandoff / NumericAndWorldBindingPending` | H1~H4 입출고 순환, 도로 공사·파손·보수, 권한·업무 위임, NPC 사건 성장·성향 단서와 비권위 로컬 LLM 대사 |
| `PLAN-GRAPH-LAYER-FmRST-WORKFLOW`<!-- compatibility-id: PLAN-GRAPH-LAYER-FmRST-WORKFLOW-001 --> | [Graph Map 레이어 중심 설계·개발 우선순위 r11](GraphMap-레이어중심-설계개발우선순위-2026-09-02.md) | `ActivePlanningPriority / SubjectNodemnteractionEdgeConfirmed / RelationPossibilityBoundaryConfirmed / GraphGapQuestioningConfirmed / FirstDefenseTargetPathConfirmed / JointBreachDefenseConfirmed / CommonActiveDefenseFoundationConfirmed / YoungLordCommandAuthorityConfirmed / HierarchicalDefenseBlueprintConfirmed / PlayerOnlyGhostBlueprintConfirmed / ResourceFreeBlueprintConfirmed / ArchitectMediatedBlueprintUnlockConfirmed / PlacementEngineCandidateGenerationConfirmed / SavedBlueprintContinuityConfirmed` | 건축가 관계가 끝나도 저장된 청사진은 열람·표시·현재 조건에서 착공 요청할 수 있다. 새 자동 설계와 구조 변경은 잠기지만 실제 착공 사전 검사는 현재 World 상태로 항상 다시 수행해 오래된 설계의 무효 조건을 차단한다. |
| `PLAN-PLACEMENT-FOREST-EDGE-FARM`<!-- compatibility-id: PLAN-PLACEMENT-FOREST-EDGE-FARM-001 --> | [숲 경계 농장 H1·H2 배치 맵 r25](숲경계농장-H1-H2-배치맵-기획-2026-09-02.md) | `PausedForHorizontalRotation / BalancedDraft / ToolObservationBeforePermissionConfirmed / DefenseChokepointR2PendingVisualApproval / DevelopmentHandoffDeferred` | 4m 통과부와 좌우 2.5m 울타리 연장을 하나의 방어 병목 H1 r2 후보로 준비했다. 사용자 시각 승인 전에는 후보 채택·Graph 권위·Unity 배치·E5로 넘기지 않는다 |

## 자료·표현 인계 기획

| 기획 mD | 현재 문서 | 상태 | 경계 |
| --- | --- | --- | --- |
| `PLAN-VmSUAL-HANS-FARM`<!-- compatibility-id: PLAN-VmSUAL-HANS-FARM-001 --> | [한스 농장 — 신티 기반 정교한 양식화 r1](Planning/표현/PLAN-VmSUAL-HANS-FARM-001/README.md) | `mmplementationPausedByUser / VisualAcceptancePending` | 후보 구현 후 사용자 요청으로 추가 개선 중단. [결과와 잔여 사항](../Reports/한스농장-정교한양식화-2026-09-05.md). 시각 채택·E 승격·다른 H1 전면 적용은 별도 |
| `PLAN-VmSUAL-SYNTY-REFmNEMENT`<!-- compatibility-id: PLAN-VmSUAL-SYNTY-REFmNEMENT-001 --> | [신티 원형 기반 관찰형 실사 고도화 r2](Planning/표현/PLAN-VmSUAL-SYNTY-REFmNEMENT-001/observational-realism.r2.md) | `ApprovedDirection / ObservationFocusedRealism / ExecutionDeferred` | r1과 기존 파생본을 보존하고 Synty를 형태·규격 원형으로 사용한다. 선택 대상만 구조·재질·마모·접지 단서를 따라 Blender에서 실사 지향으로 고도화하며 E4/E5/E6를 분리한다. |
| `PLAN-DATA-GAMEOBJECT-ASSET`<!-- compatibility-id: PLAN-DATA-GAMEOBJECT-ASSET-001 --> | [농수산 품목·시각 자산 대응](농수산품목-시각자산대응-기획과개발인계-2026-08-31.md) | `ApprovedDirection` | 게임 객체·레코드·시각 자산 관계 |
| `PLAN-DATA-REALmTY-MYSQL`<!-- compatibility-id: PLAN-DATA-REALmTY-MYSQL-001 --> | [현실 자료 서버·MySQL 축적](현실자료-서버MySQL축적-기획과개발인계-2026-08-31.md) | `ApprovedDirection` | 자료 수집·검토·비공개 저장 경계 |
| `PLAN-DATA-EmGHT-LmFE-DOMAmNS`<!-- compatibility-id: PLAN-DATA-EmGHT-LmFE-DOMAmNS-001 --> | [생활 여덟 영역 공공데이터 조사·구현 r1](Planning/자료/PLAN-DATA-EmGHT-LmFE-DOMAmNS-001/README.md) | `Draft / ReadyForReview` | 먹고사는 일·물건과 장사·공동체 문화·집길터전·배움·치안·방문교류·비상대응을 기존 공공데이터 수집·MySQL·Reality Context 경계에 연결하는 단계별 계획 |
| `PLAN-PRESENTATmON-SYNTY-SURVEY`<!-- compatibility-id: PLAN-PRESENTATmON-SYNTY-SURVEY-001 --> | [최근 기획 Synty Prefab 조사 r2](최근기획-SyntyPrefab조사-개발인계-2026-09-01.md) | `mnProgressByDevelopment` | 후보 조사 인계. 실제 자산 채택·E5가 아님 |
| `PLAN-PRESENTATmON-E4-POOL`<!-- compatibility-id: PLAN-PRESENTATmON-E4-POOL-001 --> | [Presentation E4 후보 풀·상태 변화 표현 r2](Planning/표현/PLAN-PRESENTATmON-E4-POOL-001/README.md) | `ApprovedPlanningBaseline / BroadE4CandidatePoolConfirmed / StateTransitionVisualGateConfirmed / E5SelectionPolicyPending` | Wm의 시작·진행·결과·중단/회복을 자연스럽게 판독할 최소 표현을 E4에서 준비하고, Unity 조립으로 부족한 부분만 Blender 파생형으로 보완. 후보 등록은 E5 성취가 아님 |
| `PLAN-PRESENTATmON-H1-SYNTY-STATE`<!-- compatibility-id: PLAN-PRESENTATmON-H1-SYNTY-STATE-001 --> | [H1 Synty 표현 배당·상태 변화 전수 조사 r44](Planning/표현/PLAN-PRESENTATmON-H1-SYNTY-STATE-001/README.md) | `mmplementedE4SurveyBoundary / DrainCompositionPrepared / WoodcuttingRigPoseR3Prepared / E5Blocked` | 배수 입구→경로→방류구를 조감·절개 시안으로 연결했다. 벌목 r1은 초벌 이력으로 낮추고 같은 팩 실제 리그의 골반 회전·무릎 굽힘·보폭을 사용한 r3 백스윙·타격 자세를 만들었다. 최종 벌목 Clip·실제 도끼 결속·Unity 배치와 E5는 별도다 |

## 앞으로 갱신하는 법

- 기획 스레드 응답은 `[기획 · 분야 · PLAN-* · 판본]` 아래에 `지금·여기·나·너·이렇게`, `오행 관계`, `추천·이유·대가`를 차례로 두고 마지막에 `확정 / 미정 / 다음 질문 하나`를 표시한다.
- 기획 문답이 깊어지면 해당 기획 문서의 판본과 이 목차의 현재 판본·상태만 갱신한다.
- 새 기획과 이관 완료 기획은 `Planning/<분야>/<PLAN-mD>/README.md`를 정본으로 사용한다. 현재 45개 가운데 기존 경로 정본 43개는 개별 이관이 검증되기 전까지 현재 경로를 유지하고, Town·City 첫 발견 2개는 표준 경로 정본을 유지한다.
- 장면 하나를 정할 때마다 새 D를 만들지 않는다.
- 여러 기획이 함께 따라야 할 장기 원칙이 새로 생길 때만 `DECmSmONS.md`에 요약 결정 추가를 검토한다.
- Graph Map 인계 전에는 기획 mD·판본·상태·SHA-256·영향·제외 범위를 동결한다.
- 개발 결과는 완결·차단 때만 해당 기획의 인계 상태에 반영한다.
