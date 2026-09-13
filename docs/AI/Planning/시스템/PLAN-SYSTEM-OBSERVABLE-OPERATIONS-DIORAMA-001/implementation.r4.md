# 다중 OS 생명주기 재생 E7 수직 작업 명세 r4

## 결속

- 승인 기획: [다중 OS 생명주기 재생 r4](multi-os-lifecycle-playback.r4.md), SHA-256 `B728768015BAC8304267BB29B93763E1BC80BD446DCDDD0DA81096D15E8763AB`.
- 대표 작업: 사가정역 1km 디오라마에서 `VerificationSample` 업무 하나가 같은 `WorkStableId`와 증가하는 revision으로 정상 또는 회복 생명주기를 지나도록 저장·투영·재조회하고, Unity가 그 상태 사본을 읽기 전용으로 표현한다.
- 첫 범위: `FoodDeliveryOS`, `DomesticCargoTransportOS`, `WarehouseCommerceFulfillmentOS`, `SsalddelMartUrbanLogisticsOS`의 정상·회복 각 1건, 총 8개 사례·77단계·게시 offset 5~565초.
- 자료 입력: 실제 공개 음식점 후보 12곳의 위치·업종 관측과 격리 검증 DB의 업종 기반 합성 프로필·메뉴를 `NoBusinessAffiliation`로만 결속한다.
- 제외: 나머지 6개 OS의 임의 생명주기, 실제 주문·배차·결제·정산·메시지, 외부 메뉴 수집, 운영 DB 쓰기, 새 공식 Scene.
- 쓰기 소유: Hongdal의 공통 lifecycle 계약·전용 검증 host·격리 저장·조회·시험·문서와 Unity 저장소의 runtime-only 표현·시험·캡처 도구.

## 권위와 자료원 선택

- 운영 상태의 최종 권위는 서버에 있고 Unity는 `ObservationPresentationOnly=true`인 상태 사본만 소비한다.
- 합성 음식점 프로필·메뉴와 `VerificationSample` 업무 단계는 `Development + Simulation + 전용 컨테이너 + 격리 DB` 조건에서만 생성한다. 조건이 맞지 않으면 중단하며 운영 자료 fallback을 사용하지 않는다.
- 실제 상호·업종·위치는 공개자료 관측 원장, `SyntheticFixture` 음식점 묶음과 합성 메뉴·가격은 Fixture 원장, `SourceKindCode=VerificationSample`인 업무 단계는 단계 원장에 각각 남긴다. 화면에는 `샘플 메뉴 · 실제 판매 메뉴 아님 · 해당 업체와 제휴 관계 없음 · 로컬 검토용/비배포`를 표시한다.
- Unity 위치는 사람 검토 전 `LocationPresentationAnchorOnly`이며 실제 출입구·배달 가능 경로나 업체 참여를 뜻하지 않는다.
- `ObservableOperations` Runner는 검증 Controller 아래에서 격리 원장·Outbox·MongoDB 상태 사본을 확인하는 표본 파이프라인이다. 실제 음식·화물·창고·마트 운영 Controller/UseCase/Command의 권한·전이·업무 성공 증거를 만들거나 대신하지 않는다.

## Logic E7→E1 영향 검토

| 단계 | 범위 |
| --- | --- |
| E7 | 600초 통합 실행에서 8개 사례·77단계가 5~565초 offset으로 시작·진행·대기·회복·완료되고 독립 재조회에서 같은 run/fixture/revision 계보가 확인됨 |
| E6 | 일시정지·재개, 같은 사례의 앞 revision 실패 시 뒤 revision 차단, 앞 revision부터 Outbox 재시도, 정상·회복 사례의 독립 진행 |
| E5 | 전용 MySQL의 실행·Fixture·결속·사례·단계·Outbox와 MongoDB 불변 상태 사본, Redis 실행 상태를 같은 run ID로 결속 |
| E4 | 네 OS의 동결 lifecycle 순서와 정상·회복 경로, Warehouse 공통 9단계·정상 8단계·수량 불일치 회복 10단계, 증가 revision, 의미 위치·관계·완료 조건을 계약으로 고정 |
| E3 | v2 조회 계약의 `WorkStableId`, `LifecycleStageCode`, `AttentionStateCode`, `Revision`, 자료원·개인정보·배포 경계 검증 |
| E2 | 검증 Controller → `ObservableOperations` Runner → MySQL 검증 원장/Outbox → MongoDB 검증 투영 → 지역 장면 재조회 순서와 실행 모드 제한 |
| E1 | 실제 공개 관측, 합성 검증 입력, 운영 권위, Unity 표현을 서로 다른 책임으로 유지 |

E1부터 역순으로 조립해 E7을 검증한다. 단계 원장은 사례 header와 분리하고 `(RunStableId, CaseCode, StepSequence)`를 멱등 키로 사용한다. 같은 상태 사본은 낮은 revision이나 동일 revision 충돌로 덮어쓰지 않는다. Warehouse는 `warehouse.inbound-plan`, `receiving`, `inspection`, `put-away-inventory`, `outbound-allocation`, `picking`, `packing`, `outbound-handoff`, `exception-recovery`의 9개 공통 단계 ID를 사용한다.

