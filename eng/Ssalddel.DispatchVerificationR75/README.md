# r75 로컬 배차 검증 실행기

승인 기획·작업 명세의 해시를 먼저 확인하고 외부 비용이 발생하지 않는 합성 포트로 9개 시나리오를 실행한다. 기존 배차 정책·엔진·큐 전환·요청/알림 Outbox와 음식 기사 앱의 제품 API client/PageModel을 재사용한다. 지정기사 offer API는 사용하지 않는다.

```powershell
dotnet test Ssalddel.Tests/Ssalddel.Tests.csproj --filter FullyQualifiedName~DispatchWorkflowR75Tests
dotnet run --project eng/Ssalddel.DispatchVerificationR75/Ssalddel.DispatchVerificationR75.csproj -- --local-synthetic
```

SQLite 파일과 시나리오 결과·소스 해시는 `artifacts/local/dispatch-verification-r75-implementation/runs/<runId>/`에 새로 보존한다. 기존 r74 DB·서버·기기·클라우드는 사용하거나 변경하지 않는다. 빌드·실행은 통합 담당자가 진행한다.

- 음식 정상 세 종류는 실제 loopback HTTP로 주문·음식점 수락·기사 수락·도착·픽업·전달·별도 주문자 수령 확인까지 검증한다.
- 부적격/대기 복구·중복 알림·만료·수락 응답 유실은 같은 실제 엔진/원장의 부정·복구 경로를 검증한다.
- 일반 화물은 SQL 원본 입력 뒤 실제 견적·요금수정·후불승인 Command, 실제 큐·화물 엔진·수락/재조회 Command를 검증한다. r76에서는 보수 미확정 수락이 차단됨을 먼저 확인하고, 별도 사전 합의 보수의 명시적 시험 입력 후 수락한다. 고객 견적을 기사 지급액으로 복사하지 않으며 실제 당사자 합의·입금 증거는 아니다.
- 생활 소형 물품은 실제 생활배송 등록·원본 생성·현장지급 예정 Command로 큐를 준비하고, 명시 시험 정책(기본/최소 2,000원·직선 1km당 1,000원)으로 기사 지급액을 별도 산정·동결한다. 실제 엔진 추천과 수락 뒤 운임구성의 기사 지급액도 대조한다. 현장수금 예정은 실제 수금·입금이 아니다.
- 화물/소형의 화주·기사 HTTP 화면·사진·완료는 이번 실행기의 증거가 아니다. 시장 정책·동의·운영체제 인계·Mongo는 명시 시험 포트이며 제품 정책/실제 동의 검증으로 확대하지 않는다.
- 실제 MySQL/Mongo, 운영 JWT, GPS/경로 API·유가·날씨 관측, Ollama 추론, FCM 전송, 원생 Android 화면/음원, Azure와 실금전은 미실시다. 음식 fixture의 모든 대체 포트와 화물의 합의 보수 시험 입력은 결과 JSON에 남긴다. 기록형 알림 서비스 호출 횟수는 실제 OS 알림 중복 억제나 청취 횟수가 아니다.

실패하면 결과를 보존하고 실패 원인을 해결한 뒤 새 runId로 재실행한다. 제품 설정을 완화하거나 미정 운임을 임의의 0원/고객 청구액으로 채우지 않는다.
