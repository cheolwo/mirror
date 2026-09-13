[기획 · 시스템·월드 투영 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r13]

# 역세권 디오라마 모듈 표준

- 상태: `ApprovedScopedImplementation / StandardizationDirectionConfirmed / InitialOneKilometerWindowConfirmed / MajorTransferVariableWindowAllowed / InitialStationSourceStored / ServerFirstSliceImplemented / ReadOnlyBindingSafetyTested / YongmasanUserNameConfirmed / SecondActualSampleMyeonmokConfirmed / MyeonmokLocalPrivateReviewImplemented / YongmasanLocalPrivateReviewImplemented / MissingCoverageRetained / UnityParserMeshCrossCheckPassed / UnityPlayModeGameViewVerified / UnityOfficialRunnerPending / UnitySceneBindingPending / StationScopedProjectionPending / SagajeongMarketLandmarkCollectionSpecificationApproved / BuildingVisualReplacementDirectionConfirmed / FirstReplacementBuildingPending / FreeSteamReleaseIntentRecorded / PhotoUsePolicyApproved / OwnCaptureUnavailable / KOGLType0Or1PriorityConfirmed / FirstPublicPhotoSliceStored / ExactMarketPhotoMissing / CCBYSAProfilePending / ImplementationInProgress`
- 기획 근거: 2026-09-13 사용자 제안 “사가정역에서 만든 수준을 표준화하고, 사가정을 더 깊게 만든 결과를 다른 역에도 재사용한다.”와 후속 답변 “사가정·면목·역마산은 역 중심 1km × 1km로 하고 대형 환승역부터 가변 범위를 허용하며, 서버가 역별 정보를 관리·조회하게 한다.”, “역마산역”은 용마산역을 뜻한다는 확인, 두 번째 실제 공간자료 적합성 표본을 면목역부터 진행한다는 확인, 그리고 “사가정역과 유사하게 면목역도 1km × 1km로 표현하고, 자료가 없으면 수집한 뒤에도 없으면 결손을 남긴 채 구현한다”는 범위 승인. 이어서 용마산역도 같은 1km 범위로 자료를 조사하고 Unity 장면에 드러내 달라는 구현 승인을 받았다. 후속으로 역별 랜드마크를 공식·시각·공간 근거로 수집해 Blender 모델과 배치맵에 연결하는 방향, 첫 대상을 사가정시장 입구·대표 골목으로 두고 자료 수집 명세부터 만드는 범위를 확인했다. 도로명주소·건축물대장·외관 사진을 분리 수집하고, 권리가 확인된 대표 건물만 Blender 자원으로 만들어 기존 절차적 모형을 안전하게 교체하는 제안을 추가했다. Steam 기본 게임은 무료 출시 의도를 유지하되 사진·건물·플랫폼 배포 권리를 별도 판정한다. 직접 촬영은 어렵다는 조건과 공공누리 등 변경·배포 조건이 확인되는 공공 사진을 현재 우선 경로로 삼는 방향을 확정함.
- 상위 묶음: [지역 Experience Package](../PLAN-SYSTEM-REGION-EXPERIENCE-PACKAGES/README.md)
- 첫 참조 구현: [사가정 공간 밀도 표현 r18](../PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/spatial-density-presentation.r18.md)

## 목적

사가정역 구현을 역마다 복사하지 않고, 같은 자료 계보·품질·성능·검증 경계를 갖는 역세권 디오라마 모듈로 표준화한다. 사가정은 첫 적합성 기준이자 계속 개선하는 참조 프로필이며, 후속 역은 같은 공통 조립기를 사용하되 자기 자료·판본·권리·품질 결손을 독립적으로 가진다.

역세권 모듈은 행정동·법정동을 대신하는 새 공간 정본이 아니다. 여러 동의 500m 타일을 역 중심 표현 창으로 골라 조합하는 관점별 조회 결과다. 인접 역의 창이 겹쳐도 원본 건물·도로를 역별로 복제하지 않는다.

```text
지역 Experience Package · 발견/선택/갱신
└─ 역세권 디오라마 모듈 · 역 중심 표현 창
   ├─ 법정동 공간 패키지와 500m 타일 참조
   ├─ 행정동 경계·통계·운영 사본 참조
   ├─ 공통 공간 모형 표현 프로필
   ├─ 선택: 운영 관찰 레이어
   └─ 선택: 방어·기타 gameplay 레이어
```

## 확정

### r7 면목역 1km 비공개 검토 구현 결과

