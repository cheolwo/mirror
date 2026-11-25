# 서울 전체·전국 디오라마 입력 자료 준비

**지형 파생물 정정 완료:** 원본 픽셀 원점의 반셀 위치를 보존하지 못한 전처리 결함을 독립 검증에서 발견해 수정했다. 기존 21대표 클립·수도권 파생 DEM은 사용 보류를 유지하고 [r2 교체표](../../../../artifacts/local/public-data/terrain-grid-phase-correction-20261116-r2/receipt/replacement-table.json)의 보정본을 사용한다. 원본 26타일은 정상이다. 보정 23개 투영 래스터·셀 CSV·30동 통계를 원본 직접 재투영과 대조했으며, 서울427동 묶음도 같은 보정을 반영했다. 세 동 참조 입력은 `reference-bundles-r2`다. 원문·옛 영수증·발견 기록은 보존한다.

2026-11-16 KST. 사용자는 중랑구에 한정하지 않고 서울 전체·전국 디오라마를 만들 수 있도록 자료 수집·지역별 정리·Claude 인계를 계속하도록 요청했다. 이번 범위는 **실제 자료 수집과 굽기 전 입력 준비**다. Claude가 진행하는 도로 폭 적용·면목 6동 재굽기, Unity 코드·Resources·Scene, 공용 DB·예약은 변경하지 않았다.

[앞선 공개자료 수집](2026-11-16-map-precision-public-intake.md)을 보완한다. 새 11월 건물 원본과 기존 8월 원본은 별도 판본이다. 옛 모듈의 원본 행 번호를 새 ZIP에 그대로 적용하지 않는다. [통합 자료 색인](../../../../artifacts/local/public-data/nationwide-diorama-readiness-20261116-r1/catalog.json)과 [지역별 준비표](../../../../artifacts/local/public-data/nationwide-diorama-readiness-20261116-r1/regions.jsonl)가 실제 경로·영수증 지문·준비 수준을 연결한다.

## 확보·준비 상태

| 자료 | 실제 범위와 수량 | 정리 상태 |
| --- | --- | --- |
| 최신 GIS 건물 원본 | 2026-11-09 공식 전체분 16개 지역 중 14개 ZIP·합계 1,831,023,691바이트 | 14개 CRC·SHP/SHX/DBF·CRS·구성파일 SHA 검사 통과. 경북·경남은 미수신/불완결 상태로 보류 |
| 서울 건물 도형 | 원본 695,746행, 25구·427개 행정동 후보 | 원본 도형/필지/주소/용도/높이/층수를 SQLite·공간 색인으로 보존하고 427개 동별 준비표 생성 |
| 서울 동별 실제 입력 | 25구427동의 건물·경계·도로·하천·지형 | 공통 절차로 전 동을 실제 추출, 현재다섯자료층 묶음 약118MB. 지형 보정과 전수 재조회 포함 |
| 전국 건축물대장 | 2026년 9월 표제부 8,064,354행 | 원문 지역 코드별 압축 CSV 20개, 서울 25구 파일, 시군구 원문 코드 265종 통계·조회 색인 |
| 전국 경계 후보 | 시도 16·시군구 코드 256·행정동 3,559 | 2026-10-01 편집 후보의 고유 코드와 도형을 보존. 공식 최신 경계 승인 아님 |
| 전국 차량 도로 | 방향별 링크 1,562,356개·노드 1,183,329개 | 지역 코드로 추출할 수 있는 SQLite 공간 색인, 16개 시도 소속표 |
| 전국 하천 | 원본 140,273개 | 지역별 추출 색인·보류 목록. 원문 자기교차 1개는 표시 입력에서 보류 |
| 서울 구별 도로·하천 | 25개 구, 고유 방향링크 63,566개·하천 1,800개 | 25구의 경계·도로·하천 75개 GeoJSON.gz 실제 생성 |
| 전국 기초 지형 | NASADEM 1도 타일 26개 | 신규24장·기존2장 재사용. 지역→타일 목록, 대표21개와 수도권2개 파생본은 별도r2 보정 |
| 전국 OSM 보조 | 20레이어·원문 행 합4,378,091 | 2026-11-15T21:22:28Z 추출판. 서울7레이어149,054개 실제 절단. ODbL1.0 별도 보조층 |
| 서울 밖 건물 추출 | 검증된14개 지역 ZIP을 지역 코드로 스트리밍 추출 | 부산 중앙동673피처행·제주 한림읍16,784피처행 실증. 미수신/잘못된 원본 지문은 거절 |

