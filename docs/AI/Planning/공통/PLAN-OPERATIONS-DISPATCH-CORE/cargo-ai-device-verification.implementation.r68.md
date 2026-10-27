# 화물 AI 선택과 실제 앱 API 업무 마감 r68

- 기획 ID: `PLAN-OPERATIONS-DISPATCH-CORE`, 판본 `dispatch-core.r68`, 2026-10-19.
- 상태: `Implemented / LocalAiApplied / ApiJourneyCompleted / SharedUiAcceptancePending / DeviceDeferred / WeightTrainingNotStarted / OperationalDisabled`.
- 승인 범위: 법정동 이름표를 붙인 가상 의뢰·기사를 사용해 실제 로컬 모델 선택을 기존 추천 원장과 업무 API에 연결한다. 최신 사용자 답변에 따라 **휴대폰 시험은 수행하지 않고 로컬 검증만 진행했다.**
- 선행 자료·사람 검토·학습 후보 분리는 [법정동별 화물 학습 준비 r67](cargo-regional-training.implementation.r67.md)을 따른다. 이번 세 건을 그 검토 자료의 승인 정답이나 모델 가중치 학습 결과로 추가하지 않는다.

## 1. 책임과 실행 경계

새 [검증 호스트](../../../../../eng/CargoAiDeviceVerificationR114/README.md)는 `Development / Simulation`, `http://127.0.0.1:5364`, 별도 논리 DB `mirror_ai_device_r114_20261019`를 사용한다. 기존 MySQL 13310·Mongo 27021 컨테이너에서 새 DB를 사용했으며 이전 시험 DB·의뢰·모델 시도는 보존했다. 기존 `CargoJourneyFactory`·seed·합성 지오코더/직선 경로 어댑터를 재사용했다. 기사 두 명의 운행·참여·신규 수신·위치는 실제 제품 API로 준비했다.

```text
화주의 가상 의뢰 등록 → 배차 대기 원장
├─ 법정동 자료 15개 코드의 출발/도착 이름표 검증
├─ 기존 용달운송배차업무정책의 후보 검토
│  └─ 허용 기사 두 명 → 익명 C1/C2·현재 수치 → 실제 로컬 Ollama
├─ 응답 구조·선택·이유의 입력 사실 일치 검사
│  ├─ 무효·보류·시간 초과 → 추천 미적용, 시도 기록 보존
│  └─ 유효 → 소스·대기 원장·후보 다시 확인
│     └─ 기존 대상후보조회 → 추천시작 → 추천 정본·알림 Outbox
└─ 같은 추천의 실제 앱 API 업무
   └─ 수락 → 픽업 도착 → 사진 업로드 → 상차 완료
      └─ 전달 도착 → 사진 업로드 → 인수 완료
         ├─ SQL·Mongo·화주 조회의 운송 상태 대조
         ├─ 기사 활성 업무 0개·다음 업무 대기 확인
         └─ 수금·정산·실지급은 별도 상태로 유지
```

지역 자료는 [공식 보관본에서 투영한 법정동 15개](../../../../../eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json)다. 수집일 `2026-08-11`, 원문 효력 기준일 `sourceAsOfDate=null`, 투영 SHA-256은 `737d57de3bc595aaafaba81b61044f61d76902c0bf3d2391899ac471837a973b`다. 이번 세 건의 동 이름은 고정 seed의 **시나리오 표식**이며 API의 합성 주소·좌표와 별도로 사용했다. 실제 주소의 법정동 귀속, 경계·인접성·도로 경로·GPS·ETA는 확인하지 않았다. 행정동과 혼용하지 않으며 자료의 현행성·재배포 권리 확인도 완료로 표시하지 않는다. 운임 단위는 원(KRW), 거리 표시는 합성 직선 거리이며 실측 운임·원가·지급 재원이 아니다.

