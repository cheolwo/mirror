# 화물 우선 추천 기본·목록 조회 구조 검토 r1

> 이 문서는 구현 전 조사 기록이다. 사용자 승인 뒤의 현재 구현·시험·실행 상태는 [구현 r1](cargo-recommendation-default.implementation.r1.md)을 따른다. 아래 당시 결손과 실행 상태는 과거 근거로 보존한다.

- 날짜: 2026-10-21
- 기획: `PLAN-OPERATIONS-DISPATCH-CORE`
- 상태: `DirectionAccepted / SourceAudited / PrioritiesSet / ProductUnchanged`
- 사용자 방향: AI 추천을 기본으로 하고 목록 조회도 제공한다. 음식·화물·피킹을 장기적으로 조합하되 지금은 화물 운송을 먼저 완결한다.
- 이번 범위: 현재 코드·기존 시험·실행 중 합성 회차의 읽기 검토와 다음 작업 순서 정리다. 제품 코드·기능 옵션·배차 정책·모델 가중치·실행 중 캠페인은 변경하지 않았다.

## 먼저 구분할 세 가지

| 구분 | 의미 | 현재 상태와 방향 |
| --- | --- | --- |
| 추천과 자율 공개 콜 | 누가 해당 일의 제안을 받거나 찾아서 수락할 수 있는가 | 신규 생활배송은 `automatic` 기본. `public-call`·`hybrid`는 별도 동의된 배차 방식이다. 화면 보기 전환으로 이 동의를 바꾸지 않는다. |
| 지도와 목록 | 같은 계정에 허용된 정보를 어떤 형식으로 보는가 | 공통 화면에 이미 지도/목록 전환이 있다. 기본 지도·추천 중심을 유지하고 목록은 보조 보기로 둔다. |
| 규칙과 LLM | 허용된 기사·계획 중 누가 추천 판단을 하는가 | 실제 화물 선택은 규칙이다. Ollama는 훈련 비교만 한다. 향후 검증된 후보 안에서 모델이 선택하고, 지연·무효 응답은 현재 규칙으로 복귀한다. |

‘목록 조회’라는 말만으로 모든 운송을 공개하거나 공개 콜 기능을 새로 활성화하지 않는다. 우선 기존 추천 결과의 목록 보기를 재사용한다. 다른 기사의 자율 탐색까지 연결할 때는 아래 공개 콜 결손을 별도 세로 흐름으로 마감한다. 추천을 기본으로 하더라도 기사의 수락과 실제 수행·완료는 별도 상태다.

현재 근거:

- [생활배송 작성 기본값](../../../../../Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryAuthoringViewModel.cs): 133·473행의 `Automatic`.
- [공통 지도/목록](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleMapWorkspace.razor): 20~22행 전환, 185~187행 같은 `Model.Items` 표시. [보기 상태](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Core/RoleWorkspaceState.cs): 58행 `ListOnly=false`.
- [화물 작업공간](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoWorkspaceAdapters.cs): 139~146행 추천 조회. 수행 중 현재 업무에 집중하는 기존 정책과 추천 정보 조회를 구분한다.
- [제품 DI](../../../../../Ssalddel/Extensions/ServiceCollectionExtensions.DomainServices.Operations.cs): 190행의 규칙 서비스. [훈련 AI](../../../../../eng/Ssalddel.DispatchTraining/DispatchTrainingShadowAI.cs): 28~29행 비교 전용 선언.

## 발견한 보완점

### 1. 화물 모델 판단을 실제 추천에 적용하는 연결이 없다

실제 큐 스캔은 `배차큐스캔Job` → `배차대기원장전환Service.Recommendation` → [용달운송배차업무정책](../../../../../Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.cs):81~87의 단건 후보를 사용한다. 이름에 AI가 있는 [기사 배정 서비스](../../../../../Ssalddel/Services/Dispatch/Coordination/국내화물기사배정AIService.cs)는 규칙 비용/점수 정렬이다. 다건 조율·계획 검증·잠금 적용 서비스가 등록돼 있지만 이번 제품 참조 조사에서는 DI·하위 실행 서비스·시험 외의 실제 진입 호출을 찾지 못했다.

