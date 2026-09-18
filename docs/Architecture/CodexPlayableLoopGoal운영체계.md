# Codex PlayableLoop Goal 운영 체계

## 목적

짧은 이동·대기 시간에 사용자와 Codex가 개발을 이어가더라도 큰 목적을 잃지 않도록 장기 Goal과 실제 구현 작업을 분리한다. 현행 Goal은 준비된 주체 사이의 WI 하나를 소유하며 H·E·G·WI 전체 체계를 하나의 작업으로 뭉치지 않는다. 여러 WI의 반복 폐쇄성을 함께 검증해야 할 때만 PlayableLoop를 선택적으로 연결한다. 독립적인 구현은 병렬 진행하고 실제 의존성과 수정 충돌만 조율한다.

```text
Goal = WI 하나
Subject = 권위·역할·저장 정체성이 준비된 행위 주체 또는 직접 대상
WI = 두 주체 사이의 단일 행동·직접 결과 책임
PlayableLoop = 필요할 때만 연결하는 반복 폐쇄성 검증 묶음
WorkItem = 담당·기준선·수정 범위·의존성을 가진 구현 또는 검증 작업
E = 완료를 입증하는 증거 성숙도
H = 현재 폐루프가 요구하는 공간 능력
G = 다음 E로 가기 위한 관리·검토 체계
EvidencePackage = 시험·저장·Runtime·화면·Hosted 증거 묶음
```

`AreaAggregate`와 `WorldAggregate`는 Goal이 아니다. 필수 WI Goal 또는 선택한 `PlayableUnit` 자식의 상태에서 파생하는 완결 이정표다.

## 설계실·제작실 인계

설계실은 **무엇을 만들 것인가**, 제작실은 **승인된 것을 어떻게 구현·검증할 것인가**를 소유한다. 계정 금액·모델·시간대는 역할 권한이나 성능 보장이 아니다. 기존 기획 → 단일 WI Goal → 작업 명세 → 개발 통합 → 기획 반환을 사용하며 계정별 원장이나 별도 `GOAL-xxx/README.md` 체계를 만들지 않는다.

- 설계실: `PLANNING.md`에서 현재 기획을 찾고 목적·주체·직접 결과·선택·대가·범위·완료 조건을 확정한다. 필요한 연구를 결속하고 기존 승인 기획·작업 명세를 갱신한다. 여러 WI의 학습 과정은 상위 묶음이며 실제 Goal은 WI별로 나눈다. 괘·효는 선택적 영감과 참조 이력이다.
- 제작실: 명시적으로 선택한 Goal의 인계 요약을 읽고 `Check`로 현재 코드·소유권·승인 기준선을 대조한다. 점검 통과만으로 대기 Goal을 활성화하거나 다음 Goal로 자동 진행하지 않는다.
- 기존 경계 안의 구현 결함·시험·표현 연결은 제작실이 해결한다. 플레이어 약속·WI 의미·공유 계약·승인된 표현 기준·쓰기 범위 변경은 해당 작업만 중단해 기획 또는 개발 통합 담당에 반환한다. 관련 없는 작업까지 잠그지 않는다.
- 결과는 아래 **개발 → 기획 반환**의 다섯 항목을 사용한다. 작업 명세에 연결된 원문 문서에 결과를 남기고 통합 상태판과 `CURRENT_WORK.md`는 최신 요약만 유지한다. 별도의 중복 `IMPLEMENTATION-HANDOFF.md` 원장을 만들지 않는다.

### 인계 설명과 모듈 관계

기존 E1~E7 상호작용 수직 검증 명세의 선택적 `handoffNotes`는 `purpose`, `excludedScope`, `moduleRelations`, `readRefs`, `firstTask`, `completionCriteria`, `validationCommands`, `decisionBoundary`, `returnInstructions`를 가진다. 모듈 관계·읽을 파일·검증 명령은 문자열 배열이고 나머지는 짧은 설명이다. 기존 명세 검증은 이 필드 없이도 호환되지만 인계 도구는 설명 누락을 보고한다. 승인·Goal 상태·담당·쓰기 범위·E는 설명에서 읽거나 덮어쓰지 않는다.

