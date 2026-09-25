# [자료 수집 · 국가별 해상·항공 물동량 · PLAN-SYSTEM-GLOBE-FISHERIES-OBSERVATION · r16]

상태: `CollectedStored / UnitReviewPending / NoUnityChange` (2026-09-23).

사용자의 활용신청 이후 기존 키로 재확인했다. [r15](transport-mode-data.collection.r15.md)의 서비스 키 등록 차단은 해소되었다. 입항·출항 및 인천공항 국가별 API 모두 HTTP 200 / 결과코드 00을 확인했다. 인증키는 메모리에서만 사용하고 요청 기록에는 포함하지 않았다.

## 실제 수집·저장

공식 출처는 [관세청 국가별 입항물동량](https://www.data.go.kr/data/15151805/openapi.do), [국가별 출항물동량](https://www.data.go.kr/data/15151809/openapi.do)이다. 공식 포털 메타데이터의 이용허락범위 제한 없음을 재확인했다.

202501~202512, 입항 수입(I) / 출항 수출(E), 해상(10) / 항공(40) 조합 4회를 수집했다. 환적(T/R)은 이번 별도 수집 대상이 아니다. API에 국가 제한을 추가하지 않고 전체 응답을 보존한 뒤 중국·일본·미국·호주·베트남 행만 DB 조회용으로 선택했다.

| 조합 | 원본 행 | 선택·저장 행 |
| --- | ---: | ---: |
| 입항·해상 | 7,103 | 1,129 |
| 입항·항공 | 775 | 133 |
| 출항·해상 | 8,223 | 1,037 |
| 출항·항공 | 759 | 132 |
| 합계 | 16,860 | 2,431 |

- 기존 `hongdal-mysql-1 / hongdal_dev`의 공공자료 원문 등록 서비스와 정규화 테이블 재사용. 새 DB·Migration·운영 서버 변경 없음.
- 자료원 `kcs`, 자료집합 `country-port-movement-2025`, 판본 `kcs-port-movement.r1`. 비공개·`PendingHumanReview`.
- 원본 XML 4개를 기존 원문 등록 경로로 저장하고, 선택 행의 월·선적지·입항지·BL 건수·중량·컨테이너 지표를 `TextValue`에 원필드로 보존했다. `NumericValue=null`, `UnitCode=source-row`로 단위 검토 전 계산에 쓰이지 않게 했다.
- 실제 저장 2,431행 → 독립 연결 재조회 2,431행 → 재입력 신규 0행 → 별도 읽기 전용 verify 2,431행.
- 원본·메타데이터·URL/수집시각/hash 영수증: `artifacts/local/public-data/kcs-port-movement-2025-r1/`. 인증키 미포함. 파일별 SHA256:
  - arrival-sea: `06a1bb2ec5bef969d4d1692108455d65747c675f99934742840866f6ddcaa395`
  - arrival-air: `9d977cddd10193d6888d5c4a073969fc52083801668fb96c16147ff889e8c2e1`
  - departure-sea: `3ee620d552d62f696267db329eb0fb90a76320f6aa155b6b1dbd30ba418f5794`
  - departure-air: `64707745412427db857bd19b89be85853762e5d1c3a00d79989fc067e799e6b1`

## 해석 제한

공식 입항 Swagger의 `blWght` 설명이 `총 중량(단위:통)`으로 되어 있어 kg 또는 ton으로 임의 확정하지 않았다. 컨테이너는 TEU, BL은 운송서류 건수이며 선박 대수가 아니다. 부산항·부산신항 등 항만별 행의 포함 관계도 검토 전 합산하지 않는다. 입항의 선적국과 출항의 목적국을 통관 원산국과 동일시하지 않는다.

12개월 포함·응답 totalCount가 있을 경우 실제 행 수·행 자연키 중복·비음수 지표를 검사했다. 대상 국가/항만에 없는 행은 0으로 추가하지 않는다. 이전 인천공항 파일의 912개 지표와 별도 자료집합이며 합산하지 않는다.

인천공항 국가별 API는 2025년 1월 연결 성공만 점검했다. 이번에는 이미 수집된 r15 연간 파일과 중복 저장하지 않았으며 새 연간 API 자료 수집 완료로 보고하지 않는다.

## 변경·검증·후속

- `eng/Ssalddel.PublicDataPortalImport/항만물동량자료Pipeline.cs` 및 기존 Program 진입 추가. `port-movement-acquire/apply/verify/self-test <repo-root>`.
- 단일 응답 32MiB·60초, redirect·자동 retry 없음. 완료된 응답은 hash 확인 후 재사용. 기존 레코드 내용/hash 충돌은 덮어쓰지 않고 거부한다.
- CLI build 오류 0 / 경고 0. 자체시험 6/6: 실제 원본, 공급원 오류, 잘못된 기간, 음수, 필수 필드 누락, 중복 행 거부.
- 다음은 중량 단위·항만 포함 관계를 확인한 뒤 계산 가능한 지표로 별도 변환하는 작업이다. 현재 단계는 수집·DB 축적 완료이지 Unity 배치·실제 운항 재현 완료가 아니다.
- Unity·Game View·HTTP 호스트 실행·원격 DB·공개 게시·정기 수집·commit·push 없음. 새 디오라마 규칙 후보 없음.
