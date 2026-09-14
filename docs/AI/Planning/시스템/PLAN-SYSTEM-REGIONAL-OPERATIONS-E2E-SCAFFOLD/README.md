[기획 · 시스템·운영 통합·지역 확장 · PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD · r6]

# 지역 운영 생명주기 E2E·전국 확장 뼈대 제안

- 기획 ID: `PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD`
- 기획 분야: 시스템·운영 통합·지역 확장
- 기획 판본: `r6`
- 상태: `Proposed / ReadyForReview / SagajeongFirstProfile / WindowsThreeAppFirstConfirmed / NationwideSkeletonProposed / OperationalEffectsDisabled / UnityReadOnly / AvailableActionsInlineConfirmed / FoodDeliveryAvailableActionsFirstSliceImplemented / IsolatedServerApiProofPassed / RoleAppHeadlessE2EPassed / ActiveRegionalProjectionImplemented / ActiveRegionalProjectionHttpProofPassed / UnityClientInterpreterLiveHttpProofPassed / WindowsLocalEndpointOverridePrepared / DeviceUiProofBlockedByNativeControlUnavailable / UnityEditorProofDeferred / NationwideSkeletonImplementationNotStarted`
- 사용자 요구: 사가정역을 첫 깊은 표본으로 유지하면서 합성 운영 자료로 주문자·음식점·음식 배달 기사 역할 앱의 E2E를 검증하고, 같은 진행을 Unity 디오라마에서 읽을 수 있게 하되 이후 전국 지역으로 확장 가능한 얇은 뼈대를 먼저 준비한다.
- 상위 기획: [지역 Experience Package](../PLAN-SYSTEM-REGION-EXPERIENCE-PACKAGES/README.md)
- 생명주기 의미 선행: [OS 생명주기 Core와 환경별 Adapter](../PLAN-SYSTEM-OS-LIFECYCLE-ENVIRONMENT-ADAPTERS/README.md)
- 샘플 원장·timeline 기준: [관찰 가능한 운영 디오라마](../PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/multi-os-lifecycle-playback.r4.md)
- 운영→Unity 권위 기준: [운영 서버에서 Unity로의 이관](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)
- Unity 소비자: [역세권 디오라마 모듈](../PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/README.md)
- 공간 정본: [행정동별 서버 기반 디오라마](../PLAN-SYSTEM-ADMIN-DONG-DIORAMA/README.md)
- 관련 WI·PlayableLoop: 새 WI 미등록. 운영 `FoodDeliveryOS` E2E와 Unity 읽기 전용 관찰을 연결하는 시스템 검증이며 기존 게임 WI를 자동 활성화하지 않는다.
- Graph Map 영향: `UpdateExistingCandidate`. 구현 승인 전에는 변경하지 않으며, 승인 뒤에도 기존 사가정 의미 장소에 `ReadOnlyProjection` 결속만 추가하는 후보로 제한한다.
- 다음 질문: G6 실제 UI 증거를 닫기 위해 네이티브 Windows 앱 표면이 제공되는 실행 Host에서 세 앱 자동 조작을 다시 수행할지 확정해야 한다.

## 1. 한 문장 제안

사가정 전용 주문 시스템을 새로 만들지 않는다. 전국 공통 `FoodDeliveryOS`와 기존 역할별 운영 API를 그대로 두고, **지역 Profile·검증 Fixture·E2E 증거라는 세 신규 정본이 기존 앱 메타데이터·공간 결속을 안정 ID와 hash로 참조하는 얇은 결속층**만 추가한다.

```text
주문자·음식점·기사 역할 앱
  → 기존 운영 Controller·UseCase·Command
  → 격리 Operations DB·Event·Outbox
  → 비식별 지역 관찰 사본
  → Region Experience Package
  → Unity 역세권 모듈의 읽기 전용 표현
```

앱과 Unity가 한 상태 저장소를 공유하는 구조가 아니다. 앱은 서버에 업무 요청을 보내고 같은 원장을 재조회하며, Unity는 서버가 별도로 정제한 관찰 사본만 읽는다.

## 2. 지금·여기·나·너·이렇게

- 지금: 합성 주문 한 건이 아직 시작되지 않았고, 주문자·음식점·기사 역할이 각자의 앱에서 같은 업무를 이어받을 준비를 한다.
- 여기: `world-region:kr:seoul:jungnang:sagajeong.r1` 사가정 경험 범위와 `station:kr:kric:s1107:0722` 역세권 관찰 모듈이다. 이 1km 창은 운영 배달권이나 행정 경계가 아니다.
- 나: 주문자 앱에서는 주문자, 음식점 앱에서는 음식점 작업자, 기사 앱에서는 합성 검증 기사다. Unity에서는 이들과 다른 읽기 전용 관찰자다.
- 너: 같은 `FoodDeliveryOS` 업무를 넘겨받는 음식점·기사·주문자와, 그 결과를 정제해 내보내는 운영 서버다.
- 이렇게: 각 역할은 자기 권한의 기존 API로 한 단계씩 처리하고 성공 직후 canonical 원장을 재조회한다. 서버는 비식별 관찰 사본을 발행하고 Unity는 같은 인과 계보의 최신 projection을 표현한다.
- 결과: 앱 화면, 서버 원장, Event·Outbox, 지역 projection과 Unity 카드가 같은 실행에서 어디까지 일치했는지 증거 manifest로 확인한다.
- 귀환: 수령 확인 뒤 각 앱은 완료 원장을 다시 읽고, Unity는 완료 또는 제거 사본을 표현한다. NPC 도착이나 Animation 종료는 완료를 만들지 않는다.

오행·괘상은 이번 통합 구조의 실행 권위나 시험 판정에 사용하지 않는다. 필요하면 후속 표현 메타데이터로만 연결한다.

## 3. 현재 저장소에서 이미 준비된 것

