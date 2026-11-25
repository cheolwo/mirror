# 서울·경기 이동과 복귀 추천 검증 r23

상태: `ApprovedByUser / VerificationOnly / VerifiedWithGaps / OperationalDisabled`. 2026-10-28 사용자가 서울에서 경기로 이동하며 콜을 계속 받는지, 귀가할 때 복귀콜을 받는지 검증하도록 요청했다. 이전 [지도 기기 확인 r22](cargo-ordered-map.device-review.r22.md), [고정 활동권역 r3](cargo-map-first-workloop.design.r3.md), [시간 약속을 지키는 순서 r20](cargo-feasible-stop-order.r20.md)을 대조했다. 실제 범위·결과·미완성 연결은 [검증 기록](../../../../Changes/2026-10-28-cargo-cross-region-return-r23.md)에 남겼다.

## 이번 실행

현재 추천·복귀 판정·기기 설정 연결을 먼저 감사하고 기존 회귀를 실행한다. 서울권→구리권→남양주권→서울 방향의 합성 좌표와 명시적인 모의 도로 응답으로 제품 추천 서비스/복귀 서비스를 함께 호출하는 좁은 회귀를 추가할 수 있다. 실제 거주지·계정·고객 자료는 사용하지 않는다. 지역 이름은 시험 구분이며 지오코딩이나 실제 주행 증거가 아니다.

검증할 항목은 경계 이동 후 현위치의 픽업 검색, 거절/다른 기사 전용 후보 제외, 집 기준점 불변, 가까워지는 전체 일정 끝점의 복귀 가점, 멀어지는 후보·늦은 픽업·위치/도로 미확인의 구분이다. 기존 설정에서 보장하지 않는 고정20/50km 활동 제한이나 개인 귀가 마감은 미구현으로 기록하며 시험으로 새 기능을 만든 것처럼 표현하지 않는다.

허용 쓰기 경로: `Ssalddel.Tests/Services/Dispatch/Recommendation/CargoCrossRegionReturnR23Tests.cs`, 이 문서, `docs/Changes/2026-10-28-cargo-cross-region-return-r23.md`, `docs/AI/CURRENT_WORK.md`, `docs/AI/PLANNING.md`, `docs/Changes/README.md`, `artifacts/local/cargo-cross-region-return-r23/`. 제품 코드·계약·권한·추천 플래그·운영 DB는 변경하지 않는다. 기존 dirty 변경과 개인 산출물은 보존한다.

## 완료 근거

직접 영향 회귀 Fast와 작업 범위 Task의 실제 선택/성공/실패/생략, 소스 기준선과 허용 신규 파일, 시나리오별 실제 결과와 불충족 사항을 보고한다. 모의 경로·메모리 DB의 서비스 실행은 실제 NAVER 경로·인증 HTTP·휴대폰·FCM·GPS·운영 배차 증거가 아니다. 이번은 서버 계산/설정 연결 검증을 우선하며 기기 화면은 별도 실행했을 때만 완료로 표시한다. commit/push·배포·운영 활성화는 포함하지 않는다.
