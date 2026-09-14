# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r8]

## 승인 근거와 이번 정정

2026-09-15 사용자는 동북서울 30개 행정동 디오라마를 사가정역 검토 깊이에 맞춰 계속 발전시키고, 주소·필지·도로·골목·신호·배달 기사·차량·보행 생활상을 같은 증거 관문으로 단계적으로 검증해 달라고 요청했다.

이번 절편은 그 요청 가운데 공식 최신 파일이 이미 동결된 횡단보도 점과 보행등 설치 유무를 30개 행정동의 자료 결손에 연결한다. 현행 경계, 실제 횡단보도 면, 보도 연결, 차로, 신호 현시·주기, 통행 graph 또는 Unity 생활상 적용을 승인하지 않는다.

구현 r7의 첫 결과 `northeast-seoul-admin-dong-crosswalk-candidate.g3a.r1`은 독립 감사에서 계보 hash·구 경계 충돌 진단·좌표 검증 동결·경로 봉쇄가 충분하지 않음이 확인됐다. 생성 파일과 로컬 MySQL 기록은 삭제하거나 고쳐 쓰지 않고 `RejectedAfterIndependentAudit / PreservedNotPromoted`로 보존한다. 이 문서가 정정판 `g3a.r2`의 유일한 구현 기준선이다.

## G3a 정정 범위

1. 서울 열린데이터광장 `OA-23081` 동결 XLSX 전체 21,776행을 읽는다.
2. 좌표 결손 1행은 추정하지 않고 원본 결손으로 집계한다.
3. 좌표가 있는 점을 `OA-22160` 역사 경계 후보 30개에 `covers`로 대조하고 정확히 하나의 경계가 덮는 점만 그 경계 소유 후보로 기록한다.
4. 공유 경계·복수 판정은 어느 동에도 복제하지 않고 전역 `Unresolved`로 한 번만 보존한다. 30개 동 밖 점은 상세 원장에 저장하지 않고 감사 집계만 남긴다.
5. 원본 자치구와 공간 귀속 경계의 자치구가 다른 21건은 역사 경계 소유 후보를 유지하되 정상 후보로 숨기지 않는다. `SourceDistrictSpatialAssignmentConflict`, 원본 자치구, 귀속 자치구, 역사 경계까지의 거리를 기록하고 별도 품질 집계에 포함한다.
6. 결정적 후보 묶음을 저장소 내부 `artifacts/local`의 새 hash 세대로 만들고, 로컬 MySQL 범용 공공자료 원장에 새 dataset/revision으로 `PendingHumanReview` 저장·독립 재조회한다.
7. 기존 사가정 1km 자료, 30개 행정동 r2 모듈, Mongo candidate/current, Unity Scene·Prefab·GameObject와 `g3a.r1` 자료는 수정하지 않는다.

## 동결 출처와 판정 상한

| 역할 | 판본·SHA-256 | 판정 |
| --- | --- | --- |
| 횡단보도 점 | `OA-23081`, 2026-08-24, `1A5DB9EA7A1CD58E2D7F2B4246BAF099A3E4D5278F2867A1B87F3D50D21541BE` | 21,776행, 좌표 결손 1행, 공공누리 제1유형 |
| 획득 영수증 | `sagajeong-crosswalk-acquisition.r2`, `AD9BD163328B66A0B700EE100249DCA60193226EF156D3D1D5FB02CF6642889C` | 영수증의 좌표계 추론은 원천 선언으로 사용하지 않음 |
| 역사 행정동 경계 | `OA-22160`, 2023-10-31, `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68` | `EPSG:5181`, 현행 정본 아님 |
| 30개 동 범위 | `northeast-seoul-rider.r2`, `CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B` | 정확 HJD 30개 |
| 30개 동 범위 영수증 | r2 scope manifest, `5F651C5460173A5C2EFD68F0E04DB06880B8F8F4CAE74A38AD0BDE11637D5113` | 경계 원본 hash와 정확 집합 재검증 |
| 사가정역 기준점 선택 | `station-reference-selection.r1`, `0EFE8A791CBD9D0171997B09ACD71A24C8E7D39F50FA62AD8D22E57CD9768CEC` | 국가철도공단 원본에서 선택한 검토 자료 |
| 사가정역 기준점 원본 | 도시철도 역사정보 XLSX, `CDF1D84A7E5C898B2AACD622783BA8BA9AF35C40BEE0561DC97D55CE8E063F94` | WGS84 경도 127.088502, 위도 37.580912 |