| 기반 | 현재 확인된 범위 | 이번 제안이 메우는 결손 |
| --- | --- | --- |
| `FoodDeliveryOS` | 주문·음식점 응답·조리·배차·픽업·전달·취소 보상·중단 회복의 공통 단계 의미가 있다. | 세 역할 앱과 지역 관찰이 한 실행 증거로 연결돼 있지 않다. |
| 운영 서버 | 주문 등록→음식점 수락·픽업 준비→기사 수락·픽업·전달→주문자 수령 확인과 완료 Outbox·익명 투영이 있다. | 진행 중 생명주기의 안전한 지역 관찰 projection은 완료 사본보다 좁다. |
| `FoodObserver` | 격리 Development/Simulation 환경에서 실제 로그인과 정상 역할 API, MySQL 재조회를 관통한다. | raw `HttpClient` Runner이며 앱 Client·ViewModel·실제 UI와 Unity를 실행하지 않는다. 자체 `_revision`도 주문 revision이 아니다. |
| 역할 앱 | 주문자 Client/ViewModel, 음식점 수신·수락·준비 흐름, 기사 workspace·수락·픽업·전달 Client가 있다. | 주문자 취소, 기사 가게 도착·중단·회복 등 일부 역할 기능이 앱에 아직 없다. |
| 관찰 가능한 운영 r4 | 음식·화물·창고·마트 8사례·77단계와 비공개 Fixture를 결정적으로 재생한다. | 실제 운영 Controller/UseCase/Command나 앱 UI를 호출한 증거가 아니고 사가정 ID·좌표 고정 부분이 있다. |
| 앱–관찰 대장 | `OrdererApp`, `RestaurantDeskApp`, `FoodDeliveryDriverApp`과 합성 음식 생활의 `SimulationAnalog`가 있다. | 실제 격리 운영 결과를 보는 `ReadOnlyProjection` Profile은 별도다. 두 관계를 같은 것으로 바꾸면 안 된다. |
| 사가정 r20 | 단일 합성 기사의 선택 카드·표시 토글·Simulation revision 복구가 있다. | 실제 역할 앱 E2E나 운영 projection과 연결된 기능은 아니다. |
| 지역 Experience Package | 지역 Catalog·Manifest·레이어·ETag/hash 계약과 첫 사가정 Package가 있다. | 현재 Catalog와 일부 Fixture가 코드 고정이며 전국 shard·Profile 공급 구조는 아직 없다. |

현재 `OrdererApp`과 `RestaurantDeskApp`은 Windows 대상 MAUI Blazor Hybrid이고, `FDriverApp`은 Android·iOS·macOS·Windows 대상 MAUI다. 따라서 “세 모바일 앱 실제 장치 E2E”가 이미 있다는 표현은 하지 않는다. 공통 Client/ViewModel E2E와 플랫폼별 실제 UI E2E를 분리해야 한다.

## 4. 가장 중요한 구조 원칙

### 4.1 업무는 전국 공통이고 지역은 문맥이다

- `FoodDeliveryOS`, 단계 ID, Command와 운영 원장은 역마다 복제하지 않는다.
- 주문번호와 `WorkStableId`에 사가정이나 역 코드를 넣지 않는다.
- 주소·배달권·행정동·경험 범위·역세권은 별도 관계로 결속한다.
- 사가정역은 첫 검증 Profile이자 표현 창이지 주문 상태의 파티션·권위·tenant가 아니다.

### 4.2 운영 검증과 게임 Simulation을 합치지 않는다

- 역할 앱 E2E는 기존 운영 Controller·UseCase·DB 경로를 사용하되 격리된 `Development + SsalddelExecution:Mode=Simulation`에서 외부 효과를 막는다.
- `SimulationSession`의 자율 NPC 생활은 별도 권위다.
- 같은 업무 의미를 재사용할 수 있지만 운영 주문과 합성 게임 주문의 ID·revision·Save/Replay를 공유하지 않는다.
- 운영 검증 자료는 `ReadOnlyProjection`, 자율 생활은 기존 `SimulationAnalog` 관계를 사용한다.

### 4.3 한 개의 만능 DTO·원장을 만들지 않는다

지역, 앱 능력, Fixture, 공간 결속과 증거를 각자 판본화한다. 변경 주기가 다른 자료를 하나로 합치면 지역 자료 갱신이 OS 정의 판본을 올리거나 앱 화면 변경이 Unity 공간 hash를 무효화한다. 새 정본·생성 투영·외부 정본 참조는 모두 `schemaVersion + stableId + revision + hash`를 가져 같은 이름의 다른 판본을 조용히 섞지 않는다.

## 5. 제안하는 세 신규 정본과 두 재사용 구성

새로운 정본은 `지역생명주기E2eProfile`, `지역검증FixturePack`, `E2e실행EvidenceManifest` 세 개로 제한한다. 앱 능력은 기존 API metadata와 앱 대장에서 생성하고, 공간 해석은 기존 기획의 `OsLifecycleSpatialBinding`을 재사용한다. 이름은 구현 전 충돌을 다시 확인할 후보이며 새 Entity나 공개 API를 즉시 추가한다는 뜻이 아니다.

### 5.1 `지역생명주기E2eProfile`

한 지역에서 어떤 OS·역할 앱·Fixture·공간 결속·관찰 Package를 함께 검증할지 참조만 묶는다.

```text
SchemaVersion
ProfileStableId
ProfileRevision
OperatingSystemRef                 # ID + definition revision + definition hash
RegionExperiencePackageRef         # stable ID + package revision + manifest hash
AdministrativeAreaRefs[]
StationStableId?                 # 표현 Anchor, 업무 권위 아님
ObservationFeedRef                 # stable ID + revision + hash
ObservationMembershipRuleRef       # 이 업무가 어느 지역 feed에 보이는지
ObservationProfileRef              # stable ID + policy/catalog revision + hash
AppObservationBindingRefs[]        # stable ID + policy/catalog revision + hash + RelationCode=ReadOnlyProjection
FixturePackRef                      # stable ID + revision + hash
ClientCapabilityProjectionRefs[]   # stable ID + revision + hash
OsLifecycleSpatialBindingRefs[]    # 기존 정본의 versioned reference
ProfileHashSha256
```

사가정은 첫 Profile일 뿐 공통 Core 이름에 들어가지 않는다. 다른 지역을 추가할 때 새 Controller·DB table·Unity Manager가 아니라 새 Profile과 자료 결속을 등록한다.

