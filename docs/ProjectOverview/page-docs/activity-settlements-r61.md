# 활동 정산 경제 정보 책임 r61

[승인 범위](../../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/settlement-economics.implementation.r61.md)·[이전 입고/출고 화면](activity-settlements-r60.md)을 따른다.

기존 `/activity-settlements/{agreementId}`에서 ‘내 보수와 분배 조건은 무엇인가’를 확인한다. 기본 보수는 기존 항목으로 유지하고, 선택 경제 약정만 입고·보관 연결 비용 총액, 출고 기본 작업 비용, 비용 차감 후 잔여금, 플랫폼 보유 비율·몫, 참여자 성과금 풀을 구분한다. 5%/10%는 잔여금 기준이며 매출 수수료나 실제 영업이익률이 아니다.

서버의 미확정 값을 UI가 0으로 보완하거나 원가를 다시 계산하지 않는다. 본인 배분만 표시하고 비용 원천 ID·재고 ID·사용자 ID·사진 원문은 숨긴다. 원가 상세가 권한 필터로 비었으면 0건이라고 표시하지 않는다. 과거 발생 계산은 당시 적용한 비율로 표시하고 현재 약정 입력과 구분한다.

화면은 `활동성과금Panel`/`활동정산표시`, 서버는 `NeighborhoodSettlementUseCase`의 `CostAttribution`/`ActivityRewards`/`PlatformShare`, DB는 기존 정산 항목·명령 영수증과 추가 원가 귀속 기록이다. 상태 확정·다른 업무 접근·실제 송금은 화면 책임이 아니다. 신규 route나 정책 편집·실제 지급 버튼을 추가하지 않는다.

검증 대상은 HtmlRenderer이며 브라우저·APK·휴대폰·운영 서버 검증과 구분한다. 실행 결과는 [변경 기록](../../Changes/2026-10-12-settlement-economics-r61.md)에 남긴다.
