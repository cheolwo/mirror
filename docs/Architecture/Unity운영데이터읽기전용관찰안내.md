# Unity 운영 데이터 읽기 전용 관찰 안내

## 한 문장 기준

> 운영 서버가 실제 업무 상태를 확정하고, Unity는 권한과 개인정보가 정리된 판본 있는 상태 사본을 읽어 `SimulationWorldShell`에 공간적으로 표현할 뿐 운영 원장과 업무 상태를 변경하지 않는다.

이 문서는 새 권위 규칙을 만드는 문서가 아니라, 저장소에 흩어진 운영 데이터→Unity 읽기 전용 관찰 기준과 구현 위치를 한곳에서 찾기 위한 안내다. 세부 내용이 충돌하면 실제 계약·route·시험과 아래 기준 문서를 다시 확인한다.

## 용어를 먼저 구분한다

| 용어 | 뜻 | 권위 |
| --- | --- | --- |
| 운영 원장 | 실제 주문·입고·재고·배차·운송·정산과 권한을 저장하는 서버 상태 | 운영 서버 |
| 운영 상태 사본 | 서버가 권한·공개 범위·개인정보를 적용해 만든 비식별 읽기 결과 | 운영 서버가 생성하고 Unity는 소비 |
| `OperationalSnapshot` | Unity 화면 모듈이 운영 상태 사본을 선택한 자료원 모드 | 읽기 전용 |
| `SimulationSession` | 가상 NPC·시간·Tick·Save/Replay를 가진 게임 상태 | Simulation Core |
| World Projection | 서버 또는 Simulation 상태를 공간·객체·패널·행동 cue로 번역한 표현 | 상태 확정 권위 없음 |
| Unity OS 관찰 모듈 | OS 단계·인계·대기·보류·회복을 NPC·시설·차량·화물로 보여주는 표현 모듈 | 상태 확정 권위 없음 |

`OperationalSnapshot`과 `SimulationSession`은 같은 표현 어휘를 재사용할 수 있지만 같은 상태가 아니다. 화면 모듈은 둘 중 하나를 명시적으로 선택하며 실패 시 다른 자료원으로 자동 대체하지 않는다.

## 책임 경계

| 책임 | 담당 | Unity에서 하지 않는 일 |
| --- | --- | --- |
| 실제 업무 확정 | 서버 UseCase·Command, 권한·revision 검증, DB·Event·Outbox | GameObject·애니메이션·NPC 도착으로 성공 확정 |
| 읽기 사본 생성 | 서버 Projection API | EF Entity·MongoDB 문서를 Unity에 직접 노출 |
| 전송·정합성 | 인증 GET, schema, cursor, revision, TTL, tombstone | Command 전송, 운영 자료 로컬 저장·재생 |
| 의미 해석 | Mapper·Adapter·관점별 조회 결과 | 알 수 없는 상태·귀책·공간을 임의 추론 |
| 공간 표현 | `SimulationWorldShell`의 Presenter·View·`VisualRoot` | 운영 원장 변경, 실제 Actor 순간이동 |
| 정밀 입력·민감 업무 | Web·모바일 업무 화면 | 주소·연락처·계좌·증빙 원문을 월드 객체로 공개 |

## 전체 흐름

```text
운영 DB·MongoDB·Event
  → 서버 권한·공개 범위 판정
  → 비식별 Projection API
  → Unity 인증 GET 전송
  → JSON 해독
  → cursor·revision·TTL·tombstone 메모리 반영
  → OS 단계·인계·보류 관찰 상태 Adapter
  → 공간·객체 결속 대장
  → Presenter·View
  → SimulationWorldShell
```

## `SimulationWorldShell`의 관리 중심은 OS다

운영 상태 사본의 각 항목은 `OperatingSystemId`를 가지며 Unity는 이를 첫 모듈 분류축으로 사용한다. 공용 지역 상태 사본을 받은 뒤 OS Router가 화물운송·창고 이행·마트 도심물류·음식배달 같은 관찰 모듈에 분배한다. 각 모듈은 자기 업무 인스턴스의 단계·대기·인계·보류와 표현 투영을 관리한다.

