[기획 · 데이터·공간 식별 · PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION · 승인·필지 도형 수집 준비 r7]

# 사가정역 건물 도로명주소 완결 관문

- 상태: `Approved602Baseline / SharedAddressPolicyConfirmed / ImplementationR3ValidatedLocal / PresentationBuildingPrivateLedgerValidated / DevelopmentAdminApiValidated / ParcelGeometryCollectionApproved / OfficialAlD002ContractConfirmed / ExtractionPipelineValidated / VWorldArchiveBlockedExternalAccess / ParcelGeometryStillMissing / OfficialGeometryPromotionDeferred / PublicAndUnityApplyBlocked`
- 확정 근거: 2026-09-14 사용자는 사가정역 디오라마의 모든 건물이 배달·가격·후속 지역 자료에 사용 가능한 도로명주소 결속 상태를 갖추는 것을 필수 선행조건으로 요청했다. 이어서 정식 주소 하나를 여러 건물이 공유하는 단지는 각 건물 고유 식별자를 유지한 채 `OfficialSharedComplexAddress`로 결속하는 A안을 선택하고 구현을 요청했다.
- r5 확정 근거: 사용자는 화면–기준 건물 결속 원장과 4,062개 화면 건물 주소 상태 원장을 분리해 현재 자료로 먼저 구현하는 2단계안의 첫 절편을 확정했다. 또한 역세권 디오라마 증거 체계에서 도로명주소·필지 식별자·실제 필지 경계 도형의 수집 여부와 coverage를 서로 다른 항목으로 반드시 확인하도록 요청했다.
- r6 구현 반영: [첫 절편 구현 기록](implementation.r6.md)에 따라 두 자료를 로컬 RDB의 독립 dataset으로 저장·재조회하고 Development 관리자 읽기 계약까지 검증했다. 이 결과는 후보 원장의 비공개 검토 증거이며 실제 필지 경계·공식 건물 동일성·공개·Unity·배달·가격 권위를 열지 않는다.
- r7 구현 반영: 사용자가 실제 필지 경계 도형을 다음 선행 관문으로 승인했다. 공식 브이월드 `AL_D002` 서울 전체자료의 2026-09-08 판본·EPSG:5186·A1 PNU 계약과 CC BY 표시를 확인하고 공개 컬럼 정의서를 동결했다. 다만 원본 SHP는 로그인 없이는 내려받을 수 없어 `BlockedExternalAccess`다. [수집 준비 구현 기록](parcel-geometry-collection.implementation.r7.md)의 결정적 추출·coverage 검사만 구현했으며 도형 0/3,774와 모든 적용 권위 false를 유지한다.
- 관련 기획: [도로명주소 표현층 r15](../PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/road-address-presentation.r15.md), [건물 자료 연결 대장 r12](../PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/building-evidence.r12.md), [주거 가격 관찰층 r2](../PLAN-DATA-SAGAJEONG-HOUSING-MARKET-OBSERVATION/README.md), [동북권 역세권 확장 r4](../PLAN-SYSTEM-NORTHEAST-SEOUL-STATION-DIORAMA-ROLLOUT/README.md)

## 현재 판정

602개 기준 건물은 [구현 r3](implementation.r3.md)에서 전수 상태 대장을 만들었다. r5 승인 당시 화면을 구성하는 GIS 건물 4,062개에는 주소 원장이 없었지만, r6 첫 절편에서 **화면–기준 건물 결속 원장과 화면 건물 주소 상태 원장을 분리해 4,062/4,062 판정을 비공개로 저장·재조회했다**. 공식 건물 동일성과 실제 필지 경계가 아직 없으므로 후보를 공개·Unity·배달·가격에 쓰는 관문은 계속 닫혀 있다.

| 현재 자료 | 수량 | 의미 |
| --- | ---: | --- |
| 사가정 기준 건물 | 602 | 동결된 디오라마 건물 수 |
| OSM 도로명+건물번호 | 580 | 화면 표시 후보이며 공식 주소 식별 확정은 아님 |
| 기존 78개 도로와 연결 | 568 | 도로명 표현층에서 탐색 가능 |
| 기존 수집 자료 주소 후보 결속 | 244 | 비공개 검토 후보 |
| 단일 후보 | 204 | 단일 후보일 뿐 공식 건물 동일성 확정은 아님 |
| 주소 없음 | 22 | 공식 확인 또는 명시적 미해결 필요 |

