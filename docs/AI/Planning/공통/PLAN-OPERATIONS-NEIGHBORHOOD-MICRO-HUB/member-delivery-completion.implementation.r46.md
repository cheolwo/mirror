# 회원 생활 배송 한 건 완주 구현 r46

- 기획 ID: `PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB`
- 상태: `Approved / Implementing`
- 승인: 2026-10-09 사용자가 한 건 완주·협업 당사자 한정안을 선택한 뒤 `Implement the proposed plan.` 요청.
- 기준 commit: `d1bab3eec08dc0a49c9a87f882590d394205c9a4`
- 선행: [운영 시나리오 점검 r44](operating-scenarios.review.r44.md), [회원 배송 r39](member-delivery.implementation.r39.md).
- 판본 구분: 대화에서 승인한 r45 구현 범위를 기록한다. 별도 운영 알림 r45를 덮어쓰지 않도록 저장소 판본은 r46을 사용한다.

## 승인된 범위

기존 물품 제공/필요 글의 양측 협업에서 회원 배송을 접수하고, 배정 회원의 첫 픽업 사진→픽업→첫 전달 사진→전달 뒤 실제 수령자가 확인하여 같은 협업을 완료한다. 수령자는 `ProviderRoleCode`의 반대 당사자이며 standalone 의뢰의 수령자 위임은 포함하지 않는다.

1. 서버 응답에 촬영 가능 단계 `EvidenceStageCode`를 추가한다. 사진이 없는 상태에서도 촬영은 가능하고 `CanPickup/CanDeliver`는 사진 전송·검증 뒤 완료 권한으로 유지한다. 업로드 뒤 정본 단계와 허용 행동을 재조회한다.
2. 협업 연결 회원 배송은 professional `DispatchMode=null`을 허용한다. 전문 기사/외부 수행 혼합을 거부하고 source·협업·조건 판본을 연결하며 연결 확정 전 제안·수락을 열지 않는다. 같은 접수 키로 기존 결과를 회복한다.
3. 기존 협업 `confirm-completion`을 실제 수령자에게만 허용한다. Mongo CAS 수령 intent로 충돌을 차단하고 SQL의 전달·사진·동일 협업 조건을 검증한 뒤 수령 사건/receipt를 저장한다. 같은 키로 중단을 회복한 후 협업 완료와 운송 원장 투영을 연결한다. 전달·수령·지급은 분리한다.

## 작업 지시와 소유 경계

주 상태 소유자는 `operations`다. [작업 지시서](member-delivery-completion.r46.work-order.json)의 파일 경계 안에서만 구현한다. 공유 계약·회원 Work/수령·협업 UseCase·DI·문서는 통합 담당, 촬영 UI/시험은 UI 담당, 접수·출처 parser·제안/시험은 접수 담당이다. 기존 운영 알림 dirty와 다른 작업은 보존하며 공용 문서는 마지막에 다시 읽어 최소 병합한다.

## 검증 상한과 제외

- 자동 시험: 첫 사진의 실제 서버 DTO 조건, 제공/필요 수령 권한, 중복·낮은 판본·사진/응답 유실·동의/기한·부분 저장 회복.
- 실행: 격리 MySQL/Mongo 인증 HTTP, 실제 Razor 모바일 320/390 렌더, 소비 앱 build 및 개인 Debug APK 준비를 각각 독립 증거로 남긴다. 환경에서 실행하지 못한 항목은 미검증으로 명시한다.
- 기본 OFF와 `Simulation` 유지. 운영 DB 적용·실결제/지급·원격 푸시·APK 설치·실기기·Unity·배포·commit/push는 제외한다.
- 정산 UI나 외부 제공사 등 신규 기능보다 이 한 건의 접수·수령 결과를 우선한다.

## 구현 및 실행 증거

작업 종료 시 변경 기록과 CURRENT_WORK에 실제 시험/HTTP/UI/APK 증거 및 남은 제한을 연결한다. 자동 시험 성공을 휴대폰·실운영·실지급 완료로 해석하지 않는다.
