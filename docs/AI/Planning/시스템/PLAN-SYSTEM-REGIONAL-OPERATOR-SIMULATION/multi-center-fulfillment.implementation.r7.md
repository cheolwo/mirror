# [구현 명세 · 센터·위치·직원 출고 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r7]

2026-10-25. 기획 승인 `Approved` / 개발 인계 `ReadyToDispatch`. 이 상태는 아래에 한정한 구현 착수를 뜻하며 구현·시험·게임 실행 완료가 아니다.

첫 구현 결과: `CoreImplemented / LocalAndTestHttpValidated / SaveReplayValidated / PresenterValidated / SceneIntegrationPending / PlayModeNotVerified`. 아래 범위의 코드·회귀를 마쳤으며 전체 경영 게임이나 실제 Unity 플레이·운영 출고 완료를 뜻하지 않는다.

- 승인 근거: 같은 스레드에서 사용자가 2026-10-25 **“이제 구현 작업 진행”**을 직접 지시했다. [r6 다단계 출고](multi-center-fulfillment-decisions.proposal.r6.md)와 [r5 위치 바코드·피킹](location-barcode-outbound-picking.proposal.r5.md)의 방향을 아래의 첫 가상 출고 범위로 구체화한다. 이 범위의 구현에 대한 종전 보류를 해제하며 상위 PLAN 전체를 일괄 승인하거나 완성 처리하지 않는다.
- 주책임: `Simulation`. 계약·순수 엔진·세션/저장 결속과 Runtime·Controller·Unity 표시를 분담하고 개발 통합 담당이 결합·회귀 결과를 소유한다.
- 개발 통합의 `ownerThreadId`: `01a0c8bc-d06d-7181-aa71-c34dd9986f98`(이번 사용자 대화). 같은 스레드의 계약·엔진, Runtime·표시, 문서 하위 위임은 각 허용 파일만 소유하며 다른 스레드의 기존 Active 쓰기 소유권을 대체하지 않는다.
- 기존 WI 의미: `WI-HUB-03` 출고 대상 재고 요청, `WI-HUB-04` 출고 대상 재고 피킹, `WI-HUB-05` 피킹 화물 포장. 계획 확정·실제 가상 피킹·포장 준비는 각각 구분한다. 이 세 WI를 하나의 Goal로 합치지 않는다.
- Goal 경계: 새 Goal을 생성·활성화하지 않는다. 다른 기획 판본에 고정된 `WI-OPS-SEASONAL-CAMPAIGN-ADVANCE`의 활성 Goal·승인 hash·명세를 이번 작업에 재사용하지 않으며, 대기 중 Hub Goal도 자동 활성화하지 않는다. 기존 주체·WI·Goal 원장은 이 명세의 작성만으로 바뀌지 않는다.
- 작업 문맥: 같은 스레드의 국소 개발 수용이다. 기획과 구현의 기준 입력·정확 경로·담당을 아래에 결속한다. 다른 계정·데스크톱으로 Git 인계할 때는 [교대 운영 체계](../../../../Architecture/GitHub기반기획개발교대운영체계.md)의 정확 commit·revision/hash 점검을 별도로 따른다. 이번 승인에 commit·push는 포함되지 않는다.

### 승인 기준선과 연구 경계

- 착수 시 저장소: `C:/Users/user/source/repos/Hongdal`, branch `dev/mirror-integration`, HEAD `d3a19163ba96a7dbed31028f639f3bf0c534ae79`. 기존 dirty 작업은 보존한다. HEAD가 같다는 이유로 dirty 소스가 같다고 가정하지 않고 최종 검증에 사용한 파일 판본은 개발 통합 담당이 별도로 기록한다.
- 방향 입력 r5 SHA-256: `190f86918afbf4e1734af021bc5f6b8f5b40f5da38c36d6b2e1900584b01f3967`.
- 방향 입력 r6 SHA-256: `5fcbb3e4fa7d6afb09a67acd16caa5ac0ca081827c718cde56c7c955dc914509`.
- 이 두 hash는 승인 방향을 읽은 내용의 기준선이며, 기존 상위 README의 모든 문답을 Approved로 승격하지 않는다. r5/r6가 이후 바뀌면 이번 입력과의 차이를 남긴다.
- `requiredResearch: []`: 이번 합성 수량·예약·작업 상태 절편에는 새 외부 전문 연구를 필수 선행으로 두지 않는다. 실제 소유/판매 계약·현장 고용·운송·법규·공공자료 배포가 필요한 범위는 제외한다. 기존 다른 Goal의 Accepted 연구나 공간 승인을 이번 범위로 양도하지 않는다.

