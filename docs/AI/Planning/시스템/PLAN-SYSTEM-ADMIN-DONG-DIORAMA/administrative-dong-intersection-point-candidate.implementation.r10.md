# 행정동 교차로 점 후보 G3b 자료 구현 계획 r10

- 기획 ID: `PLAN-SYSTEM-ADMIN-DONG-DIORAMA`
- 구현 판본: `administrative-dong-diorama:intersection-point-candidate.g3b.r1`
- 작업 종류: `FrozenPublicDataCandidateGeneration`
- 상태: `ApprovedForLocalPrivateCandidateGeneration`
- 범위: 동북서울 라이더 기준선의 정확한 30개 역사 행정동
- 완료 상한: 로컬 비공개 교차로 점 후보 생성·재검증

## 1. 목적과 비목적

G3b는 서울 열린데이터광장 `OA-15534`의 교차로 **점**을 동결 원본 그대로
읽고, G2에서 사용한 30개 역사 행정동 경계에 독립적으로 귀속한 검토 후보를
만든다. 교차로 점은 횡단보도 점과 별도 원천·별도 공간 판정을 가진다.

이 작업은 플레이어 행동, WI, PlayableLoop 또는 Unity 표현을 구현하지 않는다.
따라서 E7 작업 명세를 만들거나 E 단계를 주장하지 않고, 별도의 자료 구현
계약과 결정적 생성기만 둔다.

이번 판본으로 성립하지 않는 것은 다음과 같다.

- 현행 행정동 경계와 현행 교차로 존재 여부
- 교차로 접근 방향, 회전 규칙, 연결 도로와 topology
- 교통신호 제어기, 신호등, 현시, 주기와 실시간 상태
- 차선 도형, 주행 가능 차로, 정지선, 보도 또는 횡단보도 면
- 길찾기, 차량·NPC 이동, 통행 허가, Unity 배치와 Game View
- RDB·MongoDB 저장, API 게시, 공개 배포와 `current` 포인터

특히 `OA-15537`의 차선 관련 정보는 노면의 **차선 표시 자료**이지 이
판본의 주행 가능 차로가 아니다. 이 자료와 방향표시·제어기·신호등 파일은
G3b 입력으로 읽지 않는다. `OA-21208` 도보 네트워크도 설명상 2020년
기준의 별도 자료이므로 현행 이동망으로 혼합하지 않는다.

## 2. 동결 입력과 계보

### 2.1 교차로 원본

- 원본: `artifacts/local/public-data/sagajeong-static-traffic-20260913-r1/A008_P_20250814.zip`
- 데이터셋: `OA-15534`, 서울시 교차로 관련 정보
- 파일 수정일: `2025-08-14`
- 수집 영수증: 같은 폴더의 `acquisition.json`
- 원본 SHA-256:
  `A77B4D4FD2886C934D2D097558A52580FA95ADB079BA828F1305DFD15F0E0449`
- 원본 길이: `534,342` bytes
- 이용조건: `KOGL-Type1-Attribution`
- 좌표계: 포털 메타데이터와 `.prj`가 선언한 `EPSG:5186`
- 도형: `POINT` 8,097개

생성기는 ZIP의 정확한 entry 집합, uncompressed bytes, CRC32와 SHA-256을
검증한다. `.shp` 점 좌표가 원천 공간 위치다. DBF의 정수형 `XCE/YCE`는
전체 8,097행 중 301행이 비어 있고, 있는 값도 정밀 SHP 점과 차이가 있으므로
귀속이나 후보 위치에 사용하지 않는다. 이 차이는 자료 품질 진단으로만 남긴다.

후보 식별은 서로 다른 의미를 합치지 않는다.

- `MGRNU`: 8,097개 모두 비어 있지 않고 고유한 원천 시설 관리번호
- `CSS_NUM`: 8,097개 모두 고유한 원천 교차로 번호이며 G3a 횡단보도의
  `intersectionManagementNumber`와 진단 연결할 때만 사용
- 안정 후보 ID: 데이터셋·원본 판본·`MGRNU`를 length-prefixed SHA-256으로
  묶어 만든 비가역 ID

### 2.2 30개 역사 행정동 경계

