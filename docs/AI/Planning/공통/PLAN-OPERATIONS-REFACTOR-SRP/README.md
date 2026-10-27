# 운영 앱과 서버의 단일 책임 리팩토링

상태: `CompletedLocalValidation`. 2026-10-20 사용자가 전수 구조 조사에서 제시한 후보를 순서대로 모두 정리하도록 명시적으로 승인했다. 구현 범위와 보존 조건은 [작업 명세](work-order.json)의 22개 항목이며 이 문서는 진행 상태와 단계별 검증 결과를 소유한다.

완료 후 같은 날 다시 요청한 구조 조사는 [후속 조사 r2](follow-up-survey.r2.md)와 [21개 후보 순서](follow-up-work-order.r2.json)에 별도로 기록했다. r2는 `ProposedOnly`이며, 이 문서의 완료 상태나 기존 22개 구현 범위를 새 구현 승인으로 확대하지 않는다.

그 뒤 사용자가 계속 진행을 요청하여 [r2 구현 현황](implementation.r2.md)과 [별도 구현 작업 명세](implementation-work-order.r2.json)의 21개 첫 책임 경계를 구현했다. 최종 Task 13967개 통과·실패 0개·건너뜀 16개로 로컬 검증을 완료했으며 위 조사 판본은 당시의 제안 이력으로 보존한다.

2026-10-21 사용자가 앱·서버를 다시 살펴보고 기능 변경 없는 다음 우선순위를 요청했다. [후속 조사 r3](follow-up-survey.r3.md) · [14개 후보 명세](follow-up-work-order.r3.json)는 `ProposedOnly`다. r1/r2 완료 상태는 유지하며 이번에는 구현·시험 실행·운영 활성화를 하지 않는다.

그 뒤 사용자 “계속진행”에 따라 r3의 **1번 첨부파일·2번 푸시 설치 HTTP**를 구현했다. [r3 구현·검증](implementation.r3.md) · [범위 명세](implementation-work-order.r3.json)는 `CompletedLocalValidation`이며, 나머지 12개 조사 후보와 실제 기기/외부 운영·commit/push는 포함하지 않는다.

## 범위와 완료 기준

기존 업무 동작을 유지하며 변경 이유·상태 수명·실패 및 재시도 경계에 맞춰 책임을 추출한다. 파일 길이 또는 partial 파일 수 자체는 문제나 완료 기준이 아니다. public facade, route, JSON, stable ID, 오류 코드, 요청 ID·원문·revision, 음식/화물/창고의 전문 권위와 기존 기능/worker 기본값을 유지한다.

DB 스키마, 요금·배차·승인 정책, 외부 실행, 실제 수납/지급, 모델 가중치, 영상·Unity/Simulation 개발은 이번 범위가 아니다. 화면 내부 상태 추출과 페이지 목적 변경은 다른 작업으로 구분한다. 최초 추출에서는 시각과 행동을 유지하고 별도 목적의 진입/복귀가 필요한 경우 기존 페이지 책임 기준에 맞춰 검증한다.

## 구현 순서

| 단계 | 항목 | 상태 |
| --- | --- | --- |
| 0 | 기존 입력·결과·요청/판본·오류·재시도 계약 고정 | 단계별 검증에 포함 |
| 1 | SHIP-01 앱/웹 접수 복구, SHIP-02 서버 입력/구성 | 로컬 검증 완료 |
| 2 | AI-01 후보 준비/평가, AI-02 모델 전송 | 로컬 검증 완료 |
| 3 | QUEUE-01 추천 확정, QUEUE-02 발송/비용, QUEUE-03 원천/인덱스 | 로컬 검증 완료 |
| 4 | ROLE-01 기사 수명, ROLE-02 음식점 진행, ROLE-03 분산이행 UI | 로컬 검증 완료 |
| 5 | LEDGER-01 조회/관계, LEDGER-02 Outbox, WAREHOUSE-01 재위탁, WAREHOUSE-02 명령 | 로컬 검증 완료 |
| 6 | PAY-01 공통 지급 판정, ADMIN-01 추적/모의 지급, ADMIN-02 요금/리뷰 | 로컬 검증 완료 |
| 7 | COMMUNITY-01 지도/작성, COMMUNITY-02 전문 상태 표현 | 로컬 검증 완료 |
| 8 | COMPOSE-01 DI, COMPOSE-02 유지보수 CLI, DUP-01 외부 API 중복 | 로컬 검증 완료 |