OS 중심이라는 말은 OS별 Scene·서버 Client·전역 Manager를 따로 만든다는 뜻이 아니다. 공용 전송·해독·revision 정합성을 함께 쓰고, OS별 의미 해석만 모듈로 분리한다. OS 간 인계는 모듈 직접 호출이 아니라 안정 인계 ID의 읽기 전용 관계 투영으로 연결하며 공간 좌표는 Graph Map·배치맵 결속이 소유한다.

Unity는 원문 JSON과 해석 결과를 `PlayerPrefs`, 게임 Save 또는 Replay에 저장하지 않는다. 최초 조회 실패와 성공 뒤 갱신 실패를 구분하고, 갱신 실패 때는 마지막 성공 표현을 유지하면서 오류 상태만 알린다.

## OS를 Unity에서 표현하는 방법

OS 하나를 건물 하나, Manager 하나 또는 Scene 하나로 만들지 않는다. 한 업무 인스턴스를 필요한 표현으로 나눈다.

```text
OS 업무 인스턴스
├─ Actor: 일하거나 기다리는 NPC
├─ Facility: 음식점·마트·창고·인수 지점
├─ Vehicle: 오토바이·화물차
├─ WorkObject: 주문 묶음·상자·팔레트·화물
└─ Panel: 현재 단계·대기 이유·다음 예정·자료 상태
```

공통 관찰 상태는 정상 진행, 대기, 확인 필요, 보호 보류, 회복 중, 완료와 자료 지연·결손을 구분한다. 신고나 빨간색 표시만으로 기사·화주·음식점·작업자의 귀책을 확정하지 않는다. 공간 결속 근거가 없는 객체는 임의 좌표에 만들지 않고 운영지도 또는 미배치 목록에 남긴다.

## 현재 구현 지도

| 단계 | 현재 위치 | 상태 |
| --- | --- | --- |
| 운영 지역 상태 사본 조합 | `운영지역장면조회UseCase` | 음식 완료·창고 작업/Actor·화물 상태 자료원 조합 구현 |
| 공통 상태 사본 계약 | `OperationalWorldSceneContracts.cs` | 기본 v1 호환과 opt-in v2의 업무·생명주기·주의 상태·의미 위치·표본 출처 계약 구현 |
| Unity GET 경계 | `OperationalWorldProjectionTransportContracts.cs` | 인증 GET 전용, Command 없음 |
| Unity 전송 구현 | `UnityWebRequestOperationalWorldProjectionTransport.cs` | 구현됨 |
| JSON 해독 | `UnityJsonOperationalWorldSceneDecoder.cs` | 구현됨 |
| 메모리 정합성 | `OperationalWorldSceneInterpreter.cs` | cursor·revision·TTL·tombstone·부분 자료원 실패와 v2 객체별 동결·회복 처리 구현 |
| 일반 객체 변경 계산 | `WorldProjectionReconciler.cs` | 안정 ID 기반 추가·갱신·제거 구현 |
| 역할·객체 원형 후보 | `OperationalUnityTransferObjectCatalog`, `운영역할GameObjectCatalogPolicy` | 후보·생성 금지 경계 구현 |
| OS 단계·인계·보류 공통 Adapter | `OperationalOsObservationRouter`, `OperationalWorldScenePlacementPlanner` | OS·업무 단계·주의 상태를 안전한 배치 지시로 변환 |
| 실제 GameObject 결속 | `SimulationWorldShell/OperationalOsWorldRoot` | primitive 안정 ID 객체 조정과 30초 읽기 전용 갱신 구현 |

구현됨은 코드·계약 또는 해당 범위 시험이 존재한다는 뜻이다. 실제 서버 연결, Play Mode와 Game View 증거는 실행할 때마다 별도로 기록하며 코드 존재만으로 이를 대신하지 않는다.

## 합성 운영 검증 수직 조각

[관찰 가능한 운영 디오라마 r2](../AI/Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/README.md)는 실제 영업 효과 없이 위 파이프라인을 실행하기 위한 opt-in 검증 경계다. `Development + Simulation + 전용 컨테이너 + 전용 MySQL/MongoDB/Redis + 600초` 조건을 모두 만족해야 켜지고, 로컬 loopback 읽기와 상태·일시정지·재개·실패 Outbox 재시도만 제공한다.