- 공식 역 기준점 중심의 정북 1,000m × 1,000m ENU 창을 실제로 절단했다. 사본 revision은 `myeonmok-station-spatial-snapshot.private-review.r1`, content hash는 `48FED5BFA9B06075B27D7DCF22EFDF3AE3F98222D337D0298C01C098A62B913A`다.
- 동결 원천과 면목역 범위 OSM 1회 수집을 조합해 건물 4,165개, 방향성 도로 link 124개, 표면 48개, 행정동 6개, 500m tile 4개를 만들었다. 높이는 원천 관측 1,616개와 상징적 4m fallback 2,549개를 구분한다.
- 100m coverage cell 100개 중 98개는 건물 근거를 확인했고 `x1:z0`, `x1:z3` 두 셀은 수집 뒤에도 표면 coverage가 부족해 `MissingCoverage`로 남겼다. 법정동 경계, 건물 재배포 권리, 도로 폭과 통행 권위도 각각 결손 코드로 보존한다.
- 로컬 MySQL 비공개 검토 원장에 OSM 원본·영수증과 사본·감사 네 건을 ID 48~51로 저장했다. 같은 입력 재적용 신규 0건과 별도 프로세스 재조회를 확인했다.
- 서버 manifest에는 후보 fingerprint와 수량·결손을 제공하지만 `ServerLoadable=false`와 `WaitingForSpatialCoverage`를 유지한다. 행정동 하나당 current projection 하나인 기존 Mongo 계약에 역 중심 좌표 자료를 게시하면 정상 행정동 사본을 덮어쓸 수 있어, 독립 station-scoped 저장 계약 전에는 게시하지 않는다.
- 공통 Unity 읽기·검사·조립·표현 코드는 로컬 비공개 사본만 메모리에서 소비하도록 구현한다. 실제 Unity 컴파일·EditMode·Play Mode·Game View의 최종 범위는 [구현 기록](../../../../Reports/면목역-1km-디오라마-구현-2026-09-13.md)에 따로 남긴다.

### r13 용마산역 1km 비공개 검토 구현 결과

- 공식 기준점(37.573752, 127.086802) 중심의 정북 1,000m × 1,000m 창을 실제로 절단했다. 사본 revision은 `yongmasan-station-spatial-snapshot.private-review.r1`, content hash는 `43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D`다.
- 동결 원천과 용마산역 bbox OSM 1회 수집을 조합해 건물 2,859개, 방향성 도로 link 116개, 표면 41개, 행정동 4개, 500m tile 4개를 만들었다. 높이는 원천 관측 1,185개와 상징적 4m fallback 1,674개를 구분한다.
- 100m coverage cell 100개는 `ConfirmedBuilt 72 / ConfirmedOpen 6 / IncompleteSurfaceEvidence 12 / MissingCoverage 10`이다. 관계 member 한 건의 불완전, 건물 권리, 법정동 경계, 도로 폭, 표면 coverage와 통행 권위 결손 여섯 종을 보존한다.
- 로컬 MySQL 비공개 검토 원장에 OSM 원본·영수증과 사본·감사 네 건을 ID 58~61로 저장했다. 같은 입력 재적용 신규 0건과 별도 프로세스 재조회를 확인했다.
- 서버 manifest에는 용마산 후보 fingerprint를 결속했지만 `ServerLoadable=false`, 빈 지역·행정동 투영과 `WaitingForSpatialCoverage`를 유지한다. 기존 Mongo 행정동 current projection은 변경하지 않았다.
- Unity 공통 View는 정확한 용마산 OSM 영수증과 사본을 읽어 `LocalPrivateReview / PartialCoverage / Ready`로 조립했다. canonical `SimulationWorldShell` Play Mode에서 16개 chunk·renderer, vertex 88,589개, triangle 47,213개와 결손 10셀을 실제 Game View로 확인했다. Scene은 저장하지 않았고 실행 전후 hash와 clean 상태를 유지했다. 정식 Test Runner의 새 완료 결과는 미확정이며 상세는 [용마산역 구현 기록](../../../../Reports/용마산역-1km-디오라마-구현-2026-09-13.md)에 남긴다.

### r5 구현 승인 범위

- 면목역은 공식 기준점 중심의 정북 1,000m × 1,000m 창으로 구현한다.
- 사가정과 같은 공통 자료 계보·결정성·카메라·성능·결손 판독 원칙을 사용하되 사가정 도형이나 Region ID를 복사하지 않는다.
- 기존 동결 원천을 먼저 재절단하고, 실제로 빠진 면목역 표면 등은 범위가 제한된 공식·공개 원천을 추가 수집한다.
- 수집 뒤에도 없는 도형·관계 member·높이·도로 폭·보도·법정동 coverage는 합성하지 않고 `MissingCoverage` 또는 해당 품질 상태로 남긴다.
- 첫 산출물은 `LocalPrivateReview`, `distributionApproved=false`, `TraversalReady=false`, `GameplayReady=false`다. 서버는 실제 tile payload 검증 뒤에만 `PartialCoverage`를 허용하고, Unity는 읽기 전용 표현만 수행한다.
- 이번 구현은 전투·몬스터·NPC 이동·NavMesh·운영 위치·주문·배차·Scene 신설을 포함하지 않는다.

