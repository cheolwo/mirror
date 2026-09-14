[기획 · 시스템·월드 투영 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · 승인 구현 r20]

# 사가정 합성 배달 기사 관찰 첫 수직 절편

- 상태: `ApprovedScopedImplementation / PublicStatisticsDensityOnlyConfirmed / SyntheticIndividualAuthorityConfirmed / FirstCourierObservationSliceApproved / DisplayTogglePresentationOnly / SimulationAuthorityInvariantRequired / GraphMapNoImpact / SettlementFormulaDeferred / PublicDataCollectionDeferred / BoundaryPortalDeferred / ScenePersistenceForbidden / PromotionNotApproved`
- 승인 근거: 2026-09-14 사용자가 r19의 추천안인 “공공 통계는 시간대·공간 밀도에만 사용하고 개별 인물의 별칭·직업·이동·건수·가상 정산은 별도 합성 Simulation이 생성”을 채택하고 구현 작업을 요청했다.
- 상위 기획: [사가정 공공데이터·생활 관찰 심화 제안 r19](sagajeong-public-data-life-observation.proposal.r19.md)
- 재사용 WI: `WI-CITY-SYNTHETIC-MOVE`
- 대상 주체: `subject:synthetic-delivery-courier.v1`
- 권위: `SimulationSession`
- Graph Map 영향: `NoImpact`

## 1. 플레이어 약속

사가정 생활 디오라마에서 기존 합성 음식 배달 기사 한 명을 선택하면, 같은 Simulation revision의 현재 행동·업무 단계·이동 진행·합성 실적을 읽기 전용 카드로 본다. `업무 Actor` 표시를 끌 수 있지만 Simulation은 계속 진행하며, 다시 켜면 오래된 프레임이 아닌 같은 Actor의 최신 사본을 복구한다.

카드는 항상 `합성 Simulation 자료 · 실제 기사·위치·정산 아님`을 표시한다.

## 2. 지금·여기·나·너·이렇게·결과·귀환

- 지금: `scenario:synthetic-delivery.r1`의 단일 기사가 주문·조리·픽업·전달·수령·복귀 순환을 Simulation Tick으로 진행한다.
- 여기: `station:kr:kric:s1107:0722` 사가정역 명목상 1km × 1km 관찰 창의 runtime-only 검증 조립부다.
- 나: 전체 생활을 조망하다가 합성 Actor를 선택하는 관찰자다.
- 너: `actor:synthetic-courier:1`과 같은 기존 합성 배달 주체다. 실제 주민이나 기사를 대표하지 않는다.
- 이렇게: 카메라에 보이는 Actor의 화면 투영 위치를 선택하고, Simulation이 생성한 파생 관찰 사본만 카드에 표시한다.
- 결과: 현재 행동과 업무 단계, 시나리오 완료 건수, revision과 자료 한계를 한 카드에서 읽는다.
- 귀환: 카드를 닫거나 전체 보기로 돌아간다. 이 조작은 상태 revision을 바꾸지 않는다.

## 3. 승인 범위

1. 단일 합성 배달 기사 관찰 계약과 결정적 projection
2. 현재 행동·업무 단계·Tick·Simulation revision·이동 진행도
3. 같은 시나리오의 수령 확인 완료 건수
4. 실패·회복 원장이 없을 때 숫자 0으로 위장하지 않는 `NotTracked`
5. 모의 정산의 `PolicyPending / SimulationSettlementRulePending`
6. Collider 없는 Actor 선택, 카드, `업무 Actor` 로컬 표시 토글
7. 토글 전후 Simulation 상태 불변과 최신 사본 복구 시험

## 4. 제외 범위

- 공공자료 신규 수집·DB 쓰기·밀도 Profile 생성
- 경계 포털·1km 밖 생활·일반 주민·보행자·추가 차량
- 실제 사업체·주민·기사·주문·위치·소득의 추론
- 실제 정산·지급·세금·보험·수수료 계산
- 승인되지 않은 모의 단가나 금액 생성
- 실제 운영 `scene-snapshots`, 주문·배차·평판·성장 상태 변경
- Graph Map·H·AreaSet·배치 Map 수정
- `SimulationWorldShell.unity`·Prefab 저장과 새 공식 Scene
- E 승격·commit·push·배포

## 5. 권위와 자료 흐름

```text
SimulationSession 권위 상태
  → 합성 기사·음식 배달 사본
  → 결정적 사가정 기사 관찰 projection
  → Unity 메모리의 최신 revision 적용
  → runtime-only Actor 표시·선택·카드
```

- 완료 건수는 `delivery-scope:synthetic`의 음식 배달 중 `StateCode=수령확인`이고 `ReceivedTick`이 있는 항목만 집계한다.
- 전달만 끝난 주문은 완료 건수에 넣지 않는다.
- r20은 단일 기사만 가진 `scenario:synthetic-delivery.r1` 전용이다. 다중 기사는 주문↔기사 결속 원장 없이 추정하지 않는다.
- Unity는 애니메이션·GameObject·프레임 횟수로 건수나 금액을 계산하지 않는다.
- 공공 통계는 개별 카드 필드의 생성 근거가 아니다.

