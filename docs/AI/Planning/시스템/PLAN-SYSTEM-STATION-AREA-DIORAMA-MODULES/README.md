[기획 · 시스템·월드 투영 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r29]

# 역세권 디오라마 모듈 표준

- 상태: `ApprovedScopedImplementation / StandardizationDirectionConfirmed / InitialOneKilometerWindowConfirmed / MajorTransferVariableWindowAllowed / InitialStationSourceStored / ServerFirstSliceImplemented / ReadOnlyBindingSafetyTested / YongmasanUserNameConfirmed / SecondActualSampleMyeonmokConfirmed / MyeonmokLocalPrivateReviewImplemented / YongmasanLocalPrivateReviewImplemented / MissingCoverageRetained / UnityParserMeshCrossCheckPassed / UnityPlayModeGameViewVerified / UnityOfficialRunnerPending / UnitySceneBindingPending / StationScopedProjectionPending / SagajeongMarketLandmarkCollectionSpecificationApproved / BuildingVisualReplacementDirectionConfirmed / FirstReplacementBuildingPending / FreeSteamReleaseIntentRecorded / PhotoUsePolicyApproved / OwnCaptureUnavailable / KOGLType0Or1PriorityConfirmed / FirstPublicPhotoSliceStored / ExactMarketPhotoMissing / CCBYSAProfilePending / HWiAreaSetSkyModularAlignmentRequested / APlusH3PartialAreaSetConfirmed / BuildingHierarchySelectionIntentConfirmed / R15ReadOnlySelectionImplemented / BuildingRoleProfileDirectionConfirmed / R16RoleCardsImplemented / R17SagajeongCommonModuleHostImplemented / R18MobilityAdapterSeamImplemented / R18AutomatedTestsPassed / R18SceneBindingDeferred / R18GameViewNotRequiredForUnchangedOutput / UserMouseUpInjectionDeferred / R19PublicDataLifeObservationProposed / R19ImplementationNotApproved / R20SyntheticCourierObservationApproved / R20ProjectionImplemented / R20UnityRuntimeOnlyImplemented / R20AutomatedTestsPassed / R20PlayModeGameViewVerified / R20SceneBindingDeferred / R20EvidencePromotionNotPerformed / R23DioramaEvidenceEvolutionImplemented / R24JungnangMarketVisualPrivateReviewCollected / R24MarketIdentityPending / R24ItemLevelRightsPending / R24BlenderAndUnityBlocked / R25PlacementAndHeightRealityBaselineConfirmed / R25OrientationLandmarkDetailDirectionConfirmed / R25GenericFacadeExactnessNotRequired / R26SagajeongStationExitAnchorsApproved / R26SourceProjectionImplemented / R26UnityReadOnlyMarkersImplemented / R26AutomatedTestsPassed / R26CanonicalGameViewPending / R26EvidencePromotionNotPerformed / R27SagajeongSpatialSupplementCollected / R27LocalLedgerApplied / R27ParkGeometryDeferred / R27UnityBindingDeferred / R27EvidencePromotionNotPerformed / R28AddressParcelEvidenceChecklistConfirmed / R28SagajeongPrivateAddressLedgerValidated / R28ParcelGeometryMissing / R28EvidencePromotionNotPerformed`
- r29 추가 상태: `ParcelGeometryCollectionApproved / OfficialAlD002ContractConfirmed / ExtractionPipelineValidated / VWorldArchiveBlockedExternalAccess / ParcelGeometryStillMissing / R29EvidencePromotionNotPerformed`
- 기획 근거: 2026-09-13 사용자 제안 “사가정역에서 만든 수준을 표준화하고, 사가정을 더 깊게 만든 결과를 다른 역에도 재사용한다.”와 후속 답변 “사가정·면목·역마산은 역 중심 1km × 1km로 하고 대형 환승역부터 가변 범위를 허용하며, 서버가 역별 정보를 관리·조회하게 한다.”, “역마산역”은 용마산역을 뜻한다는 확인, 두 번째 실제 공간자료 적합성 표본을 면목역부터 진행한다는 확인, 그리고 “사가정역과 유사하게 면목역도 1km × 1km로 표현하고, 자료가 없으면 수집한 뒤에도 없으면 결손을 남긴 채 구현한다”는 범위 승인. 이어서 용마산역도 같은 1km 범위로 자료를 조사하고 Unity 장면에 드러내 달라는 구현 승인을 받았다. 후속으로 역별 랜드마크를 공식·시각·공간 근거로 수집해 Blender 모델과 배치맵에 연결하는 방향, 첫 대상을 사가정시장 입구·대표 골목으로 두고 자료 수집 명세부터 만드는 범위를 확인했다. 도로명주소·건축물대장·외관 사진을 분리 수집하고, 권리가 확인된 대표 건물만 Blender 자원으로 만들어 기존 절차적 모형을 안전하게 교체하는 제안을 추가했다. Steam 기본 게임은 무료 출시 의도를 유지하되 사진·건물·플랫폼 배포 권리를 별도 판정한다. 직접 촬영은 어렵다는 조건과 공공누리 등 변경·배포 조건이 확인되는 공공 사진을 현재 우선 경로로 삼는 방향을 확정함.
- r15 보완 근거: 2026-09-14 사용자는 사가정 A+를 H3 생활 회랑 2개와 부분 AreaSet 구성 후보로 관리하는 안을 확정했다. 기존 사가정 디오라마의 현실 공간 표현을 훼손하지 않고, 건물을 클릭하면 그 건물의 공간 근거와 H1·H2·H3·부분 AreaSet 결속을 확인하며 이후 준비된 WI 상호작용으로 확장할 수 있는 모듈식 구조를 요청하고 이 범위의 구현을 승인했다.
- r16 보완 근거: 2026-09-14 사용자는 음식점·창고·주거·일반을 H 단계가 아닌 건물 역할 Profile로 분리하고 한 건물에 여러 역할이 들어갈 수 있게 하는 제안을 승인했다. 역할별 카드와 해당 건물에 명시 결속된 음식배달 주문·기사 흐름의 읽기 전용 후속 보기도 함께 승인했다.
- r17 보완 근거: 2026-09-14 사용자는 발전한 사가정역 1km 디오라마를 참조 구현으로 삼아 공통단을 만들고 후속 역의 구현 부담을 줄이는 표준화를 요청했다. 면목역·용마산역을 이번 범위에서 새로 구현하지 않고 사가정 기존 결과를 해치지 않는 조건을 유지했다.
- r18 보완 근거: 2026-09-14 사용자는 기존 사가정 결과를 그대로 두고 안전하게 분리할 수 있는 부분만 리팩터링하도록 요청했다. 이에 첫 후속 절편을 한 번에 기존 Mobility 표현 하나만 선택하는 공통 Adapter 이음부와 Host 원자적 수명 보강으로 제한했다.
- r19 보완 근거: 2026-09-14 사용자는 사가정 1km 안에서 활용 가능한 공공자료를 영역별 표시 그룹으로 켜고 끄며, 주민·차량이 경계 밖에서 들어오고 나가고, 합성 Actor를 선택해 직업·업무·건수·가상 정산을 읽는 생활상을 제안서로 먼저 정리해 달라고 요청했다. [공공데이터·생활 관찰 심화 제안 r19](sagajeong-public-data-life-observation.proposal.r19.md)은 자료 원천 분류·실행 모듈·표시 그룹을 분리하고 구현·수집·DB·Unity 권한을 열지 않는다.
- r20 보완 근거: 2026-09-14 사용자는 공공 통계는 밀도 근거로만 쓰고 개별 별칭·직업·이동·건수·모의 지급은 합성 Simulation이 소유하는 추천안을 확정한 뒤, 사가정을 더 깊게 만드는 첫 구현을 요청했다. [합성 배달 기사 관찰 승인 구현 r20](sagajeong-synthetic-courier-observation.implementation.r20.md)은 기존 단일 합성 음식 배달 기사 한 명의 읽기 전용 투영·선택 카드·표시 토글과 runtime-only 검증만 연다.
- r24 보완 근거: 2026-09-14 사용자는 중랑구의 우림·동부·면목·동원·사가정시장을 디오라마 랜드마크로 발전시키기 위해 시장별 이미지와 Blender 모델링 참고 자료를 먼저 수집해 달라고 요청했다. [중랑구 전통시장 시각·모델링 자료 첫 수집 r24](jungnang-traditional-market-visual-collection.implementation.r24.md)는 공식 소식지 시장별 패널 5장과 현행 공식 행 후보 7개를 비공개 검토 원장에 결속하되, 이름·주소 변화와 개별 사진 권리 미확인을 보존해 Blender·Unity·배포를 열지 않는다.
- r25 보완 근거: 2026-09-14 사용자는 역세권 전체 건물의 외관을 사진처럼 복원하기보다 자료로 확인한 배치·높이를 현실 기준선으로 유지하고, 역 번호별 출구·공원·시장·공공시설처럼 지역에서 방향을 잡아 주는 기준점만 선택적으로 세밀하게 표현하는 방향을 확정했다. 일반 건물은 자기 외곽·높이·도로 관계를 보존하되 외관은 근거 수준에 맞춘 동네 문법으로 표현하고, 확인하지 않은 실제 외관을 주장하지 않는다.
- r26 보완 근거: 2026-09-14 사용자는 사가정역 디오라마의 첫 방향 기준점을 번호별 출구로 정하고 구현을 허용했다. [출구 방향 기준점 첫 구현 r26](sagajeong-station-exit-orientation-anchors.implementation.r26.md)은 동결 OSM 사본의 1~4번 출구를 기존 지도 hash와 같은 ENU 좌표계에 결속하고, Collider·통행·상호작용·운영 권위가 없는 일반화 절차적 표식으로만 표현한다.
- r27 보완 근거: 2026-09-14 사용자는 기존 사가정 자료를 인지한 뒤 필요한 보충 자료를 계획하고 구현하도록 승인했다. [공간 보충 자료 수집·원장화 r27](sagajeong-spatial-supplement.implementation.r27.md)은 보행망·엘리베이터·공원·버스 정류소·가로수 공식 원본을 기존 1km 창과 결속해 3,226건을 비공개 검토 원장에 저장했다. 공원 EPSG:5174 도형 변환, NGII 로그인 자료, Unity 연결은 완료로 간주하지 않는다.
- r28 보완 근거: 2026-09-14 사용자는 디오라마를 구성할 때 도로명주소·필지 식별자·실제 필지 경계 도형을 수집했는지 각각 확인하는 관문을 증거 체계에 포함하고 확정하도록 요청했다. [사가정 화면 건물 결속·주소 증거 구현 r6](../PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION/implementation.r6.md)은 이 세 항목을 서로 대체할 수 없는 필수 검사로 만들고, 사가정의 주소·PNU는 수집됐지만 필지 도형은 미수집이라는 상태를 보존한다.
- r29 보완 근거: 2026-09-14 사용자는 실제 필지 경계 도형 수집을 먼저 진행하도록 확정했다. 브이월드 로그인이 불가능하다는 후속 조건에 따라 [필지 경계 도형 수집 준비 r7](../PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION/parcel-geometry-collection.implementation.r7.md)은 공식 `AL_D002`·A1 PNU·EPSG:5186 계약, 3,774개 target, 추출·검증 도구를 준비하되 실제 원본 0건과 `BlockedExternalAccess`를 유지한다.
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

