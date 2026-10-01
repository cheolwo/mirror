# 30개 행정동 지형 공통 격자 비공개 검토 세대

[구현 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r29]

- 상태: `Implemented / 2025ContourRevisionOnly / SparseGlobalLatticeGeneratedAndVerified / StructuralPrivateReviewReady / PrecisionTerrainNotReady / UnityNotApplied`
- 기준: [등고선·표고 원본 감사 r28](thirty-admin-dong-terrain-contour-audit.implementation.r28.md)
- 공식 자료: [서울 열린데이터광장 OA-22241](https://data.seoul.go.kr/dataList/OA-22241/F/1/datasetView.do)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 구현 결론

`OA-22241`의 2025-03-20 `서울시 등고선.zip` 하나만 사용해 30개 행정동을 함께 보간하는 로컬 비공개 지형 검토 세대를 만들었다. 동마다 별도 기준점과 표면을 만들지 않고 기존 공통 ENU 500m tile lattice에 60m halo와 10m 간격을 적용했다. 모든 타일은 한 번 계산한 공통 격자 값을 slice하므로 경계와 겹치는 halo의 float32 bit가 같다.

수직 datum과 높이 단위는 원천에서 확인되지 않았다. 따라서 출력값은 `SourceNumericHeightOnly`이며 공통 원천 수치 `1.94`를 뺀 상대값이다. 구조·재현성 검토 관문은 통과했지만 원천으로부터 500m를 넘는 표본 47개와 수직 의미 미확인 때문에 `precisionTerrainReady=false`다. 이 세대를 실제 고도, 경사, 통행, Unity 지형 Mesh 또는 gameplay 권위로 사용하지 않는다.

## 원본과 판본 차단

- 사용 ZIP: `OA-22241-seoul-contour.zip`
- 길이: `45,852,601 bytes`
- SHA-256: `4FBE3C7E061B5974E7403EC116855304ED8AE321EEBCC0D12C31CA8FB7BE30BF`
- 원본 receipt SHA-256: `8911948581B53B07BF59BDE353EA2743948A9733B6EA24F341FB4364496A345D`
- 수평 CRS: `EPSG:5174`, horizontal metre
- 전체 원천: 등고선 8,570건, 표고점 45,870건
- 별도 2023-12-26 `서울시 경사도.zip`: 읽기·혼합·생성 hash 결속 없음

생성기는 2025 ZIP 이름·hash, SHP/DBF 건수, EPSG:5174, 정확한 30개 동 coverage를 fail-closed로 검증한다. 완성 세대의 JSON 전체에서 2023 ZIP 이름과 SHA-256을 다시 검색해 검출 0건을 확인했다.

## 생성기와 불변 세대

- generator: `eng/neighborhood/administrative_dong_terrain_private_review_r1.py`
- generator revision: `administrative-dong-terrain-private-review-generator.r1`
- generator SHA-256: `2334A6EA10A2A713B5548FB73336009101407F3D9D544932BB8C24B564B0E75E`
- generation: `D0AAE02B23307D4CA719853540947219E7A97D5CC27B3AAFFAB1E4C8C6EB131F`
- generation 경로: `artifacts/local/validation/admin-dong-terrain-private-review/r1/input/generations/d0aae02b23307d4ca719853540947219e7a97d5cc27b3aaffab1e4c8c6eb131f/`
- 파일: 450개, `8,609,465 bytes`
- completion이 고정한 파일: 449개이며 `complete.json` 자신은 의도적으로 제외
- 전체 completion 길이·SHA-256 독립 재대조: 불일치 0

산출물은 `manifest.json`, `audit.json`, `complete.json`, 전역 key·height binary 2개, 물리 tile height 148개, 행정동별 tile mask 297개다. Git 제외 로컬 generation이며 DB·Mongo·current pointer에 적용하지 않는다.

## 공통 격자와 seam

| 항목 | 결과 |
| --- | ---: |
| 행정동 | 30 / 30 |
| 행정동–tile 관계 | 297 |
| 고유 물리 tile | 148 |
| tile 본체 | 500m × 500m |
| halo | 사방 60m |
| 간격 | 10m |
| tile 표본 | 63 × 63 |
| 사각 envelope | 763 × 813, 620,319 key |
| 실제 tile+halo union | 391,619 key |
| 미사용 envelope | 228,700 key, 미보간·미출력 |
| 공유 key | 153,543 |
| 공유 key 비교 | 195,793 |
| bit 불일치 | 0 |
| 필수 표본 NaN/무한값 | 0 |
| 빈 행정동 mask | 0 |

첫 구현은 사각 envelope의 실제 tile union 밖 모서리까지 보간하려다 `InterpolationSearchRadiusExceeded:1624.713990:x3000:z-410`으로 중단됐다. 검색 반경을 임의로 늘리지 않고, 148개 tile의 halo가 요구하는 key union만 한 번 보간하도록 바꿨다. 사용하지 않는 228,700개 key는 `NaN` 상태로 내부 배열에만 남고 파일로 내보내지 않는다. 이 실패와 수정은 자료가 없는 외곽을 평지나 원거리 추정으로 채우지 않는 fail-closed 경계다.

## 보간과 오차 감사

- 알고리즘: 공통 ENU에서 elevation point와 40m 간격 contour constraint를 함께 쓰는 결정적 IDW
- 선택 표고점 제약: 9,191개
- 선택 등고선: 1,132건
- 파생 contour constraint: 66,240개
- 최근접 12개 제약, power 2
- 평지 fallback: 0
- 필수 표본 최대 최근접 원천 거리: `519.509899m`
- 500m 초과 표본: 47개
- 1,500m review maximum 초과: 0개

| 검사 | 수량 | P95 | 최대 |
| --- | ---: | ---: | ---: |
| 표고점 grid fit, 원천 수치 | 2,835 | 1.614154 | 13.770972 |
| contour grid fit, 원천 수치 | 24,232 | 2.444520 | 16.898818 |
| 결정적 10% 표고점 holdout, 원천 수치 | 308 | 5.642132 | 14.465763 |

이 수치는 높이 단위나 절대 고도 정확도를 입증하지 않는다. in-sample residual은 독립 정확도 증거가 아니며 holdout도 주변 contour constraint를 사용한다. 수문·경사·breakline conditioning을 하지 않았고 contour 사이를 정확히 따르는 TIN도 아니다.

## 권위와 적용 경계

| 항목 | 현재 값 |
| --- | --- |
| 비공개 검토 | `privateReviewOnly=true`, `privateTerrainReviewReady=true` |
| 정밀 지형 준비 | `precisionTerrainReady=false` |
| 수직 의미 | `heightUnitSourceDeclared=false`, `verticalDatumVerified=false` |
| 절대 고도 | `absoluteElevationAuthorized=false` |
| 공개·배포 | `publicDisplayAllowed=false`, `distributionApproved=false` |
| DB·Mongo | 쓰기 시도 모두 `false` |
| current pointer | 읽기·사용·갱신 모두 `false` |
| Unity·Mesh·Collider | 적용 시도 `false`, collider 허용 `false` |
| Runtime·이동·게임 | 모두 `false` |

## 검증과 다음 관문

- Python 문법 검사: `PASS`
- self-test: `7 / 7 PASS`
- 첫 build와 독립 `verify`: `PASS`
- 같은 입력 재실행: `changedFiles=0`
- independent completion file hash 검사: 449 / 449 일치
- 기존 파일 수정, commit, push: 없음

다음 단계는 수직 datum·단위를 공식 메타데이터나 생산기관 자료로 확인하고, 500m 초과 47개 표본을 더 가까운 공식 제약으로 보완한 새 revision을 만드는 일이다. 그 전에는 이 값을 Unity surface나 collider로 만들지 않는다. 현행 JUSO 경계와 같은 세대 공간 자료를 확보하면 역사 mask도 새 revision으로 교체한다. **새 디오라마 규칙 후보 없음**.
