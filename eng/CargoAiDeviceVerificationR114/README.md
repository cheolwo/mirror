# r114 로컬 AI 화물 추천과 업무 API 검증

## 2026-10-21 휴대폰 수동 수행용 제품 AI 프로필

기존 `--serve`와 아래 구형 선택기 경로는 그대로 유지한다. 새 `--serve --cargo-primary-ai`를 명시한 경우만 제품 `CargoDispatchOllama`를 켜고 5초 제한으로 실행한다. 기존 `start.ps1`은 이 추가 인자를 전달하지 않으므로 구형 프로필이다. 기존 비공개 환경 변수를 프로세스에 읽은 다음 아래 DLL을 직접 실행한다. 자격 증명 파일·키·토큰을 명령행이나 콘솔에 출력하지 않는다.

```powershell
dotnet build eng/CargoAiDeviceVerificationR114/CargoAiDeviceVerificationR114.csproj --no-restore
dotnet eng/CargoAiDeviceVerificationR114/bin/Debug/net10.0/CargoAiDeviceVerificationR114.dll --primary-profile-self-check
dotnet eng/CargoAiDeviceVerificationR114/bin/Debug/net10.0/CargoAiDeviceVerificationR114.dll --serve --cargo-primary-ai
```

기존 DB·계정·runStableId·의뢰·완료/실패 자료는 보존하며 새 주문과 새 ClientRequestId로만 검증한다. `ready.json`은 시작 전에 이전 판본을 보관하고, 새 자료에서 `cargoPrimaryAI=true`, `modelSelectionPath=ProductCargoCandidatePolicy`, `hostedServicesRemoved=true`를 확인한다. 자동 수락·합성 사진·배송 완료 actor는 등록하지 않는다. 사용하지 않는 훈련 Seed/Settings 형식은 경로 어댑터의 Compile Link 의존성일 뿐 실행하지 않는다.

새 프로필도 기존 `POST /verification/r114/ai-recommend`의 본문·보호 헤더·시험 표식을 사용한다. 제품의 `계획배차에서추천으로전환Async` 또는 `추천대기처리Async` → 다음 후보 정책 → 익명 토큰 AI 선택 → 최신 원장·후보 재검사 → 추천/알림 Outbox를 거친다. 특정 기사를 지정하는 `추천시작Async`는 새 프로필에서 직접 호출하지 않는다. 시간 초과·잘못된 응답·통신 오류는 현재 제품의 규칙 복귀 결과와 구분해 기록한다. 한 후보만 허용되는 경우 `SingleCandidate`이며 실제 모델 호출 증거가 아니다.

기존 10월 19일 전용 DB에는 현행 화물 모델의 nullable 열이 없을 수 있다. 새 프로필만 `R114NullableSchema`의 고정 허용 목록(10월 19일 migration 4개에 명시된 nullable 열)을 대조해 **빠진 열만 NULL 허용으로 추가**한다. 기존 열의 형식 변경·행 수정/삭제·금액/상태 초기화·migration 이력 작성·전체 migration 실행은 하지 않는다. 대상 테이블별 전후 행수가 같아야 하고 추가 목록·행수·출처 migration을 `host/.private/schema-additions/<SHA-256>.json`에 불변 보관한다. 첫 실행에서는 10개 열이 추가됐고 기존 행수가 유지됐다. 기존 `EnsureCreated` 기반 DB의 migration 이력 공백 때문에 persistence health가 계속 `Unhealthy`일 수 있으며, 이를 정상 운영 준비나 모든 schema 호환 확인으로 표시하지 않는다. 실제 기기 시험은 현재 API 응답과 원장을 별도로 검증한다.

명령은 별도 `.primary.json`과 불변 사건 파일에 먼저 보존한다. 같은 명령 재요청은 기존 결과와 정본 조회만 반환하며 모델·추천·라운드를 반복하지 않는다. 같은 의뢰에 다른 키를 주면 충돌하고, 기존 추천/수행이 있는 의뢰를 신규 AI 검증으로 바꾸지 않는다. `/proof`의 `actualCargoAIStatus`, `actualCargoAI`는 **실제 배차엔진 감사**에서 읽는다. `AiSelectionApplied=true`는 `CargoDispatchAI.Applied`와 저장된 추천·차수가 함께 확인된 경우이며, `FallbackUsed=true`는 규칙 복귀로 저장된 추천이다. 그림자 비교나 구형 selector의 결과를 실제 선택으로 표시하지 않는다.