### 소유권과 식별자

- `world-region:*`은 출시·탐색 묶음, `region:kr:bjd:*`와 `region:kr:hjd:*`는 법정동·행정동 정본으로 유지한다. 역 중심 표현 창은 이 식별자를 대체하지 않는다.
- 기존 `world-region:kr:seoul:jungnang:sagajeong.r1`, `reference:sagajeong-station`, `anchor:sagajeong-station`과 기존 지도·배치 판본은 공간 자산 호환을 위해 보존한다. 이를 새 도시철도역의 `LegacyAliases`로 등록하지 않는다.
- 새 역 이름과 고유 식별자는 공식 역명·운영기관·역 코드·기준 좌표를 확인한 뒤에만 등록한다.
- 모든 역을 별도 Unity Scene으로 만들지 않는다. canonical `SimulationWorldShell` 안에서 선택된 역세권 모듈을 조립·해제한다.
- 도시철도역 식별자는 법정동·행정동·운영지역 식별자와 분리한다. 첫 자료의 안정 식별자는 KRIC 노선번호와 선행 0을 보존한 역번호를 함께 써 `station:kr:kric:{lineNumber}:{stationNumber}`로 만든다. 이 값은 일반 작업대의 `stationStableId`와 혼동하지 않도록 API 계약에서 `TransitStationStableId`로 표기한다.

### 표현 창과 초기 대상

- 일반역의 첫 표준 profile은 역 기준점 중심, 정북 방향, 폭 1,000m × 높이 1,000m다. 면목·사가정·용마산에는 같은 profile을 적용해 자료 coverage와 화면 밀도·성능을 비교한다.
- 대형 환승역만 별도 descriptor에서 가변 폭·높이를 허용한다. 환승역이라는 이유만으로 자동 확대하지 않고, 철도 부지·출구 분산·교차 노선 범위를 확인한 뒤 치수와 사유를 판본화한다.
- 서버의 500m 원천 tile 개수와 Unity의 4×4 표현 chunk는 다른 개념이다. 역 창이 격자 경계와 어긋나면 교차 tile이 4개보다 많을 수 있으며, Unity 표현 단계에서 정확한 1km 창으로 자른다.

### 역 목록과 조회 API

- 서버는 역 정적 검토 대장과 실제 공간 사본을 분리한다. 대장은 역 식별·공식명·운영기관·노선·역번호·WGS84 기준점·표현 profile·출처·품질을 소유하고, 건물·도로 원본은 기존 법정동 공간 패키지와 행정동 500m tile을 참조한다. 대장 등록은 공개·배포·운영 승인과 다르다.
- Unity용 첫 읽기 경로는 `GET api/v1/world/stations`와 `GET api/v1/world/stations/{transitStationStableId}/diorama-manifest`다. 별도 station tile API나 관리자 공간 catalog 공개 경로를 만들지 않는다.
- manifest는 역 정보, 표현 창, 관련 `world-region:*`, 법정동·행정동, 사용 가능한 기존 tile 참조와 projection/hash, 출처·권리·품질·결손 상태를 묶는다. 원자료의 역사 전화번호는 Unity 투영에 포함하지 않는다.
- 공간 투영이 아직 없더라도 역 metadata는 반환하되 `WaitingForSpatialCoverage`로 표시하고 tile은 비운다. 결손을 합성 지도나 사가정 자료로 대체하지 않는다.
- API는 기존 `RegionExperiencePackages` 기능 플래그, 인증, ETag와 private cache 경계를 재사용한다. 모든 첫 후보는 `ObservationPresentationOnly=true`, `TraversalReady=false`, `GameplayReady=false`, `DistributionApproved=false`로 시작한다.
- 지역·행정동 사본은 schema와 정확 식별자가 일치하고, 지리 계층이 기존 manifest/tile `GET` 두 개만 제공하며, 행정동 사본이 관찰 표현 전용·비통행·비gameplay·비배포이고 tile 식별자·격자·수량이 모순되지 않을 때만 연결한다. 경계를 벗어나면 역 metadata는 유지하되 공간 연결은 `WaitingForSpatialCoverage`로 낮춘다.
- 출처 응답은 공식·제공기관 페이지, 원본과 행 hash, 수집시각, 원천 기준일, dataset 수준 이용조건 관측, `PendingHumanReview`와 제한을 함께 보존한다. 용마산은 원천 역명 `용마산(용마폭포공원)`과 사용자 표시명 `용마산역`을 분리하고, 후자는 사용자가 확인한 `UserConfirmedDisplayName`으로 표시한다.

### 첫 공식 역 자료