### r14 H·WI·AreaSet·Sky 조화형 모듈 경계

H는 Unity Hierarchy나 layer 번호가 아니라 공간의 의미 포함 깊이다. 1km 역 중심 표현 창, 건물·도로·차선·신호·행정 경계와 원본 hash는 H 계층 밖의 공간 근거로 유지한다. Actor가 실제로 기다리고 이동하고 픽업·전달·양보·휴식하는 의미 지점만 H에 결속한다.

```text
SimulationWorldShell
├─ WorldShared
│  └─ WorldAtmosphere 상태 사본
│     └─ Sky Engine 공통 투영
│        ├─ 기존 World 표현 대상
│        └─ 활성 역세권 환경 표현 대상 후보
└─ ActiveStationDioramaHost (r17 코드, 저장 Scene 미결속)
   ├─ DataEvidenceModule       역·행정동·건물·도로·원본 hash
   ├─ SpatialMeaningModule     H1 → H2 → H3 → H4/AreaSet 부분 구성
   ├─ MobilityModule           차량·보행 그래프·횡단·공유 골목 점유
   ├─ InteractionModule        기존 WI·Task·guard·ActionRecord
   ├─ LifeSimulationModule     합성 NPC 일과·업무·휴식 상태 사본
   ├─ BusinessOverlayModule    업체·광고·후원 표시
   └─ PresentationModule       Mesh·배우·UI·pool·카메라·진단
```

