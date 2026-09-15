# GitHub 기반 기획·개발 작업실 교대 운영 체계

## 목적

같은 프로젝트를 두 Codex 계정·두 로컬 데스크톱에서 이어 갈 때, 한 작업실은 기획을 정밀화하고 다른 작업실은 승인된 범위를 구현한다. 두 작업실의 공용 기억은 계정별 대화 기록이 아니라 **GitHub에 push된 저장소 정본과 정확한 커밋**이다.

이 문서는 새 실행 원장을 만들지 않는다. 다음 기존 체계를 실제 Git 교대 절차로 연결한다.

- [기획 문서 독립 관리 체계](기획문서독립관리체계.md): PLAN 정본과 목차
- [PlayableLoop 문답 정밀화 체계](PlayableLoop문답정밀화체계.md): 한 번에 질문 하나, 우선순위와 인계 상태
- [주제 기획 기반 PlayableLoop 개발 체계](주제기획기반PlayableLoop개발체계.md): 승인 관문과 개발 인계 묶음
- [Codex PlayableLoop Goal 운영 체계](CodexPlayableLoopGoal운영체계.md#설계실제작실-인계): 설계실·제작실 책임과 `Export`/`Check`
- [운영·Simulation·Unity 작업 흐름 분리](OperationsSimulationUnity작업흐름분리.md): 저장소·브랜치·커밋 책임
- [기획 인계 고정점](../AI/Handoffs/Mirror-ChatGPT-기획인계-2026-09-07.md)과 [현재 프로젝트 인계](../AI/GPT-채팅모드-현재프로젝트인계-2026-09-05.md): 기존 대화 운영 방식

## 핵심 원칙

1. 계정, 구독 등급, 모델명, 장치 이름은 역할 권한이 아니다. 현재 작업이 `기획 작업실`인지 `개발 작업실`인지 요청과 인계 자료로 선언한다.
2. 채팅 요약만으로 교대하지 않는다. 다른 작업실이 읽어야 할 결정·미정·범위·검증은 추적 파일에 기록하고 push한다.
3. 기획 우선순위와 개발 가능 상태를 분리한다. 가장 중요한 기획이라도 승인 관문을 통과하지 않았으면 구현하지 않는다.
4. 기획 작업실은 무엇을 만들지와 완료 조건을 소유한다. 개발 작업실은 승인된 범위 안에서 어떻게 구현·검증할지를 소유한다.
5. 개발 중 기획을 바꿔야 하면 조용히 보완하지 않는다. 해당 범위를 `FeedbackRequired`로 반환하고 새 기획 revision을 기다린다.
6. 로컬 `artifacts/`와 계정별 대화 기억은 GitHub 공용 정본이 아니다. 공유해야 하는 입력·명세·증거는 저장소 정책에 맞는 추적 경로에 두거나 승인된 별도 전달 수단을 명시한다.

## 역할별 소유권

| 구분 | 기획 작업실 | 개발 작업실 |
| --- | --- | --- |
| 시작점 | `PLANNING.md`, canonical PLAN, 기존 문답·연구 | push된 정확한 인계 커밋, 승인 PLAN, 작업지시서 |
| 주 작업 | 목적·주체·결과·선택·대가·범위·완료 조건 정밀화 | 현재 코드 대조, 구현, 시험, 통합 검증 |
| 바꿀 수 있는 것 | 기획 정본, 기획 목차, 현재 우선순위, 승인된 작업지시서 | 인계에 허용된 `writePaths`, 시험, 구현 증거와 결과 상태 |
| 단독으로 바꿀 수 없는 것 | 제품 코드와 운영 효과 | 플레이어·운영자 약속, 승인 범위, 공유 권위, 검증 상한 |
| 종료 산출물 | 기획 commit 또는 판본화된 개발 인계 commit | 구현 commit과 증거·남은 판단의 기획 반환 commit |

기획 작업실도 조사·문서 검증을 수행할 수 있고 개발 작업실도 구현에 필요한 기술 판단을 할 수 있다. 다만 그 판단이 사용자 약속이나 승인 범위를 바꾸면 역할 경계를 넘은 것이다.

## 세 축을 섞지 않는다

### 기획 질문 우선순위

- `P0`: 지금 답을 받아야 다음 기획이 진행되는 질문. 항상 하나만 둔다.
- `P1`: P0 답변 뒤 다시 계산할 다음 수평 질문 후보.
- `P2`: 근거 수집·전문 연구·다른 기획과의 결속이 먼저 필요한 항목.
- `P3`: 수치·균형·장기 확장처럼 지금 구현을 막지 않는 후속 항목.
- `Hold`: 외부 자료나 사람 검토를 기다리는 항목.
- `NotAQuestion`: 코드·시험·인계처럼 문답이 아닌 작업.

### 기획 문서 상태

`Draft -> ReadyForReview -> Approved`

승인된 이전 revision에 구현 결과가 있더라도 새 문답으로 바뀐 revision은 다시 `Draft`일 수 있다. 이전 구현 증거를 새 판본의 승인으로 재사용하지 않는다.

### 개발 인계 상태

`NotReady -> ReadyToDispatch -> Dispatched -> EvidenceReturned`

구현이 기획 판단을 요구하면 `FeedbackRequired`를 사용한다. `P0`와 `ReadyToDispatch`는 다른 축이며, 우선순위가 높다는 이유로 개발 상태를 올리지 않는다.

## 기획 작업실 절차

### 1. 최신 공용 상태를 맞춘다

1. 대상 저장소와 remote를 확인한다.
2. `git status --short --branch`로 기존 변경을 확인한다.
3. 안전하게 fetch한 뒤 현재 branch가 기대한 upstream과 어떤 관계인지 확인한다.
4. 관련 `AGENTS.md`, [공용 프로젝트 컨텍스트](../ProjectOverview/GptProjectContext.md), [기획 목차](../AI/PLANNING.md), [확정 결정](../AI/DECISIONS.md), [현재 작업](../AI/CURRENT_WORK.md)을 읽는다.
5. 여러 저장소가 대상이면 Mirror와 Unity 각각의 branch·HEAD·dirty 상태를 따로 기록한다.

dirty 변경은 다른 작업의 소유일 수 있다. 자동으로 버리거나 섞어서 stage하지 않는다.

### 2. 기존 기록을 먼저 찾는다

PLAN ID, 기능명, 주체, API, 안정 식별자와 질문 의미로 canonical PLAN과 `eng/planning-inquiries`를 검색한다. 같은 PLAN의 후속 문답은 그 PLAN의 `README.md`에 누적하고, 판본마다 임의의 별도 질문 파일을 만들지 않는다.

### 3. 질문과 우선순위를 기록한다

`확정 / 미정 / 추천과 대가 / 다음 질문 하나`를 PLAN 정본에 반영한다. `PLANNING.md`에는 현재 판본·상태·관계와 현재 큐만 요약한다. 의미 있는 설계 변화는 `CURRENT_WORK.md` 최신 snapshot에도 반영한다.

### 4. 구현 가능 여부를 판정한다

다음 조건이 하나라도 빠지면 `NotReady`다.

- 독립적인 구현 결과와 제외 범위가 정해졌다.
- PLAN이 `Approved`이고 revision·내용 hash·승인 근거가 있다.
- 필요한 전문 연구가 `Accepted`다.
- 작업 ID 또는 WI, 작업지시서, 담당 책임, `writePaths`가 정해졌다.
- 기준 branch·commit, 공유 계약·의존성, 검증 명령과 완료 증거가 정해졌다.
- 결과를 기획으로 돌려보낼 경로와 가장 이른 재개 단계가 정해졌다.

문답 3~5개가 모였거나 독립 구성요소 하나가 닫히면 인계 후보를 합성해 볼 수 있지만, 이 개수만으로 승인하거나 dispatch하지 않는다.

### 5. GitHub에 게시한다

기획 commit에는 관련 PLAN 정본, `PLANNING.md`, `CURRENT_WORK.md`, 그리고 승인된 경우에만 갱신한 작업지시서·원장을 포함한다. 제품 코드, 비밀값, 무관한 dirty 변경, `artifacts/local` 산출물을 섞지 않는다.

문서 전용 새 작업은 `docs/<작업명>`을 기본으로 한다. 사용자가 이미 정한 공유 branch나 통합 담당 branch가 있으면 그 기준을 따르되, 인계에 branch와 정확한 commit을 반드시 남긴다. commit 뒤 push하고 remote의 같은 ref가 같은 commit을 가리키는지 확인한다.

## 개발 인계 묶음

기획 작업실은 다음 항목을 commit된 문서 또는 작업지시서에서 식별할 수 있게 한다.

| 항목 | 필수 내용 |
| --- | --- |
| 저장소 | `cheolwo/ssalddel`, 필요하면 `cheolwo/unity`를 별도 행으로 기록 |
| Git 기준선 | remote, branch, 정확한 commit SHA |
| 기획 기준선 | PLAN ID, canonical 경로, revision, SHA-256, 승인 근거 |
| 상태 | 기획 상태와 개발 인계 상태를 각각 기록 |
| 실행 단위 | Goal/WI/작업 ID와 E7 작업지시서 또는 동등한 운영 작업지시서 |
| 읽기 범위 | `readRefs`, 필요한 연구·계약·생성 상태판 |
| 쓰기 범위 | 책임 소유자와 비중첩 `writePaths` |
| 결과 경계 | 직접 결과, 제외, 실패·회복, 운영 효과 활성 여부 |
| 검증 | 실행 명령, 성공 기준, Runtime·Game View·DB 등 검증 상한 |
| 반환 | 증거 기록 경로, 통합 담당, 기획 피드백 경로, 가장 이른 재개 단계 |

짧은 교대 메시지는 위 내용을 다시 풀어 쓴 대화가 아니라 다음 형식의 포인터로 충분하다.

```text
역할: 개발 작업실
소스: <remote>/<branch>@<commit>
대상: <PLAN-ID>@<revision>, <work-order 또는 Goal/WI>
상태: Approved + ReadyToDispatch
먼저 읽기: <PLAN>, <work-order>, <readRefs>
쓰기/검증/제외/반환: 작업지시서 기준
```

## 개발 작업실 절차

### 1. 인계를 정확한 Git 상태로 복원한다

1. 로컬 dirty 상태를 먼저 확인하고 기존 작업을 보존한다.
2. remote를 fetch한다.
3. 인계 branch와 commit이 remote에 존재하는지 확인한다.
4. 그 commit의 PLAN·작업지시서·원장 내용을 읽고, 현재 작업 branch에 같은 내용이 들어왔는지 확인한다.
5. 구현은 [책임 작업 흐름](OperationsSimulationUnity작업흐름분리.md)에 맞는 `operations/*`, `simulation/*`, `unity/*`, `integration/*` branch에서 수행한다. 기획 branch에서 제품 코드를 누적하지 않는다.

기획 commit을 다른 기준 branch에 merge하거나 cherry-pick해야 한다면 그 결과 commit에서 PLAN hash와 작업지시서 참조를 다시 확인한다. 강제 reset이나 기존 dirty 변경 덮어쓰기는 교대 절차가 아니다.

### 2. 수용 검사를 통과해야 구현한다

다음이면 구현을 시작하지 않고 기획 작업실로 반환한다.

- 상태가 `NotReady`, `Draft`, `ReadyForReview`이거나 `ReadyToDispatch`가 아니다.
- 인계 commit, PLAN revision/hash, 작업지시서가 없거나 서로 맞지 않는다.
- 핵심 주체·결과·선택·대가·실패·회복·권위 경계가 미정이다.
- 현재 코드가 기준선과 달라 `writePaths` 또는 공유 계약이 겹친다.
- Mirror와 Unity 중 필요한 저장소 기준선 하나가 빠졌다.
- 로컬 전용 파일만 있고 다른 작업실에서 재현할 수 없다.

PlayableLoop Goal이면 `manage-development-handoff.ps1 -Mode Check`를 현재 checkout과 보존된 인계 JSON에 적용한다. 이 도구의 통과는 자료 일치 확인일 뿐 실행 승인이나 Goal 자동 활성화가 아니다.

### 3. 구현·검증·반환을 분리한다

승인된 쓰기 범위만 구현하고 책임별 시험을 실행한다. 여러 저장소나 책임을 바꾸면 commit과 검증 결과를 분리한다. 완료 뒤에는 다음을 추적 문서에 반환하고 push한다.

1. 실제 통합된 결과와 commit
2. 준비됐지만 통합되지 않은 결과
3. 실행한 검증과 검증하지 못한 Runtime·환경 범위
4. 기획 판단이 필요한 항목과 `FeedbackRequired` 여부
5. 다음 작업과 가장 이른 재개 단계

채팅의 “완료”만으로 `EvidenceReturned`, 통합 완료, 운영 활성 또는 E 승격을 선언하지 않는다.

## Mirror와 Unity 두 저장소

- Mirror와 Unity는 서로 다른 Git 저장소다. 한쪽의 pull·commit·push는 다른 쪽을 갱신하지 않는다.
- 두 저장소가 모두 필요한 인계는 각 저장소의 remote·branch·commit·dirty 상태·쓰기 경로·검증을 별도 행으로 기록한다.
- 공통 계약 생산자는 Mirror, Unity 소비자는 Unity처럼 소유권을 분명히 하고 생산자·소비자 회귀를 모두 확인한다.
- 한 저장소의 `writePaths`에 `../`로 다른 저장소 경로를 넣지 않는다.
- 기획 문서가 Mirror에만 있어도 Unity 인계에는 Unity의 시작 기준 commit을 반드시 적는다.

## 충돌과 복구

- upstream이 이동했으면 현재 변경을 보존한 채 관계를 확인하고, 병합 또는 rebase 정책을 선택한 뒤 hash와 시험을 다시 확인한다.
- 승인 뒤 PLAN 내용이 바뀌면 기존 작업지시서 hash를 조용히 고치지 않는다. 진행 중 개발은 기존 승인 revision에 고정하거나 `FeedbackRequired`로 돌려 새 revision 승인을 받는다.
- 개발이 기획 정본을 바꿔야 한다고 판단하면 구현 commit에 숨기지 않고 발견 사항·대안·영향 범위를 반환한다.
- 같은 파일이나 공유 계약을 두 작업실이 동시에 수정해야 하면 먼저 통합 소유자와 순서를 정한다.
- push 실패나 remote 불일치는 로컬 commit 완료와 구분해 보고한다.

## 작업실 시작 문장

기획 작업실에서:

```text
이번 작업은 기획 작업실 역할이다. 최신 Git 기준선을 확인하고 기존 PLAN·문답을 검색한 뒤, 제품 코드는 바꾸지 말고 확정·미정·추천과 대가·다음 질문 하나를 정본에 반영해라. 개발 인계는 관문을 모두 통과한 범위만 ReadyToDispatch로 만들고 commit/push 결과를 남겨라.
```

개발 작업실에서:

```text
이번 작업은 개발 작업실 역할이다. <remote>/<branch>@<commit>의 <PLAN-ID>@<revision>과 <work-order>를 읽고 hash·상태·쓰기 범위를 Check해라. Approved + ReadyToDispatch가 아니면 구현하지 말고 반환 사유를 기록해라. 통과하면 책임 branch에서 구현·검증하고 commit/push와 증거·남은 기획 판단을 반환해라.
```

## 완료 기준

교대는 다음이 모두 성립할 때 완료다.

- 기획 또는 구현 결과가 정본에 반영됐다.
- stage와 commit에 무관한 변경이 섞이지 않았다.
- 필요한 검증과 검증 상한이 기록됐다.
- commit SHA와 branch가 명시됐다.
- push된 remote ref가 그 commit을 가리킨다.
- 다음 작업실이 계정별 대화 기록 없이 저장소만으로 시작할 수 있다.
