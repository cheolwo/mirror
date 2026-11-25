# 홍달 디오라마 분야별 공개자료 수집 · 2차

2026년 11월 23일 2차 수집 결과다. 기존 DB 111개와 이번 자료 묶음 71개를 8개 분야의 목록 하나로 찾을 수 있다. 이번 묶음에는 새로 받은 원본을 정리한 DB와 기존 원본을 재사용해 새 관측과 관계를 만든 DB가 함께 있다. 본문의 사례는 저장한 자료를 직접 조회한 결과다. [분야별 자료 찾기](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/분야별-자료-찾기.md) · [테이블 관계 찾기](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/테이블-관계-찾기.md) · [1차 보고](2026-11-23-diorama-data-field-report.md)

| 분야 | 기존 | 2차 | 2차 MYBOX |
| --- | ---: | ---: | ---: |
| 동네 지도와 거리 시설 | 18 | 4 | 4 |
| 도로·버스·지하철 | 23 | 12 | 7 |
| 사람의 움직임과 시간대 | 5 | 6 | 4 |
| 녹지·날씨·야간 | 27 | 16 | 10 |
| 음식점·상품·실내 꾸미기 | 16 | 9 | 6 |
| 동네 생활과 역사 | 16 | 9 | 7 |
| 주거와 물류 | 4 | 7 | 5 |
| 전국 확장 | 2 | 8 | 7 |
| 합계 | 111 | 71 | 50 |

## 화면을 구상할 때 바로 볼 자료

| 화면에서 보고 싶은 것 | 함께 읽을 자료 | 표시 범위 |
| --- | --- | --- |
| 사가정역 주변이 아침과 저녁에 어떻게 달라지는가 | 버스 정류소 월 누계, 지하철 승차 태그와 혼잡도, 면목3·8동의 시간대별 생활인구 | 같은 이름의 버스 정류소와 지하철역을 분리하고, 각 카드에 월·요일·시간대와 단위를 표시 |
| 같은 동네에서 여름과 겨울에 어떤 장소를 쓰는가 | 무더위·한파쉼터, 주민센터 운영표, 가로수, 산림·기상 자료 | 같은 건물의 계절별 역할을 대응. 공표 대표점과 운영 시각을 별도 표시 |
| 동 지도에서 어떤 건물을 더 자세히 그릴 수 있는가 | 도서관 건축면적과 연면적, 용마폭포공원 축구장 치수, 가로수 수종, 지역별 간판 원문 | 확인한 치수의 대상과 단위를 표시. 내부 방·출입구·방위는 별도 자료가 필요한 곳을 표시 |
| 구 지도에서 어느 동까지 자료를 사용할 수 있는가 | 같은 공개 통계 경계에 귀속한 도로·시설·녹지 자료 7개 층의 범위 | 조사 범위 밖은 빈 값. 범위 안에서 일치 행이 없으면 자료 대응 0개로 표시 |
| 전국 지도에서 경계가 달라졌을 때 통계를 어떻게 읽는가 | 인천·광주전남 공식 지역 코드와 월별 주민등록, 녹지·영업소의 이전 장부 | 변경 전후 코드를 따로 읽고 시설점과 시군구 귀속의 증거 수준을 표시 |
| 먹거리 선택이 지역과 가게에 따라 어떻게 보이는가 | 지역별 월간 외식비, 전국 착한가격업소의 대표메뉴·가격, 서울 시장의 실제 조사 가격 | 지역 평균과 업소 사례 각각에 기준일과 단위를 표시. 지도 위 업소 위치는 확인된 장소 키가 있을 때 표시 |

## 동네 지도와 거리 시설

중랑 공영주차장 25곳을 13개 공개 통계동에 대조하고 75개 요일별 운영창을 정리했다. 우림시장 노상 주차장은 44면으로 공표됐고 평일 09:00~19:00, 토요일 09:00~15:00이다. 면목5동과 봉수대는 두 장부의 면수가 달라 둘 다 확인할 수 있게 했다. 동대문 주정차 단속 지정 26구간은 원문 시작·종료 도로 설명을 살렸다. 78개 시간창 중 종료가 시작보다 앞선 27개는 자정 통과 여부를 미확인으로 뒀다. 성남 그늘막 890개에는 설치 유형과 운영계절을 보존했다. 단위가 모호한 77개는 배치 치수 검토가 필요하다. [거리 자료](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/street-operation/읽는법.md)

