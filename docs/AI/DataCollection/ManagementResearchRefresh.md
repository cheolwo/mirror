# 경영 연구 자료의 정기 갱신

2026-11-14 사용자 요청으로 [다섯 분야 실제 수집 결과](Research/2026-11-14-management-research-field-intake.md)의 갱신 경로를 등록한다. 수치 자료의 발표 주기와 예약이 확인하는 주기를 구분한다. [정책 대장](../../../eng/data-collection/research-refresh-policy.json)이 담당·간격·허용 범위를 소유한다.

## 예약과 담당

한국 시각 기준이다. 신규 두 예약과 기존 예약 한 건의 확장을 모두 ACTIVE로 등록했다.

| 자료 | 예약 | 담당 예약 | 처리 |
| --- | --- | --- | --- |
| 온라인쇼핑동향 | 매월 5일 10:20 | `automation-14` 신규 | 공식 목록에서 최신 월 발표 발견 → 글/첨부 확인 → 새 PDF 보관 |
| 한국 연평균환율·상품수출 | 매월 20일 09:40 | 기존 `automation-9` 확장 | 연간 두 지표의 최근 12개 반환값과 수정일 확인 |
| 광고·카페·배달 참고자료 | 1·4·7·10월 첫 월요일 14:00 | `automation-13` 신규 | 공식 본문·판본·신판 발표의 의미 있는 개정 확인 |

인구·상권/교통(매일09:20, CSV 최소648시간), 행정·공간(월요일13:10), 생산·가격(기존17:20), 배달/원가의 기존 담당·예약·보류 상태는 유지한다. 중단된 예약을 재개하거나 같은 수집을 복제하지 않았다.

## 수집과 저장

원본·구판은 보존한다. 초기 `management-research-data-20261114` 색인/SQLite를 덮지 않고 후속 회차는 아래 보관함에 원본·출처·기준일·취득시각·단위·SHA와 실행 결과를 남긴다. 온라인쇼핑 새 PDF의 수치표 추출과 운영 DB/게임 자동 반입은 이 예약에 포함하지 않는다.

| 담당 | 보관함 | 실행/검증 |
| --- | --- | --- |
| 온라인쇼핑 | `artifacts/local/data-collection-runs/online-shopping/` | [전용 수집기](../../../eng/data-collection/online_shopping_refresh.py) |
| 연간 환율·수출 | `artifacts/local/data-collection-runs/research-trade/` | [보충 manifest](../../../eng/data-collection/research-trade.jobs.json), [공통 수집기](../../../eng/data-collection/collect_public_snapshots.py) |
| 참고문서 개정 | `artifacts/local/data-collection-runs/research-reference-reviews/` | 예약 담당이 공식 원문 비교 후 실행시각별 원본·색인·영수증 저장 |

공통 OS 잠금을 사용한다. 저장·변경없음·주기 미도달·보류·실패를 구분하며, 거절/인증/CAPTCHA를 우회하거나 반복 재시도하지 않는다. 온라인쇼핑은 최대3요청·PDF16MiB·회차20MiB, 환율·수출은 2요청·합계512KiB, 분기 점검은 최대24요청·파일16MiB·회차32MiB다.

### 온라인쇼핑

공식 게시판의 월별 제목·공표일·글 번호와 해당 글의 PDF 첨부를 대조한다. 최소168시간 사이를 두고 확인한다. 기존 2026년9월 원본을 최신 주소로 고정해 매번 받지 않는다. 같은 발표는 PDF 요청을 생략하며, 새 발표/표시 개정 때만 해당 첨부를 확인한다.

매회 최신 관측월 한 발표가 대상이다. 놓친 과거 월의 자동 소급 수집·과거 관측월의 새 정정 발표와 URL·글/공표 메타데이터가 그대로인 무고지 PDF 교체 감시는 포함하지 않는다. 홈페이지 구조/기간·첨부의 모순이 있으면 실패로 남긴다. 새 파일을 저장했다는 사실은 수치표 검증 완료와 다르다.

### 연간 환율·수출

World Bank의 한국 `PA.NUS.FCRF`는 연평균 KRW/USD, `TX.VAL.MRCH.CD.WT`는 경상 USD 상품수출이다. 현재 환율·운임·개별 거래 금액이 아니다. `mrv=12&per_page=100`으로 확인하고 결측을 보간하지 않으며, 최근 반환 범위 밖의 과거 개정은 감시하지 않는다.

최소720시간(30일) 간격을 유지한다. 11월14일에 취득한 두 실제 원본의 SHA·시각을 초기 상태로 채택했으므로 11월20일에는 주기 미도달이 예상된다. 다음 예약상 실제 HTTP 확인 가능일은 12월20일이며, 짧은 달에는 다음 달 확인이 건너뛰어질 수도 있다. 초기 원본11연도를 새 `mrv=12` 응답으로 표시하지 않았다.

