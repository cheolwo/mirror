# 관찰용 공공자료의 로컬 DB 계보·정규화 반입

기존 `PublicDataIngestionDbContext`의 Run·RawSnapshot·NormalizedRecord를 사용한다. 원본 파일과 정규화 JSON은 비공개 파일에 유지하고 원본 위치·SHA·판본·관측값을 연결한다. 기존 동결 importer·업무 DB·Simulation·팩을 변경하거나 수집을 실행하는 도구가 아니다.

## 실행 계약

```powershell
dotnet build eng/observer-public-data-db/ObserverPublicDataDb.csproj
dotnet run --no-build --project eng/observer-public-data-db -- self-test

dotnet run --no-build --project eng/observer-public-data-db -- preview --root C:/Users/user/source/repos/Hongdal --manifest C:/Users/user/source/repos/Hongdal/artifacts/local/.../manifest.json --sha256 <SHA256> --receipt C:/Users/user/source/repos/Hongdal/artifacts/local/.../preview.json
```

`preview`는 충돌·기존 상태를 읽고, `apply`는 누락된 원본·실행·정규화 행만 하나의 트랜잭션으로 추가한다. `verify`는 모든 입력이 저장되어 있고 완전히 일치하는지 읽기 전용으로 확인한다. 세 명령 모두 별도 문맥으로 최종 재조회한다. 영수증 경로는 매번 새 경로를 사용하며 기존 파일을 덮어쓰지 않는다. `self-test`는 Docker·DB·네트워크 없이 실행한다.

소유가 일치하는 기존 `hongdal-mysql-1` / loopback `13306` / `hongdal_dev` / 비root 계정만 허용한다. 기존 `로컬공공자료Db.OptionsAsync`를 링크하고, 현재 compose의 명시 로컬 연결 호환은 process 범위에서만 준비·복원한다. 컨테이너 시작/중지·Migration·계정/영구 설정 변경은 없다. 읽기 모드에는 SaveChanges 차단기를 붙인다.

## 입력 형식

최상위는 `{"schemaVersion":"observer-public-data-db.v1","entries":[...]}`이다. 최대64항목, 전체100,000정규화행, 파일당64MiB, manifest2MiB다. 중복/미지원 JSON 필드·중복 RecordKey·경로 이탈·reparse point·지문 불일치를 거절한다. manifest와 receipt는 `artifacts/local/` 아래 절대 경로, 아래 파일 참조는 `/`를 쓰는 저장소 상대 경로다. 검증한 입력은 작업 종료까지 쓰기/삭제를 막는 읽기 핸들로 유지한다.

항목 필드는 다음과 같다.

```json
{
  "kind": "observations",
  "sourceId": "existing-source-id",
  "datasetId": "existing-dataset-id",
  "sourceVersion": "r1",
  "dataRevision": "r1:sha256",
  "collectedAtUtc": "2026-11-13T01:00:00.000000Z",
  "evidenceAsOfUtc": "2024-01-01T00:00:00.000000Z",
  "rawPath": "artifacts/local/.../source.csv",
  "rawSha256": "64자리SHA256",
  "contentType": "text/csv",
  "officialUrl": "https://example.go.kr/dataset",
  "recordsPath": "artifacts/local/.../records.json",
  "recordsSha256": "64자리SHA256"
}
```

`metadata`·`reference`는 `recordsPath/recordsSha256=null`이고 정규화0이다. 기준일 미확인은 `evidenceAsOfUtc=null`로 유지한다. 관측값의 기준일 미확인은 정규화 행으로 받지 않으며 수집 시각으로 채우지 않는다.

정규화 JSON은 배열이며 각 행은 `sourceId`, `datasetId`, `recordKey`, `stableId`, `regionStableId`, `metricCode`, `numericValue`, `textValue`, `unitCode`, `evidenceAsOfUtc`, `collectedAtUtc`, `spatialPrecisionCode`, `temporalPrecisionCode`, `qualityCode`, `limitationCode`, `dimensionKey`, `sourceVersion`, `dataRevision`을 가진다. Source/Dataset/Version/Revision/CollectedAt은 항목과 정확히 일치해야 하고 실제 RawSnapshotId는 도구가 결속한다. `RecordKey`는 준비된64자리 키를 사용하며 기존 다른 원본의 키와 충돌하면 거절한다. 숫자 결측은 null이며 0으로 바꾸지 않는다. DECIMAL(28,10)에서 손실될 값은 거절한다.