## 6. 읽기 계약

관찰 사본은 다음을 포함한다.

- schema·projection revision·projection hash
- Session 식별자·revision·WorldTick
- Actor 식별자·합성 표시명·생활/업무 역할
- 현재 행동·업무 단계·주문 상태·이동 진행도
- 진행·완료 건수와 실패·회복 추적 상태
- 모의 정산 상태와 nullable 금액·통화
- `SyntheticSimulation=true`
- `ActualPerson=false`
- `IsOperationalState=false`
- `SettlementWriteAllowed=false`
- `ObservationPresentationOnly=true`
- `ChangesAuthorityState=false`

정산 규칙이 승인되기 전에는 금액과 통화를 비우고 `SimulationSettlementRulePending`을 제공한다.

## 7. 표시·선택·토글 규칙

- 새 GameObject는 runtime-only 표시 Root 아래에만 둔다.
- 기존 건물·도로·카메라·조명·재질은 수정하지 않는다.
- 대량 Collider를 만들지 않고 카메라에 투영된 Actor 중 포인터에 가장 가까운 하나를 선택한다.
- uGUI 위 클릭은 Actor 선택으로 전파하지 않는다.
- 표시를 끄면 Actor Root와 카드만 숨기고 선택한 `ActorStableId`는 로컬 메모리에 유지한다.
- 숨긴 동안에도 최신 revision을 수용하고, 다시 켤 때 그 사본을 당장 표시한다.
- 낮은 revision·같은 revision의 다른 hash·Actor 식별 변경·유효하지 않은 진행도는 거절하고 이전 카드를 fallback으로 노출하지 않는다.

## 8. 실패·회복·저장

- 투영 검증 실패: `Blocked`와 정확한 코드를 남기고 Actor와 카드를 제거한다.
- 낮은 revision: `Stale`로 거절하고 최신 본문을 덮어쓰지 않는다.
- 회복: 더 높은 유효 revision을 받으면 같은 Actor 선택을 복구한다.
- 토글·선택은 Unity 로컬 표시 상태며 Simulation Save/Replay에 새 권위 필드를 추가하지 않는다.
- 저장 Scene과 Prefab은 변경하지 않는다.

## 9. 검증 계획과 증거 상한

1. 같은 Session 사본은 같은 projection hash·건수·카드 값을 만든다.
2. 수령 확인 전달만 완료로 계산하고 전달만 된 주문은 제외한다.
3. 정산 금액은 null이고 미승인 규칙 코드가 표시된다.
4. 낮은 revision과 같은 revision의 다른 hash를 거절한다.
5. Actor를 선택한 뒤 표시를 끄고 새로운 Simulation 사본을 적용해도 권위 상태가 변하지 않고, 다시 켜면 최신 Tick·revision이 보인다.
6. 토글 전후 Simulation 사본의 결정적 hash를 비교한다.
7. Unity EditMode 자동 시험과 runtime-only Play Mode 검증을 분리한다.
8. 전체 보기와 선택 카드 Game View를 새 실행에서 캡처하되, 정지 화면은 연속 움직임의 증거가 아님을 명시한다.
9. 현실적인 성능·선택·토글 표현을 실행 화면에서 확인하기 전에는 Presentation E5 이상을 주장하지 않는다.

## 10. 확정·미정·다음 질문 하나

### 확정

- 공공 통계는 합성 Actor의 시간대·공간 밀도 근거에만 사용하며 개별 인물 정보를 생성하지 않는다.
- 첫 대상은 기존 단일 합성 음식 배달 기사다.
- 선택·카드·토글은 읽기 전용 표현이고 Simulation·업무·정산 권위를 바꾸지 않는다.
- 이번 절편은 새 Graph Map 노드·엣지·WI·H 결속을 만들지 않는다.

### 미정

- 완료 1건당 모의 지급·거리·대기·비용·수수료·세금·보험을 어떤 게임 규칙으로 계산할지
- 실패·회복·소요 시간 원장의 정확한 소유자와 저장 범위
- 선택 상태를 세션 간 유지할지
- G2 경계 포털·밀도 Profile의 정확한 시간창과 수치

### 다음 질문 하나

다음 수직 절편에서 모의 예상 지급은 **완료 1건당 고정 지급 + 이동 거리 가산 - 모의 운행 비용**으로 시작할까, 아니면 금액을 더 나중으로 미룰까?

추천은 운영 정산과 혼동하지 않게 **일단 금액을 미루고 이번 r20은 `SimulationSettlementRulePending`으로 닫는 것**이다. 대가는 숫자형 보상 피드백이 한 판본 늦어지는 점이다.
