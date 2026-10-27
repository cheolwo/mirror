# 서버 AI 배차·규칙 복귀·경험 학습 연결 검토 r1

2026-10-21 · `Reviewed / ExistingLocalInferenceValidated / GapsIdentified / WeightTrainingNotStarted`

사용자는 공개 모델을 서버에 두고 가상 상황을 반복 판단시키며, 느리거나 실패하면 규칙으로 업무를 이어가는 구조인지 검증하도록 요청했다. 이번 범위는 소스·현재 기록 감사, 기존 검증 도구 실행과 격리 모델 비교다. 제품 코드·설정·학습 가중치·운영 원장·배포를 변경하는 구현 승인은 아니다.

## 결론

원문을 보존하는 [운영 경험 누적 r1](operating-experience-accumulation.r1.md)은 유용하다. 그러나 현재 구조 전체를 **‘서버에 배차를 학습한 AI를 올려 자동 운영하는 체계가 완료됐다’**고 볼 수는 없다. 화물 가상 운영의 실제 선택은 규칙이며 AI는 비교만 한다. 경험 보관과 완전한 학습 사례 입력·정답 검토·내보내기·가중치 학습·모델 배포는 서로 다른 단계다.

## 현재 경로

| 경로 | 확인한 동작 | 남은 부분 |
| --- | --- | --- |
| 음식 단건 추천 | 규칙 후보 → 제한된 AI 선택 → 현재 후보/주문 재검사 → 추천. 오류/지연 시 현재 규칙 후보로 복귀 | 연속 장애 차단·전체 배치 시간 예산·서버 공유 추론 한도 |
| 화물·퀵 실제 추천 | 규칙 점수·계획 검증·추천 잠금 및 저장 직전 재검증 | 현재 실제 경로에 화물 LLM 선택·복귀 adapter 없음 |
| 가상 퀵 운영 호스트 | 기존 업무 진행 후 규칙 trace와 2초·회차 15회 제한의 AI 비교 관측 | 비교 결과를 실제 선택으로 적용하지 않음 |
| 기존 화물 검토 도구 | 합성 입력 생성·규칙/모델 비교·사람의 허용 후보/보류 선택·승인 자료 내보내기 | 새 경험 원장을 소비하는 source adapter·학습기·학습 모델 등록 연결 |
| 새 경험 corpus | 불변 원문·주문 연결·미확인·사람 메모 보존 | 당시 완전한 입력/모델 판본·구조화 정답이 없어 학습 적격 false |

근거는 다음 기존 코드다.

- [음식 후보 AI](../../../../../Ssalddel/Services/Dispatch/AI/OllamaFoodDispatchCandidateAI.cs): `SelectAsync`, 기본 10초, 응답 형식·허용 토큰 검사, 지연/연결/JSON 오류 복귀, 호출자 취소 전파.
- [음식 업무 정책](../../../../../Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs): 규칙 후보와 AI 응답 뒤 현재 후보 재계산. [추천 후보 선정](../../../../../Ssalddel/Services/Dispatch/Queue/배차추천후보선정Service.cs)은 주문 변경도 재확인한다.
- [화물 DI](../../../../../Ssalddel/Extensions/ServiceCollectionExtensions.DomainServices.Operations.cs): `I국내화물기사배정AIService`는 `규칙기반국내화물기사배정AIService`에 연결된다. 이름의 AI가 LLM 호출을 뜻하지 않는다.
- [훈련 Actions](../../../../../eng/Ssalddel.DispatchTraining/DispatchTrainingActions.cs)·[비교 AI](../../../../../eng/Ssalddel.DispatchTraining/DispatchTrainingShadowAI.cs): `AdvanceAsync` 뒤 비교하고 `ShadowDriverId`를 기록한다. 이는 AI 적용 실패를 규칙으로 대체한 운영 실적과 다르다.
- [화물 사례 도구](../../../../../eng/CargoRegionalTrainingReview/CargoTrainingOllama.cs)·[검토 원장](../../../../../eng/CargoRegionalTrainingReview/CargoTrainingStore.cs): 20초 비교, 입력/프롬프트/모델 digest, 명시 검토와 holdout 제외를 이미 지원한다.

## 현재 누적 자료의 효율

원본 그대로의 보관 자료와 사람이 검토하거나 학습에 넣을 대표 자료를 분리하는 것이 적합하다.

| 현재 corpus 관측 | 값 |
| --- | ---: |
| 가상 회차 / 접수 시도 경험 / 정본 참조 주문 | 3 / 11 / 9 |
| 판단 관측 / 서로 다른 판단 ID | 366 / 366 |
| AI 예산 소진 생략 / 단일 후보 생략 / 시간 초과 | 349 / 5 / 12 |
| 유효 AI 성공 / 사람 검토 | 0 / 0 |
| 한 주문에 집중된 판단 관측 | 342 |

