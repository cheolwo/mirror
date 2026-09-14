# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r4]

## 승인 근거

2026-09-14 사용자는 앞서 확정한 행정동 기반 배달운영권역의 30개 행정동을, 사가정역 디오라마와 같은 공용 계약으로 각각 잘라 만든 뒤 지역 모듈로 재사용하도록 요청했다. 자료 조사를 먼저 하고 행정동 경계가 서로 어긋나지 않게 처리하는 것이 승인 범위다.

## 구현 범위

- 대상은 `northeast-seoul-rider.r2`의 광진구 4개, 동대문구 10개, 중랑구 16개 행정동, 합계 30개다.
- 행정동 코드·명칭·법정동 관할은 행정안전부 동결 원장을 정본으로 대조한다. 이름 문자열로 행정동을 추측하지 않는다.
- 서울시 `OA-22160` 경계 ZIP, 국토교통부 GIS건물통합정보 `AL_D010`, 국가표준 NodeLink를 각각 원본 hash·좌표계·판본과 함께 읽는다.
- 모든 원본은 사가정역 원점의 공통 ENU 좌표계로 변환한다. 인접 행정동은 별도 원점을 만들지 않아 경계 양쪽 타일을 같은 좌표로 맞출 수 있게 한다.
- 건물은 원본 도형의 `PointOnSurface`가 정확히 하나의 행정동에 포함될 때만 그 동에 귀속한다. 경계 밖·무효·복수 판정은 전체 감사자료의 `Unresolved`로 남긴다.
- 도로 중심선은 행정동 경계에서 자르고 긴 선분을 결정적으로 세분화한다. 이는 표현 후보이며 골목·보도·도로 폭·차선·통행 가능성 또는 길찾기 권위가 아니다.
- 각 동은 기존 `administrative-dong-diorama.v1` manifest·500m tile·빈 overlay 계약으로 조립할 수 있는 입력 모듈을 가진다. Unity Scene·Prefab·GameObject는 만들거나 수정하지 않는다.
- 생성 결과는 로컬 `artifacts/local`에 hash 고정 사본으로 두고, MongoDB에는 API current pointer와 분리된 불변 candidate batch로만 저장한다.

## 공식 경계 조사 정정과 게시 관문

- `OA-22160` 포털의 데이터 갱신일은 2026-09-11이지만 실제 익명 다운로드 ZIP의 SHA-256은 `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68`, 내부 파일 시각은 2023-10-20, 페이지 파일 수정일은 2023-10-31이다.
- ZIP에는 425개 행정동이 있고 이번 30개 코드는 모두 한 번씩 존재한다. 그러나 서울시 현재 안내 수 427개와 다르고 경계 유효 기준일 필드가 없으므로 현행 전체 정본으로 사용할 수 없다.
- 따라서 이 원본은 `HistoricalOfficialBoundaryBootstrap` 후보 생성과 회귀 검증에만 사용한다. 후보의 희망 준비 코드는 기존 `WaitingForAdministrativeBoundary`, 진단은 `CurrentAuthoritativeBoundaryUnavailable`이다.
- 현행 게시 정본은 행정안전부 주소정보누리집 `구역의 도형` 월 전체분 `TL_SCCO_GEMD`를 우선한다. 실제 파일의 기준월·파일명·SHA-256·공공누리 제1유형·강제 CRS `EPSG:5179`와 30개 코드의 유일성·유효 도형·위상 검사를 모두 기록한다.
- 최신 정본이 없거나 코드 판본보다 오래됐거나 hash·CRS·대상 집합·위상 검사가 실패하면 `AdministrativeBoundaryVintageRejected`로 current 게시를 거절한다. 역사적 후보를 API current manifest로 올리지 않는다.
- SGIS 2025-06-30 경계는 별도 8자리 통계 코드와 공식 crosswalk를 확보한 뒤 교차 검증에만 사용한다. 서로 다른 원본의 경계를 평균내거나 임의 결합하지 않는다.

