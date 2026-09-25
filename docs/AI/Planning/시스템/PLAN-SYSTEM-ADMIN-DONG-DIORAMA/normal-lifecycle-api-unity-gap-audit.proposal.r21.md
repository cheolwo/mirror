# [기획 · 운영 API·Unity 관찰 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 제안 r21]

## 결론

**정상 생명주기를 새로 설계하는 대신, 이미 구현된 업무 API→원장→관찰 사본→Unity 배치 사이의 부족한 연결을 먼저 보완한다.** 정상 경로가 실제로 보이는지 확인한 뒤 기존 예외 처리 기획을 같은 경로에 결속한다.

- 기준일: 2026-09-22, 현재 작업트리 소스와 기존 기획·실행 기록을 대조했다.
- 확정 방향: 사용자 요청의 정상 경로 우선 확인, 기존 API·생명주기·예외 기획 재사용, Unity의 읽기 전용 관찰을 유지한다.
- 상태: `AuditRecorded / FocusedTestsPassed / ThreeBoundaryIssuesReproduced / RemediationProposed`. 제품 코드 수정·새 업무 실행·Scene 변경 승인은 이 문서로 만들지 않는다.
- [r20 제안](unity-first-reference-and-os-lifecycle.proposal.r20.md)의 보충·정정: 음식 업무에는 실제 역할 앱 HTTP 실행과 Unity 계약 어셈블리의 live 메모리 소비 기록도 있다. 따라서 음식 정상 흐름의 처음부터 재구현은 우선순위가 아니다. 우선 **기존 실행기를 재사용한 배치·표현 연결과 관찰 정합성 보완**으로 좁힌다.

## 1. 조사 범위와 근거 수준

운영체제 식별자 목록은 10개이고, `OperatingSystemLifecycleCatalog`의 명시적 단계 정의는 화주·화물·음식·창고·마트 5개다. Unity 공간 배치 계획기의 OS 지원은 음식·화물·창고·마트 4개다. 주문자·음식점·배차·기사 역할이 있다는 사실과 독립 OS 등록 수를 혼동하지 않는다.

이번 상세 대조는 디오라마의 위 4개 업무와 화주 인계다. 공동구매 수요·공동 수입·커뮤니티 신뢰·플랫폼 운영·교육 현장체험은 식별자 존재만 확인했고 정상 종단/API/Unity 표현 전수 검증은 하지 않았다. 이를 코드 없음으로 표시하지 않는다.

| 구간 | 현재 확인 결과 | 아직 같은 실행으로 확인할 것 |
| --- | --- | --- |
| 음식 주문·음식점·배달 | 주문 등록·수락·조리/픽업 준비·추천 수락·픽업·전달·수령 확인 API/Handler/Service 존재. 정상 수직 시험과 역할 앱 HTTP 실행기 존재 | 기존 실행기를 실제 Scene의 배치·표현까지 연결. 메모리에서 읽혔다는 사실만으로 GameObject가 생겼다고 판단하지 않음 |
| 화주·화물 | 의뢰→화물 인계, 기사 상태 전이, 완료→화주 인수 인계와 멱등 시험 존재 | 현재 공통 지역 API의 화물 경로는 `인수완료`와 완료 Event 증명을 요구한다. 의뢰·수락·상차·운송 중 단계의 live 관찰과 종단 인증 HTTP 증거는 별도로 닫아야 함 |
| 창고 | 재고·적재·피킹·포장·출고 인계 UseCase와 시험, `창고WorldSnapshot조회UseCase` 존재 | 현재 관찰 사본은 재고·대기 적재·피킹·NPC·화물 인계를 조합한다. 입고/검수부터 포장·출고 종료까지 한 건의 모든 단계가 공통 v2 단계로 매핑되는지는 미완결 |
| 마트 | 주문 요청, 피킹 상세, `마트피킹포장World조회UseCase`, 라스트마일→음식배달 인계 존재 | 공통 지역 UseCase는 마트 전용 피킹·포장 Reader를 호출하지 않고 생활권 거점 상태를 읽는다. 거점 상태와 주문별 피킹·포장 진행을 구분하여 연결 필요 |
| 공통 지역 API | 인증·기능 플래그, v1 호환/v2 선택, 자료원별 실패, 비식별 상태 사본 존재 | HTTP 미들웨어·권한·DB·각 OS 업무 실행과 실제 Scene을 결속한 범위별 시험 |
| Unity | Client·Interpreter·OS Router·배치 계획기·WorldController·다중 OS 표현 Layer 존재 | 아래 배치 종류·단계 어휘·TTL·삭제 판본 결손. 동결 표본 재생과 실제 상태 사본 표현을 같은 완료로 처리하지 않음 |

