# [기획 · 증거·검증 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r18]

# E1~E10 결과물 계약과 상호작용 수직 검증 명세 명칭 정비

## 상태

- `Implemented`
- `MachineValidated`
- `NoEvidencePromotion`
- `LegacyCompatibilityPreserved`

## 목적

r17 감사에서 확인한 두 문제를 정비한다.

1. `E7 작업 명세`라는 이름이 목표 단계와 문서 종류를 혼동하게 하므로 사람이 읽는 공식 이름을 `E1~E7 상호작용 수직 검증 명세`로 바꾼다.
2. E1~E10마다 완료 시 남겨야 할 결과물 종류와 최소 내용을 기계 계약으로 고정한다.

이 정비는 증거 체계의 이름·판정 입력·검사기를 고친 것이며, 기존 Goal·WI·자료·화면을 새 E 단계로 올리는 승격 작업이 아니다.

## 공식 명칭과 호환 경계

- 사람이 읽는 공식 이름: `E1~E7 상호작용 수직 검증 명세`
- 새 파일 접미사: `.interaction-e1-e7-validation.json`
- 자료 수집·정규화·DB 저장 기록: `.data-implementation.v1.json`
- 읽기 전용 화면·캡처 기록: `.presentation-evidence.v1.json`
- 기존 `.e7-work-order.json`, `E7-WO-*`, script·schema 식별자는 저장소 호환을 위해 유지할 수 있지만 새 문서에서 현재 명칭으로 사용하지 않는다.
- 파일 이름이나 E7 문자열만으로 증거 단계를 인정하지 않는다. 현행 manager 통과, 정본 등록, Goal·WI·두 궤적·revision 결속이 모두 필요하다.

행정동 과거 자료 파일은 목적에 따라 다시 분류했다.

| 과거 용도 | 새 정본 분류 | E 단계 주장 |
| --- | --- | --- |
| 30개 행정동 후보 batch | `administrative-dong-batch.data-implementation.v1.json` | 없음 |
| 기초 Game View 캡처 | `administrative-dong-base-geometry-capture.presentation-evidence.v1.json` | 없음 |
| 주소·필지 후보 | `administrative-dong-address-parcel-candidate.data-implementation.v1.json` | 없음 |
| 횡단보도 후보 | `administrative-dong-crosswalk-candidate.data-implementation.v1.json` | 없음 |
| 사업장 첫 판 거절 기록 | `administrative-dong-business-candidate.r1.data-implementation.v1.json` | 없음 |

횡단보도 과거 경로 `administrative-dong-crosswalk-candidate.e7-work-order.json`은 r2 생성기 hash에 들어 있는 동결 입력이므로 원문 그대로 남긴다. 이것은 읽기 호환 입력일 뿐 정본 명세나 E 승격 근거가 아니다. 생성기 SHA-256 `FC949D8350EFCA7A80F4D7BC1C7071DA1362C3E4D6D104D584B909A3F9771CC8`과 C# 자체 시험을 다시 확인했다.

## E1~E10 필수 결과물

정본은 `eng/execution-ledgers/evidence-stages.json`의 `evidence-stage-output-contract.r1`이다. 모든 결과물은 공통으로 `subjectStableId`, `trackCode`, 후보 revision, 내용 또는 build hash, 증거 참조, 상태, 차단 사유, 무효화 조건을 가져야 한다.

| 단계 | 반드시 남길 결과물 | 최소 의미 |
| --- | --- | --- |
| E1 | `EvidenceContract`, `TrackImpactAssessment` | Goal·WI·주체, 플레이 약속, 권위·입출력, 성공·실패·회복·귀환, Logic·Presentation 영향과 가장 낮은 미완료 의존성 |
| E2 | `ImplementationBindingRecord`, `CompatibilityBoundaryRecord` | 계약을 소비하는 Core·서버·Adapter·Unity/앱 경로, DI·route·저장, schema·API·migration·소비자 호환 |
| E3 | `DeterminismAndReplayReport`, `ConsumerRegressionReport` | 동일 입력 결정성, 저장·복원·Replay, 멱등성, 서버·Simulation·Unity·앱 소비자 회귀와 미실행 범위 |
| E4 | `ExecutionContextBinding`, `PresentationPreparationHandoff` | 발생원·주체·대상·자료·시간·공간/H 제약, 플레이어 판독 순간·VisualKey·자산/fallback·배치 의도·열린 결손 |
| E5 | `AuthorityManifestationRecord`, `WorldPresentationBindingRecord` | 같은 revision의 권위 Command/Task/Effect/전후 상태·행위 기록과 World 객체·Anchor·최소 판독 표현 결속 |
| E6 | `RefinementReviewRecord`, `ActorActionBindingRecord` | 인과·실패·회복·귀환·권리·성능·공간 결함 검토와 Actor 동작·Clip·Rig·입력·전이·중단·귀환 결속 |
| E7 | `ActualInputClosureRecord`, `IntegratedTrackVerdict` | 사람이 실제 Runtime·저장 Scene에서 성공·실패·회복·귀환을 입력으로 완주한 기록과 두 궤적 중 낮은 통합 판정 |
| E8 | `PlayableUnitStabilityCampaignResult` | 같은 동결 build의 Logic 반복 3회 이상, Save/Replay·Local/Remote, Presentation 실제 입력 2회 이상·재진입·Console·열린 피드백 |
| E9 | `AreaHarmonySetEvaluation` | E8 Core 둘 이상, 대표 순서별 Logic·Presentation 조화, 사람 평가와 Blocking/Major 없음, revision·build 명시 승인 |
| E10 | `LimitedOperationObservationReport` | E9 불변 build의 제한 운영 프로필, 관찰 기간·완주·회복·저장·Replay·rollback·사람의 계속/중단 결정과 외부 효과 권위 |

