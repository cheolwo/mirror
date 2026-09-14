# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r13]

## 목표

서버가 출처·판본·경계 유효성을 검토한 행정동별 공간 사본을 Unity가 읽기 전용으로 받아, 하나의 `SimulationWorldShell`에서 행정동 단위 디오라마를 선택·관찰할 수 있게 한다.

첫 단일 대상이었던 면목제3·8동에서 동북서울 배달운영권역 후보 30개 행정동으로 자료 생성 범위를 넓힌다. 역 중심 1km 디오라마는 상세 관찰 창이고, 행정동 디오라마는 경계와 자료 귀속의 기본 공간 모듈이다. 어느 쪽도 배달권·통행·게임 상태의 권위를 자동으로 갖지 않는다.

## 확정

- 대상 scope는 광진구 중곡동, 동대문구 전농·답십리·장안·휘경·이문동, 중랑구 면목·상봉·중화·묵·망우·신내동에 연결된 행정동 30개다. 정확 집합은 `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json`이 고정하고 행정안전부 코드 원장과 다시 대조한다.
- 법정동·행정동·역세권 관찰 창·관리자 배달운영권역·협력권역은 서로 다른 식별자와 판본을 유지한다. 하나를 다른 하나의 이름이나 경계로 바꾸지 않는다.
- 건물은 원본 도형의 `PointOnSurface`가 하나의 행정동 경계에 포함될 때만 귀속한다. 경계 밖·무효·복수 판정은 `Unresolved` 또는 quarantine으로 남기고 임의 귀속하지 않는다.
- 도로 중심선은 행정동 경계에서 잘라 여러 행정동에 걸칠 수 있다. 건물은 중복 소유시키지 않는다.
- 모든 행정동은 같은 사가정 원점 ENU와 500m 격자를 사용한다. 동마다 별도 원점을 잡아 인접 경계를 어긋나게 하지 않는다.
- MongoDB에는 hash별 불변 manifest·tile·overlay를 저장하고 현재 판본 포인터만 갱신한다. 다만 현행 경계 정본을 통과하지 못한 결과는 별도 candidate collection에만 저장하며 API current pointer를 바꾸지 않는다.
- 행정동별 디오라마는 `ObservationPresentationOnly=true`, `TraversalReady=false`, `GameplayReady=false`, `DistributionApproved=false`를 기본 상한으로 한다.
- 공개 사업장, 상인 Claim, 후원 Campaign은 별도 원장과 권한으로 검증한다. 이번 30개 기본 공간 batch에는 업체 표시·광고·상세 주소·개인정보를 넣지 않는다.
- Unity는 인증 GET과 명시적 `SemanticPlaceStableId`만 읽는다. Unity가 경계·주소·업체 위치를 추측하거나 운영 상태를 확정하지 않는다.

## 공식 경계 판본 정정

