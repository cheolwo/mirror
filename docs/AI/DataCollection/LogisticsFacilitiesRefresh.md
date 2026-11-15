# 국내 물류시설 정기 수집·DB 갱신

2026-11-14 사용자가 물류센터/창고 자료를 주기적으로 수집하도록 예약 설정을 요청했다. [실제 최초 수집/DB 저장](Research/2026-11-14-logistics-facilities-db-intake.md)을 기준선으로 채택하고, 검증한 NLIC 공개 원본3종만 갱신 대상으로 둔다. 공통 시설 수집의 blocked 항목 전체를 해제하는 승인이 아니다.

## 대상과 주기

| 예약 | 대상 | 한국 시각 | 최초 채택 기준선 |
| --- | --- | --- | --- |
| 등록창고 월간 갱신 | NLIC 전국 등록창고 | 매월1일10:30 | 2026-11-13 취득6011행, 2026-11 기간 |
| 단지·터미널 분기 갱신 | NLIC 물류단지/터미널 | 1·4·7·10월1일10:45 | 2026-11-14 취득59/30행, 2026-Q4 기간 |

예약 실행은 같은 채팅으로 돌아오는 로컬 heartbeat를 사용한다. 2026-11-14 등록창고 `automation-15`, 단지·터미널 `automation-16`을 실제 생성하고 두 건 모두 `ACTIVE` 및 등록 설정을 확인했다. [정책](../../../eng/data-collection/logistics-facilities-refresh.jobs.json)과 [등록 검증 기록](../../../artifacts/local/logistics-facilities-intake-20261114/automation-registration.json)에 연결한다. 등록된 반복 규칙을 한국 시각으로 계산한 첫 예정은 **2026-12-01 10:30**, **2027-01-01 10:45**이다. 이는 미래 실행 영수증이나 수집 성공을 뜻하지 않는다.

최초 등록창고의 관측 기준일과 최신 현황은 확인되지 않았으며, 기준선은 수집 당시 파일이다. 11월14일 공개 조회 화면의 건수6013은 metadata 관찰로만 기록했다. 이 값으로 기존6011행을 추가 생성하거나 실제 새 원본을 취득했다고 표시하지 않았다. 다음 실제 취득에서 원문을 대조한다.

## 실행과 저장

저장소 루트에서 다음 명령을 사용한다.

```powershell
$facilityPy = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $facilityPy -B -X utf8 eng/data-collection/logistics_facility_refresh.py --group warehouses --dry-run
& $facilityPy -B -X utf8 eng/data-collection/logistics_facility_refresh.py --group hubs --dry-run
& $facilityPy -B -X utf8 eng/data-collection/logistics_facility_refresh.py --group warehouses
& $facilityPy -B -X utf8 eng/data-collection/logistics_facility_refresh.py --group hubs
```

- KST 달/분기 키와 직전 성공 확인으로부터 최소48시간 간격을 함께 확인한다. 실패 요청을 자동 재시도하지 않으며, 이 간격은 실패 시도 전체를48시간 봉쇄하는 장치가 아니다. 같은 기간의 성공 확인은 `not_due`이며, 기존 물류·무역의672시간 등 다른 source 정책을 섞지 않는다.
- `--dry-run`은 네트워크·상태 파일·DB를 바꾸지 않는다. 정상 실행의 OS 잠금은 두 그룹의 공통 상태/시설 DB 쓰기를 직렬화한다.
- 공개 HTTPS의 정해진 NLIC URL/POST만 사용한다. 리디렉션·자동 재시도·인증/키 탐색·임의 새 공급자 활성화는 없다. 그룹당 최대3요청, 원본당12MiB, 회차24MiB, 요청30초 상한이다.
- 등록창고는 공개 다운로드 페이지의 행수 설정을 확인한 뒤 해당 길이로 요청한다. 과거6011/현재표시6013을 다운로드 길이로 영구 고정하지 않는다. 실제 행수와 발견한 건수가 다르면 게시/DB 갱신을 중단한다.
- 원본 서명·시트/열명·행·문자열·수치 의미를 검증한다. 빈 결과·잘못된 헤더·부분 파일을 정상 갱신으로 처리하지 않는다.
- SHA가 같으면 `unchanged`다. 최초 원본 위치/취득시각/판본을 유지하고 시설 DB를 다시 쓰지 않는다. 확인 시각/기간과 관측 기준일은 구별한다.
- 바뀌면 `artifacts/local/data-collection-runs/logistics-facilities/`에 새 원본·영수증/manifest를 보존한다. 기존 파일/행을 덮지 않고 SQLite에 새 판본을 추가한다. 현재 조회와 전이력 조회를 구별한다.

