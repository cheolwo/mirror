# 모바일 가상 퀵 운영 훈련

기존 생활 퀵 접수·기사 운행·규칙 추천·수락·수행 API를 합성 주문과 합성 기사로 사용하는 로컬 훈련 호스트입니다. 관리자는 기존 살뜰 관리자 앱에서 주문 발생과 기사 부족을 관찰하고, 필요할 때 지원 기사 참여를 직접 선택합니다. 모델 학습 도구나 실제 운영 서버가 아닙니다.

승인 범위는 [모바일 가상 퀵 운영 r58](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/virtual-quick-operations.implementation.r58.md)을 따릅니다. 실제 격리 API에서 7건의 수락·동의·사진 픽업·전달 완료를 확인했습니다. 중지·재시작으로 8슬롯을 생략했으므로 15건 무중단 완료는 아닙니다. APK 파일 준비와 휴대폰 설치·USB 통신 검증은 별도이며, 이번 작업에서 휴대폰 검증은 하지 않았습니다. 브라우저 Preview 기동은 자동 승인 검토에서 차단되어 이번 작업에서 재시도하지 않았습니다. Preview 소스·빌드를 화면 렌더 검증으로 간주하지 않습니다.

## 운영 경험을 주문별로 쌓기

[누적 구조 r1](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/operating-experience-accumulation.r1.md)에 따라 기존 가상 회차를 별도 비공개 파일 원장으로 접수합니다. 이 명령은 서버·Docker·DB·Ollama를 시작하지 않습니다. 저장소 루트에서 명시적으로 실행하며, 원래 `session.json`·완료/보관 회차·기존 사람 검토와 승인 자료는 변경하지 않습니다.

```powershell
dotnet build eng/Ssalddel.DispatchTraining/Ssalddel.DispatchTraining.csproj -c Debug
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --experience-collect
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --experience-status
```

`--experience-collect`는 `artifacts/local/dispatch-training-r58/session.json`, `completed-runs/*/*.json`, `archive/*/session.json`만 읽습니다. 매 실행의 원문을 회차·revision·SHA에 묶어 보관하며 같은 원문 재접수는 재사용하고, 같은 회차/revision의 다른 원문은 충돌로 거부합니다. 손상은 초기화하지 않고 오류로 남깁니다. 접수 실패가 하나라도 있으면 다른 접수 성공분을 보존한 채 종료 코드 1을 반환합니다. 신규 회차의 첫 기록이 없는 `Idle`은 경험 자료가 아니므로 접수 대상이 되지 않습니다.

결과는 `artifacts/local/dispatch-experiences/current-summary.md`와 같은 폴더의 `current-summary.json`입니다. `runs/`의 원문·판본과 `reviews/`의 사람 의견을 보존하고, 보고서는 최신 판본을 다시 읽어 만듭니다. 조회와 게시를 같은 원장 잠금으로 직렬화하고, Markdown은 `reports/<SHA>.json`의 불변 자료를 참조합니다. 중간 취소 때 편의용 `current-summary.json`과 Markdown이 잠시 다른 판본일 수 있으므로 연결된 불변 JSON을 기준으로 읽습니다. 예전 판본을 나중에 접수해도 현재 상태를 되돌리지 않습니다. 주문 슬롯·접수 확인·정본에서 관측한 완료를 별도로 집계하며 조회 실패는 미확인입니다. 당시 수집하지 않은 주문 조건·단계 시각·정책/모델 해시·사진·정산·입금은 만들어 채우지 않습니다.

회차 원문에는 이전 회차 사건이 남아 있을 수 있습니다. 사건의 슬롯 번호와 요청 ID가 모두 현재 주문과 일치할 때만 연결하고, 나머지는 회차 공통·미연결 기록으로 보존합니다. 이 수치는 원문별 관측 수여서 중복될 수 있으며 미해결 주문 건수가 아닙니다. 저장 파일의 내용 변조와 중간 검토 이력 결손을 검사하지만 외부에서 마지막 사건이나 최신 폴더 전체를 삭제하는 행위까지 증명하는 백업·독립 감사 원장은 아닙니다.

특정 파일이나 별도 격리 폴더를 명시할 수도 있습니다. 모든 경로는 이 저장소의 `artifacts/local/` 아래여야 하며 링크/junction과 입력·출력 폴더 겹침은 거부합니다.

```powershell
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --experience-import --session artifacts/local/dispatch-training-r58/session.json
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --experience-collect --source-root artifacts/local/dispatch-training-r58 --experience-root artifacts/local/dispatch-experiences
```

