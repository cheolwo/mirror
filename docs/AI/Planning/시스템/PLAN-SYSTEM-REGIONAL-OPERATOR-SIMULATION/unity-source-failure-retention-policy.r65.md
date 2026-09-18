# [기획 · Unity 운영 관찰 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 원천별 마지막 정상 상태 유지 r65]

- 기준일: 2026-09-17
- 상태: `Draft / PerSourceLastKnownGoodConfirmed / PartialFailureVisible / UnsafeActionsBlocked / PlanningOnly`
- 이전 판본: [절기 캠페인 관찰 Coordinator r64](unity-seasonal-campaign-observation-coordinator.r64.md)

## 확정

Campaign 구성 원천 하나의 갱신이 실패하면 Unity는 **원천별 마지막 정상 상태를 유지하고 해당 영역에만 `갱신 지연`을 표시**한다.

- 디오라마·운영 상태·캠페인·카드·NPC 원천을 각각 독립적으로 판정한다.
- 한 원천의 실패 때문에 다른 원천의 정상 객체와 화면 전체를 지우지 않는다.
- 실패한 원천의 마지막 성공 시각·revision·오류 분류를 화면 모델에 남긴다.
- 마지막 정상 상태는 현재 사실인 것처럼 덮어 표시하지 않고 `Stale` 또는 `RefreshDelayed` 상태를 함께 보여 준다.
- 원천 실패를 실제 객체 삭제·업무 종료·캠페인 전환으로 해석하지 않는다.
- 새 세션·지역 전환에서는 이전 지역의 마지막 정상 상태를 재사용하지 않고 해당 문맥을 명시적으로 비운다.

## 원천별 유지 단위

| 원천 | 유지하는 것 | 지연 표시 위치 | 금지되는 해석 |
| --- | --- | --- | --- |
| 행정동 디오라마 | 마지막 승인 manifest·tile 메모리 | 지역 자료 상태 | 건물·도로가 실제로 사라졌다고 판단 |
| 운영 상태 | 마지막 정상 주문·기사·음식점 상태 사본 | 위험·업무 카드 | 완료·취소·배차 해제로 자동 판단 |
| Campaign | 마지막 정상 phase·결정·결산 Preview | 캠페인 진행 영역 | 다음 phase 자동 진입 |
| 카드 | 마지막 정상 카드 Family 상태 | 카드 서랍 | 잠김·해제·효과 종료 자동 판단 |
| NPC 표현 | 마지막 정상 이동·표현 상태 | Actor 상태 | NPC 업무 결과 확정 |

## 화면과 조작 경계

- 관찰은 계속할 수 있지만 지연 원천에 의존하는 조작은 `AvailableActions`와 의존 원천 상태를 함께 검사한다.
- 최신 revision이 필요한 Preview·Confirm은 지연 원천이 회복될 때까지 비활성화하고 이유를 표시한다.
- 지연과 무관한 다른 원천의 읽기·선택·카메라 이동은 계속 허용한다.
- Unity가 자체 계산으로 누락된 `AvailableActions`를 보충하지 않는다.
- 재연결 뒤 더 높은 유효 revision을 수신하면 기존 stable ID를 갱신하고 지연 표시를 해제한다.
- 낮은 revision, 지역 불일치, schema 불일치는 재연결 성공으로 간주하지 않는다.

## Coordinator 결과 후보

`SeasonalCampaignObservationCoordinator`는 조합 결과에 원천별 진단을 포함한다.

```text
SeasonalCampaignObservationResult
├─ ScreenModel
├─ SourceDiagnostics
│  ├─ SourceCode
│  ├─ LastSuccessfulRevision
│  ├─ LastSuccessfulAt
│  ├─ FreshnessCode
│  └─ ErrorCode
├─ DisabledActionReasons
└─ CompositionRevisionSet
```

`CompositionRevisionSet`은 각 원천의 revision을 묶어 화면이 어떤 조합으로 만들어졌는지 설명한다. 서로 다른 판본을 하나의 가짜 전역 revision으로 합치지 않는다.

## 검증 계획

1. 카드 원천 실패 시 디오라마·업무 상태가 유지된다.
2. 운영 상태 실패 시 기존 주문은 남지만 최신 상태 의존 Confirm은 비활성화된다.
3. 행정동 원천 실패 시 마지막 승인 배경은 남고 `갱신 지연`이 표시된다.
4. 재연결 뒤 높은 revision만 적용되고 낮은 revision은 기존 상태를 보존한 채 거절된다.
5. 지역 전환 뒤 이전 지역의 stale 상태가 새 지역에 나타나지 않는다.
6. 원천 실패·복구 순서를 Save/Replay의 권위 사건으로 잘못 기록하지 않는다.
7. 전체 화면 공백과 GameObject 중복 생성을 방지한다.

## 아직 미정

- 원천별 freshness 제한과 경고 단계.
- 어떤 조작이 어느 원천 revision에 의존하는지 나타내는 결속 형식.
- 지연 상태의 화면 색·아이콘·문구와 접근성 표현.
- 장시간 실패 때 자동 재시도 간격과 backoff 상한.

## 다음 질문 하나

지연 원천에 의존하는 조작은 어떤 기준으로 제한할까?

1. `조작별 필요한 원천·최소 revision을 계약으로 명시` — 추천. 관찰은 유지하면서 해당 조작만 정확히 비활성화한다.
2. `하나라도 지연이면 모든 조작 비활성화` — 안전하지만 관련 없는 관찰·선택까지 막힌다.
3. `경고만 표시하고 모든 조작 허용` — 유연하지만 오래된 상태로 잘못 Confirm할 위험이 있다.
