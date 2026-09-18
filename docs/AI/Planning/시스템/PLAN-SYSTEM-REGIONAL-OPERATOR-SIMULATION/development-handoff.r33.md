# [기획·개발 인계 · 운영·시뮬레이션 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 개발계획 r33·운영자 관점 복귀 r47]

- 기준일: 2026-09-16
- 기준 기획: `README.md` 문답·개발 인계 r47. r33 개발계획에 r34~r46 기준과 사가정역 출구 1~4 순서, 점심 피크 직전 운영자 관점 복귀를 결속했다.
- 기준 기획 SHA-256: `CD8C1AE7E020F559749BD69D4A987F9B6068E5A59C3BDCC7C0ED5A974798D5DD`
- 필수 보완 기획: 기존 r34~r46 보완 기획, `operator-perspective-return.r47.md`, `../PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-station-blender-integration.r30.md`, `../PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-exit-blender-sequence.r31.md`. Blender 기획은 일단 닫고 운영자 점심 피크의 첫 위험 판단을 다음 문답으로 유지한다.
- 인계 상태: `ReadyForScopedDevelopmentHandoff / OpenPolicyGatesRemain / NotReadyForOperationalRelease`
- 첫 개발 범위: `FoodDeliveryOS` 정상 흐름과 이미 확정된 예외 정책의 서버 우선 수직 절편
- 첫 구현 추천: `DEV-REGIONAL-OPERATOR-01 · 주문자 7단계 타임라인 읽기 모델`
- 권위 원칙: 운영 서버가 업무 사실을 확정하고, Simulation은 가상 정책·13주 전망을 소유하며, Unity는 같은 revision의 읽기 전용 상태 사본만 표현한다.

이 문서는 다른 GPT 계정이나 새 개발 스레드가 저장소 전체 대화를 다시 읽지 않고도 개발 순서와 중단 조건을 확인하도록 만든 인계 문서다. 기획 원문을 대체하지 않으며, 미정 정책을 구현자가 임의로 결정할 권한을 주지 않는다. 한 번에 전체 범위를 구현하는 승인이 아니라 아래 작은 절편을 순서대로 수용하는 기준이다.

## 1. 개발 목표

첫 목표는 사가정·면목 행정동 자료를 배경으로 한 가상 권역 운영자가 음식배달 운영을 관찰하고, 정책을 Preview한 뒤 Simulation에만 적용하고, 13주 동안 서비스 수준과 현금 안전성을 비교·원복할 수 있는 뼈대를 만드는 것이다.

이번 개발계획에서 직접 다룰 업무 흐름은 다음과 같다.

```text
주문자 주문
  → 음식점 수락·조리
  → 기사 배정·픽업·전달
  → 주문자 완료 관찰
  → 예외 발생 시 해당 사건 카드와 허용 조작
  → 분리된 지급·환급·준비금 의무
  → 가상 권역 운영자의 13주 결과 관찰
  → Unity 읽기 전용 표현
```

실제 배달권 자동 활성화, 실제 기사 모집 알림, PG 결제·정산, 실제 환급, 실제 통화 중계, 운영 약관 확정과 Unity의 업무 Command 실행은 포함하지 않는다.

## 2. 현재 구현 기준선

### 재사용할 서버 경로

| 책임 | 현행 기준 경로 | 현재 상태와 개발 의미 |
| --- | --- | --- |
| 주문 API | `Ssalddel/Controllers/Food/음식주문Controller.cs` | 주문·상세·수령 확인·취소와 음식점 수락·진행 route가 있다. 새 Controller를 만들기 전에 이 경계를 확장한다. |
| 주문 계약 | `Ssalddel.Contracts/Food/음식주문Dtos.cs` | 주문 상태, revision, `AvailableActions`, 주문자 취소 요청이 있다. 공개 식별자와 기존 상태값은 호환 유지한다. |
| 주문 원장 | `Ssalddel.Domain/음식/음식주문.cs`, `음식주문상태이력.cs` | 주문 상태와 이력은 존재하지만 음식점 수락과 실제 조리 시작을 충분히 분리해 증명하는 사건은 보완 대상이다. |
| 주문자 취소 | `Ssalddel/Application/Food/Handlers/주문자음식주문취소CommandHandler.cs`, `Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs` | 현재는 `주문대기`에서만 즉시 취소한다. 수락 후 취소의 `요청 → Preview → 확인`과 동결 사본은 없다. |
| 기사 수행 | `Ssalddel.Domain/음식/음식배달시도.cs`, `Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs` | 배정 수락·가게 도착·픽업·중단·전달 시각과 수행 revision을 재사용한다. |
| 배차·기사 업무 | `Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs` | 적격 기사 계산과 업무 확정 책임을 분리한 현행 경계를 유지한다. |
| 수명주기 조회 | `Ssalddel/Application/Food/음식배달수명주기SnapshotFactory.cs`, `음식배달수명주기조회UseCase.cs`, `Ssalddel/Controllers/Food/음식배달수명주기Controller.cs` | 정상 7단계와 예외 카드를 만드는 첫 읽기 모델의 주 재사용 지점이다. |
| 운영 추적 | `Ssalddel/Application/Admin/Food/음식주문운영추적UseCase.cs` | 운영자 관점의 사건·지연·예외 조회로 확장하되 주문 권위를 복제하지 않는다. |
| 주문자 공통 UI | `Ssalddel.Ui.Common/Areas/App/ViewModels/음식주문페이지ViewModels.cs`, `Services/음식주문Client.cs` | MAUI Blazor Hybrid와 Web이 같은 계약을 쓰게 한다. 현행 Client에는 취소 Preview·확정 경로를 별도로 추가해야 한다. |
| 역할 앱 | `OrdererApp`, `RestaurantDeskApp`, `FDriverApp` | 역할별 정상 업무와 사건 발생 시 예외 카드만 노출한다. 내부 운영 규칙 전체를 기본 화면에 펼치지 않는다. |
| E2E | `eng/Ssalddel.RoleAppHeadlessE2E/Program.cs` | 주문→수락→조리→기사→픽업→전달→수령의 기존 정상 폐루프를 모든 단계의 회귀 기준으로 유지한다. |

