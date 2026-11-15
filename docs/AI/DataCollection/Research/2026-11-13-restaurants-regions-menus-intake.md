# 법정동·행정동·음식점·대표메뉴 실제 수집 — 2026-11-13

사용자 요청에 따라 현재 지도 대상인 **중랑·광진·동대문·노원 4구**의 자료를 수집했다. 원본 취득, 지역별 정리, 메뉴 관측을 구별하며 [출처·판본·파일 SHA 대장](2026-11-13-restaurants-regions-menus-intake.json)에 연결했다. 이번은 자료 수집·정리이며 Unity·운영 DB·수집 예약·공유 latest를 변경하지 않았다.

## 1. 법정동·행정동과 관할 관계

[행정안전부 10/30 공표](https://www.mois.go.kr/frt/bbs/type001/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000052&nttId=130674)의 동일 `jscode20261026.zip`에 있는 B/H/mix를 재사용했다. 법정동·행정동·다대다 관할 관계를 각각 CSV/JSON으로 저장했다. 기존 code.go.kr 11/11 취득 원본과도 4구 법정동28개가 모두 일치했다. 새로 같은 원본을 다운로드하지 않았다.

| 구 | 법정동 | 행정동 | 관할 관계 |
| --- | ---: | ---: | ---: |
| 중랑 | 6 | 16 | 16 |
| 광진 | 7 | 15 | 15 |
| 동대문 | 10 | 15 | 16 |
| 노원 | 5 | 19 | 20 |
| 합계 | **28** | **65** | **67** |

- [법정동 목록 CSV](../../../../artifacts/local/restaurant-public-data-intake-20261113/regions/four-district-legal-dongs.csv)
- [행정동 목록 CSV](../../../../artifacts/local/restaurant-public-data-intake-20261113/regions/four-district-administrative-dongs.csv)
- [법정동–행정동 관할 CSV](../../../../artifacts/local/restaurant-public-data-intake-20261113/regions/four-district-administrative-legal-crosswalk.csv)
- [지역 자료 출처와 검사](../../../../artifacts/local/restaurant-public-data-intake-20261113/regions/README.md)

행정동2개는 복수 법정동을, 법정동20개는 복수 행정동을 가진다. 지역 코드를 단순 1:1 치환하거나 법정동 전체 음식점 수를 모든 행정동에 중복 배분하지 않는다. 코드표는 경계 폴리곤이 아니며 현행 경계를 취득한 것으로 처리하지 않는다.

## 2. 일반음식점·휴게음식점 인허가

[일반음식점 OA-23056](https://data.seoul.go.kr/dataList/OA-23056/S/1/datasetView.do), [휴게음식점 OA-23059](https://data.seoul.go.kr/dataList/OA-23059/S/1/datasetView.do)를 공식 공개 CSV로 취득했다. [자치구 기관코드 OA-22872](https://data.seoul.go.kr/dataList/OA-22872/S/1/datasetView.do)로 4구 기관코드를 확인하고 코드별 필터를 사용했다. Sheet의1,000행 미리보기가 아니라 전체 CSV를 받았으며, 구/종류별8파일 모두 조회 개수와 실제 행수가 일치했다.

| 구 | 일반음식점 원문행 | 휴게음식점 원문행 | 중복 관리번호 정리 후 인허가 키 |
| --- | ---: | ---: | ---: |
| 중랑 | 17,679 | 3,473 | 21,150 |
| 광진 | 18,332 | 4,932 | 23,249 |
| 동대문 | 22,030 | 4,833 | 26,854 |
| 노원 | 16,535 | 4,876 | 21,402 |
| 합계 | **74,576** | **18,114** | **92,655** |

원문92,690행에는 폐업 이력이 포함된다. `(datasetId, 관리번호)` 기준으로 영업/정상19,804개·폐업72,851개다. 이것은 자료상의 인허가 상태이며 현장 영업 여부를 확인한 값이나 고유 물리 매장 수가 아니다. 휴게음식점에는 커피숍뿐 아니라 편의점·패스트푸드 등도 포함되므로 업태 원문을 보존했다.

취득 필드는 관리번호, 사업장명, 인허가일, 상태/폐업일, 업태/위생업태, 지번·도로명주소, 우편번호, 원본 법정동 코드, 두 좌표쌍, 최종수정/자료갱신 시각 등이다. 전화번호·개인명은 조회용 정리표에 복제하지 않았다. 공개 원본 CSV의 열은 수정하지 않고 별도 보존했다.

### 조회용 파일

- [인허가별 정리표](../../../../artifacts/local/restaurant-public-data-intake-20261113/licenses/normalized/restaurants.csv): 92,655개 키. 원본별 행 참조와 중복 주소 후보를 보존한다.
- [법정동별 집계](../../../../artifacts/local/restaurant-public-data-intake-20261113/licenses/normalized/by-legal-dong.csv): 28개 법정동, 영업/폐업과 원본 코드/주소문자열 후보를 구분한다.
- [행정동별 관할 후보 집계](../../../../artifacts/local/restaurant-public-data-intake-20261113/licenses/normalized/by-administrative-dong-candidates.csv): 65개 행정동, 단일/복수 후보를 별도 표시한다. 확정 음식점 수가 아니다.
- [지역 분류 미결19개](../../../../artifacts/local/restaurant-public-data-intake-20261113/licenses/normalized/region-unresolved-records.csv)
- [원문 행 관측 정리표](../../../../artifacts/local/restaurant-public-data-intake-20261113/licenses/normalized/restaurant-source-observations.csv): 원문92,690행을 SHA+CSV행번호로 구분한다.
- [정리 결과와 진단](../../../../artifacts/local/restaurant-public-data-intake-20261113/licenses/normalized/summary.json), [독립 원본 검사](../../../../artifacts/local/restaurant-public-data-intake-20261113/license-review/raw-audit.json)

### 결측과 중복 처리

- 원문 법정동 코드가44,470행 비어 있다. 정리된 인허가 키 중 **48,177개는 원본 코드**, **44,459개는 지번주소의 `서울특별시 + 구 + 법정동명` 정확 문자열 후보**로 구분했다. 코드가 있는 경우를 주소 추정으로 덮지 않았다.
- 코드/주소 충돌7개, 소관 밖 주소8개, 그 외 미결4개는 법정동 집계에 임의로 넣지 않았다.
- 관리번호 중복35그룹은 완전 동일25그룹·지번주소 앞뒤 공백만 다른10그룹이다. 공백 정리 후 실제 주소 차이는0그룹이었다. 모든 원문 행을 보존하고 정리표에 관측 행 목록/개수·주소 후보를 기록했다.
- 현행 관할 관계에서 단일 행정동 후보15,323개, 복수 행정동 후보77,313개, 미결19개다. 복수 후보를 확정 수치로 배분하지 않았다. 폐업 이력에 붙인 관할도 현재 코드표 기준 조회 후보이며 과거 당시의 행정구역 귀속을 증명하지 않는다.
- 원본에는 기존 `좌표정보(X/Y)`와 추가 `X좌표/Y좌표`가 있고 수치 체계가 다르다. 공급 페이지는 GRS80만 적고 있으므로 정확 EPSG를 확인한 것으로 처리하지 않았으며, 변환·위경도 추정·건물 연결을 하지 않았다.

## 3. 음식점 대표메뉴·가격

### 2026년 9월 기준 신규 원본

[행정안전부 착한가격업소 3045247](https://www.data.go.kr/data/3045247/fileData.do)의 공식 CSV를 실제 취득했다. 전국12,935행 중 4구 음식업종만 추출했고 이미용·숙박 같은 비음식 업종은 메뉴로 세지 않았다. 자료 기준은2026-09-30, 포털 수정일은2026-10-16이며 취득일2026-11-13과 구분한다.

| 구 | 음식업소 원본행 | 비어 있지 않은 대표메뉴 |
| --- | ---: | ---: |
| 중랑 | 47 | 76 |
| 광진 | 49 | 81 |
| 동대문 | 93 | 217 |
| 노원 | 42 | 95 |
| 합계 | **231** | **469** |

- [음식업소·메뉴1~4 원문 CSV](../../../../artifacts/local/restaurant-public-data-intake-20261113/menus/national-four-district-stores.csv)
- [대표메뉴·가격 CSV](../../../../artifacts/local/restaurant-public-data-intake-20261113/menus/national-four-district-menu-items.csv): 메뉴명/가격 원문·수치 파싱 결과·원본 행·메뉴 슬롯을 보존한다.
- 같은 파일 이름의 JSON도 저장했다. 231×4=924슬롯을 별도 보존하고 비어 있는 가격은 null로 남겨0원과 구분했다. 이번 비어 있지 않은469메뉴는 가격값도 있었다.

**착한가격으로 지정된 대표메뉴 최대4개이며 전체 음식점의 전체 메뉴판이 아니다.** 같은 상호의 다른 원본행을 자동으로 한 매장으로 합치지 않았다.

### 별도 서울 자료

[가격안정 상품 OA-1174](https://data.seoul.go.kr/dataList/OA-1174/S/1/datasetView.do)·[업소 OA-1173](https://data.seoul.go.kr/dataList/OA-1173/S/1/datasetView.do)와 4구 모범음식점 원본을 추가 취득했다. 음식업종의 상품메뉴435행과 모범음식점 주된음식351행을 [별도 CSV](../../../../artifacts/local/restaurant-public-data-intake-20261113/menus/four-district-menus.csv)로 보존했다. 주된음식이 빈1행도 별도 기록했다. 모범음식점 원문의 행정동명/허가번호는 그대로 남겼다.

OA-1173의 원문 기준년월은 **24-10**이다. 포털 갱신일이2026년이어도 최신 가격으로 표시하지 않는다. 지정일이나 취득일을 메뉴 관측 기준일로 바꾸지 않았다. 서울786행과 행안부469항목은 모집단/시점이 달라 고유 업소·고유 메뉴 합계로 더하지 않는다. [출처별 비교](../../../../artifacts/local/restaurant-public-data-intake-20261113/menus/collection-summary.json)와 [실제 검사](../../../../artifacts/local/restaurant-public-data-intake-20261113/menus/validation.json)를 연결했다.

## 4. 후속 연결과 검증 상한

메뉴와 인허가는 원본 허가번호의 정확 일치를 먼저 확인하고, 미결은 상호·도로명주소의 공백 정규화 후 정확 일치만 후보로 다룬다. 이름만 비슷한 업체, 복수 후보, 층/주소가 다른 매장을 임의 연결하지 않는다. 메뉴 자료와 인허가의 시점·폐업 여부도 따로 남긴다. 연결되더라도 현재 전체 메뉴로 확정하지 않는다.

두 메뉴 묶음의1,255관측에 대해 **허가번호 정확 일치351관측·상호/도로명주소 정확 일치 후보70관측·미해결834관측**을 [연결표](../../../../artifacts/local/restaurant-public-data-intake-20261113/menu-license-links/menu-license-links.csv)와 [결과](../../../../artifacts/local/restaurant-public-data-intake-20261113/menu-license-links/link-summary.json)에 기록했다. 앞의1,255는 서로 다른 출처의 관측 행을 검사한 수이며 고유 메뉴 수가 아니다. 연결 후보 중 인허가가 폐업인9관측도 숨기지 않고 남겼다.

폐업 연결9관측은 고유 인허가4키다. 허가번호가 일치하더라도 주소 원문 문자열이 다른157관측·상호가 다른2관측을 경고로 남겼다. 같은 번호의 출처 참조가 현재 주소·현재 메뉴까지 확정하는 것은 아니다. [연결 독립 검사](../../../../artifacts/local/restaurant-public-data-intake-20261113/menu-license-links/verification.json)를 통과했고 수집 소스의 원래 미연결 표시는 변경하지 않았다.

지역 원본 재판독·인허가8CSV 전수검사·메뉴7CSV의 해시/행수/필수 열 검사를 수행했다. 결측·주소 충돌·범위 밖·중복은 숨기지 않았다. 기존 문서의 상가29,721/음식8,246은 현재 원본 경로가 없어 이번에 수집·반입한 수로 세지 않았다. 개인 배달 자료는 사용하지 않았다.

[정리표 독립 검증28항목](../../../../artifacts/local/restaurant-public-data-intake-20261113/license-review/normalized-verification.json)이 통과했다. 원문92,690↔관측92,690의1:1 보존, 인허가 키92,655개 유일성, 법정동 분류92,636+미결19, 행정동 후보 미배분과 불필요 전화 열 제외를 확인했다.

자료는 `artifacts/local/restaurant-public-data-intake-20261113/`에 있으며 raw/정리표/영수증/검사 결과를 분리했다. 운영 DB 연결·주기 변경·Unity 적용·current 게시·게임팩/외부 배포는 이번 작업에 포함하지 않았다. 새 디오라마 규칙 후보 없음.