## 1. 첫 완결 범위

플랫폼 운영자가 **가상 주문의 출고 센터와 그 센터 안의 위치별 재고·담당자를 비교하고, 명시적으로 예약한 뒤 부분 피킹과 포장 준비까지 확인하며 저장·복원·재생으로 같은 결과를 얻는다.** Preview만 만들고 이 범위의 완료로 보고하지 않는다.

첫 표본은 가상 센터를 두 곳 이상 사용한다. 위치의 안정 ID·바코드, 상품 식별자·상품 바코드·로트·센터별 재고 관계, 수량 단위, 담당 가능 조건과 기준 판본을 명시한다. 첫 구성은 서로 다른 소유자의 재고를 혼합하지 않는 가상 표본이며 소유권·계약 조건의 전체 검증을 구현했다고 표시하지 않는다. 실제 지점·선반·재고·근무자 관측을 등록한 것으로 표현하지 않는다. 센터·선반·담당자 수와 점수 가중치는 표본/정책 입력이며 현실의 처리 능력이나 최적값으로 승격하지 않는다.

```text
같은 Simulation 세션
  → Configure: optional Fulfillment 초기 조건 명시
  → Preview: 센터·위치·담당 가능성 / 선택·제외 근거
  → Confirm(Reserve): 현재 세션 판본에서 계획을 다시 검증하고 원자 예약
  → Pick: 위치 바코드·상품·예약·담당·실제 가상 수량 확인
  → 부분 피킹 또는 전체 피킹
  → PackReady: 배정 물량의 피킹 충족 뒤 포장 준비
  → 같은 세션 Save / Restore / Replay

피킹 전 취소 → 미수행 예약 해제
불일치·부족·오래된 판본·요청 충돌 → 실패와 사유, 업무 상태 무변경
이미 피킹한 계획 취소 → 실물 복구 명령이 없으므로 차단
```

센터를 고르는 시점부터 내부 위치·상품·수량·담당 조건을 평가한다. 내부에서 실패한 후보는 같은 센터의 적격 대안, 다른 센터와 허용된 분할 후보로 다시 검토한다. 재계획은 실행한 피킹을 삭제하거나 물건을 다른 센터에 생성하지 않는다. 주문의 분할 허용 여부를 지키고 미충족 수량·담당 부재·자료 부족을 완료나 0 비용으로 숨기지 않는다.

## 2. 상태와 계약의 권위

`SimulationFulfillment`의 순수 엔진은 `Apply(state, command)`와 Preview를 제공한다. Apply는 기존 입력 상태를 직접 수정하지 않고 검증을 통과한 새 상태와 결과를 반환한다. Preview는 상태·예약·작업·명령 이력을 변경하지 않는다. Application은 기존 세션 접근자를 통해 같은 세션에 결과를 결속한다. Unity의 선택·강조·애니메이션은 조회 결과를 표현할 뿐 재고 권위가 아니다.

상태 사본에는 선택적인 `Fulfillment`를 둔다. 기존 세션의 재고 `quantity`·상태 코드·시설/로트 ID를 가용량·예약량으로 재해석하거나 덮어쓰지 않는다. 기존 창고·마트·음식·Farm·계절 캠페인 흐름도 바꾸지 않는다. 첫 표본의 Fulfillment는 명시적으로 구성된 가상 상태이며 운영 재고나 다른 세션의 상태와 자동 합산하지 않는다.

