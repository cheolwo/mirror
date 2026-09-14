# [기획 · 운영·배달권역 · PLAN-OPERATIONS-ADMIN-DONG-DELIVERY-TERRITORY · r1]

## 목표

행정동을 배달 운영의 최소 공간 셀로 두고, 서버 관리자가 여러 행정동을 하나의 `배달운영권역`으로 묶거나 제외할 수 있게 한다. 역세권 1km 디오라마는 관찰용 화면 창으로 유지하고 배달권의 권위 경계로 사용하지 않는다.

이번 첫 구현은 운영 원장과 행정동 디오라마 준비 상태 조회를 만든다. 기존 주문·배차 판정과 Unity 장면에는 영향을 주지 않는다.

## 지금·여기·나·너·이렇게

- 지금: 주소·좌표 판정용 기존 `플랫폼배달권`이 있고, 별도 과거 검증 기록에는 면목제3·8동의 검토용 행정동 디오라마 투영이 있다. 현재 MongoDB의 게시 여부는 환경마다 다시 조회하며 없으면 대기 상태로 둔다.
- 여기: 중곡동·면목동·상봉동·중화동·망우동·신내동·묵동·이문동·휘경동·전농동·장안동·답십리동을 첫 후보 범위로 삼는다.
- 나: 서버 관리자다.
- 너: 행정안전부 코드 판본으로 확인된 행정동 운영 셀과 그 셀을 묶은 배달운영권역이다.
- 이렇게: 공식 법정동-행정동 관할 자료를 읽고 후보 셀을 조회한 뒤, 낙관적 revision과 멱등 요청 키를 사용하여 Draft 권역의 포함 셀을 교체한다.

## 확정

### 공간·권위 계층

1. 법정동은 첫 후보 범위를 고르는 공식 코드 교차표의 축이다. 이름만으로 지역을 찾지 않고 10자리 법정동 코드를 사용한다.
2. 행정동은 배달운영권역을 구성하는 최소 셀이다. 같은 시점의 한 행정동은 하나의 현행 배달운영권역에만 포함할 수 있다.
3. 배달운영권역은 여러 행정동을 묶는 서버 RDB 원장이다. 이번 판본은 `Draft`만 만들며 주문 노출·배차·정산에 직접 사용하지 않는다.
4. 협력권역은 여러 배달운영권역의 협력 관계다. 원 픽업 기사가 끝까지 운송하는 원칙과 직선거리 6km 주문 상한을 보존하되 이번 구현 범위에서는 코드로 연결하지 않는다.
5. 기존 `플랫폼배달권`과 음식 `food-cell:v1`은 호환 판정·실행 자료로 유지한다. 새 원장이 이를 덮어쓰거나 키를 바꾸지 않는다.
6. 역세권 1km 모듈은 행정동 여러 개를 가로지를 수 있는 관찰용 View preset이다. 행정동마다 Scene이나 전용 클래스를 만들지 않고 자료 기반 모듈 프로필을 사용한다.

### 첫 공식 후보 범위

사용자 발화의 `중국동`은 이어진 `중곡1동` 언급과 공식 지명을 근거로 광진구 법정동 `중곡동`으로 정규화한다. 이 해석도 이름 검색이 아니라 아래 코드 범위로 고정한다.

| 법정동 | 법정동 고유 식별자 | 행정동 수 |
| --- | --- | ---: |
| 중곡동 | `region:kr:bjd:1121510100` | 4 |
| 전농동 | `region:kr:bjd:1123010400` | 2 |
| 답십리동 | `region:kr:bjd:1123010500` | 2 |
| 장안동 | `region:kr:bjd:1123010600` | 2 |
| 휘경동 | `region:kr:bjd:1123010900` | 2 |
| 이문동 | `region:kr:bjd:1123011000` | 2 |
| 면목동 | `region:kr:bjd:1126010100` | 6 |
| 상봉동 | `region:kr:bjd:1126010200` | 2 |
| 중화동 | `region:kr:bjd:1126010300` | 2 |
| 묵동 | `region:kr:bjd:1126010400` | 2 |
| 망우동 | `region:kr:bjd:1126010500` | 2 |
| 신내동 | `region:kr:bjd:1126010600` | 2 |

합계 30개 행정동은 로컬 RDB의 행정안전부 `mois-resident-registration-codes / korea-administrative-legal-jurisdictions` 동결 파일 `jscode20260301.zip`, SourceVersion `mois-jscode:20260301:retrieved:2026-08-12`, 자료 revision `mois-hjd-bjd-20260301-8af8c1f122d67d43518f`에서 해소한다. 이 `r1` 범위는 이후 더 최신 자료가 들어와도 조용히 바뀌지 않으며, 고정 판본이 없거나 다른 판본이 섞이면 후보를 만들지 않는다.

### 원장과 상태