결과물 파일이 존재한다는 사실만으로 단계가 오르지 않는다. 상위 단계는 같은 후보 계보의 하위 결과물을 소비하며, Logic·Presentation 통합값은 낮은 쪽이다. 자료 원장이나 캡처 한 장은 유효한 상호작용 Goal과 두 궤적에 결속되기 전까지 E 결과물이 아니다.

## 공통 원장 복구

- 증거 단계 schema를 `simulation-evidence-stages.v8`, revision을 `r15`로 올리고 단계별 필수 결과물과 불충분 조건을 기계 판독 가능하게 추가했다.
- 상호작용 수직 검증 protocol을 r8로 올리고 오행 분류 출력 v3를 소비하도록 맞췄다.
- E8 안정성 캠페인이 빠졌던 `playable-loop:hexagram-campaign-retry.v1`을 `WaitingForE7`로 등록했다. 이것은 E8 승격이 아니다.
- 구성원 명세는 PlayableUnit 집계보다 앞설 수 있지만 뒤처질 수는 없도록 manager 규칙을 명시했다.
- 정본 63개를 전수 검사했을 때 61개가 통과했다. 남은 두 건은 실제 원장 정합성 문제다.
  - `nature-logging-focus-meditation`: 명세 통합 E1인데 연결된 PlayableUnit 집계는 E7이다.
  - `nature-trace-investigation`: `WI-NATURE-TRACE-INVESTIGATE`가 지정 PlayableUnit의 WI 목록에 없다.
- 두 건은 증거를 임의 승격하거나 WI를 임의 편입하지 않고 차단 상태로 남긴다.

## 검증

- `eng/tests/evidence-management-systems.ps1` 통과
- `eng/tests/e7-vertical-work-order.ps1` 통과
- `eng/tests/e7-role-object-action-gate.ps1` 통과
- `eng/tests/e7-native-work-order.ps1` 통과
- `eng/tests/post-e7-evidence-campaigns.ps1` 통과
- `eng/tests/world-interaction-gwae-classifications.ps1` 통과
- `eng/tests/station-diorama-evidence-rules.ps1` 통과
- 횡단보도 C# 자체 시험 15/15 통과, 동결 생성기 hash 일치
- 변경 JSON 12개 파싱, 관련 문서 상대 링크, `git diff --check` 통과
- 추가 표현 회귀는 `playable-loop-presentation-validation`과 `presentation-synty-survey`가 통과했다. `presentation-local-realization-scope`는 이번 변경과 무관한 Farm work item 기대값 7 대 현재 8의 기존 정합성 차이에서 실패했다.

## 검증 상한

이번 r18은 명칭·결과물 계약·검사기·공통 원장 정합성을 복구했다. 자료 수집, DB 쓰기, Unity 실행, Game View 촬영, Scene 저장, 실제 입력, E 승격, commit, push는 수행하지 않았다. 행정동 디오라마의 상한은 계속 `자료·표현 보조 증거 존재 / E단계 미승격`이다.

## 후속

- 남은 두 상호작용 수직 검증 명세는 각 Goal 소유자가 PlayableUnit 관계와 실제 증거를 재판정해야 한다.
- 기존 `.e7-work-order.json` 63개는 호환 경로로 유지하고 기능 단위 수정 시 새 접미사로 점진 이전한다. 대규모 일괄 이름 변경은 하지 않는다.
- 행정동 디오라마의 다음 자료 단계는 r16에서 고정한 `TL_SCCO_GEMD 현행 경계 → 주소 건물·건물군 → 출입구` 관문을 따른다.
