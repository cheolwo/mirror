# 업무 흐름 Runtime

## 목적

`Ssalddel.Simulation.BusinessWorkflow`는 주문·음식점·배차·배송·창고의 가상 업무를 Local 또는 원격 Simulation에서 같은 계약으로 소비하기 위한 Simulation 전용 Runtime이다. 사람이 읽는 이름은 **Simulation 업무 흐름 Runtime**이며, 실제 기사 업무 앱의 운영 API 계약과 workflow 인터페이스는 이 Runtime과 분리한다. 기존 `Ssalddel.BusinessWorkflow` assembly·namespace는 기존 Unity package와 소비자 호환을 위한 facade로만 남긴다.

이 Runtime은 기존 업무 상태를 새로 소유하지 않는다. 로컬에서는 기존 Simulation Core를, 원격에서는 기존 Simulation HTTP API를 조립해 한 facade로 제공한다.

## 프로젝트 경계

| 프로젝트 | 책임 |
| --- | --- |
| `Ssalddel.Simulation.Contracts` | 상태 사본·요청·결과와 `ISimulationLogisticsRuntime` 계약 |
| `Ssalddel.WorkflowRules.Contracts` | 순수 업무 규칙 계약과 선택적 분류 메타데이터 |
| `Ssalddel.WorkflowRules` | 상태 전이·객체 결속의 결정적 규칙 |
| `Ssalddel.Simulation.BusinessWorkflow` | 다섯 Simulation 업무 포트와 정식 `IBusinessWorkflowRuntime` facade |
| `Ssalddel.BusinessWorkflow` | 기존 namespace 소비자를 정식 Simulation facade로 연결하는 호환 계층 |
| `Ssalddel.Simulation.Application` | `LocalProcess` 조립 |
| `Ssalddel.Simulation.Infrastructure` | `RemoteHost` HTTP 조립 |
| `Ssalddel.Simulation.Hosting` | 단일 `Ssalddel` 호스트에 Simulation Controller·Hub·필터를 조립하는 비실행 모듈 |
| `Ssalddel.Client.Infrastructure` | 원격 Simulation 소비자용 선택적 DI 등록 |

Simulation Runtime은 다음 포트를 제공한다.

- `IOrderWorkflowRuntime`
- `IRestaurantWorkflowRuntime`
- `IDispatchWorkflowRuntime`
- `IDeliveryWorkflowRuntime`
- `IWarehouseWorkflowRuntime`

다섯 포트는 한 `IBusinessWorkflowRuntime` facade와 같은 하위 Simulation 상태 원장을 공유한다. facade를 등록했다고 별도 주문·배차·창고 상태가 생기지 않는다. 운영 서버는 이 인터페이스를 구현하지 않고 운영 전용 Controller·UseCase·Command 경계를 유지한다.

## 실행 위치 조립

```text
원격 Simulation 관찰 소비자
  -> SsalddelEndpoints:ServerBaseAddress
  -> AddRemoteSimulationBusinessWorkflowRuntime(...)
  -> 같은 로그인 Bearer token을 쓰는 RemoteHost HTTP adapter
  -> 단일 Ssalddel 호스트의 Simulation Hosting 모듈
  -> 기존 Simulation Core와 분리된 Session 상태 권위

Unity Solo
  -> SimulationWorldLocalRuntimeScope.BusinessWorkflow
  -> LocalBusinessWorkflowRuntimeFactory
  -> 같은 LocalSimulationRuntime과 Session Aggregate
```

Local과 Remote는 명시적으로 선택한다. 원격 호출 실패를 로컬 실행으로 자동 전환하지 않으며, 인증·`HttpClient` 수명·재시도 정책은 기존 호스트 조립자가 소유한다. 원격 Simulation은 이름 있는 `Ssalddel.Simulation.Api` 논리 클라이언트를 사용하고 운영 API와 같은 `SsalddelEndpoints:ServerBaseAddress` 및 로그인 JWT를 공유한다. 다만 운영 API용 기본 `HttpClient`를 암묵적으로 소비하지 않는다. 기존 `AddRemoteBusinessWorkflowRuntime`는 호환 진입점으로만 남고, 명시적인 Simulation API 등록 없이 호출하면 조립 단계에서 중단된다.

