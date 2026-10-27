# 배차 운영 경험을 주문별로 누적하는 구조 r1

2026-10-21 · `CompletedLocalValidation / SyntheticLocalOnly / WeightTrainingNotStarted`

사용자는 배달 기록과 영상이 축적되는 것처럼 화물 배차도 운영 경험을 한 건씩 쌓을 구조를 먼저 세팅하도록 요청했다. [작업 명세](operating-experience-accumulation.r1.work-order.json)가 이번 범위를 소유한다.

## 첫 범위

기존 `Ssalddel.DispatchTraining`의 가상 운영 session과 완료·보관 snapshot을 읽는 로컬 접수기와 별도 파일 corpus를 추가한다. 원문을 SHA와 회차/revision에 묶어 동결하고, 주문별 `ClientRequestId`와 실제 원장 참조가 있을 때만 그 참조를 연결한다. 기존 원장·복구 session·actors·사례 검토 원장은 수정하지 않는다.

흐름은 **기존 가상 회차 → 원문 접수·중복 대조 → 주문별 판단/관측 색인 → 명시적 사람 검토 → 다음 회차와 비교**다. 기존 회차에서 미수집한 단계별 수락/픽업/전달 시각·주문 조건·모델 digest는 새로 만들어 채우지 않는다. 관측 조회가 실패했거나 주문 연결이 없으면 결과는 미확인이다. 접수 성공·회차 종료·지급 준비를 전달 완료나 실입금으로 바꾸지 않는다.

## 기록과 책임

- 원본 접수: 회차·revision·원문 hash·도구 판본을 불변 묶음으로 보존한다. 동일 재접수는 재사용하며 같은 회차/revision의 다른 원문은 거부한다.
- 조회: 각 회차의 최신 판본과 주문별 사실, 지난 판본의 이력, 연결되지 않은 근거 및 미확인 항목을 보여 준다. 오래된 판본 재접수로 최신 상태를 되돌리지 않는다.
- 사람 검토: 정확한 경험/원문 판본과 requestId/revision에 연결한 approve/correct/defer를 별도 추가 기록한다. 새 관측에 예전 승인을 자동 전파하지 않는다.
- 기존 AI 검토: 기존 `CargoTrainingStore`는 고정 사례 dataset과 model/review 사건만 소유하므로 이번 경험 기록을 기존 review-events.jsonl이나 approved 내보내기에 삽입하지 않는다. 후속 source adapter로 연결할 대상이다.

## 사용자 사용 방식

배차 운영 후 Codex에 ‘기록 누적해줘’라고 요청하면 명시적 CLI로 알려진 로컬 훈련 기록을 접수하고 보고서를 갱신한다. 시간 될 때 중요한 사례를 보고 판정한다. 무인 감시·예약 실행·자동 승인·실제 운영 배차·모델 학습은 켜지 않는다. 새 화면이나 DB보다 기존 기록 재사용과 재시작·중복 보호를 먼저 닫는다.

## 구현과 최초 누적

[기존 훈련 도구 사용법](../../../../../eng/Ssalddel.DispatchTraining/README.md#운영-경험을-주문별로-쌓기)에 명시적 접수·조회·의견 기록 명령을 추가했다. 구현은 같은 도구의 `Experiences/` 아래 원문 보관·주문 투영·명령·보고서·자체 검사로 나뉜다. 기존 공개 route/contract와 실제 운영 원장은 변경하지 않았다.

기존 세 회차 원문을 접수하고 재실행·별도 프로세스 재시작을 확인했다.

| 항목 | 최초 누적 |
| --- | ---: |
| 가상 회차 / 원문 판본 | 3 / 3 |
| 실제 접수 시도 경험 | 11건 |
| 접수 확인 / 원문 정본 조회에서 완료 관측 | 9 / 9건 |
| 접수 결과 미확정 | 2건 |
| 전체 일정 슬롯 / 예약 / 생략 | 45 / 13 / 21개 |
| 현재 사람 검토 / 학습 적격 / 가중치 학습 | 0 / false / false |

출력은 로컬 `artifacts/local/dispatch-experiences/current-summary.md`와 판본별 불변 원문이다. 다시 접수하면 3개 원문 모두 재사용하고 경험 11건을 중복 추가하지 않는다. `approve/correct/defer` 입력 경로만 준비했으며 사용자의 실제 검토는 생성하지 않았다. 9건의 완료는 과거 가상 정본 관측이며 이번 실제 배송·새 배차·모델 판단의 우수성 검증이 아니다.

실측 첫 접수에서 현재 session에 이전 회차 사건도 보존됨을 발견했다. 같은 슬롯 번호만으로 연결하지 않고 요청 ID까지 일치하는 사건만 주문에 연결하도록 수정했다. 이전 요청·요청 ID 없는 사건은 원문과 회차 공통/미연결 집계에 보존한다. 원문별 관측 수는 중복될 수 있으며 독립 사건·미해결 주문 건수로 세지 않는다. 최초 실패와 후속 통과 기록은 모두 증거 폴더에 남겼다.

## 검증과 남은 범위

- 도구·소비 서버 의존 빌드: 오류 0개·경고 0개.
- 신규 자체 검사 27개: 원문/투영 검증·중복/재시작·오래된 판본·미확인 결과·역사 사건 분리·검토 CAS/재전송/동시 변경·게시 중 잠금·기존 승인 비승계 통과.
- CLI 실행 검증 12개: 경로 끝 구분자가 있는 입력/출력 겹침 거부, 비공개 경로·명시적 검토 표지·중복 옵션, 3개 원문 재사용·재시작·동시 게시·같은 판본 자료 연결 통과.
- 기존 훈련 자체 검사 14개 통과. 원본 snapshot 3개와 기존 review/approved 파일 164개의 SHA가 작업 전과 일치한다. 원본·기존 의견·승인 자료를 수정하지 않았다.
- scoped Fast/Task 검증은 통과했지만 현행 스크립트가 `eng/`를 guidance로 분류하여 빌드·시험을 생략했다. 이를 시험 통과로 세지 않고 위 명시적 도구 빌드와 실제 실행 검사를 별도로 수행했다.

증거: `artifacts/local/dispatch-experience-setup/20261021-r1/`의 `tool-build-final.log`, `experience-selfchecks-final.json`, `training-selfchecks.json`, `cli-checks.json`, `final-collection.json`, `repeated-collection.json`, `restart-status.json`, `final-validation.json`. scoped 검사 로그는 같은 폴더의 `scoped-fast.log`·`scoped-task.log`다.

남아 있는 파일의 내용 변조와 검토 chain 결손을 검사하지만, 외부에서 최신 snapshot 폴더 전체나 마지막 사건을 삭제하는 공격까지 탐지하는 별도 head anchor/백업은 없다. 보고서는 파생 자료이며 Markdown에 연결된 불변 JSON과 원문/검토 원장을 기준으로 읽는다.

다음 좁은 작업은 사용자가 중요한 미확인·예외 경험을 보고 판단하는 것이다. 실제 현장 수집 adapter, 입력 조건·정책/모델 판본을 갖춘 기존 학습 사례 연결, 별도 운영 백업은 후속 범위다. 서버 자동 수집·역할 앱/휴대폰·Ollama 호출·가중치 학습·운영 활성화·commit/push는 실행하지 않았다.