- `ActiveStationDioramaHost`는 r14 설계 후보에서 r17 공통 코드로 구현됐지만 저장 Scene에는 결속하지 않았다. 환경 표현 대상은 계속 설계 후보다.
- `AreaSet`은 H4와 H5 사이에 추가하는 새 단계가 아니라 H4 지역 의도를 H3·H2 역할 슬롯과 연결로 번역하는 구성 계약이다.
- 사가정 A+의 약 339.808m 역세권 순환과 약 302.557m 이면도로·골목 순환은 각각 H3 경관 후보로 관리한다. 1km 창이나 753.060m라는 거리 자체가 H3를 성립시키지는 않으며, 시작·행동·결과·귀환과 WI 인계가 닫혀야 한다.
- 첫 H1 후보는 역 출입·신호 대기와 횡단, 음식점 픽업, 전달·수령, 공유 골목 양보, 근무·휴식 지점이다. 모든 건물과 도로 선분을 H1으로 만들지 않는다.
- H2는 역 교차로, 생활 상권, 저층 주거와 공유 골목 블록 후보로 묶는다. 기존 `h2-candidate:lowrise-residential`, `h2-candidate:market-life-commerce`, `h2-candidate:town-residential-alley`, `h2-candidate:town-order-fulfillment`은 역할이 실제로 맞는 항목만 참고한다.
- 기존 `h3-candidate:town-resident-service-loop`와 `h3-candidate:town-market-fulfillment`도 구조 참고 후보다. Scenario 상대 좌표와 실제 사가정 좌표, 기존 Town 의미와 역세권 현실 근거를 복사하지 않는다.
- 현행 Town 구성은 `LowriseMarket`, `CircularMarket`, `ContaminationRelief`, `ResidentService`, `MarketFulfillment` 다섯 역할 슬롯을 요구한다. A+ 첫 범위는 그중 일부만 다루므로 완성 H4, Actual E5 또는 gameplay-ready AreaSet으로 부르지 않고 `부분 AreaSet 구성 후보`로 유지한다.
- H5는 한 역세권 모듈에 요구하지 않는다. 여러 역 AreaSet과 실제 역간 회랑을 한 Scenario 세계 배치로 묶을 때 별도 검토한다.

### 기존 WI 재사용과 새 WI 억제

- 생활 근무·휴식은 `WI-CITY-SYNTHETIC-LIFE-SHIFT`, `WI-CITY-SYNTHETIC-LIFE-REST`를 재사용한다.
- 음식점 수락·조리는 `WI-CITY-RESTAURANT-ACCEPT`, `WI-CITY-RESTAURANT-COOK`을 재사용한다.
- 배달은 `WI-CITY-SYNTHETIC-ASSIGN → MOVE → PICKUP → DELIVER → RECEIVE → RETURN`의 기존 직접 결과를 보존한다.
- 신호 대기, 횡단 대기, 골목 양보와 매 프레임 보행은 새 WI가 아니다. `MOVE`의 Task·guard·blocked reason과 공용 이동·점유 정책으로 기록한다.
- 주변 통근자·산책자·통과 보행자는 합성 관찰 프로필이며 WI, ActionRecord, 주문·수요·평판·재고·성장 상태를 만들지 않는다. 이들의 행동이 실제 권위 결과를 가져야 할 때만 주체·목표·비용·실패·회복·귀환을 승인한 새 WI를 검토한다.
- 실제 OSM·공공 신호·횡단보도는 공간 후보와 관찰 근거다. `TraversalReady=false` 동안 WI 실행 조건이나 성공 증거로 사용하지 않는다.

### Sky Engine 재사용 경계

- Sky Engine은 H나 AreaSet의 자식이 아니라 `SimulationWorldShell`에 한 번 존재하는 세계 공통 대기 표현 계층이다. AreaSet은 `environmentProfileRef` 같은 참조만 갖고 시간·날씨를 결정하지 않는다.
- 현행 사가정 카메라는 전용 layer와 고정 배경·표현광을 사용하고, 현행 Sky는 Nature Controller와 첫 Nature 대기 프로필에 직접 결속돼 있어 사가정 날씨가 실제로 연결된 상태가 아니다.
- 기존 Nature 프로필 `world-atmosphere:nature-night-day2.fixture.r1`을 사가정용으로 이름만 바꾸지 않는다. 상태 사본·결정성·Save/Replay·Projector 계약을 재사용하고 사가정 합성 시간·대기 프로필은 별도 승인한다.
- 첫 리팩터링 후보는 Nature 종속 없는 Shell 수준 대기 상태 공급원과 여러 표현 대상에 같은 모델을 전달하는 `WorldEnvironmentPresentationHub`다. 기존 Nature Adapter를 보존하고 사가정은 전용 카메라 배경·안개·layer 광원·구름·강수의 대상 Adapter만 가진다.
- 첫 단계의 날씨는 조명·안개·구름·비·번개·음향 표현만 바꾸며 WI 결과·NPC 일정·이동속도·경로 선택을 바꾸지 않는다. 우산·처마 대기나 날씨 경로 비용은 별도 WI·Simulation 규칙·Save/Replay 승인 뒤에만 연다.
- 동일 Session의 Nature와 사가정은 같은 WorldAtmosphere를 읽는다. 서로 다른 날씨가 필요한 독립 관찰은 AreaSet별 권위 날씨를 만들지 않고 별도 Session 또는 동결 표현 preset으로 분리한다.

### 리팩터링 선행 정합성 관문

- 현재 AreaSet 구성 패턴 검사는 기준 4·변형 4·H3 배치 32·폐루프 닫힘으로 통과한다.
- 현재 공간 계층 검사는 생성 문서 드리프트 `GeneratedDocumentOutOfDate`, 게임 기획 주도 H 재고 검사는 수요 수량 드리프트 `DemandH2TargetMismatch`로 실패한다. 각 생성기 소유 범위에서 해소하기 전 사가정 H 등록이나 hash 재봉인을 수행하지 않는다.
- 현행 생활 기록은 `GoingHome`, `Eating`, `HomeRest`, `Sleeping`까지 `LIFE-SHIFT/REST` ActionRecord로 기록할 수 있으나 WI 정본의 직접 결과는 `Working`과 `Resting`이다. 나머지는 생활 상태 사본으로 분리하는 정합성 보완이 선행돼야 한다.
- `synthetic-delivery-move` 작업 명세는 직접 결과를 `Arrived`로 쓰지만 WI 정본은 `PositionAdvanced`다. WI 의미를 바꾸지 않고 작업 명세 용어를 정본에 맞춘다.
- r15는 아래의 읽기 전용 선택 절편만 승인한다. 위 차단 해소와 실제 H 등록은 Graph Map 인계와 별도 구현 작업 명세 뒤 수행하며, 이번 절편은 생성 대장·Scene을 수정하거나 증거 단계를 승격하지 않는다.

