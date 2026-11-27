# 서울 전역 생활·교통·상권 조회

2026-11-25 · 홍달 지도 자료 · PC 내부 검토

사가정역과 서울 동부에 쓰던 조회 범위를 **서울 25구·427행정동**으로 확대했다. 기존 300m 격자의 원점·축 좌표를 유지하고, 2026년 10월 공식 경계와 실면적이 겹치는 **8,127개 타일**을 등록했다. 467개 법정동과 행정동 간 관할 관계 746개를 함께 둔다. 타일이 여러 동에 걸치면 각 교차 면적을 기록한다. 동이나 구를 집계할 때 타일 전체 값을 동마다 반복해서 더하지 않는다. [기존 타일 기준과 계산](seoul-tile-evidence.md) · [공식 경계 자료](https://www.data.go.kr/data/15050416/fileData.do)

## 저장된 범위

| 자료 | 날짜와 범위 | 조회 의미 |
| --- | --- | --- |
| 주민등록인구·1인 세대 | 2026-10-31, 427행정동 모두 코드로 연결 | 행정동 원표. 구 합계는 행정동별 원표를 한 번씩 더한다. 개인 배달 수요와 별개다. [인구](https://www.data.go.kr/data/3033254/fileData.do) · [1인 세대](https://www.data.go.kr/data/15097973/fileData.do) |
| 생활인구 | 2026-11-01~21 일별 원본 21개, 타일·일자·시간대 341,334행 중 평균값을 읽을 수 있는 타일 7,469개 | 250m 통신 기반 내국인 체류 추정치를 300m로 배분한 공개분 평균. 날짜·가림·미수록을 조회한다. [서울 생활인구](https://data.seoul.go.kr/dataList/OA-22784/S/1/datasetView.do) |
| 철도 월 누계 | 2026년 10월 원표 622개 역·호선, 서울에 위치가 확인된 389개 | 호선별 대표점에서 읽는 시간대별 교통카드 승차·하차. 서울 밖 229개와 위치 미확인 4개는 자료와 상태를 유지한다. [월별 원천](https://data.seoul.go.kr/dataList/OA-12252/S/1/datasetView.do) · [전국 도시철도 역 자료](https://www.data.go.kr/data/15013205/standard.do) |
| 버스 월 누계 | 2026년 10월 원표 42,782행을 공식 경로에서 다시 받아 SHA를 대조. 노선·정류소 35,102행을 검증해 정류소 9,796곳에 연결 | 당시 위치를 대조한 정류소의 시간대별 교통카드 태그 월 누계. 미결속 원표는 별도 검증 기록에 남긴다. [월별 원천](https://data.seoul.go.kr/dataList/OA-12913/S/1/datasetView.do) |
| 버스 일별 | 11월 19일 정류소 10,689곳, 20일 10,683곳 | 두 날짜의 시간대별 태그. 월 누계와 분리하며 다른 날짜나 요일을 채우는 데 사용하지 않는다. [일별 원천](https://t-data.seoul.go.kr/dataprovide/trafficdataviewfile.do?data_id=40) |
| 음식점·카페·제과 인허가 | 2026-11-18 원본. 서울 25구 영업·정상 신고 160,936건, 타일 연결 158,119건 | 좌표가 없는 2,817건은 구별 집계에 포함하고 타일에는 표시하지 않는다. 업종 분류를 기존 타일과 비교했다. [전국 음식점 원본과 판본](../Research/2026-11-18-national-restaurant-regional-index.md) |

생활인구는 출처 **서울특별시·KT, 서울시 통신데이터**를 표시한다. 승하차 월 누계와 일별 태그는 서로 다른 기간의 건수이며, 생활인구와 합산하지 않는다. 음식점 인허가는 신고 상태와 업종을 나타낸다. 타일별 실제 홍달 주문은 미관측으로 남겨 두었다.

## 통합 DB 관계와 조회

```text
district → admin_dong → resident_month
admin_dong ↔ admin_legal ↔ legal_dong
admin_dong ↔ tile_admin ↔ tile(q, r)
공식 250m 격자 코드 → grid_tile_weight → tile
source_day → tile_presence_day → v_tile_presence_profile
tile · admin_dong → transport_place → transport_tag → v_transport_tile_hour
district · legal_dong · admin_dong · tile → food_license → v_food_tile / v_food_district
source_release → 지역·관측·원천 정리본의 계보
```

타일은 EPSG:5179의 고정 300m 축 좌표 `(q,r)`로 찾는다. 역은 이름 검색 후 **호선과 역 식별자**로 선택한다. 예를 들어 홍대입구의 2호선과 경의선 대표점은 서로 다른 타일에 있다. 행정동은 표준 10자리 코드로 찾고, 법정동은 `admin_legal`의 복수 관할 관계를 확인한다. 구나 동의 타일 목록은 교차를 뜻하며 해당 타일의 모든 시설이 그 동에 있다는 뜻으로 읽지 않는다. 승하차 시설의 행정동은 확인한 대표점을 2026년 10월 공식 경계에 별도로 대조했다.

저장소 루트에서 아래 명령으로 기존 SQLite를 읽을 수 있다.

```powershell
python -B -X utf8 eng/data-collection/seoul_area_index.py --station 서울역
python -B -X utf8 eng/data-collection/seoul_area_index.py --district 은평구
python -B -X utf8 eng/data-collection/seoul_area_index.py --dong 1126057000
python -B -X utf8 eng/data-collection/seoul_area_index.py --tile -32 -179
```

서울역·불광·홍대입구·여의도·강남·사가정역, 은평·강서·강남구, 아현동·면목제7동의 결과를 저장했다. 같은 평일 점심 생활인구 기준으로 불광 3호선 타일은 약 1,439명, 홍대입구 2호선 타일은 약 7,926명, 여의도 5호선 타일은 약 5,942명, 사가정은 약 2,262명으로 조회됐다. 표본 기간과 공개값 누락 상태는 예시의 각 시간대에서 확인할 수 있다. [검증한 조회 예시](../../../../artifacts/local/public-data/seoul-area-evidence-20261125/query-examples.json)

## 다시 만들 때

기존 수집본의 기간·출처 지문을 확인한 뒤 경계, 생활인구, 철도·버스·인허가 색인을 만들고 마지막에 통합 DB를 재생성한다. 경계·위치 변환에는 기존 로컬 GIS 환경을 쓴다.

```powershell
python -B -X utf8 eng/data-collection/seoul_area_geography.py
python -B -X utf8 eng/data-collection/seoul_tile_population.py --output artifacts/local/public-data/seoul-area-evidence-20261125/population --pack artifacts/local/public-data/seoul-area-evidence-20261125/population-geometry-pack.json
python -B -X utf8 eng/data-collection/seoul_area_rail.py
python -B -X utf8 eng/data-collection/seoul_area_bus.py
python -B -X utf8 eng/data-collection/seoul_area_food.py
python -B -X utf8 eng/data-collection/seoul_area_index.py --build
```

현재 검증 범위는 서울이다. 다른 시도에서는 같은 출처·지역·타일·기간 관계를 쓰되, 해당 지역 경계 판본과 관측 원천의 단위·공개 범위를 확인한 뒤 편입한다. 기존 사가정 팩과 Unity 작업본은 각각 별도의 자료다. 이번 DB 약 124MB는 이용 조건과 자료 출처를 보존한 로컬 인계용이다. 기존 수집 예약은 중지 상태다. 새 디오라마 규칙 후보 없음.

[통합 SQLite](../../../../artifacts/local/public-data/seoul-area-evidence-20261125/seoul-area.sqlite) · [범위와 품질 요약](../../../../artifacts/local/public-data/seoul-area-evidence-20261125/area-summary.json) · [독립 검증 기록](../../../../artifacts/local/public-data/seoul-area-evidence-20261125/verification.json)
