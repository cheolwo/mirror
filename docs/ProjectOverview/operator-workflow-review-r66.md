# 운영자 관점 업무 흐름 검증 r66

- 기준: 2026-10-16, `dev/mirror-integration`, HEAD `b02f541aa00156376e6e33525753cd8a983d6671`와 현재 미커밋 작업 트리.
- 상태: `Reviewed / LocalTestsValidated / IsolatedQuickHttpValidated / GapsReproduced / ProductUnchanged / DeviceDeferred / OperationalDisabled`.
- 요청: 운영자가 직접 운영을 연습할 수 있는지, 각 역할의 시작·수행·예외·마감이 이어지는지 다시 검증한다. 이번에는 제품 코드를 수정하지 않았다.

정상 업무의 기본 연결은 갖춰져 있다. 격리된 퀵 운영 연습은 실제 API로 두 건을 완료했다. 다만 픽업 뒤 수락 취소, 수락 후속 유실 복구, 관리자 모바일의 반복 훈련·사건 이동, 음식 사건의 최종 인계에 공백이 있어 실제 운영 준비 완료로 판정하지 않는다. 이 보고서는 전문 OS의 상태를 새로 소유하거나 주문 원장으로 복제하지 않는다.

## 운영자가 따라볼 업무 트리

```text
운영 시작: 계정·환경·기능 활성 상태 확인
├─ 음식 주문
│  ├─ 주문 제출 → 음식점 확인 → 유효 기사 배정 → 조리
│  ├─ 준비 완료 → 기사 픽업 → 전달 → 주문자의 별도 수령 확인
│  └─ 취소·미수령 신고 → 정산 보류/지원 사건 → 검토·당사자 안내
│     └─ 실제 취소·재배송·환불/지급의 전문 업무 마감 [추가 연결 필요]
├─ 퀵·화물 의뢰
│  ├─ 조건·요금·동의 → 배차 대기 → 적격 기사 추천 → 본인 수락
│  ├─ 픽업지 도착 → 픽업 증빙 → 전달지 도착 → 전달 증빙·완료
│  ├─ 픽업 전 취소 → 재탐색
│  └─ 픽업 후 중단 → 보유 물품 확인·반납/인계 [일반 취소 우회 차단 필요]
├─ 입고·창고·출고
│  ├─ 공간·공급 조건 합의 → 입고 계획 → 실수량·사진 → 적재
│  ├─ 주문·재고 배정 → 피킹·사진 → 포장 → 출고 인계 → 배송
│  └─ 부분 입고/피킹·반납 → 잔량·보유 책임 확인 → 해당 업무 마감
└─ 정산·지급
   ├─ 업무별 완료·보수 근거를 별도로 누적
   ├─ 전달 증빙·확정 운임·수납 재원·계좌·사건 보류 재확인
   ├─ 주간 정산 명세 → 지급 준비/보류 사유
   └─ 실제 외부 송금·은행 입금 확인 [이번 검증 범위 밖]
```

알림을 읽음, 사건 안내 완료, 물품 전달, 주문자 수령, 정산 준비, 실제 지급은 각각 다른 결과다. 상위 주문은 업무 목적과 전문 원장 참조를 유지하고 최신 전문 원장의 상태를 조회한다.

## 이번 실행 근거

| 범위 | 실제 확인 결과 | 한계 |
| --- | --- | --- |
| 운영자·창고·업무 인계·운영 알림·보호지원·배차/모바일 훈련 기존 시험 | 748개 통과, 실패 0, 환경 의존 1개 최초 생략 | 시험의 가상 외부 포트와 실제 서비스/SQLite 범위이며 물리 앱 조작이 아님 |
| 처음 생략한 보호지원 Mongo 시험 | 별도 격리 DB에서 1개 통과, 실패·생략 0 | 암호화 저장·재조회·중복 접수·CAS·당사자 접근·보존/파기를 확인. 해당 시험이 만든 무작위 이름 DB만 정리 |
| 훈련 host 빌드 | 오류·경고 0 | API 실행과 구분 |
| 훈련 자체 검사 | 14개 통과 | 가상 시계·가상 역할 포트, 실제 API나 모델 시험이 아님 |
| 격리 퀵 실제 HTTP | 새 주문 2건 접수·추천·수락·배정 동의·픽업/전달 사진·완료. 신규 발생 중지 후 `Completed`, 남은 업무·실패·미해결 사건 0 | 실제 시계, 전용 MySQL/Mongo와 합성 물품·사진. 두 건은 유휴 기사 정상 수행이며 연속 다건·30분 15건·물리폰·FCM/소리·외부 결제 검증이 아님 |
| 관리자 반복 시작 | 기존 `Completed` 회차에서 모바일과 같은 null RunId Start 요청이 HTTP 409, 미적용으로 재현 | CLI의 현재 RunId 요청으로 별도 회차는 정상 시작. 모바일 버튼 결함이 해소된 것으로 보지 않음 |
| 픽업 후 취소 관문 | 현재 빌드 DLL의 독립 정책 재현에서 상차완료·운송중·인수완료 모두 취소 허용 및 기사/시각 제거 | HTTP·DB의 취소 명령 자체를 실행한 재현은 아님 |
| 기존 r64 구현 보존 | 관련 소스 56개 SHA-256가 이전 검증과 일치 | 기존 APK·기기·시각 검증을 새로 수행한 것은 아님 |