[초기 채택 도구](../../../eng/data-collection/research_trade_seed.py)는 정확한 원본2개만 한 번 연결하기 위한 도구다. 매회 재실행해 후속 상태를 덮지 않는다. 기존 물류 자료의 보류와 보충 두 지표의 결과는 각각 판정한다.

### 참고문서 개정

광고 정책·측정 안내, 특정 제조사 가이드·사양, 배달 분야 공식 신판 발표를 점검한다. 조회수·메뉴·스크립트 변화만으로 새 판본을 만들지 않는다. 과거2022 조사/2024 정책 PDF를 현재 관측으로 재분류하지 않는다. 공개 문서에서 실제 광고주 CPC·전환·자연검색 점수를 생성하지 않는다.

## 이용 조건 후속 확인

초기 온라인쇼핑 색인의 권리 미확인 기록은 동결된 취득 이력으로 보존했다. 이후 [해당 공식 글](https://www.mods.go.kr/board.es?mid=a10301010000&bid=241&act=view&list_no=447781)의 개별 제목을 지목한 본문에서 **공공누리 2유형(출처표시+상업적 이용금지)** 표시를 확인했다. 별도 AI 학습 문구도 원문대로 [후속 근거](../../../artifacts/local/data-collection-runs/online-shopping/runs/discovery-smoke-20261114/rights-metadata-followup.json)에 보존했다. 개별 첨부물의 적용 범위·게임 배포/상업 이용을 승인한 것으로 해석하지 않는다.

## 실제 확인과 실행 환경

앱의 영속 예약 설정을 다시 읽어 세 건의 ACTIVE·대상 채팅·일정을 대조했다. 기존 `automation-9`의 이름·일정·대상·알림 설정과 원래 지시문은 보존하고 보충 범위만 덧붙였다. [등록 검증](../../../artifacts/local/management-research-refresh-20261114/registration-check.json)에 다음 회차를 기록했다: 온라인쇼핑 **2026-12-05 10:20**, 기존 물류·무역 **2026-11-20 09:40**(새 두 지표는 최소간격에 따라 건너뜀), 참고문서 **2027-01-04 14:00**.

공통 수집기와 새 월간 수집기를 포함한 **오프라인107시험이 모두 통과**했다. [최종 시험/실제 실행 기록](../../../artifacts/local/management-research-refresh-20261114/validation.json)은 온라인쇼핑 dry-run·일반 실행의 `not_due`·HTTP0·PDF0을 확인한다. [무역 초기 실행 기록](../../../artifacts/local/data-collection-runs/research-trade/validation/local-smoke.json)도 두 소스 `not_due`·HTTP/DNS0이다. 초기 보관함93파일·원본31판본·SQLite의 재조회 무결성 검사는 계속 통과했다.

별도 [발견 스모크](../../../artifacts/local/data-collection-runs/online-shopping/runs/discovery-smoke-20261114/code-validation.json)는 공식 목록/본문 HTTP2회와 저장 본문 파서 대조를 확인했고 PDF를 다시 받지 않았다. 새 월 발표 PDF의 실제 향후 취득과 분기 점검의 예약 실행은 아직 일어나지 않았다. 등록 검증·모의 새 발표 시험·현재 원본 저장을 미래 회차 성공으로 합산하지 않는다.

[문서 검증](../../../artifacts/local/management-research-refresh-20261114/document-check.json)에서 새 안내·취득 보고·README 도입·현재 자료 snapshot의 로컬 연결과 UTF-8/AST/JSON/공백을 확인했다. 지정11경로의 Fast 검사와 `git diff --check`도 통과했다. Fast는 빌드/시험 생략 경로였으며 위107시험은 별도로 실제 실행했다.

로컬 예약이므로 이 컴퓨터가 켜져 있고 Codex 앱이 열려 있어야 실행된다. 네트워크·자료 발표·계정 접근 문제는 회차 영수증에서 확인한다. [공식 실행 안내](https://learn.chatgpt.com/docs/dots/tasks-and-memory#assigned-work)

수동 점검은 저장소 루트에서 다음과 같이 실행한다. `--dry-run`은 HTTP/저장 없이 현재 간격과 계획만 확인한다.

```powershell
$py = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $py -B -X utf8 eng/data-collection/online_shopping_refresh.py --dry-run
& $py -B -X utf8 eng/data-collection/collect_public_snapshots.py --manifest eng/data-collection/research-trade.jobs.json --job research-trade --dry-run
```