서울 공공공사 7,768개 사업은 One-PMIS 코드로 공정 원장 두 개를 연결했다. 위치를 확인한 공표점은 3,128개 원천행이다. 두 원장에서 같은 사업의 좌표가 다른 9건을 따로 보존했다. 굴착 허가 4구 11,160건은 사업 공정과 별도 조회를 제공한다. 공정 원장의 최근 관측일은 2025년 11월 14일이며 허가 기간으로 실제 통제를 판단하지 않는다. 공개 문화시설 위치·편의표·BF 인증은 출처를 별도로 유지했다. 45개 시설의 좌표가 0으로 기록된 경우 지점 후보에서 뺐다. [공공공사와 굴착 허가](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/construction-sites/읽는법.md) · [공공시설 접근성](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/building-access-review/읽는법.md)

공공건축물 그린리모델링 전국 선정 1,147행과 서울 동북 4구의 센터 공개 선정 79행을 모았다. 노원구 공식 완료표의 12시설 중 11곳은 전후 효율 등급을, 10곳은 공표 배출량과 절감률을 확인했다. 지도로 집계하는 지역은 구이며 건축물 번호·준공일·배출량 측정 방법은 미확인이다. [공공건축물 그린리모델링](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/public-building-energy-retrofit-context/읽는법.md)

## 도로·버스·지하철

7호선 면목·사가정·용마산의 엘리베이터 9대를 설치표의 장치 일련번호로 별도 치수 자료에 연결했다. 사가정 휠체어리프트는 2026년 9월 편의시설 표에 `Y`이고 같은 기준일의 상세 목록에서 확인되지 않았다. 안내도와 설치표의 인접 출구 표기가 다를 때도 원출처를 볼 수 있다. 기존 버스의 누락 링크 938행은 경로 원천 행·노선·순서·링크번호로 다시 대조했다. [역 접근 설비](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/mobility-access/읽는법.md) · [버스와 역 교차 검증](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/relation-query-verification.json)

고속도로 703개 구간의 평일·주말 차종별 수치와 영업소 방향별 차로 운영 889행을 정리했다. 차로 원장과 주소 원장은 같은 본부·지사·영업소를 확인한 734행에서 이어진다. 영업소 이름 ‘서울’은 주소가 성남시 분당구다. 구간 원장의 같은 이름은 연결 후보로만 뒀다. [고속도로 교통량과 차로](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/highway-observations/읽는법.md)

2026년 10월 버스 승하차 파일에서 동북 4구 노선·정류소 5,736행을 읽었다. 번호·ARS·이름·두 판의 위치를 대조해 1,313개 정류소를 화면 후보로 남겼다. 사가정역 이름을 붙인 버스 정류소 가운데 표준 번호 106000151의 오전 8~9시 승차 4,464건, 저녁 18~19시 승차 9,296건은 같은 달 누적값이다. 따릉이 두 운영판의 공통 2,780개는 같은 공개 좌표였고 19개는 후속 목록에서 빠졌다. 운영 종료일을 확인하지 못한 곳은 누락으로 표기했다. 별도 대전 노선 124개와 인천 정류소 승하차 6,856행은 선형·표준번호·시간 기준을 구분한다. [서울 버스](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/bus-stop-time-counts/읽는법.md) · [따릉이 두 판본](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/public-bike-station-status-history/읽는법.md) · [대전과 인천](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/outside-seoul-mobility-reference/읽는법.md)

전국 특별교통수단 공개 CSV의 17개 시도 68개 값은 표제의 2024년과 달리 국토교통부 e-나라지표 2023년 수치와 모두 일치했다. 2024년 명부와 일치했다는 가정 없이 원문과 대조 결과를 보존했다. 조사대상 시설 적합률, 저상버스, 실제 설치 장치는 별개 정보다. [특별교통수단과 이동편의 조사](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/transport-accessibility-observations/읽는법.md)

서울 야외 계단의 승강기 설치 계획 15곳과 이용 안내 또는 현장 관찰이 공개된 기존 시설 4곳을 조사했다. 광진 무지개계단의 기존 폭은 7m, 길이 13.8m, 고저차 9.1m다. 26인승 승강기와 별도 2.9m 보행로는 계획이다. 교통영향평가 원본 204,544행에서는 조사일과 녹색·황색 숫자를 확인했고 설명에 있는 적색 열은 실제 파일에 없었다. 파주 네 곳의 보행·신호를 1,333 장소일로, 동작구 차량 접근은 설치표와 실제 장비 번호가 맞은 1,008시각으로 정리했다. 시간 단위와 최신 신호 운영은 확인되지 않았다. [야외 수직 보행](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/outdoor-pedestrian-vertical-links/읽는법.md) · [교차로 연구와 보행 관측](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/traffic-study-signal-and-crossing/읽는법.md)

## 사람의 움직임과 시간대