기존 시험의 고유 대상은 합계 749개다. 훈련 HTTP 검사는 6개 확인 중 5개 통과·모바일 반복 시작 결함 1개 재현으로 종료 코드 1을 남겼다. 정상 수행 성공과 결함 검출을 한 성공 지표로 합치지 않는다.

실제 HTTP 회차는 `dispatch-training-a97753376fd040d386c504d99ba818b0`다. 이전 완료 회차 원문이 `completed-runs`에 동일 바이트로 보존됐음을 검사했다. 불확실한 POST는 재전송하지 않았다. 이번에 시작한 host는 두 건을 마감한 뒤 종료했고, 기존 Docker·볼륨은 유지했다. 전용 시험 DB에는 이번 합성 업무 기록을 보존한다.

Ollama 비교 결과 2개는 `Timeout`이었다. 기존 규칙이 기사를 선정했으며 모델의 판단 품질·실제 AI 선정·가중치 학습 성공을 입증하지 않는다. 합성 역할은 푸시 토큰을 등록하지 않았고 외부 결제·실물·실제 알림을 실행하지 않았다. 전체 프로세스의 모든 외부망 호출이 봉쇄됐다는 의미는 아니다.

## 보완 우선순위

### 1. 픽업 후 일반 수락 취소를 차단한다 — P1, 정책 실행 재현

`배차응답가능정책.수락취소가능`은 업무 상태가 확정이거나 큐 단계가 확정이면 본인에게 취소를 허용한다. 큐 단계가 남아 있으면 상차완료·운송중·인수완료도 허용된다. 전환 서비스는 소유자만 확인하고 재추천 정책을 적용해 기사·픽업/도착시각을 지운다. 보유 물품·완료 사실을 남긴 전문 원장과 대기열이 갈라질 수 있다.

- 근거: [수락 취소 판정](../../Ssalddel/Application/Driver/DispatchAction/Services/배차응답가능정책.cs):21–28, [원장 전환](../../Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.cs):318–353, [재추천 초기화](../../Ssalddel/Services/Dispatch/Queue/배차재추천상태Policy.cs):15–25.
- 제안: 픽업 전 취소, 픽업 후 전문 중단·반납 인계, 완료 후 취소 불가를 구분한다. Handler와 영속 전환 직전에 같은 조건을 적용하고 완료·보유 사실을 지우지 않는다.

### 2. 수락 확정과 후속 복구 의도를 함께 저장한다 — P2, 소스 확인

퀵·화물 수락은 SQL commit 뒤 시간약속과 사후 이벤트를 처리한다. 알림 Outbox는 그 사후 이벤트 안에서 생성된다. commit 직후 중단되면 배정은 확정됐지만 후속 의도 자체가 없을 수 있고, 기존 수락 재조회는 후속 작업을 다시 실행하지 않는다.

- 근거: [수락 저장과 사후 처리](../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락CommandHandler.cs):302–368, [알림 의도 저장](../../Ssalddel/Application/Driver/DispatchAction/Handlers/배차수락사후처리EventHandler.Notification.cs):45–61.
- 제안: 기존 영속 후속 처리 구조를 재사용해 수락과 같은 거래에 재처리 의도를 기록한다. 현재 배정·추천 차수를 다시 확인하고 알림·시간약속을 중복 없이 마감한다. 이번에는 중단 시나리오를 실제 프로세스 장애로 재현하지 않았다.

### 3. 관리자 모바일의 반복 훈련과 사건 이동을 연결한다 — P1/P2

반복 시작 요청에서 모바일은 RunId를 null로 보낸다. 서버는 새 시작도 현재 RunId/Revision 일치를 요구하므로 이전 완료 회차가 있으면 409다. 또한 공유 지원 패널의 주문 확인 링크는 웹 전용 경로인데 APK의 실제 상세 경로는 다르다.

- 근거: [모바일 Start 요청](../../SsalddelAdminApp/Services/DispatchTrainingPageState.cs):102, [서버 회차 검증](../../eng/Ssalddel.DispatchTraining/DispatchTrainingCoordinator.cs):450. 반복 시작 불일치는 이번 실제 HTTP로 재현했다.
- 근거: [지원 패널 링크](../../Ssalddel.Ui.Common/Areas/BackOffice/Components/PrivacySupport/FoodSupportFollowUpPanel.razor):19, [APK 주문 상세](../../SsalddelAdminApp/Components/Pages/FoodOrderDetail.razor):1, [APK 허용 경로](../../SsalddelAdminApp/Components/Routes.razor):30–34. 경로 불일치는 소스 확인이며 실제 APK 탭 조작은 하지 않았다.
- 제안: 현재 회차를 조건으로 새 회차를 요청하고, 호스트별 주문 상세 주소를 주입한다. 완료 후 재시작·사건에서 주문으로 이동을 모바일 상태/라우트와 실제 기기에서 확인한다.

