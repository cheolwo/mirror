# 운영 코드 후속 리팩토링 r2 구현

상태: `CompletedLocalValidation`. 사용자의 후속 리팩토링 요청에 따라 선정한 21개 항목의 첫 책임 경계를 구현하고 소비 앱 빌드·관련 회귀·마지막 Task 전체 시험을 통과했다. [항목별 작업 명세](implementation-work-order.r2.json)가 현재 상태를 소유한다. [조사](follow-up-survey.r2.md)와 원래 `ProposedOnly` 작업 명세·이전 22개 완료 기록은 역사 자료로 보존했다. 전체 저장소의 모든 파일이 단일 책임 원칙을 완전히 만족한다는 의미는 아니다.

업무 기능·화면 배치·문구·배차/요금 정책을 추가하거나 바꾸지 않는 구조 정리다. 공개 API/JSON/route/ID, 원문 요청·revision, 저장 키·취소/예외·권한·트랜잭션·IO/후속 효과의 기존 순서와 기본 비활성 경계를 보존했다. DB schema·실제 모델 학습·운영 활성화·실기기 설치·commit/push는 진행하지 않았다.

## 분리한 책임

| 기존에 함께 있던 책임 | 분리 결과 |
| --- | --- |
| 검수·배차 실행과 값 계산 | 입고 검수 Policy, 화물 조율 Composer, 음식 배차 SnapshotBuilder, 삽입 시도 Selector. DB·권한·저장·재확인은 기존 facade에 유지. |
| 화면 표시와 조회·선택·명령 수명 | 화주 차량추천 Session, 관리자 운송상세 ReadSession/DecisionState, 주문자 DemandSession, 판매자 ReadSession/Projection. 마크업·route·인증/동의 guard는 유지. |
| 네이티브 보관과 직렬화 중복 | 7개 인증 adapter의 Token JSON core와 2개 미확정 작업 adapter의 별도 ProtectedPending core. JSON 손상 처리의 서로 다른 정책은 합치지 않음. |
| 요약·오류·문서 출력 | 주간 명세 Projector, Problem 속성 Reader, HTML Renderer·용지 Layout·바코드 Identifier resolver. 원문 가공·파일명·출력 계약 유지. |
| 초안 저장과 생성/지문 | 수입 초안 Factory/Codec. 개별 시각 취득·재시도 키·revision·공용 잠금은 ViewModel에 유지. |
| 제공자별 지도 SDK 객체 | Android Naver/Google surface. overlay·view·listener 해제 순서, 숨긴 provider의 현행 callback 처리와 Handler의 timer/compass/generation은 유지. |
| AI 검토·공공가격 자료 | 검토 예시/권역/주문 보강 Catalog와 KAMIS/AMS typed 관측·가격 Projector. 실제 조회·취소·Source 판정·DB 순서는 facade에 유지. |
| 시작/검증 도구의 여러 역할 | 가격 수집 CLI 2개, 시험의 RepositorySource와 실제 DI descriptor 검사, 변경 파일·빌드/시험 선택 script. 실행·증거 조립은 entry에 유지. |

## 항목별 결과

| 순서 | 식별자 | 범위 | 상태 |
| --- | --- | --- | --- |
| 1 | `SERVER-R2-03` | 창고 입고 검수의 순수 판정과 원자 저장 분리 | 로컬 검증 완료 |
| 2 | `SHARED-01` | 7개 MAUI 인증 토큰 저장소의 동일 직렬화·손상 처리 | 로컬 검증 완료 |
| 3 | `SERVER-R2-02` | 국내화물 조율 입력의 근거 수집과 조합점수 조립 분리 | 로컬 검증 완료 |
| 4 | `SERVER-R2-01` | 음식 배차 시점 평가의 자료 조립과 모델 조율 분리 | 로컬 검증 완료 |
| 5 | `SERVER-R2-05` | 운송 삽입 평가의 경로조회와 순수 누적·시도선택 분리 | 로컬 검증 완료 |
| 6 | `UI-R2-01` | 화주 작성 초안과 차량추천 조회·선택 수명 분리 | 로컬 검증 완료 |
| 7 | `UI-R2-03` | 관리자 운송의뢰 상세의 관찰 수명과 취소 결정 입력 분리 | 로컬 검증 완료 |
| 8 | `UI-R2-02` | 주문자 비구속 수요 Panel의 모집권 조회와 등록·철회 상태 추출 | 로컬 검증 완료 |
| 9 | `UI-R2-04` | 판매자 수요 허브의 조회 조율·polling과 집단수요 표시 projection 분리 | 로컬 검증 완료 |
| 10 | `SERVER-R2-04` | 작업자 주간 명세의 전문 자료 조립과 완료주간 projection 분리 | 로컬 검증 완료 |
| 11 | `SHARED-02` | 기사·음식점 pending 보호저장의 잠금/읽기/쓰기/소유자 삭제 중복 | 로컬 검증 완료 |
| 12 | `SHARED-03` | Admin/FDriver HTTP 오류 메시지 파싱과 전문 오류 표현 분리 | 로컬 검증 완료 |
| 13 | `UI-R2-05` | 공통 문서 출력의 용지 규격·식별코드 조립과 렌더러 분리 | 로컬 검증 완료 |
| 14 | `UI-R2-07` | 관리자 같이수입 준비의 초안 편집·저장 지문과 OS 명령 조립 분리 | 로컬 검증 완료 |
| 15 | `SERVER-R2-06` | 공개·전국 화물 콜의 조회와 공통 제안 표현 조립 분리 | 로컬 검증 완료 |
| 16 | `UI-R2-06` | 화물기사 Android 지도 Handler의 제공자 자원과 표시 갱신 수명 분리 | 로컬 검증 완료 |
| 17 | `SERVER-R2-07` | 음식 AI 검토의 예시/공간 보강과 판단사례 기록 분리 | 로컬 검증 완료 |
| 18 | `DATA-R2-01` | KAMIS·AMS 조회와 원자료 표시 projection 분리 | 로컬 검증 완료 |
| 19 | `SHARED-04` | Program에 남은 BLS·국제가격 수집 CLI와 호스트 조립 분리 | 로컬 검증 완료 |
| 20 | `SHARED-05` | Architecture 시험의 소스 탐색과 DI 의미 검증 책임 분리 | 로컬 검증 완료 |
| 21 | `SHARED-06` | validate-changes의 검증계획 생성과 실행/증거 조립 경계 | 로컬 검증 완료 |

