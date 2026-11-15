# 네이버 키워드 API 조사·실제 수집 상태

2026-11-14 사용자가 게임 내 키워드 광고에 참고할 네이버 API를 조사하고 수집하도록 요청했다. **공식 명세·공지 취득은 완료했으나 실제 키워드 수치 취득은 인증 대기**다. 수집 준비와 관측 성공을 구분한다. [영속 근거 명세](2026-11-14-naver-keyword-api-intake.json)에 출처/실행 기록의 지문과 수집 상태를 남겼다. 기획 r176/후속 광고 기획이나 게임 규칙은 변경하지 않았다.

## 저장한 것과 실행한 것

[보관함](../../../../artifacts/local/naver-keyword-intake-20261114/README.md)에 새 공식 원문15개·4,288,979바이트를 저장하고 기존 데이터랩 참고 원문1개를 재다운로드 없이 참조했다. 기술 문서/공지와 JSON 명세이며15개 통계 데이터셋이 아니다. [통합 출처 색인](../../../../artifacts/local/naver-keyword-intake-20261114/source-catalog.json)에 URL·원문 지문·취득시각·자료 종류를 기록했다.

[13개 키워드 조회 계획](../../../../artifacts/local/naver-keyword-intake-20261114/query-plan.json)을 만들고 전용 수집기의 dry-run과 일반 실행을 했다. 양쪽 모두 DataLab3배치·검색광고3배치가 `credential_not_configured`로 보류됐다. **계정 API HTTP0·키워드 수치0행·광고 실적0행**이다. 일반 실행 종료코드2는 보류이며 수집 성공이 아니다.

[인증 존재 감사](../../../../artifacts/local/naver-keyword-intake-20261114/auth-audit.json)는 Process/User/Machine 환경변수 이름, Ssalddel의 최상위 appsettings와 프로젝트 User Secrets를 확인한 범위다. 전용 인증이 없고 기존 지도용 인증은 재사용하지 않았다. 다른 프로젝트/Windows 자격 보관함까지 전수 조사했다고 주장하지 않는다. 키·고객ID 값이나 인증 헤더는 보고/소스/영수증에 기록하지 않았다.

## API별 역할과 현재성

| 종류 | 공식 조회 | 얻는 것 | 게임 참고 후보 |
| --- | --- | --- | --- |
| 검색 추세 | API HUB `POST /search-trend/v1/search` | 기간/검색어 묶음별 상대 검색지수 | 계절·시기별 관심 변화 |
| 키워드 도구 | SearchAd `GET /keywordstool` | 연관검색어·PC/모바일 검색수·평균 광고지표·경쟁도 | 검색 주제 후보와 광고 시장 문맥 |
| 입찰 예상 | SearchAd `POST /estimate/*` | 입찰별 예상 노출/클릭/비용 등 | 향후 예산별 선택 비교; 이번 미호출 |
| 광고계정 실적 | SearchAd 통계 API | 권한 있는 광고주의 실적 | 별도 동의/범위의 향후 가져오기; 이번 미호출 |