모든 결과는 `PrivateReviewOnly / HistoricalBoundaryBootstrapOnly / PendingHumanReview`다. 원천 페이지와 시트가 좌표계를 직접 선언하지 않으므로 EPSG:5186은 `EmpiricallyValidatedCandidateNotSourceDeclared`를 유지한다.

## 좌표계와 역사 경계 변환 검증

생성기는 범위 파일의 결과값끼리 비교하는 데 그치지 않고 다음 외부 동결 입력과 계산을 다시 수행한다.

- 사가정역 기준점은 위 선택 파일에서 정확한 stable ID `station:kr:kric:s1107:0722`를 찾아 읽고 선택 파일·원본 XLSX hash를 모두 확인한다.
- WGS84 기준점을 설치된 EPSG DB로 EPSG:5186에 다시 변환하고, 동결 횡단보도 `06-0000016264`와의 거리 `8.0946m`를 허용 오차 안에서 재현한다.
- EPSG:5181·5174·2097·5179 대안 해석의 전체 파일 최소 거리 증거를 다시 계산하거나, 그 계산 결과를 canonical evidence digest로 재계산해 동결 digest와 대조한다. 범위 파일이 제공한 숫자만 서로 비교해 통과시키지 않는다.
- `historicalBoundaryConversion`은 `EPSG:5181 → EPSG:5186`, X 0m, Y +100,000m, 회전·축척·추정 평행이동 없음이라는 정확 객체와 digest를 검증한다.
- 후보 귀속 결과의 feasibility digest `DC34A66F2390281AA5BF4D70F77ABE87AEEBCFEC97328B5AE5C6D5D7FA62DDC3`를 별도로 재계산한다.

이 증거는 EPSG:5186 해석을 강하게 지지하지만 공급자 좌표 사전의 원천 선언을 대신하지 않는다.

## 결정적 후보 묶음 r2

- 범위 정의: `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-crosswalk.g3a.r2.json`
- 생성기: `eng/neighborhood/administrative_dong_crosswalk_candidate.py`
- 출력: `artifacts/local/public-data/admin-dong-crosswalk-northeast-seoul-20260915-g3a-r2/generations/{candidateSetHash}/`
- 파일: `manifest.json`, `candidates.ndjson`, `audit.json`, `complete.json`

후보 집합 hash는 UTF-8 문자열마다 big-endian 32-bit 길이를 먼저 넣는 순차 SHA-256이다. 헤더에는 최소한 schema, `g3a.r2` revision, 설계 문서 hash, OA-23081 원본·영수증 hash, OA-22160 경계 원본 hash, 실제로 전달된 r2 범위 정의 hash, r2 scope-manifest hash, 실제로 전달된 g3a r2 scope 파일 hash, 사가정역 선택·원본 hash, 생성기 hash, 좌표 검증 hash, 역사 경계 변환 hash, feasibility digest가 들어간다. 기본 scope가 아닌 다른 scope를 받았을 때 기본 scope hash를 보고하거나 같은 세대를 재사용해서는 안 된다.

후보 hash 필드에는 원천 feature key, 귀속 동, assignment state, 품질 code, 원본·귀속 자치구, 경계 거리, 후보 동 집합, 관리번호, 교차로 참조, 종류, 보행등 설치 유무, 원본 좌표 후보와 공통 ENU millimeter를 포함한다. 21개 충돌 진단의 변경은 반드시 후보 집합 hash를 바꾼다.

## 경로와 원자성 봉쇄

- `--root`, `--scope`, 출력 root, `staging`, `generations`, generation과 그 모든 부모는 해소된 절대 경로가 저장소 root 안이어야 한다.
- scope와 출력 경로에서 symlink·junction·reparse point를 허용하지 않는다. 생성 직전과 원자 이동 직전에 다시 검사한다.
- 고유 staging에 네 파일을 모두 쓰고 내용·hash를 검증한 뒤 hash generation으로 원자 이동한다. `complete.json` 없는 세대는 소비하지 않는다.
- 기존 세대가 있으면 모든 파일 byte equality를 확인하고 하나라도 다르면 collision으로 거절한다.