서울교통공사 혼잡도 2026년 2·3분기 두 파일을 열어 39개 반시간대와 요일·방향별 130,338개 수치로 정리했다. 사가정역 3분기 평일 08:00~08:30은 하선 126.7%, 상선 40.2%로 제공됐다. 같은 역의 월별 승차 태그, 동네 생활인구 상대 지수, 주민등록 인구는 각각 기간과 단위가 다르므로 비교 카드에서 따로 읽는다. 면목3·8동은 10월 31일 주민등록 23,328명과 1인 세대 6,508가구, 10월 평일 생활인구 지수 오전 98·점심 105·저녁 103을 조회했다. [열차 혼잡도](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/rail-crowding/읽는법.md) · [전국 주민과 세대](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/population-monthly/읽는법.md)

서울 4구를 한쪽에 포함하는 2026년 11월 9~15일 대중교통 이동의 측정값은 444만여 개다. 출발·도착 양쪽을 확인한 것은 4,146,302개다. 같은 동에서 오전 8시에 출발한 표본에서 월요일 상위는 논현2동 182명, 일요일 상위는 능동·반포4동·논현2동 각 24명이었다. 일주일의 기록이며 개인의 이동 경로는 제공되지 않는다. 원문 빈 시간칸은 0으로 바꾸지 않았다. [서울 4구 이동 방향과 시간](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/local-transit-flows/읽는법.md)

주민등록과 1인 세대의 월별 자료를 2024년 1월부터 2026년 10월까지 34개월 확보했다. 10월 단월 표와 장기 자료의 주민·1인 세대 수치는 당시 기관 3,620곳에서 일치했다. 폐지된 평택시 옛 고덕동은 단월 원본에 인구 0 참조가 있고 장기 원본에는 공란이다. 비교에서 이 상태를 분리했다. 2024년 중랑구의 사업체 42,344개와 소재지 종사자 115,477명은 직장 자료에서 별도로 읽는다. [전국 월별 인구와 세대](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/monthly-population-history/읽는법.md) · [사업체와 종사자](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/daytime-business-context/읽는법.md)

서울 보행 센서의 11월 9~15일 게시 자료는 원천 두 파일의 숫자 85,023개를 교차 확인했다. 중랑 2곳·광진 6곳·노원 3곳의 같은 주 자료를 읽었고 동대문 데이터는 이 주에 없었다. 광진 한 곳은 표기 동과 위치가 달라 지도 후보에서 뺐다. 2015 현장 관측 102,956개 시간대 값은 조사 지점과 연결했다. 현재 센서별 기술을 확인하지 못했으므로 공개 방문 표본을 고유 보행자 수로 환산하지 않는다. [보행 센서와 현장조사](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/pedestrian-street-observation/읽는법.md)

## 녹지·날씨·야간

공개 수질 120개월 원본에서 서울·중랑천 수계 36지점의 4,230 지점월을 정리했다. 중랑천5의 2025년 수온은 1월 4.9°C, 7월 27.8°C, 12월 8.7°C로 기록돼 있다. 묵2동의 중랑구립파크골프장은 9홀, 8,248㎡, 공시 이용 회차 5개를 확인했다. 컬럼 아홉 개가 모두 0인 12개 지점월은 검토 표시를 붙였다. 수질 측정점과 하천 관리코드, 실제 수위와 물색의 해석은 자료마다 구별한다. [수질과 수변 시설](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/green-water/읽는법.md)

서울 과거 침수 원장은 21,578행을 검토해 구별 146개·동별 814개 집계를 만들었다. 동별 건수에는 경계를 함께 걸치는 기록이 있어 이를 합해 구의 고유 기록 건수로 사용하지 않는다. 날짜의 연도가 원장 기재 연도와 다른 429행을 남겼다. 2025년 7월 한강 출입 제한과 청소는 별도 대응 이력이다. 산림 기상 표본은 80개 지점일을 정리했다. 전국 재활용센터의 185개 공표 대표점은 같은 기준의 읍·면·동에서 찾을 수 있다. [침수와 대응 이력](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/flood-history-context/읽는법.md) · [산림과 눈 관측](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/seasonal-landscape/읽는법.md) · [재활용 시설](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/recycling-and-streets/읽는법.md)

국립생물자원관의 중랑천·인근 9권역에서 겨울 조류 집계 54개와 중랑천 일반종 10종의 6개월 수치 60개를 확보했다. 중랑천의 2025년 12월에는 35종·2,847개체, 2026년 1월에는 35종·3,393개체가 기록됐다. 조사권역은 관측 지점을 추정하는 좌표로 쓰지 않는다. [겨울 조류 관측](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/urban-ecology-observations/읽는법.md)