2026-09-13에 [공공데이터포털 국가철도공단 도시광역철도 역사정보](https://www.data.go.kr/data/15093755/fileData.do)의 연결 파일을 수집했다. 포털 metadata에는 `이용허락범위 제한 없음`이 표시되지만, 내려받은 파일명 판본과 metadata 갱신일이 일치하지 않아 정확 판본의 권리 결속은 계속 사람 검토 대상으로 둔다. 원본 SHA-256은 `cdf1d84a7e5c898b2aacd622783ba8ba9af35c40bee0561dc97d55ce8e063f94`다. 원본 선택·저장·재조회와 제한은 [중랑구 7호선 역 기준자료 수집](../../../../Reports/중랑구-7호선-역-기준자료-수집-2026-09-13.md)에 분리해 기록한다.

| 후보 | 안정 식별자 | 원천 역명 | 역번호 | WGS84 위도·경도 | 원천 기준일 | 상태 |
| --- | --- | --- | --- | --- | --- | --- |
| 면목역 | `station:kr:kric:s1107:0721` | 면목 | `0721` | 37.588671, 127.087503 | 2024-12-31 | 비공개 사본 후보 등록, API는 `WaitingForSpatialCoverage` |
| 사가정역 | `station:kr:kric:s1107:0722` | 사가정 | `0722` | 37.580912, 127.088502 | 2024-12-31 | 기존 사가정·면목제3·8동의 실제 교차 tile만 `PartialCoverage` |
| 용마산역 | `station:kr:kric:s1107:0723` | 용마산(용마폭포공원) | `0723` | 37.573752, 127.086802 | 2024-12-31 | 비공개 사본·로컬 Unity 검증, API는 `WaitingForSpatialCoverage` |

최신 다운로드 파일 안에서도 세 행의 데이터 기준일은 2024-12-31이다. 이를 2026년 현재 현장 실측값이나 출구 중심점으로 해석하지 않는다. 전국 1,099행 중 이 세 행만 로컬 MySQL 비공개 검토 원장에 `OfficialSourcePendingHumanReview`로 저장했고, 공개·운영 권위·Unity 적용은 승인하지 않았다.

2026-09-13 사용자는 앞서 말한 “역마산역”이 용마산역을 뜻한다고 확인했다. 이 확인은 사용자 표시명만 확정하며, 공식 원천명·공간자료 coverage·권리 검토 상태를 바꾸지 않는다.

### 두 번째 실제 적합성 표본: 면목역

2026-09-13 사용자는 사가정 다음 실제 공간자료 적합성 표본을 면목역부터 진행하는 방향을 확인했다. 면목역 공식 기준점의 1km × 1km 창은 대략 경도 `127.0818422~127.0931645`, 위도 `37.5841659~37.5931758`이다. 서울시 동결 행정동 경계와 읽기 전용으로 교차 계산한 결과는 다음과 같다.

| 행정동 | 안정 식별자 | 창 면적 교차 비율 | 비고 |
| --- | --- | ---: | --- |
| 면목본동 | `region:kr:hjd:1126056500` | 74.344853% | 역 기준점 포함 |
| 면목제2동 | `region:kr:hjd:1126052000` | 15.233453% | 부분 교차 |
| 상봉제2동 | `region:kr:hjd:1126059000` | 4.894987% | 면목동 6개 행정동 묶음 밖 |
| 면목제5동 | `region:kr:hjd:1126055000` | 4.382885% | 부분 교차 |
| 면목제3·8동 | `region:kr:hjd:1126057500` | 1.143366% | 부분 교차 |
| 망우제3동 | `region:kr:hjd:1126066000` | 0.000456% | 극소 경계 교차, 사본에는 보존 |

따라서 기존 면목동 6개 행정동 목록을 역 창의 정답으로 재사용하지 않는다. 면목제4동·면목제7동을 자동 포함하지 않고, 상봉제2동·망우제3동을 비롯한 실제 교차 경계를 역 descriptor에서 별도로 판정한다.

현재 사가정 `sagajeong-reference.r3` 지도는 면목역 창의 남쪽 일부, 약 10.6%만 겹친다. 이 지도를 면목역 자료로 이름만 바꾸거나 기존 사가정 Region ID를 면목역에 연결하지 않는다. 현재 Mongo의 면목제3·8동 투영도 사가정 원본에서 만든 부분 사본이므로 면목역 전용 사본이 아니다.

로컬 동결 원천 가운데 서울 건물 ZIP과 전국 표준노드링크 ZIP을 새 다운로드 없이 재절단했고, 면목동 밖 상봉동 법정동 건물 183개도 실제 창에서 함께 선택했다. 건물은 총 4,165개지만 원천 이용조건 충돌 때문에 `LocalPrivateReview`를 유지한다. 표준노드링크는 방향별 link 124개·도로명 9개이며 도로 폭·보도·통행 권위로 사용하지 않는다. 면목역 전용 OSM 표면 후보를 범위 제한 수집해 48개를 보존했지만 두 coverage cell은 여전히 결손이다. 상세 원천 hash·수량·결손은 [구현 기록](../../../../Reports/면목역-1km-디오라마-구현-2026-09-13.md)에 기록한다.

첫 면목 사본은 `private-review.r1` 후보이며 새 API 상태 코드가 아니다. 건물·도로·행정동 경계를 실제 1km 창에서 잘랐고, 수집 뒤에도 없는 법정동 경계·표면 coverage·도로 폭·통행 권위를 `MissingCoverage` 계열 코드로 남겼다. 원천 권리 충돌과 결손이 해소되기 전에는 공개·배포·gameplay·통행으로 승격하지 않는다. 서버 manifest도 station-scoped payload 저장·조회 계약을 만들기 전까지 `WaitingForSpatialCoverage`를 유지한다.

### 공통 기반과 역별 프로필

공통 기반은 다음 책임만 가진다.

1. `StationDioramaDatasetDescriptor` 후보: 역 고유 식별자·표시명·명시적 역/출구 기준점, 표현 창, 좌표계, 참조 공간 패키지·타일, 지도 revision/hash, 원천 영수증과 공개 경계를 기술한다.
2. `StationDioramaPresentationProfile` 후보: chunk, 역 주변 상세 반경, 도로 표현, 건물 massing, 창문·옥상·그림자·수목, 카메라, renderer/material 예산을 기술한다.
3. 공통 Overlay Parser/Composer: 건물·표면·다각형·높이 근거·legacy 결속을 검증하고 전체 성공 또는 안전한 기본 지도 복귀만 허용한다.
4. 공통 Mesh 조립기와 View: 역별 자원 이름이나 사가정 상수를 모르고 descriptor와 profile만 소비한다.
5. 공통 품질 기록: 입력 hash, 결과 hash, coverage, fallback, EditMode, Play Mode·Game View, 성능과 Scene clean 여부를 서로 다른 증거로 남긴다.

역별 프로필은 자기 기준점·표현 창·원천·자료 수량·결손·배포 상태와 필요한 시각 예외만 가진다. 사가정의 건물 4,062개·표면 108개·높이 집계는 사가정 프로필의 근거이지 다른 역의 고정 기대값이 아니다.

### 사가정시장 첫 랜드마크 수집 명세

2026-09-13 후속 기획은 역세권 배경에 대표 랜드마크를 복원 후보로 결속하는 첫 대상을 **사가정시장 입구·대표 골목**으로 정했다. [수집 명세 r6](sagajeong-market-landmark-collection.r6.md)은 현재 가진 점 관측·주소 단일 건물 후보를 입구나 시장 전체 외곽으로 오인하지 않고 다음을 고정한다.

- 현행 공식 시장 행·원본 hash·기준일을 먼저 대조한다.
- 입구 점·방향·주접근로와 골목 중심선/영역·폭·차양 높이·분기를 각각 근거와 정밀도를 갖는 배치 자료로 수집한다.
- 데이터셋 이용조건과 개별 이미지의 사용·변형·상업적 배포 권리를 분리해 판정한다.
- Graph Map에는 `CandidateLandmarkFor`·`DerivedFrom`·`LocatedIn` 관계, 배치맵에는 검증된 좌표·방향·도형·치수만 인계한다.
- 식별·공간·시각 권리·모델 브리프가 승인되기 전에는 Blender·Unity 작업을 열지 않는다.
- 광고·후원은 랜드마크 선정·크기·배치를 변경하지 못하며, 후속 광고 레이어에서만 명시적으로 표시한다.

### 실제 외관 근거 기반 건물 교체 제안

[건물 표현 교체 제안 r8](building-visual-replacement-proposal.r8.md)은 전체 절차적 건물을 버리지 않고 `배경 매스 → 동네 외관 문법 → 검증된 대표 랜드마크`의 세 층으로 고도화한다.

- 도로명주소·건물 도형·건축물대장·항공사진은 식별·좌표·질량 근거로, 직접 촬영 또는 개별 이용권이 확인된 사진은 외관 근거로 분리한다.
- Blender 자원은 안정 건물 식별자·원천 hash·권리·LOD·scale·pivot·bounds를 가진 근거 묶음에서만 만든다.
- Prefab·overlay revision·hash·bounds 검증이 모두 성공한 뒤에만 대상 건물 매스를 숨기며, 실패하면 기존 매스를 유지한다.
- 사가정시장은 입구·골목 overlay 후보로 보존하고, 첫 실제 건물 교체는 독립 polygon 한 동에서 작은 수직 절편으로 검증하는 안을 제안 상태로 둔다.

### 무료 Steam 출시와 사진 이용 정책 제안

[외관 사진·건물 모델 이용 제안 r9](photo-source-and-building-model-use-policy.r9.md)은 현재 무료 출시 의도를 기록하되 `Price=Free`를 자산 이용 허락으로 해석하지 않는다.

- 직접 촬영·명시적 허락·개별 공공누리 제1유형·CC0/CC BY 자료를 배포 가능 후보로 우선한다.
- 거리뷰·기사·블로그·업체 사진과 변경 또는 상업 이용 제한 자료는 권리자가 별도로 허락하기 전까지 로컬 참고 전용으로 둔다.
- 사진 원본 사용, Blender 모델링 참고, 게임 배포, Steam 홍보 사용을 서로 다른 권리 단계로 판정한다.
- 직접 촬영 사진도 얼굴·번호판·세대 정보·벽화·조각·로고와 독창적인 건축 외관을 별도 검토한다.
- r9는 첫 표본의 직접 촬영을 제안했지만 사용자가 어렵다고 확인했으므로 r10의 공공 이용허락 사진 우선 방향이 이 선택을 대체한다.
- 향후 광고·후원 가능성을 보존하기 위해 첫 배포 자원은 가능한 한 `CommercialCompatible=true`를 만족시킨다.

### 공공 이용허락 사진 우선 방향

[공공 사진 우선 수집 r10](public-licensed-photo-collection-direction.r10.md)은 직접 촬영을 첫 경로에서 제외하고 개별 변경·배포 조건을 확인할 수 있는 공공 자료를 우선한다.

- 공공누리 제0·1유형, CC0, 변경·배포 가능한 CC BY와 별도 명시적 허락 자료만 기본 Blender 후보로 둔다.
- 사진이 정확한 건물에 대응하면 `BuildingSpecificVisualEvidence`, 같은 지역의 분위기만 보여주면 `RegionalFacadeGrammarEvidence`로 분리한다.
- 정확한 사가정 사진이 없으면 다른 지역 사진으로 해당 건물을 꾸미지 않고 기존 절차적 매스를 유지한다.
- 첫 검색은 사가정시장 입구·골목 정확 자료와 사가정역 1km 독립 건물 정확 자료를 함께 찾되, 결과의 증거 등급을 섞지 않는다.
- 사진 원본은 로컬 비공개 검토 상태로 두고 원문·개별 라이선스·hash가 확인되기 전에는 Git·Unity 배포 자원에 넣지 않는다.

### 공공 사진 첫 수집 결과

[첫 수집 결과 r11](public-photo-collection-result.r11.md)은 사가정역 정확 사진 5건을 원문·파일별 라이선스·SHA-256과 함께 비공개로 저장했다. 공공영역 3건은 역 내부·표지 근거이고, 출입구 2건은 정확한 외부 구조를 보여주지만 `CC BY-SA 4.0`의 동일조건변경허락과 사람·상가 간판 검토가 남아 있다.

사가정시장 정확 사진은 Commons와 공공누리 제0·1유형 우선 검색에서 확보하지 못했다. 중랑구 공식 소식지는 개별 사진 권리가 불명확하고, 공공누리의 중랑구 후보 3건은 모두 제4유형이므로 원본을 받지 않았다. 검색 결손과 제외 후보도 기존 로컬 MySQL 비공개 검토 원장에 별도 레코드로 남겼으며, Blender·Unity 관문은 계속 닫는다.

### 공공데이터포털 사진 자료 조사

[공공데이터포털 조사 결과 r12](data-go-kr-photo-research-result.r12.md)는 한국관광공사 관광사진 API 안내서와 여행기사 CSV 1,332행, 사가정공원·용마랜드·용마산 공급자 미리보기 3건을 비공개로 수집했다. 정확 범위 여행기사 행은 0건이고 관광사진 API는 현재 키로 HTTP 403이다.

사가정공원은 장소 대응만 확인했으며 개별 이용조건은 미확인이다. 다른 두 사진은 제3자 표기 또는 제4유형 문맥으로 제외했다. 로컬 MySQL에는 6건을 검토 보류로 저장했지만 모델링 승인 사진은 0건이므로 기존 절차적 매스를 유지한다.

### 레이어 구성

- 공간 모형 기반은 건물·도로·표면·높이 근거·카메라처럼 관찰에 필요한 중립 표현만 소유한다.
- 운영 업무 사본은 선택형 운영 관찰 레이어다. 운영 원장·GPS·개인정보를 역세권 모형 자산에 저장하지 않는다.
- 방어 준비·몬스터·옥상 배치·피난과 같은 gameplay는 선택형 gameplay 레이어다. 방어 기능이 없는 역도 같은 공간 모형 기반을 사용할 수 있어야 한다.
- 역마다 Unity layer 번호를 늘리지 않는다. 한 번에 활성화된 역세권 표현은 공용 관찰 layer와 단일 활성 모듈 정책을 사용하고, 인접 지역 동시 표시는 타일 조합으로 해결한다.

### 품질·검증 관문

- 새 역마다 출처 URL, 기준 시각, 이용 조건, 원본 SHA-256, 파생 asset SHA-256, 좌표계와 검토 상태를 독립 검증한다.
- `MissingCoverage`는 공터로, 높이 fallback은 실제 높이로 해석하지 않는다. 결손은 보존하고 화면·감사 결과에 별도 상태로 남긴다.
- 같은 입력의 결정성, 교차 역 asset 거부, 잘못된 revision/hash/좌표계 거부, 부분 적용 방지와 fallback을 자동 시험한다.
- 사가정 초기 성능 상한인 4×4 chunk, renderer 128 이하, material 12 이하를 첫 비교 기준으로 사용하되 역별 profile 값과 실제 Game View 결과를 구분한다.
- 전체 보기와 역 주변 확대 보기, 카메라 framing, 공간 밀도·높이 차·도로 위계 판독, Scene 비변경과 모듈 전환 뒤 자원 회수를 실제 Unity에서 확인한다.
- 두 번째 합성 역 fixture로 중심 밖 역 기준점, 다른 표현 창과 profile, 교차 자료 거부, 모듈 전환 시 카메라·layer 누출 방지를 검증하기 전에는 범용 표준 구현 완료를 선언하지 않는다.

## 구현 순서 제안

1. 공식 역 자료를 원본 hash와 함께 비공개 검토 원장에 저장하고 동일 입력 재적용·독립 재조회를 확인한다.
2. 역 목록·역별 manifest의 얇은 서버 조회 절편을 만들고, station-scoped 저장 계약이 없는 면목·용마산은 비공개 사본 후보가 있어도 서버에서는 metadata-only로 둔다.
3. 사가정 r18 결과·hash·Game View·성능을 첫 적합성 기준으로 동결한다.
4. 동작을 바꾸지 않고 descriptor와 presentation profile을 도입하고, 기존 사가정 이름·계약에는 호환 adapter를 둔다.
5. Overlay 계약·Loader·Composer의 검증 알고리즘과 원천 allowlist를 분리한다.
6. Mesh 조립·카메라·조명·후처리·성능 측정을 공통 View 책임으로 분리한다.
7. 합성 두 번째 역 fixture로 범용성을 먼저 시험한 뒤, 면목역과 용마산역의 독립 `private-review.r1`을 실제 적합성 표본으로 만든다.
8. 면목 전용 Region manifest를 만들기 전에 tile summary뿐 아니라 실제 tile payload·행정동 경계·projection hash·도형 교차를 대조하는 서버 검증을 추가한다.
9. 이후 사가정에서 발견한 개선은 공통 규칙인지 사가정 profile 예외인지 먼저 분류해 반영한다.

## 미정

- 환승역의 대표 기준점을 역 중심 하나로 둘지 출구·승강장 기준점 묶음으로 둘지
- 면목역의 법정동 바깥 건물 coverage와 표면 다각형 원천·이용조건, 망우제3동 극소 경계 교차의 정밀 판정
- 면목·용마산 비공개 역 사본을 기존 행정동 current projection과 분리해 제공할 station-scoped 저장·조회 계약
- 사가정·면목·용마산 공통 View를 한 Scene에서 전환할 역 선택 UI와 `OnGUI` 공존 배치
- 공통 프로필의 초기 시각 허용 범위와 저사양 기기 성능 예산
- 첫 실제 건물 교체 표본의 정확한 건물·사진 권리·자원 예산
- 관광사진 API 활용 범위 확보와 개별 항목의 공공누리 유형 재조회
- 무료 출시 뒤 광고·후원·유료 부가 콘텐츠를 허용할지와 그에 따른 자산 권리 상한

## 다음 질문 하나

공공데이터포털에서 한국관광공사 관광사진 API 활용 신청을 진행해 개별 사진의 이용조건까지 조회할까?

추천은 신청을 진행하되 승인되기 전까지 사진을 비공개 검토 상태로 유지하는 것이다. 제출은 외부 서비스에 의사를 전달하므로 사용자 확인과 로그인된 브라우저가 준비된 시점에 별도 수행한다. 기존 `CC BY-SA` 정책 질문은 후속으로 보존한다.

## 현재 검증 상한

공식 XLSX와 metadata를 로컬 비공개로 수집하고 schema·대상 3행·결정성 self-test 8건을 통과했다. 로컬 MySQL `hongdal_dev`에 원본 1건과 정규화 3건을 저장해 첫 적용 3건 삽입, 재적용 신규 0·기존 3, 별도 연결 재조회 3건을 확인했다. 서버 공용 계약·검토 대장·조회 UseCase·인증 Controller·DI를 구현했고, 출처 계보·1km 비격자 교차 tile·잘못된 지역/행정동 권위·중복 tile·정의 불일치·400/404·ETag·DI를 포함한 집중 시험 13/13을 통과했다. 전체 이번 경로 14개를 지정한 최종 Fast도 계약·서버·시험 project build, 코드 지도, Simulation 관련 221/221과 서버 관련 121/121을 통과했다(`artifacts/local/validation/20260913-112827`). Task는 두 solution build 경고·오류 0과 Simulation 전체 1,913/1,913을 통과했고 서버 전체는 5,210건 중 5,203건 통과·기존 작업트리 관련 7건 실패로 완료 관문이 닫히지 않았다(`artifacts/local/validation/20260913-113110`). 사용자 표시명 확인 반영 뒤 변경 7개 경로 한정 Fast에서 diff·생성 지도, 계약·서버·시험 build와 집중 시험 13/13이 다시 통과했다(`artifacts/local/validation/20260913-114129`). Hosted HTTP와 공개·운영 사용은 수행하지 않았다. 이후의 Unity 공통 리팩터링·실제 공간 사본 조립·Play Mode·Game View는 아래 역별 후속 결과가 소유한다. 관련 코드·기획·증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다.

후속 면목역 구현에서는 독립 1km 사본을 만들고 건물 4,165개·방향별 도로 link 124개·표면 48개·행정동 교차 6개를 결속했다. 100m cell 100개 중 두 셀의 `MissingCoverage`와 권리·도로 폭·통행 권위 결손을 유지한 채 로컬 MySQL 비공개 원본 저장·별도 재조회와 Unity parser·Mesh 교차 검사를 완료했다. canonical `SimulationWorldShell` Play Mode의 임시 View로 실제 Game View까지 확인했지만 Scene 영속 결속, 사가정·면목 overlay 공존 배치, 정식 Unity Test Runner 새 결과는 아직 남아 있다. 서버 payload는 station-scoped 저장 계약 전까지 계속 `WaitingForSpatialCoverage`다.

후속 용마산역 구현에서는 공식 기준점 중심의 bbox `127.0811416,37.5692469,127.0924624,37.5782568`를 수집·동결하고 건물 2,859개·도로 116개·표면 41개·행정동 4개를 결속했다. coverage 100셀 중 10셀과 여섯 결손 코드를 유지했으며, 로컬 MySQL ID 58~61의 재적용 신규 0·독립 재조회와 서버 집중 시험 13/13·Controller 관련 17/17을 확인했다. Unity 생성 project는 오류 0으로 빌드됐고 compiled fixture 4/4와 canonical `SimulationWorldShell` 실제 Play Mode·Game View에서 16 renderer·88,589 vertex·47,213 triangle을 확인했다. 정식 Test Runner는 시험 원본 23 case를 확인했으나 이번 새 완료 결과가 없어 미확정이다. Scene은 저장하지 않았고 실행 전후 SHA-256 동일·dirty false다. 서버는 독립 station-scoped 계약 전까지 `ServerLoadable=false`와 `WaitingForSpatialCoverage`를 유지한다.

사가정시장은 기존 2025-11-10 점 관측·원본/행 hash·주소 단일 건물 후보를 수집 명세에 분리 결속했다. 명세와 기계 후보 대장만 작성했으며, 현행 원본 다운로드·사진 취득·현장/원격 공간 확인·Graph Map·배치맵·Blender·Unity는 아직 미실행이다.

실제 외관 교체는 저장소·Blender 경로의 정적 구조와 공식 자료·이용조건을 조사해 제안으로만 추가했다. 후보 건물 선정·사진 수집·모델링·FBX·Prefab·Scene·Play Mode·Game View는 수행하지 않았다.

무료 Steam 사진 이용 정책은 공식 저작권법·한국저작권위원회·공공누리·Steamworks 안내를 대조해 제안으로만 추가했다. 개별 사진·건물에 대한 이용 허락이나 법률 판단, 사진 다운로드·직접 촬영·권리자 문의는 수행하지 않았다.

사용자가 직접 촬영이 어렵다고 확인해 공공누리 제0·1유형 등 공공 이용허락 사진 우선 방향으로 대체했다. 사가정역 정확 사진 5건을 비공개 수집해 원문·라이선스·hash를 보존했고, 로컬 MySQL에 사진 후보 5·시장 결손 1·공공누리 제4유형 제외 3건을 저장했다. 첫 적용 신규 9, 재적용 신규 0·기존 9, 독립 재조회 9를 확인했다. 사가정시장 정확 사진, CC BY-SA 프로젝트 정책, 내용 승인, Blender·Unity·Scene·배포는 아직 완료되지 않았다.

공공데이터포털 후속 조사에서는 공식 안내서·여행기사 CSV와 공급자 미리보기 3건을 비공개 수집하고 로컬 MySQL에 6건을 저장했다. 자체 시험 17건, 첫 적용 신규 6, 재적용 신규 0·기존 6, 독립 재조회 6을 확인했다. 관광사진 API는 HTTP 403이고 개별 권리 확인이 끝나지 않아 Blender·Unity·배포 관문은 닫혀 있다.
