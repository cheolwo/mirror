# 공공데이터 소표본 검토 반입

## 면목동 집·길·터전 표본 검토

- [실행 결과·미완료 구간](../../docs/Reports/면목동-집길터전-공식자료와표본결속-2026-09-08.md). 정확 출력은 `artifacts/local/public-data/myeonmok-land-20260908-r1/`, 기존5537관측/지도/가격 원본은 고정hash로 보존한다.
- `land-prepare`는 기존 공개 관측의6그룹×5개를 ID순/기존 연결유형 할당으로 동결한다. 이미 폴더가 있으면 거부한다. 기존 건물 원장은 `land-inventory`로 읽기만 하며 면목동 원장으로 간주하지 않는다.
- `land-acquire`는 공식 메타4개와 건축HUB 표제부만: 최대30필지·필지당3페이지×100행/응답2MiB·합32MiB·30초·자동retry/redirect0. 첫403 뒤 나머지22필지는 요청하지 않았다. `land-acquire-resume`은 이미 보존한 최초BOM읽기 실패·메타/명세hash에 한정한 일회성 복구이며, 완료된 이번 수집을 재호출할 수 없다.
- `land-review`는 GIS 서울 원본을 다시 받지 않고 스트리밍해 표본 지번40행과 형상 구조를 검토한다. CRS는5186, 인코딩/권리와 위상 검증은 여전히 보류다. `land-replay`는 동결선정/GIS전체결과를 재현하되 출력/DB쓰기0. 임의 좌표스냅이나 주소 Assignment 호출은 없다.
- `land-apply`는 동결 selection/review/manifest3hash 및 기존입력9개·기존4테이블hash를 확인하고 기존 원문등록/수집 저장소에 파생 검토관측30개만 저장한다. 새 자료ID `myeonmok-spatial-review/myeonmok-building-parcel-road-sample`, 검토보류·비공개·NumericValue null. `land-verify`는 새 연결로 재조회한다. 같은 입력 추가0, 충돌 덮어쓰기0. 기존 확정연결0행 불변.
- [validate-land.ps1](validate-land.ps1)은 build/19신규+73기존 검사·실제원본 재파싱·DB읽기·동결hash/구간집계 확인이다. `-RepositoryRoot C:/Users/user/source/repos/Hongdal`. 재수집/DB저장은 검증스크립트에 포함하지 않는다.
- `BuildingParcel=공식식별연결`은 해당 Method/Reason의 **GIS A2 원천 PNU 참조만** 뜻한다. 필지 도형 확보/다필지 전체/접면/도로 접근 성공이 아니다. 14단일/8복수/8미연결은 지번→GIS 후보 판정이고, 전 구간 완료는0/30이다.

## 면목동 상가·등록공장 후속

- 사용자 승인으로 `business-*`를 추가했다. 기존 수집모드/동결 산출물은 보존한다. [결과·제한](../../docs/Reports/면목동-사업체상가제조업-수집-2026-09-08.md).
- `business-acquire`: 공식 메타데이터와 링크 확인 → 등록공장 CSV/상가 전국 ZIP 각1회. 새 폴더만 허용, retry/redirect 없음, ZIP390MiB·전체405MiB·ZIP240초/나머지30초 상한. 전국 ZIP은 원본 컨테이너일 뿐 전국 DB 반입이 아니다.
- `business-prepare`: 서울 CSV만 스트리밍 해석하고 법정동1126010100 및 주소 일치로 선택. 공장은 `(면목동)` 명시 주소 선택. 원문 모든 선택 필드는 selected.json에 보존, 기존 varchar2000 정규화에는 필요한 사업체/주소/업종/좌표/층/호수/원행hash만 기록한다.
- `business-preview`/`apply`/`verify`: 동결 receipt/원본/선택 hash 검사 뒤 기존 Docker만 읽기/트랜잭션 저장/독립 재조회. 신규5,537, 동일 입력 추가0. 비공개 검토보류/내용 충돌 거부/기존 LastSeen 갱신 의미 유지.
- `business-export`: 기존2,006행과 새5,537행의 주소 참고 연결·이름주소 중복 후보. 자동 병합/입주확정/Unity입력교체 없음. 출력이 이미 있으면 CreateNew가 거부한다.
- `business-self-test` 36검사, `business-source-check` 원본 선택 재현, [validate-business.ps1](validate-business.ps1)은 build+신규/기존 검사+독립 DB 재조회+파일 대조. 실행 인수는 `-RepositoryRoot C:/Users/user/source/repos/Hongdal`.
- 정확 입력·출력은 `artifacts/local/public-data/myeonmok-business-20260908-r1/`. 실제 신규source/관측과 기존음식점/의료 겹침을 구별한다. 이번 Unity/Scene/Assets 변경0.

2026-09-08 사용자 요청에 따른 일회성 소규모 검토 반입 도구. 기존 서버 원본 등록 서비스와 EF 공공자료 저장소를 재사용한다. 별도 수집 서버/DB/게시 원장은 만들지 않는다.

- 정확 입력: `artifacts/local/public-data/eight-life-domains/market-20260908-r1`의 동결 3파일과 수집 영수증.
- 공식 표준자료 15012894 전국 원본 1,393행에서 중랑구 12행만 선택한다. 공개 API 15052837의 응답으로 위장하지 않는다.
- `self-test`: DB·HTTP 없이 입력/파서 검사. `preview`: DB 읽기만. `apply`: 트랜잭션으로 기존 비공개 계보와 검토보류 12행 저장. `verify`: 독립 읽기.
- 실행: `dotnet run --project eng/Ssalddel.PublicDataPortalImport -- <mode> <repository-absolute-path>`.
- `acquire.ps1`은 현재 공식 링크의 제한 파일 3개만 받는다. 기존 폴더 재사용 거부, 파일별 5MiB 상한, 30초 제한. 새로운 판본은 코드의 동결 hash·기준일·개수 검토 없이 반입되지 않는다.
- DB: Compose `hongdal`의 `hongdal-mysql-1`·`hongdal_dev`·13306만, 환경 비밀값은 메모리에서만 소비. migrations/초기화/root계정/HTTP host/키발급 없음.
- 새 자료를 이 도구에 임의로 끼워 넣거나 원본·기존 동네 반입도구를 덮어쓰지 않는다. 결과는 [첫 수집 보고서](../../docs/Reports/생활영역-공공데이터-첫수집-2026-09-08.md) 참조.