각 단계는 동작 비교 → 좁은 추출 → 관련 회귀 검사 → 소비 클라이언트 확인 순서로 닫는다. 트랜잭션·저장·후속 처리 순서가 바뀌는 변경을 단순 구조 이동으로 숨기지 않는다. 실기기·운영·실금전 검증은 코드/격리 시험 증거와 구분한다.

## 작업 소유와 증거

기존 dirty 작업을 보존하고 편집 직전 파일을 다시 읽는다. 공통 파일과 테스트 프로젝트 조립은 소유자를 조율하며, build/test는 병렬 소스 수정이 멈춘 경계에서 중앙 실행한다. 사용자가 commit/push를 요청하지 않았으므로 stage·commit·push하지 않는다.

현재 기준선과 진행 원장은 `artifacts/local/refactoring-implementation/20261020-r1/`에 있다. 이전 조사와 직전 사례 검토/승인 대기 자료는 보존한다. 새 코드·DLL 판본의 승인 자료를 과거 판본에서 자동 승계하지 않는다.

## 1단계 결과

앱·웹의 결과 조회와 계정 수명을 `ShipperSubmissionResultSession`으로 추출했다. 보호 원문·동의·명시적 재시도는 앱, 브라우저 잠금·조회 전용 복구는 웹에 유지했다. 세션 비교값은 private 메모리에 두고 토큰을 문자열/JSON으로 내보내지 않는다.

서버 생성 입력·의뢰/운임 구성·수정 비교·정산상태를 순수 helper로 옮겼다. 원장→SQL 투영→운임 저장→인계의 본문과 저장 순서를 대조했다. `Fast`의 전체 소비 솔루션 빌드와 관련 **167/167 시험**이 통과했으며 원문 비교·검증 중 소스 해시 유지도 확인했다. 증거: `artifacts/local/validation/20261020-164011/` 및 `wave-01-*` 자료. 실제 기기·실DB 업무 완주 증거로 확대하지 않는다.

## 2단계 결과

후보 준비·경로/도착 평가·세 정렬 분기·검토 근거를 독립 책임으로 나누고 정책 facade와 AI 후 현재 후보 재검토를 유지했다. Ollama HTTP 응답의 64KiB 제한 읽기와 envelope 검사를 공통화하되 요청·기한·후보 본문·fallback은 기존 어댑터에 남겼다. 서버/시험 소비 빌드와 관련 **308/308 시험**이 통과했다. 증거: artifacts/local/validation/20261020-165431/ 및 wave-02-* 자료. 실제 모델 호출·운영 적용·학습 완료를 뜻하지 않는다.


## 3단계 결과

추천 시도별 요금 snapshot·준비·동결·명시 원복, Hub→push→log 발송, 연료비/복귀지 조회, 원천 메타데이터 병합과 음식/화물 실행 인덱스를 독립 책임으로 추출했다. 현재 호출자 저장 경계와 예외 전파를 유지했다. 관련 **202/202 시험**과 서버/소비 빌드가 통과했다. 증거: artifacts/local/validation/20261020-170700/ 및 wave-03-* 자료.

후속 동작 변경 후보: 요금 Port가 값을 변경한 뒤 ArgumentException/AgreedRateTableException을 던지는 기존 경로는 명시 원복이 없다. 감사 저장이 있는 합성 경로에서 이를 특성화했으며 이번에는 자동 원복이나 거래 순서를 추가하지 않았다. 운영 장애 발생을 확인한 것은 아니다.
## 4단계 결과

기사 화면의 갱신·영속 보류·위치 전송 수명, 음식점의 수신함·진행 요청·알림, 분산이행 화면의 명령 영수증·사진 준비·역할 행동 검증을 독립 내부 객체로 나눴다. 공개 화면·문구·API·복구 JSON과 자동 POST 금지, 계정/세대 경계, 사진 업로드와 업무 완료의 분리를 유지했다. 음식점 architecture 검사는 기존 Assert 194개를 보존하고 실제 책임 파일을 함께 읽는다.

