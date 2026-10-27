# 로컬 화물 AI 시험실 사용 r1

2026-10-21 · `Running / BrowserModelRoundTripVerified / HumanReviewPending / WeightTrainingNotStarted`

사용자는 우선 자신의 PC에서 AI 서버를 켜두고 가상 상황을 반복 시험하려고 한다. 기존 Docker Ollama와 기존 화물 검토 호스트를 재사용한다. 별도 앱·UI·API·DB·학습기를 추가하지 않는다. [선행 연결 검토](server-ai-learning-readiness.review.r1.md)의 화물 규칙/비교 AI 구분과 지연·입력 누락 한계는 유지한다.

## 현재 준비와 실제 실행 상태

- 기존 모델: `mirror-local-dispatch-ai-ollama-1`, `qwen3:1.7b`, `127.0.0.1:12681`. 새 다운로드·모델 교체 없음.
- 기존 검토 호스트: `eng/CargoRegionalTrainingReview/bin/Debug/net10.0/CargoRegionalTrainingReview.dll`, 포트 5595.
- 새 전용 자료: `artifacts/local/local-cargo-ai-lab/review/<dataset SHA>/`. `--prepare --plan-review`로 가상 사례 **33개(학습 전 검토 25개·독립 평가 8개)**를 준비했다. 기존 검토와 경험 corpus를 덮지 않는다.
- 현재 새 자료의 모델 비교는 1건, 사용자 승인·학습 적격은 0, 실제 배차·가중치 학습은 false다.
- 처음 자동 시작 요청은 `blocked by policy`로 거절됐으며 당시에는 중지 상태였다. 이후 **사용자가 직접 서버를 시작**했고 `5595/health`와 `12681/api/version`, `api/tags` 및 loopback listener·프로세스 인자를 확인했다. 앞선 거절을 우회하거나 정책을 변경하지 않았다.
- 현재 검토 호스트와 Ollama는 실행 중이며 그대로 유지했다. 브라우저에서 실제 비교 버튼 → 서버 → 모델 → 기록 저장 → 새로고침 뒤 재표시를 확인했다. 모델 API 응답 성공과 판단 적정성은 구분한다.

초기 준비 영수증·거절 상태는 `artifacts/local/local-cargo-ai-lab/prepared-state.json`에 그대로 보존한다. 최신 실행 상태는 `artifacts/local/local-cargo-ai-lab/current-runtime.json`, 이번 실행 증거·스크린샷은 `runtime/20261021-r1/`을 따른다. [선행 검토](server-ai-learning-readiness.review.r1.md)의 세 비교와 이번 한 비교는 서로 다른 실행이다.

## 사용자 시작 후 브라우저 검증

`cargo-review-long_wait-01`의 두 가상 오토바이 후보는 즉시 수행 가능, 픽업 ETA 5분·적재 10kg/25L로 같고 대기가 C1 35분·C2 5분이다. 규칙 결과는 C1이며 사용자 답안은 미기록이다.

브라우저의 자료 구분을 ‘학습 전 검토’로 바꾸고 이 사례의 비교 버튼을 한 번 실행했다. 실제 모델은 **13,960ms**, C1과 `PLAN_VERIFIED`를 반환했으나 이 입력에는 검증된 조합 계획이 없어 `invalid / UnsupportedReasonFact`로 기록됐다. 규칙 C1은 그대로 보존됐다. 이는 실제 배차를 바꾼 뒤 되돌린 결과가 아닌 비교 결과 보존 검증이다.

모델 원응답·검증 상태·응답 시간이 화면에 표시되고, 페이지 새로고침 후에도 같은 결과가 다시 보였다. 전용 `review-events.jsonl`에 모델 사건 1개만 저장됐고 사람 검토 사건은 0개다. dataset 원문 SHA는 실행 전과 일치한다. 사용자 승인·평가 자료 비교·가중치 학습은 수행하지 않았다. 검토 화면은 열린 상태로 남겼다.

## PC에서 직접 시작하기

