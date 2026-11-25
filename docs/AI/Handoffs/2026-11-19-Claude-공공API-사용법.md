# 공공 API 사용법 — 신청한 API별 호출 방법과 지도에 쓰는 법

2026-11-19 08:18 KST · Claude. 내부 검토용 · 배포 아님.

- 사용자: 「기타랑, 나중에 해도 되는 것은 빼두고 일단 다 신청해놨어. API들 어떻게 사용해야되는지 각각 파악을 해봐.」
- 방법: 공공데이터포털 각 API 페이지의 명세(Swagger · 요청변수 표)를 읽어 정리했다. 원본: `artifacts/local/hongdal-unity-claude-20261112/public-api-20261119/swagger-raw.json` · `specs-summary.json`.
- **실제 호출은 아직 못 했다.** 인증키를 보거나 적지 않았고, Claude가 쓰는 두 셸(클라우드 · PC 작업 셸) 모두 `apis.data.go.kr` · `api.vworld.kr`로 나가는 길이 막혀 있다(프록시 403).
- 시험 호출 스크립트: `public-api-20261119/probe_apis.py` — 인증키를 환경변수나 `%USERPROFILE%\secrets\` 파일에서 읽어 API마다 1번(최대 3건) 부르고 `samples/`에 저장한다. 연락처 칸은 저장하지 않는다. PC에서 사용자나 Codex가 돌린다.

## 0. 공통

- 주소: `https://apis.data.go.kr/<기관번호>/<서비스>/<기능>?serviceKey=<인증키>&…`
- 인증키: 공공데이터포털 마이페이지의 **일반 인증키 하나로 모든 API**를 부른다. Decoding 키를 쓰고 프로그램이 URL 인코딩하게 한다(Encoding 키를 또 인코딩하면 「SERVICE_KEY_IS_NOT_REGISTERED」).
- 신청 직후에는 키가 등록되기까지 1 ~ 2시간 걸릴 수 있다(포털 안내).
- 하루 호출 한도(개발 계정): 대부분 10,000 · 교통량 **100** · 공항 5,000. 운영 계정은 활용사례 등록 뒤 늘린다.
- 응답 형식: `_type=json` · `type=json` · `returnType=json` · `output=json`처럼 API마다 이름이 다르다. 안 주면 XML.
- 좌표: 상가 · 버스는 위경도(WGS84). **행안부 인허가(대규모점포 · 가축사육업 · 도축업)는 `CRD_INFO_X/Y`가 EPSG:5174**라서 변환해야 한다.

## 1. 지도용

### 버스정류소 (15098534) · 국토교통부 TAGO
- 서비스: `/1613000/BusSttnInfoInqireService`
| 기능 | 필수 | 나오는 것 |
| --- | --- | --- |
| `getCtyCodeList` 도시코드 | — | citycode · cityname |
| `getCrdntPrxmtSttnList` 좌표 반경 500 m 정류소 | gpsLati · gpsLong | nodeid · nodenm · gpslati · gpslong · citycode |
| `getSttnNoList` 도시별 정류소 | cityCode (nodeNm · nodeNo 선택) | 위 + nodeno |
| `getSttnThrghRouteList` 정류소별 경유 노선 | cityCode · nodeid | routeid · routeno · routetp · 시종점 |
- 지도에서: 정류소 점은 이미 받은 **전국 버스정류장 파일(22만 7천 곳)**로 충분하다. API는 **정류소별 경유 노선**을 붙일 때 쓴다(정류소 굵기 · 노선 수).
- 주의: 서울이 TAGO 도시코드에 있는지는 `getCtyCodeList`로 확인해야 한다.

### 교통량 (15076407) · 한국건설기술연구원
- 서비스: `/1613000/KictTms` · **하루 100건**
| 기능 | 필수 | 나오는 것 |
| --- | --- | --- |
| `spotlist` 측정 지점 | year · dtype(1 고속 · 2 일반 · 3 지방도 · 5 국가지원지방도) · PageNo · numOfRows | 지점번호 · xcode/ycode · 노선 · 구간(시종점) · 차로 수 |
| `vt` 상시 차종별 | year · dtype · month · numOfRows | 지점 · 일 · 방향 · 차종 1 ~ 12 |
| `itmsh` 상시 시간대별 | year · dtype · month · numOfRows | 지점 · 일 · 방향 · 1 ~ 24시 |
| `spothcv` 수시 도로유형별 | year · dtype · pageNo · numOfRows | 지점 · 방향 · 시간 · 차종 |
| `eqplist` · `eqpstate` 측정 장치 | — · equipment_id | 위치 · 상태 |
- 지도에서: 국도 · 지방도 구간의 하루 통행량 → 도로 굵기 · 화물차 비율. **시내 도로는 거의 없다.**
- 한도가 100건이라 지점 목록 → 월별 차종표 순으로 연 1번 받아 둔다.

