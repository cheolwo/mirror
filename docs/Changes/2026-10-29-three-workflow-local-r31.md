# 화물·음식·창고 로컬 업무 완주와 역할 앱 확인 r31

2026-10-29. 사용자가 실제 원격 서버 대신 로컬 서버로 가능한 업무를 검증하도록 요청했다. [앱 공통 제작 절차](../AI/Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-input-guide.md)에 따라 같은 업무 ID의 실제 제품 API·격리 DB 결과와 실제 앱 행동을 구분한다. **세 업무의 서버 결과와 휴대폰 주문자·창고 조회/인증 복구, PC 음식점 인증·수신함·재실행 복구를 확인했고 최종 전체 Task도 통과했다. PC 빌지 설정 진입은 실행 대기가 아닌 `미완료 / 별도 보완 필요`로 남긴다.**

## 실행 경계

- 기존 R108의 제품 Startup·JWT·Controller·UseCase를 재사용했다. 접속 대상은 `http://127.0.0.1:5378/`, 실행은 `Simulation`, 전용 실행 ID는 `integrated-r108:d1e48b984f5e40368f027925af8c16cc`다. SQL/Mongo DB는 이 실행에 결속한 `r108m_d1e48b984f5e40368f027925af8c16cc`다.
- 실제 MySQL/Mongo와 합성 계정·주문·상품·좌표/사진을 사용한다. 자동 worker·외부 HTTP·실금융·FCM을 켜지 않았다. 과거 r30의 완료 건과 실패·미확정 시도를 새 완료 건으로 합산하지 않는다.
- API 완료, SQL/Mongo 확인, 후속 투영, 실제 APK 버튼, 앱 재실행, Windows 화면 표시를 별도 근거로 남긴다. 이 회차의 완료가 운영·공개 준비나 실제 운송 완료를 뜻하지 않는다.

최종적으로 같은 서버 바이너리와 같은 DB를 유지한 채 서버를 종료·재기동했다. 재시작 전후 세 업무의 상태·수량·이력·선택 투영 및 Mongo 문서 지문이 모두 같았고 14개 확인 조건이 통과했다. 준비 상태 조회는 200 세 번이었다. 이 검사는 순차 읽기의 비교이며 SQL/Mongo 간 원자적 스냅샷은 아니다. 이전 r30의 준비 확인 503 원인을 이번에 규명·수정한 것으로 표현하지 않는다.

## 확인한 세 업무

| 업무 | 같은 건의 실제 결과 | 독립 확인과 남은 경계 |
| --- | --- | --- |
| 화물 | 의뢰 `e3029262-e022-8a43-8275-8719d8b069a2`, 운송 `9`의 수락·상차·하차를 제품 API에서 처리해 `인수완료`를 조회했다. | 독립 SQL/Mongo 10개 검사를 통과했다. 네 단계 이벤트의 순서·단일 기록, 완료 원장 1건, 결제 준비와 미승인·미지급 상태를 대조했다. 기사 하차 완료가 만든 `인수완료`이며 별도 고객 수령 버튼이나 실금융 확인으로 표현하지 않는다. |
| 음식 | 주문 `FOOD-20261029024745747`의 음식점·기사·주문자 제품 API를 거쳐 `수령확인`을 재조회했다. 서버 재시작 뒤 같은 주문·이력·운송 사건과 독립 DB를 대조했다. | 선택한 후속 투영 6건만 실제 handler로 처리했다. 미선택 outbox 8건은 보존했으며 전체 worker를 켜거나 알림 발송·게시글 생성·전체 후속 완료로 확대하지 않았다. |
| 창고 | 입고 `910003`의 수령→검수→적재를 처리하고 새 SKU의 가용 수량 `5`와 지정 적재 위치를 재조회했다. | 독립 DB 8개 검사로 상품 1건·단계별 이력·수령/적재 이동의 중복 없음과 동일 적재 재요청 전후 SQL 불변을 확인했다. 마지막 적재 재요청 응답의 플래그는 수집하지 못해 보류하며, 피킹·출고·운송 인계나 물리 바코드 스캔까지 완료한 것은 아니다. |

