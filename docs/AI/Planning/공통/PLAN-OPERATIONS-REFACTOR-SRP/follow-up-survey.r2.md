# 운영 코드 리팩토링 재조사 r2

상태: **`ProposedOnly` — 구조 조사와 후속 순서 제안**. 2026-10-20 현재 working tree를 기준으로 조사했다. 앞서 완료한 22개 항목과 CargoTrainingStore 분리는 유지한다. 이번 요청은 부족한 구조를 찾는 것이므로 제품 코드는 변경하지 않았고, 기능 수정·구현 승인으로 확대하지 않았다.

[기존 22개 결과](README.md) · [후속 후보와 순서 JSON](follow-up-work-order.r2.json) · [원자료와 영역별 감사](../../../../../artifacts/local/refactoring-survey/20261020-r2/)

## 조사 범위와 해석

- 검색 대상 소스 **8,672개**, 프로젝트 파일 **85개**, 합계 **8,757개**의 경로·줄 수·해시·소속을 집계했다. 소스는 1,343,497줄이며 시험으로 분류된 파일은 1,814개다. 빌드 산출물·vendor·비공개 artifact·중첩 worktree 등은 제외했다.
- 이 집계에는 Simulation/Unity도 포함되지만 **수동 책임 검토와 아래 후보는 운영 앱·서버·공통 코드·개발 검증 도구**에 한정했다. 모든 소스의 의미나 모든 실행 경로를 전수 검증한 결과는 아니다.
- 선언된 ProjectReference 214개에서 순환 그룹은 발견하지 못했다. 조건부 참조의 실제 MSBuild 평가나 DI·런타임 결합까지 확인한 것은 아니다.
- 이전 r1 목록 대비 추가 79개·해시 변경 69개·삭제 0개다. 이는 두 조사 시점의 차이이며 이번 턴의 제품 변경량을 뜻하지 않는다.
- 공백을 정규화한 30줄 창 비교는 후보 탐색에만 썼다. 9개 겹침 그룹 중 Kakao facade 5개는 완료된 호환 wrapper로 제외했고, 나머지 4개 중첩 창은 7개 역할 토큰 저장 구현을 직접 대조하는 단서로 사용했다. 이를 9개 결함으로 세지 않는다.
- 기존 dirty 작업은 보존했다. 새 조사 파일과 문서만 작성했으며 build/test·외부 API·DB·브라우저·APK/휴대폰 시험·모델 호출·commit/push는 실행하지 않았다.

## 결론

남은 주요 문제는 업무 기능 누락보다 **한 객체 안에서 자료 조회·계산·표시·비동기 수명이 함께 변경되는 구조**와 **역할 앱에 반복된 보호저장 처리**다. 이미 나눈 책임을 다시 분해하거나 파일 길이를 줄이는 작업은 우선하지 않는다.

다음 순서를 권한다. 1~2는 작은 범위의 동작 비교를 만들기 쉽고, 3~4는 배차와 입력 흐름의 핵심 경계를 정리한다. 5~6은 공통·표현·도구의 후속 정리다. 각 단계도 한 번에 전부 바꾸지 않고 후보별로 닫는다.

| 순서 | 묶음 | 목적 | 후보 수 |
| --- | --- | --- | ---: |
| 1 | 입고 검수 계산 | 권한·트랜잭션은 유지하고 계산/재시도 비교만 분리 | 1 |
| 2 | 7개 역할 토큰 저장 | 역할 키와 예외 차이를 보존하며 동일 JSON 처리 공유 | 1 |
| 3 | AI 배차 입력과 조합 계산 | 자료 수집·모델 조율과 순수 점수/시간 계산 분리 | 3 |
| 4 | 화주·주문자·운영자 화면 수명 | 조회·선택·명령·타이머의 소유자 명확화 | 4 |
| 5 | 공통 복구와 명세/출력 | 실제 저장 adapter와 표시 계산을 작은 책임으로 정리 | 6 |
| 6 | SDK·검토·정보·도구 | 검증 부담이 크거나 운영 핵심에서 먼 후보를 후순위 처리 | 6 |

## 21개 후속 후보

표의 번호는 다음 구현을 지정할 때 쓸 수 있는 순서다. 정확한 호출자·줄·기존 시험과 한계·보존 조건은 영역별 감사 JSON과 작업 순서 JSON에 기록했다. **기존 시험 파일을 발견한 것과 이번에 시험이 통과한 것은 다르다.**

