# 생활 배송 수행 방식 선택 r24 변경·검증

2026-10-07. 사용자가 [생활 배송 수행 방식 선택 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/fulfillment.implementation.r24.md)의 구현을 승인했다. 현재 `Implemented / LocalSimulationValidated / RealProviderPending`다. 미커밋 로컬 구현이며 실제 사업자 연결은 포함하지 않는다.

우리 앱의 생활 물품 접수를 자체 배차 또는 두 모의 공급자 중 사용자가 확정한 하나의 수행에 연결한다. 비용·시간·만료를 비교하고 응답 유실·취소 확인 전에는 자동 대체 접수를 하지 않는다. 기존 자체 배차 모드·권한·동의·사유 지도와 직접 지급 경계를 유지한다.

## 구현 결과

- 기존 작성 화면에서 자체 배차·모의 퀵 배송·모의 생활 배송의 비용, 완료 예정, 만료와 제한을 비교한다. 추천만으로 접수하지 않고 사용자가 후보·비용·정보 제공을 확인한다. 금액·완료 시각 미확인은 확정하지 않는다.
- 견적·현재 선택·실행 시도는 세 SQL 테이블에 저장한다. 같은 요청의 재전송, 두 SQL 연결의 동시 확정, 지연 조회와 취소 후 재선택을 판본·고유 키·직렬화 transaction으로 보호한다. 접수 결과가 불명확하면 같은 시도를 조회하며 다른 공급자를 자동 호출하지 않는다.
- 원 의뢰만 저장된 중단 상태는 새 견적으로 복구하고, 아직 전송하지 않은 의뢰는 공급자나 자체 큐 호출 없이 취소한다. 오래된 동기화 자료가 취소 의뢰를 재개하지 않는다. 일반 조건 수정·기존 배차 선택·관리자 취소도 활성 수행을 우회하지 못하게 한다.
- 외부 수행은 자체 큐와 기사 위치를 만들지 않는다. 사유 지도는 최신 원 의뢰와 수행을 다시 읽고, 조회 도중 변경되거나 종료된 경로를 버린다. 공개 지도·상세 주소/연락처 보호와 기존 자체 배차 방식은 유지한다.
- 모의 수행은 카드 결제·FakePG·후불 승인·정산 지급에서 제외한다. 완료 상태·확정 비용·실제 지급은 별도이며 일반 결제 지연 이벤트도 모의 수행을 실제 배차/정산으로 승격하지 않는다.

`IFulfillmentProvider`와 DTO, EF 설정·migration, 기존 Controller/UseCase·동기화·결제/관리자 경계, 공통 Client/ViewModel/Razor를 연결했다. 최소 공급자 계약은 역량·견적·접수·조회·취소다. 모의 호출에는 실행 식별자·상태·예정 시각만 전달하며 개인정보 payload를 새로 저장하거나 외부에 전송하지 않는다.

## 대표 화면

아래는 제품 공통 Razor/VM에 합성 자료를 연결하고 실제 DOM 행동을 실행한 Web 화면이다. 실제 DB 의뢰·외부 사업자 접수·휴대폰 캡처가 아니다.

| 장면 | 화면 |
| --- | --- |
| 후보 비교 후 비용·정보 제공·배송 동의를 확인한 작성 · 390px | [화면](../assets/changes/2026-10-07-neighborhood-fulfillment-r24/create-confirmed-390.png) |
| 결과 불명확 상태에서 같은 실행을 조회해 접수를 확인한 뒤 · 320px | [화면](../assets/changes/2026-10-07-neighborhood-fulfillment-r24/recovered-accepted-320.png) |
| 원 의뢰만 저장된 상태에서 새 견적·동의로 자체 배차를 선택한 뒤 · 390px | [화면](../assets/changes/2026-10-07-neighborhood-fulfillment-r24/pending-reselected-internal-390.png) |

## 검증 기록

모바일 UI는 11장면×320/390px의 22개 흐름을 통과했다. 추천의 자동 확정 금지, 금액 미확인·만료, 실제 동의 체크와 접수 가능 상태, 응답 유실의 동일 요청 재확인, 취소 확인 전 변경 차단, 취소/미선정 후 새 견적 확정, 완료/지급 구분, 익명·조회 오류를 검사했다. 가로 넘침·페이지 오류·실패 스타일 응답·업무 API/외부 HTTP 요청·소스/바이너리 지문 변경은 0이다. `artifacts/local/neighborhood-fulfillment-r24/ui/verification.json`에 실제 실행을 기록했다. InteractiveServer 이벤트 반영을 기다린 뒤 버튼 상태를 검사하며 조건을 임의로 우회하지 않는다.