## 시각·판본·멱등성

모든 입력 시각은 UTC·소수초 최대6자리여야 한다. 실제 세 테이블의 datetime10열이 precision6인지 DB에서 먼저 확인한다. 도구가 시각을 조용히 절삭·반올림하지 않는다. 원문이7자리인 자료를 준비 단계에서 명시적으로 절삭했다면 원래 시각을 원문·준비 영수증·행의 textValue 등에 보존한다.

동일 Source/Dataset/원본SHA가 이미 있으면 저장 경로·판본·시간·종류·정규화SHA·공식URL 지문과 정규화 행 전체가 일치해야만 no-op이다. 기존 행을 고치거나 LastSeen을 갱신하거나 실행 행을 추가하지 않는다. 같은 원문에 다른 정규화 판본을 덧붙이는 작업도 충돌로 반환해 별도 검토한다. 일반 upsert는 사용하지 않는다.

apply는 advisory lock을 얻고 전체 입력을 사전 대조한다. 새 행을 저장한 뒤 트랜잭션 안에서 tracking을 비우고 전체를 다시 확인한 후 commit한다. 쓰기 연결을 닫은 뒤 새로운 pooling=false 문맥으로 영속 결과를 재확인한다. 두 번 더 apply하여 `strictNoOp=true`, `databaseWriteAttempted=false`, counts 불변을 확인할 수 있다. DB 전체 counts는 다른 스레드의 변경도 포함할 수 있으므로 항목별 독립 재조회 결과와 함께 읽는다.

예외는 자체 오류 코드 또는 타입 이름만 출력하며 연결 문자열·비밀값·원예외·실제 관측 행을 영수증에 넣지 않는다. DB 반입 성공은 배포·게임 적용·관측 사실의 사람 검토 승인을 뜻하지 않는다.

## r159 준비 자료와 실제 반입 범위

[작업 명세](../../docs/AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/public-data-db-storage.work-order.r159.md)가 소유한 로컬 준비다. 범용 정기 반입 설정이나 사용자용 자료팩이 아니다.

- `PrepareResearchCatalog.py`: 원 조사 JSON의 예상 SHA를 명시해 후보26개를 개별 안내 JSON으로 처음 분리한다. 원본 시각을 알 수 없으므로 준비시각과 관측시각을 구별한다. 파일이 이미 있으면 덮어쓰지 않고 기존 준비 결과를 재사용한다.
- `PrepareObservations.py --root <저장소>`: 기존 영수증·원문18개에서 45,666행을 재현한다. 원문 파일은 바꾸지 않으며 관세청 시각 정밀도 처리와 원래 시각 보존을 명시한다. 산출물은 고정된 `artifacts/local/public-data-db-20261113/` 아래에만 쓴다. 반입 후 동결된 파일을 바꿔야 한다면 별도 판본/작업 범위를 먼저 정한다.
- `PrepareManifest.py --root <저장소>`: SHA를 대조해 44개 준비 항목을 엄격한 DB 도구 형식으로 투영한다. 존재하는 동결 명세와 다르면 거절한다.
- `PrepareManifest.py --root <저장소> --db-storage`: `already-stored-verified.json`에서 기존 관세청3원문·12행의 의미 일치를 확인한 경우에만 이3개를 제외한41개 반입 명세를 만든다. 기존 자료를 복제하거나 그 수집 실행·원본·정규화 행을 갱신하지 않는다.
- `ReadObservations.sql`: 이번 정규화 판본의 집계·결측·계보 조회 예시다. 기존 관세청12행은 종전 판본이므로 원문 SHA와 기존 Source/Dataset으로 별도 확인한다.

원본18개는 실제 수치 관측7개·시점 미확인 참조3개·안내8개다. 후보26개 안내를 합친44개 중, 관세청3원문은 이미 DB에 있어 추가 반입에서는 제외한다. 신규 반입 대상은 원본/안내41개와 정규화45,654행이며, 별도 검증한 기존12행까지 합치면 확인한 관측은45,666행이다. 수치가 없는 BLS1행은 null로 보존한다.

실제 실행 명세·SHA·영수증은 위 로컬 폴더에 보존한다. 원문은 파일 저장소에 남고 DB는 경로·SHA·계보와 정규화 관측값을 저장한다. 원본 파일의 외부 공개·Drive 업로드·게임 연결·자동 예약은 이 도구가 수행하지 않는다.
