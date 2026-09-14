# [기획 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · 구현 r12]

## 정정 목적

G4a r1 독립 감사에서 Python 자체 시험에 사용한 20자리 예시 공급자 ID가 실제
후보 29,721개 중 하나와 일치하고, 그 값이 추적 대상 생성기와 r1의 동결 생성기
사본에 포함된 사실을 확인했다. 이는 정확 원천 ID를 `candidates.ndjson`과 보호
RDB에만 둔다는 r9의 경계를 위반한다.

r1 후보 세대와 로컬 DB 행은 삭제하거나 덮어쓰지 않고
`RejectedAfterIndependentPrivacyAudit / PreservedNotPromoted`로 남긴다. 정확
일치 값은 문서·로그·명령 출력에 반복하지 않는다. r12는 같은 동결 원본과 같은
29,721개 업무 관측을 새 G4a r2 계보로 재발급하는 정정 명세다.

## r9에서 유지하는 것

- SEMAS 2026-06-30 전국 ZIP, 서울 CSV, 행정안전부 2026-03-01 코드,
  `OA-22160` 역사 경계와 동북서울 30개 행정동 scope의 원본 hash를 유지한다.
- 전체 29,721곳, 음식 대분류 `I2` 8,246곳, 건물관리번호 결손 67곳,
  역사 경계 진단 35곳, 동일 상호명·도로명주소 후보 236그룹·490행을 유지한다.
- 공급자 ID를 기준으로 한 원천 행 hash, 안정 ID와 후보 집합의 length-prefixed
  SHA-256 계약, 원문 행정동 소유와 역사 경계 진단의 분리를 유지한다.
- 정확 상호명·주소·좌표·공급자 ID는 Git 무시 local-private 후보와 보호 RDB에만
  두고 Unity·Mongo current·공개 API·로그로 내보내지 않는다.
- 현행 경계, 좌표 datum, 확정 건물·출입구, 현재 영업, Claim, 배포, 주문,
  Simulation, Unity, 통행과 gameplay 권위는 모두 열지 않는다.

## r2 정정 계약

- revision은 `northeast-seoul-admin-dong-business-candidate.g4a.r2`, dataset은
  `northeast-seoul-admin-dong-business-candidate-g4a-r2`, scope는
  `scope:administrative-dong-business:northeast-seoul-rider:g4a:r2`로 분리한다.
- 출력은
  `artifacts/local/public-data/admin-dong-business-northeast-seoul-20260915-g4a-r2/`
  아래의 새 content-addressed generation만 사용한다.
- 자체 시험의 정상 ID는 명백한 합성 fixture `MASYNTHETICFIXTURE01`을 사용한다.
  생성기는 이 합성 fixture가 동결 SEMAS 후보 ID 집합과 겹치면 값은 출력하지 않고
  `SyntheticFixtureCollidesWithSourceIdentity`로 전체 생성을 거절한다.
- 생성기는 자신의 바이트에서 20자리 공급자 ID 형식 토큰을 추출해 실제 선택 ID
  집합과 교집합을 계산한다. 하나라도 겹치면 값을 출력하지 않고
  `GeneratorContainsSelectedProviderShopId`로 전체 생성을 거절한다.
- 독립 감사는 Git에 추가될 코드·문서·scope 전체와 aggregate-only
  `manifest.json`·`audit.json`·`complete.json`을 같은 방식으로 스캔한다.
  허용 위치는 local-private `candidates.ndjson`, r1의 보존된 비공개 생성기 사본,
  r1·r2 보호 RDB payload뿐이다.
- 후보 집합 hash에는 새 설계 hash, 새 scope hash와 정정된 생성기 hash를 포함한다.
  r1 hash나 행을 갱신하지 않는다.
- C# 반입기는 r2의 새 scope·generation·hash·Source/Dataset/Revision만 허용하고,
  r1 원장의 exact count·지속 상태 digest가 적용 전후 같음을 검사한다.
- r2 최초 적용 뒤 같은 입력 재적용은 신규·갱신 0이어야 한다. 별도 DB 연결에서
  exact 29,721개 key, 30개 동 분포, 음식 8,246, 진단 35, 결손 67,
  보호 필드 14개, r2 candidate hash와 r1 보존 digest를 재조회한다.

## 검증 순서

1. r1의 원본·범위·수량과 거절 사유를 값 비노출 방식으로 확인한다.
2. r2 scope·설계·생성기 hash를 동결하고 Python 자체 시험을 실행한다.
3. r2를 두 번 생성해 두 번째 `changedFiles=0`, `verify`, 독립 framing/hash와
   source-ID 누출 교집합 0을 확인한다.
4. C# build·self-test·preview·첫 apply·반복 apply·verify를 실행한다.
5. 새 연결로 r2 exact set과 r1·G3a·기존 면목 원장의 불변을 재조회한다.

## 완료 상한

완료 표현은
`G4aR2BusinessCandidateLedgerStoredAndVerified / R1RejectedPreserved /
ExactBusinessDetailsPrivateLocalAndProtectedRdbOnly`다. r2 정정은 실제 업체 표시,
현재 영업, 건물 결속, 메뉴·주문, 광고, NPC 목적지 또는 생활상 Game View를
승인하지 않는다. 후속 Unity·운영·gameplay 소비에는 별도 등록된 정식 E7 작업
명세가 필요하다.
