# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r11]

## 승인 범위

- 구현 판본: `administrative-dong-diorama:intersection-point-candidate-ledger.g3b.r1`
- 작업 종류: `LocalPrivatePublicDataCandidateLedger`
- 상태: `ApprovedForLocalPrivateRdbLedger`
- 소비 후보 집합:
  `DB83F1CC82EB65F864188DD790A003EDF570DBB459D56D4C07CE690988F2837B`
- 완료 상한: `LocalPrivateHistoricalIntersectionPointCandidateLedgerStoredAndVerified`

이 판본은 구현 r10이 만든 하나의 완결된 G3b 교차로 점 후보 세대를 기존 범용
공공자료 RDB에 비공개·검토보류 원장으로 저장하고 독립 재조회하는 절편이다.
r10의 생성 계약, 생성기, scope, 후보 파일과 content-addressed 세대는 수정하거나
다시 게시하지 않는다. `current` 포인터도 만들지 않는다.

이 문서는 PlayableLoop 작업 명세가 아니며 E7 또는 다른 E 단계를 주장하지 않는다.
MongoDB 저장, 현행 행정동·교차로 확정, 공개 API, Unity, 이동 graph, 차로·방향,
신호 제어, traversal과 gameplay는 모두 범위 밖이다.

## 동결 소비 입력

소비 가능한 유일한 세대 디렉터리는 다음과 같다.

`artifacts/local/public-data/admin-dong-intersection-northeast-seoul-20260915-g3b-r1/generations/db83f1cc82eb65f864188dd790a003edf570dbb459d56d4c07ce690988f2837b`

| 파일 | SHA-256 | 길이 |
| --- | --- | ---: |
| `manifest.json` | `17DBA105F6C9F4FA70235B0ACA62CDF61B17E6B585B05CABFAE0D608CC2C3BDC` | 22,428 bytes |
| `candidates.ndjson` | `2D72DEF4E530E84D6D3DC4A344B688A8694129C0DC8F95D3568F6468C5506ABD` | 1,562,936 bytes |
| `audit.json` | `C9028FC8861C8EE310823C4F5301930EE236F2B551497A438D5B03B6E7AF7694` | 18,952 bytes |
| `generator-source.py` | `7CA125BFAE2B18E1AABCE489AECC803B3A668DE6BEA142492EB94A1455BA6107` | 82,458 bytes |
| `complete.json` | `FC6D0A9410FA16D243E666DE9A3B4EB8BCCDAAF32FBCCF8FBBD3A2A8EF27603F` | 2,271 bytes |

완료 표식의 content hash는
`BBA981A5A90E7CFD55F177D0F90513C3B8A6D2E8E61A9491500D11767F9143EF`다.
파일명이나 manifest·complete의 선언값만 믿지 않는다. importer는 r10의 고정
header와 후보 필드 순서, unsigned big-endian 32-bit 길이 접두 UTF-8 framing을
별도로 구현하고 `candidates.ndjson` 전 행에서 후보 집합 hash를 다시 계산한다.
생성기와 공용 hash helper를 호출해 결과를 받아오는 방식은 독립 검증으로 보지
않는다.

다음 수량과 30개 행정동별 분포도 후보 본문에서 다시 계산해 모두 일치해야 한다.

- 원본 8,097 / 후보 554 / 대상 밖 7,543 / 복수 귀속 0
- 후보가 있는 역사 행정동 30
- 원본 자치구 일치 553 / 충돌 1
- 횡단보도 링크가 있는 후보 524 / 없는 후보 30
- 횡단보도 링크 관계 1,491 / 같은 역사 행정동 1,271 / 다른 동 220
- 모든 후보의 `crosswalkAssignmentInherited=false`

후보 세대 hash, 파일 hash·길이, generator snapshot bytes, scope별 수량 중 하나라도
다르면 RDB 연결 전 거절한다. live generator와 생성 세대의 `generator-source.py`는
각각 정확한 bytes로 읽고 둘 다 위 generator hash와 일치해야 한다.

## 격리된 RDB 원장 계약

- `SourceId`: `seoul-open-data-admin-dong-intersection-private-review`
- `DatasetId`: `northeast-seoul-admin-dong-intersection-point-candidate-g3b-r1`
- `MetricCode`: `administrative-dong-intersection-point-candidate`
- `SchemaVersion`: `administrative-dong-intersection-point-candidate.v1`
- `DataRevision`: `northeast-seoul-admin-dong-intersection-point-candidate.g3b.r1`
- `EvidenceAsOfUtc`: `2025-08-14T00:00:00Z`
- `DerivedAtUtc`: `2026-09-15T00:00:00Z`
- DB named lock: `mirror:public-data:admin-dong-intersection-g3b-r1`