## Presentation E7→E1 영향 검토

| 단계 | 범위 |
| --- | --- |
| E7 | 실제 Play Mode·Game View에서 전체 조망 1장, 음식 픽업·신호 근접 1장, 창고·화물·마트 인계 근접 1장과 필요 시 회복 1장을 같은 실행으로 캡처 |
| E6 | runtime 객체의 위치·색·표식·카드가 단계 revision에 따라 변하고, 대기·회복·완료·귀환 및 Transform 변화 기록이 남음 |
| E5 | canonical `SimulationWorldShell/OperationalOsWorldRoot` 아래 runtime-only OS별 Root에 같은 안정 ID 객체를 재사용해 결속 |
| E4 | 기존 사가정 지도·교통·배달 표현과 primitive/텍스트 fallback을 재사용하며 새 외부 시각 자산은 `NotApplicable` |
| E3 | Decoder·Interpreter·Reconciler가 네 OS lifecycle, 낮은 revision, 동일 revision 충돌, 미지원 OS, 미결속 위치를 검증 |
| E2 | 서버 상태 사본을 OS별 Unity 메모리 모델·경로·의미 위치에 명시적으로 매핑 |
| E1 | 관찰자·합성 업무·지역 배치 객체의 역할과 개인정보·운영 효과 비노출 원칙 |

`presentationE4Preparation`: 주 후보는 기존 primitive 상태 표식, 사가정 도로·신호·기사 재생 표현이다. 대체 후보는 OS별 색상과 텍스트 진단 표식이다. runtime 객체는 `DontSaveInEditor | DontSaveInBuild`로 생성해 Scene에 직렬화하지 않는다. 화면 캡처만으로 움직임을 증명하지 않고 같은 run의 단계/revision/Transform manifest를 함께 보존한다.

## 검증 순서

1. 네 OS 공통 lifecycle과 Warehouse 정상 8단계·수량 불일치 회복 10단계 및 서버 runner 집중 단위 시험을 통과시킨다.
2. 격리 DB schema를 기존 볼륨에서 안전하게 승격하고 12개 합성 프로필·메뉴와 8개 사례·77단계를 저장한 뒤 별도 연결로 재조회한다.
3. 실제 loopback HTTP의 상태·시작·일시정지·재개·조회와 Outbox/Mongo 투영을 확인한다.
4. Unity EditMode에서 단계 해석·revision·객체 재사용·삭제·지역 전환을 검증한다.
5. 실제 Editor에서 canonical Scene을 열어 Play Mode를 실행하고 캡처·manifest·Console을 확인한 뒤 Play Mode를 종료한다.
6. Hongdal 변경 경로를 `Fast`, 이어서 `Task`로 검증하며 Unity 저장소 검증 결과를 별도로 기록한다.

## 실패·회복과 무효화

- unsupported OS는 진단에만 남기고 객체나 가짜 단계를 만들지 않는다.
- 한 사례의 앞 revision이 실패하면 뒤 revision 게시를 막고 Retry에서 앞 revision부터 재개한다. MongoDB에 이미 더 높은 revision이 있어 과거 Outbox가 stale이면 `Superseded`와 `ObservableOperationsProjectionRevisionStale` 근거 코드로 종결한다. 다른 사례의 갱신은 계속한다.
- MySQL·MongoDB·Redis·HTTP 가운데 하나라도 실제 실행 증거가 없으면 격리 `VerificationSample` 파이프라인 실행 완료로 선언하지 않는다. 이 네 저장·조회 증거가 있어도 실제 운영 Controller/UseCase workflow 성공으로 승격하지 않는다.
- PNG와 manifest/run ID가 다르거나 Transform·단계 기록이 없으면 움직임 검증으로 사용하지 않는다.
- 실제 메뉴·제휴로 오인될 표시, 개인정보, 운영 효과, 새 공식 Scene 저장이 발견되면 즉시 중단하고 가장 이른 E를 다시 연다.
- lifecycle 단계·기획 hash·지도/배치/경로 hash·공개자료 판본이 바뀌면 해당 Logic 또는 Presentation 궤적을 다시 검증한다.

## 완료 보고 분리

코드·자동 시험, 실제 격리 DB, 실제 HTTP, Unity 컴파일/EditMode, Play Mode·Game View, Scene 저장 여부, commit, push를 각각 별도 사실로 보고한다. 한 항목의 성공을 다른 항목의 증거로 대신하지 않는다.

## 구현·검증 결과 (2026-09-13)

### 코드·자동 시험