PowerShell에서 다음 명령을 실행한다. 저장소 루트에서 시작해야 기존 호스트가 프로젝트와 상대 경로를 찾는다.

```powershell
Set-Location C:\Users\user\source\repos\Hongdal
docker start mirror-local-dispatch-ai-ollama-1
dotnet eng/CargoRegionalTrainingReview/bin/Debug/net10.0/CargoRegionalTrainingReview.dll --port 5595 --plan-review --data-root artifacts/local/local-cargo-ai-lab/review
```

마지막 명령은 검토 서버를 실행하는 동안 해당 터미널을 사용한다. 이미 포트 5595를 다른 서비스가 쓰고 있다면 그 서비스를 임의 종료하지 않는다. 이 안내는 모델/호스트 자동 시작·Windows 서비스·스케줄러·무인 시험을 등록하지 않는다. PC 재시작 뒤에는 다시 시작해야 한다.

시작 뒤 주소는 **http://127.0.0.1:5595/admin/freight-delivery/dispatch-ai-review/training**이다. 정확한 Host 검사 때문에 `localhost`로 바꾸지 않는다.

상태는 `http://127.0.0.1:5595/health`와 `http://127.0.0.1:12681/api/tags`를 각각 확인한다. `/health` 정상만으로 모델의 응답 성공이나 배차 적정성을 확인한 것은 아니다. Codex에는 ‘로컬 시험 서버 켰으니 연결 확인해줘’라고 요청하면 된다.

## 반복 시험 순서

1. 자료 구분을 **학습 전 검토**로 선택한다. 첫 화면의 전체 목록에는 독립 평가 8개도 보인다. 평가 자료를 반복 보거나 조정에 사용했다면 미지 자료 성능 증거로 그대로 쓰지 않는다.
2. 가상 주문 사례를 고르고 마감·차량·적재·대기·기존 약속과 허용 후보/계획을 확인한다.
3. **이 사례 모델과 비교**를 누른다. 검토 서버가 Ollama를 호출한다. 브라우저가 모델 API를 직접 호출하지 않는다.
4. 규칙 결과와 모델 결과·응답 시간·검증/실패 근거를 비교한다. 20초 제한이며 실패·시간 초과·부적격 답은 모델 성공으로 세지 않고 기존 규칙 결과를 보존한다. 이 화면은 실제 추천·수락·알림을 적용하지 않는다.
5. 사용자와 판단한 뒤 **적절함 / 수정 의견 / 판단 보류**, 허용 후보 여러 명 또는 배차 보류와 메모를 정해 **검토 저장**한다. Codex의 도구 시험을 사용자의 승인으로 대신 기록하지 않는다.
6. 다른 사례로 이동해 반복한다. 재비교로 모델 실행 판본이 바뀌면 과거 의견을 새 승인으로 자동 옮기지 않고 다시 검토한다.

호스트 내부 모델 비교는 한 번에 한 건이다. 별도 CLI 비교와 UI 비교를 동시에 실행하면 이 내부 잠금을 공유하지 않아 단일 추론 큐에서 지연이 생길 수 있다. 우선 한 화면에서 하나씩 비교한다. 저장하지 않은 작성 의견은 창을 닫으면 사라지므로 저장 완료를 확인한다.

시험 결과는 전용 자료 SHA 폴더의 추가 이력에 남는다. 같은 상황 반복 관측을 서로 다른 학습 정답으로 세지 않는다. 가상 자료 생성·모델 추론·사용자 검토·승인 내보내기·가중치 학습은 별개의 단계다. 앞으로 완전한 새 사례와 경험 원장 연결은 선행 검토의 후속 제안으로 남아 있다.

## 직접 종료하기

검토 서버 터미널에서 `Ctrl+C`를 누른다. Ollama도 종료하려는 경우에만 아래 명령을 별도로 실행한다. 모델·volume·검토 자료를 삭제하지 않는다.

```powershell
docker stop --timeout 5 mirror-local-dispatch-ai-ollama-1
```
