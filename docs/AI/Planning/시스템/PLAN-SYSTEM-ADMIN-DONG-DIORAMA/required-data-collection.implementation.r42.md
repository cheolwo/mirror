# 집중 구역 건물·보도·DEM 자료 후속 수집

[기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r42]

- 사용자 방향: 2026-10-02 자료 수집 우선 요청에 이어 “계속해 봐.”
- 상태: `SixAdditionalOfficialPayloadsAcquired / LocalMySqlSourceLineageStoredAndRequeried / PendingHumanReview`.
- 선행: [첫 실제 수집·MySQL 축적 r41](required-data-collection.implementation.r41.md), [소수 구역 집중 r39](focused-area-refinement.decision.r39.md).
- 이번 범위: 공식 원본 취득·동일성/시점/권리 대조·기존 수집 계보 축적. 기존 형상과 Unity Scene은 보존한다.

## 이번에 실제로 추가 확보한 자료

반입 가능한 원문 payload **6개·672,919바이트**를 출처와 취득 receipt에 결속했다. 자료 종류가 다른 아래 행수를 하나의 공간 객체 수나 정밀화 완료율로 합산하지 않는다.

| 자료 | 실제 규모 | 판본·단위와 확인 범위 |
|---|---:|---|
| 건축HUB 층별개요 | 24행·19,230바이트 | 면목동77 동일 필지의 한 페이지 전체 응답. 층별 면적 m², 층번호는 높이가 아닌 서수 |
| 건축HUB 기본개요 | 9행·6,302바이트 | 별동8개와 총괄1개. 기존 표제부8행과 대장 PK로 연결 |
| 서울시 보도 현황 XLSX | 5,677행·576,002바이트 | 2025-12-31 기준. 폭/연장 m, 면적 m². 중랑구 관련6개 도로명171행, 사가정 이름/구간 단서19행 |
| NGII 집중 창 DEM 도엽 목록 | 6행·893바이트 | 성동37705의2025/2024/2023/2022/2021/2014판. 실제 raster 아님 |
| 서울 행정동 라이브 CSV | 425행·20,616바이트 | 코드·이름·중심좌표·면적 목록. CP949 이름 일부 손실, polygon 없음 |
| 동일 행정동 공식 UTF-8 응답 | 425행·49,876바이트 | 이름 교차 검증용 별도 원문. 제한된 레코드만 파싱하고 반환 JS를 실행하지 않음 |