`ObservationFeedRef`와 `ObservationMembershipRuleRef`는 새 독립 원장이 아니라 기존 Region Experience manifest가 소유하는 가변 feed와 포함 규칙을 참조한다. 이 규칙은 업무가 어느 지역 관찰 feed에 포함되는지를 정한다. feed의 `SourceKindCode`는 `VerificationSample`이다. 아래의 공간 결속은 그 단계가 디오라마의 어디에 그려지는지만 정한다. 두 책임을 합치지 않는다.

기존 앱–관찰 대장의 세 `SimulationAnalog` 결속은 그대로 둔다. 구현 시 별도 운영 검증 관찰 Profile과 주문자·음식점·기사 앱의 `ReadOnlyProjection` 결속 세 개를 기존 대장에 추가하고, `지역생명주기E2eProfile` validator가 정확한 Profile 판본·hash와 `RelationCode=ReadOnlyProjection`을 강제한다. 이는 네 번째 신규 정본을 만드는 일이 아니다.

### 5.2 생성 투영 `생명주기ClientCapabilityProjection`

각 역할 앱이 어떤 계약 판본을 읽고 어떤 행위 의미를 요청할 수 있는지는 기존 API metadata·앱 catalog·Client/ViewModel 대장에서 결정적으로 생성한다. 이를 새 권위 원장이나 사람이 중복 편집하는 Profile로 만들지 않는다.

```text
SchemaVersion
CapabilityProjectionStableId
CapabilityProjectionRevision
AppCode
ExperienceRoleCode
SupportedOperatingSystemId
SupportedLifecycleDefinitionRevision
ReadableStateCodes[]
RequestableActionIds[]
ClientOrViewModelRefs[]
SupportedPlatformCodes[]
E2eDriverCapabilityCode          # None | HeadlessExperience | DeviceUi
SourceCatalogRefs[]              # stable ID + revision + hash
CapabilityProjectionHashSha256
```

이 투영은 구현·시험 범위를 보여줄 뿐 권한을 부여하지 않는다. 서버는 매 Command에서 로그인 역할·업무 소유권·현재 상태를 다시 검사한다. `ExpectedRevision`은 현행 요청 계약이 제공하는 route에서만 검사하고, 기사 제안 수락·픽업·전달처럼 아직 요청 revision이 없는 route는 그 결손을 capability 투영과 증거에 남긴다. 첫 정상 흐름 뒤 필요하면 공개 계약 호환 migration으로 보완한다.

### 5.3 `지역검증FixturePack`

합성 계정·음식점·메뉴·배달 대상·기사와 결정적 seed를 격리 환경에 공급한다.

```text
SchemaVersion
FixturePackStableId
FixtureRevision
GeneratorVersion
DeterministicSeed
RegionContextRefs[]
SyntheticActorRefs[]
SyntheticBusinessRefs[]
SyntheticMenuRefs[]
SemanticPlaceRefs[]
FixtureHashSha256
SourceKindCode=SyntheticFixture
DistributionApproved=false
OperationalEffectsAllowed=false
EnvironmentCode=DevelopmentSimulation
```

현재 실제 관측 후보 12곳과 샘플 메뉴 결속을 운영 주문 가능 상태로 뒤집지 않는다. 첫 E2E에는 별도 합성 주문 가능 Fixture를 만들고, 실제 건물은 `LocationPresentationAnchorOnly / NoBusinessAffiliation`로만 참조한다. 입력 묶음은 `SyntheticFixture`이며, 그 실행에서 파생되어 Unity가 읽는 단계·관찰 사본에만 `VerificationSample`을 사용한다.

### 5.4 기존 `OsLifecycleSpatialBinding` 재사용

업무 단계와 Unity 의미 장소의 연결은 [OS 생명주기 Core와 환경별 Adapter](../PLAN-SYSTEM-OS-LIFECYCLE-ENVIRONMENT-ADAPTERS/README.md)의 `OsLifecycleSpatialBinding`이 이미 소유한다. 이 제안에서 같은 의미의 `BindingSet` 정본을 다시 만들지 않는다. 구현할 때는 기존 결속을 `BindingStableId + BindingRevision + BindingHashSha256`으로 참조할 수 있게 만드는 최소 version envelope만 선행 기획에 재결속한다.

```text
지역 관찰 포함 여부
  → ObservationFeedRef + ObservationMembershipRuleRef

현재 단계의 표현 위치
  → OsLifecycleSpatialBinding versioned reference
```

공간 결속이 없어도 권위 업무와 지역별 집계·진단은 유지한다. 해당 객체의 지도 배치만 `WaitingForSpatialBinding`으로 남기며 가까운 건물·도로·다른 역의 좌표를 자동 대체하지 않는다.

### 5.5 `E2e실행EvidenceManifest`

한 실행의 앱·서버·저장·투영·Unity 증거를 연결하되 서로 다른 revision을 하나의 숫자로 합치지 않는다.

```text
SchemaVersion
EvidenceManifestStableId
E2eRunStableId
CaseStableId
CorrelationStableId             # 보호 영역
EvidenceManifestRevision
ProfileRef                      # stable ID + revision + hash
FixturePackRef                  # stable ID + revision + hash
RegionExperiencePackageRef      # stable ID + package revision + manifest hash
ObservationFeedRef              # stable ID + revision + hash
ClientCapabilityProjectionRefs[]
OsLifecycleSpatialBindingRefs[]
ServerBuildFingerprint
AppBuildFingerprints[]
UnityBuildFingerprint?
ExecutionEnvironmentCode
DeterministicClockRef
IsolationScope
RoleSessionEvidence[]
CommandEvidence[]
CanonicalRequeryEvidence[]
AggregateRevisionEvidence[]     # Order, DeliveryAttempt 등 각자 보존
EventAndOutboxEvidence[]
ObservationProjectionEvidence[]
ProjectionItemEvidence[]          # SnapshotStableId + projection-safe WorkStableId
UnityMemoryEvidence[]
DeviceUiEvidence[]
CleanupEvidence[]
CleanupResultCode
ResultCode
CoverageBoundary
EvidenceManifestHashSha256
```