운영 worker·FCM·자동 지급·배송료 선결제·음식 자동 배차는 OFF다. 제품 기본 DI·권역·추천 유효 시간·운영 배포를 바꾸지 않았다. 전용 호스트 TTL 300초와 제품 기본 TTL 60초를 구분하며, 기본 60초 안의 사용자 수락은 이번 검증 범위가 아니다. APK·Unity·영상·외부 게시·commit/push는 수행하지 않았다.

## 2. 모델 선택과 중복 적용 보호

[선택기](../../../../../eng/CargoAiDeviceVerificationR114/CargoAiSelector.cs)는 기존 `qwen3:1.7b`를 `http://127.0.0.1:12681/api/generate`로 호출한다. `/api/tags`로 실제 digest를 기록하고 JSON Schema, `temperature=0`, `think=false`, `stream=false`를 사용한다. 모델 설치·가중치 변경은 없다. 모델 입력에는 익명 후보와 현재 수치·법정동 이름표만 보내며 기사 식별자·고객 주소·연락처·자유 메모를 보내지 않는다. 대기 점수는 분 단위 대기 시간과 구분하고, 모르는 ETA를 0으로 채우지 않는다. 공식 기술 계약은 [구조화 출력](https://docs.ollama.com/capabilities/structured-outputs)과 [Generate API](https://docs.ollama.com/api/generate)를 참조한다.

이유 규약 r2는 후보별 `anyOf` Schema로 해당 후보의 입력 수치가 뒷받침하는 이유만 허용한다. `REVIEW_NEEDED`는 `hold`에서만 사용할 수 있다. 두 후보와 보류 선택은 유지하고 특정 정답은 주입하지 않았다. 기존 엄격 검증은 그대로 유지했다. 이전 Selector 원본·해시와 첫 두 실패의 시도 원장은 보존했고, 같은 의뢰를 새 프롬프트로 다시 추론하지 않았다.

[로컬 제어기](../../../../../eng/CargoAiDeviceVerificationR114/CargoJourneyControlStartupFilter.cs)는 정확한 loopback 호스트·Origin·비공개 헤더 키·Simulation DB·합성 의뢰 표식을 검사한다. `POST /verification/r114/ai-recommend`는 요청 키와 의뢰를 추론 전에 영속 기록하며 한 번에 한 호출, 전체 최대 30초다. 동일 요청의 재전송은 기존 시도와 정본을 조회하고, 같은 의뢰에 다른 키/내용을 쓰면 409로 거절한다. 추론·적용 결과가 불명확해도 자동 재시도하거나 규칙 엔진으로 대체해 AI 성공으로 표시하지 않는다.

모델 응답 뒤 원장·후보·소스가 같고 최신 대상 후보 검사도 통과해야 기존 `추천시작Async`를 호출한다. `aiRecommendationVerified`는 유효 모델 선택뿐 아니라 적용 시도 기록과 실제 추천 기사·차수의 일치까지 요구한다. 이는 휴대폰 수신·사용자 수락·운송 완료·입금 확인을 뜻하지 않는다.

## 3. 실제 실행 결과

실행 식별자는 `cargo-ai-device-r114:d6e9d0f4313d4dcab48801c17f5ab960`이다. [최종 정본 대조](../../../../../artifacts/local/cargo-ai-device-r114/final-review.json)에 세 의뢰를 별도로 보존했다.

| 가상 사례 | 실제 모델 결과 | 정본 적용 |
| --- | --- | --- |
| 01: 중화동 → 휘경동 | 7,376ms, `UnsupportedReasonFact` | 추천 0·차수 0·알림 0, 대기 유지 |
| 02: 장안동 → 면목동 | 3,225ms, `UnsupportedReasonFact` | 추천 0·차수 0·알림 0, 대기 유지 |
| 03: 회기동 → 망우동 | 이유 규약 r2, 5,104ms, `C2 / SERVER_ALLOWED` 유효 | 기사 `cargo-journey-driver-b`·추천 차수 1 적용 |

세 번째 의뢰는 `af8690ba-c82f-2ab3-8344-d6e315355043`다. 모델 선택을 실제 추천 원장에 적용한 뒤 **화면 버튼 대신 같은 제품 수락 API**로 수락하고 이후 업무 API를 끝까지 실행했다. `인수완료`가 된 운송 원장·Mongo·화주 조회가 일치하고 기사 활성 운송은 0개, 다음 행동은 대기였다. 다른 기사의 해당 운송 상세 조회는 404로 거절됐다.

의뢰 헤더는 `생성됨`, 결제는 `결제대기`, 정산은 `입금대기`다. 운송 `인수완료`를 전체 주문·수금·실지급 완료로 바꾸지 않는다. 고정된 시험 보수 6,200원도 입금 증거가 아니다. 제품 의사결정 감사 조회는 빈 배열이고, 모델 선택·적용 근거는 별도 로컬 시도 원장에 있다. 제품 감사 원장까지 기록됐다고 주장하지 않는다.

| 확인 항목 | 실제 결과와 한계 |
| --- | --- |
| 검증 호스트·읽기 probe 빌드 | 오류 0·경고 0. APK 빌드·설치가 아님 |
| 순수 응답·적용 근거 검사 | [31개 통과](../../../../../artifacts/local/cargo-ai-device-r114/selector-selfcheck-reason-r2.json). 모델 품질 점수와 별도 |
| 추천 보호 | 다른 기사 수락 409, 오래된 차수 409, 키 없는 제어 조회 401, 같은 명령 재조회, 다른 키 반복 409. [추천 보호 기록](../../../../../artifacts/local/cargo-ai-device-r114/api/r114-03-guards-recommended.json) |
| 사진 필수 보호 | [픽업](../../../../../artifacts/local/cargo-ai-device-r114/api/r114-03-guards-pickup-arrived.json)·[전달](../../../../../artifacts/local/cargo-ai-device-r114/api/r114-03-guards-dropoff-arrived.json) 각각 사진 없는 완료 400, 상태 유지 |
| 사진 업로드·완료 연결 | 68바이트 합성 PNG 두 번. 업무/파일 결속·완료 참조·저장 바이트 해시 일치. 실제 카메라·실물 인계 검증 아님 |
| 알림 | Outbox 1개, `Pending / attempts=0 / readAt=null`. 실제 전송·배너·소리·읽음 확인 아님 |
| 공통 클라이언트·ViewModel·Razor | 실제 HTTP GET을 사용한 6단계 중 추천 보수 근거 검사 실패, 수락 후 5단계 검사 통과. 실제 버튼 조작·Android 실행 아님 |
| 제품 Fast·Task 시험 | 도구 경로에 대해 라우터가 제품 시험을 생략했다. 이전 r67의 전체 시험 건수를 이번 새 실행 결과로 재사용하지 않음 |

로그인 제한 429도 관찰했다. 읽기 도구는 유효 토큰을 비공개 시험 캐시에서 재사용하도록 조정했고 서버 인증 제한은 유지했다. 제한 기간 뒤 화주·다른 기사 조회를 마무리했다.

## 4. 공통 화면에서 확인된 두 공백

첫째, 일반 화물의 직접 운임 의뢰에는 소형배송 `Pricing`이 없어 추천 카드의 보수 근거가 미확인이다. [추천 단계 결과](../../../../../artifacts/local/cargo-ai-device-r114/client/r114-03/recommended/result.json)의 `offerHasPricingEvidence=false`, `payoutScheduledKrw=null`로 남겼다. 제안 운임·고정 보수·기사 실지급액을 구분하는 계약 보완이 필요하다.

둘째, 같은 추천의 `ServerCandidateAvailable=false`로 공통 화면의 수락 가능 판정도 통과하지 못했다. [현재 ViewModel](../../../../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Cargo/CargoOfferActionViewModel.cs)의 `CanSubmit`은 차량 적합·원장 상태·만료 판정과 사용자 확인을 검사하며 **Pricing을 직접 검사하지 않는다.** 보수 미확인은 버튼 비활성의 원인이 아니다. [실제 응답의 시간대 진단](../../../../../artifacts/local/cargo-ai-device-r114/client/recommendation-timezone-diagnostic.json)에서 오프셋 없는 `2026-10-19T00:19:30.682334`를 같은 .NET 파서로 읽으면 `Unspecified`이고, 한국 시간대의 `ToUniversalTime()`이 9시간 전 `2026-10-18T15:19:30.682334Z`로 바꾼다. 당시 관찰 시점에는 서버 UTC 의미로 유효하지만 client 변환으로는 만료여서, 이 게이트만으로 차단 조건을 독립 재현했다. 당시 Source의 모든 게이트값을 결과에 남기지는 않았고 실제 확인 토글·수락 클릭도 수행하지 않았다.

[화면 기록 6장](../../../../Changes/2026-10-19-cargo-ai-device-verification-r114.md)은 실제 GET 자료를 공통 컴포넌트로 SSR한 뒤 동일 빌드 CSS·기본 테마를 적용해 브라우저에서 캡처한 것이다. 원래 SSR·테마·파생 HTML 해시는 [갤러리 manifest](../../../../../artifacts/local/cargo-ai-device-r114/client/gallery-manifest.json)에 보존했다. `http://127.0.0.1:5365/view/completed`의 검토기는 허용된 HTML/CSS만 제공한다. 실제 지도 SDK·네이티브 배너·상호작용·휴대폰 화면을 재현했다고 표시하지 않는다.

## 5. 서버 준비와 자료 보존

[준비 상태 기록](../../../../../artifacts/local/cargo-ai-device-r114/host/ready.json)은 `PreparedWithHealthWarnings`, live 200·ready 503이다. 격리 DB를 최신 모델의 `EnsureCreated`로 만들었으나 migration은 실행하지 않았다. 미적용 migration은 main 66·전통시장 2·농수산 17·공공수집 7개다. 특정 업무 API의 성공과 서버 전체 배포 준비 통과를 분리한다. 준비 검사를 완화하거나 이 실행에서 운영 migration을 적용하지 않았다.

`artifacts/local/cargo-ai-device-r114/`에 fixture·실제 HTTP·최종 대조·단계별 GET/SSR·모델 응답·소스/입력/후보 해시·이유 규약 변경 전 소스·빌드/검증 기록을 보관한다. 비공개 키·토큰·기사 토큰 대응·원시 시도는 `.private/`에서만 관리하고 공개 문서나 화면에 옮기지 않는다. `host/ready.json`은 시작 시점, `final-review.json`은 후속 실행 결과이며 시작 기록의 `modelInferencePerformed=false`를 최종 미실행으로 오인하지 않는다.

## 6. 다음 우선순위

1. **추천 정보에서 일반 화면 수락까지 연결한다.** 직접 운임 화물의 기사 보수 근거를 별도 계약으로 확인하고, 서버 후보 적합·만료 시각의 UTC/지역 시간 처리를 대조한다. 새 의뢰에서 정당한 현재 추천을 공통 화면으로 조회·확인·수락하는 경로가 통과해야 완료다. 보수 값을 임의로 채우거나 만료 검사를 건너뛰지 않는다.
2. **USB 재연결 뒤 같은 원장으로 실제 APK 흐름을 검증한다.** 추천 배너·소리·수락·픽업/전달 카메라·백그라운드/재연결을 단계별 화면과 원장 차수로 연결한다. 현재 로컬 자료를 Android 성공으로 소급하지 않고 실제 푸시와 앱 내부 조회 알림을 구분한다.

운영 전에는 별도로 migration/readiness를 맞춘 환경에서 준비 검사를 통과해야 한다. 실제 도로 ETA·법정동 귀속·수행 중 마지막 목적지 기준 추천·다건 적재 조합·실제 FCM·송금은 이번 한 건의 마감으로 검증되지 않았다. 모델의 구조·사실 검사 통과와 추천 원장 적용은 확인했지만, 최적성·공정성·운영 규모의 성능·학습 충분성은 사람의 독립 평가가 남아 있다.
