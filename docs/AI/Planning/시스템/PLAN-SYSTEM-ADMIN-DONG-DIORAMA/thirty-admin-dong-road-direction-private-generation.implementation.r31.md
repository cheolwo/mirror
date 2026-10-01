# 30개 역사 행정동 방향표시 비공개 후보·검토 번들 생성

[구현 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r31]

- 상태: `PrivateCandidateGenerated / Historical30DongReviewBundleGenerated / UnityAppliedAndCaptured / CurrentPublicationBlocked`
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동
- 선행 근거: [OA-15536 원본 감사 r30](thirty-admin-dong-direction-biotope-source-audit.implementation.r30.md), [역사 보행망 Unity 비공개 검토 r27](thirty-admin-dong-walk-network-unity-review.implementation.r27.md)

## 입력과 출처

[서울시 방향표시 관련 정보 OA-15536](https://data.seoul.go.kr/dataList/OA-15536/A/1/datasetView.do)의 `A055_P_방향표시_20260910.zip`을 사용했다. 데이터 기준일은 2026-09-10, 첨부 수정일은 2026-09-17이다. 공식 원본은 ESRI Point Shapefile 158,373점, EPSG:5186, EUC-KR이며 ZIP 길이는 6,071,161 bytes, SHA-256은 `641432B09B0C4A7AE38909873287BEE3E553B5C4F2CC0CD5394E03E7C36CCBAD`다. 취득 receipt SHA-256은 `2BD67989B5ED1139BAB98F1CF84E7F4B1105504B854AF249DEB99E568D0881C1`이다. 출처 표시는 서울특별시·공공누리 제1유형을 따른다. 로컬 원본·정의서·페이지·receipt는 `artifacts/local/public-data/admin-dong-road-direction-20260926-r1/raw/`에 보존했다.

공간 귀속에는 OA-22160의 2023-10-31 역사 행정동 경계 ZIP SHA-256 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`과 정확한 30개 scope 정의 SHA-256 `CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B`를 썼다. 이 경계는 현행 행정동 정본이 아닌 비공개 검토 모판이다.

## 비공개 후보 세대 r1

새 생성기 `eng/neighborhood/administrative_dong_road_direction_private_review_r1.py`의 SHA-256은 `2049D2C30417154FB0770EE2C5F12212ECC9359584FFCF6A9C16CA5C6BFD243D`다. 입력 원본·receipt·경계·scope·생성기 hash를 결속한 불변 generation은 `19FBA1601FCEDF6AFF1A41E497360C89FFC0492DA869B992057A3A83410702CF`이며, `artifacts/local/validation/admin-dong-road-direction-private-review/r1/input/generations/19fba1601fcedf6aff1a41e497360c89ffc0492da869b992057a3a83410702cf/`에 manifest·audit·candidates NDJSON·complete 4파일을 만들었다.

158,373점 중 역사 경계 하나에만 귀속된 8,415점을 선택했다. 범위 밖은 149,958점, 복수 귀속은 0점이다. 30/30개 동에 점이 있고 동별 101~653점이며, 선택된 공식 `A055_KND_C` 종류 코드는 20종이다. 후보는 원천 관리번호 대신 hash stable ID, 기존 공통 좌표계의 ENU 밀리미터 위치, 공식 종류 코드, `DRN` 숫자 원값만 보존한다. 원천 관리번호·원본 좌표와 `LENX` 원값은 후보에 넣지 않았다. `LENX` 값이 있는 선택 행 8,415건은 `sourceLengthValuePresentButUnitUnverified`로만 audit에 집계한다.

공식 정의서는 `DRN`을 방향표시 각도라고 설명하지만 0축, 회전 방향, 단위는 확인되지 않았다. 따라서 이 값은 `sourceNumericCandidateOnly`이며 `angleSemanticVerified=false`, `directionResolved=false`, `renderedArrowAuthorized=false`다. `LENX`의 단위도 미확인이므로 `sourceLengthUnitVerified=false`이고 실제 치수나 렌더 크기로 사용하지 않는다.

## r4 Unity 검토 입력 결속

새 결속 생성기 `eng/neighborhood/administrative_dong_road_direction_unity_review_r4.py`의 SHA-256은 `87B4D35EF30EC629D7F43BBE982A88C5E2F82D11B1A6BFC37A3E3DFACB2674E8`이다. 기존 walk 검토 generation `29764F2B861BF00D60951D23F6F03B9D59281D28C0DF447E2F0AAE35F2972915`의 30개 번들에 동별 `privateRoadDirectionOverlays` v1을 중첩했다. index·bundle·completion은 v4, exporter는 r5다. 기존 v1~v3 필드는 보존하고 index·bundle·audit·complete의 새 hash를 계산했다.

새 불변 generation은 `3A7475C181E6BE11D46F256B40150119B95C3473477F529FDA7802B51053CD33`, index SHA-256은 `6170476FEEB84A4584E4C2D4EE5B3EA9B6CF651BEF12FF39E21780C4F020E887`이다. 로컬 경로는 `artifacts/local/validation/admin-dong-diorama-observation-review/r4/input/generations/3a7475c181e6be11d46f256b40150119b95c3473477f529fda7802b51053cd33/`이다. 생성 파일은 33개이고 complete의 파일 descriptor는 32개다. 모든 30개 번들에 방향표시 점을 결속했고 각 overlay는 stable ID·내용 hash·source receipt·2026-09-10 source vintage·공식 종류 코드·미해석 `DRN` 수치·권위 차단 플래그를 포함한다.

기존 walk 노드 17,267개, 링크 조각 23,472개, 링크 원천 경로 꼭짓점 54,574개와 출력 경로점 71,841개를 유지했다. r3와 r4의 기존 observation subtree 및 walk subtree는 각각 30/30개 번들에서 동일하다. 모든 walk 노드·경로점과 방향표시 점이 해당 번들의 manifest bounds 안에 있음을 검사했다. 공개 `displayOverlays` 항목은 0개이고, 새 방향표시 overlay의 원천 ID·`LENX`·원본 CRS 좌표 등 금지 필드 검출은 0건이다.

`privateReviewVisualizationAllowed=true`, `reviewVisualizationOnly=true`, `historicalBoundaryBootstrapOnly=true`는 비공개 위치 glyph 검토 범위만 뜻한다. `angleSemanticVerified`, `directionResolved`, `renderedArrowAuthorized`, `sourceLengthUnitVerified`, 공개 표시, current pointer, runtime, traversal, gameplay은 모두 `false`다. 노면 기호가 실제 접근·주행 방향, 차로, 정지선, 신호 현시 또는 통행 허가를 확정하지 않는다.

## 실행 검증과 남은 단계

- 두 생성기의 `self-test`는 각각 4/4 통과했고 `py_compile`은 오류 없이 끝났다.
- 후보 r1과 결속 r4 모두 build→verify를 통과했다. 같은 입력의 재생성은 각각 `changedFiles=0`이었다.
- 두 산출물은 `artifacts/local/` 아래 Git ignore 대상이다. 이번 작업 변경 범위는 생성기 2파일과 이 구현 기록 1파일이며 commit·push는 하지 않았다.

Unity 쪽은 기존 v1~v3 입력 호환을 유지하면서 v4 결속·hash·권위 플래그를 fail-closed로 검사하도록 확장했다. `DRN`과 `LENX`는 mesh 방향·크기에 사용하지 않았다. 8,415개 후보는 모두 같은 크기의 무방향 마름모로 표현했고, 후보당 4정점으로 방향표시 정점 33,660개를 기존 2×2 mesh batch에 추가했다. 화살표는 0개다. 일반 C# 빌드는 EditMode와 Editor 프로젝트 모두 경고·오류 0으로 끝났고 Unity EditMode 집중 시험은 26/26 통과했다. 결과 XML SHA-256은 `1D3E813703A7AC4CF08859E579BAFB7AA479BC3B5258B3C6D608E516500440BE`다.

Unity `6000.5.6f1`의 실제 Play Mode와 Game View에서 30개 동 overview와 면목3·8동 detail 1장을 새로 캡처했다. 증거 폴더는 `C:/Users/user/ssalddel/Documentation/Changes/2026-09-26-administrative-dong-private-road-direction/`이고 `capture-manifest.json`과 31개 PNG의 hash를 독립 재검산했다. 모든 동은 renderer 4개, mesh filter 4개, Collider 0개를 유지했다. 전체 모듈 mesh 정점은 2,284,404개, 모듈 최대는 133,156개, batch 최대는 82,792개였다. canonical Scene SHA-256은 실행 전후 모두 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`이고 Scene은 저장되거나 dirty 상태로 남지 않았다.

캡처 중 기존 canonical Scene에서 Console 오류 8개가 관찰됐으며 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`였다. 따라서 이 결과는 방향표시 overlay의 실제 표현·mesh 예산·Scene 불변 증거이고 Console 0이나 서버·DB·Mongo 통합 증거는 아니다. 현행 행정동 정본 확보, current pointer, 공개 게시, 이동·게임 권위와 E 증거 승격도 수행하지 않았다. 새 디오라마 규칙 후보 없음.