### 재사용할 Simulation·Unity 경로

| 책임 | 현행 기준 경로 | 경계 |
| --- | --- | --- |
| Simulation 음식배달 | `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식배달.cs` | 운영 상태를 임의 확정하지 않고 가상 상태·정책 결과를 소유한다. |
| 동네 투영 | `Ssalddel.Simulation.Application/Neighborhood/음식배달여정Projection.cs` | 서버 또는 Simulation 상태를 같은 revision의 관찰 자료로 변환한다. |
| Unity 계약 | `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/음식배달여정Contracts.cs` | 공개 가능한 최소 필드만 전달한다. 정확 주소·원시 GPS 관측 metadata·과거 이동 궤적·증거 사진은 넣지 않고, 필요하면 서버가 승인한 현재 표시 좌표만 별도 상태 사본으로 전달한다. |
| Unity 표시 | `Ssalddel.Unity/Runtime/Observation/동네관찰Presenter.cs`, `동네관찰SessionController.cs`, `Ssalddel.Unity/Presentation/음식배달관찰경로.cs` | 읽기 전용 표시와 상대 경로 후보가 있다. 공식 Scene은 `SimulationWorldShell` 하나이며 새 공식 Scene을 만들지 않는다. |

현재 정상 운영 폐루프와 합성 생활 표본은 재사용 근거이지, 아래 예외 흐름과 실제 지역 디오라마가 이미 결속됐다는 증거가 아니다.

## 3. 확정 정책과 구현 금지 추론

### 구현에 사용할 확정 기준

1. 주문자 정상 화면은 `주문 접수 → 음식점 수락 → 조리 중 → 기사 배정 → 픽업 → 배달 중 → 완료`의 7단계다.
2. 지연·취소·재조리·재배차·환급은 실제 사건이 생겼을 때만 별도 예외 카드로 보여 준다.
3. 음식점 수락과 조리 시작은 서로 다른 서버 사건이다.
4. 수락 후 취소는 취소 요청 시점의 음식점·기사·결제·취소 상태를 같은 revision으로 동결하고 금액 Preview 뒤 확정한다.
5. 조리 미시작·기사 배차 미수락이면 전액 환불 후보이며 음식점·기사 지급 의무를 만들지 않는다.
6. 픽업 전 기사 비귀책 취소수행대금은 `배정 수락 직후 < 음식점 이동 중 < 음식점 도착`의 단계별 정액이고 정상 완료 수행대금을 넘지 않는다.
7. 픽업 후 기사 비귀책 주문자 취소는 정상 수행대금 전액을 보호하지만 정상 전달 완료 건수로 세지 않는다.
8. 조리 지연 보호 해제는 기사 도착과 최초 조리 준비 예정시각 경과가 모두 성립할 때 열고, 주문은 취소하지 않은 채 수행 시도만 종료하고 보호 우선순위로 재배차한다.
9. 수령자 부재는 최대 3회 연락, 안전한 문 앞 전달 우선, 불가하면 고객센터 사건, 3분 미응답 뒤 정책표의 허용 조작, 증거 수락 뒤 기사 복귀 순서다.
10. 기사 수행대금, 음식점 상품대금, 주문자 환급, 조건부 환급 준비금, 플랫폼 고객지원 비용은 서로 다른 원장으로 유지한다.
11. `FoodDeliveryOS`의 일반 조리 음식은 픽업 후 취소·수령자 부재의 기본 처분을 안전한 현장 폐기로 두되, 회수·반환 의무가 강한 화물 규칙을 자동 상속하지 않는다.
12. 예외 내규는 현행 소비자분쟁해결기준·전자상거래법·약관·실제 손실·귀책과 대조하며, 충돌 가능성이 있으면 자동 확정하지 않고 사람 검토로 보낸다.