따라서 현재 합성 주문이 배송 완료됐다는 결과를 LLM이 실제로 기사를 골랐다는 증거로 바꾸지 않는다. 모델 선택, 실제 사용한 규칙/AI 출처, 추천 저장, 기사 수락, 수행 결과를 별도로 남겨야 한다.

### 2. 대기 기사와 수행 중 기사의 픽업 약속 검사가 다르다

[용달운송배차업무정책](../../../../../Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.cs):224~250에서 예상 도착·여유 시각은 처음에 null이고 수행 중 기사에게만 실제 경로·가용 시각·전체 일정 검사를 적용한다. [원거리 접근 조건](../../../../../Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.RemoteAccess.cs):56~62는 기본 반경 안이면 시간창 검사 전에 허용한다. 대기 기사는 이 상태로 `CandidateAllowed`가 될 수 있다.

이는 정적으로 확인한 검사 차이다. 현재 런타임에서 만료 주문을 만들어 재현하지 않았다. 최종 [수락 적격성](../../../../../Ssalddel/Services/Dispatch/Recommendation/화물배차수락적격성Service.cs):183~191·230~240에는 추가 검사가 있으므로 보호 없이 확정된다고 단정하지 않는다. 우선 위험은 ‘추천은 받았지만 수락하면 약속/일정 문제로 거절되는’ 불일치다.

후속 회귀는 대기 기사도 **반경 안이지만 이미 만료 / 지금 출발해도 늦음 / 시간창·경로 미확인 / 충분히 도착 가능**을 구분해야 한다. 수행 중 기사와 공통 약속 조건을 검사하되 기존 끝점·잔여 적재·현재 업무 책임은 보존한다.

### 3. 훈련 모델 응답을 성공으로 인정하는 검사에 차이가 있다

[훈련 ShadowAI](../../../../../eng/Ssalddel.DispatchTraining/DispatchTrainingShadowAI.cs):123~142는 message와 허용 후보 토큰을 검사하지만 응답의 정확한 `model`과 `done=true`는 검사하지 않는다. [제품 공통 판독기](../../../../../Ssalddel/Services/Dispatch/AI/OllamaChatResponseReader.cs):32~40에는 두 검사가 이미 있다.

잘못된 모델·미완료 응답이 실제로 관측됐다는 뜻은 아니다. 그런 응답을 비교 성공으로 누적할 수 있는 정적 결손이다. 다른 모델·`done=false`·done 누락·정상 응답을 분리하는 계약 시험과 기존 판독 기준 재사용이 필요하다. 과거 원문이나 비교 결과를 조용히 고치지 않는다.

### 4. 다건 추천 적용의 transaction 경계는 연결 전에 검증해야 한다

[다건 잠금 적용](../../../../../Ssalddel/Services/Dispatch/Coordination/국내화물배차조율적용Service.Locking.cs):26~27은 직접 transaction을 시작하고, MySQL 설정은 `EnableRetryOnFailure`다. 이 경로에는 전체 transaction을 실행 전략으로 감싼 경계가 없다. 직전 [추천 재검증 수정](../../../../../Ssalddel/Services/Dispatch/Coordination/국내화물추천재검증Service.cs):39~51과 유사한 잠재 충돌이다.

이번에 실행한 단건 캠페인의 장애로 재현한 것은 아니다. 다건 조율을 제품 진입에 연결하기 전에 전체 transaction 단위 재시도, 외부 transaction 소유권, 취소, 중복 추천/알림을 검사해야 한다. 기존 [재시도 시험](../../../../../Ssalddel.Tests/Application/Driver/DispatchAction/CargoRecommendationRefreshExecutionStrategyTests.cs)을 참고하되 그 통과를 이 별도 경로의 통과로 간주하지 않는다.

## 자율 공개 콜까지 연결할 때의 별도 결손

같은 추천의 목록 보기는 이미 있다. 아래는 비추천 기사도 찾아 수락하는 공개 콜을 추가로 연결할 때 해결할 내용이며 이번에 기능을 켜지 않았다.