모듈 관계는 **입력 → 처리 담당 → 권위 상태 → 조회 결과 → Unity 표시** 순서로 적으며 각 연결의 기존·수정·신규·미연결을 명시한다. 시작 시 주입과 실행 중 호출을 구분한다. 목적·관계·첫 수정 위치를 앞에 두고 E/H 상세는 기존 명세로 연결한다. 음식점 조리 명세의 설명은 인계 도구의 읽기 전용 표본이며 신규 게임 실행을 승인하지 않는다.

### 생성과 시작 전 점검

도구: [`manage-development-handoff.ps1`](../../eng/execution-ledgers/manage-development-handoff.ps1). PowerShell 7과 `git`, `rg`가 필요하다.

```powershell
pwsh -NoProfile -File eng/execution-ledgers/manage-development-handoff.ps1 -Mode Export -GoalId interaction-goal:restaurant-cooking.v1 -UnityProjectRoot C:/Users/user/ssalddel
pwsh -NoProfile -File eng/execution-ledgers/manage-development-handoff.ps1 -Mode Check -GoalId interaction-goal:restaurant-cooking.v1 -UnityProjectRoot C:/Users/user/ssalddel -BaselinePath artifacts/local/development-handoffs/interaction-goal-restaurant-cooking.v1/handoff.json
```

- `GoalId`는 현행 단일 WI Goal의 정확 ID다. 기존 Loop 호환 Goal도 주체·상호작용 상태판의 투영 ID로 선택한다. 여러 작업이 연결되면 `WorkItemId`가 필수이며 다른 WI/Goal 작업을 지정하면 거부한다.
- `Export`는 `artifacts/local/development-handoffs/<Goal과 작업의 안전한 이름>/handoff.md`와 `handoff.json`을 생성한다. 같은 대상 재생성은 그 요약을 교체하므로 **제작실은 전달받은 JSON을 보존하고 재생성 전에 Check한다**. JSON은 선택 원장 항목의 hash, 실제 입력 파일 hash, 저장소별 HEAD, 원본 참조와 차단·경고를 담는다. 같은 입력은 같은 출력을 만든다. 생성 파일을 직접 수정하지 않는다.
- `Check`는 파일을 쓰지 않고 JSON 결과를 표준 출력으로 반환한다. 차단이 있으면 예외/비정상 종료를 반환한다. Export는 차단 상태에서도 검토 자료를 남긴다. `ChecksPassedNotExecutionApproval`은 현재 점검 통과일 뿐 실행 권한·시험 성공·Evidence 승격이 아니다.
- 기존 주체·작업 명세 검증기를 재사용한다. 전역 주체 원장 검사 오류는 별도 `CatalogValidation` 경고이고, 선택한 명세·작업 검증 오류는 차단이다. 담당 누락, 공유 파일·계약 충돌, 선행 통합 미완료, 승인/판본 불일치, 설명·필수 입력 누락을 추정으로 보충하지 않는다. 담당은 기존 work item의 `ownerThreadId`, 없으면 명세의 명시적 `ownerThreadId`에서만 읽는다.
- `requiredResearch: []`는 필수 연구 없음의 명시적 선언이다. 연구가 있으면 각 항목의 `statusCode=Accepted`, `documentRef`, `sha256`를 검사한다. 선언이 없거나 다른 형식이면 `ResearchDeclarationMissing`/`ResearchBindingNeedsReview`로 반환한다. 도구 적용을 위해 연구를 임의 승인하거나 기획과 불일치하는 빈 배열로 바꾸지 않는다.
- Hongdal과 별도 Unity의 기준선은 분리한다. 새 인계 `readRefs`는 Hongdal 상대 경로 또는 `unity:Assets/...`로 적는다. 기존 절대 Unity 경로는 명시한 Unity 저장소 내부일 때만 읽기 호환하며 경고한다. 새 작업의 저장소별 쓰기 소유권은 기존 원칙을 따른다. 외부 경로·상위 탈출·와일드카드·reparse point는 거부한다.
- 지정 읽기 파일·쓰기 경로의 현재 내용도 기준선에 포함한다. HEAD가 같아도 미커밋 입력 변경·삭제·추가는 재검토 대상이다. 지정 입력 밖의 dirty 파일과 HEAD 변경만으로는 차단하지 않는다. 누락된 새 쓰기 파일은 `Missing`으로 기록하여 생성 후 변화를 탐지한다.
- 검증 명령은 보여 주기만 하며 자동 실행하지 않는다. 계정 로그인/전환, 예약 작업, 연속 실행, Unity·Blender 조작, 운영 API, commit·push는 포함하지 않는다. 로컬 산출물은 자동 Git 공유되지 않으므로 다른 작업실로 옮길 때 승인된 인계 수단으로 명세·입력 파일·JSON을 함께 전달한다. 채팅 기억·계정 인증정보는 인계 자료가 아니다.