### 연속지적도 (15056910) → 브이월드 2D 데이터 API
- 공공데이터포털 신청이 아니라 **브이월드 개발키**로 부른다(사용자 확인).
- 주소: `https://api.vworld.kr/req/data` · `service=data&request=GetFeature&data=LP_PA_CBND_BUBUN&key=…&domain=…`
- 필수: `geomFilter`(BOX · POLYGON 등, **한 번에 2 km² 이하**) 또는 `attrFilter=pnu:=:<19자리>`. `size` 최대 1,000 · `page`.
- 나오는 것: pnu · 지번 · 본번 · 부번 · 주소 · 지가 · 기준년월 · 필지 도형(GeoJSON). `crs`로 좌표계를 고른다(EPSG:4326 기본, 5179 · 5186 등).
- 지도에서: 동 하나(약 1 ~ 2 km²)를 몇 번에 나눠 받아 필지 경계 · 지가 색을 깐다. 넓은 범위는 파일(「연속지적도_전국」)이 낫다. 파일의 이용 조건은 상업 이용 금지 유형이었다.

### (이미 승인) 건축물대장 (15134735) · 건축HUB
- 서비스: `/1613000/BldRgstHubService` · 필수 `sigunguCd`(5) · `bjdongCd`(5) · 선택 bun · ji · `_type=json`
- 표제부 `getBrTitleInfo`: **heit(높이) · grndFlrCnt(지상층) · roofCdNm(지붕)** · 주용도 · 사용승인일 등. 층별 · 총괄표제부 · 지역지구도 있다.
- 지도에서: 높이 임시값 건물을 채운다. 다만 전국 일괄은 **사용자가 받은 표제부 대용량 파일**(651 MB · 2026-09)이 낫다. API는 새로 지은 건물 확인용.

## 2. 상권

### 상가(상권)정보 (15012005) · 소상공인시장진흥공단
- 서비스: `/B553077/api/open/sdsc2` · `type=json` (파라미터 이름이 `ServiceKey` · `servicekey`로 섞여 있다)
| 기능 | 필수 | 쓰임 |
| --- | --- | --- |
| `storeListInDong` 행정구역 단위 업소 | divId(ctprvnCd · signguCd · **adongCd**) · key(코드) · pageNo · numOfRows | 동 하나의 업소 목록 |
| `storeListInRectangle` · `storeListInRadius` · `storeListInPolygon` | 영역 | 화면 범위 업소 |
| `storeListInBuilding` · `storeListInPnu` | 건물관리번호 · PNU | 건물에 업소 붙이기 |
| `storeListByDate` | 수정일 | **바뀐 업소만** 받기(최신화) |
| `storeZoneInAdmi` · `storeZoneOne` | 행정구역 · 상권번호 | **상권 경계 도형(coords)** · 상권명 · 면적 |
| `large/middle/smallUpjongList` | — | 업종 분류표 |
| `baroApi` | resId · catId | 행정경계 코드 |
- 나오는 것(업소): 업종 대 · 중 · 소분류, 표준산업분류, 행정동 · 법정동 코드, 지번 · 도로명 주소, **건물관리번호(bldMngNo)**, 층 · 호, 위경도.
- 지도에서: 전국 파일(분기)을 바탕으로 두고, API로 동별 갱신(`storeListByDate`)과 **상권 경계**를 받는다. 건물관리번호로 굽기 건물에 바로 붙는다.
- 주의: 상호가 들어 있다 → 지도에는 업종 · 익명 점 · 집계로.

### 대규모점포 (15154948) · 행정안전부 인허가
- 서비스: `/1741000/large_scale_retail_stores` · `/info`(현재) · `/history`(특정일 기준) · `returnType=json` · pageNo · numOfRows 필수
- 조건: `cond[OPN_ATMY_GRP_CD::EQ]`(개방자치단체코드 = 시군구 단위) · `cond[SALS_STTS_CD::EQ]`(영업상태) · `cond[ROAD_NM_ADDR::LIKE]` · `cond[DAT_UPDT_PNT::GTE]`(갱신 이후만).
- 나오는 것: 사업장명 · 업태(대형마트 · 백화점 · 쇼핑센터 등) · 영업상태 · 인허가일 · 폐업일 · 주소 · 면적 · `CRD_INFO_X/Y`(EPSG:5174).
- 지도에서: 대형 점포 점 · 영업 · 폐업 변화.

## 3. 축산

### 가축사육업 (15155089) · 도축업 (15155105) · 행정안전부 인허가
- 서비스: `/1741000/livestock_farming_v2/info_v2` · `/1741000/slaughterhouses/info` (+ history) · 형식은 대규모점포와 같다.
- 조건: 개방자치단체코드 · 영업상태 · 주사육업종(`cond[PRMN_LSFBIZ_SPECS::LIKE]`, 사육업) · 종업원 수(`cond[TNOEMP::GTE]`, 도축업).
- 나오는 것: 사업장명 · 영업상태 · 주소(사육업은 동 · 호 가림) · 주사육업종 · `CRD_INFO_X/Y`(EPSG:5174).
- 지도에서: 시군구별 축사 밀도 · 도축장 점. 개별 농가는 집계로만 쓴다.