휴대폰 첫 사례에서는 전용 DB의 `차량제원`이 0행이어서 제품의 적재 검사가 후보를 허용하지 않았다. 공개 단계로 전환된 원장·추천 차수 0·첫 시도의 원문은 보존한다. 새 프로필은 기존 `IdentityDataSeeder`의 1톤 카고 수치를 합성 fixture 1행으로만 추가하고, 시험 메모와 원본 소스 SHA를 `host/.private/vehicle-fixture/`에 남긴다. 기존 같은 코드/별칭이 있으면 덮지 않고 명시 fixture와 다를 때 시작을 보류한다. 기사 실차·장비를 승인하거나 현실 측정값으로 표시하지 않는다. 새로운 시도부터 `추천시작됨`·기사 ID·현재 추천 차수와 대상의 일치가 있어야 `Applied=true`를 기록하고, 다른 단계 전환은 `TransitionProcessed`로 구분한다. `/proof`의 `currentCandidateObservation`은 모델을 호출하지 않는 현재 후보 검토 결과다. 안전한 코드·점수·합성 기사 ID만 반환하고 불변 관측에 저장하며, 과거 선택 순간의 근거나 새 추천 적용으로 해석하지 않는다.

새 프로필의 HTTP 추가 허용은 정확한 `POST http://127.0.0.1:12681/api/chat`뿐이다. 모델 다운로드·다른 모델 API·원격 주소·프록시·리다이렉트는 허용하지 않는다. 별도 훈련 도구의 `DispatchTrainingFixtureRoutes`를 컴파일 링크해 같은 합성 구간 합산/시간 추정을 재사용한다. 제품 DTO의 complete route 플래그는 이 시험의 mock 값으로만 제공하며 **현실 도로·ETA·GPS 검증이 아니다**. 합성 격자는 위도 `37.58~37.59`, 경도 `127.08~127.09` 안이다. 기존 첫 시나리오의 `37.588,127.085 → 37.590,127.087`은 범위 안이고, 다른 기존 시나리오를 범위 안으로 임의 이동하거나 과거 데이터를 수정하지 않는다.

수락·기사 공개 동의·도착·카메라 촬영과 전송·픽업/전달 완료는 휴대폰의 기존 화면과 정상 API로 진행하고 단계별 스크린샷·동일 의뢰/운송 ID·정본 상태를 대조한다. 이 도구의 `deviceVerified=false`는 서버 proof만으로 단말 성공을 인증하지 않는다는 뜻이다. 실제 단말 증거는 별도 검증 기록에 연결한다. 외부 FCM·실물 인도·수령·지급은 이 시험의 완료 상태와 합치지 않는다.

## 기존 r114 기본 프로필과 로컬 검증 기록

격리된 Development/Simulation 호스트에서 가상 의뢰의 실제 허용 후보를 Ollama가 선택하고, 기존 제품 추천·수락·사진 픽업·전달 API로 이어지는지를 확인하는 도구다. 모델 가중치 학습·실제 고객 업무·운영 자동 배차가 아니다. 기존 r114 기본 검증 범위는 **휴대폰 없이 로컬 서버와 앱과 같은 API까지**다. 서버 proof는 계속 `deviceVerified=false`를 유지하고, 2026-10-21 이후의 별도 단말 검증은 위 새 프로필 절차와 기록을 따른다.

## 범위와 재사용

- 호스트는 `127.0.0.1:5364`, DB는 `mirror_ai_device_r114_20261019`다. MySQL 13310과 Mongo 27021의 기존 로컬 컨테이너를 사용하되 r67 DB 이름과 섞지 않는다.
- 기존 `CargoJourneyFactory`·`CargoJourneySeed`·합성 경로 어댑터를 연결해 사용한다. r67 소스·설정·기존 자료는 수정하지 않는다.
- 제품의 worker·외부 푸시·결제·지급·운영 지역 판정·기본 DI는 바꾸지 않는다. 루프가 자동으로 여러 주문을 생성하거나 무인 배차하지 않는다.
- `cargo-journey-driver`, `cargo-journey-driver-b`의 전용 합성 계정만 모델 후보로 허용한다. 이름·주소·실제 기사 ID·접속 키·토큰은 모델에 보내지 않는다.
- [공식 법정동 투영](../local-dispatch-ai/cargo-regional-training/legal-dong-regions.json)의 고정 SHA-256과 15개 코드를 확인한다. 수집일은 2026-08-11, 원문 효력 기준일은 미확인이다. 이 코드는 **시나리오 이름표**이며 합성 좌표가 해당 법정동 안에 있다는 뜻이 아니다. 경계·인접·실경로를 확인한 것으로 표시하지 않는다.