규칙 상태·선택 기사 label·후보 배열만으로 묶은 참고 투영은 23종이다. 당시 완전한 입력 해시가 없으므로 **23개 독립 학습 사례라고 확정하지 않는다.** 새 GUID가 붙은 기록 366개를 학습 사례 366개로 넣으면 반복과 한 주문의 편중을 키운다. 모델 호출 생략이나 시간 초과도 AI 정답이 아니다.

11건 전부 주문 조건·단계별 시각·정책 지문·모델 digest·학습 사례 연결을 미확보 상태로 유지한다. [경험 검토 DTO](../../../../../eng/Ssalddel.DispatchTraining/Experiences/DispatchExperienceDtos.cs)의 `approve/correct/defer`와 메모에는 **정답 후보 ID/허용 보류**가 없다. 새 경험의 검토를 기존 학습 답안 승인으로 바꾸지 않는다. 기존 `CargoRegionalTrainingReviewCommand`의 `AcceptableCandidateIds`·`AcceptHold`와 판본 검사를 재사용하는 후속 연결이 필요하다.

과거 입력은 현재 DB나 추정치로 소급 채우지 않는다. 현재 자료는 먼저 복구·예산 소진·시간 초과 회귀 근거로 사용한다. 앞으로 생성한 가상 상황은 seed·시나리오 가족·완전한 입력을 처음부터 동결할 수 있다. 모델에는 상황과 후보를 주고 평가 정답은 분리한다. 모델 출력·실제 사용 선택·사용자 확인 답안도 별개다.

## 실제 로컬 실행 결과

기존 중지된 `mirror-local-dispatch-ai-ollama-1`만 잠시 시작했다. 기존 `qwen3:1.7b`의 digest는 `8f68893c685c3ddff2aa3fffce2aa60a30bb2da65ca488b61fff134a4d1730e7`, Ollama는 0.40.2였다. CPU 4개·RAM 6GiB·추론 병렬 1·GPU 예약 없음이다. 다운로드·모델 생성/교체·학습은 하지 않았다. 검증 뒤 원래 중지 상태로 복구했다.

기존 검토 도구를 빌드하고 `--compare-first 3 --data-root artifacts/local/server-ai-learning-readiness/20261021-r1/model-comparison`으로 새 격리 저장소에서 실행했다.

| 합성 사례 | 응답 시간 | 모델 검증 결과 | 유지된 규칙 후보 |
| --- | ---: | --- | --- |
| `cargo-review-long_wait-01` | 15,070ms | `invalid / UnsupportedReasonFact` | C1 |
| `cargo-review-long_wait-02` | 11,686ms | `invalid / UnsupportedReasonFact` | C2 |
| `cargo-review-deadline_flip-01` | 11,065ms | `invalid / UnsupportedReasonFact` | C2 |

세 응답 모두 C1을 제안하며 입력에 없는 `PLAN_VERIFIED`를 주장했다. JSON 형태만 맞았다고 유효 판단으로 인정하지 않았다. 규칙 비교 결과는 보존했고, 실제 추천·수락·알림·업무 원장을 변경하지 않았다. 이 세 건은 전체 모델 성능의 통계가 아니며 정식 cold/warm·동시 부하 벤치마크도 아니다. 현재 설정에서 2초 실시간 비교를 충족했다고 볼 수 없다.

모델 중지 상태에서도 별도 비교 1건을 실행했다. 약 2,052ms에 `timeout / ModelTimeout`, AI 후보 null로 기록하고 규칙 C1을 유지했다. 이 상태는 실제 모델 추론이 느린 경우와 연결 제한 시간 초과를 같은 코드로 기록할 수 있어 실패 원인 분류가 후속 개선점이다. 모델 연결 실패를 AI 성공이나 새 정답으로 세지 않는다.

- 기존 음식 AI 선택·업무 정책·추천 적용 관련 시험 174개 통과, 실패/생략 0개. HTTP 대역·격리 시험이지 실제 휴대폰·푸시·클라우드 운영 검증이 아니다.
- 기존 화물 검토 자체 검사 34개 통과, 실패 0개. 합성 사본·대역 모델 검사로 실제 사람 승인·운영 DB·가중치 학습은 0이다.
- 검토 도구 및 의존 서버 빌드 통과, 오류/경고 0개.
- 원래 경험 corpus의 `current-summary.json` SHA는 작업 전과 같다. 이번 모델 비교/실패는 기존 검토 자료를 덮지 않고 별도 경로에 남겼다.

실행 증거는 `artifacts/local/server-ai-learning-readiness/20261021-r1/`의 `experience-data-audit.json`, `local-model-state.json`, `model-comparison-summary.json`, `unavailable-comparison-summary.json`, `cargo-review-selfchecks.json`, `tests/dispatch-ai-fallback.trx`, `verification-summary.json`이다.

## 서버 등록 준비와 지연 위험

