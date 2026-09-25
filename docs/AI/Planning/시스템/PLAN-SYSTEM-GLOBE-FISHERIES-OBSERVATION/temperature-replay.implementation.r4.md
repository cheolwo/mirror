# [기획 · 지구본·수산물 관찰 · PLAN-SYSTEM-GLOBE-FISHERIES-OBSERVATION · r4]

## 확정 / 이번 수직 범위

2026-09-22 사용자의 “한번 그렇게 해봐”로 [r3](temperature-and-marine-diorama.proposal.r3.md)의 기록된 수온을 날짜별로 재생하는 방향을 수용했다. 원 기획 SHA-256은 `081bfe66db9ff3ff38e8fada8ef0c233e30fabfdad89b7672fba1b0c4ab7face`다.

첫 구현은 공식 자료 → 기존 가져오기 → 기존 로컬 MySQL → 독립 재조회 → 날짜별 읽기 프레임이다. 물고기 이동이나 해양 Scene까지 구현 완료했다는 뜻이 아니다. 생물종 대응·이동 규칙·표현 명세가 미정이므로 새 WI/Goal·E 증거를 자동 생성하지 않는다.

- 쓰기 범위: `eng/Ssalddel.PublicDataPortalImport/해수면수온표본.cs`, 기존 `Program.cs`의 전용 명령 분기, 이 기획 폴더, `PLANNING.md`, `CURRENT_WORK.md`.
- 기존 ASP.NET Core의 `평창군공공공간원본등록Service`, `EfExternalDataIngestionStore`, `PublicDataIngestionDbContext`를 CLI에서 재사용한다. 별도 HTTP 서버·테이블·전체 migration·배포는 없다.
- 공공자료는 비공개·검토보류로 저장한다. 가져오기 명령의 `committed`는 DB transaction이지 Git commit이 아니다.

## 실제 자료와 검증용 선택

