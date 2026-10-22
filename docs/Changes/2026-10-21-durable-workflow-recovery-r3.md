# 앱 재실행 후 미확정 업무 복원 r3

- 상태: `CompletedLocalValidation / NativeRestartAndServerLookupPending`
- [승인 계획](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/durable-workflow-recovery.r3.md) · [작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/durable-workflow-recovery.r3.work-order.json)
- 선행: [단계별 예외·복구 r2](2026-10-21-domain-workflow-recovery-r2.md)

## 이번 범위

기존 단말 보호 저장소를 상하차 완료와 일반 입고 신청·완료에 연결한다. 화면이 닫히거나 새 ViewModel이 만들어져도 이전 요청을 확인한 뒤 진행한다. 기록은 계정·서버·업무 단계·대상·로컬 관측 번호만 보유한다. 서버 전문 원장·상태 전이·사진 검증·권한·운영 비활성은 유지한다.

```text
완료·신청 요청
├─ 이전 요청 확인 / 보호 저장 실패 → 새 요청 차단
└─ 보호 저장·재조회 성공 → 이번 명령 전송
   ├─ 완료 응답·정본 확인 → 확인 기록 정리 → 다음 업무
   ├─ 응답 불명 → 기록 유지 → 재실행해도 결과부터 조회
   └─ 명령 미전송 또는 명시적 미적용 확인 → 원 요청 기록만 정리
```

일반 입고 생성에서 접수번호 전 응답이 끊기면 기존 서버에 요청별 조회 계약이 없어 자동 확정할 수 없다. 목록 수동 확인을 제공하되 후보 자동 매칭·재등록·임의 초기화는 하지 않는다. 72시간 경과 후 조회·저장할 때 payload가 지워져도 관측 번호와 대상은 유지되어 잠금이 풀리지 않는다. 정시에 물리 삭제하는 타이머를 새로 만든 것은 아니다.

## 구현 내용

- 공통 `WorkflowRecoveryContext`는 계정·실제 단말 세션 판본·정규화한 API 주소의 해시를 묶는다. 서버 URL 원문·토큰은 복구 기록에 저장하지 않는다. 새 화물·입고 scope에만 적용하며 기존 분산 업무의 키를 바꾸거나 과거 기록을 초기화하지 않는다.
- 기사 `DriverCargoCompletionRecovery`는 사진 캐시와 별개로 운송·상하차 단계별 관측 번호를 보관한다. 저장 검증이 먼저 성공해야 완료 명령을 실행한다. 사진 준비 실패처럼 명령 미전송이 확인된 경우와 전송 여부가 불명확한 경우를 구분한다. A 요청의 늦은 거절이 B 관측을 제거하지 못하도록 원 관측 번호를 고정한다.
- 일반 입고는 기존 `IWorkflowRecoveryStore`를 재사용하며 생성/완료 종류·대상 ID·처리 결과만 저장한다. 명시적인 같은 OS·단계의 `NotApplied` 없이 HTTP400만으로 재등록을 허용하지 않는다. 완료 응답 후 GET 실패는 저장 성공을 보존한다. 다른 입고 화면에서는 `이전 입고 확인`으로 원 대상에 돌아간다.
- 보호 저장소의 빈 문자열·손상 JSON·읽기/쓰기/삭제 실패는 “기록 없음”으로 바꾸지 않는다. 저장소가 정상화되면 같은 화면에서 다시 확인할 수 있다. 실제 단말 DI는 기존 인증 세션의 판본과 같은 운영 API 주소를 사용한다.

책임은 [상차 화면](../ProjectOverview/page-docs/DriverApp/DriverApp-P12/README.md), [하차 화면](../ProjectOverview/page-docs/DriverApp/DriverApp-P13/README.md), [입고 화면](../ProjectOverview/page-docs/SsalddelApp/SsalddelApp-P04-1/README.md)에 연결했다.

## 화면 자료

[화면 모음](../assets/changes/2026-10-21-durable-workflow-recovery-r3/index.html) · [캡처 근거](../assets/changes/2026-10-21-durable-workflow-recovery-r3/evidence.json)

실제 공통 오류 컴포넌트에 가상 사례를 입력한 로컬 정적 렌더다. 상차 요청 확인, 하차 완료 후 확인 기록 정리, 입고 신청 미확정, 입고 완료 후 내역 확인, 상차 명령 전 준비 실패의 5사례를 390/1280px로 각각 저장했다. 전체 업무 페이지·버튼 상호작용·실제 서버 요청이나 휴대폰 캡처와 구분한다.

## 검증 결과

- 최종 Fast: 소비 `Ssalddel.v3.5.slnx` 빌드와 집중 **212개 통과**, 실패·미실행 0. 기록 `artifacts/local/validation/20261021-230459/`.
- 최종 Task: 동일 소비 솔루션 빌드와 전체 **14,328개 통과**, 실패 0, 환경 의존 **16개 미실행**. 기록 `artifacts/local/validation/20261021-230653/`. 첫 집중116·첫 Fast189도 별도 로그로 보존한다.
- 새 저장소 인스턴스의 복원과 저장·읽기·삭제 실패/무효 쓰기/기간 경과/계정·서버 변경/지연 응답·후속 관측 보호·장애 회복 후 재확인을 검증했다. 사진 준비 실패는 완료 API 미전송의 명시적 증거가 있을 때만 해제한다.
- 별도 프로세스3회에서 실제 공통 `ProtectedWorkflowRecoveryStore`를 파일 IO delegate로 실행해 생성→종료 후 복원→기간 경과 후 원문 제거와 ID 유지, 다른 계정·서버 격리를 확인했다. 단말 SecureStorage 검증이 아니다. 근거 `artifacts/local/durable-workflow-recovery/20261021-r1/process-probe-r2.json`.
- 실제 공통 컴포넌트 가상5사례·Chrome 390/1280px 총10장, 가로 넘침·브라우저 오류·외부 통신 0. 렌더의 공통 UI/계약 DLL과 최종 빌드 DLL의 해시를 대조했다. 전체 업무 페이지·클릭·기기 검증을 뜻하지 않는다.

최종 소스26개와 시험·정적 화면·프로세스/링크/diff 검사는 로컬 `artifacts/local/durable-workflow-recovery/20261021-r1/verification.json`에 연결했다. 신규 문서 전체와 수정한 목차/책임 카드 상단을 링크 검사한다.

## 남은 범위

실제 휴대폰에서 완료 응답 유실 후 앱 강제 종료·재실행, 다른 계정 로그인과 보호 저장소 장애를 확인해야 한다. 일반 입고 신청이 접수번호 전 유실되면 요청별 결과 조회 계약이 아직 없어 자동 해제할 수 없다. 구판 실패 응답 정형화·다른 OS 개별 적용도 후속이다. 웹 기본 저장소는 메모리 기반이므로 웹 프로세스 재시작 보존을 약속하지 않는다. 새 APK 설치·외부 서버·운영·지급·commit/push는 이번 완료 주장에 포함하지 않는다.