## 먹거리 후속 (2026-09-08)

- `food-acquire`: 기존 서버의 KAMIS 비밀설정을 메모리에서 소비하고 공식 HTTPS에 소매/도매 각1회만 요청한다. 2026-09-07/부류100/kg환산Y 고정. 30초·2MiB 상한, redirect/자동retry/새키발급 없음. 입력 폴더에 파일이 하나라도 있으면 재수집 거부한다.
- 인증정보가 포함될 수 있는 top-level condition은 제거하고 data 원문을 보존한다. data 안 비밀값은 명시 거부. 전체 HTTP 원문으로 표현하지 않는다.
- `food-self-test`: HTTP/DB0,16검사. `food-preview`: DB 읽기. `food-apply`: 동결2추출본+영수증 hash 확인 후 감자4행만 트랜잭션 저장. `food-verify`: 독립 재조회. 동일 실행방식이며 원래 market mode는 유지.
- 기존 `kamis/price-observations` 계보, 사적 검토4행만 반입한다. unit와kg환산 표시가 함께 돌아오는 차이를 숨기지 않고 원필드21개/TextValue·NumericValue null·UnitReviewRequired로 남긴다.
- [먹거리 수집 보고서](../../docs/Reports/생활영역-먹거리가격-수집-2026-09-08.md). 현재 판매가·비교추세·Simulation/Public 게시용으로 승인하지 않았다.

## 감자 단위 대조 후속

- `food-unit-acquire`: 같은 날짜/부류를 도소매 N/Y4회로 제한, `food-unit-20260908-r1` 전용 폴더. 기존 수집모드/동결파일 불변. 이전Y2개와 새Y2개 동일hash를 대조한다.
- `food-unit-review`/`food-unit-self-test`: 파일만읽기/단위시험12. `food-unit-apply`/`food-unit-verify`: 별도 비교증거4행 저장/독립 재조회. 기존 가격 레코드 수정0·NumericValue null·검토보류 유지.
- [대조 결과](../../docs/Reports/생활영역-먹거리가격-단위대조-2026-09-08.md):28필드 중 정확환산7/반올림가설양립5/N-Y불변12/결측4. 부분필드 환산을 응답전체 단위로 일반화하거나 과거가격을 임의보정하지 않는다.

## 중랑구 공간 집중 수집 (2026-09-08)

- 사용자 후속 요청으로 `spatial-*`만 추가했다. 기존 시장/가격 모드와 동결 입력은 보존한다. 신규 원본은 `artifacts/local/public-data/jungnang-spatial-20260908-r2`이며 r1 메타데이터 읽기 실패도 보존한다.
- `spatial-inventory`: 기존 로컬 DB의 원본 자료별 수와 중랑구 정규화 재고를 읽는다. `spatial-acquire`: 공식 표준 공원15012890/주차장15012896/화장실15012892만 일회성 수집한다. 공식 DCAT 이용조건 확인 뒤 1만 행 페이지를 읽는다. 자료당4만행·응답32MiB·전체128MiB·요청45초 상한, 재시도/리디렉션 없음. 표준 다운로드가 지역 필터를 무시하는 것을 확인했으므로 전국 원문 파일에서 주소로 중랑구만 선택한다. 전국 DB 적재는 하지 않는다.
- `spatial-self-test`: 원본 hash/개수 확인 및 파서14검사. `spatial-preview`: DB 비교만. `spatial-apply`: 고정 영수증hash→원본hash→주소/날짜/기관/식별자/중복 검사 후 기존 등록 서비스와 EF 저장소로 검토보류134행 반입. advisory lock/트랜잭션, 기존 내용 불일치는 거부한다. `spatial-verify`: 연결을 다시 열어 재조회한다. 같은 원본 재입력은 추가0이며 관측 LastSeen만 갱신할 수 있다.
- `spatial-export`: DB에서 시설134+기존시장12를 읽어 원문hash/내용을 대조한 뒤 비공개 위치 조회JSON 생성. 잘못되거나 없는 좌표는 null이며 실제 높이·출입구·통행/영업 상태는 추정하지 않는다. 변경된 원천을 받아 기존 hash를 자동 바꾸지 않는다.
- Unity 연결 준비는 [prepare-jungnang-markers.ps1](../neighborhood/prepare-jungnang-markers.ps1)의 명시 입력/hash/출력 경로로 수행한다. 기존 WGS84 지역 접평면과 사가정 1km 정사각형만 재사용하며 출력은 `artifacts` 안이다. Unity Assets/Scene으로 자동 복사하지 않는다.
- [수집 결과·전체 지역의 남은 자료](../../docs/Reports/중랑구-공간자료-집중수집-2026-09-08.md). 이번134건을 중랑구 전체 공간 수집 또는 Unity 적용 완료로 표현하지 않는다.

## 면목동 주소 중심 후속

### 기존 지도 연결

