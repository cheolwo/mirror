# 홍달 지도 축척별 자료와 표시 근거

2026년 11월 23일 · 사가정역과 중랑구에서 검토를 시작해 동·타일·구·전국·세계에 맞는 자료를 배치했다. [지도 검토판](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/지도자료-검토판.html)에서 다섯 축척을 바꾸며 값을 볼 수 있다. [축척별 자료 목록](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/축척별-자료-찾기.md) · [Claude 자료 인계](../../Handoffs/2026-11-23-Codex-홍달-지도축척자료.md)

## 축척별 배치

| 축척 | 읽는 내용 | 이번 조회와 대표 사례 |
| --- | --- | --- |
| 동 | 생활시설 점과 필요한 동별 공시 | 표준 좌표가 있는 정류소·무더위·한파 쉼터·시장 3,264점을 구·동·타일 근거에 연결했다. 새로 받은 중랑구 민방위 대피시설 공시 161행은 16동의 공시 합계다. 면목7동 11건은 좌표 공란을 유지하고 동 카드에서 조회한다. |
| 타일 | 한 칸의 시설 개수와 원래 격자 | 현행 게임의 중심 간격 300m 타일 3,189칸과 디오라마 자원 26곳을 대조했다. 사가정역 타일 `(-32,-179)`의 공개 정류소 관측 연결은 4곳이다. 기존 게임 시설 참고점 6개, 게임 타일 건물 271동과 음식점 인허가 84건, 추정 주민 1,194명은 각자의 기준으로 읽는다. |
| 구 | 서울 25구의 사업 환경과 시간대 | 중랑구 2024 일반음식점 3,887곳·휴게음식점 997곳, 2025 일반화물 보유 차량 381대, 2026년 10월 주민등록 인구 377,019명·1인 세대 89,129세대를 조회했다. 중랑구 영세자영업 외식업 2023 공표 추정 매출액 지표는 188.7백만원이다. |
| 전국 | 생산·물류 거점, 시도 집계와 지역 흐름 | 2026년 2분기 공표 산업단지 1,367곳과 시도별 가동업체를 읽는다. 지역 중복 단지 4곳은 시도별 표에서 유지한다. 2024년 서울→경기는 35,347,127톤, 경기→서울은 19,685,491톤이다. 현재 공공 물류시설 조회 691점 중 686점을 기본 내부 조회에 두었다. |
| 세계 | 국가·지역과 거점, 분야별 교역 관계 | 공개 세계 지도 기본 형상 177개와 표시점 247개, 확대용 265개 지도 단위를 분리했다. 자연 지리 파일, 국가 코드, 인구·도시화, 공항·항만 위치를 따로 조회한다. 2026년 6월 인천→샌프란시스코 정기 여객 138편과 같은 달 승객 35,413명은 공항 간 실적이다. 2024년 한국 항만 처리량 31,850,924TEU는 국가 카드에 표시한다. |

### 동·타일

