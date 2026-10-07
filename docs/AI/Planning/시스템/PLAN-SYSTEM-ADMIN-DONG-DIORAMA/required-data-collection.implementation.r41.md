# 정밀 공간 구현에 필요한 자료 수집 우선

[기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r41]

- 사용자 방향: 2026-10-02 “디오라마가 문제가 아니라 필요한 데이터들을 계속 수집할 필요가 있다.”
- 상태: `OfficialSourcesAcquired / LocalMySqlSourceLineageStoredAndRequeried / PendingHumanReview`.
- 후속: [추가 수집·PK 설명 정정 r42](required-data-collection.implementation.r42.md). 아래21자리 PK 설명은 실제14/22자리 원문으로 정정하며 r1 원문/동결 receipt/DB 계보는 보존한다.
- 선행: [소수 구역 집중 r39](focused-area-refinement.decision.r39.md), [단일 건물 보완 r40](focused-building-ring-refinement.implementation.r40.md), [지형 원천 근거 감사 r36](thirty-admin-dong-terrain-official-standard-evidence.audit.r36.md).

## 현재 우선순위

기존 행정동 자료와 완성된 형상 보완은 보존한다. 다음 작업은 자료 결손을 찾고 실제 원본을 확보해 출처·판본·취득 시각·좌표계·단위·권리를 결속하는 일이다. 자료를 받은 횟수와 모델의 정밀화 완료율을 같은 지표로 사용하지 않는다. 30개 동의 추가 렌더 확장 대신 사가정 주변 첫 대상에서 필요한 자료와 실제 동일성을 확인한다.

| 순서 | 필요한 자료 | 현재 확보와 결손 | 다음 수집·대조 단위 |
|---|---|---|---|
| 1 | 건물 높이·지상/지하층·건물 동일성 | 같은 필지의 실제 건축물대장 표제부 8건 확보. 특정 footprint와 어느 별동이 같은지는 미확정 | 대장 PK 원문, 동명, 주소, 대지/건축면적과 공식 도형을 대조. 일대일 동일성 근거가 있는 건물만 결속 |
| 2 | 실제 도로 경계·보도·연석·교차로·출입구 | 도로노선 범주형 폭과 계획 도로면 확보. 현황 실폭과 보도/연석 도형은 미확보 | 공개 현황 GIS 원본의 제공/반출 조건 확인. 기존 영상의 실제 위치·방향·모델 결속 대조와 분리 |
| 3 | 지형 고도 원본과 수직 기준 | DEM 도엽 성과목록 확보. 실제 DEM raster·집중 구간의 정확한 도엽 교차는 미확보 | 좌표계 코드/원점·원시 제작년도·격자·높이 기준을 해석하고 집중 구간 도엽을 특정한 뒤 원본 확보 |
| 4 | 현재 행정동 경계와 자료별 시점 | 기존 2023 역사 경계를 보존. 최신 포털 갱신일만으로 도형의 관측일을 확정하지 않음 | 실제 변경 이력과 판본별 경계를 확보하고 과거 자료와 별도 비교 |

기호 높이 4m, 층수에 일정 높이를 곱한 값, 도로명 단서, 범주형 도로폭, 계획 도로면, 인근 영상은 실측 또는 같은 건물의 확인 근거를 대체하지 않는다. 필요한 데이터는 공간 원본·측정 수치·원본 해석을 위한 명세를 함께 수집한다.

## 이번 실제 수집: 20261002-r1

원본/추출 payload **7개·22,425,083바이트**를 확보했다. 다음 표의 서로 다른 레코드 종류를 합쳐 하나의 “공간 데이터 건수”로 표시하지 않는다.

