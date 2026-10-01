# 음식점 앱 업무·결제·정산 점검 r3

- 날짜: 2026-09-27. 코드·집중시험 점검이며 실제 PG·지급·운영 DB·Android 실기기 실행은 없다.
- 합의: 주문 첫 화면, 하단 주문/메뉴/가게, 메뉴 등록·수정 별도 화면과 저장 후 목록 복귀. 아직 제품 UI 미반영이다.

## 현재 코드 대조

| 항목 | 현재 상태 |
| --- | --- |
| 메뉴 | Menus.razor와 인증 GET/POST/PUT 존재. 목록과 폼은 같은 화면이다. |
| 탐색 | MainLayout은 서랍, Home은 운영 홈이다. 하단 3탭과 주문 첫 화면 미구현. Release 허용 경로에 가게/정산 전용 화면이 없다. |
| 주문 | OrderDetail에 수락·거절·조리시간 변경·픽업 준비·재조회가 있다. 기사 픽업·전달 권위는 기사 업무다. |
| 알림 | 음식점주문SignalRClientService에 인증 연결·재연결·재조회 요청 존재. Android 종료/백그라운드 수신 미검증. |
| 정상 주기 | 음식배달정상수직생명주기Tests는 SQLite·실제 Handler로 수령확인까지 검증하지만 추천 운송을 직접 만들고 NoOpSettlement·NoOp 알림을 사용한다. 결제·정산 종단 시험이 아니다. |
| 결제 | 음식주문결제승인완료EventHandler는 FoodOrderPaymentApproved 의도를 Command알림Outbox에 적재한다. 같은 문자열의 음식 주문 적용 소비자는 서버 소스 검색에서 확인되지 않았다. 적재만으로 결제 반영·지급 완료를 주장할 수 없다. |
| 환불 | 주문자음식주문취소CommandHandler는 취소·활동·Event 기록을 수행한다. 이 Event에서 음식 주문 환불까지 연결하는 소비자는 검색에서 확인되지 않았다. |
| 정산 | 음식배달기사업무Service의 I기사월정산Service는 배차 건수·기사 이용료 계산이며 음식점 판매대금/기사 배달대금 지급이 아니다. 기사지급준비UseCase는 화주운송의뢰 기반 화물 경로다. RestaurantDeskApp에 음식점 정산 화면/client는 확인되지 않았다. |

## 실행 검증

- `artifacts/local/validation/restaurant-lifecycle-audit/restaurant-lifecycle-audit.trx`: 화면 구성·메뉴·정상주기·생명주기 조화·결제 Outbox 43/43 통과.
- 같은 폴더 `restaurant-food-audit.trx`: Application.Food와 메뉴검증·음식점 접근범위·조리시간 정책/Service 69/69 통과.
- 두 filter에 중복이 있어 독립 112건으로 합산하지 않는다. 첫 실행 build 후 두 번째 같은 산출물 --no-build/--no-restore 사용.
- 실제 인증 HTTP·MySQL/MongoDB·PG·실기기 검증 성공으로 확대하지 않는다.

## 다음 순서

1. 합의한 탐색·별도 메뉴 입력 화면 적용.
2. 샘플 주문 한 건의 결제 승인→음식점 처리→배달→정산 대상 생성→음식점 조회를 같은 주문번호와 금액 근거로 대조. 실지급 없이 결제·수수료·환불/보류·지급대상·지급완료 구분.
3. 취소/거절 환불, 중복 승인/Outbox, 응답 유실, 지급 보류 검증. 금액·정산 주기 임의 확정 금지.
4. 정산 화면은 가게 안에서 상세 진입하는 후보로 둔다. 위치는 미확정이다.

이번 변경은 점검 문서뿐이며 제품 코드·금융 정책·commit·push·배포 변경 없음.