시간 될 때 Codex에 ‘배차 기록 누적해줘’, ‘미확인·예외 사례부터 함께 검토하자’라고 요청하면 됩니다. **명시적 실행마다 누적**하며 무인 감시·자동 승인·학습·실제 배차를 켜지 않습니다. 사람 의견도 아직 기존 학습용 approved export에 포함하지 않습니다. 후속 검토 adapter에서 입력 조건과 모델/정책 판본을 갖춘 뒤 별도로 연결해야 합니다.

### 사용자와 검토한 의견을 남기기

보고서의 정확한 `experienceId`, 최신 `snapshotSha256`, `reviewRevision`을 읽고 문답으로 결정한 뒤 다음 JSON을 `artifacts/local/` 아래 비공개 파일에 저장합니다. 아래는 **형식 예시**이며 값을 실제 보고서로 바꾸기 전에는 실행하지 않습니다. `requestId`는 새 의견마다 새 UUID를 사용하고, 전송 결과가 불명확할 때만 같은 내용과 ID로 재시도합니다.

```json
{
  "requestId": "<새 UUID>",
  "experienceId": "<보고서의 experienceId>",
  "snapshotSha256": "<보고서의 snapshotSha256>",
  "expectedReviewRevision": 0,
  "outcome": "defer",
  "note": "사용자와 검토한 이유. 고객 정보는 넣지 않음."
}
```

```powershell
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --experience-review --command artifacts/local/dispatch-experience-review.json --human-reviewed
```

의견은 `approve`·`correct`·`defer`이며 최신 원문 SHA와 검토 revision을 대조합니다. `--human-reviewed`는 사람이 실제로 검토했다는 명시적 실행 표지이며 시스템이 자동으로 승인했다는 뜻이 아닙니다. 같은 요청은 중복 추가하지 않고, 다른 내용의 같은 ID·오래된 판본·동시 변경은 거부합니다. 새 원문 판본이 들어오면 과거 의견은 보존하되 현재 판본의 승인으로 자동 복사하지 않습니다. `correct`의 메모도 운송 명령이나 실제 배차 수정이 아닙니다.

자체 검사는 임시 합성 fixture만 사용하고 실제 누적 원장을 수정하지 않습니다.

```powershell
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --experience-selftest
```

## 훈련 구성

| 항목 | 현재 설정 |
| --- | --- |
| 서버 | `http://127.0.0.1:5324/` |
| MySQL | `127.0.0.1:13312`, DB `ssalddel_dispatch_training`, 사용자 `dispatch_training` |
| MongoDB | `127.0.0.1:27023`, DB `ssalddel_dispatch_training` |
| 실행 경계 | 전용 Development / Simulation 호스트, 전용 Docker 네트워크·볼륨 |
| 주문 발생 | 실제 시계 30분, 120초 간격, 시작 즉시부터 28분까지 최대 15슬롯 |
| 기사 | 기본 기사 6명, 대기 중인 지원 기사 2명 |
| 훈련 사례 | 제안 거절 1회, 추천 만료 1회, 18분 이후 신규 수신 중단에 따른 기사 부족 |
| 관리자 계정 | `dispatch-training-admin` |

제품 DB·기존 검증 컨테이너·볼륨을 사용하지 않습니다. 훈련의 주소·요금·차량·사진은 합성 자료입니다. 사진 업로드 API를 사용해도 실제 촬영·상하차 증빙이 되지 않습니다. 실제 결제·정산·송금·외부 푸시·지도 호출은 하지 않습니다. 운송 업무 상태는 기존 API 정본에서 읽고, `session.json`은 접수 일정·제어 요청·관측 기록만 소유합니다.

추천 조회에 필요한 유가는 외부 Opinet 대신 훈련 전용 고정값 1,700원/L를 사용합니다. 실제 시세·주행거리·ETA 근거가 아닙니다. 기사 부족 사례의 기본 기사들은 신규 수신을 끄고 기존 업무를 마친 뒤 정상 운행 종료 API로 퇴근합니다. 이 배우 행동은 실제 운영 정책이나 배차 엔진 점수 변경이 아닙니다.

