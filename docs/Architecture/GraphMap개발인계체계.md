# Graph Map 개발 인계 체계

## 목적

Graph Map에서 정리한 노드·엣지·배치 제약·기존 배치 규칙·코드 결속을 개발 작업이 검증 가능한 작은 구현 단위로 받도록 한다. Graph Map 전체를 한 번에 구현하지 않고, 준비된 주체와 단일 WI Goal·E1~E7 상호작용 수직 검증 명세에 맞는 slice 하나를 골라 개발에 인계한다. PlayableLoop는 여러 WI의 폐쇄성을 함께 검증할 때만 선택적으로 참조한다.

Graph Map은 개발 명세나 상태 권위가 아니다. 플레이 관계와 구현 진입점을 함께 찾게 하는 계획 자료이며, 개발은 승인된 기획·작업 명세·현재 코드와 실제 파이프라인 차단을 다시 확인한 뒤 구현한다.

Graph Map 계획·기획 인계·개발 인계의 파일 안전과 결정적 출력은 `GraphMapTooling.ps1`을 공통 사용한다. 개발 인계 관리자는 Goal·WI·명세·slice 수용 조건만 추가 검증하고 경로·JSON·해시·중복 검사 구현을 따로 복제하지 않는다.

## 전체 순환

```text
기획 승인
  → Graph Map 레벨 1·2·3 반영
  → 개발 가능한 작은 slice 선정
  → 기존 Goal·WI·작업 명세 대조
  → ReadyForDevelopment 인계
  → 개발의 수용·정확 쓰기 경로·검증 상한 확정
  → 코드·시험·필요 전문 작업
  → Integrated / Blocked / Deferred 최종 반환
  → Graph Map 상태와 다음 기획 질문에 환류
```

기획 스레드는 이 중간 진행을 기다리지 않는다. Graph Map 담당은 개발 인계 항목을 만들고, 개발 담당은 최종 결과가 생겼을 때 Graph Map과 기획에 반환한다.

## 소유권

| 역할 | 소유 | 금지 |
| --- | --- | --- |
| 기획 | 플레이어 약속·선택·대가·실패·회복·귀환, 승인 판본 | 개발 편의를 위해 의미를 조용히 바꿈 |
| Graph Map | 레벨 1~3와 federation, 구현 후보 slice의 노드·엣지·제약·결속 선택, 개발 인계 원장 | 코드 쓰기 경로·기술 구현을 임의 확정, 전체 맵 자동 구현 |
| 개발 | Goal·WI·작업 명세 재검증, 기존 작업 재사용/새 작업 필요 판단, 정확 쓰기 경로, 코드·시험·통합 결과 | Graph Map 계획을 실제 E5나 권위 상태로 간주 |
| 전문 담당 | 개발이 나눈 월드·공간·배치·애니메이션·자료 조사 산출물 | 개발 통합 없이 E 승격·Scene 완료 주장 |
| 기획 반환 | 플레이 선택이 필요한 차단의 다음 질문 | 기술 오류를 새 게임 선택으로 돌림 |

## 개발 slice 선정 기준

한 인계는 다음을 모두 만족해야 한다.

1. 준비된 Actor·직접 대상 주체와 대표 WI 하나를 중심으로 한다. 여러 WI를 함께 검증해야 할 때만 PlayableLoop 하나를 추가 참조한다.
2. Graph Map 노드·엣지·제약과 선택한 기존 배치 규칙을 정확한 안정 ID로 열거한다.
3. 선택한 요소는 `Unresolved`가 아니어야 한다. 미해결 관문은 별도 차단으로 남긴다.
4. 관련 레벨 3 결속과 미결속 대상을 구분한다.
5. 단일 WI Goal과 `Approved` 기획 관문·작업 명세를 참조한다.
6. 핵심 진행이라면 `나의 막힘→회복 행동과 대가→기여 자원→상대 필요→권위 결과→환류` 중 이 slice가 실제로 닫는 범위와 미결속 범위를 명시한다. 보조 층이면 직접 회복 기여가 아님을 보존한다.
7. 기존 work item이 있으면 재사용 후보로만 표시하고 개발 수용 전 자동 재활성화하지 않는다.
8. 목표 E와 실제 검증 상한을 따로 적는다.
9. 전체 Graph Map·모든 Area·모든 Prefab 완성을 선행조건으로 삼지 않는다.