새 업무 테이블이나 migration을 만들지 않고 기존
`PublicDataIngestionDbContext`, 원본 등록 service와 범용 정규화 원장을 재사용한다.
다른 자료의 Source/Dataset/Revision을 재사용하거나 그 행을 update하지 않는다.

후보의 원천 관리번호를 RDB key와 표준 출력에 노출하지 않는다. 다음 비가역
식별식을 사용한다.

1. `candidateIdentitySha256 = UPPERHEX(SHA256(UTF8(candidateStableId)))`
2. `StableId = administrative-dong-intersection-point-candidate:sha256:{lowerhex(candidateIdentitySha256)}`
3. `DimensionKey = intersection-point-candidate|sha256|{candidateIdentitySha256}`
4. `RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId, administrativeAreaStableId, MetricCode, EvidenceAsOfUtc, DimensionKey)`

`candidateStableId`, 위 digest, `StableId`, `DimensionKey`, `RecordKey`는 각각 정확히
554개이고 모두 고유해야 한다. 같은 digest가 다른 후보 본문을 가리키면 hash
collision 또는 identity conflict로 전체 적용을 거절한다.

정규화 payload는 후보의 검토 의미와 계보만 담는 2,000자 이하 canonical JSON이다.
후보 전체 JSON을 임의로 잘라 넣지 않는다. 최소한 후보 본문 SHA-256, 행정동 stable
ID, 원천·경계·후보 집합 hash, 자체 점 귀속 상태, 자치구 충돌 여부, 횡단보도 링크
수·링크 집합 hash, 품질 코드와 모든 권위 플래그를 포함한다. 링크 개별 목록과 원천
관리번호는 raw snapshot에만 남긴다. `RawSnapshotId`는 등록 service가 발급하므로
exact-body 비교에서만 제외하고, 나머지 모든 업무·계보 필드는 byte-equivalent
canonical 값으로 비교한다.

## 원본·설계·생성 계보 snapshot

적용은 다음 자료를 각각 다른 artifact DatasetId의 raw snapshot으로 등록한다.
각 snapshot은 실제 bytes의 SHA-256·길이·content type·저장소 상대 경로를 검증한다.

1. `OA-15534` 원본 ZIP과 획득 영수증
2. `OA-22160` 역사 행정동 경계 ZIP
3. `northeast-seoul-rider.r2.json`과 실제 r2 `scope-manifest.json`
4. 구현 r10 설계 문서와 r10 자료 구현 계약
5. G3b 전용 `northeast-seoul-rider-intersection.g3b.r1.json`
6. 실행한 live `administrative_dong_intersection_candidate.py` bytes
7. 완결 세대의 `manifest.json`, `candidates.ndjson`, `audit.json`,
   `generator-source.py`, `complete.json`
8. G3a r2 연결 계보의 scope 및 완결 세대
   `manifest.json`, `candidates.ndjson`, `audit.json`, `complete.json`

live generator snapshot과 생성된 `generator-source.py`는 내용이 같더라도 서로 다른
역할·DatasetId로 둘 다 보존한다. G3a 자료는 링크 계보 증명용 새 snapshot일 뿐,
기존 G3a 정규화 행이나 그 13개 snapshot을 참조 변경·삭제·갱신하지 않는다.

## 적용 원자성·멱등성

`preview`와 `verify`는 읽기 전용이다. `apply`는 다음 순서를 지킨다.

1. 파일·hash·framing·수량·30개 동 분포·권위 플래그를 DB 연결 전에 검증한다.
2. 대상 원장의 기대 554개 exact body를 메모리에서 완성한다.
3. G3b named lock과 보존 원장별 lock을 고정 ordinal 순서로 획득한다.
4. 한 DB transaction 안에서 snapshot 충돌과 기존 대상 행 exact-body를 모두
   preflight한다. 이 검사가 끝나기 전에는 첫 write를 하지 않는다.
5. 계보 snapshot을 등록하고 대상 행을 최대 500개씩 처리한다. 기존 exact 행은
   그대로 두고, 불일치 행은 update하지 않고 전체 transaction을 rollback한다.