## 자료·권리 경계

- `OA-22160`은 서울특별시 공공누리 제1유형 관측이다.
- `AL_D010`은 현재 공식 공공데이터포털 메타와 기존 수집 당시 브이월드 표시 사이의 이용조건 충돌이 남아 있다. 건물 결과는 `PrivateReviewOnly`이며 Steam·공개 빌드 배포를 승인하지 않는다.
- NodeLink는 공개 중심선 관측이며 실제 주행·보행·신호 권위가 아니다.
- 상세 주소, 상호, 신청자, 담당자, 연락처, 개인정보와 승인되지 않은 사진은 이번 모듈에 넣지 않는다.
- 건물 계약 v1은 단일 외곽 ring만 지원한다. hole·multipart는 가장 넓은 외곽 ring을 표현 후보로 저장하고 생략 수를 감사자료와 limitation에 남긴다. 이를 원형 완전 보존으로 보고하지 않는다.

## 검증 계약

1. 대상 행정동 30개와 법정동 12개의 정확 집합, 코드 원장과 경계의 유일한 결속을 검증한다.
2. 원본 세 개의 byte length·SHA-256·좌표계를 동결하고 하나라도 달라지면 생성 전에 중단한다.
3. 모든 선택 건물은 정확히 한 행정동 또는 사유 있는 `Unresolved`이며 행정동 사이 중복 소유는 0이어야 한다.
4. 모든 행정동은 유효 경계와 하나 이상의 건물·도로 표현 후보를 가져야 한다.
5. 같은 입력의 재생성 결과와 각 module hash가 같아야 한다.
6. Mongo candidate apply는 같은 batch를 재실행해 새 immutable 문서를 만들지 않고, 별도 연결에서 batch·30 manifests·모든 tiles·overlays를 재조회한다.
7. 기존 API current pointer는 바뀌지 않아야 하고 `distributionApproved=false`, `TraversalReady=false`, `GameplayReady=false`를 유지한다.
8. 서버 계약·API·Unity 해석기의 기존 회귀를 실행한다. 실제 Unity Editor·Play Mode·Game View는 이번 범위가 아니다.

## 완료 상한

이번 판본의 최대 완료는 `HistoricalBoundaryCandidateBatchStoredAndVerified / CurrentPublicationBlocked`다. 이는 30개 행정동의 데이터 기반 모듈 골격과 전수 공간 처리 파이프라인을 준비했다는 뜻이며, 현행 행정동 경계 정본·공개 배포·이동 가능성·게임플레이·실제 Unity 배치를 증명하지 않는다.

## 실제 구현 결과

