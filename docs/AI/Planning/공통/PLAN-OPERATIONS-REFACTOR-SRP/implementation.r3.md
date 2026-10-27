# 운영 앱·서버 후속 리팩토링 r3 구현

2026-10-21 · `ApprovedInCurrentThread / CompletedLocalValidation`

사용자의 “계속진행”에 따라 직전 권고의 **1번 첨부파일 처리·2번 푸시 설치 HTTP 전송**을 완료했다. [작업 명세](implementation-work-order.r3.json)가 이번 두 항목의 구현 범위와 보존 조건을 소유한다. [14개 조사 제안](follow-up-survey.r3.md)은 당시 ProposedOnly 이력으로 보존하며, 나머지 **3~14번은 이번에 구현하지 않았다.**

## 무엇을 분리했는가

| 항목 | 새 책임 | 기존 객체에 유지한 책임 |
| --- | --- | --- |
| `UI-R3-02` | `CommunityComposerAttachmentPolicy`: 첨부 형식·크기·선택 순서. `CommunityComposerAttachmentUploader`: 순차 업로드·부분 실패 집계 | 기존 Composer의 게시/수정/예약, 초안, 상태 알림과 닫기 |
| `SHARED-R3-01` | `SsalddelMobilePushInstallationTransport`: 설치 PUT/DELETE 요청·본문·Bearer·경로·단일 전송 | 기존 회원·기사 facade의 계정/revision·권한·토큰 회전·철회·저장과 요청/응답 소유 |

기존 프로젝트 안에 좁은 책임을 두었으며 새 framework·assembly·상태머신은 만들지 않았다. public facade 생성자·메서드, endpoint·JSON·설치 식별자, 화면 구조·문구·업무 정책·기능/worker 기본값은 유지했다. 페이지 목적·정보 배치·navigation이 바뀌지 않는 내부 리팩토링이며 시각 변경이나 새 페이지 책임 카드 작성으로 확대하지 않았다.

## 특히 보존한 처리 순서

- 첨부 목록은 게시 저장 await 및 null 응답 확인 **후** 확정한다. 업로드마다 현재 `Draft.Password`를 읽으며 반환용 `SubmissionPassword` snapshot으로 대체하지 않는다.
- 업로드는 순차 실행한다. 일반 실패는 다음 파일을 계속 처리하고 호출자 취소는 현재·남은 파일을 실패 집계한다. null 첨부 응답도 기존처럼 성공 횟수로 센다. 게시 성공 뒤 첨부 실패를 새 게시 요청으로 바꾸지 않는다.
- `ClearLocalDraftAsync(None) → Reset → 닫기 → 최종 상태 → finally IsSaving=false` 순서와 실패 목록/표시용 중복 제거의 차이를 유지한다.
- 회원 PUT/DELETE는 caller의 `using` 획득 **뒤** Bearer를 지정한다. 잘못된 인증값의 예외에서도 기존 요청/본문 해제 경계를 유지한다. FDriver의 반환 전 인증 구성은 원형 그대로 둔다.
- PUT 및 회원 DELETE는 로컬 저장까지 응답 수명을 유지한다. FDriver DELETE는 응답·요청 해제 → bool 반환 → 계정 재확인 → 로컬 저장 순서다. DELETE 404와 앱별 OS 권한/해제 동작 차이도 유지한다.

## 검증 결과

| 증거 | 결과 | 범위 |
| --- | --- | --- |
| 추출 전 특성화 | 18개 통과 | 원본 Composer 6개·푸시 12개. 최초 잘못 잡은 FDriver timeout 기대값은 현행 문구로 바로잡고 제품은 수정하지 않았다. |
| 최종 Fast | 111개 통과·실패 0개 | 기존 관련 시험 및 새 특성화 22개(첨부 6·푸시 16), 소비 솔루션 빌드 |
| 최종 Task | 13,989개 통과·실패 0개·16개 미실행 | 현재 선택된 `Ssalddel.Tests` 전체 회귀와 `Ssalddel.v3.5.slnx` 소비 빌드 |
| 독립 코드 대조 | 완료 | 선택 getter·파일 snapshot·비밀번호·부분 실패·취소·요청/응답 해제·기존 상태 조율 |
| 소스 결속 | 완료 | 최종 8개 소스/시험 해시가 중앙 검증 동안 유지됨. 기존 관련 시험 파일은 수정하지 않음. |

로그: [최종 Fast](../../../../../artifacts/local/validation/20261021-080900/tests-01.log) · [최종 Task](../../../../../artifacts/local/validation/20261021-081127/tests-01.log) · [묶음 결과](../../../../../artifacts/local/refactoring-implementation/20261021-r3/batch-01/batch-result.json).

기존 빌드 경고는 남아 있다. 코드·시험·소비 빌드 검증이며 실제 브라우저/휴대폰 화면·SecureStorage·외부 FCM 수신·음성 알림·운영 연결·실금전 완료의 증거는 아니다. 실제 푸시 설정을 새로 활성화하거나 APK를 설치하지 않았다. stage·commit·push도 수행하지 않았다.

## 다음 순서

다음 제안은 **3번 마트 기사 화면의 미확정 명령·결과 확인 수명**이다. 음식 메인에 이미 분리한 세션과 마트 전용 업무를 합치지 않고, 계정·requestId·revision·원문 보호·조회 확인의 현행 경계를 보존하는 작은 후속 작업으로 남긴다.