따라서 기존 r15는 도로명을 고르고 건물로 카메라를 이동하는 읽기 전용 표현층이며, 배달 목적지·부동산 관측·사업장 입점의 공통 주소 권위로 사용할 수준은 아니다.

## r4 화면 건물 주소 가능성 감사

화면 건물은 주소 근거가 없는 임의 장식물이 아니다. 동결된 국토교통부 GIS 건물통합정보 `AL_D010` 4,062개는 모두 고유한 `GIS건물통합식별번호`, 19자리 필지고유번호(PNU), 법정동코드와 지번을 갖는다. 현재 공간 오버레이가 외형·높이 표현에 필요한 필드만 내보내면서 주소 결속 필드를 의도적으로 제외했기 때문에 화면 자료에서 주소가 보이지 않는 것이다.

2026-08 행정안전부 건물DB와 관련지번 자료를 읽기 전용으로 교차한 현재 후보 분포는 다음과 같다.

| 화면 건물 주소 후보 상태 | 수량 | 현재 의미 |
| --- | ---: | --- |
| 단일 도로명주소 후보 | 3,796 | 같은 PNU 또는 공식 관련지번 경로에서 도로명주소 키가 하나지만, 아직 화면 도형과 공식 주소 건물의 동일성 확정은 아님 |
| 복수 도로명주소 후보 | 60 | 한 화면 건물 후보에 공식 주소가 둘 이상 연결되어 자동 선택 금지 |
| 공식 주소 후보 없음 | 206 | 부속 구조물·원천 판본 차이·주소 결손 가능성을 구분해야 함 |
| **합계** | **4,062** | 4,062/4,062에 판정 상태를 기록할 수 있음 |

기존 화면–기준 건물 footprint 결속 544개에 r3 주소 원장을 전달하면 공식 주소 511개, 후보 23개, 미해결 10개다. 그러나 공식 주소 511개를 PNU 경로와 다시 대조했을 때 491개만 동의하고 18개는 주소가 충돌하며 2개는 PNU 주소 후보가 없다. 따라서 PNU·최근접·겹침 하나만으로 4,062개를 `Official*`로 채우지 않고 충돌과 후보를 원장에 그대로 남긴다.

사가정 화면 건물 전수 주소화의 목표는 **4,062개 모두에 독립 주소를 만들어 주는 것**이 아니라 **4,062개 모두가 주소 확정·공유·후보·모호성·독립 주소 없음·미해결 중 하나의 감사 가능한 상태를 갖게 하는 것**이다.

## 필수 완료의 정의

602개 기준 건물의 필수 조건은 모두에 임의 문자열을 채우는 것이 아니다. 모든 건물이 다음 중 정확히 하나의 상태를 가져야 한다.

- `OfficialIndividualAddress`: 공식 건물 단위 도로명주소와 결속
- `OfficialSharedComplexAddress`: 하나의 공식 주소를 공유하는 단지·건물군의 구성 건물
- `RoadAddressCandidate`: OSM 주소는 있으나 공식 건물 결속 미확정
- `NoIndependentRoadAddress`: 부속동 등 독립 도로명주소가 없는 사실이 공식 자료로 확인됨
- `Unresolved`: 자료 결손·도형 불일치·복수 후보 때문에 아직 판정 불가

완료 관문은 `602/602 AddressResolutionState 보유`, `임의 주소 0`, `무근거 복제 0`이다. `Unresolved`가 남아도 원인을 빠짐없이 기록한 **대장 완결**은 가능하지만, 배달·가격처럼 확정 주소가 필요한 기능의 개별 건물 적용은 계속 차단한다.

화면 건물 4,062개는 별도 `PresentationBuildingAddressAssignment`에서 다음 상태를 사용한다.

- `OfficialIndividualAddress`: 공식 건물군 도형의 주소 복합키 또는 상세주소 동 도형의 `bdMgtSn`까지 화면 도형과 단일 결속
- `OfficialSharedComplexAddress`: 단지·집합건물의 여러 화면 도형이 검증된 같은 공식 주소를 공유
- `ReferenceBindingCandidate`: 기존 기준 건물과 유일하게 겹치며 그 주소를 후보로 참조하지만 교차 원천 확인 전
- `ParcelAddressCandidate`: PNU·관련지번에서 도로명주소 하나가 나오지만 건물 동일성은 미확정
- `MultipleAddressCandidates`: 같은 필지·도형 후보에 공식 도로명주소가 여러 개
- `NoIndependentRoadAddress`: 부속동 등 독립 도로명주소가 없다는 공식 근거가 확인됨
- `CrossSourceConflict`: 기준 건물 주소와 PNU·공식 도형 주소가 서로 다름
- `Unresolved`: 공식 후보 부재 또는 원천 판본·도형 관계 미해결