입력·응답에는 다음 결속을 유지한다.

| 계약 | 최소 의미 |
| --- | --- |
| 구성 | 센터·위치·바코드·상품/로트·단위·수량·담당 가능 조건·구성 판본 |
| 주문·계획 | 같은 주문/라인 ID, 요청 수량, 허용 센터·분할 조건, 후보별 사유, 배정 센터·위치·재고·담당 참조 |
| 확정·작업 명령 | `CommandId`, 명령 종류·입력 본문, 상위 세션에 대한 `ExpectedRevision`, 현재 주문/배분 ID와 확인 수량 |
| 조회 결과 | 상위 세션 revision과 optional Fulfillment, 센터/위치별 보관·예약·피킹 결과, 계획과 담당 지시, 부분/전체 상태와 차단 사유 |
| 명령 결과 | 성공·실패·기존 결과 재사용 여부, 같은 명령과 본문에 결속된 결과, 변경 후 상태의 근거 |

`ExpectedRevision`은 기존 세션이 검사하고 Fulfillment 엔진에 독립 revision을 추가하지 않는다. 예약 명령은 현재 상태에서 같은 주문을 다시 계획·검증하며 DTO에 별도 `PreviewHash`를 요구하지 않는다. 엔진의 `BuildStateKey`·`BuildCommandKey`와 세션의 payload/hash 기록으로 상태·명령을 결속한다. 같은 `CommandId`·같은 정규화 본문은 이미 기록된 동일 결과를 재사용하고 재차 예약·피킹하지 않는다. 같은 `CommandId`의 다른 본문은 거절한다. 새 명령의 오래된 세션 revision은 현재 상태를 다시 읽도록 거절하며 자동 재POST하지 않는다. 기록된 동일 명령의 재사용과 새로운 명령의 판본 검사를 구분한다.

필수 자료 누락과 조건 불일치는 이유 있는 실패로 남긴다. 새 전역 최적화나 현실 처리 시간을 만들어내지 않고, 명시 입력과 기존 배분 규칙을 재사용한다. 배송권·시간·담당 조건의 필수 제한과 점수 선호를 구분한다.

## 3. 수량·부분 처리·복구 불변식

1. 동일 재고 참조를 여러 주문 라인·동시 확정에서 중복 예약하지 않는다. 라인 배분 합계는 요청 수량을 넘지 않고 가용 수량은 음수가 되지 않는다.
2. 선택 센터들의 예약은 한 명령 안에서 전부 성공하거나 업무 상태를 유지한다. 일부 센터의 예약 성공을 전체 성공으로 반환하지 않는다. 외부 시스템 예약·분산 보상은 이번 구현에 포함하지 않는다.
3. `OnHandQuantity`는 선반 위 보관 수량이고 `ReservedQuantity`는 그 안의 점유다. 가용분은 `OnHandQuantity - ReservedQuantity`이며 `0 ≤ ReservedQuantity ≤ OnHandQuantity`를 유지한다. Reserve는 예약량만 증가시키고 OnHand를 줄이지 않는다. Pick은 해당 예약의 OnHand와 Reserved를 같은 수량만큼 줄이고 배분의 `PickedQuantity`를 늘린다. PackReady는 수량을 다시 차감하지 않는다. 예를 들어 보관 10·예약 0에서 3개 예약 뒤에는 보관 10·예약 3·가용 7, 그중 2개 피킹 뒤에는 보관 8·예약 1·가용 7·피킹 2다. 전체 물량은 선반 잔량과 피킹분을 각각 한 번만 합산해 대조하며, 선반 잔량에 이미 포함된 Reserved를 다시 더하지 않는다.
4. 부분 피킹은 양수이며 해당 예약의 남은 미피킹 수량 이하다. 위치 바코드·상품·예약·담당 관계와 현재 판본을 함께 검증한다. 상품 코드 입력과 가상 현장 확인을 실제 카메라 스캔 증거로 표현하지 않는다.
5. 부분 피킹은 전체 피킹이나 PackReady가 아니다. 필요한 배정 물량을 충족하기 전에는 포장 준비를 차단한다. PackReady도 출고 운송 인계·판매·수령·정산 완료가 아니다.
6. 아직 피킹하지 않은 계획의 취소는 보유 예약을 한 번만 해제한다. 이미 피킹한 계획의 취소는 반납·격리·재배정 명령을 구현하지 않았으므로 차단하며 피킹 수량을 가용량으로 되돌리지 않는다.
7. 실패 명령은 재고·예약·작업 결과·단계·업무 revision에 부분 반영을 남기지 않는다. 전송/응답 불확실과 명시 실패를 구분하며 같은 요청의 결과를 대조한다.
8. 구성 명령은 새 가상 상태를 명시적으로 초기화하는 경계다. 기존 진행 중 상태와 예약·명령 이력을 조용히 초기화하거나 덮어쓰는 수단으로 사용하지 않는다.