### r15 건물 선택형 H 정보 모듈

첫 구현은 기존 사가정 디오라마를 재조립하지 않고 그 위에 선택·조회 계층만 추가한다. `SagajeongReference.json` r3의 건물 602개·도로 2,397개, 지도 hash, `sagajeong-spatial-density-presentation.r18` Mesh, 카메라 회전·전체 보기 크기, 조명·색보정과 Renderer·Material 예산을 회귀 기준으로 동결한다.

- 건물 선택은 602개 건물에 Collider나 개별 GameObject를 추가하지 않는다. 동결 건물 외곽을 읽는 CPU hit-test가 선택 지점과 가장 먼저 만나는 건물 프리즘을 판정한다.
- 선택 결과는 원본 건물 고유 식별자, 공개된 도로명·건물번호, 건물 종류·표현 높이 근거와 명시적으로 등록된 `H1 → H2 → H3 → 부분 AreaSet` 후보 결속을 읽기 전용 카드로 보여 준다.
- 첫 결속은 기존 합성 의미 위치가 건물 윤곽 안에 확인된 `osm:way:1256772531` 음식점·픽업 후보와 `osm:way:1256772606` 주거·전달 후보 두 곳뿐이다. 모든 건물을 억지로 H1으로 승격하지 않는다.
- H2·H3는 확정 행정 경계나 실제 입주·건물 용도가 아니라 명시된 `CandidateMeaningRef`다. 첫 구현은 거리·주소·건물 종류로 소속을 자동 추론하지 않으며 나머지 건물은 `공간 근거만 있음 / WaitingForGraphMapBinding`으로 남긴다. 동결 회랑과 중심·종류 기반 자동 그룹은 별도 판본화된 결속 대장과 사람 검토가 생긴 뒤의 후속 후보로 보존한다.
- 부분 AreaSet 카드는 `ActualE5=false`, `TraversalReady=false`, `GameplayReady=false`, `serverProjectionReady=false`를 명시한다. 클릭과 카드 닫기는 WorldRevision·업무 상태·WI·ActionRecord를 바꾸지 않는다.
- 선택 상태는 View를 닫거나 전체 보기·다른 도로·업무 표식을 선택하면 제거한다. 선택 전에는 기존 화면에 새 경계색·반투명 덮개·건물 변형을 상시 표시하지 않는다.
- 후속 상호작용은 카드가 H를 직접 편집하는 방식이 아니라, 선택 건물에 결속되고 준비된 기존 WI의 조회·Preview 진입점을 노출하는 방식으로 추가한다. 준비되지 않은 WI 버튼은 만들지 않는다.
- [건물 H 선택 구현 명세](../../공간/PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC/building-hierarchy-selection.implementation.r6.md)가 첫 비침습 수직 절편과 검증 상한을 소유한다.

### r17 사가정 참조형 공통 모듈 Host

[구현 명세](station-module-standardization.implementation.r17.md)와 [E7 수직 작업 명세](station-module-standardization.e7-work-order.json)에 따라 r14의 `ActiveStationDioramaHost` 후보를 첫 코드 절편으로 구현했다.

- 공통 Profile은 `DataEvidence → BaseSpatialPresentation → SpatialMeaning → Mobility → Interaction → LifeSimulation → BusinessOverlay → EnvironmentPresentation` 여덟 모듈의 순서·필수 여부·의존성을 정의한다.
- 공통 Planner는 Adapter가 보고한 `Ready / PrivateReview / Blocked / NotProvided / BlockedByDependency`를 검증하고 같은 입력에서 같은 조립 hash를 만든다. `PrivateReview`는 관찰 가능 상태일 뿐 공개·통행·gameplay 준비가 아니다.
- `ActiveStationDioramaHost`는 canonical `SimulationWorldShell` 안에서 준비된 역 Adapter 하나만 표시하고, 필수 자료·표현 모듈 결손과 등록되지 않은 역을 거부한다. 다른 역 자료로 대체하지 않는다.
- 기존 `사가정운영디오라마View`는 첫 호환 Adapter로서 자료 근거·중립 공간 표현·H 의미·읽기 전용 상호작용 모듈만 제공한다. 이동·생활·업체 표시·환경 모듈은 기존 개별 코드가 있어도 아직 Host Adapter로 결속하지 않고 `WaitingForModuleAdapter`로 남긴다.
- 활성 모듈 Catalog에는 사가정만 등록했다. 기존 면목·용마산의 비공개 자료와 공통 공간 표현 코드는 보존했지만 이번 r17에서 새 View·Scene·활성 Profile을 만들지 않았다.
- Scene·Prefab·MeshBuilder·카메라·색·자료 asset을 바꾸지 않고 메인 파일과 동일한 격리 Unity 소스에서 신규 9건과 기존 사가정 회귀 75건, 합계 84/84를 통과했다. 위치 독립 factory가 후속 역의 식별자·표시명·판본만으로 같은 구조를 만들되 활성 Catalog에 등록하지 않는 범위도 확인했다. Play Mode·Game View는 새 시각 출력이 없어 재실행하지 않았다.

### r18 사가정 Mobility Adapter 보존형 리팩터링

[구현 명세](mobility-module-adapter-refactor.implementation.r18.md)와 [E7 수직 작업 명세](mobility-module-adapter-refactor.e7-work-order.json)에 따라 r17 Host 뒤에 첫 단일 모듈 Adapter 이음부를 추가했다.

