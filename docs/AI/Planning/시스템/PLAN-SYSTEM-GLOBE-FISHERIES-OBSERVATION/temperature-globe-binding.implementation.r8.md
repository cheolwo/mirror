# 지구본 수온 사본 결속 r8

## 확정 범위와 승인 경계

2026-09-22 사용자 요청: 수온값과 물고기 이동을 기존 위경도 지구본에 연결한다. r4의 소표본 가져오기·r7의 같은 곡면을 재사용한다. 이번 수온 표시 범위는 `ApprovedScopedLocalPreview`이며 운영 효과·새 공식 Scene·공개 배포를 포함하지 않는다.

- NOAA L4 2026-09-01~07, 36.975~37.075°N / 130.025~130.125°E, 0.05도 격자 63행을 기존 로컬 MySQL에 비공개 검토보류로 저장·독립 재조회한다.
- 기존 가져오기 CLI의 `marine-sst-export`로 DB 검증 사본과 SHA-256을 내보낸다. Unity Editor Loader가 검증한 사본만 메모리에 읽는다. Resources·Save·Replay·운영 API·배포 자원에는 넣지 않는다.
- 기존 지구본의 실제 좌표에 해역 표식·수온을 표시하고, 선택 카드에서 날짜와 9개 격자를 선택해 좌표·섭씨·분석오차·출처를 확인한다. 과거 분석값이며 현재 실측·해저 수온·어군 관측이 아니다. 확대 카드의 격자와 실제 지도 축척을 구분한다.
- 물고기 이동에는 아직 종별 근거/규칙 승인이 없다. `가상 실험 어군 + 사용자 지정 선호 수온 + 인접 해양 격자 이동`과 `실제 어종 근거 조사 후 연결` 중 사용자 선택을 요청했다. 미답변을 승인으로 해석하거나 임의 최적 수온을 만들지 않는다. 이에 해당하는 게임 규칙 구현은 대기한다.

## 소유 경로·검증

- Hongdal: `eng/Ssalddel.PublicDataPortalImport/해수면수온표본.cs`의 export 분기, 이 문서·목차·현재 작업·화면 기록.
- 별도 Unity: `Presentation/WorldMap/해수면수온미리보기View.cs`, `Editor/해수면수온미리보기Loader.cs`, `Tests/EditMode/해수면수온미리보기Tests.cs` 및 meta. 기존 지구본/다른 미리보기는 수정하지 않는다.
- 검증: 원본/metadata/hash/DB 재조회, 중복 입력 신규 0, 내보내기 반복 동일 hash, DTO 단위·시간·좌표·결측·중복·품질 검증, 날짜/격자 선택, 실제 Play Mode·Game View. 새 WI·E 승격 없음.

## 진행 상태

- 원본 확보 63행, 로컬 `hongdal-mysql-1 / hongdal_dev` 저장 63행·독립 재조회 63행, 재입력 신규 0/수정 0 확인.
- 원본 SHA-256 `519e32ecc83f0d360eb215009c2a9eead9c955814296d501d3390cd8483e8951`, 정규화 SHA-256 `44b14e0ca62fa9d2883df84efeee245dd7f6f176a60279f6af11ad75ec6ed157`.
- 기존 r4의 NOAA 수신 차단은 이번 소표본에 한해 해소됐다. 국내 수온 API 인증 차단과 어군 규칙 미정은 별도로 유지한다.
- 공식 제품 설명·이용조건은 [NOAA CoastWatch metadata](https://coastwatch.noaa.gov/erddap/info/noaacwBLENDEDsstDNDaily/index.html)에서 재확인했다. GHRSST free/open 원문과 metadata hash는 로컬 receipt로 보존했다. 공개 승인은 부여하지 않았다.
- CLI build·자체시험 14/14, Unity 컴파일·전용 EditMode 10/10 통과. export를 두 번 실행해 같은 SHA-256 `8400d6a281364303e13f006141b09d8c37f439c46e71df26965e2bdd9ddcf429`을 확인했다.
- 실제 Play Mode에서 Editor Loader로 사본을 읽고 날짜 1일→7일·중앙→북서 격자·접기→열기를 API로 검증했다. 1일 중앙 37.025°N/130.075°E는 28.03°C, 7일 북서 37.075°N/130.025°E는 27.24°C로 표시됐다. [실제 Game View 2장](../../../../Changes/2026-09-22-globe-sst.md)을 남겼다. 물리 클릭·빌드·지속 실시간 API 연동은 미검증이다.
- 기존 시험 도구 `TestResultCollector` 콜백 오류가 재현되며 월드 종료 부모 변경 오류도 기존 미해결이다. Console 0을 주장하지 않는다. 일회성 검증 코드의 `FindAnyObjectByType`은 `DontSave` 미리보기 컴포넌트를 찾지 못해 실패했으며, 실제 Loader와 같이 지구본의 `GetComponent`로 재조회해 위 절차를 확인했다.
- Play Mode 종료, Scene 저장·commit·push 없음. r4 수신 차단을 수온 사본 연결 완료로 갱신하되 어군 이동 구현 완료로 확대하지 않는다. 기존 구면·자료/가상 분리 경계를 유지하며 새 디오라마 규칙 후보 없음. E 승격 없음.

## 재현

기존 안전한 로컬 DB 연결 환경에서 `dotnet run --no-build --project eng/Ssalddel.PublicDataPortalImport -- marine-sst-export .`를 실행한다. 원본을 지우거나 acquire를 덮어 실행하지 않는다.

Unity Play Mode에서 `Tools/Mirror/로컬 수온 사본 열기`로 `artifacts/local/public-data/marine/sst-20260901-07-r1/sst-preview.json`을 선택한다. 같은 폴더의 `.json.sha256` 파일이 필요하다. 사본·토큰을 배포 자원에 포함하거나 자동으로 최신 관측이라고 표시하지 않는다.