`address-link-test` / `address-link-build`는 동결 DB 재조회 사본과 기존 사가정 r3 OSM 건물의 주소를 대조한다. DB·HTTP 쓰기 없이 비공개 `artifacts/local/public-data/myeonmok-spatial-link-20260908-r1/connection.json`을 CreateNew로 만든다. 이미 존재하면 덮어쓰지 않는다. 두 입력의 고정 hash/판본과 기존 주소 정규화 규칙을 재사용하고, 단일 후보213자료/121건물·복수22·일치없음1,726·주소없음45를 분리한다. 미연결을 가까운 건물에 임의 배치하지 않는다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- address-link-test C:/Users/user/source/repos/Hongdal
# 검사 재실행은 가능하다. address-link-build는 이미 생성된 r1 출력을 LinkOutputExists로 거부한다.
```

Unity의 기존 사가정 조립 경로가 이 파일을 Editor에서만 읽어 주소·시설 목록을 보여주는 코드와 연결됐다. 비공개 사본은 Resources/배포에 복사하지 않았다. 건물 후보는 현재 입주·영업·출입구나 새 건축물 원장이 아니다. [정확 연결 범위](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/address-connection.r1.md).

### 원자료 수집·저장

- `myeonmok-inventory`: 기존 음식점/건물 원장을 읽는다. `myeonmok-acquire`: 공동주택15006098·의원OA-16220·병원OA-16165에 한정한다. r1 주택 HTTP500과 정상 병의원 원문을 보존했고, r2에서는 병의원 원본과 수집시각을 재사용하고 주택 다운로드만 명시 보완했다. r2가 있으면 실행 거부하며 자동 retry 없음. 공식 공개 Sheet 조회100행/페이지·최대20페이지/자료, 단일8MiB·합32MiB·30초/요청, redirect 없음.
- `myeonmok-self-test`: CSV/지역선택·폐업/결측·DB시간 정밀도20검사. `myeonmok-preview`/`apply`/`verify`: 동결 receipt와 원본 hash 확인, 현재 로컬 DB만 읽기/트랜잭션 반입/독립 재조회. `diagnose`는 읽기 차이 분석이다. 기존 저장소의 비교 규칙은 변경하지 않는다.
- `myeonmok-export`: DB의 신규362행·기존음식점1594·시설50를 읽어 주소별 참조를 생성한다. 기존 `공개사업장주소정규화Engine`을 재사용하며 건물 Assignment를 쓰지 않는다. 동일 주소 묶음은 실제 입주·단일 건물 확정이 아니다. 원문 상세주소는 보존하며 이름만으로 병합하지 않는다.
- 신규 병의원 수집시각은 MySQL `datetime(6)`의 실제 절삭 관찰에 맞춰 마이크로초로 저장 비교한다. 원본 receipt의 100ns 시각과 최초 RecordKey 계산 입력은 그대로 보존해 기존 식별자/내용을 바꾸지 않는다. 첫 재입력의 `UnexpectedUpdate`는 롤백됐고 보완 뒤 추가0/기존362 확인.
- 정확 원문: `artifacts/local/public-data/myeonmok-address-20260908-r2/receipt.json`, SHA `C70B5FDEDAD5B8CB7D1D1E197DF4FCB5B0D9AA8350ABB4811159F2BE98ACC593`. 모든 원본은 private artifacts, 조회는 검토보류이며 기존 DB나 Unity 전역 설정 변경 없음.
- [면목동 결과와 남은 주소·건물 연결](../../docs/Reports/면목동-주소기반-공간자료-집중수집-2026-09-08.md).

### 주소 중심 디오라마 투영 r1

- `diorama-self-test`는 DB·HTTP·Unity 없이 주소 중심 공간 색인과 배포 비승인 표현 투영의 계약을 검사한다.
- `diorama-build`는 기존 동결 자료만 읽고 `artifacts/local/public-data/myeonmok-diorama-view-r1/`에 `private-spatial-index.json`, `presentation-projection.json`, `manifest.json`을 CreateNew로 만든다. 같은 입력 재실행은 기존 출력 때문에 거부되어 중복 판본을 만들지 않는다.
- 비공개 색인은 주소·관측 이름·원천 계보를 유지하지만 Unity 표현 투영은 이름·상세 주소·건물관리번호·원천 관측 ID를 제거하고 업종 범주·연결 상태·운영 근거 상태만 전달한다. `distributionApproved=false`, `gameStateConnected=false`, `privateReviewOnly=true`가 모두 고정 관문이다.
- Graph Map/Placement Map의 `jungnang-myeonmok-reference.v2`가 이 투영을 `PrivacyProjectionOf` 관계로 연결한다. 주소/건물/필지/도로/활동은 별도 계층이며, 공식 건물 연결 0건과 표본의 단일14·복수8·미연결8을 그대로 보존한다.
- `diorama-verify`는 동결 입력과 생성 파일의 hash·형상·개인정보 제거를 재검증한다. Unity 쪽 기본 화면은 범주와 근거만 보여주며, 이름·주소는 로컬 상세 검토 토글을 명시적으로 켠 경우에만 기존 비공개 자료에서 읽는다.

### 행정동 실자료 완결 관문 r1

- `admin-dong-diorama-preview|apply|verify`는 서울시 `OA-22160` 경계 ZIP과 사가정 지도 고정 hash, MySQL의 행정안전부 코드·면목동 상권 관측을 함께 대조한다. 면목동 6개 행정동으로 건물 후보 602개를 `PointOnSurface` 전수 귀속하고 면목제3·8동 도로를 경계에서 자른다.
- `admin-dong-data-acquire|preview|apply|verify|self-test`는 서울시 `OA-14991` 2026년 7월 행정동 생활인구 완결본에서 면목동 6개 행정동의 4,464개 시간 관측을 추출한다. 원본은 비공개 보관하고 MySQL에는 `PendingHumanReview` 성격의 통계 추정치를 저장한다. 이 자료는 서울시가 밝힌 2016 행정동 구역 기준 사전 집계이며 현행 경계 재투영값이나 게임 상태가 아니다.
- `apply`는 면목제3·8동 manifest/tile/빈 overlay와 6개 동 귀속 감사자료를 로컬 `ssalddel_dev`에 hash별 불변 사본으로 저장한다. 같은 입력을 즉시 재적용해 멱등성을 확인하고, 새 Mongo 연결로 manifest·모든 tile·감사자료·현재 포인터를 재조회한다. `verify`는 쓰지 않고 같은 재조회를 반복한다.
- OSM 건물은 공식 건축물대장으로 승격하지 않으며 MySQL에 쓰지 않는다. 좌표가 있는 상권 관측도 행정동 분석까지만 수행하고 Claim·표시 승인을 만들지 않으므로 공개 표식은 0건이다. 기능 생활권·운영 HTTP·Unity Scene·Game View를 자동 실행하지 않는다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-diorama-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-diorama-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-diorama-verify C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-data-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-data-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-data-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-data-verify C:/Users/user/source/repos/Hongdal
```

### 동북서울 30개 행정동 디오라마 역사 경계 후보 r4