검증: [`development-handoff.ps1`](../../eng/tests/development-handoff.ps1). 표본의 실제 차단은 게임 결함이나 도구 실패와 분리해 보고한다.

## 운영 원장

현행 주체·WI 입력은 [`gameplay-subjects.json`](../../eng/execution-ledgers/gameplay-subjects.json)과 [`subject-interaction-development.json`](../../eng/execution-ledgers/subject-interaction-development.json)이다. 새 Goal은 준비된 Actor·직접 대상 주체, WI 하나, 승인 기획, 직접 결과와 파생 작용 판정을 가져야 한다. [`codex-playable-loop-goals.json`](../../eng/execution-ledgers/codex-playable-loop-goals.json) v4는 기존 복수 활성 Goal과 `workItems`를 보존하는 호환 원장이다. 그 항목은 `nextWorldInteractionId`의 단일 WI Goal로 투영하고 `loopStableId`는 선택적 검증 묶음 참조로 읽는다. 전역·스레드별 Goal/WI 개수는 표시 정보이지 실패 조건이 아니다. 새 Goal은 [주체·상호작용 중심 개발 체계](주체상호작용중심개발체계.md)의 관문과 [주제 기획 기반 개발 체계](주제기획기반PlayableLoop개발체계.md)의 `Approved` 관문을 통과해야 한다.

기획 주제와 WI는 1:1 또는 1:N일 수 있고, WI와 PlayableLoop도 N:M 참조를 허용한다. 기획서는 재미·플레이어 약속·선택·대가를 소유하고 Goal 원장과 상태판은 현재 WI·E·증거·차단을 소유한다. 과거 활성 Goal을 위한 `LegacyActiveMigration`은 이전 자료를 읽기 위한 한시 상태일 뿐 새 Goal에 양도할 수 없다. 현재 주체·WI 결속은 [주체·상호작용 상태판](../AI/generated/subject-interaction-development.md), 기존 Loop 기획 승인은 [주제 기획 상태판](../AI/generated/playable-loop-topic-planning.md), 기존 작업 상태는 [Goal 상태판](../AI/generated/codex-playable-loop-goals.md)을 함께 사용한다.

활성 Goal은 현재 WI의 주체 결속, 파이프라인 프로필 key·revision, Logic·Presentation·통합 관문 상태, 가장 이른 재개 E와 차단 사유도 함께 가진다. 권위 변화의 행위 기록이나 표현 엔진 cursor 소비가 빠지면 Goal을 교체하지 않고 같은 WI Goal의 해당 E를 다시 연다. Goal은 E7에서 끝나며 E8~E10 반복 안정·조화·관찰은 별도 캠페인으로 인계한다.