### 구현자가 정하면 안 되는 미정값

- 취소수행대금의 원화 금액과 단계별 차액
- 조리 단계별 음식점 보호금액·증거와 고객지원 준비금 상한
- GPS 전송 주기·정확도 하한·stale 판정 시간·지도 스냅 방식
- 실제 통화 중계·가상번호·callback 사업자와 계약
- 증거 보존기간·법적 보존 조치 승인 역할
- 실제 FoodDeliveryOS 회수 예외 품목·계약 목록
- 첫 운영 행정동 묶음, Simulation 시간 배율, 기사 부족 임계값과 알림 예산

이 값이 필요한 코드는 `정책 revision + 비활성 기본값` 또는 `Simulation 전용 Fixture`까지만 만들고 실제 운영 기본값을 발명하지 않는다.

## 4. 목표 구조

```text
역할 앱
  ├─ 주문자: 정상 7단계 + 발생한 예외 카드 + 본인 허용 조작
  ├─ 음식점: 주문·조리·인계 사실과 본인 허용 조작
  ├─ 기사: 배정·도착·픽업·전달 + 보호 해제·증거 제출
  └─ 운영자: 사건 queue·책임 후보·기한·재무 영향
             │
Controller API → UseCase/Command → FoodDeliveryOS 원장·Event/Outbox
             │                         ├─ 주문·조리 사실
             │                         ├─ 기사 수행 시도
             │                         ├─ 예외 사건·증거 봉투
             │                         └─ 분리된 재무 의무
             │
             ├─ 역할별 읽기 모델과 AvailableActions
             ├─ 13주 Simulation 입력 사본
             └─ 비식별 Unity 관찰 사본 → SimulationWorldShell
```

Engine과 Policy는 후보·금액 산식·이유 Code·다음 확인 시각을 계산할 수 있지만 상태·지급·환급·배차를 확정하지 않는다. UseCase가 권한, 예상 revision, 멱등 key, 현재 상태와 정책 revision을 확인한 뒤 원장을 변경하고 같은 원장을 재조회한다.

## 5. 단계별 개발계획

### 단계 0 — 착수 감사와 작업 명세 결속

목적은 새 계정이 오래된 대화나 문서만 믿고 코드를 덮어쓰는 일을 막는 것이다.

- `git status --short --branch`와 가까운 `AGENTS.md`를 확인한다.
- `README.md` r47, 이 문서 r33, `operator-perspective-return.r47.md`, 기존 r42~r46 보완 기획, 역세권 `sagajeong-station-blender-integration.r30.md`, `sagajeong-exit-blender-sequence.r31.md`, `PLANNING.md`, `CURRENT_WORK.md`, `OperationsSimulationUnity작업흐름분리.md`, `BusinessWorkflowResponsibilityModel.md`를 읽는다.
- 위 표의 route·contract·handler·store·domain·role app·Simulation·Unity 경로가 실제로 존재하는지 다시 확인한다.
- 절편 하나당 WI 하나와 E1~E7 상호작용 수직 검증 명세를 만든다. 승인된 기획 revision/hash, 수정 경로, 제외 범위와 검증 상한을 기록하되 Goal을 자동 활성화하거나 E를 자동 승격하지 않는다.
- 대규모 공통 추상화나 새 OS를 먼저 만들지 않는다.

완료 조건은 첫 절편의 정확 쓰기 경로와 회귀 시험 목록이 동결되고, 같은 파일을 수정 중인 다른 작업과 소유권 충돌이 없는 것이다.

### 단계 1 — `DEV-REGIONAL-OPERATOR-01` 주문자 7단계 타임라인

가장 먼저 할 수 있는 읽기 전용 수직 절편이다. 기사 위치 공개와 금액 정책을 요구하지 않는다.

1. `음식배달수명주기SnapshotFactory`가 기존 주문·기사 수행 사건을 7단계로 파생한다.
2. 아직 도달하지 않은 단계, 현재 단계, 완료 단계와 예상시각 범위를 구분한다.
3. `AvailableActions`는 서버가 현재 revision에서 허용한 조작만 제공한다.
4. 기존 주문자 상세 계약을 호환 확장하고 `OrderDetail.razor`에서 타임라인을 표시한다.
5. 예외가 없으면 예외 카드 영역을 만들지 않는다. 기존 취소 버튼의 동작을 이 단계에서 넓히지 않는다.

