# 운영 알림 페이지 책임 r45

| 페이지 | 한 가지 책임 | 코드 | 저장/조회 |
|---|---|---|---|
| `/operations-alerts` | 긴급·확인 중·해결됨 목록을 탐색 | `OperationsAlerts.razor`, `OperationsAlertsView`, `OperationsAlertsState` | 관리자 API → `operations_alerts` |
| `/operations-alerts/{AlertId}` | 최신 상태 확인과 해당 업무 이동 | 같은 호스트의 상세 분기 | 알림/채널 outbox, 원장 검증 시각 |
| 기존 음식·지원·운송 페이지 | 실제 업무를 처리 | 기존 역할 use case 유지 | 기존 주문/운송/지원 원장 |

목록: 로그인 복구 → 관리자 권한 → 목록 읽기 → 상세. 상세: 최신 읽기 → 해당 업무 보기 또는 알림 확인. 확인 command는 request ID와 expected revision을 요구한다. 알림 확인과 배송 완료·사건 종결·입금 완료는 별개다.

공통 UI는 MVVM을 사용한다. 로그인 계정 변경/화면 폐기 시 응답 세대를 무효화한다. 로딩·빈 목록·조회 실패/재시도·기능/외부 발송 OFF·오래된 확인·동시 변경을 표현한다. 원문 주소·고객/사진 정보는 이 화면에 복제하지 않는다.

DB: 알림은 문제 ID/발생 회차/상태/확인 revision, outbox는 채널별 발송·임대·재시도, checkpoints는 source cursor/최초 가동, receipts는 확인 command 재실행을 저장한다. `20261009140000_AddOperationsAlerts`는 기존 상태를 변경하지 않는 추가 migration이다.

Android 설치와 서버 관리자 권한은 독립이다. FCM의 `ssalddel.admin` 설치는 authenticated installation API로 등록하고, 발송/수신 모두 계정을 대조한다. Android 13 알림 권한은 사용자가 앱에서 허용하며 OS 설정의 소리/방해금지는 강제하지 않는다. 설정 파일이 없으면 APK는 로컬 시험용으로 빌드하되 실제 푸시는 연결되지 않는다.
