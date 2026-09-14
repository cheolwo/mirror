# 사가정 필지 경계 도형 수집 준비 구현 r7

- 기준 기획: `PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION` r7
- 사용자 승인: 2026-09-14 실제 필지 경계 도형부터 수집
- 현재 결과: `ExtractionPipelineValidated / VWorldArchiveBlockedExternalAccess / ParcelGeometryNotCollected`
- 공개·Unity·배달·가격·업체·통행·게임 권위: 모두 `false`

## 공식 원천 확인

국토교통부·브이월드의 `연속지적도형정보`를 정본 후보로 정했다.

| 항목 | 확인 결과 |
| --- | --- |
| 브이월드 자료 | dataset 23, `연속지적도형정보` |
| 서울 전체자료 | SHP 97MB, 기준일 2026-09-08, 갱신일 2026-09-09 |
| 좌표계 | EPSG:5186(GRS80) |
| 레이어·PNU 필드 | `AL_D002`, `A1` 19자리 필지고유번호 |
| 성격 | 측량·법적 경계가 아닌 참고용 도면 |
| 권리 표시 | 브이월드 화면 CC BY, 공공데이터포털 metadata는 이용허락범위 제한 없음. 소비 전 표시 차이는 사람 검토 |
| 원본 확보 | 로그인 필수. 사용자가 현재 로그인할 수 없어 `BlockedExternalAccess` |

공개 컬럼 정의서 `국가중점데이터_컬럼정의서(26.01.02)_배포용.xlsx`는 비공개 원본 폴더에 수집했다. 길이는 224,115바이트이고 SHA-256은 `46DD29C6AB681C1E34CF00D91F8F2FE68B7E1868A853315EAA292838238ECB0F`다. 원본 SHP를 대신하는 도형 증거로 사용하지 않는다.

## 구현한 파이프라인

- `build-sagajeong-parcel-geometry-evidence.py`
  - 기존 주소 원장의 파일·내용 hash와 화면 건물 4,062개·고유 PNU 3,774개를 먼저 검증한다.
  - 공식 ZIP의 CRC와 `AL_D002_11` SHP·SHX·DBF·PRJ 구성, EPSG:5186, A1 PNU를 검사한다.
  - 면목동 PNU 범위를 읽고 정확한 3,774개 대상만 선별한다.
  - 동일 PNU의 여러 feature는 출처 개수를 보존한 뒤 union하고 Polygon/MultiPolygon·비어 있지 않음·유효성을 검사한다.
  - PNU별 geometry hash·면적·bbox와 누락·무효·multipart·중복 feature 분포를 만든다.
  - 원본 좌표계의 결정적 GeoJSON과 manifest를 `artifacts/local` 아래에만 쓴다.
- `manage-sagajeong-parcel-geometry-evidence.ps1`
  - `Readiness / Build / Validate / Summary / SelfTest`를 제공한다.
  - 원본 ZIP과 출력 경로를 `artifacts/local` 아래로 제한한다.
  - `Build`에는 사람이 확인한 원본 SHA-256과 판본을 반드시 요구한다.
- `sagajeong-parcel-geometry-evidence.ps1`
  - 실제 주소 원장에서 4,062/3,774 target을 다시 계산한다.
  - 원본이 없으면 도형 0·`NotCollected/Missing`·`applicationAuthorized=false`인지 검사한다.
  - 임시 AL_D002 SHP fixture를 실제 작성·ZIP 재읽기해 field·CRS·중복 union·누락·결정성을 검증한다.

## 현재 실행 결과

```text
Status=BlockedExternalAccess
BlockerCode=VWorldLoginRequiredAndNoOfficialArchiveAvailable
EarliestResumePoint=ProvideOfficialSeoulAlD002Archive
PresentationBuilding=4062
TargetParcel=3774
GeometryParcel=0
CollectionState=NotCollected
CoverageState=Missing
ApplicationAuthorized=false
```

전용 회귀 시험 `sagajeong-parcel-geometry-evidence.ps1`과 갱신된 증거 대장 시험 `station-diorama-evidence-rules.ps1`은 모두 통과했다. 관리 도구 자체 시험도 `SelfTestPassed / tests=10`이며 증거 대장 요약은 `Sources=10 / Rules=12 / Candidates=12 / Provisional=0 / Accepted=0 / SpatialChecks=9 / Collected=2 / Missing=1 / Unassessed=6`이다. 범위 지정 Fast는 `git diff --check`를 통과했고 기록은 `artifacts/local/validation/20260914-191042`다. 대상이 문서·도구·시험뿐이라 build와 제품 시험은 생략됐다.

실제 원본 도형이 없으므로 `Build`, 로컬 DB·MongoDB 저장, 독립 도형 재조회, Unity 투영은 실행하지 않았다. metadata나 synthetic self-test 결과를 실제 수집으로 승격하지 않는다.

## 가장 이른 재개점

브이월드에서 서울 `AL_D002` 전체 ZIP을 내려받아 다음 비공개 경로에 둔다.

```text
artifacts/local/public-data/sagajeong-parcel-geometry-20260914-r1/raw/<official-file>.zip
```

그 뒤 파일의 실제 SHA-256과 화면 기준일을 명시해 `Build`를 실행한다. coverage 결과를 확인한 후에만 파생 도형 원장 저장·독립 재조회 절편을 연다. 원본이 없거나 hash·CRS·PNU 계약이 다르면 멈추며 건물 도형, OSM 필지 추정 또는 synthetic 형상을 대신 사용하지 않는다.

## 증거 규칙 판정

- 기존 `missing-coverage-remains-explicit` 후보를 지지한다.
- 수집 도구 준비는 `ParcelGeometry` 수집 완료나 E 단계 상승의 근거가 아니다.
- 새 디오라마 규칙 후보 없음.

commit·push는 수행하지 않았다.
