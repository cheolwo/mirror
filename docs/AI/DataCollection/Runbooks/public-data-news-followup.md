# 공공데이터 수집과 동시기 기사 연결

가격을 갱신하는 한 번의 실행으로 기사 후보까지 받는다. 장터에서 가격을 펼치면 원표의 품목·지역·유통 단계·관측 기간·단위를 먼저 읽고, 확인된 연결만 「이 시기 기사」에 표시할 수 있다. 첫 적용은 KAMIS 전국 가을배추 상품 소매 월평균이다. [이전 배추 실물 사례](../Research/2026-11-25-cabbage-price-news-index.md)

## 수집에서 조회까지

| 단계 | 기준 | 남기는 것 |
| --- | --- | --- |
| 관측 확인 | 현재 월, 전국 범위, 가을배추 상품 소매 1포기를 원표에서 확인 | 원본 SHA-256, 원표 주소, 조회 시각, 기준월, 가격과 전월 평균 |
| 후보 수집 | 관측월 시작과 조회일 14일 전 중 늦은 날부터 조회일까지 후보를 찾음. 날짜는 한국 표준시 | 농식품부 RSS의 게시일 힌트, GDELT 색인 시각, 기사 제목과 원문 주소 |
| 관계 검토 | 원문 발행일과 본문 속 조사·취재·발표 날짜를 각각 확인. 월중 조회 시점 이후 관측과 다른 달의 사건은 검토에서 제외 | 발행일, 본문 날짜 범위, 발행처, 관계 근거 |
| 관점 화면 인계 | 확인한 관계만 가격 관측 ID로 조회. 과거 날짜 재연에는 `available_from`을 기준으로 제한 | `v_news_context`와 `reviewed-news-cards.json` |

`period_end`는 기준월의 달력상 마지막 날이다. 원표의 실제 마지막 조사일은 제공되지 않아 화면 입력에 `actualLastObservationDate: null`을 둔다. `available_from`은 가격 조회일과 기사 발행일 중 늦은 날이며, 검토한 시각은 `reviewed_at`에 별도로 남긴다. 달력상 같은 기간이라도 주제나 조사 범위가 다른 기사는 그대로 후보로 남긴다. 이번 월중 표에는 `same_period_context`만 내보낸다. 다른 계열의 **같은 수치**를 연결할 때는 날짜·품종·등급·유통 단계·단위·지역·인용값을 모두 대조한다. [관계 근거 예시](../Research/2026-11-25-cabbage-price-news-index.md#기사와-근거의-대응)

## 수동 실행과 재실행

저장소 루트에서 Python 3.11 이상으로 실행한다.

```powershell
python -B -X utf8 eng/data-collection/cabbage_news_followup.py collect
python -B -X utf8 eng/data-collection/cabbage_news_followup.py review --review <검토한-기사-JSON>
```

기본 저장 위치는 `artifacts/local/public-data/hongdal-cabbage-news-followup/`이다. `--output`으로 별도 로컬 폴더를 지정할 수 있다. 실행 뒤 `article-candidates.json`과 SQLite의 `search`를 읽는다. 검토 입력은 `observationId`, `articleUrl`, `title`, `publisher`, `publishedOn`, `subjectFrom`, `subjectTo`, `evidenceNote`를 쓴다. 날짜는 YYYY-MM-DD로 적는다. 원문을 직접 찾아 확인한 경우에도 같은 입력을 쓸 수 있다. 같은 근거의 반복 등록은 한 관계로 남고, 같은 원문 주소에 다른 검토 내용이 들어오면 충돌을 알린다.

같은 날짜와 가격 조합은 기존 관측을 쓴다. 성공한 기사 조회는 6시간 동안 재사용한다. 일부 후보 출처가 실패하면 저장된 가격·후보·확인 관계를 유지하고, 실패한 출처의 다음 시도까지 5분을 둔다. 새로 실행한 날짜의 관측을 수집하며, 이전 날짜의 실패는 해당 관측에 남는다. 반환 코드 0은 실행 성공, 1은 자료 확인 또는 저장 실패, 2는 후보 출처의 일부 실패 또는 재시도 대기다.

기사 색인은 링크와 필요한 서지 정보, 독립 작성한 연결 근거를 보존한다. 화면에는 원문으로 이동하는 주소를 제공한다. 농식품부의 발행일 힌트와 GDELT가 발견한 날짜는 원문을 열어 확인한다. GDELT를 사용한 내보내기에는 프로젝트 이름과 링크를 남긴다. [농식품부 RSS](https://www.mafra.go.kr/home/5327/subview.do) · [GDELT DOC API](https://blog.gdeltproject.org/gdelt-doc-2-0-api-debuts/) · [GDELT 이용 조건](https://www.gdeltproject.org/about.html#termsofuse)

## 다른 공공데이터에 붙이는 지점

기존 공공데이터 작업으로 확대할 때는 수집 실행 전체의 성공 여부와 별도로 `sourceResults[]`의 출처별 완료를 본다. 실제 실행에서 저장한 데이터 원본의 SHA-256을 확인하고, 정규화한 관측 날짜·계열·지역·단위가 있는 자료를 연결한다. 원본의 부수적인 HTML 변경과 지표의 변경은 따로 판단한다. 정기 수집 프로그램이 새 관측값을 저장하는 지점에서 후속 검색을 실행하고, Unity는 확인한 카드만 읽는다. 현재는 배추 수동 실행에 붙어 있으며 [기존 수집 예약 상태](../README.md)가 적용된다.

## 2026년 11월 25일 시험 결과

전국 가을배추 상품 1포기의 KAMIS 월중 소매 평균 2,741원과 전월 평균 4,236원을 저장했다. 농식품부 RSS에서 후보 4건을 받았다. 그중 수급대책 발표 1건과 별도로 원문을 확인한 경향신문 현장 보도 1건을 시기 맥락으로 연결했다. GDELT는 첫 요청에 요청량 제한을 반환했고, 다음 요청은 시간 초과였다. 재실행에도 관측 1건과 관계 2건을 보존했다. 로컬 색인의 원본 지문·무결성·날짜별 조회와 저장소 수집 시험 200개를 확인했다.

[실제 기사 후보](../../../../artifacts/local/public-data/hongdal-news-followup-20261125/article-candidates.json) · [확인된 화면 입력](../../../../artifacts/local/public-data/hongdal-news-followup-20261125/reviewed-news-cards.json) · [실행 검증](../../../../artifacts/local/public-data/hongdal-news-followup-20261125/verification.json)