Unity의 `MonoBehaviour`는 Runtime을 소유하거나 업무 상태를 판정하지 않는다. 기존 상태 사본을 읽고 NPC·건물·화물의 표시와 수명만 관리한다.

Runtime descriptor는 `AuthorityScopeCode=SimulationSession`, `ExperienceRoleCode=AutonomousNpcWorld`, `AllowsOperationalDriverActions=false`, `ObservationPresentationOnly=true`를 고정한다. `RemoteHost`는 원격 Simulation Host를 뜻하며 FDriver의 실제 수락·위치·픽업·전달 서버가 아니다. 운영 기사 앱은 `IFoodDeliveryDriverApiService`와 인증된 운영 API를 사용한다.

## 운영 서버 클라이언트 경계

Web·MAUI와 Unity의 운영 관찰 기능은 Ssalddel 운영 서버를 같은 권위 원본으로 보되, 실행 환경별 전송 계층을 사용한다.

| 소비자 | 설정 키·접속 경계 | 허용 범위 |
| --- | --- | --- |
| Web·MAUI | `SsalddelEndpoints:ServerBaseAddress`, `AddSsalddelOperationalApiHttpClient` | 기존 인증·Controller API에 따른 운영 조회와 승인된 Command |
| Unity 운영 관찰 | `OperationalWorldProjectionEndpoint`, `IOperationalWorldProjectionTransport` | 승인된 World Projection의 GET 조회만 허용 |
| 원격 Simulation | `SsalddelEndpoints:ServerBaseAddress`, `AddRemoteSimulationBusinessWorkflowRuntime` | Simulation 전용 HTTP 계약 |

운영과 원격 Simulation 논리 클라이언트는 한 물리 주소를 쓰지만 계약과 상태 원장은 서로 대체하지 않는다. 운영 API 실패를 Simulation 계약으로, 원격 Simulation 실패를 운영 계약이나 Local Runtime으로 자동 전환하지 않는다. Unity의 운영 전송 계약에는 POST·Command·쓰기 메서드가 없으며 접근 토큰은 실행 중 메모리 공급자에서만 받는다. `WorldProjectionSourceSelection`은 화면 모듈마다 `OperationalSnapshot` 또는 `SimulationSession`을 명시하고 자동 fallback을 거부한다.

운영 서버 기능·판본 확인은 `GET api/v1/version-feature-flags`를 공통 route 계약으로 사용한다. 이 조회가 성공해도 개별 업무 Command 권한이나 Unity 표현 준비를 의미하지 않는다.

## 오행·괘상 분류의 위치

오행·괘상은 `WorkflowClassificationMetadata`에 남는 **선택적 설명 메타데이터**다.

- `IsExecutionAuthority`는 항상 `false`다.
- 분류 유무나 분류 값은 상태 전이, 객체 결속, 배차, HTTP route 선택을 바꾸지 않는다.
- 기존 JSON의 분류 필드는 호환 입력으로 읽을 수 있지만 일반 실행 계약의 필수값이 아니다.
- 새 실행 타입·메서드·파일 이름은 `BusinessWorkflow`, `WorkflowRule`, `BusinessObjectInteraction` 같은 보편 용어를 사용한다.

따라서 분류는 문서·검색·분석에 활용할 수 있지만 실행 권위는 계속 Controller·UseCase·Command·Domain·Simulation Session·DB/Event/Outbox에 있다.

## 안전 경계

- 정식 Simulation Runtime project는 Unity, MongoDB, ASP.NET DI, `HttpClient`와 기존 호환 assembly를 직접 참조하지 않는다.
- 기존 호환 assembly는 정식 Simulation Runtime만 향하고 생산 조립 코드는 호환 assembly를 참조하지 않는다.
- 규칙 엔진은 후보·허용 여부·진단을 반환할 뿐 상태를 저장하지 않는다.
- 운영 효과와 로컬 Simulation을 자동 혼합하지 않는다.
- Unity Scene·Prefab·Save는 이 조립만으로 생성하거나 변경하지 않는다.
- 실제 서버 연결, Unity Play Mode와 Game View는 코드·단위 시험과 별도 증거다.