### 1~2: 작은 추출부터 시작

| 번호 / ID | 현재 근거 | 제안하는 책임 경계 | 꼭 유지할 것 |
| --- | --- | --- | --- |
| **1 / SERVER-R2-03** | `WarehouseOperationService.cs:902,942,977` | 입고 검수 입력·동일 재시도 비교·정상/가용 수량을 순수 Policy/Plan으로 추출 | 권한·Serializable·최신 이력·tracked entity 적용·Save/commit 순서 유지. 기존 재위탁 UseCase는 다시 나누지 않음 |
| **2 / SHARED-01** | 7개 MAUI SecureTokenStore의 Load/Save/Clear | 플랫폼과 무관한 JSON 저장 core와 앱별 SecureStorage adapter 분리 | 역할 키·singleton·손상 JSON 삭제·취소/예외 순서 유지. Seller의 `auth_token` 키와 4개 앱의 명시 null 검사 차이 보존 |

1번의 기존 검수 시험은 권한·멱등·메모 차이·예약 수량·중복 감사/이벤트를 다룬다. 2번은 가짜 store를 사용하는 상위 인증 시험만으로 실제 adapter가 검증되었다고 볼 수 없으므로, 공통화 전에 7개 저장 프로필의 현재 결과를 delegate 기반으로 고정해야 한다.

독립 재대조에서도 두 경계를 확인했다. 검수의 기존 메모 formatter와 검증→인증 순서는 유지한다. 토큰의 JsonException 처리는 역직렬화만 감싸고, JSON 리터럴 `null`은 삭제하지 않는 현재 차이를 보존한다.

### 3~5: 배차 계산과 자료 수집

| 번호 / ID | 현재 근거 | 제안하는 책임 경계 | 꼭 유지할 것 |
| --- | --- | --- | --- |
| **3 / SERVER-R2-02** | `국내화물배차조율입력Factory.cs:87`, `.Evaluation.cs:36,118,193` | 이미 수집한 평가·비용·복귀·제외 근거로 점수와 이유를 만드는 순수 Composer | 적재 적합성·시간 약속·점수/가중치·미확인 상태·조회 순서. AI 판단 규칙은 바꾸지 않음 |
| **4 / SERVER-R2-01** | `FoodDispatchTimingService.cs:108,142,160` | 주문풀/현재 큐/시각에서 지역·대기·유입 통계를 만드는 SnapshotBuilder | 원주문 재조회·기한·fallback·취소 전파. 효과가 있을 수 있는 demand 평가를 무효과 Reader로 가장하지 않음 |
| **5 / SERVER-R2-05** | `운송일정삽입평가Service.cs:118`, `.Simulation.cs:25,43` | 완성된 삽입 시도 선택 → 필요 시 경로 응답별 순수 시간 누적 | 경로 API 순차 호출·횟수·조기 중단·약속·작업/대기 시간·전체 일정 끝점 유지 |

현재 AI 후보 준비/순위와 Ollama 전송은 이미 분리돼 있다. 이번 대상은 그 앞뒤의 **입력 조립과 기존 계산 책임**이며 새로운 학습·모델 호출·최적화 규칙을 추가하는 일이 아니다. 추출 전에 대표 입력의 점수·이유·선택 결과와 외부 호출 순서를 함께 고정한다.

### 6~9: 화면의 상태 수명

| 번호 / ID | 현재 근거 | 제안하는 책임 경계 | 꼭 유지할 것 |
| --- | --- | --- | --- |
| **6 / UI-R2-01** | `운송의뢰작성ViewModel.cs:155,212,251,260` | 차량추천 조회 순번·입력지문·응답·선택 출처를 ViewModel별 Session으로 추출 | 기존 최신 응답 판정·실패 후 재확인·초안 복원과 접수 고정복사의 차이. 완료된 접수 복구 Session과 별개 |
| **7 / UI-R2-03** | `SsalddelAdmin/Components/Pages/RequestDetail.razor:374,484,600,674` | 기존 observer를 쓰는 읽기/polling Session과 취소 검토 입력/명령 상태 분리 | 같은 페이지·공동 잠금·30초 주기·인증 실패와 일시 실패 차이·원장 ID/revision/명시 확인 |
| **8 / UI-R2-02** | `GroupPurchaseDemandPanel.razor:324,421,523,597` | 모집권 선택 상태와 수요 등록/철회·정본 재조회 Session 추출 | 출처키·명시 동의·401 모드·명령 성공과 후속 조회 실패를 구분. 결제/집단 확정 기능 추가 금지 |
| **9 / UI-R2-04** | `SellerApp/Components/Pages/OrdererDemand.razor:277,336,394,464` | 네 원장 조회/timer 수명과 집단수요 표시 projection 분리 | 현행 조회 순서·부분 반영·403/404·검색/정렬·개인정보 가림 |