기본 우선순위는 Core를 먼저 닫고 Extension을 뒤에 검토하는 방식이다. 다음 순서는 선택 지침이며 앞선 독립 작업이 활성이라는 이유만으로 뒤 작업을 차단하는 전역 실행 잠금이 아니다.

```text
Nature Core 6개
→ Farm Core 2개
→ Hub Core 2개
→ Town Core 1개
→ City Core 1개
→ Nature Extension 3개
→ Farm Extension 1개
→ Town Extension 1개
```

이 순서는 영역 간 업무 의존이 아니다. Farm 결과를 Hub 시작 조건으로 사용하지 않으며 각 영역은 독립 Fixture·Save/Replay·공간 증거를 먼저 가진다.

모든 Goal은 `world-layout:sim:pyeongchang:nature-farm-hub-town.v1`의 `WorldAreaAnchor`를 영역 위치 기준으로 사용한다. 폐루프 구현은 현재 AreaSet 내부의 H1·H2·H3를 성숙시키며 H5 중심 좌표를 임의로 다시 잡지 않는다. `Reserved` City 앵커는 City의 위치·특징 의도만 고정하며 City Goal의 E5나 통행 가능성을 선행 증명하지 않는다.

특정 Goal 이름이나 과거 대기 순서를 현행 활성 상태로 고정하지 않는다. 현재 작업 목록에서 명시된 실제 선행 계약과 승인 범위가 준비된 작업만 선택하며, 새로운 병렬 운영 방식만으로 대기 Goal/WI를 자동 활성화하지 않는다.

## 병렬 작업과 통합 소유권

- 서로 다른 WI Goal과 같은 WI의 논리·표현·시험 하위 작업은 독립적인 변경 범위이면 병렬로 등록한다. Goal 하나에 여러 WI를 넣지 않는다. 작업마다 담당 스레드, 기준 revision/hash, 작업 명세, 수정 경로, 공유 계약, 선행 작업, 증거 상한과 통합 상태를 남긴다.
- 하나가 차단되어도 무관한 작업을 정지시키지 않는다. 기획 미승인, 기준 hash 불일치, 실제 선행 계약 미완료, 조율되지 않은 파일·공유 계약 충돌을 해당 작업의 구체적인 차단으로 기록한다. 다른 WI가 활성이라는 사실은 차단 사유가 아니다.
- 같은 파일·공유 계약의 쓰기는 변경 소유자를 정해 조율한다. 별도 worktree는 같은 계약에 대한 경쟁 변경을 안전하게 만들어 주지 않는다. 읽기 기준선과 인계 결과 hash를 비교하고 변경된 의존성은 재검증한다.
- 분야별 담당은 독립 코드·Fixture·검증 결과를 `개발` 스레드에 인계한다. 개발은 Simulation·WI 구현과 최종 통합을 겸임하고 통합 결과·남은 기획 판단을 `기획` 스레드로 반환한다. 호환되는 결과는 묶어 통합하고 독립 시험은 병렬 수행할 수 있다. 공통 원장과 공식 Scene의 실제 쓰기는 소유자가 충돌 없이 반영한다.
- 같은 WI의 여러 작업이 만든 증거는 동일한 WI·판본의 기록으로 합친다. 작업 개수와 인계 완료는 Evidence 승격이 아니며 승인된 E 상한과 Logic/Presentation 중 낮은 통합 E를 유지한다.
- 현재 작업·증거와 이전 단일 활성 자료는 보존한다. 기존 기획의 한시적 이전 예외를 새 병렬 작업에 복제하거나, 미승인 WI를 지원 작업으로 바꿔 기획 관문을 우회하지 않는다.

## 전문 담당과 기획 환류

‘하위 스레드’는 앱의 물리적 계층이 아니라 작업 배분·결과 인계 관계다. 기존 스레드를 재사용하고 이름·위치·브랜치를 자동 변경하지 않는다. 현재 담당 ID와 원문 인계는 [개발 통합 상태판](../AI/개발통합상태판.md)에서 찾는다.

