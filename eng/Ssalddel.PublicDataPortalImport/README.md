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