6. commit 뒤 완전히 새 `PublicDataIngestionDbContext`와 새 DB 연결로 snapshot,
   554개 exact key/body, 30개 동 분포, 후보 hash와 계보 결속을 재조회한다.
7. 같은 입력의 반복 적용은 normalized 신규 0·갱신 0, snapshot 신규 0이어야 한다.
8. lock은 성공·실패 모두 역순으로 해제한다.

`Contains` 조회, preflight, insert와 재조회는 모두 500개 이하 chunk를 사용한다.
부분 일치, 낮은 revision, 동일 key의 다른 body, 동일 snapshot identity의 다른 bytes,
누락 snapshot, 알 수 없는 행정동, true 권위 플래그는 전체 적용을 거절한다.

## 기존 원장 불변 관문

적용 전과 commit 뒤 새 연결 재조회에서 다음 세 원장의 지속 상태 digest를 각각
독립 계산한다. G3a·G4a는 정렬된 normalized row의 모든 지속 필드와 raw snapshot의
identity·hash·길이·상태를 length-prefixed framing으로 묶는다. 기존 면목 원장은
G4a importer의 기존 canonical digest 식을 그대로 사용해 shop·factory 행, 그 행이
참조하는 raw snapshot과 ingestion run의 모든 지속 필드를 함께 묶는다. 개수와
digest가 하나라도 달라지면 G3b 적용을 성공으로 보고하지 않는다.

- G3a r2: normalized 1,533행, raw snapshot 13개
- G4a r1: normalized 29,721행, raw snapshot 19개
- 기존 면목 SEMAS 원장: shop 5,411행, factory 126행

G3a·G4a·기존 면목 원장에는 insert, update, delete를 수행하지 않는다. 새 G3b
snapshot과 normalized row는 위 원장들의 DatasetId 또는 revision에 속할 수 없다.

## 품질·권위 상한

RDB에 저장된 모든 후보는 `PendingHumanReview`, `PrivateReviewOnly`,
`HistoricalBoundaryBootstrapOnly`, `ObservationCandidateOnly`다. 다음 상태는 전부
`false`로 보존한다.

`currentAdministrativeBoundaryEstablished`, `currentIntersectionTopologyEstablished`,
`approachDirectionEstablished`, `controllerBindingEstablished`, `laneBindingEstablished`,
`signalBindingEstablished`, `crosswalkAssignmentInherited`, `distributionApproved`,
`publicDisplayAllowed`, `runtimeAuthorized`, `traversalReady`, `gameplayReady`,
`unityApplyAllowed`.

로컬 DB 저장 완료는 이 후보 판본의 재조회 사실만 뜻하며 현행성·공개·이동·신호
또는 Unity 권위를 부여하지 않는다. 표준 출력에는 후보별 좌표·관리번호·연결 목록을
내보내지 않고 수량, digest, hash, boolean과 오류 code만 출력한다.

## 검증 및 완료 조건

- C# 자체 시험에서 framing 경계, 후보 key 결정성, 중복·충돌, exact-body mismatch,
  500행 chunk 경계와 보존 digest 변조를 확인한다.
- 직접 build 후 `self-test`, `preview`, 첫 `apply`, `verify`, 반복 `apply`를 수행한다.
- 첫 적용은 정확 554개 후보와 정의된 snapshot만 추가한다.
- 새 연결에서 후보 집합
  `DB83F1CC82EB65F864188DD790A003EDF570DBB459D56D4C07CE690988F2837B`,
  30개 동 분포와 링크·충돌 수량을 재현한다.
- 반복 적용은 모든 신규·갱신 수량이 0이고 같은 readback hash를 반환한다.
- G3a r2, G4a r1, 기존 면목 원장의 적용 전후 수량·digest가 동일하다.
- 범위 지정 Fast 검증을 통과하고 상세 출력은 `artifacts/local/validation/`에 둔다.

이 조건을 모두 통과해도 완료 표현은
`LocalPrivateHistoricalIntersectionPointCandidateLedgerStoredAndVerified`뿐이다.
r10 생성 계약과 산출물, Mongo/current/API/Unity/traversal/gameplay는 변경하지 않는다.

동반 자료 구현 계약은
`administrative-dong-intersection-point-ledger.data-implementation.v1.json`으로
관리한다. 이 문서는 동반 계약의 hash를 참조하지 않아 두 파일 사이 순환 hash를
만들지 않는다.
