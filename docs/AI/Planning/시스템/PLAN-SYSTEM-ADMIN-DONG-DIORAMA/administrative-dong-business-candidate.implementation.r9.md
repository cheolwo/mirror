# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r9]

## 승인 근거

2026-09-15 사용자는 동북서울 30개 행정동 디오라마를 사가정역 수준으로 계속 발전시키고, 주소·도로·신호뿐 아니라 실제 지역 생활상이 드러날 공공자료를 충분히 조사·축적해 달라고 요청했다.

이번 G4a 절편은 소상공인시장진흥공단 상가 관측을 30개 행정동별 비공개 후보 원장으로 저장한다. 정확 상호명과 주소를 `.gitignore` 아래 local-private 생성물과 보호 RDB에 재검토 가능한 원문 관측으로 보존하지만, 현재 영업·실제 입점 건물·상인 Claim·주문 가능·공개 표시 또는 Unity 생활상 권위를 만들지 않는다.

## 동결 입력과 정확 범위

| 역할 | 동결 값 | 판정 |
| --- | --- | --- |
| 상가 원본 | `소상공인시장진흥공단_상가(상권)정보_20260630.zip` | 기준일 2026-06-30, 게시·수정 2026-08-05 |
| 원본 SHA-256 | `57AA361544108FF4FC73334F87D2CED61E41FE8638DD70BCD842930F865D2386` | 352,699,739 bytes, 변경 시 거절 |
| 서울 CSV | `소상공인시장진흥공단_상가(상권)정보_서울_202606.csv` | raw 이름 CP949 51 bytes·Base64 `vNK787D4wM69w8DlwfjI77D4tNxfu/OwoSi787HHKcGkurhfvK2/718yMDI2MDYuY3N2`·SHA-256 `2C3149340CC8F500C21E47FB1D217D861F053A88B85AADFDBB638DAD8D929639`, 논리 이름 UTF-8 SHA-256 `C348217E1FE0276DDBB5FBE568E2AC29104040FCAE79F1FB86C5C7A7F11CD6E4`, 압축 해제 301,889,640 bytes·CRC32 `CC980DEE`·SHA-256 `08D3FD08B37840256CCCD4F09BAD6FC33133B647CBFF05F22F26FC962CF84152`, 554,092행 |
| 획득 영수증 | `artifacts/local/public-data/myeonmok-business-20260908-r1/receipt.json` | 2,567 bytes·SHA-256 `A15B270AB8AC12018F11471BF5F1C39B0E56D989639D91C6B1ADC9958EE373C8` |
| 내려받기 metadata | 같은 폴더의 `shop-download-metadata.json` | 10,637 bytes·SHA-256 `364E3468D1A4F908503AF83C373EADAD1546E564D7191BB03C57146673122775` |
| 자료 metadata | 같은 폴더의 `shop-metadata.json` | 1,593 bytes·SHA-256 `C2D61FB768B0C7EBBCA290FB580B9464AF20EC3EBC7975651EB520DA60F63BB2` |
| 이용조건 | 공공데이터포털 `이용허락범위 제한 없음` | 수집 허용과 공개·현재 영업·주문 승인은 별도 |
| 행정동·법정동 코드 | `artifacts/local/public-spatial/administrative-codes/20260301/jscode20260301.zip` | 2,129,571 bytes·SHA-256 `8AF8C1F122D67D43518F58B37AEA6EEA7986F2809062F24E2E03465F21AE7A08`; `KIKcd_H.20260301` 1,180,823 bytes·CRC32 `62570AA6`·SHA-256 `A45A60618D9974B79F9D22B4A3DB3972FCA20AE0577D2C9284EF31527CE7E044`; `KIKmix.20260301` 3,599,970 bytes·CRC32 `AD0F8066`·SHA-256 `74F3EDC5C02F3F242129949725B7301C0EA26D8303F03138FC440F6EBBDF0405` |
| 30개 동 범위 | `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json` | 11,323 bytes·SHA-256 `CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B` |
| 실제 범위 manifest | `artifacts/local/public-data/admin-dong-diorama-northeast-seoul-20260914-r2/scope-manifest.json` | 18,800 bytes·파일 SHA-256 `5F651C5460173A5C2EFD68F0E04DB06880B8F8F4CAE74A38AD0BDE11637D5113`·내부 content hash `CF2D4057F49BEA4542A641D6F9741A6FF66AA263EB6DB686E38AD03F24BC62D8` |
| 역사 경계 진단 | `artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip`, EPSG:5181 | 1,676,539 bytes·SHA-256 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`, 현행 경계 아님 |

SEMAS의 8자리 행정동 코드를 `+"00"` 같은 문자열 추측으로 바꾸지 않는다. 행정안전부 동결 원장에서 활성 10자리 코드와 유일하게 결속된 정확한 30-entry map만 사용한다. 명칭 표기 차이는 식별 근거로 사용하지 않는다.

## 동결 수량과 진단

- 대상 사업장 29,721곳, 음식 대분류 `I2` 8,246곳이다.
- 상가업소번호 결손·범위 내 중복은 0이며 대상 ID는 모두 `MA`로 시작하는 20자다. 생성기는 ZIP의 지역 CSV 16개·전국 2,772,484행을 streaming으로 모두 읽어 대상 29,721개 ID가 전국에서도 각각 정확히 한 번만 나타나는지 검증한다. 형식 불일치·누락·중복은 생성을 거절하며 전국 원문 행을 별도 보관하지 않는다.
- 원본 행정동 표시는 MOIS 정식명과 30개 중 26개가 다르므로 명칭 결속을 금지하고, 8자리 원본 코드와 active 10자리 MOIS 코드의 scope 고정 map만 사용한다.
- 좌표·도로명주소·지번주소 결손은 0이다. 건물관리번호 결손은 67곳이며 음식 업종은 그중 15곳이다.
- 비어 있지 않은 건물관리번호는 모두 25자리 숫자이고 고유 번호는 11,931개다. 한 건물관리번호에 여러 사업장이 있는 것은 정상 다중 입점 후보이며 사업장 ID로 합치지 않는다.
- 서로 다른 공급자 ID가 같은 원문 상호명·도로명주소를 가진 경우는 236그룹·490행이다. `PotentialSameNameRoadAddressIdentityCandidate / NotMerged`로 기록한다.
- 원본 행정동과 2023 역사 경계 점 대조는 일치 29,686, 다른 대상 동 경계 29, 대상 밖 6, 복수 0이다. 35건을 역사 경계로 재귀속하지 않고 원본 행정동 소유를 유지하면서 공간 진단을 함께 기록한다.
- 경계 충돌은 `1126060000→1126061000` 11, `1126066000→1126065500` 7, `1126069000→1126068000` 3, `1123057000→1123066000` 2, `1123073000→1123057000` 2, 나머지 네 쌍 각 1건이다. 대상 밖은 `1123056000` 1, `1123065000` 4, `1123074000` 1이다. 이 35행·13개 `(좌표, 원본 HJD, hit HJD/state)` 그룹을 exact 진단으로 고정한다.
- 기존 면목 SEMAS 5,411개 provider ID는 새 29,721개의 정확한 부분집합이며 새 범위 추가 ID는 24,310개다. 이는 병존·불변 검사 수량이지 기존 원장을 참조하거나 합치는 규칙이 아니다.
- SEMAS metadata는 경도·위도의 좌표 기준을 선언하지 않는다. WGS84 후보 해석을 통한 역사 경계 대조와 ENU 값은 `SourceCoordinateDatumUnconfirmed` 진단일 뿐 좌표 권위가 아니다.
- 원본 8자리 행정동과 행정안전부 원장의 법정동 코드·명칭, scope의 `legalAreaStableId`를 함께 대조하며 불일치는 0이다. 알 수 없는 코드나 HJD→BJD 불일치는 생성을 거절한다.

30개 동별 `전체/음식(I2)` 기대치는 다음과 같고 합계는 `29,721/8,246`이어야 한다.

```text
1121574000 1196/316; 1121575000 1190/251; 1121576000 785/225; 1121577000 921/241
1123056000 1706/462; 1123057000 517/138; 1123060000 929/207; 1123061000 1012/211
1123065000 2153/626; 1123066000 1688/517; 1123072000 625/170; 1123073000 544/155
1123074000 995/349; 1123075000 408/96; 1126052000 971/287; 1126054000 526/123
1126055000 362/92; 1126056500 1440/408; 1126057000 866/218; 1126057500 1246/406
1126058000 648/176; 1126059000 1578/586; 1126060000 517/91; 1126061000 1155/347
1126062000 929/258; 1126063000 923/253; 1126065500 1755/538; 1126066000 507/107
1126068000 1039/279; 1126069000 590/113
```

## 결정적 후보 묶음

- 범위 정의: `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider-business.g4a.r1.json`
- 생성기: `eng/neighborhood/administrative_dong_business_candidate.py`
- 출력: `artifacts/local/public-data/admin-dong-business-northeast-seoul-20260915-g4a-r1/generations/{candidateSetHash}/`
- 파일: aggregate-only `manifest.json`·`audit.json`, 보호 원문을 포함하는 local-private `candidates.ndjson`, revision별 생성기 바이트 `generator-source.py`, 마지막 완료 표식 `complete.json`

생성기는 ZIP의 지역 CSV 16개를 streaming으로 읽고 거대한 중간 `selected.json`을 만들지 않는다. 서울 entry에서 후보를 투영한 뒤 나머지 15개를 포함한 전국 2,772,484행에서는 선택 ID occurrence만 재검증한다. 원본 ZIP, 서울 entry, 획득 영수증·metadata, 행정동 코드 원본, 30개 scope와 scope manifest, 역사 경계, 실제 G4a scope, 설계와 생성기 hash를 후보 집합 hash에 포함한다. 생성기 자체도 hash별 세대에 복사해 가변 작업 경로만 기록하는 과거 재현성 결손을 반복하지 않는다. 동일 입력은 같은 generation을 만들고 기존 세대와 byte가 하나라도 다르면 collision으로 거절한다.

후보는 trim한 원문 상가업소번호의 ordinal 순으로 정렬한다. 각 원천 행은 39개 열을 header 순서대로 canonical framing한 `sourceRowHash`를 가진다. 후보 hash에는 공급자 ID, 원본 HJD 8자리와 행정안전부로 해소한 HJD 10자리/stable ID, 원본 법정동 코드·명칭, 상호·지점명, 업종 코드·명칭, 도로명·지번 주소, 건물관리번호·건물명·동·층·호, 원본 좌표 문자열, 역사 경계의 `covers` hit HJD 목록과 진단, 동일 이름·주소 후보 상태와 `identityMergeState=NotMerged`, 모든 권위 플래그를 포함한다. 상세 원문 39열 전체를 후보 payload에 복제하지는 않지만 `sourceRowHash`로 정확 행 계보를 보존한다.

후보 집합 hash는 serializer 기본값에 의존하지 않는다. header와 후보 값을 고정 ordinal 순서로 순회하며 각 값은 `presence byte(0=null, 1=present) + uint32 big-endian UTF-8 byte length + bytes`로 framing한다. bool은 소문자 `true|false`, 정수는 invariant decimal, 좌표는 CSV 원문 문자열 그대로 사용하고 부동소수로 재포맷하지 않는다. 배열은 count 뒤 ordinal item을 같은 방식으로 붙인다. `candidates.ndjson`은 UTF-8 no-BOM·LF·고정 property 순서로 쓰되 권위 hash는 위 length-prefixed framing으로 계산한다.

header framing 순서는 domain separator `administrative-dong-business-candidate-set.v1`, schema/revision, 설계 path/hash, G4a scope path/hash, 생성기 path/hash, SEMAS ZIP path/hash/bytes, 서울 entry의 논리 이름·논리 이름 hash·raw encoding·raw 이름 bytes/Base64/hash·압축 해제 bytes·CRC/content hash/행 수, 영수증·내려받기 metadata·자료 metadata의 path/hash/bytes, MOIS archive와 `KIKcd_H`·`KIKmix` 두 entry의 path/name/hash/bytes/CRC, r2 scope와 scope manifest의 path/file hash/bytes·내부 content hash, OA path/hash/bytes, mapping·source-row·boundary·coordinate·identity·expected-count 계약 hash다. 이어 `providerShopIdCanonical` ordinal 순 후보의 scope에 고정된 전체 공개·보호 필드를 framing한다. NDJSON 출력 순서와 hash 순서는 반드시 같다.

`candidates.ndjson`과 그 staging/generation 디렉터리는 `.gitignore`가 적용되는 `artifacts/local` 아래의 비공개 검토 자료다. 정확 상호·주소·원천 ID·건물관리번호·원좌표·층·호는 이 파일과 보호 RDB payload에만 존재해야 한다. `manifest.json`, `audit.json`, `complete.json`, CLI 출력과 로그에는 집계·digest·품질 코드만 두며 원문 값을 출력하지 않는다.

출력 root·staging·generation과 모든 부모는 저장소 내부이며 reparse point가 아니어야 한다. 다섯 파일을 고유 staging에 완성·검증한 뒤 hash generation으로 원자 이동하고 `complete.json` 없는 세대는 소비하지 않는다.

## 로컬 RDB 보호 원장

- `SourceId`: `semas-admin-dong-business-private-review`
- `DatasetId`: `northeast-seoul-admin-dong-business-candidate-g4a-r1`
- `MetricCode`: `administrative-dong-business-candidate`
- `SchemaVersion`: `administrative-dong-business-candidate.v1`
- `DataRevision`: `northeast-seoul-admin-dong-business-candidate.g4a.r1`
- `EvidenceAsOfUtc`: `2026-06-30T00:00:00Z`
- `DerivedAtUtc`: `2026-09-15T00:00:00Z`
- `CollectedAtUtc`·`FirstSeenAtUtc`·`LastSeenAtUtc`: 획득 영수증의 `2026-09-08T10:31:09.6612043Z`

기존 범용 `PublicDataIngestionDbContext`, 원본 등록 service와 멱등 저장소를 재사용하되 새 Source/Dataset/Revision namespace로 분리한다. 500행 이하 chunk, preflight exact-body 검사, DB named lock과 transaction을 사용하고 기존 행을 update하지 않는다. exact-body 비교에는 저장소가 발급하는 `RawSnapshotId`만 제외하고 모든 업무·계보 필드를 포함한다. source ZIP·entry·metadata·scope·설계·생성기 snapshot을 먼저 등록한 뒤 모든 후보가 그중 같은 canonical source ZIP snapshot을 참조하는지 독립 재조회한다. 같은 입력 재적용은 신규·갱신 0이어야 하며 새 DB 연결에서 exact 29,721개 key·30개 동 분포·음식 8,246·진단 35·결손 67·candidate hash를 재조회한다.

원장 식별자는 원천 ID를 노출하지 않는 다음 고정식을 사용한다.

- `providerShopIdCanonical = Trim(원문 상가업소번호)`이며 빈 값·중복을 거절한다.
- `providerBusinessIdSha256 = UPPERHEX(SHA256(UTF8(providerShopIdCanonical)))`다.
- `sourceFeatureKey = source-feature:semas:business:sha256:{lowerhex(providerBusinessIdSha256)}`다.
- `StableId = administrative-dong-business-candidate:semas:sha256:{lowerhex(providerBusinessIdSha256)}`다.
- `DimensionKey = business-candidate|semas|sha256|{providerBusinessIdSha256}`다.
- `RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId, mappedHjdStableId, MetricCode, EvidenceAsOfUtc, DimensionKey)`다.

StableId·DimensionKey·RecordKey는 각각 exact unique여야 한다. 같은 digest가 다른 원문 ID나 다른 본문을 가리키면 hash collision/identity conflict로 전체 적용을 거절한다. raw provider ID는 local-private candidate와 보호 RDB payload에만 두고 식별자·manifest·audit·로그에는 넣지 않는다.

기존 면목동 원장의 SEMAS 5,411행과 factory 126행은 삭제·갱신하지 않는다. 새 30개 동 원장은 5,411행을 참조하거나 덮어쓰지 않고 독립 revision으로 병존한다. 적용 전후 기존 원장의 전체 지속 상태 digest와 새 원장과의 provider ID overlap `5,411`, 새 ID `24,310`을 검사한다.

정확 상호명·지점명·도로명주소·지번주소·공급자 ID는 보호 RDB에서만 조회한다. `SourceVersion=source-row-sha256:{sourceRowHash}`, `UnitCode=source-business-observation`, `SpatialPrecisionCode=source-address-and-lonlat-crs-not-confirmed`, `TemporalPrecisionCode=source-date-only`로 저장하며 compact `TextValue`가 2,000자를 넘으면 거절한다. Unity 계약, Mongo diorama current, 공개 API나 로그에는 원문 보호 필드를 넣지 않는다.

## 품질과 권위 상한

기본 `QualityCode`는 `PendingHumanReview`다. 후보별 진단은 다음을 별도 필드로 둔다.

- `SourceAdministrativeDongMappedHistoricalBoundaryMatched`
- `SourceAdministrativeDongMappedHistoricalBoundaryConflict`
- `SourceAdministrativeDongMappedOutsideHistoricalBoundaryScope`
- `SourceCoordinateDatumUnconfirmed`
- `BuildingManagementNumberMissing`
- `PotentialSameNameRoadAddressIdentityCandidate`
- `CurrentOperationUnverified`
- `ExactBusinessDetailsPrivateLocalAndProtectedRdbOnly`

모든 후보는 `privateReviewOnly=true`이며 `currentAdministrativeBoundaryEstablished`, `sourceCoordinateDatumEstablished`, `exactBuildingBindingEstablished`, `currentOperationVerified`, `merchantClaimVerified`, `distributionApproved`, `publicDisplayAllowed`, `orderScenarioEligible`, `runtimeAuthorized`, `gameplayReady`, `unityApplyAllowed`를 모두 `false`로 둔다.

## 검증과 완료 의미

- ZIP·서울 entry·metadata·코드·scope·경계·설계·생성기 hash와 행 수를 확인한다.
- 정확한 30개 HJD map, 29,721/8,246/67/35/236그룹·490행 수량을 고정 시험과 독립 재계산으로 검증한다.
- 중복 provider ID, 알 수 없는 HJD, 이름 기반 결속, 공급자 좌표를 원천 선언 좌표계로 승격, 역사 경계 재귀속, 자동 동일업체 병합과 true 권위 플래그를 거절한다.
- 같은 입력의 2회 생성, Python 자체 시험·verify, C# build·self-test·preview·첫 apply·반복 apply·새 연결 readback을 수행한다.
- 기존 면목동 원장 digest, Mongo current와 Unity 자료·Scene·Prefab 불변을 확인한다.

완료 상한은 `G4a BusinessCandidateLedgerStoredAndVerified / ExactBusinessDetailsPrivateLocalAndProtectedRdbOnly`다. 실제 업체·음식점 아이콘, 메뉴, 주문 가능 여부, 광고·후원, NPC 목적지와 생활상 Game View는 만들지 않는다. 그것들은 현행 경계·건물관리번호/출입구의 확정 결속, 현재 영업 검토, Claim·배포 승인과 G5 Simulation 결속을 모두 통과한 뒤 별도 판본에서 연다.