## 4. 무역 · 물류

### 시군구별 품목별 수출입 (15134343) · 관세청
- 서비스: `/1220000/sigunguperprlstperacrs/getSigunguPerPrlstPerAcrs`
- 필수: `strtYymm` · `endYymm`(YYYYMM) · **`HsSgn`(HS 6단위)** · `sidoCd`(시도코드).
- 나오는 것: 시군구명 · 품목명 · 수출 · 수입 금액(USD) · 건수 · 무역수지.
- 지도에서: **품목 하나를 골라** 시군구별 수출입 색칠. 품목마다 따로 불러야 한다(전 품목 합계는 시도 API로).
- 운영 단계는 **심의승인**이다(개발 단계 하루 1만 건으로 시작).
- 확인 필요: sidoCd 값 체계(행정 2자리 11인지, 관세청 자체 코드인지) — 첫 호출로 확인.

### 시도별 품목별 수출입 (15101641) · 관세청
- 서비스: `/1220000/sidoitemtrade/getSidoitemtradeList` · 필수 strtYymm · endYymm · sidoCd
- 나오는 것: HS 부호 · 품목명 · 수출 · 수입 금액 · 건수 · 무역수지.
- 지도에서: 시도별 주력 수출 품목 · 시도 색칠. 품목 목록을 여기서 얻어 시군구 API에 넣는다.

### 항만별 선박 입출항 (15059059) · 해양수산부
- `/1192000/SsopVsslEtryndHarbor2/YM`(sym · eym 연월) · `/Year`(syear · eyear)
- 나오는 것: 항만청 · 항만명 · 입항 · 출항 척수와 총톤수.
- 지도에서: 항만 점(보유 위치 자료)에 입출항 규모 기둥.

### 수출입 컨테이너 처리 (15059131) · 해양수산부
- `/1192000/SsopCargContnImxprt2/Ym`(sym · eym) · `/Year`(syear · eyear)
- 나오는 것: 수출 · 수입 · 환적 컨테이너(TEU, 외항선 · 연안선 · 적 · 공). 연도판은 지역(areaCd · areaNm)별.
- 지도에서: 항만 지역별 컨테이너 물동량.

### 외항컨테이너 반출입 (15057599) · 해양수산부
- `/1192000/CargContnOut3/Info3` · 필수: 항만청 코드 · 입항연도 · 입항횟수 · **선박 호출부호**
- **선박 한 척의 입항 건을 조회하는 API다.** 지도용 집계에는 맞지 않는다. 쓰지 않는 쪽을 권한다.

### 전국공항 수송실적 (15158834) · 한국공항공사
- `/B551178/airport-transport-stats/info` · 필수 `startDePd` · `endDePd`(YYYYMM, 전월까지)
- 선택: 국제/국내(routeBe) · 정기/부정기 · 여객/화물 · 화물 종류(cargoBe).
- 나오는 것: 공항별 도착 · 출발 운항 수 · 여객 · 화물.
- 지도에서: 공항 점(보유)에 화물 · 여객 규모. 인천공항은 별도 API(이미 승인된 국가별 항공 통계)다.

## 5. 지도에 넣는 순서(제안)

1. **키 확인:** `probe_apis.py`를 PC에서 한 번 돌려 15개 응답 모양을 `samples/`에 저장한다(사용자 또는 Codex).
2. **바탕은 파일, 갱신은 API:** 상가 · 버스 · 건축물대장은 이미 받은 파일로 굽고, API는 바뀐 것만 받는다(상가 `storeListByDate`, 인허가 `DAT_UPDT_PNT::GTE`, 건축물대장 startDate).
3. **통계는 코드로 잇는다:** 무역(시도 · 시군구) · 항만 · 공항 · 교통량은 굽지 않고 장부(시도 · 구 단계)에 붙인다(지도 단위별 관리 제안의 「위 단계 장부」).
4. **정기 수집:** 월 1회(무역 · 항만 · 공항 · 인허가 갱신분), 분기 1회(상가), 연 1회(교통량). Codex 수집 예약에 넣을지는 Codex · 사용자가 정한다(지금 예약은 멈춤).

## 6. 막힌 것 · 사용자에게

- **API를 부를 길:** Claude 쪽 셸은 두 곳 모두 이 주소로 나갈 수 없다. ① PC에서 Codex가 돌리거나 ② 조직 설정에서 `apis.data.go.kr` · `api.vworld.kr`를 허용 목록에 넣으면 Claude가 PC 작업 셸에서 직접 부를 수 있다.
- **인증키 두기:** 키는 저장소에 넣지 않는다. `C:\Users\user\secrets\data_go_kr_key.txt`(공공데이터포털 Decoding 키 한 줄) · `vworld_key.txt` 또는 환경변수로 둔다.
- 신청 결과는 공공데이터포털 로그인이 풀려 확인하지 못했다.
