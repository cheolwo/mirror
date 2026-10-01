# 30개 행정동 공통 깊이 슬롯 첫 구현

[구현 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r24]

- 상태: `Implemented / FrozenSourcesRecovered / HistoricalBootstrapRegeneratedAndVerified / AdministrativeJurisdictionLedgerReadbackVerified / UnityReviewExported / FocusedEditMode13Of13Passed / ActualGeometryGameViewVerified / LocalPrivateReviewOnly / CurrentPublicationBlocked / MongoCandidateAuthenticationBlocked`
- 기준: [30개 행정동 사가정 참조 깊이 확장 r23](thirty-admin-dong-sagajeong-depth-rollout.plan.r23.md)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 행정동

## 구현 결론

별도 Unity 저장소의 기존 `행정동디오라마검토Snapshot`과 `행정동디오라마검토View`에 행정동 전용 `행정동디오라마깊이Plan`을 붙였다. 역세권 공통 Profile의 여덟 의미 자리와 순서를 맞추되, 역 고유 식별자나 역세권 Profile을 행정동 ID로 바꾸어 쓰지 않는다. 수용된 행정동 상태 사본마다 행정동 전용 계획 ID·입력 결속 hash와 같은 여덟 슬롯이 생기고, View는 현재 검토 가능 슬롯과 대기·미제공 슬롯을 표시한다.

이 구현은 정확한 30개 scope 모두에 같은 구조를 적용하고, 복구한 동결 원본으로 역사 경계 bootstrap의 경계·건물·도로 기초 도형을 다시 생성해 Unity 검토 입력까지 export했다. 이는 `OA-22160` 역사 경계에 기반한 비공개 검토 자료이며, 현행 경계·같은 배포 세대의 건물/건물군·출입구·통행·생활·사업장·환경 자료가 채워졌다는 뜻은 아니다.

## 행정동 전용 여덟 슬롯

| 순서 | 슬롯 | 의존성 | 현재 상태 | 현재 의미 |
| --- | --- | --- | --- | --- |
| 10 | `DataEvidence` | 없음 | `PrivateReview` | 복구한 동결 원본의 길이·SHA-256과 r2 생성·검증·export 계보를 검토한다. 현행 JUSO 원본 검증 완료를 뜻하지 않는다. |
| 20 | `BaseSpatialPresentation` | `DataEvidence` | `PrivateReview` | 역사 경계 bootstrap의 경계·건물·도로 입력 30개를 검토한다. 실제 Game View 증거는 별도 캡처 원장에 결속한다. |
| 30 | `SpatialMeaning` | `DataEvidence` | `Blocked` | 도형만으로 주소·출입구·건물 역할을 만들 수 없어 차단한다. |
| 40 | `Mobility` | `DataEvidence` | `Blocked` | 도로 중심선만으로 보행·차량 통행을 확정할 수 없어 차단한다. |
| 50 | `Interaction` | `BaseSpatialPresentation` | `PrivateReview` | 기존 건물 도형 선택을 읽기 전용으로 검토한다. 실제 Runtime 입력은 아직 검증하지 않았다. |
| 60 | `LifeSimulation` | `Mobility` | `NotProvided` | 생활 Simulation 입력을 제공하지 않는다. |
| 70 | `BusinessOverlay` | `DataEvidence` | `NotProvided` | geometry-only 검토에서 사업장·광고·후원 표시를 제공하지 않는다. |
| 80 | `EnvironmentPresentation` | `BaseSpatialPresentation` | `NotProvided` | 날씨·시간대·대기 표현 입력을 제공하지 않는다. |

`DataEvidence`, `BaseSpatialPresentation`, `Interaction`의 `PrivateReview`는 검증된 동결 입력과 역사 bootstrap을 이용한 비공개 관찰 후보라는 뜻이다. 현행 공식 원본 수집 완료, 실제 사용자 입력 검증, 공개·배포 승인이나 E 승격을 뜻하지 않는다. `SpatialMeaning`과 `Mobility`는 근거가 부족해 `Blocked`, `LifeSimulation`, `BusinessOverlay`, `EnvironmentPresentation`은 입력 자체가 없어 `NotProvided`로 구분한다.

## 식별자·권위·Host 보존

- 행정동별 계획은 행정동 `AdministrativeAreaStableId`에 결속하고 공통 슬롯은 `administrative-dong-diorama-layer:*` 이름 공간을 쓴다. 역 고유 식별자와 `StationEvidenceProfile`은 변경하지 않는다.
- 기존 `ActiveStationDioramaHost`, 역세권 Adapter·Catalog와 사가정역 1km 관찰 창은 수정하지 않는다. 행정동 계획은 기존 행정동 상태 사본 안에서만 생성한다.
- 계획은 `ObservationPresentationOnly=true`, `ChangesOperationalState=false`, `DistributionApproved=false`, `TraversalReady=false`, `GameplayReady=false`, `RuntimeVerified=false`를 유지한다. 서버가 운영 상태의 최종 권위다.
- `BusinessOverlay`는 `NotProvided`로 남고 기존 공개 사업장·Claim·광고·후원 관문을 우회하지 않는다. 건물 선택도 운영 상태 변경이나 업체 표시 승인이 아니다.
- Scene·Prefab, 서버 API 계약, DB current pointer와 역세권 공통 Profile은 이번 절편에서 변경하지 않는다.