일반 `--serve` 프로필에서는 실제 기사 선택을 기존 규칙이 합니다. Ollama는 `http://127.0.0.1:12681/api/chat`의 기존 `qwen3:1.7b`로 익명 후보를 비교하는 그림자 역할입니다. 복수 후보에 한해 회차별 최대 15회, 호출당 2초를 사용하며 모델 설치·다운로드·가중치 학습은 하지 않습니다. AI 지연·오류·허용 밖 선택은 비교 불가로 기록하고 이 프로필의 실제 배차를 바꾸지 않습니다. 후보 거리·기본점수·대기점수·총점·탈락 사유는 실제 규칙 관측으로 확인하며, 미확인 수치를 추정해 채우지 않습니다. 다음의 명시적 화물 AI 선택 프로필과 구분합니다.

### 화물 AI 선택을 격리 환경에서 검증하기

`--serve-cargo-ai`는 같은 전용 Development / Simulation 호스트에서 **화물 한 건의 허용 후보 선택**에 모델을 연결하는 명시적 시험 프로필입니다. 제품의 `CargoDispatchOllama.Enabled` 기본값은 `false`, 호출 기한 기본값은 2초이며, 일반 `--serve`도 이를 유지합니다. 이 프로필만 프로세스 설정으로 `Enabled=true`, 호출 기한 5초를 적용합니다. 제품 설정 파일이나 Operational 기능을 활성화하지 않습니다.

저장소 루트의 **같은 PowerShell 세션**에서 환경을 불러온 뒤 실행합니다. `Load`만 별도 PowerShell 프로세스로 실행하면 부모 세션에는 환경변수가 남지 않으므로 아래처럼 dot sourcing합니다. 전용 DB 준비와 훈련 호스트 빌드는 이 문서의 준비와 서버 실행 절차를 사용하고, 기존 Ollama에 `qwen3:1.7b`가 준비되어 있어야 합니다.

```powershell
. ./eng/verification/dispatch-training.ps1 -Action Load
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --serve-cargo-ai
```

일반 `-Action Serve`는 계속 `--serve`를 실행합니다. 두 프로필은 5324 포트·전용 훈련 DB·`session.json`·호스트 lease를 공유하므로 동시에 실행하지 않습니다. 실행 중인 회차를 새로 초기화하지 말고 현재 관측과 제어 상태를 먼저 확인한 뒤, 기존 호스트를 `Ctrl+C`로 종료하고 선택한 프로필로 다시 실행합니다. 호스트 준비만으로 주문 발생 회차가 자동 시작되지는 않습니다. `ready.json`의 `cargoCandidateAI`가 선택 프로필 여부를 나타내며 `productionEffects=false`와는 별개입니다.

모델 접속은 프로필에서 기존 루프백 `http://127.0.0.1:12681/api/chat`, 허용 모델 `qwen3:1.7b`를 사용합니다. 차량·적재·약속·참여 조건을 통과하고 규칙 후보보다 대기 가점이 낮지 않은 후보의 익명 토큰과 확인된 수치만 전달합니다. 실제 기사 ID·주소·연락처는 모델에 보내지 않습니다. 모델은 허용 토큰 하나를 제안하며, 응답 뒤 후보·의뢰·원장 판본을 다시 확인합니다. 후보 변경·기한 초과·잘못된 응답·혼잡·호출 예산 소진은 **현재 규칙 후보**로 복귀하고, 원입력 변경으로 같은 판단의 유효성을 확인할 수 없으면 과거 선택을 반환하지 않습니다. 모델이 수락·배정·완료를 직접 저장하지는 않습니다.

공유 추론 한도는 프로세스 전체 동시 1건, 30초 구간당 최대 4회이며, 실패 뒤 30초 동안 쉬고 규칙으로 복귀합니다. 추론 대기열을 늘리지 않습니다. 이 제한은 일반 그림자 비교의 회차별 15회 제한과 다른 경계입니다.

선택 프로필은 같은 모델에 별도의 그림자 비교 요청을 중복 발송하지 않습니다. 기존 비교 필드에는 `NotRunPrimarySelectionProfile`을 기록합니다. `AiResult`·`LatestAi`·API의 `LatestAiResult`는 **그림자 비교 기록**이며 실제 AI 선택의 성공·실패나 최종 선택 기사를 나타내는 필드가 아닙니다. 원래 규칙 관찰의 후보와 기준 기사도 그대로 보존합니다. 실제 적용 출처는 같은 의뢰의 SQL 정본 `운송이벤트` 감사에 저장된 `CandidateReason`의 `CargoDispatchAI.Applied` 또는 `CargoDispatchAI.Timeout` 등 상태를 대조합니다. `NotRunPrimarySelectionProfile`을 AI 실패로, 그림자 `Applied`를 실제 추천 적용으로 표시하지 않습니다.