| 검증 | 결과와 범위 |
| --- | --- |
| 최종 Fast | 전체 3.5 제품·소비 앱 빌드 오류 0, 관련 536개 시험 통과. 원장 투영·기존 배차·결제/지급·관리자 취소·기존 운임 저장 구조 회귀를 포함한다. `artifacts/local/validation/20261007-131913`. |
| 최종 Task | 같은 소스의 전체 3.5 제품·소비 앱 빌드 오류 0, 경고 58개. 전체 시험 8,162개 통과·실패 0·환경 조건 Mongo 통합 2개 건너뜀. `artifacts/local/validation/20261007-132029`. |
| 관계형 서비스 | 실제 SQLite의 고유 키·판본·두 SQL 연결·재시작·지연 응답·동의·미접수 취소·실제 Mongo builder→SQL projector를 확인했다. Mongo-first와 RDB 사본에서도 모의 외부 수행의 자체 큐는 생성되지 않는다. 기존 직접 생성·내부 배차 Command 시험도 실제 projector를 통과한다. 실제 Mongo 서버 통신 증거는 아니다. |
| HTTP 계약 | Loopback Kestrel HTTP와 SQLite 서비스의 견적/선택/조회/취소 및 전체 상세 응답, 다른 사용자·빈 입력·오래된 판본·no-store 경계를 확인했다. 시험용 인증과 모의 공급자를 사용하며 실제 운영 로그인·업체 호출 증거로 확대하지 않는다. |
| 지급·취소 경계 | 실제 기존 결제 준비/승인·FakePG 재처리·지연 완료 이벤트·조건 수정, 관리자 취소의 모의 차단과 legacy 유지 시험을 통과했다. 모의 완료가 지급·정산 준비로 바뀌지 않는다. |
| 공통 UI | 실제 Razor/VM의 22개 장면 통과. 검토용 호스트·제품·시험에 사용한 `Ssalddel.Ui.Common.dll` SHA-256이 일치한다. |
| 소스·기존 작업 | 최종 Fast/Task 동안 대상 66개 파일 지문 변경 0. 시작 시 관련 없는 `Ssalddel.Ui.Common.csproj`의 원시 해시·branch·HEAD와 staging 없음 상태를 보존했다. |

상세 로그와 원시 자료는 `artifacts/local/neighborhood-fulfillment-r24/`에 보관하며 최종 집계·DLL·작업 보존은 `final-verification.json`에 결속했다. 결과를 반영한 문서 갱신은 Task 종료 후 수행했고 컴파일/시험 대상 코드의 지문은 유지했다. 첫 전체 시험의 구조 검사 실패는 기존 운임 대입을 보존하고 모의 지급 예정액을 DB 추가 전에 제외하는 방식으로 고쳤으며 시험 조건을 약화하지 않았다. UI 실행·컴파일·관계형 단위 시험·실제 업체 운영을 서로 다른 증거로 기록한다.

## 사용과 남은 경계

기본 설정은 비활성이다. Simulation에서만 `NeighborhoodFulfillment:Enabled=true`로 켜며 견적·시간은 모의 값이다. 실제 플랫폼별 연결에는 해당 사업자의 허용된 접수/조회/취소 계약과 개인정보 제공·요금·지급 책임이 필요하다. [승인 계획의 로컬 사용](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/fulfillment.implementation.r24.md#로컬-사용과-후속-공급자-연결)을 따른다.

새 서버를 DB에 연결하기 전에 `20261007140000_AddNeighborhoodFulfillment` migration이 필요하다. 플래그가 꺼져 있어도 원 의뢰의 두 열을 읽으므로 스키마를 먼저 갱신해야 한다. EF 설정·migration·snapshot은 대조했지만 실제 MySQL schema upgrade, MySQL 동시성/UTC 왕복, 사업자 API·결제·개인 단말·Azure·Unity·커밋/푸시는 이번 실행에 포함하지 않는다. SQLite의 스키마 생성·서비스 시험과 이를 구분한다.

기존 작업 폴더의 관련 없는 `Ssalddel.Ui.Common.csproj` 변경은 보존한다. 페이지 책임과 상태별 행동은 [책임 카드](../ProjectOverview/page-docs/neighborhood-fulfillment-r24.md)를 따른다.
