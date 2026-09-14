# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r13]

## 승인 범위

- 구현 판본: `administrative-dong-diorama:walk-network-candidate.g3c.r1`
- 작업 종류: `LocalPrivateHistoricalWalkNetworkCandidateLedger`
- 완료 상한: `LocalPrivateHistoricalWalkNetworkCandidateStoredAndVerified`
- 대상: `northeast-seoul-rider.r2`의 광진구 4개·동대문구 10개·중랑구 16개,
  합계 30개 역사 행정동

서울 열린데이터광장 `OA-21208`의 자치구별 도보 네트워크를 새 비공개 G3c
원본과 후보 세대로 동결하고, `OA-22160`의 2023-10-31 역사 행정동 경계에
공간 결속한 뒤 기존 범용 공공자료 RDB에 멱등 저장한다. 이 문서는
PlayableLoop 작업 명세가 아니며 E 단계를 주장하지 않는다.

## 공식 원천 확인

- 공식 페이지:
  `https://data.seoul.go.kr/dataList/OA-21208/A/1/datasetView.do`
- 공식 CSV 내려받기 endpoint:
  `https://datafile.seoul.go.kr/bigfile/iot/sheet/csv/download.do`
- 설명 기준: 2020년, WGS84
- 이용허락: 공공누리 1유형, 제3저작권자 없음
- 포털 갱신 표기와 행의 `수집일자`는 자료 설명의 2020 공간 기준을 대체하지 않는다.
- 2026-09-15 조사 시 Sheet 전체는 491,082행이고 광진구 16,288행,
  동대문구 21,988행, 중랑구 21,448행이었다. 행 수는 수집 판본에서 다시
  동결하며 다음 판본의 고정 기대값으로 재사용하지 않는다.

공식 화면이 노출하는 필터는 `SGG_NM`이므로 세 자치구 CSV를 각각 내려받는다.
법정동명 내부 조회의 단순 합은 중랑구 자치구 총계보다 41행 많았고, 면목동
기존 원본에는 같은 링크 ID의 서로 다른 본문 8쌍이 있었다. 따라서 법정동
필터 합계나 링크 ID 단독을 고유성 근거로 쓰지 않는다.

## 생성 규칙

1. 원본 CSV 3개와 링크·노드 유형 코드북, 수집 영수증을 새 G3c 경로에
   동결하고 허용된 정확 5개 파일 외의 파일·하위 폴더·reparse point를 거절한다.
   SHA-256·길이·행 수·CP949 header와 모든 행의 자치구 코드·명·필터 일치,
   코드북의 링크 16개·노드 4개 코드 및 의미 본문을 검증한다.
2. WKT의 경도·위도 순서와 WGS84 범위를 검증하고 EPSG:5186으로 변환한다.
3. NODE는 자기 점을 덮는 역사 행정동 하나에만 귀속한다. 0개 또는 여러
   경계이면 임의 귀속하지 않고 진단으로 남긴다.
4. LINK는 역사 경계에서 실제 선분을 자른다. 한 원천 링크가 여러 행정동에
   fragment를 가질 수 있고, 경계 접점만 있는 0길이 결과는 만들지 않는다.
   두 행정동의 공유 경계와 일치하는 선분은 같은 물리 조각 hash를 가진 채
   양쪽 후보로 보존한다. 원선과 대상 경계 합집합 교차 길이, 물리 조각 합집합
   길이, 공유 조각을 포함한 귀속 길이를 분리해 기록한다.
5. 식별자는 `NODE_ID`·`LNKG_ID` 단독이 아니라 시군구 원본, 유형, ID digest,
   timestamp를 제외한 의미 본문 digest, 동결 CSV 역할과 1부터 시작하는 data-row
   번호로 만든 원천 occurrence, 행정동, fragment geometry digest와 ordinal을
   함께 사용한다. 현재 CSV에는 `RONUM` 열이 없으므로 완전히 같은 원문 행도
   occurrence가 다르면 NODE와 LINK 모두 별도 후보로 보존한다.
6. 코드북의 통행 주체 표시는 `sourceReportedActorCode`로만 보존한다. `PM`은
   배달 오토바이가 아니며, 차량 포함 표기도 현행 통행 가능·법적 진입·차로를
   확정하지 않는다.
7. 2020 도보망과 2023 역사 경계의 판본 불일치, 세 자치구 필터 밖 경계 halo,
   보도 폭·연석·출입구·차로·방향·신호 결손을 모든 후보의 제한으로 보존한다.

## 비공개 원장

- `SourceId`: `seoul-open-data-admin-dong-walk-network-private-review`
- `DatasetId`: `northeast-seoul-admin-dong-walk-network-candidate-g3c-r1`
- `DataRevision`: `northeast-seoul-admin-dong-walk-network-candidate.g3c.r1`
- 품질: `PendingHumanReview`
- 시간 정밀도: `YearOnly2020;AnchorDateNotExactObservationDate`

새 업무 테이블이나 migration을 만들지 않고 `PublicDataIngestionDbContext`,
원본 등록 서비스와 `외부데이터정규화Record`를 재사용한다. 정규화 payload에는
원본 ID를 넣지 않고 digest, 후보 본문 hash, 행정동, node/link fragment 종류,
길이와 품질·권위 flag만 둔다. 전체 WKT와 원본 행은 비공개 snapshot에만 남긴다.

## 권위 차단

다음은 모두 `false`다.

`currentAdministrativeBoundaryEstablished`, `currentPassabilityEstablished`,
`sidewalkWidthEstablished`, `curbEstablished`, `entranceBindingEstablished`,
`motorcycleAccessEstablished`, `vehicleLaneEstablished`, `signalBindingEstablished`,
`distributionApproved`, `publicDisplayAllowed`, `runtimeAuthorized`, `traversalReady`,
`gameplayReady`, `unityApplyAllowed`.

Mongo/current/API/Unity/Scene/Prefab/NavMesh를 수정하지 않는다. 이 원장은 골목·보행
회랑의 조사 후보일 뿐 NPC·차량·오토바이 경로의 권위가 아니다.

## 검증

- 동일 원본·경계·생성기로 같은 후보 집합 hash와 파일 hash를 재현한다.
- CSV 합계, NODE/LINK, 유효·무효 WKT, 밖·단일·복수 귀속, 경계 fragment,
  30개 동별 분포와 원본 시군구 충돌을 manifest/audit에서 재계산한다.
- 같은 원천 ID의 서로 다른 본문을 덮어쓰지 않는다.
- 후보 fragment를 1mm 정수 좌표로 직렬화한 뒤 지정 역사 경계의 2mm 수치
  허용대 안에 다시 포함되는지, 바깥 선 길이가 1mm 이하인지 검사한다.
- 경계 일치 물리 조각의 양쪽 귀속, 동일 행정동 내 중복 부재, 원선의 대상
  교차 길이 보존을 검사한다.
- 후보 집합 hash는 header field와 후보 field의 고정 순서, UTF-8, 각 값 앞의
  unsigned big-endian 4-byte 길이 framing을 manifest에 기록하고 C#이 전체
  후보 파일을 읽어 독립 재계산한다.
- RDB 첫 적용, 새 연결 독립 재조회, 반복 적용 신규 0·갱신 0을 확인한다.
- 기존 G3a·G3b·G4a와 사가정 원장을 update/delete하지 않는다.

완료해도 표현은
`LocalPrivateHistoricalWalkNetworkCandidateStoredAndVerified`로 제한한다.