완료 조건:

- 기존 정상 Headless E2E가 7단계를 순서대로 관찰한다.
- 역할이 다른 사용자가 주문 상세·허용 조작을 볼 수 없다.
- 오래된 revision으로 조작할 수 없다.
- MAUI Blazor Hybrid와 Web이 같은 `Ssalddel.Ui.Common` ViewModel·Client를 소비한다.
- Unity·Simulation 코드는 변경하지 않는다.

### 단계 2 — `DEV-REGIONAL-OPERATOR-02` 수락 후 취소 Preview·확정

첫 쓰기 절편은 가장 명확한 `조리 미시작 + 기사 배차 미수락` 전액 환불 후보부터 닫는다.

1. 취소 요청 자체와 취소 확정을 분리한다.
2. `CancellationStageSnapshot` 후보 원장에 주문, 조리, 유효 기사 수행, 결제 상태와 각 revision·서버 시각·정책 revision을 동결한다.
3. 순수 Policy는 취소 가능 여부, 주문자 예상 환급, 음식점 보호, 기사 보호, 플랫폼 준비금 후보와 이유 Code를 계산한다.
4. Preview 응답은 만료시각과 source revision을 포함한다.
5. 확정 Command는 Preview revision과 현재 원장을 다시 비교하고 다르면 `409` 계열 충돌로 새 Preview를 요구한다.
6. 동일 멱등 key 재처리는 같은 결과를 반환하며 지급·환급·Event를 중복 생성하지 않는다.
7. 실제 PG 환급 Adapter는 기본 비활성이다. Simulation/FakePG에서만 결과를 닫고 운영 모드에서는 승인되지 않은 외부 효과를 차단한다.

이 단계에서 기존 `주문자음식주문취소요청`과 공개 route의 호환 전략을 먼저 정한다. 기존 `주문대기` 즉시 취소를 깨지 말고, 수락 후 취소에만 Preview·확정 계약을 추가하는 점진 이행을 우선한다.

완료 조건:

- 수락·조리 시작·배차 수락과 취소 요청의 경합 시험이 서버 사건 순서와 예상 revision으로 결정된다.
- 소급 조리 시작으로 이미 산출된 환불 기준을 바꿀 수 없다.
- Preview만으로 주문·지급·환급 상태가 변하지 않는다.
- 주문자·음식점·기사·플랫폼 금액 후보가 한 필드를 나눠 쓰지 않는다.
- 실제 금액 미정 구간은 운영 실행 차단 상태이며 Fixture 금액을 운영 정책으로 노출하지 않는다.

### 단계 3 — `DEV-REGIONAL-OPERATOR-03` 조리 지연 보호 해제와 재배차

1. 음식점 수락 때의 최초 준비 예정시각과 이후 변경 이력을 보존한다.
2. 기사 가게 도착 서버 사건을 수행 시도 revision에 결속한다.
3. `현재시각 - 최초 준비 예정시각`과 `현재시각 - max(기사 도착시각, 최초 준비 예정시각)`을 서로 다른 지연 시계로 계산한다.
4. 조건이 성립하면 기사 `AvailableActions`에 `FoodDelivery.ReleaseAssignmentForPreparationDelay`를 연다.
5. 선택 시 주문은 유지하고 현재 수행 시도만 `ProtectedRelease`로 닫으며 기사를 가용 상태로 복귀시킨다.
6. 주문을 `PreparationDelayProtected` 우선순위로 재배차 queue에 넣는다. 상위 주문에 적격 기사가 없어도 다른 주문 배차를 막지 않는다.
7. 같은 주문에는 한 시점에 유효 배차 시도 하나만 허용하고 이전 기사는 기본 재추천에서 제외한다.

완료 조건은 지연 조건·경합·멱등·재탐색·병렬 주문·기사 비귀책·주문 비취소가 자동 시험에서 함께 증명되는 것이다. 새 현장 지연 금전 보상은 만들지 않는다.

### 단계 4 — `DEV-REGIONAL-OPERATOR-04` 수령자 부재 보호 종료

보안·개인정보·외부 Adapter가 포함되므로 정상 흐름과 분리해 개발한다.