표본은 음식 배달·화물 운송·창고/마트의 정상 완료와 회복 완료 각 한 건이다. `sourceKindCode=VerificationSample`과 `scenarioRunStableId`로 실제 운영 투영과 구분하며, 이름·전화번호·정확 주소·GPS·토큰과 로컬 저장·Replay는 허용하지 않는다. 이 검증 성공을 개별 OS의 실제 업무 API·권한·현장 운영 완료 증거로 해석하지 않는다.

## 문서별 역할

처음 읽을 때는 다음 순서면 충분하다.

1. [공용 프로젝트 컨텍스트](../ProjectOverview/GptProjectContext.md): 제품 전체에서 서버·Simulation·Unity가 맡는 역할
2. [운영·Simulation·Unity 작업 흐름 분리](OperationsSimulationUnity작업흐름분리.md): 상태 소유권과 작업·커밋 책임
3. [서버 상태에서 Unity World Projection으로의 설계](UnityServerStateToWorldProjectionDesign.md): DB·MongoDB에서 Projection·UseCase·Presenter·View로 가는 변환 원칙
4. [운영 서버에서 Mirror Unity로의 이관](../AI/Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md): 현재 이관 등급·객체 원형·지역 장면 구현 상태
5. [Unity OS 관찰 모듈 상향식 계획](../AI/Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/unity-os-observation-modules.r4.md): 서버 OS 생명주기부터 Unity 메모리 인계·공간·Prefab까지의 OS별 수직 관문
6. [비정상 업무 처리·회복](../AI/Planning/공통/PLAN-OPERATIONS-ABNORMAL-WORK-RECOVERY/README.md): Unity가 임의로 만들면 안 되는 보류·일부 인수·회복의 운영 의미

역할별 보조 문서는 다음과 같다.

- [음식 배달 기사 앱과 Unity NPC 경계](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/driver-role-boundary.r2.md)
- [화물 기사 앱과 Unity NPC 경계](../AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/freight-driver-role-boundary.r1.md)
- [음식 배달 완료 상태 사본](../AI/Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/food-delivery-completed-world-projection.r1.md)
- [관찰 중심 개인 세계](../AI/Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/README.md)

## 금지하는 지름길

- Unity에서 운영 DB나 MongoDB를 직접 조회한다.
- Unity가 운영 OS 상태를 별도로 저장하거나 전진시킨다.
- `OperationalSnapshot` 실패를 `SimulationSession` 표본으로 숨긴다.
- 실제 사용자 ID·정밀 위치·주소·연락처·계좌·평점·증빙 원문을 배포 자원이나 GameObject에 넣는다.
- 서버가 확정하지 않은 일부 인수·환불·재배송·귀책을 Unity가 먼저 표현한다.
- 애니메이션 완료나 NPC 도착을 주문·배차·운송 완료로 기록한다.
- OS별 공식 Scene이나 전역 Manager를 늘린다.

## 개발 시작 기준

읽기 전용 관찰 모듈은 OS 하나씩 다음 순서로 좁게 개발한다.

1. 운영 서버의 정상 생명주기·권한·저장·멱등·재시작 검증
2. 완료 상태 사본의 발행·비식별·지역·revision·TTL 검증
3. 인증된 HTTP 재조회와 Unity 전송·해독·메모리 정합성 검증
4. Unity 안전 OS 관찰 계약·Router·OS Adapter
5. 공간·객체 결속 대장
6. primitive Presenter·View
7. canonical `SimulationWorldShell` 실제 결속
8. Unity import·Play Mode·Game View 검증
9. 검증된 primitive를 Prefab·애니메이션으로 교체·고도화

실제 운영 생명주기의 첫 기준 표본은 이미 정상 완료 이력과 완료 사본이 있는 `FoodDeliveryOS`다. 합성 검증 호스트의 여섯 표본은 여러 OS의 투영·인계·표현 파이프만 함께 확인하며, 각 OS의 실제 업무 API·권한 시험을 대신하지 않는다.

## 후속 변경 경계

현재 기준은 운영 데이터에 대해 완전한 읽기 전용이다. 장래 Unity에서 실제 업무 변경 진입이 필요해도 이 관찰 모듈에 Command를 덧붙이지 않는다. 별도 기획에서 `Preview → 명시적 확인 → 서버 Command → canonical 재조회`를 갖춘 개별 WI로 검토하며, 민감하고 정밀한 업무는 Web·모바일 인계를 기본으로 유지한다.