## 시작과 입력 준비

`start.ps1`은 기존 비공개 환경 파일에서 필요한 값을 읽어 전용 호스트를 시작한다. 키·암호·토큰 값을 터미널이나 문서에 출력하지 않는다. 실제 실행·빌드 결과는 상위 검증 기록에서 확인한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/CargoAiDeviceVerificationR114/start.ps1
```

화주는 기존 정상 인증·견적/등록·필요한 후불 승인 API로 의뢰를 만든다. 상품/물품 이름에는 `R114`를 포함한다. 의뢰의 `요청사항` 또는 `화물설명`, 그리고 전달된 운송 원장 메모에서 다음 표식을 판독한다.

```text
R114_AI_LEGAL_DONG/pickup=1126010100;dropoff=1126010200
```

두 법정동 코드는 공식 투영 15개 중 하나여야 한다. 표식이 없거나 서로 다른 출발/도착 표식이 충돌하면 이 시험에 사용하지 않는다. API 주소와 위치는 기존 합성 어댑터가 허용하는 시험 값이며 법정동 이름에서 실주소·좌표를 생성하지 않는다.

기사는 정상 업무 API로 운행·희망 업무·위치·신규 수신 상태를 준비한다. 서버의 현재 수신·차량·적재·기존 수행·시간 조건 검사를 통과한 후보만 비교한다. 이 도구가 해당 상태를 직접 완료로 덮어쓰거나 탈락 기사를 복원하지 않는다.

## 로컬 전용 제어 API

두 제어 API는 loopback 원격 주소와 정확한 호스트, 요청 Origin을 검사한다. 비공개 `x-r114-key` 헤더가 필요하다. 헤더 값은 `CargoJourneySettings.AccessKey`의 기존 비공개 환경 설정에서 읽어 사용하며 소스에 기록하지 않는다.

| 동작 | 요청 |
| --- | --- |
| 가상 의뢰의 현재 근거 확인 | `GET /verification/r114/proof?requestId=<의뢰 GUID>` |
| 한 번의 AI 후보 선택과 추천 적용 | `POST /verification/r114/ai-recommend` |

POST 본문은 다음 두 필드만 허용한다.

```json
{
  "requestId": "해당 의뢰의 GUID",
  "requestIdempotencyKey": "이번 명령의 새 GUID"
}
```

한 의뢰의 요청 ID와 본문을 먼저 영속 기록한다. 같은 의뢰를 재요청하면 동일 명령의 기존 시도·현재 추천 정본을 조회하며 **모델과 추천 적용을 다시 실행하지 않는다**. 다른 본문 또는 다른 멱등 키로 같은 의뢰를 다시 시도하면 충돌이다. 추론·저장 응답이 불명확하다고 새 키를 만들어 재전송하지 않는다. 새 독립 사례가 필요하면 기존 사례를 보존한 채 정상 API로 새 의뢰를 만든다.

## 실제 모델 선택과 적용

```text
현재 의뢰·운송 원장과 시험 표식 확인
→ 용달운송배차업무정책.후보검토Async
→ 허용 후보를 C1/C2 등의 익명 토큰으로 변환
→ 실제 로컬 Ollama의 구조화 선택
→ 응답 구조·허용 토큰·이유와 실제 입력 수치 대조
→ 원장/입력·후보·소스 판본 재검사
→ 대상후보조회Async로 선택 기사 적격성 재확인
→ 기존 추천시작Async(의뢰, 선택 기사, 시험 TTL 300초)
→ 추천 원장과 알림 요청 Outbox를 재조회
```

모델은 기존 `http://127.0.0.1:12681/`의 `qwen3:1.7b`만 사용한다. `/api/tags`의 digest를 기록하고 `/api/generate`에 JSON Schema, `temperature=0`, `think=false`, `stream=false`를 사용한다. 자동 설치·다운로드·원격 대체 모델이 없으며 실제 비교는 한 번에 하나, 요청 전체 최대 30초다. 기술 근거는 [Ollama 구조화 출력](https://docs.ollama.com/capabilities/structured-outputs)과 [Generate API](https://docs.ollama.com/api/generate)다.