건축 자료는 [국토교통부 건축HUB](https://www.data.go.kr/data/15134735/openapi.do), 보도 현황은 [서울 열린데이터광장 OA-22240](https://data.seoul.go.kr/dataList/OA-22240/F/1/datasetView.do), 도엽은 [국토지리정보원 DEM](https://www.data.go.kr/data/15059920/fileData.do), 행정동 목록은 [OA-22160](https://data.seoul.go.kr/dataList/OA-22160/S/1/datasetView.do)에서 취득했다. 건축HUB 이용허락범위 제한 없음과 보도/행정동 공공누리1 원문 근거를 보존한다. 취득일과 포털 갱신일은 조사일·현재 물리 상태 확인일이 아니다.

## 건물: 대장 간 관계 확인, GIS 동일성은 미확정

같은 필지에 한정해 공식 요청2회를 추가했다. r41 표제부8개, 이번 기본개요의 별동8개, 층별개요24개는 **별동 PK8개가 모두 일치**한다. 층별 면적합도 별동별 표제부 연면적과 **8/8 정확히 일치**한다. 총괄표제부 PK는 `10081100210123`이며 숫자 원문을 부동소수점으로 변환하지 않는다. 교사동의 층번호5는 옥탑층이므로 일반5층 또는 층높이로 추론하지 않는다.

그러나 대상 `vworld:al-d010:0000208470354536004400000000`의 건물명·동명·건축물ID가 비어 있고 면적·높이·층수는0이다. GIS A1 식별자와 건축물대장 PK의 namespace가 다르며 A21 참조체계연계키 `B00100000008WVYCO`의 공식 대응표가 없다. 같은 필지 또는 가까운 위치만으로 특정 별동을 결속하지 않는다. 상태는 `PendingExplicitBuildingIdentityEvidence`이며 새 높이 적용은0개다.

**r41 설명 정정:** 일부 PK를21자리라고 쓴 문장은 오류다. 실제 원문은14자리·22자리이며 r41 원문 바이트/숫자 토큰/DB 계보에는 변환 손상이 없었다. 정정 근거는 r2의 `building-register-exact-pk-relations.json`과 `수집-동일성-확인.md`다. 동결된 r1 receipt와 반입 manifest는 수정하지 않는다. 기존 공식 PK 전환 안내를 GIS A1에 임의 적용하지 않는다.

## 보도: 실제 현황표와 공간 표본의 확보 범위 구분

OA-22240은 방향·구간 위치·폭·연장·면적이 있는 실제 현황표다. 도로명 단서171행/사가정19행은 정확한 모델 구간에 연결하기 전의 후보이며 전체 중랑구 보도나 집중250m 창의 검증된 형상 수가 아니다. 폭 값을 모델에 대입하려면 방향과 시작/끝 위치를 결속하고 현황 시점을 대조해야 한다. 원문 면적과 연장×폭이 다른 행을 자동 보정하지 않는다.

[재난안전데이터 인도 자료909](https://www.safetydata.go.kr/disaster-data/view?dataSn=909)의 공개 첨부 SHP/SHX/DBF도 실제로 받았다. **Polyline100개·invalid0개**지만 PRJ·단위·출처별 이용허락이 미확인이다. `privateStorageAllowed=null / PendingSourceSpecificRightsReview`로 보존하고 **DB 반입6개에서 제외**했다. 공개 다운로드 표본은 전국 전체·최신 보도 확보가 아니며 지도 미리보기 CRS를 SHP 원본 CRS로 채택하지 않는다. EPSG:3857 가정하 교차0은 보도 부재 증거가 아니다. 원본 확보와 재사용 권리 확인을 분리한다.

## 지형: 첫 대상 도엽 특정, raster 확보는 대기

동결된 건물 중심250m 창의 SHA `9E53A5968E06421565EE1028EDF13673B39A00950753A8637E4F30D53B72A133`를 기준으로 ENU→ECEF→WGS84→지도 좌표를 변환해 공식 공간조회했다. 중심은127.096686/37.584667이다. 사가정역 중심 창과 현재 건물 중심 창은 별개다.

공식 반환 도엽 성동37705의 경계가 집중 polygon 전체를 포함함을 확인했다. 다른 지역 대조조회에는 성동 결과가0개여서 이름 검색만으로 얻은 후보와 구분된다. `EPSG:5179`는 공식 지도 조회 좌표계이며 raster 좌표계 확인이 아니다. 도엽 경계 포함은 raster 유효 셀 범위·해상도·격자·수직 datum 확인도 아니다.

실제 DEM 다운로드에는 공식 안내상 로그인과 대용량 전송 프로그램이 필요하다. 실제 raster는 받지 않았으며 이 접근 조건을 사용자에게 알렸다. 제한 단계를 우회하거나 프로그램을 설치하지 않았다. 공식 client 좌표계 설명 정정은 `catalog-verification.correction.r1.json`에 별도로 남겼으며 기존 동결26파일은 보존했다.

## 행정동: 코드·이름 대조와 출력 품질 결손

공식 라이브 목록425코드는 기존2023 역사 경계의425코드와 추가/제거 없이 일치한다. 기존30개 모듈도 코드30/30이 존재한다. CP949 CSV에서7개 이름의 가운데점이 `?`로 변했으므로 원본을 고치지 않고 공식 UTF-8 응답을 별도 확보했다. 이름을 정확한 코드로 대조하면 기존30개 이름은 **30/30 일치**한다. 기존 생성기의8자리 제공코드에`00`을 붙이는 표현 규칙과 stable ID를 보존한다.

두 공식 출력의 중심좌표는 일치하지만 **면적25값이 서로 다르다**. UTF-8 출력 일부 값의 끝자리0이 사라진 양상이 있으나 원인을 확정하거나 역산하지 않았다. 양쪽 원문과25개 차이표를 보존하며 어느 값을 정밀 모델의 권위 수치로도 적용하지 않는다. 따라서 이름 대조 성공을 숫자 출력 전체의 신뢰성 확인으로 확대하지 않는다.

포털 갱신2026-09-11과 현재 code/name 동일성은 경계 형상 최신성을 입증하지 않는다. 공개 ZIP 첨부의 기준은2023-10-31이다. 현재 경계 polygon은 여전히 미확보이며 기존 역사 경계와 source current pointer는 교체하지 않는다.

## 기존 수집 계보 반입과 검증

기존 [ASP.NET 파일등록·MySQL 반입 CLI](../../../../../eng/neighborhood-data-collection/README.md)를 재사용했다. manifest SHA는 `F2C222350A1410667AE4802A3F91685A43ED14DE9BA92E95580EDD4178522F3B`다. 원문6개와 intake receipt6개의 출처/판본/취득시각/SHA/크기/비공개 경로를 현재 기존 로컬 MySQL **hongdal_dev**에 저장했다. 원문은 Git 제외 로컬 파일에 보존하며 CSV/XLSX 행이나 대장 관계를 DB에서 행 단위 정규화한 것은 아니다.

- 실제 신규 계보12개: RawSnapshot341~352 / IngestionRun345~356. 전체 Raw330→342 / Run334→346.
- `Partial / PendingHumanReview / NormalizedCount=0`, 기존 NormalizedRecord71,081 유지.
- apply 후 별도 물리 연결 재조회와 별도 verify 모두12/12 일치. 기존 InnoDB transaction으로 반입했다.
- 같은 manifest apply2회는 모두 신규0·쓰기false·commitfalse. Raw16열/Run18열과 LastSeen 전체 지문 `DF45639D35E5BB85CBB89B1B62D4E5FEB29D9ABFD6B46B3A15BFBB7E6457F9B5` 불변.
- r1 기존14DB행과21입력 결속(20파일+manifest) 전후 불변. 이번 JSON 지문 방식은 r1의 과거 TSV 지문과 직접 비교하지 않는다.
- 기존 Program/DLL SHA를 고정해 재사용했고 새 build는 실행하지 않았다. guard self-test31개 통과. Compose·서비스·기존 credential·schema·제품 코드를 변경하지 않았다. 연결 정보는 process 메모리에서만 사용하고 결과에 출력하지 않았다.

root는 반입 전6원본·6intake·원래 receipt의 모든 hash/크기와 r1의 동결20개 결속 파일을 독립 재검증했다. 보도 XLSX ZIP CRC 통과, 행정동30개 이름 대조와 면적25불일치 분리 기록을 확인했다. 원문 수정·Unity/Runtime 적용·자동 주기 수집·commit/push는 수행하지 않는다.

문서5경로 Fast 검증 `20261002-212242`와 diff 검사, 새 r42 문서의 로컬 링크 대조를 통과했다. 이 Fast는 문서 변경 검사로 build/test를 생략했다. 기존 DLL guard31개와 실제 MySQL 반입/재조회/두 no-op 증거는 별도 실행이며 새 제품 build·Unity 실행 증거가 아니다.

기존 역세권 디오라마 규칙 r5와 비교한 결과 **새 디오라마 규칙 후보 없음**이다. 기존 Candidate12/Provisional0/Accepted0와 규칙 원문 SHA를 유지한다. 자료 품질 결손은 이번 source audit에 기록하며 공통 규칙·Graph·Runtime 권위로 승격하지 않는다.

## 결과 위치와 다음 수집 단위

로컬 묶음은 `artifacts/local/neighborhood-data-collection/20261002-r2/`다. 핵심은 `ingestion-manifest.json`, `ingestion-receipts/`, `root-input-verification.json`, `db/`, `buildings/`, `streets/`, `terrain/`, `boundaries/`다. 새 raw와 원래 receipt는 동결하고 수정 설명은 sidecar에 남긴다.

다음 우선순위는 **①대장 PK와 특정 건물 도형을 잇는 명시적 대응·별동 배치 근거 ②보도171/19후보의 방향·시작/끝 위치 결속과 SHP의 CRS/권리 ③성동37705 실제 DEM 및 격자·수직 metadata ④현행 경계 polygon·변경 이력**이다. 확보 가능한 공식 출처부터 기존 자료와 판본/중복을 확인해 취득하고, 기존 DB 반입→독립 재조회→no-op 검증을 반복한다. 미확정 값으로 Unity 형상을 먼저 바꾸지 않는다.