용산구 가로수 7,930행 중 도로명·구·동과 공개 경계에 따라 7,743점을 후보로 남겼다. 산림청의 2023년 생활권 도시숲 통계에서 중랑구 4,766,260.79㎡를, 부산 북구 도시숲 원장에서 개별 시설 18곳의 면적과 수목 수를 읽었다. 이 자료는 시설 한 곳, 가로수 한 그루, 구 전체의 단위가 다르다. 별도 서울 표고점 원본 76,580행 중 기존 30m 격자와 중첩된 2,358개를 비교했다. 중랑구에서 비교한 1,541개의 절대 수치 차이 중앙값은 3.39이며 두 자료의 동일 수직 기준은 확인되지 않았다. [도시숲과 가로수](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/urban-tree-and-forest-context/읽는법.md) · [서울 표고와 지형 격자](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/seoul-terrain-source-review/읽는법.md)

서울 무더위쉼터 3,937행과 한파쉼터 1,503행에서 같은 이름·도로명 주소·동과 가까운 공표점을 확인한 1,396쌍을 장소 후보로 연결했다. 면목3·8동 주민센터의 09:00~18:00 기본, 18:00~21:00 연장, 10:00~18:00 추가는 여름 원천의 운영 표기다. 겨울 표에는 운영 시각이 없고 실제 개폐는 미확인이다. 한강 교량·수변 조명 설치 27곳과 교량 제원 32곳은 내부 참고판으로 분리했다. [계절 쉼터](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/seasonal-shelter-context/읽는법.md) · [한강 교량 조명](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/river-night-landmarks/읽는법.md)

서울 2024~2025 구별 전력과 상수도 표에서 관측값 8,632개를 정리했다. 2025년 중랑구 전력은 8월 134,634 MWh, 12월 95,956 MWh다. 상수도는 연간 사용량이며 가구별 설치·가동을 알 수 없다. 부산·인천 조위관측소 월평균 공표 1,092행은 기준면과 단위가 자료에서 확인되지 않았다. 예측과 기상부이 실측은 별도 내부 자료다. 해수욕장 277곳의 2021년 공표 치수, 시즌 방문과 개·폐장 표 118행을 구분해 정리했다. [전력·상수도](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/district-utility-consumption/읽는법.md) · [조위·부이](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/coastal-tide-and-weather-context/읽는법.md) · [해수욕장과 해변공원](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/seasonal-coastal-public-spaces/읽는법.md)

같은 한 주의 서울 환경 센서 원본 204,318행을 다시 수신했다. 기존 야간 조도 원본과 지문이 같았고 낮 조도·기온·상대습도·소음을 새로 정리했다. 중랑 11월 12일 14시 기온 평균의 센서별 중앙값은 17.5°C, 낮 조도 중앙값은 6,176 lux다. 같은 구·시각의 두 보행 장치에서 표본 행 12개가 있다. 공통 장비나 인과 관계는 확인되지 않아 두 지표를 나란히 조회한다. [거리 환경과 시간대](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/street-environment-timing/읽는법.md)

## 음식점·상품·실내 꾸미기