확장 완료 관문은 `4,062/4,062 PresentationAddressResolutionState 보유`, `최근접 주소 복사 0`, `복수 후보 강제 선택 0`, `후보의 배달·가격·Unity 사용 0`이다.

## 정본과 식별 관계

1. 주소기반산업지원서비스의 도로명주소 DB·건물 DB·상세주소 동 표시를 주소 정본으로 사용한다.
2. 주소 복합키는 `행정구역코드 + 도로명코드 + 지하여부 + 건물본번 + 건물부번`과 자료 판본을 보존한다.
3. `bdMgtSn`, 건축HUB `mgmBldrgstPk`, OSM 건물 ID, 디오라마 건물 ID를 하나로 합치지 않는다.
4. 각 관계는 연결 방법·근거·신뢰도·기준일을 가진 `BuildingAddressAssignment`로 저장한다.
5. 한 주소가 여러 동에 대응할 수 있으므로 주소 하나를 건물 하나로 강제하지 않는다.

공식 주소 자료도 여러 건물이 하나의 도로명주소를 갖는 집합건물을 한 건의 주소로 제공하며, 상세주소 동 표시는 건물 단위 동 정보를 별도로 제공한다. 따라서 공유 주소를 명시적 관계로 지원해야 주소를 조작하지 않고 602개 건물을 관리할 수 있다.