[공식 이관 공지](https://developers.naver.com/notice/article/32530)와 [경과 조치](https://developers.naver.com/notice/article/32973)를 확인했다. 개발자센터 신규 신청은2026-07-31부터 API HUB로 전환됐고, 그 이전 신청자만2027-06-30까지 기존 창구를 사용할 수 있다. 기존 문서의 `openapi.naver.com/v1/datalab/search`를 신규 기준으로 활성화하지 않았다. 종료된 옛 NCP Search Trend 상품도 새 API HUB와 구별한다.

현재 신규 기준은 [API HUB 검색어 트렌드 명세](https://api.ncloud-docs.com/docs/naver-api-hub-search-trend)의 `https://naverapihub.apigw.ntruss.com/search-trend/v1/search`다. 전용 Client ID/Secret 헤더를 사용한다. [현행 개요](https://guide.ncloud-docs.com/docs/apihub-overview)는 한시 무료·검색어 트렌드 월50,000호출 등을 안내한다. 이는 구 개발자센터의 하루1,000회나 SearchAd의 한도가 아니다. 이번 자체 상한은 두 제공자 합계6요청·회차2MiB·응답256KiB다. 새 서비스 가입/요금제 변경은 하지 않았다.

SearchAd는 [공식 명세](https://naver.github.io/searchad-apidoc/assets/json/ncc-keywordstool.json)와 guide의 `https://api.searchad.naver.com`를 기준으로 한다. 일부 Swagger에 남은 오래된 host를 승계하지 않았다. 고정 일일/QPS 한도는 이번 공식 원문에서 확정하지 못했다.

## 조회 계획과 해석 경계

고유 키워드13개는 커피·아메리카노·카페라떼·디카페인커피·커피배달·치킨배달·도시락배달·면목동맛집·면목동카페·택배·풀필먼트·해외직구·키워드광고다. 연구용 수동 후보이며 인기순위/실제 구매검색어라고 표시하지 않는다.

DataLab은2025-11-01~2026-10-31 완결12개월·월 단위, 전체 기기/연령/성별을 계획했다. 최대5그룹씩3요청이고 비교 문맥의 `커피`가 반복돼15그룹 등장/고유13개다. 각 그룹은 단일 검색어를 쓴다. 검색어 배열 상한은 API 본문20개와 guide FAQ5개 설명이 달라 별도 보존했고 이번 단일 검색어 계획은 어느 쪽도 넘지 않는다.

상대지수100은 실제 검색100건이 아니다. 정규화 기준이 다른 요청/기간의 지수를 직접 합치거나 공통 키워드를 이유로 자동 재보정하지 않는다. 동명 검색어를 넣어도 해당 동 주민/방문자의 검색량이나 도로 유동인구가 되지 않는다.

검색광고 키워드 도구는5+5+3개로 계획했다. 검색수는 **최근30일 합계**, 평균 광고 클릭·CTR/depth는 **최근4주**다. `"<10"`은10미만 검열값으로 남기고0·10·중간값으로 바꾸지 않는다. CTR의 배율은 공식 명세에서 확정하지 못해 원값을 보존하고 확률/퍼센트로 자동 변환하지 않는다. `compIdx`는 PC광고 기준 경쟁도다. 평균 광고지표의0은 자료 없음도 포함하므로 무클릭 실측으로 단정하지 않는다. 배치별 중복 연관키워드도 합산하지 않는다.

키워드 통계에 실제 CPC/플레이스 자연검색 순위/매장 매출/전환율이 모두 들어 있다고 가정하지 않는다. 특히 계정 통계의 `salesAmt`는 공식 정의상 부가세 포함 광고비로, 매장 매출과 다르다.

## 게임에서 이용하는 순서

먼저 검색 주제/연관어를 광고 선택 후보로, 검색 추이를 시간별 관심 변화의 참고자료로 제공하는 안이 적합하다. 최근 검색수와 PC광고 경쟁도는 규모·혼잡 문맥으로 표시한다. 원본 판본과 기준기간을 게임 세션에 고정하고 실제 자료/가상 산식을 구분해야 한다.

광고 예산·입찰·매장 품질에 따른 가상 노출/클릭/주문, 실제 제조·배달 여력과 비용은 기존 Simulation의 승인된 규칙이 결정한다. 외부 검색수를 그대로 주문수로 쓰거나 광고비가 자연순위/매출을 보장하도록 만들지 않는다. 구체 가중치·환산율·CPC·연구 보상은 이번 자료 수집에서 확정하지 않았다.

## 인증·이용 조건과 재개

수집기에서 읽는 전용 환경변수 이름은 다음과 같다. 키 값은 채팅이나 추적 파일에 넣지 않는다.

- API HUB: `NAVER_API_HUB_CLIENT_ID`, `NAVER_API_HUB_CLIENT_SECRET`
- SearchAd: `NAVER_SEARCHAD_API_KEY`, `NAVER_SEARCHAD_SECRET_KEY`, `NAVER_SEARCHAD_CUSTOMER_ID`

[이용 조건 확인 범위](../../../../artifacts/local/naver-keyword-intake-20261114/apihub-reference/terms-and-pricing-findings.json)에 새 HUB 응답의 영구 저장/DB화·게임 배포 조건은 미확인으로 남겼다. 구 AI·Naver API 약관과 검색결과 API 특약을 새 DataLab에 확대 적용하지 않았다. API HUB의 수치 보관은 `numericPersistenceEnabled=false`로 보류하며, 조건 근거가 확인되면 담당이 정책을 명시적으로 갱신한다.

수집기/정책은 [naver_keyword_collect.py](../../../../eng/data-collection/naver_keyword_collect.py), [naver-keyword.jobs.json](../../../../eng/data-collection/naver-keyword.jobs.json)이다. 후속 저장은 `artifacts/local/data-collection-runs/naver-keywords/runs/<회차>/`의 공개 요청 계획·원문 JSON·정리 JSON·영수증으로 분리한다. 동일 요청은 최소168시간을 두고,401/403/429 뒤에는 권한 개선 확인 전 같은 제공자 요청을 중단한다. 기존 원본·DB·예약·게임은 바꾸지 않는다.

```powershell
$py = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $py -B -X utf8 eng/data-collection/naver_keyword_collect.py --dry-run
& $py -B -X utf8 eng/data-collection/naver_keyword_collect.py
```

## 검증

[최종 검증](../../../../artifacts/local/naver-keyword-intake-20261114/validation.json): 전체 수집기124시험(기존107+신규17)이 통과했다. 미인증 일반 실행의6보류·HTTP0·관측0을 실제 영수증으로 확인했다. [원문/문서 검증](../../../../artifacts/local/naver-keyword-intake-20261114/final-check.json)에서 원문16참조·영속 근거5파일의 지문과 JSON·UTF-8·AST·공백/로컬 연결을 대조했다. 지정7경로의 Fast/`git diff --check`도 통과했다. Fast 자체는 빌드/시험 생략 경로이며124시험은 별도로 실제 실행했다. 모의 응답 검증과 실제 인증 API 성공은 별개이며 아직 실제 수치 응답은 없다.

새 수치 예약·DB 쓰기·게임 코드/실행·광고 집행·commit/push는 하지 않았다. 정기 수집 설정의 기존 담당과 간격은 유지한다.