6번에는 입력 변경·역순 응답 등을 다루는 기존 동작 시험이 있다. 7~9번의 발견 시험은 주로 소스 조립 검사이므로 fake service의 지연·오류·후속 조회·폐기 결과를 먼저 특성화한다. **새 generation guard·조회 병렬화·자동 재전송은 별도 동작 개선**이며 이번 순수 추출에 끼워 넣지 않는다. 화면/route를 늘리는 제안도 아니다.

### 10~15: 공통 저장·명세·출력

| 번호 / ID | 현재 근거 | 제안하는 책임 경계 | 꼭 유지할 것 |
| --- | --- | --- | --- |
| **10 / SERVER-R2-04** | `NeighborhoodSettlementUseCase.Weekly.cs:24,70,115` | 완료주간·중복 제거·정렬·합계의 순수 주간명세 Projector | 수요일/KST 경계·실제 완료시각·보수 null·실지급/모의지급 구분·입고 원가 배부 중복 제외. 지급 준비 Policy는 재작성하지 않음 |
| **11 / SHARED-02** | `FDriverSecurePendingOperationStore.cs:11`, `RestaurantSecureProgressPendingStore.cs:12` | 인스턴스별 직렬 실행과 단일 보호 snapshot I/O만 공유 | 원문/요청 ID/revision·owner 삭제·손상 자료/삭제 실패 전파. 업무 검증과 복구 판단은 전문 adapter에 유지 |
| **12 / SHARED-03** | `AdminAuthenticatedApiClient.cs:118,145`, `FoodDeliveryDriverApiService.cs:360,375` | 공통 Problem 메시지 탐색 primitive | detail→message→title·raw fallback·현재 non-object 차이·전문 exception·401 정책 유지 |
| **13 / UI-R2-05** | `SsalddelDocumentOutputService.cs:46,140,214,481` | 용지규격 resolver → 문서별 renderer | HTML/평문·encoding·파일명/MIME·A4/감열 alias·QR/바코드 옵션. 기존 코드 생성기 재사용 |
| **14 / UI-R2-07** | `같이수입준비관리ViewModel.cs:464,859,998` | 초안 Factory/Codec → 저장 지문과 요청키 Session | JSON 옵션/순서·같은 입력의 재시도 키·revision 동기화·공동 잠금·OS 후속 기능 OFF |
| **15 / SERVER-R2-06** | `공개배차Service.cs:84`, `NationalDispatchRequestService.cs:65` | 두 조회가 사용하는 공통 제안 표현만 순수 Projector로 추출 | 공개/전국 자격·가격 경로·상태/null 차이·주소 가림. 두 배차 정책 통합 금지 |

11번은 2번과 손상 JSON 처리 의미가 다르므로 하나의 과도한 범용 저장기로 합치지 않는다. 12번도 이미 있는 넓은 Problem parser로 일괄 치환하면 예외/공백 처리 의미가 달라질 수 있다. 13번은 대표 출력의 전체 HTML·평문을 비교하고 실제 인쇄 성공과 구분한다.

10번은 주간 경계와 최종 명세 projection부터 시작한다. 음식→화물→활동 순서에 따른 첫 중복 항목 선택, 주간 필터 이전 카운터와 서로 다른 두 Now 관측 시점은 합치지 않는다. 완료시각 선택은 다음 작은 추출로 두며 SQL 조회·후속 명령을 함께 이동하지 않는다.

### 16~21: 검증 부담과 운영 영향에 맞춘 후순위