`FoodObserver._revision`은 화면·증거 순서에 가까우므로 권위 `OrderRevision`으로 사용하지 않는다. Unity에는 원 주문번호나 사용자·기사 ID를 보내지 않는다. 공개 wire에서는 기존 `OperationalWorldSceneItem.WorkStableId`를 투영 안전 가명 ID로 사용하고, 완료 v1 호환 자료는 현행처럼 `SnapshotStableId` fallback을 허용한다. 보호된 증거 원장에서만 `CanonicalWorkStableId ↔ ProjectionWorkStableId` 대응을 보존하며 `ObservationWorkStableId`라는 병렬 wire 필드는 새로 만들지 않는다.

## 6. 식별자와 지역 계층

| 계층 | 첫 사가정 값 | 책임 |
| --- | --- | --- |
| 법정동 | `region:kr:bjd:1126010100` | 주소·법정 구역 정본 |
| 행정동 | `region:kr:hjd:1126057500` | 행정 분석·현행 경계 정본 |
| 배달권 | 기존 원장 배달권 안정 ID | 주문·운송 가능 범위와 역할 연결 |
| 교통 Anchor | `station:kr:kric:s1107:0722` | 역 식별과 관찰 중심 후보 |
| 경험 범위 | `world-region:kr:seoul:jungnang:sagajeong.r1` | 출시·탐색·표현 Package |
| 의미 장소 | 기존 `SemanticPlaceStableId` | 픽업·전달·대기 같은 업무 의미 |
| 운영 업무 | 서버의 `WorkStableId`·주문 revision | 실제 격리 업무 상태의 권위 |
| 공개 관찰 | 기존 wire의 가명 `WorkStableId`·`SnapshotStableId`·projection revision/hash | Unity에 제공 가능한 상태 사본. canonical 운영 `WorkStableId` 원문을 뜻하지 않음 |
| 검증 | `FixturePackStableId`, `E2eRunStableId`, `CaseStableId` | 입력과 한 번의 실행 증거 |

기존 `world-region:...sagajeong.r1`의 `r1`은 호환 ID 일부이므로 유지한다. 단순 자료 갱신 때 새 지역 identity를 만들지 않고 `PackageRevision`과 manifest hash를 올린다. `area:kr-seoul-jungnang-myeonmok` 같은 과거 지역 표기는 정본으로 확대하지 않고 명시적 Alias/Crosswalk 후보로 격리한다.

## 7. 저장 책임

| 저장 위치 | 소유 자료 | 금지 |
| --- | --- | --- |
| RDB 운영 영역 | 주문·음식점·기사·배달권·권한·상태 전이·Event·Outbox | Unity 좌표, GameObject, Simulation Save |
| RDB 안정 관찰 투영 영역 | 기존 음식배달 완료 사본과 후속 비식별 진행 사본의 정본·revision | 원 계정·상세 주소·정밀 GPS, Unity 쓰기 |
| RDB 검증 보호 영역 | Fixture header, E2E run, 원 ID와 가명 ID 대응, 실행 증거 | 운영 공개, 실제 지급·외부 효과 |
| MongoDB | RDB 안정 투영에서 재생성 가능한 Region Experience feed 문서·cache, projection hash, 최신 포인터 | 진행/완료 사본의 유일 정본, 원 계정·상세 주소·정밀 GPS |
| Redis | 알림·진행 heartbeat·짧은 실행 상태 | 완료 권위, 유일한 증거 |
| Region Experience Package | 정적 layer와 가변 feed의 참조·준비 상태·ETag/hash | 업무 원장 복제 |
| 역할 앱 | 화면 상태와 서버가 허용한 제한적 입력 대기 | 로컬 완료 확정 |
| Unity | hash 캐시와 최신 읽기 전용 메모리 표현 | 운영 Command, 원문 저장, 운영 Replay |

전국 Catalog는 장기적으로 모든 역을 한 응답에 넣지 않고 `대한민국 요약 Index → 시·도/권역 shard → Region summary → Experience manifest → layer`로 내려가는 Catalog v2 후보를 사용한다. 현행 평면 `Items[]` 계약은 그대로 보존하며 이 계층 계약은 `SkeletonProof`에 포함하지 않는다. 큰 정적 자료는 hash로 공동 참조하고, 가변 운영 사본만 ETag나 cursor로 갱신한다.

## 8. 첫 사가정 정상 E2E

첫 구현을 승인한다면 정상 음식배달 한 건만 닫는다.

```text
1. 지역검증FixturePack과 세 합성 역할 계정 준비
2. 주문자 앱 Client/ViewModel → 샘플 주문 등록
3. 서버 저장 → 주문자 canonical 상세 재조회
4. 음식점 앱 수신함 재조회 → 수락 → 조리 → 픽업 준비
5. 기사 앱 workspace 재조회 → 제안 수락 → 가게 도착 → 픽업
6. 기사 앱 → 전달 완료
7. 주문자 앱 → 수령 확인 → 완료 상세 재조회
8. 음식점·기사 앱도 같은 업무의 자기 권한 사본 재조회
9. Event·Outbox → 비식별 진행/완료 지역 관찰 projection
10. 지역 관찰 포함 규칙이 해당 업무를 사가정 feed에 포함하고, 공간 결속이 있는 단계만 의미 장소에 배치
11. Unity → 최신 projection-safe `WorkStableId`·projection revision/hash 표현
12. E2e실행EvidenceManifest가 앱·DB·Outbox·projection·Unity 증거를 결속
```

각 성공 뒤 재조회가 필수다. SignalR·푸시 알림은 갱신 힌트이고 상태 권위가 아니다. 서버 오류를 sample 성공 화면으로 대체하지 않는다.

## 9. 앱과 Unity의 호환 방식

```text
세 역할 앱                         Unity
──────────                         ─────
원 주문·역할별 상세                가명 업무 ID
서버가 검증하는 다음 행위 후보      현재 단계·주의 상태·표시 Cue
Command 요청                       GET 관찰 사본
canonical 재조회                   높은 projection revision만 적용
민감 주소·사용자 정보              일반화 의미 장소
```

- 앱과 Unity는 `OperatingSystemId`, lifecycle definition revision, 단계 의미와 인과 계보를 공유한다.
- 앱은 원 주문 aggregate를, Unity는 정제 projection을 읽는다.
- Unity의 `ProjectionRevision`과 hash는 별도이며 원천 `OrderRevision`, `DeliveryAttemptRevision` 계보를 보존한다.
- 같은 projection revision의 다른 hash, 낮은 revision, 다른 지역 Profile과의 교차 결속은 거절한다.
- Animation·NPC 도착·선택 카드·표시 토글은 서버 revision을 바꾸지 않는다.
- 완료 뒤 tombstone 또는 명시적 유지 기간에 따라 객체를 제거한다. Unity 로컬 Save/Replay로 운영 업무를 되살리지 않는다.