건축물대장 행, GIS 건물 도형, 방향별 도로 링크, 도형이 여러 동에 속하는 횟수는 서로 다른 수량이다. 원본을 합산해서 확정 건물 수나 실제 운영 시설 수로 부르지 않는다.

## 서울 427동 건물 입력

Claude가 개별 파일을 바로 읽으려면 먼저 [427동 묶음 안내](../../../../artifacts/local/public-data/seoul-diorama-source-bundles-20261116-r1/README.md)와 [동별 색인](../../../../artifacts/local/public-data/seoul-diorama-source-bundles-20261116-r1/index.csv)을 사용한다. 각 동의 `bundle.json`이 건물·경계·도로·하천·보정 지형 파일과 지문을 연결한다. 건물 동 소속700,901건·도로69,915건·하천2,242건이며 서울 고유 원본은 각각695,446·63,566·1,800개다. 하천 원본이 양의 면적으로 걸치지 않는130동은 빈 하천층을 그대로 유지했다. 이는 실제 물이 전혀 없다는 뜻이 아니다.

서울 묶음의 전체674,085개 지형 셀은 원본 전체 타일에서 직접 계산한 값과 대조했다. 보정 후 최대 차이는0.00003052m이고 결측은0셀이다. 이는 원본 재표본화와의 계산 일치이며 현실 지형의 밀리미터 정확도를 뜻하지 않는다. 초기 잘못된 위상 결과·진단과 현재 결과는 분리 보존했다.

정본은 `artifacts/local/public-data/seoul-diorama-building-inputs-20261116-r2/`다.

- `seoul-building-inputs.sqlite`: 원문 건물 695,746행과 원본 고리·복합 도형, 원문 필지/법정동/주소/용도/높이·층수, 동별 교차 소속과 면적, 공간 범위 색인.
- `dong/<행정동코드>.json`: 427개 동의 준비표. 같은 후보 경계·같은 필드·같은 결측 정책을 사용한다.
- `index.json`, `dong-summary.csv`: 25구·427동의 조회 목록과 수량.
- `building-review.jsonl`: 원문 도형/고리 해석의 보류 근거.
- `samples/`: 사직동 1,900개·면목제3·8동 2,722개·신사동 1,393개의 실제 절단 GeoJSON.gz 입력.

원본 695,746개 중 695,446개가 하나 이상의 후보 동과 면적으로 교차한다. 동별 소속은 총 700,901건이며 경계에 걸친 건물은 여러 동에서 같은 원본 행을 참조한다. 300개는 미배정 상태로 보존했다. 원문 무효 도형 44개와 외곽 고리가 확인되지 않는 2개를 포함하며, 나머지는 후보 경계와 유효 면적 교차가 없는 경우다. 가까운 동으로 임의 이동시키지 않았다.

원문 높이가 양수이고 검토 범위 안인 행은 305,790개, 양수 지상층수가 있는 행은 545,173개다. 빈 높이를 층수 곱·상징 높이로 채우지 않았다. 자료에 기록된 값이며 실제 조사 정확도를 새로 인증한 수치가 아니다.

첫 r1 검사에서 SHP의 구멍 고리를 외곽으로 해석할 수 있는 사례를 확인했다. r2는 이를 보류하고 새 DB로 전체 원본을 다시 처리했다. r1은 이전 검토 근거로만 남기고 **최종 소비는 r2**로 한다. 고리/복합 도형을 임의로 고치거나 가장 큰 외곽만 남기지 않았다.

`building.source_row`는 **이번 ZIP SHA에 결속된 1부터 시작하는 원본 행**이다. `source_id`와 PNU 원문을 보존하지만 전역 유일한 건물관리번호라고 가정하지 않는다. `building_dong`은 후보 경계와의 교차 관계이며 법적 관할 확정이나 출입구·보행 접근 증명이 아니다. 대장과의 같은 건물 연결은 이번 서울 확장에서 새로 확정하지 않았다.

