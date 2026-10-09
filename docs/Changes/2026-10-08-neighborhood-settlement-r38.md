# 생활 거래 정산 서버·원장 r38

상태: `Implemented / LocalValidated / ServerAndLedgerOnly / AppAndOperationalDeferred`. [승인 r32](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/neighborhood-settlement.implementation.r32.md)·[페이지·서버·DB](../ProjectOverview/page-docs/neighborhood-settlement-r38.md)를 연결한다.

## 변경

음식·보관 상품·운송을 구별하고 상품대금·보관료·입고/피킹비·배송료의 약정, 실제 비용 발생, 양측 확인, 지급 신고, 수령자 확인을 나눈다. 소유자 직접 수금과 판매자 수금은 약정별 선택이며 전달액·내부 배분은 경제 수입을 이중 집계하지 않는다. 미확정 금액은 0으로 채우지 않는다.

기존 업무 원장에서 실제 당사자·동결 가격·완료 수량을 읽는다. 같은 원본 실적을 약정명·판본·항목명·지급자를 바꿔 중복 발생시키거나 겹치는 보관 기간을 재청구하지 못하도록 검증한다. 취소/반품은 기존 항목을 덮어쓰지 않는 조정이고 사진/배송 완료와 지급 완료는 별개다.

약정·판본·항목·지급 신고·영수증 5개 테이블과 보호 JSON, 새 migration 코드를 추가한다. 인증된 서버 API와 기존 DI에 연결하되 기본 OFF·정확한 profile·Simulation 전용이다. 기존 음식/화물 지급 원장을 수정하거나 새 송금을 만들지 않는다.

## 검증

작업 근거는 Git 제외 `artifacts/local/neighborhood-settlement-r38-20261008/`에 보관한다.

- 최종 Fast `artifacts/local/validation/20261008-165706`: `Ssalddel.v3.5.slnx` 빌드와 집중 시험 **48/48 통과**.
- 최종 Task `artifacts/local/validation/20261008-165907`: 같은 solution 빌드와 전체 시험 **8,986 통과 / 실패 0 / 환경 의존 MongoDB 시험 2 건너뜀**.
- 격리 SQLite와 실제 로컬 Kestrel HTTP에서 익명 401, 범위 밖 계정 404, 요청 재실행과 원본 실적 연결을 확인했다. 입고 수량 3 × 합의 단가 100 = 300원, 지급 신고 100원, 수령자 확인 100원, 남은 금액 200원을 별도로 재조회했다. 출력은 `root/http/inbound-payment-recipient-confirmed.json`이며 은행 확인은 false다.
- 정산·저장 시험은 판본 재동의, 다당사자 역할과 원본 범위, 완료량/기간 상한, Lot↔주문 중복 청구, 공동 배송비 부담, 무료/미확정, 판매자 총액 전달, 원장 참고 중복 방지, 부분 지급·환급·추가 정정, 내부 항목 제외, 저장 보호를 검증했다.
- 새 migration은 격리 SQLite에서 5개 테이블을 생성해 검사했다. MySQL은 서버 연결 없이 실제 모델과 migration snapshot을 대조했고 JSON 보호 변환을 확인했다. MySQL 실서버 migration 적용을 인증하는 결과는 아니다.
- 시험 전후 컴파일 대상 **22개 파일의 SHA-256이 동일**하다. 관련 없는 기존 변경 파일 **15,105개는 변경 0개**로 대조했다. 새 문서의 상대 링크 7개와 좁은 범위 diff 검사를 통과했다.

초기 Fast `20261008-162335`는 서비스 partial 작성 중 인터페이스 미완성으로 실패했다. 첫 전체 작성본 Fast `20261008-164716`는 38 통과·6 실패였으며, 배열 조회의 EF 표현식 오류, 시험 자료의 누락 관계, 보호 시험의 모델 캐시 혼용을 수정했다. 실패 로그를 보존하고 최종 통과 결과와 구별한다.

## 범위와 남은 작업

앱 화면·APK·Unity·배달 영상·공용 DB migration 적용·실제 금전·Azure·휴대폰 시험·commit/push는 실행하지 않는다. 이전 r37 로컬 검증은 보존하며 사용자의 앱 작업 보류에 따라 r37 APK 작업을 중단했다. 주문자/화주/화물기사 3개 패키지 로그는 있지만 창고 앱과 4개 manifest 완료 증거는 없으므로 네 앱 준비 완료로 보고하지 않는다.

분산 원장에 과거 상품 총액이 없으면 ProductSale은 미확정으로 남는다. 음식점 운영 접근권만으로 물품 소유자를 지정하지 않는다. 주문별 수량/처리자를 복원할 수 없는 묶음 피킹과 개별 수령 시각은 임의 생성하지 않는다. 직접 계좌이체는 당사자 진술이며 `IsActualBankVerified=false`를 유지한다. 전체 시험과 로컬 HTTP 시험은 실제 입금, 일반 운영 환경, 앱 화면 또는 휴대폰 검증을 대체하지 않는다.