### 기존 실실행 기록은 보존한다

- [역할 앱 실행기](../../../../../eng/Ssalddel.RoleAppHeadlessE2E/README.md)와 [지역 운영 기획의 r4~r6 실행 기록](../PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/README.md)은 역할별 실제 HTTP Client와 MySQL 재조회, Unity Client/Interpreter/Router의 live 메모리 소비를 기록한다. 현재 실행기 소스도 각 음식 단계에서 같은 가명 업무를 재조회한다.
- 같은 실행기의 수령 확인 검사는 **진행 중 업무가 메모리에서 제거되는지**까지다. 동일 주문의 완료 표식·완료 카드와 연결되어 보이는지는 별도 조건이다.
- [다중 OS r4](../PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/implementation.r4.md)의 4 OS·8사례·77단계는 격리 표본 원장·Outbox·DB와 동결 Unity 재생 증거다. 실제 업무 Controller/UseCase 호출 증거로 대체하지 않는다.
- 이번에 과거 HTTP/DB 실행을 재실행하거나 당시 raw 결과를 모두 복원하지는 않았다. 과거 기록과 이번 자동 시험 결과를 구분한다.

## 2. 이번 실행으로 재현한 연결 결손

현재 `Ssalddel.Unity` 프로젝트를 참조한 독립 진단 실행기를 로컬 산출물 폴더에 만들었다. 제품 코드와 기존 시험을 수정하지 않고 실제 컴파일된 코어를 호출했다.

### G1 — 진행 중 음식 자료가 배치 단계에서 제외됨

- 서버 `진행중음식배달WorldProjectionReader`는 `ItemKind=ActiveLifecycle`을 반환한다.
- `OperationalWorldSceneInterpreter`는 진단 입력을 수용했고 `CurrentItems=1`이었다.
- `OperationalWorldScenePlacementPlanner.VisualByItemKind`에는 `CompletedLifecycle`, `WarehouseTask`, `WarehouseActor`, `CargoHandoff`만 있다.
- 실제 결과: `placementCount=0`, `ItemKindUnsupported`.
- 보완 제안: 진행 자료의 명시적 표현 종류를 기존 계획기에 결속하고 해당 자료를 실제 Controller→View로 전달하는 회귀 시험을 추가한다. 조회 성공만 체크하는 시험으로 끝내지 않는다.

### G2 — 같은 업무 단계의 유효기간 연장이 반영되지 않음

- 진행 Reader는 현재 조회 시각 기준 2분 TTL을 계산하지만 `PublishedAtUtc`는 주문의 변경 시각이다.
- 지역 API의 증분 조회는 `PublishedAtUtc > cursor`만 전송한다. 주문 단계가 그대로이면 살아 있는 업무가 재전송되지 않을 수 있다.
- Interpreter 역시 같은 revision과 같은 게시 시각이면 새 만료 시각을 적용하지 않는다.
- 진단: revision 5, 게시 시각 동일, 만료 시각을 00:02→00:03으로 늘려 재적용했지만 저장된 만료는 00:02였다. 121초 뒤 객체 수는 0이었다.
- 보완 제안: 업무 revision과 관찰 유효기간 갱신을 구분한다. 증분 응답의 생존 확인 계약 또는 주기적 전체 재조회와 동일 내용 TTL 갱신 중 기존 계약에 맞는 작은 방법을 고른다. 단순히 같은 revision의 다른 업무 내용을 허용하거나 TTL을 무제한 늘리지 않는다.

