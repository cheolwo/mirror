# 이번 공식 공간자료의 로컬 비공개 계보 반입

기존 `평창군공공공간원본등록Service.RegisterFileAsync`, `PublicDataIngestionDbContext` 및 `로컬공공자료Db.OptionsAsync`를 그대로 재사용한다. 기존 원본 파일·수집 receipt·publisher 기준일을 새 자료와 구별한다. 실제 파일은 Git 제외 `artifacts/local/`에 보존하며 MySQL에는 출처/판본/hash/크기/비공개 파일 경로와 검토보류 계보를 등록한다. 파일 본문을 MySQL에 넣거나 CSV/SHP/건축HUB 행 전체를 정규화하는 도구가 아니다.

수집 파일당 원본과 새 intake receipt 두 개를 `public_data_raw_snapshots`에 등록하고 각각 `public_data_ingestion_runs`를 연결한다. receipt의 자료 ID는 원본 `datasetId + :receipt`다. `Partial / PendingHumanReview`, `NormalizedCount=0`을 유지한다. publisher 문서와 원래 취득 receipt는 새 intake receipt의 `originalReceiptPath / originalReceiptSha256`으로 연결하고 원문 hash를 추가 검증한다. 같은 SourceId/DatasetId/hash에 판본·경로·기준일·상태가 다르면 충돌로 거절한다.

대상은 정확히 기존 `hongdal-mysql-1 / 127.0.0.1:13306 / hongdal_dev`이다. 컨테이너 project/service/working_dir/포트를 검증한 뒤 기존 non-root 계정으로 process 메모리 안에만 명시 로컬 연결을 만든다. 기존 `SSALDDEL_PUBLIC_DATA_LOCAL_CONNECTION`이 있으면 기존 검증 분기로 검사한다. 기존 Compose·컨테이너·앱·서버 연결 설정은 수정하지 않는다. 키/비밀번호/원예외/연결 문자열은 로그·결과·파일로 출력하지 않는다.

## 입력과 실행

```powershell
dotnet run --project eng/neighborhood-data-collection/NeighborhoodDataCollection.csproj -- self-test
dotnet run --project eng/neighborhood-data-collection/NeighborhoodDataCollection.csproj -- preview C:/Users/user/source/repos/Hongdal artifacts/local/.../ingestion-manifest.json <exact-manifest-sha256>
dotnet run --project eng/neighborhood-data-collection/NeighborhoodDataCollection.csproj -- apply C:/Users/user/source/repos/Hongdal artifacts/local/.../ingestion-manifest.json <exact-manifest-sha256>
dotnet run --project eng/neighborhood-data-collection/NeighborhoodDataCollection.csproj -- verify C:/Users/user/source/repos/Hongdal artifacts/local/.../ingestion-manifest.json <exact-manifest-sha256>
```

`preview / verify`는 읽기 전용이다. `apply`는 새 기록을 트랜잭션 안에서 추가하고 별도 DbContext/물리 연결로 재조회한다. 이미 정확히 같은 기록이 있으면 RegisterFileAsync의 LastSeen 갱신도 호출하지 않아 DB 행을 바꾸지 않는 strict no-op이다. 동일 입력으로 `apply`를 두 번 더 실행하여 raw/run 신규0·ID 유지·쓰기 시도false를 확인한다. 출력 JSON은 호출자가 별도 비공개 artifacts 검증 폴더에 보관한다.

이번 고정 범위는 취득일 UTC **2026-10-02**·최대7 입력/14 원본 등록·파일당64MiB이며, `.go.kr` 또는 정확 `www.vworld.kr` HTTPS 출처만 받는다. 이는 전국 반복 수집, 새 scheduler나 범용 무인 수집 API가 아니다. 실제 source receipt 준비 전에는 self-test/build와 스키마 읽기 확인만 수행한다.

## Manifest 계약

schema는 `neighborhood-public-data-local-collection.v1`. 모든 JSON은 camelCase를 사용하며 중복/미지원 필드를 거절한다. 파일/receipt 경로는 `/`를 쓰는 `artifacts/local/` 아래 상대 경로이고 reparse point를 거절한다. SHA는 실제 원본 바이트의64자리16진수다. 날짜는 UTC offset0, MySQL6자리 microsecond 정밀도이며 확인되지 않은 기준일은 null이다.

```json
{
  "schemaVersion": "neighborhood-public-data-local-collection.v1",
  "batchId": "이번-고정-취득-범위-r1",
  "revision": "r1",
  "reviewState": "PendingHumanReview",
  "distributionApproved": false,
  "unityApplied": false,
  "records": [
    {
      "sourceId": "data-go-kr-ngii-dem-performance",
      "datasetId": "15067637",
      "sourceVersion": "20231107",
      "dataRevision": "이번-취득-판본-r1",
      "payloadKind": "DatasetInventory",
      "officialSourceUrl": "https://www.data.go.kr/data/15067637/fileData.do",
      "collectedAtUtc": "2026-10-02T12:00:00.000000Z",
      "evidenceAsOfUtc": null,
      "evidenceAsOfNote": "단일 파일 기준일 미확정; 행별 기준일은 별도 보존",
      "licenseCode": "실제-확인한-이용조건",
      "privateStorageAllowed": true,
      "rightsConflict": false,
      "file": {
        "path": "artifacts/local/이번-취득/실제-성과목록.csv",
        "sha256": "실제64자리SHA256",
        "bytes": 12345,
        "contentType": "text/csv"
      },
      "receipt": {
        "path": "artifacts/local/이번-취득/intake-receipt.json",
        "sha256": "실제64자리SHA256"
      }
    }
  ]
}
```

