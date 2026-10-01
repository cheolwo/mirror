# 30개 행정동 역사 보행망 Unity 비공개 검토

[구현 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r27]

- 상태: `Implemented / PrivateWalkNetworkGenerationVerified / UnityGameViewCaptured / HistoricalAndPrivateReviewOnly / CurrentPublicationBlocked`
- 기준: [역사 보행망 비공개 검토 세대 r26](thirty-admin-dong-walk-network-private-review.implementation.r26.md)
- 공식 자료: [서울 열린데이터광장 OA-21208](https://data.seoul.go.kr/dataList/OA-21208/A/1/datasetView.do)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 구현 결론

r26의 로컬 보행망 후보를 30개 행정동의 기존 기초 도형과 비공개 관찰 overlay에 결속하는 새 Unity review generation과 소비 경로를 구현했다. 노드 17,267개, 링크 조각 23,472개를 보존했고 링크의 시작·끝점만 남기지 않고 원 경로의 모든 꺾임 54,574점을 ordered path로 내보냈다. Unity는 각 연속 점 쌍을 기존 2x2 결합 Mesh 안에 그리며 노드와 경로점 합계 71,841개를 manifest에서 전수 대조한다.

이 결과는 2020년 도보 네트워크와 2023-10-31 역사 행정동 경계를 결합한 로컬 비공개 검토 표현이다. 현재 통행 가능성, 연결망 완결, NavMesh, 배달 경로, Runtime 또는 gameplay 권위를 만들지 않는다.

## 고정 generation과 경로 보존

- generator: `eng/neighborhood/administrative_dong_walk_network_unity_review_r3.py`
- index·bundle·completion schema: `administrative-dong-diorama-unity-review-*.v3`
- exporter revision: `administrative-dong-diorama-unity-review-exporter.r4`
- 최종 generation: `29764F2B861BF00D60951D23F6F03B9D59281D28C0DF447E2F0AAE35F2972915`
- index SHA-256: `63B8EEAC823E1CD5F8E4D636331419B4A7292148D1E09CB84AB77EC3FC57C977`
- exporter source SHA-256: `7EB8846AAB1CB4A0D180F35A3557C9894B8A92E8AE1A00D85E4BC46950987272`
- audit SHA-256: `5B486A56B1DA41FB04AF966EB987397E4F1373C7AA826ECFBE8001A59D4F2F97`
- complete SHA-256: `228307EEC9423AC426D5E119ED9610A284C7D960057C19E6EB82B3369CB24A82`
- 로컬 경로: `artifacts/local/validation/admin-dong-diorama-observation-review/r3/input/generations/29764f2b861bf00d60951d23f6f03b9d59281d28c0df447e2f0aae35f2972915/`

초기 로컬 초안 generation `5BE5230766892F771D1A8D2EB6D48D07F2B2EA7B31F29553E49C653DC1262D8C`는 링크별 시작·끝점만 직렬화해 경로의 중간 꺾임을 잃었다. 이 초안은 덮어쓰지 않고 `SupersededLocalDraft`로 보존하되 사용·보고 대상에서 제외했다. 최종 generation은 `commonEnuMillimeterPath[]` 전체를 보존한다.

| 항목 | 최종 수량 |
| --- | ---: |
| 행정동 | 30 / 30 |
| 노드 점 | 17,267 |
| 링크 조각 | 23,472 |
| 링크 path 점 | 54,574 |
| 노드 + 링크 path 점 | 71,841 |
| 원천 자치구와 공간 귀속 충돌 후보 | 48 |

원천 직렬화 길이는 `882,519.354m`, 공통 ENU path 재계산 길이는 `882,518.768107m`다. 전체 차이 `0.585893m`는 1m 기준 안이고 조각별 최대 차이 `0.002409m`는 3mm 기준 안이다. 30개 bundle의 모든 path 점은 해당 행정동 manifest bounds 안에 있다.

생성 폴더는 33개 파일, 85,186,100 bytes다. `complete.json`이 고정하는 payload는 bundle 30개와 index·audit 2개, 합계 32개이며 자기 자신은 의도적으로 목록에서 제외한다. 모든 기록 길이와 SHA-256을 실제 파일과 재대조했고 동일 입력 rebuild는 `changedFiles=0`, self-test `7/7`, 독립 `verify PASS`였다.

## 위치 정보와 공개 경계

원천 node/link 식별자, 원천 자치구 식별, 법정동 식별, 원천 timestamp와 직접 EPSG:5186 좌표 필드는 bundle에 넣지 않았다. candidate stable ID는 `sha256:<64 hex>` 형태다.

그러나 최종 bundle은 WGS84 기반 변환 원점과 millimeter 단위 공통 ENU 점·선을 포함하므로 정밀 지리 위치로 역변환할 수 있다. 따라서 `원본 CRS 직접 좌표 미포함`을 `정확 위치 없음`으로 해석하지 않는다. generation과 Unity 캡처는 Git 제외 로컬 비공개 자료이며 공개 문서·API·화면으로 배포하지 않는다.

| 경계 | 값 |
| --- | --- |
| 역사 경계 후보 | `historicalBoundaryBootstrapOnly=true` |
| 비공개 검토 | `privateReviewOnly=true`, `reviewVisualizationOnly=true` |
| 공개 표시·배포 | `publicDisplayAllowed=false`, `distributionApproved=false` |
| current pointer | 사용·생성·갱신 모두 `false` |
| DB·Mongo | 읽기·쓰기·적용 모두 없음 |
| Runtime | `runtimeAuthorized=false` |
| 이동·게임 | `traversalReady=false`, `gameplayReady=false` |
| 원천 후보 직접 적용 | `sourceCandidateUnityApplyAllowed=false` |

공개 `displayOverlays.items`는 30개 모두 0이다. private review capture 입력을 만들었다고 공개 current generation이나 운영 사본으로 승격하지 않는다.

## Unity 구현

별도 Unity 저장소 `C:\Users\user\ssalddel`에서 기존 행정동 검토 흐름의 다섯 파일만 확장했다.

- `행정동디오라마검토Models.cs`: v3/r4 계약, stable ID·hash·영수증·권위 flag·중복·유한 좌표·local bounds·path 길이 검증과 깊은 복사
- `행정동디오라마검토MeshBuilder.cs`: 링크의 모든 연속 edge와 모든 노드를 기존 2x2 배치 Mesh에 결합
- `행정동디오라마검토View.cs`: private review 경고, 노드·fragment·path·mesh 수량과 Mobility/Traversal 차단 표시
- `행정동디오라마검토Tests.cs`: observation과 walk 진단 분리, 전체 path 보존·권위 거절·mesh count 시험
- `행정동디오라마검토GameView검증.cs`: v1 geometry, v2 observation, v3 walk 호환과 generation·audit·completion·캡처 manifest 검증

모듈별 Renderer 상한 4개와 Collider 0개를 유지하고, 단일 batch 500,000 vertex 거절 관문을 유지한다. 이 오버레이는 화면 표현만 추가하며 `Mobility` 깊이 슬롯은 `Blocked`, `TraversalReady=false`다.

## 실제 Unity 검증

- `dotnet build Ssalddel.Unity.Tests.EditMode.csproj`: 오류 0
- `dotnet build Ssalddel.Unity.Editor.csproj`: 오류 0
- 집중 Unity EditMode: `22 / 22 PASS`, 실패·skip·inconclusive 0
- 실제 graphics process: canonical `SimulationWorldShell` Play Mode 진입과 Game View 캡처, exit code 0
- 증거 경로: `C:\Users\user\ssalddel\Documentation\Changes\2026-09-26-administrative-dong-private-walk-network\`
- 증거: overview PNG 30장, 면목제3·8동 detail 1장, `capture-manifest.json`, `README.md`
- manifest: `administrative-dong-diorama-gameview-evidence.v3`, `actualPlayMode=true`, `actualGameViewCapture=true`
- 모든 모듈 Renderer 4개, 전체 Collider 0개
- 전체 mesh vertex 2,250,744개, 동별 최대 132,408개, 단일 batch 최대 82,404개
- 보행망 mesh vertex 193,476개, 기존 관찰 overlay mesh vertex 21,128개
- PNG 31개의 SHA-256과 manifest 재대조: 불일치 0

canonical Scene SHA-256은 실행 전후 모두 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`이며 `sceneSaved=false`, `sceneDirtyAfterPlay=false`다. 캡처 중 기존 canonical Scene의 서버·replay·local endpoint 주변 Console 오류 8건이 기록됐고 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`다. 보행망 검증과 캡처는 완료됐지만 Console 0, 서버 통합 또는 주변 오류 해결을 주장하지 않는다.

## 남은 한계와 다음 정밀화

1. 원천은 2020년이고 경계는 2023년 역사 후보다. 현재 공사·폐쇄·시간 제한과 실제 통행성을 반영하지 않는다.
2. 대상 세 자치구 밖 halo가 완전하지 않아 경계 바깥 연결을 완결된 graph로 해석하지 않는다.
3. 보도 폭·경사·계단·연석·출입구·정지선·신호 현시와 배달 오토바이 통행은 검증하지 않았다.
4. `OA-22241` 2025 등고선·표고점으로 30개 동 공통 격자와 seam 검증을 수행하되 수직 datum·높이 단위 미확인을 유지한다.
5. 승인된 현행 JUSO 행정동 경계와 같은 세대 건물·건물군·출입구를 확보하면 새 revision으로 재귀속하고 이 역사 세대를 조용히 덮어쓰지 않는다.

현재 완료 표현은 `PrivateHistoricalWalkNetworkGameViewCaptured`다. 공개·배포, current pointer, Scene·Prefab 저장, Runtime·Traversal·Gameplay, E 승격은 완료 주장에 포함하지 않는다. **새 디오라마 규칙 후보 없음**.