- 대상은 `northeast-seoul-rider.r2`에 고정한 광진구 4개·동대문구 10개·중랑구 16개 행정동이다. 생성기는 서울시 `OA-22160` 역사 경계, 서울 `AL_D010` 건물, 국가표준 NodeLink를 각각 한 번 읽어 30개 모듈로 나눈다.
- 건물은 원본 도형의 명시적 `assignmentPoint`로 한 행정동에만 귀속하고 방법·신뢰 수준·경계 판본을 함께 기록한다. 최종 61,897개가 귀속됐고 미해결 27개는 경계 밖 13개·원본 무효 11개·1mm 반올림 후 무효 3개다. 복수 귀속은 0개이며 임의 형상 보정은 하지 않는다.
- 방향 NodeLink 4,209개를 행정동 경계에서 잘라 100m 이하 두 점 선분 11,769개로 만든다. 게시 경계 밖 1µm 초과는 0개지만 도로 폭·차로·보도·신호·통행 권위는 아니다.
- C# 단계는 로컬 MySQL의 행정안전부 동결 원장을 독립 재조회하고 원본·도구·모듈 hash와 `OA-22160` 독립 경계 변환을 다시 확인한 뒤 기존 투영 Builder로 500m tile 297개를 만든다. MongoDB 쓰기는 `administrative_dong_diorama_candidate_*` 네 collection에만 하며 `administrative_dong_diorama_current`를 갱신하지 않는다.
- 실제 저장 결과는 batch 1·manifest 30·tile 297·overlay 30, 합계 358개다. 반복 적용은 신규 0·기존 일치 358이고 독립 재조회가 통과했다. 과거 r1 후보는 `PreservedNotPromoted`로 남긴다.
- 실제 내려받은 `OA-22160` ZIP 내부 자료가 2023년이므로 결과는 `WaitingForAdministrativeBoundary`, 비공개·비통행·비게임 후보다. 최신 JUSO `TL_SCCO_GEMD`를 승인 반입하기 전에는 current API나 Unity에 게시하지 않는다.

```powershell
C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -B eng/neighborhood/administrative_dong_diorama_batch.py build --root .
C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -B eng/neighborhood/administrative_dong_diorama_batch.py verify --root .
C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -B eng/neighborhood/administrative_dong_diorama_batch.py self-test --root .
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-diorama-batch-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-diorama-batch-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-diorama-batch-verify C:/Users/user/source/repos/Hongdal
```

### 30개 Unity 검토 내보내기·주소 후보 r5-r6

- `admin-dong-diorama-batch-export-unity-review`는 역사 경계 후보 30개를 `current`나 DB에 쓰지 않고 hash별 완성 세대에 내보낸다. 각 세대는 index·bundle 30개·`complete.json`을 같은 staging에서 전부 검증한 뒤 원자 이동하며, exporter 소스 hash와 판본을 결속한다.
- `admin-dong-building-address-candidate-*`는 같은 61,897개 건물의 `AL_D010` PNU와 동결 2026-08 JUSO 건물DB를 결합해 `ParcelAddressCandidate`, `MultipleParcelAddressCandidates`, `NoCandidateInFrozenJusoVintage`만 기록한다. 주소 문자열·출입구·필지 도형·배달 목적지·Unity 권위는 만들지 않는다.
- `apply`는 로컬 MySQL의 범용 공공자료 원장에 `PendingHumanReview`로 멱등 저장하고, `verify`는 새 연결에서 exact set·상태 분포·투영 hash를 재조회한다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-diorama-batch-export-unity-review C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-building-address-candidate-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-building-address-candidate-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-building-address-candidate-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-building-address-candidate-verify C:/Users/user/source/repos/Hongdal
```

### 30개 행정동 횡단보도 점 후보 G3a r2

- 서울 열린데이터광장 `OA-23081` 동결 XLSX 21,776행을 r2 역사 행정동 경계에 점 포함 방식으로 결속한 비공개 후보 원장이다. 좌표가 있는 21,775행 중 30개 동 안의 단일 귀속 1,533행만 보존하고, 경계 밖 20,242행과 좌표 결손 1행은 집계·감사 자료에만 남긴다. 복수 경계 귀속은 0행이며 보행자 신호등 표기는 `유` 933행·`무` 600행이다.
- 원본 자치구와 역사 경계 귀속 자치구가 다른 21행은 삭제하거나 다른 동에 복제하지 않는다. 역사 경계 소유를 유지하면서 `SourceDistrictSpatialAssignmentConflict`, 원본·귀속 자치구, 경계 거리와 동별·자치구쌍 집계를 명시해 일반 후보와 구별한다.
- Python 생성기는 후보·manifest·audit·`complete.json`을 hash 세대 폴더에 원자 게시한다. C# CLI는 완성 표식부터 읽고 정확한 최종 세대와 파일 집합, r8 설계·작업 명세·실제 scope·생성기·모든 원천, 좌표·대안 CRS·역사 경계 변환·feasibility hash, 파일별 SHA-256/길이·내용 hash와 모든 권위 차단 플래그를 다시 검증한다. 후보 집합 hash도 Python과 독립된 길이 접두 SHA-256 구현으로 재계산한다.
- `apply`는 기존 범용 공공자료 원장에서 r2 DatasetId별 snapshot의 hash·길이·형식·원본명·판본·비공개 저장 위치를 쓰기 전에 검사한 뒤 advisory lock과 단일 transaction에서 원천·설계·생성 산출물 snapshot 13건 및 후보 1,533행을 `PendingHumanReview`로 저장한다. 동일 입력 재실행은 쓰기 없이 기존 1,533행을 확인하고, `verify`는 새 DB 문맥에서 정확한 집합·30개 동별 분포·충돌 21행·후보 집합 hash를 독립 재조회한다.
- 독립 감사 전에 저장된 `g3a.r1` 후보 1,533행과 snapshot 11건은 `RejectedAfterIndependentAudit / PreservedNotPromoted` 이력으로 삭제·갱신하지 않는다. r2는 별도 DatasetId와 revision으로 병존하며 CLI가 실행 전후 r1 정규화 행·snapshot의 모든 지속 필드를 길이 접두 SHA-256으로 비교한다.
- 좌표계는 원천 선언이 아니라 별도 실증 검증 후보이고 행정동 경계는 역사 bootstrap이다. 이 자료는 횡단보도 점 관측 후보일 뿐 현재 행정동 경계, 횡단보도 면 형상, 정지선, 신호 주기·현시, 보도 연결, 공개 표시, Runtime·통행·게임·Unity 적용 권위가 아니다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-crosswalk-candidate-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-crosswalk-candidate-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-crosswalk-candidate-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-crosswalk-candidate-verify C:/Users/user/source/repos/Hongdal
```