### G3 — 낮은 revision의 삭제 사본이 최신 객체를 제거함

- Interpreter는 `IsTombstone`/만료 여부를 먼저 처리하고 그 뒤 낮은 revision을 검사한다.
- 진단: revision 5인 객체에 revision 1 tombstone을 적용한 뒤 객체 수가 0이었다.
- 보완 제안: 삭제 역시 판본 검사를 거치고, 삭제 이후 지연 도착한 과거 사본의 부활 방지도 시험한다. 삭제 자료가 왔다는 사실만으로 최신 상태를 없애지 않는다.

## 3. 소스 대조로 추가 확인한 불일치

- **단계 어휘:** 진행 음식 API는 `조리중`·`기사배정` 등의 업무 상태를 내보낸다. Unity의 `다중Os생명주기검증Models`는 `food.cooking` 등 표본 단계 대장을 검사하고 미지원이면 `LifecycleStageUnsupported`로 거절한다. G1만 풀어도 표현이 끝나지는 않는다. 기존 음식 관찰 Adapter의 정규화를 재사용하여 운영 상태→공통 단계→표현을 명시적으로 결속해야 한다. 두 원본 계약을 일괄 이름 변경하지 않는다.
- **완료 연결:** 진행 업무는 HMAC 가명 `food-delivery-work:*`, 완료 자료는 별도 완료 식별자를 사용하고 공통 조회의 기본값은 완료 `SnapshotStableId`를 업무 ID로 쓴다. 활성 객체 제거와 완료 객체 간의 연결을 조회자가 추측하게 하지 않는다. 개인정보를 추가하지 않는 서버 발급 연결 계약을 검토한다.
- **정상 대기와 오류:** 활성 음식 사본의 주의 상태는 대체로 `Active`이고 취소/거절 tombstone은 `RecoveryPending`이다. 관리자 조화 Projector의 조리·배송 지연·기술 실패 의미가 그대로 Unity에 온다는 보장은 없다. 단계를 새로 만들지 말고 필요한 비식별 주의 상태만 분리 투영한다.
- **화물 중간 상태:** 완료 전용 조회를 유지하며 진행 상태 Reader를 별도로 검토한다. 실제 운송 원장·Event에서 생성하고 표본 타임라인으로 채우지 않는다.
- **창고·마트:** 이미 있는 전용 조회 결과에서 단계·업무 ID·인계 관계를 비식별 공통 사본으로 변환한다. 작업 종류/거점 상태를 전체 생명주기 완료라고 해석하지 않는다.

이 절의 항목은 소스 대조 결과이며 실제 Unity 화면에서 모두 재현했다는 뜻이 아니다.

## 4. 기존 예외 처리 기획을 이어받을 범위

