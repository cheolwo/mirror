# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r14]

## 승인 범위

- 구현 판본: `administrative-dong-diorama:living-population-candidate.g4b.r1`
- 작업 종류: `LocalPrivateHistoricalLivingPopulationCandidateLedger`
- 완료 상한: `LocalPrivateHistoricalLivingPopulationCandidateGeneratedAndVerified`
- 대상: `northeast-seoul-rider.r2`의 광진구 4개·동대문구 10개·중랑구 16개,
  합계 30개 행정동

서울 열린데이터광장 `OA-14991`의 동결 파일 `LOCAL_PEOPLE_DONG_202607.zip`에서
2026년 7월 행정동별·시간별 **총생활인구수**만 추출해 비공개 G4b 후보 세대로
만든다. 이번 구현은 자료 생성과 결정성 검증까지만 수행한다. RDB 저장, API,
Mongo/current pointer, Unity, NPC, 주문, Game View 및 E 단계는 범위 밖이다.

## 원본과 공간 의미

- 공식 자료: `https://data.seoul.go.kr/dataList/OA-14991/S/1/datasetView.do`
- 동결 파일: `artifacts/local/public-data/myeonmok-administrative-data-20260912-r1/LOCAL_PEOPLE_DONG_202607.zip`
- ZIP: 45,045,885바이트, SHA-256
  `C2DBC97BB2D1018D27CEDE7D47A7B49EEDD3A8F6ABDD268616FFFECBEAE3F7BC`
- 내부 CSV: 111,984,891바이트, SHA-256
  `DD0AA1D519E9816EAB8E0C9628EA75F004898CE4C8245F5CDFA8659722DCC353`
- 전체 자료: 315,456행, 8자리 행정동 코드 424개, 코드마다 744행
- 대상 자료: 30개 행정동 × 31일 × 24시간 = 22,320행
- 이용허락: 공공누리 제1유형

관측 시각은 2026년 7월이지만 공간 단위는 공급자가 밝힌 **2016 행정동
구역 기준 사전 집계**다. `northeast-seoul-rider.r2`의 2023-10-31 역사 경계나
아직 확보하지 못한 현행 행정동 경계에 재투영한 값으로 해석하지 않는다.
생활인구는 통신·공공자료를 결합한 통계적 추정치이며 실제 주민 수, 화면 속
사람 수, 개별 사람의 위치·직업, 주문 수요 또는 NPC 생성 권위가 아니다.

## 추출·식별 규칙

1. ZIP은 정확히 한 CSV만 포함해야 한다. 전체 315,456행과 424개 코드, 코드별
   744개의 중복 없는 `기준일ID + 시간대구분`을 먼저 검증한 뒤 대상만 고른다.
2. 원본 행정동 코드는 정확한 8자리 문자열로 보존한다. 대상 10자리 고유
   식별자는 `code8 + "00"`과 일치해야 한다.
3. 표시명은 원본 CSV의 사실이 아니라 승인된 30개 범위의 설명값이다. 코드
   원장과 대조할 때만 `.`을 `·`로 바꾸며 다른 문자·공백·`제` 표기는 임의로
   정규화하지 않는다.
4. `총생활인구수`는 비음수 십진수로 읽고 지수 표기 없는 canonical 문자열로
   보존한다. 성·연령 세부 28개 열은 후보·hash·manifest·audit에 넣지 않는다.
5. 후보 고유 식별자는 다음처럼 판본과 범위를 포함한다.

   `candidate:administrative-dong-living-population:northeast-seoul-rider:g4b:r1:{code10}:{yyyyMMdd}:{HH}`

   따라서 기존 면목 6개 동 원장의 `metric:kr:hjd:*` 고유 식별자와 충돌하지 않는다.
6. 후보는 행정동 고유 식별자, 원본 8자리 코드, 날짜, 시간, 총생활인구 canonical
   값, 단위, 통계·시간·공간 한계와 권위 차단만 담는다. 개인정보는 존재하지
   않으며 원본의 세부 인구 분해를 후보에서 재구성할 수 없게 한다.

## 기존 면목 6개 동 원장 보호

기존 `myeonmok-admin-dong-living-population-20260912.r1`의 4,464행은 삭제·갱신·
재키잉하지 않는다. G4b는 새 `SourceId`·`DatasetId`·`DataRevision`과 범위가
포함된 후보 고유 식별자를 사용한다. 두 원장이 표현하는 면목 6개 동의 4,464개
동일 사실은 향후 RDB importer가 수치·시각·코드를 exact equivalence로 대조할
보호 대상이지, 한쪽을 다른 쪽으로 덮어쓸 근거가 아니다.
같은 관측을 담은 두 dataset을 합산하면 생활인구가 두 배로 계산되므로 조회·분석은
반드시 하나의 승인된 `DataRevision`만 선택하고 dataset 간 합산을 거절한다.

## 후보 hash와 불변 생성

- 후보 순서: `administrativeAreaStableId`, `observationDate`,
  `observationHourCode`, `candidateStableId`
- hash: UTF-8 문자열마다 unsigned big-endian 4-byte 길이를 앞에 붙여 SHA-256
- header는 schema·revision·scope와 설계/계약/scope/생성기 hash, ZIP/CSV hash,
  모든 권위 flag, 전체·대상 coverage를 결속한다.
- 각 후보는 고유 식별자부터 총생활인구 canonical 값과 모든 제한·권위 flag까지
  고정 field 순서로 결속한다.
- 생성 경로는 후보 집합 hash를 포함하며 이미 같은 경로가 존재하면 byte 단위로
  같을 때만 통과한다. 포인터나 가변 최신 파일은 만들지 않는다.

## 권위 차단

다음 권위 flag는 모두 `false`다.

`currentAdministrativeBoundaryEstablished`, `currentPopulationEstablished`,
`individualPresenceEstablished`, `individualIdentityDerivationAllowed`,
`npcDensityAuthority`, `npcIdentityDerivationAllowed`,
`npcProfessionDerivationAllowed`, `orderDemandAuthority`,
`deliveryDemandAuthority`, `distributionApproved`, `publicDisplayAllowed`,
`runtimeAuthorized`, `gameplayReady`, `unityApplyAllowed`.

## 검증과 완료 조건

- 전체 315,456행·424개 코드·코드별 744행과 대상 22,320행·30개 동을 검증한다.
- 대상 동마다 2026-07-01 00시부터 2026-07-31 23시까지 744개 시각이 정확히 한
  번씩 존재하고, 누락·중복·음수·비정상 코드·시각을 거절한다.
- 동결 ZIP과 내부 CSV, 설계→자료 계약→범위→생성기 hash 계보를 검증한다.
- 동일 입력으로 같은 후보 집합 hash와 다섯 생성 파일을 재현한다.
- 후보 NDJSON에 성·연령 세부 값이나 개인정보가 없음을 검사한다.
- 기존 면목 6개 동 원장은 읽기 보호 계약으로만 참조하고 변경하지 않는다.

완료해도 상태는
`LocalPrivateHistoricalLivingPopulationCandidateGeneratedAndVerified`다. 현행 경계
재집계, 공개, RDB 저장, 디오라마 밀도 표현과 실제 생활상 검증은 각각 별도
승인·구현·증거가 필요하다.