- [동·타일 대조](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/near/읽는법.md)에서 공개 위치·시설 식별자·현행 게임 타일을 연결했다. 기존 D4의 공개 시설 참고점 3,181행과 원본 지문을 대조했다. 새 출처와 같은 정류소를 가리키면 기존 점의 상세를 확장할 수 있다. 중복되거나 자료 범위가 다른 참고점은 원본의 목록을 통해 판단한다. [중랑구 대피시설 공시](https://www.data.go.kr/data/15158871/fileData.do)
- 사가정역 주변 37칸을 2024 SGIS 원천 1km 격자 9칸, 서울 시간대별 250m 원천 격자 60칸과 대조했다. 교차 208건을 독립 검산했다. 서울 250m 공표값 가림 453행과 확인한 범위에 원천 행이 없는 칸·시간을 구분했다. 면적 교차는 지역 관계로만 사용했다. [원천 격자 검증](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/near/grid-review/읽는법.md) · [SGIS 공표 자료](https://www.data.go.kr/data/15141768/fileData.do)
- 면목·사가정·용마산 세 역의 3겹은 각각 37칸이고, 합치면 고유 타일 80칸이다. 세 역 화면의 시설 기록 342건은 고유 출처 기록 240개와 반복 102개에 해당한다. 역별 보기와 구 범위 집계를 각각 계산한다. 사가정역 타일은 SGIS 2025년 기준 면목7동 81.9%, 면목3·8동 18.1%로, 기존 게임의 동 연결과 의미가 다르다. [세 역 비교](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/near/station-crosscheck/읽는법.md)

### 구

- 화면의 구 경계는 SGIS 2025년 6월 30일 서울 25구를 30m 기준으로 간소화한 자료다. 모든 경계의 위상과 구 안의 선택 기준점을 확인했다. 관측 시점은 각 수치 옆에 남겼다. 운수업체 구별 총계 등 공표표의 빈값도 저장했다. [식품위생업 공표표](https://data.seoul.go.kr/dataList/OA-438/S/1/datasetView.do) · [업종별 운수업체](https://data.seoul.go.kr/dataList/OA-702/S/1/datasetView.do) · [영세자영업 추정 매출액](https://data.seoul.go.kr/dataList/DT201015017/S/2/datasetView.do)
- 서울교통공사 역 코드 원본으로 위치 미연결 66행 중 9호선 2·3단계 13개와 상봉·자양 7호선 2개를 결속했다. 2026년 10월 중랑구 오전 8시대 승차는 기존 11개 역·호선 376,394건에 상봉 78,581건을 붙여 454,975건이다. 별개 역·호선이 미연결이면 해당 공표 월의 자료를 보류한다. [역 연결 검증](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/district/station-coverage/읽는법.md) · [공식 승하차 자료](https://data.seoul.go.kr/dataList/OA-12252/S/1/datasetView.do) · [공식 역 식별](https://www.data.go.kr/data/15044230/fileData.do)

### 전국·세계

- **시도 판본.** 행정안전부 2026-10-31 원본 전국 51,075,282명과 1인 세대 10,470,820세대는 16시도 목록과 함께 본다. 2025-06-30 시도 지도는 같은 날 17개 시도 수치를 사용한다. 광주·전남의 통합값을 과거 지도에 나누어 넣는 동작을 보류했다. 2026년 7월 도형 후보는 확인했고, 내려받기와 행정동 여부 확인이 남았다. [현행 인구와 2025년 지도](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/korea/population-context/읽는법.md) · [2026년 7월 경계 조사](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/korea/boundary-refresh/읽는법.md) · [행정안전부 원천](https://jumin.mois.go.kr/statMonth.do) · [브이월드 시군구 목록](https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=MK&dsId=20250122DS00019)
- **거점과 실적.** 산업단지 공표표는 시군구 위치 설명이 명확한 곳만 읽고 좌표 없는 행은 목록으로 본다. 국가물류통합정보센터 위치의 조회일은 확인했고 시설 등록·운영 시점은 공란으로 남았다. 전국 도로 화물 선은 시도 대표점 사이 통계선이고 철도 TEU는 별도 목록이다. [공단 산업단지 공표](https://www.kicox.or.kr/boardDetail/1110?bbsSeq=49696) · [물류시설 지도](https://www.nlic.go.kr/nlic/InfoLogisMap.action) · [도로 화물 공표](https://www.nlic.go.kr/nlic/frghtRoad0010.action)
- **세계.** 공항 운항편·여객과 원두 국가 간 수입, 항만 처리량은 별도 층이다. 2024년 항만 처리량은 공표값이 있는 지도 코드 103곳을 비교할 수 있다. 북한은 확인한 원문의 공란이고, 공표 경제권 코드와 결속되지 않은 곳은 별도 상태다. KATI 원두 수입 수치와 상세 국제 실적은 같은 PC 검토 범위를 유지했다. [세계 조회 기준](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/world/읽는법.md) · [Natural Earth 지도](https://www.naturalearthdata.com/downloads/110m-cultural-vectors/110m-admin-0-countries/) · [국가별 항만 처리량](https://data.worldbank.org/indicator/IS.SHP.GOOD.TU)

## 공통 관계와 아이콘

자료 목록 182건을 동 105·타일 5·구 40·전국 31·세계 1의 우선 축척에 연결했다. 27개의 보조 축척으로 같은 원본을 다시 읽을 수 있다. 구에 붙는 지표와 공표 좌표가 있는 시설을 구별해 실제 조회 35개를 만들었다. 배달 기사·음식점 사장·동네 주민·화물 및 창고·홍달 경영자별 추천 층도 조회할 수 있다. 경기 변화나 수입량을 별도 재료로 쓸 때 각각의 기준 날짜와 공간 코드를 읽는다. 2026년 11월 23일은 한국천문연구원 달력에서 겨울 **소설**이다. [축척과 절기 연결](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/읽는법.md) · [한국천문연구원 절기 계산표](https://astro.kasi.re.kr/kor/almanac/solarTerms/download)

원본 지도 아이콘 40종을 SVG와 24px·48px 투명 PNG로 만들고 기존 182개 자료에 주제 기호를 지정했다. 위치가 확인된 시설은 지도 점의 아이콘, 행정구역 집계는 면의 색과 범례·카드, 실적 관계는 선의 단위와 범례에 대응시켰다. 가까운 아이콘 일부를 점으로 먼저 보이고 선택한 원래 위치에서 큰 아이콘을 보여 준다. [아이콘 모음](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/symbols/아이콘-한눈에.html) · [PNG 사용 기준](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/symbols/읽는법.md)

사가정 37칸에서 공표 횡단보도 대표점 157건, 따릉이 위치 15곳을 별도로 대조해 아이콘을 배치했다. 중심 칸에서는 횡단보도 12건과 따릉이 1곳을 조회한다. 가로수 아이콘은 자료 주제에 지정했고, 가로수 좌표는 자료 이용 조건 표기가 달라 같은 PC 검토로 유지했다. [사가정 표시점과 검증](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/symbols/ground-samples/읽는법.md)

## 검증 범위와 남은 자료

- 182개 자료의 실제 조회 입구는 뷰 169개와 테이블 13개다. 행 수와 파일 지문을 대조했다. 새로 연결한 자료의 키 중복, SQLite 무결성, 외래키, 화면에서 읽는 대표 값을 확인했다. [자료 검증](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/verification.json) · [브라우저 검증](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/review-checks/verification.json)
- 비공개 보관 묶음에는 다섯 축척의 조회 16개, 공표 출처와 사용 가능한 도형, 아이콘 40종을 담았다. PC에 탑재된 MYBOX 폴더에서 ZIP을 다시 열어 파일 지문과 새 폴더의 SQLite 조회를 대조했다. 별도 설치 기기에서의 클라우드 동기화는 확인 대기다. [보관·복원 영수증](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/archive/verification.json)
- 최신 100m 인구 공식 원본과 전국 최신 행정동·시군구 도형의 검증은 남았다. 자치구별 2026년 분기 추정 매출·점포 원본은 제공 페이지에서 파일이나 다운로드 결과를 확보하지 못했다. 이번 구 비교에는 별도로 내려받은 2023 영세자영업 공표 지표를 사용한다. [분기 매출 자료 안내](https://data.seoul.go.kr/dataList/OA-22176/S/1/datasetView.do) · [분기 점포 자료 안내](https://data.seoul.go.kr/dataList/OA-22173/S/1/datasetView.do)
- 세 역 비교는 기존의 관찰 화면과 집계 권위 분리를 지지한다. 역별 중복을 구 집계에서 한 번 읽는 기준을 `Candidate`로 기록했다. 원천별 발행·시작·권리 및 반례, 조립·표현의 후속 확인 범위는 후보 기록에서 읽는다. [역세권 증거 후보](../../../../artifacts/local/public-data/hongdal-map-scale-index-20261123/near/station-crosscheck/검증-후보.json)
- 현재 증거는 같은 PC의 자료 조회와 오프라인 브라우저 표현이다. Unity 반입과 실제 게임 화면 검증은 인계 후 작업 범위다. [Claude 인계](../../Handoffs/2026-11-23-Codex-홍달-지도축척자료.md)