| 기존 기획·코드 | 이미 정한 것 | 후속 확인 |
| --- | --- | --- |
| [환경별 Adapter r8](../PLAN-SYSTEM-OS-LIFECYCLE-ENVIRONMENT-ADAPTERS/README.md) | 판본 충돌→정본 재조회, 일시 통신 장애→같은 요청 ID로 제한 재시도, 최종 후속 실패→운영자의 안전 재시도 예약 | 같은 실패가 Unity에 어떻게 비식별 표시·복구되는지, 음식 외 OS 적용 여부 |
| [지역 운영 생명주기](../PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/README.md)와 `음식배달운영생명주기조화Projector` | 조리·배차 병행, 픽업 인계, 배송, 수령 확인과 종료. 조리 지연·배송 지연·자동 배차 회복 구분 | 정상 대기와 업무 지연·통신 오류를 섞지 않는 관찰 상태 연결 |
| [비정상 업무 회복 r5](../../공통/PLAN-OPERATIONS-ABNORMAL-WORK-RECOVERY/README.md) | 정상 8개·파손 2개 분리, 원 업무/사건/통제 상태 분리, 운영자 revision 결정, 보험·귀책·배상 자동 확정 금지 | 수령인 부재 대응 AWR-Q5는 아직 질문 상태. 정책을 임의 확정하지 않음 |
| 화주·화물 인계 Service와 시험 | 의뢰→화물, 완료→화주 인수의 멱등 인계. 인수와 정산 완료 분리 | 중간 단계·부분 보류 결과가 같은 관계로 관찰되는지 |
| 창고·마트 기존 UseCase와 라스트마일 인계 | 적재·피킹·포장·출고 근거와 중복 방지, 상황별 배차 인계 | 전 과정에서 수량·예약·실제 인계가 보존되는지, 불량 격리와 후속 정책의 실제 구현 범위 |

문서 안의 옛 ‘아직 없음’ 문장보다 후속 구현 절·현재 코드를 우선 대조한다. 예를 들어 비정상 업무 기획 앞부분의 ‘검토 Command 없음’은 뒤의 r5 구현 및 현재 `비정상운송사건운영UseCase`·시험과 시점이 다르다. 이를 미구현 항목으로 다시 만들지 않는다.

## 5. 보완 순서 제안

1. **P0 관찰 정합성:** G1~G3를 실패 재현 시험으로 고정하고 작은 계약·Adapter 수정으로 닫는다. 운영 상태·DB를 변경하는 대형 리팩터링은 하지 않는다.
2. **P1 음식 정상 경로 실제 화면:** 기존 역할 앱 실행기를 재사용한다. 실제 인증 API→주문/배차 원장→진행 사본→배치 계획→World 객체→완료 연결까지 같은 건으로 확인한다. `cursor=0`만 사용하는 실행과 실제 Controller의 증분 조회 둘 다 시험한다.
3. **P2 다른 정상 경로의 연결표:** 화주·화물, 창고, 마트를 각기 독립한 한 건으로 검증한다. 기존 Controller·UseCase·전용 Reader를 재사용하고 없는 중간 사본·매핑·시험만 보완한다. 모두의 기반을 새로 만드는 작업은 제외한다.
4. **P3 예외 회복 결속:** 정상 경로 확인 후 기존 판본 충돌·통신 복구·조리/배송 지연·부분 파손/격리를 같은 사건/업무 연결로 보여준다. 미승인 환불·반송·배상·보험 처리는 개발하지 않는다.
5. **P4 다른 행정동 재사용:** 면목제3·8동/사가정 기준판에서 통과한 구성만 다른 동으로 옮긴다. 지역 전환은 업무 실행을 멈추거나 원장을 재생성하지 않는다.

P0·P1에서는 기존 공간 배경을 유지하므로 사진·고급 모델링·30개 행정동의 전체 정밀화 완료를 기다릴 필요가 없다. 현행 지리 자료·권리·배포 관문은 그대로 유지한다.

## 6. API 검증 시 반드시 분리할 증거

- Controller 메서드 직접 호출 시험과 실제 HTTP의 인증·권한·라우팅·기능 플래그 시험을 구분한다.
- HTTP 200만 보지 않고 역할 변경/403, 조회 범위/404, 잘못된 revision·중복 요청, 상태 전이 후 독립 원장 재조회, Event·Outbox 재처리를 확인한다.
- 원장 성공과 관찰 발행 실패를 분리한다. 관찰 실패가 원 업무 재실행을 유발하지 않아야 한다.
- 같은 revision의 긴 조리 대기, 동시 갱신, 삭제 뒤 역순 응답, 부분 자료원 실패, 정상 재접속을 포함한다.
- 일반 30초 관찰은 사이에 지난 모든 순간을 보장하지 않는다. 검증 실행기는 각 주요 단계가 한 번 이상 조회될 때까지 기다리거나 단계 이력과 대조한다. 실제 업무를 화면 검증 때문에 지연시키지는 않는다.
- 정상 종료·취소·부분 인수·보류·정산 완료는 서로 다른 결과다. 배송 완료 화면을 정산 지급 증거로 사용하지 않는다.
- Unity 실행은 canonical `SimulationWorldShell`의 최초·진행·대기·완료·복귀 캡처, 실제 입력, 객체 수·Console과 연결한다. 현재는 실제 Editor/Play Mode/Game View를 다시 실행하지 않았다.

