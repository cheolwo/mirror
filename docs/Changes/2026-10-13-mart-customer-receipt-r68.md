# 마트 고객 수령과 기존 보수 지급 조건 r68

기사 전달만 있던 마트 흐름에 실제 주문자의 전량 수령과 부분/누락/파손 보류를 별도로 기록한다. 기존 마트배송시도에 보호 근거 한 열을 추가하고 현재 전문 원본을 대조해 기존 정산 지급 관문에 연결했다. 구현·로컬 검증을 완료했다.

## 변경과 경계

숫자 마트주문 ID의 실제 주문자만 수령 조회·확인·문제 신고·요청 결과 확인을 수행한다. 현재 시도 판본 CAS와 Serializable transaction, 주문/시도별 요청 ID·입력 지문을 사용한다. 기사 상태 `Delivered`와 주문·루트 원장은 수령 상태로 덮어쓰지 않는다.

공통 Reader가 현재 배송시도·사용된 전표·상품별 출고 수량·픽업 당시 실제 재고 이력을 검증한다. 픽업 준비 상태를 배송 후 다시 요구하지 않는다. 여러 시각 호출로 생긴 정상 시각 차이는 수락→출고→픽업→전달 순서로 검증하고 동결 지문에 남긴다.

수령 뒤 문제 신고도 기존 확인 근거·보수·지급 기록을 보존하며 새 양수 지급을 보류한다. 자동 해제·대리 확인·환불 확정·실제 송금·성과금·새 단가를 추가하지 않는다. 고객 수령은 사전 합의용 원천 판본을 바꾸지 않는다.

검토에서 SQL `datetime(6)`와 창고 완료 JSON의 100ns 정밀도 차이를 확인해, 신규 피킹·포장 시각을 저장 전에 UTC 마이크로초 단위로 맞춘다. 근거의 정확한 일치 검사는 유지하며 과거 기록을 일괄 수정하지 않는다. 저장된 요청 존재와 현재 수령 검증도 분리해 이후 취소/출고 변경에서 과거 요청을 없어진 것으로 표시하지 않는다.

## 검증 상태

신규 흐름과 정밀도 회귀 집중 시험 45개가 통과했다. 실제 피킹·포장·QR 픽업·기사 전달·고객 수령·기존 명시적 지급 신고를 연결하고, 부분/누락/파손 보류 뒤 기존 보수·지급 보존과 음수 환급을 확인했다. 동일 요청·낡은 판본·권한·손상된 근거·현재 원천 변경 뒤 요청 복구·증분 시계·암호화 저장·격리 migration도 포함했다. 근거는 `artifacts/local/mart-customer-receipt-20261013/focused-r2/`다.

Fast 집중 시험 347개도 통과했다. 소비 앱을 포함한 `Ssalddel.v3.5.slnx` 빌드는 오류 0개·기존 경고 61개다. Fast 근거는 `artifacts/local/validation/20261013-125311/`다.

구형 DB 시험 보완 후 DB 호환 Fast 4개도 통과했다. 근거는 `artifacts/local/validation/20261013-130507/`이며 이 실행의 빌드는 오류 0개·경고 147개였다.

최초 빌드의 시험 namespace 누락과 집중 시험의 필수 증빙 참조·수령 진술 사유 누락은 시험 입력을 보완해 해결했다. 첫 전체 Task에서는 초기 r58 테이블의 열만 현재 모델과 비교하던 구형 DB 시험 한 건이 실패했다. 과거 migration은 유지하고 후속 수령 열을 합친 전체 열 이름·타입·null 가능 여부·판본을 검사하도록 시험을 보완했다. 실패 로그와 TRX는 보존하고 정산 조건은 완화하지 않았다.

최종 Task는 11,236개 통과·실패 0개·환경 의존 MongoDB 15개 건너뜀이다. 최종 소비 앱 빌드는 오류 0개·기존 경고 61개다. 근거는 `artifacts/local/validation/20261013-130754/`다. 문서 로컬 링크 11개와 범위 밖 기존 파일 171개 해시 보존, 소스 27개 해시를 확인했다. 증거 원장은 `artifacts/local/mart-customer-receipt-20261013/completion-evidence.json`이다. stage·commit·push는 하지 않았다.

실제 MySQL 왕복, 서로 다른 실제 동시 HTTP 요청, 고객 수령과 늦은 Outbox를 결합한 운영 실행, 실제 DB 적용·운영 활성화·앱 화면·APK 설치·단말·Azure/GCS·Unity·commit/push는 별도다. 낡은 DB 수정의 CAS와 후속 투영의 수령 근거 미변경은 로컬 시험·정적 코드 검토 범위로 구분한다.

- [수용 설계](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mart-customer-receipt.implementation.r68.md)
- [작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mart-customer-receipt.r68.work-order.json)
- [API→코드→원본](../ProjectOverview/page-docs/mart-customer-receipt-r68.md)