## 복구·재생성 결과

| 대상 | 확인 결과 | 판정 |
| --- | --- | --- |
| 서울시 `OA-22160` 역사 행정동 경계 | 1,676,539바이트, SHA-256 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68` | 역사 경계 bootstrap 원본 복구 |
| 서울 `AL_D010` 건물 원본 | 135,675,376바이트, SHA-256 `674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755` | 동결 건물 원본 복구 |
| 2026-08-12 국가표준 NodeLink | 269,611,477바이트, SHA-256 `5BBF5A01D677B6DCB941CC5954FCC256D6DED60D96F95336A23FC2B53B39D4E4` | r2 입력 세대 복구 |
| 행정안전부 행정동-법정동 원본 | 2,129,571바이트, SHA-256 `8AF8C1F122D67D43518F58B37AEA6EEA7986F2809062F24E2E03465F21AE7A08` | 동결 원본 복구·정규화 |
| 로컬 MySQL 행정동-법정동 관할 원장 | 정규화 25,739행(`행정기관` 3,922·`관할` 21,817), 첫 적용 25,739행, 재적용 신규 0, 독립 재조회 정확 30개 행정동·12개 법정동 | 비공개 원천 원장 복구 완료; 운영 권역 확정 아님 |
| r2 batch | 정확 30개 행정동·건물 61,897개·scope 미해결 27개·도로 선분 11,769개·타일 297개, build/verify 통과 | `HistoricalBoundaryBootstrapOnly`, 배포 승인 없음 |
| Unity 검토 export | generation `C6CD2501CD2FC876BFF671FB25C36C139AFDAF14F78EC2FD3F7503E31A1153AC`, index SHA-256 `C8D4999494767E9588E998100893A2D4227A8E116DEBD8F8B81611BA94D3E123`, bundle 30개·기초 도형 준비 30·생활 준비 0 | `currentPointerUsed=false`, `currentPointerUpdated=false`, `publishBlocked=true` |
| MongoDB 디오라마 candidate | apply와 verify 모두 보존 볼륨 인증에서 `MongoAuthenticationException`; `databaseWriteAttempted=false`, `committed=false` | candidate·current 모두 쓰지 않음; 보존 자료를 지우지 않는 인증 복구 뒤 재개 |

복구된 동결 원본은 역사 bootstrap과 Unity 비공개 검토 입력의 재생성을 지지한다. MySQL 적용 대상은 디오라마 candidate가 아니라 행정동-법정동 관할 원천 원장이며, 이 적용은 current pointer·게시·배포·운영 권한을 만들지 않는다. 현행 JUSO 경계와 같은 배포 세대의 건물·건물군·출입구 관문은 계속 열려 있다.

## 공식 원천 접근 재확인 · 2026-09-26

- 주소정보누리집의 현행 행정동 경계 제품은 `구역의 도형`의 `TL_SCCO_GEMD`다. 같은 세대의 건물·건물군·출입구는 `도로명주소 전자지도`의 `TL_SPBD_BULD / TL_SPBD_EQB / TL_SPBD_ENTRC`로 묶어 신청하는 구성이 맞다. [공식 다운로드 목록](https://business.juso.go.kr/jst/jstAddressDetailsSearch)에서 현재 선택 가능한 최신 월전체는 2026.08이었고, 자료 선택·장바구니·신청과 관할기관 승인을 거쳐야 하므로 로그인 없는 자동 수집 대상으로 처리하지 않는다.
- 공공데이터포털 `15083092`의 GIS건물통합정보는 [VWorld 제공 화면](https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=NA&dsId=18)으로 연결된다. 포털 이용조건은 공공저작물 출처표시 제1유형이지만 VWorld 실제 다운로드는 로그인이 필요하다. 현재 서울 전체분은 기준일·갱신일 2026-09-09, 129MB로 표시됐으며 이번 절편에서 내려받지 않았다.
- 공공데이터포털 `15025526`의 표준노드링크는 [ITS 공식 자료실](https://www.its.go.kr/nodelink/nodelinkRef)의 익명 세션·CSRF 절차로 2026-09-14 `NODELINKDATA.zip`을 내려받아 검증했다. 파일은 271,260,899바이트, SHA-256 `FA0392BFDC919B77E7892CB2D73BC59D1DB03580DA1927764EE913C0C8E47140`이며 `artifacts/local/public-data/its-nodelink-20260914-r1/`에 별도 revision으로 보존한다. r2는 계속 2026-08-12 NodeLink를 사용하며 최신 파일을 기존 세대에 섞지 않았다.
- 역사 bootstrap 자료 채움과 비공개 Unity 검토 export는 완료했다. 현행 디오라마 정본의 첫 관문은 계속 `BlockedExternalAccess`다. JUSO 승인 원본을 확보하면 새 revision에 원본 hash·기준월·권리조건을 기록하고 정확 30개 집합을 검증한 뒤에만 새 DB candidate와 Unity 입력을 만든다. 이번 실행은 JUSO 로그인·신청·승인을 우회하지 않았고 current pointer·게시·배포·운영 권한을 변경하지 않았다.

## 변경 범위

별도 Unity 저장소 `C:/Users/user/ssalddel`:

- `Assets/Ssalddel/Runtime/World/행정동디오라마검토Models.cs`
- `Assets/Ssalddel/Presentation/World/행정동디오라마검토View.cs`
- `Assets/Ssalddel/Tests/EditMode/행정동디오라마검토Tests.cs`

Hongdal에는 이 구현 기록과 기획 색인 외에 `eng/Ssalddel.PublicDataPortalImport/행정동관할원장복구.cs`와 해당 명령 등록을 추가했다. 로컬 동결 원본·r2 생성물·Unity 검토 export는 `artifacts/local`의 비공개 증거이며 current 게시물이 아니다. 관련 없는 기존 변경은 보존한다.

## 검증 상태와 남은 placeholder

- Unity 6000.5.6f1의 집중 EditMode `행정동디오라마검토Tests`는 실제 export generation을 둔 최종 소스에서 **13/13 통과**했다. 결과는 `C:/Users/user/AppData/LocalLow/DefaultCompany/ssalddel/TestResults-admin30-20260926.xml`이며, 면목제3·8동/면목본동의 동일 8슬롯, 계획 hash 결정성, 권위 false, 원본 DTO와의 분리, View의 동 교체·거절·Clear 시 계획 잔류 없음까지 확인했다.
- `Ssalddel.PublicDataPortalImport.csproj` 직접 build는 최종 소스에서 경고 0·오류 0으로 통과했다. Unity 실제 컴파일과 집중 시험도 별도로 통과했다.
- r2 생성기 `verify`는 행정동 30·건물 61,897·미해결 27·도로 선분 11,769·최대 길이 99.86221m로 다시 통과했다. 강화한 관할 원장 검사는 raw snapshot 1개와 저장 행 25,739(`행정기관` 3,922·`관할` 21,817)을 독립 재조회했고, 재적용은 신규 0·기존 일치 25,739였다. importer build는 경고 0·오류 0, 지정 파일 Fast·Task와 양쪽 저장소 `git diff --check`도 통과했다. 최종 Fast·Task 상세 로그는 `artifacts/local/validation/20260926-112026`이며 경로 분류상 build/test는 건너뛰어 앞의 명시 build와 실행 검증을 별도 근거로 둔다.
- canonical `SimulationWorldShell`의 실제 Play Mode에서 30개 행정동 개요 30장과 면목제3·8동 건물 선택 상세 1장을 만들었다. 캡처 원장 `C:/Users/user/ssalddel/Documentation/Changes/2026-09-26-administrative-dong-common-depth/capture-manifest.json`의 SHA-256은 `844F6EF27C1B7A6A1F23DF20E22931D195B5A0648234FDC04E90B3F3C55CF0D7`이며 `actualPlayMode=true`, `actualGameViewCapture=true`, `sceneHashUnchanged=true`, `sceneDirtyAfterPlay=false`다. 30개 모두 Mesh 4개·Collider 0개이고 이미지 hash 재검증도 통과했다.
- 캡처 중 Console 오류 8건은 `SimulationReplayHashMismatch`, 로컬 서버 미실행에 따른 네 가지 session/request 실패, 다른 World 표현 두 건과 종료 시 한 건이다. 행정동 검토 코드 stack을 가진 오류는 0건이지만 canonical Scene 전체가 오류 0이라는 증거는 아니므로 그대로 결손으로 기록한다.
- `DataEvidence`의 동결 원본·재생성 계보와 `BaseSpatialPresentation`의 실제 Game View는 확인했다. 현행 JUSO 원본 계보와 `Interaction`의 실제 마우스 입력 검증은 남아 있다. 코드 안의 `RuntimeVerified=false`는 캡처가 운영·배포 상태를 바꾸지 않도록 유지한다.
- `SpatialMeaning`, `Mobility`, `LifeSimulation`, `BusinessOverlay`, `EnvironmentPresentation`은 표의 `Blocked / NotProvided` 상태가 실제 placeholder다. 다른 동의 값이나 사가정 전용 값을 복사해 채우지 않는다.
- 현행 행정동 경계·동일 세대 건물/건물군·출입구, 실제 필지 도형, 보행면·연석·정지선·신호 현시와 현재 통행 graph는 계속 미확인이다.
- 로컬 MySQL 행정동-법정동 관할 원장은 적용·동일 입력 무쓰기·독립 재조회를 완료했다. MongoDB 디오라마 candidate와 실제 HTTP/API, Scene·Prefab 저장, 실제 마우스 입력, 공개·배포, E 승격, commit·push는 완료 주장에 포함하지 않는다.

기존 여덟 슬롯의 의미를 행정동 전용 상태 사본에 적용한 좁은 구현이며, 역세권 공통 규칙을 바꾸거나 새로 승격하지 않는다. **새 디오라마 규칙 후보 없음**.
