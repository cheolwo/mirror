# [기획 · 증거·검증 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 감사·제안 r17]

# 디오라마 E1~E10·Codex 사용 적합성 감사

## 상태

- `Audited`
- `ProposalOnly`
- `CurrentPromotionBlocked`
- `RemediationNotImplemented`

> 이 상태는 r17 감사 시점 기록이다. 이후 명칭·결과물 계약·공통 회귀 정비 결과는 [구현 r18](evidence-stage-output-and-name-migration.implementation.r18.md)이 소유하며, r17 자체를 소급해 구현 완료로 바꾸지 않는다.

## 감사 질문

1. 현행 E1~E10 정의가 아래 단계에서 위 단계로 증거를 누적하는 상향식 체계인가?
2. 행정동·역세권 디오라마가 그 체계에 실제로 결속되어 있는가?
3. Codex가 파일명이나 시험 한 건만 보고 단계를 과대 판정하지 않도록 기계 관문이 충분한가?

이번 감사는 문서·대장·검사기의 현행 정합성을 확인했다. E 승격, Unity 실행, Game View 촬영, 원본 수집, DB 변경, 코드 복구는 수행하지 않았다.

## 결론

**E1~E10의 현행 개념 구조는 상향식 증거 체계로 적절하다. 그러나 현재 저장소 전체의 기계 정합성은 완전하지 않고, 행정동 디오라마의 기존 E7 명세 3개는 현행 E7 작업 명세로 인정할 수 없다. 따라서 Codex가 현행 체계를 사용하려면 먼저 `대상 분류 → 현행 검사기 통과 → 정본 등록 확인 → 낮은 궤적 단계 적용`을 강제해야 한다.**

- E1~E7은 같은 WI Goal의 Logic·Presentation을 각각 검증하고 통합 단계는 둘 중 낮은 값으로 정한다.
- E8은 E7을 통과한 한 `PlayableUnit`의 반복 안정, E9는 E8 Core 둘 이상의 영역 조화와 사람 승인, E10은 승인된 불변 빌드의 제한 운영이다.
- 상위 단계는 같은 후보 revision·hash의 모든 하위 근거를 누적 소비한다. 화면, 시험, 문서 또는 자료 수집 하나가 중간 단계를 건너뛸 수 없다.
- 역세권 디오라마 증거 대장은 출처·수집 관문·규칙 후보·역별 프로필을 관리하는 **보조 계보**다. 자체적으로 E단계를 승격하지 않는다는 경계는 올바르다.
- 반면 감사 당시 행정동 디오라마에 있던 과거 `.e7-work-order.json` 3개는 현행 validator가 첫 필수 필드인 `protocolRevision` 부재로 모두 거절했다. 이후 r18에서 자료 구현 기록·관찰 표현 증거로 개명하며 실제 상호작용 검증 명세와 분리한다.

## 현행 E1~E10 판정

| 단계 | 현행 정본의 질문 | 디오라마에서 확인한 보조 근거 | 현재 E 승격 판정 |
| --- | --- | --- | --- |
| E1 | 플레이 약속·StableId·권위·입출력·완료 조건이 확정됐는가 | 관찰 전용 계약, stable ID, 출처·권위·게시 차단이 존재한다 | **부분**. 자료·관찰 계약은 있으나 행정동 디오라마 자체의 유효한 WI Goal·현행 작업 명세가 없다 |
| E2 | 계약을 실행할 Core·서버·Adapter·Unity 소비자가 준비됐는가 | 생성기, importer, manifest·tile, 읽기 전용 Unity 조립이 있다 | **부분**. 구성 요소 증거는 있으나 유효한 E1 작업 명세가 이를 같은 revision으로 소비하지 않는다 |
| E3 | 동일 입력의 결정성·저장·복원·재생이 검증됐는가 | hash, 멱등 저장·재조회, 변조 거절, Unity 회귀가 다수 존재한다 | **부분**. 자료 결정성은 강하지만 WI Save/Replay·Logic/Presentation E3와 동일하지 않다 |
| E4 | Actor·대상·시간·H·자료·표현 문맥이 결속됐는가 | 행정동·역·자료 판본·H 후보·fallback 경계가 일부 기록돼 있다 | **부분**. 실제 생활·이동 WI의 Actor·대상·시간·H 결속은 현행 명세로 닫히지 않았다 |
| E5 | 권위 상태와 같은 revision의 실제 World 표현이 결속됐는가 | 역사 경계 기반 runtime-only Mesh와 Game View가 있다 | **미성립**. Collider·통행·권위 상태·행위 기록·WI 발현 결속이 없고 current 게시도 차단돼 있다 |
| E6 | 의미·인과·배치·권리·개인정보·성능·실패·회복이 정제됐는가 | 자료 격차·개인정보 정정·공간 반례·누락을 보존했다 | **부분**. 자료 품질 정제는 있으나 플레이 실패·회복·귀환까지 같은 Goal로 검토하지 않았다 |
| E7 | 사람이 실제 입력으로 결과·실패·회복·귀환을 Game View에서 완주했는가 | 30개 행정동 전체·근접 캡처와 별도 사가정 합성 관찰이 있다 | **미성립**. 캡처는 기초 표현 증거이며 행정동 디오라마 생활 WI의 실제 입력 폐루프가 아니다 |
| E8 | 한 E7 PlayableUnit이 반복·Save/Replay·재진입·Local/Remote에서 안정적인가 | 해당 행정동 디오라마 안정성 캠페인이 없다 | **진입 불가**. 먼저 유효한 E7 주체가 필요하다 |
| E9 | 같은 영역의 E8 Core 둘 이상이 조화를 이루고 사람이 승인했는가 | 여러 행정동이 렌더링됐지만 E8 Core 조화 묶음은 없다 | **진입 불가**. 역·행정동 수량은 E9를 대신하지 않는다 |
| E10 | E9 승인 불변 빌드를 관찰창·rollback과 함께 제한 운영했는가 | 해당 제한 운영 창이 없다 | **진입 불가** |