| 담당 | 소유하는 작업 | 기본 인계 대상 |
| --- | --- | --- |
| 기획 | 문답·플레이어 약속·우선순위·연구 및 기획 변경 승인 | 개발 |
| 개발 | Simulation·WI 구현, 공통 계약, 전문 작업 배분, 결합·회귀 검증·실행 원장 | 기획 |
| 월드·공간·배치 | 지형·H·LH·실내외 배치·Synty 공간 조합의 독립 모듈과 시험, View 캡처 실행 | 개발 |
| 애니메이션 | 행위 표현·Clip/Rig·도구 결속·취소/복귀·Audio/FX 연결 요구와 시험 | 개발 |

UI·저장·네트워크·검증을 처음부터 별도 상설 스레드로 늘리지 않는다. 독립 산출물과 비중첩 쓰기 범위가 생겼을 때 개발이 전문 분리를 제안한다. 기존 스카이 엔진 담당은 필요한 때 재사용하는 후보이며 이 정책으로 자동 재개하지 않는다. 기상 자료의 권위 해석은 Simulation 담당, 하늘 표현은 표현 담당으로 구별한다.

### 배분과 변경 소유권

- Graph Map에서 구현 후보를 넘길 때는 [Graph Map 개발 인계 체계](GraphMap개발인계체계.md)를 사용한다. `ReadyForDevelopment`는 Goal의 `workItems`가 아니며 자동 등록·재활성화·실행 권한도 아니다. 개발이 현재 Goal·WI·승인 기획·E1~E7 상호작용 수직 검증 명세·파일 소유와 검증 상한을 확인한 뒤 기존 작업 재사용 또는 비중첩 작업 추가를 결정하고, 그때부터 실행 원장이 실제 개발 상태를 소유한다.
- 실행 구현은 기존 `workItems`에 승인 기획·명세·대상 WI·담당·변경 경로·의존성·E 상한을 연결한다. 공통 연구·미승인 공간 후보는 [전문 연구 절차](PlayableLoop전문심화연구분기재결속체계.md)와 후보 인계로 관리하며, 작업 목록을 채우려고 실행 WI나 E를 만들어내지 않는다.
- 개발이 공통 계약을 확인한 뒤 각 담당은 동결된 입력 상태 사본으로 독립 구현한다. 미정 계약은 조사·후보까지만 진행하며 구현 승인을 추정하지 않는다.
- 같은 `.unity`·Prefab·`.meta`·Animator Controller·공유 계약의 쓰기는 담당을 조율한다. 공식 `SimulationWorldShell` 저장·공통 원장·생성기 반영은 개발이 책임지고, 실제 조작을 다른 담당에게 맡길 때 경로와 기간을 명시한다. 같은 Unity Editor의 Scene/Play Mode 변경·시험도 겹치지 않게 사용한다. 전체 개발 WIP 잠금은 아니다.
- 서로 다른 작업 트리는 실제 소스·패키지·대상 프레임워크와 hash를 비교한다. 특히 dirty 파일은 HEAD만으로 식별되지 않는다. Hongdal과 별도 Unity 저장소의 경로·기준선·검증을 각각 남기고 한 저장소의 `writePaths`에 `../`로 외부 경로를 넣지 않는다. 외부 저장소 변경 소유는 인계에 명시하고 개발이 함께 대조한다.
- 기획은 승인 문서를, 전문 담당은 자기 산출물을, 개발은 실행 원장·생성 상태판·통합 상태판을 갱신한다. `CURRENT_WORK.md`는 관련 절의 쓰기 담당을 먼저 알리고 직전 내용을 다시 읽어 반영한다.

### View 캡처 분담