자동 생성 코드 지도는 공식 생성기로 갱신했다. 전체 소비 `Ssalddel.v3.5.slnx` 빌드, 코드 지도 검사, 관련 **1,332/1,332 시험**이 통과했다. 증거: `artifacts/local/validation/20261020-171921/`, `wave-04-*`, `role02-*`. 기존 Android/nullable 등 빌드 경고는 남아 있으며, 실제 화면·휴대폰·운영 통신 검증을 추가로 수행한 것은 아니다.
## 5단계 결과

주문 조회/전문 상태·서명 투영은 조회 Service로 옮기고 관계 변경과 CAS 저장 본문은 유지했다. 음식/창고/합의취소/기사후속 Outbox의 실제 payload 실행은 4개 Handler로, 창고 재위탁은 별도 실행 객체로, 분산 입고·합의·피킹·전달/반환·완료근거 기록은 typed 내부 실행 객체로 분리했다. 분산 영수증 실행기·Serializable 경계와 준비/사진 검증은 기존 facade에 남겼다.

서버/소비 빌드·코드 지도·관련 **578/578 시험**이 통과했다. SQLite 실제 INSERT 실패/전체 롤백/Mongo 미호출과 SQL 성공 뒤 Mongo 예외/새 컨텍스트 replay를 추가 검증했고, 기존 분산 CAS·사진/완료행/영수증 재호출 시험을 재사용했다. 증거: `artifacts/local/validation/20261020-173203/`, `wave-05-*`, `warehouse02-*`, `ledger01-handoff.json`.

옮겨진 파일이 탐색에서 빠지지 않도록 기존 food-driver manifest의 source 결속에 새 내부 책임을 연결하고 공식 생성기를 다시 실행했다. 기존 StepKey·효과 경계는 유지하며 실제 통신/완료 증거를 추가한 것은 아니다.

후속 동작 변경 후보: 재위탁의 SQL 확정 뒤 Mongo 예외가 나도, 기존 출고 의뢰 replay는 Mongo를 다시 호출하지 않는다. null 동기화 반환도 현재 성공 경로에 남는다. 이번에는 이를 기존 계약으로 특성화했으며 새로운 자동 복구·투영 성공 판정을 넣지 않았다.

## 6단계 결과

기사·승인·주간 정산의 지급 준비 판정을 공통 Policy로 옮기고 기존 내부 facade를 유지했다. 관리자 추적 조회와 모의 지급 요청/입력 수명, 요금 편집·리뷰 조회·미리보기 계산을 독립 객체로 나눴다. 두 화면 마크업·route, 응답 소유권 검사, 3GET 순서/일괄 반영, hidden 요금 필드와 nullable 의미·올림 계산을 보존했다.

서버·관리자·시험 프로젝트 빌드와 관련 **302/302 시험**이 통과했다. 증거: artifacts/local/validation/20261020-174430/ 및 wave-06-* 자료. 첫 시도는 1단계에서 옮긴 화주 운임 구성의 기존 파일본문 검사 1개가 실패했고, 기존 Assert를 보존해 실제 Factory/InputPolicy와 facade 호출을 함께 검사한 뒤 재검증했다. 실제 지급이나 운영 통신은 실행하지 않았다.

## 7단계 결과

커뮤니티 지도의 snapshot/주기 조회, 내 원장 조회, 질문 작성/명시적 게시, Google SDK 자원을 별도 세션과 adapter로 나눴다. 페이지는 선택·URL·레이어·표시를 조립한다. SDK 초기화는 원형처럼 runtime 조회 뒤 최신 마커/선택을 읽는다. 전문 원장 10종의 상태 표현은 순수 projection으로, 7개 다이어그램 레이어는 catalog로 옮겼으며 UI event adapter와 하위 ViewModel 수명은 유지했다.

공통 UI·웹·시험 빌드와 관련 **380/380 시험**이 통과했다. 실제 Home Razor/HtmlRenderer의 다이어그램 진입·복귀, 동일 초안/선택/canvas/하위 ViewModel 유지와 fake HTTP·JS의 지연/실패/폐기를 확인했다. 증거: artifacts/local/validation/20261020-175716/, community-01-handoff.json, community-02-source-compatibility.json 및 wave-07-* 자료. 첫 빌드의 신규 시험 속성명 오류를 실제 오류메시지 속성에 맞춘 후 재검증했다. 브라우저 포인터·휴대폰·실제 SDK 접속 검증은 아니다.