- `StationDioramaModuleLayerRuntimeStateFactory`와 `StationDioramaSingleLayerAdapter`가 역별 단일 contribution의 상태 생성, 등록·해제, 숨김·원래 `Behaviour.enabled` 복원을 공통 처리한다.
- `사가정MobilityModuleAdapter`는 기존 저밀도 교통 또는 기존 음식배달 Journey 중 조립부가 고른 표현 하나만 `Mobility`로 보고한다. 두 표현은 같은 배달 기사·경로를 중복 그릴 수 있으므로 동시 지정은 `StationDioramaModuleSourceAmbiguous`로 거부한다.
- 저밀도 교통의 조립 판본은 매 Tick의 적용 revision이 아니라 검증된 이동 그래프·차로·신호·교통 프로필·배달 표시 경로 판본 조합으로 고정한다. 새 상태 사본을 적용한 조립부는 `RefreshModuleState()`를 명시적으로 호출해 `Blocked → PrivateReview`와 표시 여부를 다시 계산한다.
- Host는 새 Adapter를 먼저 숨긴 뒤 전체 contribution을 사전 검증하고, 중복 layer가 있으면 목록·활성 plan을 바꾸지 않는다. 해제한 Adapter도 즉시 숨기며 남은 contribution으로 다시 조립한다.
- 비활성 저밀도 Playback에 새 상태 사본을 적용해도 정적 차로·신호와 동적 배우 Root가 먼저 노출되지 않도록 기존 이동 계산 밖의 표시 수명만 보강했다. 경로·차로·신호·보간·pool 계산은 바꾸지 않았다.
- 실제 쓰기 파일과 SHA-256이 같은 격리 Unity 사본에서 신규 Adapter 10/10, 공통 Host 11/11, 저밀도 교통 16/16, 음식배달 Journey 27/27, 기존 사가정 공간·H·모형·운영 View 75/75, 합계 139/139를 통과했다.
- 새 Adapter는 아직 production 조립부나 canonical `SimulationWorldShell` 저장 Scene에 넣지 않았다. Scene·Prefab·Mesh·카메라·화면 출력은 바꾸지 않아 이번 판본에서는 Play Mode·Game View를 재실행하지 않았다.

### r19 사가정 공공데이터·생활 관찰 심화 제안

[제안서 r19](sagajeong-public-data-life-observation.proposal.r19.md)은 사가정 기존 외형을 보존하면서 생활상을 깊게 만드는 다음 범위를 기획 상태로 추가한다.

- 기존 생활 여덟 영역 자료 분류, 현행 여덟 Unity 실행 모듈, 사용자가 켜고 끄는 표시 그룹을 서로 다른 축으로 유지한다.
- 공공자료 전수 조사를 선언한 공식 카탈로그 안의 모든 관련 후보를 `Included / Excluded / Blocked / NoCoverage / Superseded`와 근거까지 판정하는 작업으로 정의한다.
- 화면 건물 4,062개와 주소·업무 의미 기준 건물 602개의 결속, 차량·보행 이동망, 경계 포털, 집계 자료의 합성 밀도 규칙을 개별 Actor 배치보다 먼저 닫는다.
- 외부 이동은 상세 1km 바깥 지형을 만들지 않고 `OutsideWindow → StationBoundaryPortal → CoreWindow`로 잇는다.
- 건물·Actor·업체는 하나의 읽기 전용 선택 Coordinator 아래에서 한 번에 하나만 선택하고, 합성 Actor 카드는 실제 개인 자료가 아님을 명시한다.
- 표시 토글은 Simulation Tick·업무 생명주기·가상 정산을 바꾸지 않는다. 신규 수집·DB·API·Simulation·Unity·Scene·E7 구현은 아직 승인되지 않았다.

### r20 사가정 합성 배달 기사 관찰 첫 절편

[승인 구현 r20](sagajeong-synthetic-courier-observation.implementation.r20.md)과 [E7 수직 작업 명세](../../../../../eng/execution-ledgers/work-orders/sagajeong-synthetic-courier-observation.e7-work-order.json)에 따라 r19 전체를 열지 않고 합성 기사 한 명의 G5 읽기 절편만 구현했다.

- 서버·Simulation은 `station:kr:kric:s1107:0722`, `scenario:synthetic-delivery.r1`, `actor:synthetic-courier:1`을 포함한 관찰 사본과 결정적 SHA-256을 만든다. 수령 확인과 `ReceivedTick`이 모두 있는 주문만 완료로 세고 전달만 끝난 주문은 제외한다. 실패·회복은 `NotTracked`, 모의 지급은 금액 없이 `PolicyPending / SimulationSettlementRulePending`이다.
- 서버와 Unity가 역·Session revision·World revision·Tick·단계·행동·경로 길이·합성 주문 식별·사본 hash를 함께 검증한다. 낮은 revision, 높은 revision의 낮은 Tick, 같은 revision의 다른 hash와 다른 역 자료는 최신 사본을 덮지 못한다.
- Unity에는 메모리 해석기, Collider 없는 runtime 기사 표시·화면 좌표 선택·읽기 전용 카드, `LifeSimulation` 단일 모듈 Adapter를 추가했다. Camera·좌표가 결속되기 전에는 준비 상태로 보고하지 않으며, Host 가시성과 사용자의 `업무 Actor` 토글을 분리한다.
- 서버 변경 경로 Task는 Simulation solution build와 전체 1,965/1,965를 통과했다. 실제 작업 파일과 SHA-256이 같은 격리 Unity 사본은 합성 Actor·공통 모듈·Mobility·사가정 운영 View 회귀 37/37을 통과했다.
- 실제 canonical `SimulationWorldShell` Play Mode의 전체/근접 Game View에서 Tick `14→15`, 숨김 중 Actor 5m 이동, 상위 revision 수용, 재표시 최신 사본, 선택·표현 전후 권위 hash 불변과 Collider 0을 확인했다. Scene hash `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`는 실행 전후 같고 `sceneDirtyAfterPlay=false`다. 대표 화면과 상한은 Unity `Documentation/Changes/2026-09-14-sagajeong-synthetic-courier-observation/README.md`에 있다.
- 이 결과는 runtime-only 검증 조립이다. 실제 사용자 Mouse 입력·production Host/Scene 배선·live 서버·도로 주행·일반 주민·경계 포털·공공자료 수집·DB·정산 규칙·E 승격은 열지 않았고 Scene·Prefab·commit·push를 변경하거나 수행하지 않았다.

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
- 사가정시장은 입구·골목 overlay 후보로 보존한다. r8의 독립 polygon 한 동 교체안은 자원 교체 기술을 시험하는 후보로만 남기며, r25의 시각 우선순위는 번호별 역 출구와 지역 방향 기준점을 먼저 따른다.

### r25 방향 기준점 중심 외관 정밀화

역세권 디오라마의 현실성은 모든 건물의 정면 외관을 복원하는 데서 시작하지 않는다. **건물·도로의 배치, 건물 높이와 지형 관계는 실제 자료를 우선**하고, 외관 정밀화는 플레이어가 현재 위치와 진행 방향을 알아보게 하는 대표 기준점에 집중한다.