따라서 현재 행정동 디오라마의 정직한 상한은 `자료·표현 보조 증거 존재 / E단계 미승격`이다. 각 자료 절편의 구현 수준을 설명할 수는 있지만 하나의 통합 E 번호로 올릴 수는 없다.

## 자동 검사 결과

2026-09-16 작업트리에서 현행 검사기를 그대로 실행한 결과다.

### 통과

- `eng/tests/station-diorama-evidence-rules.ps1`: 역·행정동 출처, 30개 동 수집 관문, 주소·필지 분리, 필지 도형 결손, 사람 승격 관문과 E 비승격을 통과했다.
- `eng/tests/world-interaction-gwae-classifications.ps1`: 현행 오행 분류 출력 v3와 원천 r10, 134건을 통과했다.
- `eng/tests/e7-native-work-order.ps1`: Loop 없는 native Goal과 기존 Loop 형식의 호환 관문을 통과했다.
- 정본 `eng/execution-ledgers/work-orders/*.e7-work-order.json` 63개를 현행 manager로 개별 확인했을 때 51개가 통과했다.

### 실패·드리프트

1. **행정동 디오라마 과거 E7 파일 3개가 현행 형식이 아니다.**
   - 이전 `administrative-dong-batch.e7-work-order.json`
   - 이전 `administrative-dong-base-geometry-capture.e7-work-order.json`
   - 이전 `administrative-dong-address-parcel-candidate.e7-work-order.json`
   - 세 파일 모두 `simulation-e7-vertical-work-order.v2`를 주장하지만 `protocolRevision`이 없어 첫 관문에서 거절된다. `activeWorldInteractionId`, 단계별 Logic·Presentation 계획, 현행 통합 단계 등 필수 구조도 없다.
   - 세 파일은 자료·화면 작업인데 `WI-CITY-SYNTHETIC-MOVE`를 공통 사용한다. 실제 플레이 의미와 자료 구현 범위를 명확히 구분하지 못한다.

2. **현행 E7 공통 회귀가 오행 출력 schema 판본 차이로 실패한다.**
   - E7 protocol은 `mirror-world-interaction-gwae-classification-output.v2`만 요구한다.
   - 현행 생성기는 v3를 만들고 별도 오행 검사는 v3를 정본으로 통과한다.
   - 그 결과 `e7-vertical-work-order.ps1`와 `e7-role-object-action-gate.ps1`가 `RoleObjectActionClassificationSchemaInvalid`로 실패한다.

3. **정본 E7 작업 명세 63개 중 12개가 현재 manager를 통과하지 못한다.**
   - v2/v3 schema 불일치 7개
   - Logic 단계와 PlayableUnit 단계 불일치 3개
   - Presentation 단계 불일치 1개
   - WI가 PlayableUnit 범위 밖인 항목 1개
   - 이것은 51개 통과를 무효화하지 않지만, 저장소 전체를 “현재 E7 정합”으로 부를 수 없게 한다.