국립수산과학원 API는 공급원 인증키를 요구하므로 [NOAA CoastWatch 제품 metadata](https://coastwatch.noaa.gov/erddap/info/noaacwBLENDEDsstDNDaily/index.htmlTable)의 공개 소량 조회를 사용한다. 이 선택은 기술 검증 표본이지 해당 해역에 세 품목이 실제 서식한다는 판단이 아니다.

| 항목 | 값과 경계 |
| --- | --- |
| 공급원 / 제품 | NOAA NESDIS CoastWatch / `noaacwBLENDEDsstDNDaily`, `Geo_Polar_Blended-OSPO-L4-GLOB-v1.0` |
| 자료 종류 | 위성 등을 합성한 L4 해수면 분석 수온. 현장 실측·해저 수온·어군 관측 아님 |
| 기간 | 2026-09-01~07, 매일 자료 기준 시각 12:00 UTC. 수집 시각과 별도 |
| 격자 중심 | 위도 36.975 / 37.025 / 37.075, 경도 130.025 / 130.075 / 130.125 |
| 해상도 | 원본 0.05도. 각 격자 중심의 대표값이며 확대한다고 정밀도가 높아지지 않음 |
| 보존 값 | 섭씨 수온, 분석오차, 해양 mask, 위경도·기준 시각 |
| 깊이 | 수층별 측정 깊이 없음. 0m 실측으로 임의 표기하지 않음 |
| 이용조건 | metadata의 GHRSST free/open 문구 확인. 원본 metadata·hash 보존. 프로젝트 공개 승인과 구분 |
| 보관 | `artifacts/local/public-data/marine/sst-20260901-07-r1` 원본·metadata·receipt, 기존 MySQL의 공공자료 3개 테이블 |

## 실패·재생 계약

- 원본·metadata hash와 정규화 결과 hash를 구분한다. 정규화 hash는 수집시각·입력 행 순서와 독립적이다.
- 단위·제품·mask 의미가 바뀌면 중단한다. 범위 밖 좌표, 중복 격자, 육지/얼음, 비정상 수온을 거부한다.
- null/fill 값은 결측으로 보존한다. 없는 날짜에 최근값을 복제하거나 미래값을 대신 사용하지 않는다.
- 날짜 재생은 정확한 자료 기준 시각의 셀을 선택하는 조회이며, 현재는 CLI의 일별 프레임 검증까지다. Unity 재생 UI나 자동 재생 시계는 아직 아니다.
- 같은 입력은 기존 행을 재사용한다. 이미 저장한 같은 키의 내용이 달라졌으면 조용히 덮지 않고 중단한다.
- NOAA 직접 조회는 이 개발 가져오기 명령에서만 수행한다. Unity 실행 중 외부 API 직접 호출 경로는 만들지 않는다.

## 실행

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- marine-sst-self-test .
dotnet run --no-build --project eng/Ssalddel.PublicDataPortalImport -- marine-sst-acquire .
dotnet run --no-build --project eng/Ssalddel.PublicDataPortalImport -- marine-sst-preview .
dotnet run --no-build --project eng/Ssalddel.PublicDataPortalImport -- marine-sst-apply .
dotnet run --no-build --project eng/Ssalddel.PublicDataPortalImport -- marine-sst-verify .
```

`acquire`는 기존 폴더를 덮지 않는다. `apply`는 기존 로컬 대상 검증을 통과해야 하며, 공유 컨테이너이면 기존 `SSALDDEL_PUBLIC_DATA_LOCAL_CONNECTION` 경계를 이용한다. 비밀값은 로그·문서에 남기지 않는다.

## 검증 상태

- 상태: `HistoricalReplayDirectionAccepted / ImportToolImplemented / AcquisitionBlocked / NoUnityRuntime`.
- 도구 build와 자체시험 14/14 통과. 정상 파싱, 날짜 선택·누락일, 결측값, 행 순서 독립 hash, 수집 시각 독립 키와 단위/중복/좌표/기간/시각/mask/값/크기 위반을 검사했다. 합성 시험 자료는 공식 관측으로 저장하지 않았다.
- 지정 6개 경로의 Fast 검증·`git diff --check`와 기획 내부 링크 확인 통과. Fast는 이 `eng` 도구를 문서/지침 범위로 분류해 build/test를 생략하므로, 위 `dotnet run ... marine-sst-self-test`의 실제 build·시험과 별도로 보고한다. Fast 기록: `artifacts/local/validation/20260922-164008`.
- 최초 HTTP 조사에서 수온·mask 63행 응답을 확인했다. 첫 행은 2026-09-01 12:00 UTC, 36.975 / 130.025, 27.869993°C, water mask 1이다. 이 응답은 콘솔 표본 확인이며 원본 파일·DB 축적 완료가 아니다.
- 이후 원본·metadata 묶음 확보 명령은 시간 초과했다. 공식 metadata JSON, HTML, DAS 경로와 IPv4 직접 요청도 응답 없이 제한 시간을 초과했다. 웹 조사 도구의 공식 설명 열람 성공과 개발 환경에서 원본을 완전 수신·보관하는 성공을 구분한다.
- 실제 `hongdal-mysql-1` Compose 프로젝트·작업 경로·실행 상태와 기존 `hongdal_dev` 공공자료 테이블을 읽기 전용으로 확인했다. 현행 Compose는 `docker-compose.yml` 및 모바일 현장검증 override다. `docker-compose.dev-deps.yml`로 가정하지 않는다.
- 이번 자료의 DB 저장·독립 재조회·실제 동일 입력 재처리는 **미실행**이다. 가져오기 명령이 `databaseWriteAttempted=false`에서 끝났다. 실제 DB 멱등성은 아직 증명하지 않았다.
- Unity 제품 코드·Scene·Play Mode·Game View·commit·push는 변경/실행하지 않았다. CLI 일별 프레임과 Unity 재생 화면을 혼동하지 않는다.

재개는 `marine-sst-acquire`부터다. 출처 원본 수신 후 기존 로컬 연결 검증을 거쳐 preview → apply → verify → 동일 입력 apply를 수행하고, 그 결과를 기준으로 표시용 상태 사본과 Unity 날짜 선택을 연결한다. 미완료를 합성 수온으로 대체하지 않는다.

## 미정 / 다음 구현 경계

실제 어종 최적수온·속도·이동 경로, 해양 디오라마의 시각 명세와 기존 곡면 카메라 결속은 미정이다. 다음 단계는 이 소표본을 읽는 **수온 전용 해역 표시와 날짜 선택**부터 좁게 연결하고, 어군은 출처가 있는 종별 규칙 또는 승인된 가상 실험 매개변수를 별도로 결속한다.

새 디오라마 규칙 후보 없음. 기존 사실/해석/표현 분리 원칙을 지지하며 공통 규칙·E 자동 승격 없음.