동별 JSON 안의 `database` 경로는 해당 `dong/<코드>.json` 위치를 기준으로 한다. `index.json`의 목록은 이를 복사한 탐색 정보다. 독립 소비자는 통합 색인에 지정한 DB 경로와 추출 명령을 사용한다.

## 전국 자료를 꺼내 쓰는 방식

### 서울 밖의 건물

`artifacts/local/public-data/nationwide-diorama-building-extract-20261116-r1/extract_buildings.py`는 고정 경계의 지역 코드로 해당 시도의 검증 ZIP을 찾아 필요한 도형만 스트리밍 추출한다. 전체 전국 건물을 별도 DB로 복제하지 않는다. 원본 레이어명·원본 행·ZIP SHA·주소/법정동/필지/용도/높이/층수 원문과 고리·복합 도형을 보존한다. 무효 도형과 외곽 고리가 확인되지 않는 경우는 별도 보류한다.

부산 중구 중앙동2611051000에서673피처행, 제주 한림읍5011025000에서16,784피처행을 실제 추출하고 경계·유효 도형·각3개 원본 표본의 속성/행/절단 기하를 대조했다. 원본 ZIP 지문 불일치와 경북·경남 미수신 거절3건도 통과했다. 같은 필지/원본 ID/복수 레이어를 곧바로 고유 건물 수로 간주하지 않는다. 다른 지역의 모든 동을 미리 추출한 것은 아니며, 통합 준비표가 원본 보유·필요시 추출 지원·미리 추출한 서울427동을 구분한다.

```powershell
$지도자료Python = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $지도자료Python -B -X utf8 artifacts/local/public-data/nationwide-diorama-building-extract-20261116-r1/extract_buildings.py --region 2611051000 --check-only
```

위 명령은 원본 보유·지문만 읽기 전용으로 확인한다. 아직 추출하지 않은 지역 코드는 `--check-only`를 빼면 `regions/<지역코드>/`에 생성한다. 이미 실증 파일이 있는 코드는 기존 결과를 읽는다. 도구는 영수증이 있는 완료 결과의 덮어쓰기를 거절한다.

### 전국 건축물대장

`artifacts/local/public-data/nationwide-diorama-register-20261116-r1/`의 `index.sqlite`·`district-summary.csv`·`sido/`·`seoul-district/`를 사용한다. 정확한 파일 목록은 영수증·inventory를 따른다. 공식 확인한 27개 원문 열과 필요한 파생 상태만 사용한다.

원문 지역 코드는 20종이다. 현행 경계 후보의 16시도와 다른 `38·46·99` 및 `시군구=0` 한 행을 그대로 보존했다. 보유 법정동 코드와 폐지·미발견 대조가 필요한 행은 1,715개, 음수 높이 211개와 1,000m 초과 98개는 별도 검토 파일에 있다. 옛 코드를 현행 코드로 자동 바꾸거나 자료를 버리지 않는다. 전역 대장 PK 중복은 0이다.

이 대장은 법정동·필지의 기록이다. 시군구 단위 원문 행을 해당 시군구의 모든 행정동에 나눠 넣지 않는다. 지역별 준비표의 대장 수는 **해당 원문 시군구 전체 참조 수**이며 행정동에 귀속된 건물 수가 아니다.

### 전국 도로·하천

`artifacts/local/public-data/nationwide-diorama-vectors-20261116-r1/`의 `national-vectors.sqlite`, `region-index.csv`, `sido-index/`, `regions/`를 사용한다. 기존 전국 원본을 재사용하고 서울 25개 구는 실제 파일까지 추출했다. 16개 시도에서 행정동 하나씩 실제로 추출하는 검증도 수행했다.

- 도로는 2026-09-14 원본의 방향별 링크 ID·시작/끝 노드를 유지한다. 노드 참조 누락·고유 키 결측/중복은 0이다. 선이 교차하거나 가까워졌다는 이유로 새 통행 연결을 만들지 않는다.
- 시도 교차 도로 1,558,651개와 경계 밖/접촉만인 3,705개를 분리했다. 복수 시도 교차는 1,855개다.
- 하천은 시도 교차 138,993개·경계 밖/접촉만 1,279개·원문 무효 1개다. 복수 시도 교차는 1,108개다.
- 서울 구별 합은 도로 64,653개·하천 1,973개이며 고유 원본보다 많다. 구를 넘는 원본을 버리거나 다른 원본 ID로 새로 만들지 않는다.
- 도로 원문 좌표계는 `ITRF2000_Central_Belt_60`의 PRJ를 그대로 사용한다. 투영 수치가 비슷하다는 이유로 원문 datum을 EPSG:5186으로 덮지 않았다. 하천은 EPSG:5179다. GeoJSON 출력은 경도·위도 순서다.
- 이 도로는 주요 차량 도로망이다. 모든 골목·보행로·실폭·교통 통제·도로 단차·안전 운행을 완비한 내비게이션 자료가 아니다.

