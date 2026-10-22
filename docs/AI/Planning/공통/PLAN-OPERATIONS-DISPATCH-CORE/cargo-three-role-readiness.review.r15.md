# 화물 3역할 한 건 처리·운영 준비 재검토 r15

상태: `ReviewedWithFindings / ProductUnchanged / DeploymentNotStarted`. 조사일: 2026-10-22.

사용자는 화물 운송을 먼저 안정화한 다음 음식 배달과 서버 배포로 이어가도록, 화주·기사·운영자 입장이 업무 시작부터 마무리까지 반영돼 있는지 검증해 달라고 요청했다. 이번에는 현재 소스·기존 시험·로컬 읽기 HTTP와 별도 가상 재현으로 문제를 확인했다. 제품 수정·신규 운송 접수·기존 DB 변경·휴대폰 조작·클라우드 배포는 하지 않았다.

선행: [정상 화물 한 건 역할 마감](../../../../Changes/2026-10-19-cargo-order-closeout-r70.md), [실제 폰 추천·사진·완료](../../../../Changes/2026-10-21-cargo-ai-phone-verification-r1.md), [오류창·복귀](../../../../Changes/2026-10-21-role-error-recovery-r1.md), [업무 맥락·실패](../../../../Changes/2026-10-21-domain-workflow-recovery-r2.md), [재실행 복원](../../../../Changes/2026-10-21-durable-workflow-recovery-r3.md), [업무별 역할 분리 r9](business-role-bundles.r9.md), [요약·내역 r12](platform-overview-drilldown.r12.md).

## 결론과 실제 확인 범위

모바일 다단계 접수, 서버 차량·시간·결제 자격 검사, 추천·수락, 사진을 요구하는 상하차, 운송 완료와 인수·수납·지급 분리는 마련돼 있다. 정상 합성 운송은 이전 실제 폰에서 완료됐지만, **현재 신규 기본 선결제 접수와 UI 결제/수락 사이에는 순환 차단이 있으며, 응답 시간초과·금액·운영자 종결 표시에도 결손이 남는다.** 기존 기결제·승인 후불 사례의 성공을 기본 신규 접수의 성공으로 확대할 수 없다.

이번 제품 빌드와 기존 집중 시험은 436통과·0실패·0미실행이다. 별도 가상 재현9개는 대조군3개와 문제 관측6개(결함 유형5개)이며 기존 시험 성공과 별개다. 실제 브라우저·기기·PG·송금·차량 이동을 재현한 것이 아니다. 코드 감사에서 발견한 추가 문제는 아래에 정적 확인으로 명시한다.

```text
화주: 접수 조건·개인정보 동의·접수번호 보존
├─ 기본 선결제·결제대기
│  └─ [F1] 상차 전 결제 UI 차단 ↔ 미결제 기사 수락 차단
└─ 결제 확보 / 명시적으로 승인된 후불·현장지급
   └─ 서버 후보 검사 → 추천 → 기사 수락
      ├─ [F2] 통합 입력 화면의 시간초과·미확정 안내 결손
      └─ 상차 도착 → 사진·인수 조건 → 상차 완료
         └─ 하차 도착 → 사진 → 운송 완료
            ├─ 화주: 같은 의뢰 진행·인수 확인
            │  ├─ [F3] PC 상세의 대상 전환·늦은 응답
            │  └─ [F6] 화물 분쟁 진입·지급 보류 연결
            ├─ 기사: 보수·다음 업무·지급 준비
            │  └─ [F4] 고객 운임과 기사 보수 표시 혼동
            └─ 운영자: 운송·사건·증빙·수납·지급 조회와 판단
               ├─ [F5] 수납을 전체 정산 완료로 표시
               └─ [F8] 선택 운송에서 상세·사건·지급 준비 인계
```

## 먼저 보완할 6개 문제

### F1 · P1 · 기본 선결제 접수에서 결제와 수락이 서로 기다림

**정책 재현 + 호출 경로 정적 확인.** [모바일 생성](../../../../../SsalddelApp/Services/ServerBackedShipperOperationsService.cs)은 신규 의뢰에 `선결제`를 넣는다. [공통 상세 판정](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/ShipperRequestDetailPresentation.cs)의 `CanPay`와 [모바일 항목](../../../../../SsalddelApp/Models/Shipper/ShipperRequestItem.cs)은 상차 이후에만 결제를 허용한다. 반면 [배차 진입 정책](../../../../../Ssalddel/Application/Shipper/Request/화주운송배차진입정책.cs)은 미결제 선결제 의뢰의 기사 수락을 거절한다.