- `배달운영권역`: 안정 식별자, 표시명, `Draft` 상태, revision, 생성·수정 시각을 가진다.
- `배달운영권역행정동Membership`: 행정동/법정동 고유 식별자, 공식 자료 revision, 표시명 사본, `Included/Excluded`, 포함·제외 시각을 가진다.
- `배달운영권역CommandReceipt`: `ClientRequestId`, 요청 hash, 관리자 행위자, 결과 revision·응답 사본을 보존해 같은 요청을 재실행하지 않고 나중 재시도에도 당시 결과를 반환한다.
- `배달운영권역변경Outbox`: 같은 aggregate revision의 전체 행정동 집합과 관리자 행위자를 후속 투영에 전달할 수 있게 남긴다. 이 revision별 사본이 반복 제외·재편입의 시간축을 보존하며 이번 판본은 발행기를 켜지 않는다.
- 행정동의 코드 관할 확인과 디오라마 투영 준비는 서로 다른 사실이다. 코드 관할이 확인되어도 Mongo manifest가 없으면 `WaitingForSpatialProjection`으로 표시한다.
- Mongo manifest가 있으면 그 manifest의 `ReadinessCode`, `sourceVintage`, `projectionHash`, 공개 승인 여부를 그대로 보여 주며 운영 원장이 임의로 승격하지 않는다.

### 관리자 API

- `GET api/v1/admin/delivery-territories/source-scopes/delivery-territory-source-scope:northeast-seoul-rider.r1/administrative-dong-modules`
- `GET api/v1/admin/delivery-territories`
- `GET api/v1/admin/delivery-territories/{deliveryTerritoryStableId}`
- `POST api/v1/admin/delivery-territories`
- `PUT api/v1/admin/delivery-territories/{deliveryTerritoryStableId}/administrative-dongs`

쓰기 API는 `서버관리자전용`, `ClientRequestId`, `ExpectedRevision` 경계를 적용한다. 다른 권역에 포함된 행정동, 공식 후보 범위 밖 행정동, 중복 행정동, 낮은 revision은 거절한다.

## 구현 범위

- RDB aggregate·membership·멱등 수신증·Outbox와 EF Core 구성·migration.
- 공식 관할 자료를 읽는 첫 후보 범위 조회.
- Mongo 행정동 디오라마 manifest 유무와 판본을 합성한 읽기 모델.
- Draft 권역 생성·멤버 교체·목록·상세 관리자 API.
- 단위·Application·Controller 계약 시험과 로컬 MySQL 재조회 검증.

## 제외

- 배달운영권역 `Active` 전환과 운영 담당자 배정.
- 협력권역 원장과 권역 간 지원 규칙.
- 음식점 6km 노출/주문 차단과 기존 배차 알고리즘 변경.
- 주문·배차·정산 원장에 새 권역 키를 자동 투영하는 작업.
- 행정동 경계·건물·도로·주소를 추정하거나 새 Mongo 디오라마를 게시하는 작업.
- Unity 계약·Client·Scene·Prefab·Game View 변경.

## 검증 및 완료 조건

- 공식 동결 판본에서 정확히 12개 법정동과 30개 활성 행정동이 결정적으로 해소된다.
- 이름이 같은 타 지역 법정동은 코드 범위 밖이므로 포함되지 않는다.
- 같은 `ClientRequestId`와 같은 payload를 다시 보내도 revision·membership·Outbox가 늘지 않으며 이후 aggregate가 바뀌어도 최초 요청 당시 응답을 반환한다.
- 같은 `ClientRequestId`의 다른 payload, 낮은 `ExpectedRevision`, 다른 Draft 권역과의 행정동 중복이 거절된다.
- 제외한 행정동의 과거 membership은 `Excluded`로 남고 현행 유일성 키만 해제된다.
- 디오라마 manifest가 없는 행정동은 `WaitingForSpatialProjection`이며 임의 경계·타일을 만들지 않는다.
- 새 원장 쓰기가 기존 `플랫폼배달권`, 음식 주문, 배차 결과에 영향을 주지 않는다.
- 실제 로컬 MySQL에 Draft 권역을 저장하고 새 DbContext에서 독립 재조회하며 검증 자료는 종료 뒤 제거하거나 전용 검증 식별자로 격리한다.

## 검증 상한

이번 판본의 상한은 `운영 RDB Draft 관리 + 공식 행정동 후보 조회 + 디오라마 준비 상태 조회`다. 실제 운영 활성, 주문 가능 지역, 배차 권위, 협력권역, Unity 표현, 실제 Game View는 완료로 주장하지 않는다.

## 다음 질문 하나

첫 실제 배달운영권역을 어떤 행정동 묶음으로 활성화할지는 후속 기획으로 남긴다. 추천은 면목제3·8동을 포함한 작은 Draft를 먼저 만들고 경계·주소 판정과 담당자 배정을 검증한 뒤 인접 행정동을 추가하는 방식이다. 한 번에 30개를 하나의 운영권역으로 활성화하면 관리 단위가 너무 커지고 공간 결손을 찾기 어렵다는 대가가 있다.