- 경계 원본: `OA-22160`, 파일 수정일 `2023-10-31`, `EPSG:5181`
- 원본 SHA-256:
  `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`
- 범위 정의:
  `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json`
- 범위 manifest:
  `artifacts/local/public-data/admin-dong-diorama-northeast-seoul-20260914-r2/scope-manifest.json`

경계는 KGD2002 Central Belt의 false northing 차이만 반영해
`EPSG:5181 -> EPSG:5186`으로 `Y + 100,000m` 변환한다. 회전·축척·추정
평행이동은 적용하지 않는다. `covers(point)`가 정확히 하나인 경우에만 해당
역사 행정동 후보로 둔다. 0개면 대상 밖, 2개 이상이면
`UnresolvedMultipleHistoricalBoundaryCover`이고 임의 귀속하지 않는다.

이 경계는 현행 권위 경계가 아니라 `HistoricalBoundaryBootstrapOnly`다.

### 2.3 G3a 횡단보도 연결 진단

G3a r2의 완결된 후보 집합
`78312E5F0DDB89BFFA9AD1881379CBF7E8037A6C6454D4F0ABDF2A3E4D22D30A`
를 정확한 파일 hash와 완료 marker로 검증한 뒤 읽는다. G3b 교차로를 자체 점
귀속으로 먼저 확정하고, 그 다음 `CSS_NUM == intersectionManagementNumber`
인 횡단보도들을 진단 링크로만 덧붙인다.

- 연결된 교차로 후보: 524개
- 연결되지 않은 교차로 후보: 30개
- 연결 횡단보도 관계: 1,491개
- 같은 역사 행정동 관계: 1,271개
- 다른 역사 행정동 관계: 220개

`CrosswalkLinkObservedOtherHistoricalHjd`는 오류를 숨기지 않는 진단이다.
횡단보도 후보의 행정동을 교차로 후보에 복사하거나, 반대로 교차로 귀속을
횡단보도에 복사하지 않는다. 링크의 존재도 교차로 후보 포함 조건이 아니다.

## 3. 기대 판정

| 항목 | 기대값 |
| --- | ---: |
| 원본 점 | 8,097 |
| 30개 동 단일 귀속 | 554 |
| 대상 밖 | 7,543 |
| 복수 귀속 | 0 |
| 후보가 있는 행정동 | 30 |
| 원본 자치구와 역사 경계 자치구 일치 | 553 |
| 원본 자치구와 역사 경계 자치구 충돌 | 1 |

충돌 1건은 원본 `GU_CDE=200`(성동구), `CSS_NUM=149`,
`MGRNU=82-0000000970` 점이 역사 경계의
`region:kr:hjd:1123060000`(답십리제1동·동대문구)을 덮는 경우다. 경계까지
거리는 약 `0.255m`이므로 자동 수정하지 않고
`SourceDistrictSpatialAssignmentConflict`로 보존한다.

30개 동별 후보 수와 링크 진단 분포는 전용 scope JSON에 전부 동결한다.
전역 합계만 맞고 행정동별 결과가 달라져도 검증 실패다.

## 4. 후보와 품질 계약

후보 한 행은 다음 계보와 판정을 가진다.

- `candidateStableId`, `sourceFeatureKey`, `sourceManagementNumber`,
  `intersectionManagementNumber`
- 원본 ZIP hash와 역사 경계 hash
- `sourceGeometryPointEpsg5186`와 공통 ENU millimeter 좌표
- 자체 `administrativeAreaStableId`, `assignmentStateCode`,
  `ownershipBasisCode=OwnSourcePointHistoricalBoundaryCover`
- 원본 자치구 코드·명과 공간 판정 자치구명
- G3a 링크의 원천 feature key, 횡단보도 관리번호, 횡단보도 자체 행정동,
  `sameHistoricalAdministrativeArea`
- 다음 품질 코드의 정렬된 집합

품질 코드는 다음과 같다.

- 기본 검토 상태: `PendingHumanReview`
- 경계 한계: `HistoricalBoundaryBootstrapOnly`
- 원본 자치구 충돌: `SourceDistrictSpatialAssignmentConflict`
- 횡단보도 링크 없음: `NoInScopeCrosswalkLinkObserved`
- 다른 동 횡단보도 링크: `CrosswalkLinkObservedOtherHistoricalHjd`
- DBF 위치 속성 결손: `SourceCoordinateAttributeMissing`