## 7. 이번 검증 결과와 재현 자료

| 실행 묶음 | 결과 | 의미 |
| --- | --- | --- |
| 음식 정상·진행 사본·지역 Controller/UseCase·화물 전이/회복·창고/마트 조회·조화 Projector | 50/50 통과 | SQLite/메모리·대역을 포함한 집중 자동 시험. 실제 서버 HTTP 아님 |
| 창고 UseCase·마트 요청/라스트마일·화주/화물 인계·운영자 사건 처리 | 54/54 통과 | 첫 묶음과 창고 조회 시험 4건 중복. 전체 API 실실행 증거로 확대하지 않음 |
| Unity Client/Interpreter·OS Router·배치 계획기 | 18/18 통과 | 순수 .NET 계약 시험. 실제 Unity Editor 아님 |
| 독립 코어 진단 | G1~G3 재현 | 기존 시험이 위 경계 사례를 모두 검사하지 않는다는 근거 |

세 시험 묶음은 총 122회 실행, 중복 이름을 제외한 시험 118개이며 모두 통과했다. 이것과 별개로 진단이 결손 3개를 재현했으므로 ‘모두 정상’이라고 결론 내리지 않는다.

로컬 재현 경로: `artifacts/local/validation/os-lifecycle-api-audit-20260922/`의 TRX 3개와 `probe/Probe.csproj`, `probe/Program.cs`. 재현 명령은 `dotnet run --project artifacts/local/validation/os-lifecycle-api-audit-20260922/probe/Probe.csproj --verbosity quiet`다. 임시 진단은 제품 소스나 배포 자산이 아니다. 장기 근거는 이 문서의 입력·출력·소스 위치로 남긴다.

주요 소스: Hongdal `Ssalddel/Application/WorldProjection/운영지역장면조회UseCase.cs`, `Ssalddel/Application/Food/진행중음식배달WorldProjectionReader.cs`, `Ssalddel.Unity/Runtime/WorldProjection/OperationalWorldSceneInterpreter.cs`, `OperationalWorldScenePlacementPlanner.cs`, `eng/Ssalddel.RoleAppHeadlessE2E/Program.cs`; 별도 Unity 저장소 `Assets/Ssalddel/Bootstrap/운영관찰WorldController.cs`, `Assets/Ssalddel/Runtime/World/다중Os생명주기검증Models.cs`.

## 확정 / 미정 / 다음 질문 하나

- 확정: 기존 정상 생명주기/API와 예외 기획을 재사용한다. API와 Unity의 부족한 연결부터 보완하는 방향을 기록한다.
- 미정: 개별 수정 승인·정확 쓰기 경로·기획 hash, 진행→완료의 가명 연결 계약, OS별 미응답 예외 정책. 이번 조사를 실제 운영 권한이나 새로운 Goal 활성화로 해석하지 않는다.
- 다음 질문: **먼저 재현한 관찰 결손 3개와 음식 단계 매핑을 고친 뒤, 기존 음식 주문 실행기를 Unity 실제 화면까지 연결할까요?** 추천은 이 범위다.
- 출처·규칙 판정: 이번 자료는 현재 소스·기존 판본 문서·자동 시험이다. 새 지리 원본·현장 반복 관찰은 없다. 새 디오라마 규칙 후보 없음. 기존 권위·출처·비공개 경계와 E 상태를 유지한다.