- 서울시 `OA-22160` 페이지는 2026-09-11 갱신으로 표시되지만 실제 내려받기 파일은 페이지 수정일 2023-10-31, 내부 파일 시각 2023-10-20, 425행이다. SHA-256은 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`, 좌표계는 `EPSG:5181`이며 이번 대상 30개는 모두 존재한다.
- 위 ZIP은 `HistoricalOfficialBoundaryBootstrap`으로만 쓴다. 포털 갱신일을 경계 유효 기준일로 바꾸어 기록하지 않으며, 이 자료만 사용한 batch는 `WaitingForAdministrativeBoundary / CurrentAuthoritativeBoundaryUnavailable`이다.
- 현행 게시 정본은 행정안전부 주소정보누리집의 최신 월 전체분 `TL_SCCO_GEMD`다. 승인 취득한 실제 파일의 기준월·파일명·hash·공공누리 제1유형·`EPSG:5179`와 30개 코드 유일성·유효 도형·위상을 검증한다.
- 최신 정본이 없거나 코드 판본보다 낮거나 hash·CRS·대상 집합·위상 검사가 실패하면 `AdministrativeBoundaryVintageRejected`로 current 게시를 중단한다.
- SGIS 2025 행정구역 경계는 8자리 통계 코드와 공식 crosswalk를 확보한 경우에만 독립 비교에 쓴다. 서로 다른 원본 경계를 평균내거나 혼합하지 않는다.

## 공용 읽기 계약

- `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-manifest`
- `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/diorama-tiles/{tileStableId}`
- `GET api/v1/world/administrative-areas/{administrativeAreaStableId}/display-overlays`

기존 계약은 manifest, 500m tile, 건물·도로, 출처·품질과 공개/후원 표시 overlay를 제공한다. 서버는 경계 판독, ENU 변환, 건물 귀속, 도로 절단, 결정적 hash, Mongo 불변 저장과 ETag를 담당한다. Unity Client·Decoder·Interpreter는 이를 읽기 전용 메모리 모델로만 유지한다.

## r4 구현 slice

- [구현 명세](administrative-dong-batch.implementation.r4.md)
- [E7 수직 작업 명세](administrative-dong-batch.e7-work-order.json)
- 자료 기반 scope catalog와 일괄 생성기는 `OA-22160`, 서울 `AL_D010`, 국가표준 NodeLink를 한 번씩 읽어 30개 모듈로 fan-out한다.
- 건물 계약 v1은 단일 외곽 ring만 지원하므로 hole이 있는 원본은 가장 넓은 외곽 ring만 표현 후보로 내보내고 생략 수를 감사자료에 기록한다. 원형 완전 보존으로 보고하지 않는다.
- NodeLink는 방향 링크 중심선이며 최대 100m 이하 두 점 선분으로 결정적으로 나눈다. 도로 폭·골목·보도·신호·통행 가능성은 별도 자료가 필요하다.
- 역사적 경계 결과는 로컬 `artifacts/local`과 Mongo candidate batch에만 저장한다. 기존 면목제3·8동 current pointer를 포함한 API current 자료는 변경하지 않는다.

## r4 구현·검증 결과

- `northeast-seoul-rider.r2`의 30개 행정동·12개 법정동을 모두 생성했다. 같은 입력을 다시 생성했을 때 변경 파일은 0개였다.
- 건물 61,897개를 하나의 행정동에 귀속했다. 미해결 27개는 경계 밖 13개, 원본 무효 형상 11개, 1mm 좌표 반올림 뒤 자기교차한 형상 3개이며 복수 귀속은 0개다. v1 외곽 ring과 원본 `PointOnSurface` 판정이 달라지는 2개는 원본 판정점을 명시적으로 보존했다.
- 방향 NodeLink 4,209개를 행정동 경계에서 절단해 행정동별 출현 4,620개, 100m 이하 2점 선분 11,769개로 만들었다. 397개 링크가 둘 이상의 행정동에 걸렸고, 게시한 1mm 경계 밖으로 1µm보다 많이 벗어난 선분은 0개다.
- C# 투영기는 Python 결과의 30개 경계를 독립 변환한 `OA-22160` 경계와 모든 꼭짓점에서 2mm 이내인지 확인한 뒤 건물 61,897개·도로 11,769개를 다시 조립해 500m tile 297개를 만들었다.
- 로컬 MongoDB candidate collection에 batch 1개·manifest 30개·tile 297개·overlay 30개, 합계 358개 문서를 저장하고 새 연결로 재조회했다. 같은 batch를 다시 적용했을 때 신규 0개·기존 일치 358개였으며 `administrative_dong_diorama_current` 사본 hash는 적용 전후 같았다.
- Python 실행 영수증은 Python 3.12.14, pyshp 2.3.1, pyproj 3.7.2, Shapely 2.1.2, GEOS 3.13.1, PROJ 9.5.1, EPSG DB v11.022를 고정한다. 도구나 의미 규칙이 달라지면 같은 자료라도 새 revision으로 만든다.
- 앞서 생성한 r1 로컬 산출물과 Mongo 후보는 삭제하거나 현행으로 승격하지 않고 `PreservedNotPromoted`로 보존한다. r4 정본 후보는 r2 산출물과 별도 candidate batch다.

세부 출처·행정동별 건물/도로/tile 수·hash·검증 상한은 [동북서울 30개 행정동 디오라마 역사 경계 후보 보고](../../../../Reports/동북서울-30개-행정동-디오라마-역사경계-후보-2026-09-14.md)에 기록한다.

## r5 기초 화면 기준선

- [깊이 기준·캡처 구현 명세](administrative-dong-depth-parity.implementation.r5.md)와 [E7 수직 작업 명세](administrative-dong-base-geometry-capture.e7-work-order.json)에 따라 역사 경계 후보 30개를 로컬 Unity 검토 묶음으로 결정적 내보냈다.
- canonical `SimulationWorldShell`을 저장하지 않고 한 번에 행정동 하나를 결합 Mesh 4개·Collider 0개로 조립했다.
- 실제 Play Mode Game View에서 전체 조망 30장과 면목제3·8동 건물 선택 근접 1장을 남겼다. 연락판에서 보이는 넓은 공백·분절은 생활상 완결이 아니라 `MissingCoverage`의 시각 증거다.
- 이 결과는 `G6 UnityBaseView / PrivateReviewOnly`만 충족한다. 현행 경계, 주소, 필지, 출입구, 이동 표면, 생활 자료와 NPC·운영 흐름은 검증하지 않았고 E 단계도 승격하지 않았다.

## r6 주소·필지 후보 첫 절편

- [주소·필지 후보 구현 명세](administrative-dong-address-parcel-candidate.implementation.r6.md)와 [E7 수직 작업 명세](administrative-dong-address-parcel-candidate.e7-work-order.json)에 따라 61,897개 건물 전부의 PNU·필지 주소 후보 상태를 범용 공공자료 원장에 `PendingHumanReview`로 저장했다.
- 단일 후보 49,876개, 복수 후보 1,351개, 동결 판본 내 후보 없음 10,670개이며 후보 보유율은 82.76%다. 동일 입력 재적용 신규·갱신 0, 별도 연결의 exact set·분포·투영 hash 재조회가 통과했다.
- 이것은 `G2a`만 닫는다. 주소 문자열을 공개·Unity 계약에 내보내지 않으며 확정 건물 주소, 실제 필지 도형, 건물 출입구, 배달 목적지나 가격 권위를 만들지 않는다.

후속 r5·r6의 수치·화면·경고·권위 상한은 [30개 행정동 깊이 기준선 및 주소 후보 결과](../../../../Reports/동북서울-30개-행정동-디오라마-깊이-기준선-및-주소후보-2026-09-14.md)에 기록한다. r4 보고서는 역사 경계 후보 생성·Mongo 저장 당시 기록으로 보존한다.

## r8 횡단보도·보행등 설치 후보 G3a

- [정정 구현 명세 r8](administrative-dong-crosswalk-candidate.implementation.r8.md)과 [자료 구현 동반 명세](administrative-dong-crosswalk-candidate.e7-work-order.json)에 따라 서울시 `OA-23081` 2026-08-24 파일의 횡단보도 점을 30개 역사 경계 후보에 결속했다. 동반 명세는 호환 파일명만 `.e7-work-order`이고 이 비공개 자료 후보를 기존 playable-loop Goal에 허위 결속하지 않는 비-E7 문서다. 후속 Unity·통행·Simulation 소비에는 별도 등록된 정식 E7 명세가 필요하다.
- 원본 21,776행 가운데 좌표 있음 21,775·결손 1·대상 30개 동 단일 귀속 1,533·범위 밖 20,242·복수 귀속 0이다. 30개 동 모두 후보가 있고 보행등 설치 관측은 있음 933·없음 600이다.
- 원본 자치구와 역사 경계의 귀속 자치구가 다른 21건은 정상 자료로 숨기지 않고 `SourceDistrictSpatialAssignmentConflict` 진단, 두 자치구와 경계 거리 `0.286~22.076m`를 후보 hash에 포함했다.
- 공식 사가정역 WGS84 기준점과 횡단보도 점의 `8.0946m` 대조가 EPSG:5186 해석을 지지하지만 원천 페이지·시트가 좌표계를 선언하지 않으므로 `EmpiricallyValidatedCandidateNotSourceDeclared`를 유지한다.
- 정정 후보 집합 SHA-256은 `78312E5F0DDB89BFFA9AD1881379CBF7E8037A6C6454D4F0ABDF2A3E4D22D30A`다. 경계 원본, 실제 scope, 설계, 공식 역 기준점, 좌표 검증과 생성기를 모두 hash 계보에 넣었고 Python 자체 시험 17/17·2회 결정성 생성·독립 재감사가 통과했다.
- 첫 r7 구현 결과였던 `g3a.r1`은 독립 감사 뒤 `RejectedAfterIndependentAudit / PreservedNotPromoted`로 보존한다. 정정판은 새 `g3a.r2` dataset/revision으로만 소비하며 과거 자료를 삭제·갱신·승격하지 않는다.
- 로컬 MySQL 범용 공공자료 원장에는 정정 후보 1,533행과 원천·계보 사본 13건을 별도 판본으로 저장했다. 반복 적용은 신규·갱신 0이고 별도 연결에서 exact set·30개 동 분포·충돌 21·후보 hash를 재조회했다.
- 이 절편은 점 위치와 보행등 설치 유무의 비공개 검토 원장일 뿐 횡단보도 면, 정지선, 보도 연결, 신호 현시·주기, 주행 차로·통행 graph 또는 Unity 표시를 만들지 않는다. 따라서 새 생활상 Game View는 촬영하지 않고 r5의 기초 화면을 현행 비교 기준으로 유지한다.

## r9·r12 지역 사업장 후보 G4a와 개인정보 경계 정정

- [첫 구현 명세 r9](administrative-dong-business-candidate.implementation.r9.md)의 `g4a.r1`은 소상공인시장진흥공단 2026-06-30 전국 상가 파일 16개·2,772,484행을 전수 확인하고, 서울 554,092행에서 정확한 30개 원천 행정동 코드의 사업장 29,721곳과 음식 대분류 `I2` 8,246곳을 별도 후보로 만들었다. 그러나 독립 개인정보 감사에서 선택된 원천 식별자 하나가 추적 생성기의 시험 fixture에 들어간 사실을 확인해 `RejectedAfterIndependentPrivacyAudit / PreservedNotPromoted`로 낮췄다. 그 값은 문서·보고에 반복하지 않으며 r1 산출물·DB 29,721행·사본 19건·실행 19건은 삭제하거나 갱신하지 않는다.
- [정정 명세 r12](administrative-dong-business-candidate.implementation.r12.md)와 [r2 비-E7 자료 구현 명세](administrative-dong-business-candidate.r2.data-implementation.v1.json)는 실제 원천 집합에 없는 명시적 합성 fixture, 생성기·집계 산출물의 선택 식별자 교집합 0 관문, 새 dataset/revision·범위·세대·hash와 r1 불변 digest를 요구한다. 기존 `.e7-work-order` 호환 파일은 r1 거절 이력이며 어느 문서도 Unity·운영·게임 소비를 승인하는 playable-loop E7 명세가 아니다.
- 좌표·도로명주소·지번주소 결손은 0, 건물관리번호 결손은 67곳이며 고유 건물관리번호는 11,931개다. 역사 경계와 일치 29,686·다른 동 29·범위 밖 6·복수 경계 0을 재귀속이 아닌 진단으로 보존했다.
- 상가업소번호는 29,721개 모두 고유하다. 같은 원문 상호명·도로명주소를 가진 서로 다른 ID 236그룹·490행은 자동 병합하지 않는다. 기존 면목동 SEMAS 5,411행과 공장 126행은 그대로 보존하고 새 범위의 추가 ID 24,310개를 별도 Source/Dataset/Revision으로 저장했다.
- 정정 r2 후보 집합 SHA-256은 `5F9C61BD71112C69EE3988CFB3B8542AE22BF60AB95D542C702D2355B8A3774C`다. Python 자체 시험 25/25·두 번째 생성 변경 0·verify, C# 자체 시험 59,482/59,482를 통과했다. 로컬 MySQL 보호 원장에 r2 후보 29,721행과 원천·계보 사본 19건을 새로 저장했고 반복 적용은 신규·갱신·사본 0이다. 별도 연결에서 exact set·30개 동·음식 8,246·진단 35·결손 67·보호 필드 14개를 재조회했으며 r1 상태 digest와 기존 면목 5,411·공장 126행은 적용 전후 같았다. 독립 재감사는 tracked 10,757개·untracked 134개와 r2 집계 산출물을 선택 원천 식별자 29,721개와 교차 검사해 노출 0, High 0·Medium 0으로 판정했다.
- 정확 상호명·주소·좌표는 Git 무시 local-private 세대와 RDB 보호 필드에만 둔다. 현행 경계, 확정 건물·출입구, 현재 영업, 상인 Claim과 배포 승인이 없으므로 Unity 업체 아이콘·메뉴·주문 가능·광고·NPC 목적지는 만들지 않는다.

## r10·r11 교차로 점 후보 G3b

- [교차로 점 생성 명세 r10](administrative-dong-intersection-point-candidate.implementation.r10.md)과 [비-E7 자료 구현 명세](administrative-dong-intersection-point-candidate.data-implementation.v1.json)에 따라 서울시 `OA-15534`의 원천 점 도형 8,097건을 읽고 30개 역사 경계 후보에 단일 귀속한 교차로 554건을 별도 세대로 만들었다. 원천 자치구 일치 553·충돌 1·범위 밖 7,543·복수 귀속 0이며 30개 동 모두 후보가 있다.
- 교차로 자체 점으로 먼저 귀속한 뒤 G3a 횡단보도 ID는 진단 관계로만 연결했다. 교차로 524건은 횡단보도와 연결되고 30건은 연결되지 않으며, 관계 1,491개 중 같은 역사 행정동 1,271·다른 행정동 220개다. 횡단보도 귀속을 교차로에 복사하지 않는다.
- 후보 집합 SHA-256은 `DB83F1CC82EB65F864188DD790A003EDF570DBB459D56D4C07CE690988F2837B`다. Python 자체 시험 15/15·두 번째 생성 변경 0·verify·독립 hash 재계산을 통과했다.
- [교차로 보호 원장 명세 r11](administrative-dong-intersection-point-ledger.implementation.r11.md)과 [비-E7 동반 계약](administrative-dong-intersection-point-ledger.data-implementation.v1.json)에 따라 로컬 MySQL에 후보 554행과 계보 사본 19건을 저장했다. C# 자체 시험 16/16, 별도 연결의 exact set·30개 동·후보 hash 재조회, 반복 적용 신규·갱신·사본 0을 통과했다. 적용 전후 G3a r2 1,533행·사본 13건, G4a r1 29,721행·사본 19건, 기존 면목 5,411·공장 126행의 상태 digest는 같았다. 이후 별도 생성한 G4a r2는 r11 보존 계약에 포함하지 않으며 후속 G3b 판본에서 명시적으로 결속해야 한다.
- 교차로 점은 접근 방향, 도로 연결 위상, 정지선, 제어기, 차로 또는 신호 현시·주기를 증명하지 않는다. 따라서 G3a와 G3b를 가까운 점끼리 이어 실제 통행 graph로 만들거나 Unity에 신호등을 배치하지 않는다.

## r13 도보 네트워크 후보 G3c

- [구현 명세 r13](administrative-dong-walk-network-candidate.implementation.r13.md)과 [비-E7 자료 구현 계약](administrative-dong-walk-network-candidate.data-implementation.v1.json)에 따라 서울 열린데이터광장 `OA-21208`의 2020년 기준 WGS84 도보망을 광진구·동대문구·중랑구 공식 CSV 59,724행과 유형 코드북으로 동결했다.
- 모든 CSV 행의 자치구 코드·명과 공식 필터, 코드북 링크 16개·노드 4개 의미를 검사했다. 현재 원본에는 `RONUM`이 없으므로 파일 역할과 1부터 시작하는 CSV data-row 번호를 occurrence로 삼아 NODE와 LINK 모두 완전 동일행을 덮어쓰지 않게 했다.
- `OA-22160` 2023-10-31 역사 경계에 점을 귀속하고 선을 절단해 30개 동에 NODE 17,267개와 LINK fragment 23,472개, 합계 40,739개 후보를 만들었다. 범위 밖 원본은 19,770행이고 원천 자치구와 공간 귀속 충돌 후보는 48개다.
- 후보 집합 SHA-256은 `43248F7F563DCDFE059650F3EC000085CD0D7CFB65FC5300EFDFCBDD685990AA`다. 1mm 직렬화 좌표의 역사 경계 재포함, 동일 행정동 안 조각 중복 부재, 원선 대상 교차 길이 882,519.308089m 보존과 고정 field/framing hash를 검사했고 Python 자체 시험 17/17·재생성·verify가 통과했다.
- C# importer가 전체 98,781,674-byte 후보 파일을 읽어 framed 후보 hash를 독립 재계산하고 로컬 MySQL에 40,739행과 계보 사본 17건을 저장했다. build 경고 0·오류 0, 자체 시험 16/16, 새 연결의 30개 동 exact set·후보 hash 재조회가 통과했고 반복 적용은 신규·갱신·사본 0이다. G3a r2·G3b r1·G4a r2·사가정 r27 OA-21208을 합한 보호 상태 71,501행·사본 84건·실행 84건의 digest는 적용 전후 같았으며 최종 독립 정적 감사는 High·Medium·Low 0이다.
- 이 자료는 2020 역사 관측이고 세 자치구 밖 boundary halo가 없으며 보도 폭·연석·출입구·현재 통행·오토바이 허용·차로·방향·신호가 없다. Mongo/current/API/Unity/이동 graph·NPC 경로 권위로 승격하지 않는다.

## 이전 r3 증거의 재해석

- 기존 면목제3·8동 current projection의 건물 250·도로 선분 722·타일 3은 사가정 1km 참고 지도 602개 건물을 면목동 6개 역사적 경계에 귀속한 좁은 자료다.
- 과거 문서가 `OA-22160` 포털 갱신일을 경계 판본으로 기록해 `ActualAdministrativeDongDataGate=Closed`라 한 부분은 실제 ZIP 내부 증거와 맞지 않는다. 현행 경계 관문은 다시 열어 `WaitingForAdministrativeBoundary`로 정정한다.
- 서울시 `OA-14991` 생활인구 4,464건은 2016 행정동 구역 기준의 2026년 7월 통계 관측이다. 현행 경계 주민 수나 NPC 생성 권위가 아니다.

## 미정·후속

- 최신 행정안전부 `TL_SCCO_GEMD` 서울 전체분 승인 취득과 30개 경계 재생성·독립 SGIS 대조.
- 필지 경계 `AL_D002`, 30개 행정동 건물의 확정 건물관리번호·출입구와 시설 자료 수집. PNU·필지 주소 후보 상태는 r6에서 전수 기록했고 사업장 29,721곳은 정정 r12의 비공개 G4a r2 보호 원장으로 저장했지만 건물·출입구 결속과 현재 영업 확인은 남아 있다.
- 건물 hole·multipart, 도로 폭·차로·보도·골목·신호를 보존할 v2 geometry·mobility 계약. 횡단보도 점 G3a·교차로 점 G3b·도보 네트워크 G3c의 비공개 후보 원장은 검증했지만, 접근 방향·정지선·제어기·신호 현시와 현행 통행을 같은 판본으로 결속한 graph는 없다.
- current 게시 승인, 행정동 선택 UI, 실제 Prefab/Scene 저장 결속. 역사 경계 후보의 runtime-only `SimulationWorldShell` 조립과 Game View는 r5에서 비공개 검토 범위로만 확인했다.
- current 승격 전 manifest·overlay의 시간·좌표 프레임·범위·권위 플래그까지 포함하는 전체 canonical hash와 중복 문서 exact-body 검증.
- Claim·후원 Campaign HTTP API, 결제·정산·실제 광고 판매. 현재 광고 표시는 0건이다.

## 검증 상한

r13 현재 최대 증거는 `HistoricalBoundaryCandidateBatchStoredAndVerified / G2aAddressParcelCandidateLedgerValidated / G3aCrosswalkPointCandidateLedgerValidated / G3bIntersectionPointCandidateLedgerStoredAndVerified / G3cWalkNetworkCandidateLedgerStoredAndVerified / G4aCorrectedPrivateBusinessCandidateLedgerStoredAndVerified / G6UnityBaseViewGameViewVerified / CurrentPublicationBlocked`다. G3c는 40,739개 local-private 후보와 계보 사본 17건의 보호 원장 저장·독립 재조회·반복 무쓰기까지만 포함한다. 현행 행정동 경계, 확정 주소·필지·출입구, 현재 이동 graph·정지선·신호 현시, 공개 사업장 결속, 실제 통행, 운영 주문·배차와 생활상 Game View는 증명하지 않는다.

## 다음 질문 하나

G3c RDB 독립 검증을 마쳤더라도 세 자치구 경계 밖 halo를 추가 확보하지 않은 채 이 후보를 current 이동 graph로 승격하지 않는 원칙을 유지할 것인가? 추천은 `유지`다. 현재 후보는 조사·결손 분석에는 쓸 수 있지만 현행 통행·보도 폭·오토바이 허용·NPC 길찾기 권위가 아니다.