Graph Map의 엣지 하나가 여러 Area나 WI를 잇는다면 각 Area의 독립 폐루프가 준비되기 전에는 영역 간 통합 slice로 넘기지 않는다. Farm→Hub 같은 선택형 연결은 양쪽 독립 Goal의 상태를 확인한 뒤 별도 인계한다.

## 개발이 인수 전에 확인할 것

- Graph Map 판본·SHA-256과 원 기획 인계가 현재인지
- 선택한 노드·엣지·제약이 실제 참조인지 계획 관문인지
- 대표 WI가 공식 대장에 있고 선택 노드에서 실제로 참조되는지
- 주체 `Ready`, 단일 WI Goal, 작업 명세, 기획 문서의 판본·hash와 `Approved` 상태
- 원 기획의 상위 목적 정렬이 현재 판본에 있고, 핵심 진행과 보조 층이 구분됐는지
- 같은 파일·계약·Editor를 점유하는 기존 work item과 충돌하는지
- 가장 이른 Logic/Presentation E와 통합 E 상한
- 코드·시험만으로 끝나는 범위와 Unity Play/Game View가 필요한 범위

검사를 통과한 뒤 개발은 기존 work item을 재사용할지, 비중첩 하위 작업을 만들지 결정한다. 인계 원장의 `candidateWorkItemId`는 자동 선택이나 실행 명령이 아니다.

## 레벨별 개발 해석

### 레벨 1

노드·엣지는 사용할 계약·상태·입력·결과·귀환의 구현 범위를 찾는 근거다. 개발은 연결의 플레이 의미와 목적 사슬에서 이 slice가 맡은 구간을 보존하되 노드마다 새 Controller나 클래스를 만들지 않는다. `이렇게`의 대표 행동을 구현할 때는 시간·공간·Actor·대상 조건과 선택 근거를 보존하고, 기획이 열어 둔 다른 유효 행동을 기술 편의 때문에 제거하거나 자동 추천·자동 성공으로 바꾸지 않는다. 표시·대화·애니메이션만 구현한 경우에는 보조 표현 또는 Presentation 증거로 반환하고 권위 결과·회복 기여까지 구현했다고 확대하지 않는다.

### 레벨 2

제약은 기존 Domain/Application 검증, 배치 엔진, Unity 공간 검증 중 어느 책임이 집행하는지 나눈다. `placementRuleRefs`는 현재 제약에 직접 결속된 기존 규칙만 전달하며, `AvailableNotSelected` 규칙을 구현 요구로 자동 승격하지 않는다. 문서 수치가 미정이면 상수를 추정하지 않고 연구·기획 차단으로 반환한다.

### 레벨 3

코드 결속은 파일·assembly·심볼·현재 hash를 찾는 진입점이다. 개발은 실제 코드와 가까운 `AGENTS.md`를 다시 읽고 기존 계약·서비스·Adapter를 먼저 재사용한다. 결속이 있다는 사실만으로 Scene 배선·Runtime 실행을 완료했다고 하지 않는다.

## 상태

```text
Draft
  → ReadyForDevelopment
  → AcceptedByDevelopment
  → InProgress
  → Integrated | Blocked | Deferred | NoImplementationRequired
  → Superseded
```