위 경로/시각/크기/hash/권리는 입력 형태 예시이며 실제 저장 입력이 아니다. 실제 receipt의 값으로 작성하고 root/자료 담당이 exact manifest SHA를 인계한다.

| payloadKind | 보존하는 의미 |
|---|---|
| `DatasetInventory` | 도엽별 성과목록 등 실제 공식 목록 레코드. DEM 고도 raster 자체로 표현하지 않음 |
| `SourceMetadata` | 공식 필드 정의·공급처 설명. 도형·수치 원본이 아님 |
| `PlanningGeometry` | 도시계획 도로면 등 계획 도형. 현재 현황도로·실폭·통행 권위가 아님 |
| `OfficialBuildingRegisterRecords` | 실제 건축HUB 공개 표제부 응답. 응답8표제부가 특정 target 건물 동일성 확정은 아님 |
| `PhysicalGeometryOrMeasurement` | 실제로 취득한 지리 도형/측정 원본. 자동 공개·Unity·현재 통행 승인은 아님 |

## Intake receipt 계약

schema는 `neighborhood-official-data-acquisition.v1`. 아래 필드는 manifest와 정확히 일치해야 한다. 원래 취득 receipt 연결 두 필드는 함께 제공하거나 함께 생략한다.

```json
{
  "schemaVersion": "neighborhood-official-data-acquisition.v1",
  "sourceId": "manifest와 같음",
  "datasetId": "manifest와 같음",
  "sourceVersion": "manifest와 같음",
  "officialSourceUrl": "manifest와 같음",
  "payloadPath": "manifest.file.path와 같음",
  "payloadSha256": "manifest.file.sha256와 같음",
  "payloadBytes": 12345,
  "payloadKind": "DatasetInventory",
  "collectedAtUtc": "2026-10-02T12:00:00.000000Z",
  "evidenceAsOfUtc": null,
  "evidenceAsOfNote": "manifest와 같음",
  "licenseCode": "manifest와 같음",
  "privateStorageAllowed": true,
  "rightsConflict": false,
  "distributionApproved": false,
  "runtimeAuthorized": false,
  "originalReceiptPath": "artifacts/local/이번-취득/original-acquisition-receipt.json",
  "originalReceiptSha256": "실제64자리SHA256"
}
```

AL_D010·RightsConflict 및 기존 frozen generation 파일은 이번 반입에서 거절한다. 저장은 권리 충돌 해소·새 공급원 활성화·current 게시·게임 적용을 뜻하지 않는다. migration/EnsureCreated/서버 host 시작/원격 운영 DB/공개 게시/Unity 변경/commit/push는 수행하지 않는다.

현재 빌드 오류0·경고0, guard self-test31개 통과. 이것은 실제 취득·MySQL 반입·서버 API 응답 성공의 증거와 구별한다. D429의 기본 축적 원칙은 `AGENTS.md:149`, `docs/AI/DECISIONS.md` D-429와 `docs/Architecture/게임자료조사전문운영.md`를 따른다.

## 2026-10-02 실제 반입 결과

`artifacts/local/neighborhood-data-collection/20261002-r1/ingestion-manifest.json`의 SHA256 `651984E4CD360ADC1631863A8E80EB03BB624C72E2654ACE392656E439A0751F`를 고정하여 7파일(22,425,083바이트)과 7 intake receipt를 실제 로컬 MySQL에 등록했다. RawSnapshot ID327~340, IngestionRun ID331~344이며 모두 `Partial / PendingHumanReview`다. RawSnapshot은316→330, IngestionRun은320→334이고 기존 NormalizedRecord71,081개는 그대로다.

apply 내부의 별도 연결 재조회 및 별도 verify 실행이14/14 통과했다. 동일 apply 두 번은 신규0·쓰기 시도false·commitfalse인 strict no-op이며, 두 실행 전후 신규14 raw/run의 모든 필드와 LastSeen 지문은 SHA256 `4462B8D9B72D424EE992708BFF967854A7AA9C087B5CCA7B97451E316A7CB23A`로 동일하다. 종료 후 process의 임시 명시 연결 환경값이 없는 상태도 확인했다. 원본7개·intake receipt7개·원래 취득 receipt7개의21개 hash를 다시 대조했다. 입력 receipt와 원본은 이 시점 이후 별도 새 판본 없이 수정하지 않는다.

과거 문서의 “Raw11 Vworld 정의서”는 현재 검증 대상 `hongdal_dev`와 일치하지 않았다. 현재 Raw11은 `representative-products-2026-09-21`이며, source/dataset/알려진 이전 hash로도 옛 정의서 행이 조회되지 않았다. 옛 XLSX 로컬 사본도 이 체크아웃에 없어 과거 원본 바이트와 새 XLSX의 동일성을 확정하지 않는다. 새 Vworld 정의서의 이번 binding은 Raw333이며 SHA256 `403BA657F9C2B112445793679BE5D6571E8F91FD8E6DF35097476A64CB48DF54`다. 출처·dataset·hash 단위 멱등 등록은 전역 콘텐츠 유일성 보장이 아니다.

실행 JSON·입력 바이트 검증·MySQL 원장 지문·빌드 및 guard 시험은 `artifacts/local/neighborhood-data-collection/20261002-r1/db/`에 있다. `verification-summary.json`이 종합 결과다. CSV/SHP/표제부 내용의 전체 정규화, DEM 고도 raster 취득, 권리 충돌 해소, API 실행, 공개 게시, Unity 적용은 이 결과에 포함하지 않는다.