- [수락 화면](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionViewModel.cs):45~48은 추천 API에서만 찾으므로 공개 전용 건을 만료 추천으로 거부한다.
- [공개 목록 서비스](../../../../../Ssalddel/Services/Dispatch/Queue/공개배차Service.cs):96~100은 실제 차량 판정 없이 적합 true·경고 없음으로 투영한다. 이를 적재·일정 검증 완료라고 표시하면 안 된다. 목록의 미확인과 상세/수락의 검증 결과를 구분해야 한다.
- 일반 공개 콜의 기사 보수 근거는 null일 수 있다. [기존 투영 시험](../../../../../Ssalddel.Tests/Services/Dispatch/CargoCallOfferProjectionTests.cs):99과 [보수 표시](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoDriverPayoutPresentation.cs):10~21을 기준으로 고객 청구 운임을 기사 보수로 대체하지 않는다.
- 생활배송 [상세 접근 정책](../../../../../Ssalddel/Application/Driver/Recommendation/생활배송기사정보공개Policy.cs):20~28은 추천/확정 기사 관계를 요구한다. 공개 목록용 조건 조회가 필요하더라도 상세주소·좌표·연락처 보호를 완화하지 않는다.
- 수락 화면은 타이머가 있을 때만 추천 판본을 전달하지만 생활배송 공개 모드도 현재 판본 검사가 필요하다. 공개 제안의 판본을 보존해 같은 수락 명령으로 검증한다.
- 현재 공개 목록은 전체 대기를 읽은 뒤 조건을 순차 검사한다. 실제 공개 운영 전 페이지 조회와 정렬·결과 크기를 별도 계약으로 정한다. 임의 상한으로 일부 일을 누락시키지는 않는다.

## 다음 작업 순서

1. **추천 전 약속 검사와 비교 자료의 정확성:** 대기/수행 기사 ETA 조건을 맞추고 모델 응답의 완료·모델 일치를 검사한다. AI가 고르면 안 되는 후보와 잘못된 성공 자료를 먼저 줄인다.
2. **화물 한 건의 AI 선택 연결:** 기존 허용 후보·수행 계획 안에서 선택 → 짧은 호출 기한/공유 예산 → 현재 조건 재검증 → 기존 추천 저장. 지연·오류·후보 변경은 현재 규칙으로 복귀한다. 기사의 수락은 유지한다. 현재 모델 지연으로 실시간 충족을 확인하지 못했으므로 검증 환경부터 연결한다.
3. **기본 추천·보조 목록의 실제 사용 검증:** 기존 지도/목록 전환을 재사용하고 선택한 일·보수·화물·상하차 조건이 일치하는지 확인한다. 자율 공개 콜까지 연결할 경우 위 결손을 한 흐름으로 검증한다.
4. **여러 화물의 조합:** 다건 transaction과 실차·구간별 적재·시간창·현재 업무·수락 경쟁을 검증한 뒤 실제 진입에 연결한다. 음식·피킹 조합은 그 뒤 전문 원장과 자격·동의·선호를 유지하며 확장한다.

```text
화주: 화물·상하차 장소·시간·운임 등록
└─ 서버: 입력/차량/약속 검사 → 가능한 기사·계획
   ├─ 기본 추천: 유효 AI 선택 → 현재 상태 재검사 → 기사에게 제안
   │            └─ 느림·무효 → 현재 규칙 결과로 제안
   └─ 보조 목록: 현재 허용된 추천을 조회 (공개 콜은 별도 동의 범위)
      └─ 기사 수락 → 픽업 → 전달 완료 → 실제 사용 판단과 결과 누적
```

위 트리는 목표 구조다. 현재 화물 제품에 LLM 실제 선택이나 다건 진입이 연결됐다는 완료 표시는 아니다. 정확한 기한/부하 예산·금액 가중치는 별도 측정·문답 전 미정으로 둔다. 오래 기다린 기사 우선은 약속과 적재·자격을 충족하는 후보 사이의 선택 기준이며, 지킬 수 없는 약속을 가점으로 상쇄하지 않는다.

## 이번 검증 수준

제품 소스·DI·route·기존 시험 소스를 조사했고, 실행 중 가상 캠페인의 상태를 읽었다. 현재 합성 수행의 규칙 추천 성공과 Ollama 비교 timeout을 분리했다. 새 구현·모델 호출·재현 시나리오·build/test·실제 화면 렌더·기기·운영 DB 변경·클라우드 배포·commit/push는 하지 않았다. 문서 링크와 diff만 별도 검사한다. 기존 캠페인과 검토 자료는 그대로 유지한다.
