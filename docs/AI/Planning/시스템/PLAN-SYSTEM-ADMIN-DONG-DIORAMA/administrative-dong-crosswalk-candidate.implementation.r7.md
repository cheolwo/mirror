# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r7]

## 승인 근거

2026-09-15 사용자는 동북서울 30개 행정동 디오라마를 사가정역 검토 깊이에 맞춰 계속 발전시키고, 각 동의 건물·도로명주소·도로·골목·신호와 배달 기사·차량·보행 생활상을 단계적으로 검증해 달라고 요청했다.

이번 절편은 그 요청 가운데 공식 최신 파일이 이미 동결된 횡단보도 점과 보행등 설치 유무를 30개 행정동의 자료 결손에 연결한다. 사용자의 요청은 역사 경계를 현행 경계로 승인하거나 점 자료를 실제 통행 graph·현재 신호 현시·게임플레이에 사용하는 승인이 아니다.

## 첫 G3a 범위

1. 서울 열린데이터광장 `OA-23081`의 동결 XLSX 전체 21,776행을 읽는다.
2. 좌표가 없는 1행은 추정하지 않고 원본 결손으로 집계한다.
3. 좌표가 있는 점을 역사 경계 후보 30개에 `covers`로 대조해 정확히 한 행정동에 들어가는 점만 그 행정동 후보로 귀속한다.
4. 공유 경계·복수 판정은 어느 동에도 복제하지 않고 전역 `Unresolved`로 한 번만 보존한다.
5. 30개 동 밖의 점은 상세 원장에 저장하지 않고 감사 집계만 남긴다.
6. 결정적 후보 묶음을 로컬 `artifacts/local`에 hash 세대별로 만들고, 로컬 MySQL의 기존 범용 공공자료 원장에 `PendingHumanReview`로 저장·재조회한다.
7. 기존 사가정 1km 횡단보도 84건, r2 30개 동 모듈, Mongo candidate/current, Unity Scene·Prefab·GameObject는 수정하지 않는다.

이 절편은 횡단보도 점 후보만 다룬다. 횡단보도 면 도형, 정지선, 보도 연결, 차로, 신호 주기·현재 현시, 차량·오토바이·보행 통행 graph는 포함하지 않는다.

## 동결 출처와 좌표계 관문

| 항목 | 동결 값 | 판정 |
| --- | --- | --- |
| 공식 자료 | 서울시 교차로 및 횡단보도 시설·위치정보 `OA-23081` | 공공누리 제1유형, 제3자 권리 없음 |
| 원본 파일 | `서울시 교차로 및 횡단보도 시설·위치정보_20260824.xlsx` | 2026-08-24 자료, 1,735,662 bytes |
| 원본 SHA-256 | `1A5DB9EA7A1CD58E2D7F2B4246BAF099A3E4D5278F2867A1B87F3D50D21541BE` | 변경 시 즉시 거절 |
| 원본 행 | 21,776 | 좌표 결손 1행 |
| 30개 동 범위 | `northeast-seoul-rider.r2` | 정확 HJD 30개·역사 경계 후보 |
| 경계 원본 | 서울시 `OA-22160`, EPSG:5181 | 2023 역사 경계·현행 정본 아님 |

`OA-23081` 공식 페이지와 시트에는 좌표계가 직접 명시되지 않는다. 따라서 다른 T-GIS 자료의 좌표계를 그대로 전이하지 않고 다음 독립 기준점 검사를 동결한다.

- 공식 사가정역 기준점: WGS84 경도 `127.088502`, 위도 `37.580912`
- 이를 EPSG:5186으로 변환한 값: X `207817.3774`, Y `553488.0505`
- XLSX의 `사가정역` 횡단보도 `06-0000016264`와 거리: `8.0946m`
- 같은 숫자를 EPSG:5181·5174·2097로 읽을 때 전체 좌표 중 기준점과 최소 거리: 모두 `83km` 이상
- EPSG:5179로 읽을 때 전체 좌표 중 기준점과 최소 거리: `1,580km` 이상

위 검사는 EPSG:5186 해석을 강하게 지지하지만 원천 선언을 대신하지 않는다. 결과에는 `CoordinateReferenceStatus=EmpiricallyValidatedCandidateNotSourceDeclared`를 기록하고, 사람 검토나 공급자 좌표 사전 확인 전까지 좌표 권위를 `false`로 둔다.

역사 경계 EPSG:5181과 후보 점 EPSG:5186은 같은 KGD2002 중부원점 투영에서 false northing만 `500,000m`와 `600,000m`로 다르므로 경계 Y에 정확히 `100,000m`를 더해 후보 좌표계에서 판정한다. 다른 회전·축척·평행 이동을 추정하지 않는다.

## 결정적 후보 묶음

새 범위 정의와 생성기를 기존 r2와 분리한다.

- 범위 정의: `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-crosswalk.g3a.r1.json`
- 생성기: `eng/neighborhood/administrative_dong_crosswalk_candidate.py`
- 출력: `artifacts/local/public-data/admin-dong-crosswalk-northeast-seoul-20260915-g3a-r1/generations/{candidateSetHash}/`
- 파일: `manifest.json`, `candidates.ndjson`, `audit.json`, `complete.json`