`SourceCoordinateAttributeMissing`은 SHP 점의 결손을 뜻하지 않는다. 후보는
SHP 점으로 생성하되 원천 속성의 결손을 숨기지 않기 위한 진단이다.

모든 후보·manifest·audit·complete marker에는 다음 상한을 보존한다.

- `privateReviewOnly=true`
- `historicalBoundaryBootstrapOnly=true`
- `observationCandidateOnly=true`
- `sourceDeclaredCoordinateReference=true`
- `currentAdministrativeBoundaryEstablished=false`
- `currentIntersectionTopologyEstablished=false`
- `approachDirectionEstablished=false`
- `controllerBindingEstablished=false`
- `laneBindingEstablished=false`
- `signalBindingEstablished=false`
- `crosswalkAssignmentInherited=false`
- `distributionApproved=false`
- `publicDisplayAllowed=false`
- `databasePersistenceCompleted=false`
- `runtimeAuthorized=false`
- `traversalReady=false`
- `gameplayReady=false`
- `unityApplyAllowed=false`

## 5. 결정성 및 저장 안전

출력은 다음 5개 파일이다.

1. `manifest.json`
2. `candidates.ndjson`
3. `audit.json`
4. `generator-source.py`
5. `complete.json`

후보 집합 hash는 고정 순서의 header와 후보 필드를 각각 UTF-8로 만들고,
각 필드 앞에 unsigned big-endian 32-bit 길이를 붙여 SHA-256으로 계산한다.
JSON은 key 정렬·compact separators·NaN 금지로 정규화한다. generator 자체
bytes와 자료 구현 계획, 자료 구현 계약, scope, 모든 소비 입력의 실제 hash가
header에 들어간다. 따라서 원본·규칙·도구 중 하나라도 달라지면 새 세대가 된다.

출력 경로는
`artifacts/local/public-data/admin-dong-intersection-northeast-seoul-20260915-g3b-r1/generations/{candidateSetHash}`다.
생성기는 다음을 강제한다.

- 저장소 root와 모든 입력·출력의 저장소 내부 경로 확인
- 입력, 출력 부모와 기존 세대의 symlink/junction/reparse 거부
- 범위 밖·절대 경로·`..` 거부
- 같은 출력 부모의 임시 디렉터리에 완전 생성·재검증 후 atomic rename
- 동일 hash 세대가 있으면 정확한 파일 집합·bytes 비교 후 무변경 반환
- `complete.json`을 마지막 파일로 기록
- `current` 포인터를 생성하거나 변경하지 않음

## 6. 검증과 완료 조건

- 자체 시험에서 canonical framing 구분, 경로 탈출, reparse 판정,
  후보 ID 결정성, 횡단보도 링크 비상속, 다중 경계 미귀속, 원자 생성 실패
  정리와 기존 세대 변조 거부를 확인한다.
- 실제 동결 입력으로 첫 생성과 둘째 생성을 실행한다.
- 둘째 실행은 같은 세대 hash, `changedFiles=0`이어야 한다.
- 별도 `verify`가 5개 파일과 내부 hash·행 수·scope 기대값을 다시 계산한다.
- 8,097 / 554 / 7,543 / 0, 30개 동, 553 / 1,
  524 / 30, 1,491 / 1,271 / 220을 모두 대조한다.
- generator snapshot bytes가 실행한 generator bytes와 같아야 한다.

이 조건을 통과해도 완료 표현은
`LocalPrivateHistoricalIntersectionPointCandidateGenerated`뿐이다. RDB·MongoDB,
현행성, 이동 graph, 신호, Runtime, Unity와 gameplay 완료로 확대하지 않는다.

## 7. 구현 파일

- 자료 구현 계약:
  `administrative-dong-intersection-point-candidate.data-implementation.v1.json`
- 범위 정의:
  `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-intersection.g3b.r1.json`
- 생성기:
  `eng/neighborhood/administrative_dong_intersection_candidate.py`

공유 `PLAN` README, `PLANNING.md`, `CURRENT_WORK.md`, `Program.cs`, 기존 importer
README는 이 좁은 절편에서 수정하지 않는다.