### 30개 행정동 교차로 점 후보 G3b r1

- 서울 열린데이터광장 `OA-15534` 2025-08-14 동결 ZIP의 원천 선언 `EPSG:5186` 점 8,097개를 2023 역사 행정동 경계와 대조한 DB83 완결 세대만 읽는다. 정확히 하나의 대상 동에 귀속된 554개를 비공개 후보 원장에 보존하고, 범위 밖 7,543개·복수 귀속 0개는 집계로만 검증한다. 원본 자치구 일치는 553개, 역사 경계 소유와의 충돌 진단은 1개다.
- G3a r2 횡단보도 후보와의 연결은 교차로 번호 진단일 뿐 귀속을 복사하지 않는다. 교차로 524개에 횡단보도 관계 1,491개가 연결되며, 같은 역사 행정동 1,271개와 다른 동 220개를 따로 재계산한다. 모든 후보는 `crosswalkAssignmentInherited=false`다.
- `apply`는 원천·scope·r10 설계·생성기·G3a 연결 계보와 생성 세대의 `generator-source.py` bytes를 포함한 snapshot 19건을 별도 DatasetId로 등록한다. 원천 관리번호는 표준 출력·`StableId`·`DimensionKey`에 넣지 않고 `candidateStableId` SHA-256 식별자와 후보 본문·연결 집합 hash만 정규화 payload에 둔다.
- 기존 본문의 불일치를 update로 숨기지 않고 쓰기 전에 전체를 거절한다. 고정 순서 named lock, 단일 transaction, 500행 이하 묶음, 새 DB 연결 exact-set 재조회를 사용하며 반복 `apply`는 신규·갱신·snapshot 신규가 모두 0이어야 한다. 실행 전후 G3a r2 1,533행/13 snapshot, G4a r1 29,721행/19 snapshot, 기존 면목 사업장 5,411행·공장 126행의 전체 지속 상태 hash를 비교한다.
- 이 자료는 교차로 **점** 후보일 뿐 현행 교차로 위상, 진입 방향, 제어기, 차로, 신호를 제공하지 않는다. `OA-15537`은 차선 **표시 선형**이지 주행 가능 차로가 아니며 소비하지 않았고, `OA-21208` 2020 보행 네트워크도 현행 자료로 간주하지 않고 제외했다. Mongo/current/API/Unity/통행/gameplay 권위는 없다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-intersection-candidate-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-intersection-candidate-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-intersection-candidate-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-intersection-candidate-verify C:/Users/user/source/repos/Hongdal
```

### 30개 행정동 도보 네트워크 후보 G3c r1

- 서울 열린데이터광장 `OA-21208`의 2020년 기준 WGS84 도보망을 공식 Sheet 자치구 필터로 동결한 광진구·동대문구·중랑구 CSV 59,724행과 링크·노드 유형 코드북만 소비한다. 모든 행의 자치구 코드·명, exact raw file set과 코드북 LINK 16개·NODE 4개 의미를 DB 연결 전에 검사한다.
- 2023 역사 행정동 경계에 NODE를 귀속하고 LINK를 절단한 완결 세대는 NODE 17,267·LINK fragment 23,472, 합계 40,739개다. 후보 집합 hash는 `43248F7F563DCDFE059650F3EC000085CD0D7CFB65FC5300EFDFCBDD685990AA`다.
- 원본에는 `RONUM`이 없으므로 동결 CSV 역할과 1부터 시작하는 data-row 번호를 source occurrence로 사용한다. 1mm 직렬화 경계 재포함, 경계 일치 물리 조각의 양쪽 귀속, 원선 대상 교차 길이 보존과 중복 진단을 audit에서 확인한다.
- RDB payload는 후보 digest·행정동·종류·길이·품질·권위 요약만 2,000자 이하로 저장하고 98,781,674-byte 전체 geometry는 비공개 `candidates.ndjson` snapshot에만 둔다. C#은 Python helper를 호출하지 않고 명시된 4-byte big-endian 길이 framing으로 전체 후보 hash를 독립 재계산해야 한다.
- 적용은 정확 17개 계보 snapshot, 500행 이하 묶음, 기존 본문 update 금지, 새 연결 exact-set 재조회와 반복 무쓰기를 요구한다. G3a r2 1,533/13, G3b r1 554/19, G4a r2 29,721/19, 사가정 r27 `OA-21208` 2,902/1의 전체 지속 상태 digest가 전후 같아야 한다.
- Python 자체 시험 17/17과 local-private generation·verify, C# build 경고 0·오류 0과 자체 시험 16/16을 통과했다. 로컬 MySQL 첫 적용은 후보 40,739행·계보 사본 17건이고, 새 연결 exact set·후보 hash 재조회와 반복 적용 신규·갱신·사본 0을 확인했다. 기존 보호 상태 71,501행·사본 84건·실행 84건의 digest는 전후 같았다. Mongo/current/API/Unity/현재 통행·오토바이·차로·신호·traversal/gameplay 권위는 없다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-walk-network-candidate-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-walk-network-candidate-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-walk-network-candidate-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-walk-network-candidate-verify C:/Users/user/source/repos/Hongdal
```

### 30개 행정동 사업장 후보 G4a 정정 r2