후속 동작 변경 후보: 기존 질문/게시/내 원장 조회에는 선택·계정이 바뀐 뒤 늦은 결과를 차단하는 세대 보호가 일부 없다. 원형 특성화 시험으로 확인하고 이번에는 새 guard나 AuthSession.Changed 구독을 섞지 않았다. 서버 권한 검증과 브라우저의 오래된 표시 방지는 서로 다른 책임이다.

## 8단계 결과

DI는 업무별 연속 등록을 이름 있는 조립 메서드로 나누고 descriptor 165개·factory 8개·scoped alias·TryAdd·worker 원래 위치와 업무별 옵션 기본값을 보존했다. Program의 품목코드 inspect·YouTube 국가 sync·HSK import만 기존 Startup TryRunAsync 패턴으로 옮겼다. 세 분기 외 60,432바이트와 줄끝/BOM은 동일하고, 이동된 조회/JSON/파싱/로그/마이그레이션 조건은 명시한 정규화 기준으로 대조했다.

두 Kakao facade는 interface/options/생성자 설정을 유지하고 HTTP 요청/주소·지역 파싱을 동일 내부 소스로 공유한다. 별도 assembly나 호스트 의존을 추가하지 않았고 현행 문화권·H 지역 우선·null/예외/취소 의미를 보존했다. FoodApi는 주 소비 솔루션 밖에 있으므로 별도 restore와 실제 호스트 빌드를 포함했다.

서버·FoodApi·시험 빌드와 관련 **128/128 시험**이 통과했다. 증거: artifacts/local/validation/20261020-180734/, wave-08-*-handoff.json, wave-08-food-api-restore.log 및 wave-08-* 자료. 첫 시험 빌드의 새 namespace alias 충돌은 테스트 별칭만 명확히 바꾼 뒤 재검증했다. 실제 CLI/DB 마이그레이션·외부 지도 API·worker 운영은 실행하지 않았다.

## 최종 통합 검증

조사에서 승인한 **22개 항목을 8단계로 모두 구현하고 로컬 통합 검증을 완료했다.** 각 영역의 public facade·route/JSON·stable ID·오류·명시적 요청/재조회·전문 원장 권위와 기존 업무/기능 기본값을 유지했다. 이번 결과는 프로젝트 모든 코드의 의미 검증이나 운영 준비 완료를 뜻하지 않는다.

- `Task` 전체 소비 `Ssalddel.v3.5.slnx` 빌드: **오류 0, 경고 145개**. 기존 nullable·플랫폼 관련 경고 등이 남아 있다.
- 전체 시험: **13,585개 통과, 실패 0개**. 실제 격리 MongoDB/MySQL 환경을 요구하는 **16개 건너뜀**은 실행 성공에 합산하지 않는다. TRX `NotExecuted` 결과 16개를 직접 대조했다.
- 주 솔루션 밖의 `Ssalddel.FoodApi` 별도 최종 빌드: **경고/오류 0**.
- food-driver 코드 지도·`git diff --check`·검증 전후 **155개 소스/설정/문서 해시** 확인 통과. 검증 종료 후에는 아래 정본 문서 4개의 완료 상태만 갱신했고 구현/시험/생성지도 151개는 검증 판본과 같다.
- 실제 Home Razor의 모드 진입/복귀 렌더 검사는 7단계에 포함했다. 물리 단말·브라우저 포인터·실제 원격 SDK/배차·수납/지급·운영 worker·모델 가중치 학습은 이 검증 결과와 구분한다.

통합 증거는 `artifacts/local/validation/20261020-181018/`, `artifacts/local/refactoring-implementation/20261020-r1/final-validation.json`, `final-sources.json`, `final-source-verification.json`, `final-delivery-sources.json`, `delivery-scope.json`에 있다. 단계별 인계 해시는 인계 당시 판본이며, 시험 수정 후의 확정 판본은 `wave-XX-sources.json`과 최종 검증/전달 원장이 소유한다. 검토/승인 대기 중인 이전 AI 사례를 새 코드/DLL에서 자동 승인하지 않았다.

기존 dirty 작업은 보존했다. stage·commit·push·운영 배포·영상/Unity 변경은 수행하지 않았다. 위 3·5·7단계의 기존 동작 개선 후보는 별도 후속 작업이며 이번 책임 분리와 섞어 처리하지 않았다.
