# 입고·보관 공통 UI 구조 정리 r65

상태: Implemented / LocalValidated / OperationalDeferred.

사용자는 기능 확장이 커졌으므로 정리를 먼저 하도록 요청했다. [수용 범위](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-cleanup.implementation.r65.md)·[작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-cleanup.r65.work-order.json)에 따라 최근 공통 UI 안의 실행 책임과 표시 중복만 정돈한다.

## 정리 내용

- 분산이행PageViewModel의 주문 후보·선택·추가·취소·수령을 Orders partial에, 사진 촬영·검증·업로드·완료·재개를 Photos partial에 모은다. 기존 클래스와 private 상태를 공유하며 새 서비스·interface·DI를 만들지 않는다.
- 계정·수명·조회·일반 요청/영수증은 공통 파일, 저장/재확인은 기존 Recovery partial이 계속 소유한다. 요청 번호·계정 변경 차단·현재 판본·사진 결속과 저장 형식은 바꾸지 않는다.
- 입고·공간 패널에서 반복한 표시를 기존 분산이행표시 파일로 모은다. 일반 업무 상태와 입고 협의 상태의 다른 문구, 문맥별 미확인 문구, InboundReason의 null 반환과 HTTP 오류 안내는 보존한다.
- 페이지/카드/입력/버튼/단색 아이콘/CSS·route는 유지한다. 새 원장·기능·운영 옵션을 추가하지 않는다.

## 검증

- 공통 파일은 552행에서 330행으로 줄이고 주문 128행·사진 114행을 같은 클래스의 별도 파일로 옮겼다. 기존 멤버 90개의 서명·본문·초기값을 대조해 추가·누락·중복·변경이 없음을 확인했다.
- 중복 표시 함수 9개와 읽기 전용 선택 목록 4개를 기존 표시 파일로 모았다. 선택 순서와 문맥별 미확인 문구를 유지한다.
- Fast 225건 통과, 실패 0건. Task 11,167건 통과, 실패 0건·환경 의존 MongoDB 15건 건너뜀. 전체 소비 앱 빌드 오류 0개·경고 145개이며 이번 변경 파일의 빌드 경고는 없다.
- 기존 Razor 출력 10개는 정리 전후 바이트와 SHA-256이 모두 동일하다. CSS 3개와 서버·계약·DB·복구 등 보호 파일 38개도 해시가 같다.
- Fast 기록은 `artifacts/local/validation/20261012-203252/`, Task 기록은 `artifacts/local/validation/20261012-203513/`다. 이동 대조·화면 전후 사본·해시·문서 링크 검사와 최종 범위 검사는 `artifacts/local/inbound-r65/`에 보관한다.

[기존 r64 대표 화면](2026-10-12-inbound-visual-r64.md)을 새 디자인으로 바꾸지 않으며, 이번에 단말 화면을 촬영하거나 APK를 설치한 것으로 보고하지 않는다.

## 후속 보완과 경계

입고 전체 상태의 처리 순서 의존, 보관 변경안의 작성 입력 보존, 조회/실행의 용량 판정 일치, 현재 업무 중심 작성 진입, 목록/상세 조회 최적화는 별도 보완으로 남긴다. 구조 이동에 이 동작 변경을 섞지 않는다.

기존 서버/계약/DB·기본 OFF·Simulation·관련 없는 미커밋 변경은 보존한다. 실제 DB·인증 HTTP·휴대폰·Azure·Unity·계약/지급·commit/push는 이번 정리 범위 밖이다.