- [주소기반산업지원서비스 공개 주소·건물 DB 안내](https://business.juso.go.kr/addrlink/attrbDBDwld/attrbDBDwldList.do?cPath=99MD&menu=%EB%8F%84%EB%A1%9C%EB%AA%85)
- [여러 건물이 한 주소를 갖는 경우와 상세주소 동 표시 안내](https://eng.juso.go.kr/addrlink/qna/qnaDetail.do?bulletinRefSn=117833&currentPage=286&keyword=&noticeMgtSn=117833&noticeType=QNA&noticeTypeTmp=QNA&page=&searchType=)
- [건물 DB 전체자료 안내](https://eng.juso.go.kr/addrlink/qna/qnaDetail.do?bulletinRefSn=129513&currentPage=141&keyword=&noticeMgtSn=129513&noticeType=QNA&noticeTypeTmp=QNA&page=&searchType=)
- [행정안전부 도로명주소 전자지도 안내](https://www.data.go.kr/tcs/dss/selectFileDataDetailView.do?publicDataPk=15050413)
- [건물군 도형과 상세주소 동 도형의 식별자 차이 안내](https://m1.juso.go.kr/addrlink/qna/qnaDetail.do?bulletinRefSn=104116&currentPage=436&keyword=&noticeMgtSn=104116&noticeType=QNA&noticeTypeTmp=QNA&page=&searchType=)

## 저장과 소비 경계

RDB는 주소 원본 판본, 공식 주소 복합키, 건물 식별 관계, 검토 상태를 소유한다. 지역 디오라마 투영은 승인된 공개 도로명주소와 일반화된 위치만 읽으며, 상세 동·층·호는 기본 Unity 계약에 포함하지 않는다.

화면–기준 건물 결속 원장과 화면 건물–주소 결속 원장은 합치지 않는다. 건물 footprint 판본과 월별 주소 판본의 수명이 다르므로 `PresentationBuildingBindingLedger`와 `PresentationBuildingAddressAssignment`가 각자 revision·hash를 가지고 station manifest가 둘을 참조한다. 인접 역 창에 같은 원천 건물이 나타나면 역별 주소 행을 복제하지 않고 같은 정본 Assignment를 참조한다.

```text
OfficialAddressSnapshot
  → CanonicalRoadAddress
  → BuildingAddressAssignment
       ├─ DioramaBuildingStableId
       ├─ AssignmentKind
       ├─ EvidenceMethod / Confidence
       └─ SourceVintage / SourceHash
            ↓
     DeliveryAddressProjection
     HousingObservationProjection
     BusinessLocationProjection
```

- 배달은 주소만으로 길찾기를 열지 않는다. 확정 건물 뒤에 `EntranceAnchor`와 접근 가능한 도로/보행 연결이 따로 확인되어야 한다.
- 가격은 확정 건물 또는 허용된 주소·H2/H3 집계에만 연결한다.
- 사업장은 주소가 같아도 실제 입점 건물·호수를 추정하지 않는다.
- H1·H2·H3와 건물 역할은 주소에서 자동 생성하지 않는다.

## 단계적 실행안

1. 주소기반산업지원서비스 최신 서울 건물 DB·주소 DB·상세주소 동 표시의 판본·hash·이용조건을 동결한다.
2. 중랑구와 사가정 1km 후보를 추출하고 기존 OSM 주소 580개를 공식 주소 복합키와 대조한다.
3. 공식 주소 좌표/관련 지번·건축HUB·건물 도형을 단계적으로 대조해 602개 `BuildingAddressAssignment`를 생성한다.
4. 공유 주소, 부속동, 철거·변경 주소, 복수 후보를 별도 상태로 분류하고 사람 검토 대장을 만든다.
5. 로컬 RDB에 원본과 Assignment를 멱등 저장하고 독립 재조회한다.
6. 주소 확정률·미해결 사유·원본 판본을 포함한 서버 읽기 투영을 만든다.
7. 기존 r15 도로명 선택 UI는 새 투영을 읽도록 후속 전환하되 원래 지도 hash와 건물 배치는 유지한다.
8. 주소 완결 뒤 배달 출입구 결속과 주거 가격 관찰층을 각각 별도 관문으로 진행한다.

r4 후속은 다음 두 단계로 분리한다.

1. 현재 동결 원천으로 4,062개 화면 건물의 PNU·지번·결속 방법·후보 주소·충돌·미해결 상태를 별도 불변 원장과 관리자/Development 읽기 API에 기록한다. 이때 기존 ZIP의 관련지번 entry도 파일명·열 정의·길이·hash를 원본 영수증에 추가한다.
2. 신청·승인이 필요한 도로명주소 전자지도의 건물군 도형과 집합건물 상세주소 동 도형을 확보한다. 건물군 도형의 주소 복합키와 상세주소 동 도형의 `bdMgtSn`을 구분해 공간 결속한 뒤 후보를 `OfficialIndividualAddress` 또는 `OfficialSharedComplexAddress`로 승격한다.

1단계는 공식 주소 후보 대장을 완결하는 단계이고 공식 건물 동일성을 전수 확정하는 단계가 아니다. 2단계 자료를 확보하지 못해도 후보·결손 원장은 재현 가능하게 닫되, 후보 주소의 배달·가격·업체·Unity 사용은 열지 않는다.

## r5 디오라마 증거 점검 관문

역세권 디오라마의 건물·주소 기반을 검토할 때는 역별 `StationEvidenceProfile`에 다음 세 항목을 정확히 한 번씩 기록한다.

| 증거 종류 | 확인하는 사실 | 다른 사실로 대체할 수 없는 경계 |
| --- | --- | --- |
| `RoadAddress` | 공식 도로명주소 원본과 후보·충돌·미해결을 포함한 대상 건물별 판정 상태 | 주소 후보가 있다고 실제 건물 동일성·배달 가능·Unity 공개를 승인하지 않는다. |
| `ParcelIdentifier` | PNU와 법정동·지번을 대상 건물별로 확보했는지 | PNU 보유는 필지 경계 도형을 확보했다는 뜻이 아니다. |
| `ParcelGeometry` | 실제 필지 경계 원본 도형을 판본·좌표계·hash와 함께 확보했는지 | metadata나 PNU만으로 필지 경계를 만들거나 건물 도형을 필지 도형으로 가장하지 않는다. |

각 항목은 `collectionState`, `coverageState`, 대상·판정·근거 있음·결손 수, 출처 영수증, 원장 revision/hash, 제한과 `applicationAuthorized=false`를 가진다. `Complete`는 대상 전부에 수집 또는 결손 상태를 기록했다는 뜻이며 전부 공식 주소로 확정됐다는 뜻이 아니다. 수집 상태와 이용권·배달·가격·업체·Unity·gameplay 권위는 별도 관문이다.

사가정 첫 적용의 기준선은 다음과 같다.

- `RoadAddress`: 4,062/4,062 판정 상태를 만드는 첫 절편을 승인한다. 단일 후보 3,796, 복수 후보 60, 후보 없음 206을 보존하고 `Official*` 전수 승격은 하지 않는다.
- `ParcelIdentifier`: 화면 건물 4,062/4,062의 PNU·법정동코드·지번을 확보했으며, 서로 다른 PNU는 3,774개다.
- `ParcelGeometry`: 연속지적도 metadata만 확인했고 실제 경계 원본 도형은 아직 수집하지 않았다. `NotCollected / Missing`으로 기록한다.
- 면목역·용마산역은 이 판본에서 값을 추정하지 않고 `NotAssessed / Unassessed`로 시작한다.

이 체크 도입은 기존 디오라마 규칙 후보의 승격이나 E 단계 자동 승격이 아니다. 기존 형상·높이·화면·이동·Simulation 및 공개 Unity 계약도 변경하지 않는다.

## 검증 조건

- 602개 건물에 중복 없이 정확히 하나의 `AddressResolutionState`가 존재한다.
- 동일 원본 판본으로 같은 Assignment와 hash가 생성된다.
- 동일 주소를 공유하는 복수 건물을 누락하거나 하나의 건물로 합치지 않는다.
- 주소 변경·폐지·건물 철거를 새 revision으로 반영하고 과거 연결을 감사할 수 있다.
- 공식 주소 부재와 수집 실패를 빈 문자열이나 가까운 주소로 숨기지 않는다.
- 상세 동·층·호와 개인 정보가 기본 Unity 투영에 포함되지 않는다.
- 주소 결속이 기존 건물 좌표·외곽·높이·도로와 H/역할을 변경하지 않는다.
- 화면 건물 4,062개 모두가 정확히 한 주소 판정 상태를 가지며 화면–기준 결속 원장과 주소 원장의 hash가 독립적이다.
- 동일 PNU·관련지번 입력으로 같은 후보 집합과 Assignment hash를 만들고 월판 변경은 새 revision으로 남긴다.
- 18개 교차 원천 충돌을 포함한 불일치를 조용히 덮어쓰거나 최근접 주소로 해소하지 않는다.
- 관리자/Development API의 후보·PNU·`bdMgtSn`은 공개 Unity 계약에 새지 않고, 공개가 승인된 `Official*` 도로명주소만 후속 투영할 수 있다.

## 확정

- 사가정 602개 건물의 주소 상태 대장 완결은 배달·가격·사업장 확장의 필수 선행 관문이다.
- OSM `street + houseNumber` 580개는 유용한 후보지만 공식 결속 완료로 세지 않는다.
- 주소가 없는 건물에 가까운 주소를 임의 배정하지 않는다.
- 주소 완결과 배달 출입구·도로 접근성 완결은 서로 다른 관문이다.
- 단지·집합건물은 건물을 합치거나 주소를 조작하지 않고, 각 디오라마 건물이 같은 정식 주소 및 복수의 건물관리번호 후보를 공유 관계로 참조한다.
- 첫 구현은 2026-08-31 기준 최신 월 전체 건물DB를 동결한다. 2026-09 일변동 반영은 재현가능한 r1 전수 결속 후 별도 현행화 작업으로 남긴다.
- 이 판본은 공식 월 전체 원본·결정적 Assignment·로컬 MySQL 저장·독립 재조회까지를 승인한다. 운영 API 공개, Unity Scene·Game View 변경, 배달 출입구·가격 결속은 포함하지 않는다.
- 화면 건물 첫 절편은 별도 `PresentationBuildingBindingLedger`와 `PresentationBuildingAddressAssignment`를 생성하고, 로컬 RDB에 멱등 저장한 뒤 관리자·Development 전용 station-scoped 읽기 API로만 제공한다.
- 첫 절편의 API는 4,062개 후보 상태·PNU·지번·공식 주소 후보를 검토할 수 있지만 공개·Unity·배달·가격·업체 적용 권한은 모두 거짓으로 유지한다.
- 도로명주소·필지 식별자·필지 경계 도형의 수집 여부와 coverage를 역별 증거 프로필의 필수 선언으로 확정한다.

## 미정

- `Unresolved` 허용 상한과 사람 검토 종료 조건
- 상세주소 동 표시를 서버 보호 영역에서 어느 범위까지 보존할지
- 2026-09 일변동을 어떤 주기로 월 전체 판본에 재적용할지
- 신청·승인이 필요한 도로명주소 전자지도 건물군·상세주소 동 도형의 확보 시점과 배포 허용 범위
- 60개 복수 후보·206개 후보 없음·18개 교차 원천 충돌의 사람 검토 종료 조건

## 다음 질문 하나

r6 첫 절편은 완료됐다. 다음은 2단계의 신청형 도로명주소 건물군·상세주소 동 도형과 실제 필지 경계 도형 중 무엇을 먼저 확보할지, 또는 복수 후보·후보 없음·교차 원천 충돌의 사람 검토 표본을 먼저 정할지를 선택한다.