이유 규약 r2는 후보별 `anyOf` 구조를 사용한다. 후보 선택에는 그 후보의 수치로 성립하는 이유만 허용하고, `REVIEW_NEEDED`는 `hold`에서만 허용한다. 서버 허용·대기 점수·거리·수행 종료·단일 후보 이유의 조건을 프롬프트에도 각각 명시한다. 모든 후보와 보류 선택은 유지하며 정답 후보를 강제하지 않는다. 프롬프트 해시에는 이 생성 구조도 포함한다. 독립적인 엄격한 응답 검사는 그대로 유지하고, 이전 시도의 거절·원응답·소스 사본은 보존한다. 새 규약을 시험하기 위해 이미 시도한 의뢰를 재실행하지 않는다.

입력에는 현재 정책의 허용 후보 수치만 들어간다. 거리·기본/대기/총점과 확인된 수행 종료·픽업 도착 예상이 없으면 `null`로 둔다. 대기 점수를 실제 대기 분으로 바꾸지 않는다. 동등한 수행 조건에서는 오래 기다린 방향을 고려하도록 요청하되 정답 후보나 기존 엔진 선택을 모델 입력에 넣지 않는다.

후보 밖 선택, 근거 없는 이유, 시간 초과, 자료/원장 변경, 최신 적격성 실패이면 추천하지 않는다. 기존 규칙으로 돌아가 추천한 뒤 AI 적용 성공으로 보이게 만드는 대체 처리는 없다. 모델의 보류도 실제 추천 성공과 구분한다. 300초는 로컬 촬영·검증용 TTL이며 제품 기본 추천 수락 시간의 검증이 아니다.

## 업무 마감 확인

추천 적용 뒤에는 앱과 같은 정상 API를 사용한다.

1. `GET /api/v1/driver/recommendations`로 제안과 추천 차수를 확인한다.
2. `POST /api/v1/driver/dispatch-actions/{requestId}/accept`로 같은 차수의 제안을 수락한다.
3. `GET /api/v1/driver/transports/{transportId}`의 현재 상태와 다음 허용 동작을 확인한다.
4. `arrive-pickup` → 정상 사진 업로드 → `pickup-complete`를 수행한다.
5. `arrive-dropoff` → 정상 사진 업로드 → `complete`를 수행한다.
6. 제어 API의 `proof`로 추천·수락·완료·알림 Outbox·사진 영수증과 저장 바이트·Mongo 상태를 대조한다.

로컬 API 시험의 합성 사진은 실제 카메라 촬영이나 물품 인도 증거가 아니다. 휴대폰을 다시 연결했을 때 별도 허용 범위에서 APK 배너·소리·수락·실제 카메라·화면 전환을 확인한다. 실제 FCM과 전경 조회/기기 로컬 알림도 구분한다. 이 호스트의 worker·FCM 설정은 비활성 상태를 보존한다.

## 보존 자료와 한계

`CargoJourneySettings.ArtifactRoot/.private/r114-ai/` 아래에 의뢰별 시도 원장, 각 단계의 불변 사건, 모델 입력·출력/digest, 원장/후보/정책 해시, 후속 proof를 보관한다. 키·암호·토큰·고객 연락처/상세 주소를 저장하지 않는다. 실제 후보의 합성 ID 대응은 비공개 자료에서만 보존하며 모델 입력에는 들어가지 않는다. 사진 proof는 URL·원본 사진을 응답에 포함하지 않고 업로드 영수증의 업무 결속·해시/크기·실제 저장 바이트 일치를 확인한다.

`aiSelectionMatchesCanonical`과 `aiRecommendationVerified`는 해당 시도의 유효 모델 선택과 실제 추천 차수/기사의 연결만 뜻한다. 휴대폰 표시·음성 청취·촬영·고객 수령·지급 완료를 인증하지 않는다. 입력 자료·실제 모델 실행·원장 적용·API 마감·실제 단말·현장 운행·실제 수금/송금은 각각 별도 결과다. 모델이 실행되거나 유효한 JSON을 반환했다는 사실만으로 배차 품질·최적성·학습 완료를 주장하지 않는다.