## 10. 전국 확장 규칙

1. 공통 코드에 `Sagajeong` switch, 좌표, 상호명, 행정동 코드를 넣지 않는다.
2. 새 지역은 `지역생명주기E2eProfile + Fixture provider + 기존 OsLifecycleSpatialBinding 참조 + Experience manifest`로 추가한다.
3. 역세권은 표현·탐색 단위이고 행정동·배달권·생활권을 대체하지 않는다.
4. 한 행정동·공간 tile은 겹치는 여러 역 Package가 같은 hash로 참조하며 복제하지 않는다.
5. 창 밖 이동은 `OutsideWindow`와 검증된 경계 결속으로 표시하고 다른 역 도형을 fallback하지 않는다.
6. 전국 자료 완결과 전국형 코드 구조를 구분한다. 두 번째 실제 역의 모든 건물·업체 자료가 없어도 합성 두 번째 Profile로 하드코딩 부재를 시험할 수 있다.
7. 새 지역마다 세 앱×모든 OS의 전체 E2E를 반복하지 않는다.

검증 비용은 다음 세 층으로 나눈다.

- 공통 OS·앱 계약 적합성: lifecycle·권한·revision·멱등·개인정보를 공통으로 한 번 검증
- 지역 Package 적합성: Profile·Fixture·공간 hash·결손·다른 지역 fallback 금지를 지역마다 검증
- 대표 종단 E2E: 사가정 같은 기준 지역과 릴리스 대표 지역에서만 실제 앱·서버·Unity 전체 흐름을 깊게 검증

이 구조는 지역 수가 늘어날 때 시험 수가 `지역 수 × 앱 수 × OS 수`로 폭증하는 것을 막는다.

## 11. 단계적 구현 제안

### G0 — 현행 계보 감사

- 현행 `FoodDeliveryOS` 단계·route·Client·ViewModel·주문 revision·Outbox·완료 projection을 대조한다.
- 각 Command route의 역할·소유권·현재 상태·`ExpectedRevision` 지원 여부를 표로 고정하고, 미지원 route를 지원 완료로 표시하지 않는다.
- `FoodObserver._revision`을 권위 revision으로 쓰지 않도록 증거 순서와 이름을 분리한다.
- 첫 정상 E2E는 기존 Command route로 구성할 수 있다. r2에서 호출자별 상태 사본에 작은 `AvailableActions`를 포함하는 방식을 확정하고 음식배달 첫 절편에 구현했지만, 이것만으로 뼈대·장치 E2E 완료를 선언하지 않는다.

### G1 — 최소 뼈대와 두 지역 Profile

- 세 신규 정본과 validator, 기존 metadata에서 만드는 앱 능력 투영, 기존 공간 결속의 versioned reference만 추가한다.
- 사가정 첫 Profile과 실제 자료가 없는 두 번째 합성 Profile을 등록해 공통 코드의 지역 하드코딩을 검사한다.
- 기존 앱–관찰 대장에 운영 검증 관찰 Profile과 세 앱의 `ReadOnlyProjection` 결속을 추가하고 Profile validator가 판본·hash·관계를 검사한다.
- 기존 `SimulationAnalog` 결속은 수정·대체하지 않는다.

### G2 — 사가정 격리 운영 정상 폐루프

- 별도 주문 가능 합성 Fixture를 격리 DB에 멱등하게 준비한다.
- 기존 route로 표현 가능한 실제 Controller·UseCase·Command, Event·Outbox, MySQL 독립 재조회까지 정상 한 건을 먼저 닫는다.
- 결제·외부 알림·실제 지도·지급은 Fake 또는 차단 상태를 유지한다.

### G3 — 역할 앱 Client·ViewModel Headless E2E

- raw HTTP Runner와 별도로 세 앱의 실제 Client·ViewModel 또는 그 공용 업무 Client를 호출하는 headless Experience E2E Driver를 둔다.
- 각 앱은 Command 성공 뒤 자기 권한의 canonical 상태를 다시 읽는다.
- 가게 도착이 정상 서버 전이에 필수인데 기사 앱 공용 Client에만 빠져 있다면 그 한 행위만 이 단계에서 보완한다.
- 정상 사례에 없는 주문자 취소, 기사 중단·재배차·회복은 이 단계의 선행조건으로 넣지 않는다.

### G4 — 진행 중 비식별 지역 projection

- 기존 EF/RDB 음식배달 완료 투영은 정본으로 보존하고, 진행 중 단계도 RDB 안정 투영에 별도 안전 계약으로 추가한다.
- MongoDB에는 이 RDB 투영에서 재생성 가능한 Region Experience 동적 feed/cache만 두며 이중 정본을 만들지 않는다.
- 원 ID·상세 주소·GPS·결제·평가를 제외하고 가명 ID·단계·주의·일반화 의미 장소만 내보낸다.
- 동일 revision 다른 hash, tombstone, TTL, 부분 projection 실패와 Outbox 재처리를 검증한다.

### G5 — Unity 사가정 모듈 연결

- Region Experience Manifest의 운영 사본 layer를 공용 Repository/Client가 읽는다.
- `ActiveStationDioramaHost`에는 별도 `OperationalSnapshot` Adapter로 연결하고 r20 `LifeSimulation`과 동시에 같은 객체를 갱신하지 않는다.
- 메모리 적용·지역 전환·표시 토글·선택 카드·삭제를 먼저 확인한 뒤 실제 Play Mode·Game View를 별도 실행한다.

### G6 — 플랫폼별 실제 UI E2E

- 공통 Client/ViewModel E2E와 실제 장치 UI E2E를 구분한다.
- Windows OrdererApp·RestaurantDeskApp과 Windows 또는 Android FDriverApp을 같은 run manifest에 순차 결속한다.
- UI 자동화 도구와 실행 Host는 별도 기술 조사 뒤 정하며, 현재 Appium/MAUI E2E 기반이 있다고 가정하지 않는다.