이는 로컬 합성 수행 시험이며 새 모델 다운로드·가중치 학습·실제 운영·휴대폰 검증이 아닙니다. 다건 조율 진입이나 음식·피킹 조합을 활성화하지 않습니다. 시험 성공 수와 실행 당시 상태는 해당 판본의 검증 보고서에서 별도로 확인합니다.

새 합성 주문은 포장 200×150×100mm·1kg·수량 1, 팔레트 미사용 0개, 회전 가능·비적층, 픽업/전달 작업시간 각 1분과 두 시간창을 명시합니다. 이는 가상 조건이며 실제 촬영·계측 자료가 아닙니다. 기존 제품의 필수정보 검사와 적재·일정 검사는 그대로 거칩니다. 훈련 DI의 경로 제공자만 한정 격자에서 직선 구간별 거리와 고정 20km/h 시간을 합산합니다. `DispatchTrainingSyntheticOnly`로 표시하며 DTO의 경로 제공 플래그는 완전한 mock 구간을 뜻합니다. 실제 도로·ETA·GPS·현장 적재 검증으로 해석하지 않습니다.

## 배달하는 동안 유한 회차를 누적하기

로컬 서버 준비 뒤 새 캠페인을 명시적으로 시작합니다. 30분에 최대 15개 가상 주문을 접수하고, 가상 기사가 기존 업무 API로 수락·픽업·전달을 수행합니다. 관측 시간은 기본 2,200초이며 무한 반복하지 않습니다.

```powershell
python eng/verification/dispatch-operating-campaign.py --start --support-on-shortage
```

`--support-on-shortage`는 가상 사용자가 부족 상황에서 지원 기사 참여를 요청하는 시험 옵션입니다. AI 자동 투입 기능이 아닙니다. 일반 `--serve`에서는 실제 화물 배차는 기존 규칙, Ollama는 최대 15회·2초 제한의 비교 전용입니다. 명시적 `--serve-cargo-ai`를 선택했으면 앞의 화물 AI 선택·재검증·현재 규칙 복귀 경계를 사용하며 그림자 비교는 생략합니다. 모델이 실패해도 가능한 현재 규칙으로 업무 처리를 이어갑니다.

캠페인은 `artifacts/local/dispatch-operating-campaign/20261021-r1/current-campaign.json`에 현재 기록 위치를 남기고, 60초마다 바뀐 session을 기존 corpus에 접수합니다. 단계별 명령 확인과 사진 영수증 유무는 별도 보조 기록이며 정확한 발생 시각을 추정하지 않습니다. 새 회차가 시작되기 전 이전 완료 원문은 기존 보존 절차를 따릅니다.

중단 후 같은 `--campaign-id <UUID>`로 실행하면 저장된 종료 시각과 명령 번호를 유지하며 먼저 재조회합니다. 불명확한 POST는 재전송하지 않습니다. 시간 종료나 정상적인 Ctrl+C 처리에서는 이 캠페인이 시작한 동일 회차에만 신규 발생 종료를 요청하고 결과를 보존합니다. 종료가 확인되지 않으면 미확인으로 남기며 현재 운송을 취소하지 않습니다. 호스트가 사라졌거나 Windows 파이프 실행에서 프로세스가 강제 종료된 경우도 새 회차로 자동 초기화하지 않습니다.

화면 없는 실행을 멈추려면 다음 명령으로 **로컬 정지 요청만** 남깁니다. 실행 중 관측기가 요청을 읽어 정리합니다. 관측기가 이미 중단됐다면 같은 ID로 재개해 마무리합니다.

```powershell
python eng/verification/dispatch-operating-campaign.py --request-stop --campaign-id <UUID>
python eng/verification/dispatch-operating-campaign.py --campaign-id <UUID>
```

최대 관측 시간 뒤에도 마지막 HTTP 조회·접수 정리 시간은 추가될 수 있습니다. 정지는 진행 중 운송 취소·서버 종료가 아닙니다.

검토할 때는 `artifacts/local/dispatch-experiences/current-representatives.md`를 엽니다. 정상 완료와 예외·미확인·모델 문제를 최대 5개 고르고 같은 내용의 반복 관측을 묶습니다. 이 묶음은 **관측 유사성**이며 완전한 입력 동일성·독립 학습 자료·사용자 승인을 뜻하지 않습니다. 새 판본을 접수해도 과거 원문과 사람 의견은 보존합니다.

```powershell
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --experience-status
python eng/verification/dispatch-operating-campaign-test.py
```