## 4. 기존 세션·저장·Runtime 결속

- `Fulfillment`가 없는 기존 세션·snapshot·save는 기존 의미와 저장 판본·hash 경로를 유지한다. 필드가 없다는 이유로 자동 구성하거나 기본 재고를 주입하지 않는다.
- 초기값은 createRequest의 암묵 기본값이 아니라 Configure 명령으로 설정한다. 수용된 명령의 입력·종류·판본·결과를 명령 로그에 기록하고 재생한다.
- 모듈이 있는 저장은 `simulation-save.v34` wrapper에 상태와 명령 payload/hash를 결속한다. 없는 경우의 기존 저장 판본은 보존한다. clone·hash·검증·restore·replay가 같은 optional 필드를 사용하며 상태만 저장하고 명령 계보를 버리지 않는다.
- Save/Restore/Replay에서는 센터·위치·재고·담당·예약·부분 피킹·PackReady·취소와 명령 중복 결과를 보존한다. 변조·지원하지 않는 판본·불일치 hash를 정상 복원으로 숨기지 않는다.
- 새 Local adapter는 기존 물류 서비스와 같은 SessionAccessor를 사용한다. 기존 `LocalSimulationRuntime.cs`와 그 공용 시험을 수정하지 않는다. Local과 HTTP adapter는 같은 권위 서비스의 GetState/Preview/Confirm 계약을 소비한다.
- Unity Presenter는 후보·불가 사유·선택 센터·위치·상품·담당 지시·부분 상태·오류/재조회/요청 중 상태를 소비한다. 실제 Scene 저장·선반 GameObject 배치·입력 조작·실제 스캔 검증은 포함하지 않는다.

## 5. 담당과 허용 쓰기 경로

아래는 이번 개발 통합 담당이 수용한 경로다. 새 파일의 경로는 아직 작성·컴파일 완료를 뜻하지 않아 코드로 표기한다. 새 UnityPackage/Unity C# 파일의 대응 `.meta`는 해당 담당이 함께 소유하며 기존 GUID를 재생성하지 않는다. 목록 밖 공유 계약이나 기존 업무 상태를 바꿔야 하면 이 명세의 영향 범위와 담당을 먼저 갱신한다.