### G7 — 전국형 적합성 검증

- 실제 두 번째 역을 모델링하지 않고 두 번째 합성 지역 Profile로 같은 정상 사례를 실행한다.
- `Sagajeong` 문자열·고정 좌표·과거 `area:kr-seoul-jungnang-myeonmok`이 공통 경로에 새어 나오는지 검사한다.
- 실제 다음 지역을 열 때는 공식 행정·공간·사업장 자료의 별도 준비도를 그대로 적용한다.

정상 폐루프가 닫힌 뒤에만 누락된 앱 기능을 보완하며 거절·취소·조리 지연·기사 중단·재배차·회복과 모바일 오프라인 정책을 새 판본으로 확장한다.

## 12. 검증 행렬

| 관문 | 주문자 앱 | 음식점 앱 | 기사 앱 | 서버·저장 | Unity |
| --- | --- | --- | --- | --- | --- |
| Fixture | 샘플·비운영 고지 | 합성 음식점 고지 | 합성 기사 고지 | seed·hash·환경·외부효과 차단 | `VerificationSample / 비배포` |
| 주문 | 등록·상세 재조회 | 아직 미노출 | 아직 미노출 | 멱등 Command·revision | 게시 정책에 따라 대기 또는 주문 단계 |
| 접수·조리 | 새 상태 재조회 | 수신함·수락·준비·재조회 | 제안 대기 | 권한·순서·Event·Outbox | 같은 가명 업무의 높은 projection |
| 배차·픽업 | 상태 재조회 | 픽업 상태 재조회 | 제안 수락·도착·픽업·재조회 | 배달권·후보·시도 revision | 기사 별칭과 일반화 위치만 표현 |
| 전달·수령 | 전달 확인·수령확인 | 완료 재조회 | 전달완료·재조회 | 최종 revision·완료 Outbox | 진행→완료 또는 제거 |
| 충돌 | 전체 재동기화 | 전체 재동기화 | workspace 재조회 | 낮은/충돌 revision 거절 | 기존 표현 동결·진단 |
| 재시작 | canonical 복원 | canonical 복원 | canonical 복원 | DB·Outbox에서 재개 | 최신 GET으로 재구성, Replay 없음 |
| 개인정보 | 본인 자료만 | 소유 음식점 자료만 | 본인 제안만 | 원 ID 대응은 보호 영역 | 원 ID·주소·GPS·연락처 0건 |
| 권위 | 요청만 | 요청만 | 요청만 | Controller·UseCase가 전이 | 도착·Animation·UI로 전이 불가 |

## 13. 증거 수준별 완료 조건

### 13.1 `SkeletonProof`

- 공통 계약·validator에 사가정 문자열·좌표·상호가 없다.
- 사가정과 두 번째 합성 지역 Profile이 코드 변경 없이 같은 OS 사례를 구성한다.
- 세 신규 정본, 생성 앱 능력 투영, 기존 공간 결속 참조가 모두 `schemaVersion + stableId + revision + hash`로 검증된다.
- 같은 Profile·Fixture 입력은 같은 hash를 만들고 재적용 시 중복 계정·결속을 만들지 않는다.
- 이 수준은 실제 주문 실행, 앱 실행, Unity 연결을 완료했다고 뜻하지 않는다.

### 13.2 `SagajeongVerticalProof`

- 격리 서버에서 사가정 정상 주문 한 건이 실제 Controller·UseCase·Command·DB·Event·Outbox를 통과한다.
- 세 역할 앱의 실제 Client/ViewModel이 한 `E2eRunStableId` 아래 각자 권한 있는 Command와 canonical 재조회를 수행한다.
- 운영 aggregate별 revision, projection revision과 증거 순서를 서로 바꿔 쓰지 않는다.
- 앱 실패·DB 실패·projection 실패·Unity 거절을 sample 성공으로 숨기지 않는다.
- 같은 Fixture 재적용은 중복 계정·주문을 만들지 않으며 실행 종료 후 격리 범위와 정리 결과가 증거에 남는다.
- Unity는 가명·일반화된 진행/완료 사본만 읽고 Command·원문 저장·운영 Replay를 갖지 않는다.
- Region Experience Package 전환 시 이전 지역의 동적 객체·카드·cache가 규칙대로 해제된다.
- 이 수준은 실제 장치 UI 자동 조작을 완료했다고 뜻하지 않는다.

### 13.3 `DeviceUiProof`

- 선택한 Windows·Android 조합에서 실제 역할 화면 입력과 오류·재동기화가 같은 run manifest에 결속된다.
- 자동 시험, 실제 서버 HTTP, 앱 Client/ViewModel, 장치 UI, Unity Play Mode·Game View와 Scene 저장 여부를 서로 다른 증거 항목으로 기록한다.
- 장치 UI가 통과해도 Unity 화면이나 Animation을 운영 완료 권위로 승격하지 않는다.

## 14. 기대 효과

- 사가정에서 깊게 만든 주문·조리·배달·수령 흐름이 다음 지역에서도 코드 복사 없이 Profile 교체로 재사용된다.
- 앱 화면과 Unity 화면이 같은 서버 인과 계보를 보므로 “앱은 완료인데 디오라마는 이전 단계”인 불일치를 revision·hash로 찾을 수 있다.
- 실제 운영 자료, 검증 Fixture와 게임 Simulation이 분리되어 샘플이 실제 영업·실적·정산으로 오인되는 위험이 줄어든다.
- 공통 적합성 시험과 지역별 자료 시험이 분리되어 전국 확장 시 검증 비용이 통제된다.
- 지역 Package가 공간·생활·업체·운영 layer를 독립적으로 켜므로 자료가 덜 준비된 지역도 결손을 숨기지 않고 단계적으로 추가할 수 있다.

대가는 Profile·Fixture·공간 결속·증거 manifest를 각각 관리해야 하고, 진행 중 비식별 projection과 앱별 누락 기능을 먼저 보완해야 한다는 점이다.

## 15. 이번 제안의 제외 범위