1. 연락 버튼 요청과 실제 발신·연결 callback을 서로 다른 사건으로 기록한다.
2. 최대 3회 연락, 문 앞 전달 가능성, 고객센터 사건 개설, 3분 서버 시간 제한을 상태 기계가 아니라 사건 원장과 `AvailableActions`로 연결한다.
3. 담당자 명령과 자유 문장 채팅을 분리한다.
4. 미응답 뒤 안전 정책표가 `DoorDropAtVerifiedSafePlace`, `ReturnToMerchant`, `DisposeOnSite` 중 가능한 조작만 연다.
5. 처분 사진은 보호 저장소 object key, SHA-256, 촬영·수락시각, 사건·주문·수행 revision을 연결한다. 원본은 Unity·공개 API·일반 운영 카드에 포함하지 않는다.
6. 증거 수락 뒤 수행을 보호 종료하고 기사를 재무 후처리와 무관하게 다음 배차 후보로 복귀시킨다.
7. 주문자 부재의 주문 청구, 음식점 상품대금, 기사 정상 수행대금, 조건부 환급 준비금을 분리한다.

완료 조건은 연락/증거 실패 때 완료 확정 금지, 3분 재시작 방지, 늦은 담당자 명령의 revision 충돌, 사진 접근권한·보존정책, 중복 처분 방지와 기사 복귀가 검증되는 것이다. 실제 통화 사업자와 법적 보존기간이 미정이면 Fake callback·보호 저장소 계약까지만 닫는다.

### 단계 5 — `DEV-REGIONAL-OPERATOR-05` 역할 앱 예외 카드

- 주문자: 정상 7단계, 취소 Preview·확정, 본인 환급 상태와 문의 경로만 표시한다.
- 음식점: 조리 시작·완료 사실, 취소 시 본인 보호 상태와 필요한 조작만 표시한다.
- 기사: 정상 완료 수행대금을 주 금액으로 유지하고 실제 취소 사건 뒤에만 단계·정책 revision·취소수행대금 상태를 상세 표시한다.
- 운영자: 사건 담당, 기한, 증거 충돌, 책임 후보와 상향 queue를 표시한다.
- 모든 앱은 변경 성공 뒤 canonical 원장을 다시 조회한다.

예외가 없는 정상 화면에 폐기·분쟁·내규를 상시 노출하지 않는다. 다만 소비자에게 불리한 제한과 비용은 주문·취소 결정 시점의 고지에서 숨기지 않는다.

### 단계 6 — `DEV-REGIONAL-OPERATOR-06` 13주 운영·현금 Simulation

운영 원장을 직접 변경하지 않는 별도 Simulation 절편이다.

1. 입력 사본은 자료 기준시각, source revision/hash, 행정동·가상 권역, 수요·가용 기사·처리시간 분포와 재무 가정을 가진다.
2. 각 업무 Event를 당사자·금액·발생시각·지급기일·원인 revision을 가진 기준중립 `FinancialEvent`로 투영한다.
3. 원장은 가용 운영현금, 제한 자금, PG미수, 음식점·기사·지사 미지급 의무, 고객 환급의무, 조건부 준비금, 플랫폼 수익·비용 후보와 성장 예산을 분리한다.
4. 기준·점심 피크·스트레스·회복 scenario에서 가용현금 최저점, 미지급 의무, 소진 기간과 서비스 수준을 계산한다.
5. 정책은 `Draft → Previewed → ActiveInSimulation → Expired/RolledBack`만 변경한다.
6. 같은 seed·입력·정책 revision·계정 매핑 판본은 같은 결과와 hash를 만든다.
7. 회계사 역할은 미대사·미매핑·미승인 분류와 현금 위험을 조언하지만 실제 전표·지급·환급을 확정하지 않는다.
8. 기사 부족 알림은 가상 알림함만 사용하고 실제 FCM·문자·기사 앱을 호출하지 않는다.
9. 별도 회계사 앱·화면·상위 메뉴를 만들지 않고 기존 운영자 읽기 모델에 재무 요약·예외·상세 참조를 결합한다.
10. 정상 사건은 자동 집계·대사하고 관련 예외만 격리한다. 운영자 확인 판본은 주간·월간·13주 단위로 분리한다.
11. 주간 마감 projection은 정산 총액·정산 후 가용현금·정산 확인 필요를 반환하며 완료율과 정상 건별 목록은 기본 응답에서 제외한다.
12. 월간 projection은 전월 비교와 서원 회고를 분리한다. 서원 자체로 재무 수치·배차·보상·불이익을 변경하지 않는다.
13. `안정 운영 유지`는 유효한 기본정책 판본을 참조한다. 판본·승인값이 없으면 실제 효과를 차단하고 `정책 기준 미확정`을 반환한다.

정확 임계값·예산·시간 배율이 미정인 동안은 정책 비교 도구와 Fixture scenario만 구현한다. 20억원은 외부 확정 자본이 아닌 내부 비교 입력이다.

### 단계 7 — `DEV-REGIONAL-OPERATOR-07` Unity 읽기 전용 권역 관찰

서버·Simulation의 같은 revision 상태 사본이 안정된 뒤에만 시작한다.