4. **E8 캠페인 전수성 검사가 실패한다.**
   - 현재 `PlayableUnit`은 23개지만 `PlayableUnitStabilityCampaign`은 22개다.
   - 누락 대상은 `playable-loop:hexagram-campaign-retry.v1`이다.
   - `post-e7-evidence-campaigns.ps1`와 이를 소비하는 `evidence-management-systems.ps1`가 `StabilityCoverageCountInvalid`로 실패한다.

## Codex 사용 적합성

현재 체계는 **조건부 적합**이다. 의미 모델은 충분히 엄격하지만, Codex가 문서 제목·확장자·서술형 상태만 읽으면 과대 판정할 수 있다. 다음 순서를 기계적으로 지킬 때만 단계 판정에 사용한다.

1. **대상 분류**
   - `DataLayer`: 출처·수집·정규화·DB·hash 자료. `evidenceStageClaimed=null`이 기본이다.
   - `ObservationPresentation`: 권위를 바꾸지 않는 읽기 전용 화면. 유효한 Goal이 없다면 `PresentationProofOnly`다.
   - `PlayableInteraction`: 승인된 Goal·WI와 Logic·Presentation 두 궤적을 가진 경우에만 E1~E10 대상이다.

2. **정본 검사 우선**
   - 파일명의 `.e7-work-order` 또는 문서의 `E7` 문자열은 증거가 아니다.
   - 현행 manager 통과와 정본 등록을 모두 확인한다.
   - 과거 호환 파일은 별도 자료 구현 계약으로만 읽고 E단계 계산에서 제외한다.

3. **상향식 누적 소비**
   - E1부터 해당 목표 단계까지 같은 Goal·WI·revision·hash가 이어져야 한다.
   - Logic·Presentation 중 낮은 단계를 통합 단계로 사용한다.
   - 시험·DB 재조회·Game View 중 하나만으로 다른 궤적이나 중간 단계를 채우지 않는다.

4. **E8 이후 주체 전환**
   - E8은 등록된 안정성 캠페인, E9는 AreaHarmonySet과 사람 승인, E10은 불변 빌드·관찰창만 읽는다.
   - G 관리 완료, 행정동 수, 캡처 수, 규칙 후보 수를 E8~E10으로 환산하지 않는다.

5. **오류 시 보수적 중단**
   - manager·전수성·revision 검사가 하나라도 실패하면 마지막 유효 단계보다 높게 보고하지 않는다.
   - 검사기 자체 판본 드리프트이면 해당 대상의 새 승격을 중단하고 기존 확정 결과도 판본 범위를 명시한다.

## 권고 복구 순서

1. E7 protocol의 오행 출력 schema 소비를 현행 v3와 일치시키고 공통 E7 회귀를 복구한다.
2. 누락된 E8 안정성 캠페인을 등록하거나 해당 항목의 `PlayableUnit` 분류가 맞는지 먼저 재판정한다.
3. 정본 E7 작업 명세 12개의 단계·WI 범위 드리프트를 개별 복구한다.
4. 행정동 디오라마의 과거 E7 파일 3개를 `자료 구현 계약`으로 명시적으로 낮추고, 실제 플레이 약속이 승인될 때만 별도 native Goal·WI·현행 E7 작업 명세를 만든다.
5. 디오라마 증거 대장에 대상 분류와 `evidenceClaimMode = None / PresentationOnly / FullDualTrack`을 추가해 Codex가 자료 증거를 E단계로 환산하지 못하게 한다.

순서는 1~4의 정본 복구를 먼저 하고 5의 편의 기능을 나중에 적용하는 것이 안전하다. 잘못된 원장을 새 대시보드로 보기 좋게 만드는 것보다 판정 입력부터 맞추는 편이 우선이다.

## 확정

- E1~E10은 하위 근거를 누적 소비하는 상향식 체계로 유지한다.
- 디오라마 출처·수집·규칙 대장은 E 증거의 보조 계보이며 E단계를 자동 승격하지 않는다.
- 현재 행정동 디오라마는 자료·표현 증거가 존재하지만 E7 이상은 성립하지 않는다.
- 현행 manager가 거절하는 과거 행정동 `.e7-work-order` 파일을 E단계 근거로 사용하지 않는다.

## 미정

- 공통 E7/E8 원장 드리프트를 이번 행정동 기획 범위에서 직접 복구할지, 별도 증거 체계 정비 작업으로 분리할지.
- 행정동 디오라마에 실제 플레이 WI를 새로 정의할지, 당분간 자료·관찰 전용으로 유지할지.

## 다음 질문 하나

먼저 공통 E7/E8 검사 드리프트와 행정동 과거 E7 파일 3개의 잘못된 결속을 복구하는 정비 작업으로 진행할 것인가? 추천은 `진행`이다.
