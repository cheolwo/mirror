# 주문·배차·기사 앱 로컬 통합 시나리오

[r49 승인 범위](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-scenarios.implementation.r49.md)에서 같은 권역·인접권·현재 수요의 3개 대표 흐름을 반복 검증한다. 합성 주문과 격리 SQLite, 루프백 HTTP의 주문·기사 Controller, 실제 FDriver API client·PageModel·알림 수신기를 사용한다. 실제 요금 정책은 Simulation에서 합성 경로·기상 입력으로 실행하며, 외부 푸시·지도·위치 장치 등 대체한 경계는 사례별 근거에 남긴다. 외부 운영 서버·고객 자료·FCM 발송·기기 설치는 사용하지 않는다.

저장소 루트에서 실행한다.

```powershell
dotnet run --project eng/local-dispatch-ai/DispatchWorkflowRunner
dotnet run --project eng/local-dispatch-ai/DispatchWorkflowRunner -- --mock
dotnet run --project eng/local-dispatch-ai/DispatchWorkflowRunner -- --actual
```

인자가 없으면 추론·업무 실행·산출물 저장을 하지 않는다. `--mock`은 HTTP 모의 모델 응답으로 실제 업무 연결을 시험하며 실제 Ollama 호출은 0회다. `--actual`은 같은 소스 해시의 세 모의 흐름이 모두 통과한 근거를 먼저 검사하고, 이미 실행 중인 `127.0.0.1:12681`의 `qwen3:1.7b`만 호출한다. 모델·서버를 설치하거나 시작하지 않는다.

대표 사례 중 유효 비교 후보가 두 명 이상인 같은 권역·인접권에서만 각 한 번 호출한다. 현재 수요에서 최우선 후보 한 명만 허용되면 `SingleCandidate`로 모델을 생략한다. AI를 호출하기 위해 수요 순위를 조작하지 않는다. r49의 누적 시작 시도 상한은 실패·중단을 포함해 4회이며, 호출 직전 파일을 독점 예약한다. 이전 r46~r48 예산은 사용하지 않고 잔여 예산을 채우기 위한 호출도 하지 않는다.

근거는 Git 제외 `artifacts/local/local-dispatch-ai/workflow-scenarios-r49/`에 새 실행별로 보존한다. 단계·서버 조회·알림 게시 요청·모델 입력/응답과 해시·선택한 소스 파일의 해시를 기록한다. 저장소 전체 의존성의 동일성을 인증하는 검사는 아니다. 실패도 삭제하지 않는다. 실제 FCM 전달, Android 배너·소리·진동·탭, 화면 픽셀과 GPS·보호 저장은 이 시험의 통과로 인증하지 않는다. Outbox 처리는 시험이 명시적으로 실행하므로 제품 호스트의 자동 이벤트·백그라운드 작업 예약 검증과도 구분한다.