- Game View 캡처와 배선 설명용 Scene View 캡처는 `월드·공간·배치` 담당이 실행한다. 대상은 공간 작업에 한정하지 않고 WI·UI·애니메이션의 통합 화면도 포함한다. 애니메이션 전문 시험 자체의 책임을 이관하는 것은 아니다.
- `개발`은 캡처가 필요한 승인 작업의 WI/폐루프, 저장소와 Scene, 소스·배치·상태 판본, 재현 입력, 시점·필요 화면, 확인할 성공/실패 조건을 정리해 의뢰한다. 다른 전문 담당은 필요한 화면을 개발에 요청한다.
- 공간 담당은 기존 Editor 점유와 dirty 상태를 확인하고 캡처 시간대를 조율한다. 캡처 담당 지정은 공식 Scene 저장·기존 미저장 변경 폐기·새 공식 Scene 생성 권한이 아니다. 같은 Editor를 쓰는 다른 작업만 조율하며 독립 코드 개발까지 멈추지 않는다.
- 반환에는 캡처 경로, 시각, 대상 Scene·판본, Play Mode 여부, 실행한 입력·관찰 결과, Console 오류와 미검증 조건을 포함한다. 원본은 `artifacts/local/`에 두고 장기 보존할 대표 화면만 기존 시각 기록 정책에 따라 `docs/assets/changes/`에 둔다.
- 개발은 반환 결과가 같은 WI·판본의 논리 상태와 연결되는지 검토하고 증거 묶음·통합 상태판에 반영한 뒤 기획에 반환한다. 캡처 획득만으로 E5/E7을 승격하지 않으며 Scene View·기획 이미지로 Game View 증거를 대체하지 않는다.
- 이번 분담은 캡처가 필요한 때의 담당을 정한다. 모든 작업의 캡처 의무화나 즉시 Editor 실행 요청은 아니며 기존 승인 범위·검증 조건은 유지한다.

### 전문 담당 → 개발 인계

정상 완료의 기본 수신자는 개발이다. 전문 담당끼리 정보를 교환할 수 있지만 공통 계약 변경·최종 통합 판정을 우회하지 않는다. 다음을 하나의 원문 인계로 남긴다.

```text
작업 ID 또는 연구/후보 ID · 담당 · 대상 WI/폐루프
승인 범위 · 기준 revision/hash · 저장소/변경 파일 · 산출물 hash
구현 내용 · 재현 명령 · 통과/실패 결과 · 실행/화면 미검증 범위
의존 계약 차이 · 남은 결함 · 통합 방법 · 필요한 기획 결정
```

개발은 승인·hash·소유권을 확인하고 생산자와 소비자 연결 및 회귀를 검사한다. 낡은 기준선이나 결함은 해당 인계에만 돌려보내며 무관한 작업은 계속한다. 원문 보고 수신·문서 반영·시험 성공은 실제 결합이나 `Integrated` 판정의 대체가 아니다.

### 개발 → 기획 반환

호환되는 결과 묶음이 준비될 때마다 통합·검증 후 상태판을 갱신하고 기획에 짧게 반환한다. 모든 전문 담당의 완료를 기다리지 않는다. 플레이어 약속·연구 기준선의 변경이 필요한 차단은 통합 완료 전에도 개발이 근거를 확인해 우선 반환한다. 정기 실행 자동화나 시간 기반 보고를 이 규칙만으로 만들지 않는다.

반환 양식은 다음 다섯 항목이다.

1. **실제 통합 결과:** 플레이어가 새로 할 수 있는 것. 아직 실행 연결이 없으면 없다고 기록한다.
2. **준비됐지만 미통합:** 받은 산출물과 연결되지 않은 이유·담당.
3. **검증 범위:** WI/판본별 Logic·Presentation·통합 E, 시험·Runtime·Game View 구별, 원문 증거·통합 승인 기록 참조.
4. **기획 판단:** 필요한 결정과 대안·영향·가장 이른 재개 E. 구현 결함은 기획 선택으로 떠넘기지 않는다.
5. **다음 작업:** 기존 승인 안에서 가능한 작업과 재승인 대상을 분리한다.