- 코드, DB migration, API, 앱, Unity Scene·Prefab 수정
- 실제 업체·메뉴·사용자·기사·주소·GPS 수집 또는 운영 저장
- 기존 사가정 12곳 비공개 후보를 주문 가능한 실제 음식점으로 승격
- 전국 건물·업체·도로 자료 수집과 두 번째 실제 역 모델링
- 결제·송금·정산·실제 푸시·실제 지도·자동 배차 운영 활성화
- 모바일 오프라인 완료 확정
- 기존 r20 합성 Actor를 운영 주문 Actor로 변경
- Graph Map·H·AreaSet·Evidence 승격
- commit·push·배포

## 16. 확정

- 첫 깊은 검증 지역은 사가정으로 유지한다.
- 첫 업무는 `FoodDeliveryOS` 정상 한 건이다.
- 주문자·음식점·기사 앱은 같은 운영 서버를 사용하고 Unity는 비식별 읽기 전용 projection만 소비한다.
- 역은 표현 Anchor이고 주문·배달의 권위나 파티션이 아니다.
- 전국 확장은 공통 Core 복제보다 Profile·Fixture와 기존 공간 결속 참조의 교체로 준비한다.
- 전국 확장 뼈대와 E2E 증거 묶음은 여전히 제안 단계다. 다만 역할별 상태 사본의 `AvailableActions` 첫 음식배달 절편 구현은 사용자 승인에 따라 별도로 진행했다.
- 첫 장치 UI 검증 순서는 Windows 주문자·음식점·기사 3앱부터 맞추고 Android 기사 앱은 후속 장치 증거로 분리한다.
- 2026-09-14 새 전용 MySQL·MongoDB 볼륨과 최신 서버 이미지에서 정상 역할 API 폐루프를 실제 300초 실행했다. 실행 `84ed7de3ee3c48908866fb2439fa4f6d`는 합성 역할 4명·사건 15건으로 `FOOD-20260914044839651`을 `수령확인 / 배달완료`까지 진행하고 `Completed`로 종료했다.
- 이 실행은 단계별 역할 상태 응답의 `AvailableActions`와 예상 action ID를 함께 검사한다. 불일치하면 Runner가 `Failed`로 종료하므로 이번 결과는 격리 서버/API 수준의 첫 실행 증거다. 실제 Windows 앱 UI나 Unity가 실행됐다는 증거는 아니다.
- Windows 예약 포트와 겹치던 기존 loopback `5215`는 기본 `5321`로 교체하고, 검증 도구는 여전히 `127.0.0.1`만 허용한다. 이전 결과와 DB 볼륨은 삭제하지 않았다.
- Windows 정상 흐름을 첫 UI 증거로 두고 stale revision 회복은 분리한다. 다만 현재 자동화 연결에서는 네이티브 앱 상태·입력 API가 제공되지 않아 실제 클릭 검증을 시작하지 못했다.
- UI 바로 전 단계인 G3는 세 앱이 실제 사용하는 Client 소스를 링크한 headless 실행기로 검증했다. 첫 표본은 기사 수락 뒤 음식점의 오래된 revision이 `409`로 거절됐고, 명시적 음식점 상세 재조회 뒤 최신 revision을 쓰도록 보완했다. 실패 DB 볼륨은 보존했다.
- 두 번째 표본 `a21d3ec8f51b412b8a164e28beadca04`에서 `OrdererApp.SharedClient → RestaurantDeskApp.Client → FDriverApp.Client → OrdererApp.SharedClient`가 주문 `FOOD-20260914050829785`를 `수령확인 / 배달완료`로 끝냈다. MySQL 독립 재조회는 주문 1건·상태 이력 7건이다.

## 17. 미정

- 정상 절편 뒤 주문자 취소와 기사 중단·재배차·회복 중 무엇을 두 번째 사례로 열지
- 네이티브 Windows 앱을 읽고 클릭할 수 있는 UI 자동화 실행 Host
- 기존 `OsLifecycleSpatialBinding`의 version envelope·최종 서버 저장 위치와 Graph Map 생성 관계

## 18. 다음 질문 하나

**G6 `DeviceUiProof`를 닫기 위해 네이티브 Windows 앱 표면과 입력을 제공하는 실행 Host에서 주문자→음식점→기사→주문자 실제 버튼 흐름을 다시 수행할까?**

추천은 해당 Host가 준비되면 같은 `appsettings.Local.json` 격리 주소 계약과 합성 계정으로 G6만 재개하는 안이다. G5까지의 서버·Unity 메모리 증거를 다시 만들 필요는 없다.

대가는 현재 실행 Host에서는 앱 프로세스를 시작해도 UI 제어 플러그인이 브라우저 표면만 반환해 자동 로그인·클릭·화면 판독을 할 수 없다는 점이다. headless Client나 빌드 성공을 `DeviceUiProof`로 대신하지 않는다.

### r2 확정: 역할별 상태 사본의 작은 AvailableActions

- 주문자·음식점·기사 응답에 `ActionId`, `RevisionKindCode`, `ExpectedRevision?`, `ExpiresAtUtc?`, `EnvironmentRequirementCodes`만 포함한다.
- 큰 입력 schema와 실제 권한 판정은 기존 Command 계약에 남긴다.
- 앱은 버튼 노출·활성화에 이 목록을 사용하지만 Command 실행 시 서버가 역할·현재 상태·revision을 다시 검증한다.
- Unity 읽기 전용 관찰 상태 사본에는 조작 행동을 싣지 않는다.

### r3 검증: 격리 서버/API 정상 폐루프

- 최신 서버 이미지와 새 전용 DB 표본에서 주문자·음식점·근거리 기사·원거리 기사 로그인을 거쳐 주문 등록, 중복 멱등 확인, 음식점 수락·준비, 거리 기반 기사 수락, 픽업, 전달, 수령 확인을 실행했다.
- 단계별 `AvailableActions`는 주문자의 취소·수령 확인, 음식점의 조리 시간 변경·픽업 준비 완료, 기사의 제안 수락·거절·배송 완료를 각 시점에 검사하고 완료 뒤 빈 목록을 확인한다.
- 실행 결과는 `artifacts/local/verification/food-observer/result.json`에 저장했고 전용 컨테이너는 종료했다. 로컬 검증 자료이며 Git 배포 자료가 아니다.
- 증거 상한은 `IsolatedServerApiProofPassed`다. Windows 3앱 UI, 진행 중 지역 projection, Unity live HTTP·Game View, 전국 Profile은 아직 검증하거나 구현하지 않았다.