- 네 OS의 정상·회복 8개 사례와 77단계, Warehouse 공통 단계, MySQL 단계/Fixture 원장, Outbox, MongoDB 투영, Redis 실행 상태, loopback 검증 API와 Unity runtime-only 표현 계층을 구현했다.
- Fixture 전체 결속 집합과 안전 필드를 쓰기 전에 검사해 충돌 시 프로필·메뉴 부분 저장을 막는다. run에는 묶음 ID·hash·revision을 동결하고 실제 묶음과 다시 대조한다.
- 완료 판정은 8개 사례·77개 단계·77개 Outbox의 정확한 키 집합, 사례 최종 필드 일치와 각 사례 최종 revision의 `Published`를 요구한다. 낮은 revision만 `Superseded`를 허용한다.
- 단계 hash에는 객체·항목·역할·자료원 분류를, 투영 hash에는 게시·만료 시각과 timeline revision을 포함한다. MongoDB 조회는 만료되지 않은 최신 유효 run 하나만 반환한다.
- 서버 집중 시험 31/31과 Unity EditMode 10/10이 통과했다. Hongdal 변경 경로 한정 Fast도 build·targeted test·diff 검사를 통과했다(`artifacts/local/validation/20260913-231412`). 최신 Task는 전체 solution build를 통과했고 서버 전체 5,235건 중 5,228건 통과·기존 기준선과 같은 7건 실패였다(`artifacts/local/validation/20260913-231857`); 이번 집중 범위 실패는 없다.

### 실제 격리 서버 실행

- run: `observable-operations-run:r4-hardened-20260913225629`
- `Development + Simulation`, loopback `127.0.0.1:53216`, 600초로 실행하고 초반 일시정지·재개를 확인했다.
- 553초에 MongoDB를 실제 중단했다. 600초에 8사례·77단계가 생성됐어도 마지막 Outbox가 `Failed=1`이라 run은 `Running`을 유지했고, MongoDB 복구와 명시적 Retry 뒤에만 `Completed`가 됐다.
- MySQL 독립 재조회: 4 OS·8사례·8 Work ID, 77단계·77개 고유 단계 hash·revision 일치 77, Outbox 77건 전부 `Published`, 최종 revision 게시 8/8, 재시도 1건.
- Fixture 독립 재조회: v2 묶음 1개에 profile 12·menu 30·결속 42, 음식점당 메뉴 2~3개. 모든 profile은 업체 ID 없음·주소 빈 값·좌표 0·비공개·주문 불가이고 결속은 `PendingHumanReview / NoBusinessAffiliation / ActualOrderAllowed=false / DistributionApproved=false`다.
- MongoDB 독립 재조회: 해당 run 최종 사본 8개·OS 4개·Work ID 8개, final revision/step 일치 8/8, 단계/투영 hash 64자 8/8, `fixtureRevision=observable-operations-lifecycle.r4` 8/8.
- Redis 독립 재조회: `Completed / 600초 / 8사례 / 77단계 / Pending 0 / Failed 0`과 같은 Fixture 묶음 계보를 확인했다.
- 실제 검증 HTTP v2 재조회: 최신 run 한 건만 선택한 8개 항목, OS 4개·Work ID 8개, `VerificationSample`·저장/Replay 금지·합성 표시·정확 위치/개인정보 없음·timeline r4 8/8, source failure와 금지 필드 0건.
- DB에서 Unity용 로컬 Fixture를 두 번 내보내 같은 12개 아이콘·30개 샘플 메뉴와 SHA-256 `f253fed02190c42c1548301f3638adb7abfb8318ab4a5f1fb812ed69d9578a4a`를 확인했다.

### Unity 실행

- 전용 Unity worktree에서 실제 비배치 Play Mode를 실행해 전체, 음식배달 회복 근접, 창고·화물·마트 인계 근접 Game View 세 장을 남겼다. 캡처 프로세스는 종료 코드 0이었다.
- 같은 표식의 1.2초 `SmoothStep` 일반화 기준점 보간을 실제 프레임에서 표본화했다. XZ 이동거리는 Food 96.830, Cargo 67.231, Warehouse 56.639, Mart 43.278 Unity 단위이며 중간 프레임은 전체 거리의 47.5~50.4%였다.
- `movementMode=GeneralizedStageAnchorInterpolation`, `routeAuthority=false`, `liveHttpEndToEnd=false`, `operationalEffectsAllowed=false`, `sceneSaved=false`다. PNG는 정지 화면이고 연속 이동 근거는 같은 실행 manifest의 프레임 표본이다.
- `SimulationWorldShell.unity`와 `UrbanMarketManagerPrimitive.unity`의 SHA-256은 실행 전후 동일했고 `.unity` 변경은 0건이다. 구현 소유 C# 8개와 `.meta` 6개는 검증 worktree와 기본 Unity 작업트리에서 byte 동일하다.

### 남은 경계

- 실제 음식·화물·창고·마트 Controller/UseCase/Command를 호출한 운영 workflow 검증이 아니다.
- Unity 화면은 서버 HTTP를 live로 소비하지 않은 동결 표현 timeline이다. 실제 도로·차선·신호·길찾기 권위도 없다.
- 기존 Scene 기준선의 누락 Prefab 56 GUID, Unknown script 10건, SearchDatabase 예외 1건, 로컬 WorldTile 연결 실패 1건과 종료 시 JobTempAlloc 경고 2건은 이 절편 밖에 남아 있다.
- canonical Scene 저장, 운영 DB·외부 효과, 나머지 6개 OS lifecycle, commit·push는 수행하지 않았다.
