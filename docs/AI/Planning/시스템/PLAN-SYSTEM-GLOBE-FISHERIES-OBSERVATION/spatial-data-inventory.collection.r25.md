# 공간 계층 자료 재고 및 서울 경계 보충 r25

2026-09-23. `ExistingAssetsInspected / HistoricalSourcesCollectedAndRegistered / GeometryIntegrationPending`.

## 목적

VWorld 연동을 선행 조건으로 두지 않는다. 기존 자료를 먼저 확인하고 지구→한반도→수도권→서울→중랑구→동/역세권→상세 디오라마에 필요한 결손만 보충한다. 이번에는 Unity Scene을 변경하지 않았다.

## 현재 확인한 재고

| 계층 | 실제 확인한 자료 | 결손·제한 |
| --- | --- | --- |
| 지구 | Unity Resources의 `WorldGlobeCountryCatalog.json` | 국가 수준 자료이며 도시 정밀 지형을 대신하지 않음 |
| 한반도 | `KoreanPeninsulaTerrainPreviewManifest.json`, 지형/하천 PNG, `KoreanPeninsulaCorridorCatalog.json` | 지형 미리보기 405×540, Natural Earth 기반. 한강·압록강·두만강을 기록하고 대동강은 미확보. 정밀 DEM 아님 |
| 수도권 | 이번 좁은 재고에서 결속된 수도권 중간 지도 사본을 확인하지 못함 | 서울 외 경기도·인천 경계 및 중간 축척 지형 대조 필요. 저장소 전체 부재 판정 아님 |
| 서울·중랑구 | 이번 공식 자치구 경계 파일 25개 객체 확보 | 2023-10-31 역사적 참고. 좌표 변환·도형 위상·중랑구 코드 선택 미검증 |
| 동·역세권 | `northeast-seoul-rider.r2.json`: 행정동 30개·법정동 12개 범위, Graph/배치/객체 대장 존재 | 대장이 지정한 행정동 ZIP·서울 건물 ZIP·NodeLink ZIP이 현재 작업트리 경로에 없음. 행정동 원본만 같은 hash로 재확보. 38동 전부 준비됐다고 주장하지 않음 |
| 사가정 상세 | `SagajeongReference.json` r3: 도로 선분 2,397개·건물 602개, 높이·도로명·방향·배치 보조 파일 | OSM/참고 표현. 높이 추정·통행 비권위 경계 유지. 재다운로드하지 않음 |

현재 작업트리의 `artifacts/local/neighborhood-source-acquisition/`에는 Overture 건물 GeoJSON도 있다. 파일 존재 확인까지만 했으며 전체 범위·hash·권리·대장 결속을 이번에 검증한 것은 아니다.

## DB 확인

- 현재 `hongdal-mysql-1`은 Compose `hongdal/mysql`, 현재 저장소 경로, localhost 13306임을 확인했다.
- 컨테이너 설정은 기존 개발 의존성 전용 Compose와 달라 기존 `spatial-inventory`가 `ComposeMismatch`로 거절했다. Compose를 바꾸거나 DB를 재생성하지 않았다.
- 컨테이너의 기존 비-root 계정으로 접근 가능한 `hongdal_dev`를 확인하고 기존 명시적 로컬 연결 설정 경로를 메모리에서만 사용했다.
- 기존 수집기의 실제 `spatial-inventory` 실행에 성공했다. `region:kr:sig:11260` 정규화 조회 결과는 0행이었다. 이 조회는 다른 지역 ID·MongoDB·다른 DB까지 없다는 뜻이 아니다. MongoDB 공간 자료는 이번에 재조회하지 않았다.

## 실제 보충한 공식 원본

| 데이터 | 객체 수(DBF 헤더) | 파일 크기 | SHA-256 |
| --- | ---: | ---: | --- |
| [OA-22160 행정동](https://data.seoul.go.kr/dataList/OA-22160/S/1/datasetView.do) | 425 | 1,676,539 bytes | `969f7033bd3609a5fd586790f5b2cfedc638d7647ef45c78f9c75e1dabf79f68` |
| [OA-22161 자치구](https://data.seoul.go.kr/dataList/OA-22161/S/1/datasetView.do) | 25 | 488,551 bytes | `abd34d82897f0c2191fe4095df55a4aacfe8fce815fa4dacf730cc34c6dd962c` |

두 공식 페이지는 EPSG:5181, 공공누리 제1유형, 월간 갱신을 안내한다. 그러나 실제 파일 수정일은 2023-10-31이다. 페이지 갱신일 2026-09-11을 원본 기준일로 사용하지 않는다. 행정동 파일 hash는 기존 r2 대장과 일치하므로 신규 최신 자료가 아닌 누락 원본 재확보다. 과거 대장의 경로나 hash를 덮어쓰지 않았다.

- 비공개 저장 경로: `artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1/`.
- ZIP 2개, 공식 페이지 HTML 2개, `receipt.json` 보존. HTTP 500은 명시적 연구 도구 User-Agent를 넣은 후 해소됐으며 자동 재시도는 없다.
- 새 CLI `seoul-boundaries-acquire/apply/verify`는 최대 응답 8MiB·45초, 리디렉션 없음, 기존 완성 폴더 재수집 거부, ZIP/DBF 기본 구조와 hash를 확인한다.
- 기존 `평창군공공공간원본등록Service`를 재사용해 `PublicDataIngestionDbContext.RawSnapshots` 및 `IngestionRuns`에 신규 2건 등록했다. 원본 바이너리는 비공개 파일에 두고 DB에는 hash·경로·판본·기준일과 검토보류 제한을 저장한다.
- 두 번째 반입 신규 0건, 별도 verify 프로세스에서 원본 기록 2건 재조회. 정규화 도형 행은 아직 0건이다. MySQL에 450개 도형을 저장했다고 표현하지 않는다.
- 수집 CLI 빌드 경고 0·오류 0. 도형 위상·코드·정밀도 시험, Unity Play/Game View 검증은 미실행이다.

## 다음 우선순위

1. **확보한 서울 25구 경계를 파싱·검증**: EPSG:5181→WGS84 변환, 자치구 코드·중랑구 선택, 도형 유효성·범위 검사. 역사적 관찰 사본으로만 만든다.
2. **자료 연결 복구**: 행정동 재확보 사본을 기존 hash 참조와 연결하고, 건물/NodeLink 원본은 다른 승인 보관소의 존재 확인 후 필요한 범위만 복구한다. 먼저 재다운로드하지 않는다.
3. **현재 행정동 정본 확보**: 기존 r16의 `TL_SCCO_GEMD` 수집 관문은 여전히 미완료다. 이번 2023년 파일로 대체하거나 완료 처리하지 않는다.
4. **서울 중간 축척 표현에 필요한 지형·한강·간선망**: 기존 원천에 포함되는지 먼저 대조하고, 정밀도에 맞는 원본만 소규모 보충한다. 수도권은 그다음이다.
5. **Unity 연결**: 준비된 사본만 같은 위경도 중심·축척으로 연결. 최신 경계나 실제 운행 가능 도로로 승격하지 않는다.

## 증거 규칙 점검

기존 규칙 중 원본 hash·판본 보존, 역사적 경계와 최신 경계 분리, 수집 완료와 Unity 적용 분리 원칙을 지지한다. 기존 규칙 무효화 없음. 새 디오라마 규칙 후보 없음. 공통 대장/E 단계 승격, Scene 저장, commit/push 없음.
