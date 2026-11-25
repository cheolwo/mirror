# 중랑·광진·동대문·노원 음식점 공공자료 — 2026-11-18

홍달 배달 플랫폼에서 지역별 음식점 분포와 제휴 후보를 검토할 때 쓰는 자료다. 기존 지도 우선 범위인 중랑구·광진구·동대문구·노원구를 먼저 정리했다.

## 1. 일반·휴게음식점 인허가 갱신

2026-11-18 13:13 KST에 서울 열린데이터광장에서 구별 전체 인허가 CSV 8개를 받아 저장했다. 건수 조회와 본문 요청을 합쳐 16회, 41,091,847바이트를 수신했고 실패는 0건이었다. 원문 수량·기관코드·헤더·SHA-256을 검사한 뒤 SQLite로 옮겼다. 동일 인허가 여부는 데이터셋과 관리번호의 조합으로 판정했다. [일반음식점 OA-23056](https://data.seoul.go.kr/dataList/OA-23056/S/1/datasetView.do) · [휴게음식점 OA-23059](https://data.seoul.go.kr/dataList/OA-23059/S/1/datasetView.do) · [실제 수집 영수증](../../../../artifacts/local/data-collection-runs/seoul-four-district-restaurant-licenses/runs/20261118T041305Z-9a0d53eb2608.json)

| 구 | 전체 원문행 | 고유 인허가 키 | 영업/정상 보고 키 |
| --- | ---: | ---: | ---: |
| 중랑구 | 21,153 | 21,151 | 4,355 |
| 광진구 | 23,272 | 23,257 | 5,449 |
| 동대문구 | 26,868 | 26,859 | 5,171 |
| 노원구 | 21,418 | 21,409 | 4,833 |
| **합계** | **92,711** | **92,676** | **19,808** |

폐업 보고 키 72,868개와 중복 원문행 35개를 보존했다. 인허가 건수, 물리적 점포 수, 현장에서의 실제 영업 여부는 각각 별개의 값이다. 구는 원천의 소관기관 코드 기준이다. 수집 시각과 점포별 인허가·수정·갱신일을 함께 보존했으며 전국이나 서울 전체를 수집한 수치로 사용하지 않는다. 인허가 원천 이용조건은 공공누리 1유형이다.

## 2. 지역별 조회

- **구·업종·상태:** [음식점 조회 DB](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/license-index/restaurants.sqlite)의 `v_district_index`와 `v_observations`에서 읽는다. 원문 보고 상호, 업태, 도로명·지번주소, 인허가·폐업·수정일, 원천 행 번호와 SHA를 확인할 수 있다. 전화번호·개인명 필드는 파생 조회 DB에 복제하지 않았다. [출처 명세](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/license-index/source-manifest.json) · [92,711행 독립 대조](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/license-index/independent-verification.json)
- **법정동 28개:** [법정동별 영업/정상 색인](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/license-index/by-legal-dong.json)에 19,806키를 넣었다. 19,594키는 원천 코드, 212키는 원천 코드가 비었을 때 지번주소의 시·구·동이 공식 코드표와 정확히 맞은 후보로 표시한다. 원천 코드와 주소가 달랐던 2키의 위치 판단은 보류했다. 모든 영업/정상 키가 색인 또는 보류에 한 번씩 있는지 대조했다.
- **행정동 65개:** 최신 인허가 가운데 이전 자료의 도로명주소·지번주소가 모두 맞는 **16,971키**의 기존 동·건물 후보를 이어 썼다. **2,837키**는 주소 차이·충돌 또는 기존 후보 없음으로 대기다. 행정동별 표는 같은 인허가의 복수 동 후보를 포함할 수 있으므로 행을 합해 실제 매장 수로 쓰지 않는다. 단일 건물·단일 동, 같은 동에 복수 건물, 복수 동 후보를 표에서 구별한다. [65동 후보표](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/administrative-index/by-administrative-dong.md) · [후보 조회 DB](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/administrative-index/administrative-candidates.sqlite) · [후보 근거·검증](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/administrative-index/summary.json)

예를 들어 면목제3·8동의 후보 표시는 390키다. 그중 단일 건물·동 후보는 315, 동일 동의 복수 건물 후보는 64, 다른 동도 가능한 후보는 11이다. 기존 2026-10-01 행정동 경계 후보와 2026-08-09 건물 원본의 연결을 재사용했으므로 최신 공식 공간 귀속을 새로 검증한 결과로 쓰지 않는다. 인허가 DB와 행정동 후보 DB는 데이터셋과 관리번호의 조합으로 연결한다.

## 3. 공개 대표메뉴와 가격

서울시의 신규 [착한가격업소 OA-23138](https://data.seoul.go.kr/dataList/OA-23138/S/1/datasetView.do)에서 서울 전체 원문 2,155행을 받았다. 4구 음식업소 **231행·대표메뉴와 가격 469관측**을 건별 출처 행과 메뉴 칸 번호를 포함해 별도 저장했다. 중랑 76건, 광진 81건, 동대문 217건, 노원 95건이다. 공개 페이지의 11월 16일은 공개·갱신일이며 업소별 가격 관측 기준일은 확인되지 않았다. [기존 서울시 자료 종료 및 대체 공지](https://data.seoul.go.kr/together/notice/datasetNoticeView.do?bbsCd=10008&ditcCd=&pageIndex=1&seq=04858ed9665e99802030ae579aad2744) · [지역별 대표메뉴](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/menu-source/four-district-menu-observations.json) · [수집·출처 명세](../../../../artifacts/local/public-data/restaurant-regional-20261118-r1/menu-source/source-manifest.json)

4구 469관측은 기존 [행정안전부 9월 30일자 자료](https://www.data.go.kr/data/3045247/fileData.do)의 같은 범위와 일치했다. 두 출처의 항목 수를 더하지 않는다. 착한가격업소의 공개 대표메뉴에 한정되며, 메뉴 관측을 최신 영업 인허가나 현재 판매가로 자동 연결하지 않았다. [기존 서울 모범음식점·가격안정 메뉴 자료](2026-11-13-restaurants-regions-menus-intake.md#3-음식점-대표메뉴가격)도 별도 출처로 보관한다.

## 4. 홍달 기획에서 읽는 순서

구별 업태·영업 상태를 비교한 뒤, 첫 운영 동의 단일 건물·동 후보부터 살펴본다. [지도 음식점 제휴 기획 r289](../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/delivery-branch-growth-play-loop.design.r257.md)은 실제 위치·업태를 참고해 가상 점포를 배치하는 범위다. 원천 상호·주소는 관찰 자료이며 가상의 계약·주문·평가에 바로 연결하지 않는다. 확인되지 않은 위치·메뉴는 결측 또는 후보 상태로 남긴다.

현재 자료는 게임에서 쓸 지역별 검토 입력이다. 실측 위치·점포 출입문·현재 영업을 확인한 수치와 인허가 보고 상태를 구별한다. 실제 게임 점포 생성·지도 연결·예약 변경은 이번 자료 작업에 포함하지 않았다.
