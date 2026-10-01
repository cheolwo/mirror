# 30개 행정동 비공개 관찰 오버레이 첫 결속

[구현 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r25]

- 상태: `Implemented / OfficialSourcesRecovered / PrivateObservationReviewGeneratedAndVerified / CurrentRdbReadbackEmpty / MongoReadbackAuthenticationBlocked / UnityPrivateObservationOverlayImplementedAndGameViewCaptured / CurrentPublicationBlocked`
- 기준: [30개 행정동 공통 깊이 슬롯 첫 구현 r24](thirty-admin-dong-common-depth-slots.implementation.r24.md)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 구현 결론

공식 서울 열린데이터광장 POST 다운로드로 횡단보도와 교차로 원본을 다시 확보하고 길이와 SHA-256을 고정했다. 새 생성기 `eng/neighborhood/administrative_dong_private_observation_review.py`는 이 원본을 기존 `OA-22160` 역사 행정동 경계와 r24의 기초 도형 검토 세대에 결속해, 30개 행정동별 `privateObservationOverlays`를 만들었다.

결과는 횡단보도 점 1,533개(보행신호 설치 관측 933개)와 교차로 점 554개다. 점의 식별·위치 표현은 원천 관리번호를 노출하지 않는 hash stable ID와 공통 ENU millimeter를 사용한다. 후보 품질·충돌·연결 수 같은 최소 검토 메타데이터만 함께 두며, 정확한 관리번호·교차로명·주소·업체 식별자는 출력하지 않는다.

이 결과는 역사 경계에 결속한 로컬 비공개 관찰 표현 입력이다. 현행 행정동 경계, 횡단보도 면, 보도 연결, 접근 방향, 정지선, 신호 현시, 통행 graph 또는 gameplay 권위를 만들지 않는다.

## 공식 원본 복구

원본과 획득 영수증은 Git 추적 밖 `artifacts/local/public-data/admin-dong-precision-observation-20260926-r1/`에 보존한다. 획득 방식은 `OfficialSeoulDataPortalPostDownloadExactHashVerified`이며 공식 포털 응답의 실제 파일을 아래 값으로 검증했다.