시설 DB는 [최초 조회 DB](../../../artifacts/local/logistics-facilities-intake-20261114/logistics-facilities.sqlite)를 이어 쓴다. 새 source 판본은 원본 지문과 결속한 별도 source/snapshot 관계로 저장해, 과거 취득시각을 최신 시각으로 바꾸지 않는다. `facility_search`는 이력을 유지한다. 첫 변경 회차에 함께 생성하는 `facility_latest_search`는 출처별 최근 성공 취득 판본을 읽는 조회다. 최근 취득이 곧 최신 현장 확인이라는 뜻은 아니다. 본문이 과거 판본으로 되돌아오면 자동 최신화하지 않고 `historical_body_reappeared_requires_review`로 확인 필요 상태를 남긴다. 원문 기준일/좌표가 확인되지 않으면 계속 null이다.

## MySQL 계보 반입과 부분 성공 재개

새 원본에 대해 수집기가 기존 `observer-public-data-db.v1` 형식의 **reference 전용 manifest**와 지문을 준비한다. 예약 담당은 기존 [DB 도구](../../../eng/observer-public-data-db/README.md)로 다음을 순서대로 수행한다.

1. 반환된 manifest의 절대 경로와 실제 SHA를 확인하고 새 receipt 경로로 `preview`한다.
2. 일치한 입력만 `apply`, 별도 연결의 `verify`, 두 번째 `apply`의 `strictNoOp=true/추가쓰기0`을 확인한다.
3. 성공한 verify 영수증의 **저장소 상대 `artifacts/local/...` 경로**를 수집기의 `--ack-mysql-receipt`에 전달해 해당 pending만 해제한다. C# 도구의 입력 경로는 절대 경로이므로 두 명령의 경로 형식을 혼동하지 않는다.

receipt는 덮어쓰지 않는다. 도구가 없으면 현재 소스를 빌드하되 컨테이너 시작/재시작·migration·계정·권한·영구 설정을 바꾸지 않는다. 기존 `hongdal-mysql-1 / 127.0.0.1:13306 / hongdal_dev`의 공공자료 테이블만 사용한다.

SQLite 커밋 전에 복구 저널을 저장한다. 커밋 직후 영수증/상태 파일 저장이 중단되면 다음 실행에서 실제 DB·원본 지문을 대조해 HTTP 없이 pending을 복구한다. SQLite 저장 뒤 MySQL이 실패하면 그 입력의 pending manifest를 유지한다. 이후 `not_due`/`unchanged`라도 pending을 읽어 HTTP 재취득 없이 계보 반입을 재시도한다. verify 영수증의 대상/manifest 지문·검증 상태가 일치하기 전에는 전체 DB 갱신 완료로 보고하지 않는다. 데이터는 받았지만 MySQL이 미완료인 경우를 수집 실패나 전체 성공과 섞지 않는다.

기준일 없는 시설 기록을 기존 시계열 정규화 행에 넣기 위해 날짜를 만들지 않는다. 물리 시설 중복·실가동·가동률·거래 관계·지도 결속·게임팩/레이어의 준비도도 자동 승격하지 않는다.

## 기존 예약과 운영 경계

기존 공간/시설 주간 예약, 인구·이동, 가격·농수산, 물류·무역·항공·해운, 연구자료·USB/배달 예약은 유지한다. 별도 명시된 NLIC3종만 이번 주기로 허용하며 공통 jobs.json의 blocked와 기존 collector/원본을 변경하지 않는다. 새 수집원·활용신청형 API·DB 정규화/게임 반입은 자동 추가하지 않는다.

로컬 작업은 이 컴퓨터가 온라인이고 ChatGPT 앱이 열려 있어야 이어서 실행될 수 있다. [공식 실행 안내](https://learn.chatgpt.com/docs/dots/tasks-and-memory#assigned-work)

매 회차에는 changed/unchanged/not_due/blocked/failed, 실제 요청·저장·행수, 원본 지문, SQLite 판본, MySQL 미완료/검증 상태를 짧게 보고한다. 정기 등록·오프라인 시험·지금의 baseline 검증·미래 회차 성공은 서로 다른 사실이다.

## 현재 검증

오프라인 시험20개가 통과했다. 원본 변경/동일·잘못된 헤더/행수·트랜잭션 rollback·잠금·복구 저널·MySQL 검증 영수증 결속을 확인했다. 실제 기준선에서는 두 그룹의 dry-run과 정상 실행이 모두 HTTP0·시설 DB 추가쓰기0이며 정상 실행은 `not_due`였다. 기존6100관측 DB의 지문과 무결성/외래키가 유지됐고, XLS3종 전행 수6011/59/30을 다시 읽었다. [기준선 검증](../../../artifacts/local/logistics-facilities-intake-20261114/refresh-baseline-verification.json)과 등록 검증을 별도로 보존한다. 기존 예약21개의 설정 지문도 그대로다. 미래 회차의 실제 변경 자료 취득/반입은 아직 실행하지 않았다.