`status.json`의 관측 종료와 회차 `Completed`, 주문별 정본의 전달 완료 수는 서로 다릅니다. 사진은 시험 이미지이며 휴대폰 촬영·푸시 소리·정산·입금은 이 시험에 포함하지 않습니다. 실제 실행 결과는 [캠페인 r1](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/local-operating-campaign.r1.md)에 기록합니다.

## 준비와 서버 실행

아래 명령은 저장소 루트에서 실행합니다. .NET 10 SDK와 Docker Desktop이 필요하며 APK를 다시 만들 때는 Android MAUI workload도 필요합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verification/dispatch-training.ps1 -Action Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verification/dispatch-training.ps1 -Action Up
dotnet build eng/Ssalddel.DispatchTraining/Ssalddel.DispatchTraining.csproj -c Debug
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verification/dispatch-training.ps1 -Action Serve
```

`Prepare`는 `artifacts/local/dispatch-training-r58/environment/.env`에 전용 자격 증명을 만들며 기존 파일을 덮어쓰지 않습니다. 비밀번호·JWT·암호화 키 값을 콘솔이나 문서·검증 보고서로 출력하지 않습니다. 관리자 로그인 비밀번호가 필요하면 본인 PC의 비공개 파일에서 `DISPATCH_TRAINING_ACCOUNT_PASSWORD` 항목만 직접 확인합니다. 이 파일을 공유·커밋하지 않습니다.

`Serve`는 환경변수를 프로세스에만 적용하고, 격리 경계를 검사한 뒤 훈련 API를 엽니다. 호스트가 준비되어도 새 회차는 자동 시작하지 않습니다. 준비 상태는 `artifacts/local/dispatch-training-r58/ready.json`에 기록합니다. Docker 상태만 보려면 스크립트의 `-Action Status`를 사용합니다.

## 관리자 APK에서 보기

USB용 준비 파일은 `artifacts/local/dispatch-training-r58/android/ssalddel-admin-training-usb-20261016.apk`이며 같은 폴더의 `apk-manifest.json`에 해시·접속 주소·빌드 형태가 있습니다. 이 APK의 접속 주소는 `http://127.0.0.1:5324/`입니다. 빌드 후 관리 코드가 들어간 파일을 보존한 것이며, 설치·실제 기기 실행 완료를 뜻하지 않습니다.

다시 만들려면 저장소 루트에서 실행합니다.

```powershell
dotnet build SsalddelAdminApp/SsalddelAdminApp.csproj -f net10.0-android -c Debug -p:SsalddelServerBaseAddress=http://127.0.0.1:5324/ -p:USBFieldTest=true -p:EmbedAssembliesIntoApk=true
```

출력은 `SsalddelAdminApp/bin/Debug/net10.0-android/com.ssalddel.adminapp-Signed.apk`입니다. 새 출력이 앞의 보관 APK에 자동 반영되지는 않습니다.

휴대폰을 연결해 USB 디버깅을 승인한 뒤 다음 순서로 검증합니다. 여러 기기가 연결됐으면 `adb -s <기기 ID>`로 대상 기기를 지정합니다.

```powershell
adb reverse tcp:5324 tcp:5324
adb install -r artifacts/local/dispatch-training-r58/android/ssalddel-admin-training-usb-20261016.apk
```

살뜰 관리자 앱에서 전용 관리자 계정으로 로그인하고, **화물 운영 → 가상 퀵 운영**으로 들어갑니다. `/training/dispatch`에서 시작·일시정지·재개·신규 발생 종료·지원 참여를 선택하고, `/training/dispatch/decisions`에서 규칙 판단과 AI 비교를 봅니다. 훈련 서버 capability와 서버관리자 인증이 확인된 경우에만 진입점이 표시됩니다. 앱은 전경에서 5초마다 상태를 조회하며 복귀할 때 다시 확인합니다. 실제 푸시 수신 검증은 포함하지 않습니다.

## 휴대폰 없이 API 관측하기

`eng/verification/dispatch-training-verify.py`는 같은 관리자 계정으로 로그인해 기존 훈련 API만 조회합니다. Python 실행 파일이 PATH에 없으면 설치된 Python의 전체 경로를 사용합니다.

```powershell
python eng/verification/dispatch-training-verify.py --start --observe-seconds 2200
```

기존 일시정지 회차를 명시적으로 재개할 때는 다음 명령을 사용합니다. `--start`와 `--resume`은 함께 사용할 수 없습니다.