| 역할 | 공식 자료·판본 | 파일 | 길이 | SHA-256 |
| --- | --- | --- | ---: | --- |
| 횡단보도·보행신호 설치 관측 | [`OA-23081`](https://data.seoul.go.kr/dataList/OA-23081/F/1/datasetView.do), 2026-08-24 | `서울시 교차로 및 횡단보도 시설·위치정보_20260824.xlsx` | 1,735,662 bytes | `1A5DB9EA7A1CD58E2D7F2B4246BAF099A3E4D5278F2867A1B87F3D50D21541BE` |
| 교차로 점 | [`OA-15534`](https://data.seoul.go.kr/dataList/OA-15534/S/1/datasetView.do), 2025-08-14 | `A008_P_20250814.zip` | 534,342 bytes | `A77B4D4FD2886C934D2D097558A52580FA95ADB079BA828F1305DFD15F0E0449` |

`OA-23081`의 좌표계는 원천 선언이 없으므로 기존 판정 `EmpiricallyValidatedCandidateNotSourceDeclared`를 유지한다. `OA-15534`는 원천 선언 `EPSG:5186`을 사용한다. 두 자료 모두 공공누리 제1유형 출처표시 조건을 영수증에 보존했다.

## 생성 계약과 결과

- overlay schema: `administrative-dong-private-observation-overlays.v1`
- overlay revision: `northeast-seoul-private-observation-overlay.r1`
- 검토 index·bundle·completion: `administrative-dong-diorama-unity-review-*.v2`
- exporter revision: `administrative-dong-diorama-unity-review-exporter.r3`
- 입력 경계: `OA-22160` 역사 경계 SHA-256 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`
- 입력 기초 검토 세대: r24 generation `C6CD2501CD2FC876BFF671FB25C36C139AFDAF14F78EC2FD3F7503E31A1153AC`
- 새 r2/r3 검토 generation: `4D94046D95DAFE6C31A02B484CCE68B8AFB790276197668EF72B5DE879FE3971`
- 생성 결과: 30개 행정동 bundle + `index.json` + `complete.json`, 첫 생성 `changedFiles=32`
- 즉시 재검증: 같은 세대의 파일 집합·내용을 다시 계산한 `verify`가 `PASS`

기초 bundle의 공개 `displayOverlays.items`는 계속 빈 배열이며 전체 30개 합계도 0이다. 새 점은 별도 `privateObservationOverlays`에만 들어간다. 횡단보도 stable ID는 원천 feature key를 판본과 함께 SHA-256으로 바꾸고, 교차로도 기존 비공개 후보의 hash stable ID만 소비한다. 원천 관리번호·교차로명·도로명 또는 지번 주소·상세주소·건물관리번호·업체명·업체 ID는 bundle에 싣지 않는다.

## 권위와 저장 경계

| 항목 | 현재 값 | 의미 |
| --- | --- | --- |
| 비공개 검토 표현 | `privateReviewVisualizationAllowed=true` | 비공개 검토 입력을 허용한다. 별도 실제 Unity Play Mode·Game View 증거도 아래 manifest로 확인했다. |
| 공개 표시 | `publicDisplayAllowed=false`, 공개 `DisplayOverlays=0` | 공개 API·화면에 노출하지 않는다. |
| current pointer | 생성·사용·갱신 모두 `false` | 기존 current 세대를 선택하거나 바꾸지 않는다. |
| DB·Mongo 사용/쓰기 | 모두 `false` | 이번 overlay 생성은 파일 기반 파생이며 DB나 Mongo에 적용하지 않았다. |
| Runtime | `runtimeAuthorized=false` | 서버 또는 Unity Runtime 권위가 없다. |
| 이동·게임 | `traversalReady=false`, `gameplayReady=false` | 실제 보행·차량 이동과 gameplay에 소비하지 않는다. |
| 원천 후보 Unity 적용 | `sourceCandidateUnityApplyAllowed=false` | 후보 원장을 그대로 Scene에 적용할 수 없다. |

`privateObservationOverlayCaptureReady=true` 필드만으로는 Scene 결속, Play Mode 실행, 실제 Game View 캡처나 사용자 입력 검증을 대신하지 않는다. 이번 실제 Play Mode·Game View 결과는 아래 별도 capture manifest로 확인했으며 Scene·Prefab 저장과 사용자 입력 검증은 여전히 별도다.

## 현재 저장소 상태 재확인

과거 r6·r8·r11·r12·r13 문서의 MySQL 저장·독립 재조회 완료는 당시 실행 기록으로 보존한다. 그러나 2026-09-26 현재 `hongdal_dev`를 다시 확인한 결과 G2a·G3a·G3b·G3c·G4a·G4b 대상 행은 각각 0행이다. 따라서 그 과거 기록을 현재 DB에 후보 원장이 남아 있다는 증거로 사용하지 않는다.

MongoDB는 인증 실패로 현재 candidate·current 상태를 재조회하지 못했다. 실패를 0건이나 미적용으로 바꾸어 해석하지 않으며, 인증을 복구한 뒤 읽기 전용 재확인이 끝날 때까지 Mongo 현재 상태는 `Unverified`다. 이번 r25 생성기는 DB·Mongo에 연결하거나 쓰지 않았다.

## Unity 진행 상태와 검증 상한

r25의 v2/r3 bundle을 읽는 Unity 비공개 overlay 소비 경로를 구현하고 Editor build와 집중 EditMode 18/18을 통과했다. 실제 graphics process도 canonical `SimulationWorldShell`을 Play Mode로 열어 30개 행정동 overview와 면목제3·8동 detail을 Game View로 캡처하고 exit code 0으로 끝났다.

실제 증거는 Hongdal 저장소 안이 아니라 별도 Unity 저장소 `C:\Users\user\ssalddel`의 `Documentation\Changes\2026-09-26-administrative-dong-private-observation-r2\`에 있다. 이 경로에는 `modules/`의 overview PNG 30개, `31-1126057500-myeonmok-3-8-detail.png`, `capture-manifest.json`이 있으며 manifest schema는 `administrative-dong-diorama-gameview-evidence.v2`, 캡처 시각은 `2026-09-26T03:11:05.5248219Z`, Unity 판본은 `6000.5.6f1`이다.

manifest는 검토 generation `4D94046D95DAFE6C31A02B484CCE68B8AFB790276197668EF72B5DE879FE3971`과 overlay 집합 hash `47A71221F9634A8AA63A5E9DB99F80C40D483848679B4C5A0ECD1C8FEBF6567B`를 기록한다. 30개 모듈 합계는 횡단보도 점 1,533개·교차로 점 554개이며 모든 모듈이 `rendererCount=4`, `colliderCount=0`이다. `actualPlayMode=true`, `actualGameViewCapture=true`, `privateObservationOverlayCandidateEvidence=true`이고 공개 `DisplayOverlays`는 계속 0이다.

Scene SHA-256은 실행 전후 모두 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 같고 `sceneSaved=false`, `sceneDirtyAfterPlay=false`다. 캡처 실행은 기존 canonical Scene의 서버·replay·local endpoint 주변 오류 8건을 숨기지 않고 기록했으며 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`다. Console 0은 아니지만 전체 overlay bundle 검증, 30개 overview·detail 생성과 manifest 완료 뒤 exit code 0이므로 이 8건을 행정동 overlay 캡처 실패로 분류하지 않는다. 주변 오류 자체가 해결됐다는 뜻도 아니다.

따라서 현재 완료 표현은 `PrivateHistoricalObservationOverlayGameViewCaptured`까지다. Scene·Prefab 저장, 실제 마우스 입력, 공개·배포, E 승격, 통행·gameplay는 완료 주장에 포함하지 않는다.

## 후속 순서

1. G3c 역사 보행망을 같은 행정동·공통 ENU·비공개 관찰 계약으로 결속하고 점과 선의 계보를 분리한다.
2. 최신 `OA-15536` 노면 방향표시를 새 판본으로 확보해 접근 방향 후보를 검토하되 정지선·통행 권위로 확대하지 않는다.
3. `OA-22241` 지형과 `OA-21145` 비오톱을 출처·판본·좌표계별로 분리해 기초 표현 후보를 만든다.
4. `OA-22784` 250m 생활인구를 공간 집계 관측으로 연결하되 개인·가구·NPC 생성 권위로 사용하지 않는다.
5. 승인된 현행 JUSO 행정동 경계와 같은 세대 건물·건물군·출입구를 확보한 뒤 새 revision으로 재귀속한다.

이 순서는 자료·검토 깊이를 늘리는 후속 대기열이며 current 게시나 Unity·gameplay 적용의 자동 승인이 아니다. **새 디오라마 규칙 후보 없음**.