음식의 제한 실행은 outbox ID `5, 6, 12, 13, 16, 17`에 한정했다. 여섯 건의 성공 1회·다시 실행 시 추가 처리 없음과 미선택 지문·정산·이력·이벤트 불변을 확인했다. 남은 8건에는 음식점 알림·커뮤니티 활동과 완료 월드 투영이 포함된다. 실제 발송·공개 게시·Unity 반영을 주장하지 않는다. 기존 음식 상위 단계의 legacy 매핑과 음식 주문 블록의 상태도 보존했다.

## 좁게 보완한 제품 코드와 실패 복구

- `Ssalddel/Services/LogisticsProcessing/Warehouse/WarehouseOperationService.cs`의 `CompleteInboundAsync`, `InspectInboundItemAsync` 두 메서드에서 수령·검수의 사용자 트랜잭션을 EF 실행 전략 안에서 처리하도록 보완했다. 재시도 시 상품·재고·이력·알림의 단일 저장과 롤백, 기존 호출자 트랜잭션·호출 전 변경 보존을 회귀로 확인한다. 두 메서드 밖의 기존 제품 소스는 이번 보완에서 바꾸지 않았다.
- 새 회귀는 `Ssalddel.Tests/Services/LogisticsProcessing/Warehouse/WarehouseInboundExecutionStrategyR31Tests.cs`에 있다. 전체 시험에서 드러난 시험용 EF 내부 서비스 제공자 캐시 누적을 추가로 분리했다. 이 시험 격리 수정은 제품의 경고를 숨기는 변경이 아니다.
- r30의 시간창 확인은 원래 실패 journal을 보존한 별도 `r31-new-explicit-update`로 재검증했다. 기존 의뢰의 현재 주소·연락처를 메모리에서 그대로 포함하고 네 시각을 동시점 `+09:00`/`Z`로 표현한 새 PUT 한 번을 실행했다. GET·독립 SQL의 같은 UTC 순간과 비시간 업무 값 불변을 확인했으며 원래 r30 실패를 덮거나 새 의뢰·승인·추천을 만들지 않았다.
- 창고의 초기 수령 실패, 재개 도구의 중단 및 적재 응답 플래그 누락은 각각 보존했다. 중단 후 같은 입고 ID의 정본 API·DB를 다시 읽어 실제 저장 결과와 도구의 관측 실패를 구분했다. 불명확한 요청을 무인 재전송하지 않았다.

## 시험 상태와 이전 실패 보존

| 실행 | 확인한 결과 |
| --- | --- |
| 첫 전체 Task | 통과 `16,735`·실패 `5`·환경 생략 `17`. EF `ManyServiceProvidersCreatedWarning`으로 실패한 근거를 보존했다. |
| 다음 전체 Task | 통과 `16,729`·실패 `11`·환경 생략 `17`. 실패 11개는 새 창고 fixture의 같은 EF 제공자 누적 경고였다. 앞선 위치 관련 5개 실패는 반복되지 않았다. |
| fixture 추가 보완 뒤 집중 검사 | `80` 통과. 신규 창고 재시도·롤백·중복 및 관련 소비 경계를 함께 검사했다. 전체 Task 성공으로 승계하지 않는다. |
| 최종 전체 Task `20261029-124128` | 전체 솔루션 빌드와 `16,740` 통과·실패 `0`·환경 생략 `17`을 확인했다. 전체 `16,757`이며 실행 TRX와 `task-final-r2-summary.json`을 보존했다. |

기존 Task 로그·TRX와 실패 진단은 남긴다. 집중 80개 통과나 전체 솔루션 빌드만으로 최종 전체 회귀 성공을 선언하지 않으며, 위 최종 수치는 별도로 완료된 전체 실행의 TRX 근거다.