| 담당 | 허용 경로·최소 변경 |
| --- | --- |
| 계약·순수 엔진 담당 | 새 `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFulfillmentContracts.cs`와 `.meta`; 새 `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFulfillmentEngine.cs`와 `.meta`; 새 `Ssalddel.Simulation.Tests/SimulationFulfillmentEngineTests.cs`. 계약은 Configure/Reserve/Pick/PackReady/Cancel, 엔진은 Preview/Apply/Clone/CloneOrder/CloneCommand/BuildStateKey/BuildCommandKey/ValidateState를 소유 |
| 세션·저장 담당 | 새 `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationFulfillmentSession.cs`, `SimulationFulfillmentSaveReplay.cs`와 각 `.meta`; 새 `Ssalddel.Simulation.Tests/SimulationFulfillmentSessionTests.cs` |
| 저장 결합 회귀 | 새 `Ssalddel.Simulation.Tests/SimulationFulfillmentSaveCompatibilityTests.cs`. v33/v34 결합의 양 구성 순서·JSON 왕복·명령 종류 충돌을 검증 |
| 기존 세션 최소 hook | `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationSaveReplayContracts.cs`, `경영SimulationSessionContracts.cs`; `Ssalddel.Simulation.Domain/UnityPackage/Runtime/경영SimulationSession.cs`, `SimulationSaveReplay.cs`, `SimulationSaveReplayCloner.cs`, `SimulationReplayHasher.cs`, `SimulationSessionReplay.cs`. optional field·clone/hash·명령 저장/재생·검증만 변경 |
| 기존 명령 ID 충돌 검사 hook | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/SimulationDecisionWork.cs`, `SimulationWorldSurvivalInventory.cs`. Fulfillment와 기존 명령 종류 사이의 ID 충돌 검사에 필요한 최소 hook만 변경 |
| 가상 표본 구성 | 새 `Ssalddel.Simulation.Application/RuntimeCore/다중센터출고표본.cs`와 `.meta`. .NET과 Unity RuntimeCore가 같은 합성 두 센터·위치/상품/담당 조건을 사용하며 기존 세션에는 자동 주입하지 않음 |
| Runtime·서버 adapter 담당 | 새 `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/ISimulationFulfillmentRuntime.cs`와 `.meta`; 새 `Ssalddel.Simulation.Application/RuntimeCore/LocalSimulationFulfillmentRuntime.cs`와 `.meta`; 새 `Ssalddel.Simulation.Infrastructure/RemoteSimulationFulfillmentRuntime.cs`; 새 `Ssalddel.Simulation.Hosting/Controllers/SimulationFulfillmentController.cs` |
| 기존 서비스 최소 hook | `Ssalddel.Simulation.Application/경영Simulation물류창고Service.cs`의 `GetFulfillmentState`·`PreviewFulfillment`·`ConfirmFulfillment` delegate 세 메서드만 추가 |
| 기존 Solo Runtime 결속 | `Ssalddel.Simulation.Application/RuntimeCore/LocalSimulationRuntime.cs`는 선언에 `partial` 한 단어만 최소 병합. 새 `LocalSimulationRuntime.Fulfillment.cs`와 `.meta`에서 기존 `ExecuteAsync`·`RequireSession`을 이용해 같은 세션·직렬 명령 경계의 포트를 구현. 기존 작업 메서드·생성자·저장/방문자 로직은 변경하지 않음 |
| Unity 표시·adapter 시험 담당 | 새 `Ssalddel.Unity/Runtime/Warehouse/출고센터선택Presenter.cs`와 `.meta`; 새 `Ssalddel.Simulation.Tests/SimulationFulfillmentRuntimeIntegrationTests.cs`; 새 `Ssalddel.Unity.Tests/출고센터선택PresenterTests.cs` |
| 경로·책임 지도 정합성 | `Ssalddel.Simulation.Tests/SimulationServerHttpBoundaryTests.cs`는 신규 세 경로를 별도로 검사하며 종전 192개 경로의 hash를 보존. `docs/AI/generated/simulation-unity-code-map.json`·`.md`, `evidence-responsibility-code-map.json`·`.md`는 기존 도구로만 재생성하고 직접 수정하지 않음 |
| 기존 출처 검사 경로 정정 | `Ssalddel.Simulation.Tests/운영업무출처MetadataTests.cs`의 음식 평가/정렬 검사를 실제로 분리된 `음식배달후보평가Service`·`음식배달후보순위Policy` 호출로 갱신. 운영 평가 로직·실행 조건은 변경하지 않음 |
| 개발 통합·문서 담당 | 이 `multi-center-fulfillment.implementation.r7.md`, 상위 `README.md`의 관련 포인터, `docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`와 실제 검증 결과. 단일 문서 작성 위임자는 이 파일만 수정하고 상위 포인터는 개발 통합 담당이 소유. 운영 출고배치 Architecture/엔진은 변경하지 않으며 Simulation 상세 기준은 이 r7 |

수정 전 파일의 기존 dirty 내용을 다시 읽고 관련 없는 변경을 보존한다. 같은 공유 파일의 소유자가 다르면 통합 담당을 통해 경로·순서를 조율한다. 기존 Solo Runtime의 별도 Active 작업과 겹치는 변경은 위 선언 한 단어의 호환 병합에 한정하고 실제 새 실행 코드는 독립 파일에 둔다. `LocalSimulationRuntimeTests.cs`, 기존 Goal·주체·WI 원장과 자동 생성 실행 상태판은 이번 허용 경로에서 제외한다.

## 첫 표본을 연결하는 방법

첫 입력은 `다중센터출고표본`의 합성 자료다. 가까운 센터에는 재고가 있지만 현재 가능한 피킹 담당이 없고, 다른 센터는 선반·재고·단위가 맞는 담당자를 갖춘다. 설정과 주문을 명시적으로 넣은 새 회차에서 후보 사유와 선택 결과를 확인한다. 두 센터를 실재 지역/시설로 표시하지 않는다.

```csharp
var created = await runtime.Sessions.CreateAsync(
    다중센터출고표본.Create(Guid.NewGuid()));