1. 기존 `SimulationWorldShell`과 행정동 디오라마 배경을 유지한다.
2. 사가정역 1km 관찰 창은 자르거나 다시 만들지 않고 시각 회귀 기준으로 유지한다. 면목제3·8동을 주 운영·집계 권위로, 면목제7동 등은 자기 귀속을 유지한 인접 맥락으로 표시한다.
3. 주문·음식점·기사·예외 사건의 공개 가능한 일반화 상태만 Decoder·Client·Interpreter·메모리 모델에 넣는다.
4. 정확 주소, 연락처, 사진, 원시 GPS 관측 metadata와 과거 궤적, 내부 책임 비율과 지급 상세를 제외한다. 서버가 승인한 현재 표시 좌표만 주문 결속·최신성·공개 종료시각과 함께 허용한다.
5. 주문자 앱은 유효 배차 수락부터 전달 완료·취소·배차 종료 전까지 서버가 수락한 최신 기사 위치를 실시간으로 표시한다. 본인 주문 소유권과 현재 수행 기사 결속을 매 조회·구독에서 검증한다.
6. 위치 갱신이 끊기면 마지막 갱신시각과 stale 상태를 표시하고, 클라이언트 보간 위치를 서버 사실처럼 표시하지 않는다. 종료 뒤 구독을 닫고 과거 원시 GPS 궤적은 주문자에게 제공하지 않는다.
7. Unity 운영 관찰 프로필은 승인된 현재 위치 사본만 투영한다. 원시 GPS·개인 이동 이력을 내보내거나 가상 NPC 위치와 실제 기사 위치를 같은 권위로 합치지 않는다.
8. GameObject 이동·Animation·카드가 주문·배차·완료·지급을 확정하지 못하게 한다.
9. 기존 상대 경로 후보를 실제 GIS 주행 권위로 승격하지 않는다. 행정동 디오라마의 검증된 도로·보행·정차 연결 자료가 없으면 `Unresolved`로 남긴다.
10. 첫 실제 생활상 판독 후보는 면목제3·8동 안의 합성 음식점·기사·전달지로 구성한 점심 정상 배달 1건이다. 같은 `WorkStableId`·revision의 정상 7단계를 먼저 닫고 예외 흐름은 별도 절편으로 남긴다.
11. Actor 이동 전 현행 면목제3·8동 대상 건물을 실자료 기반 표현 또는 `MissingCoverage`로 전수 판정한다. 검증된 건물·기준점·연결 구간만 경로 이동에 쓰고 결손 구간은 `이동 중` 상태로 표현한다.
12. 전수 건물의 첫 표현은 공공데이터 기반 절차적 매스로 만들고 중요 건물만 근거·이용조건 확인 뒤 Blender로 상세화한다. 교체는 `VisualRoot`에 한정하고 안정 식별자와 업무 결속을 유지한다.
13. 첫 Blender 상세화 묶음은 사가정역 출구 주변이며 역세권 r8·r9·r11·r26·r30을 정본으로 사용한다. 권리·현행성·모델 브리프가 닫히기 전 Blender·Unity 작업을 열지 않는다.
14. 출구 상세화 순서는 1→2→3→4로 보존하되 Blender 문답은 일단 중단한다. 운영 구현 판단은 점심 피크 직전의 미배차·지연·수급·재무 위험과 `AvailableActions`로 돌아간다.

완료 조건은 계약 판본 거절, projection hash 불일치, 행정동 전환 때 메모리 삭제, 중복 객체 방지, 화면과 서버 상태의 revision 표시를 EditMode와 실제 Play/Game View에서 별도 확인하는 것이다. 자동 시험만으로 실제 화면 검증이나 E 승격을 선언하지 않는다.

### 단계 8 — 운영자 정책·카드 표현 확장

일반 운영 폐루프와 현금 Simulation이 안정된 뒤에 진행한다.

- 점심 피크 기사 부족 Preview와 사전 승인 범위의 가상 알림 자동화
- 기본값 유지·정책 비교·적용·만료·원복
- 절기·제철 자료의 출처 있는 현실 관측층
- 아르카나·주역 카드의 가상 수요·위험·회복 비용 표현

카드가 실제 배차·정산·공공자료 값을 성공시키거나 증거 단계를 올리지 못하게 한다. 첫 개발 묶음의 선행조건이 아니다.

## 6. 개발 묶음과 의존 관계