- 일반 건물은 건물별 외곽선·표현 높이·도로와의 이격·블록 밀도를 보존한다. 용도·준공 시기·지붕 형태처럼 근거가 있으면 지역 외관 문법의 선택값으로 사용할 수 있지만, 사진이나 승인된 시각 근거가 없으면 실제 창호·재료·간판을 재현했다고 표시하지 않는다.
- 세부 표현 우선 대상은 번호와 방향을 가진 지하철 출구, 공원, 전통시장 입구와 대표 골목, 공공시설, 교통시설, 지형적으로 두드러지는 구조물이다. 역 출구는 출구 번호 자체뿐 아니라 어느 도로·시장·공원 방향으로 이어지는지를 함께 보존한다.
- 대표 기준점의 목적은 근접 실사 복제가 아니라 전체 조망과 선택 추적에서 `어디에 있는지`, `어느 방향으로 가야 하는지`, `그 지역이 무엇으로 기억되는지`를 판독하게 하는 것이다.
- 시각 근거가 승인된 기준점만 고유 외형 자원 후보가 된다. 자원·판본·hash·bounds 결속에 실패하거나 권리가 미확정이면 기존 절차적 매스와 중립 표식을 유지한다.
- 공간 모형 기반의 기준점은 위치·방향·관찰 의미만 제공한다. 이후 방어·업무·생활 gameplay가 같은 기준점을 사용하더라도 통행 가능성·상호작용·성공 권위는 각 선택형 레이어가 별도로 검증한다.
- 따라서 목표 품질은 `배치·높이의 근거 충실도 + 대표 기준점의 지역 식별력 + 나머지 건물의 일관된 디오라마 문법`이며, 모든 건물의 사진 대응 외관은 완료 조건이 아니다.

### r26 사가정역 출구 방향 기준점

- 첫 세부 표본은 사가정역 1~4번 출구 묶음으로 확정했다. 각 출구는 OSM node/version과 WGS84 좌표, 기존 사가정 ENU 좌표, 지도 revision·hash를 함께 가진다.
- Unity는 출구당 일반화된 저상 구조·차양·7호선 색상 번호 표식을 만들고 화면 번호 라벨을 배치한다. 실제 출구 사진·간판·창호를 재현하지 않는다.
- 구조물의 방향은 역 기준점을 향한 읽기 보조값일 뿐 실제 입구 방향·보행 경로가 아니다. 모든 출구는 `TraversalReady=false`, `InteractionReady=false`, `OperationalAuthority=false`다.
- 출구 자료가 없거나 지도 hash와 맞지 않으면 출구 표식만 보류하고 기존 건물·도로 디오라마를 유지한다. 다른 역 출구나 임의 좌표를 fallback으로 사용하지 않는다.

### r27 사가정역 공간 보충 자료 원장

- [수집·원장화 기록 r27](sagajeong-spatial-supplement.implementation.r27.md)에 따라 기존 사가정 1km 창을 바꾸지 않고 보행망 2,902건, 엘리베이터 1건, 공원 정체성 1건, 버스 정류소 30건, 가로수 292건을 별도 `PendingHumanReview` 관측으로 저장했다.
- WGS84 자료는 기존 사가정 ENU 기준점에 읽기 전용 좌표 후보로 투영했다. 공원 SHP는 EPSG:5174 원본과 정체성만 결속했으며 경계 좌표 변환은 보류했다.
- 자료는 Runtime·통행·Collider·NavMesh·gameplay 권위가 아니다. Unity Scene·Prefab에는 적용하지 않았고 새 시각 원본도 수집하지 않았다.
- 국토지리정보원 수치지도 V2·수치표고모형은 로그인·전용 전송 절차가 필요한 `BlockedExternalAccess`로 남기며 다른 자료나 평지 fallback으로 결손을 숨기지 않는다.

### r28 주소·필지 증거 필수 관문

- 모든 역 `StationEvidenceProfile`은 `RoadAddress`, `ParcelIdentifier`, `ParcelGeometry`를 정확히 한 번씩 기록한다.
- 세 항목은 `collectionState`, `coverageState`, 원본 영수증, 원장 판본·hash, 결손 수와 `applicationAuthorized`를 각각 가진다.
- `Complete`는 대상 전부에 수집 또는 결손 판정을 기록했다는 뜻이며 공식 주소 확정·필지 경계 확보·배달 가능·Unity 공개를 뜻하지 않는다.
- PNU·지번·건물 도형으로 실제 필지 경계 도형을 추정하거나 수집 완료로 대신하지 않는다.
- 사가정은 주소 판정 4,062/4,062와 PNU 4,062/4,062를 갖지만 고유 PNU 3,774건의 필지 경계 도형은 0건이다. 면목·용마산은 아직 세 항목 모두 `NotAssessed`다.
- 필수 체크리스트 자체는 확정했지만 기존 공통 디오라마 규칙 12개는 `Candidate`로 유지하고 E 단계나 공통 적용을 승격하지 않는다.

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
10. r3 Game View와 상태 hash를 회귀 기준으로 동결한 뒤 `ActiveStationDioramaHost` 후보와 기존 사가정 View의 호환 Adapter를 도입해 출력 불변을 먼저 확인한다.
11. A+ 두 순환로를 H3 후보와 부분 AreaSet 구성으로 결속하되 기존 Town 좌표·역할 슬롯을 복사하지 않고 Graph Map 레벨 1~3에서 WI·배치 제약·코드 결속을 분리한다.
12. r18의 단일 Mobility Adapter 이음부를 검증 전용 조립부에서 저밀도 교통 하나에 결속하고 출력 불변을 확인한 뒤, 기존 WI 직접 결과와 생활 상태 기록 불일치를 정리한다.
13. Shell 수준 대기 상태 공급원과 다중 표현 대상 Adapter를 추가한 뒤 기존 Nature 경로를 먼저 회귀하고 사가정 전용 layer·카메라·광원 결속을 검증한다.
14. 동일 seed·Tick·AreaSet/H·WI·Mobility·Atmosphere 참조로 같은 조립 hash를 만들고, 역 전환 시 배우·표현 Root·구독을 모두 회수하는지 확인한다.
15. 코드·자동 시험, canonical Play Mode, Game View·Console·청음, Hosted 상태 사본, Save/Replay와 실제 통행 승인을 각각 별도 증거로 보고한다.
16. r25의 배치·높이 현실 기준선을 회귀로 고정하고, 첫 방향 기준점 하나를 선택해 출구 번호·방향 관계·고유 외형 근거·절차적 fallback을 작은 표현 절편으로 검증한다.
17. r27 보충층은 사람 검토와 자료별 시각 예산을 통과한 뒤에만 읽기 전용 Adapter 후보로 올린다. 공원은 EPSG:5174 경계 변환을 별도 판본에서 검증하기 전까지 정체성 표식 이상으로 사용하지 않는다.
18. r28 주소·필지 관문은 세 증거 종류를 역별로 독립 판정하고 하나라도 미수집이면 그 결손을 유지한다. 수집 완료와 이용·표현·이동 권위 승인을 같은 상태로 합치지 않는다.