### 4. 운송 관제·현금 흐름 화면의 실패 복구를 통일한다 — P2, 소스 확인

`Operations`/`Finance`는 `AdminApiException`만 처리하지만 성공 응답의 JSON 해석 오류는 그 형태로 변환되지 않는다. 관제 반복 갱신이 종료되거나 안내 없이 이전 자료가 남을 수 있다. 진행 중 조회에도 계정·화면 세대 확인과 취소가 없어 늦은 결과를 버리는 보호가 부족하다.

- 근거: [API 응답 해석](../../SsalddelAdminApp/Services/AdminAuthenticatedApiClient.cs):29, [관제 조회와 반복 갱신](../../SsalddelAdminApp/Components/Pages/Operations.razor):237–303, [현금 흐름 조회](../../SsalddelAdminApp/Components/Pages/Finance.razor):173–190.
- 제안: 이미 음식 관제에서 사용하는 조회 수명·계정 전환 보호·마지막 확인 시각·다시 확인 행동을 재사용한다. 쓰기 결과가 불명확하면 요청 번호를 보존하고 정본부터 확인한다.

### 5. 음식 지원 사건을 실제 전문 업무 마감으로 인계한다 — P2, 기획·코드 마감

현재 후속 행동은 `RetryDispatch`와 `OperatorReview`다. 운영 검토는 주문 상태를 그대로 두고 `AgreementReviewPending`만 기록한다. 검토·안내 뒤 취소·재전달·환불 확인·기사 지급의 담당자와 완료 근거를 이어야 한다. 다른 원장에 별도 기능이 있다는 사실만으로 이 사건에서 그 업무가 완료됐다고 보지 않는다.

- 근거: [음식 지원 후속](../../Ssalddel/Application/Admin/Food/음식주문지원후속UseCase.cs):70, 102–109, 158–162.
- 제안: 조리 전·조리 후·픽업 후 상황별 합의/비용 책임을 먼저 정하고 전문 명령·결과 참조·다음 담당자를 연결한다. 사건 종결로 자동 환불/지급하거나 새 권한을 임의 부여하지 않는다.

## 검증 한계와 다음 운영 연습

- 전용 훈련은 기사 동시 업무 1건이며 경로·ETA/삽입 경로를 null로 반환한다. r64의 수행 중 A 끝점 → 후속 B 선정/수락은 이 HTTP 환경에서 인증하지 않는다. 검증된 합성 경로를 명시한 별도 시나리오로 확인해야 한다.
- 훈련 배우는 408/429를 포함한 4xx 단계 응답을 Rejected로 보존한다. 미적용 일시 제한이나 사진 응답 유실 시 검토가 필요할 수 있다. 정상 두 건 완료가 모든 예외의 자동 복구 성공을 뜻하지 않는다.
- 음식·입고·창고·정산은 현재 코드와 관련 기존 시험을 대조했다. 공동구매/수입·교육 등 10개 OS 전체, 모든 페이지의 실제 모바일 화면, 영업시간/실제 지도 경로·GPS·백그라운드 푸시·서버 재시작·외부 송금까지 전수 실행한 것은 아니다.
- 주간 정산은 완료일 기준과 수·목 예정 시각, 원천 재원·변경·사건 보류를 검증하는 코드/시험이 있다. 이번 실제 HTTP 두 건은 정산·송금 완료까지 수행하지 않았다. [정산 명세 재검증](../../Ssalddel/Application/Admin/Settlement/배송료정산배치UseCase.cs):174–220을 참고한다.
- [업무 집중 기간 r65](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-work-focus.design.r65.md)의 기간 기준은 아직 미답변이고 구현 전이다. 이번 운영 검증으로 기간 정책을 확정하거나 집중 업무 기능이 존재한다고 표시하지 않는다.

다음은 1번 원장 보호 → 2번 수락 후속 복구 → 3·4번 관리자 조작/조회 복구 → 5번 사건별 마감 기준 순으로 보완한다. 이후 같은 주문 번호로 기사 거절·미응답·픽업 뒤 중단·정산 보류를 하나씩 운영자가 처리하는 연습을 추가한다.

## 로컬 증거

- [운영 시험 TRX](../../artifacts/local/validation/20261016-operator-workflow-review-r66/operator-workflows.trx), [격리 Mongo TRX](../../artifacts/local/validation/20261016-operator-workflow-review-r66/privacy-support-mongo.trx).
- [훈련 자체 검사](../../artifacts/local/validation/20261016-operator-workflow-review-r66/training-selftest.log), [실제 HTTP 관측과 명령 확인](../../artifacts/local/validation/20261016-operator-workflow-review-r66/http-audit.json).
- [취소 정책 독립 재현](../../artifacts/local/validation/20261016-operator-workflow-review-r66/repro/repro-result.json), [기존 소스 해시 대조](../../artifacts/local/validation/20261016-operator-workflow-review-r66/source-hash-check.json).

제품 코드·기능 기본값·운영 DB·APK·Azure·GCS·Unity·commit/push는 이번에 변경하지 않았다. 이번 실행은 localhost 격리 시험이며 실제 서비스 운영 완료가 아니다.
