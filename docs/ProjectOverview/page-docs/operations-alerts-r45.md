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

## r21 미해결 대응과 책임 분리

기본 목록은 `Unresolved`로 확인 전과 확인한 미해결 건을 함께 유지한다. 해결 전인 긴급 건·오래된 건을 페이지 나누기 전에 정렬한다. 관측·확인 명령·발송은 기존 coordinator에 남기고 조회는 `OperationsAlertQueryService`, 대응 안내는 `OperationsAlertHandlingPolicy`, 화면 문구·안전한 복귀 링크는 `OperationsAlertWorkflowPresentation`이 소유한다.

확인 명령 응답 유실 뒤에는 공통 복구 창에서 GET 재조회만 진행한다. 로그인·사건·필터 변경 시 이전 화면 응답을 무효화하며 목록 필터·페이지를 복귀 링크로 보존한다. 지원·음식은 기존 사건/주문으로, 화물의 공통 링크는 `운영 현황`으로 표시한다. 아직 없는 관리자 통합앱 화물 상세 화면을 연결 완료로 표시하지 않는다.

원장 재조회 시각과 운영자의 다음 검토 시각을 나눈다. 6·24·누적72시간 서비스 목표는 거래 분쟁에만 표시하며 근무시간 기준이 정해지기 전 실제 처리 기한을 생성하지 않는다. [r21 책임 카드·단독 대응 순서](cargo-operating-verification-r21.md)와 [실행 결과](../../Changes/2026-10-22-cargo-operating-verification-refactor-r21.md)를 따른다.
