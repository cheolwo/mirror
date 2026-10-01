# 30개 행정동 등고선·표고 원본 감사와 지형판 생성 계약

[구현·감사 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r28]

- 상태: `OfficialRawAcquiredAndVerified / ThirtyAdministrativeAreaCoverageAudited / TerrainGenerationContractProposed / TerrainSurfaceNotGenerated / UnityNotApplied / CurrentPublicationBlocked`
- 후속: [지형 공통 격자 비공개 검토 r29](thirty-admin-dong-terrain-private-generation.implementation.r29.md)에서 2025 ZIP 단독 sparse global lattice와 seam 검증을 새 revision으로 구현했다. 수직 datum·단위 미확인과 `precisionTerrainReady=false`는 유지한다.
- 기준: [30개 행정동 역사 보행망 비공개 검토 세대 r26](thirty-admin-dong-walk-network-private-review.implementation.r26.md)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 결론

서울 열린데이터광장 [`OA-22241`](https://data.seoul.go.kr/dataList/OA-22241/F/1/datasetView.do)의 현재 페이지와 두 개의 공식 다운로드 ZIP을 2026-09-26에 익명 POST로 받아 길이·SHA-256·압축 구성을 검증했다. 원본과 영수증은 Git 추적 밖 `artifacts/local/public-data/admin-dong-terrain-contour-20260926-r1/raw/`에만 보존한다.

현재 파일 중 `서울시 등고선.zip` 2025-03-20 판은 등고선과 표고점을 모두 포함하고 30개 대상 행정동 모두에서 후보가 확인됐다. `서울시 경사도.zip` 2023-12-26 판도 실제 압축 내부는 등고선과 표고점 SHP이지만 범위·레코드 수·hash가 다른 별도 판본이다. 두 ZIP을 하나의 세대로 섞지 않고, 첫 지형 후보는 2025-03-20 `서울시 등고선.zip` 단일 파일만 소비하는 새 revision으로 만든다.

이번 절편은 공식 원본 확보·구조 감사·30개 동 임시 coverage 대조까지다. 보간 표면, 높이 Mesh, 건물·도로의 Y 배치, DB·Mongo 저장, current pointer, 게시, 공개, Unity 적용은 시도하지 않았다.

## 공식 페이지와 원본 판본

`OA-22241`의 공식 제목은 `서울시 경사도`지만, 설명은 경사도 SHP를 직접 제공하지 않고 경사도를 추출할 수 있는 표고점·등고선 SHP를 제공한다고 명시한다. 기준 원천은 국토지리정보원이 고시하는 **2023년 기준 수치지형도**다. 포털의 공개일은 2023-12-26, 데이터 갱신일은 2025-03-20, 메타정보 수정일은 2026-02-11, 갱신 주기는 `주기없음`이다. 이용 조건은 공공누리 제1유형 출처표시이다.

| 페이지 표시 판본 | 로컬 파일 | 길이 | SHA-256 | 내부 구성 |
| --- | --- | ---: | --- | --- |
| `서울시 등고선.zip`, 2025-03-20, download sequence `2` | `OA-22241-seoul-contour.zip` | 45,852,601 bytes | `4FBE3C7E061B5974E7403EC116855304ED8AE321EEBCC0D12C31CA8FB7BE30BF` | Polyline 등고선 `N3L_F001` 8,570건, Point 표고점 `N3P_F002` 45,870건 |
| `서울시 경사도.zip`, 2023-12-26, download sequence `1` | `OA-22241-seoul-slope.zip` | 103,960,741 bytes | `0243111EFC7BC30A03B992E92C419C9A9E891BC7DF3094FFE0ABF85DAE24F44C` | Polyline 등고선 13,704건, Point 표고점 76,580건; 새 판본과 혼합 금지 |

공식 페이지 HTML은 117,826 bytes, SHA-256 `134604C17CB801FD493F63C8764B5C4EAA6B25C1482682A5817104E5CDECCB73`로 함께 보존했다. `receipt.json`은 5,211 bytes, SHA-256 `8911948581B53B07BF59BDE353EA2743948A9733B6EA24F341FB4364496A345D`다. 영수증과 두 ZIP을 다시 열어 길이·hash·SHP·PRJ 존재·권한 false를 검사한 결과는 `PASS`다.

## 좌표계·높이 증거 경계

두 ZIP의 PRJ와 SHP XML은 `Korean_1985_Modified_Korea_Central_Belt`, `LatestWKID 5174`, `EPSG 5174`, 수평 단위 metre를 같게 선언한다. 따라서 수평 좌표계는 `EPSG:5174`로 고정한다. 2025 판의 등고선 `CONT / HEIGHT`, 표고점 `NUME / HEIGHT`는 전체 레코드에서 서로 일치했고, 비유한 높이·중복 UFID·중복 표고점 좌표는 각각 0건이었다.

그러나 포털 메타정보, PRJ, SHP XML, 동봉 자료 설명은 **수직 기준면(vertical datum)과 높이 필드의 물리 단위를 명시하지 않는다.** 수평 CRS의 metre를 `HEIGHT`의 단위로 자동 전용하지 않는다. 수직 기준면과 단위를 공식 스키마나 추가 원천으로 확인하기 전에는 `SourceNumericHeight / VerticalDatumUnverified / HeightUnitNotSourceDeclared`로만 표시한다. `PhysicalElevation`, DEM 정본, 절대 표고, 배치·통행 권위로 표현하지 않는다.

## 30개 행정동 임시 coverage 감사

2025 판 하나를 `OA-22160` 2023-10-31 역사 행정동 경계에 읽기 전용으로 임시 대조했다. 이 경계는 현행 정본이 아니므로 coverage 확인은 생성 가능성만 지지한다.

- 대상: 정확히 30개 역사 행정동
- 30/30개 동에 표고점 존재
- 30/30개 동에 등고선 교차 존재
- 단일 경계 귀속 표고점: 2,424개
- 30개 동 합집합과 교차하는 고유 원천 등고선: 377개
- 동별 등고선 교차 횟수: 860회, 합계 clip 길이 922,054m
- 관찰된 대상 범위 등고 수치: 10~345

수치 범위는 원천이 말하는 절대 표고 단위를 확정한 것이 아니다. 경계 밖으로 잘린 등고선을 폐곡선으로 간주하거나, 경계 끝에서 높이를 평탄화해 결손을 숨기지 않는다.

## 기존 파이프라인 감사

Hongdal의 현재 행정동 디오라마 입력과 `AdministrativeDongDioramaTile`은 경계·건물·도로만 갖고 지형 표면·표고 격자·mask 필드가 없다. `eng/neighborhood` 안에는 `OA-22241` 압축을 검증·변환·보간하는 생성기도 없다.

`eng/public-spatial/build-daegwallyeong-l2-artifacts.py`는 500m tile, 60m halo, 10m 표본 간격, little-endian `height-f32-v1`이라는 후단 형식을 이미 제공한다. 다만 입력을 `EPSG:5186` raster DEM으로 강제하고 대관령 판본에 결속되어 있으므로 `EPSG:5174` 등고선·표고점에 직접 쓸 수 없다. 이천 지형 검토도 Copernicus DSM raster의 지역 미리보기로, 행정동 등고선 보간 계약이 아니다.

별도 Unity 저장소의 범용 표고 Mesh Builder는 tile별 최저 높이를 각각 0으로 빼고 중앙 500m를 Mesh로 만든다. 이 방식을 297개 행정동 tile에 그대로 쓰면 인접 tile이나 동 경계마다 수직 기준이 달라져 단차가 난다. r28은 이 Builder나 Unity 코드를 수정하지 않았다.

## 다음 구현 계약

첫 지형 표면은 `northeast-seoul-admin-dong-terrain-private-review.r1` 새 후보 판본으로 만든다. 기존 r2 건물·도로 세대나 DB/Mongo current에 원본을 추가하지 않는다.

1. 2025-03-20 ZIP의 길이·hash·entry 집합·record 수·`EPSG:5174`·필드 일치를 fail-closed로 검증한다. 2023 ZIP은 비교 영수증으로만 보존하고 입력에 섞지 않는다.
2. 등고선을 높이 breakline, 표고점을 point constraint로 두되 보간 알고리즘·버전·수치 정밀도를 생성기 hash에 고정한다. 경계별로 따로 보간하지 않고, 공통 ENU의 30개 동 scope와 외곽 halo 전체를 **한 번만** 보간한다.
3. 전체 격자는 기존 사가정 원점 ENU와 500m tile lattice를 그대로 쓴다. 첫 후보는 tile 500m, halo 60m, 표본 간격 10m, 63×63 `height-f32-v1` 형식을 대상으로 한다. 이 수치는 생성기·검증기 구현 시 성능·오차 대조를 거쳐 새 revision에 고정한다.
4. 표고 격자는 동·tile별로 다시 계산하지 않고 scope-global 격자를 잘라서 낸다. 인접 core 경계와 중복 halo의 같은 표본 좌표는 IEEE-754 little-endian 바이트가 정확히 같은 `sharedEdgeBitEquality=true`를 통과해야 한다.
5. 수직 원점은 tile 최저값이 아니라 생성 세대 하나의 `commonVerticalReferenceSourceValue`를 쓴다. 이 값과 높이·과장 배율은 모든 동·tile·건물 바닥에 같이 적용한다. 수직 datum이 미확인이므로 절대 표고 이름을 붙이지 않는다.
6. 보간 결과에는 원천에서 떼어둔 표고점의 residual 분포, 결손·외삽 수, 30/30 동 coverage, 최소·최대·기울기 분포, 인접 tile bit equality를 기록한다. 수용 기준은 미리 문서화하고 미달 표본을 평지로 채우지 않는다.
7. 행정동 경계는 표면을 새로 보간하는 입력이 아니라 같은 scope-global 표면의 표시 mask로만 쓴다. 인접 동이 각자 다른 표면을 갖지 않게 한다.
8. 첫 출력은 Git 제외 private review bundle로만 내고 `publicDisplayAllowed=false`, `runtimeAuthorized=false`, `colliderAllowed=false`, `traversalReady=false`, `gameplayReady=false`, `distributionApproved=false`를 검증한다. 별도 Unity 저장소는 해당 bundle의 hash를 재검증하는 읽기 전용 표현만 한다.

## 권한·저장 경계

| 항목 | r28 실제 값 | 판정 |
| --- | --- | --- |
| 로컬 공식 원본·영수증 | 작성·hash 재검증 완료 | `PrivateReviewOnly` |
| DB write | `false` | 시도 안 함 |
| Mongo write/readback | `false` | 시도 안 함 |
| current pointer read/update | `false / false` | 현재 세대를 사용하거나 바꾸지 않음 |
| public API·게시·배포 | `false` | 시도 안 함 |
| 표면·Mesh 생성 | `false` | 다음 revision |
| Unity 적용·Play Mode·Game View | `false` | r24~r26 화면을 지형 증거로 재해석하지 않음 |
| 통행·게임플레이·운영 권위 | `false` | 생성하지 않음 |

이번 원본 확보로 서울 30개 동의 등고선·표고점 **접근 차단**은 해소됐다. 해소되지 않은 것은 수직 기준면·높이 단위 공식 검증, 보간 알고리즘과 오차 관문, 공통 수직 원점을 쓰는 Unity 표현이다. 이 세 가지가 닫히기 전에는 “정밀 지형이 Unity에 적용됐다”고 보고하지 않는다.
