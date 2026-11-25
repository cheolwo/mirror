# 홍달 기존 자료의 가계·기업·정부 분류

2026-11-25 KST · 분류 기준과 조회 방법. 실제 색인과 원본은 같은 PC에서 연다.

자료를 **누가 어떤 판단을 내릴 때 먼저 보는가**로 정리했다. 공공기관이 발표한 소매가격도 장보기에 쓰이므로 가계에서 먼저 본다. 원본 분야, 관측 날짜와 단위, 지역 범위, 기존 열람 범위는 원래 자료를 따른다.

| 먼저 보는 곳 | 기존 8개 분야 | 농수산물·화물 지표 | 추가 참고 | 대표 자료 |
| --- | ---: | ---: | ---: | --- |
| 가계 | 50 | 5 | 0 | 장보기·외식비, 아파트 단지, 대중교통 시간표와 혼잡도, 여가·생활시설 |
| 기업 | 21 | 15 | 0 | 상권·사업체·생활인구, 매장 설비, 경락가·위판가, 화물 조사 운임과 수출입 운송비 |
| 정부 | 46 | 8 | 0 | 공공시설 운영·공사, 교통 수요, 침수·환경 관측, 재배·수확 통계, 화물 원가·안전운임 고시 |
| 공통 | 65 | 2 | 1 | 행정경계·도로망, 달력과 날씨, 인구·세대 집계, 유가, 용마폭포공원 옛 채석장 자료 |
| 합계 | 182 | 30 | 1 | 213개 조회 항목 |

- 주체별 전체 목록(`artifacts/local/public-data/hongdal-data-audiences-20261125/주체별-자료-찾기.md`)에서 각 항목의 원래 자료 ID와 안내를 찾을 수 있다. 안내 파일이 PC에 없는 자료는 기존 분야별 목록의 ID로 보관 위치를 확인한다. 기존 분야별 목록(`artifacts/local/public-data/diorama-data-relations-20261123-r2/분야별-자료-찾기.md`)
- 주된 보는 곳은 항목마다 하나다. 32개 항목에 다른 관점에서 쓰는 이유를 37건 추가했다. 예를 들어 생활물가는 가계에서 먼저 보고 기업은 매장 가격대를, 정부는 생활물가 추이를 파악할 때 같은 자료를 조회한다. 공원 운영표는 정부에서 먼저 보고, 가계는 운영 시간과 이용 가능일을 찾아볼 때 조회한다.
- 농수산물·화물 30개 항목은 기존 DB의 분야와 지표로 나눈 조회다. 예를 들어 가계의 배추 소매가격, 기업의 산지별 경락가, 정부의 재배면적과 생산량은 각각 원래 단계·규격·시간대로 읽는다. DB의 기존 99,832행을 새 파일에 옮기지 않는다. [가격·화물 자료 읽는 법](../../Handoffs/2026-11-24-Codex-농수산물가격-화물비자료인계.md)
- 공통 자료는 가계·기업·정부 화면에서 함께 펼쳐 볼 수 있다. 주민등록 인구와 1인 세대 자료는 지역 집계로 읽는다. 공통의 가로 기준·모델·재질은 디오라마 제작 참고로 남긴다. 절기 색인(`2026-11-23-diorama-season-index.md`) · [용마폭포공원 채석장 자료](2026-11-25-yongma-quarry.md)

## Claude가 연결할 때

SQLite 색인(`artifacts/local/public-data/hongdal-data-audiences-20261125/data-audience-index.sqlite`)에서 `v_primary_items`는 분류별 기본 목록이고 `v_audience_items`는 선택 관점의 기본·보조·공통 자료를 반환한다. 화면 모드는 `view_group`의 `household`, `business`, `government`를 사용한다. 한 원본은 `source_catalogue_id + source_key`로 찾고, 가격은 `selector_family + selector_metric`으로 기존 `v_display_observation`을 조회한다. 지도 축척, 기존 역할별 표시 순서, 열람 범위는 각 원래 기준에서 읽는다. 기존 지도 축척 자료 인계(`2026-11-23-Codex-홍달-지도축척자료.md`)

```sql
SELECT placement, title, use_note, source_key, read_scope
FROM v_audience_items
WHERE view_group = 'business'
ORDER BY CASE placement WHEN 'primary' THEN 0 WHEN 'also_useful' THEN 1 ELSE 2 END,
         field, title;
```

기존 182개 목록을 전수 대조하고 가격 DB 30개 지표와 채석장 참조 묶음을 연결했다. 지도나 Unity 화면에서의 적용은 대기다. 검증 기록(`artifacts/local/public-data/hongdal-data-audiences-20261125/verification.json`)