[개발 통합 상태판](../AI/개발통합상태판.md)은 이 반환의 최신 요약이며 별도 실행 원장이나 E 권위가 아니다. 원문 인계·기존 `workItems`·EvidencePackage·통합 승인 기록을 연결한다. 상태가 달라지면 기준 시각과 참조를 갱신하고, 기획의 응답은 승인 기획의 다음 revision 또는 미정 질문으로 남긴다. 기술 통합 승인은 기획 승인이나 사람의 시각 승인을 대신하지 않는다.

### 분담의 조사 근거

- Unity는 동일 Scene의 서로 다른 객체 수정도 같은 파일에 기록됨을 설명하며 독립 Prefab을 통한 충돌 감소를 권한다. 이 프로젝트는 공식 Scene을 늘리는 대신 전문 산출물을 분리해 통합한다. [Unity Scene·Prefab 협업 지침](https://unity.com/blog/author-scenes-and-prefabs-with-verson-control)
- Assembly는 의존성·재컴파일 범위를 분리하는 수단이다. 스레드 수에 맞춘 새 Assembly가 아니라 기존 계약을 우선 사용한다. [Unity Assembly 지침](https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definition-files.html)
- Git worktree는 HEAD가 독립적이어도 일부 참조와 설정을 공유한다. 별도 작업 트리가 계약 호환이나 최신 기준선을 보증하지 않으므로 실제 입력 hash를 확인한다. [Git worktree 문서](https://git-scm.com/docs/git-worktree)

## 작업 목록 등록과 통합 기록

`workItems`의 각 항목은 `workItemId`, `loopStableId`, `worldInteractionId`, `trackCode`, `targetEvidenceStageCode`, `ownerThreadId`, `worktreePath`, `writePaths`, `sharedContractKeys`, `dependsOnWorkItemIds`, `baselineFiles`, `workOrderRef`·`workOrderSha256`, 승인 당시 `planningGate`를 가진다. 같은 폐루프에서 기획서가 갱신돼도 이전 작업의 승인 판본을 조용히 덮어쓰지 않는다.

- `Active`: 승인된 범위를 구현·검증한다. `ReadyForIntegration`: 동일한 승인·기준 검사를 유지한 채 통합 인계를 기다린다.
- `Blocked`: 이유를 표시하고 해당 작업만 대기한다. 손상되거나 오래된 명세 때문에 다른 독립 작업까지 멈추지 않는다. 같은 파일 쓰기를 다음 작업에 넘길 때는 선행 작업·통합 기록을 연결한다.
- `Integrated`: `integrationReceiptRef`·`integrationReceiptSha256`로 고정된 통합 승인 JSON을 연결한다. 기록에는 작업/Goal/WI, `workOrderSha256`, 목표 E, `Accepted` 상태, 원장의 `integrationOwnerThreadId`와 같은 `acceptedByThreadId`, `acceptedAt`, 결과 파일의 경로·hash를 가진 `artifactRefs`를 넣는다. 상태 문자열만 바꿔 후속 의존성을 통과시킬 수 없다. 이 통합 기록은 목표 E 달성이나 PlayableUnit E7 완료를 자동 선언하지 않는다.
- `writePaths`는 저장소 상대 경로이며 폴더 범위도 허용한다. 대소문자·구분자·상하위 경로를 정규화해 충돌을 검사한다. 공유 계약 키는 읽기가 아니라 변경 소유권을 표시한다.

검사는 `eng/tests/parallel-development-work.ps1` 및 Goal·전달 우선순위·문답 원장 시험을 사용한다. 모든 기존 원장을 같은 담당자가 검증 후 갱신하며 생성 문서와 C#은 생성기로 반영한다. 구형 `activeGoal`·`activeWork`·단일 C# 상수는 대표 표시/읽기 호환용이고 신규 실행 선택은 작업 목록을 사용한다.

## Goal 생명주기

1. 기획서의 필수 절·revision·hash·승인 근거와 등장 주체 후보를 확인한다.
2. 기획서의 전문 심화 연구 판정을 확인하고 모든 `Required` 문서가 `Accepted` 상태로 재결속됐는지 확인한 뒤 기획서를 `Approved`로 고정한다.
3. Actor와 직접 대상 주체를 `Ready`로 만든 뒤 현재 WI 하나와 목표 E를 Goal로 고정한다.
4. WI의 직접 결과를 고정하고 파생 작용의 필요 여부와 최대 2-hop 경계를 판정한다.
5. E7→E1 영향 검토에서 실제 선행 의존성과 승인 상한을 확인하고, 소유 범위가 조율된 작업들을 등록한다.
6. 같은 WI의 하위 작업을 필요한 증거 단계까지 구현하고 결과를 인계해 E1→E7 방향으로 다시 검증한다.
7. 새 영향이 나오면 같은 Goal과 작업 명세의 하향 검토 또는 잘못된 전문 연구를 다시 연다.
8. 플레이어 약속이나 WI 의미가 바뀌면 새 주제·WI Goal로 분리한다. 여러 WI의 반복 폐쇄성 검증이 필요할 때만 PlayableLoop를 연결한다. 독립 Goal의 추가가 진행 중인 다른 Goal의 폐기를 뜻하지 않는다.

작업별로 현재 논리 또는 표현 궤적을 표시한다. 기존 `activeMaturityTrackCode`는 대표 표시를 위한 호환 조회이며 다른 궤적 작업을 금지하지 않는다. 표현 실패가 권위 상태 누락에서 시작됐으면 같은 Goal의 관련 논리 책임으로 돌아가며, 통합 E는 [논리·시각 이중 순환 기준](플레이폐루프논리시각이중순환체계.md)에 따라 두 궤적 중 낮은 단계다.

E8~E10은 Goal의 다음 수직 목표가 아니다. 각 `PlayableUnit`이 E7을 통과하면 자기 E8 안정성 캠페인으로 인계한다. 같은 영역의 E8 Core 둘 이상이 준비됐을 때만 E9 조화·사람 승인 캠페인을 열고, 승인 후보만 E10 제한 운영으로 보낸다. 상세 기준은 [E1~E7 수직 폐루프와 E8~E10 수평 증거 체계](E1-E7수직폐루프와E8-E10수평증거체계.md)를 따른다.

## E7 종료와 E7 이후 인계

- `E7 PlayClosed`: 실제 입력, canonical `SimulationWorldShell` Play Mode, Game View, 성공·실패·회복·귀환, 결정적 Save/Restore/Replay가 유효하다.
- `NpcRoutine`이 필수인 폐루프도 해당 PlayableUnit의 수직 목표는 E7이다. NPC 생활 연속성은 관련 E9 `AreaHarmonySet`의 필수 조화 모듈로 다시 검증한다.
- Scene·Synty 배치·문서·EditMode 성공만으로 E7을 선언하지 않는다.
- E7 완료 뒤 Goal을 E8로 올리지 않는다. 해당 Goal을 완료하고 `post-e7-evidence-campaigns.json`의 개별 E8 안정성 캠페인을 연다.

## 현재 작업 조회와 보고

현재 활성 Goal·작업·WI와 실제 E·차단·다음 의존성은 생성 상태판을 현재 기준으로 삼는다. 담당자는 대표 `activeGoal`만 보고 다른 활성 작업을 놓치지 않도록 전체 작업 목록에서 자신의 소유 범위와 공유 계약을 먼저 확인한다. 다른 작업의 완료를 무조건 기다리지 않으며 승인되지 않은 작업은 병렬 여부와 무관하게 활성화하지 않는다.

진행 보고는 항상 다음 순서를 사용한다.

```text
작업 ID·담당 / Goal·WI·궤적 / 현재 E단계 / 이번에 추가된 증거 /
실제 차단·의존성 / 통합 상태·다음 인계
```

새 권위 부여, 외부 Provider 호출, 운영 쓰기, 범위가 다른 폐루프 추가 또는 기존 플레이어 약속 변경이 필요하면 구현을 멈추고 사용자 결정을 요청한다.