## 미정

- 환승역의 대표 기준점을 역 중심 하나로 둘지 출구·승강장 기준점 묶음으로 둘지
- 면목역의 법정동 바깥 건물 coverage와 표면 다각형 원천·이용조건, 망우제3동 극소 경계 교차의 정밀 판정
- 면목·용마산 비공개 역 사본을 기존 행정동 current projection과 분리해 제공할 station-scoped 저장·조회 계약
- 사가정·면목·용마산 공통 View를 한 Scene에서 전환할 역 선택 UI와 `OnGUI` 공존 배치
- 공통 프로필의 초기 시각 허용 범위와 저사양 기기 성능 예산
- 사가정역 출구별 실제 출입 방향과 연결 도로·시장·공원 문구를 뒷받침할 공간 근거, 사진 권리와 자원 예산
- r27 공원 SHP의 EPSG:5174→사가정 ENU 변환과 경계·입구의 현행성 검토
- 사가정 고유 PNU 3,774건의 실제 필지 경계 도형 원본·CRS·판본·hash와 coverage
- 면목·용마산의 도로명주소·PNU·필지 경계 세 항목 최초 assessment
- 보행망 2,902건·가로수 292건을 화면 혼잡 없이 선택적으로 표현할 밀도·LOD·성능 예산
- 관광사진 API 활용 범위 확보와 개별 항목의 공공누리 유형 재조회
- 무료 출시 뒤 광고·후원·유료 부가 콘텐츠를 허용할지와 그에 따른 자산 권리 상한
- 약 390m × 350m·753.060m A+ 활성 범위와 첫 관찰 시간대
- 사가정 합성 대기 프로필의 시간·날씨 순서와 실제 날씨가 WI·NPC 행동에 영향을 주는 가장 이른 후속 범위
- H 재고·생성 문서 드리프트와 생활 ActionRecord·`MOVE` 직접 결과 명칭 불일치의 소유 작업
- 실제 공개 사업장·입점·거주 근거를 어느 검토 원장에서 건물 역할 Profile로 승격할지
- 창고 역할의 읽기 전용 경로 사본과 역할별 다중 입점 UI를 어떤 후속 WI로 검증할지
- 공공 통계를 합성 Actor의 시간대·공간 밀도에만 쓸지, 합성 직업 구성에도 제한적으로 반영할지
- 화면 건물 4,062개와 기준 건물 602개의 결속·`BackdropOnly` 정책
- 보행·차량·오토바이·대중교통 경계 포털의 정확한 수·위치·판본과 합성 유입 일정
- Actor 카드의 합성 건수·모의 지급·비용 계산식과 세션 간 지속성
- 사용자별 표시 그룹 기본값·저장 위치와 실제 운영 Projection의 권한

## 다음 질문 하나

공식 서울 `AL_D002` ZIP을 확보할 수 있게 되면 **동일한 2026-09-08 전체판을 재현용으로 받을지**, 아니면 **그 시점의 최신 전체판을 새 판본으로 받을지** 정할까?

추천은 **가능하면 2026-09-08 전체판을 먼저 확보하고 최신판을 별도 revision으로 추가한다**다. 기존 2026-08 건물·주소 근거와 시간 차이가 가장 작아 재현 검증에 유리하다. 대가는 과거 파일을 더는 제공하지 않으면 최신판만 받아 기준일 차이를 명시적으로 검토해야 한다는 점이다.

## 현재 검증 상한

공식 XLSX와 metadata를 로컬 비공개로 수집하고 schema·대상 3행·결정성 self-test 8건을 통과했다. 로컬 MySQL `hongdal_dev`에 원본 1건과 정규화 3건을 저장해 첫 적용 3건 삽입, 재적용 신규 0·기존 3, 별도 연결 재조회 3건을 확인했다. 서버 공용 계약·검토 대장·조회 UseCase·인증 Controller·DI를 구현했고, 출처 계보·1km 비격자 교차 tile·잘못된 지역/행정동 권위·중복 tile·정의 불일치·400/404·ETag·DI를 포함한 집중 시험 13/13을 통과했다. 전체 이번 경로 14개를 지정한 최종 Fast도 계약·서버·시험 project build, 코드 지도, Simulation 관련 221/221과 서버 관련 121/121을 통과했다(`artifacts/local/validation/20260913-112827`). Task는 두 solution build 경고·오류 0과 Simulation 전체 1,913/1,913을 통과했고 서버 전체는 5,210건 중 5,203건 통과·기존 작업트리 관련 7건 실패로 완료 관문이 닫히지 않았다(`artifacts/local/validation/20260913-113110`). 사용자 표시명 확인 반영 뒤 변경 7개 경로 한정 Fast에서 diff·생성 지도, 계약·서버·시험 build와 집중 시험 13/13이 다시 통과했다(`artifacts/local/validation/20260913-114129`). Hosted HTTP와 공개·운영 사용은 수행하지 않았다. 이후의 Unity 공통 리팩터링·실제 공간 사본 조립·Play Mode·Game View는 아래 역별 후속 결과가 소유한다. 관련 코드·기획·증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다.

후속 면목역 구현에서는 독립 1km 사본을 만들고 건물 4,165개·방향별 도로 link 124개·표면 48개·행정동 교차 6개를 결속했다. 100m cell 100개 중 두 셀의 `MissingCoverage`와 권리·도로 폭·통행 권위 결손을 유지한 채 로컬 MySQL 비공개 원본 저장·별도 재조회와 Unity parser·Mesh 교차 검사를 완료했다. canonical `SimulationWorldShell` Play Mode의 임시 View로 실제 Game View까지 확인했지만 Scene 영속 결속, 사가정·면목 overlay 공존 배치, 정식 Unity Test Runner 새 결과는 아직 남아 있다. 서버 payload는 station-scoped 저장 계약 전까지 계속 `WaitingForSpatialCoverage`다.