- 범위 정의: `eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json`, 30개 행정동·12개 법정동.
- 생성 사본: Git 제외 `artifacts/local/public-data/admin-dong-diorama-northeast-seoul-20260914-r2/`, module 30개. 반복 생성은 `changedFiles=0`이다.
- 건물: 원본 695,761행 중 합집합 경계 후보 85,182행을 검사해 61,897개를 단일 귀속했다. 미해결 27개는 경계 밖 13개·원본 무효 11개·1mm 반올림 후 무효 3개이며 복수 판정은 0개다. hole 16개 건물의 내부 ring 18개는 v1 제약으로 감사자료에만 남겼고, 원본 판정점을 명시적으로 보존한 건물은 2개다.
- 도로: 전국 NodeLink 1,557,364행 중 합집합 교차 방향 링크 4,209개를 행정동별 출현 4,620개로 절단했다. 두 동 이상에 걸친 링크는 397개, 최종 100m 이하 선분은 11,769개, 최대 길이는 99.86221m다. 게시 경계에 대한 1µm 허용오차 초과는 0개다.
- 투영: C# 반입기는 Python 경계와 원본 `OA-22160`을 독립 변환해 모든 꼭짓점 2mm 이내를 확인하고, Builder가 같은 건물 61,897개·도로 11,769개를 거절 없이 받아 500m tile 297개를 생성하는지 검사한다. Builder는 무효·자기교차 건물과 비유한·0길이·경계 밖 도로를 거절한다.
- 후보 식별: 범위 내용 hash와 전체 C# 투영 집합, 투영 Builder 의미 revision·소스 hash를 함께 batch ID에 넣어 Builder 변경 뒤 과거 후보와 같은 ID로 충돌하지 않게 했다. batch marker를 자식 문서보다 먼저 점검한다.
- MongoDB: `administrative_dong_diorama_candidate_*` 4개 collection에 batch 1·manifest 30·tile 297·overlay 30, 합계 358개를 저장했다. 반복 적용은 inserted 0·exact existing 358이고 별도 연결 재조회가 통과했다. current pointer snapshot hash `F60B1913F67E4DEEB15C6CBE69BA5A1BBE28041952219BC358F45053759374FD`는 적용 전후 같았다.
- r1 처리: 과거 r1 산출물과 Mongo 후보는 삭제하지 않고 `PreservedNotPromoted`로 유지한다. r2와 섞거나 current로 승격하지 않는다.
- current 승격 차단: 공용 current 저장 경로의 기존 `ProjectionHash`는 아직 manifest의 생성 시각·좌표 프레임·범위·권위 플래그와 overlay 기준 시각을 모두 포함하지 않고, 중복 시 전체 BSON 일치까지 검사하지 않는다. r2 candidate 저장은 전체 투영 집합 hash와 exact BSON 검증을 사용해 영향을 받지 않지만, current/publication 전에 공용 canonical hash와 duplicate exact-body 검증을 별도 보강한다.

### 고정 hash

| 대상 | SHA-256 |
| --- | --- |
| 범위 정의 파일 | `CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B` |
| Python 생성기 | `24BD8D246D2FA714A9C83065D5A8594179DED8E466F8B816FCD3BA02DB79351C` |
| scope manifest 파일 | `5F651C5460173A5C2EFD68F0E04DB06880B8F8F4CAE74A38AD0BDE11637D5113` |
| scope manifest 내용 | `CF2D4057F49BEA4542A641D6F9741A6FF66AA263EB6DB686E38AD03F24BC62D8` |
| audit 파일 | `57876C41A78189EB4D416DA9E11D022207573BE2BAFA20A8D3C846A9FA1A8AF6` |
| audit 내용 | `E59D3A9F252C07673F4EEECC19C9CDAF0E5124F9B5C47C1877F35879FA5E3E8A` |
| module 파일 hash 집계 | `584223C24BEA8D4B1DC3A98661C08B0B95E22614EB7F7F072C4E0F53CF9983BC` |
| C# batch 내용 | `FF8689FB8CEE013E4CCF88BCA2BF4ACD9986F418F1F561FA5B50C0F06B1C2B1F` |
| C# projection 집합 | `043A9E979B150ADC674D4BFB6A8170607D40B0A904CBD16265D8787B9DA00258` |
| 투영 Builder 소스 | `548B3DE46F39994BBD717DBD4610A46683285B905739AF8ACE3370EFCEF08C6D` |

실제 결과와 출처 링크는 [검증 보고](../../../../Reports/동북서울-30개-행정동-디오라마-역사경계-후보-2026-09-14.md)에 둔다.

## 가장 이른 후속 재개점

행정안전부 주소정보누리집에서 서울 최신 월 전체분 `TL_SCCO_GEMD`를 승인받아 내려받은 뒤, 원본 영수증을 동결하고 같은 생성기를 새 경계 Adapter로 재실행하는 단계부터 재개한다. 그 결과가 30개 코드·위상·교차 검증을 통과하고 공용 current hash·중복 본문 검증이 보강된 뒤에만 current 게시와 Unity 선택 관찰을 별도 승인한다.