| 묶음 | 선행 | 병렬 가능 | 시작 관문 |
| --- | --- | --- | --- |
| 01 정상 7단계 | 단계 0 | 서버 조회와 주문자 UI 시험 | 즉시 가능 |
| 02 취소 Preview | 01 계약 기준선 | Policy 단위 시험·FakePG Adapter | 정확 원화 없이 첫 전액 환불 절편 가능 |
| 03 조리 지연 | 01, 최초 준비 예정시각 사건 | queue Policy 시험 | 새 금전 보상 제외 조건으로 가능 |
| 04 수령자 부재 | 01, 보호 증거 설계 | Fake callback·권한 시험 | 보존기간·실제 제공자는 운영 전 차단 |
| 05 역할 앱 | 각 서버 절편 | 역할별 앱은 파일 소유가 겹치지 않을 때 병렬 가능 | 공유 계약 revision 동결 후 |
| 06 13주 Simulation | 02~04의 원인·의무 Code | 순수 Simulation 엔진과 Fixture | 실제 정책값 없이 비교 시나리오만 가능 |
| 07 Unity | 안정된 읽기 계약, 디오라마 자료 준비 | Decoder·메모리 시험 | 서버 수락 현재 위치만 사용하고 종료 뒤 구독·메모리 제거 |
| 08 카드 확장 | 06~07 | 표현 조사 | 일반 운영 폐루프 안정 후 |

개발 담당은 여러 묶음을 한 커밋에 섞지 않는다. 공유 계약 변경, 서버 쓰기, 역할 UI, Simulation, Unity 표현, 문서·대장 갱신을 맥락별 커밋 후보로 나눈다. 실제 commit·push는 현재 작업 지시가 있을 때만 수행한다.

## 7. 시험과 증거 계획

### 서버·계약

- 정상 주문 Headless E2E 회귀
- 각 역할의 인증·소유권·권한
- 예상 revision 충돌과 멱등 재처리
- DB 트랜잭션과 Event/Outbox 재처리
- Preview 무변경과 확정 뒤 canonical 재조회
- 같은 사건의 지급·환급·준비금 중복 생성 방지
- 기존 공개 상태값과 route의 역호환

### 역할 앱

- 서버 `AvailableActions` 외 버튼 비활성·미표시
- 주문자·음식점·기사·운영자별 정보 최소 공개
- 예외가 없을 때 정상 7단계만 표시
- 예외 발생 때 원인·영향·다음 조작·예상 금액과 문의 경로 표시
- MAUI Blazor Hybrid와 Web 소비 계약 회귀

### Simulation

- 같은 seed·입력 revision·정책 revision의 결정성
- 무입력 기본값, Preview 무변경, 적용·만료·원복
- 13주 Save/Replay와 LocalProcess/RemoteHost 동등성
- 가용현금 최저점과 분리 원장의 보존식
- 운영 Adapter·FCM·PG가 Simulation에서 호출되지 않는지 확인

### Unity

- schemaVersion·sourceVintage·projectionHash·readinessCode 거절
- 같은 업무 객체의 중복 생성 금지와 행정동 전환 시 메모리 정리
- 연락처·보호 증거·원시 GPS metadata·과거 궤적 미포함, 승인된 현재 표시 좌표만 포함
- 실제 Play Mode·Game View에서 단계·예외·NPC 표현 판독
- Unity 입력과 Animation이 서버 revision을 바꾸지 않음

### 검증 실행 순서

1. 수정 직후 `eng/validate-changes.ps1 -Level Fast -Paths <이번 파일>`
2. 절편 완료 전 `-Level Task -Paths <이번 파일>`
3. 공유 계약 변경 시 서버뿐 아니라 해당 역할 앱과 Simulation/Unity 소비자까지 검증
4. 로컬 MySQL을 쓰는 절편은 저장 후 독립 재조회·동일 입력 중복 방지를 확인
5. 실제 외부 효과, 실제 Scene/Game View, commit, push는 각각 별도 사실로 보고

상세 결과는 `artifacts/local/validation/`에 두고 `CURRENT_WORK.md`에는 성공·실패·첫 오류·실행하지 않은 항목을 구분한다.

## 8. 중단·반환 조건

다음 상황에서는 개발자가 추측으로 계속하지 않고 같은 Goal의 가장 이른 책임으로 반환한다.

- 음식점 수락과 조리 시작을 현행 원장에서 구분할 수 없음
- 취소 순간 주문·기사·결제 revision을 한 동결 사본으로 만들 수 없음
- 정확 금액·기사 위치·보존기간처럼 미정값이 없으면 다음 상태를 결정할 수 있음
- 실제 약관·소비자분쟁기준과 자동 정책의 충돌 가능성 발견
- Unity 표현을 위해 정확 주소·GPS·사진을 공개 계약에 넣어야 하는 구조
- 다른 작업의 dirty 파일과 같은 계약·migration·Scene을 안전하게 병합할 수 없음
- 기존 정상 Headless E2E 또는 운영 주문 route 호환이 깨짐

반환 보고에는 발견 사실, 영향을 받은 기획 항목, 가장 이른 재개 단계, 임시 우회가 권위를 바꾸는지 여부를 기록한다.