후속 용마산역 구현에서는 공식 기준점 중심의 bbox `127.0811416,37.5692469,127.0924624,37.5782568`를 수집·동결하고 건물 2,859개·도로 116개·표면 41개·행정동 4개를 결속했다. coverage 100셀 중 10셀과 여섯 결손 코드를 유지했으며, 로컬 MySQL ID 58~61의 재적용 신규 0·독립 재조회와 서버 집중 시험 13/13·Controller 관련 17/17을 확인했다. Unity 생성 project는 오류 0으로 빌드됐고 compiled fixture 4/4와 canonical `SimulationWorldShell` 실제 Play Mode·Game View에서 16 renderer·88,589 vertex·47,213 triangle을 확인했다. 정식 Test Runner는 시험 원본 23 case를 확인했으나 이번 새 완료 결과가 없어 미확정이다. Scene은 저장하지 않았고 실행 전후 SHA-256 동일·dirty false다. 서버는 독립 station-scoped 계약 전까지 `ServerLoadable=false`와 `WaitingForSpatialCoverage`를 유지한다.

사가정시장은 기존 2025-11-10 점 관측·원본/행 hash·주소 단일 건물 후보를 수집 명세에 분리 결속했다. 명세와 기계 후보 대장만 작성했으며, 현행 원본 다운로드·사진 취득·현장/원격 공간 확인·Graph Map·배치맵·Blender·Unity는 아직 미실행이다.

실제 외관 교체는 저장소·Blender 경로의 정적 구조와 공식 자료·이용조건을 조사해 제안으로만 추가했다. 후보 건물 선정·사진 수집·모델링·FBX·Prefab·Scene·Play Mode·Game View는 수행하지 않았다.

무료 Steam 사진 이용 정책은 공식 저작권법·한국저작권위원회·공공누리·Steamworks 안내를 대조해 제안으로만 추가했다. 개별 사진·건물에 대한 이용 허락이나 법률 판단, 사진 다운로드·직접 촬영·권리자 문의는 수행하지 않았다.

사용자가 직접 촬영이 어렵다고 확인해 공공누리 제0·1유형 등 공공 이용허락 사진 우선 방향으로 대체했다. 사가정역 정확 사진 5건을 비공개 수집해 원문·라이선스·hash를 보존했고, 로컬 MySQL에 사진 후보 5·시장 결손 1·공공누리 제4유형 제외 3건을 저장했다. 첫 적용 신규 9, 재적용 신규 0·기존 9, 독립 재조회 9를 확인했다. 사가정시장 정확 사진, CC BY-SA 프로젝트 정책, 내용 승인, Blender·Unity·Scene·배포는 아직 완료되지 않았다.

공공데이터포털 후속 조사에서는 공식 안내서·여행기사 CSV와 공급자 미리보기 3건을 비공개 수집하고 로컬 MySQL에 6건을 저장했다. 자체 시험 17건, 첫 적용 신규 6, 재적용 신규 0·기존 6, 독립 재조회 6을 확인했다. 관광사진 API는 HTTP 403이고 개별 권리 확인이 끝나지 않아 Blender·Unity·배포 관문은 닫혀 있다.

r25는 배치·높이를 현실 근거의 기준선으로 유지하고 번호별 역 출구·공원·시장·공공시설을 방향 기준점으로 선택 정밀화하는 기획만 확정했다. 새 공공자료 수집·모델링·Prefab·Scene·Play Mode·Game View·증거 승격은 수행하거나 승인하지 않았다.

r26은 기존 동결 OSM 사본에서 사가정역 1~4번 출구를 별도 자료로 결정 투영했고, 재생성 SHA-256 일치와 격리 Unity 신규 시험 4/4·관련 회귀 21/21을 통과했다. Collider·통행·상호작용·운영 권위와 다른 역 fallback은 열지 않았다. 원본 Unity Editor의 Pipeline이 연결되지 않아 canonical `SimulationWorldShell` Play Mode·Game View·Console은 새로 검증하지 않았고 Scene·Prefab·E 증거도 승격하지 않았다.

r27은 서울 열린데이터광장 공식 자료 5개를 사가정 1km 창에 결속해 보행망·엘리베이터·공원 정체성·버스 정류소·가로수 3,226건을 로컬 비공개 원장에 저장했다. 자체 검사 14/14, 첫 적용 신규 3,226·원본 5, 재적용 신규 0·기존 3,226, 독립 재조회 3,226·원본 5를 확인했다. 공원 EPSG:5174 경계 변환, NGII 로그인 자료, 사람 검토, Unity Adapter·Scene·Prefab·Game View와 E 승격은 완료하지 않았다.

r28은 화면–기준 건물 결속 5,211행과 화면 건물 주소 7,582행을 독립 dataset으로 로컬 MySQL에 저장하고 전체 12,793행의 정확 key 집합·원본 snapshot 계보·hash를 새 문맥에서 재조회했다. 4,062개 화면 건물은 모두 판정 상태와 PNU를 갖지만 실제 필지 경계 도형은 0건이다. Development 관리자 API와 집중 시험까지만 검증했으며 공개·Unity·배달·가격·업체·통행 권위, Scene·Prefab·Play Mode·Game View와 E 승격은 열지 않았다.

r16 건물 역할 카드는 기존 602개 건물 Mesh를 바꾸지 않고 음식점·주거 전달·창고 역할 Profile을 세 건물에만 명시 결속했다. Profile 목록은 한 건물의 복수 역할을 지원하지만 현재 자료에서 실제 다중 입점 사례를 만들지는 않았다. 선택 카드의 역할 탭·역할별 H1·생명주기 요약과 준비된 음식배달 경로 선택은 모두 읽기 전용이다. 실제 작업 파일과 SHA-256이 같은 격리 Unity 사본에서 관련 시험과 기존 사가정 회귀 75/75를 통과했다. r16 Play Mode·Game View·사용자 MouseUp은 새로 검증하지 않았고 Scene·Prefab·서버·Graph Map·H 생성 대장은 수정하지 않았다. 이번 r16 변경은 아직 commit·push하지 않았다.

r17은 엔진 비의존 공통 Profile·위치 독립 factory·Planner와 `ActiveStationDioramaHost`, 사가정 호환 Adapter를 구현했다. 메인 Unity 쓰기 파일과 SHA-256이 같은 격리 사본에서 신규 9/9와 기존 사가정 공간 Overlay 54/54·H 선택 10/10·모형 4/4·운영 View 7/7을 각각 통과했다. 넓은 검색 실행의 기존 Synty 대장 5건은 격리 사본에 외부 공급사 `Assets/Synty` 팩이 없어 카탈로그의 Prefab GUID 4개를 해소하지 못한 실패로 확인했고, 메인 자산 트리에서는 네 GUID를 모두 확인해 이번 쓰기 경로와 분리했다. 면목·용마산 활성 Profile, 서버 schema, Scene·Prefab·Game View, Hosted·Save/Replay, commit·push는 변경하거나 실행하지 않았다.