### r4 검증: 역할 앱 Client headless 정상 폐루프

- `eng/Ssalddel.RoleAppHeadlessE2E`는 주문자 공용 Client와 `OrdererApp` 인증 Client, `RestaurantDeskApp` 주문·인증 Client, `FDriverApp` 업무·인증 Client 소스를 직접 링크해 컴파일한다. 장치 보안 저장소만 비밀값을 남기지 않는 메모리 구현으로 교체한다.
- 첫 실행의 `409 Conflict`는 기사 수락으로 높아진 주문 revision을 음식점이 수락 직후의 이전 값으로 보낸 결과다. 서버의 stale revision 거절을 완화하지 않고, 음식점 상세를 재조회한 뒤 최신 `Revision`으로 픽업 준비를 요청한다.
- 새 격리 표본의 성공 실행은 세 앱 Client 로그인, 주문 등록·수신함 재조회·수락, 기사 근무·위치·추천 재조회·수락, 음식점 최신 상태 재조회·픽업 준비, 기사 픽업·전달, 주문자 전달 상태 재조회·수령 확인을 관통했다.
- 결과는 `artifacts/local/validation/role-app-headless-e2e-20260914-r1/result.json`에 남겼다. MySQL 원장은 동일 주문 1건, `수령확인 / 배달완료`, 상태 이력 7건이었다.
- 증거 상한은 `RoleAppHeadlessE2EPassed`다. 주문자 앱 창 프로세스 실행은 확인했지만 네이티브 UI 상태·입력 API가 없어 로그인·클릭·화면 판독은 수행하지 못했으므로 `DeviceUiProof`는 차단 상태다.

### r5 검증: 진행 중 비식별 지역 투영

- 기존 완료 전용 `음식배달완료WorldSnapshot`과 Outbox는 변경하지 않았다. 진행 주문은 RDB 정본을 읽어 `operational-world-scene.v2`의 `ActiveLifecycle`로만 생성하며 v1에는 추가하지 않는다.
- 내부 주문 ID·주문번호·상세 주소·사용자·기사 식별자는 응답에 내보내지 않는다. 32바이트 이상 서버 설정 키의 HMAC-SHA256 가명, 단계 revision, 일반화 의미 위치와 projection hash만 제공하며 `LocalStorageAllowed=false`, `ReplayAllowed=false`, `distributionApproved=false`를 유지한다. 키가 없으면 해당 자료원만 닫힌 채 실패한다.
- 활성 자료는 최근 2시간 변경분에서 만들고 조회 시 2분 TTL로 재검증한다. 거절·취소는 15분 tombstone, 수령 확인은 진행 투영에서 즉시 제외하고 기존 완료 사본이 담당한다.
- 새 격리 볼륨 `bb9873ada44e4fe6b101982ea4acba77`에서 세 역할 앱 Client가 주문 `FOOD-20260914052915772`를 완료했다. 같은 가명 업무가 `주문대기 → 조리중 → 기사배정 → 픽업완료 → 전달완료`로 갱신되고 수령 확인 뒤 진행 목록에서 제거되는 것을 실제 인증 HTTP로 확인했다. MySQL 독립 재조회는 주문 1건, `수령확인 / 배달완료`, 상태 이력 7건이다.
- 증거 상한은 `ActiveRegionalProjectionHttpProofPassed`다. Mongo feed/cache, Unity live Client·Interpreter, Scene·Prefab·Play Mode·Game View, 네이티브 앱 UI, 전국 Profile은 구현·검증하지 않았다.

### r6 검증: Unity live 메모리 소비와 Windows UI 재시도

- `FoodDeliveryOsObservationAdapter`가 기존 완료 사본뿐 아니라 v2 `ActiveLifecycle`을 받아들이도록 확장했다. 음식배달의 진행 단계·활동 코드·주의 상태를 엄격히 검사하며 낮거나 같은 revision, 비식별 정책 위반, 허용되지 않은 단계는 계속 거절한다.
- 역할 앱 headless 실행기는 실제 `OperationalWorldSceneClient`·JSON Decoder·`OperationalWorldSceneInterpreter`·`FoodDeliveryOsObservationAdapter`·Router·Session을 사용한다. 새 격리 표본 주문 `FOOD-20260914054522350`에서 같은 가명 업무가 `주문대기 → 조리중 → 기사배정 → 픽업완료 → 전달완료`로 갱신되고 수령 확인 뒤 Unity 메모리에서 제거됐다.
- 독립 MySQL 재조회는 주문 1건, 최종 `수령확인 / 배달완료`, 상태 이력 7건이다. 결과는 `artifacts/local/validation/role-app-headless-e2e-20260914-g5/result.json`에 남겼다. 집중 Unity 시험 15/15와 headless 도구 build가 통과했다.
- 세 Windows 앱은 격리 서버 주소를 출력 폴더의 Git 비추적 `appsettings.Local.json`으로 덮어쓸 수 있게 공통화했고, `OrdererApp`·`RestaurantDeskApp`·`FDriverApp` Windows 빌드는 각각 경고 0·오류 0으로 통과했다.
- G6 실제 UI는 완료하지 못했다. 현재 UI 제어 런타임은 네이티브 앱 목록을 빈 배열로 반환하고 문서상 앱 선택 함수 호출도 `getApp is not a function`으로 거절했다. 직접 시작한 `OrdererApp`도 네이티브 표면으로 등록되지 않았다. 따라서 로그인·버튼 입력·화면 판독·동일 run manifest 결속은 수행되지 않았으며 `DeviceUiProofBlockedByNativeControlUnavailable`을 유지한다.
- 생성된 E 책임 지도를 생성기로 갱신한 뒤 범위 지정 Fast의 두 solution build·대상 Unity/서버 시험·지도 검사·diff 검사가 모두 통과했으며 기록은 `artifacts/local/validation/20260914-145354`다. 격리 컨테이너는 종료했다.
- 현재 증거 상한은 `UnityClientInterpreterLiveHttpProofPassed`다. 이것은 Unity 어셈블리의 실제 HTTP·메모리 적용 증거지만 Unity Editor·Play Mode·Game View 또는 네이티브 장치 UI 증거는 아니다.