| 번호 / ID | 현재 근거 | 제안하는 책임 경계 | 선행 조건/제한 |
| --- | --- | --- | --- |
| **16 / UI-R2-06** | `DriverNativeMapViewHandler.Android.cs:67,109,201,438,451` | Naver/Google SDK surface 자원과 위치표시 timer 수명 | 앱 간 Handler 통합은 하지 않음. callback 세대·disconnect·제공자 전환·timer 해제는 Android host 검증 필요 |
| **17 / SERVER-R2-07** | `FoodDeliveryDispatchAIReviewService.cs:53,99,407,520` | 예시 catalog/공간 보강 → 검토 표시 → 판단사례 요청 구성 | 예시·근사·실제 정보의 현행 구분과 조회/기록 분리. 실제 배차나 학습으로 자동 연결하지 않음 |
| **18 / DATA-R2-01** | `Kamis중심UsdaAms가격비교QueryService.cs:125,311,754,1013` | DB 최신 관측 선택과 가격정보 표시 Projector 분리 | 원본 통화/단위/결측/출처와 `AllowsDirectPriceDifference=false` 유지. 환산 기능 추가 금지 |
| **19 / SHARED-04** | `Ssalddel/Program.cs:662,712` | 남은 BLS·국제가격 CLI를 기존 Startup 패턴으로 이동 | 앞서 이동한 3개 CLI와 다름. 파싱 기본값 차이·분기 우선순위·Migrate→Collect·early return 고정 |
| **20 / SHARED-05** | `CommunityPageViewModelCompositionTests.cs:41,58`, `AdminMobileOperationsCompositionTests.cs:97,119` | 명시적 소스 읽기 fixture와 DI descriptor 의미 검사 분리 | 기존 route/보호 assertion을 약화하지 않음. 완료된 음식점 Assert 194개 정리는 재작업하지 않음 |
| **21 / SHARED-06** | `eng/validate-changes.ps1:153,336,584,640` | 검증 계획 생성과 명령 실행/증거 writer 분리 | 직접 자동 회귀시험을 찾지 못했으므로 PlanOnly/runner 특성화부터. 파일별 선택 정책·상한/fallback·JSON·exit code 변경 금지 |

## 분해를 권하지 않는 것

- 긴 정적 catalog·계약 모델·순수 국내운송 계획기는 크기만으로 쪼개지 않는다.
- Kakao의 두 host facade, 역할별 전문 검증 wrapper, 의도된 source-linked Compile은 유지한다. 중복 제거를 이유로 새 assembly나 역방향 프로젝트 참조를 만들지 않는다.
- 공동구매 Mongo 저장소의 응답 구성은 별도 검토 여지가 있지만 CAS mutation closure·현재 시각·기존 Engine 호출과 연결돼 있다. 같이수입 ProcessManager도 기존 정책/evidence 조율 자체가 책임일 수 있다. 둘 다 **긴 파일이라는 이유로 이번 필수 후보에 넣지 않았다**.
- 완료된 주문 조회·Outbox·재위탁·공통 지급 준비·커뮤니티 지도 Session·DI 묶음·CargoTrainingStore 분리는 재작성하지 않는다. 주문 원장의 상위 참조와 전문 원장의 상태 권위도 유지한다.

## 구조 리팩토링과 분리해 남긴 동작 개선

기존 구현 과정에서 특성화된 아래 세 경로는 새 기능/오류 처리 변경 검토이며 이번 21개 구조 후보에 숨겨 넣지 않았다. 실제 운영 장애가 발생했다는 의미도 아니다.

1. 요금 Port가 값을 바꾼 뒤 특정 예외를 던질 때의 명시 원복 범위.
2. 재위탁 SQL 성공 뒤 Mongo 예외/null 결과가 생겼을 때 replay 동기화 기준.
3. 커뮤니티 질문·게시·내 원장의 선택/계정 변경 뒤 늦은 응답 표시 보호.

## 이후 구현할 때의 완료 기준

후보 하나마다 **현재 입력/결과/오류/호출 순서 고정 → 작은 내부 책임 추출 → 기존 시험과 새 특성화 대조 → 실제 소비 프로젝트 빌드** 순서로 진행한다. 새 helper를 만든 사실이나 facade 줄 수 감소만으로 완료로 표시하지 않는다.

공개 API·JSON·route·stable ID·요청 ID/원문/revision, 보안 저장 키, DB schema·트랜잭션·저장 순서, 요금/배차/승인 정책과 worker/기능 기본값은 동작 보존 기준이다. UI 수명은 객체별·화면별 소유를 유지한다. 자동 생성 지도는 필요한 경우 공식 생성기로 갱신하고 시험이 원래 감시한 책임을 보존한다.

이번 완료 증거는 **구조 집계·직접 소스 대조·문서/JSON 정합성·조사 전후 소스 해시 확인**이다. 이전 r1의 전체 시험 결과를 이번 조사에서 다시 실행한 결과로 인용하지 않는다. 실제 SDK·APK·실기기·운영 배차·입금 검증은 후속 단계의 별도 증거다.
