# 로컬 배차 수요 참고 AI 시험

이 도구는 음식 배차 서버와 독립된 **읽기 전용 실험**이다. 권역별 주문 집계를 작은 로컬 언어 모델에 보여 주고 JSON 참고 응답을 검사한다. 운영 DB·업무 API·기사 배정·대기 승격·원장 상태 변경은 호출하지 않는다. LLM이 없거나 틀리거나 응답하지 않아도 서버 규칙은 계속 사용한다.

## 이번 준비 상태 (2026-10-11)

- **최신 사용자 요청에 따른 Docker 시작과 실제 모델 응답을 확인했다.** [Docker 실행·검증 기록](docker/README.md)에 따라 `ollama/ollama:0.40.2`와 `qwen3:1.7b`를 CPU로 실행했다. 합성 balanced 한 건은 약 12.7초에 `validated_advisory_only`를 반환했다. 운영 API·상태 변경은 0건이며 GPU·Azure·제품 배차 통합은 별도다.
- PC: Intel i5-14400, RAM 약 31.7GiB, NVIDIA RTX 5050 VRAM 8,151MiB. GPU 값은 4GB 이상을 잘못 보고할 수 있는 WMI 대신 `nvidia-smi`로 확인했다.
- 공식 Ollama `v0.40.2` Windows AMD64 portable ZIP을 다운로드하여 공식 릴리스의 SHA-256과 대조하고 사용자 폴더에 압축을 풀었다. 실행 파일의 Ollama Inc. 서명도 확인했다. 설치 프로그램·Windows 서비스·자동 시작·사용자 PATH·방화벽 규칙은 변경하지 않았다. 검증·압축 해제가 끝난 임시 ZIP은 삭제했다.
- 모델은 공식 Ollama registry의 `qwen3:1.7b`, Q4_K_M 약 1.4GB를 선택했다. 기존 표준 설치/모델 경로에 재사용할 Ollama 또는 GGUF 모델은 없었다. 다운로드한 모델과 설치 지문·저장 공간은 Git 제외 실행 기록으로 확인한다.
- **이전 Windows portable 서버 시작은 실행 도구에서 `blocked by policy`로 거절됐다.** 상세 사유·정확한 검토 구성요소는 제공되지 않았다. 후속 사용자가 Docker 실행을 한 번 요청한 이번에는 실행 도구가 Docker 시작을 허용했고, API·모델 지문·실제 합성 추론을 확인했다. 이전 거절의 원인이나 모든 실행 경로가 해제됐다고 주장하지 않는다. GPU 사용과 운영 배차 품질은 미검증이다.
- 원시 실행 기록: 저장소의 `artifacts/local/local-dispatch-ai/`. 설치 파일과 모델: `%LOCALAPPDATA%\Ssalddel\LocalDispatchAI\`. 출력·로그·가중치는 Git에 넣지 않는다.

## 모델 없이 운영 시나리오 비교 (r45)

[승인 범위와 해석 기준](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/offline-scenarios.implementation.r45.md)에 따라, 실제 `음식배달수요참여Policy`를 호출하는 별도 .NET 개발 도구를 추가했다. 제품 서버·AI·DB를 시작하지 않는다. 고정 합성 입력은 [scenarios.json](ScenarioRunner/scenarios.json)이며 실제 배달 기록·고객 정보는 읽지 않는다.

```powershell
pwsh -NoProfile -File eng/local-dispatch-ai/run-scenarios.ps1
```

12가지 수요·유입·자료 장애 상황에서 OFF 가정/관측/모의 정확/과소/과대/응답 없음/무효/만료를 계획 처리량 3·3.5·4건/h로 비교한다. 정확 모의값은 알려진 미래 도착표를 사용하며 AI 예측 정확도의 증거가 아니다. 시간당 계획 처리량과 실제 수행시간은 분리하고, 가상 기사별 수행은 순차로 누적하며 동시 약속 3건을 검사한다. 실제 ETA·경로·조리·수락 엔진은 이 도구에서 재현하지 않는다.

최신 보고서는 `artifacts/local/local-dispatch-scenarios-r45/campaign/report.md`, 최신 실행 포인터는 `latest.json`이다. 실행별 `runs/<근거 해시>/` 아래 입력 사본·전체 결과·보고서를 보존하고 다른 근거로 덮어쓰지 않는다. 입력·정책·실행 파일의 해시, 기사별 가상 집계와 분별 시간표, 결정 해시 재현·자료 오류 복귀·주문 보존 검사를 남긴다. 배정 대기 10분 이상·모의 완료소요 45분 초과·미완료는 탐색용 검토 대상이며 실제 SLA·자동 취소/운영 설정이 아니다. 실제 이동 시간·소득·정원 원자성·실서버/단말 성공을 뜻하지 않는다. 서버 업무 회복 시험은 `artifacts/local/local-dispatch-scenarios-r45/workflow-recovery/`의 별도 기록이다. 네트워크 0회는 시나리오 런타임 범위이며 도구 빌드의 패키지 복원은 별도다.

## 프로젝트 근거를 참고하는 설명 r46

[승인 범위·근거 분류·검토 기준](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/project-advisory.implementation.r46.md)에 따라 `project_advisor.py`는 기존 정책의 계산 결과와 허용된 규칙·출처를 `qwen3:1.7b`에 보여 주고 한국어 설명·위험·기존 대안 의견을 받는다. 가중치 학습이나 제품 배차 통합은 하지 않는다. 아래 기존 `dispatch_advisor.py`의 단순 합성 산술 시험과 별도 진입점이다.

`run-scenarios.ps1`가 기본 입력 `artifacts/local/local-dispatch-scenarios-r45/campaign/advisory-input.json`을 만든다. 관측 기준·3.5건/h의 12개 상황에서 최대 3개 시점씩 선택하며 대안은 이미 계산된 전체 기간의 사후 지표다. 해당 시점의 미래 예측이나 정책 변경 효과로 해석하지 않는다. 허용 근거는 [project-rules.json](project-rules.json)의 현재 규칙·시험 가정·후속 제안을 분리하며 현재 소스 앵커·해시를 대조한다.

기본은 HTTP 없는 dry-run이다. 실제 호출에는 `--execute`를 명시하고 실행 중인 Docker의 루프백 주소를 사용한다. `--max-cases`는 1~36(기본 36), 개별 `--timeout`은 기본 90초다. 실행 도구는 컨테이너를 자동 시작하거나 종료하지 않는다. 응답 구조·출처·숫자 검사 실패와 연결/시간 초과는 기록하고 기존 규칙 결과를 유지한다.

저장소 루트에서 다음 순서로 사용할 수 있다. 첫 명령은 모델을 호출하지 않고 합성 비교 입력을 생성한다.

```powershell
pwsh -NoProfile -File eng/local-dispatch-ai/run-scenarios.ps1
python eng/local-dispatch-ai/project_advisor.py run
python eng/local-dispatch-ai/project_advisor.py run --execute --max-cases 36 --timeout 90 --endpoint http://127.0.0.1:12681
```

기본 출력은 `artifacts/local/local-dispatch-ai/project-advisory-r46/`다. 실행별 입력·근거·요청·응답·검사·시간을 보존한다. 검토 명령으로 사람의 승인 또는 정정을 추가하며 `approved` 기록만 `training-candidates.jsonl`로 분리한다. 이 파일은 향후 학습 후보이고 실제 추가 학습·정답 확정·운영 승격은 아니다. 이번 12개 상황·30개 시점의 실제 호출은 완료됐다. 구조 검사 18개 통과·12개 규칙 결과 복귀이며 설명 정확도·사람 승인은 별도다. 실행·검사·지연·반례 수치는 [변경 기록](../../docs/Changes/2026-10-11-local-dispatch-project-advisory-r46.md)에서 확인한다.

현재 소형 모델의 구조 통과 응답에서도 대안의 비교 방향을 잘못 옮기거나 모순된 설명이 발견됐다. 구조 통과율은 설명 정확도가 아니며 자동 운영 판단에 채택하지 않는다. 검증한 사실·비교 지표와 자유 설명을 구분하고 사람 검토 전 참고용으로만 사용한다. CPU에서 설명 한 건이 수십 초 걸리는 현재 구성은 실시간 배차의 응답을 기다리는 경로에 넣지 않고 기존 배차와 분리된 사후 검토로 유지한다.

최종 실행이 끝난 뒤 [검산 보고 도구](project_review_report.py)로 입력·실행·결과 해시와 기준값을 대조한 수치표를 만든다. 모델을 추가 호출하지 않는다.

```powershell
python eng/local-dispatch-ai/project_review_report.py --run '실제 실행 ID'
```

`runs/<실행 ID>/verified-comparison.md`의 수치표를 먼저 보고 별도 `report.md`의 AI 초안·응답을 대조한다. 수치표는 C# 입력의 기준 결과와 대안 4종에서 직접 만들며 null은 미확인으로 표시한다. `verified-comparison.meta.json`은 입력/실행/결과/보고서/생성기 해시를 연결한다. 같은 내용은 재사용하고 내용이 달라진 기존 파일을 덮어쓰지 않는다. 수치표 생성은 AI 문장의 정확성·사람 승인과 별개다.

실제로 검토한 뒤 해당 실행·사례 ID로 의견을 기록한다. 아래 값은 예시이므로 실제 결과의 식별자로 바꾼다. 시험 성공만을 근거로 승인 명령을 자동 실행하지 않는다.

```powershell
python eng/local-dispatch-ai/project_advisor.py review --run '실제 실행 ID' --case '실제 사례 ID' --verdict needs_correction --reviewer '검토자 이름' --note '정정할 근거와 내용을 기록'
```

문제 없는 응답을 사람이 승인할 때만 `--verdict approved`를 사용한다. `reviews.jsonl`은 이전 의견을 보존하고 새 의견을 추가한다. 같은 실행·사례의 최신 의견이 정정 필요라면 파생 학습 후보에서 제외한다. 기존 입력·응답·검토 기록은 남는다. 공개 주소·계좌·인증값·개인정보를 검토 메모에 넣지 않는다.

후보를 별도 학습 검토에 넘기기 전 다음 명령으로 검토 이력·후보·요청/실행/응답 해시를 다시 대조한다. `training-candidates.meta.json`이 없거나 오래됐거나 연결 자료가 달라지면 사용을 중단한다. 이름 입력을 독립적인 검토자 신원 확인으로 간주하지 않는다.

```powershell
python eng/local-dispatch-ai/project_advisor.py check-candidates
```

## 기존 단순 집계 시험

일반 LLM은 배차 전문 최적화 엔진이 아니다. 이번 작은 모델은 합성 집계를 이해하고 정해진 계산을 설명하는 첫 연결 시험용이다. 실제 시간대별 수요 예측이나 배차 품질을 학습·검증한 모델이 아니다.

`quiet`, `balanced`, `surge` 세 표본만 내장한다. 입력은 합성 권역 코드·대기 주문 수·다음 한 시간 주문 가정·활동 기사 수·기사당 시간 처리량이다. 실제 주소·좌표·고객·기사 식별자·주문번호·사진·전사·배달 원장은 읽거나 보내지 않는다. 임의 파일/문장을 입력하는 옵션도 없다.

설명용 기준선은 다음과 같다.

```text
활동 기사 참고 수 = 올림((대기 주문 + 다음 한 시간 주문 가정) / 기사당 한 시간 처리량)
```

이 계산은 서버의 현재 수행 여력·긴급성·참여 순서·최대 대기·기존 약속을 반영하는 최종 정책과 구분한다. 모델은 입력된 수요 가정을 그대로 되풀이하며 수요를 예측했다고 주장하지 않는다. 응답의 `confidence`도 모델의 주관적 값으로 보존하며 측정된 정확도나 정책 가중치로 사용하지 않는다.

JSON의 필드·권역·수치 범위·유한한 confidence·읽기 전용 표시·기준선 계산 일치를 검사한다. 불일치·시간 초과·연결 실패·잘못된 JSON·미완료·도구 호출·과대 응답이면 `rule_reference_retained`로 돌아간다. 정상 응답도 `advisoryOnly`이며 실배차 효과는 0건이다.

## 실행

Python 3.10 이상이 필요하다. Python 외부 패키지는 쓰지 않는다. 기본 실행은 HTTP 요청 없는 dry-run이다.

```powershell
python eng/local-dispatch-ai/dispatch_advisor.py --scenario balanced
python -m unittest discover -s eng/local-dispatch-ai -p 'test_*.py' -v
```

Python 경로를 지정해 기본 시험을 할 수도 있다.

```powershell
pwsh -NoProfile -File eng/local-dispatch-ai/run-local.ps1 -Mode DryRun -PythonPath 'C:\path\to\python.exe'
```

해당 환경에서 런타임 실행이 허용될 때 다음 명령은 준비된 portable 서버를 숨김 창·루프백 `127.0.0.1:12681`에 잠시 시작하고 표본 하나를 시험한 뒤 직접 만든 서버 PID만 종료한다. `OLLAMA_NO_CLOUD=1`, 고정 모델 이름, 모델 즉시 메모리 해제와 프로세스 환경 복구를 사용한다. 사용 중인 포트를 빼앗거나 기존 Ollama를 종료하지 않는다. 이 명령은 모델을 자동 다운로드하지 않는다.

```powershell
pwsh -NoProfile -File eng/local-dispatch-ai/run-local.ps1 -Mode Execute -Scenario balanced
```

이미 별도로 시작한 시험 서버에는 다음처럼 연결할 수 있다. 이 도구는 전달받은 서버를 종료하지 않는다.

```powershell
python eng/local-dispatch-ai/dispatch_advisor.py --scenario surge --execute --endpoint http://127.0.0.1:12681
```

명시적 루프백 IP/HTTP/포트만 허용한다. 외부 URL·사용자정보·경로·query·fragment·redirect·환경 proxy는 허용하지 않는다. `:cloud` 모델이나 외부 API로 자동 대체하지 않는다. 최대 대기 시간은 120초, 응답은 64KiB다.

## 전문 배차와의 역할 구분

| 구성 요소 | 적합한 역할 | 최종 업무 상태 권한 |
| --- | --- | --- |
| 서버 규칙 | 수용 인원·참여 순서·수행 여력·권한·상태 전이 검증 | 서버만 소유 |
| OR-Tools 같은 최적화 도구 | 이동시간 행렬·적재량·시간 창·픽업/전달 선후관계를 만족하는 조합 탐색 | 계산 결과를 서버가 재검증 |
| 로컬 언어 모델 | 집계의 설명·운영자 질문 응답·참고 신호 실험 | 없음 |

OR-Tools의 무료 routing solver와 유료 Google Maps Route Optimization API는 별개다. 이번에는 OR-Tools 패키지를 설치하거나 실제 지도를 호출하지 않았다. 다음 단계는 서버 규칙의 동일한 합성 시나리오와 전문 최적화 결과를 비교하는 것이다. 단순 표본 계산 통과만으로 기존 기사님의 소득·기회나 실제 수요 예측 정확도를 입증하지 않는다.

## 공식 근거

- [Ollama Windows standalone 안내](https://docs.ollama.com/windows)
- [공식 Ollama v0.40.2 릴리스](https://github.com/ollama/ollama/releases/tag/v0.40.2)
- [qwen3:1.7b 모델·양자화·크기·라이선스](https://ollama.com/library/qwen3:1.7b)
- [Qwen3 공식 소개](https://qwenlm.github.io/blog/qwen3/)
- [Ollama JSON Schema 구조화 응답](https://docs.ollama.com/capabilities/structured-outputs)
- [Ollama chat API·생각 모드·모델 메모리 해제](https://docs.ollama.com/api/chat)
- [Ollama 루프백·클라우드 비활성화 설정](https://docs.ollama.com/faq)
- [OR-Tools 차량 경로 문제](https://developers.google.com/optimization/routing/vrp)

설치·연결·표본 추론·정책 시험·운영 사용은 각각 별도 증거로 관리한다. 이 도구를 준비했다고 운영 서버의 LLM 의존성을 추가하거나 자동 배차를 활성화하지 않는다.