## 포장·실제 휴대폰·Windows 확인

창고·주문자·음식 기사·음식점 Android 검토 APK 네 개를 `0.1.138 / code138`, 고정 endpoint `5378`, 각각 기존 ID의 `.localreview` 패키지로 제작했다. 외부의 기존 debug keystore를 재사용하고 새 FCM 설정은 만들지 않았다. 네 APK 모두 서명·버전·해시와 arm64-v8a/x86_64 각각의 최종 내장 앱 DLL을 컴파일 DLL·endpoint 메타데이터와 대조했다. 포장 중 관측 입력 드리프트는 0이다.

Android 입력 3,396개의 고정 대장은 `client-input-freeze.json`이다. Windows 후속 고정 대장과의 차이는 제작 recipe·identity targets·검증 도구뿐이며 제품·공통 소스는 동일하다. 이 목록을 모든 환경·동적 입력의 완전한 전이 의존성으로 주장하지 않으며 서버 소스의 별도 수정·빌드와도 구분한다. 원래 사용자 앱과 데이터를 교체·삭제하지 않는다.

| 대상 | 이번 실제 확인 | 최종 확인 상태 |
| --- | --- | --- |
| 실제 휴대폰 화물 기사 | 현재 건의 `상차지 도착` 버튼과 정본 상태, 앱 종료·재실행 뒤 인증 유지·같은 업무 조회를 확인했다. 나머지 API 전이를 모두 native 버튼으로 수행한 것으로 표현하지 않는다. | 이 한정 범위 확인, 전체 native 완주 미검증 |
| 실제 휴대폰 창고 APK138 | 품목 `910004`의 가용 수량 `5`·위치 `R31-A01-5B5CD8196160`을 조회하고 앱 재시작 뒤 인증 유지·홈 복귀를 확인했다. | 조회·인증 복구 확인, 물리 스캔·전체 native 입고 미검증 |
| 실제 휴대폰 주문자 APK138 | 실제 로그인 뒤 같은 주문 `FOOD-20261029024745747`의 완료 목록·상세에서 `수령확인`/배달완료와 금액 `10,000원`을 확인했다. 강제 종료·재실행 뒤 로그인 유지와 전체 이력 `1건` 재조회를 확인했다. | 동일 주문 조회·인증 복구 확인, API의 수령확인 행동을 native 버튼 수행으로 승격하지 않음 |
| 실제 휴대폰 음식 기사·음식점 APK138 | 별도 패키지의 포장·설치·기동과 서버 API 완료를 구분한다. | 전체 native 업무 완주는 미검증 |
| Windows 음식점 Attempt4 | 실제 창을 최대화한 뒤 화면·로그인·`5378` 진행 수신함 조회를 확인했다. 완료 주문은 진행 수신함에서 제외돼 `0건`으로 보였다. 창을 닫고 같은 검토 EXE를 다시 기동한 뒤 비밀번호 재입력 없이 `restaurant101` 인증과 실제 서버 수신함의 `12:47:23` 조회 시각 갱신을 확인했다. 원래 `RestaurantDeskApp` 어셈블리명과 최종 AppInfo PackageName `.localreview`·endpoint·발행 파일 736개 해시를 검증했다. | 인증·수신함 조회·재실행 복구 확인. **빌지 설정 진입 미완료 / 별도 보완 필요 — 아래 제한 참조** |

Windows Attempt1의 잘못된 Mono Windows runtime 복원, Attempt2의 WinUI 필수 import 누락과 Attempt3의 흰 화면 관측은 보존했다. 제품 소스 대신 앱에만 적용되는 outer/inner hook과 실제 SDK import chain으로 제작 도구를 보완했다. Attempt4는 원래 어셈블리명을 유지해 CSS 이름 불일치를 제거했다. 이는 흰 화면의 원인을 확정한 결과가 아니며, 실제 화면 표시는 최대화 뒤 별도로 확인했다. 기존 주소 검색 스크립트의 참조 경로 결손도 별도로 남겼다. AppInfo PackageName에 따른 저장소 경계, 전용 WebView2 캐시, 인증 복구와 실제 화면은 각각 구분한다.

