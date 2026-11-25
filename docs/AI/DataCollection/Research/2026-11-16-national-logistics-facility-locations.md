# 전국 물류시설 위치 자료 — 2026-11-16 실제 취득·검증

- 목적: 전국 지도에서 물류센터·창고·보세 관련 시설·항만·공항의 위치를 빛으로 찾기 위한 참고 자료. 사용자가 필요한 공공 자료의 수집을 허용했다.
- 상태: `OfficialSourcesCollected / ExistingNlicReused / LocalReferencePrepared / CoordinateValidationPassed / NationwideCoverageIncomplete / GameBindingPending`.
- 연결 기획: [전국 지도·시설 빛 현행 r252](../../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/nationwide-freight-management-map.proposal.r155.md). 상세 표현은 이 기획이 소유한다.
- 근거: [수량·출처·SHA-256](2026-11-16-national-logistics-facility-locations.json), [로컬 전체 명세](../../../../artifacts/local/national-logistics-facility-locations-20261116/manifest.json), [독립 검증](../../../../artifacts/local/national-logistics-facility-locations-20261116/verification.json).

## 실제 확보 범위

| 원천 | 출처별 관측 | 좌표와 기본 표시 검토 후보 | 판본·제약 |
| --- | ---: | --- | --- |
| 기존 NLIC 등록창고·단지·터미널 | 6,100 | 좌표 없음 | 등록창고 6,011·단지 59·터미널 30. 11월 13~14일 취득 원본과 SQLite를 읽기 전용 재사용. 시설별 기준일 미기재 |
| 경기도 물류 창고업체 CSV | 1,758 | 유효 좌표 1,733, 이 중 출처상 영업중 1,005행 | 경기도 범위. CSV의 관측 기준일은 미기재. 2026-11-16 취득일을 시설 현황 기준일로 바꾸지 않음 |
| 한국공항공사 공항 위치정보 | 14 | 14점 | WGS84, 원문 기준일 2026-09-23 |
| 국토교통부 AIP 인천공항 기준점 | 1 | 1점 | 2026-10-15 발행 묶음의 RKSI ARP. 도분초를 WGS84 경위도로 변환. 발행일과 현장 관측일 구분 |
| 해양수산부 항만정보 | 72 | 위치 설명·명부, 좌표 없음 | 파일 판본 2023-12-12. 포털 수정일 2025-06-27과 구분 |
| 국립해양조사원 항만구역 | 70 | 공식 면형 도형에서 대표점 70개 도출, 55개를 기본 후보로 선별 | 파일 판본 2025-08-11. 원본 EPSG:5179 → EPSG:4326. 항만 출입구·부두 실측점이 아님 |

합계는 **8,015개의 출처별 관측**이다. 경기도 자료와 NLIC의 같은 시설 가능성을 아직 병합하지 않았으므로 고유 시설 수가 아니다. **기본 표시 검토 후보는 1,075점 = 공항 15 + 경기 창고 1,005 + 항만구역 대표점 55**다. 전국 창고·물류센터의 위치가 완비됐다는 뜻이 아니다.

기존 등록창고의 `관련법률=관세법` **764행**은 보세 관련 시설 조사 후보로 보존했다. 좌표는 전부 미확보다. 이 분류만으로 현재 보세 허가·실가동을 확정하지 않으며 임의의 시·군 중심이나 `(0,0)`에 놓지 않는다. 별도의 관세청 허가 현황·공식 좌표/주소 보완이 후속이다.

## 원문 출처와 취득 방법