- 소상공인시장진흥공단 2026-06-30 동결 상가 자료에서 정확한 30개 행정동 코드에 속한 29,721행을 별도 비공개 후보 원장으로 보존한다. 음식 대분류 `I2`는 8,246행이고 건물관리번호 결손 67행, 원본 행정동과 2023 역사 경계 진단 불일치 35행, 동일 원문 상호·도로명주소 후보는 236그룹·490행이다. 불일치 행을 역사 경계로 재귀속하거나 동일업체로 자동 병합하지 않는다.
- 첫 `g4a.r1`은 독립 개인정보 감사에서 선택된 원천 식별자 하나가 추적 생성기의 시험 fixture에 들어간 사실을 확인해 `RejectedAfterIndependentPrivacyAudit / PreservedNotPromoted`로 보존한다. 해당 값은 출력하지 않고 r1 후보 29,721행·snapshot 19건·ingestion run 19건의 전체 지속 상태 digest를 r2 실행 전후 비교한다. 현재 명령은 새 DatasetId와 revision의 정정 r2만 대상으로 한다.
- raw 공급자 ID는 trim 후 SHA-256 digest를 사용해 `StableId`·`DimensionKey`·`RecordKey`를 만든다. 정확 상호명·지점명·도로명/지번주소·공급자 ID·건물관리번호/건물명/동/층/호·원 경위도 문자열 14개는 candidate hash의 실제 `hashValues`를 compact JSON으로 저장해 보호 RDB에서 정확 재조회하며, digest로 대체하지 않는다. 이 값은 Git에서 제외된 `candidates.ndjson`과 보호 로컬 RDB payload에만 남기고 manifest·audit·CLI 출력에는 포함하지 않는다.
- 생성기 self-test fixture는 실제 원천 집합에 없는 명시적 합성 값을 사용한다. 선택된 29,721개 원천 식별자가 추적 생성기나 manifest·audit·complete 같은 집계 산출물에 하나라도 나타나면 DB 연결 전에 거절한다.
- `preview`와 `apply`의 쓰기 전 검사는 DB 발급 `RawSnapshotId`를 제외한 기존 행 본문을 모두 비교한다. `apply`는 advisory lock과 단일 transaction에서 500행 이하 묶음으로만 저장하고 기존 행을 갱신하지 않는다. `verify`는 새 연결에서 정확한 29,721개 key, 30개 동 분포, 음식 8,246, 진단 35, 결손 67과 후보 집합 hash를 독립 재조회한다.
- ZIP 자체와 검증한 서울 CSV entry를 구분해 원본 계보를 남긴다. C#은 entry의 301,889,640 bytes·CRC32·SHA-256과 CP949 raw 이름을 ZIP에서 다시 확인하고, 추출본을 만들지 않은 archive-entry snapshot을 포함한 계보 snapshot 19건을 보호 RDB에 둔다.
- 기존 면목동 SEMAS 5,411행과 공장 126행은 별도 원장으로 보존한다. 실행 전후 전체 지속 상태 digest와 새 원장의 provider ID overlap 5,411·신규 ID 24,310을 확인하지만 두 원장을 참조·병합·덮어쓰지 않는다.
- 모든 행은 `PendingHumanReview / PrivateReviewOnly`다. 현행 행정동 경계, 좌표 기준, 정확 건물·출입구 결속, 현재 영업, Claim, 공개 표시, 주문 가능, 광고, Runtime·Simulation·게임·Unity 권위는 만들지 않는다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-business-candidate-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-business-candidate-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-business-candidate-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- admin-dong-business-candidate-verify C:/Users/user/source/repos/Hongdal
```

### 중랑구 7호선 역 기준자료 r1

- `station-reference-acquire`는 공공데이터포털의 국가철도공단 도시광역철도 역사정보 metadata와 연결 XLSX를 제한 크기·시간 안에서 로컬 비공개 폴더에 수집하고 원본 SHA-256을 기록한다.
- `station-reference-self-test`는 전국 1,099행의 고정 schema에서 면목·사가정·용마산(용마폭포공원) 3행만 선택하고, `노선번호 + 선행 0을 보존한 역번호` 기반 `station:kr:kric:*` 식별자와 전화번호 미투영을 검사한다.
- `station-reference-preview|apply|verify`는 기존 MySQL 공공자료 수집 원장을 재사용한다. 첫 적용은 원본 사본 1건과 검토보류 3행만 저장하고, 재적용은 신규 0·기존 3이어야 하며 `verify`는 별도 연결에서 같은 3행을 재조회한다.
- 포털 metadata에 표시된 이용허락 범위와 현재 연결 파일 판본의 정확한 결속은 사람 검토 대상으로 유지한다. 이 자료는 출처가 보고한 WGS84 역 기준점일 뿐 출구·역세권 면적·건물·도로 coverage, 공개 배포, 운영 권위 또는 Unity 적용을 승인하지 않는다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- station-reference-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- station-reference-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- station-reference-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- station-reference-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- station-reference-verify C:/Users/user/source/repos/Hongdal
```

### 사가정 공공 사진 비공개 검토 r2

- `sagajeong-photo-acquire`는 Wikimedia Commons의 사가정역 분류 metadata와 정확 사가정시장 검색 결과를 제한 조회하고, 고정 후보 5건만 로컬 비공개 폴더에 수집한다. 응답은 30초, metadata 2MiB, 사진당 8MiB로 제한하며 파일 host는 `upload.wikimedia.org`만 허용한다. 출력 폴더가 있으면 재수집하지 않는다.
- 공공영역 역 내부·표지 3건과 `CC BY-SA 4.0` 출입구 2건을 서로 구분한다. 모든 사진은 내용 검토 전 `modelDerivationAllowed=false`, `gameDistributionAllowed=false`다. 공공누리 제4유형 중랑구 후보 3건은 metadata만 기록하고 사진 파일은 받지 않는다.
- `sagajeong-photo-self-test`는 원본 hash, 파일 크기, 라이선스, 시장 검색 0건과 Blender·Unity 차단을 확인한다. `preview|apply|verify`는 기존 로컬 Docker MySQL 공공자료 원장을 재사용해 사진 후보 5·시장 결손 1·권리 제외 3건을 저장·재조회한다.
- 실제 결과는 [수집 보고](../../docs/Reports/사가정-공공사진-첫수집-2026-09-13.md)와 [기계 대장](../world-seedbeds/station-landmarks/sagajeong-public-photo.collection.r2.json)을 따른다. 비공개 사진 원본을 Git·Unity 자원·빌드로 복사하지 않는다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-photo-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-photo-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-photo-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-photo-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-photo-verify C:/Users/user/source/repos/Hongdal
```

### 사가정 공공데이터포털 사진 자료 조사 r1

- `sagajeong-data-go-photo-acquire`는 한국관광공사 관광사진·국문 관광정보·여행기사 자료와 관광사진 API 안내서를 제한 수집한다. 공급자 미리보기 사진은 로컬 비공개 조사 자료로만 보존한다.
- 관광사진 API는 기존 사용자 비밀 키로 접근 여부만 검사한다. 키와 오류 원문은 저장하지 않으며 HTTP 403을 사진 검색 성공이나 권리 확인으로 대체하지 않는다.
- `self-test`는 영수증·원문·사진 hash와 Blender·Unity 비승인을 확인한다. `preview|apply|verify`는 로컬 MySQL 공공자료 원장에 사진 3·자료군 1·API 접근 1·검색 결손 1을 저장·재조회한다.
- 실제 결과는 [조사 보고](../../docs/Reports/사가정-공공데이터포털-사진자료조사-2026-09-13.md)와 [기계 대장](../world-seedbeds/station-landmarks/sagajeong-data-go-kr-photo-research.collection.r1.json)을 따른다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-data-go-photo-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-data-go-photo-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-data-go-photo-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-data-go-photo-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-data-go-photo-verify C:/Users/user/source/repos/Hongdal
```