가상 `생성됨/미시작/결제대기/선결제/입금대기` 의뢰에서 실제 공통 UI 결제 허용과 실제 서버 수락 허용이 모두 false임을 재현했다. 신규 접수부터 실제 서버에 제출한 통합시험은 아니다. 기존 승인 후불 fixture를 근거로 이 기본 경로를 통과했다고 표시하지 않는다.

서버 [Toss 결제 준비](../../../../../Ssalddel/Application/Shipper/Payment/Handlers/토스결제준비CommandHandler.cs)는 명시적 선결제 옵션을 지원하지만 제품 UI에서 `공통결제준비Async`/`토스결제준비Async` 호출이 연결되지 않았다. 등록 후에는 상세로만 이동한다. 기존 [FakePG 승인](../../../../../Ssalddel/Application/Shipper/Payment/Handlers/페이크결제승인CommandHandler.cs)도 상차 전 선결제 허용 인자를 사용하지 않으므로 버튼만 풀어서는 해결되지 않는다.

**보완:** 배차 전의 기존 선결제 준비→사용자 결제→같은 요청 결과 확인→결제 확보 뒤 배차를 화면에 연결한다. Simulation 검증과 실제 PG를 구별한다. 직접 거래·후불·현장지급은 명시적으로 확인된 별도 조건을 따르며, 결제·배차 보호를 완화해 통과시키지 않는다.

### F2 · P1 · 통합 기사 수락·상하차의 시간초과를 조용히 무시

**현재 ViewModel 재현.** [CargoInputLifetime](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoInputLifetime.cs)의 무조건 `catch (OperationCanceledException)`이 HTTP 시간초과와 실제 화면 이탈을 합친다. [수락](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionViewModel.cs)과 [상하차](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoTransportActionViewModel.cs)는 POST 뒤 GET으로 확인하지만, POST가 시간초과면 그 GET까지 도달하지 못한다.

상차·하차 각각에서 실제 ViewModel에 `TaskCanceledException`을 주입했다. 오류 없음·문구 null·Saved=false·CanSubmit=true가 남고, 이전 상태를 반환하는 preflight GET 이후 두 번째 수동 제출은 POST를 다시 호출했다. 같은 조건의 `HttpRequestException`은 안내와 제출 차단이 정상 작동했다. 진짜 caller 취소는 별도 대조군으로 확인했다. 자동 재POST나 실제 서버 중복 운송 생성·중복 송금을 확인한 것은 아니다.

**보완:** 실제 수명 토큰/판본 취소만 조용히 폐기하고, 시간초과는 결과 미확정으로 유지한다. 같은 원요청의 정본 확인을 우선하고 재실행 후 복구도 같은 보호에 연결한다. 전용 DriverApp의 복구를 통합 입력 화면에 이미 적용했다고 보지 않는다.

### F3 · P1 · PC 화주 상세에서 A 요청의 늦은 응답이 B 선택 뒤 표시됨

**현재 Web ViewModel 재현.** [Web 상세 VM](../../../../../Ssalddel.WebApp/ViewModels/ShipperRequestDetailPageViewModel.cs)은 `IsBusy`이면 새 대상을 버리고, 이후 응답의 대상·계정 판본을 재검사하지 않는다. A 조회를 지연→B 조회 요청→A 응답 완료에서 B GET0회·표시 A를 확인했다. 가상 HttpMessageHandler·정상 합성 인증/feature 응답을 사용했으며 실제 사용자의 주문을 열지 않았다. 취소·계정 변경의 모든 변형을 새로 실행한 것은 아니다.

**보완:** 모바일의 조회 수명 원칙을 재사용해 대상·계정·화면 판본을 결속하고 최신 대상 조회를 보장한다. 이전 요청의 성공·실패·finally가 새 화면을 덮지 못하게 한다.

### F4 · P1 · 화주 총운임을 기사 보수로 표시하는 경로

**Web 실제 변환 재현 / 전용 기사 매핑 정적 확인.** [공통 상세 Snapshot](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/ShipperRequestDetailPresentation.cs)은 `DriverFare = source.최종운임`으로 넣는다. 고객 운임12,000원·약정 기사액9,000원을 입력하면 기사액12,000원이 됐다. 계약의 `요금옵션.기사지급예정운임`과 다르다.

[전용 기사 Mapper](../../../../../DriverApp/Services/기사운송표시Mapper.cs)도 `source.운임`을 예상수익으로 변환하며, 진행/하차 페이지가 이를 표시한다. 서버는 [기사 보수 Projector](../../../../../Ssalddel/Application/Driver/Transport/Handlers/기사운송보수Projector.cs)에서 고객 운임과 동결 기사액을 분리한다. 전용 앱의 새 기기 실행이나 그 Mapper의 별도 재현은 이번에 하지 않았다.