- `ReadyForDevelopment`: Graph Map slice와 기존 Goal·명세 후보가 검증됐다. 아직 개발 수용·실행은 아니다.
- `AcceptedByDevelopment`: 개발이 판본·소유·정확 경로·상한을 확인했다.
- `InProgress`: 등록한 범위에서 구현·검증 중이다.
- `Integrated`: 코드·시험과 요구된 실제 증거가 개발 통합을 통과했다.
- `Blocked`: 기획, Graph Map, 작업 명세, 기술 또는 실제 실행의 구체 차단이 있다.
- `Deferred`: 다른 독립 작업을 막지 않고 후속으로 미뤘다.
- `NoImplementationRequired`: 기존 구현으로 요구가 충족됨을 현재 근거로 검증했다.
- `Superseded`: 새 Graph Map 또는 기획 판본이 대체했다.

## 차단 반환 위치

| 발견 | 반환 |
| --- | --- |
| 선택·대가·실패·회복·귀환 미정 | 기획 |
| 노드·엣지·제약의 구조·소유·관계 오류 | Graph Map |
| Goal·WI·작업 명세·코드·시험·권한·저장 문제 | 개발 |
| Prefab 형상·배치·통행·Game View 문제 | 월드·공간·배치 → 개발 통합 |
| Rig·Clip·접촉·중단·복귀 문제 | 애니메이션 → 개발 통합 |
| 현실 자료 출처·권리·수집 공백 | 자료 조사 → 개발 통합·기획 보고 |

기술 차단을 새 플레이 선택으로 바꾸지 않고, 기획 차단을 임시 코드로 메우지 않는다.

## 기계 판독 자료

- 개발 인계 원본: [`graph-map-development-handoffs.json`](../../eng/world-seedbeds/graph-map-development-handoffs.json)
- 관리 도구: [`manage-graph-map-development-handoffs.ps1`](../../eng/world-seedbeds/manage-graph-map-development-handoffs.ps1)
- 생성 JSON: [`graph-map-development-handoffs.v1.json`](../../eng/world-seedbeds/generated/graph-map-development-handoffs.v1.json)
- 사람용 상태판: [`graph-map-development-handoffs.md`](../AI/generated/graph-map-development-handoffs.md)
- 회귀시험: [`graph-map-development-handoffs.ps1`](../../eng/tests/graph-map-development-handoffs.ps1)

원장은 개발 결과의 단일 권위가 아니다. Goal·work item·E1~E7 상호작용 수직 검증 명세·코드·시험·EvidencePackage가 실제 구현 상태를 소유하고, 이 원장은 Graph Map slice와 그 개발 상태를 연결한다.

## 반환 형식

개발은 최종 반환에 다음을 분리한다.

- 소비한 Graph Map 판본과 element ID
- 재사용/추가한 Goal·work item·작업 명세
- 변경한 정확 경로와 공유 계약
- 코드·시험·Runtime·Game View·Save/Replay·서버 연결의 각각의 결과
- 목적 사슬에서 실제로 닫힌 구간과 아직 미결속인 나의 회복·상대 기여·환류 구간
- Graph Map에 되돌릴 실제 성립/차단/무효화 정보
- 기획 질문이 필요한지와 그 이유
- commit·push 여부

`Integrated`는 요청한 acceptance contract를 충족했을 때만 사용한다. 코드·시험 통과만 요구된 slice와 실제 화면이 필요한 slice의 완료 조건을 섞지 않는다.

## 첫 적용

북부 생활권 Graph Map r4의 Farm 생산→작업마당을 첫 `ReadyForDevelopment` 사례로 등록한다. 선택 범위는 Farm 노드 2개, 필수 엣지 1개, 실제 참조·Farm 흐름 분리·자산 후보 비할당 제약, 기존 `ProductionProcessingSeparation` 규칙과 관련 코드 결속이다. 기존 `work:farm-crop-cycle:landscape-binding-guard`는 재사용 후보이며, 이 인계만으로 그 작업을 재시작하거나 새 Unity 실행을 승인하지 않는다.