생성기는 고유한 staging 디렉터리에 모든 파일을 쓴 뒤 hash 세대 경로로 원자 이동한다. `complete.json`은 manifest·candidate·audit 파일 hash와 후보 집합 hash를 다시 결속한다. 미완성 staging이나 완료 표식 없는 세대는 소비하지 않는다.

후보 집합 hash는 UTF-8 문자열마다 big-endian 32-bit 길이를 먼저 넣는 순차 SHA-256으로 계산한다. 헤더는 schema·revision·원본 XLSX hash·기존 acquisition 영수증 hash·r2 범위 정의 hash·r2 scope-manifest hash·생성기 hash·좌표계 검증 hash 순서다. 후보는 원천 feature key 순으로 정렬하고 원천 key·귀속 동·상태·후보 동 집합·관리번호·교차로 참조·종류·보행등 설치 유무·원본 좌표·공통 ENU millimeter를 순서대로 넣는다.

## 로컬 RDB 후보 원장

새 업무 테이블이나 migration을 만들지 않고 `PublicDataIngestionDbContext.NormalizedRecords`, `RawSnapshots`, `IngestionRuns`와 기존 공공자료 등록·멱등 저장 경계를 재사용한다.

- `SourceId`: `seoul-open-data-admin-dong-crosswalk-private-review`
- `DatasetId`: `northeast-seoul-admin-dong-crosswalk-candidate-g3a-r1`
- `MetricCode`: `administrative-dong-crosswalk-point-candidate`
- `SchemaVersion`: `administrative-dong-crosswalk-candidate.v1`
- `DataRevision`: `northeast-seoul-admin-dong-crosswalk-candidate.g3a.r1`
- `EvidenceAsOfUtc`: `2026-08-24T00:00:00Z`
- `DerivedAtUtc`: `2026-09-15T00:00:00Z`

원본 횡단보도 관리번호와 시트 행 번호로 안정 원천 key를 만들고, 행정동·원천 key·투영 revision을 record key에 포함한다. payload에는 원천 행, 관리번호, 교차로 관리번호·명칭 후보, 횡단보도 종류, 보행등 설치 유무 관찰, 원본 좌표, 공통 ENU millimeter, 귀속 상태, 경계·원본·투영 hash만 둔다. 상세주소·담당자·실시간 신호·원본 전체 행은 넣지 않는다.

적용은 preflight, DB named lock, transaction, 500행 단위 멱등 upsert 순서로 수행한다. 반복 적용은 신규·갱신 0이어야 하며 새 DB 연결에서 정확 후보 집합·행정동별 분포·후보 hash·원본 snapshot을 다시 확인한다.

## 권위와 표시 상한

manifest와 모든 후보는 다음을 강제한다.

- `privateReviewOnly=true`
- `historicalBoundaryBootstrapOnly=true`
- `observationCandidateOnly=true`
- `currentAdministrativeBoundaryEstablished=false`
- `sourceDeclaredCoordinateReference=false`
- `crosswalkGeometryEstablished=false`
- `stopLineEstablished=false`
- `signalPhaseTimingEstablished=false`
- `sidewalkConnectionEstablished=false`
- `distributionApproved=false`
- `publicDisplayAllowed=false`
- `runtimeAuthorized=false`
- `traversalReady=false`
- `gameplayReady=false`
- `unityApplyAllowed=false`

`pedestrianSignalPresent`는 보행등 설치 유무 관찰이며 현재 색상·잔여 시간·주기·통행 허용을 뜻하지 않는다. 이후 화면에서 적색 정지·녹색 재출발을 표현하더라도 별도 `SyntheticScenario` 설정과 구별한다.

## 검증

- 원본 길이·SHA-256·헤더·행 21,776·좌표 결손 1을 확인한다.
- 정확한 30 HJD allow-list, r2 범위·scope-manifest와 역사 경계 hash를 확인한다.
- 유일 귀속·공유 경계 unresolved·범위 밖 제외를 합성 경계 자체 시험으로 검증한다.
- 후보 중복, 모르는 행정동, true 권위 플래그, 완료 표식 또는 파일 hash 변조를 거절한다.
- 같은 입력으로 생성기를 두 번 실행해 변경 파일 0과 같은 후보 hash를 확인한다.
- C# 자체 시험, 직접 build, 첫 MySQL apply, 반복 apply, 독립 readback을 실행한다.
- 기존 Mongo current 포인터, Unity 자료·Scene·Prefab이 바뀌지 않았음을 범위상 확인한다.

## 완료 의미와 다음 관문

이번 완료 상한은 `G3a CrosswalkPointCandidatesStored / HistoricalBoundaryCandidateOnly`다. 30개 동별 횡단보도·보행등 설치 후보 coverage를 비교할 수 있지만, `G3 MobilitySurface` 전체나 `G7 UnityLifeView`를 닫지 않는다.

다음 절편은 현행 행정동 경계를 확보하면 같은 원본을 재귀속하고, 그 전에는 2020 보행망·2021 차선 표시선·2025 교차로·2026 방향표시를 각기 다른 판본과 품질로 분리 수집하는 것이다. 실제 주행 lane과 골목·보도 연결, 신호 주기와 NPC 이동은 이 자료들을 같은 이동 graph로 검토한 뒤에만 구현한다.