### 전국 지형

`artifacts/local/public-data/nationwide-diorama-terrain-20261116-r1/normalized/terrain-tiles-index.json`과 `region-to-terrain-tiles.json`에서 타일의 실제 원본·SHA·지역 교차 목록을 찾는다. 원본 합계는 211,714,885바이트이며 새 타일은 187,574,886바이트다.

시도16개와 백령도·가거도·마라도·울릉도·독도 주변의 대표30m 클립21개를 만들었다. **최종 대표 클립 색인은 `terrain-grid-phase-correction-20261116-r2/normalized/national-representative-clips-index.r2.json`**이다. 원본 타일 목록은 기존 경로를 그대로 쓴다. 후보 내부 표본의 결측은0이고 필요한 타일 목록의 결손도0이다. 타일 경계 표본 중복 가능성을 명시했으며 정밀 해안·모든 소도서·전체 영토 완비를 인증한 결과는 아니다.

관측은 2000년 2월이고 높이 기준은 EGM96다. 레이더 표면·보간 영향이 있으므로 현재 정밀 DTM, 건물 높이, 출입구 단차 자료로 쓰지 않는다. 원본 EPSG:4326과 대표 클립 EPSG:5179·30m를 보존했다. 서울 건물의 EPSG:5186과 합칠 때 좌표계와 수직 기준을 따로 확인한다.

### OSM 골목·보행·철도·녹지 보조