[현재 모델 옵션](../../../../../Ssalddel/Services/Dispatch/AI/FoodDispatchOllamaOptions.cs)은 HTTP·명시적 loopback IP/포트·고정 모델만 허용한다. 별도 Docker 컨테이너 DNS나 Azure 모델 URL을 설정에 넣기만 하면 되는 상태가 아니다. 외부 서버로 옮기려면 같은 네트워크 공간에 두는 배치 또는 허용 사설 주소·인증·지속 모델 volume·상태 확인·기동/복구 정책을 검증해야 한다. 기존 URL 검사를 단순 제거하는 방식은 제안하지 않는다.

제품 선언 기본값은 OFF지만 현재 PC의 `appsettings.Local.json`에는 음식 후보 AI ON·10초가 저장돼 있다. 일반 PC 호스트의 설정 우선순위에 따라 적용될 수 있으므로 ‘현재 모든 서버 AI가 OFF’라고 단정하지 않는다. 이번 훈련 호스트는 별도 설정에서 음식 후보 AI를 강제로 OFF하며, 이번 감사에서는 제품 서버를 새로 시작하거나 해당 설정을 바꾸지 않았다.

[큐 스캔 Job](../../../../../Ssalddel/Infrastructure/BackgroundJobs/FoodDeliveryDispatch/음식배달배차큐스캔Job.cs)은 주문을 순차 await하고 동시 Job 실행을 금지한다. 기본 배치 100건·주기 30초에서 여러 요청이 각 10초를 기다릴 가능성이 있으므로 **개별 fallback이 있음과 전체 배차가 즉시 계속됨은 다르다.** 관련 경로의 연속 실패 cooldown/circuit breaker·배치 전체 시간 예산·공유 추론 제한은 후속 검증/보완 대상이다. 실제 누적 지연 부하 시험을 이번에 수행한 것은 아니다.

## 제안하는 다음 순서

아래는 제안이며 이번에 구현하거나 운영 활성화하지 않았다.

1. **완전한 판단 1회 묶음:** 기존 합성 생성기를 재사용해 seed/가족/입력·후보/배제 사유·규칙 결과·모델/프롬프트/입출력 지문·지연/실패·실제 사용 선택/출처를 동결한다. 기존 경험 snapshot에는 참조만 연결한다.
2. **기존 검토 도구 연결:** 완전한 새 묶음만 source adapter로 전달하고, 같은 입력의 대표만 사용자에게 보여 허용 후보/보류를 선택하게 한다. 기존 승인·원자료·평가 자료를 덮지 않으며 새 자료로 예전 승인을 승계하지 않는다.
3. **실시간 지연 관문:** 화물에서 규칙의 허용 후보/계획만 AI가 고르도록 연결할 때 짧은 기한·전체 배치 예산·연속 장애 시 즉시 규칙 사용을 함께 검증한다. 응답 뒤 현재 약속·적재·수신 상태를 다시 검사한다. 구체 기한 수치는 부하 측정과 사용자 판단 전 미정이다.
4. **학습·배포는 분리:** 사용자 승인 자료만 내보내고 주문/시나리오 가족별로 학습·미사용 평가를 분리한다. 처음에는 승인 사례 참조와 규칙 대비 평가를 하고, 필요할 때 별도 미세조정·Ollama 등록·서버 제한 적용을 검토한다. 실제 주문 중 자동으로 가중치를 바꾸지 않는다.

```text
오프라인: 가상 상황 → 규칙/모델 비교 → 사용자 답안 → 승인 사례·평가 자료
실시간:   주문 → 적격 후보/계획 → 기한 내 유효 AI 선택 → 현재 상태 재검사
                               └─ 지연·오류·불명확 → 현재 규칙 결과
공통 기록: 당시 입력·모델 결과·실제 사용 결과·수행 결과·사용자 의견을 분리
```

## 공식 자료 확인

- [Ollama Docker](https://docs.ollama.com/docker): CPU/GPU로 자체 호스팅할 수 있다. 이 가능성과 현재 Azure 배포 완료는 다르다.
- [Qwen3-1.7B 모델 카드](https://huggingface.co/Qwen/Qwen3-1.7B): Apache-2.0 모델이다. 모델 사용료와 별개로 실행 서버의 CPU/GPU·RAM·저장소 비용을 검토해야 한다.
- [Ollama 모델 가져오기](https://docs.ollama.com/import): 준비한 모델 가중치를 등록해 실행하는 경로다. 우리 프로젝트의 기록 접수가 모델 가중치 변경을 실행하지는 않는다.
- [구조화 출력](https://docs.ollama.com/capabilities/structured-outputs): JSON Schema 제약은 가능하지만 사실·배차 적정성 검사는 서버가 따로 해야 한다. 문서상 Ollama Cloud는 현재 이 구조화 출력을 지원하지 않아 자체 호스팅 구성과 혼동하지 않는다.
- [Azure Linux VM 요금](https://azure.microsoft.com/en-us/pricing/details/virtual-machines/linux/): 인스턴스·리전·사용량별 실행 비용이 있다. 모델 라이선스가 무료라는 이유로 서버 운영 전체가 무료가 되는 것은 아니다. 이번에는 유료 리소스 생성·변경·배포를 하지 않았다.