## 9. 다른 GPT 계정의 첫 작업 지시문

아래 문장을 새 GPT 계정의 첫 요청으로 사용할 수 있다.

아래 지시문의 목록과 함께 `operator-perspective-return.r47.md`, 기존 r42~r46 보완 기획, 역세권 `sagajeong-station-blender-integration.r30.md`, `sagajeong-exit-blender-sequence.r31.md`를 반드시 읽는다. Blender는 1→2→3→4 순서만 보존하고 새 제작을 시작하지 않으며, 점심 정상 배달과 운영자 위험·허용 조작의 서버 우선 절편에 집중한다.

> 저장소의 `AGENTS.md`, `docs/ProjectOverview/GptProjectContext.md`, `docs/AI/DECISIONS.md`, `docs/AI/CURRENT_WORK.md`, `docs/AI/PLANNING.md`, `docs/AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/README.md`, `development-handoff.r33.md`, `accountant-role-and-ledger.r34.md`, `accounting-operating-conventions.r35.md`, `operator-integrated-accounting.r36.md`, `operator-integrated-accounting-summary.r37.md`, `exception-first-financial-review.r38.md`, `weekly-settlement-summary.r39.md`, `monthly-performance-and-vow.r40.md`, `stable-operations-vow-policy.r41.md`를 순서대로 읽어 주세요. 먼저 git branch와 dirty worktree를 확인하고 관련 없는 변경을 보존하세요. 구현은 `DEV-REGIONAL-OPERATOR-01 · 주문자 7단계 타임라인 읽기 모델` 하나만 수용하세요. 기존 `음식배달수명주기SnapshotFactory`, 음식 주문 계약·Controller, `Ssalddel.Ui.Common`, `OrdererApp`, Headless E2E를 재사용하고 Unity·Simulation·취소 쓰기 경로는 수정하지 마세요. 별도 회계사 앱·화면·상위 메뉴를 만들지 말고 운영자 표시 문구는 `정산 확인 필요`를 사용하세요. 정상 경로는 자동화하고 관련 예외만 격리하며 주간·월간·13주 확인 판본을 분리하세요. 주간 마감 결과에는 정산 완료율을 넣지 마세요. 월간 수치 비교는 전월 실적만 사용하고 서원을 회계 계획으로 취급하지 마세요. 기본 서원은 승인된 정책 판본을 참조만 하며 미승인 값을 발명하거나 활성화하지 마세요. 코드 전에는 이 절편의 E1~E7 상호작용 수직 검증 명세와 정확 쓰기 경로·회귀 시험을 작성하고, 충돌이 없을 때만 구현하세요. 완료 시 코드·시험·실행 화면·DB·commit·push를 각각 분리해 보고하고, 미정 정책은 임의 결정하지 마세요.

## 10. 첫 절편 이후 인계 순서

첫 계정이 01을 완료하면 다음 계정 또는 같은 계정은 결과를 재조회하고 아래 순서로 한 절편씩 수용한다.

```text
01 정상 7단계
  → 02 수락 후 취소 Preview·확정
  → 03 조리 지연 보호 해제·재배차
  → 04 수령자 부재 보호 종료
  → 05 역할 앱 예외 카드 통합
  → 06 13주 운영·현금 Simulation
  → 07 Unity 읽기 전용 관찰
  → 08 운영자 정책·절기·카드 확장
```

각 절편은 앞 절편의 구현 완료를 문서 상태만으로 믿지 않고 contract, test, 실행 설정과 저장 결과를 다시 확인한다. 서버 사실, Simulation 결과, Unity 표현을 하나의 완료 증거로 합치지 않는다.

## 11. 기사 위치 구현 계약과 다음 기획 질문

주문자 실시간 기사 위치는 확정됐다. 구현은 기사 GPS 수집, 서버 최신 위치 판정, 주문 소유권이 적용된 조회·실시간 전송, stale 처리, 종료 구독 해제와 감사 시험을 하나의 수직 절편으로 묶는다. 정확 전송 주기와 위치 품질 기준은 부하·배터리 측정 뒤 설정 판본으로 정하되 운영자가 코드 배포 없이 조정할 수 있게 한다.

다음 기획 질문은 취소수행대금의 첫 Simulation 단계별 정액 비율이다. 실제 계약 금액을 확정하기 전, 정상 완료 수행대금을 기준으로 `배차 수락 직후 / 음식점 이동 중 / 음식점 도착` Fixture를 어떤 비율로 둘지 선택해야 13주 현금 스트레스 시험을 재현할 수 있다. 추천 후보는 `25% / 50% / 75%`이며 실제 운영 금액·약관·기사 계약을 확정하는 값은 아니다.
