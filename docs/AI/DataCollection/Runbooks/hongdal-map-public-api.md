# 홍달 지도 공공 API 수집과 조회

홍달 지도용 공개자료의 출처·행정구역·시설·필지·업소·기간 통계를 로컬 SQLite로 모은다. [Claude의 API 명세와 사용법](../../Handoffs/2026-11-19-Claude-공공API-사용법.md), [공용 원장의 반입 기준](../../../../eng/observer-public-data-db/README.md)과 연결한다. 조회 DB는 기존 공용 MySQL, 전국 음식점 DB 및 Unity 굽기 자료와 경로를 따로 쓴다.

## 현재 자료

- [로컬 지도 DB](../../../../artifacts/local/public-data/hongdal-map-api/map-public-data.sqlite)에 **공식 지역 코드 24,207개, 행정동–법정동 관할 21,516개**가 있다. 2026-10-30 공표 원본 SHA-256을 대조하고 같은 원본을 다시 넣으면 판본과 행 수가 유지된다. 시·도 16, 시·군·구 282, 행정동 3,630, 법정동·리 20,279는 해당 코드 판본의 건수다. 다른 판본이나 지도 파일의 도형 수와 바로 비교하지 않는다. [행정안전부 코드 공표](https://www.mois.go.kr/frt/bbs/type001/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000052&nttId=130674)
- [API 목록](../../../../eng/data-collection/map_public_api_catalog.json)은 **13개 출처, 20개 호출**을 관리한다. 2026-11-19에 이전 Ssalddel 로컬 설정의 키를 사용해 **12개 출처에서 66,967개 현재 기록**을 저장했다. 수집 범위와 일부 응답이 끝까지 오지 않은 자료는 [Claude 검토 인계](../../Handoffs/2026-11-19-Codex-홍달지도공공API실수집-인계.md)를 따른다.

## 첫 수집 순서

| 우선 | 자료 | 시험할 호출 |
| --- | --- | --- |
| 1 | 건물·상가 | 면목동 법정동의 건축물 표제부, 면목제3·8동 상가. 층수·높이·상가 건물관리번호와 식별자 일치 여부를 확인한다. 수정일별 상가는 변경·삭제 행을 읽는다. |
| 2 | 버스·도로·필지 | TAGO 도시 코드→정류소→경유 노선. 교통량은 지점→차종별·시간대별로 받는다. 브이월드 필지는 작은 지도 범위를 먼저 확인한다. |
| 3 | 상권·시설 | 상권 경계, 대규모점포·가축사육업·도축업. 인허가의 개방자치단체코드를 제공처에서 확인한 뒤 조건으로 사용한다. |
| 4 | 지역·항만·공항 통계 | 시도 수출입, 시군구·HS6 수출입, 항만 선박 입출항·컨테이너, 한국공항공사 수송. 한 공표 월을 먼저 확인한다. |

건축HUB 대용량 파일, 수치지형도·등고선, SGIS 집계구·격자는 기존 파일 공급 절차와 보유 원본을 사용한다. 외항컨테이너 반출입은 선박 건별 조회이므로 이 지도용 통계 목록에서 뺐다. 기타·후순위 신청 자료는 이번 수집 범위에 넣지 않았다.

## Windows에서 실행

아래 `python`은 현재 PC에 준비된 Python 3.12 이상이다. 기본 위치는 `artifacts/local/public-data/hongdal-map-api/map-public-data.sqlite`다. `init`은 지도용 DB만 만들거나 같은 DB의 호출 목록을 확인한다. 다른 DB에는 쓰지 않는다. `status`는 읽기 전용이다.

```powershell
python -B -X utf8 eng/data-collection/map_public_api.py init
python -B -X utf8 eng/data-collection/map_public_api.py seed-regions
python -B -X utf8 eng/data-collection/map_public_api.py list
python -B -X utf8 eng/data-collection/map_public_api.py status
```

공공데이터포털 키는 `DATA_GO_KR_KEY` 환경변수, `%USERPROFILE%\secrets\data_go_kr_key.txt`의 Decoding 키, Ssalddel의 기존 로컬 User Secrets `PublicData:DataGoKrServiceKey` 순으로 읽는다. 브이월드는 별도 `VWORLD_KEY` 또는 `%USERPROFILE%\secrets\vworld_key.txt`를 읽는다. 웹용 키의 `VWORLD_DOMAIN`에는 등록 URL을 넣는다. 등록 URL이 없는 APP 키는 조회할 PowerShell 창에서 `$env:VWORLD_KEY_TYPE = 'app'`을 설정한다. 이때 `VWORLD_DOMAIN`이 비어 있으면 요청에서 도메인을 생략한다. [브이월드 공식 명세](https://www.vworld.kr/dev/v4dv_2ddataguide2_s002.do?svcIde=cadastral&apiVer=2)

```powershell
python -B -X utf8 eng/data-collection/map_public_api.py collect building_titles --param sigunguCd=11260 --param bjdongCd=10100 --rows 3
python -B -X utf8 eng/data-collection/map_public_api.py collect shops_by_region --param divId=adongCd --param key=11260575 --rows 3
python -B -X utf8 eng/data-collection/map_public_api.py collect bus_cities
python -B -X utf8 eng/data-collection/map_public_api.py collect parcels_bbox --param "geomFilter=BOX(127.0870,37.5800,127.0900,37.5820)" --rows 3
python -B -X utf8 eng/data-collection/map_public_api.py collect trade_sigungu --param strtYymm=202609 --param endYymm=202609 --param sidoCd=11 --param HsSgn=090121
```

기본 한 페이지이며 호출별 형식·인증 인수 이름·페이지 시작값은 목록에서 가져온다. `bounded`는 분량 또는 페이지 구간이 제한된 결과다. `nextPage`가 있으면 같은 조건과 `--rows`로 `--from-page`를 주어 이어받는다. 한 실행은 최대 20페이지이고 교통량은 이 DB에서 전체 교통량 호출을 합쳐 한국 시각 하루 100회를 넘기지 않는다. 외부 호출은 이 DB 집계에 포함되지 않는다. 특정 인증 거절이나 잘못된 응답을 정상 0건으로 처리하지 않는다. 뒤 페이지에 실패하면 그 실행에서 받은 행은 밖으로 반입하지 않는다.

## 지도에서 읽는 관계

| 조회 | 연결 근거 |
| --- | --- |
| `current_regions`, `region_jurisdiction` | 공표 판본·행정동과 법정동의 다대다 대응. 조회 중인 최신 판본의 ID로 관계를 확인한다. |
| `current_map_regions` | 출처가 명시한 행정 코드와 기준 코드의 정확 일치. 기준표에 없는 값은 원코드를 그대로 보며 `region_name`이 비어 있다. |
| `current_map_subjects` | 건물관리번호·건축HUB 표제부·PNU·TAGO 정류소와 노선·교통량 지점·HS6의 식별 체계. 다른 체계는 따로 관리한다. |
| `current_map_features` | 좌표계가 확인된 점과 도형. EPSG:5174는 원 좌표를 보존하며 `pyproj`가 있으면 WGS84 변환치를 함께 담는다. 없는 경우 위경도는 비운다. 교통량 지점의 좌표계 미확인값도 원 좌표만 담는다. |
| `current_map_observations` | 수치·단위·관측기간. 수치는 소수 자릿수를 보존한다. 항만 컨테이너 월 API에는 국내 항만 식별 필드가 확인되지 않아 항만별로 나누지 않는다. 한국공항공사 데이터에는 인천공항이 포함되지 않는다. [컨테이너 API](https://www.data.go.kr/data/15059131/openapi.do) · [공항 API](https://www.data.go.kr/data/15158834/openapi.do) |
| `collection_run`, `run_snapshot`, `response_snapshot`, `record_revision` | 출처·요청 범위·수신시각·관측기간·검증된 필드·응답 SHA 및 변경 이력. 원 응답 전문은 보관하지 않는다. |

서울 상가 응답은 행정동 8자리 코드와 이름을 함께 보낸다. 지역 기준표와 코드 대응·정규화한 동 이름이 모두 같은 행을 10자리 행정동에 연결한다. 상가 건물관리번호는 인계에 있는 주소·건물 후보를 사용한다. 건축HUB 표제부 키는 별도 번호다. 상권 `coords`의 WKT 원문을 보존하고 좌표 범위와 닫힌 고리를 확인한 도형을 같이 조회한다. 관세청의 지역 코드 개편 전후 통계, 인허가 개방기관 코드, 공항·항만 명칭에는 확인된 식별자를 사용한다. 가축사육업은 공개기관 코드·업종·상태 중심이며 개별 농가 좌표·주소·상호는 저장하지 않는다. [서울시 상권 코드표](https://golmok.seoul.go.kr/images/adstrd_code.pdf) · [상가 API](https://www.data.go.kr/data/15012005/openapi.do) · [건축HUB API](https://www.data.go.kr/data/15134735/openapi.do)

```sql
-- 상가와 연속지적도의 PNU가 정확히 일치하는 경우
SELECT shop.record_id AS shop_id, parcel.record_id AS parcel_id, shop.source_identifier AS pnu
FROM current_map_subjects shop
JOIN current_map_subjects parcel ON parcel.scheme=shop.scheme
  AND parcel.source_identifier=shop.source_identifier
JOIN current_map_records active_shop ON active_shop.record_id=shop.record_id AND active_shop.is_deleted=0
WHERE shop.kind='shop' AND parcel.kind='parcel' AND shop.scheme='pnu';

-- 면목제3·8동과 연결된 업소의 현재 지도 점
SELECT r.record_id, f.longitude, f.latitude
FROM current_map_regions r JOIN current_map_features f USING(record_id)
WHERE r.region_kind='administrative' AND r.region_code='1126057500'
  AND f.kind='shop' AND f.longitude IS NOT NULL;
```

내부 수집용이며 게임 배포 자료는 출처별 이용 조건·개인정보·연결 품질을 확인해 별도 팩으로 만든다. Unity는 이 수집 도구를 호출하지 않고 검증된 자료를 읽는다. [현재 공공자료 예약 상태](../README.md)도 따로 관리한다.
