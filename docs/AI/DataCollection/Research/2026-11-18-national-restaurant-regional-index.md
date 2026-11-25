# 전국 음식점 인허가 원본과 지역별 조회 자료 — 2026-11-18

중랑구와 인접 3구의 [기존 조회 자료](2026-11-18-restaurant-regional-refresh.md)에서 범위를 넓혔다. 행정안전부가 공개한 일반음식점·휴게음식점·제과점영업의 **전국 전체 파일**을 보관하고, 서울 25개 구와 전국 시·군·구, 법정 읍·면·동·리 및 행정 읍·면·동 후보별로 검색할 수 있게 했다.

## 원본과 검증

| 업종 | 전체 행 | 영업·정상 보고 | 폐업 보고 |
| --- | ---: | ---: | ---: |
| 일반음식점 | 2,312,239 | 673,344 | 1,638,895 |
| 휴게음식점 | 654,063 | 198,757 | 455,306 |
| 제과점영업 | 71,180 | 20,457 | 50,723 |
| **합계** | **3,037,482** | **892,558** | **2,144,924** |

2026-11-18 13:24 KST에 HTTP 200의 전체 CSV 3종을 실제 받았다. 원본 바이트 935,123,502개를 압축 307,322,005바이트로 손실 없이 보관했다. 전 행의 열 수, 각 원천 230개 소관기관 코드별 건수와 상태, 원본·압축본 SHA-256을 대조했다. 응답에 전체 기준일이 명시되지 않았고 원문 행 갱신의 최댓값은 11월 17일이다. [일반음식점 공식 정보](https://www.data.go.kr/data/15045016/fileData.do) · [휴게음식점 공식 정보](https://www.data.go.kr/data/15006730/fileData.do) · [제과점영업 공식 다운로드](https://file.localdata.go.kr/file/download/bakeries/info)

| 저장물 | 용도 |
| --- | --- |
| [전국 음식점 조회 DB](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/index/restaurants.sqlite) | 3,037,482행. 업소명·업태·상태·도로명/지번주소·원본 좌표·인허가/폐업/수정 시점을 저장한다. 전화번호는 조회 DB에 복제하지 않았다. |
| [시·도 및 서울 25개 구 요약](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/index/지역별-요약.md) | 전국 시·도와 서울 구별 전체 인허가 및 영업·정상 수. |
| [전국 세부지역 집계](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/index/regional-summary.json) | 시·도, 시·군·구, 법정 읍·면·동·리, 행정 읍·면·동 후보별 집계. |
| [행정 읍·면·동 단일 후보 집계](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/index/by-administrative-area-refined.json) | 공식 관할 단일 후보와 좌표로 하나로 좁힌 후보를 분리해서 집계한다. [좌표 후보 DB](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/coordinate-index/administrative-coordinate-candidates.sqlite) · [좌표 대조 기록](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/coordinate-index/verification.json) |
| [일반 원천 수신 기록](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/raw/general_restaurants.receipt.json) · [휴게](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/raw/rest_cafes.receipt.json) · [제과](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/raw/bakeries.receipt.json) | 실제 취득 날짜, 원본·압축 파일 SHA와 크기, 세부 건수. 같은 폴더의 `.csv.gz`가 원본이다. |
| [DB 판본·검증](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/index/verification.json) · [독립 조회 검사](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/index/independent-verification.json) | 원천, 국가·서울·수원·제주·세종 표본 조회와 SHA 확인. |

고유 인허가는 원천 유형·소관기관·관리번호로 확인했다. 동일 조합 중복은 0개다. 하나의 위치에 여러 업종 인허가가 있을 수 있으며, 영업·정상은 원천의 신고 상태다. 주소를 기준으로 정리한 서울 전체는 **707,073행, 영업·정상 160,936행**이다. 이는 제과가 추가되고 수집처가 다른 전국판의 서울 수치다. 기존 서울시 4구 파일에 합산하지 않는다.

## 지역코드와 조회 기준

행정안전부 2026-10-30 기준 행정동·법정동·관할구역 코드의 전국 판본을 사용했다. 시 안의 구도 시와 구를 각각 저장한다. 세종처럼 상위 시·군·구 칸이 따로 없는 지역과 현재 시·도 이름이 바뀐 과거 주소의 출처 표기를 다음 조회에서도 구별한다. [행정안전부 코드 판본](https://www.mois.go.kr/frt/bbs/type001/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000052&nttId=130674)

- **법정 읍·면·동·리:** 영업·정상 892,558행 중 891,615행의 주소를 현행 법정 코드에 연결했다. 943행은 충돌·이전 주소·하위 주소 불명으로 법정 지역을 보류했다. 주소를 확인한 부분에서는 시·도나 시·군·구까지 조회할 수 있다.
- **행정 읍·면·동:** 공식 관할표에서 후보가 하나인 509,614행을 보존했다. 후보가 여럿인 382,001행은 원본 좌표를 EPSG:5174에서 변환하고 2026-10-01 경계 후보와 대조했다. 가능한 경계를 모두 확보한 상태에서 정확히 한 후보 내부에 든 358,448행만 좁혔다. 두 근거를 합쳐 3,549개 행정 읍·면·동의 **868,062행**에 후보가 한 곳이다. 나머지 24,496행은 주소 미결 943, 좌표 없음 16,227, 경계 부족 4,992, 후보 경계 밖 2,334행으로 남겼다.
- **영업·정상만 읽기:** SQLite의 `v_operating`을 사용한다. `v_operating_admin_candidates`는 관할표의 모든 후보를 담는다. 하나로 좁힌 주소는 아래의 `--refined-admin`으로 조회하고, 공식 관할 단일 후보와 좌표 기반 후보 중 어떤 근거인지 화면에서 확인한다. 두 DB는 같은 원본 파일의 `dataset_id`와 `source_record`로 연결한다.

행정구역 좌표 후보는 해당 날짜의 경계 자료를 사용한 공간 해석이다. 원본 주소·좌표와 판본을 유지하고 공식 현행 귀속·출입구·현장 영업의 확인과 구별했다. 좌표에서 선택한 후보가 공식 관할표에 들어 있는지 전수 대조했고, 262행의 경계 내부 판정을 독립 재현했다.

저장소 루트에서 실행하는 조회 예시다.

```powershell
$restaurantPython = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$restaurantQuery = 'artifacts/local/public-data/national-restaurant-regional-20261118-r1/query_index.py'
& $restaurantPython -B -X utf8 $restaurantQuery --province 서울특별시 --district 중랑구 --limit 5
& $restaurantPython -B -X utf8 $restaurantQuery --province 경기도 --city-county 수원시 --district 영통구 --limit 5
& $restaurantPython -B -X utf8 $restaurantQuery --province 제주특별자치도 --locality 애월읍 --limit 5
& $restaurantPython -B -X utf8 $restaurantQuery --admin-code 1126057500 --refined-admin --limit 5
```

## 아티팩트 정리

과거 APK 빌드 중 별도 보관본과 SHA-256이 같고 추적·사용 정황이 없는 생성 사본 **6개, 946,897,825바이트**를 삭제했다. 삭제 후 여섯 보관본을 다시 읽어 해시가 일치함을 확인했다. [정리·보관본 확인 기록](../../../../artifacts/local/public-data/national-restaurant-regional-20261118-r1/cleanup-receipt.json)

전국 압축 원본 3개와 조회 SQLite 2개의 합은 1,971,412,117바이트다. 지역별 통계는 작은 조회 파일로 함께 제공한다.

이번 작업은 전국 자료 수동 수집과 지역별 저장까지다. 예약·운영 DB·Unity·굽기 파일에는 변경이 없다.