`artifacts/local/public-data/nationwide-diorama-osm-auxiliary-20261116-r1/`의 `receipt/completion.json`, `normalized/layers-index.json`, `scripts/extract_auxiliary.py`를 사용한다. [Geofabrik 공식 남한 목록](https://download.geofabrik.de/asia/south-korea.html)에서 동결된 `south-korea-261115-free.shp.zip` 591,666,822바이트를 실제 취득했고 공식 MD5·101개 구성 파일 CRC를 대조했다. 실제 SHP 기준 시각은 ZIP 내부 README에서도 확인했다. 실제 관측·편집 시각이 모든 객체에 동일하다는 뜻은 아니다.

전국 원문 도로1,833,222행·철도24,673행·토지이용면253,188행(공원18,364·산림51,914)·수로39,771행을 확인했다. 좁은길 분류 후보942,363행과 `footway/path/pedestrian/steps` 합306,392행은 폭·차량 진입·보행 가능·연결 그래프를 확정하지 않는다. 레이어별 행 합은 고유 OSM 개체 수가 아니다.

서울은 도로128,399·철도2,897·토지이용면10,340·자연점6,540/면5·수면451·수로422개를 실제 절단했다. 유효 기하와 범위·원문 필드 보존을 검증했다. 국경 주변 버퍼와 선택된 SHP 태그/레이어의 한계를 보존하며, 공식 도로·하천·건물 자료와 자동 병합하지 않았다. © OpenStreetMap contributors·ODbL1.0 조건의 별도 보조층이고 배포 미승인이다.

```powershell
# 검증된 전국 ZIP을 스트리밍해 다른 지역을 별도로 추출한다.
& $지도자료Python -B -X utf8 artifacts/local/public-data/nationwide-diorama-osm-auxiliary-20261116-r1/scripts/extract_auxiliary.py --region 41 --name gyeonggi-review
```

### 원본 픽셀 위상 보정 기록

`merge`의 임의 경계 또는0도 기준 `target_aligned_pixels=True`는 Point 원본의 반셀 원점을 보존하지 못했다. 원본 transform으로 읽을 창을 계산한 뒤 시작 픽셀을내림·끝 픽셀을올림하고 그 정수 창의 실제 bounds로 잘라 보정했다. 전처리 결과를 원본 TIFF band에서 동일 목적 격자로 직접 변환한 값과 독립 비교했다.

기존 전국21대표·수도권2투영 래스터, native crop, 셀 표와 통계를 [별도r2](../../../../artifacts/local/public-data/terrain-grid-phase-correction-20261116-r2/receipt/completion.json)로 다시 만들었다. 옛 격자·CRS·유효 마스크는 보존했고 보정23개의 최대 계산 차이는0.00006104m다. 옛 값과 새 값은 최대79.34m까지 달랐으므로 옛 파생물은 재사용하지 않는다. 보정 NE의 음수1셀도0으로 바꾸지 않았고 통계를 다시 계산했다. 기존 원본·옛 영수증을 덮거나 과거 검증의 의미를 바꾸지 않았다.

## 재조회·추출 예

저장소 루트에서 기존 번들 Python을 사용한다. 새 패키지 설치와 새 API 키가 필요하지 않다.

```powershell
$지도자료Python = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'

# 서울 한 동의 건물을 경계에 맞춰 추출한다. 기존 파일을 덮지 않는다.
& $지도자료Python -B -X utf8 eng/neighborhood/prepare_seoul_diorama_building_inputs.py --mode extract --code 1168051000 --destination artifacts/local/seoul-sinsa-building-input.geojson.gz

# 전국 벡터는 시도 2자리·시군구 5자리·행정동 10자리 코드를 받는다.
& $지도자료Python -B -X utf8 eng/neighborhood/prepare_national_diorama_vectors.py extract --region 2611051000

# 중랑구의 대장 조회와 전국 지형 타일 조회다.
& $지도자료Python -B -X utf8 eng/neighborhood/partition_national_building_register.py --mode query --sigungu 11260
& $지도자료Python -B -X utf8 artifacts/local/public-data/nationwide-diorama-terrain-20261116-r1/scripts/query_terrain_inputs.py --code 11260
```

벡터 추출은 해당 수집 묶음의 `regions/<코드>/` 아래 새 지역 출력을 만든다. 기존 서울 25구 원본 검증 파일을 덮지 않도록 새로운 동 코드를 선택한다. 굽기 도구와 Unity Resources에 쓰는 명령이 아니다.

## 검증과 Claude 인계

원본·출력 지문, ZIP CRC/구성, SQLite 무결성·키 참조, 지역 소속의 수량 보존, 도형 유효성, 지형 격자값, 지역별 실제 추출을 검사했다. [최초 네 묶음 독립 재조회](../../../../artifacts/local/public-data/nationwide-diorama-readiness-20261116-r1/independent-review.json), [최종 아홉 묶음 색인·서울427동·보정 후속 검증](../../../../artifacts/local/public-data/nationwide-diorama-readiness-20261116-r1/catalog-and-bundle-review.json)과 각 묶음 영수증을 함께 읽는다. 수집·준비 완료를 Unity 빌드·실제 화면·출입구 정확성·외부 이용자 배포 완료로 표현하지 않는다.

Claude의 첫 소비는 기존 도로 폭 작업을 유지하면서 서울의 새 동 하나에서 **경계·건물·차량 도로·하천·지형 입력**을 확인하는 것이다. 이후 같은 추출 방식으로 구 전체와 다음 지역을 준비한다. 원본 수신만 된 다른 시도의 건물 ZIP은 지역별 공간 정규화와 대장 연결 검토를 거친 뒤 사용한다. 전국 동일 품질의 골목·보행·공원/녹지·가로수·가로등·철도·출입구·주소 연결은 아직 이 묶음의 완비 층이 아니다. 보유 자료가 없는 것으로 단정하지 않고 추가 정리 대상으로 구분한다.

공식 최신 주소/경계·실폭도로·출입구의 신청 승인·수령은 여전히 별도 확인이 필요하다. 기존 예약·공용 정책·제품 코드를 변경하지 않았으며 커밋·푸시·자료 외부 업로드도 하지 않았다. 모든 자료의 공개 배포 승인은 `false`로 유지했다.

기존 공간층의 판본·좌표계·권위 분리 및 결측 명시 후보를 지지한다. 원문 고리를 자동으로 고치지 않고 격리한 반례를 자료 검증에 반영했지만 공통 규칙·E 단계는 승격하지 않았다. **새 디오라마 규칙 후보 없음.**