### 사가정 1km 음식점 비공개 검토 디렉터리 r1

- 기존 소상공인시장진흥공단 면목동 상가 원장 5,411행과 동결된 주소·건물·인허가 검토 자료를 같은 Unity XZ 1km 창으로 다시 투영한다. 음식 업종 599행과 원문 상호 587종을 `public_data_normalized_records`에 파생 자료로 저장하며 부모 `SourceId`·`DatasetId`·`RawSnapshotId`를 그대로 보존한다.
- 각 행은 원문 상호·업종·공개 도로명주소·좌표, 부모 행 식별자와 hash, 단일 건물 후보 또는 미해소 사유, 인허가 후보 ID를 보존한다. 전체가 `PendingHumanReview`, `distributionApproved=false`, `orderScenarioEligible=false`이므로 공개 상호 표시·업체 참여·주문·광고 권한이 아니다.
- 현재 로컬 DB에 인허가 전용 테이블이 없으면 동결 입력의 인허가 후보와 hash만 보존하고 `SkippedTableUnavailable`을 보고한다. migration을 자동 실행하거나 테이블을 추정 생성하지 않는다.
- `prepare`는 결정적 manifest를 로컬 산출물 폴더에 한 번만 만들고, `apply`는 누락된 파생 행만 transaction으로 저장한다. 같은 입력 재실행은 599행을 재조회하되 DB 쓰기를 하지 않아야 한다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-restaurant-directory-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-restaurant-directory-prepare C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-restaurant-directory-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-restaurant-directory-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-restaurant-directory-verify C:/Users/user/source/repos/Hongdal
```

### 사가정 1km 건물 도로명주소 결속 r1

- `sagajeong-building-address-acquire`는 행정안전부 주소기반산업지원서비스에서 현재 내려받을 수 있는 2026-08-31 기준 건물DB 월 전체 ZIP과 서울 MS949 31열 자료, 활용 가이드를 `artifacts/local` 비공개 원본으로 동결한다. 공공데이터포털 metadata의 행정안전부 공급자·`이용허락범위 제한 없음`을 같이 검사한다.
- `prepare|preview|replay`는 동결 지도 602개를 `OfficialIndividualAddress / OfficialSharedComplexAddress / RoadAddressCandidate / NoIndependentRoadAddress / Unresolved`로 전수 분류한다. 건물 주소 태그, 윤곽 안의 주소 지점, 유일한 공식 건물명 순으로만 대조하고 가까운 주소를 복사하지 않는다.
- `apply|verify`는 602건의 결속 상태를 기존 `public_data_normalized_records`에 저장하고 새 `DbContext`로 재조회한다. 공유 주소는 건물 ID를 합치지 않고 같은 주소 및 복수 건물관리번호 후보를 참조한다. 모든 행은 `distributionApproved=false`, `deliveryEligible=false`, `priceObservationEligible=false`, `unityApplyAllowed=false`다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-building-address-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-building-address-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-building-address-prepare C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-building-address-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-building-address-replay C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-building-address-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-building-address-verify C:/Users/user/source/repos/Hongdal
```

### 사가정 화면 건물 결속·주소 증거 원장 r1

- `sagajeong-presentation-building-evidence-*`는 이미 생성한 화면 건물 4,062개 결속 원장과 주소 후보 원장을 다시 생성하거나 형상을 바꾸지 않고, 기존 `public_data_normalized_records`에 비공개 검토 자료로 보존한다. 입력 파일·내용·원천 영수증 hash와 결속·주소 상태 계수를 모두 고정 검증한다.
- 결속 원장은 manifest 1·출처 영수증 2·화면 건물 4,062·기준 건물 602·일대일 결속 544, 합계 5,211행이다. 주소 원장은 manifest 1·출처 영수증 5·건물별 배정 4,062·중복 제거한 도로명주소 후보 3,514, 합계 7,582행이다. 건물별 배정에는 후보 ID와 그 건물에서의 근거 방법만 두고, 주소·공식 합성키·건물관리번호는 후보 카탈로그 행에 한 번만 저장해 모든 `TextValue`를 2,000자 이하로 유지한다.
- `apply`는 advisory lock과 단일 transaction에서 두 생성 원본 snapshot과 누락 정규화 행만 저장한다. 쓰기 전 충돌 검사, 새 연결의 독립 재조회, 동일 입력 재실행의 쓰기 0건을 강제한다. 모든 행은 `PendingHumanReview`이며 필지 형상·공식 건물 승격·공개 API·Unity·배달·가격·사업장·통행·게임 권위가 없다.
- `self-test`는 DB 없이 전수 원장·행 길이·분리 계수·권위 차단을 검사한다. `preview`는 DB와 충돌 여부만 비교하고, `verify`와 `replay`는 저장된 12,793행 및 두 원본 계보를 새 읽기 문맥에서 확인하며 쓰지 않는다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-presentation-building-evidence-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-presentation-building-evidence-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-presentation-building-evidence-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-presentation-building-evidence-verify C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-presentation-building-evidence-replay C:/Users/user/source/repos/Hongdal
```

### 사가정 정적 차선·신호 관측 r1

- `sagajeong-traffic-acquire`는 서울 열린데이터광장의 차선 `OA-15537`, 방향표시 `OA-15536`, 교차로 `OA-15534`, 교통신호제어기 `OA-15538`, 신호등 부착대 `OA-15546` 공식 전체 파일을 내려받는다. 파일명·코드에 동결한 기준일·SHA-256·길이·포털 좌표계 선언·공공누리 제1유형 확인을 하나의 비공개 영수증에 보존하며, 원본은 `artifacts/local` 밖으로 복사하지 않는다. 기준일은 원천 관측일이 아니며 포털 HTML 자체는 원본 snapshot으로 저장하지 않는다.
- 기존 명목상 사가정 1km 표현 창은 EPSG:5186 envelope로 약 `1,034.423m × 1,022.067m`다. 차선은 실제 선분 절단이 아닌 bbox 교차 후보이고, 1,039행은 관리번호 1,036개와 이력 중복 3개를 포함한다. 다른 점 자료는 좌표 포함 후보지만, 신호등 125행은 포털 EPSG:5186 표기와 내장 geometry SRID 2093이 충돌하므로 실제 사가정 지역 ID가 아닌 `area:kr:seoul:traffic-signal-crs-unresolved-review` 검토 버킷에 `crs-conflict-numeric-envelope-candidate`로 저장한다.
- `self-test|preview|apply|verify`는 전체 556,346행 중 CRS가 해소된 거친 후보 1,398행과 CRS 미해결 숫자 envelope 후보 125행, 합계 1,523행을 검사·비교·저장·독립 재조회한다. 모든 행은 `PendingHumanReview`이고 공개·Runtime·통행·게임 권위가 없다. `apply` 재실행은 신규 원본·행 0건이어야 하며 같은 원본의 새 수집 시각은 중복이나 충돌을 만들지 않는다.
- 횡단보도 `OA-23081`은 r1에 포함하지 않았다. 별도 XLSX schema와 더 최신 판본을 교차로·정지선에 결속하려면 r2 영수증으로 다시 열어야 하며, 이것이 정지선·보행 신호 관계의 가장 이른 후속 지점이다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-traffic-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-traffic-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-traffic-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-traffic-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-traffic-verify C:/Users/user/source/repos/Hongdal
```

