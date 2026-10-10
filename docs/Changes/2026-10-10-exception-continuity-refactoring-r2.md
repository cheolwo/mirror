# 예외 복구 코드 책임 분리 r2

상태: `Implemented / LocalValidated / DeviceAndOperationalDeferred`. r1 및 다른 작업의 수치를 r2 검증 결과로 재사용하지 않는다.

## 변경 목적

오늘 보완한 예외 복구 동작을 유지하면서 입력·요청 준비·역할별 완료 근거·화면 표시의 책임을 분리한다. [사용자가 승인한 r2](../AI/Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.refactoring.r2.md)와 [작업 지시](../AI/Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.r2.work-order.json), [페이지 책임](../ProjectOverview/page-docs/exception-continuity-refactoring-r2.md)이 현재 범위를 소유한다. [r1 구현과 기존 한계](2026-10-10-exception-continuity-hardening.md)는 보존한다.

## 책임 분리

- `FoodRoleCompletionExpectation`: 기존 진입점 뒤에서 주문자 취소·음식점 수락·진행 변경·기사 중단·운영자 검토의 완료 근거를 개별 내부 함수로 판독한다. 원래 판정식을 유지하며 단순 상태 일치를 요청 성공으로 확대하지 않는다.
- `FoodRoleRequestPreparation`: 입력과 읽은 원장 근거의 값 사본으로 요청 본문을 준비한다. 기존 화면 ViewModel이 계정·세대·취소·전송·정본 확인·동일 요청 재시도를 조율한다.
- 음식 역할 입력 구성요소는 기존 폼의 같은 위치에서 렌더한다. 문구·입력 제한·DOM 순서·주 행동을 유지하며 자식 DOM의 CSS isolation과 부모 확인 요약 갱신을 대조한다.
- `MemberDeliveryRecoveryState`: 원 배송/사건 문맥·고정 pending·응답/조회 근거·선택/확인을 소유한다. 부모 지원 ViewModel이 계정/취소/세대·상호 요청 차단·API·오류/안내를 조율하고 Panel은 기존 두 표시 위치를 조립한다.

공개 API/contract·route·DB·저장 형식·만료 정책을 바꾸지 않았다. 음식/운영자 미확정 요청은 기존 화면·계정 수명을 유지한다. 물류 접수·분산 사진·보호 저장 경계를 재추상화하지 않았다.

## 검증

| 실행 | 이번 결과 | 로컬 근거 |
| --- | --- | --- |
| 관련 Fast | 116건 통과, 실패·건너뜀 0 | `artifacts/local/validation/20261010-203326/` |
| 전체 Task와 공유 UI 소비 앱 | 10,328건 통과, 실패 0·Mongo 환경 의존 15건 건너뜀. `Ssalddel.v3.5.slnx` build 오류 0 | `artifacts/local/validation/20261010-204148/` |
| 별도 출력 preview build | 오류 0 | `artifacts/local/exception-continuity-refactoring-r2-20261010/preview-build-isolated.log` |
| 실제 제품 컴포넌트 화면 | 390×844, 37항목 통과, 실패·브라우저 오류 0 | `artifacts/local/exception-continuity-refactoring-r2-20261010/ui/verification.json` |

신규 회귀 사례는 요청 준비 12건·역할 입력 렌더 8건·운영자 배송 복구 9건이다. 관련 Fast는 기존 완료 근거·응답 유실·GET 실패·동일 요청 재시도·계정 변경·늦은 응답 시험까지 함께 실행했다. 독립 정적 대조에서도 역할별 완료 근거의 기존 조건과 보조 판정이 유지되는 것을 확인했다.

화면 검증은 입력 복귀 시 값 보존, 음식점 즉시 픽업/조리시간 변경의 부모 요약 갱신, 운영자의 명시 확인, 저장 후 응답 유실→GET 영수증 확인, 미저장 조회 뒤 같은 번호/본문의 명시 재시도를 확인했다. 운영·외부 HTTP 시도는 0이며 모든 자료는 격리 메모리 fixture다. 대표 화면은 [음식점 요청 확인](../../artifacts/local/exception-continuity-refactoring-r2-20261010/ui/restaurant-immediate-confirmation-390.png)과 [운영자 복구 재확인](../../artifacts/local/exception-continuity-refactoring-r2-20261010/ui/support-explicit-retry-ready-390.png)이다. 실제 단말·운영 자료 검증이 아니다.

변경 전 주문자 세 장면의 DOM·PNG와 원본 CSS는 `before/`에 보존했다. 처음 확보한 화면은 제품 스타일 전체가 미적용된 상태여서 스타일 동일성의 기준으로 사용하지 않았다. 초기 DOM은 변경 후와 일치하며, 입력 스타일은 원래 DOM·원래 CSS isolation 선택자에 동일한 테마 문맥을 적용한 별도 정적 참조와 대조했다. `ui/original-markup-css-reference.json`·`ui/reference-*-390.png`에 방법과 결과를 남겼다. 이 참조는 과거 대화형 런타임을 복원했다는 증거가 아니다.

첫 Fast는 새 시험 2건이 기존 안내문을 항상 null로 가정해 실패했다. 화면 전환/종료 전의 안내를 보존하면서 늦은 응답이 이를 바꾸지 않는지 검증하도록 시험을 바로잡았으며 제품 동작은 변경하지 않았다. 첫 UI 실패는 기준선 스타일 누락과 렌더 대기 결손이었고, 참조 화면 테마를 맞춘 뒤 토글의 렌더 완료를 기다리도록 검증기를 보완했다. 초기 실패 기록은 `20261010-203128/`, `ui-initial/`, `ui-rerun.log`에 보존한다. 일반 preview 출력은 기존 다른 호스트의 파일 잠금으로 빌드하지 못했으므로 그 호스트를 종료하지 않고 별도 `preview-runtime/` 출력으로 검증했다.

검증 대상 소스·시험·호스트 18개의 검증 전후 지문은 `validation-final-before.json`·`validation-final-after.json`에 기록했고 차이 0건이다. 실제 화면 실행 DLL 4개의 지문은 `preview-runtime-hashes.json`에 기록했다. 승인 당시 설계 hash와 개발 반환이 추가된 현재 hash는 작업 지시에서 구분한다. 확인용 preview 호스트는 종료했으며 다른 실행 중 호스트와 작업 변경을 보존했다. 기존 의존성·컴파일 및 Firebase 미설정 안내 경고는 남는다.

## 남은 범위

화주 Mongo 생성→SQL 투영→운임/인계 부분 완료 복구는 별도 동작 변경 후속이다. r1의 C04 사진 업로드·실제 보호 저장·단말·카메라·지도·인증 운영 HTTP/DB/Outbox·실배차·알림·입금 검증도 유지한다. Azure·실기기 설치·영상·MYBOX/SNS·Unity·commit/push·배포는 이번에 수행하지 않는다. 다른 작업의 기존 변경은 보존한다.