PC의 빌지 설정을 클릭하면 `Routes.razor`의 Release `AllowedRoutes`에 `/settings/receipts`가 없어 기존 내부 테스트 범위 경고가 나온다. 확인한 경로 차단이므로 단순 실행 대기가 아닌 미완료·별도 보완 필요다. 이 회차에서는 제품 코드를 바꾸지 않았으며 빌지 설정 진입·저장과 물리 출력 확인은 다음 보완으로 남긴다. 진행 수신함 `0건`을 완료 주문의 PC 상세 조회 성공으로 표현하지 않는다.

## 로컬 근거와 후속

원시 응답·계정·토큰·연락처·raw capture를 이 문서에 복사하지 않는다. 로컬 `artifacts/local/three-workflow-local-r31/`의 다음 자료가 회차별 실제 근거를 소유한다.

- 화물: `cargo/independent-db.json`, `cargo/result.json`, `cargo/r31-new-explicit-update.json`.
- 음식: `food/final-verification.json`, `food/projection-final-verification.json`과 이전 projection 검토·429 원기록.
- 창고: `warehouse/post-resume-canonical-db.json`, `warehouse/resumed-ingress-proof.json`, `warehouse/source-scope-audit.json`, `warehouse/task-provider-diagnosis-r2.json`, `warehouse/fixture-provider-focused-r2.json`.
- 제작·기기: `apps/apk-*-attempt1-proof.json`, `apps/apk-*-attempt1-endpoint.json`, 설치본 해시 기록, `apps/desktop-restaurant-attempt4-proof.json`, 별도 기기/Windows 관측.
- 실제 화면·재실행 원기록은 로컬 비공개 자료로 보존한다. `orderer-completed-row-r138.png`, `orderer-received-detail-r138.png`, `orderer-restored-history-r138.json`, `warehouse-stock-expanded-r138.png`, `warehouse-after-relaunch-r138.json`, `restaurant-inbox-windows-r138.png`, `restaurant-reopened-windows-r138.png`가 해당한다. 계정·원시 화면을 문서 자산으로 자동 공개하지 않는다.
- 시험: `task.log`, `task-after-fixture-isolation.log`, `warehouse-fast-fixture-isolation.log`, 최종 재실행 `task-final-r2.log`·`task-final-r2-summary.json` 및 `artifacts/local/validation/20261029-124128/Ssalddel.Tests.trx`.
- 최종 재시작: `restart-preservation/final-r31-before.json`, `final-r31-after.json`, `final-server-lifecycle.json`, `final-server-after.json`. 같은 바이너리 SHA와 DB, 세 업무의 지문 일치를 대조한다.

후속 보완은 PC 빌지 설정 경로와 모든 역할의 전체 native 수행이다. 기준선과 마감표는 [현재 검증 기준선](../Versions/unified-1.0/current-validation-baseline.md)·[통합 1.0 마감표](../Versions/unified-1.0/checklist.md)가 소유하며 이 문서가 자동으로 마감 상태를 승격하지 않는다.

검증용 화물 세션의 메모리 토큰은 종료하며 버렸다. 사용자가 로컬에서 계속 확인할 수 있도록 최종 `5378` 서버와 검토 앱·설치본·격리 DB는 남겼고, 자동 worker·외부 서비스는 켜지 않았다. 휴대폰 화면 유지 설정은 원래값15이며 사용자 앱·데이터를 지우지 않았다. 원본 APK/이전 포장·실패 기록과 기존 변경·Git index는 보존한다.

게시글 CRUD·AI 자료 수집/학습, 실결제·실수납/지급, 실제 FCM·GPS·도로 운행, 물리 바코드·프린터 검증은 0이다. 이번 회차에서 commit·push·원격 배포·운영 활성화는 수행하지 않았다.