```powershell
python eng/verification/dispatch-training-verify.py --resume --observe-seconds 120
```

두 옵션을 생략하면 접수 상태를 바꾸지 않고 관측합니다. 관측 시간은 1~3600초입니다. `--support-on-shortage`를 명시하면 이 검증 스크립트가 테스트 사용자를 대신해 부족 상황에서 남은 지원 기사 참여를 한 번 요청합니다. 서버가 자동으로 지원 기사를 투입하는 기능이 아닙니다. 모바일에서 직접 참여를 선택하는 검증과도 구분합니다.

명령 POST는 반복하지 않고 `current`의 `AppliedCommandIds`로 처리 여부를 확인합니다. 통신이 끊겨도 `finally`에서 마지막 관측·안전한 오류 코드·`current/decisions/incidents`의 조회 가능 여부를 `artifacts/local/dispatch-training-r58/http-verification-*.json`에 보존합니다. 재조회에 실패한 마지막 관측은 최신 정본으로 표시하지 않습니다. 인증 응답·토큰·비밀번호·오류 원문은 증거에 저장하지 않습니다.

## 일시정지·종료·재시작

- **일시정지**는 신규 주문 발생만 멈춥니다. 기존 접수·추천·기사 수행은 계속 확인합니다.
- **재개**는 원래 30분 창 안의 다음 슬롯부터 이어갑니다. 지나간 슬롯은 건너뛰고 밀린 주문을 몰아서 만들지 않습니다. 일시정지 시간만큼 종료 시각을 연장하지 않습니다.
- **신규 발생 종료** 또는 30분 경과 뒤에는 `Draining`으로 전환해 기존 업무를 마감합니다. 접수 결과가 불명확한 건은 재조회하며, 해결됐다고 임의 확정하지 않습니다.
- **호스트 프로세스 종료**는 콘솔에서 `Ctrl+C`로 합니다. 재시작 시 기존 발생 중 회차는 일시정지하며 진행 업무를 재조회합니다. Docker의 `-Action Stop`은 컨테이너를 멈추는 별도 명령이며 업무 취소가 아닙니다.
- **완료 뒤 새 훈련 시작**은 기존 `Completed` 세션 원문을 `completed-runs/<runId>/<revision>.json`에 먼저 동결하고 SHA-256을 대조합니다. 보존 실패·충돌이면 새 시작을 취소하고 기존 회차를 유지합니다. 아래 CLI 보관과는 별개이며 이전 판단·문제 기록을 소급 해결하지 않습니다.

## 미확정 회차를 보존하고 새 회차 준비하기

불명확한 접수 때문에 기존 회차가 마감되지 않는 경우, 자동 초기화하지 않습니다. 먼저 훈련 호스트를 종료한 뒤 명시적으로 보존 명령을 실행합니다.

```powershell
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --archive-session
```

실행 중인 호스트가 있으면 lease·5324 listener 확인으로 거부합니다. `session.json`·`actors.json`·`ready.json` 원문을 `artifacts/local/dispatch-training-r58/archive/<UTC>-<GUID>/`에 복사하고 해시를 대조한 뒤 세션 제어 기록만 새 `Idle`로 교체합니다. DB·기존 운송 업무를 취소하거나 초기화하지 않으며, 이전 미확정 업무를 해결·완료로 표시하지 않습니다. 보존 원문과 보고서는 후속 검토 대상으로 남습니다.

## 검증의 범위

```powershell
dotnet eng/Ssalddel.DispatchTraining/bin/Debug/net10.0/Ssalddel.DispatchTraining.dll --selftest
```

자체 검증은 가짜 시계로 30분 일정을 빠르게 검사하며 실제 API·모델·DB를 실행하지 않습니다. 실제 호스트의 시간은 가속하지 않습니다. 자체 검증, 실제 격리 HTTP·DB 흐름, APK 패키징, 브라우저 화면, USB 기기 실행, 실제 운영·정산은 각각 다른 검증 단계입니다. 현재 API 통합 시험 결과와 남은 사항은 완료 기록에서 별도로 갱신합니다.

최종 자체 검증 14개와 제품 Task 12,100개가 통과했고, 환경 의존 시험 16개는 미실행입니다. 이번 회차의 Ollama 기록은 시간 초과·호출 한도 소진·단일 후보 생략으로 성공 비교를 입증하지 않습니다. [최종 결과와 한계](../../docs/Changes/2026-10-16-virtual-quick-operations-r58.md)를 확인해 주세요.