## 로컬 RDB 후보 원장

- `SourceId`: `seoul-open-data-admin-dong-crosswalk-private-review`
- `DatasetId`: `northeast-seoul-admin-dong-crosswalk-candidate-g3a-r2`
- `MetricCode`: `administrative-dong-crosswalk-point-candidate`
- `SchemaVersion`: `administrative-dong-crosswalk-candidate.v1`
- `DataRevision`: `northeast-seoul-admin-dong-crosswalk-candidate.g3a.r2`
- `EvidenceAsOfUtc`: `2026-08-24T00:00:00Z`
- `DerivedAtUtc`: `2026-09-15T00:00:00Z`

새 업무 테이블이나 migration을 만들지 않고 기존 `PublicDataIngestionDbContext`의 범용 원장과 preflight, DB named lock, transaction, 500행 단위 멱등 upsert를 재사용한다. payload에는 상세주소·담당자·실시간 신호·원본 전체 행을 넣지 않는다. 첫 적용 후 새 DB 연결에서 정확 1,533개 key·30개 동 분포·21개 충돌 진단·후보 hash·원본 snapshot을 재조회하고, 반복 적용은 신규·갱신 0이어야 한다.

이미 저장된 r1의 1,533개 normalized row와 11개 raw snapshot은 감사 이력으로 보존한다. r2 importer는 이를 삭제·갱신·승격하지 않고 새 DatasetId와 revision으로 병존시킨다.

## 권위와 표시 상한

manifest와 모든 후보는 `privateReviewOnly=true`, `historicalBoundaryBootstrapOnly=true`, `observationCandidateOnly=true`를 강제하고 다음을 모두 `false`로 둔다.

`currentAdministrativeBoundaryEstablished`, `sourceDeclaredCoordinateReference`, `crosswalkGeometryEstablished`, `stopLineEstablished`, `signalPhaseTimingEstablished`, `sidewalkConnectionEstablished`, `distributionApproved`, `publicDisplayAllowed`, `runtimeAuthorized`, `traversalReady`, `gameplayReady`, `unityApplyAllowed`.

`pedestrianSignalPresent`는 보행등 설치 유무 관찰일 뿐 현재 색상·잔여 시간·주기·통행 허용을 뜻하지 않는다. UI, Unity, 주문·배차·NPC 의사결정에는 연결하지 않는다.

## 검증과 완료 의미

- 원본 길이·hash·헤더·21,776행·결손 1행, 정확한 30 HJD와 모든 동의 후보 존재를 확인한다.
- 합성 경계로 유일 귀속·공유 경계 unresolved·범위 밖 제외·자치구 충돌 진단을 시험한다.
- 후보 1,533개, 충돌 21개, 보행등 있음 933개·없음 600개, 동별 분포와 결정적 hash를 독립 재계산한다.
- 낮은 revision, 알 수 없는 행정동·품질 code, true 권위 플래그, 파일·완료 표식 변조, 저장소 밖 scope/output와 reparse 경로를 거절한다.
- 같은 입력으로 두 번 생성해 두 번째 변경 파일 0을 확인한다.
- C# 자체 시험·직접 build·MySQL 첫 apply·반복 apply·새 연결 exact-set readback을 수행한다.
- Mongo current와 Unity 자료·Scene·Prefab이 바뀌지 않았음을 범위상 확인한다.

완료 상한은 `G3a CrosswalkPointCandidatesStored / HistoricalBoundaryCandidateOnly / SourceCRSNotDeclared`다. 30개 동별 횡단보도·보행등 설치 후보 coverage와 21개 경계 충돌을 비교할 수 있지만 `G3 MobilitySurface`나 `G7 UnityLifeView`를 닫지 않는다. 다음 절편은 현행 행정동 경계 재귀속이며, 그 전의 보행망·차선 표시·교차로·방향 자료는 각 원본 판본과 품질을 분리해 후속 후보 원장으로 수집한다.