행정안전부의 2026년 3분기 착한가격업소 전국 자료 두 벌을 대조해 외식업 관측 9,710행과 대표메뉴·가격 19,617개를 정리했다. 중랑구 47개 업소 행에 대표 품목 가격 76개가 있다. 서울 동북 4구 가격 469개는 앞서 받은 서울 원본과 전부 일치해 사례를 이중 합산하지 않는다. 두 전국 파일의 메뉴 이름 표기가 다른 48개 슬롯과 전체 메뉴가 공개 슬롯을 넘는 행도 남겼다. 지역과 업소명만 일치한 항목은 지도에 자동 배치하지 않았다. [전국 메뉴 표와 조회 예](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/good-price-merchant-menu-reference/읽는법.md) · [행정안전부 착한가격업소 자료](https://www.data.go.kr/data/3045247/fileData.do)

참가격의 16개 공표 지역·36개월·9개 외식비 항목을 받아 5,184개 수치를 같은 PC용 DB에 저장했다. 2026년 10월 서울 김밥은 한 줄 3,923원이다. 삼겹살은 업소 표시 기준 18,231원과 200g 환산 21,406원을 구분했다. 2026년 11월은 조회 결과가 비어 있었고 2016년 12월에서 2017년 1월로 넘어갈 때 김밥의 공표 단위가 1인분에서 1줄로 달라졌다. 현재 포털에서 과거 월에 표시하는 광주·전남 이름은 당시 행정구역 코드와 연결하지 않았다. [지역별 월간 외식비](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/dining-price-monthly-reference/읽는법.md) · [참가격 외식비 원문](https://www.price.go.kr/tprice/portal/servicepriceinfo/dineoutprice/dineOutPriceList.do)

서울 4구 시장·마트의 58,492개 가격 관측과 전국 소비자원 표본의 238,801개를 정리했다. 가격 기록을 조사 장소·품목 표기·단위·점검일에 붙이고 지도 대표점은 별도 근거를 사용했다. 사가정시장 번호 `245`에서 11월 12일 관측 77개를 읽을 수 있다. 상품 단위나 식별자가 다른 경우 가격·포장·모형을 자동으로 합치지 않는다. KC·환경표지·식품 공개 표시에서 개별 모델·상품 번호를 확인할 방법을 따로 정리했다. 내부 음식점 기준과는 이름·공개 주소를 함께 맞춘 93건을 선별했다. [시장과 판매가격](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/commerce-space/읽는법.md) · [상품 표시](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/product-identity-public/읽는법.md) · [음식점 지정 근거](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/restaurant-operations/읽는법.md)

공공 비품의 종류·규격 100가지에서 치수 숫자 220개를 확인했다. 단위가 확인된 숫자는 107개이고, 같은 의자 모델의 1150 mm와 1159 mm 표기 차이를 남겼다. 서울 중구 신발장 높이 120 cm와 성남시 150 cm는 각각 폐기 비용의 기준이다. 각각의 자료를 제품 크기 또는 판매 가격으로 읽을 근거는 다르다. [물품 규격과 지방자치단체 분류](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/household-equipment-reference/읽는법.md)

전통시장 44곳의 공표 편의 속성 748개를 지도상의 기존 시장 근거와 맞췄다. 사가정시장 고객지원센터는 있음, 고객휴게실은 없음으로 8월 원장에 표시돼 있다. 11월 가격 77건과 같은 시장 카드에서 별도 날짜로 조회한다. 서초구 옥상간판 44행과 부산진구 옥외광고물 13,832행은 규격 기입값을 추렸다. 단위·축 순서가 확인되지 않은 숫자는 모형의 실제 크기로 바로 쓰지 않는다. 중랑 봉우재로 일대 대상 68곳 중 45곳의 간판 정비 완료는 별개 보고에서 확인했다. [전통시장 편의](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/market-operating-space/open-reference/읽는법.md) · [간판 규격과 정비](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/public-storefront-signage-context/읽는법.md)

## 동네 생활과 역사

중랑 도서관의 시설 번호와 공식 도서관코드를 대조해 5개 관의 연간 대출 통계를 찾았다. 양원숲속도서관은 2024년에 154,567권이 기록됐다. 사가정역 스마트도서관은 2025년 설치표에서 1대로 확인했고, 위치 안내에는 역 대표점을 사용한다. 겸재 작은도서관은 원자료 주소가 달라 동 배정을 보류했다. 서울 미래유산 499행과 35개 체험코스도 받았다. 코스 좌표 174행은 오류·결측이어서, 그중 126개 지점만 정확한 이름·주소가 일치한 다른 유산 대표점을 확인했다. [도서관과 평생교육](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/civic-history/읽는법.md) · [유산과 관광 보행](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/tour-and-heritage/읽는법.md)

용마폭포공원 축구장 111×77m와 이용시간, 어린이대공원 열린무대의 지붕 22×12m, 노원구 맨발길 17구간을 기록했다. 중랑캠핑숲은 공고에 나온 조성계획 면적과 행사 신청 면적을 별도로 담았다. 용마폭포공원 기존 대표 위치와 치수 자료를 원천 번호로 대조했고, 축구장 위치·방위는 미확정으로 뒀다. [공원 운영·규격·계획](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/park-props-operating-space/읽는법.md)

중랑·광진 도서관 7곳의 공개 열람공간 13곳과 층별 면적 18개, 운영시간 6개를 정리했다. 양원숲속도서관의 1·2층 면적은 각각 485.39㎡다. 별도의 4구 달력 93건 중 장소 키를 확인한 행사 10건, 개최가 끝났다는 보고 1건, 현장 관측 4건을 구분했다. 도서관 실내와 행사 결합자료는 같은 PC 검토용으로 보관했다. [도서관 실내 공간](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/public-interior-facility-anchors/읽는법.md) · [지역 행사 달력](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/public-event-calendar/읽는법.md)

한반도 생활사·화폐 유물 공개 목록 566건과 치수 754개·무게 135개를 저장했다. 제작 시대, 계량치의 단위, 소장처를 각각 조회할 수 있다. 공개 목록에 전시실 명칭이 표시된 항목은 2건이며 전시 기간과 현재 진열은 확인되지 않았다. 김제 부거리 옹기가마의 공표 제원은 길이 22.5m·폭 1.0m·높이 1.6m이며, 1층 작업장 면적은 124㎡다. 공식 3D 자산 카드는 두 건의 공표 용량·폴리곤·이용 표시를 저장했다. 실제 파일과 가마 구성은 미확인이다. [유물·시대·치수](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/historical-object-and-place-timeline/읽는법.md) · [옹기가마와 작업장](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/specialized-building-physical-reference/읽는법.md)

한강공원 11곳의 2018년 1월~2024년 2월 기존 원장 815행과 2024년 3월~2026년 10월 통신 추정 352개 공원·월을 나눴다. 뚝섬은 2026년 10월 공원 전체 추정 1,257,472명이며, 별도 물놀이장·수영장 시즌 보고의 뚝섬 방문 182,581명과 날짜·대상이 다르다. 잠실의 과거 같은 공원·날짜 두 원천 행은 값을 합치지 않았다. [한강공원과 계절 운영](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/river-park-seasonal-use/읽는법.md)

## 주거와 물류

서울·전국 3종의 공동주택 자료에서 단지 관측 3,924개, 아파트 단지·세대·공동시설의 면적과 출처를 분리했다. 서울시 중랑 101개 원천행 중 구명과 위치를 대조하고 중복 좌표가 없는 98개를 후보로 남겼다. 원천 좌표계는 미기재다. 전국 휴게소 215곳의 대표점은 2025년 통계동 코드로 확인했고 주차장 표 197개와 방향이 확인된 라운지 56행을 연결했다. 우체국 데이터는 3,307행의 우정청·총괄국과 주소의 광역·시군구 표기를 구분해 기록했다. [주거 자료](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/housing-stock/읽는법.md) · [휴게소와 우체국](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/roadside-support/읽는법.md)

2024년 도로 화물은 서울→경기 35,347,127톤과 경기→서울 19,685,491톤으로 공표됐다. 시도 간 289쌍 모두 전체량과 31개 품목의 합이 일치했다. 철도는 2025년 1~10월 23개 역 이름 사이의 TEU를 적재·공 컨테이너로 나눴다. 산지유통센터 363행과 수산물 가공통계 원천 61,419행도 시설과 시기별 집계로 조회할 수 있다. 개별 운전자·배송·운행 경로는 이 집계에 없다. [도로·철도 화물](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/freight-corridors/읽는법.md) · [식품 공급지](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/food-supply-space/읽는법.md)

철도 화물 취급점의 공표 거리 210쌍을 확인했다. 190쌍에서는 경유지 순서와 누계가 맞았다. 군산항·신광양항·오봉·울산신항은 거리표의 구간과 근거를 찾아갈 수 있다. 현재 운행·실제 선형 확인과 고유역 코드 연결은 별도로 남아 있다. [철도 취급점과 거리](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/rail-network-reference/읽는법.md)

커피의 2025·2026년 9·10월 수입은 생두와 볶은 원두를 품목 코드·상대국·kg·CIF 달러로 읽을 수 있게 했다. 상대국을 실제 발주 기업·입항지·국내 공급망으로 표시할 근거는 없다. [원두 수입 통계](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/coffee-trade-and-roasting/읽는법.md)

## 전국 확장

2026년 7월·10월 국가데이터처 법정·행정동 공식 연계 원문 43,584행과 기존 2025년 SGIS 및 행정안전부 권한 원장을 대조했다. 면목동처럼 하나의 법정동이 여섯 행정동에 속할 수 있어 관계를 양쪽 코드로 조회한다. 오래된 SGIS 3,559동 중 3,067곳의 코드·이름·상위 단위를 확인했다. 승인이 필요한 2026년 10월 경계는 같은 PC에서 3,044쌍의 실제 면적을 비교했다. 면목3·8동의 겹침은 합친 면적 대비 94.5%였다. 오래된 인구를 어느 동에 배분할지는 보류했고 승인 경계 원문은 내부 PC에 두었다. 연안 여객선의 웨이포인트 3,077개와 인천공항 2026년 1~6월 상대 공항별 수송 7,688행도 각각 정리했다. [행정과 통계 코드](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/national-code-editions/읽는법.md) · [같은 PC 경계 대조](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/boundary-overlay-pc/읽는법.md) · [해상과 항공](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/sea-air-motion/읽는법.md)

2020·2025 농림어업총조사를 실제 취득했다. 2025년의 중랑 공표값은 농가 907가구와 경지 330 ha이고, 남양주는 6,174가구와 2,252 ha다. 농가 조사 단위의 경지 합계는 행정구역 안 물리적 농지 면적과 의미가 다르다. 남양주 2026년 텃밭 203구좌는 공급 계획이다. 서울 동북 4구 최신 작물별 면적은 아직 확인되지 않았다. [농업 조사와 공공 텃밭](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/regional-landuse-and-farm/읽는법.md)

인천과 광주·전남의 2026년 7월 개편 코드 441쌍을 실제 인구 원장에 연결했다. 유효한 6월 옛 코드와 7월 새 코드의 주민·1인세대 1,764개 수치가 존재하며, 반대 판본의 같은 수만큼은 공란이다. 광주·전남 옛 구 코드 앞자리만 바꾸면 27개 중 12개가 다른 지역에 붙는다는 것을 확인했다. 고속도로 영업소 43건은 새 시군구까지 확인했으며, 위치 원전이 없으므로 시군구만 기록했다. 인천의 녹지 66건은 바뀐 보고 구와 기존 SGIS 위치가 달라 최신 행정동 귀속을 보류했다. [인천 코드 개편](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/incheon-reorganization-context/읽는법.md) · [광주·전남 통합](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/gwangju-jeonnam-reorganization-context/읽는법.md) · [행정 판본 교차 검증](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/region-edition-join-audit/읽는법.md)

전국 **현행 주민등록 집계 단위 3,620곳** 가운데 3,061곳을 2025 공개 경계 코드와 연결했다. 현행 기관 기준 보류는 559곳, 이전 경계 3,559동 기준 미연결은 498동이다. 광주·전남 출장소 관측 26곳은 통합특별시와 27개 시군구의 해당 월 공식 합계를 재현할 때 포함된다. 구 지도에서 출장소를 별도 도형으로 표시하거나 상위와 하위 집계를 합하지 않는다. 서울 신설동·용두동의 통계 원문은 있고 이전 용신동 경계에 나눠 붙이지 않았다. [공개 지도 연결 범위](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/national-boundary-link-readiness/읽는법.md) · [독립 재검산](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/national-boundary-link-readiness-independent/읽는법.md)

전국 공개 SGIS 페이지의 수정일은 2026년 7월이지만, 실제 전국 경계 판본은 2025년 6월 30일이었다. 2026년 7월 개편을 반영한 전국 도형의 무신청 다운로드는 확인하지 못했다. SGIS는 기준연도 6월 30일 도형을 안내하므로, 이후 2026년판이 공급되면 파일의 코드와 개편 시행일을 대조해야 한다. 주소기반 구역 도형은 별도 신청·이용 목적 심사를 안내한다. 이번에는 확보된 승인판을 같은 PC에서 확인했고, 비교 수치와 공개 경계의 지도 적용 범위를 따로 기록했다. [SGIS 공개 자료](https://www.data.go.kr/data/15129688/fileData.do) · [SGIS 서비스 범위](https://sgis.mods.go.kr/view/pss/openDataIntrcn) · [주소기반 전자지도](https://www.data.go.kr/data/15050416/fileData.do)

순천시의 2026년 10월 공개 CSV를 별도로 취득했다. 포털 설명은 405행과 시군구 경계 2행을 안내하지만 첨부 원본은 403행이며 시군구 단계가 없다. 전부 종전 순천에 해당하는 `46150` 계열 법정구역이다. 공식 변경표에서 고유 법정구역 207개의 새 코드를 확인했다. 옛 행정기관 코드와 숫자가 우연히 일치하는 사례도 11개 있어 법정동과 행정동의 숫자만으로 합치면 잘못 연결된다. 같은 법정코드가 다른 원본 표에 나온 196쌍 중 195쌍은 도형이 정확히 일치하지 않았으며 1쌍은 유효성 문제로 비교할 수 없었다. 도형 기준일은 미기재다. 원본과 검수 근거는 같은 PC에 두고 71개 색인 및 보관판 반영은 보류했다. [순천시 경계 원문 설명](https://www.data.go.kr/data/15178082/fileData.do) · [코드와 파일 대조](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/national-boundary-next-source/읽는법.md) · [도형 재검증](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/national-boundary-next-source/geometry-check.json)

서울 25구의 2023년 지역내총생산 자료를 익명 취득해 공개 통계구 지도에서 구 이름으로 찾을 수 있게 했다. 중랑구의 당해년 생산액은 5,784,606백만원이며 원표의 1인당 생산액은 15,318천원이다. 이 수치는 해당 지역의 생산 활동을 나타낸다. 2023년 집계 범위와 2025년 공개 경계가 같은지는 미확인이다. 전국 주소지별 근로소득은 별도의 국세청 표가 있으며 내려받기 요청이 오류로 끝나 수집하지 못했다. [서울시 지역내총생산과 조회 안내](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/regional-income-source-audit/읽는법.md) · [서울시 데이터 원문](https://data.seoul.go.kr/dataList/OA-22776/S/1/datasetView.do) · [국세청 근로소득 표](https://kosis.kr/statHtml/statHtml.do?orgId=133&tblId=DT_133001N_4215)

## DB 연결과 확인 범위

- 분야별 DB가 각각 원천 판본·표준 단위·원천 행을 가진다. 공통 목록은 자료의 경로·표·열·외래키를 찾고, 교차 조회 DB는 정확히 확인한 행정동·시설 근거만 묶는다. 원본에는 둘 이상의 시점이 있을 수 있어 출처와 관측 시점을 같이 읽는다.
- `places_for_map_review`는 점의 의미와 공개 경계 기준을 읽는다. `place_readings`는 장소의 지표, `dong_readings`는 동의 지표를 읽는다. 코드는 원천과 판본 이름을 보존한다. 실패·다대다·결측·비공개 격자를 실제 0이나 임의 위치로 바꾸지 않는다. [교차 조회 DB](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/scene-relations.sqlite)
- 기존 로컬 MySQL `hongdal_dev`에 수질 216개 관측과 혼잡도 원본 참조 2개를 저장했다. 세 원천을 별도 읽기로 확인했고 같은 명세를 다시 넣었을 때 추가 쓰기 0이었다. [MySQL 재확인](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/mysql-reference/verify.json) · [재실행 확인](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/mysql-reference/repeat-noop.json)
- 목록 DB의 `map_layer_coverage_by_dong`과 `map_layer_coverage_by_district`에서 7개 자료층을 고르고 검토 범위를 볼 수 있다. 버스는 동북 4구 64동·1,313 정류소, 가로수는 용산 16동·7,743점이다. 중랑구에서 용산 가로수 층의 수치는 공란이다. 공개 시장 자료를 살핀 64개 동 중 24개에서 44곳을 연결했다. 같은 층의 지표 단위와 기준 시각을 함께 열어 본다.
- 같은 PC 목록에서 DB 간 관계 32개를 찾을 수 있다. 원천 행이 일치한 연결 21개, 비교·표시용 10개, 출처끼리 일치하지 않은 관계 1개다. 공식 출처 238개와 지문이 있는 판본 368개도 등록했다. 2차 작업 폴더와 재사용 파일 한 곳에서 2차 판본 114개의 원본 바이트를 다시 대조했다. 나머지는 해당 검사 범위에서 취득 기록과 정리본을 중심으로 읽는다. [원본 파일 별도 검수](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/source-payload-audit.json)
- 182개 DB의 지문·무결성을 검증했다. 대표 교차 DB에는 장소 304개와 위치가 확인된 장소 294개가 있다. 대표점 관측은 서로 다른 원천 기록을 포함해 297개다. 나머지 10개 장소는 지역 범위 근거가 있다. 두 목록을 별도로 재생성해 각각 표 16개의 구조·행이 같고, 장소 조회 DB는 파일 단위로 같음을 확인했다. [전체 DB 재조회](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/full-local-database-readback.json) · [목록 재생성](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/catalogue-replay-verification.json) · [장소 조회 재생성](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/scene-replay-verification.json)

MYBOX에 이번 50개 ZIP 107,473,514바이트와 공통 읽기 색인을 보관했다. 검수 중 이용 범위가 섞인 초기 ZIP 9개를 확인해 7개를 선별본으로 교체하고 2개를 PC 전용으로 옮겼다. 연결 드라이브에서 이전 ZIP이 빠진 것을 확인했다. 최종 보관본과 지문이 같은 PC 임시 복사본 108,800,422바이트를 정리했다. 보관판의 1차 96개와 2차 50개를 모두 열어 DB 무결성을 확인했다. 로컬 원본 없이 ZIP에서 역·시장·인구·자전거·중랑구 생산 통계 조회를 재현했다. 보관 색인의 출처 참조 281개와 관계 근거 경로 30개를 실제 ZIP에서 찾았다. 2차 자료 21개는 같은 PC용이다. 클라우드 서버 동기화, 공개 배포, Unity 화면은 미확인이다. [보관 복원 결과](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/archive-readback-verification.json) · [출처·관계 참조 검증](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/portable-provenance-readback.json) · [독립 보관 검수](../../../../artifacts/local/public-data/diorama-data-relations-20261123-r2/latest-portable-forbidden-audit/verification.json)