**보완:** 고객 청구·수락 전 약정·수락 후 동결 기사액·지급 완료를 구분한다. 금액 미확인 시 고객 총운임으로 채우지 않으며 ‘지급 예정’과 ‘실지급’을 따로 표시한다. 실제 원장 금액 계산을 바꾼다는 뜻이 아니다.

### F5 · P1 · 운영자가 화주 수납 확인을 전체 정산 완료로 오해할 수 있음

**현재 Factory 재현.** [관제 단계](../../../../../SsalddelAdmin/Services/운송워크플로우관제상세Factory.Steps.cs)의 정산 완료 판정이 `입금확인완료`도 완료로 표시하고, [관제 조립](../../../../../SsalddelAdmin/Services/운송워크플로우관제상세Factory.cs)이 마지막 완료 단계를 현재 단계로 고른다. 기사 지급 근거를 하나도 전달하지 않은 입금확인 의뢰에서 정산 단계 완료=true를 재현했다.

[정산 화면](../../../../../SsalddelAdmin/Components/Pages/TransportWorkflowSettlement.razor)의 ‘기사 정산 후보’는 해당 운송 기사에게 줄 운송료가 아니라 기사 월 이용료 목록을 표시한다. 실제 [지급 준비 Policy](../../../../../Ssalddel/Application/Settlement/기사지급준비Policy.cs)는 수납 뒤에도 계좌 확인 등을 구분한다. UI 오표시이지 실제 지급 실행을 관측한 것이 아니다.

**보완:** 수납 확인→지급 준비→지급 요청/결과를 다른 단계로 표시한다. 월 이용료는 별도 목적·명칭으로 분리하고 해당 운송의 지급 준비 조회를 연결한다.

### F6 · P1/P2 · 화물 신고 진입과 실제 지급 보류의 연결 결손

**정적 호출 경로 확인.** 현재 [통합 화주 카드](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceAdapters.cs)에는 조회·수령·취소만 있고 화물 분쟁 접수 진입이 없다(P2). 한편 지원되는 `/commerce/disputes?sourceKind=cargo-request&sourceId=...`의 [거래분쟁 Controller](../../../../../Ssalddel/Controllers/Common/거래분쟁Controller.cs)→[보호지원 UseCase](../../../../../Ssalddel/Services/PrivacySupport/보호지원UseCase.cs)는 당사자를 확인하고 Mongo 지원 사건을 저장한다.

[기사 지급 준비](../../../../../Ssalddel/Application/Driver/Settlement/기사지급준비UseCase.cs)와 [지급 승인](../../../../../Ssalddel/Application/Admin/Settlement/기사지급승인UseCase.cs)은 SQL 비정상운송사건 보류를 읽지만 이 일반 화물 분쟁은 읽지 않는다(P1 연결 결손). UI 진입 누락과 직접 지원 route의 보류 누락을 한 실제 화면 재현으로 합치지 않는다. 실제 분쟁·지급 명령은 실행하지 않았다.

**보완:** 화물 당사자의 정식 분쟁 접수·원운송 연결·보류·관리자 검토·해제 기준을 기존 두 책임 사이에 명시적으로 인계한다. 일반 도움 문의를 모두 지급 보류로 확대하지 않고, 단순 창 닫기나 사건 종결을 송금 완료로 처리하지 않는다.

## 뒤이어 점검할 연결