### 사가정 횡단보도·보행등 관계 관측 r2

- `sagajeong-crosswalk-acquire`는 서울 열린데이터광장 `OA-23081` 전체 XLSX의 원본 hash·길이·판본·EPSG:5186·공공누리 제1유형 조건을 확인하고 비공개 동결 영수증을 만든다. 원본 XLSX는 `artifacts/local/public-data/sagajeong-static-traffic-20260913-r2-crosswalk/`에 미리 받아 두어야 하며, 수집기는 원본을 수정·복사하지 않는다.
- `self-test|preview|apply|verify`는 21,776행을 읽고 좌표가 모두 빈 1행은 추정 보정 없이 제외한 뒤, 기존 EPSG:5186 사가정 명목상 1km 선택 창 내 84행만 정규화한다. 이 범위에서 보행등 `유` 40건·`무` 44건, 교차로 27개고, 사가정역 교차로 `3205`는 7건(`유` 4·`무` 3)이다.
- 보행등 유무는 공식 관측 사실이지만 신호 주기·현시·정지선·통행 규칙이 아니다. 전체 행은 `PendingHumanReview`이고 공개·Runtime·통행·게임 권위가 없다. `apply` 재실행은 원본·정규화 신규 0건으로 멱등해야 한다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-crosswalk-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-crosswalk-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-crosswalk-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-crosswalk-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-crosswalk-verify C:/Users/user/source/repos/Hongdal
```

### 중랑구 전통시장 시각·정체성 비공개 검토 r1

- `jungnang-market-visual-acquire`는 중랑구소식지 2016년 9월호 PDF를 공식 URL과 고정 hash로 확인하고, 우림·동부·면목·동원·사가정의 설명과 사진이 함께 남는 시장별 검토 이미지 5장을 `artifacts/local`에 만든다.
- 2025-11-10 전국전통시장표준데이터의 현재 후보 7행을 함께 보존한다. `면목시장/면목골목시장`, `동원시장/동원전통종합시장`, 과거 동부시장 주소 차이는 자동 병합하지 않는다.
- 소식지의 개별 사진 이용권을 확인하지 못했으므로 전부 `PrivateReviewOnly`다. 사진·crop을 Git 또는 Unity에 넣거나 Blender 형상·텍스처 근거로 사용하지 않는다.
- `apply|verify`는 시각 참고 5건, 정체성 검토 5건, 권리 경계 1건을 로컬 Docker MySQL에 저장하고 독립 재조회한다. 같은 입력 재적용은 신규 0건이어야 한다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- jungnang-market-visual-verify C:/Users/user/source/repos/Hongdal
```

### 사가정역 1km 공간 보충 자료 r1

- 서울 열린데이터광장 공식 자료에서 보행망 `OA-21208`, 역 엘리베이터 `OA-21212`, 생활권계획 공원 `OA-15529`, 버스 정류소 `OA-15067`, 가로수 `OA-1325`를 수집한다. 원본과 영수증은 `artifacts/local/public-data/sagajeong-spatial-supplement-20260914-r1/`에만 둔다.
- 기존 사가정 WGS84 창을 사용해 보행망 2,902·엘리베이터 1·공원 정체성 1·버스 정류소 30·가로수 292건을 `PendingHumanReview`로 정규화한다. WGS84 자료의 ENU 중심 좌표는 읽기 후보이며 통행·Runtime·gameplay 권위가 아니다.
- 공원 SHP의 EPSG:5174 경계 변환은 보류다. 국토지리정보원 수치지도 V2·수치표고모형은 로그인·전용 전송 절차가 필요한 `BlockedExternalAccess`이고 fallback하지 않는다.
- `apply`는 200행 단위로 저장하고 같은 입력의 재적용은 신규 0·기존 3,226이어야 한다. `verify`는 새 DB 문맥으로 정규화 행과 원본 사본을 독립 재조회한다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-spatial-supplement-acquire C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-spatial-supplement-self-test C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-spatial-supplement-preview C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-spatial-supplement-apply C:/Users/user/source/repos/Hongdal
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- sagajeong-spatial-supplement-verify C:/Users/user/source/repos/Hongdal
```