var port = (ISimulationFulfillmentRuntime)runtime;
await port.ConfirmFulfillmentAsync(created.SessionStableId,
    new SimulationFulfillmentCommand
    {
        CommandId = "command:sample:configure",
        ExpectedRevision = created.Revision,
        ActionCode = SimulationFulfillmentActionCodes.Configure,
        InitialState = 다중센터출고표본.InitialState()
    });
var screen = new 출고센터선택Presenter(port,
    (id, token) => runtime.Sessions.GetAsync(id, token), created.SessionStableId);
await screen.조회Async();
await screen.주문선택Async(다중센터출고표본.Order());
// screen.센터후보 / 보류사유 / 선택센터목록 / 피킹지시를 표시한다.
await screen.예약확정Async("command:sample:reserve");
```

위 코드는 기존 `LocalSimulationRuntime`을 공유하는 소비 진입 예다. 센터/분할 조건을 바꾸면 미리보기만 바뀌고 예약 전에는 재고를 점유하지 않는다. 확정 뒤에는 `피킹지시`의 배분 ID·위치/상품 바코드·남은 수량을 사용해 `피킹확인Async`를 호출한다. 전량 피킹 후 `출고준비확인Async`로 `PackReady`를 확인하며 화면 표시는 **피킹 충족·포장 준비**다. 실제 포장 작업·기사 인수·배송 완료로 올리지 않는다.

명령 ID는 행위별로 구분하고 같은 행위의 확인 재시도에는 원래 ID와 입력을 유지한다. 응답이 불명확하면 화면은 자동 재전송하지 않고 현재 세션을 다시 조회한다. 이 Presenter와 표본을 추가한 것이 실제 Unity View/Bootstrap/Scene에 버튼과 선반 강조를 설치한 것은 아니다. 기존 게임·열린 Editor·진행 중 세션에 자동 주입하지 않는다.

## 6. 검증 계약과 실제 결과 기록

### 필수 확인 항목

- 두 센터 이상의 후보에서 내부 재고/위치/담당 조건을 평가하고, 부적격 사유와 대안을 반환한다. 허용 분할과 분할 금지·품절을 구분한다.
- Preview 무변경, 입력 상태의 깊은 보존, 입력 순서와 명령 재생의 결정성을 검증한다.
- 여러 센터 원자 예약, 같은 재고 중복 배분 방지, 가용 수량 경계와 예약/피킹 수량 보존을 검증한다.
- 동일 요청 재사용, 같은 ID 다른 payload 거절, stale 세션 revision 거절·현재 상태의 예약 재계획과 실패 후 상태 무변경을 검증한다.
- 올바른 위치·상품·예약·담당 확인, 부분 피킹, 과다/음수 피킹 거절, 미완성 PackReady 차단과 전체 피킹 후 PackReady를 검증한다.
- 미피킹 취소와 예약 해제의 반복 안전성, 이미 피킹한 계획의 취소 차단을 검증한다.
- optional 모듈 없는 이전 세션·save/hash의 호환과 모듈이 있는 세션의 저장·복원·명령 재생·변조 거절을 검증한다.
- 같은 세션에 대한 Local/HTTP GetState·Preview·Confirm의 결과, 요청 취소·오류·재조회와 Unity Presenter의 권위 결과 표시를 검증한다.

### E 단계와 증거 상한

| 단계 | 이번 확인 대상 | 현재 결과 |
| --- | --- | --- |
| E1 | r5/r6의 승인 범위·입력/결과·금지 경계와 계약 | 첫 출고 범위와 계약을 결속. 기존 WI/Goal의 단계는 승격하지 않음 |
| E2 | 순수 엔진·optional 세션 상태·권위 adapter | 같은 Solo Runtime/세션 및 테스트 서버 HTTP 연결·빌드 확인 |
| E3 | 단위 시험·결정성·실패 무변경·저장/재생 | 신규 출고 집중 65사례와 관련 전체 회귀 통과. 파일 슬롯 재시작·v33/v34 JSON 결합·충돌/변조 포함 |
| E4 | Local/HTTP 및 Unity Presenter 소비 경로 | Presenter의 .NET 소비 회귀 확인. 실제 View·공간 배치·표현 확정은 별도 미완료 |
| E5 | 실제 Unity World·Scene의 상태 발현 | 범위 밖·미검증 |
| E6 | 실제 공간/현실 자료 결속과 인과 판독 | 범위 밖·미검증 |
| E7 | 플레이 입력·Play Mode·Game View·귀환 | 범위 밖·미검증 |

검증은 관련 집중 시험과 영향 프로젝트 build를 우선한다. 실제 명령·필터·출력·실패/생략·source 판본은 개발 통합 담당이 실행 후 여기에 기록한다. 0-test·오래된 산출물·빌드 실패·미실행을 성공으로 올리지 않으며 Scene/Play 증거 없이 E5~E7·게임 플레이 완료를 선언하지 않는다. 자동 생성 Goal/상태판을 이 표로 대체하거나 직접 수정하지 않는다.

재현용 집중 시험 명령은 아래와 같다. 실제 실행 범위와 결과는 다음 절에 기록한다.

```text
dotnet test Ssalddel.Simulation.Tests/Ssalddel.Simulation.Tests.csproj --filter "FullyQualifiedName~SimulationFulfillmentEngineTests|FullyQualifiedName~SimulationFulfillmentSessionTests|FullyQualifiedName~SimulationFulfillmentRuntimeIntegrationTests"
dotnet test Ssalddel.Unity.Tests/Ssalddel.Unity.Tests.csproj --filter "FullyQualifiedName~출고센터선택PresenterTests"
```

저장·hash hook의 영향은 기존 SaveReplay/Session 관련 회귀와 함께 확인한다. Runtime/Controller/Presenter의 실제 build, HTTP 시험, Unity package 소비 결과는 실행 기록에서 구분한다. 실제 DB 설치·마이그레이션·영속 운영 검증이나 Unity 편집기 실행을 위 명령이 대신하지 않는다.

### 실제 실행 결과 (2026-10-25)

- `eng/validate-changes.ps1 -Level Fast -Paths <이번 경로> -NoRestore`: Simulation 관련 320 / Unity 관련 54 통과, 실패·생략 0. 두 솔루션 build·코드 지도·E 책임 지도·공백 검사 통과. 로그 `artifacts/local/validation/20261025-151452/`.
- `eng/validate-changes.ps1 -Level Task -Paths <이번 경로> -NoRestore`: Simulation 전체 2,066 / Unity 전체 978, 합계 **3,044 통과**, 실패·생략 0. 두 솔루션 build 및 코드/책임 지도 검사도 통과. 로그 `artifacts/local/validation/20261025-152209/`.
- 신규 `SimulationFulfillment*` 집중 65사례 통과. .NET Presenter 신규 8사례는 위 Unity 회귀에 포함한다. 이 수를 전체 회귀에 중복 합산하지 않는다.
- Local 실제 공유 Aggregate, 테스트 서버의 인증/세션 접근과 Remote adapter 왕복, 무변경 Preview, 전체 예약·부분 피킹·포장 준비, 잘못된 위치/상품·과다/단위·직원 수용량·stale·중복 명령을 검증했다. 새 Runtime의 로컬 파일 슬롯을 다른 Runtime 인스턴스에서 열어 예약 수량/hash 보존도 확인했다.
- v33 프랜차이즈와 v34 출고 모듈의 구성 순서를 바꾼 JSON 저장/복원과 부분 피킹 재개를 검사했다. 선택 모듈 없는 이전 세션은 기존 저장 판본을 유지한다.
- 초기 검증 중 Unity 시험의 restore 산출물 누락과 코드/책임 지도 갱신 필요를 해결했다. 확장 Fast에서 발견한 기존 음식 평가 검사 경로는 실제 분리된 평가·정렬 호출로 정정했으며 운영 로직은 변경하지 않았다. 첫 Task에서 새 호환성 시험의 필수 입력 로트/결과 후보 누락을 보완했고 위 최종 실행은 모두 통과했다. 실패 실행 로그도 해당 validation 폴더에 보존한다.
- 실제 Unity Editor 컴파일·View/Bootstrap/Scene 진입·Play Mode·Game View는 실행하지 않았다. 센터 디오라마/선반 강조의 실제 장면 연결은 후속이다. 운영 DB·실재 센터·실물 바코드·고용/급여·배송·모델 학습·배포·commit/push도 실행하지 않았다.

현재 예약이 확정된 같은 주문을 다른 센터로 바꾸는 전용 재계획 명령은 아직 없다. 확정 전 후보 선택/현재 판본 재계산과, 미피킹 취소에 따른 예약 해제까지를 구현했다. 이미 피킹한 물건의 반납·재배정과 함께 후속 실행 명세로 확장한다.

## 7. 제외 범위와 반환

이번 범위에 실제 운영 API·재고/주문/창고 DB 쓰기, 실물 스캔·장치, 실제 고용/임대/공급 계약, 금융·배차·인계·배송·회계 실행은 없다. 임대·직원 채용과 근무 관리 전체, 다중 소유권·냉장/기한·실제 판매 계약의 전체 검증, 단위 자동 환산, 여러 외부 시스템 보상, 피킹한 물품의 반납/격리·재배정 명령, 전역 최적화·실제 주행/실내 길찾기, 새 공식 Scene·Synty 배치, 실제 휴대폰·운영 배포, 영상 공개·AI 학습·운영 추천도 제외한다.

필수 입력이나 원자성·보존 계약이 빠지면 해당 명령/소비 경로만 차단하고 원인과 가장 이른 수정 단계를 반환한다. 임의의 0 수량·성공 fallback·새 권위로 채우지 않는다. 기존 저장·다른 WI·진행 중 세션과 공유 계약을 손상시키는 변경은 분리한다.

완료 보고는 **구현된 계약/상태/adapter, 실제 시험·build, 미검증 Runtime·Scene, 남은 명세·기능, commit/push 여부**를 나눈다. 첫 완결 범위를 통과해도 r5/r6 전체와 다섯 지역 플랫폼 경영 게임이 구현 완료됐다고 표시하지 않는다. 결과는 이 명세와 상위 PLAN의 관련 포인터로 반환하며 새로운 중복 인계 원장을 만들지 않는다.