| 확보 자료 | 공식 출처 | 실제 규모 | 판본·기준·한계 |
|---|---|---|---|
| 건축HUB 표제부 실제 응답 | [국토교통부 건축HUB 건축물대장](https://www.data.go.kr/data/15134735/openapi.do) | 12,752바이트, 표제부 8건 | 면목동 77 한 필지만 1회 조회. 높이 양수 8건(3.9~22.6m), 지상층수 양수 8건. 집중 건물의 특정 별동 동일성은 미확정 |
| 건물 필드 정의서 | [VWorld GIS 건물 통합정보](https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=NA&dsId=18) | XLSX 224,314바이트 | 2026-08-19 공식 공개 정의서. 개별 건물 높이 원본 아님 |
| 건축HUB 명세 | [공식 API 페이지](https://www.data.go.kr/data/15134735/openapi.do) | Swagger JSON 222,938바이트, 10 operation | 공식 HTML에서 추출. HTML 취득과 추출 시각을 분리. `heit` 높이(m), 층수와 대장 PK 계약 확인 |
| 건축HUB 활용가이드 | [동일 공식 API 공개 첨부](https://www.data.go.kr/data/15134735/openapi.do) | ZIP 883,973바이트 | HWP·기존 PK 전환 규칙 PDF·시군구 통합 코드 PDF. 개별 공간 도형 아님 |
| 서울시 도로노선 | [OA-22650](https://data.seoul.go.kr/dataList/OA-22650/S/1/datasetView.do) | CP949 CSV 850,613바이트, 14,073행 | 라이브 페이지 갱신 2026-10-02. 노선별 도로폭 범주이며 특정 지점 차도·보도 실측폭 아님. 조사일 미확인 |
| 도시계획시설 도로 면 | [OA-21134](https://data.seoul.go.kr/dataList/OA-21134/S/1/datasetView.do) | ZIP 14,479,305바이트, polygon 17,172개 | 2026-09판, 실제 PRJ와 포털 `EPSG:5174` 일치. invalid geometry 99개. 현재 차도/보도/연석 도형으로 적용하지 않음 |
| 국토지리정보원 DEM 성과목록 | [공식 DEM 메타데이터 15067637](https://www.data.go.kr/data/15067637/fileData.do) | CP949 CSV 5,751,188바이트, 23,190행·39필드 | 2023-11-07 발행판, 행별 원시 제작년도는 별도. 격자 간격·원점·좌표계·표고 기준 기록. 실제 DEM 파일 아님 |

취득 시각은 2026-10-02 UTC이며 실제 원래 receipt 값과 원문 SHA를 보존했다. 건축HUB의 `crtnDay` 값 20220813/20250804/20251027은 대장 생성일이며 측량일이 아니다. 21자리 숫자로 제공된 일부 `mgmBldrgstPk`는 부동소수점 변환 없이 원본 바이트/숫자 토큰을 보존했다.

DEM 목록의 서울/성동 이름 단서 559행은 탐색 후보일 뿐 집중 구간 교차 확인이 아니다. 일부 행에 인천항 평균해수면·정표고·1m 격자가 기록돼 있어도 해당 raster 취득, 코드의 정확한 CRS 해석, 기존 서울 등고선 ZIP의 수직 metadata 확인을 대신하지 않는다. [실제 DEM 제공 안내](https://www.data.go.kr/catalog/15059920/fileData.json)는 별도로 보존했으며 실제 DEM raster는 받지 않았다.

계획 도로 후보는 기존 사가정역 기준 250m 창과 현재 집중 건물 중심 동결 창을 구분한다. 전자는 8개(1 invalid), 후자는 6개(모두 valid)다. 창이 다르므로 같은 수치로 합치지 않고 각 후보의 원천 ID·기하·좌표 변환 근거를 별도 로컬 audit에 남겼다.

## 기존 자료의 재사용과 아직 받지 못한 자료

- 기존 Overture 파일 2,969개를 읽기 전용 재감사했다. 높이 양수 44개·층수 양수 49개이며 둘 다 있는 건물은 44개다. 새로운 수집 건수에 포함하지 않았고 최근접 위치만으로 AL_D010 건물과 결속하지 않았다.
- VWorld 공개 정의서 취득으로 기존 AL_D010 `RightsConflictUnresolved`를 해소하지 않는다. 새 등록에는 동결 AL_D010 원본/검토 세대를 넣지 않는다.
- 서울빅데이터캠퍼스의 도로경계_면·보도(인도)_면은 공개 카탈로그만 확보했다. 반출 제한이 있는 원본을 받았다고 표시하지 않는다.
- JUSO 다운로드 URL의 HTTP200 응답은 SPA 시작 HTML이다. 주소 전자지도·건물 입구 원본 수집 완료가 아니다.
- 디지털 수치지형도 및 실제 DEM의 로그인/제공 프로그램 절차를 우회하지 않는다. 접근 불가·미수집·자료 자체 부재는 구별한다.
- 촬영 준비 기준과 미검토 원본은 유지한다. 자료 수집 우선 요청으로 기존 원본 검토를 생략하거나 사용자에게 이미 촬영한 것을 다시 요구하지 않는다.

## 기존 ASP.NET 수집 계보와 MySQL 축적

기존 `평창군공공공간원본등록Service.RegisterFileAsync`, `PublicDataIngestionDbContext`, `로컬공공자료Db.OptionsAsync`를 [반입 CLI](../../../../../eng/neighborhood-data-collection/README.md)에서 재사용했다. 새 DB·테이블·migration·서버 host를 만들지 않았다. 현재 건강한 `hongdal-mysql-1`, `127.0.0.1:13306`, **hongdal_dev**를 실제 확인한 후 process 메모리 안에서 기존 non-root 계정으로 접속했다. 기존 Compose·서비스·연결 설정과 운영 앱은 변경하지 않았다.

MySQL에는 원본 payload 7개와 결속용 intake receipt 7개의 **출처·판본·취득시각·hash·크기·비공개 파일 경로**를 등록했다. 실제 파일은 Git 제외 `artifacts/local/`에 보존한다. CSV/SHP/표제부 8행의 본문을 DB 행 단위로 정규화하거나 본문 전체를 DB blob에 넣은 것은 아니다.

- 수집 run 상태: `Partial / PendingHumanReview`, `NormalizedCount=0`.
- RawSnapshot: 316→330, 이번 ID 327~340. IngestionRun: 320→334, 이번 ID 331~344.
- 기존 NormalizedRecord 총수 71,081 유지. 자료 등록은 공간 객체의 동일성·현재성 확정이 아니다.
- apply 후 별도 DbContext/물리 연결로 14개 모두 출처·판본·hash·경로·상태를 재조회했다.
- 동일 manifest로 apply를 두 번 더 실행했다. 두 번 모두 새 등록0·쓰기 시도false였고, 신규 raw/run 전체 필드·`LastSeen`의 전후 지문도 동일했다.
- `current pointer`, 공개 배포·Unity·Runtime·Traversal·Gameplay 권위는 이번 수집으로 생성하지 않는다.

원본과 원래 취득 receipt는 변경하지 않는다. 사후 권리 설명·추가 집중창 대조는 별도 파일로 남기고 새 intake receipt가 원래 receipt의 SHA를 가리키도록 한다. 원래 receipt/hash와 경로·판본·권리가 다르면 같은 DB 행을 덮어쓰지 않고 충돌로 중단한다. 정확히 같은 등록의 멱등성과 전역 콘텐츠 중복 없는 전국 수집 완료는 다른 주장이다.

## 검증과 로컬 결과 위치

- 7경로 Fast 검증 `20261002-205038` 및 diff 검사 통과. 이 Fast는 문서/도구 경로 검사로 build/test를 생략했다. 아래 CLI build/self-test와 실제 MySQL 증거는 별도 실행이다.
- CLI build 오류0·경고0, guard self-test 31개 통과. 권리충돌·권위 승격·위장 host·잘못된 hash/경로·receipt 불일치·동결 원본을 거절한다.
- root가 원본 7개·intake receipt 7개·원래 receipt의 모든 SHA/크기를 다시 대조했다. XLSX/ZIP CRC 검증 통과, 표제부 8개와 높이/층수·PK 토큰 보존 확인.
- 키/비밀번호는 값 출력·로그·receipt URL에 남기지 않았다. 건물 담당의 저장된 자격 증명 검색은 유출 없음으로 확인했다.
- 기존 역세권 디오라마 규칙 r5 Validate PASS와 SHA 전후 동일을 확인했다. Candidate12/Provisional0/Accepted0 유지, 새 후보 등록·공통 적용·승격·Graph 편집은 없다.

로컬 결과 묶음은 `artifacts/local/neighborhood-data-collection/20261002-r1/`이다. 핵심은 `ingestion-manifest.json`, `ingestion-receipts/`, `root-input-verification.json`, `collection-catalog.json`, `db/`, `buildings/`, `streets/`, `terrain/`이며 원본은 commit/push 대상이 아니다. manifest SHA-256은 `651984E4CD360ADC1631863A8E80EB03BB624C72E2654ACE392656E439A0751F`다.

다음 수집도 **결손 선정→기존 자료 중복/판본 확인→공식 원본 취득→출처·권리·CRS/단위 감사→기존 수집 계보 등록→독립 재조회→동일 입력 no-op 확인→동일성/현재성 검토**로 진행한다. 이번 도구는 2026-10-02 고정 7개 입력의 검증·반입 도구다. 다음 날짜의 무인 다운로드, 상시 감시, 자동 스케줄, 자동 Unity 적용을 구현한 것으로 보고하지 않는다.