## 실행 증거

| 묶음 | 검증 | 통과 | 실패 | 미실행 | 로그·TRX 위치 |
| --- | --- | ---: | ---: | ---: | --- |
| 1 | Fast 관련 | 446 | 0 | 0 | `artifacts/local/validation/20261020-185658` |
| 2 | Fast 관련 | 275 | 0 | 0 | `artifacts/local/validation/20261020-191020` |
| 3 | Fast 관련 | 349 | 0 | 0 | `artifacts/local/validation/20261020-193110` |
| 4 | Fast 관련 | 318 | 0 | 0 | `artifacts/local/validation/20261020-194519` |
| 5 | Task 전체 | 13967 | 0 | 16 | `artifacts/local/validation/20261020-195448` |

마지막 Task는 이번 소유 경로의 합집합을 명시해 `Ssalddel.v3.5.slnx` 소비 빌드와 제품 전체 suite를 실행했다. **13967 통과·0 실패·16 건너뜀**이다. 위 Fast 수는 서로 겹치는 회귀 실행이므로 합산한 고유 시험 수가 아니다. TRX adapter의 `Counters.notExecuted`는 0이지만 실제 `UnitTestResult`의 `NotExecuted` 16건과 console의 건너뜀 16건이 일치하므로 결과별 상태를 기준으로 집계했다. 격리 DB 등 환경이 필요한 미실행 이유는 TRX에 남겼으며 실제 DB/운영 검증 성공으로 바꾸지 않는다.

문서 출력은 분리 전에 실제 원형 DLL로 저장한 운송장·입출고 품목표 22개 전체 HTML/평문 출력과 일치했다. 검증 script는 원형의 12개 PlanOnly JSON, 검색어 16개·시험 24개 상한·명시 필터 우선순위와 명령 성공/실패·출력·check 기록을 대조했고, 기존 `eng/tests/validate-changes-routing.ps1`도 통과했다. 전체 validator 실패 후 finally 증거 닫힘은 원문 대조 근거이며 새 runtime 시험으로 표시하지 않는다.

`DriverApp`의 `net10.0-android`를 따로 지정한 컴파일도 오류 0개로 통과했다. 기존 패키지 제약 등 경고 27개가 남아 있다. SDK 수명 시험은 원형 본문/해제 순서와 실제 소스 결속 검사이며 실제 기기의 지도·GPS·provider 전환·늦은 callback 실행을 대체하지 않는다.

원형 8,757개 소스/프로젝트 해시와 재대조한 결과 기존 변경 38개·새 소스 51개·삭제 0개이며 이번 소유 범위 밖 소스 변경은 0개다. 각 묶음의 중앙 검증 중 소유 소스 해시 변경도 0개였다. 기존 dirty 작업·원래 조사·영상·Unity 자료는 유지했다.

원형·이동 비교·소유 경로·실제 TRX·독립 검토는 `artifacts/local/refactoring-implementation/20261020-r2/`에 있다. 일부 최초 빌드는 이동 파일의 누락 참조/타입 모호성 때문에 실패했고, 최소 using/공유 resolver 보완 뒤 재검증했다. 최초 실패 로그와 수정 이력을 삭제하지 않았다.

## 유지한 경계와 남은 검증

기존 동작을 고정하는 작업이므로 관리자 자동 조회/수동 조회의 오류 처리 차이, 수요 등록/철회 후 조회의 메시지 차이, 계정·상품 변경의 기존 부분 초기화, 가격 갱신/원장 동기화의 기존 예외 동작은 새 정책으로 고치지 않았다. 조사 메모의 차량추천 GET 표기는 원문 확인으로 정정했으며 실제 `POST api/v1/shipper/requests/recommend-vehicle`를 유지했다.

최소 경계 뒤의 추가 분리 가능성도 구분한다. Android presentation timer는 Handler에, AI Workspace/판단사례 요청 조립은 facade에, 검증 실행/증거 writer는 entry에 남겼다. 이는 현재 첫 추출의 검증 범위이며 모든 내부 코드를 끝까지 쪼갰다는 주장이 아니다.

브라우저/휴대폰 화면·카메라·실제 보호저장·푸시·네이버/구글 provider·모델 호출/학습·외부 서버·운영 DB·실제 정산/송금은 이번에 실행하지 않았다. 로컬 빌드/시험 성공과 실제 운영 준비 완료를 구분한다. 커밋·푸시는 하지 않았다.
