# 모바일 가상 퀵 운영 훈련

기존 생활 퀵 접수·기사 운행·규칙 추천·수락·수행 API를 합성 주문과 합성 기사로 사용하는 로컬 훈련 호스트입니다. 관리자는 기존 살뜰 관리자 앱에서 주문 발생과 기사 부족을 관찰하고, 필요할 때 지원 기사 참여를 직접 선택합니다. 모델 학습 도구나 실제 운영 서버가 아닙니다.

승인 범위는 [모바일 가상 퀵 운영 r58](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/virtual-quick-operations.implementation.r58.md)을 따릅니다. 실제 격리 API에서 7건의 수락·동의·사진 픽업·전달 완료를 확인했습니다. 중지·재시작으로 8슬롯을 생략했으므로 15건 무중단 완료는 아닙니다. APK 파일 준비와 휴대폰 설치·USB 통신 검증은 별도이며, 이번 작업에서 휴대폰 검증은 하지 않았습니다. 브라우저 Preview 기동은 자동 승인 검토에서 차단되어 이번 작업에서 재시도하지 않았습니다. Preview 소스·빌드를 화면 렌더 검증으로 간주하지 않습니다.

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

실제 기사 선택은 기존 규칙이 합니다. Ollama는 `http://127.0.0.1:12681/api/chat`의 기존 `qwen3:1.7b`로 익명 후보를 비교하는 그림자 역할입니다. 복수 후보에 한해 회차별 최대 15회, 호출당 2초를 사용하며 모델 설치·다운로드·가중치 학습은 하지 않습니다. AI 지연·오류·허용 밖 선택은 비교 불가로 기록하고 실제 배차를 바꾸지 않습니다. 후보 거리·기본점수·대기점수·총점·탈락 사유는 실제 규칙 관측으로 확인하며, 미확인 수치를 추정해 채우지 않습니다.

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
