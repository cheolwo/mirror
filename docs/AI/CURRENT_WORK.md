# Mirror(거울) Current Work

## 동북서울 30개 행정동 디오라마 자료 기준선 r13 (2026-09-15)

- 사용자는 30개 행정동 모듈을 사가정역 검토 깊이로 계속 발전시키고, 현재 가능한 화면을 보존하되 부족한 주소·필지·도로·신호·생활 자료를 추가 수집하도록 요청했다. [기획 r13](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/README.md), [깊이 기준·캡처 r5](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-depth-parity.implementation.r5.md), [주소 후보 r6](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-address-parcel-candidate.implementation.r6.md), [횡단보도 r8](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-crosswalk-candidate.implementation.r8.md), [사업장 첫 판 r9](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-business-candidate.implementation.r9.md), [사업장 개인정보 정정 r12](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-business-candidate.implementation.r12.md), [교차로 r10·r11](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-intersection-point-ledger.implementation.r11.md), [도보망 r13](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-walk-network-candidate.implementation.r13.md), [G3c 원장 검증 결과](../Reports/동북서울-30개-행정동-도보네트워크-G3c-후보-2026-09-15.md)에 단계별 결손과 검증 상한을 고정했다.
- 역사 경계 후보의 건물 61,897개·도로 선분 11,769개·tile 297개를 hash별 완성 세대로 내보냈다. 같은 입력의 재내보내기 두 번은 모두 변경 0이고 index·bundle·`complete.json` 독립 검증이 통과했다. 격리 Unity에서 canonical `SimulationWorldShell`에 한 번에 한 동만 결합 Mesh 4개·Collider 0개로 조립했으며 EditMode 9/9와 실제 Play Mode Game View 전체 조망 30장·면목제3·8동 근접 1장을 남겼다. 원본 PNG 31개는 hash 불일치가 없고 Scene은 저장하지 않았다.
- 30개 연락판의 넓은 공백과 분리된 건물 군집은 실제 공터나 완성 표현이 아니라 역사 경계·현재 건물/도로 기준일 불일치와 이동·생활 layer 결손을 드러내는 `MissingCoverage`다. 화면은 `G6 UnityBaseView / PrivateReviewOnly`이며 사가정 생활상 동등성, 실제 통행, gameplay 또는 공개 배포 증거가 아니다.
- 동결 `AL_D010` PNU와 행정안전부 2026-08 건물DB를 결합해 61,897개 건물의 주소 후보 상태를 로컬 MySQL에 저장했다. 단일 후보 49,876·복수 1,351·동결 판본 내 후보 없음 10,670, 후보 보유율 82.76%이며 별도 연결 exact set·분포·투영 hash `4ceff3d6582a267aca57f972dd05a16e0c9b223229dea733fe9e7fb232be5a6b` 일치를 확인했다.
- `OA-23081` 횡단보도 점은 정정 r2 후보 1,533개·보행등 설치 933·역사 경계 자치구 충돌 21을 로컬 MySQL에 저장했다. 후보 hash는 `78312E5F0DDB89BFFA9AD1881379CBF7E8037A6C6454D4F0ABDF2A3E4D22D30A`이며 반복 적용 신규·갱신 0과 독립 재조회가 통과했다. 첫 r1은 `RejectedAfterIndependentAudit / PreservedNotPromoted`다.
- `OA-15534` 교차로 원천 점 8,097개 중 30개 동에 단일 귀속한 554개를 별도 G3b 세대로 생성하고 로컬 MySQL 보호 원장에 후보 554행·계보 사본 19건을 저장했다. 횡단보도와 연결된 교차로 524·미연결 30, 관계 1,491개 중 다른 역사 행정동 연결 220개를 별도 진단으로 유지하며 후보 hash는 `DB83F1CC82EB65F864188DD790A003EDF570DBB459D56D4C07CE690988F2837B`다. Python 15/15·C# 16/16, 별도 연결 exact set·30개 동·hash 재조회, 반복 적용 신규·갱신·사본 0이 통과했다. G3a r2, G4a r1, 기존 면목 원장의 적용 전후 digest는 같으며 이후 별도 생성한 G4a r2는 이 r11 보존 계약 밖이어서 후속 G3b 판본에서 명시적으로 결속해야 한다.
- G3b r1의 기존 동반 자료 계약은 실행 전 상태인 `ledgerPersistenceCompletedClaimed=false`와 `consumedCandidateDatabasePersistenceCompleted=false`를 유지하지만 이후 실제 DB 저장·재조회 결과는 완료로 보고되어 있다. 이는 데이터 손실이 아니라 기계 계약과 사후 실행 상태의 알려진 flag 정합성 결손이며, 기존 판본을 조용히 수정하지 않고 후속 호환 판본에서 정리해야 한다.
- `OA-21208` 2020년 WGS84 도보망은 세 자치구 공식 CSV 59,724행과 유형 코드북을 동결하고 2023 역사 경계에 결속해 NODE 17,267·LINK fragment 23,472, 합계 40,739개 G3c 후보를 생성했다. 후보 hash는 `43248F7F563DCDFE059650F3EC000085CD0D7CFB65FC5300EFDFCBDD685990AA`이며 Python 자체 시험 17/17, 1mm 직렬화 경계 재포함, 원선 대상 교차 길이 882,519.308089m 보존, 최종 세대 verify가 통과했다. C# importer도 전체 98,781,674-byte 후보의 framed hash를 독립 재계산한 뒤 로컬 MySQL에 40,739행·30개 동·계보 사본 17건을 저장했다. build 경고 0·오류 0, 자체 시험 16/16, 새 연결의 exact set·hash 재조회가 통과했고 반복 적용은 신규·갱신·사본 0이다. 기존 보호 상태 71,501행·사본 84건·실행 84건의 digest는 전후 같았으며 최종 독립 정적 감사는 High·Medium·Low 0이다.
- 소상공인시장진흥공단 2026-06-30 전국 상가 2,772,484행을 전수 대조해 30개 동 사업장 후보 29,721개·음식 업종 8,246개를 G4a 보호 원장으로 만들었다. 첫 r1은 독립 개인정보 감사에서 선택된 원천 식별자 하나가 추적 생성기의 시험 fixture에 들어간 사실을 확인해 `RejectedAfterIndependentPrivacyAudit / PreservedNotPromoted`로 낮췄고 값은 반복하지 않는다. 정정 r2는 실제 원천에 없는 합성 fixture와 새 dataset/revision을 사용하며 후보 hash는 `5F9C61BD71112C69EE3988CFB3B8542AE22BF60AB95D542C702D2355B8A3774C`다. Python 25/25·C# 59,482/59,482, 최초 MySQL 후보 29,721·사본 19 저장, 반복 신규·갱신·사본 0, 별도 연결 exact set·30개 동·음식 8,246·진단 35·결손 67·보호 필드 14 재조회를 통과했다. r1 29,721행·사본 19·실행 19의 상태 digest와 기존 면목 5,411·공장 126행은 적용 전후 같았다. 독립 재감사는 tracked 10,757개·untracked 134개와 r2 집계 산출물에서 선택 원천 식별자 교집합 0, High 0·Medium 0을 확인했다.
- 주소는 `G2a`, 횡단보도는 `G3a`, 교차로는 `G3b`, 도보망은 `G3c`, 사업장은 `G4a`로 분리한다. G3c는 local-private 후보 생성과 보호 RDB 원장 검증까지만 닫혔다. 현행 행정동 경계, 실제 필지 도형·출입구, 실폭도로·골목·보도·차로·정지선·신호 현시의 현재 graph, 확정 사업장 건물 결속·현재 영업·Claim, G5 NPC·차량·OS 결속과 G7 생활상 Game View는 남아 있다.
- 서버 도구 build, 기존 절편의 Python/C# 자체 시험과 MySQL 독립 재조회, Unity 검토 묶음 무변경 재생성, Unity EditMode·실제 Game View를 분리 검증했다. G3c는 Python 생성·verify와 C# 보호 RDB 첫 저장·독립 재조회·반복 무쓰기를 통과했다. Unity 실행에는 기존 Editor SearchDatabase 예외 1건과 종료 시 JobTempAlloc 경고 2건이 있어 전체 Console 무오류로 보고하지 않는다. 운영 DB·Mongo current·실제 주문/배차/NPC 이동·E 승격·commit·push·배포는 수행하지 않았다.

## 동북서울 30개 행정동 디오라마 역사 경계 후보 r4 (2026-09-14)

- 사용자가 앞서 확정한 배달운영권역 범위의 행정동을 사가정역과 같은 공용 디오라마 입력 모듈로 만들도록 요청했다. [기획 r4](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/README.md), [구현 명세](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/administrative-dong-batch.implementation.r4.md), [결과 보고](../Reports/동북서울-30개-행정동-디오라마-역사경계-후보-2026-09-14.md)에 광진 4·동대문 10·중랑 16, 합계 30개 행정동과 12개 법정동의 범위·출처·검증 상한을 고정했다.
- 서울시 `OA-22160` 페이지 갱신일과 실제 파일 판본을 다시 조사한 결과 ZIP 내부 자료는 2023-10-20, 파일 수정일은 2023-10-31, 425행이었다. 이를 현행 정본이 아닌 `HistoricalOfficialBoundaryBootstrap`으로 낮췄고, 최신 주소정보누리집 `TL_SCCO_GEMD` 서울 전체분을 승인 반입하기 전에는 `WaitingForAdministrativeBoundary / CurrentPublicationBlocked`를 유지한다.
- 결정 생성기는 공통 사가정 ENU에서 30개 경계를 처리해 건물 61,897개를 단일 귀속하고 도로 선분 11,769개를 경계에서 절단했다. 미해결 27개는 경계 밖 13·원본 무효 11·1mm 반올림 뒤 무효 3이며 복수 귀속은 0이다. 반복 생성 `changedFiles=0`, 1µm 경계 허용오차 초과 도로 0, C# 독립 경계 변환 2mm 이내, 투영 입력 거절 0을 확인했다.
- 기존 투영 Builder로 500m tile 297개를 조립하고 로컬 MongoDB candidate collection에 batch 1·manifest 30·tile 297·overlay 30, 합계 358개를 저장했다. 반복 적용은 신규 0·기존 일치 358이고 새 연결 재조회가 통과했다. `administrative_dong_diorama_current` snapshot hash는 적용 전후 같으며 과거 r1 후보는 `PreservedNotPromoted`로 남겼다.
- 최신 JUSO ZIP을 위한 `EPSG:5179` 전용 reader는 파일 구성·코드 유일성·branch 중첩·도형 위상·multipart/hole·삭제 행을 엄격히 검사한다. 이 r4 당시 결과는 서버 비공개 역사 후보이며 주소·필지·업체·광고, 도로 폭·골목·보도·차로·신호·통행, Unity Scene·Prefab·Play Mode·Game View를 구현하거나 검증하지 않았다. 후속 r5·r6의 기초 Game View와 주소 후보 원장은 위 최신 snapshot에 별도로 기록한다. current 게시·E 승격·commit·push도 수행하지 않았다.
- 최종 감사에서 공용 current 투영 hash가 manifest의 시간·좌표 프레임·범위·권위 플래그와 overlay 기준 시각을 전부 포함하지 않고 중복 시 전체 BSON을 대조하지 않는 기존 결손을 확인했다. r2 candidate는 전체 투영 집합 hash와 exact BSON 검증을 사용해 영향을 받지 않지만, 최신 경계 반입과 함께 공용 current 저장 검증을 보강하기 전에는 게시를 열지 않는다.
- Python `verify`·`self-test`, C# preview·standalone Mongo verify, 서버 집중 회귀 34/34, Unity 읽기 계약 4/4가 통과했다. 16개 관련 파일로 제한한 Fast의 diff·두 project build·표적 시험도 통과했고 기록은 `artifacts/local/validation/20260914-224141`이다. 증거 대장 검사는 기존 출처/Simulation/표현 분리와 결손 보존 후보 규칙을 지지했으며 새 디오라마 규칙 후보는 없다.

## 행정동 기반 배달운영권역 Draft 관리 첫 절편 r1 (2026-09-14)

- 사용자가 행정동들을 관리자가 묶어 배달권으로 관리하는 서버 리팩터링을 다른 역 디오라마 확장보다 우선했다. [승인 기획 r1](Planning/운영/PLAN-OPERATIONS-ADMIN-DONG-DELIVERY-TERRITORY/README.md)과 E7 작업 명세에 법정동·행정동·배달운영권역·협력권역·역세권 관찰 창을 서로 다른 축으로 고정했다. 발화의 `중국동`은 후속 `중곡1동` 언급과 공식 코드를 대조해 광진구 법정동 `중곡동`으로 정규화했다.
- 행정안전부 동결 파일 `jscode20260301.zip`, SourceVersion `mois-jscode:20260301:retrieved:2026-08-12`, SHA-256 `8AF8C1F122D67D43518F58B37AEA6EEA7986F2809062F24E2E03465F21AE7A08`, 자료 revision `mois-hjd-bjd-20260301-8af8c1f122d67d43518f`을 `delivery-territory-source-scope:northeast-seoul-rider.r1`에 고정했다. 중곡·전농·답십리·장안·휘경·이문·면목·상봉·중화·묵·망우·신내 12개 법정동에서 활성 행정동 30개를 정확히 해소하며 이후 최신 판본으로 조용히 바뀌지 않는다.
- RDB에 Draft aggregate, 현행 행정동 단일 권역 membership, 관리자 행위자와 당시 결과 사본을 가진 멱등 수신증, revision별 전체 행정동 집합과 행위자를 가진 변경 Outbox를 추가했다. 관리자 API는 후보 모듈·목록·상세·Draft 생성·행정동 전체 교체를 제공하고 `ClientRequestId`, `ExpectedRevision`, 중복 권역 귀속, 고정 자료 범위를 검증한다. Mongo manifest 결손은 `WaitingForSpatialProjection`으로만 표시하며 경계나 타일을 추정하지 않는다. 현재 로컬 `hongdal_dev` MongoDB를 독립 확인한 결과 행정동 디오라마 current·manifest·tile은 모두 0건이므로, 이 환경의 30개 후보는 전부 공간 투영 대기 상태다. 과거 별도 검증 기록의 면목제3·8동 게시 사본을 현재 DB 사실로 오인하지 않는다.
- 서버·계약·EF·관리자 API 및 API 판본 회귀 80/80이 통과했다. 같은 요청의 지연 재시도가 최초 응답 revision을 반환하는지, 반복 제외·재편입의 revision별 Outbox 집합, 관리자 감사 ID, DB 예외 상세 비노출도 포함한다. 로컬 Docker MySQL의 기존 `hongdal_dev`에서 실제 공식 30개를 읽고, 전용 임시 schema에 새 migration 2개·테이블 4개를 적용해 Draft 저장과 새 DbContext 독립 재조회 1/1을 통과한 뒤 transaction과 임시 schema를 제거했다. 범위 지정 Fast는 빌드·표적 시험·코드 지도 검사를 통과했다(`artifacts/local/validation/20260914-204500`). Task의 v3.5 build는 통과했고 전체 시험은 5,268/5,275건 통과했으나 이번 범위 밖 기존 문서·CSS·역할 API metadata·Web route 7건이 실패했다(`artifacts/local/validation/20260914-204639`).
- 기존 `hongdal_dev`의 정상 migration chain 적용은 이번 migration보다 앞선 `20260911050534_SyncPublicDataHrAndFreightContinuity`가 이미 없는 HR index 삭제를 시도해 중단됐다. 따라서 새 배달운영권역 테이블을 기존 개발 schema에 적용했다고 주장하지 않으며 이 선행 schema drift는 별도 복구가 필요하다. 현 EF assembly scan에는 전용 공공자료의 지역 업체·후원 구성이 섞이는 기존 pending-model drift도 남아 있어 새 두 migration은 해당 표를 포함하지 않도록 격리 생성했다.
- 이번 절편은 `Draft` 관리만 구현했다. `Active`, 담당자 배정, 협력권역, 직선 6km 주문 정책, 기존 `플랫폼배달권`·`food-cell:v1`·주문·배차·정산 연결, Unity 계약·Scene·Prefab·Play Mode·Game View는 변경하거나 검증하지 않았다. 기존 “역 중심 창은 권위가 아니다” 규칙을 지지하지만 새 역 자료 관측은 아니므로 새 디오라마 규칙 후보 없음이다. commit·push는 수행하지 않았다.

## 사가정 필지 경계 도형 수집 준비 r7 (2026-09-14)

- 사용자가 실제 필지 경계 도형을 주소 결속의 다음 선행 관문으로 확정했으나 브이월드에 로그인할 수 없다고 알려, 실자료 확보를 `BlockedExternalAccess`로 유지한 채 다음 단계인 검증 가능한 반입 파이프라인을 구현했다. 임의 경계나 합성 fixture를 실자료로 승격하지 않는다.
- 공식 `연속지적도형정보`의 서울 `AL_D002`, PNU 필드 `A1`, EPSG:5186, 기준일 2026-09-08 계약을 확인했다. 공개 컬럼 정의서 224,115바이트를 로컬 비추적 원본 폴더에 보존했고 SHA-256은 `46DD29C6AB681C1E34CF00D91F8F2FE68B7E1868A853315EAA292838238ECB0F`다. 브이월드와 공공데이터포털의 이용조건 표기가 달라 소비 승인은 계속 차단한다.
- [수집 준비 구현 기록 r7](Planning/시스템/PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION/parcel-geometry-collection.implementation.r7.md)과 E7 작업 명세를 추가했다. 도구는 기존 화면 건물 주소 원장의 파일·내용 hash, 화면 건물 4,062개와 고유 PNU 3,774개를 먼저 확인하고, 공식 ZIP의 CRC·SHP 구성·CRS·필드·도형 유효성·coverage·결정적 hash를 검사한다.
- 현재 readiness는 `PresentationBuilding=4062 / TargetParcel=3774 / GeometryParcel=0`, `VWorldLoginRequiredAndNoOfficialArchiveAvailable`이다. 따라서 실제 필지 도형 저장, Mongo 투영, 서버 소비 API, Unity 결속, 배달·가격·업체·통행·게임 권위는 구현하거나 열지 않았다.
- 합성 `AL_D002` SHP는 파서·좌표계·중복 union·누락·결정성 자체 시험에만 사용하며 출처 증거가 아니다. 필지 전용 시험과 증거 대장 시험이 통과했고 자체 시험은 10/10이다. 역세권 디오라마 증거 대장은 r3으로 갱신했지만 `ParcelGeometry=NotCollected/Missing`, 공통 규칙 후보 12개와 승인·적용 0개를 유지한다. 범위 지정 Fast의 `git diff --check` 기록은 `artifacts/local/validation/20260914-191042`이며 제품 build/test는 대상 밖이라 생략됐다.
- 실제 재개점은 공식 서울 `AL_D002` ZIP, 확인한 SHA-256, 출처 판본을 `artifacts/local` 아래에 제공하는 순간이다. commit·push는 수행하지 않았다.

## 사가정 화면 건물 주소·필지 증거 첫 절편 r6 (2026-09-14)

- 사용자는 [건물 주소 완결 기획 r6](Planning/시스템/PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION/README.md)의 두 원장 첫 절편과, 역세권 디오라마 증거 체계에서 도로명주소·필지 식별자·실제 필지 경계 도형의 수집 여부를 각각 확인하는 관문을 확정했다. [구현 기록 r6](Planning/시스템/PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION/implementation.r6.md)에 현재 결과와 상한을 분리 기록했다.
- 결정 생성기는 화면–기준 건물 결속을 `Bound 544 / BackdropOnly 3,423 / AmbiguousGlobal 88 / AmbiguousMultiple 2 / WeakCandidate 5`, 기준 건물을 `Bound 544 / Unresolved 58`로 분류했다. 화면 건물 주소 상태는 `ParcelAddressCandidate 3,278 / ReferenceBindingCandidate 516 / MultipleAddressCandidates 47 / CrossSourceConflict 18 / Unresolved 203`으로 4,062/4,062를 닫았으며 공식 승격·최근접 복사는 0이다.
- 로컬 Docker MySQL `hongdal_dev`에 결속 dataset 5,211행과 주소 dataset 7,582행, 합계 12,793행을 저장했다. 전체 scoped dataset의 정확 key 집합·수·원본 snapshot 111·112 연결·hash를 새 문맥에서 재조회하고, 같은 입력의 재적용·재생에서 신규 쓰기 0을 확인했다. 반입기 자체 검사는 rogue extra·누락·도로명주소 stable ID 공식 변조를 포함해 30건 통과했다.
- 서버에는 `api/v1/admin/world/stations/{transitStationStableId}/diorama-building-evidence/*` 아래 manifest·결속·주소 읽기를 추가했다. 서버 관리자 정책과 Development 환경을 함께 요구하고, ETag·비공개 cache·사가정 범위·정규화 행 수·단위·시각·원본 snapshot·파일 hash/길이와 도로명주소 stable ID 공식을 검증하며 결손·변조는 503으로 닫는다. 집중 시험 15/15, API 판본 회귀 56/56, 실제 DB fresh-reader probe가 통과했다.
- 디오라마 증거 대장 r2에서 사가정은 `RoadAddress Collected 4,062/4,062`, `ParcelIdentifier Collected 4,062/4,062·고유 PNU 3,774`, `ParcelGeometry NotCollected 0/3,774`다. 면목·용마산은 세 항목 모두 `NotAssessed`다. 각 검사에는 `applicationAuthorized=false`가 필수이며 주소·PNU·건물 도형으로 실제 필지 경계를 대신하지 않는다.
- 체크리스트는 필수 정책으로 확정했지만 기존 디오라마 보편 규칙 12개는 계속 `Candidate`다. `ProvisionalSharedRule`·`AcceptedSharedRule`·E 단계 승격은 하지 않았고, 역 Graph Map schema와 현행 인계 도구의 호환 차단도 남아 있다.
- 문서까지 포함한 최종 범위 지정 Fast는 `artifacts/local/validation/20260914-172712`에서 통과했다. Task의 solution build는 통과했고 전체 시험은 5,250/5,257건 통과·이번 범위 밖 기존 dirty 작업 7건 실패로 기록됐다(`artifacts/local/validation/20260914-165846`). 이번 절편은 비공개 자료 준비·RDB·Development 관리자 API까지이며 Unity·Scene·Prefab·Play Mode·Game View, 실제 필지 도형, 배달·가격·업체·통행·공개 권위는 변경하거나 검증하지 않았다. commit·push도 수행하지 않았다.

## 사가정역 1km 공간 보충 자료 수집·원장화 r27 (2026-09-14)

- [구현 기록 r27](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-spatial-supplement.implementation.r27.md)에 따라 기존 사가정 1km 창과 건물·도로 자료를 보존하면서 서울 열린데이터광장의 보행망 `OA-21208`, 역 엘리베이터 `OA-21212`, 공원 `OA-15529`, 버스 정류소 `OA-15067`, 가로수 `OA-1325`를 공식 링크·기준일·hash와 함께 비공개 동결했다.
- 기존 WGS84 창에서 보행망 2,902·엘리베이터 1·공원 정체성 1·버스 정류소 30·가로수 292, 합계 3,226건을 `PendingHumanReview`로 정규화했다. WGS84 자료에는 기존 사가정 ECEF→ENU 중심 좌표를 붙였지만 Runtime·통행·Collider·NavMesh·gameplay 권위는 주지 않았다.
- 로컬 MySQL에 첫 적용 신규 행 3,226·원본 5를 저장했고, 재적용 신규 0·기존 3,226·원본 신규 0과 독립 재조회 행 3,226·원본 5를 확인했다. importer build 경고·오류 0, 자체 검사 14/14가 통과했다.
- 공원 SHP는 EPSG:5174 원본·정체성까지만 저장해 경계 변환을 보류했다. NGII 수치지도 V2·수치표고모형은 로그인·전용 전송 도구가 필요한 `BlockedExternalAccess`로 남겼으며 fallback하지 않았다. 새 시각 원본, Unity Adapter·Scene·Prefab·Play Mode·Game View, E 증거 승격, commit·push는 수행하지 않았다.
- 디오라마 증거 대장에 출처 기록 1건과 “보충 공간층은 원천별 기준일·좌표계·권위를 보존한다” `Candidate` 1건을 추가했다. `applicationAuthorized=false`이며 공통 적용 승인은 하지 않았다.

## 사가정역 출구 방향 기준점 첫 구현 r26 (2026-09-14)

- 사용자는 역세권 전체 건물의 외관을 사진처럼 복원하는 것을 완료 조건으로 두지 않고, 실제 자료에서 확인한 배치·높이를 우선 보존하기로 확정했다. 일반 건물은 건물별 외곽·높이·도로 관계와 일관된 동네 외관 문법을 유지하며 확인하지 않은 실제 외관을 주장하지 않는다.
- 첫 세부 표본은 사용자가 확정한 사가정역 1~4번 출구다. 기존 동결 OSM 사본의 node/version·좌표를 `sagajeong-reference.r3`과 같은 WGS84 ECEF→ENU로 투영한 별도 `SagajeongStationOrientationAnchors.json`을 만들었다. 기존 지도 hash와 출구 자료를 서로 결속하고 다른 역·임의 좌표 fallback을 금지했다.
- 별도 Unity 저장소에는 출구당 일반화 저상 구조·차양·7호선 색상 번호 표식과 충돌 회피 화면 라벨을 추가했다. 사진 외형을 복제하지 않았고 `TraversalReady=false`, `InteractionReady=false`, `OperationalAuthority=false`, Collider 0을 검사한다. 자료 오류 시 지도는 유지하고 출구 표식만 보류한다.
- 생성기 재실행 resource SHA-256 `D1A6874F86512A67F40B96F8D054C61F51F8E6226010150F781E694223309BAB` 일치, 격리 Unity 신규 시험 4/4와 기존 사가정 View·모형·H 선택 회귀 21/21을 통과했다. 원본 Unity Pipeline은 연결 불가여서 canonical `SimulationWorldShell` Play Mode·Game View·Console은 새로 검증하지 않았다. Scene·Prefab·운영 API·Simulation·Graph Map·E 승격·commit·push는 수행하지 않았다.
- 디오라마 증거 대장의 기존 OSM·역별 Profile·교차 역 fallback 금지·절차적 표현 규칙을 지지하고, “번호별 역 출구는 역 Profile의 방향 기준점”을 사가정 단일 표본의 새 `Candidate`로 등록했다. 공통 적용은 승인하지 않았으며 대장 검사는 `Sources=6 / Rules=11 / Candidates=11 / Provisional=0 / Accepted=0 / Stations=3`으로 통과했다.

## 중랑구 전통시장 시각·모델링 자료 첫 수집 r24 (2026-09-14)

- [수집·구현 기록 r24](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/jungnang-traditional-market-visual-collection.implementation.r24.md)에 따라 중랑구 공식 소식지 2016년 9월호 PDF를 비공개로 동결하고 우림·동부·면목·동원·사가정의 설명·당시 주소·사진이 함께 남는 시장별 패널 5장을 만들었다. 원본 PDF SHA-256은 `8f5ede7e2a31068f421fa2cf977dce9da78b250ac3b903c115cd4df8cc05e0a4`, 수집 영수증 SHA-256은 `5cff086303232496ea599961f8aaddbe198035d947a9364111252afd7bd076c2`다. 원본과 패널은 `artifacts/local/public-data/jungnang-traditional-market-visuals-20260914-r1/`에만 보관한다.
- 기존 2025-11-10 전국전통시장표준데이터 SHA-256 `13ffd04a946ec7eec28222c8c3762f2e77aab919cd250888cdaaf7f51cce303b`에서 현행 후보 7행을 연결했다. 2016년 면목시장 이름은 현재 면목시장 행과 같지만 주소는 면목골목시장 행과 같고, 동원은 현행 행이 두 개이며, 동부도 현행 주소가 달라 모두 자동 병합하지 않았다. 우림·사가정의 이름/주소 일치도 정본이 아닌 사람 검토 후보로 유지한다.
- `중랑구전통시장시각자료` 수집·검증 경로와 `jungnang-market-visual-*` 명령을 추가했다. 로컬 Docker MySQL `hongdal_dev`에 시각 참고 5건, 정체성 검토 5건, 권리 경계 1건을 저장했고 첫 적용 신규 11, 같은 입력 재적용 신규 0·기존 11, 독립 재조회 11을 확인했다. 자체 검사 17건과 도구 build 경고 0·오류 0도 통과했다.
- 기계 판독 대장 `eng/world-seedbeds/station-landmarks/jungnang-traditional-market-visual.collection.r1.json`과 [수집 보고](../Reports/중랑구-전통시장-시각자료-첫수집-2026-09-14.md)를 추가했다. 디오라마 증거 대장은 새 출처 1건과 `옛 랜드마크 명칭·주소와 현행 행을 자동 병합하지 않는다` 후보 1건을 추가해 `Sources=6 / Rules=10 / Candidates=10 / Provisional=0 / Accepted=0 / Stations=3` 검증을 통과했다. 후보의 공통 적용은 승인하지 않았다.
- 범위 지정 Fast는 `git diff --check`를 통과했으며 기록은 `artifacts/local/validation/20260914-131534`다. 공용 검사기가 `eng` C#을 guidance-only로 분류해 build/test를 생략했으므로, 별도 수행한 도구 build·자체 검사·원장 재조회와 구분한다.
- 공식 발행물이라는 사실만 확인됐고 개별 사진의 상업 이용·변형 허락은 확인되지 않았다. 모든 사진은 `PrivateReviewOnly / ItemLevelRightsUnverified`이며 Blender 파생 모델·Unity 자원·Scene·Game View·배포는 수행하거나 승인하지 않았다. 현행 출입구·시장 외곽·중심선·골목 폭·차양 높이와 사람·차량·상표 검토도 남아 있다. commit·push는 수행하지 않았다.

## 지역 운영 생명주기 E2E·전국 확장 뼈대 제안 r6 (2026-09-14)

- 사용자는 사가정역을 첫 깊은 표본으로 유지하면서 합성 운영 자료로 주문자·음식점·음식 배달 기사 역할 앱을 단계적으로 E2E 검증하고, 같은 진행을 Unity 디오라마에서 읽되 후속 전국 지역 확장을 막지 않는 뼈대를 제안서로 먼저 정리해 달라고 요청했다. [제안서 r6](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD/README.md)의 전국 확장 뼈대는 아직 `Proposed / ReadyForReview`이고, 역할별 작은 `AvailableActions`, 격리 서버/API, 역할 앱 Client headless, 진행 중 비식별 지역 투영과 Unity Client·Interpreter 메모리 소비까지 별도 승인·구현·검증했다.
- 현행 `FoodDeliveryOS`, 실제 역할 API·Outbox·완료 투영, 격리 `FoodObserver`, 앱별 Client/ViewModel, 8사례·77단계 관찰 timeline, 앱–관찰 대장, 지역 Experience Package와 사가정 r20을 대조했다. 역할 앱 Client→서버→진행 중 비식별 HTTP projection은 한 실행으로 결속됐지만, 네이티브 앱 UI와 Unity live 소비는 아직 같은 증거로 묶이지 않았다.
- 제안은 신규 정본을 `지역생명주기E2eProfile`, `지역검증FixturePack`, `E2e실행EvidenceManifest` 세 개로 제한한다. 앱 능력은 기존 API metadata·앱 대장에서 생성하고 공간 해석은 기존 `OsLifecycleSpatialBinding`을 versioned reference로 재사용한다. 역은 표현 Anchor로만 두고 주문 `WorkStableId`와 운영 원장은 전국 공통으로 유지하며, 기존 자율 생활의 `SimulationAnalog`와 격리 운영 결과의 `ReadOnlyProjection`을 합치지 않는다.
- 기존 앱–관찰 대장의 `SimulationAnalog`는 보존하고 별도 운영 검증 Profile과 세 앱의 `ReadOnlyProjection` 결속을 추가하는 후보로 정리했다. 기존 EF/RDB 음식배달 완료 투영과 후속 진행 투영을 안정 정본으로 두고 Mongo는 Region Experience용 재생성 가능 feed/cache로만 사용해 이중 정본을 피한다.
- 첫 구현 후보는 사가정 정상 음식배달 한 건이다. 완료 수준은 `SkeletonProof → SagajeongVerticalProof → DeviceUiProof`로 나눠 공통 Client/ViewModel, 실제 플랫폼 UI, 운영 서버·DB·Event/Outbox, 진행 중 비식별 projection과 Unity 관찰을 별도 증거로 결속하고 두 번째 합성 지역 Profile로 하드코딩 부재를 검사한다. 이번 첫 절편은 주문자·음식점·기사 상태 응답에 `ActionId`·revision 종류·예상 revision·만료·환경 요구사항만 싣고 세 앱의 기존 버튼을 이 목록에 결속했다. 실제 Command의 역할·상태·revision 재검증은 유지했고 Unity·Graph Map·Scene·DB schema·전국 Profile은 변경하지 않았다.
- 역할별 투영·조회·주문자 ViewModel 집중 시험 25건과 범위 지정 Fast 검증은 통과했고 Fast 기록은 `artifacts/local/validation/20260914-133558`이다. 이후 `FoodObserver` Runner에 단계별 `AvailableActions` 단언을 추가했고 관련 집중 시험 20/20과 서버 build가 통과했다. `OrdererApp`, `RestaurantDeskApp`, `FDriverApp` 빌드도 오류 0으로 통과했다. Task의 solution build는 통과했지만 전체 시험은 이번 범위 밖 기존 작업의 Web route capability 1건, 역할 API metadata 4건, 재료 화면 CSS 1건, 아키텍처 문구 1건 등 7건 때문에 실패했으며 기록은 `artifacts/local/validation/20260914-133300`이다.
- Windows 예약 포트 `5141–5240`과 겹친 기존 `5215` 때문에 첫 기동이 차단된 사실을 확인했다. 검증 도구의 loopback 기본 포트를 `5321`로 바꾸고 명시 포트 선택을 지원하되 `127.0.0.1` 이외 주소는 계속 거절하도록 보완했다. 기존 결과와 DB 볼륨은 삭제하지 않았다.
- 최신 서버 이미지와 새 전용 MySQL·MongoDB·App 볼륨 `12777908f7c74bd283fbe8e5d136b8f0`에서 실제 300초 정상 역할 API 폐루프를 실행했다. 실행 `84ed7de3ee3c48908866fb2439fa4f6d`는 합성 역할 4명·사건 15건으로 주문 `FOOD-20260914044839651`을 `수령확인 / 배달완료`까지 진행하고 299.7초에 `Completed`로 끝났다. 단계별 예상 `AvailableActions`와 완료 뒤 빈 목록 단언도 같은 실행에 포함됐으며 결과는 `artifacts/local/verification/food-observer/result.json`에 저장했다. 이는 실제 영업 증거가 아니고 검증 컨테이너는 종료했다.
- 이 300초 실행까지의 증거 상한은 `IsolatedServerApiProofPassed`였다. 첫 장치 순서는 Windows 주문자·음식점·기사 3앱으로 확정했지만, 이 실행 자체는 앱 UI·진행 중 지역 projection·Unity를 포함하지 않았다.
- Windows `OrdererApp` 프로세스와 `살뜰 주문` 창 제목까지 확인했지만 현재 UI 제어 연결에는 네이티브 앱 상태·입력 API가 없어 내부 로그인·클릭·화면 판독을 수행하지 못했다. 이를 UI 성공으로 올리지 않고 프로세스를 종료했다.
- UI 직전 G3 검증을 위해 [역할 앱 Client headless 실행기](../../eng/Ssalddel.RoleAppHeadlessE2E/README.md)를 추가했다. 주문자 공용 Client와 `OrdererApp` 인증 Client, `RestaurantDeskApp` 주문·인증 Client, `FDriverApp` 업무·인증 Client 소스를 직접 링크해 빌드하며 장치 보안 저장소만 메모리 구현으로 대체한다. 도구 build는 경고 0·오류 0이다.
- 첫 headless 표본 `4199fa401d7f4480acf509cf5632bd19`은 기사 수락 뒤 음식점이 이전 revision으로 픽업 준비를 요청해 서버가 `409 Conflict`로 거절했다. 실패 DB 볼륨은 보존했다. 실행기는 서버 검증을 약화하지 않고 음식점 상세를 다시 조회해 최신 `Revision`을 사용하는 방식으로 보완했다.
- 두 번째 표본 `a21d3ec8f51b412b8a164e28beadca04`에서 세 앱 Client 정상 폐루프가 `Completed`로 끝났다. 주문 `FOOD-20260914050829785`는 주문자 등록·음식점 수신함 재조회·수락·기사 추천 재조회·수락·음식점 최신 상태 재조회·픽업 준비·기사 픽업·전달·주문자 수령 확인을 통과했다. MySQL 독립 재조회는 주문 1건, 최종 `수령확인 / 배달완료`, 상태 이력 7건이며 결과는 `artifacts/local/validation/role-app-headless-e2e-20260914-r1/result.json`이다.
- G4는 기존 완료 Outbox·사본을 보존하고 최근 2시간의 진행 음식 주문을 `operational-world-scene.v2` `ActiveLifecycle`로만 생성한다. 상세 주소·주문번호·사용자·기사 식별자는 제외하고, 서버 설정 키의 HMAC-SHA256 가명·단계 revision·일반화 의미 위치·projection hash를 싣는다. 조회 TTL은 2분, 거절·취소 tombstone은 15분이고 수령 확인은 진행 투영에서 제외해 기존 완료 사본에 맡긴다. 키가 없으면 해당 자료원만 실패로 격리한다.
- 새 격리 볼륨 `bb9873ada44e4fe6b101982ea4acba77`의 주문 `FOOD-20260914052915772`에서 세 역할 앱 Client와 인증 v2 HTTP를 함께 실행했다. 동일 가명 업무가 `주문대기 → 조리중 → 기사배정 → 픽업완료 → 전달완료`로 갱신되고 수령 확인 뒤 제거됐다. MySQL 독립 재조회는 주문 1건, 최종 `수령확인 / 배달완료`, 상태 이력 7건이며 결과는 `artifacts/local/validation/role-app-headless-e2e-20260914-g4/result.json`이다.
- G5에서 `FoodDeliveryOsObservationAdapter`를 v2 `ActiveLifecycle`로 확장하고 실제 Unity `OperationalWorldSceneClient`·Decoder·Interpreter·OS Router·Session을 headless 역할 앱 실행기에 결속했다. 새 격리 표본 주문 `FOOD-20260914054522350`의 같은 가명 업무가 `주문대기 → 조리중 → 기사배정 → 픽업완료 → 전달완료`로 갱신되고 수령 확인 뒤 Unity 메모리에서 제거됐다. MySQL 독립 재조회는 주문 1건, 최종 `수령확인 / 배달완료`, 상태 이력 7건이며 결과는 `artifacts/local/validation/role-app-headless-e2e-20260914-g5/result.json`이다.
- Unity 진행·완료 Adapter 집중 시험은 15/15가 통과했고 headless 도구와 세 Windows 앱 빌드는 경고 0·오류 0으로 통과했다. 세 앱은 출력 폴더의 Git 비추적 `appsettings.Local.json`을 통해 격리 서버 주소를 덮어쓸 수 있게 맞췄다.
- G6 실제 Windows UI는 현재 실행 Host가 네이티브 앱 표면을 제공하지 않아 차단됐다. UI 상태는 앱 목록을 빈 배열로 반환했고 문서상 앱 선택 호출은 `getApp is not a function`이었으며, 직접 시작한 `OrdererApp`도 표면으로 등록되지 않았다. 따라서 실제 로그인·버튼·화면 판독은 수행하지 않았고 빌드나 headless 결과를 `DeviceUiProof`로 승격하지 않았다.
- 현재 증거 상한은 `UnityClientInterpreterLiveHttpProofPassed`다. Unity Editor·Play Mode·Game View, 네이티브 장치 UI, Mongo feed/cache, 전국 Profile은 아직 검증하거나 구현하지 않았다. 생성된 E 책임 지도를 생성기로 갱신한 뒤 범위 지정 Fast의 두 solution build·대상 Unity/서버 시험·지도 검사·diff 검사가 모두 통과했으며 기록은 `artifacts/local/validation/20260914-145354`다. 격리 컨테이너는 종료했다. 실제 Scene·Prefab·Graph Map·E 승격·commit·push는 수행하지 않았다.

## 사가정 기준 디오라마 증거 진화 최소 구현 r23 (2026-09-14)

- [기획·구현 기록 r23](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/diorama-evidence-evolution.implementation.r23.md)과 [역세권 디오라마 증거 진화 체계](../Architecture/역세권디오라마증거진화체계.md)를 추가했다. 사가정에서 발견한 규칙을 출처 기록 → 후보 → 교차 역 검토 → 사람 승인 → 공통 적용 순으로 발전시키되 기존 E1~E10을 새 단계나 자동 점수로 바꾸지 않는다.
- 기계 대장 `eng/execution-ledgers/station-diorama-evidence-rules.json`에 비밀값 없는 공식 링크와 저장소 근거를 가진 사가정 출처 기록 5건, 최초 규칙 후보 9건, 사가정·면목·용마산 역별 검토 Profile 3건을 결속했다. 모든 최초 규칙은 `Candidate`, `applicationAuthorized=false`, 승인 공통 규칙 0건이다.
- 개발 에이전트는 역세권 디오라마 작업 종료 시 새 후보와 기존 규칙의 지지·반례·무효화를 확인하고 후보가 없으면 `새 디오라마 규칙 후보 없음`을 보고한다. `ProvisionalSharedRule` 이상과 실제 공통 적용은 명시적인 사람 승인을 요구한다.
- 독립 관리 도구와 시험은 schema·고유 식별자·공식 HTTPS 링크·비밀 URL 금지·SHA-256·저장소 참조·승격 및 자동 적용 금지를 확인했다. 결과는 `Sources=5 / Rules=9 / Candidates=9 / Provisional=0 / Accepted=0 / Stations=3`과 집중 시험 PASS다. 범위 지정 Fast는 문서·지침 경로의 `git diff --check`를 통과했고 build/test는 guidance-only로 생략했다.
- 외부 링크의 현재 응답, 원본 재수집·로컬 DB 재조회, 서버·Simulation·Unity 코드, Scene·Prefab·Play Mode·Game View는 이번에 변경하거나 검증하지 않았다. commit·push도 수행하지 않았다.

## 사가정 합성 배달 기사 읽기 전용 관찰 r20 (2026-09-14)

- 사용자가 r19의 추천안인 “공공 통계는 밀도 근거로만 쓰고 개별 별칭·직업·이동·건수·모의 지급은 합성 Simulation이 소유”를 확정하고 구현을 요청했다. [승인 구현 r20](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-synthetic-courier-observation.implementation.r20.md), 새 Goal과 [E7 수직 작업 명세](../../eng/execution-ledgers/work-orders/sagajeong-synthetic-courier-observation.e7-work-order.json)에 첫 단일 기사 절편을 결속했다. 기획 SHA-256은 `1CCC9C212830464238AC2586C1BED72EEEB8E55EFEA2674FCB450C30509B07AC`이며 Graph Map 영향은 `NoImpact`, 증거 승격은 승인하지 않아 E0을 유지한다.
- `Ssalddel.Simulation.Contracts`와 `Ssalddel.Simulation.Application`에 `station:kr:kric:s1107:0722`·`scenario:synthetic-delivery.r1`·`actor:synthetic-courier:1` 전용 관찰 계약, 결정적 fingerprint, factory·validator·최신 사본 projector를 추가했다. 수령 확인과 `ReceivedTick`이 모두 있는 주문만 완료로 집계하고 전달만 끝난 주문은 제외한다. 실패·회복은 `NotTracked`, 모의 지급은 금액·통화 없이 `PolicyPending / SimulationSettlementRulePending`이다.
- Unity는 역·revision·Tick·단계·행동·경로 길이·합성 주문·fingerprint를 서버와 같은 경계로 검사한다. Camera·좌표가 결속되기 전에는 관찰 준비나 선택을 허용하지 않고, 오류 사본 뒤에는 표시를 숨겼다가 더 높은 정상 revision에서 복구한다.
- 별도 Unity 저장소에 Collider 없는 runtime 기사 표시, 화면 좌표 선택, 읽기 전용 카드와 `업무 Actor` 로컬 토글을 추가하고 `LifeSimulation` 모듈 Adapter로 분리했다. Host 표시와 사용자 토글을 별도로 보존하며, 공통 단일층 Adapter의 기존 해제 기본값은 유지하고 사가정 생활 Adapter만 명시 해제 뒤 숨김을 선택한다. 기존 건물·도로 Mesh·카메라·조명·Scene·Prefab은 수정하지 않았다.
- Hongdal 범위 Task는 Simulation solution build와 전체 시험 1,965/1,965를 통과했다(`artifacts/local/validation/20260914-115000`). 격리 Unity에서는 대상 소스 SHA-256 24/24 일치 상태로 합성 Actor 9건, 공통 모듈 11건, Mobility 10건, 사가정 운영 View 7건, 합계 37/37을 통과했다(`C:/Users/user/ssalddel/artifacts/local/validation/sagajeong-synthetic-courier-observation-r20/editmode-20260914-120938/`).
- 실제 canonical `SimulationWorldShell` 그래픽 Play Mode에서 전체 조망과 기사 선택 카드를 새로 캡처했다. Tick `14→15`, 숨김 중 Actor X `10→15`의 5m 변화, 상위 revision 수용, 재표시 최신 사본, 선택·표현 전후 권위 hash 불변, runtime 표시 1개와 Collider 0을 확인했다. Scene SHA-256은 실행 전후 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`로 같고 `sceneDirtyAfterPlay=false`다. 대표 화면은 Unity `Documentation/Changes/2026-09-14-sagajeong-synthetic-courier-observation/`, 원본 manifest·로그는 `C:/Users/user/ssalddel/artifacts/local/validation/sagajeong-synthetic-courier-observation-r20/gameview-20260914-121047/`에 있다.
- 화면 선택은 검증 조립부의 프로그램식 좌표 호출이며 정지 PNG는 실제 도로·차로·신호 주행 증거가 아니다. production Host/Scene 배선, 실제 Mouse 입력·uGUI 경합, live 서버, 실제 주문·배차·기사·위치·정산, 일반 주민·경계 포털·신규 공공자료·DB·E 승격은 미구현이다. canonical Scene의 기존 누락 Prefab·replay hash·localhost·JobTempAlloc 경고도 남는다. Scene/Prefab 저장, commit, push, 배포는 수행하지 않았다.

## 사가정 공공데이터·생활 관찰 심화 제안 r19 (2026-09-14)

- [제안서 r19](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-public-data-life-observation.proposal.r19.md)에 사가정 기존 디오라마를 보존하면서 공공자료 표시 그룹, 1km 경계 포털, 합성 주민·기사·차량, 읽기 전용 Actor 카드로 생활상을 심화하는 방향을 기록했다.
- 기존 생활 여덟 영역 자료 분류, 여덟 Unity 실행 모듈, 사용자가 켜고 끄는 표시 그룹을 서로 다른 축으로 분리했다. 표시를 꺼도 Simulation Tick·업무 생명주기·가상 정산은 계속되며 다시 켜면 최신 상태를 보여 주는 원칙이다.
- 현행 기준선은 기준 건물 602·도로 2,397, 화면 건물 4,062, 공식 주소 559, 차선 후보 1,039, 횡단보도 후보 84, 면목 상가 관측 5,411, 사가정 창 음식 관측 599, 생활인구 시간 관측 4,464다. 화면 건물과 의미 건물 결속, 신호 CRS, 보행망·출입구, 통계의 합성 밀도 변환은 선행 관문으로 남겼다.
- 전수 조사는 현실의 모든 개인·업체를 확보했다는 뜻이 아니라 선언한 공식 카탈로그 안의 관련 후보를 출처·판본·hash·CRS·이용조건·coverage·상태와 제외 사유까지 판정하는 것으로 정의했다.
- 이 판본은 기획만 갱신했다. 신규 자료 수집·DB 쓰기·API/Simulation/Unity 코드·Scene/Prefab·Play Mode·Game View·E7 작업 명세·commit·push는 수행하거나 승인하지 않았다.

## 사가정 Mobility Adapter 보존형 리팩터링 r18 (2026-09-14)

- [구현 명세 r18](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/mobility-module-adapter-refactor.implementation.r18.md)에 따라 r17 Host 뒤에 단일 모듈 상태 factory와 등록·해제·원래 `Behaviour.enabled` 복원을 맡는 공통 `StationDioramaSingleLayerAdapter`를 추가했다. 서버·Simulation·WI·WorldRevision 권위는 바꾸지 않았다.
- `사가정MobilityModuleAdapter`는 기존 저밀도 교통 또는 음식배달 Journey 가운데 조립부가 고른 하나만 `Mobility` contribution으로 보고한다. 둘을 동시에 지정하면 동일 배달 기사·경로 중복 표시를 막기 위해 거부한다. 교통 source 판본은 매 Tick이 아닌 기존 이동 그래프·차로·신호·교통 프로필·배달 표시 경로 판본 조합으로 고정하고 상태 사본 적용 뒤 `RefreshModuleState()`로 Host 준비도를 갱신한다.
- `ActiveStationDioramaHost`는 새 Adapter를 숨긴 상태에서 전체 contribution을 사전 검증하고, 중복 layer 등록 실패 때 목록·활성 plan·hash를 보존한다. 해제한 Adapter는 즉시 숨기고 남은 contribution으로 다시 조립한다. source 판본 결손은 예외 전파 대신 `Blocked` 진단으로 격리한다.
- 비활성 저밀도 Playback에 상태 사본을 적용해도 새 정적 차로·신호와 동적 배우 Root가 먼저 노출되지 않도록 표시 수명만 보강했다. 기존 경로·차로·신호·보간·pool 계산과 `사가정운영디오라마View`의 Mesh·카메라·선택 카드는 수정하지 않았다.
- 메인 쓰기 파일과 SHA-256이 같은 격리 Unity 사본에서 Adapter 10/10, 공통 Host 11/11, 저밀도 교통 16/16, 음식배달 Journey 27/27, 기존 공간·H·모형·운영 View 75/75, 합계 139/139를 통과했다. 결과는 `C:/Users/user/ssalddel-building-h-validation/artifacts/local/validation/station-diorama-module-r18/`에 있다.
- 새 Adapter는 production 조립부와 canonical `SimulationWorldShell` 저장 Scene에 아직 결속하지 않았다. Scene·Prefab·Mesh·카메라·화면 출력은 변경하지 않았고 Play Mode·Game View도 재실행하지 않았다. 따라서 이번 결과는 공통 이음부와 EditMode 수명 검증이며 실제 World 작동 증거가 아니다. commit·push는 수행하지 않았다.

## 사가정 참조형 역세권 디오라마 공통 모듈 표준화 r17 (2026-09-14)

- [구현 명세 r17](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/station-module-standardization.implementation.r17.md)에 따라 Unity Runtime에 엔진 비의존 공통 Profile·Planner를 추가했다. 자료 근거, 중립 공간 표현, H 의미, 이동, 상호작용, 생활, 업체 표시, 환경의 여덟 모듈을 순서·필수 여부·의존성으로 정의하고 `Ready / PrivateReview / Blocked / NotProvided / BlockedByDependency`와 결정적 조립 hash를 만든다.
- Presentation에는 `ActiveStationDioramaHost`와 호환 Adapter 계약을 추가했다. 필수 자료·표현 모듈이 준비된 역 하나만 표시하며 등록되지 않은 역, 필수 모듈 결손과 교차 역 대체를 거부한다. 서버·Simulation 권위, 업무 상태, WI·WorldRevision은 변경하지 않는다.
- 기존 `사가정운영디오라마View`를 첫 Adapter로 연결했다. 기존 MeshBuilder·4×4 chunk·카메라·색·건물 602개 선택·역할 카드는 그대로 두고 자료 근거·중립 공간 표현·H 의미·읽기 전용 상호작용 네 모듈만 보고한다. 이동·생활·업체·환경은 이번 Host 결속에서 `WaitingForModuleAdapter`로 남긴다.
- 활성 모듈 Catalog에는 사가정만 등록했다. 기존 면목·용마산 비공개 자료와 공통 공간 View는 삭제하지 않았지만 새 활성 Profile·View·Scene은 만들지 않았다.
- 메인 Unity 쓰기 파일과 SHA-256이 같은 격리 사본에서 공통 모듈 9/9, 사가정 공간 Overlay 54/54, H 선택 10/10, 모형 4/4, 운영 View 7/7, 합계 84/84를 통과했다. 위치 독립 factory가 후속 역 자료·View·Catalog 등록 없이 같은 8모듈 구조를 만드는 범위도 포함한다. 넓은 `사가정*` 실행의 기존 Synty 대장 5건은 격리 사본에 외부 공급사 `Assets/Synty` 팩이 없어 카탈로그의 Prefab GUID 4개를 해소하지 못한 `LegalDongScenicCatalogInvalid`이며, 메인 자산 트리에서는 네 GUID를 모두 확인했다. 따라서 이번 표준화 쓰기 경로와 분리했다.
- Scene·Prefab·자료 asset·서버 API schema·Hosted·Save/Replay·Play Mode·Game View는 변경하거나 실행하지 않았다. 메인 Unity Editor는 열려 있으나 Pipeline이 응답하지 않아 강제 종료하지 않았고 격리 사본 Test Runner로 검증했다. commit·push는 수행하지 않았다.

## 사가정역 1km 건물 도로명주소 결속 r3 (2026-09-14)

- 사용자가 확정한 공유 주소 A안을 [승인 기획 r2](Planning/시스템/PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION/README.md)와 [구현 결과 r3](Planning/시스템/PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION/implementation.r3.md)에 결속했다. 각 건물 ID는 유지하고 같은 공식 주소를 `OfficialSharedComplexAddress`로 참조한다.
- 행정안전부 주소기반산업지원서비스에서 현재 가능한 2026-08-31 건물DB 월 전체분 149,263,413바이트를 비공개 동결했다. 원본 SHA-256은 `4aa70c569aaf14f550313b5836346a1ba438491fb4cec61877e615c9e602afa7`이고, 서울 MS949 31열 파일·활용 가이드·공공데이터포털 `이용허락범위 제한 없음` metadata를 같이 검사했다.
- 602/602개에 중복 없이 주소 상태를 부여했다. 공식 개별 480개, 공식 공유 79개(41그룹), 공식 월 원본과 불일치하는 OSM 후보 28개, 미해결 15개다. 기존 건물 주소 외에 윤곽 안 주소 지점 6개와 유일 공식 건물명 1개를 회수했으며 가까운 주소 복사는 0건이다.
- 투영 SHA-256 `86d318afdc34bd3af0e6470ccd49f29692a3f71b888fa0252c0b4aaac84c80bf`를 재생했고, 로컬 `hongdal-mysql-1 / hongdal_dev`에 원본 사본 ID 72와 정규화 602행(ID 92965~93566)을 저장했다. 첫 적용은 신규 602, 즉시 재적용은 신규 0·기존 602·`databaseWriteAttempted=false`, 새 `DbContext` 독립 재조회는 602건이다. 자체 시험 14/14, 도구 build 경고0·오류0을 확인했다. 범위 지정 Fast·Task는 `git diff --check`를 통과했지만 공용 검사기가 `eng` C#를 문서로 분류해 build·test를 생략했으므로 이 별도 검증과 구분한다.
- 모든 행은 `distributionApproved=false`, `deliveryEligible=false`, `priceObservationEligible=false`, `unityApplyAllowed=false`다. 28개 후보·15개 미해결은 주소 확정 필수 기능에서 계속 차단하며, 일변동·출입구·가격·API·Unity·Scene·Game View는 별도 후속이다. commit·push는 수행하지 않았다.

## 사가정역 주거 가격 관찰층 제안 r2 (2026-09-14)

- [제안서 r2](Planning/시스템/PLAN-DATA-SAGAJEONG-HOUSING-MARKET-OBSERVATION/README.md)은 매매·전세·월세 실거래와 선택적 공시가격을 H 역할이 아닌 별도 주소·건물 관찰층으로 분리한다. H1은 선택 건물, H2는 블록, H3는 생활 회랑의 조회 범위로만 사용한다.
- 국토교통부 실거래가 공개시스템·공공데이터포털, 도로명주소 건물 식별 안내와 표준주택가격 자료를 공식 근거로 검토했다. 반복 수집은 공개 화면 자동화가 아니라 공공데이터포털 API를 사용하고, 실거래 정정·해제와 주소 판본을 revision으로 보존하도록 제안했다.
- 기존 사가정 602개 건물 중 주소 문자열은 580개지만 기존 수집 주소 후보 결속은 244개, 단일 후보는 204개뿐이다. 따라서 전 건물 가격을 채우지 않고 `ConfirmedBuilding / AddressLevelOnly / AmbiguousBuilding / AreaAggregateOnly / Unresolved`를 구분한다.
- 기본 디오라마에는 가격을 표시하지 않고 사용자가 `주거 관찰` 탭을 열 때 배포 승인된 집계만 보여 준다. 소유자·임차인·상세 호수·연락처는 제외하며 가격은 OS·NPC·광고·게임 경제에 영향을 주지 않는다.
- 사가정 602개 건물 주소 상태 대장 완결을 필수 선행 관문으로 추가했다. 실제 자료 수집·DB·API·Unity·Scene·시험·commit·push는 수행하지 않았으며, 첫 카드의 실거래·공시가격 범위 문답은 주소 공유 정책 뒤로 보류했다.

## OS 생명주기 Core·환경별 Adapter 분리 제안 r4 (2026-09-14)

- [제안서 r5](Planning/시스템/PLAN-SYSTEM-OS-LIFECYCLE-ENVIRONMENT-ADAPTERS/README.md)은 음식배달·화물·창고·마트의 안정 단계 의미를 Core에 두고 운영 서버, Simulation Local/Hosted, Web·모바일 경험, Unity 상태 사본·H 공간 표현을 환경별 Adapter로 분리한다.
- Web·모바일은 별도 상태 권위가 아니라 같은 운영 서버 Command와 canonical 재조회를 사용하는 클라이언트로 정의한다. Unity는 운영 상태 관찰과 게임 Simulation 표현을 구분하며 NPC 도착·Animation만으로 업무 완료를 확정하지 않는다.
- H 결속은 Core에 넣지 않고 `OperatingSystemId + LifecycleStageId + SemanticPlaceStableId`를 역할·H1·H2·H3·입출구·귀환 위치에 연결하는 별도 Integration 계약 후보로 둔다. 현재 네 OS는 단계·의미 위치 표현까지 준비됐지만 H 교차 결속은 부분 상태다.
- 사용자 선택에 따라 Core가 단계 ID·순서·설명뿐 아니라 허용 전이, 실패·회복·귀환과 환경 비의존 순수 guard까지 소유하도록 확정했다. 인증·실제 DB·외부 API·기기·시계·Unity 공간 도착은 환경 검사로 남긴다.
- 사용자 선택에 따라 Core 판정을 `AllowedByCore / BlockedByCore / RequiresEnvironmentValidation` 세 상태로 나누고, 정의 판본·현재 단계·요청 전이·다음 단계 후보·안정 이유·환경 요구사항 코드를 반환하도록 확정했다. 이는 상태 변경 완료 판정이 아니며 권위 UseCase는 환경 검사와 revision을 다시 확인한다.
- 호출자별 작은 `AvailableActions`를 같은 역할 상태 사본에 포함하는 방향은 확정됐다. 음식배달 첫 절편에서 서버가 주문자·음식점·기사별 행동을 투영하고 OrdererApp·RestaurantDeskApp·FDriverApp이 기존 버튼을 이 목록으로 통제하며, 음식점 revision 지원 명령은 해당 행동의 `ExpectedRevision`을 보낸다. 전체 OS Core·환경 Adapter 리팩터링과 실제 장치 UI·Unity 실행은 아직 수행하지 않았다.

## 사가정 건물 복수 역할 카드·읽기 전용 후속 보기 r16 (2026-09-14)

- [구현 명세 r16](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/building-role-cards.implementation.r16.md)에 따라 건물과 역할을 분리하고 `건물 → 역할 Profile 여러 개 → 역할별 H1 여러 개`의 읽기 전용 계약을 구현했다. 역할 코드는 음식점·창고·주거 전달·미결속 일반이며, 역할 Profile은 배열이라 한 건물의 복수 역할을 지원한다.
- 합성 의미 위치가 r3 윤곽 하나에만 포함되는 세 건물만 명시 결속했다. `osm:way:1256772531`은 음식점, `osm:way:1256772606`은 주거 전달, `osm:way:470492219`는 창고 역할이다. 나머지 599개는 실제 입점·거주를 추정하지 않고 `GeneralUnbound / WaitingForGraphMapBinding`으로 남긴다.
- 건물 선택 카드에 역할 탭, 역할별 H1·생명주기 요약과 후속 보기 상태를 추가했다. 준비된 음식배달 경로가 있을 때만 기존 `음식배달JourneyPlaybackLayer`의 경로를 선택하며, 없으면 `WaitingForReadOnlyRouteSnapshot`을 표시한다. 창고는 합성 생명주기 미리보기만 제공한다. 모든 후속 결과는 읽기 전용이며 주문·배차·결제·재고·WorldRevision을 바꾸지 않는다.
- 실제 작업 파일과 SHA-256이 모두 같은 격리 Unity 사본에서 역할 시험과 기존 사가정 선택·모형·운영·공간 회귀 75/75가 통과했다. r16 Play Mode·Game View와 실제 MouseUp은 새로 확인하지 않았고, 기존 r15 캡처를 r16 증거로 재사용하지 않는다. Scene·Prefab·서버·Graph Map·H 생성 대장은 수정하지 않았으며 commit·push도 수행하지 않았다.

## 사가정 디오라마 보존형 건물 H 계층 선택 r6 (2026-09-14)

- 사용자 확정에 따라 사가정 A+를 H3 생활 회랑 두 개와 부분 AreaSet 구성 후보로 관리하되, 기존 사가정의 현실 공간 표현을 우선 보존하는 [시스템 기획 r15](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/README.md)와 [공간 기획·구현 명세 r6](Planning/공간/PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC/building-hierarchy-selection.implementation.r6.md)를 열었다.
- 첫 절편은 r3 원본 건물 602개를 CPU 외곽으로 선택하고 원본 건물 정보와 읽기 전용 H 후보 카드를 보여 주도록 구현했다. 높이·공간 보완이 대체한 r3 건물은 MeshBuilder가 실제로 그린 높이·외곽으로 판정한다. 개별 GameObject·Collider·Renderer를 추가하지 않았고, 기존 역 방어 IMGUI·하단 통합 시점 uGUI가 입력을 점유하면 건물 선택을 막는다.
- H 후보는 합성 의미 위치가 윤곽 안에 확인된 `osm:way:1256772531` 음식점·픽업과 `osm:way:1256772606` 주거·전달 두 건물만 명시적으로 결속한다. 나머지는 주소·종류·근접 거리로 추론하지 않고 `WaitingForGraphMapBinding`으로 남긴다. 부분 AreaSet은 `ActualE5=false / TraversalReady=false / GameplayReady=false / ServerProjectionReady=false`다.
- 격리 Unity EditMode에서 선택 8건과 기존 사가정 모형·운영·공간 회귀 65건, 총 73/73이 통과했다. canonical `SimulationWorldShell` 그래픽 Play Mode에서 1399×628 Game View 3장을 새로 캡처했고, 선택 전후 Renderer 39·Material 5·MeshFilter 39·Collider 0과 Scene SHA-256 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`·dirty=false를 유지했다.
- 화면 좌표→ray 선택은 통과했지만 사용자 MouseUp 입력 주입은 수행하지 않았다. 캡처는 `LocalPrivateReview / publicReleaseEvidence=false`이며, 기존 Scene의 누락 Prefab GUID 56개·Unknown script 10건·replay hash·localhost 연결·JobTempAlloc 경고는 별도 문제로 재현됐다. 새 WI·Goal·ActionRecord·WorldRevision·실제 H/Graph Map 등록·서버 HTTP·Scene 저장·Evidence 승격은 수행하지 않았다. commit·push도 수행하지 않았다.

## 사가정 다중 OS 샘플 생명주기 재생 구현·검증 r4 (2026-09-13)

- [승인 방향 r4](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/multi-os-lifecycle-playback.r4.md)와 [E7 작업 명세](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/implementation.r4.md)에 따라 음식배달·국내화물·창고·마트 네 공간형 OS의 정상·회복 8개 사례와 77단계를 구현했다. 나머지 6개 OS는 생명주기가 정의될 때까지 진단 목록에만 두고 단계나 객체를 만들지 않는다.
- 격리 검증 MySQL에 단계 원장과 Fixture 묶음·결속을 추가하고 같은 `WorkStableId`의 증가 revision을 Outbox로 MongoDB에 투영한다. Fixture는 기존 `음식점공개프로필`·`음식점메뉴`에 비공개 샘플 12곳·메뉴 30개를 저장하고 42개 결속을 `NoBusinessAffiliation / LocationPresentationAnchorOnly / ActualOrderAllowed=false / DistributionApproved=false`로 고정한다. 전체 결속 사전 검사, run별 묶음 계보, 단계·투영 내용 hash, 최신 유효 run 단일 조회와 최종 revision 게시 완료 관문을 보강했다.
- 실제 `observable-operations-run:r4-hardened-20260913225629`를 600초 실행했다. 일시정지·재개 뒤 553초에 MongoDB를 중단했으며 600초에도 마지막 Outbox 실패 1건 때문에 `Running`을 유지했다. MongoDB 복구와 명시적 Retry 뒤에만 `Completed / 8사례 / 77단계 / Pending 0 / Failed 0`이 됐다. 독립 재조회에서 MySQL 단계·Outbox 77건, MongoDB 최종 사본 8건, Redis 완료 상태와 HTTP v2 최신 run 8건이 같은 계보로 확인됐다. 실제 상호·주소·연락처·정확 좌표는 Unity 계약에 포함되지 않았다.
- Unity는 canonical `SimulationWorldShell`을 저장하지 않고 `FrozenPresentationTimeline` 기반 runtime-only 계층으로 네 OS와 음식점 아이콘 12개를 합성했다. EditMode 10/10과 실제 Play Mode Game View 3장을 확인했으며, 네 OS 표식이 1.2초 동안 일반화 단계 기준점 사이를 이동한 실제 프레임 표본을 manifest에 남겼다. 이는 `routeAuthority=false / liveHttpEndToEnd=false`인 표현 검증으로 실제 도로·차선·신호·길찾기 또는 운영 업무 실행 증거가 아니다.
- 서버 집중 시험은 최종 31/31, 변경 경로 한정 Fast는 build·targeted test·diff 검사를 모두 통과했다(`artifacts/local/validation/20260913-231412`). 최신 Task는 전체 solution build를 통과했고 서버 전체 5,235건 중 5,228건이 통과했다(`artifacts/local/validation/20260913-231857`). 남은 7건은 앞선 기준선과 같은 역할별 API metadata 4건, 공식 재료 화면 1건, 아키텍처 용어 1건, WebApp capability 1건이며 이번 집중 범위 시험 실패는 없다. Scene 저장·실제 Unity HTTP 연결·실제 네 OS Controller/UseCase/Command 실행·운영 DB·외부 효과·Evidence 자동 승격은 수행하지 않았다. 기존 Unity Scene의 누락 Prefab·Unknown script 등 기준선 경고도 별도 문제로 남으며, commit·push는 수행하지 않았다.

## 사가정 음식점 아이콘·운영 생명주기 검증 제안 r3 (2026-09-13)

- [조사 제안 r3](Planning/공간/PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/restaurant-icons-operational-lifecycle-validation-proposal.r3.md)는 실제 음식점 관측 아이콘, 검증된 상인 Claim/운영 메뉴, 광고·후원, 진행 중 음식배달 상태 사본을 서로 다른 권위·표현 계층으로 관리한다. 기존 공공 사업장·건물 Assignment·Claim·Campaign과 `음식점공개프로필`·`음식점메뉴`를 재사용하고, 합법적인 외부 메뉴 원천이 생긴 경우에만 별도 메뉴 관측 원장을 추가하는 방향을 제안했다.
- NAVER 지역검색은 메뉴 필드가 없고 현행 약관상 지역정보 별도 DB화·광고 영업 이용·API 결과와 광고 동시 노출이 허용되지 않아 원천 후보에서 제외했다. 배달의민족은 조사 범위에서 제3자용 공개 메뉴 조회 API를 확인하지 못해 화면 수집을 채택하지 않았다. 공공 인허가·중랑구 모범음식점의 `주된음식`, 상인 직접 제출, 합성 fixture 순으로 권위를 제안했다.
- 서버 음식배달에는 정상·거절·취소·중단·회복 전이가 있으나 지역 장면 API와 Unity Adapter는 현재 수령확인 완료 결과만 표현한다. 첫 후속 절편은 실제 후보 12곳의 개발자 전용 `LocalPrivateReview` 정적 카테고리 아이콘과 세 가상 음식점 중 한 주문의 비식별 진행 생명주기, 건물 anchor, 전체·음식점 근접·이동 근접 Game View 검증이다. 실제 상호는 선택 시에만 `검토용·비배포·주문 불가`와 함께 보이는 안을 추천했다. 이번 작업은 조사·문서만 수행했으며 외부 자료 수집·DB migration/쓰기·API/Unity 구현·Play Mode/Game View·Scene 저장·commit·push는 수행하지 않았다.

## 사가정 1km 음식점 비공개 원장·세 가상 음식점 폐루프 r2 (2026-09-13)

- [승인된 사업장·음식점 주문 결속 r2](Planning/공간/PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/business-order-binding-proposal.r2.md)에 따라 기존 동결 자료를 사가정역 기준 `(550,8)`·반경 500m 정사각형으로 결정 재투영했다. 음식 관측 599행·원문 상호 587종, 단일 건물 후보 180행, 중랑구 음식점 인허가 후보와 일치한 관측 334행을 manifest SHA-256 `d89369ecf0fa85093522aef2a7a2dc9f413154c59d175f58391e3de3b8bb64cd`로 동결했다.
- 로컬 MySQL `hongdal-mysql-1 / hongdal_dev`의 기존 `public_data_normalized_records`에 파생 자료 599행을 저장하고 별도 `DbContext`에서 599행을 독립 재조회했다. 같은 입력을 다시 적용했을 때 `inserted=0 / updated=0 / existing=599 / databaseWriteAttempted=false`였다. 부모 `SourceId`·`DatasetId`·`RawSnapshotId`를 재사용하며 모든 행은 `PendingHumanReview`, `distributionApproved=false`, `orderScenarioEligible=false`다. 현재 DB에 인허가 전용 테이블이 없어 후보 334건은 동결 입력·hash까지만 검증했고 migration이나 추정 테이블 생성은 하지 않았다.
- 첫 주문 폐루프에는 실제 상호 대신 `가상 사가정 큰길식당`, `가상 면목 생활길분식`, `가상 골목안 도시락` 세 `SyntheticFixture` profile을 추가했다. 기존 주문 상태 기계와 NPC 조리·배차·기사 이동을 재사용해 5개 자동 주문이 세 음식점을 순환하고 `조리중 → 픽업대기 → 기사배정 → 픽업완료 → 전달완료 → 수령확인 → 기사 복귀`까지 닫히며 Save/Replay hash가 일치함을 확인했다. 세 `DisplayRouteKind`는 후속 공간 결속용 표시 profile이며 이번 단계에서 대로·생활길·골목의 서로 다른 실제 이동 경로를 뜻하지 않는다. 표시 Presenter는 음식점 ID·합성 이름·상태만 결속하고 Command를 실행하지 않으며 Simulation 원천·정확 판본·profile/route 출처가 모두 맞는 사본만 받는다.
- 집중 회귀는 Simulation 49/49, Unity 패키지 26/26이 통과했다. 이동 중간 저장 뒤 복원·계속 진행한 결과도 무중단 실행과 같은 최종 Replay hash로 닫혔다. 범위 Fast는 두 solution build·생성 지도와 Simulation 227/227·Unity 28/28을 통과했고(`artifacts/local/validation/20260913-200111`), Task는 같은 build·지도와 Simulation 전체 1,957/1,957·Unity 전체 785/785를 통과했다(`artifacts/local/validation/20260913-200256`). 세 재사용 E7 v2 명세 검사도 통과했지만 현재 Evidence 단계 `E0`은 자동 승격하지 않았다. 이번 r2에서는 실제 상호 공개·주문 참여·Claim/광고 결속, Entrance/CurbStop·검토 통행, Hosted HTTP, canonical Scene 저장, Play Mode·Game View를 수행하지 않았다. 기존 이동 r1의 Game View 증거를 이번 세 음식점 화면 증거로 재해석하지 않으며 commit·push도 수행하지 않았다.

## 2026-09-13 맥락별 로컬 커밋 정리

- Hongdal 기능·자료 변경은 역 방어 준비, 사가정 공간 표현 도구, 도로·건물 검토 원장, 역세권 시각 근거, 면목·용마산 공간 사본, 공공자료 명령, 이동망 후보, 역세권·이동망 조회, 합성 음식 배달 여정, 자동 생성 코드 지도의 10개 로컬 커밋으로 분리하고 기획·검증 문서는 2개 커밋으로 따로 묶었다. Unity 변경은 관찰 레이어, 사가정 오버레이, 역세권 공통 표현, 사가정 절차 모형, 음식 배달 재생, Game View 증거, 공간 모판 동기화의 7개 로컬 커밋으로 분리했다.
- Hongdal 집중 검증은 방어 37건, 이동망·역세권·지역 Package 47건, 합성 배달·동네 이동 78건과 공공자료·공간 사본·생성 지도 검사를 통과했다. 12개 커밋의 147개 경로를 다시 지정한 최종 Fast도 두 solution build·두 targeted test 묶음·코드 지도 검사를 모두 통과했다(`artifacts/local/validation/20260913-144231`). Unity 생성 EditMode project는 오류 0으로 빌드됐고, 음식 배달 27/27·사가정 운영 디오라마 7/7의 기존 최종 결과와 합성 배달 Game View 3장을 보존했다. 역세권 공통 표현의 정식 Test Runner 재실행은 `pipeline_test_status.json` 공유 충돌로 새 결과를 내지 못해 기존 문서의 `UnityOfficialRunnerPending`을 유지한다.
- Unity의 기존 연구 Scene 4개, H2·NatureH3·평창 생성 Prefab/Scene 16개와 `ssalddel.slnx` 순서 변경은 현재 기능과 무관한 전체 재직렬화·IDE 잡음으로 판정해 스테이징하지 않았다. 이 21개 작업트리 변경은 삭제하거나 되돌리지 않고 보존했다. 두 저장소 모두 원격 push는 수행하지 않았다.

## 용마산역 1km 비공개 관찰 디오라마 r1 (2026-09-13)

- [용마산역 구현 기록](../Reports/용마산역-1km-디오라마-구현-2026-09-13.md)과 [역세권 디오라마 모듈 표준 r13](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/README.md)에 따라 공식 역번호 `0723`, 기준점 37.573752·127.086802 중심의 정북 1km 창을 독립 `LocalPrivateReview` 사본으로 만들었다. OSM bbox 원본 1,389,565 bytes와 SHA-256 `5E690398DD779E91EDF467A09424FBBBB4448E5B3DC7D6FE3A15865E543227F1`을 동결했고, 사본 content hash는 `43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D`다.
- 실제 사본은 건물 2,859개·도로 116개/선분 211개·표면 41개·행정동 4개·500m tile 4개를 가진다. 건물 높이는 원천 관측 1,185개와 상징적 4m fallback 1,674개를 구분한다. coverage 100셀은 `ConfirmedBuilt 72 / ConfirmedOpen 6 / IncompleteSurfaceEvidence 12 / MissingCoverage 10`이며 관계 member·권리·경계·도로 폭·표면·통행 결손 여섯 종을 합성하지 않고 보존했다.
- 로컬 MySQL `hongdal_dev` 비공개 원장에 OSM 원본·영수증·사본·감사를 ID 58~61로 저장했다. 같은 입력 재적용 신규 0건과 별도 프로세스 재조회를 확인했고, 기존 Mongo 행정동 current projection은 변경하지 않았다. 서버 manifest는 후보 fingerprint만 결속해 `WaitingForSpatialCoverage`, 빈 Region/Area 투영, `ServerLoadable=false`와 공개·통행·gameplay false를 유지한다. 서버 집중 13/13과 Controller 관련 17/17은 통과했다.
- 별도 Unity 저장소에서 정확한 용마산 OSM 영수증과 교차 역 fallback 거부를 결속했다. 생성 EditMode project build는 오류 0·기존 경고 475이고 compiled fixture 4/4가 통과했다. 정식 Test Runner는 시험 원본 23 case를 확인했지만 이번 새 완료 결과는 게시되지 않아 통과 미확정이다.
- Unity `6000.5.6f1`의 canonical `SimulationWorldShell` Play Mode에서 저장하지 않는 임시 View로 `LocalPrivateReview / PartialCoverage / Ready`를 실제 표시했다. source 5·건물 2,859·도로 116·표면 41·행정동 4·coverage 100·결손 6/10셀, 16 chunk·renderer, vertex 88,589·triangle 47,213·collider 0을 확인하고 [Game View PNG](../../artifacts/local/validation/yongmasan-station-gameview-r1/yongmasan-station-playmode-gameview-final.png)를 남겼다. 용마산 관련 Console 오류는 0이지만 기존 서버 미연결·replay·missing script Error/Warning 20건은 남는다. Scene은 저장하지 않았고 dirty false·실행 전후 SHA-256 동일이다. 공개·배포·서버 payload·통행·gameplay는 수행하지 않았다. 관련 구현·증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다.

## 사가정 가상 배달 이동 기반 r1 (2026-09-13)

- [사가정 가상 배달 이동 r1](Planning/공간/PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/README.md)과 [E1~E7 명세](Planning/공간/PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/implementation.md)에 따라 기존 디오라마를 보존하고 `Mobility`를 독립 지역 계층으로 추가했다. 첫 상태는 실제 주문이 아닌 `SyntheticFixture`이며 Unity는 읽기 전용 표현만 맡는다. 실제 사가정 경로·접근점 승인 전 결과 상한은 `Logic E3 / Presentation E4 구현`이다.
- 동결 OSM을 기존 `WorldOffset=(550,8)` 좌표계의 500m `2×2` 타일로 결정 생성했다. 실제 결과는 노드 2,807개·간선 2,574개·projection hash `59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7`이다. 방향 미확정 간선 2,430개와 외곽 portal 115개를 보존했고 음식점 후보 2곳의 1.437m/1.404m 접근선은 검토 자료로만 남겨 생성 connector를 0개로 유지했다. 모든 정적 권위 값은 false다.
- `region-mobility-graph-manifest.v1`/tile 계약과 개발 환경·서버관리자 전용 GET 2개, ETag, 로컬 원본·내용·파일 hash·좌표·타일 stitch 검증을 추가했다. `RegionMobilityObservation`은 행정동 디오라마 관찰과 함께 켜져야 하며 기본 비활성이다. 지역 Experience Package r2는 이를 `OnDemand / ImmutableByHash`로 참조하지만 지역 열기에 필수로 만들지 않는다.
- `food-delivery-journey-snapshot.v1`은 기존 합성 동네에서 Motorcycle/Pedestrian 7구간을 만들고 graph revision/hash·경로 지문·진행 위치·차단·revision을 검증한다. 별도 Unity 저장소에는 엄격 JSON Decoder, polyline 진행 해석, 전체 보기 표식, 확대 보기 기사·스쿠터, 선택 경로, 배우 pooling·정리 계층을 준비했다. 같은 revision의 다른 상태와 배우 신원 교체를 거부하고 정투영·원근 카메라의 거리별 표현을 모두 다루며, 도착은 주문·픽업·전달 상태를 바꾸지 않는다.
- 정적 생성·감사 자체 시험 5/5, Simulation 이동망·여정 집중 시험 41/41, 서버 이동망·기능 플래그·지역 Package 집중 시험 27/27이 통과했다. 최종 범위 Fast는 두 solution build와 Simulation 15/15·서버 144/144가 통과했다(`artifacts/local/validation/20260913-125209`). Task는 두 build와 Simulation 전체 1,928/1,928이 통과했지만 서버 전체 5,224개 중 기존 작업트리 관련 7개 실패가 남았다(`artifacts/local/validation/20260913-123841`); 이번 집중 경로 실패는 없다. E 책임 지도는 850개 표기·미표기 0으로 갱신했다.
- Unity Runtime/Presentation 단독 build는 각각 오류 0이다. 새 이동 계층 EditMode 27/27·기존 사가정 운영 디오라마 7/7을 확인했고, 최종 검증용 Editor 코드도 재컴파일 오류 0이다. Unity `6000.5.6f1`의 canonical `SimulationWorldShell` Play Mode에서는 저장하지 않는 임시 검증 Root로 `SyntheticFixture` 7구간·총 116m 자동 이동을 실제 표시했다. 전체 보기·Motorcycle 확대·Pedestrian 인계 확대 Game View PNG 3장을 남겼으며, 시간 경과 관찰에서 revision 14→377, 적용 사본 10→373, 누적 위치 변화 0.269m→11.920m와 Motorcycle→Pedestrian 전환을 확인했다. 이번 여정 관련 Console Error/Exception은 0건이었다.
- 이 화면 증거는 합성 상태 사본의 읽기 전용 이동 표현 검증이다. 세 PNG는 연속 프레임이 아니라 판독 지점 3개의 고정 사본이며, 저장 Scene 영속 결속, 실제 입력 폐루프, Hosted HTTP/live server, 실제 OSM 간선 길찾기·통행 승인, 운영 주문 상태 전이와 Save/Replay는 검증하지 않았다. canonical Scene의 기존 bootstrap/replay/server 연결 Error/Exception 8건은 별도 잔존한다. 실행 전후 Scene SHA-256 동일·종료 뒤 Scene clean·임시 Root 0개를 확인했다. DB 쓰기·Steam 공개는 수행하지 않았다. 관련 구현·화면 증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다.

## 사가정 저밀도 차로·신호·A+ 생활 회랑 r5 (2026-09-14)

- [사가정 차로·신호·A+ 생활 회랑 r5](Planning/공간/PLAN-SPATIAL-SAGAJEONG-LANE-SIGNAL-TRAFFIC/README.md)에 따라 서울시 공식 전체 파일 5종 47,430,933 bytes와 전체 556,346행을 동결했다. 기존 명목상 1km 창의 거친 후보는 차선 1,039·방향표시 318·교차로 25·제어기 16·신호등 숫자 envelope 125, 합계 1,523행이다. 원천 CRS가 해소된 1,398행만 사가정 지역 ID에 두고 신호 125행은 별도 CRS 미해결 검토 버킷에 보존했다.
- 원본 5건과 후보 1,523행을 로컬 MySQL 비공개 검토 원장에 저장하고 별도 프로세스 재조회와 재적용 신규 0건을 확인했다. 횡단보도 `OA-23081` r2 원본 1건·정규화 84건도 저장·독립 재조회했으며 사가정역 `CSS_NUM=3205`에는 7행, 보행등 `유` 4행이 후보로 결속된다. 모두 `PendingHumanReview`이고 공개·통행·gameplay 권위는 false다.
- `station-synthetic-traffic-snapshot.v2` 계약과 결정적 Engine은 오토바이 1대·주변 승용차 최대 3대, 0.25초 Tick, 28초 합성 신호, 우측 차로·정지선·최소 간격을 유지하면서 `사가정로 접근 → 면목로44길·면목로44가길 생활도로 → OSM way 1256772587 골목 → 후보 종점`의 3-leg 표시 경로를 연결했다. 이동망 판본·projection/content hash, 원천 edge 15개와 역순 표시 3개, 원천/표시 거리와 별도 경로 지문을 상태 사본에 보존한다. 실제 OSM 방향·access는 각각 `Unknown / PendingHumanReview`이고 모든 통행·gameplay·주문 변경 권위는 false다.
- 서버 저밀도 집중 시험 26/26와 배달 관련 결합 회귀 65/65를 통과했고 개별 TRX는 `artifacts/local/validation/20260913-sagajeong-continuous-route-evidence`에 남겼다. 최종 범위 Task는 Simulation solution build와 전체 시험 1,954/1,954를 통과했다(`artifacts/local/validation/20260913-185444`). 첫 실제 Play에서 정지선까지 일부 이동한 Tick의 `WaitingAtSignal`에 이동 평균 속도가 남는 불일치를 발견해, 위치·진행량은 보존하고 대기 상태의 현재 속도만 0으로 고쳤다. 서버와 Unity 양쪽의 Tick 0~400 전 구간 회귀로 재발을 막았다.
- 별도 Unity 저장소에는 v2 엄격 Interpreter, 차선·횡단보도·신호기·저밀도 배우 Playback과 전체·생활도로·골목 후보 종점 검증 View를 추가했다. Unity EditMode 15/15가 통과했다. 격리된 Unity `6000.5.6f1` canonical `SimulationWorldShell` Play Mode에서 자동 상태 사본 287개·최종 Tick 286을 적용해 적색 정지·녹색 재출발, 세 구간 진입과 후보 종점 정차를 확인했다. 경로 진행은 277.277/277.277m, 실제 Transform 관찰은 대로 106.590m·생활도로 147.209m·골목 23.335m다.
- 전체 대로 접근·생활도로·골목 후보 종점 Game View PNG 3장을 `artifacts/local/validation/20260913-sagajeong-continuous-unity/gameview/20260913-185020-153`에 남겼고 지붕 가림 구간에는 실제 기사 Transform의 화면 투영점을 투시 표식으로 명시했다. 화면은 Engine 상태 사본을 Unity Interpreter에 직접 적용한 증거이며 Hosted HTTP·JSON Client, 실제 OSM 통행 승인·길찾기, 운영 주문 상태 전이, Save/Replay, 실제 입력과 저장 Scene 결속은 미검증이다. 실행 전후 저장 Scene SHA-256은 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`로 같고 Git 변경은 0이며 메인 편집기의 미저장 Scene을 보존했다. 기존 canonical Scene의 bootstrap·replay·server 연결과 누락 자산 오류는 별도 잔존한다. 이번 r3 변경과 캡처는 아직 commit·push하지 않았다.
- 사용자는 A안을 선택하면서 단일 교차로 주변보다 충분한 거리에서 통근·장보기·산책·배달과 골목 양보가 이어지는 생활상을 요청했다. 전체 1km는 정적 배경으로 유지하고, 약 390m × 350m 안의 역세권 순환 339.808m와 동측 이면도로·골목 순환 302.557m를 연결부 왕복으로 묶은 약 753.060m 8자형 활성 생활 회랑과 보행자 우선 공유 골목 충돌 구역 2곳을 r4 후보로 기록했다.
- r4 배우 후보는 기존 합성 생활 배우 9명과 주변 보행자 6명이며, 차량 방향 그래프·보행 그래프·횡단 충돌 구역·공유 골목 면을 분리한다. 원본 건물·주소 결속·도로 좌표는 보존하고 폭·여백·배우 offset은 `PresentationGeometry / SyntheticDisplayOnly`로만 과장할 수 있다. 정확한 활성 범위와 첫 시간대는 질문 상태이므로 r4 코드·작업 명세·Graph Map·Unity 실행은 시작하지 않았고 commit·push도 하지 않았다.
- 사용자는 A+ 생활상을 기존 H1~H4·AreaSet, WI와 Sky Engine에 조화시키고 모듈식으로 관리하는 리팩터링 방향을 요청했다. 1km 공간 근거는 H 밖에 유지하고 의미 있는 행동 지점만 H1, 역 교차로·생활 상권·주거·공유 골목을 H2, 339.808m 역세권 순환과 302.557m 이면도로·골목 순환을 H3 후보로 둔다. 현행 Town 구성의 일부 역할만 충족하므로 H4/AreaSet은 부분 구성 후보이고 H5는 단일 역 첫 범위에서 만들지 않는다.
- 기존 음식점 `ACCEPT/COOK`, 생활 `LIFE-SHIFT/REST`, 배달 `ASSIGN/MOVE/PICKUP/DELIVER/RECEIVE/RETURN` WI를 재사용한다. 신호·횡단 대기와 골목 양보는 새 WI가 아니라 `MOVE` 하위 Task·guard·점유 규칙이다. 조사 중 생활 코드가 `Working/Resting` 밖의 상태까지 생활 WI ActionRecord로 기록하는 불일치와 `MOVE` 작업 명세의 `Arrived`가 WI 정본 `PositionAdvanced`와 다른 문제를 확인했으며, 별도 구현 승인 전에는 수정하지 않았다.
- Sky Engine은 H/AreaSet 자식이 아닌 세계 공통 표현 계층으로 유지한다. 현행 사가정 전용 카메라·layer·고정 오후 광원은 Nature에 직접 결속된 Sky를 소비하지 않으므로, Shell 수준 상태 공급원과 다중 표현 대상 Adapter로 분리한 뒤 사가정 배경·안개·전용 광원·구름·강수만 같은 상태 사본에서 투영하는 안을 제안했다. 첫 날씨는 표현 전용이며 WI·NPC 일정·이동 결과를 바꾸지 않는다.
- 읽기 전용 재검증에서 AreaSet 구성 패턴은 `Baseline=4 / Variants=4 / H3=32 / Closed=True`로 통과했다. 공간 계층은 `GeneratedDocumentOutOfDate`, 게임 기획 주도 H 재고는 `DemandH2TargetMismatch`로 실패했다. 관련 생성물은 갱신하지 않았고 r5 작업 명세·Graph Map·코드·Scene·Unity 실행·E 승격·commit·push는 수행하지 않았다.

## 역세권 디오라마 모듈·공공 사진·H/WI/Sky 방향 (2026-09-14)

- [역세권 디오라마 모듈 표준 r14](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/README.md)는 사가정 r18을 복사 대상이 아닌 첫 적합성 기준으로 유지하면서, 일반역은 역 중심 1km × 1km이고 대형 환승역만 사유와 치수를 가진 가변 profile을 쓰도록 정했다. 면목역과 용마산역은 서로의 도형·Region ID를 복사하지 않은 독립 `LocalPrivateReview` 사본으로 구현했다. r14는 `SimulationWorldShell` 안의 단일 활성 역세권 Host 아래 공간 근거, H/부분 AreaSet, Mobility, WI/NPC, 업체 Overlay와 Presentation을 분리하고 Sky는 World 공통 형제로 유지하는 제안까지 추가했다.
- 국가철도공단 연결 XLSX 1,099행에서 면목 `0721`, 사가정 `0722`, 용마산(용마폭포공원) `0723`을 선택했다. 원본 SHA-256·수집 시각·원천 기준일을 보존하고 기존 로컬 MySQL 비공개 검토 원장에 원본 1건과 정규화 3건을 저장했다. 첫 적용 신규 3, 재적용 신규 0·기존 3, 별도 연결 재조회 3을 확인했으며 전화번호는 정규화 투영에서 제외했다. 자료·판본 불일치와 제한은 [수집 보고](../Reports/중랑구-7호선-역-기준자료-수집-2026-09-13.md)에 남겼다.
- 공용 역 manifest에 면목 사본의 revision·파일/content hash·수량·결손 5종을 결속했지만 `ServerLoadable=false`, `WaitingForSpatialCoverage`를 유지했다. 기존 Mongo의 행정동별 current projection을 역 중심 좌표계로 덮어쓰지 않으며 station-scoped 저장·조회 계약 전에는 서버 payload로 게시하지 않는다. 기존 행정동 tile 참조는 summary뿐 아니라 실제 payload의 schema·ID·hash·격자·수량·도형 교차까지 검사한다. 지역 Package·역세권 UseCase·Controller 회귀 25/25는 통과했다.
- [면목역 1km 구현 기록](../Reports/면목역-1km-디오라마-구현-2026-09-13.md)에 따라 동결 원천과 면목역 bbox OSM 1회 수집을 조합해 건물 4,165개·방향성 도로 link 124개·표면 48개·행정동 6개·500m tile 4개를 만들었다. 100m cell 100개 중 98개는 건물 근거를 확인했고 `x1:z0`, `x1:z3` 두 곳은 결손으로 남겼다. content hash는 `48FED5BFA9B06075B27D7DCF22EFDF3AE3F98222D337D0298C01C098A62B913A`다. 로컬 MySQL 비공개 원본 ID 48~51은 재적용 신규 0과 별도 `verify` 재조회를 통과했으며 공유 운영 DB에는 쓰지 않았다.
- 별도 Unity 저장소에는 역별 descriptor/profile, 명시 경로 local loader, 엄격 parser/composer, 4×4 Mesh 조립기와 공통 View를 추가했다. 실제 10MB 사본을 `LocalPrivateReview / PartialCoverage / Ready`로 해석하고 16 chunk·16 renderer·vertex 127,629·triangle 67,935, 잘못된 index 0을 확인했다. 확인된 EditMode에서 compiled fixture 직접 호출 15/15를 통과했지만 정식 Test Runner는 새 결과를 게시하지 않아 별도 미확정으로 남긴다. Unity `6000.5.6f1`의 canonical `SimulationWorldShell` Play Mode에서 임시 면목역 View를 실제 표시하고 Game View를 캡처했다. 건물·도로·표면·결손 상태가 화면과 일치했고 면목역 관련 Console 오류는 0이었다. 사가정과 면목역 `OnGUI` 좌표 중첩은 후속 통합 결손이며, 서버 미연결·기존 replay에 따른 범위 밖 Scene 오류 9건은 분리했다. 검증 뒤 임시 설정을 복원했고 Scene은 저장하지 않았으며 dirty false와 실행 전후 동일 SHA-256을 확인했다. 범위 Fast는 이번 면목 경로가 아닌 동시 작업의 기존 `EVIDENCE001` 4건에서 선행 중단됐다. 공개·운영·통행·gameplay는 수행하지 않았다. 관련 구현·증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다.
- [사가정시장 랜드마크 수집 명세](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-market-landmark-collection.r6.md)와 기계 후보 대장을 추가했다. 기존 시장 점 `X=635.556, Z=50.628`과 `osm:way:1256772552`는 각각 `ReferencePointNotEntranceOrFootprint`, `UniqueAddressCandidateNotVerifiedOccupancy`로 유지한다. 수집 항목을 현행 공식 행, 입구·주접근로, 골목 중심선·폭·차양·분기, 시각 캡처 6종, 개별 이미지 권리로 분리했다. 광고·후원은 선정·크기·배치를 바꾸지 못하며 별도 표시 레이어만 허용한다. 아직 사진 다운로드·실측·Graph Map·배치맵·Blender·Unity·Scene은 미실행이다.
- [실제 외관 근거 기반 건물 교체 제안 r8](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/building-visual-replacement-proposal.r8.md)은 `배경 매스 → 동네 외관 문법 → 검증된 대표 랜드마크`의 세 층을 제안한다. 주소·건물도형·건축물대장·항공사진은 위치·질량 근거로, 직접 촬영 또는 개별 이용권을 확인한 사진은 외관 근거로 분리한다. 검증된 Prefab을 먼저 생성한 뒤에만 대응 매스를 숨기고 자원·hash·판본·bounds 오류에는 기존 매스를 유지한다. 사가정시장 입구·골목 overlay는 보존하며 첫 실제 교체 건물 선정은 승인 대기다. 사진 다운로드·후보 확정·Blender·Unity 실행은 하지 않았다.
- [무료 Steam 외관 사진·건물 모델 이용 제안 r9](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/photo-source-and-building-model-use-policy.r9.md)은 무료 가격과 사진·건축물·플랫폼 배포 권리를 분리한다. 직접 촬영·명시적 허락·개별 공공누리 제1유형·CC0/CC BY 자료를 우선하고, 거리뷰·기사·블로그·무표시 사진은 로컬 참고 전용으로 둔다. 사진 원본 사용·모델링 참고·게임 배포·홍보를 별도 판정하며, 첫 표본은 일반적인 독립 건물 한 동의 자체 촬영 사진을 텍스처로 복제하지 않고 저폴리 모델링에 참고하는 안이다. 개별 권리 판단·사진 수집·Blender·Unity 실행은 하지 않았다.
- [공공 사진 첫 수집 결과 r11](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/public-photo-collection-result.r11.md)에 따라 사가정역 정확 사진 5건을 Commons 파일별 원문·라이선스·SHA-256과 함께 로컬 비공개로 수집했다. 공공영역 3건은 역 내부·표지이고, 출입구 2건은 `CC BY-SA 4.0`이어서 ShareAlike 정책과 사람·상가 간판 검토 전에는 Blender·Unity 사용을 차단했다. 사가정시장 정확 사진은 0건이며 중랑구 공식 소식지는 개별 사진 권리 미확인, 공공누리 중랑구 후보 3건은 모두 제4유형이라 원본을 받지 않았다. 로컬 MySQL에는 사진 후보 5·검색 결손 1·권리 제외 3건을 저장했고 첫 적용 신규 9, 재적용 신규 0·기존 9, 독립 재조회 9를 확인했다. Graph Map·배치맵·Blender·Unity·Scene·공개는 수행하지 않았다. 수집 코드·검증·기획은 로컬 커밋으로 정리했고 원격 push는 하지 않았다.
- [공공데이터포털 사진 조사 r12](Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/data-go-kr-photo-research-result.r12.md)에 따라 관광사진 API 안내서·여행기사 CSV 1,332행과 한국관광공사 공급자 미리보기 3건을 로컬 비공개로 수집했다. 정확 범위 여행기사 행은 0건이고 관광사진 API는 현재 키로 HTTP 403이다. 사가정공원은 개별 이용조건 미확인, 용마랜드는 제3자 표기, 용마산 기사는 제4유형 문맥이라 모델링 승인 사진은 0건이다. 로컬 MySQL에는 6건을 저장했고 첫 적용 신규 6, 재적용 신규 0·기존 6, 독립 재조회 6과 자체 시험 17건을 확인했다. API 신청·Blender·Unity·Scene·공개는 수행하지 않았다. 조사 코드·검증·기획은 로컬 커밋으로 정리했고 원격 push는 하지 않았다.

## 지역 Experience Package 서버 첫 절편 (2026-09-13)

- [지역 Experience Package r1](Planning/시스템/PLAN-SYSTEM-REGION-EXPERIENCE-PACKAGES/README.md)에 따라 무료 Steam 본편이 지역별 콘텐츠를 발견·갱신할 Catalog/Manifest 공용 계약과 인증 GET 2개를 추가했다. 첫 `world-region:kr:seoul:jungnang:sagajeong.r1`은 기존 법정동 공간 패키지 대장, 면목제3·8동 지리 API, 표시 overlay와 운영 장면 v2를 참조한다.
- 기존 세부 API와 권위는 합치지 않았다. 기능 플래그가 꺼진 구현 레이어는 endpoint 없는 `Disabled`, 생활밀도·복구 시나리오·gameplay는 `Planned`이며 광고·운영·배포·gameplay 준비 값은 모두 false다. 새 `RegionExperiencePackages` 플래그도 기본 비활성이다.
- 공유 계약·UseCase·Controller·기능 플래그와 기존 행정동/운영 장면 회귀 집중 시험 21/21, 변경 경로 한정 Fast가 통과했다(`artifacts/local/validation/20260913-092935`). Task의 두 solution build 오류·경고 0과 Simulation 전체 1,913/1,913도 통과했다. 서버 전체는 5,197건 중 5,190건 통과·기존 작업트리 관련 7건 실패로 완료 관문이 닫히지 않았다(`artifacts/local/validation/20260913-093030`): 주거공동체/농장 World 관점 metadata 3, 기존 농수산 액션 명명 1, 아키텍처 문구 1, WebApp capability 1, 재료 화면 CSS 1이며 이번 지역 패키지 경로 실패는 없다. RDB/MongoDB 쓰기, Simulation·Unity·Scene, Steamworks/CDN, 실제 다운로드·광고·서비스·공개 출시는 수행하지 않았다. 기존 사가정 공간·방어 작업과 다른 작업트리 변경은 보존했다.

## 사가정역 공간 밀도 모형 표현 (2026-09-13)

- [공간 밀도 표현 r18](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/spatial-density-presentation.r18.md)에 따라 r3 지도는 그대로 두고, 국토교통부 건물도형4,062개와 OSM 표면108개를 읽기 전용 비공개 표현 사본으로 합성했다. 원천 높이1,427개와 상징적4m fallback2,635개를 구분하며 층수 추정·실제 외관·통행/방어 권위는 만들지 않았다. 100칸 감사 결과는 건물확인88·열린표면확인2·표면근거불완전5·결손5다.
- 별도 Unity 저장소의 기본 절차적 모형은 4×4 chunk, 높이/용도 massing, 창문·옥상, 도로 접합·연속 표시, 실제 표면 녹색, 장식 나무·접지 그림자를 표현한다. Synty 분기는 비공개 overlay를 읽지 않고 r3 판본/hash만 보고한다. 사가정 관찰 레이어30과 기존 오프라인 생활 레이어29를 공용 계약으로 분리해 카메라·조명·Volume·점유 판정의 상호 간섭을 차단했다. 로컬 비공개 자료와 캡처는 `artifacts/local/`에만 두며 공개 Resources·서버/API·canonical Scene은 변경하지 않았다.
- 원자료 재파싱·독립 감사·동일 입력 2회 byte 결정성·rehash 변조 거부·승격 음성 시험이 통과했다. Unity 컴파일 오류0, 공간 overlay54/54·사가정 전체73/73·레이어 계약2/2를 최신 응답으로 확인했다. 실제 전체/역 확대 Game View에서 overlay건물4,062·표면108·renderer batch36·장식 나무97·역 상세 건물225를 확인했고, 최종 전체 보기3,546프레임 평균289.28fps·95백분위4.78ms였다.
- 현재 자료는 `LocalPrivateReview / distributionApproved=false / RightsConflictUnresolved`다. 모든 출처의 공개 허용·feature 결속과 별도 검토된 revision→asset SHA가 없으면 공개 자산을 거부하고 r3로 복귀한다. 실제 입력·주소/방어 폐루프·서버 연결·운영 효과·WI/E 승격은 검증하지 않았으며, 다른 canonical 모듈의 기존 서버 세션 오류는 남는다. Play Mode 종료와 Scene stopped/clean을 확인했다. 관련 구현·화면 증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았으며 다른 스레드 변경은 보존했다.

- 최신 실제 [Game View 재검증](../Changes/2026-09-12-sagajeong-address-gameview.md)을 완료했다. 도로명/건물번호 페이지/선택/휠/드래그/전체귀환과 방어 준비 전환을 합성 GUI 입력으로 확인했고, 후보 높이8.3m/19.1m 및 결손4m 유지도 실행 Mesh와 화면에서 대조했다. 첫6개 주소 제한은 페이지 탐색으로, 방어 버튼 겹침은 창 이동으로 보완했으며 부모 Transform을 선택 좌표에 반영했다. 수정은 Unity View·도로명주소 시험2파일과 검증 문서/PNG에 한정된다.
- 최종 컴파일 오류0·집중72/72, 지도/Scene hash 불변, Editor stopped/Scene clean. 이번 도로명 Game View 미검증 상태는 해소했지만 관찰 Controller Success/업무0건으로 여섯 업무 새 실행은 하지 않았다. 기존 다른 영역의 서버 연결/세션 부재·Replay hash 오류는 별도이며 Console 전체 정상·실운영·작은 화면·E승격·Scene 저장은 선언하지 않는다. 관련 구현·화면 증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다. 상세 결과/초기시험실패/Console은 `artifacts/local/validation/sagajeong-gameview-r15/`에 있다.

- 최신 [도로명주소 표현층 r15](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/road-address-presentation.r15.md)는 동결 OSM 원자료와 지도 r3를 대조해 도로명78개·명명 구간1,109개·연결 건물568개를 별도 Unity 표현 자산으로 생성했다. 전체 보기 큰길4개 우선, 확대 시 가까운 생활도로, 도로명 선택 시 공개 건물번호와 관찰 좌표를 제공한다. 사업체·입주·영업 정보와 비공개 주소 대장은 넣지 않았고, 지도 hash·업무 표식·높이·방어 자리·Scene·서버/DB는 변경하지 않았다.
- 최초 자동 검증은 총70/70이며 `artifacts/local/validation/sagajeong-road-address-r15/summary.json`에 기록했다. 후속 실제 Game View와 최종72/72 결과는 위 최신 검증 및 [구현 결과](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/road-address.implementation.r15.md)를 따른다.
- 연속 필터 실행 중 Pipeline 시험 수집기의 중복 완료/상태 파일 공유 진단이 발생했으나 각 완료 결과는 통과였고 대기 세션 정리·Console 초기화 후 새 오류가 없었다. 제품 코드 오류와 구분해 구현 결과에 남겼다.

- 최신 [건물 높이 보완층 r14](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/building-presentation.r14.md)는 사용자 다음 단계 승인으로 두 주소·필지 후보의 대장 높이8.3m/19.1m를 Unity 읽기 전용 표현층에 연결했다. `건물높이보완자료.cs`, `SagajeongBuildingHeightOverlay.json`, `사가정지리MeshBuilder.cs`, `사가정운영디오라마View.cs`, 전용 EditMode 시험을 추가/수정했다. 원본602개 지도 hash·좌표·임시 높이·운영/주소/방어 배치 계약은 보존하고, 5층/높이결손 건물은4m를 유지한다. 공식 건물 동일성 확정이나 DB 검토 상태 승격이 아니다.
- 새 시험19/19·기존 지도7/7·방어8/8·운영 표시5/5 통과. 생성 Mesh/뷰 조립은 EditMode에서 검증했으며 실제 Play Mode/Game View·Scene 저장·E 승격·commit/push는 하지 않았다. 초기 EditMode 초기화·메시지 호출 시험 오류와 보완 이력을 `artifacts/local/validation/sagajeong-building-height-r14/`에 보존한다. [구현 결과](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/building-height.implementation.r14.md)에서 최종 범위와 제한을 확인한다.
- 선행 [재수집 r13](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/building-evidence.r13.md)의 같은3필지/3건(원본44~46·정규화90738~90740) 저장·재처리 신규0/독립 재조회3은 유지된다. 이번에는 외부 API 재호출·DB 쓰기 없이 동결 응답 hash와 두 높이를 독립 대조했다. r13의 Fast/Task·도구 build·새8/기존19+19 검증 기록은 `artifacts/local/validation/20260912-220551`이다.

- 최신 [건물 자료 연결 대장 r12](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/building-evidence.r12.md)는 기존 지도602개 건물에서 주소 후보244개·GIS 필지 후보226개(기존 ZIP의 고유행264개)를 찾아 비공개 검토 대장으로 연결했다. C# 대장 도구·기존 명령 진입·독립 PowerShell 검사만 추가하고 기존 원본/건물 ID·지도 hash를 유지했다. 현재 로컬 `hongdal-mysql-1 / hongdal_dev`에 대장 참조602건(ID90136~90737, 원본참조43)을 저장했으며 재처리 신규0/기존602, 독립 재조회602를 확인했다. 자체19·기존 주소17/공간19 시험과 도구 build, GIS 재추출/대장 재생·독립602개 대조가 통과했다. 추가 건축물대장 조회는 첫 HTTP403으로 중단되어 나머지2필지를 요청하지 않았고 신규 건물 속성 수신은0건이다. API 접근·GIS 이용조건 및 건물 윤곽 대응·추정 산식 확인이 남아 Unity 높이 변경은0건이며 Scene·실행 검증·새 개발 인계·commit/push는 하지 않았다.
- r12 범위 Fast/Task와 로컬 문서 링크 검사는 통과했다(`artifacts/local/validation/20260912-215956`). 공용 검사가 eng 코드를 문서류로 분류해 build/test를 생략하므로 별도 도구 검증과 구분한다. 기존 건축물 표제부 DB는 다른 시군구 자료뿐으로 사가정 높이 보완에 사용하지 않았다.

- 최신 [구간 좌표·차로 수 축적 r9](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/road-space.r9.md)는 전국 배포본 한 건 다운로드 승인을 받아 사가정 방향별 링크 160개를 추출하고 기존 로컬 DB에 검토보류로 저장했다(ID89976~90135, 원본/표본 계보40/41). 최초 신규160·재처리 신규0/기존160·독립 재조회160을 확인했고 기존8건과 검토 기록168건이다. 원본 바이너리 별도 검사·추출 결정성 통과, 좌표/차로/출처 활용 트리 r9 갱신. 모든 선택 구간 원문 갱신일은20250512로 최신 현장 관측으로 오인하지 않는다. 수집/추출/검증 도구와 기존 명령 진입만 변경했으며 운영 API·공유 계약·Unity·게임 폭 정책·commit/push는 변경/수행하지 않았다. r8 지도 조회 응답은 저장하지 않고 별도 허락이 안내된 배포본만 사용했다.

- 후속 [공식 도로 자료 축적 r7](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/road-space.r7.md)는 사가정로·면목로·용마산로·면목천로의 두 공식 공급원 관측 8건을 기존 로컬 `hongdal-mysql-1 / hongdal_dev`에 비공개 검토보류로 저장했다. CSV 19행과 도로명 필터 243행에서 각 4행만 선택했으며 독립 재조회 8건·재처리 신규 0건을 확인했다. 보도 폭과 폭 구간 분류는 다른 의미이고 세 큰길의 소로 분류/일부 보도 면적 산술 차이/구간 대응 미확인을 기록했다. 수집 전용 `사가정도로자료.cs`와 명령 진입만 추가했고 도구 build·자체 시험 8개·독립 CSV 점검은 통과했다. 일반 Fast는 eng 경로 build/test를 생략하므로 별도 검증과 구분한다. 수치 확대율·차로 수는 미정, 게임·Scene·운영 API·새 개발 인계·commit/push는 변경/수행하지 않았으며 기존 r1 승인 hash·명세를 유지한다.

- [승인 기획](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/README.md)과 [구현·검증 기록](Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/implementation.r1.md)의 범위로 진행한다. 기존 행정동 변경 43파일을 계약/서버/실자료/문서 네 커밋(`f231fdf8`, `b7fcd3cd`, `abdabab8`, `e1987725`)으로 먼저 분리했다. push하지 않았으며 이후 다른 스레드의 생활인구 작업은 별도다.
- 공통 Core에 독립 메모리 준비 세션, 미리보기·명령 멱등성·예상 판본·단일 인원 점유·재배치를 구현했다. Unity는 기존 지도/카메라에 준비 UI와 옥상 A/B 확대 기호를 추가하고 운영 표식만 숨긴다. 실제 업무 사본의 갱신과 만료는 계속된다. 공식 Scene 하나에 연결했으며 새 Scene·운영 API·실제 인물 연결은 없다.
- 공간 연구 r1의 닫힘 변 거리 오류를 초기 시험에서 발견하고 실패 이력을 보존했다. 공간 담당이 재검산한 r2(기호 반경 6m+외곽 추가 여유 2m)를 수용했다. 실제 옥상 접근·안전·사격 승인이 아니다.
- Core 집중 12/12, Unity 준비 8/8·운영 표시 5/5·사가정 지도 7/7, 컴파일 오류 0, 주체 검사 도구 회귀 통과. 새 WI 등록 관련 생성 목록/참조 판본 누락을 보완해 최종 범위 Task의 Simulation build·전체 시험이 통과했다(`artifacts/local/validation/20260912-202849`). 실제 준비/A배치/B재배치/모드복귀 후 보존 화면도 확인했다([최종 화면](../Changes/2026-09-12-station-defense-preparation.md)). 전환은 API 직접 호출이며 자동 마우스 입력은 성공하지 않아 실제 클릭 미검증이다. 최종 Editor stopped/clean, Scene/배치자료 hash 불변. 기존 Scene 정책 분류 1건·시작 Console 진단·전역 Graph Map 전수 기획 영향 누락은 별도 문제로 남긴다.
- 새 방어 구현은 미커밋이며 다른 스레드 변경을 포함하지 않는다. 전투·몬스터·이동·사격·바리케이드·주민 피해·보급 경제·영속 저장·Hosted·행정동 타일 이식은 범위 밖이다. 형식상 E 승격을 선언하지 않고 코드/시험/실제 화면 결과를 분리한다.

## 면목동 6개 행정동 생활인구 수집·결손 분석 r1 (2026-09-12)

- [행정동별 서버 기반 디오라마 r3](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/README.md)와 [수집·분석 보고](../Reports/면목동-행정동별-생활인구-수집분석-2026-09-12.md)에 따라 서울시 `OA-14991` 2026년 7월 완결본을 수집했다. 원본은 45,045,885바이트, SHA-256 `c2dbc97bb2d1018d27cede7d47a7b49eedd3a8f6abdd268616fffecbeae3f7bc`이며 공공누리 제1유형이다.
- 면목동 6개 행정동마다 31일×24시간인 744건, 합계 4,464개의 총생활인구 통계 추정치를 로컬 `hongdal-mysql-1 / hongdal_dev` 비공개 검토 원장에 저장했다. 첫 적용 신규 4,464·독립 재조회 4,464, 동일 입력 재적용 신규 0·기존 4,464를 확인했다. 행정안전부 2026-03-01 코드 원장과도 6개 동 이름·식별자를 대조했다.
- 여섯 동 모두 이번 한 달의 야간 평균이 주간보다, 주말 평균이 평일보다 높았다. 면목본동의 시간평균이 27,121.2268로 가장 높고 면목제5동은 11,332.3760으로 가장 낮다. 다만 이 값은 서울시가 2016 행정동 코드·구역으로 사전 집계한 추정치이며 현행 경계 재투영값, 실제 거주자 수, 게임 수요가 아니다.
- 기존 경계 판정을 다시 출력해 건물 후보는 면목제2동 0·제4동 1인 반면 사업장 좌표 관측은 각각 971·526임을 확인했다. 이를 실제 건물 부재나 입주 밀도로 해석하지 않고 사가정 중심 공간 원본의 범위 결손으로 분류했다. 최신 250m 격자 재집계, 현행 주민등록 인구·세대, 면목제2동·제4동 건물 윤곽과 생활 시설은 후속이다.
- 수집기 project build 오류 0, 자체 시험 9/9, 원본 hash·ZIP 구조·4,464시간 완전성·DB 원본 계보·분석 재계산을 확인했다. 운영 HTTP, Mongo 디오라마 통계 갱신, Unity/Scene·Play Mode·Game View, 광고·NPC·주문·배차 연결은 수행하지 않았다.

## 행정동별 서버 기반 디오라마 실제 자료 관문 r2 (2026-09-12)

- [행정동별 서버 기반 디오라마 r2](Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/README.md)에 따라 면목동 실제 자료 완결을 기능 생활권 구현의 필수 선행 관문으로 확정하고 `ActualAdministrativeDongDataGate=Closed`로 검증했다. 첫 Unity 제공 범위는 면목제3·8동(`region:kr:hjd:1126057500`)이며 기존 면목동 법정동 공간 패키지는 그대로 둔다.
- 서버에 결정적 manifest/tile/overlay 생성기, 도로 경계 자르기, Mongo hash별 불변 사본과 현재 판본 포인터, 인증 GET 3개와 ETag를 추가했다. 기능 플래그 `AdministrativeDongDioramaObservation`과 `LocalDioramaSponsorship`은 기본 비활성이며 후원은 관찰 기능과 함께 켜져야 한다.
- 공개 사업장과 후원 표시를 분리했다. 공개 인허가 사업장에 결속한 표시 Claim과 후원 캠페인 RDB 원장·운영자 검토 Service·migration을 추가했고, 검증된 Claim·승인된 기간·`광고`/`후원` 고지를 모두 충족한 항목만 표시한다. 건물 크기·NPC·주문·배차·퀘스트·평판·순위·오행 분류를 바꾸는 계약과 결제 기능은 만들지 않았다.
- Unity 데이터 계층에는 인증 GET 전용 Client·JSON decoder·판본 해석기를 추가했다. manifest에 명시된 의미 장소만 운영 상태와 결합하며 법정동 단위 상태를 모든 행정동에 복제하지 않고 미해결 수로 남긴다. 상태는 메모리에서만 유지하고 `Clear`로 비운다.
- 행정동·후원·기능 플래그 집중 시험 22/22, 서버·API metadata 포함 74/74와 Unity 읽기 경계 4/4가 통과했고 서버·Unity 데이터 project build 오류 0, EF 미반영 모델 차이 0이다. 추가한 EPSG:5181 변환·전수 귀속을 포함한 행정동 디오라마 집중 시험 12/12와 Unity 집중 시험 4/4도 통과했고, 이번 실자료 변경 경로 12개를 지정한 최종 Fast도 통과했다(`artifacts/local/validation/20260912-195806`). 서울시 공식 경계 ZIP(SHA-256 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`)과 사가정 지도에서 6개 행정동, 원본 건물 602·도로 2,397, 건물 귀속 602·미해결 0·중복 0을 확인했고 면목제3·8동에 건물 250·잘린 도로 722·500m 타일 3을 생성했다. 공개 상권 관측 5,411개는 좌표만 판정하고 표시 승인은 0개로 유지했다. 로컬 Mongo `ssalddel_dev`에 투영 hash `d1357bea67e649bd7a930cdac2de51f4a716f423181d917da3aafadba9780f2a`와 귀속 감사 hash `BA174BE2E7713D5BBE65F5C1B41DEF8FA6629B285F443FF2FEE9CCF2734110F0`를 실제 저장한 뒤 같은 입력 재적용과 별도 연결 재조회를 통과했다. MySQL 쓰기, 운영 HTTP, RDB migration 적용, Claim/후원 HTTP API, Unity Scene/Prefab·Play Mode·Game View는 미실행이다. 기존 작업트리 변경은 보존했으며 commit·push는 수행하지 않았다.
- 범위 Task는 `Ssalddel.v0.0.slnx` build를 통과하고 서버 전체 시험 5,190개 중 5,183개가 통과했으나, 이번 행정동 변경 밖의 기존 역할별 API 분류 4건·Web capability 1건·공식 재료 UI 1건·아키텍처 문구 1건 때문에 전체 성공에는 이르지 못했다(`artifacts/local/validation/20260912-195911`).

## 사가정역 생활 디오라마 r3 (2026-09-12)

- [승인 기획 r3](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/diorama.r3.md)와 [구현·검증 기록](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/implementation.r3.md)에 따라 사가정 1km 동결 지도를 기존 배달 Controller에서 분리하고, 공식 `SimulationWorldShell/OperationalOsWorldRoot`의 여섯 가상 완료 업무에 지도 판본·해시가 결속된 위치를 연결했다. 생성 콘셉트는 구도·색감 기준이며 실제 지리와 영업 근거로 사용하지 않았다.
- 지역 지리 Mesh·표현용 그림자·전용 관찰 카메라·전체/선택/귀환과 확대·이동, 휘발성 상태의 만료·비활성화 삭제·재접속을 구현했다. 미등록 위치는 임의 배치를 하지 않는다. 공통 Client는 취소된 늦은 응답과 메모리 만료/삭제의 동시 접근을 보호한다. 운영 API·DB schema·실GPS·움직임·모바일/Web은 변경하지 않았다.
- 실제 첫 검증에서 이전 표본 TTL 만료에 따른 0건, Camera.main 오캡처, 미등록 환경을 숨기는 기존 표시 정책을 각각 구분했다. 지도 Root를 기존 `TryRegisterRuntimeEnvironment`에 등록/반납하도록 고쳤으며 전역 표시 정책을 우회하지 않았다.
- Unity 컴파일 오류 0, 새 지도/배치/라벨 7/7·기존 관찰/재활성화 4/4·지도 비공개 회귀 4/4·표시 정책 26/26을 확인했다. 범위 Fast와 Task가 통과했으며 Task는 해당 실행 시점 Unity solution build 및 공통 시험 779/779다. 상세는 `artifacts/local/validation/20260912-185508`, `20260912-185646`, `sagajeong-diorama-r3`다. 이후 다른 스레드의 행정동 디오라마 변경은 이번 검증에 포함하지 않는다.
- 기존 검증 DB 볼륨을 보존한 새 실행 `observable-operations-run:6dc1f458e32a4d38a1369ea4d17f0bdc`은 600초 Completed, 6/6 발행, Outbox 대기 0·실패 0으로 종료했다. 수정 후 실제 Play Mode의 전체/선택/귀환 및 OnGUI 포함 화면에서 6개 업무·지도 Renderer 5개 유지를 확인했고 안내창·라벨 겹침도 해소했다([실제 화면](../Changes/2026-09-12-sagajeong-diorama.md)). 전환은 검증 메뉴로 확인했으며 실제 클릭·휠·드래그 입력, 상세 자산 시각 마감·이동·실운영은 미검증/범위 밖이다. Unity는 저장 없이 검증을 종료했다. commit·push·운영 활성화는 하지 않았다.

## 관찰 가능한 운영 디오라마 10분 수직 조각 (2026-09-12)

- [관찰 가능한 운영 디오라마 r2](Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/README.md)에 따라 음식 배달·화물 운송·창고/마트의 정상 완료와 회복 완료 합성 표본 6건을 구현했다. 검증 호스트는 `Development + Simulation + 전용 컨테이너 + 전용 MySQL/MongoDB/Redis + 600초` 조건에서만 켜지고 결제·메시지·외부 HTTP를 실행하지 않는다. 상태·명시 시작·일시정지·재개·실패 Outbox 재시도는 전용 loopback API와 명령줄에만 두었다.
- 기존 운영 지역 장면 GET은 매개변수 생략 시 v1 wire 모양을 유지하고, opt-in `operational-world-scene.v2`에는 업무 ID·생명주기·주의 상태·객체 종류·의미 위치·관계·출처와 합성 실행 ID를 추가한다. 미지원 판본은 400 Problem Details로 거절한다. v2 해석기는 잘못되거나 낮은 revision 한 건만 마지막 정상 상태에서 동결하고 다른 객체는 계속 갱신하며, 더 높은 정상 revision으로 해당 객체만 회복한다. 이름·전화번호·정확 주소·GPS·토큰과 Unity 로컬 저장·Replay는 금지했다.
- 전용 Docker 이미지는 서버의 현행 Simulation project graph를 선복사하도록 보완했고, Windows 예약 포트 범위를 피한 `127.0.0.1:53216`에서만 검증 API를 노출한다. 실제 600초 실행 `observable-operations-run:30a3baef85b74a59bb260209f8f48dff`은 6/6 발행, Outbox 대기 0·실패 0으로 완료했다. MongoDB에는 같은 실행의 장면 사본 6건, Redis에는 `Completed/600/6/0/0` 상태가 확인됐고 v2 재조회는 네 OS·6건·자료원 실패 0·로컬 저장/Replay 허용 0을 반환했다.
- Unity canonical `SimulationWorldShell`에 `OperationalOsWorldRoot`를 실제 저장하고 30초 읽기 전용 Controller와 안정 ID primitive View를 결속했다. 실제 Editor 컴파일과 EditMode 3/3, 공유 Unity Data 장면 해석 시험 10/10이 통과했다. 실제 Play Mode에서 Controller `Success`, 네 OS 그룹과 객체 6건을 확인하고 Game View를 캡처했다. 공식 Scene의 다른 기존 CompositionRoot에서는 별도 서버 세션 부재, 오래된 Replay hash, missing script 오류가 함께 관찰됐으므로 Console 전체 무오류나 다른 플레이 기능 정상은 선언하지 않는다.
- 서버 운영 지역 장면 집중 시험 7/7과 Unity Data 장면 해석 10/10, 서버·Unity Data build를 통과했다. 범위 Fast는 코드 지도·E 책임 지도·세 솔루션 build와 선택 회귀를 모두 통과했다(`artifacts/local/validation/20260912-174444`). 범위 Task는 Simulation·Unity 전체 시험을 통과한 뒤 이번 변경 밖의 기존 API 분류 4건·공식 재료 CSS 1건·아키텍처 문구 1건·Web capability 1건 때문에 서버 전체 `5,175`개 중 `5,168`개 통과에서 중단됐다(`artifacts/local/validation/20260912-174727`). 실제 운영 활성화·실사용자/GPS·모바일/Web 화면·결제·메시지·Windows Player build는 수행하지 않았다. Docker 검증 컨테이너와 격리 볼륨은 결과 재조회용으로 유지했으며 원격 push는 하지 않았다.

## 운영 후속 처리 복구 작업대·Unity 배치 준비 첫 절편 (2026-09-12)

- 완료된 원 업무를 되돌리지 않고 실패한 후속 처리만 별도로 관리하도록, 기존 `음식마트원장동기화Outbox`와 `운영체제업무인계Outbox`를 개인정보 없는 공통 복구 조회 결과로 조합했다. 새 중복 원장이나 migration은 만들지 않았으며 현재 책임 OS, 자동 재시도·처리 중·운영자 확인 상태, 다음 처리 시각과 안전 요약만 관리자 API에 제공한다. 원천 ID, 사용자·기사 ID, 주소·연락처, payload와 오류 원문은 반환하지 않는다.
- 실제 재처리기가 있는 음식·마트 원장 동기화의 확정 실패만 운영자가 재시도 예약할 수 있다. 예약은 처리 상태만 `Pending`으로 바꾸고 기존 시도 횟수와 마지막 오류를 보존하며, 이미 완료된 주문·출고 상태를 수정하지 않는다. 처리기가 연결되지 않은 운영체제 인계 Outbox는 재시도 버튼을 제공하지 않고 현재 책임 OS의 확인 대상으로 남긴다.
- 같은 계약을 `Ssalddel.Ui.Common` 카드형 작업대로 만들고 관리자 Web·MAUI 앱에서 재사용했다. Web 개발 환경의 명시적 `?sample=true` 미리보기로 실제 브라우저 데스크톱과 390×844 폭을 확인했으며, 긴 안정 ID 때문에 생긴 가로 넘침을 줄바꿈으로 수정한 뒤 요약·책임·상태·버튼 배치를 재검증했다. 샘플은 운영 API 실패 fallback이 아니며 화면에 검증 자료임을 표시한다.
- Unity Data에는 기존 `OperationalWorldSceneInterpreter`의 현재 사본을 음식·창고·화물 OS별 안정 앵커·시각 키로 바꾸는 배치 준비 계획기를 추가했다. 좌표·표현 JSON·개인정보를 복사하지 않고 만료 항목, 영속 저장 허용 항목, 미지원 OS를 격리한다. 이는 Scene/GameObject 생성이 아닌 순수 C# 준비 계약이며 실제 Unity Editor·Play Mode·Game View는 실행하지 않았다.
- 복구 UseCase·기존 Outbox 집중 시험 `7/7`, Unity 해석·배치 준비 시험 `7/7`, `Ssalddel.v3.5.slnx`와 관리자 Web·관리자 MAUI Windows build는 통과했다. v3.5 build에는 기존 MAUI AndroidX 제약·nullable 경고 60개가 남는다. 넓은 API metadata 표본은 이번 Controller가 아닌 기존 역할별 World 관점·농수산 동작명·도입 이력 문제 5건에서 중단됐다. 범위 Fast는 다른 진행 중 변경의 `OperationalOsObservation*` E3 항목과 공용 생성 지도 불일치에서 중단됐고 공용 지도를 덮어쓰지 않았다(`artifacts/local/validation/20260912-153700`). 실제 MySQL/MongoDB, 운영 서버 HTTP, MAUI 런타임, Unity package import·Scene은 실행하지 않았고 commit·push도 수행하지 않았다.

## 생활권 소형 물류거점 Simulation 첫 절편 r1 (2026-09-12)

- [생활권 소형 물류거점 r1](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/README.md)의 첫 수직 슬라이스를 구현했다. 독립 비주거 공간 후보는 기존 Mongo 지도 신청 가원장의 존재·`warehouse-inbound` 종류·신청자 참여 관계를 검증한 뒤 MySQL 거점에 결속한다. 소유자·관리담당자 동의와 플랫폼 현장 확인·승인을 분리하고, Pilot 진입 시 기존 `창고/임시보관소`를 한 번만 연결한다. 용량 예약과 인계 완료는 멱등하며 완료 건당 보상은 `실제지급대상=false`인 모의 기록만 만든다. `Operational` 모드 Pilot은 차단한다.
- 운영지역 상태 사본에는 Pilot·Active 거점을 `WarehouseActor / NeighborhoodMicroHub`로 추가했지만 생활권 키·대략 위치·수용량만 포함한다. 정확 위치 보호 참조, 신청 원장, 사용자 ID는 공개 API와 Unity 투영에 포함하지 않는다. 관련 Domain·Service·API 권한·운영지역 조회 시험 13개와 서버 build, 마이그레이션 목록·모델 정합성은 통과했다. 실제 MySQL/MongoDB 적용, 서버 실행, Unity Play Mode·Game View, Web·모바일 UI는 실행하지 않았으며 commit·push도 수행하지 않았다.

## 비정상 운송 정상 8개·파손 2개 부분 처리 구현 r5 (2026-09-12)

- [비정상 업무 처리·회복 r5](Planning/공통/PLAN-OPERATIONS-ABNORMAL-WORK-RECOVERY/README.md)에 따라 기사 문제 신고에 정상 확인 수량·영향 수량·현장 진행 불가를 선택적으로 추가했다. 서버는 화주 의뢰의 전체 화물 수량과 합이 일치하는지 확인하고 사건 원장에 업무 통제 상태·보류 범위를 운송 상태와 별도로 저장한다.
- 플랫폼이 적용 가능한 적재물 관련 보험 체계를 마련하고 사고 접수·심사·지급 결과를 비정상 업무 생명주기에 연결하는 방향을 확정했다. 보험 신고만으로 귀책·배상액·지급을 확정하지 않으며 실제 가입 주체·피보험자·수익자·한도·자기부담금·면책·청구 절차는 법률·보험사·운영 검토 전까지 미정이다.
- 특수한 긴급 위험 화면을 별도 체계로 만들지 않고 기존 운송 원장에 `문제 사건 원장`을 결속하는 일반 경로를 확정했다. 배송·운송 지연, 상품 파손, 교통사고, 수량 불일치, 상하차·수령 불가와 기타 문제를 선택하고 설명·증빙을 남기면 플랫폼 운영 담당자와 필요한 관계자에게 알린다. 운송 생명주기 상태·문제 사건 상태·업무 통제 상태를 분리해 정상 진행 맥락을 보존한다.
- 관리자 운송 진행 API에 비정상 사건 목록 조회와 예상 revision 기반 결정을 추가했다. 정상 8개·파손 2개를 선택하면 운임 정산의 임시 잠금은 해제되고 영향 2개는 적재물 보험 적용 가능성 검토 대기로 남는다. 전체 보류·재개·종료도 명시적 결정으로 제공하지만 실제 보험금·귀책·배상·환불·재배송은 자동 확정하지 않는다.
- 기존 검토대기 자료는 새 bool이 없어도 정산을 계속 차단하고, 새 부분 인수 결정 뒤에는 기사 운임 지급 준비가 열리도록 호환했다. 새 migration `AddAbnormalTransportIncidentScopeAndDecision`을 생성했지만 개발·운영 DB에는 적용하지 않았다.
- 서버와 `Ssalddel.v3.5.slnx` build는 오류 0으로 통과했고 관련 집중 회귀는 `35/35` 통과했다. 범위 Fast는 build 통과 후 `178`개 중 `177`개가 통과했으며, 이번 Controller가 아닌 기존 세 Controller의 API 도입 이력 메타데이터 누락 1건 때문에 중단됐다. 로그는 `artifacts/local/validation/20260912-143543`이다.
- Web·모바일 화면, 실제 MySQL·MongoDB, 보험사 연계, Unity는 실행하지 않았다. commit·push도 수행하지 않았다. 다음 문답은 수령인 부재에서 재연락·짧은 대기 뒤 운영자가 새 시간창·안전 보관·반송을 고르는 경로다.

## Unity 운영 OS 생명주기 우선 수직 관문 (2026-09-12)

- 운영 서버가 실제 업무 상태를 확정하고 Unity는 비식별·판본 있는 상태 사본을 읽어 `SimulationWorldShell`에 표현만 한다는 경계를 [Unity 운영 데이터 읽기 전용 관찰 안내](../Architecture/Unity운영데이터읽기전용관찰안내.md)로 모았다. 이 문서는 새 권위를 만들지 않고 공용 컨텍스트, 책임 분리, 서버→Unity 변환, 현행 이관 기획과 역할별 경계 문서를 읽는 순서로 연결한다.
- [Unity OS 관찰 모듈 상향식 계획 r4](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/unity-os-observation-modules.r4.md)는 `FoodDeliveryServerVerticalSliceTested / OperatingSystemCenteredShellConfirmed`로 갱신했다. OS 하나의 운영 서버 생명주기·완료 사본·인증 HTTP·Unity 메모리 인계를 먼저 닫고, 그런 뒤에만 공간·primitive·Prefab을 검토하는 OS별 수직 관문을 확정했다. 첫 표본은 `FoodDeliveryOS` 정상 완료다.
- `음식배달정상수직생명주기Tests`를 추가해 실제 주문 등록→음식점 수락→픽업 준비→기사 수락→픽업→전달→주문자 수령 확인→완료 World 상태 사본 조회를 한 SQLite 서버 시험으로 관통했다. 일곱 단계 순서, 수령 확인·완료 Outbox 멱등성, 완료 revision 일치와 주문·주문자·기사·상세 주소 비공개를 확인했으며 집중 시험 `1/1`과 범위 Fast가 통과했다. Task의 `Ssalddel.v0.0.slnx` build도 통과했지만 전체 시험은 기존 분류·문구·UI 기준선 8건 때문에 `5,148`개 중 `5,140`개 통과에서 중단됐고 로그는 `artifacts/local/validation/20260912-132158`이다.
- 배차 후보 탐색·점수 계산은 기존 배차 엔진 독립 시험의 범위이며 새 서버 시험은 추천된 운송 원장을 입력 경계로 삼는다. Unity에는 `OperationalWorldOperatingSystemIds`, OS별 메모리 모듈 대장·Router, `FoodDeliveryOsObservationAdapter`, 인증 GET→Interpreter→Router를 잇는 `OperationalOsWorldObservationSession`을 추가했다. 완료·온라인 일회성·저장/재생 금지 조건만 받아들이고 미지원 OS와 안전하지 않은 항목을 진단으로 남기며, TTL 제거와 거부된 갱신의 기존 상태 보존을 집중 시험 `5/5`, 관련 운영 World 회귀 `19/19`, Unity Data build 오류 0으로 확인했다. E 책임 지도는 새 E3 책임을 반영해 재생성했으나 범위 Fast는 이번 모듈 밖의 기존 `EVIDENCE001` 8건에서 중단됐고 로그는 `artifacts/local/validation/20260912-133812`이다.
- 실제 GameObject·`SimulationWorldShell` 공간 결속은 아직 수행하지 않았다. 실제 MySQL·서버 HTTP·Unity package import·Play Mode·Game View는 이번 변경에서 실행하지 않았고 commit·push도 수행하지 않았다.

## 주문 중심 운영체제 업무망 우선순위 1~5 구현 (2026-09-12)

- [주문 중심 운영체제 업무망 r6](Planning/공통/PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK/README.md)에서 주문을 다른 OS의 권위가 아니라 인과축·읽기 관점으로 유지한 채 구현 우선순위를 갱신했다. 안정 OS 10개의 생명주기 정의·주문 구간·미정 상태와 실제 구현된 네 OS 인계 계약을 정적 대장으로 제공하고 버전 메타데이터 조회에 포함했다.
- 운송 건마다 주 담당자 1명과 보조 담당자 여러 명을 append-only revision으로 저장한다. 배정이 없으면 화주 본인이 암묵적 주 담당자이며, 보조 담당자의 기본 권한은 진행 조회·현장 증빙 등록·연락 기록이다. 화주만 지정·변경·철회할 수 있고 담당자는 재위임하거나 화주·원천 주문·비용 귀속을 바꿀 수 없다.
- 기존 의뢰 목록·단건·수정·현장 지급·후불 승인·인수증 등록은 현재 담당자 revision의 세부 권한을 검사한다. `GET/PUT api/v1/shipper/requests/{requestId}/operators`로 배정 상태를 조회·갱신하며 EF migration `AddTransportWorkOperatorAssignments`를 생성했다. migration은 공유 개발·운영 DB에 적용하지 않았다.
- 공통 읽기 계약 `OrderWorkNetworkProjection`과 `GET api/v1/shipper/requests/{requestId}/work-network`를 추가했다. 실제 화주→화물 인계, 화물 운송 원장, 화물 완료→화주 인수 인계를 같은 비식별 `CorrelationStableId`로 조합하되 원본 원장을 합치거나 전진시키지 않는다. 주소·연락처·좌표·사용자/기사 식별자는 반환하지 않고 없는 단계는 `PendingCodes`로 남긴다.
- 개인 화주 기본값, 주·보조 담당자 배정·철회·세부 권한, 비권한 은닉, 화주→화물→화주 왕복과 개인정보 제외 집중 시험은 `21/21` 통과했다. 서버와 `Ssalddel.v3.5.slnx` build는 오류 0이고 기존 nullable 경고 2개만 남았으며 EF 모델은 마지막 migration과 일치한다.
- 범위 Fast는 build 통과 뒤 전체 메타데이터 표본 `170`개 중 기존 API 분류·판본 이력 5개가 실패했고, Task는 전체 `5,147`개 중 `5,139`개 통과 뒤 직전 기준선과 같은 아키텍처 문구·역할별 API 메타데이터·재료 UI·WebApp capability 분류 8개에서 중단됐다. 로그는 `artifacts/local/validation/20260912-110016`, `artifacts/local/validation/20260912-110213`이고 집중 시험 결과는 `artifacts/local/validation/order-network-priority-1-5-final`이다.
- 6순위는 일부만 완료했다. 생성 뒤 운송 건 위임은 동작하지만 공동주문 대표·각 개인 주문·비용 분담·운송 생성 전 위임을 결속하는 권위 계약은 없다. 근거 없이 Mongo의 `대표UserId`를 화주로 복제하지 않았으며, 이 결정을 마친 뒤 마트·음식 업무망으로 확장한다.
- 실제 Ssalddel 서버, MySQL/MongoDB, Web·모바일 UI, Unity, 운영 외부 효과는 실행하지 않았다. commit·push도 수행하지 않았다.

## 화주 운송관리 OS ↔ 화물운송 OS 왕복 인계와 비정상 운송 보류 (2026-09-12)

- [화주 운송관리 OS r4](Planning/공통/PLAN-OPERATIONS-SHIPPER-TRANSPORT-MANAGEMENT/README.md)에 따라 기존 화주 운송의뢰·기준운임·인수증·정산 기능을 여덟 단계 생명주기로 정렬하고 안정 식별자 `ShipperTransportManagementOS`를 운영체제·버전 조회 대장에 추가했다. 추상적인 `회복 경로`는 `비정상 운송 처리·업무 회복`으로 바꾸고, 사건 보존·정산 보류·당사자 검토·명시적 해결 뒤 안정 상태에 도달하는 절차로 정의했다.
- 기존 `POST api/v1/shipper/requests`의 서버 운임 계산·요금 검토·Mongo 원장/RDB 투영을 그대로 확정 경계로 재사용한다. 저장된 의뢰만 `ShipperTransportManagementOS → DomesticCargoTransportOS` 인계 원장에 결속하고, 화물운송 OS가 동일 의뢰의 실행 업무를 확인한 뒤 명시적으로 수락한다. 인계는 배차나 기사 상태를 변경하지 않으며 기존 결제·후불 승인 조건을 유지한다.
- 기사 앱의 기존 하차 사진 필수 `운송인수완료Command`와 저장 후 이벤트를 재사용해 `DomesticCargoTransportOS → ShipperTransportManagementOS` 완료 결과 인계를 요청한다. 화주의 기존 인수증 등록이 이 인계를 수락하지만 정산 완료·지급·파손 판정·반품·재위탁은 자동 확정하지 않는다. 단건·목록 재조회는 정방향 책임 인계와 역방향 완료 결과 인계를 구분해 반환한다.
- 정방향 최소 사본은 화물·차량·시간창·운임·정산 조건만, 역방향 최소 사본은 완료 상태·시각·증빙 존재와 생명주기 단계만 포함한다. 두 사본 모두 사용자·기사 식별자, 주소·전화번호·GPS와 사진 객체명·URL을 복제하지 않는다.
- 기존 기사 문제 신고 가운데 `수량불일치`, `화물훼손`, `하차지부재`는 같은 저장 단위에서 결정적 `비정상운송사건` 원장을 생성하거나 revision을 전진시킨다. 사건 원장에는 원문 메모·사진 URL·상세 주소·좌표를 복제하지 않는다. 열린 사건은 화주 정산을 `비정상운송검토보류`로 표시하고 운송 완료 입금 요청, 기사 지급 준비·관리자 승인, 화주의 정산 조건·현장 지급·후불 승인 변경을 차단한다. 보류는 기사 과실·평가·환불·재배송·재위탁을 자동 확정하지 않는다.
- EF migration `SyncTransportWeatherSurchargeAndAbnormalIncident`는 새 사건 원장과, 현재 코드 모델에는 있었지만 이전 스냅샷에 빠져 있던 기사 기상 할증 열을 함께 동기화한다. 개발·운영 DB에는 적용하지 않았다.
- 비정상 사건·입금·지급 준비·지급 승인·화주 조회 집중 시험은 `24/24`, 이번 비정상 운송 32개 경로로 한정한 Fast, `Ssalddel.v3.5.slnx` 전체 build와 EF 모델 변경 누락 검사는 통과했다. 범위 로그는 `artifacts/local/validation/20260912-093625`다. 전체 작업 트리 Fast는 build 뒤 기존 API 판본 이력 메타데이터 시험 1건에서, Task는 전체 `5,132`개 중 `5,124`개 통과 뒤 직전 기준선과 같은 아키텍처 문구·역할별 API 메타데이터·재료 UI·WebApp capability 분류 시험 8개에서 중단됐다. 로그는 `artifacts/local/validation/20260912-093354`, `artifacts/local/validation/20260912-093514`다. 실제 서버·MongoDB·MySQL·결제·배차·화주 Web/모바일 화면은 실행하지 않았다. commit·push는 수행하지 않았다.

## 음식 배달 반경 재판정·픽업지 기상 할증 기반 구현·세 지점 정책 확정 (2026-09-11)

- 기존 `음식배달배차업무정책`의 전역·인접·확장 음식배달권 후보, 기본 5km와 기사별 허용 반경, 위치 10분 신선도, 거절 이력 판정을 그대로 재사용했다. 적격 기사가 없으면 기존 30초 주기 스캔이 같은 엔진을 다시 실행하고, 기사가 반경 안으로 진입한 뒤 선정되는 회귀를 추가했다.
- 적격 기사가 선정된 뒤에만 운영 서버가 음식점 픽업지 좌표를 기상청 초단기실황 5km 격자로 변환해 `PTY`를 조회한다. 같은 격자·관측 기준 시각은 서버 메모리 캐시로 공유하고 동시 캐시 미스도 외부 요청 1회로 합친다. 정상 자료는 기본 70분, 실패·자료 없음은 기본 60초 캐시한다. 운영 정책의 기본·거리 지급액과 플랫폼 부담 건당 1,000원 할증을 계산해, 관측 시각·강수 코드·출처·원본 hash·정책 판본·총 예상 지급액을 같은 `운송실행투영`에 한 번만 동결한다. 자료가 없거나 실패하면 가짜 강수를 만들지 않고 상태를 남기며 자동 할증을 적용하지 않는다.
- [운영 배차 공통 코어 r41](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/README.md)에서 기사 현재 위치·픽업지·전달지 중 한 곳이라도 유효한 강수가 확인되면 후보별 제안에 기상 할증을 한 번 적용하고 여러 지점의 강수를 중복 가산하지 않는 정책을 확정했다. 좌표 기반 배달권을 유지하면서 법정동은 주소·공간, 행정동은 행정·통계, 기상청 격자는 실제 강수 판정에 각각 사용한다. 이 세 지점 일반화와 후보별 동결은 아직 구현하지 않았으며 현재 코드는 픽업지 한 곳만 판정한다.
- FDriver의 기존 `신규 배차 배너 → 지도·상세`를 유지했다. 배너는 도착만 알리고, 상세 배달권에서 서버 총 예상 지급액과 `기상 할증 +1,000원`을 표시하도록 계약·투영·표시 경로를 연결했다. 기존 데이터베이스에는 시작 호환 점검이 부족한 열을 멱등적으로 추가하도록 보완했다.
- 서버 빌드와 FDriver 전체 타겟 빌드는 오류 0으로 통과했다. 기상·요금·서버 캐시·반경 재판정·배차 감사·운영 정책 집중 시험은 `29/29` 통과했다. 같은 격자·관측 시각의 동시 요청 8개가 외부 HTTP 요청 1회로 합쳐지는 것을 검증했다. 실제 공공 API·MySQL·모바일 화면 실행은 아직 검증하지 않았다. 결과는 맥락별 로컬 커밋으로 저장하고 원격 push는 수행하지 않는다.
- 범위 지정 Fast 검증은 통과했다. Task 검증은 전체 `5,110`개 중 `5,102`개가 통과했고, 이번 배차 범위 밖의 기존 아키텍처 문구·커뮤니티/농수산 API 메타데이터·재료 UI·WebApp capability 분류 시험 8개 때문에 전체 성공에는 이르지 못했다. 기사 지급 예정액을 확정해 보여 주는 경로까지이며 실제 지급·월 정산 집행은 이번 범위에 포함하지 않았다.

## 운영 완료 → 비식별 지역 장면 → Unity 메모리 해석 수직 조각 완료 (2026-09-11)

- 음식배달·창고·화물 업무의 순서 있는 완료를 공통 `운영업무완료증명`으로 판정하고, 완료된 음식 배달·권한 있는 창고·당사자 화물 인수 자료원을 `GET api/v1/world/areas/{areaStableId}/scene-snapshots`에서 조합한다. 실제 사용자·주문·기사 식별자, 주소와 GPS는 응답에 넣지 않으며 한 자료원 실패가 다른 자료원 갱신을 막지 않는다.
- 창고 출고 완료는 기존 화물 의뢰를 `WarehouseCommerceFulfillmentOS → DomesticCargoTransportOS` 인계 원장에 멱등 결속하고 명시적으로 수락한다. Unity는 전용 관찰 기능 관문 아래 GET Client·JSON decoder·cursor/TTL/tombstone 해석기를 사용하며 원문과 결과를 로컬 저장하거나 운영 Command를 보내지 않는다.
- 서버 집중 시험 `14/14`, Unity 전송·Client·해석·package 결속 시험 `14/14`, 서버·Unity build 오류 `0`, EF 모델 변경 누락 없음이다. 일회용 로컬 MySQL에 전체 주 DB migration을 적용했고 음식 완료 투영의 재시작·멱등·만료 시험을 통과했다. 최종 기능 스위치로 실제 서버를 기동해 미인증 지역 장면 `401`, 인증된 지역 장면·음식 완료 조회 `200`, 자료원 실패 `0`을 확인한 뒤 서버·검증 DB·임시 인증 항목을 제거했다.
- 공유 개발·운영 DB에는 migration을 적용하지 않았고 운영 기능은 기본 비활성이다. E 책임 지도에는 Unity Client와 시험을 E3로 반영해 현행화했으며 범위 Fast는 공백·Simulation/Unity 코드 지도를 통과한 뒤 이번 범위 밖의 기존 `EVIDENCE001` 8건에서 중단됐다. 현행 체크아웃에는 제품 Unity 프로젝트와 canonical `SimulationWorldShell` 실체가 없어 Unity import·Editor·Play Mode·Game View는 검증하지 않았다. 결과는 맥락별 로컬 커밋으로 저장하고 원격 push는 수행하지 않는다.

## 운영 지역 장면 Unity 읽기 Client 보완 (2026-09-11)

- 인증된 운영 지역 장면 API에 기존 창고 이행 기능 판본 관문을 명시하고, Unity가 `cursor` 기반 GET 응답을 메모리에서 해석하는 `OperationalWorldSceneClient`와 `JsonUtility` decoder를 추가했다. 원문 JSON·해석 결과의 로컬 저장과 운영 Command 전송은 계속 허용하지 않는다.
- 운영 지역 장면 관찰 API는 창고 이행 기능과 분리된 `OperationalWorldObservationWorkflow` 기능 관문을 사용하며 기본값은 비활성이다. 다른 음식·창고 기능만 활성화해도 관찰 API가 열리지 않는 집중 시험 9/9를 확인했다.
- .NET 계약 시험 4/4와 `Ssalddel.v3.5.slnx` 전체 build 오류0을 확인했다. 기존 AndroidX·nullable 분석 경고 60개는 남아 있다. Unity 프로젝트 import·Editor·Play Mode·Game View와 실제 인증 HTTP 연결은 실행하지 않았다. 관련 코드는 로컬 커밋으로 저장했으며 원격 push는 하지 않았다.

## 창고 출고 완료 → 화물운송 OS 인계 보완 (2026-09-11)

- 기존 `출고운송인계완료UseCase`가 출고 완료와 이미 생성된 화물 운송 의뢰를 `WarehouseCommerceFulfillmentOS → DomesticCargoTransportOS` 인계 원장에 결속하고, 도착 OS의 명시적 수락까지 같은 업무 흐름에서 기록하도록 보완했다. 새 운송 의뢰나 배차를 만들지 않으며 결정적 요청 ID로 재시도를 멱등 처리한다.
- 운영 지역 장면 조회가 음식 배달 완료·창고 작업·화물 완료 상태 사본을 한 응답으로 조합하되 실제 주문·운송·기사·화주 식별자와 위치 좌표를 노출하지 않는 회귀 시험을 추가했다. 음식 완료 상태 사본의 MySQL 재시작·만료 시험은 존재하지 않는 탐색 속성 대신 실제 외래 키를 조회하도록 바로잡았다.
- 음식 완료 투영·창고 출고 인계·지역 장면 조회 집중 시험 9/9가 통과했다. 실제 MySQL·운영 서버·Outbox 소비·Unity Editor/Play Mode/Game View는 실행하지 않았다. 관련 코드는 로컬 커밋으로 저장했으며 원격 push는 하지 않았다.

## FoodDeliveryOS 정상 완료 World 상태 사본 수직 조각 (2026-09-11)

- [음식 배달 정상 완료 상태 사본 r1](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/food-delivery-completed-world-projection.r1.md)에 따라 주문자 `수령확인` 저장과 같은 DB 저장 단위에서 기존 Outbox에 발행 요청을 남긴다. 비동기 작업은 `주문대기 → 조리중 → 픽업대기 → 기사배정 → 픽업완료 → 전달완료 → 수령확인` 순서와 확정 기사를 다시 검증하며, 한 항목 실패가 다른 상태 사본 처리를 막지 않고 최대 5회 재시도한다.
- 발행 상태 사본은 실제 주문번호·사용자·음식점·기사 식별자·주소·GPS를 제외하고 사본별 합성 역할 ID와 단계별 경과 초만 보존한다. 면목동·중화동 법정동명이 명시된 주소만 기존 지역 고유 식별자로 분류하고 나머지는 미분류로 남긴다. 한 시간 뒤 조회에서 제외·정리하며 계약은 로컬 저장과 재생을 금지한다.
- 인증된 지역 장면용 `GET api/v1/food-delivery/world/areas/{areaStableId}/completed-lifecycles` 조회와 30초 갱신 Unity 공유 계약을 추가했다. `20260911093054_AddFoodDeliveryCompletedWorldProjection` migration을 만들고 EF 모델 변경 누락 없음까지 확인했으나 실제 DB에는 적용하지 않았다.
- 서버 집중 시험 11/11, Unity 계약 시험 2/2와 서버 build 오류0, EF 모델 변경 누락 없음, 공백·Simulation/Unity 코드 지도 정합성을 확인했다. E 책임 지도는 이번 Unity 계약 시험을 E3로 반영해 현행화했으며 범위 Fast는 이번 변경이 아닌 기존 미분류 8건의 strict 관문에서 중단됐다(`artifacts/local/validation/20260911-183358/evidence-map-check.log`). 실제 서버·MySQL·Quartz·인증 HTTP 연결, Unity 프로젝트 import·Editor·Play Mode·Game View는 실행하지 않았고 Scene·Prefab·모바일·Web 화면은 변경하지 않았다. 관련 코드는 로컬 커밋으로 저장했으며 원격 push는 하지 않았다.

## 단일 Ssalddel 서버로 Hosted Simulation 통합 (2026-09-11)

- [D-557](DECISIONS.md#d-557-ssalddel-하나가-운영-api와-hosted-simulation-api를-함께-호스팅한다)에 따라 장기 실행 호스트를 `Ssalddel` 하나로 통합했다. 기존 `/api/simulation/v1/*`와 Simulation SignalR Hub 계약은 유지하고, 별도 실행 프로젝트 `Ssalddel.Simulation.Server`와 전용 Compose 서비스는 제거했다. Controller·Hub·조립은 비실행 `Ssalddel.Simulation.Hosting` 모듈로 옮겼고 migration·공간 파생 명령은 `eng/Ssalddel.Simulation.Tools` 단발성 CLI로 분리했다.
- Web·MAUI·Hosted Unity의 운영 API와 원격 Simulation 논리 Client는 모두 `SsalddelEndpoints:ServerBaseAddress`와 주 서버 로그인 JWT를 사용한다. 논리 Client·계약·오류 경계는 분리하며 운영 실패·Simulation 실패·Local Runtime 사이 자동 fallback은 없다.
- 물리 호스트만 통합했다. 운영 주문·계약·결제·재고와 Simulation Session·WorldTick·save/replay의 권위는 계속 분리하고 `SimulationSession`·`SimulationWorldDerived` DB도 별도 유지한다. 개인 Simulation 세션은 로그인 주체와 세션 고유 식별자를 새 접근 원장에 결속하며 다른 사용자의 세션은 404로 숨긴다. 정확한 `Testing` 환경의 명시적 계약 시험 우회 외에는 미등록 세션 선점이 허용되지 않는다.
- `20260911085933_SimulationSession접근원장추가` migration을 만들고 EF 모델 차이 없음까지 확인했으나 실제 DB에는 적용하지 않았다. 통합 Compose overlay 구문, Simulation 솔루션과 `Ssalddel.v3.5.slnx` build, 유지보수 CLI, 변경 routing을 확인했고 Simulation 전체 1,900/1,900, 서버 통합 경계·공통 UI 40/40, 원격 업무 흐름 Runtime 16/16 시험이 통과했다. 3.5 build는 오류 0, 기존 DriverApp AndroidX·nullable·xUnit 분석 경고 63개다. E 책임 지도를 재생성했으며 이번에 추가한 시험 호스트는 E3 계약 회귀로 분류되어 기존 미분류 8건만 남는다. 범위 Fast는 공백 검사와 Simulation·Unity 코드 지도를 통과한 뒤 이 기존 8건의 strict 관문에서 중단됐다(`artifacts/local/validation/20260911-182605/evidence-map-check.log`).
- 실제 Ssalddel 서버·MySQL·MongoDB·Redis 연결이나 컨테이너 기동은 하지 않았다. Unity 프로젝트 import·Editor 컴파일·Play Mode·Game View도 실행하지 않았고 Scene·Prefab은 변경하지 않았다. 관련 코드는 로컬 커밋으로 저장했으며 원격 push는 하지 않았다.

## 운영 서버 클라이언트 경계 일원화 (2026-09-11)

- [운영 기능 Unity 이관 r6](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)에 따라 Web·MAUI 1차 앱 조립을 `SsalddelEndpoints:ServerBaseAddress`와 `AddSsalddelOperationalApiHttpClient`로 통일했다. 기존 bare `HttpClient` 소비자는 같은 운영 주소의 호환 등록으로 유지하고, 기능·판본 확인용 `GET api/v1/version-feature-flags` route와 읽기 전용 capability client를 공통화했다. 운영 앱 시작 코드에서 Simulation 업무 흐름 Runtime의 암묵적 등록은 제거했다.
- 원격 Simulation은 `SsalddelEndpoints:ServerBaseAddress`, 이름 있는 `Ssalddel.Simulation.Api`, `AddRemoteSimulationBusinessWorkflowRuntime`를 명시적으로 사용한다. 운영용 일반 `HttpClient`를 Simulation adapter가 소비하지 못하게 조립 관문을 추가했고 운영·Simulation·Local 사이 자동 fallback은 허용하지 않는다.
- Unity는 `IOperationalWorldProjectionTransport` GET 전용 계약과 별도 UnityEngine 전송 assembly를 추가하고 CommunityMarketSquare·Farm·LearningCards·PublicDataHall·ResidentialPickup·UrbanLogisticsCenter·UrbanMarket·WarehouseWorld 샘플의 중복 `UnityWebRequest`를 공통 구현으로 모았다. 자료원 선택 계약은 `OperationalSnapshot`과 `SimulationSession`을 명시적으로 구분하며 자동 fallback과 운영 Command 메서드를 제공하지 않는다.
- 집중 회귀는 운영 클라이언트 구성 44/44, Simulation 조립 15/15, Unity 운영 전송 계약·샘플 중앙화 9/9를 통과했다. 서버, WebApp, UnityReviewApp, Admin, Orderer Windows, RestaurantDesk Windows, HumanResourcesManager Windows와 6개 Android 앱 빌드는 오류0이다. `DriverApp` Android에는 기존 AndroidX 제약 경고 27개, 서버에는 기존 nullable 경고 2개와 병렬 검증 중 파일 점유 재시도 경고가 남았다.
- E 책임 코드는 이번 신규 전송 회귀 시험을 E3로 분류하고 생성 지도를 현행화했다. 범위 Fast는 공백 검사와 Simulation·Unity 코드 지도를 통과한 뒤 이번 경계 작업이 아닌 기존 미분류 8건의 strict 관문에서 중단됐다(`artifacts/local/validation/20260911-171439/evidence-map-check.log`).
- 현행 체크아웃에는 canonical 이름 외에 `SimulationWorldShell` Scene·Controller 실체가 없어 새 Scene을 만들지 않고 실제 Shell 결속을 보류했다. 실제 운영 서버 HTTP·인증·DB 연결, Unity 프로젝트 import·Editor 컴파일·Play Mode·Game View는 실행하지 않았다. 관련 코드는 로컬 커밋으로 저장했으며 원격 push는 하지 않았다.

## 운영 역할 기반 Unity 객체 원형 대장 구현 (2026-09-11)

- [운영 기능 Unity 이관 r5](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)에 서버 `SsalddelActor` 17개 전부와 시설 3개·차량 2개·업무 객체 3개를 합친 객체 원형 25개를 결속했다. 각 원형은 소유 OS·업무·표현 상태·비식별 ID 정책·`VisualKey`와 `Candidate`/`NoUnityRepresentation`을 명시한다. 플랫폼 운영자·고용 주체·관세사·해외 판매자/배송대행지는 명시적으로 생성 제외다.
- 정책 원본과 자동 생성 JSON/Markdown 대장을 v3로 확장하고 `RoleObject` 조회를 추가했다. 서버 역할 전수 대응, 중복 ID, 알려지지 않은 OS·관찰 프로필, 운영 권위·개인정보 유입, 자산 경로 직접 결속을 회귀 검사한다.
- 공통 Simulation 계약은 JSON 구조만 소유하고 Unity 정책은 표현 준비와 실제 Scene 생성 허용을 분리한다. 실제 생성은 `prefabReady`와 `sceneReady`가 모두 필요하지만 현재 모든 원형은 둘 다 `false`다. 따라서 이번 범위는 문서·계약·정적 관문이며 실제 Prefab·Scene·Play Mode·Game View나 운영 서버 연결을 수행하지 않았다.
- 검증은 이관 대장 회귀 50/50, 서버 역할 전수·개인정보 경계 3/3, Unity 생성 관문 4/4와 생성기 build 오류0을 통과했다. 생성 대장 Check와 `RoleObject` OS 조회도 통과했으며 코드 지도·E 책임 지도를 재생성했다. 범위 Fast·Task는 공백 검사와 Simulation·Unity 코드 지도까지 통과한 뒤 이번 변경이 아닌 기존 E 책임 미분류 8건의 strict 관문에서 중단됐다(`artifacts/local/validation/20260911-155647/evidence-map-check.log`, `artifacts/local/validation/20260911-155701/evidence-map-check.log`). 공간 대장 선언 수와 실제 H2/H3/H4 수 차이 경고도 기존대로 남아 있다.

## 화물운송 OS·음식배달 OS 백엔드 기반 구현 (2026-09-11)

- [화물운송 OS와 음식배달 OS r5](Planning/공통/PLAN-OPERATIONS-LOGISTICS-OS/README.md)에 따라 `DomesticCargoTransportOS`와 `FoodDeliveryOS`의 순서 있는 전 생명주기 대장을 계약 계층에 추가했다. 버전 업무 조회는 이 단계와 OS별 Engine Catalog 항목·입출력/정책 판본·역할·활성 상태를 함께 반환한다.
- 배차 후보 선택은 공통 Engine family의 모든 구현을 섞지 않고 `배차업무유형 → 소유 OS → 그 OS의 Active Primary` 순서로 해석한다. 화물은 `CargoYongdalDispatchEngine`, 음식은 `FoodDeliveryDispatchEngine`만 선택하며 승인된 fallback·shadow 항목은 아직 등록하지 않았다. 기존 출고·피킹·공동구매 집단화 엔진도 해당 OS Catalog에 명시적으로 결속했고, 구현이 없는 논리 엔진은 `Declared`로 남긴다. 화물 엔진의 직접 DB 조회를 제거하고 후보 선정 Application Service가 읽은 최소 운송 방식만 엔진 입력 맥락으로 넘긴다.
- 두 OS 사이 인계 계약, MySQL 원장·EF 구성, Coordinator와 Outbox를 추가했다. 도착 OS만 `수락·거절·보류`할 수 있고 수락 전·거절·보류·만료에는 출발 OS가 책임을 유지한다. 생성/결정 요청 ID, 업무 revision, 동시성 토큰, 고유 인덱스로 순차·경쟁 재시도를 멱등 처리하며 Outbox에는 최소 결과만 기록한다.
- `20260911062517_AddOperatingSystemHandoffLedger` 마이그레이션을 생성하고 EF 모델 차이 없음까지 확인했으나 실제 DB에는 적용하지 않았다. 집중 회귀 37/37 통과, 서버 build 오류0·기존 nullable 경고2다. 범위 Fast·Task는 공백 검사와 Simulation·Unity 코드 지도를 통과한 뒤 이번 범위 밖의 기존 E 책임 미분류 8건에서 중단됐다(`artifacts/local/validation/20260911-153333/evidence-map-check.log`, `artifacts/local/validation/20260911-153358/evidence-map-check.log`). 창고 출고 완료→기존 화물 의뢰 인계 생성·수락은 후속 수직 조각에서 연결했다. 마트·음식 주문 등 나머지 ProcessManager 연결, Outbox 발송 worker, 승인된 fallback, 운영 API·모바일·Web·Unity 표현과 운영 활성화는 후속이다. 실제 서버·DB·Redis·Unity 실행은 하지 않았고 관련 코드는 로컬 커밋으로 저장했으며 원격 push는 하지 않았다.

## 공통 코어와 운영·Simulation·Unity·모바일·Web 경계 확정 (2026-09-11)

- [운영 기능 Unity 이관 r4](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)에서 운영 workflow와 Simulation workflow 인터페이스를 분리하고, 상태 코드·값 객체·안정 ID·revision 의미·순수 판정 규칙만 공통 코어에서 공유하기로 확정했다. Web·모바일은 운영 API와 Client Adapter, Unity는 Simulation 계약 또는 승인된 읽기 전용 운영 상태 사본을 사용하며 UI·통신·영속·UnityEngine 의존성을 공통 코어에 넣지 않는다.
- 정식 Simulation facade를 새 `Ssalddel.Simulation.BusinessWorkflow` assembly·namespace와 Unity package로 분리하고 Simulation Application·Infrastructure·Client Infrastructure·Unity 데이터 코어의 생산 참조를 전환했다. 기존 `Ssalddel.BusinessWorkflow`는 기존 namespace·Unity package 소비자를 위한 상속·위임 호환 facade로 남기며 정식 assembly는 이를 역참조하지 않는다. `Ssalddel.Ui.Common`의 API Client 혼재는 후속 분리 감사 대상으로 남겼다. 정식·호환 project 단독 build 오류0, 계약·조립 집중 시험 13/13, Simulation 전체 1,896/1,896, Unity .NET 전체 746/746, `Ssalddel.v3.5.slnx` 전체 build 오류0(기존 경고61)을 확인했다. 범위 Fast는 코드 지도와 공백 검사를 통과한 뒤 기존 E 책임 미분류 8건에서 중단됐으며 로그는 `artifacts/local/validation/20260911-144618/evidence-map-check.log`다. 실제 Unity 프로젝트 import·Editor 컴파일·Play Mode·Game View와 운영 서버·DB·Redis 연결은 수행하지 않았다.

## 운영 배차 공통 코어 백엔드 5차 중단·회복 및 화물 연속배차 기반 (2026-09-11)

- 후속 화물 문답을 [운영 배차 공통 코어 r35](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/README.md)에 반영했다. 플랫폼은 기사 휴식·주유·식사 사유를 수집하거나 추측하지 않고 서버 내부 `운송 약속 그래프`를 주기적으로 순회한다. 다음 콜은 도착 임박 2분·일반 3분·이동 중 5분의 임시 배타 예약으로 유지하되 현재 운송 지연으로 약속 이행이 불가능하면 남은 시간과 관계없이 수락 전 예약만 조기 해제한다. 이 해제는 기사 지표에 넣지 않고 이미 수락한 운송에는 적용하지 않는다. 조기 해제 효과는 아직 구현하지 않았다. 그래프 판정과 경로 안내는 화주에게 공개하지 않으며, 화주에게는 실제 업무 결과가 달라졌을 때만 최소 결과를 알리고 이후 정상 범위로 회복되면 같은 상태 revision에 한 번만 회복 결과를 알린다. 이는 Unity Graph Map 변경이 아니다.
- 화물 연속 배차는 기본 비활성·Shadow 경계에서 계약, 기사 의사 상태, 다음 콜 단일 보유 예약, 수락 reservation/revision 검증, 운송 시간 약속, 현재 위치 기반 위험 조회, Memory/Redis 재구성 투영과 조회 API까지 백엔드 기반을 정리했다. 한 기사당 활성 예약은 nullable 고유 키로 1건만 허용하고, 보유 만료는 기존 추천 만료를 넘지 않는다. 자동 추천·최소지급 정책은 명시 설정 전 켜지지 않는다.
- 기사 수락 뒤 핵심 운송 조건 변경은 기사 재동의를 필수로 하고, 재동의 거절을 기사 불이익에 넣지 않으며 이미 발생한 이동·대기는 보전 대상으로 분리하는 원칙을 확정했다. 다만 조건 변경 요청·재동의·보전 원장은 아직 실제 흐름이 없어 선행 빈 테이블로 만들지 않았고 후속 수직 구현으로 남겼다.
- [운영 배차 공통 코어 r35](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/README.md)은 특정 지역·Unity 장면과 독립된 정본이다. 이번 묶음은 `상품 × 시간대` 조리 설정과 주문별 조리 결정, 기사 배달 시도, 가게 도착·현장 대기, 픽업 전후 중단·재조리·재배차, 운영자 책임 검토를 서버·계약·MySQL 원장에 결속했다.
- 조리 참고값은 최근 28일 같은 음식점·해당 시간 구간의 픽업 준비 완료 표본이 3건 이상이면 산술평균, 부족하면 음식점의 현행 설정, 둘 다 없으면 20분이다. 음식점 명시 선택값은 참고값보다 우선하며 참고·선택·적용값과 결정 출처를 주문에 보존한다. 기사 배정 뒤 조리시간을 바꿔도 기존 배정을 취소하지 않는다.
- 기사 수락마다 `음식배달시도` revision 원장을 만든다. `restaurant-arrival`은 서버 수신 시각을 권위로 기록하고 위치는 차단하지 않는 감사 자료로만 남긴다. 픽업 때 현장 대기 초를 계산하며 조리 지연 재배차 기사는 수락 시각+10분과 최신 예정 시각 중 늦은 값을 표시 기준으로 받는다.
- 중단은 기사 신규 배차 ON/OFF를 바꾸지 않는다. 픽업 전에는 주문을 조리중/픽업대기로 되살리고 같은 영속 배차 원장을 즉시 재추천하며, 픽업 후에는 모든 중단 사유에서 재조리 ID와 새 예정 시각을 만든 뒤 재추천한다. 사고·배터리 부족·배달 수단 고장·위험 기상·하루 첫 개인 긴급은 보호, 조리 지연은 음식점 책임, 나머지는 미확정으로 시작한다.
- 운영 추적 API는 주문의 모든 배달 시도·도착·대기·중단·재조리를 반환한다. 운영자 책임 검토는 revision·멱등 요청 ID·담당자·판정 사유를 저장하며 보호 중단을 기사 책임으로 바꾸려면 사람의 악용 확정이 필요하다. 검토 사건에서 오늘·어제·그제 완료율과 균형 점유 Redis 투영을 영속 원장 기준으로 다시 만든다.
- `20260911030000_AddFoodDeliveryInterruptionRecovery`와 후속 schema 동기화 마이그레이션을 생성했고 EF 모델 차이 없음까지 확인했다. 실제 개발/운영 MySQL에는 적용하지 않았고 실제 Redis 연결도 이번 묶음에서 실행하지 않았다. 서버 build 오류0, 운영 배차·음식 중단·화물 수락·연속배차 정책 등을 묶은 집중 회귀 214/214, Simulation 1,895/1,895, Unity 관찰 25/25가 통과했고 DriverApp 4개 대상 플랫폼 빌드도 오류0이다.
- 범위 Fast와 Task는 `git diff --check`와 Simulation/Unity 코드 지도를 통과한 뒤 이번 범위 밖 기존 E 책임 미분류 8건에서 빌드·시험 실행 전에 중단됐다. 로그는 `artifacts/local/validation/20260911-114326/evidence-map-check.log`, `artifacts/local/validation/20260911-114413/evidence-map-check.log`다. 이 8건은 이전 4차와 같은 Simulation·Unity 타입이며 이번 백엔드 신규 타입은 오류 목록에 없다.
- DriverApp은 서버 화물 작업공간과 reservation/revision을 사용하도록 정리했고 FDriver는 서버가 내려준 동시 수행 상한을 사용한다. 주문자·음식점 전용 프론트엔드는 이번 묶음에서 변경하지 않았다. 세 번째 조리 지연 주문자 알림·취소, 음식점별 오늘·어제·그제 분석·권고 메시지, 사고 재조리 비용 정산, 기사 통지·이의 제기, 실제 배차 점수와 운영 활성화는 후속이다. 실제 인증 HTTP·현장 운행·Play Mode·Game View·배포는 수행하지 않았다.

## 운영 앱과 같은 장면 자율 음식 생활 관찰 분리 (2026-09-10)

- [승인 기획](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/same-scene-food-life-observation.r1.md)과 [구현·검증 보고](../Reports/같은장면-자율음식생활관찰-2026-09-10.md): 운영 앱과 Unity 생활 관찰을 별도 다대다 대장으로 분리했다. 첫 프로필은 `OrdererApp`, `RestaurantDeskApp`, `FoodDeliveryDriverApp`을 `SimulationAnalog`로 묶되 `SimulationSession / AutonomousNpcWorld / observationPresentationOnly`만 허용하고 운영 행위는 금지한다. 기존 Hub 첫 표본은 유지했다.
- Presenter는 주문별 주문자·음식점·기사·단계·대기·현재 시설·완료/거절을 상태 사본에서만 파생한다. Unity 휴대폰은 카드 선택 뒤 주체 상세의 `이 NPC 따라가기`를 다시 눌러야 카메라가 이동하고, 대상이 없으면 전체 개요로 돌아간다.
- 검증은 이관 대장34/34, .NET 관찰25/25·합성 동네10/10·한 기사 전체 흐름1/1, Unity 휴대폰 EditMode9/9·생활 EditMode3/3을 통과했다. 문서 범위 표준 Fast와 변경 파일 공백 검사는 통과했고 코드 범위 표준 Fast·Task는 빌드·시험 전에 기존 E 책임 미분류8건에서 중단됐다. 실제 `SimulationWorldShell` 프로필5 Play에서 주문·정책 추가 입력 없이 Tick50 첫 주문 `수령확인`, 담당 기사 `Idle`, 기사 주문 ID 빈 값을 확인했고 전체 개요·수동 추적·완료 주문 Game View와 Console 오류0을 기록했다. Scene은 저장하지 않았고 실제 마우스 클릭·운영 서버/DB·commit·push는 수행하지 않았다.

## 운영 화물 기사와 Unity NPC 관찰 역할 경계 (2026-09-10)

- [승인 기획](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/freight-driver-role-boundary.r1.md)과 [구현 보고](../Reports/화물배달-운영기사와Unity관찰-경계보완-2026-09-10.md): DriverApp의 실제 화물 운송과 Unity의 가상 화물 NPC 관찰을 분리했다. Unity 기존 물류 Presenter는 `가상 화물 NPC 관찰 · 실제 화물 운송 없음`을 표시하며 운영 기사 API를 연결하지 않는다.
- 화물 수락은 고정 활성 건수 상한을 사용하지 않는다. 추천 잠금 수만 기존 노출 한도로 제한하고, 수락 시 서버가 최신 원장에서 차량·혼적 금지·권장 경로 구간별 중량/부피/팔레트·기존/신규 시간창·근거 누락을 재검증한다. 추천 라운드 불일치와 경고 미확인도 안정 오류 코드로 차단한다.
- `GET api/v1/driver/transports/workspace`는 모든 활성 운송, 다음 행동 운송 하나, 정렬된 상·하차 정차점과 검증 상태를 반환한다. `/current`도 최근 수정 건 대신 현장 행동 우선순위로 선택한다. DriverApp은 별도 화물 작업공간 Store를 사용하고 경고를 명시 확인한 뒤에만 수락 요청에 확인 코드를 보낸다.
- 현재 검증: 서버 수락·화물 조율·작업공간·현재 행동·앱 경계 집중33/33, DriverApp Windows build 오류0, Unity 물류 EditMode10/10 통과. 앞선 범위 Fast는 솔루션 build와 200개 중 195개가 통과했으나 이번 변경 밖 기존 역할별 Controller metadata 5건이 실패했다. 최종 재실행은 다른 생성 문서의 후행 공백 4줄 때문에 전역 diff 관문에서 먼저 중단됐고, 이번 변경 파일만의 `git diff --check`는 통과했다. 실제 운영 서버·DB 다중 host 경쟁·현장 운행·Unity Play Mode/Game View·Scene/Prefab·commit·push는 실행하거나 변경하지 않았다.

## 운영 음식 배달 기사와 Unity NPC 관찰 역할 경계 (2026-09-10)

- [승인 기획](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/driver-role-boundary.r2.md)과 [구현·검증 보고](../Reports/음식배달-운영기사와Unity관찰-경계보완-2026-09-10.md): FDriver의 실제 기사 업무와 Unity의 가상 NPC 관찰을 분리했다. `IBusinessWorkflowRuntime`은 `SimulationSession / AutonomousNpcWorld / ObservationPresentationOnly`만 허용하며 운영 기사 행위는 허용하지 않는다.
- 음식 배달 수락은 기사별 진행 중 건수와 새 요청을 합산해 최대 3건으로 제한한다. 단건·묶음 모두 같은 정책과 `Serializable` 트랜잭션을 사용하고 초과 시 HTTP 409와 `FoodDeliveryActiveWorkLimitExceeded`를 반환한다. FDriver는 서버가 내려준 `MaxActiveDeliveries`를 화면 판단에 사용한다.
- Unity 프로필5는 로컬 자율 NPC 관찰, 프로필6은 격리된 원격 Simulation 검증이며 Editor·Development Build에서만 허용한다. 관찰 화면에는 `가상 NPC 관찰 · 실제 기사 배차 없음` 경계를 표시한다.
- 최종 검증은 Simulation Runtime 12/12, 서버·FDriver 관련 32/32, FDriver Windows build 오류0, 실제 Unity Editor 컴파일 및 EditMode 27/27이다. 범위 Fast는 이번 변경과 무관하게 이미 남아 있던 E 책임 미분류 8개에서 중단됐다. 실제 운영 서버·DB 동시 수락 부하·기사 운행·Unity Play Mode/Game View·배포 빌드 거부 실행은 미검증이다. Scene/Prefab·운영 데이터·commit·push는 변경하거나 실행하지 않았다.

## 업무 흐름 Runtime의 웹·모바일·Unity 공통 조립 (2026-09-10)

- [기준 문서](../Architecture/업무흐름Runtime.md)와 [구현·검증 보고](../Reports/업무흐름Runtime-공통조립-2026-09-10.md): 최초에는 `Ssalddel.BusinessWorkflow`로 주문·음식점·배차·배송·창고 포트를 조립했고, 현재 정식 책임은 `Ssalddel.Simulation.BusinessWorkflow`로 이전했다. 역할별 포트는 같은 하위 Simulation 원장을 사용하며 상태를 복제하지 않고 기존 assembly는 호환 facade로 유지한다.
- 실행 API와 파일 이름은 `BusinessWorkflow`, `WorkflowRule`, `BusinessObjectInteraction`으로 정리했다. 오행·괘상은 선택적 `WorkflowClassificationMetadata`로만 보존하며 `IsExecutionAuthority=false`이고 실행 판정에 사용하지 않는다.
- 웹·모바일은 명시적 `RemoteHost`, Unity Solo는 기존 `LocalSimulationRuntime`을 공유하는 `LocalProcess` 조립을 사용한다. 실패 시 실행 위치 자동 전환은 없다.
- 공통 Runtime 집중 시험 8/8, 규칙·객체 결속 집중 시험 14/14, `Ssalddel.v3.5.slnx`와 `Ssalddel.Unity.slnx`, 공공데이터 importer build 오류 0을 확인했다. 범위 Fast에서 이번 신규 타입의 E 책임 누락은 보완됐고, 병행 작업의 기존 미분류 타입 8개가 남아 전체 Fast는 미통과다. Unity package와 소스 연결은 반영했지만 Editor import·Play Mode·Game View·실제 서버 연결은 미검증이다.

## 법정동별 의미 레이어·배치 전 관계 기반 (2026-09-10)

- [기획·구현 기준 r2](Planning/시스템/PLAN-SYSTEM-NEIGHBORHOOD-SPATIAL-PACKAGES/README.md)와 [구현·검증 보고](../Reports/법정동-의미레이어-관계기반-r2-2026-09-10.md): 기존 구조 `layer`와 v1 읽기 호환을 유지하면서 행정 경계·좌표계·건물·도로·주소·시설 관측·지역 통계·시나리오의 의미 레이어 8개를 공통 대장으로 분리했다. 법정동별 12개 결속은 `semanticLayerStableId`로 조회하며 기존 면목동 Graph/배치 Map과 602건물·2,397도로 안정 ID를 보존한다.
- 첫 확장 동은 중화동 `region:kr:bjd:1126010300`이다. 동결 공식자료에서 상가1,672·공공시설14, 좌표 후보1,683건을 상호명·상세주소·공급자 원본 ID 없이 관측 재고로 만들었다. 공식 경계·건물·도로 도형이 없어 `InventoryReady / GraphMap NotCreated / PlacementMap NotCreated / SceneReady=false`로 멈췄다.
- `spatial-catalog.r4`는 기존 `layer`와 별도로 `semanticLayerStableId` 조회를 제공하고 v2 동별 패키지·면목동 별칭·읽기 전용 Unity 인계 문서를 보관한다. 로컬 Mongo 현행 묶음 `1862BC54…74AAF2`는 34문서·7,511요소·9,692관계, 최초 신규17,238·독립 재조회·같은 입력 신규0이며 다른 Mongo 컬렉션 불변을 확인했다.
- 공간자료 집중 .NET24/24, importer build 경고0/오류0, 전용 원본/hash·수량·개인정보 경계와 범위 Fast를 통과했다. Task의 v3.5 전체 build는 통과했고 전체 시험은 4,993개 중 4,986개 통과·이번 범위 밖 기존 Controller metadata 6건과 공식 재료 UI 1건 실패로 전체 회귀는 미완료다. Unity·Simulation·Scene·Prefab·Play Mode·Game View·MySQL·운영 업무 상태·외부 재수집·commit·push는 변경하거나 실행하지 않았다. 중화동은 계속 `InventoryReady`이며 다음 동도 공식 경계·geometry 전에는 자동 배치하지 않는다.

## 오행 업무 오프라인 생활 구성·보고 (2026-09-09)

- [구현·검증 보고](../Reports/오프라인생활-구성과보고-2026-09-09.md): 명시 합성 JSON → 기존 로컬 Mongo 검토 보관 → JSON 내보내기 → Unity의 LocalSimulationRuntime → 로컬 저장/보고 → 별도 Mongo 보관을 연결했다. 실행 중 서버/DB 조회는 없다. 주민·음식점·기사·마트·창고·화물 기사 안정 ID와 역할/능력을 검사하며 기존 고정 지도와 오행 업무 규칙을 재사용한다.
- 기본 NPC 9개, 기사 수 1~4/ID 변경 지원. 실제 DB 내보내기 JSON만으로 Core 1800 Tick·음식 수령16/마트15/창고보충4를 확인했다. 준비/보고 각각1개 Mongo 문서의 독립 재조회·동일 반입 신규0 확인. 보고는 완전 감사 로그가 아닌 기존 최근 행위 기록+누적 점검점이다.
- 관련 .NET 54+38+17 통과, Unity EditMode5/5(생성/중복방지/명시 해제/슬롯복원/로컬쓰기실패 정지), 수집 도구 build 경고0/오류0. 범위 Fast/Task는 공유 생성 코드 지도 stale 관문에서 중단; 전체 통과 아님. 상세 실패 기록을 보존했다.
- [승인 부록](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/offline-life-bootstrap.r1.md)의 같은 WI·기존 E/승격 비활성 유지. 현재 표시체는 큐브/캡슐이며 실제 Play 자동 설치·UI/Game View·애니메이션은 미검증이다. Scene/Prefab 저장·운영 주문/원격 서버·commit/push0. 다음은 별도 공간 실행의 실제 화면/재진입 검증이다.

## 면목동 오행 업무 객체 대장·Mongo 후보 투영 (2026-09-09)

- [기획·구현 결과](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/myeonmok-five-element-game-object-catalog.r1.md): 주문자=`JIN/WOOD`, 음식점=`RI/FIRE`, 배차=`GAN/EARTH`, 배송=`GAM/WATER`, 창고=`TAE/METAL` 분류를 비권위 메타데이터로 보존했다. 객체 원형 26개, WI Profile 11개, 역할·행위 결속 25개를 정본 JSON으로 만들었고 현재 `BusinessObjectInteractionResolver`와 `BusinessWorkflowRuleEngine`은 이 분류 없이도 같은 업무 규칙을 판정한다.
- 기존 사가정 Geometry를 복제하지 않고 건물602개를 원본 ID·500m 타일에 결속한 중립 `BackdropOnly` 후보로, 도로2,397개를 하나의 중립 도로망 후보로 투영했다. 음식점·주택·창고 지정, 출입구·통행·Collider·Scene/Prefab은 생성하지 않았다.
- `spatial-catalog.r2`에 객체 대장·후보 문서/요소를 추가하고 어댑터 판본을 불변 문서 ID에 결속했다. 첫 apply의 이전 r1 ID/hash 충돌과 다음 묶음의 source manifest 안정 ID 오기는 삭제·덮어쓰기 없이 새 판본으로 복구했다. 현행 로컬 `hongdal-mongo-1 / ssalddel_dev` 묶음 `4387BA71…B78CC66`은 28문서·5,791요소·7,945관계, 이번 판본 최초 신규4,935개·같은 입력 재반입0개이며 독립 재조회와 다른 Mongo 컬렉션 불변을 확인했다.
- 관련 .NET 46/46, 전용 객체 대장 검사1/1, 전체 importer build 경고0/오류0과 문서 링크 검사를 통과했다. 표준 범위 Fast는 이번 범위 밖 동시 변경으로 기존 Simulation Unity 코드 지도가 stale여서 그 관문에서 중단했고 생성 지도를 임의 갱신하지 않았다. MySQL·외부 API·운영 업무 상태·Unity Editor/Play/Game View·Scene/Prefab·commit/push는 실행하거나 변경하지 않았다. Unity용 비식별 인계 사본은 로컬 artifacts에 생성했지만 `SceneReady=false / gameStateConnected=false`다.

## 업무 흐름 규칙 Engine 공통 조립 (2026-09-09, 2026-09-10 명칭 보완)

- [구현·검증 결과](../Reports/업무흐름Runtime-공통조립-2026-09-10.md): 주문·음식점·배차·배송·창고의 순수 규칙 포트를 `BusinessWorkflowRuleEngine`으로 정리하고 기존 `업무상태전이Policy`를 같은 일반 규칙 경계에 연결했다. 과거 `GAN`·`GAM` 설명은 선택적 분류 메타데이터로만 보존하며 실행 권위는 기존 UseCase·Domain·DB/Event/Outbox에 유지한다.
- 음식 주문 Simulation 포트를 음식점 응답부터 수령 확인까지 완성했다. Local Runtime과 HTTP Remote Adapter가 같은 인터페이스를 구현하고, Server의 누락 음식점 응답 경로를 보완했다.
- Unity의 기존 합성 배달 관찰 Controller에 권위 상태 사본→표시 전용 모델 투영기를 연결했다. 최신 Session/Revision·기사 안정 ID·주문 단일 결속과 최신 전이의 오행 규칙 재검증을 통과한 경우에만 기사·차량·화물 GameObject를 갱신하고 Engine·Module·괘·규칙 판본을 표시한다. 기본 한 건 관찰은 첫 주문 수령 뒤 기사가 복귀하면 다음 Tick 전에 멈춘다.
- Workflow 대상27/27, 기존 Simulation 대상31/31, HTTP boundary11/11, 괘 관리133개에 더해 이번 한 기사 전체 흐름·Save/Replay 1/1과 Unity 대상 EditMode18/18을 통과했다. Unity 생성 시험 프로젝트 빌드는 오류0이며 전체 기존 경고를 포함한 경고2,064개가 남는다. 범위 Fast는 현재 작업 트리의 기존 Simulation Unity 코드 지도 stale에서 중단돼 생성 지도를 강제 갱신하지 않았다.
- 운영 DB·실제 배차·Unity Play Mode/Game View·Scene/Prefab·commit/push는 실행하거나 변경하지 않았다. Unity Remote 선택 조립, 다수 기사 수명 관리와 실제 화면 검증은 후속이다.

## 업무 의미 핵심·변경 가능한 표현 분리 — 음식 배달 첫 결속 (2026-09-09)

- [구현·검증 결과](../Reports/업무의미핵심-표현변경분리-음식배달-2026-09-09.md): 음식 배달 공통 규칙을 `food-delivery.v2`로 올려 실제 서버의 `조리중 → 기사배정`을 결속하고, 음식점 진행·EF/메모리 저장·기사 픽업/전달 상태 변경이 같은 내부 Guard를 통과하게 했다. 공개 API·DTO·DB schema·Event/Outbox·상태 코드는 변경하지 않았다.
- 괘·오행 같은 설명 메타데이터는 상태 전이 실행 권위로 사용하지 않는다. 같은 작업 트리의 괘 분류 확장은 이전 미완료 분류 개편과 파일이 겹쳐 이번 커밋 범위에서 제외하고 보존했다.
- 괘 관리 검사, 음식 집중40/40, 관련 서버157/157, Simulation28/28을 통과했다. 새 격리 Docker 주문 `FOOD-20260909020321000`도 299.7839556초 뒤 `수령확인 / 배달완료`로 끝났고 관찰 컨테이너는 종료했다.
- 범위 Fast·Task는 모두 Simulation/Unity 코드 지도까지 통과했으나 현재 작업 트리의 기존 E 책임 코드 지도 stale에서 중단됐다. 공유 생성물을 임의 재작성하지 않았으며 Unity Editor/Play Mode/Game View·push는 수행하지 않았다.

## 운영 서버 음식 배달 폐루프·Unity 상태 사본 (2026-09-09)

- [구현·검증 결과](../Reports/운영서버-음식배달-폐루프와Unity이관-2026-09-09.md): 음식점 메뉴 관리 API, 수락과 같은 트랜잭션의 영속 배차 요청, 재시도 가능한 배차 생성, 픽업 준비 선행검사, 주문 판본, 기사 위치 감사와 운영/Simulation 공통 읽기 상태 사본을 추가했다.
- 격리 Docker 실행 `FOOD-20260909004800418`은 300초 동안 `메뉴 등록 → 주문 → 수락 → 추천·배정 → 픽업 준비 → 픽업 → 전달 → 수령 확인`을 완료했다. MySQL 최종 `수령확인 / 배달완료`, 배차 Outbox `Succeeded/1회`, Mongo 음식 주문 원장 revision8·운송 원장 revision4를 독립 재조회했다. 외부 영업·결제·실제 배달이 아닌 합성 검증이다.
- 서버 집중31/31, Unity 표현1/1, 넓은 음식·배달 회귀574/575 통과. 남은1개는 기존 음식 재료 화면 CSS 최소 높이 기대 불일치다. 서버·Simulation Contracts·Unity 빌드는 오류0이다.
- Unity는 같은 `음식배달ActorView` 타입의 여러 Prefab 인스턴스가 서로 다른 안정 ID와 상태 사본을 표시하는 샘플까지만 준비했다. Editor/Play Mode/Game View·제품 Scene·실제 서버 연결은 미검증이며 Scene/Prefab 원본·commit/push 변경0이다.

## 운영 서버 중심 Simulation 업무 출처 메타데이터 (2026-09-09)

- [다섯 분야 후속 재검증](../Reports/운영서버-5분야-Simulation이관재검증-2026-09-09.md): 주문/배송·운송/창고/음식점/배차의 기존 대표 회귀 157/157에 이어 출처 8/8·운영-Simulation 차이 13/13을 통과했다. `food-workflow-lineage`는 9단계이며 실제 공유 규칙 호출과 의미 재구성, Unity 상태 사본 소비를 구분한다. 전체 API 이관·운영 서버 접속·Unity 화면 성공은 아니다.
- [구현·검증 결과](../Reports/운영서버-Simulation-업무출처메타데이터-2026-09-09.md): 기존 메타데이터/코드 지도에 원천 코드·재사용 종류·공유 규칙·변형 경계를 추가했다. 주문·음식점·음식/화물 배차·화물 운송·창고 적치/출고·Unity 표시를 조회하며 실행 본문과 운영·가상 상태 권위는 변경하지 않았다.
- 최종 관련 회귀 50/50(Simulation29/Unity 라이브러리16/서버5)와 후속 출처·차이 21/21, 코드 지도 9단계 조회·check 통과. Simulation/Unity 빌드 경고0/오류0, v3.5 빌드 기존 경고60/오류0. Fast는 기존 E 책임 미표기 3대상에서 차단되어 전체 검증 완료가 아니다. 생성 지도에는 기존 미반영 타 작업도 함께 반영되었으며 이번 코드 성과와 구분한다.
- 운영 서버/DB 접속·실제 주문·Unity Editor/Play/Game View·Scene 변경·commit/push0. 다음은 필요한 업무별 출처 확대 또는 별도 승인 범위의 실제 연결 검증이며 중지된 작업 자동 재개는 없다.

## Mongo 공간자료 JSON 보관함 r1 (2026-09-09 검증 마감)

- [구현·검증 보고](../Reports/공간자료-MongoDB-JSON통합-2026-09-08.md): 기존 Graph Map11/배치 맵9와 연관6문서를 로컬 `hongdal-mongo-1 / ssalddel_dev`에 native BSON으로 저장했다. 26문서/5,115구성요소/3,689명시 관계, 같은 입력 신규0과 독립 재조회/원문26 hash·mtime 불변을 확인했다. 원문 파일이 편집 권위이고 Mongo는 비공개 검토 사본이다.
- 관리자 전용 JSON API와 `/spatial-catalog` 실제 웹에서 같은 묶음·레이어·페이지·관계·기본 민감 항목 숨김을 확인했다. 관계는 정확 참조2,366/외부·미해소1,279/복수 표현44이며 현실의 공식 연결이나 게임 승인으로 승격하지 않는다.
- 신규40+관리자4 = 범위 Fast44/44 통과. Task 빌드 통과/전체4,940/4,947·타경로7실패로 전체 회귀는 미완료다. 주 앱 `Restarting/exit139`는 미수리이며 제한 검증 host와 운영 서버를 구분한다. 상세 오류·[화면](../Changes/2026-09-08-spatial-catalog.md)·[재현 안내](../Architecture/Mongo공간자료Catalog.md)는 연결 문서를 따른다.
- 자기 검증 탭/host 종료·5298/5299 리스너0. Mongo 자료는 유지했다. 다음은 소비자별 최소 권한/필드 연결이며 Unity 클라이언트·Scene·게임 상태·MySQL·기존 지도/원본·E승격·commit/push 변경0이다. 중지된 다른 작업을 재개하지 않았다.

## 면목동 주소 중심 현실 레이어·디오라마 r1 (2026-09-08)

- [구현·검증 보고](../Reports/면목동-주소중심-현실공간레이어-디오라마-r1.md): 주소204·활동관측801·토지표본30의 비공개 공간 색인과 개인정보 제거 Unity 표현 투영을 생성했다. 이름·상세주소·건물관리번호·원천관측ID는 기본 표현에서 제외하며 배포승인/게임상태연결은 false다.
- Graph/Placement Map v2는 주소색인과 표현투영을 `PrivacyProjectionOf`로 분리하고 47노드/47관계·36타일·204 overlay를 생성했다. 공식건물연결0/단일14/복수8/미연결8을 그대로 유지하며 기존 v1 회귀도 통과했다.
- Unity 소비자는 도로·건물·업종 레이어 토글과 로컬 상세검토 토글까지 코드/어셈블리 컴파일 오류0으로 준비했다. Editor 연결 불가 기준선 때문에 EditMode 실행·Play Mode·Game View는 미검증이다. DB/Scene/게임상태/원자료 변경, commit/push 없음.

## 면목동 집·길·터전 공식자료·30표본 검토 (2026-09-08)

- [결과·접근 차단](../Reports/면목동-집길터전-공식자료와표본결속-2026-09-08.md): 기존 GIS 원본 면목동13,389속성 재사용, 최대30표본 동결 후 지번→GIS14단일/8복수/8미연결. 40개 도형의 구조/폐합 재현이며 위상 전체·공식 경계/다필지·좌표 포함은 미검증. 건물관리번호5404건은25자리 형식만 확인했고 공식 건물/필지/도로 전구간 완료0/30이다.
- 기존 로컬 Docker `hongdal_dev`에 파생 검토관측30행 비공개 `PendingHumanReview` 저장·독립 재조회, 같은 입력 추가0/기존30. 기존 정규화85,473·건물37,383·인허가4,535·확정연결0의 직렬hash 전후 동일. 새DB/API/Assignment/게임 적용0.
- 건축HUB15134735 첫 요청 HTTP403에서 중단/나머지22필지 미요청. 활용신청 승인·현재 키의 서비스권한 확인 필요,403 원인/로그인 필요 자체는 미확정. 공식 경계·주소 건물대응·필지/도로 자료와 GIS 권리/인코딩은 후속. 기존4,889미연결의 원인도 현재 판정불가로 보존.
- 직접 도구 build0경고/오류, 신규19+기존73검사/원본40재현/DB30 재조회 통과. 상세 검증은 전용 보고 참조. 이 작업은 Unity·Scene·동결UI·가격·영업상태 변경0, commit/push0이며 아래 다른 작업의 실제화면 기록과 별도다.

## 공공데이터 기반 아이소메트릭 3D 디오라마 방향 기록 (2026-09-08)

- [표현 방향 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/public-data-isometric-diorama-direction.r1.md): Graph Map이 의미 관계를, 배치 Map이 좌표·외곽·집계와 미배치 사유를 소유하고 Unity는 공개 가능한 읽기 전용 결과를 아이소메트릭 저폴리 3D 디오라마로 표현하는 방향을 확정했다.
- 사가정역 중심 1km를 첫 기준으로 두되 현재 `PlacementReviewOnly / SceneReady=false`를 유지한다. 기존 도형 동네는 `ScenarioOverlayOf` 판타지·업무 시나리오층으로 보존하며 현실 공간으로 승격하지 않는다.
- 이번 변경은 기획 문서만 갱신했다. Unity 코드·Scene·Prefab·카메라·LOD·Play Mode·Game View, DB·외부 API, commit·push는 변경하거나 실행하지 않았다.

## 중랑구 색인·면목동 500m 타일 배치 참고 지도 (2026-09-08)

- [결과](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/regional-reference-map-result.r1.md): 중랑구를 상위 색인, 면목동을 사가정역 ENU 기준 500m 타일 36개의 상세 후보로 정리했다. Graph Map 45노드/44관계와 배치 Map의 타일별 집계를 생성했으며 기존 도형 동네는 `ScenarioOverlayOf` 가상 레이어로 보존했다.
- 동결 사본 hash를 대조해 상가 위치 후보5,411·OSM 건물602/도로2,397·주소 연결801행/204건물·중랑구 공공시설146(현행1km 후보10)을 보존했다. 주소 미연결6,742행과 밖100/좌표없음36은 강제 배치하지 않았다. 개별 주소·사업체 행은 Git 산출물에 복제하지 않았다.
- 정상 source freshness와 오류 주입8종 검사 통과. 기존 Docker DB도 읽기 전용 재조회해 중랑구 공간 신규층134·면목동 주택/병의원362·상가/공장5,537행 일치와 세 실행 모두 `databaseWriteAttempted=false / committed=false`를 확인했다. 결과는 `PlacementReviewOnly / SceneReady=false`; DB쓰기·외부재수집·Unity/Scene/Play/Game View·업무상태 변경0, commit/push0.

## 면목동 전체 수집자료 분포 확장 (2026-09-08)

- 후속 조화 조정: `면목동전체자료View`의 큰 참고판을 제거하고 5411자료 중 기존 건물 연결585건의 개별 점을 제외했다(자료 삭제 아님). 나머지4826건은 원거리355개의100m 밀집도 셀, 근거리 작은 저채도 표식으로 분리했다. 기본 전체보기off는 두 표시 모두 숨기며 기존 카메라 기본구도는 유지한다. 밀집도는 기존 바닥보다 낮게 두어 도로를 덮지 않는다. 실제 Play 검사에서 count5411/중복585/셀355, 기본숨김·근거리·원거리 전환 통과. PNG `Documentation/Changes/2026-09-08-myeonmok-data/harmony-ui.png`; 입력은 검증스크립트 설정이며 수동 줌/선택 완주·업무회귀와 별도다. 원문/DB/기존지도 변경0, commit/push0.
- [전체 분포 표현](../Reports/면목동-전체자료분포-Play확장-2026-09-08.md): DB 재조회 사본의 면목동 상가5411개 위치를 모두 같은 세계에 표시하는 Editor 전용 모듈을 추가했다. 기존 1km 지도/801자료·204건물 패널은 보존한다. 원문 위경도의 측지 기준 미확인으로 WGS84 가정의 검토 표식임을 명시한다. 전역 건물/도로/DEM 완성·영업/입주 확인은 아니다.
- DB read-only verify5537행, 생성5411고유ID·hash 및 재실행 동일hash 확인, 기존 좌표13+맵8오류 검사 통과. Unity 컴파일 오류0, 실제 Play에서 전체보기 count5411·관찰 범위 약2553×2552m를 확인했다. 최초 Bounds 복사본 누적 문제를 수정했다. `Documentation/Changes/2026-09-08-myeonmok-data/wide-ui.png`에 실제 UI 포함 캡처. 기호 배경판은 실제 지형/행정경계가 아니다. Scene저장·DB쓰기·commit·push 없음.

## 축적된 면목동 자료의 Play 표현 (2026-09-08)

- [표현 변경](../Reports/면목동-축적자료-Play표현-2026-09-08.md): 최신 비공개 동결 연결 사본801관측/204건물을 기존 Unity 주소 패널에 연결했다. 같은 지도에 자료 종류별 참고 윤곽을 추가하고 겹치는 숫자 표식을 줄였다. 실제 입주/영업/용도/출입구 확정이나 업무 상태 변경은 아니다. 이전 사본용 검사는 유지한다.
- DB 읽기 재검증: 사업체5,537행 일치, 쓰기0. Unity 이름충돌 컴파일 오류 수정 후 재컴파일 오류0·주소연결 EditMode8/8 통과. 실제 canonical Scene 프로필5 Play에서 801자료·204건물·204윤곽을 확인했고 패널의 총수·주소목록이 나온 화면을 캡처했다. 패널은 검증 스크립트로 열었으며 실제 마우스 클릭 완주·배송/저장 회귀와 별도다. 후속 표식 겹침 개선의 최종 화면은 보고서에 기록한다. 테스트 도구 자체의 중복 완료 callback 오류는 기능 시험 결과와 분리한다.
- 비공개 Editor 전용 관찰, 격리 저장 경로 사용. Scene 저장·공개게시·commit·push 없음. 아래 수집 당시 Unity 미연결 기록은 당시 범위다.

## 면목동 사업체 밀도 보강 (2026-09-08)

- [상가·등록공장 수집 결과](../Reports/면목동-사업체상가제조업-수집-2026-09-08.md): 기존 Docker `hongdal_dev`에 상가5,411+등록공장126=신규5,537관측을 비공개 검토보류 저장·독립 재조회. 동일 입력 추가0/기존5,537. 상가2026-06-30·공장2026-02-24 기준이며 실제현재 영업/전사업체 전수 아님.
- 상가217세부업종/좌표5,411/건물번호5,404/층3,660/호수0. 기존 이름+주소 중복 후보994행·새자료 내부38묶음은 병합하지 않았다. 기존2,006과 합계7,543자료행이며 고유사업체 수 아님.
- 새588행 단일 건물주소후보·60복수·4,889현재지도일치없음. 합계801관측/204건물 후보의 비공개 `myeonmok-business-20260908-r1/connection.json` 생성만 했다. 기존 Unity 동결입력/지도/Scene·실제화면은 변경0, 면목역 포함 지도 확대·입주/출입구는 후속.
- 직접 build0경고/오류·신규36+기존37검사·관련서버회귀30/30 및 원본 재파싱/DB멱등 검증 통과. 초기 스크립트 Item 접근 오류와 수정 후 성공을 보고서에 분리. 검증 `validation-20260908T103725932`, TRX `artifacts/local/validation/myeonmok-business-20260908-r1`. 전체서버회귀/Unity 실행·게시·commit·push0.

## 면목동 수집자료 → 기존 사가정 건물 참고 연결 (2026-09-08)

- [주소 연결 결과](../Reports/면목동-주소자료-사가정건물연결-2026-09-08.md): DB 재조회2,006행 중213행을 기존 OSM121건물의 단일 주소 후보로 연결했다. 음식점145·의원63·공동주택3·시장2. 복수후보22/일치없음1,726/주소없음45는 미연결 보존, 현재 입주·영업·출입구 확정 아님.
- 비공개 `myeonmok-spatial-link-20260908-r1/connection.json` 고정hash → 기존 Unity 사가정 조립의 선택적 주소 패널/숫자 표식 코드로 연결. Editor 로컬 파일 읽기만이며 DB/Resources 복사·게임 상태·지도 도형·Scene 불변. 폐업 등 의료20행 기본숨김/이력별도, 상태 공란은 미확인.
- 직접 도구 build/17검사 및 Unity 신규 EditMode8/8·기존건물4/4 통과. 최종 컴파일오류0·Console신규error0·Editor stopped/Scene dirtyfalse. 실행응답 timeout과 독립 결과회수를 분리했다. Fast/Task는 이 경로에서 diff만이며 전체서버회귀 아님. 실제 Play/Game View·UI 클릭 미검증, Scene저장·commit·push0. 기존 면목역 범위 확대·복수건물·좌표·실시간 서버 연결은 남음. 아래 수집 당시 Unity미연결은 당시 이력으로 보존한다.

## 면목동 주소 중심 밀도 보완 (2026-09-08)

- [면목동 수집 결과](../Reports/면목동-주소기반-공간자료-집중수집-2026-09-08.md): 기존 Docker `hongdal_dev`에 공동주택55·의원300·병원7 신규362행을 비공개 검토보류 저장·독립 재조회했다. 기존 음식점1,594와 시설50 재사용, 합계2,006자료행/1,395주소 묶음/도로명주소미확보45. 고유 장소 수·실제 입주 확정이 아니다.
- 병의원307행은 원천정상202/폐업99/기타6으로 분리했다. 주택은30세대 이상만이며 작은 빌라/다세대 전체·공공임대 여부 미확인. 병의원EPSG:5174 좌표는 미변환, 건물13,389 기존원본은 권리·필드 검토보류 유지. 사가정역·면목역은 후속 공간연결 중심이며 이번 역세권/Unity 확대0.
- 최초 재입력 시간정밀도 차이로 롤백 후 원본/RecordKey를 보존하는 DB시간경계 보완. 동일입력 추가0/기존362·재조회362, 파서20·파일38·Fast96/96 통과. Task v0.0 build 성공/4,900통과·기존과 동일명7실패(이번 경로 밖), 전체성공 아님. 로그 `artifacts/local/validation/20260908-190000`, 최종조회 `myeonmok-address-20260908-r2/address-review-20260908T095925381.json`.
- 이번 Unity/Scene/Assets/Play·새키/신청·게시·commit·push0. 다음은 주소/도로/출입구의 정확 공간결속과 작은 공동주택 공백 보완이다. 아래 사가정 화면·LH 작업은 다른 소유의 별도 근거로 유지한다.

## 중랑구 공간자료 집중 수집 (2026-09-08)

- [첫 수집 결과와 다음 자료층](../Reports/중랑구-공간자료-집중수집-2026-09-08.md): 기존 Docker `hongdal_dev`에 공원58·주차장25·화장실51 신규134행을 검토보류 저장했다. 독립 재조회134·같은입력 추가0·기존시장12 보존 확인. 신규 좌표98/결측36이며 화장실은2018~2020년 원천, 현행 운영으로 해석하지 않는다.
- DB 재조회146행(기존시장12 포함)과 기존 사가정 1km 좌표 안10개 표시 후보를 artifacts에 생성했다. 밖100/좌표미확보36을 보존한다. 이번 Unity/Assets/Scene/Play 변경·실행0, 기존 사가정 화면의 신규시설 적용은 아직 아니다.
- 파서14·출처/수집기반22/22 및 DB 멱등·표시좌표 결정성/거부 검증. 중랑구 전역의 행정경계·도로/출입구·건물/고도·최신 생활시설은 후속이며 `전체수집 완료`가 아니다. 기존 VWorld 권리표기/인코딩·서버 조회 연결 차이를 유지한다.
- 범위 Task: v0.0 빌드 성공, 전체4,904 중4,897통과/7실패(음식화면 CSS·경로/Controller metadata, 이번 쓰기경로 밖). `artifacts/local/validation/20260908-184207/` 보존, 전체성공으로 보고하지 않음. 원인 경로는 전용 보고서에 분리했다.

## 같은 도형 동네의 공공 좌표 참고 확장 (2026-09-08)

- 후속 [사가정 LH 조사·리팩토링](../Reports/사가정-LH연결-리팩토링-2026-09-08.md): 기존 LH 창 계산/표면/스트리밍 책임과 프로필5 우회 경로를 확인했다. 사가정 데이터 r3에 주소580개·층수33개의 원문 속성과 이름/분류/식별자를 보존하고 Unity DTO를 분리했다. 기존 LH 계산기를 재사용하는 500m 주변 건물 조회를 현재 전체1km 조립에 연결했다. 실제 고도 DEM·자동 인접 다운로드·이동별 표시 수명·건물 정보 UI는 미연결이다. 대관령 EPSG:5186 경로와 사가정 ENU를 혼합하지 않는다.
- r3 검증: 연결 Editor의 신규 EditMode4/4 통과(경계 중복 방지·이동 재조회·원본 불변·주소 보존·잘못된 입력 거부), 기존 좌표13건+맵 오류8종 통과. 별도 CLI 시험은 열린 Editor 점유로 시작 실패 후 Pipeline으로 대체했다. 이번 변경의 Play/Game View는 미검증이고 Editor는 Play 종료 상태다. Scene 저장·commit·push 없음.
- 최신 [사가정역 확장 r2](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/neighborhood-extension.r2.md): 역 중심 1km×1km에 OSM 건물602개·도로선분2,397개를 같은 Unity 동네에 단순 3D 표현했다. 기존 도형 동네·경로는 보존했다. 출처/판본/hash/ODbL 표기를 보존하며 높이 미상4m·도로폭은 기호값, 미포함 지역은 공터로 단정하지 않는다. 새 도로의 NPC 이동·업무 시설·DB 연결은 하지 않았다.
- r2 실제 검증: Unity 재컴파일 오류0, Play 카메라 렌더 PNG 판독 완료 (`C:/Users/user/ssalddel/Assets/Documentation/Changes/2026-09-08-sagajeong/overview.png`). 메시 레이어 누락을 고쳐 재실행했다. Tick0/Revision0의 격리 저장 경로에서 공간만 확인했으며 UI 클릭/배송 완주/5분 관찰/저장 회귀는 이번에 검증하지 않았다. Scene 저장·commit·push 없음. 아래 r1 미검증 표기는 당시 결과다.
- [확장 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/neighborhood-extension.r1.md): 기존 동네 9도형·44기준점·14경로를 보존하고 동쪽에 1km×1km 자료 참고 구역을 코드로 추가했다. 면목시장·면목골목시장·사가정시장의 동결 원문 위경도를 WGS84 지역 접평면으로 변환하고 같은 Root의 기호 표식으로 표시한다. 원장 검토보류·출입구/도로 미확정은 유지한다.
- 기존 배치 생성기가 확장 좌표까지 생성한다. 기본 프로필5에서 동네 집중/확장 개요를 전환하며 새 Scene·실행 프로필은 없다. Unity `Ssalddel/동네 관찰` 아래 기본 동네·업무 서버·이전 실험으로 실행 메뉴를 정리했다.
- 좌표·거부·원본 대조13건, 기존 맵 정상/오류8종, Contracts 빌드 및 Unity 컴파일·저장복원 EditMode1/1 통과. 실제 Play Mode·Game View·Scene 저장은 미검증이며 확장 구역의 배달 경로·업무 시설 연결은 후속 범위다. commit·push 없음.
- 범위 Fast 통과. Task는 E 책임 코드 지도와 현재 소스의 불일치로 중단됐다 (`artifacts/local/validation/20260908-180118/evidence-map-check.log`). 전체 검증 완료가 아니다.

## 생활 여덟 영역 공공데이터 계획 r1 (2026-09-08)

- 최신 [감자 환산 전후 단위 대조](../Reports/생활영역-먹거리가격-단위대조-2026-09-08.md): N/Y4회 직접수집·28필드 대조에서 정확환산7/반올림가설양립5/값불변12/결측4. 1개월·1년·평년 비교값에 kg환산을 일괄가정할 수 없다. 비교증거4행(가격신규아님)을 기존 DB에 검토보류 저장·재조회·재입력추가0 확인. 기존가격4/시장12 보존. 자체시험12+기존16/11·관련회귀34 통과, 실제가격/서버소비/게임 적용0. 다음은 기존 가격 서비스의 비교기간 단위계보·소비 거부 관문 보완이다.
- 최신 [감자 가격 두 번째 실자료 수집](../Reports/생활영역-먹거리가격-수집-2026-09-08.md): KAMIS 2026-09-07 도매·소매 상품/중품4행을 기존 `hongdal_dev`에 검토보류 저장, 독립 재조회4·동일입력 추가0. 인증값이 돌아오는 condition은 저장 제외, data 추출본만 보존했다. 원천 unit와kg환산 표기 대조가 남아 NumericValue=null·UnitReviewRequired, 공개/게임 가격 미적용. 자체시험16·기존시장11·관련회귀34 통과. 새키발급/새DB/Unity/commit/push 없음.
- 후속 사용자 요청에 따라 [중랑구 전통시장 첫 실자료 수집](../Reports/생활영역-공공데이터-첫수집-2026-09-08.md)을 수행했다. 포털 공식 JSON 1,393행 중 12곳을 기존 Docker `hongdal_dev` 공공자료 표에 `PendingHumanReview`로 반입하고 독립 재조회·동일 입력 추가0을 확인했다. 원본 hash/이용조건/2025-11-10 기준일을 보존하며 공개 시장 원장·게임에는 적용하지 않았다. 전체 여덟 영역 완료는 아니다.
- [생활 여덟 영역 공공데이터 조사·구현 계획](Planning/자료/PLAN-DATA-EIGHT-LIFE-DOMAINS-001/README.md)을 `Draft / ReadyForReview`로 작성했다. 먹고사는 일·물건과 장사·공동체 문화·집길터전·배움·치안·방문교류·비상대응을 사람이 읽는 분류로 두되 새 게임 단계나 권위 상태로 만들지 않는다.
- 첨부가 제안한 공통 기록은 기존 `PublicDataApiMetadataCatalog → ExternalDataSourceCatalog → ExternalDataIngestionRuntime → RawSnapshot/외부데이터정규화Record → MySQL → SimulationRealityContextService`를 재사용한다. 새 만능 테이블·병렬 카탈로그를 만들지 않고, 반복되는 실제 계약 결손만 선택적으로 보완한다.
- 우선 세 영역은 KAMIS/aT, 전통시장·인허가 사업장, 법정동/VWorld의 기존 계보를 먼저 대조한다. 전통시장 표준자료 15012894는 이번 직접 확인했고 나머지 후보의 실제 접근·권리·좌표계는 별도 확인이 필요하다.
- 최초 계획 작성은 문서만이었고, 위 후속은 제한 도구 빌드·자체시험11·실제 DB 쓰기/재조회까지 수행했다. 키 추출·새 DB/서버·Unity·게임 규칙·commit·push 없음.

## 동네 전체 그래프·배치 맵과 공통 코드 기준 (2026-09-08)

- [맵·코드 선행 정리 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/neighborhood-maps.r1.md) 구현. 관계 노드 19개·관계 20개, 물리 도형 9개·기준점 44개·경로 14개·NPC 9명을 기존 식별자로 연결했다. 논리적 배달 대기 거점은 기존 마트 주변을 공유한다.
- 정본: `eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json`, `placement-map-profiles/synthetic-neighborhood.v1.json`. `manage-neighborhood-maps.ps1`이 공통 C# 배치 정의를 생성·대조한다. 기존 GraphMapTooling 경로/JSON/중복 검사를 재사용했다.
- 코드 소비: 기존 대기점·주체 작업점·음식점/마트 귀가 경로와 Unity 주요 건물·도로·대기점 연결. 기존 좌표·저장·모드·Scene을 보존하며 운영 조회와 생활 작업점 차이는 구별했다.
- 최종 검증: Core 동네 관련 46/46, 맵 정상 검사 및 오류 주입 8종, Unity CLI EditMode `동네관찰RuntimeAdapterTests` 1/1과 Unity 컴파일, 문서 Fast 통과. 통로 해제 직후 이동 재개·밤 전 귀가와 저장/재생·재고 회귀 포함. `artifacts/local/validation/neighborhood-maps-final-core.log`, `neighborhood-map-checks.log`, `neighborhood-maps-unity.xml` 참조.
- 전체 Task 검사는 공용 E 책임 코드 지도의 현행 소스 불일치에서 차단됐다(`neighborhood-maps-task.log`). 공용 생성물을 임의 재작성하지 않았다.
- 남은 배치 차단: 마트 입고점의 기존 바닥 밖 위치, 운영 조회/생활 작업점 차이, 실제 외형·문턱·사람/차량 간격. `SceneReady=false`. 씬 배치·저장·Play Mode·Game View·캡처·E 승격·commit·push 없음.

## 같은 동네의 하루 — 코드 우선, 씬 반영 보류 (2026-09-08)

- [하루 생활 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/neighborhood-day.r1.md): 기존 주민·음식점 주인·마트 작업자·배달 기사의 근무/휴식에 저녁 마감·도보 귀가·식사·수면을 연결하는 선택적 코드 구현. 기존 저장은 종전 근무 주기를 유지하며 보충창고·화물 역할은 보존한다.
- 변경: 생성 계약/저장 복제, `가상동네생활`·`가상동네하루`·해시, 기존 표본 factory·휴대폰 Presenter 및 Unity 소비 코드. 새 Scene·Actor 복제·운영 원장 변경 없음.
- 검증: 후속 맵 정리에서 통로 차단/재개 시험을 보충했고, 하루/생활을 포함한 Core 동네 시험 46/46과 Unity 컴파일·연결 EditMode 1/1이 통과했다. 전체 Task의 E 책임 코드 지도 불일치와 실제 화면 미검증은 위 최신 snapshot 참조.
- 최신 사용자 지시: 코드 정의 → 자동 시험·정합성 확인 → 준비된 변경 묶음의 씬 반영 순서. 이번 씬 배치·저장·캡처는 보류한다. Play Mode·Game View 미검증, E 승격·commit·push 없음.

## 기존 음식 자료 → NPC 선택 → 같은 동네 배달 (2026-09-08)

- [기존 음식 자료로 선택하고 배달하는 동네 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/data-driven-food.r1.md)을 구현했다. 괘상 분류나 추가 수집 대신 기존 식약처 메뉴 이름 세 개와 재료/KAMIS 참고 자료를 읽기 전용으로 추려 동결했다. 검증용 5,000원 판매가와 오래된 재료 가격은 분리하고 공개 원본 DB는 수정하지 않는다.
- 서버 변경: `음식자료선택Policy`, 기존 FoodObserver Runner/Options, 검증용 내보내기·Compose·실행 스크립트, 집중시험. 필요·선호·예산·현재 판매 가능 여부로 하나를 선택하고 불충족이면 주문 없이 대기한다. 정상 역할 API와 기존 주문·배차·픽업·수령 흐름을 재사용하며 자동 반복 주문은 없다.
- 실제 최초 실행에서 기존 `주문자음식주문조회UseCase`가 메뉴 ID를 응답에 복사하지 않던 누락을 발견해 보완하고 회귀시험을 추가했다. 실패 기록과 DB 볼륨은 보존했다. 새 격리 표본의 `FOOD-20260908050802385`는 된장국/수량1/5,000원으로 등록되어 `수령확인 / 배달완료`, 관찰299.264초/설정300초 `Completed`로 종료됐다. 독립 DB 조회의 전체 주문 수는1이고 같은 메뉴·수량·가격을 확인했다. 실제 영업·외부 결제/배송이 아닌 격리 합성 실행이다.
- Unity 변경: 기존 `업무배달관찰Model`, `업무배달관찰ApiMapper`, `업무배달관찰휴대폰View`, `업무배달관찰Tests`. 선택한 메뉴·후보·출처·기준일·선택 이유를 같은 휴대폰에 추가했다. 같은 실행의 메뉴/사본 교체를 거부한다. 기존 Scene·공간·모드·Actor·마트는 보존했으며 마트 업무 자동 실행까지 연결한 것은 아니다.
- 검증: 서버 집중34/34, Unity EditMode57/57 통과, 서버 및 v0.0 솔루션 build 통과, 문서·변경 파일 공백 검사 통과. 최종 Task 전체4,901 중4,894 통과/7 실패(API 메타데이터·다른 UI·route 분류)가 남는다. 해당 실패를 이번 변경 밖에서 임의 수정하지 않았다. 상세는 `artifacts/local/validation/20260908-140802/tests-01.log`와 `artifacts/local/validation/data-driven-food/`.
- 화면: 공간 담당이 Pipeline 인스턴스 없음으로 반환, PNG0장. 실제 Play Mode·Game View·Console·배치 판독은 미검증이다. 코드·격리 서버 성공을 Unity 통합 E 승격으로 보고하지 않는다. 최종 격리 서버는 자동 재주문 없이 결과 조회 상태로 남아 있다. commit/push 없음.

## 공통 도형 동네·마트 입고 관찰 (2026-09-08)

- [같은 동네에 누적하는 마트 창고 업무 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/shared-neighborhood-warehouse.r1.md)의 부분 구현. 기존 모드·슬롯·Scene은 보존하고 로컬/서버 관찰의 마트 내부 조립을 공유했다. 기존 선반·작업자에 입고점·검수대를 추가했다. 작업자 표현 ID는 기존 `actor:synthetic-mart-worker`를 사용한다.
- Unity 변경: `마트공통공간`, `마트입고관찰Model/Repository/Binding`, 선택적 `마트입고검증Connection`, 기존 Controller/배경/서버 휴대폰 및 집중 시험. 조회 자료 범위·중복·역행 시각을 검증하고 같은 작업자로 표현하며 실패 시 마지막 자료와 오류를 유지한다. 보관위치 두 개는 명시적 배치 연결을 요구하고 미연결 위치를 임의 선반으로 배정하지 않는다.
- 서버 업무/API/원장 변경 없음. 실제 입고·검수·적치 자동 실행기와 인증된 창고 표본은 아직 연결되지 않았다. 선택적 연결 파일 또는 기존 인증 client 주입 전에는 미연결로 남으며 로컬 가짜 성공으로 대체하지 않는다.
- 검증: 서버 창고 회귀11/11, 최종 Unity EditMode57/57 통과(실패/건너뜀 0, 연결 설정·보관위치 보완 포함). 문서 Fast와 변경 소스 공백 검사 통과. 공간 담당의 화면 확인은 연결된 Unity Pipeline 인스턴스 없음으로 Blocked이며 PNG 0장이다. 실제 인증 서버 입고 완료·Play Mode·Game View 전중후·E 승격은 미검증. `artifacts/local/validation/shared-neighborhood-warehouse/unity-final-editmode.xml` 참조. commit/push 없음.

## 효사 기획 정본 통합과 조건 정밀화 (2026-09-08)

- [역경 스토리 기획 r36](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)은 사용자에게 효사 질문을 제시할 때 Unicode 괘상 한 글자에만 의존하지 않고 여섯 효를 실제 괘상처럼 쌓은 고정폭 `괘상 시각 카드`를 먼저 보여 주도록 확정했다. 상효는 위, 초효는 아래에 놓고 양효는 이어진 한 줄, 음효는 가운데가 끊긴 두 줄로 그리며, 상·하괘 경계와 자연상·현재 효·아래에서 위로 읽는 방향을 함께 표시한다. 이는 기획 대화·문서의 판독 형식이며 게임 HUD·Unity 구현·Evidence 승격을 뜻하지 않는다.
- [산수몽 캠페인 r3](Planning/스토리/PLAN-STORY-HEX04-CAMPAIGN-001/README.md)는 효 하나를 사건 하나로 제한하지 않고 음효는 두 사건, 양효는 세 사건을 기본 리듬으로 삼아 `2·3·2·2·2·3`의 14개 Story Beat로 구성했다. 기존 사건은 보존하면서 구이를 공격 훈련·방어 훈련·경계 근무로, 상구를 방어 준비·실제 방어·추격 중단과 결산으로 나눴다. 이는 Mirror의 프로젝트 기획 규칙이며 효 안정 ID·호환 ID를 늘리거나 WI·H·Unity 객체를 자동 생성하지 않는다.
- [역경 스토리 기획 r35](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)은 **괘 하나당 정본 `README.md` 하나**를 기준으로 확정했다. 괘 전체 Story Arc와 육효 흐름을 먼저 보고, 현재 효의 `지금·여기·나·너·이렇게`와 재사용/신규 H1·필요 WI·Graph Map·배치 맵 영향을 한 절에서 정밀화한다. 이 표시는 제작 범위 미리보기이며 새 ID 생성·Graph/Unity 반영·Evidence 승격을 자동 승인하지 않는다.
- [수뢰둔 캠페인 r11](Planning/스토리/PLAN-STORY-HEX03-CAMPAIGN-001/README.md)과 [산수몽 캠페인 r1](Planning/스토리/PLAN-STORY-HEX04-CAMPAIGN-001/README.md)을 첫 괘 정본으로 열어 각각 육효 절을 통합했다. 기존 `PLAN-STORY-HEX03/04-LINE-001..006`의 12개 경로와 384효 안정 ID는 삭제·재번호화하지 않고 현행 절을 가리키는 호환 안내로 보존했다.
- 제작 대장은 `hexagram-story-production.r15`, 효 요구 대장은 `hexagram-line-planning-requirements.r7`, 탐색 트리는 `hexagram-story-tree.r3`으로 갱신했다. 수뢰둔 요구사항 6개는 문서 전체가 아니라 각 효 절의 독립 hash에 결속되고, 자동 생성 트리는 열린 괘 정본 2개·열린 효 절 12개를 직접 가리킨다. 수뢰둔의 기존 `ReadyForDevelopment` 판정은 요구사항 자체를 바꾸지 않은 채 새 정본 절에 재결속됐으며 개발은 여전히 준비된 WI 하나씩 수용한다.
- 수뢰둔 육이 r11에서 한스의 해석을 `평범한 가축 흔적은 아닌 것 같다`는 제한적 판단으로 확정하고 원인·종·수·배후는 육삼 조사로 넘겼다. 육삼 r6은 기존 이야기와 `WI-NATURE-TRACE-INVESTIGATE`, `WI-NATURE-11`, 흔적·후퇴·귀환 H1을 보존한 채 첫 빈칸인 `지금`의 정밀화를 시작한다.
- [수뢰둔 육이 r10](Planning/스토리/PLAN-STORY-HEX03-LINE-002/README.md)은 순찰 중 발견할 징후를 경작 구획의 `눌린 고랑`과 울타리 아래의 `바깥으로 이어지는 발자국`으로 확정했다. 두 징후는 별도 관찰 사실로 기록하고 같은 원인·개체·종류라고 단정하지 않으며, 육삼 조사의 입력으로만 넘긴다. 정확 Mesh·Decal·지면 변형과 Unity 배치는 후속 표현 후보 조사 전 미정이다.
- [역경 스토리 기획 r32](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)은 일반 게임 기획의 `상황 선행조건 → 참여자 선행조건 → 행동 계약 → 사후 상태`에 다섯 조건과 결과를 대응시켰다. 이안에게 행동 전부터 있는 권한·능력·보유 상태는 `나`, 직접 대상의 수락·가용·현재 상태는 `너`, 선택 뒤 실제로 수행하는 동사·경로·도구 사용·품질·완료 기록은 `이렇게`가 소유한다.
- [수뢰둔 육이 r9](Planning/스토리/PLAN-STORY-HEX03-LINE-002/README.md)은 `나`를 체류권 활성과 순찰 가능한 보행 상태, `너`를 한스의 당일 가용성과 공동 순찰 수락, `이렇게`를 H1 순환 경유·독단 추격 금지·일몰 전 공동 귀환 기록으로 분리했다. 최대 체력·특정 무기·전투 숙련은 시작 조건이 아니며, 실제 Graph/배치/Scene 조화 검증은 후속 단계로 남긴다.
- [역경 스토리 기획 r31](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)은 384효마다 대표 `지금·여기·나·너·이렇게` 카드 하나를 두도록 정리했다. `여기`는 AreaSet 범위, Graph Map 관계, 배치 맵 구도, H 의미 계층, LH 셀 준비를 분리하며 어느 하나도 다른 책임이나 Simulation 진행 권위를 대신하지 않는다.
- [수뢰둔 육이 r8](Planning/스토리/PLAN-STORY-HEX03-LINE-002/README.md)의 대표 공간을 Farm AreaSet 안의 `생활주택 → 수리 울타리 → 경작 구획 바깥 → 숲 경계 진입점 → 생활주택` H1 순환으로 확정했다. 현행 Graph Map의 육이 결속에는 생활주택→울타리만 있어 후속 재결속이 필요하고, 배치 프로필 revision 5의 정확 거리·통행 폭·시각 승인은 미정이다. 기존 H2/H3 후보는 삭제하지 않았지만 육이 관문으로 요구하지 않는다.
- [역경 스토리 기획 r30](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)과 [수뢰둔 육이 r7](Planning/스토리/PLAN-STORY-HEX03-LINE-002/README.md)에서 `지금`의 시간 권위와 하늘 판독을 분리했다. 육이는 초구 다음 날 늦은 오후에 시작해 일몰 전 귀환하는 것으로 두고, `NatureCycleClock`·WorldTick·절기/계절이 시간을 판정하며 기존 `월드시간대Presenter`와 `SkyEnginePresenter`가 태양 고도·빛·그림자·하늘색으로 보여 준다. 흐림·비·실내에서는 시간대·일몰 임박·예상 귀환 여유를 보조 표시한다. Sky는 H나 진행 권위가 아니며 별도 Blender 모델은 요구하지 않는다.
- [역경 스토리 기획 r29](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)은 당분간 한 괘의 효를 정식 순서대로 진행하고, 현재 효를 `직전 결과 → 지금 → 여기 → 나 → 너 → 이렇게 → 결과 → 다음 효 조건`으로 완결한 뒤에만 다음 효를 정밀화한다. 자유 생활은 효 사이에 유지하지만 효 건너뛰기나 순서 재배열은 별도 승인 전 기본값이 아니다.
- [역경 스토리 기획 r28](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)에서 공간 영향이 있는 효의 `지금·여기·나·너·이렇게 → 결과·다음 선택`을 모델링·배치 표현 요구 카드로 내리도록 했다. 필요한 전·중·후 상태, AreaSet·H·배치 인스턴스, 주체·대상 모델, 상호작용 접점·접촉점, 완료 외형과 다음 동선을 같은 기획 판본으로 추적한다.
- 기존 Synty Prefab·Blender `.blend`·PNG·H 안정 ID·배치 시안은 삭제하거나 일괄 개명하지 않고 `ExistingCandidate / LegacyCandidate` 이력으로 보존한다. 새 Blender 파생형은 기존 후보로 상태 변화가 읽히지 않을 때만 원천 GUID·파생 이유·판본을 연결한다.
- 첫 표본은 [수뢰둔 초구 r11](Planning/스토리/PLAN-STORY-HEX03-LINE-001/README.md)의 울타리 손상→수리 완료다. 기존 `h1-stock:farm-fence-edge`, Synty 목재 울타리 후보 3종, 세 구간 Graph/배치 맵과 fallback Presenter를 재사용하고 실제 외형·Scale/Pivot/Bounds·접지·통행 검증 결손도 그대로 드러냈다. 손상 기본값은 가운데 구간 82도 기울어짐과 윗 가로재 한쪽 파손·처짐, 수리 결과는 90도 재정렬과 교체 목재·보강 흔적으로 확정했다.
- 초구의 마지막은 한스가 수리된 가로재를 손으로 눌러 확인한 뒤 실제 행동 기록에 근거해 첫 신뢰와 체류권을 허락하는 장면으로 닫았다. 해당 동작은 `InspectRepairedFence` E6 애니메이션 요구로만 기록했으며 Animation Event가 수리·신뢰 상태를 만들지 않는다. 다음 선형 문답은 수뢰둔 육이의 `지금` 조건이다.
- [관찰형 실사 고도화 r2](Planning/표현/PLAN-VISUAL-SYNTY-REFINEMENT-001/observational-realism.r2.md)를 후속 판본으로 추가했다. Synty는 삭제·교체 대상이 아니라 형태·규격·배치 원형이며, 선택 자산만 실제 구조·상태 원인·PBR 재질·마모·접지·관찰 거리 단서를 따라 Blender 파생형으로 검토한다. 기존 r1과 해시 결속 문서는 수정하지 않았다.
- Graph Map은 의미 관계, 배치 맵은 특정 AreaSet의 상대 배치, Blender는 외형 후보, Presentation E4는 정적 후보 동결, E5는 실제 Unity 결속, E6는 Rig·Animation·전이를 각각 소유한다. 이번 변경은 기획·템플릿·표현 절차 문서 보완이며 모델 제작·Scene 변경·E 승격·Game View·commit·push는 수행하지 않았다.

## 수뢰둔 육효 진행 관문 정밀화 (2026-09-08)

- [수뢰둔 캠페인 r8](Planning/스토리/PLAN-STORY-HEX03-CAMPAIGN-001/README.md)과 육효 문서에 흩어진 완료 조건을 `지금·여기·나·너·이렇게 → 결과·다음 효`의 선형 흐름으로 정리했다. `이렇게`에는 행동 종류·유효 횟수·품질·상황 난도·실제 결과를 두고 `StoryRequired / ReadinessRecommended / OptionalMastery`를 분리해 다음 효의 최소·예상·숙련 성장 구간을 계산할 자리를 마련했다. 정확 경험치·숙련 곡선은 미정이다.
- 공간 발동은 AreaSet과 눈에 보이는 H1을 함께 충족하는 이중 결속으로 확정했다. 수뢰둔→산수몽은 한스의 소개 수락 뒤 `area-set:sim:pyeongchang:town-market.v1`과 경비 초소 입구 H1에 실제 도착해야 한다. 경비 초소 H1은 요구 코드 `TownGuardPostEntry`만 정했으며 기존 후보 대조 전 안정 ID·Prefab은 미정이다.
- 새 관문을 현행 코드·Graph Map·배치 맵·Unity Collider에 재결속하지 않았으므로 관련 기획 상태는 `ImplementationRebindRequired`다. 코드·Unity·E 단계·Game View·commit·push는 이번 보완에서 변경하지 않았다.

## 프로필5 Actual E5 AreaSet 운영지도 (2026-09-08)

- 프로필5 휴대폰에 `운영지도`를 추가했다. 별도 지도 원장이나 새 Scene을 만들지 않고 실제 `Network.AreaSets`의 Nature·Farm·Hub·Town만 표시하며, 선택 시 기존 스트리밍 세션으로 AreaSet을 활성화하고 `DioramaTopDownCameraRig`의 관찰 초점만 runtime Root로 옮긴다.
- `현재 생활 현장으로 복귀`와 선택 실패 복원을 구현했다. 카메라 전환은 Player·NPC 이동, Simulation Command·상태, Tick과 업무 권위를 변경하지 않으며 기존 숫자 1~4 AreaSet 전환도 유지한다.
- 공용 Unity 패키지 빌드와 실제 Unity 컴파일은 오류 없이 통과했고, Diorama EditMode 7/7·휴대폰 EditMode 8/8이 통과했다. Actual E5 Network 시험은 4개 중 2개가 Unity 6000.5.6f1 내부 `Access version should be odd when acquiring lock` Assert로 실패했으며 기존 Network 시험에서도 같은 증상이 재현됐다.
- 프로필5 Play 진입 뒤 같은 Assert 반복으로 Pipeline이 무응답이 되어 Town→Farm→Hub→Nature→Town, 5분 자율 진행, Game View·Console 0건은 검증하지 못했다. 진입 전 `SimulationWorldShell`은 dirty=false였고 Scene 저장·강제 종료·E 승격·commit·push는 하지 않았다.

## 산수몽 호송 지리 학습·다음 효 진입 관문 (2026-09-08)

- [산수몽 육삼 r8](Planning/스토리/PLAN-STORY-HEX04-LINE-003/README.md)에 알렉스의 출발 전 설명과 실제 호송 통과를 분리했다. 설명은 `KnownOutline`, 기존 L2 발견 원장을 통한 통과는 `Confirmed`이며, 한스 농장→북쪽 숲길→타운 외곽→허브와 전 구간 호송로를 안정 공간 ID로 제공한다.
- Simulation 탐색 사본에 호환 필드 `MapKnowledgeEntries`를 추가했다. 새 별도 권위 원장이나 중복 명령을 만들지 않고 기존 캠페인 단계·L2 발견 사건에서 결정적으로 투영하므로 동일 이동 명령 재시도와 Save/Replay에서 같은 결과가 복원된다. 관련 .NET 시험 11/11 통과.
- Unity에는 전체/미니 지도 투영, `M` 열기·`Esc` 닫기, 플레이어 이동·전투 입력만 차단하고 WorldTick·NPC 진행은 유지하는 표현, 절기·괘 표시와 로컬 미니맵 위치·크기·숨김 설정, 서버 읽기 Adapter·구성 Root·공식 Scene용 Builder를 추가했다. 연결된 Editor 재컴파일은 오류 없이 완료했다.
- 공식 Scene에는 Builder를 아직 실행하지 않았고 실제 서버 연결·Play Mode·Game View·입력 체감은 미검증이다. 열린 Editor에서 EditMode 실행을 요청한 뒤 Pipeline이 `Access version should be odd when acquiring lock`을 반복하며 응답 불가가 되어 시험 결과를 확정하지 않았고, 해당 프로세스를 임의 종료하지 않았다. 공개 공공데이터 지도는 변경하지 않았으며 E 승격·commit·push는 하지 않았다.
- 다음 효 진행은 `이전 효·세계 상태 관문`과 `공간 발동 접점`으로 분리했다. 육삼→육사는 호송 사건 해결 뒤 타운 AreaSet 앞 진입 영역에 실제 도착해야 열리며, 마차 손상·지연·부상은 원칙적으로 진입 차단보다 육사 시작 장면의 변형으로 넘긴다. 정확한 타운 입구 안정 ID와 H 결속, Collider·Game View 검증은 미정이며 이번 보완에서 코드·Unity·E 단계는 변경하지 않았다.

## KOSIS 지역 인구·생활경제 원장·공개 조회 준비 (2026-09-07)

- 기존 공공데이터 원문 사본→정규화 원장 흐름에 기본 비활성·키 필수 `kosis-regional-statistics`를 추가하고, 시도·시군구 등록인구·세대·연령대·사업체·종사자·고용률을 기존 법정동 지역 ID에 유일 대응할 때만 저장하도록 구현했다. 공개 경로는 `GET /api/v1/community/world-map/regional-statistics`이며 읽기 전용이다.
- 신규 집중10/10, 관련 회귀56/56, 범위 Fast와 v3.5 전체 빌드 통과. Task 전체 시험은 4,886/4,893이며 범위 밖 기존 route/UI/Controller metadata 7건이 실패했다. 결측값0치환 금지, 서울/중랑구·부산/해운대구 대응, 필터·공개 필드와 키 누락 조기 차단을 확인했다. 현재 KOSIS 키가 없어 실제 공급자 호출은 미실행이다.
- 로컬 Docker MySQL 8.4 healthy는 확인했지만 기존 볼륨 계정과 compose 개발 기본값이 달라 migration/fixture 적재·재조회가 인증 차단됐다. 비밀 추출·컨테이너 재생성은 하지 않았다. [구현·검증·남은 경계](../Reports/KOSIS-지역인구생활경제-원장Projection-2026-09-07.md). 커밋·푸시·Unity 실행 없음.

## 기존 기획 인지형 역경 64괘 스토리 정밀화 (2026-09-07)

- [순차 학습 기준 r2](../Architecture/역경64괘효사순차학습기획.md)와 [스토리 기획 r18](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/README.md)에 따라 기존 기획을 먼저 대조하고, 이미 다룬 범위를 반복하지 않은 채 미완성 괘를 정식 순서로 정밀화한다.
- 조사 결과 중천건·중지곤은 육효 서막 씨앗, 수뢰둔은 육효 `StoryApproved`, 산수몽은 승인 2·진행 1·씨앗 3의 기존 문서가 있다. 수천수는 캠페인 정체성과 큰 줄기만 있으며 육효 여섯 개가 모두 `Unmapped`다. 따라서 현재 문답 위치를 `제5괘 수천수 초구`로 옮겼다.
- 건너뜀은 기존 기획 삭제나 미완성 범위의 완료 소급이 아니다. 실제 플레이 직선 진행, WI·H·Graph Map 자동 생성, 코드·Unity·저장·E 변경이나 개발 인계는 수행하지 않았다. 커밋·푸시하지 않았다.

## ChatGPT 기획 인계 최소 묶음 (2026-09-07)

- 최근 현행화 결과를 [독립형 인계 문서1개](Handoffs/Mirror-ChatGPT-기획인계-2026-09-07.md)로 압축했다. 선택 첨부는 기존 대표 화면1개다. 목적·모듈·프로필 구분·자동/실제 증거·미완료·후속 질문용 요청문과 필요시 요청할 원문 경로를 담았다.
- 기준 문서나 기존 파일을 삭제/축약하지 않았으며 요약은 날짜가 고정된 읽기용 사본이다. 실제 ChatGPT 전송·다른 스레드 인계·코드 변경·commit/push는 하지 않았다.

## 자율 음식 배달 반복 관찰 — 현행화 r6 (2026-09-07)

- [공통 목적 r12](게임상위목적-오행순환과광복기-기획-2026-09-02.md)에서 첫 자율 음식 배달 반복과 현행화 직접 구현을 승인했다. [범위·결과 r6](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/autonomous-food-world.r6.md): 기존 생활 엔진을 재사용하고 주민별 음식 수령 횟수·현재 단계·다음 주문/마트 대기·정책/마감 이유를 휴대폰에 추가했다. 새 엔진·원장·무한 재생·자동 시작·스토리는 추가하지 않았다.
- .NET 생활10/10(신규300Tick 반복·인과 포함), 조회/입력24/24, Unity 컴파일 오류 없음·휴대폰7/7 통과. Fast/Task 통과: Simulation1847/1847·Unity 공용744/744·두 솔루션 build·지도 검사 완료.
- 격리 실제 관찰309.758초에서 Tick249/Revision529, 두 주민 각2회 음식수령(총4건), 마트2건·보충1건 확인. 재생1회 뒤 업무 개입 없음. 정지 호출 때문에300초 상한을9.758초 초과했으며 실제5분 이내로 주장하지 않는다. Playing/Busy/SaveBlocked=false, 최근/최대 저장1942.7/2287.2ms로 저장 성능은 남아 있다. 원본 hash 불변·profile5/saveRoot null/Play off/Scene clean 복원. [대표 화면](../assets/changes/2026-09-07-autonomous-food/two-receptions.png).
- 변경은 공용 Presenter·.NET 시험2개·Unity 휴대폰View·기획 문서다. 서버/운영·기존 표본1800Tick/마감/정지/저장 경계·맵·기존 이야기·WI hash·Goal/E는 보존한다. 개발 스레드에 구현을 인계하지 않았고 commit/push 없음.

## 편집 Game View·Play 공간 일치 — r5 (2026-09-07)

- 사용자 요청에 따라 현행화가 [편집 미리보기 r5](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-edit-preview.r5.md)를 구현했다. 기존 Play 전용 생성 때문에 편집 Game View가 다른 Main Camera를 보여주던 구조에, 같은 Build/공간 자료·명암·구도를 쓰는 Editor 전용 비저장 미리보기를 추가했다. 프로필5/6·canonical Scene에서만 표시하며 Play 진입·프로필/Scene 변경·reload 때 정리한다.
- 변경: Unity 신규 Bootstrap/Editor/시험3개와 meta, Hongdal 작업 명세·목차·현재 상태·대표 화면2개. Runtime 설치·조회·Tick·저장·API 호출 없이 공간만 표시하고 동적 주체/물품·휴대폰 조작은 Play에서 다룬다. 컴파일 오류 없음·신규 시험2/2 통과.
- 실제 프로필5 편집/정지 Play에서 카메라 위치·회전·크기·726×470 관찰 영역과 바닥90×58 일치. Play 진입 시 preview0/runtime1, 종료 후 preview1 복귀 확인. 복사 슬롯 Tick249/Revision529·원본/복사 hash 불변. profile5/saveRoot null/Play off/Scene clean 복원, Game View 배율1.1→1.0 유지. 프로필6 실제 화면·동적 재생·Player 빌드는 미검증이다. Console 오류0/경고13이며 기존 경고 해소를 주장하지 않는다.
- 이전 광역 diff 검사에서는 기존 Scene/slnx 공백 경고가 있었으며 수정하지 않았다. 이번 소스3개는 범위 제한 diff/공백 검사 통과. commit/push 없음.

## 기본 입체 도형 — 현행화 직접 구현 r4 (2026-09-07)

- [공통 목적 r10](게임상위목적-오행순환과광복기-기획-2026-09-02.md)에서 위쪽 대각선 구도와 이번 표현 구현의 현행화 직접 담당을 승인했다. 기존 개발 읽기 조사와 별개이며 관련 경로 점유 없음 회신을 받았다. 정확 범위·검증·결과는 [입체 도형 작업 명세 r4](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-3d-work.r4.md)가 소유한다.
- Unity 기존 코드5경로와 신규 shader/meta: Cube 면별 고정 명암, 대각선 카메라·이름표, 로컬 동네의 휴대폰/화면비 전체 맞춤을 적용했다. 기존 지도 크기·시설/NPC 좌표·조리/배차/이동 상태·열린 시설 내부·서버/저장은 보존한다. Synty/Blender·마법 통신 UI는 추가하지 않았다.
- Unity CLI→Pipeline 컴파일 오류 없음·업무배달46/46·로컬 휴대폰7/7 통과, Scene clean 확인. 코드·시험 검증 완료 / Play Mode·Game View·실제 이동·Player 빌드 미검증. 실제 화면의 입체감과 글자 가독성은 후속 확인이 필요하다. 새 주문·서버 실행·슬롯 저장·E 승격·commit/push 없음.

## 기획실·개발 분리 — 이관 현황 읽기 조사 수용 (2026-09-07)

- 사용자 승인에 따라 이 `현행화` 스레드(`01a06f87-43c4-7150-8138-3368d9744af5`)는 기획 문답·우선순위·결과 검토를 맡고, 기존 `개발` 스레드(`01a02198-8b2a-7491-ac93-366b30ff474c`)에 첫 읽기 조사를 전달했다. 개발의 명시적 수용 응답을 받았으며 결과는 아직 대기 중이다. 반환 대상은 별도의 `기획` 스레드가 아니라 이 현행화 스레드다.
- 조사 범위: 주문·음식점 수락/조리·배차·음식/화물 배달·창고 입출고의 운영 서버/Simulation Core/Unity 표현별 기존 구현·재사용·미연결·미검증을 근거로 정리하고, 음식 배달 흐름과 보완 후보3개/추천1개·다음 기획 질문을 반환한다. 현재 로컬 생활과 서버 관찰 프로필6은 별도 실행 경로이며 맵 확장 배경을 업무 연결 증거로 보지 않는다.
- 이번 인계는 조사만 승인한다. 코드·설정·추적 문서·DB/API 쓰기, 서버/Unity 실행, 기존 중지 작업 재개, 신규 위임, Goal/E 변경, commit/push는 제외한다. 필요시 Git 제외 로컬 조사 산출물만 허용한다. 결과 검토 후 구현 범위/완료 조건을 별도로 정한다. 역할별 인계 기준은 [Goal 운영 체계](../Architecture/CodexPlayableLoopGoal운영체계.md#설계실제작실-인계)를 따른다.

## 동네 관찰 맵 유지·확장 — r3 구현·화면 검증 완료 (2026-09-07)

- 현행 [승인 기획](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-map.r3.md)·[표현 작업 명세](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-map-work.r3.md)·[결과](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-map-result.r3.md). 기존 서버 프로필6의58×28 배경 때문에 동네가 좁아진 문제를 확인했고 기존90×58 범위까지 포함하는120×96 공간 기준선을 Accepted로 결속했다.
- 변경: Unity의 배경 자료/조립·카메라 계산·Binding·미연동 안내·집중 시험5개 코드 경로와 신규 meta. 도로·배경 주택14동/음식점4동/마트·창고2곳을 추가하고 휴대폰 화면비 전체 맞춤, 이름표, 다른 카메라 배경 비침을 보완했다. root는 Hongdal 기획/명세/결과·시각 기록을 통합했다.
- 최종 Unity 컴파일 오류 없음·신규 EditMode18/18·기존 관찰23/23 통과. 실제1920×1080·1280×720의 열림/닫힘·NPC 선택·작은 화면 스크롤을 확인했고 [대표 화면](../assets/changes/2026-09-07-observer-neighborhood-map/04-final-map-closed-1920.png)을 보존했다. 서버 완료 주문/판본343/4주체 좌표/13이벤트 불변. Play 종료·프로필5·저장 경로 null·Scene clean·Free Aspect/1.1x 복원.
- 기존 배달 좌표·프로필1~5·Scene·슬롯·API/DB·5분 검증 정책은 보존했다. 추가 시설은 미연동 배경이며 여러 주문/NPC가 새 구역을 순회하는 것은 다음 구현 범위다. 최종 Console Error0·미해결 Warning12(미지정 스크립트/Nature, 이전 기준선 비교 없음); 기존 전체 회귀 실패/저장 지연은 r2 결과에 남아 있다. 새 주문 생성·5분 재실행·commit·push·E 자동 승격 없음.

## NPC 앱·업무 서버 관찰 — r2 (2026-09-07)

- 현행 [승인 범위](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone.r2.md)·[API 통합 명세](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-api-work.r2.md)·[구조/실행법/검증 결과](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone-result.r2.md). 기본 실제 검증을300초로 변경하되 기존 로컬30분 생활 규칙·저장 슬롯은 보존한다.
- 변경: Hongdal opt-in 검증 서버/정상 역할 API 실행기·전용 Docker/DB, 음식점 준비 완료 시 기사배정 보존, Docker 참조 누락·기존 migration 대비 HR 인덱스 불일치 보완. Unity는 프로필6·HTTP 조회·NPC 선택·역할 휴대폰·도형 표현을 별도 결속하고 LocalSimulationRuntime을 설치하지 않는다.
- 검증: 서버 집중34/34·추가 인계/Outbox10/10, Unity 집중23/23·기존8/8 통과. 최종 Fast `20260907-153208` build·집중37/37 통과. Task v3.5 build 통과, 전체4873/4880·범위 밖7실패. 실제 초기 두 실패의 운행 입력·큐 단계 연결을 수정했고 실패 주문/DB를 보존했다.
- 실제 세 번째 실행은299.4018초 제한 관찰로 수령확인/배달완료·업무 이벤트13개를 유지했다. MySQL 주문1·배차1·위치28·Outbox성공8, Mongo 음식/운송 완료·투영판본8/4 일치. 1920×1080·1280×720 역할 선택/휴대폰/명시적 시작 확인. app 단독 중지·재시작 뒤 같은 run·주문·선택·경과시간을 유지하고 Revision341→343으로 복구했다. 새 주문/NPC 자동 재개 없음.
- 기존 저장 지연은 복사 슬롯1회에서 Save3726.1ms로 남아 있다. 실제 영업·E 자동 승격·commit·push 없음. 완료/미검증의 현행 판단은 r2 결과 문서가 소유한다.
- 추가 공용 Unity 모드 회귀는3/5·2실패(Farm 모드 전환/Scene 버튼 개수). 비저장 진단에서 새 잠금false·기존 Farm 카메라 범위 누락을 확인했으며 baseline 비교 없이 기존 작업을 수정하지 않았다. 새 관찰23/23·휴대폰8/8과 전체 회귀를 구분한다. 전용 서버는 완료 결과 조회 상태로 유지, Unity는 원래 프로필5/저장 경로로 복원한다.

## 가상 동네 관찰·운영 휴대폰 — r1 보존 범위 (2026-09-07)

- 현행 [휴대폰 승인 범위](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone.r1.md)·[사용법/코드 관계/결과](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone-result.r1.md). NPC 9명의 [30분 직업 생활 기반](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/neighborhood-life-result.r1.md)은 보존하고 새 프로필만 오른쪽 휴대폰의 동네·주문·NPC·창고·정책 앱으로 관찰한다.
- 변경: Hongdal `Ssalddel.Unity/Runtime/Observation` 실행 조정/조회 모델/정책 초안과 시험. Unity Runtime Adapter·휴대폰 View·Binding·Controller/도형 View의 책임 분리·시험. Tick/정책/저장 중복 차단, 정책 적용 뒤 재생 유지, 저장 실패 정지/수동 회복. 기존 슬롯/schema/r1~r4/Scene 불변. 관련 없는 dirty 변경 보존.
- 검증: 집중 .NET 20/20, 기존 생활/주문/마트/배달 회귀 35/35, Fast `20260907-130227`, Task `20260907-125935` Unity 솔루션 build·전체 740/740·코드/E 지도 검사 통과. Unity 컴파일 오류 0·EditMode 8/8(UI 7, 실제 Runtime 저장 1). 기존 23개 WI 작업 명세에 공통 표현 영향 문서를 결속했고 E0/기획 hash/승격 비활성 유지.
- 실제 1920×1080·1280×720에서 앱·정책·주문→NPC/시설·따라가기·닫기 확인. 저장 중 UI 정체를 보완한 최종 소스로 12:59:44.800→13:30:00.731 KST 30분15.9초 연속 관찰했다. 음식/마트/보충 수령6/6/2, 근무 NPC7명 첫 휴식·복귀 확인. 같은 슬롯 Play 재진입에서 Tick694/Revision1354·원장·재고·Actor 일치와 정지 상태 복원 확인. 내부 시각은11:34이며 1800 Tick 실시간 완주는 미달이다. 저장 검증/빈도를 낮추지 않았고 기록 증가에 따른 저장 비용 개선이 다음 우선순위다.
- 마지막/최대 저장4749.7/21404.3ms·저장698회. 별도 복사 슬롯의 가속1800 Tick 완료→실제 새 표본 취소/확인→Tick0 정지/저장 성공은 실시간 관찰과 분리했다. Escape 미검증, Editor 부가기능/검증 정리 오류2건은 결과 문서에 남겼다. Play 종료·Scene clean·원래 저장 경로/프로필 복원, Game View 배율만1.1x→1.0x 잔여.
- 실행: `NPC 직업 생활 · 30분` → Play → `휴대폰 → 동네 → 재생`. Hosted·실제 운영 서버/주문/정산 없음. Scene 저장·commit·push 없음. 아래 r2 등은 과거 구현 범위다.

## 마트·도로변 기사 대기 — r2 실행 연결 (2026-09-07)

- 최신 [작업 명세](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/waiting-fleet-work.r1.md)·[구현 결과/사용법](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/waiting-fleet-result.r1.md). 기사3명·마트자리3개/도로변2개·음식점 주문·가까운 빈 자리 복귀·복귀 중 다음 주문 예약을 r2 전용 Session/슬롯에 연결했다. 기존 단일 기사 r1은 보존한다.
- 공유 통행 묶음을 배달/복귀 동안 한 기사만 보유하는 보수적 첫 구현이다. 동시 도로 이동1대이며 구간별 병렬 통행/교착 해결로 확대하지 않는다. 주체 목록·자리 예약·대기·통행 소유자·재생 해시와 Unity 기본 도형/프로필/상태 패널을 연결했다.
- 최종 Task `20260907-085659`: 두 솔루션 build·Simulation1819/1819·Unity 라이브러리720/720·코드/E 지도 통과. Unity 컴파일·EditMode3/3 통과. 실제 Play Mode 자동 재생에서 첫 묶음 수령27/58 Tick·도로변 자리3/4 복귀·다음 묶음을 확인했다.89 Tick 저장 완료 후 재생/Editor 일시정지. `waiting-fleet-play.json`에 상태를 보존했고 docs/assets/changes/2026-09-07-synthetic-delivery/waiting-start.png 및 waiting-return.png에 실제 카메라 캡처를 보존했다. 버튼 직접 입력 시험은 아니다.
- 남음: 구간별 동시 통행, 타 모듈 UI/서버 초기화 격리, 마트에 가려지는 초기 차량 판독 개선, 실제 버튼 입력 완주·Hosted. 마트 주문은 범위 밖. E 자동 승격·commit·push 없음.

## 다중 기사 — 공통 배차·점유 판정 기반 (2026-09-07)

- 최신 작업: [작업 명세](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/dispatch-foundation-work.r1.md), [구현 결과](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/dispatch-foundation-result.r1.md). 승인된 기사3명·기존 평가 유지·도로/출입구 대기의 지원 계산을 먼저 구현했다.
- WorkflowRules의 기존 음식 픽업 평가에 명시적 소요시간 입력을 추가하고 음식배달후보선정Policy·이동자원점유Policy와 다중기사공통규칙Tests를 추가했다. 후보별 제외 사유·기존 정렬·권역 확장, 원자적 자원 묶음·대기순서·부분 점유 차단을 계산한다. 실제 배차 확정이나 자원 점유 쓰기는 하지 않는다.
- 검증: 최종 Task `20260907-084139`에서 Simulation 솔루션 build·전체1814/1814 시험·코드 지도/E 책임 지도 검사·diff 검사 통과. 신규 시험11개 포함. 최초 지도 불일치와 시험 E 표기 누락을 보완하고 생성 도구로 evidence-responsibility-code-map.md/json을 갱신했다. .NET 검증이며 신규 Unity 실행·운영 서버 소비 시험은 하지 않았다.
- 다음 우선순위: 기사3명 Session 상태와 승인 공간 사본·경로·대기 위치/자원 해제·Save/Replay 연결. 이후 Unity 후보 카드·UI/운영 초기화 분리와 실제 캡처. 기존 r1 코드/슬롯/기획 hash·일시정지 Editor를 변경하지 않았고 E 승격·commit·push 없음.

## 가상 배달 관찰 — Core·Unity 연결 구현 (2026-09-07)

- 최신 승인: 기사3명·기존 배차 판단 순서 유지와 점유 도로 진입 대기·출입구 한 명씩 사용. [공간·실행 통합 r2](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/synthetic-spatial-study.r2.md)에 r1 기준·실제 Play Mode 캡처·발견 문제·후속 점유 규칙을 결속했다. 캡처2개는 docs/assets/changes/2026-09-07-synthetic-delivery/에 원본 보존했다. 이후 같은 기획의 발견 사항도 판본·검증 수준을 유지하며 통합한다. 현재 코드는 여전히 단일 기사 r1이며 다중 기사 구현·명세 hash 재결속은 남아 있다. 이번 변경은 문서·증거 통합이다.
- 사용자의 `그냥 끝까지 구현해 줘` 범위는 [전체 연결 명세](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/complete-delivery-work.r1.md), 최신 사용법·결과는 [구현 결과](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/runtime-result.r1.md)다. 담당은 현 작업실 `01a06f87-43c4-7150-8138-3368d9744af5`. 이전 담당 미지정·공유 조리 시험 쓰기 충돌은 해소했으며 다시 승인 대기로 돌리지 않는다.
- 신규 기사·차량·주문·주문자 계약과 배정/이동/픽업/전달/수령/복귀 WI6개·각 E7 명세·Accepted 연구 hash를 결속했다. WI 대장118개, 주체14개와 Goal31개 결속 검사 통과. 한 담당이 공유 파일을 순차 수정했으며 E 자동 승격은 없다.
- 공통 Core에 두 주문 생성→기존 NPC 수락/조리→기사 한 주문 배정→차량5m/초·보행1m/초→픽업→전달→다음 Tick 수령→복귀→다음 주문 묶음을 연결했다. 기사 상태 사본·복제·해시·Tick 재생과 행위 기록을 결속했고 플레이어 성장 지급은 비활성이다. 별도 주문 원장·운영 DB는 만들지 않았다.
- Unity 저장소에 명시적 가상 배달 프로필·전용 슬롯·Controller·기본 도형·관찰 카메라·재생/정지·매 Tick 저장·저장 재시도·종료 후 새 표본 선택을 추가했다. canonical Scene 파일·기존 표본/100 Tick 설정은 보존했다. 새 표본은 기존 Session 상한 내365 Tick이며 무제한 상시 실행은 아니다.
- 신규 Core 시험6개 통과. Fast `20260907-075947/`: 관련 Simulation304/304·Unity 라이브러리10/10. 최종 Task `20260907-080133/`: Simulation1803/1803·Unity 라이브러리720/720·두 솔루션 build 통과. 관련 WI 대장 검사 통과.
- 사용자 저장·재시작 승인에 따라 열린 Scene·자산을 저장하고 Unity를 정상 종료·재시작했다. 시작 중 발견한 Controller의 `Application.isPlaying` 이름 충돌을 `UnityEngine.Application.isPlaying`으로 수정했다. Safe Mode를 거친 뒤 정상 Editor(PID29956)에서 canonical Scene을 다시 열고 최신 `Save` 메서드·카메라 layer31 분리 반영을 확인했다.
- 실제 Unity EditMode 신규 시험2/2 통과: 도형 좌표·상자 표시·사본 불변, 합성 프로필/저장 슬롯 분리. 임시 검사 객체는 제거했다. 기록: `artifacts/local/validation/synthetic-delivery-editor-tests-after-restart.json`, `synthetic-delivery-editor-after-restart.json`. 이전 0개 탐지·카메라 확인 실패는 재시작 전 증거다.
- 사용자 캡처 요청으로 canonical Scene에서 합성 프로필 Play Mode에 진입하고 화면의 재생 버튼을 실제 클릭했다. 36 Tick까지 첫 주문 픽업·주택 이동·수령 확인·복귀 진행을 관찰하고 배달 재생과 Editor를 일시정지했다. Game View 원본은 `artifacts/local/validation/delivery-play-moving.png`, `delivery-play-home.png`, UI 포함 Editor 원본은 `delivery-play-editor.png`, 최종 상태는 `delivery-play-runtime-final.json`이다. 카메라 캡처에는 OnGUI 패널이 포함되지 않으므로 UI 증거는 Editor 원본을 사용한다.
- 실제 화면에서 상태 패널 잘림·다른 모듈 UI 혼입 및 Hub 입고/Nature 탐색/일일 작업의 서버 연결 오류를 발견했다(`delivery-play-console.json`). 이번 요청은 확인·캡처이며 이 문제를 수정하지 않았다. 첫 주문 관찰은 전체365 Tick 완주·두 주문 수령·저장 재진입·Hosted 성공이나 E7 승격을 의미하지 않는다. 동적 GIS·다중 기사·정산·우천은 범위 밖. commit·push 없음.

## 관찰 세계 현재 상태 — 면목동 원본 계보 저장·속성 검사 (2026-09-07)

- 최신 범위: [면목동 실제 지도 관찰 r1](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/README.md), [구현 명세](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/implementation.md), [원본 확보·등록·남은 관문](Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/source-acquisition.md). 사용자 승인에 따라 `hongdal-mysql-1 / hongdal_dev`에 서울 건물 ZIP·정의서의 원본 계보 2건을 저장했다. 파일 바이트는 Git 제외 `artifacts/local/neighborhood-source-acquisition/`에 보존하고 DB에는 출처·판본·해시·크기·비공개 경로를 기록한다.
- 이번 변경: `동네공간SourceRegistration`에서 기존 건물 마스터 ID를 보존하고 실제 AL_D010·필드 정의서 ID를 분리(총 5개 정의), 관련 시험 보완. `eng/Ssalddel.NeighborhoodSourceImport/`에 지정 파일·해시·컨테이너·DB를 대조하는 제한된 원본 등록 도구 추가. 기존 `RegisterFileAsync`를 재사용하며 migration·전체 건물 정규화·분류·시각 계획은 실행하지 않는다. [도구 안내](../../eng/neighborhood/README.md), 구현 명세·확보 기록·목차를 갱신했다.
- 실제 DB 검증: 새 원본 ID 10·11, 수집 실행 ID 15·16, `Partial / NeighborhoodSourceReviewPending`. 사전 0건 → 등록 2건 → 재입력 추가 0건·기존 2건·ID 유지. 별도 연결의 재조회와 독립 MySQL 조회 통과. 정규화·건축물 원장 연결 0건. 기존 VWorld 원본 3건과 서버의 `ssalddel_dev` 연결 설정은 변경하지 않았다.
- 속성 검사: 서울 DBF 695,761개를 끝까지 읽었고 CP949 후보 해독의 면목동 속성은 13,389건이다. 높이 0은 8,628건, 숫자 형식 오류 1건으로 후속 품질 처리가 필요하다. 공급처 문자 인코딩은 미확정이며 500m 경계 추출·SHP 개별 형상 검사·실제 높이 검증은 아니다. 행 단위 주소·도형을 새 원장이나 Unity에 적재하지 않았다.
- 이번 검증: 원본·승인 기획 SHA-256 재대조, 가져오기 도구 build, 관련 출처/수집 기반 시험 19/19, 실제 DB 등록·재조회·중복 방지 통과. 잘못된 명령(64)·누락 입력(1)의 쓰기 전 차단, 로컬 문서 링크 127개 확인. Fast `20260907-053313/`는 build·관련 시험 90/90 통과. Task `20260907-053436/`는 0.0 솔루션 build 통과, 서버 전체 4859/4866 통과·기존과 동일한 7개 실패로 전체 실패다. 첫 확인 오류는 `RoleAppControllers_HaveAudienceAndBusinessCapability`의 한국어 업무 영역 metadata 누락이며 해당 공유 API·웹 파일은 수정하지 않았다. 세부 로그 `artifacts/local/validation/neighborhood-source-registration/`.
- 이전 기반: 불변 공간 사본·전처리 JSON 검사·결정적 경로 후보·`Ssalddel.NeighborhoodPreflight`는 유지했다. 합성 지도 시험 42/42, Simulation 전체 1765/1765, 차량 200m·도보 5m·단절/차량 출입구 차단 확인은 2026-09-06 증거다. 당시 Task는 웹/서버 4858/4865 통과·범위 밖 7개 실패였다. 실제 면목동이나 NPC/Unity 연결 증거로 사용하지 않는다.
- 현재 차단: AL_D010 전용 필드·SHP 변환 미연결, 높이·문자 인코딩 품질 검토, 제공처 CC BY 표시/상세 CC BY-NC-ND 링크 불일치. 도로·출입구 미확보. 로컬 DB 저장 승인은 서버 조회 연결 변경이나 게임 적용 허가가 아니다.
- 다음 관문: 권리·필드/높이 처리 확인 → 도로·출입구 확보와 검토된 500m 구역 전처리 → 공간/접근 연구 승인 → 단일 WI의 배차·도달·픽업·수령·복귀·저장/재생·앱 실행 시간 결속 → Unity 표현. [개인 작업 허용 시간 정책](Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/implementation.md)의 `ExecutionWindows` 실행기 결속도 미완료다.
- 승인 기획 hash와 기존 다른 변경·공유 파일 소유권을 보존했다. 신규 WI/Goal 활성화·E 승격·자동 NPC 실행·Unity/Blender/Play Mode/Game View·서버 API/실제 운영 연결·commit·push는 하지 않았다.

## 설계실·제작실 — 인계 요약·기준선 점검 (2026-09-06)

- [역할과 사용법](../Architecture/CodexPlayableLoopGoal운영체계.md#설계실제작실-인계). 기존 기획·단일 WI Goal·작업 명세·개발 반환을 유지하고 AGENTS에는 진입 링크만 추가했다. 계정별 원장·자동 실행·새 게임 승인은 만들지 않았다.
- `eng/execution-ledgers/manage-development-handoff.ps1`의 Export/Check와 `eng/common/development-handoff.ps1`, `eng/tests/development-handoff.ps1`을 추가했다. 모듈 관계·첫 작업·완료 조건 요약, 승인/연구/주체/판본·선행 작업·소유권 확인, 저장소별 실제 파일 hash 비교를 제공한다. 기존 명세 검증을 재사용하며 Check는 원본과 인계 파일을 쓰지 않는다.
- 음식점 조리 명세에는 선택적 `handoffNotes` 설명만 추가했다. 읽기 전용 표본은 `artifacts/local/development-handoffs/interaction-goal-restaurant-cooking.v1/`의 Markdown·JSON이다. 담당 미지정(`OwnerMissing`), 자동 수락 Goal과 `Ssalddel.Simulation.Tests/Simulation음식배달Tests.cs` 쓰기 범위 충돌이 남아 `Blocked`로 출력된다. 담당을 임의 배정하거나 다른 작업을 중단·정리하지 않았다.
- 검증: 인계 시험 36개 통과(기존 E7 native 승인·주체·hash·상한 회귀 검사 포함). 지정 7개 경로의 Fast/Task 통과. 공통 검증기는 문서·도구로 분류해 build/test를 생략했으며 인계 시험은 별도로 실행했다. 동일 입력 결정성·기준선 변경 탐지·Check 무변경을 확인했다.
- 게임·Unity 소스/Scene 변경, Play Mode·Game View·서버 연결 검증, 계정 조작, commit·push는 하지 않았다. 다음은 기존 개발 담당의 소유권 조율과 이후 명시적 작업 수용이다. 인계 점검 통과도 실행 승인을 대신하지 않는다.

## 관찰 운영 — Unity 구조 안내·음식점 로컬 카드 (2026-09-06)

- [현행 구현과 검증](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/restaurant-processing.md). 승인 r3의 NPC 자동 수락→음식점별 FIFO 조리 자리 배정→픽업 대기를 유지한다. 설정 변경·자리 축소·자동 처리 중지는 이미 배정된 작업 일정을 바꾸지 않는다.
- 새 세션은 합산 Tick을 내부에서 한 단계씩 진행한다. 새 저장의 `TickRuleRevision=world-tick-step.r1`를 hash·복제·재생에 포함하며, 필드 없는 구형 저장은 합산 진행과 기존 턴 마감을 유지한다. 새 턴 마감만 음식점 수락→진행 중 작업→새 자리 배정 순서를 일반 Tick과 공유한다.
- [Unity 구조 안내](../Architecture/UnityClientLayeredArchitecture.md)를 웹·MAUI 대응, 두 저장소, 실제 음식점 호출 경로와 사용법으로 정리했다. README는 짧은 링크만 추가했다. Bootstrap의 주입, 공통 Core의 권위, Presenter의 표시 해석, Unity View를 분리하며 오래된 HTTP 소스 미확인 설명을 교정했다.
- [승인된 관찰 UI 범위 r1](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/restaurant-observer-ui.r1.md)에 따라 `음식점관찰표본`과 읽기 전용 주문 표시 행을 추가하고 별도 Unity의 `음식점관찰AutoBootstrap`·`SceneController`·`View`·프로필 메뉴를 연결했다. 자리·시간·새 조리 배정, 명시적 주문 추가·1 Tick·재조회·저장 버튼이다. 정책 누락·중복·조회 실패·판본 충돌에서 편집을 차단/재조회하며 기존 문자열 속성은 보존한다.
- 음식점 프로필은 공식 `SimulationWorldShell`의 전용 슬롯 `restaurant-observer-r1-primary`만 사용한다. 메뉴는 Play를 자동 시작하지 않는다. 기존 자연 생존·턴 마감·물류·전투·공간 스트리밍 초기화는 음식점 프로필에서만 건너뛴다. Runtime/Session 하나를 주입하며 기본 프로필·저장·Scene/자산 파일은 바꾸지 않는다. 자동 시간·오프라인 보충·재접속 주문 생성은 없다.
- 음식점 [수락 명세](../../eng/execution-ledgers/work-orders/restaurant-auto-accept.e7-work-order.json)와 [조리 명세](../../eng/execution-ledgers/work-orders/restaurant-cooking.e7-work-order.json)를 E7 v2 양식으로 정리했다. 검증기는 가짜 Loop 없이 주체 기반 Goal·WI·Ready 주체·승인 판본/hash·등록 명세·전달 상한을 검사한다. 명세 단계는 E0, 자동 승격은 계속 비활성이다.
- [공통 검증 복구](../Reports/공통검증복구-2026-09-06.md): 검토 책임 누락 14곳을 실제 역할·하위 모듈·미검증 경계로 분류했다. WI 112개와 이름 구성 대장의 ID 집합, 한스 2개 WI의 성장 기여 없음, 공간 모판 32개 WI의 구성 유지를 검증한다. 모판 r16은 WI 원장 r47을 참조하며 정의·배치는 그대로다. 검증 제외나 E 승격은 하지 않았다.
- 최신 검증: 음식점 Core 29/29·카드 8/8, 전체 Simulation 1713/1713·공통 Unity 라이브러리 720/720, Fast 통과. E 책임 지도는 797 후보·794 지정·3 사유 있는 제외·미분류 0이며 음식점 두 명세 검증 및 r3 hash 불변을 확인했다. Task는 코드 지도·세 솔루션 build·위 두 전체 시험을 통과했지만 범위 밖 웹/API 시험 7건에서 실패했다. 첫 오류는 `주거공동체World관점Controller`의 한국어 업무 영역 메타데이터 누락이다. 해당 파일은 수정하지 않았다.
- Unity 전용 Bootstrap와 새 EditMode 시험 소스는 실제 Unity 참조로 C# 컴파일 통과했다(기존 경고 존재). Pipeline 재컴파일·시험 응답 지연으로 EditMode 실행 결과는 아직 확정하지 않았다. CLI batch도 열린 Editor의 프로젝트 잠금으로 중단됐다. 코드·컴파일을 실제 화면 완주로 보고하지 않는다. [변경·검증 기록](../Changes/2026-09-06-restaurant-observer.md), 로그 `artifacts/local/validation/restaurant-observer/`, Task `20260906-174214/`, Fast `20260906-174425/`; Unity 로그 `artifacts/restaurant-observer/`.
- 별도 잔여: 기존 Loop 명세 8개 중 열원·개인 계획 2개는 논리 E4, 이들을 포함한 6-WI 묶음은 E5여서 전용 작업 명세 검증이 실패한다. 담당 증거·명세와 묶음의 재결속이 필요하며 묶음 E5를 복사하거나 검사 조건을 완화하지 않았다.
- 후속: 카드 실제 입력/화면 확인과 원격 어댑터·자동 관찰 진행은 별도 검증·작업이다. 도로 거리 기반 기사 배정은 해당 WI의 승인·주체·명세를 먼저 확인한다. Play Mode·Game View·Hosted·기사 배정/이동·운영 DB·식재료 소비는 미검증 또는 미구현이다. 현재 E0·승격 비활성, 커밋·푸시 없음.

## 다음 우선순위 — 독립 창고 출고 관찰 연결 (2026-09-06)

- `창고출고관찰표본`으로 보관 완료 모의 재고 300 KGM의 출고 요청→피킹→포장·출고 대기를 연결했다. 기존 WI-HUB-03/04/05와 NPC 루틴을 사용하며 첫 입고 표본은 보존했다. 입고 표본에 없던 피킹 공간만 기존 정의에서 추가하고 영역 간 운송은 포함하지 않는다.
- 정책 카드에 명시적 출고 표시 옵션과 한국어 출고 상태를 추가했다. 기본 입고 화면은 바꾸지 않으며 출고 화면에서만 설정을 선택한다. 초기 자동 처리 해제 시 대기, 재개 후 한 번 실행, 진행 중 해제는 기존 작업을 취소하지 않는 규칙과 Save/Replay를 검증했다.
- 관련 Simulation/LocalRuntime 시험 24/24, .NET Unity 라이브러리 712/712 통과. 로그 `artifacts/local/validation/warehouse-outbound-observer/`. 초기 피킹 공간 결손으로 신규 두 시험이 실패한 뒤 표본 결속을 수정했다.
- 문서 링크 2곳 누락 없음, diff 공백 검사 통과. 공통 Fast·Task는 기존 코드 지도 불일치로 중단됐다(`artifacts/local/validation/20260906-132257/code-map-check.log`).
- Blender/Synty 시각 고도화는 [후속 E4 후보 방향](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/warehouse-policy-cards.md)에 기록만 했다. 실제 자산 가공·Unity UI/Scene·Play Mode·Game View·E 승격·커밋·푸시는 하지 않았다. 다중 창고 배분 엔진의 세션 연결과 실제 출고 차량/운송은 이번 완료 범위가 아니다.

## 첫 창고 관찰 표본·정책 카드 연결 (2026-09-06)

- [구현·E4 후보 인계](Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/warehouse-policy-cards.md). 기존 Hub R2 루틴으로 입고 300 KGM을 WI-001 검수→WI-002 적치하며 출고 정책은 제외한 `창고입고관찰표본`을 추가했다. Farm/City 업무를 선행 실행하지 않는다.
- `ISimulationNpcPolicyRuntime`, LocalRuntime 구현과 Unity 라이브러리 `창고정책카드Presenter`를 연결했다. 초안/적용을 분리하고 기존 정책 명령 후 재조회한다. 개정 충돌·실패는 자동 덮어쓰기하지 않으며 재조회 실패 시 낡은 상태를 성공으로 표시하지 않는다. 기존 정책·저장·서버 API의 의미는 변경하지 않았다.
- 집중 시험: 창고/카드 연결 11/11, Presenter 2/2 통과. 무명령 자동 검수·적치, 카드 자동 처리 차단/재개, 수량 보존, 개정 충돌·범위 밖 우선순위, Save/Replay 및 재조회 실패를 확인했다. 로그는 `artifacts/local/validation/warehouse-policy-cards/`.
- 확장 검증: LocalRuntime·입고 UI 연결·공간 포함 Simulation 31/31, .NET Unity 라이브러리 전체 712/712 통과. 문서 링크 2곳 누락 없음, diff 공백 검사 통과. 공통 Fast·Task는 기존 코드 지도 불일치로 차단됐으며 생성물을 일괄 갱신하지 않았다.
- 미완료: 실제 카드 UI·창고 Scene/Prefab·자산 후보 동결, Hosted 카드 어댑터, 상세 표시명·차단 이유 한국어 해석. .NET 시험이며 Unity Editor·Play Mode·Game View는 미실행이다. 전체 E4/E5 승격·커밋·푸시는 하지 않았다.

## 관찰 업무 재사용 — 공통 계산 규칙 1차 이관, 세션 연결 미완료 (2026-09-06)

- [재사용 조사·구현 관문](Planning/공간/PLAN-SPATIAL-FOOD-DELIVERY/workflow-reuse-audit.md)에 기존 Hub WI-001/002·WI-HUB-03/04/05, 운영 음식 주문·배차 후보와 Simulation 자동 생애주기의 차이를 기록했다. 기존 공간 기획의 표현 지원 승인을 새 주문·배차 권위 변경 승인으로 확대하지 않았다.
- 변경: 기존 회귀 6건에 이어 `창고출고배분Policy`, `음식배달픽업평가Policy`를 공통 WorkflowRules로 추출했다. 운영 `OutboundBatchEngine`과 `음식배달배차업무정책`이 이를 실제 호출하며 기존 순수 계산 중복을 제거했다. 저장 형식·API 변경은 없다.
- 이번 검증: 공통 규칙/음식배달 Simulation 시험 20/20, 운영 창고/음식배달 정책 시험 15/15 통과. 이관 전 창고 계산을 동결한 시험 전용 기준과 모의 입력 150개에서 결과 전체가 일치한다. 로그는 `artifacts/local/validation/observer-engine-transfer/`. Simulation 세션의 실제 후보 조회·배정 연결 증거는 아니다.
- 검증: Simulation 관련 52/52, 운영 음식점·배차 정책 단위 시험 15/15 통과. Simulation TRX는 `artifacts/local/validation/observer-workflow-baseline/simulation-baseline.trx`. 새 문서 링크 2곳 누락 없음, 대상 diff 공백 검사 통과. 실제 서버·DB·Unity Editor·Game View는 미검증이다.
- 공통 Fast·Task는 기존 Simulation/Unity 코드 지도와 현재 메타데이터 불일치에서 중단됐다. 로그는 `artifacts/local/validation/20260906-123114/code-map-check.log`부터 확인한다. 다른 변경이 섞인 생성물을 일괄 재생성하지 않았다.
- 남은 작업: 사용자 선택은 기존 엔진 이관 우선이며 식재료 모델은 제외한다. 식재료 반환 질문은 선행 차단으로 사용하지 않는다. 새 고객 접수/음식점 수락·가상 배정 WI의 준비 주체와 revision/hash·작업 명세 결속, 모의 후보 공급과 세션 호출, 새 관찰 프로필·생성기·기사/차량 점유·경로 차단·복귀 및 전체 E4는 미완료다. Goal/E 승격·Unity 실행·커밋·푸시는 하지 않았다.

## 음식점–주택 배달 표본 — 경로·관찰 코드와 E4 준비 (2026-09-06)

- [작은 아스팔트 동네 배달 r1](Planning/공간/PLAN-SPATIAL-FOOD-DELIVERY/README.md)과 [구현·후보 인계](Planning/공간/PLAN-SPATIAL-FOOD-DELIVERY/implementation.md)를 등록했다. 음식점 1·주택 2·기사/오토바이 각 1, H1/H2와 기존 경관 결속 후보·상대 위치를 기록했다.
- `Ssalddel.Unity/Presentation/음식배달관찰경로.cs`에 꺾인 도로의 길이 비례 위치 계산·역방향 경로·정차/현관 연결 검사·두 주택 표본과 기존 음식배달 상태의 읽기 전용 관찰 설명을 구현했다. 이는 엔진 독립 표현 라이브러리이며 실제 Scene·기사 이동이나 배달 상태 변경은 수행하지 않는다.
- 신규 시험 13/13, .NET Unity 라이브러리 전체 710/710, 기존 음식배달 Core 시험 6/6 통과. 공통 Fast·Task는 코드 지도 불일치로 차단됐다. Unity Editor 시험·Game View는 수행하지 않았다.
- 도로·교차로·상점·기사·주택 Prefab 5개 경로와 파일 SHA-256을 확인했다. 오토바이는 Prefab/FBX 이름 조사에서 미확보이며 종속 자산·탑승/인계 Clip·시각 채택·통행/차폭/Bounds는 미검증이다.
- **남은 작업:** 기존 Core는 시간으로 배달을 진행하고 실제 기사·차량·경로 도달·복귀를 소유하지 않는다. 해당 권위 계약과 저장/재생, 경로 차단에 따른 주문 중단을 결속한 뒤 E5 실제 배치를 수행해야 한다. 현재 경로 검사만으로 실제 주문 중단을 주장하지 않는다. 신규 WI/Goal·Graph Map·H 대장·E 승격·Unity 저장·커밋·푸시는 없다.

## 관찰 중심 개인 세계 — 정책 기반, 전체 구현 미완료 (2026-09-06)

- [승인 기획 r1](Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/README.md)과 [지원 모듈 구현 명세](Planning/시스템/PLAN-SYSTEM-OBSERVER-WORLD/implementation.md)를 등록했다. 관찰 기본·개인별 세계·미접속 분신 성장 금지·개인 NPC 누적 24시간·상한 뒤 소비 작업 대기·현실 자료 표현 우선과 P0~P4 인계 순서를 보존했다.
- `Ssalddel.Simulation.Application/관찰세계진행Policy.cs`는 서버 시각을 받아 세계/분신/개인 NPC의 허용 시간 구간과 체크포인트를 계산한다. 반복 종료·재접속·제어권 전환·역행 시각·소유자 불일치·구형 프로필 기본 비활성을 다룬다. 실제 Tick이나 DB가 아직 소비하지 않으므로 현재 게임의 오프라인 생산·성장 제한이 적용됐다고 해석하지 않는다.
- 신규 정책 시험 14/14, 기존 NPC·Save/Replay·Session 저장·공공데이터 경계를 포함한 관련 시험 48/48 통과. 문서 탐색은 검사 대상 합계 경로 참조 131개, 누락 0. 공통 Fast·Task 검증은 코드 지도와 현재 메타데이터 불일치에서 중단됐다. 다른 작업의 생성 지도는 덮어쓰지 않았다.
- **미완료:** 지속 서버 실행·내구 체크포인트·접속 임대·개인/세계 효과의 실제 Tick 분리·가상 입력 생성·Unity 관찰/제어권 인계·현실 자료 표시. 기존 `AdvanceWorldState`가 학습·NPC·생존을 함께 진행하므로 타이머를 연결하면 미접속 개인 상태 불변을 보장할 수 없다. 후속은 개인 효과/자원 소유권 분류와 원자적 정산 소비를 먼저 구현한 뒤 기존 창고 WI의 승인 hash·주체·작업 명세를 재결속한다.
- 새 실행 Goal/WI 활성화·E 승격·DB 변경·서버 상시 실행·Unity/Play Mode/Game View·외부 API·커밋·푸시는 수행하지 않았다. 이번 결과는 P0와 P1 순수 정책 기반이며 계획 전체 완료가 아니다.

## README 링크 중심 안내도 (2026-09-06)

- 루트 README를 개발 중 안내와 중첩 링크 목차로 축약했다. 이야기·주체/WI·H 공간·실행/표현·E 검증·기존 업무 기반에서 상세 문서로 이동하며, 64괘·효사 기획·WI·H 색인에 직접 연결한다.
- 긴 절차·단계 해설·이미지·코드 트리는 README에서 덜어냈다. 연결 대상 문서와 이미지 파일은 삭제하지 않았으며, 기존 웹 자료는 프로젝트 화면 안내와 화면 카탈로그로 연결한다. 기획 승인·기능·실제 실행 상태는 변경하지 않았다.
- README 탐색 검사 통과(검사 대상 문서 합계 경로 참조 128개, 누락 0). 실제 화면·외부 링크 접속·커밋·푸시는 수행하지 않았다.

## 기획 표시명 정리 (2026-09-05)

- 일반 기획 목차의 표시명에서 `-001`을 생략하고 판본은 별도로 유지했다. 기존 ID는 숨김 호환 주석, 경로와 기계 참조로 보존했다.
- 신규 일반 기획의 무접미사 명명과 향후 답변 표기를 독립 관리·한국어 출력 지침에 반영했다. 효 순번·WI/H·기존 승인 본문·hash·Unity는 변경하지 않았다.
- 이번 명명 변경만 별도 커밋 대상으로 분리한다. 다른 스레드의 기존 변경과 앞선 모델 고도화 문서는 포함하지 않는다.


## 신티 원형 기반 모델 고도화 전략 (2026-09-05)

- [Blender 모델 고도화 전략 r1](Planning/표현/PLAN-VISUAL-SYNTY-REFINEMENT-001/README.md)을 `ApprovedDirection / ExecutionDeferred`로 기록했다. 선정한 신티 원형을 Blender에서 고도화·시각 검토한 후 Unity에 배치한다.
- E4 모델·상태 준비, E5 실제 배치·결속, E6 동작 검증 경계를 유지한다. 기존 한스 후보와 승인 hash는 보존했다.
- 이번 범위는 문서화만이다. 모델·Unity·코드 변경, 작업 인계·자동 실행·커밋·푸시는 하지 않았다. 첫 표본과 세부 품질·성능 예산은 미정이다.

## 기획 문맥 설명 정리 (2026-09-05)

- 사용자 요청으로 `이렇게`를 상황 적중·가장 적절한 행위로 정의한 설명을 제거했다. `지금·여기·나·너·이렇게` 틀은 유지하고 행동·수행 방법·조건·대가·대안을 기록한다.
- `PLAN-PLANNING-PLAYER-CONTEXT-001`은 r4다. README, 정본·기획 목차·전수 목록, 문답·독립관리·플레이어 중심·Graph Map 기준, 한스 농장 사례와 GPT 인계 설명을 함께 정리했다.
- 코드 실행 규칙은 변경하지 않았다. 전투 명중·타이밍 미니게임, 운영 문서의 중용과 별도 회복 규칙은 이번 삭제 대상이 아니다. 커밋·푸시는 수행하지 않았다.
- 검증: 문서 Fast 통과, 주요 문서 링크 177곳 누락 없음. 생성 검색 색인 갱신은 기존 공간 증거의 `SpatialProofDrift`로 차단되어 검색 색인에는 이전 문구가 남는다. 정본 문서의 설명 삭제는 반영됐으며 공간 증거는 변경하지 않았다.

## README·스토리 영감 분리 리팩토링 (2026-09-05)

- 현재 기준은 [스토리 영감과 플레이 진행 분리](../Architecture/스토리영감과플레이진행분리.md) `story-inspiration.r1`이다. 아래 과거 상태의 괘·육효 강제 진행 설명은 참고 이력이며 이 기준을 우선한다.
- README의 기획 진입점·최근 구현·Graph Map 3레벨을 정리했다. 원전 순서와 캠페인 단계 수를 분리하고 기존 API·저장 식별자를 유지한다.
- 변경 범위: README, 기획 목차·스토리 정본·캠페인 복원 r2·결속 기준, 캠페인 Contract·Domain·ReplayHasher·시험·E7 작업 명세, 탐색 검사 도구.
- 검증: 캠페인 15건과 서버 HTTP 경계 11건, 합계 26/26 통과. 이야기 단계 1·3·6·8, 단계 수 변조 거부, 구형 필드 누락 저장, 재시도 복원을 확인했다. 주요 문서 참조는 작업 트리 기준 누락 0건이며 커밋 HEAD는 기존 참조 누락 18곳이 남는다. 게시 시 연결 문서를 함께 커밋해야 한다.
- Fast·Task 공통 검증은 `Simulation Unity code map check` 불일치에서 차단됐다. 직접 대상 시험은 별도 수행했다. Play Mode·Game View·실제 서버 연결, 커밋·푸시는 수행하지 않았다.

> 2026-09-05 기준 최신 상태판이다. 완료 이력은 각 기획·Architecture·보고서와 Git 이력에서 읽고, 장기 결정은 [DECISIONS.md](DECISIONS.md)를 따른다.

## 현재 기준선

- **범위 마감 — 한스 농장 정교한 양식화:** 사용자 요청으로 추가 개선·렌더 반복을 중단한다. [승인 기획 r1](Planning/표현/PLAN-VISUAL-HANS-FARM-001/README.md)의 WI20 지원 결속, 재질·기둥·길 경계·식생·현관 조명 후보와 Blender r6를 마련했다. [결과·잔여 사항](../Reports/한스농장-정교한양식화-2026-09-05.md)에 실제 확인 범위를 기록한다. 시각 완성·E 승격은 미완료이며 커밋·푸시는 하지 않았다.

- **한스 농장·인접 숲 구조 후보 구현:** Graph r2·배치 profile revision 5/기획 r25를 결속하고 실제 손상 주택·작업 공간·밭·연속 흙길·울타리·식생을 canonical Scene에 적용했다. 실제 Play에서 드러난 원거리 후보 지면 제거 결함을 보정했고, 공간 담당 반환의 한스 집중시험 10/10·저장/재개방/재진입 중복 없음과 실제 Play 진단 이미지를 검토했다. 반환 파일 13개 hash는 모두 일치했다. 자산 조사·공간 지식 공통 검사 2건은 새 판본·실제 노드·이전 계보·비승격 경계를 검사하도록 최소 보완 후 본 스레드 재실행에서도 통과했다. **시각 승인 Pending, E 승격 없음**이며 밝기·자연밀도·집재질·큰풀·울타리 지주 마감, 실제 OS 보행/UI·Farm WI·LH 통합은 남는다. 기존 Missing Script 경고 10건도 남는다. 커밋·푸시 없음. [승인 범위](../Reports/한스농장-인접숲-자연경관조립-2026-09-05.md), [이미지·검증·미완료](../Reports/한스농장-자연경관-시각검토-2026-09-05.md).

- **한스 농장 화면 정리 완료:** canonical Scene의 무관한 H계층 전시 Root를 백업 후 비활성화하고 최소 농장 표현(집 한 채·경작 구획 후보)을 추가했다. 기존 울타리·도끼·벌목 대상과 Player/Runtime은 보존했다. 저장·재개방 및 Play 2회에서 중복 없음, 관찰 Console 오류 0을 확인했고 실제 Play PNG를 검토했다. 어두운 판독성, 실제 OS 입력·수리 완주 미검증은 남으며 E는 승격하지 않았다. 원본 Prefab 삭제·커밋·푸시 없음. [결과·이미지·복구 경로](../Reports/한스농장-월드화면정리-2026-09-05.md).

- **현재 우선 작업 — 한스 첫 울타리 수리 완결:** Unity 컴파일 결손과 WI19·20 실행 대장 누락을 보완하고, 독립 HansFenceE5 슬롯에서 기존 벌목·수집→목재 소비·수리→저장 재개를 연결했다. Unity EditMode 18/18, 한스 정적 계약 34사례와 주체 개발 체계 회귀가 통과했다. .NET 집중 회귀는 64/65로 캠페인 네 구성요소의 E 책임 메타데이터 누락이 남는다. **Logic E5 / Presentation E4 / 통합 E4**이며, 실제 배치·입력·Game View는 월드 담당에게 검토를 요청했으나 미검증이다. 아래 이전 작업 기록의 컴파일 차단을 현재 차단으로 읽지 않는다. [변경 범위·검증·다음 순서](../Reports/한스울타리-E5연결결손보완-2026-09-05.md), [개수 대신 현재 원장 기반 상태](generated/subject-interaction-development.md)를 따른다. 커밋·푸시하지 않았다.

- 역경 스토리 기획을 `hexagram-story-sequence.r15`로 정리했다. 문답은 괘 의미와 큰 이야기 합의 뒤 효사 원문·의미·각색 차이를 하나씩 대조한다. [64괘 플레이 스토리 큰 줄기 r1](Planning/스토리/PLAN-STORY-HEXAGRAM-SEQUENCE-001/괘의미별-플레이스토리-큰줄기.md)은 기존 정체성을 8개 읽기 묶음으로 연결한 `Proposed`다. 모험가의 편안한 생계→관계와 방어→생활권 회복을 장기축으로 제안하고, 수뢰둔의 원문 대응와 부분 각색을 구분했다. 기존 승인 효·제작 커서·학습 맥락 카드·주체·WI·H·Runtime·Evidence는 유지한다. 제작 대장·생성기와 문서 템플릿을 같은 순서로 정리했다. 관련 검사 6종(제작 21·정체성 12·효 요구사항 13·씨앗 9·트리 12사례와 E1 색인), 64괘 표의 중복·누락 검사, 수정 문서 링크 검사와 범위 한정 Fast가 통과했다. 로그는 `artifacts/local/validation/20260905-203031`에 있다. 기획 hash 변경을 소비하는 Graph Map·Presentation E4 인계는 별도 재결속 대상이다. 전체 통합·Unity 실행은 이번 검증에 포함하지 않았으며 커밋·푸시는 보류한다.
- 한스 농장·주인공·약초 관련 중복 기획을 소유권 기준으로 통합했다. 메인 스토리 r84는 `마음 편히 살 자리와 생계를 만든다`는 모험가의 장기 욕망만 소유하고, 수뢰둔 r4는 한스 농장의 육효 이야기와 선택형 약초 생활을 단일 정본으로 소유한다. 첫 벌목·울타리 문서는 이야기 정본이 아니라 기존 WI·Logic E5·표현 경계의 `SupportingSlice`로 낮췄고, 약초 제작은 재사용 기능 규칙, 첫 플레이 체감은 발견·이탈·귀환 감각만 맡도록 분리했다. 과거 본문과 구현 계보는 삭제하지 않았으며 코드·Unity·Evidence 단계는 변경하지 않았다. 효사 요구사항 검사와 H 시각 대장 검사는 통과했지만 `hexagram-story-production` 생성물과 Presentation E4 기획 색인은 새 판본 반영 전이라 stale로 차단됐으며, 다른 진행 변경이 섞인 생성물을 이번 기획 정리에서 덮어쓰지 않았다.
- 수뢰둔 `PLAN-STORY-HEX03-CAMPAIGN-001 r3`의 여섯 효 이야기와 주체·WI·H 의미 정의는 `StoryApproved / RequirementsResolved / LogicReadyForDevelopment`로 유지한다. 시각자료는 생활주택·경작 구획·울타리 H1 세 건만 채택 후보로 남겼다. 기존 H2 조립 문맥과 H3 캠페인 조감도는 품질 기준을 충족하지 못해 `VisualUndecided`로 낮추고 이력 참조만 보존했으며, 공간 표현 개발 준비는 미완료다. 실제 Prefab·Unity World 배치·Play Mode·Game View·E5 승격은 수행하지 않았고 Animation은 E6에 남겼다.
- 역경 메인 스토리의 64괘 전체에 `핵심 상황·갈등·주체 관계·압박·판정 규칙·완주 변화`를 가진 캠페인 정체성 초안을 배정했다. 조합 fingerprint와 실제 조합 중복을 모두 거부하는 대장·생성기·시험을 추가했고 `64개 / 짧은 실제 플레이 서막 2개 / 본격 캠페인 62개`를 검증했다. 중천건·중지곤은 각각 육효의 짧은 실제 플레이 비트를 가진 `StorySeeded` 서막으로 바꾸고, 정식 제작 커서 `HEX-01-QIAN`, 선행 표본 문답 커서 `HEX-04-MENG-L3`, Runtime `NotEstablished`를 분리했다. 수뢰둔·산수몽의 기존 안정 ID와 이야기는 보존했다. 관련 4개 대장 Check와 전용 회귀 45사례가 통과했다. 전체 주제 기획 회귀는 기존 `playable-loop:hexagram-campaign-retry.v1`의 기획 문서가 허용된 상세 설계 루트 밖에 있어 중단되며, WI·H·Unity·Runtime·Evidence 승격은 수행하지 않았다.
- 괘 캠페인 실패·초효 복귀를 Logic E3로 구현했다. 한 괘는 초효부터 상효까지 순차 진행하며, 부상·지연·부분 파손·자원 손실은 현재 효를 유지하는 회복 가능 손실로 기록한다. 수뢰둔에서 `HansLost` 또는 `HansFarmFullyLost`가 발생하면 진입 상태 전체를 복원하고 시도 번호와 결정적 variation seed를 갱신해 초효로 돌아간다. 이전 시도 저장은 현재 시도를 덮어쓸 수 없고, 상효 완료 때만 여섯 WI를 영구 해금한다. 계약·Simulation Core·LocalProcess·RemoteHost API·Save/Replay v31을 연결했고 집중 시험 8/8과 HTTP 경로 호환 검사를 통과했다. Presentation E4 이상, Unity·Play Mode·Game View는 수행하지 않았다.
- 역경 스토리 기획에 64괘·384효 전체 탐색 트리를 추가했다. 모든 괘와 효는 안정 앵커로 클릭할 수 있고, 실제 연구를 연 수뢰둔·산수몽 12효만 물리 기획 문서로 연결한다. 수뢰둔 6효는 주체·WI·H·Graph Map·배치 맵·인계 상태를 함께 내려가 볼 수 있으며 H 참조 9개를 별도 통합 색인으로 만들었다. 기존 기획 가운데 플레이어 경험을 직접 다루는 27개를 주괘 1개·보조 후보 1개로 분류했고, 한스 농장 표본 1개만 사용자 확인 `Confirmed`, 나머지는 `Candidate`로 유지한다. 기술·운영·자료 조사 문서는 분류에서 제외했으며 이 색인은 효사 문서·WI·H·개발 Goal을 생성하거나 Evidence를 승격하지 않는다. 전용 검사 12개와 기존 자유 씨앗 9개·64괘 제작 15개·효사 요구사항 7개를 통과했다. Unity·Runtime·DB·commit·push는 수행하지 않았다.
- 역경 스토리 제작을 `정식 01→64 공부·제작`과 `자유 기획 씨앗 유입`의 이중 흐름으로 현행화했다. 자유 기획은 먼저 `StorySeed`로 보존하고 Codex가 주괘 하나와 순위가 있는 보조 후보를 제안하며, 사용자 확인 뒤에만 `HexagramConfirmed`가 된다. 주체·상황·변화·결과가 구체화되기 전에는 효사로 내려가지 않고 재분류 이력을 보존한다. 한스 농장 첫 생활 거점은 수뢰둔, 독립 학습 맥락은 산수몽의 괘 수준 표본으로 이관했으며 둘 다 효 분류는 `Deferred`다. 자유 분류는 정식 활성 괘·runtime·WI·Evidence를 변경하지 않으며 현재 정식 활성 괘는 계속 제1괘 중천건이다. 새 씨앗 대장·생성 색인 검사 9개, 기존 64괘 제작 검사 15개, 효사 요구사항 검사 7개가 통과했다.
- 수뢰둔→산수몽 이데아 맵 학습 전환을 r2와 Logic E1~E3로 현행화했다. 산수몽은 육효를 순차 통과하는 퀘스트가 아니라 실제 학습 필요와 `ActionRecord`를 근거로 제안·수락·보류·안전 경계 해제되는 별도 괘상 맥락 카드다. 한 번에 하나만 활성화하고 자유 행동을 막지 않으며, 발단 학습 필요에 대응하는 실제 행동 하나가 확인되면 해소한다. 육효는 순서 없는 공명 원형이고 NPC 학습 카드와 산수몽 보정은 같은 실제 행위에 출처별 `+1`로 가산되며 카드 수락 자체는 성장하지 않는다. 제안·활성·효 공명·효과 영수증은 기존 학습중점 Save/Replay 상태에 포함하고 LocalProcess·HTTP 조회·명령 경계를 추가했다. 집중 시험 25/25, Simulation 전체 시험 1,638/1,638, Simulation 솔루션 빌드와 E7 작업 명세·엔진 인계 79개·표현 검증 23개·E8 대기 23개 정합성 검사가 통과했다. 정식 제작 괘는 계속 제1괘이고 산수몽 UI·기초 지도 표현·Unity·Play Mode·Game View와 E4 이상은 미구현이다.
- 개발 기본 단위를 `PlayableUnit 필수`에서 `Ready 주체 → WI 하나의 Goal → 직접 결과 → 파생 작용 0~2 hop → 선택적 PlayableLoop 검증 묶음`으로 전환하는 호환 관문을 추가했다. 주체 대장은 실행 Actor 4종과 권위 WI 계약의 직접 대상 1종을 `Ready`로 관리하며, 현행 WI 108개 전부에 Actor·직접 대상이 결속됐다. 기존 안정 ID를 삭제하지 않고 단일 WI Goal로 투영하며 `loopStableId`를 선택적 검증 참조로 보존한다. 산수몽 괘상 맥락까지 등록된 PlayableUnit은 23개이고 엔진 상호작용 프로필 79개와 E8 대기 항목 23개가 동기화됐다. 신규 Loop 없는 Goal 등록·승인 기획 hash·직접 결과·파생 작용 인과/멱등/Save-Replay 검사를 지원하며 자동 활성화·E 승격은 하지 않는다. 전체 주제 기획 검사는 다른 진행 작업의 `nature-woodcutting-animation` 기준 hash 불일치에서, 전체 개발 체계 검사는 기존 `nature-camp-visitor-stay` 증거 파일 hash 불일치에서 각각 차단된다. Unity·Play Mode·Game View·서버 연결·DB·commit·push는 이 변경에서 수행하지 않았다.
- `playable-loop:nature-hans-farm-fence-restoration.v1`의 첫 복구 폐루프를 `부러진 농장 도끼 줍기 → 개인 도끼로 나무 1그루 벌목 → 목재 2개 → 손상 울타리 3구간 원자적 수리`로 동결했다. 서버 권위 상태·오류·Save/Replay와 WI-NATURE-19/20을 구현해 관련 Simulation 시험 52/52를 통과했으므로 Logic은 E5다. Graph Map·배치 맵과 Unity 최소 표현 Presenter·EditMode 시험은 작성했지만, Unity 프로젝트가 이번 범위 밖 `SimulationPlayerLearningFocusService` 패키지 경로 결손으로 컴파일되지 않아 Presentation은 E4 `Blocked`, 통합 단계는 E4로 유지한다. E5는 실제 또는 fallback GameObject의 배치·Renderer·Collider·Bounds·상태 판독까지이며, 실제 Actor AnimationClip·Rig/Avatar·전이·중단/귀환은 E6로 분리했다. 전용 E7 작업 명세 검사는 통과했으나 전체 Goal 상태판 검사는 기존 `nature-woodcutting-animation` 명세 hash 불일치에서 중단돼 생성 상태판을 갱신하지 않았다. Play Mode·Game View·Scene 저장·commit·push는 수행하지 않았다.
- Blender 5.2.1 LTS의 Synty 원본 불변→프로젝트 소유 복사본→작은 가공→검증→FBX 흐름으로 보편 `농장 생활 주택 H1`의 첫 손상 상태 파생형을 만들었다. `SM_Bld_Farmhouse_02`를 기준 원본으로 고정하고 곳곳의 작은 지붕 결손·노출 판재와 불균일한 갈색 먼지 풍화를 프로젝트 소유 메시·재질에서만 표현했으며 `.blend` 재열기, FBX 왕복, 원본 해시 불변과 명세 계약 7/7을 통과했다. Unity Import·Prefab/Scene·Game View·E5는 수행하지 않았다. 상세는 [H1 생활주택 Blender 손상 파생형](../Reports/H1생활주택-Blender손상파생형-2026-09-03.md)과 [작업 뼈대](../Reports/Blender-Synty파생자산-작업뼈대-2026-09-03.md)다.
- 루트 README는 현행 `기획 정본 → 주체·WI·오행 → Graph Map → 배치 맵·Presentation E4 → 개발·E5` 흐름과 `cheolwo/mirror` 저장소 명칭으로 갱신했다. 원격 기준 로컬 선행 34커밋은 [8개 맥락 묶음](../Reports/최근커밋-맥락분류-2026-09-03.md)으로 분류했지만 squash·rebase·push는 수행하지 않았다.
- 공통 기획과 공간 특화 작업을 분리했다. 모든 기획은 먼저 지금·여기·나·너·이렇게·결과·다음 선택과 상태 변화를 정리하고, 위치·통행·시야·H 조합·자산 형상이 선택이나 결과에 영향을 줄 때만 `Graph Map → 배치 맵 → Synty 조사 → 선택적 Blender → Unity 실제 결속`으로 분기한다. 비공간 WI는 사유 있는 해당 없음으로 이 경로를 생략할 수 있으며, 준비 자산·Blender·staging만으로 E5를 선언하지 않는다.
- 현행 기획은 [PLANNING.md](PLANNING.md)의 `PLAN-*` 51개가 소유한다. 일반 기획 답변은 새 `D-###`로 만들지 않고 해당 기획의 판본을 올린다.
- H1 Synty 표현 전수 배당은 별도 대장으로 구현했다. 새 기획을 포함한 현행 기획 48개, H1 85개(상호작용 53·표현 패턴 32), H2 38개를 대조하고 실제 Unity `Assets/Synty` Prefab 4,221개에서 모든 H1의 정확 파일 후보를 찾았다. 최초 결과는 `PendingVisualReview 84 / BlenderRequired 1 / NoCandidate 0 / ExactCandidate 0`이며, 이후 로컬 대화형 검토에서 3개 H1의 개별 소품 후보 묶음을 승인했다. 이는 공간 전체 `ExactCandidate` 승격이 아니다. H2는 H1 조립만 검사하고 직접 Prefab을 배당하지 않는다. 상태 변화는 기존 E4 결속 1·추가 상태 계약 필요 64·정적 20으로 분리했다. 서버 DB 반입·Unity Import/Scene/Game View·E5 승격은 0이다. [전수 상태판](generated/h1-synty-representation-assignments.md)을 따른다.
- H1 후보를 기획 대화에서 직접 보고 확정할 수 있도록 공급사 FBX 읽기 전용→Blender 검토 사본→PNG 제시 흐름을 열었다. 첫 표본 `h1-stock:farm-production`의 주 후보 `SM_Env_Dirt_Rows_01`, 연결·확장 대안 `SM_Env_Dirt_Rows_Center_01`, 수확 결과 소품 후보 `SM_Prop_Potato_01`을 렌더했다. 공급사 외부 PSD 경로가 Blender에서 해소되지 않아 검토 사본에만 중립 재질을 적용했으며, 형상 검토용일 뿐 최종 재질·후보 승인·Unity Import·E5 근거가 아니다.
- 첫 시각 피드백에 따라 원본 약 5×5m 고랑을 균일 축소하지 않고 중심부를 잘라 2.5×2.5m `경작 구획 H1` 검토 파생형을 만들었다. 원본 FBX hash 불변, `.blend` 저장, 프로젝트 소유 FBX 내보내기와 재반입 뒤 2.5×2.5×0.18834m 크기 일치를 확인했다. 이 크기와 플레이어의 청사진·배치 모드 설치 가능성을 기획으로 확정했다. 실제 설치는 부지·겹침·통행·권한·자원·상위 배치 규칙을 통과해야 하며 Unity Import·배치 검증·E5는 아직 아니다. 한국어 로컬 자료함 `ArtSource/Blender/H1-시각자료/`와 기계 목록을 마련했다.
- 경작 구획 H1의 첫 파종은 정중앙 `(u=0.5,v=0.5)`에서 가장 가까운 고랑 중심선에 맞추고, 다중 종자는 중앙에서 고랑 방향으로 대칭 확장하는 E4 배치 규칙을 추가했다. 가장자리·구획 밖 배치는 특수 작물의 명시적 규칙 없이는 막는다. 종자 준비 공간 H1의 종자 봉투 2종과 곡물 자루 1종은 사용자 시각 검토에서 모두 채택됐다.
- `2.5×2.5m 경작 구획 H1`을 실제 플레이에 가까운 첫 상태 표본으로 열고, 감자 예시의 `빈 고랑 / 파종·초기 발아 / 성숙 / 부분 수확` 네 상태를 Blender Collection과 개별 PNG로 분리했다. 작물은 세 고랑 중심선에 정렬하고 중앙에서 대칭 확장한다. 상태 마스터 `.blend` 재개방과 입력 자산 6개 hash, 상태별 개체 수를 기록했으며 사용자 외형 승인 전 `E4VisualCandidate`다. Unity Import·Prefab/Scene·Game View·E5는 수행하지 않았다.
- 농기구 보관 공간과 수확물 임시 적치 공간의 팔레트 상자·나무 상자·도구 상자도 모두 채택됐다. 같은 소품은 H1마다 주 용기·분류 상자·도구 보조 등 역할 태그를 달리한다. 개별 소품 용도만 승인됐으며 공간 전체 조립, Unity Import·배치, Renderer/Collider/Bounds와 E5는 여전히 미검증이다.
- 농산물 선별 공간 H1의 판재형·평판형 작업대와 분류 상자를 모두 채택했다. 두 작업대는 모두 수확 후 출하 준비용 선별 작업대로 사용하고, 상자는 선별 농산물을 담는 용도로 둔다. 오행 분류와 운송·출고 공정은 이번 확정에서 제외했으며, 농산물이 담긴 상자 상태·공간 배치·Unity E5는 후속 검토다.
- 현행 농산물 세척 공간 H1의 자동 후보인 우물·급수탑·자연 수로와 보충 후보인 긴 수조·반통·물뿌리개를 Blender로 렌더했다. 자동 후보는 세척 설비보다 물의 출처에 가까워 `급수점 H1 + 세척 작업점 H1 + 배수·오수 처리점 H1 → 농산물 세척 공간 H2` 계층을 제안했다. 사용자 결정 전 기존 H 정의·식별자·H2 조합은 변경하지 않았고 Unity E5도 아니다.
- 긴 세척 수조와 반통 세척조를 `농산물 세척 작업점 H1`의 형태 후보로 보존하고, 긴 수조부터 `빈 수조 / 물 채움 / 농산물 세척 중 / 오염수 배출 대기` 네 상태를 각각 독립 PNG·`.blend`로 저장했다. 재개방 검사에서 상태별 물·농산물·침전물 가시 개체 수가 각각 `0/0/0`, `1/0/0`, `1/5/0`, `1/0/3`으로 분리됐다. 이는 E4 시각 표본이며 세척 애니메이션·수질·급배수·저장·Unity Scene·E5는 미검증이다.
- 사용자 검토로 긴 수조와 반통 세척조의 네 상태를 모두 E4 상태군으로 채택했다. 반통은 `빈 반통 / 물 채움 / 농산물 세척 중 / 오염수 배출 대기`를 독립 PNG·`.blend`로 보존한다. 공급사 원본 내부의 솟은 중앙 면이 수면을 관통해 원본은 보존하고 상태 표본 파생형에서 내부 정점 13개만 낮췄다. 재개방 검사 수치는 `0/0/0`, `1/0/0`, `1/4/0`, `1/0/2`다. 실제 급배수·세척·수질·Unity Scene·E5는 미검증이다.
- 다음 H1 검토로 배수·오수 처리 후보 세 가지를 Blender에서 분리 렌더했다. Town 바닥 배수구는 세척점의 배수 입구 H1, Construction 콘크리트 관은 방향성 있는 배수 경로 Edge의 표현, City 수변 관은 하류 방류구 H1 후보로 분리한다. 세 후보는 아직 사용자 채택 전이며, 방류 권한·수리 계산·Unity 배치·E5는 수행하지 않았다.
- 배수 입구의 공간적 의미를 보기 위해 세척 바닥 경사·배수구·낙차 연결관·절개 배수관·수변 방류관을 잇는 Blender E4 시안을 조감도와 측면도로 만들었다. 청색 흐름 표식은 설명용이며 실제 유체가 아니다. 구배·펌프·막힘·역류·방류 수질과 권한·Unity World Anchor·E5는 미검증이다.
- `WI-NATURE-06` 벌목의 최초 저폴리 자세 r1은 사용자 검토에서 하체·허리 연쇄가 약해 초보자처럼 보인다는 결손이 확인돼 초기 블로킹 이력으로 낮췄다. 같은 SwordCombat 팩 캐릭터와 동작의 실제 Armature 자세값은 정상 이전됨을 확인해, 골반 회전 백스윙과 넓은 보폭·무릎 굽힘·몸통 추종이 있는 타격을 r3 독립 PNG·`.blend`로 다시 만들었다. 원천 검술 동작을 벌목 검토에 전용한 E4 연구이므로 최종 벌목 AnimationClip·정밀 그립/접촉·Unity 재생·E5는 미검증이다.
- 급수원과 세척점·고랑을 잇는 실제 보유 자산의 농장 호스·스프링클러·호스 릴을 Blender로 렌더했다. 바닥 배치 호스와 호스 릴·손잡이를 각각 H1으로 보고, 둘을 H단계를 소비하지 않는 `급수 호스 세트` 기본 구성으로 확정했다. 수동 관수에는 호스 끝을 직접 사용할 수 있어 스프링클러 H1은 선택 설치물이며, 추가할 때만 자동·반자동 살수 구성이 생긴다. 연결·급수·통행 점유는 H1과 별도 Graph Map Edge이며 Anchor·길이·유량·압력·개폐·누수·통행 방해 및 동시 공급 규칙은 아직 미정이다. canonical H와 Unity E5는 변경하지 않았다.
- H1을 실제 연결한 Blender H2 자료함을 새로 만들고 농산물 세척 공간과 고랑 관수 공간 시안 2개를 저장했다. 두 시안은 급수탑→호스 릴·손잡이→호스→소비점의 연결을 직접 표현하고, 관수 시안의 스프링클러는 선택 설치물로 표시한다. 수동 우물은 별도 펌프가 필요해 연결 완료 시안에서 제외했다. 조립 시안·`.blend`·검증 결과는 로컬 E4 자료이며 Unity Scene·입력·물 효과·E5는 아니다.
- AreaSet 조감도의 수동 높이·축척 오차를 줄이기 위해 `숲경계-한스-생활농장 r3` 저작 순환을 구현했다. Unity 지형 삼각 격자와 9종 Synty Prefab의 실제 Bounds·GUID·dependency hash를 내보내고, Blender에서는 수평 위치·회전·균일 축척만 편집하며 Unity가 높이를 자동 접지한다. 지형·자산 판본, 범위, 경사, 높이 편차, 중첩, 통행 여유를 검사해 31개 객체가 오류 0·경고 0으로 staging Prefab까지 생성됐고 Blender 무편집 왕복 변환값도 31/31 동일했다. 검증은 격리된 Editor 프로젝트에서 수행했으며 canonical `SimulationWorldShell` Scene·Play Mode·Game View·서버 결속·E5는 수행하지 않았다. 본 프로젝트 전체 컴파일은 별도 진행 중인 학습 중점 서비스의 Unity 패키지 결손 1건으로 차단돼 해당 변경은 건드리지 않았다.
- H2 시안의 의미를 고정 배치가 아닌 연결 위상으로 한정했다. 실제 Unity E5에서는 서로 떨어진 급수원·호스 릴·세척점·관수점의 World Anchor와 거리·장애물에 맞춰 호스가 동적으로 변형되고, 필요·최대 길이·지면 추종·Collider·통행 간섭·물 공급 또는 살수 도달이 같은 배치 판본에서 검증돼야 한다. 현재는 이 요구만 기록했으며 구현·Scene 배치·E5 승격은 하지 않았다.
- 기획 문답은 `지금·여기·나·너·이렇게·결과·다음 선택`을 사용한다. `이렇게`는 상황 안에서 플레이어가 고르는 적절한 행위이며 유일한 정답이나 성공 보장이 아니다.
- WI 오행은 105개 전부 분류됐다. 권위 전환을 소유하거나 일으키는 플레이어·NPC·차량·시설·자연물·환경·세계 규칙의 역할 행위 분류는 E5 진입 필수조건이며, 누락·오래됨·공식 WI 전환 불일치는 E7 작업 명세 검사에서 차단된다. 이는 상태 계산·보상·실제 World 결속·E 승격을 대신하지 않는다.
- 모든 WI는 오행으로 행위 관계를 기록하고 실제 권위 결과·행위 기록이 있으면 명상에서 되돌아볼 후보가 된다. 오행은 행위 관계, 명상은 경험 재해석, 회복·위협은 실제 영향 판정이며 승인 Profile 없이는 자동 성장·회복·위협 변화를 만들지 않는다. 현행 명상 카탈로그 65개 플레이어 WI는 전체 오행 분류 105개의 부분집합이다.
- 실제 Actor의 WI·ActionRecord와 같은 판본의 오행 분류·행위별 기여 Profile을 결합해 `오행 활동 발자국`을 보여 주는 방향을 확정했다. 벌목은 금, 파종·생육 농사는 목, 도끼 전투는 금, 대장간 제작은 원자 공정별 화+금 혼합을 표현할 수 있다. 오행값은 원소별로 독립 누적하고 UI에서만 선택 기간의 100% 상대 분포로 보여 준다. 이는 고정 적성이 아닌 재구성 가능한 파생 이력이며, 현행 코드에는 아직 오행 누적 상태·재조회·UI 투영이 없다.
- 역할 객체 구현 우선순위는 `오행 역할 주체 노드 → 직접 상호작용 엣지와 직접 결과 → 필요한 파생 엣지·인접 노드 변화`다. 상호작용 완료를 정의하는 직접 결과는 두 번째 단계에 포함하고, 2차 파급이 없는 WI에는 가짜 효과를 만들지 않는다.
- 기획 문답은 현재 작은 slice의 주체 하나를 먼저 고정하고, 기존 확정 내용을 대조한 뒤 아직 미정인 핵심 행위·제약 하나만 질문한다. 확정된 행위는 오행으로 분류하고 직접 상호작용·필요한 파생 작용 순으로 인계하며, 개발 완료를 기다리느라 다음 독립 기획 문답을 멈추지 않는다.
- 당분간 기획은 Graph Map 부분 그래프를 기본 화면으로 사용한다. 역할 객체를 주체 노드로, 기존 WI·후보 행동을 오행 메타데이터가 붙은 엣지로 두고 `주체→직접 상호작용→직접 결과→필요한 1-hop 파급` 순으로 결손 하나만 질문한다. L2 배치 제약과 L3 코드 결속은 L1 의미에서 자동 생성하지 않는다.
- Graph Map은 플레이어의 정답 순서나 전술을 지시하지 않고 노드 간 관계와 `Available·Conditional·Blocked·NotApplicable` 경계를 표현한다. Graph Map 중심 문답은 관계 존재·방향·조건·차단·조건 해소와 인접 1-hop 변화만 질문하며, 실제 선택·조작·우선순위는 플레이어와 Runtime에 남긴다.
- 모험가·한스 첫 방어의 일반 지상 마수 목표 경로는 `농장 외곽 접근→울타리 공격·돌파→씨앗·식량 창고 공격`으로 확정했다. 울타리가 유효한 동안 내부 목표로 건너뛰지 못하며 비행·굴착·도약·잠입은 확인된 적 능력과 별도 대응 엣지가 있을 때만 예외로 연다.
- 첫 울타리 돌파 때 모험가와 한스는 수리와 전투로 갈라지지 않고 같은 침입 마수 무리를 공동 방어한다. 위협 제거·후퇴 뒤에만 울타리 수리, 창고·주민 피해 확인, 농장 생활 귀환을 순서대로 연다.
- 모험가와 소가주는 같은 `위협 발생→접근로·병목→H1 방어 지점→H2 방어 블록→보호 대상` 능동 방어 기반을 공유한다. 플레이어는 지점 사이를 직접 이동해 방어에 개입하며, H2는 좌표상 통째 배치가 아니라 연결·통행·방호 조건을 충족한 H1 묶음에서 파생된다. 소가주의 기본 지휘권은 담당 영지·부대·물자 안에서만 열리고 가주 직할·타 영주 범위는 별도 권한을 요구한다.
- 거점은 H3 전체 역할과 H2 생활·생산/방어/공세 지원 블록을 청사진으로 먼저 계획하고, 자원·인력·시간·권한을 실제 투입해 하위 H1을 하나씩 가동한다. H1별 기능은 즉시 열 수 있지만 H2·H3 성립은 필수 H와 연결 조건에서 파생되며, 청사진·배치 맵·실제 건설·Presentation E5는 서로 대신하지 않는다.
- 건설 확정 전 청사진은 플레이어 화면에만 보이는 `DraftGhost`다. 자유 편집 중 겹침·통행·부족·권한 문제는 경고만 표시하고 World·NPC·적·Collider·방어·생산·H 성립에는 영향을 주지 않는다. 확정 사전 검사에서 부지·권한·통행·자원·선행 H를 통과한 뒤에도 실제 기능은 개별 H1의 착공·진행·완료 상태에서만 열린다.
- 청사진의 작성·저장·수정·교체와 예상 비용 조회는 무료이며 자재·인력·부지를 잠그지 않는다. 특정 H1 착공을 확정할 때만 해당 부지와 자원을 예약하고 건설 상태를 만들며, 착공하지 않은 나머지 H1 슬롯은 계속 자유롭게 바꿀 수 있다.
- 정식 H2·H3 청사진 제안은 건축가 NPC와 협력·영입·고용·공식 위임이 성립하고 NPC가 설계 업무를 수행할 수 있을 때 열린다. 배치 엔진은 Graph Map·배치 맵·현재 부지·위협·자원·권한·건축 지식을 입력으로 복수 후보와 부족·위험·근거를 반환할 뿐, 채택·착공·자원 예약·World 변경을 확정하지 않는다.
- 건축가 관계가 끝나거나 업무 불능이어도 저장된 청사진은 작성자·설계 지식·Graph Map·배치 맵·World 기준 판본과 함께 남아 열람·표시·착공 요청에 사용할 수 있다. 새 자동 설계와 H2/H3 구조 변경은 잠그되, 실제 H1 착공은 현재 부지·권한·통행·자원·점유를 항상 재검사해 오래된 설계가 유효하지 않으면 차단한다.
- 절기 전략은 초반·중반·후반 세 구간 사이의 두 내부 분기에서 방어·회복·제한 공세를 다시 배정하는 r14로 정밀화했다. 디펜스는 기본 압력이지만 공세 결과가 다음 방어 조건을 바꿀 수 있으며, 상대 성 점령은 한 분기에서 끝내지 않고 여러 절기에 걸친 상위 캠페인으로 분리한다.
- 두 빙의 시작의 전투 규모를 분리했다. 모험가는 한스와 농장 울타리·생활 거점을 지키는 소규모 직접 방어에서 시작하고, 소가주는 여러 지휘관·부대·보급을 조율하는 지역 방어와 제한 공세에서 시작한다. 두 경로는 같은 전황에 합류하지만 서로의 행동을 자동 완료하지 않으며, 후반 콘텐츠를 시작 선택으로 영구 잠그지 않는다.
- 두 주인공이 같은 전황에 있어도 시작 때 선택한 한 명만 직접 조작한다. 선택하지 않은 주인공은 독립된 주요 NPC로 남아 자기 상태·목표·관계·권한에 따라 행동하며, 플레이어는 대화·약속·정식 명령으로 협력할 수 있지만 몸 전환이나 무권한 직접 조작은 하지 않는다.
- 첫 주체 발전 표본은 나무꾼 출신 모험가의 `생활 벌목 → 전투 도끼술`이다. 두 기술은 별도 이데아 노드이며 공유되는 금 작용이 학습 부담을 낮춘다. 큰 방향 공개, 핵심 관계 관찰, 안전한 훈련, 실전 검증을 구분하고 벌목 반복만으로 전투 숙련·피해를 자동 지급하지 않는다.
- 첫 관계선은 후속 경계 조우에서 몸에 밴 벌목 동작이 전투 응용으로 실제 나타나고, 한스가 장점과 위험을 짧게 교정한 뒤 발현한다. UI는 두 노드가 이어지는 모습을 보여 주되 이 연출·대사만으로 전투 숙련 완료나 수치 보정을 만들지 않는다.
- 한스의 정확 교정 동작·문구는 후속 세부 기획으로 미뤘다. 이 미정은 생활 벌목·전투 도끼술 관계선이나 현재 Farm E5 집중 질문을 차단하지 않는다.
- 한스 농장 첫 밭갈기는 한스 집에서 보이는 가장 가까운 비통행 허용 구획으로 확정했다. 절기 말에는 농사일·전투 응용 중 다음 학습 중점을 별도 카드로 고르며, 이는 세계 운영 타로와 식별자·저장·효과 책임을 합치지 않는다.
- 새 절기 시작에 목의 농사일과 금의 벌목·전투 도끼술 중점을 고른다. 비선택 분야는 잠기지 않는다. 카드 선택 순간 영구 능력치를 지급하지 않고 선택 계열의 절기 효과를 활성화해 실제 관련 작업·교습·훈련 결과에 적용한다. 경험·숙련 외 직접 작업·전투 효율까지 바꿀지는 미정이다.
- 첫 NPC 학습 중점 slice는 `playable-loop:player-npc-learning-focus.v1`로 E3까지 구현됐다. 비울 수 있는 주 슬롯 한 칸, 외부 초·중·후반 일정, 최초 경계 즉시/절기 중 다음 구간 적용, 한스 농사·도끼 카드, 결속된 플레이어 행위의 세부 숙련 이해도 `+1`, 중복 방지와 Save/Replay v30, Local/HTTP 동일 투영을 포함한다. 집중 13/13·전체 Simulation 1,624/1,624·솔루션 빌드 경고 0/오류 0을 통과했다. 실제 UI·Unity·Game View·관계 취득·플레이어 멘토 공유·보조 슬롯과 E4 이상은 미구현이다. 상세는 [E3 구현 결과](../Reports/플레이어-NPC학습중점-E3-2026-09-03.md)다.
- 절기 시작 카드는 하나의 플레이 흐름으로 통합한다. 이전 절기 정산 뒤 새 절기 시작 지점에서 아르카나 세계 화두를 먼저 뽑고, 이어서 한스 등 관계가 열린 NPC에게서 배울 개인 성장 중점을 고른다. 화면 순서는 합치되 두 카드의 식별자·저장 사본·효과 권위는 분리한다.
- 상태창은 현재 절기·날짜 범위·남은 기간·다음 절기를 최상단에 두고, 오행 활동을 `현재 절기 / 최근 활동 / 전체 이력`으로 전환해 본다. 아르카나와 NPC 학습 중점은 서로 다른 큰 슬롯으로 표시한다.
- 아르카나 카드는 절기 시작에 하나를 필수 선택하고 절기 동안 고정한다. NPC 학습 카드는 절기를 초·중·후반 세 구간으로 나눠 시작과 초반·중반 종료 시점에 다음 구간 카드를 선택하며, 새 효과는 다음 WorldTick 정산 구간부터 적용하고 이전 구간을 소급하지 않는다. 초반에는 한스 카드만 보이지만, 이후 NPC별 고유한 배울 점이 관계·공동 행동·가르침 허용·핵심 관계 관찰 근거로 열리면 보유 카드 풀에 등록한다. 등록된 카드는 NPC와 떨어져 있어도 자유롭게 선택할 수 있고 이는 원격 가르침이 아니라 이미 배운 관점의 회상이다. 동시에 활성화할 주·보조 카드 슬롯 수는 아직 미정이다.
- NPC 학습 중점 효과는 관련 경험·숙련 성장과 이데아 발견 가능성만 높인다. 첫 단계는 약하고 실제 관련 작업·교습·훈련 완료와 확인된 관계가 누적될수록 강해지며, 카드 반복 선택만으로 강화하거나 생산량·작업 속도·전투 피해를 직접 높이지 않는다. 계열 전환 때 이전 진척을 보존할지는 미정이다.
- 비동기 플레이어 멘토 카드 방향을 확정했다. 플레이어는 서버가 권위 행위·성장 계보로 검증한 자기 경험 가운데 공개에 동의한 학습 주제만 판본화된 카드로 등록하고, 다른 플레이어는 이를 NPC 카드와 같은 보유 학습 카드 영역에서 선택할 수 있다. 이는 실시간 멀티플레이나 캐릭터 복제가 아니며 인벤토리·재화·세계 진행·비공개 내면을 공유하지 않는다. 수신자의 실제 관련 행동에만 제한된 학습·이데아 발견 보정 후보를 제공한다. 현재 공식 온라인 계약은 검증된 명상 경험 공유까지만 구현돼 있으므로 멘토 카드의 발행·취득·철회·차단은 별도 E1과 서버 계약이 필요한 미구현 범위다.
- 멘토 카드 발행 범위는 `전체 공개`와 `친구 한정`을 모두 허용한다. 전체 공개 카드는 누구나 공개 목록에서 취득할 수 있고, 친구 한정 카드는 양쪽이 수락한 현재 친구 관계에서만 새로 취득할 수 있다. 공개 범위는 취득 권한만 바꾸며 카드 효력·인기도 보정·연락처 공개와 결합하지 않는다. 발행 철회나 친구 관계 종료 뒤 이미 받은 카드의 처리와 정확 신고·차단 상태는 아직 미정이다.
- 정상적인 공개 중단이나 친구 관계 종료 뒤에도 이미 정당하게 받은 멘토 카드 판본은 계속 사용할 수 있다. 멘토가 성장하면 기존 카드를 변경하지 않고 새 검증 근거의 판본을 발급하며, 친구 관계 종료 뒤 발급된 친구 한정 신 판본은 취득할 수 없다. 신 판본이 전체 공개라면 일반 공개 절차로 취득할 수 있다. 안전상 무효 판정된 카드는 기존 사용도 막을 수 있는 별도 예외로 남긴다. 새 판본을 자격 있는 수신자에게 자동 교체할지 선택형 갱신으로 제공할지는 미정이다.
- 멘토 카드 신판은 자격 있는 수신자에게 알리되 기존 카드를 자동 교체하지 않는다. 수신자가 구판 유지 또는 신판 취득을 선택하며, 신판 장착을 확인해도 진행 중인 학습 구간에는 소급하지 않고 다음 구간부터 적용한다. 구판의 과거 학습 기록과 판본 근거는 보존한다.

## 원자 E1 조립

- [원자 E1 색인](generated/playable-loop-planning-e1-index.md)은 기획 47개, PlayableUnit 20개, 검토 원자 모듈 11개, 원자 후보를 정리한 기획 34개·후보 182개를 포함한다.
- 현재 주 폐쇄 대상은 `play-transaction:hans-farm.till-one-plot.v1`이며 기존 `WI-FARM-01`과 `playable-loop:farm-crop-cycle.v1`을 재사용한다.
- Farm 공동 준비 묶음은 `WI-FARM-01~06`을 E4까지 함께 준비하되 E5·E6·E7 증거는 WI별로 독립 판정한다.
- 운영 서버→Unity 기획은 Hub 입고 상태 사본 조회→검수→적치→권위 재조회 네 원자 후보로 정리됐다. 이는 실제 운영 Command나 Unity 배치를 활성화하지 않는다.

## Presentation E4 준비

- E4 후보 풀을 r2로 확장해 오행별 모델 수가 아니라 `시작→진행→결과→중단/회복`의 자연스러운 변화 판독을 검사한다. 첫 표본은 벌목 접촉·나무 낙하·농장 생활주택 수리 3건이며 모두 실제 결손을 보존한 `Blocked`다. Blender 현행 후보 목록은 8건으로 정리했고, 기존 Prefab·Unity 조립·재질·Animation으로 부족한 부분만 프로젝트 소유 파생형으로 연다. 후보 풀 회귀 18사례와 Blender 목록 계약 6사례가 통과했으며 Blender 제작·Unity Import·Scene·Game View·E5 승격은 수행하지 않았다.
- [E4 후보 풀](generated/playable-loop-presentation-e4-candidate-pool.md)은 기획 47개를 `Frozen 11 / Provisional 23 / NotApplicable 13`으로 구분하고 첫 묶음 WI 22개를 추적한다.
- 한스 숲 경계 농장 프로필은 배치 인스턴스 5개와 `h1-stock:farm-residential-home`을 포함한다. 생활주택의 실제 Synty 기준 원본과 프로젝트 소유 손상 파생형은 `BlenderValidatedCopy`까지 준비됐지만 Graph 통합, Unity Import·Prefab·Collider·Bounds·Scene·Game View가 없어 Presentation E5는 계속 `Blocked`다.
- 숲 경계 생활농장 방어 지역 H4는 별도 대체 계층을 만들지 않고 기존 H3 세 개를 역할별로 재사용하는 E4 준비 프로필로 추가했다. 한스 배치 프로필 r4는 H4 대상, H3 역할, 적 주 진입·조건부 돌파·플레이어/한스·보급/수리·후퇴/회복 경로를 결속한다. 검증된 r3 Blender 장면에서 파생한 r4 마스터는 실제 배치 31개, 미승인 H1 반투명 자리표시자 3개, 경로 5개와 `Normal`/`Defense`/`BreachRecovery` View Layer를 보유하며 재개방 구조 검사를 통과했다. 이 결과는 Blender Presentation E4뿐이며 공식 H4 대장 등록, canonical Scene, Play Mode·Game View, 실제 조건부 경로, Unity 지면 재접지와 E5는 미검증이다.
- `울타리 방어 병목 지점 H1` r2 Blender 후보를 PolygonFarm 나무 울타리·출입문 원본 불변으로 생성하고 로컬 H1 시각 자료 대장 r25에 등록했다. 중앙 4m 통과부, 좌우 각 2.5m 울타리 연장, 안쪽 82° 문 개방, 내부 플레이어·한스 위치를 결속하고 표식 없는 이미지와 설명용 이미지를 분리했다. 저장 `.blend` 재개방과 생성 hash·E4 경계를 확인했으나 사용자 시각 승인, 공식 H1 채택, Graph 권위, Unity 배치·감속·전투·Collider와 E5는 아직 성립하지 않았다.
- H1 시각 검토는 H2 조합보다 먼저 수행한다. 각 H1을 단품 이미지로 확인하고 필요한 Blender 파생형은 원본을 덮어쓰지 않은 채 안정 ID 후보·revision별 `.blend`, 대표 PNG, 원본 hash·치수·변형 사유와 함께 보존한다. 경작 구획 네 상태 표본은 저장 완료했다. 우물 이후 발전 방향은 상수도로 확정했고, 프로젝트 소유 `농장 상수도 급수점 H1`의 개방형 호스 소켓 r2를 사용자 시각 기준선으로 채택했다. 연결 판독용 수동 관수 r2는 급수점→호스 릴·손잡이→호스→경작 구획을 묶고, 파란 체결 슬리브가 회색 출수구 외경 전체를 감싼 뒤 감속 연결부에서 호스로 좁아지게 했다. 닫힘/45도 열림 상태를 두 PNG·두 `.blend`로 분리했으며 재개방 검사에서 양쪽 체결부 2개, 닫힘 물줄기·젖은 흙 각 0개, 열림 물줄기 5개·젖은 흙 2개를 확인했다. 사용자 검토에서 체결부가 다소 두꺼운 한계를 인정한 E4 조립 시안으로 채택했으며, 젖음의 고랑 방향 확산은 후속 상태 방향으로만 보존한다. 실제 체결·호스 변형·수압·유량·젖음 확산·입력·Unity Import·Prefab/Scene·Game View·E5는 수행하지 않았다. 다음 독립 H1 검토는 농산물 세척 작업점의 긴 수조·반통 후보다.
- 공간 대장은 H1 85개, H2 39개, H3 20개, H4 6개를 생성·검증한다. H 정의와 Synty 후보는 실제 Prefab 적합성·Renderer·Collider·Bounds·입력 증거가 아니다.
- 24절기 생활 작업·복장·식생 조사는 정적 후보와 Animation/Blender 결손을 기록했다. 새 가공이나 Scene 적용을 승인하지 않는다.

## 운영 서버→Unity 선별 이관

- [운영 기능 이관 대장](generated/operational-unity-transfer-catalog.md)은 페이지 기능 241개, EF Core `DbSet` 271개, MongoDB collection 사용 지점 28개를 결정적으로 재생성한다.
- 기본 분류는 `PlayableAction 67 / ReadOnlyContext 111 / AmbientSimulation 59 / ServerOnly 4`다. H 대응은 검토 후보이며 DB 행이나 페이지를 H1로 자동 생성하지 않는다.
- 현행 상위 기획은 현실의 창고·상하차·분류·배달을 작업자·기사·화물·차량의 움직임으로 비추는 `Mirror 물류 표현`을 첫 대표 구현 목표로 삼는다. 운영 사실, 플레이 선택, Unity 세계 표현은 같은 판본을 참조하되 서로를 대신하지 않는다.
- 첫 기술 표본은 Hub 입고·검수·적치다. 현행 Presentation은 E1이고 다음 목표는 E4 준비이며, 실제 Prefab·World 배치·입력·같은 revision 관측 전 E5는 차단한다.
- 다음 독립 표본 후보는 도심 물류 거점의 출고→오토바이 배송→인수 또는 실패→복귀·재배송이다. 아직 정확 WI·H1/H2·자산 후보가 동결되지 않았으며 자동 활성화하지 않는다.
- 오행 역할 행위 표본은 `기사 인수 목 → 상차 화 → 적재 완료 토 → 출차 금 → 경로 운송 수 → 목적지 인수 목`으로 준비됐다. 현행 WI에 기사 인수, 오토바이 출차·차량 종류, 실패 뒤 재시도·반품 계약이 없어 `Blocked`이며 이 결손을 임시 상태로 메우지 않는다.

## 현재 차단과 미커밋 경계

- Graph Map은 `r13`으로 현행화했다. 기획 59건을 `UpdateExisting 19 / CreateSubgraph 4 / Blocked 9 / NoImpact 27`로 판정했고, WI `r48`, partition `r9`, overlay `r6`, 배치 규칙 결속 `r6`, 정규화 표본 `r3`을 같은 계보로 맞췄다. 새 생활 여덟 영역 공공데이터 기획은 자료 분류이며 AreaSet·H·통행·배치 권위를 만들지 않는 `NoImpact / PlanningReference`로 결속했다.
- Hub 등록→검수→하역 대기→창고 입출고→운송대 편성→출고 관계와 선택형 통제 중계 하위 그래프를 결속했다. 전체는 노드 39개·간선 41개·제약 37개·하위 그래프 7개·포트 14개·연결자 8개이며 원본 Check와 결정적 생성물 재생성이 통과했다.
- 기획 인계는 `r13` Integrated 1건과 과거 Superseded 8건으로 갱신했고 회귀 47건이 통과했다. 개발 인계는 Graph Map `r13`과 Goal 대장 `r144`에 맞춰 다시 생성·검사했으며 회귀 68건이 통과했다. 첫 벌목 성찰 명세의 기획 정본은 소가주 기획에서 수뢰둔 `hex03-campaign.r11`로 재결속했다.
- Graph Map 전체 회귀의 Unity SourceAndSymbol 단계는 별도 Unity 작업 사본의 canonical Scene hash 변경으로 중단된다. 외부 작업의 hash를 임의 승인하지 않았다. 첫 벌목 성찰 E7 명세의 독립 검사는 기존 `LogicStageDiffersFromPlayableUnit`에서 중단되며 hash 복구와 별도인 성숙도 불일치로 남긴다.
- Unity Editor·Play Mode·Game View·Scene 저장, 서버 실제 연결, 운영 DB 쓰기, Evidence 승격, commit·push는 이번 현행화에서 실행하지 않았다.

## 이번 검증된 커밋

- `a740e101` `docs(planning): consolidate canonical gameplay plans`
- `6c1c37f3` `feat(planning): assemble atomic E1 planning index`
- `d4c238db` `feat(metadata): classify inquiry depth and WI elements`
- `28ed5b97` `feat(spatial): prepare forest-edge farm placement profiles`
- `0221523c` `feat(presentation): manage E4 candidate pool`
- `e5692f25` `feat(integration): catalog operational Unity transfer`
- `fac571d5` `docs(research): record seasonal Synty presentation gaps`
- `687201d8` `fix(unity): add logging reflection metadata`
- `efb4ac14` `docs(governance): separate planning from decision history`

각 묶음은 `git diff --check`와 범위 Fast를 통과했다. 원자 E1, WI 오행, 공간·농장 배치 준비, Presentation E4, 운영 이관 전용 회귀가 통과했고 운영 이관 도구는 0경고·0오류로 빌드됐다. 원격 push는 하지 않았다.

## 다음 우선순위

1. Graph Map r11의 25개 미판정 기획과 partition·overlay 판본을 별도 Graph Map 작업으로 닫는다.
2. 한스 농장 `WI-FARM-01` 한 구획의 정확 안정 ID·접근·도구·입력·VisualKey를 동결해 E5 실행 명세로 인계한다.
3. Hub 입고·검수·적치의 H1/H2·Graph Map·배치 맵·Synty 후보를 E4에서 같은 판본으로 결속한다.
4. 도심 창고 출고·오토바이 배송의 정확 WI와 성공·실패·복귀 경계를 한 원자 폐루프로 기획한다.