| 항목 | 현재 근거·영향 | 판정·최소 방향 |
| --- | --- | --- |
| F7 전용 기사 신고 응답 유실·보류 표시 | [신고 서비스](../../../../../DriverApp/Services/DriverTransportExceptionService.cs)는 시간초과를 미저장으로 단정한다. [Mapper](../../../../../DriverApp/Services/기사운송표시Mapper.cs)는 서버의 운송 보류·허용행동을 표시 모델에 전달하지 않는다. | P2 정적 확인. 같은 신고 결과 조회/중복 보호와 보류 사유·담당자·허용행동을 전달한다. 서버 보류 guard는 이미 존재한다. |
| F8 관리자 선택 운송의 상세 인계 | [화물 관리자 Adapter](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOperatorWorkspaceAdapter.cs)는 전체 사건 목록만 연결하고 선택 운송의 상세·지급 준비로 내려가지 않는다. | P2 정적 확인. 기존 운송 상세·사건/복귀 문맥을 선택 ID로 연결한다. r9/r12의 조회·업무 복귀 개선은 유지한다. |
| F9 부재한 지원 담당자 인계 | [보호지원 UseCase](../../../../../Ssalddel/Services/PrivacySupport/보호지원UseCase.cs)는 본인 배정만 제공하고 다른 담당자에게 할당된 사건의 행동을 차단한다. | P2 정적 확인. 기존 관리자 권한 안의 명시적 인계와 사유·판본·감사를 검토한다. 새 권한 자동 부여는 하지 않는다. |
| F10 인수증 동시 등록 | [등록 Handler](../../../../../Ssalddel/Application/Shipper/Request/Handlers/화주운송의뢰인수증등록CommandHandler.cs)는 미등록 읽기→검증 await→일반 저장이며 조건부 쓰기/CAS가 없다. 순차 멱등 시험과 별개다. | P2 정적 경쟁 가능성. 두 컨텍스트의 동시 재현은 미실행. 미등록 조건부 쓰기 후 같은 번호/다른 번호 정본 대조를 우선 검증한다. |
| F11 수행 중 추가 추천 | [기사 Adapter](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceAdapters.cs)는 활성 운송0일 때만 추천을 읽는다. 서버의 후속 추천과 UI 계약이 다르다. | UX·설계 공백. 현재 업무 집중을 유지하면서 별도 다음 추천 진입을 둘지 정한다. 무조건 새 기능으로 활성화하지 않는다. |

## 배포 전 점검과 이번 HTTP 사실

기존 로컬5364의 `/health/live`는200, `/health/ready`는503 `Unhealthy`였다. 선행 r114는 EnsureCreated와 migration 이력 공백을 기록했지만 **이번503의 의존성별 원인을 새로 확정한 것은 아니다.** 현재 공개 health 본문은 단순 상태뿐이다. 5365/5367 조회 전용 검증 호스트는 health 경로에405를 반환하므로 일반 제품 health 장애와 합치지 않는다. 세 호스트의 관리자 운송 GET은 무인증401이었다. 인증된 역할별 신규 운송 전체를 이번 HTTP로 검증하지 않았다.

[현재 Azure VM Compose](../../../../../deploy/azure-vm/compose.yaml)는 준비 검사 통과를 Caddy 시작 조건으로 사용하고 국내 화물 기능도 기본 OFF다. 이 상태로 복사 배포한다고 화물 운송이 열리는 것은 아니다. 의존 DB migration·복구 가능 백업·실제 HTTPS·역할 인증·FCM·지급 자격과 기능별 운영 준비는 별도 검증한다. OFF 경계를 우회하거나 건강 검사를 완화해 완료로 표시하지 않는다. Azure/NCP 중 사업자 선택과 이 업무 결함 해결은 별개다.

## 검증 기록과 제안 순서

1. F1 신규 선결제 또는 명시적으로 선택한 수납 조건을 기존 정책에 맞춰 접수부터 검증한다.
2. F2 시간초과와 F3 대상 전환을 포함해 같은 요청의 결과 확인·재실행 복구를 마감한다.
3. F4 세 역할의 고객 운임·기사 약정/동결액과 F5 수납·실지급 표시를 맞춘다.
4. F6/F7/F8 신고→보류→운영자 검토→기존 업무 복귀를 같은 운송에 연결하고 F9/F10 경합을 검증한다.
5. 위 흐름을 새 격리 시험 주문으로 완주하고 최신 APK와 실제 휴대폰·서버 정본을 다시 대조한다. 그다음 클라우드 준비/배포로 이동한다.

원시 자료: `artifacts/local/cargo-three-role-audit/20261022-r1/`.

- `focused-tests.log`, `cargo-three-role.trx`: 현재 제품 소비 빌드와 기존26개 필터의436사례 통과.
- `probe/Program.cs`, `probe-results-r3.json`, `probe-build-r3.log`: 실제 공통/Web VM·서버 정책·관리자 Factory와 가상 전송을 사용하는9개 재현/대조. 마지막 빌드 경고0·오류0. r2의 단계 property 이름을 잘못 쓴 검증 도구 컴파일 실패1개는 로그에 보존했고 제품 실패로 세지 않는다.
- `local-http-boundary.json`: 무인증 로컬 GET9건. 업무 명령·로그인·DB/원장 변경 없음.
- `verification.json`: 문서·소스·결과 해시와 검증 범위. 실제 폰·운영·송금·배포·모델 학습·commit/push는 미실행.

보고서·임시 가상 재현 도구만 작성했으며 발견한 제품 결함을 이번 조사에서 수정했다고 표시하지 않는다. 기존 dirty 작업과 원장·운영 기본값을 보존한다.