- NLIC [등록창고](https://www.nlic.go.kr/nlic/WhsInfoWarehouseSch.action), [물류단지](https://www.nlic.go.kr/nlic/fmComplex0010.action), [물류터미널](https://www.nlic.go.kr/nlic/fmTerminal0010.action): [기존 반입 보고](2026-11-14-logistics-facilities-db-intake.md)의 원본과 관측 식별자·취득 시각을 재사용했다. 이번에 갱신 수집한 것으로 계산하지 않는다.
- [한국공항공사 공항 위치정보](https://www.data.go.kr/data/15002851/fileData.do): 공공데이터포털의 공개 CSV 다운로드로 14행 취득.
- [국토교통부 AIP 인천공항](https://aim.koca.go.kr/eaipPub/Package/2026-10-15/html/eAIP/KR-AD-2.RKSI-en-GB.html?amdt=show): `372745N 1262621E`를 `37.4625, 126.4391666667`로 변환. 같은 발행 묶음의 [GEN 2.1 수평 참조 기준](https://aim.koca.go.kr/eaipPub/Package/2026-10-15/html/eAIP/KR-GEN-2.1-en-GB.html)으로 WGS84를 확인하고 별도 원문·취득 영수증을 저장했다.
- [경기데이터드림 물류 창고업체 현황](https://data.gg.go.kr/portal/data/service/selectServicePage.do?infId=8768459560O0NKL9G5N61044701&infSeq=1): 공식 공개 전체 CSV 1,758행 취득. 공개 API의 표본 5행·전체 수 1,758도 대조했으며 표본 API를 반복 호출해 제한을 우회하지 않았다.
- [해양수산부 항만정보](https://www.data.go.kr/data/15088273/fileData.do): 공개 CSV 72행 취득.
- [국립해양조사원 항만구역](https://www.data.go.kr/data/15130180/fileData.do): SHP·DBF·PRJ·CPG 등이 있는 공개 ZIP 취득. 원본 도형과 한국어 이름·원문 속성을 보존했다.
- [해양수산부 항만가이드라인 위치](https://www.data.go.kr/data/15121268/fileData.do): 2020-07-31 판본의 **2,344개 안내/위치 참조 행**도 별도 저장했다. 항만 12개 명칭의 안내 경유점이며 2,344개 시설이 아니다. 좌표계 확인 전 기본 시설점에 섞지 않는다.

공개 다운로드의 실제 파일 식별자를 확인하고 포털의 내려받기 제한 점검을 거쳤다. 새 인증키·계정 조작·CAPTCHA 우회는 없었다. 원문별 URL·HTTP 상태·취득 UTC·본문 크기·SHA-256을 `raw/*.receipt.json`에 남겼다. 새 시설 원천 6개와 기존 NLIC 3개를 결속했고 AIP 좌표계 설명은 별도 보조 문서다.

## 선별·좌표·결측

경기도 CSV의 상태는 영업중 1,007·폐업 737·이관 11·휴업 2·미기재 1행이다. 영업중 중 유효 좌표가 있는 1,005행만 기본 표시 후보이며, 영업중 2행의 좌표 부족을 보존했다. 좌표가 있는 폐업 행도 원문 관측에는 남지만 기본 지도 후보에서 제외했다. 출처의 영업상태는 현재 현장 확인과 다르다.

항만구역은 원본 투영 좌표계에서 도형 내부 대표점을 구하고 WGS84 경위도로 변환했다. 70개 중 원본 도형 오류 7개와 대한민국 항만명 대조 미해결 자료를 보수적으로 제외해 55개의 **구역 대표점**을 얻었다. 해주·성진, 무명 구역과 이름 미대응 구역을 대한민국 기본 목록에 넣지 않는다. 울산·부산신항·광양/하동 등 일부 중요 구역도 원본 도형 오류 때문에 보류되어 있다. 임의 도형 수리나 위치 보정은 하지 않았다. 제외 15행의 이름·사유는 명세에 남겼다.

국내 항만 명부와 정확한 이름 또는 명시적 이름 변형을 대조했다. 부산 신항/부산항신항, 동해·묵호/동해묵호 등 변형은 기록하며 관측이나 시설 ID를 병합하지 않는다. 한 항만의 여러 구역을 여러 독립 항만으로 세지 않는다.

시설 관측 식별자는 출처·원본 지문·원문 행 식별자를 결속한다. 출처가 다른 시설의 세계 공통 고유 ID는 아직 부여하지 않았다. 주소·명칭의 유사성만으로 기존 게임 거점이나 통계 거점과 합치지 않는다. 원문 기준일이 없으면 `null`, 좌표가 없으면 `null`로 유지한다. 취득 시각·자료 발행일·시설별 관측일은 별도다.

## 저장과 검증

- [조회용 SQLite](../../../../artifacts/local/national-logistics-facility-locations-20261116/national-facility-reference.sqlite): 출처 9개·시설 관측 8,015행·별도 안내 참조 2,344행.
- [기본 표시 후보 GeoJSON](../../../../artifacts/local/national-logistics-facility-locations-20261116/map-candidates.wgs84.geojson): 1,075점, 경도→위도 순서. 점마다 관측 ID·출처·좌표 산출 방법·원문 상태 보존.
- [전체 항만구역 WGS84 도형](../../../../artifacts/local/national-logistics-facility-locations-20261116/port-areas.wgs84.geojson): 70행. 검토가 필요한 도형도 상태와 함께 보존.
- [관측 JSONL](../../../../artifacts/local/national-logistics-facility-locations-20261116/facility-observations.jsonl), [오프라인 변환 도구](../../../../artifacts/local/national-logistics-facility-locations-20261116/build_facility_reference.py), [독립 검증 도구](../../../../artifacts/local/national-logistics-facility-locations-20261116/verify_facility_reference.py).

독립 검증 **7개 묶음 통과**: 원문 행수·종류별 수량, 원본/산출물 SHA-256, 공항 좌표/기준일·인천 도분초 변환, 기본 후보의 좌표 순서/범위·상태/국내 대조, 55개 대표점의 원본 유효 도형 내부 포함, 보세 후보 결측/폐업 제외, SQLite 무결성/외래키·안내 참조 분리·0좌표 대체 없음.

기존 SQLite의 SHA-256은 전후 `d2963721cb8d5276041c9e86e8d89089f3dd6bc6d80e68153db28fa29f1466d9`로 같다. 새 로컬 참고본만 썼다. 공용 MySQL·기존 DB·수집 정책/예약·Unity 소스/팩/Scene·저장 규칙은 변경하지 않았다. 실제 Unity 연결·빌드·Game View·클릭·성능 검증은 수행하지 않았다. 내부 취득과 게임 동봉/재배포 검토는 별개이며 이번에 배포 승인 상태를 바꾸지 않았다.

## 남은 자료 보완

전국 창고·센터/단지/터미널과 보세 관련 시설의 좌표를 공식 원천으로 보완한다. 경기도 창고와 NLIC의 동일 시설 후보는 주소·원문 ID·등록 맥락을 대조한 뒤 연결한다. 항만 원본 오류·명칭 미대응은 별도 정정 근거가 생길 때만 보완한다. 대형 시설의 대표점·부지·출입구를 구분하며 시설 위치만으로 운송망·용량·물동량·게임 소유·실제 계약을 만들지 않는다. 이번 일회 수집을 새 정기 예약으로 확대하지 않았다.
