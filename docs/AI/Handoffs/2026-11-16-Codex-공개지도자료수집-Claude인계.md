# Codex → Claude · 신청 없는 지도 정밀화 자료 인계

**후속 정정:** 기존 NASADEM 정규화 DEM·셀 CSV·동별 지형 통계는 원본 격자 위상 결함으로 소비 보류다. 원본 타일·하천·건물·대장은 영향이 없다. [서울·전국 확장 인계](2026-11-16-Codex-서울전국디오라마자료확장-인계.md)의 `terrain-grid-phase-correction-20261116-r2` 보정 자료를 우선한다. 옛 파일과 영수증을 조용히 덮지 않는다.

2026-11-16 KST. 사용자 최신 정정에 따라 **도로 폭 적용과 6동 재굽기는 Claude 소유**다. Codex는 공개자료 수집·정규화·검증만 수행했다. 진행 중인 Unity·굽기 파일과 공용 DB·예약은 건드리지 않았다. 사용자 신청은 모두 마쳤다는 보고이며 승인·파일 수령 확인은 별개다.

## 먼저 읽을 결과

파일 탐색은 `artifacts/local/public-data/map-precision-intake-20261116-r1/intake-catalog.json`에서 시작한다. 네 자료 묶음의 영수증 지문과 독립 검증 결과를 가리킨다. 로컬 `artifacts/`는 이 PC의 자료이며 GitHub에 자동 공유되거나 개발 실행권을 부여하지 않는다.

수량·판본·좌표계·이용조건·한계는 [수집 결과 정본](../DataCollection/Research/2026-11-16-map-precision-public-intake.md)에 모았다. 기존 [공통 지도 계획](2026-11-16-Claude-개발계획-공통지도정밀화.md)과 [1회차 반환](2026-11-16-Claude-공통지도정밀화-1회차-반환.md)의 자료 보완이며, 새 기획 승인·작업 활성화·공개 배포 승인이 아니다. 실제 게임 적용·빌드·Runtime은 이번에 하지 않았다.

| 계획 묶음 | 읽을 위치 — Hongdal 루트 기준 | 사용할 때 구분할 것 |
| --- | --- | --- |
| W3 건물 기록 | `artifacts/local/public-data/map-precision-buildings-20261116-r1/normalized/building-title-2026-09-four-gu.sqlite` | 4구 91,462행, 원문 27열. 대장 PK와 지도 건물 ID는 다름 |
| W3 서울 전체 | 같은 폴더의 `building-title-2026-09-seoul-verified-columns.csv.gz` | 서울 584,691행. `verified-columns` 출력을 사용 |
| W3 필지 연결 검토 | `artifacts/local/public-data/map-precision-enrichment-20261116-r1/receipt.json`와 `building-candidates.csv.gz` | 65동 추가 높이 후보 1,000·현재 6동 161. 확정 건물 연결이나 적용 결과가 아님 |
| W2 경계 후보 | `artifacts/local/public-data/map-precision-boundary-20261116-r1/normalized/capital-region.geojson.gz`와 `capital-region-index.csv` | 수도권 1,187동 후보. 기존 65동과 동일하며 공식 최신 경계 미확인 |
| W5 지형 | `artifacts/local/public-data/map-precision-terrain-rights-20261116-r1/receipt/terrain-normalization.json`의 `normalizedOutputs` | EPSG:5186·30m·EGM96. 2000년 원관측, 정밀 지표면 자료 아님 |
| W5 하천 | 같은 묶음 `receipt/water-normalization.json`의 `normalizedOutputs` | 경도·위도 GeoJSON. 권역 14,274면/역사 30동 113면 |
| W1 건물 이용조건 | 같은 묶음 `receipt/rights-evidence.json` | 기존 원본과 재취득 ZIP 동일 SHA. 과거 영수증 유지, 배포 미승인 |
| 독립 검증 | `artifacts/local/public-data/map-precision-intake-20261116-r1/independent-verification.json` | 87파일 지문·10묶음 재조회. Unity 화면 검증과 별도 |

## 기존 작업에 합칠 때

1. 현재 도로 폭·6동 굽기 작업은 Claude의 기존 범위로 계속한다. Codex 수집 결과를 이유로 현재 굽기 입력을 한꺼번에 교체하지 않는다.
2. 높이 기록은 기존 GIS 도형과 같은 건물인지 검토한 후보만 읽는다. PNU는 필지이므로 같은 필지에 여러 건물·대장이 있는 경우 보류한다. 기존 유효 높이·층수와 원문을 보존한다.
3. `heightM` 양수 여부만으로 적용하지 않는다. 7,100m·10,655m 원문 2행은 `receipt/height-value-review.json`에서 보류했다. 값 존재와 값 정확도를 구분한다. 연결 검토에서 같은 필지의 단일 건물·단일 대장·주소/용도 일치를 확인한 후보 68,197개 중 추가 높이는 1,000개이며, 현재 6동에는 161개다. 상징 높이 자리의 개선 후보는 0개다. 두 자료의 높이 차이가 10m 이상인 33개와 실행판에서 r3 검토 집합 밖인 원본 행 2항목은 따로 읽는다.
4. 6동의 실제 실행판·역사 30동 마스크·r3의 후보 65동은 범위와 판본이 다르다. 하천/지형 30동 출력을 65동 완비 자료라고 표시하지 않는다. 권역 상자도 행정구역상 수도권 전체가 아니다.
5. 원본 좌표계는 건물 EPSG:5186·하천 EPSG:5179·NASADEM 경위도이며, GeoJSON은 경도→위도 순서다. 지형 높이 기준과 건물 높이를 합성할 때 수직 기준을 별도 유지한다.
6. 한 동을 비교할 때 도로-건물 침범·하천 위 건물·지형 높이 과장·출입구 단차를 관찰하고 적용 전후 화면과 입력 지문을 남긴다. 이 문서는 그 적용을 수행한 반환이 아니다.

## 보존한 경계와 후속

- 현재 공식 행정동·전자지도·주소 출입구·내비게이션 DB 수령은 미확인이다. 수령하면 원본·명세·기준월·CRS·ID와 기존 후보를 대조한다.
- 국토지리정보원 공개 DEM 실제 원본은 받지 않았다. NASADEM을 그 원본으로 바꾸어 부르지 않는다.
- 차선 간 폭은 실제 도로 외곽 폭보다 좁을 수 있다. 미대응 차선 약 84km는 검증된 골목 도로망이 아니다.
- 기존 음식점/주소/메뉴 연결과 기사 빛 표현은 유지한다. 새 대장 행으로 다른 매장·기사 상태를 만들지 않았다.
- 새 수집용 SQLite 외 공용 DB·정기 수집 정책·예약은 변경하지 않았다. 커밋·푸시도 하지 않았다.
- 기존 공간층 판본 보존·결측 명시 후보를 지지하며 새 반례·무효화·공통 승격·E 승격은 없다. **새 디오라마 규칙 후보 없음.**
