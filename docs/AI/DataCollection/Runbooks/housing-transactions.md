# 아파트·빌라 실거래 수집과 연소득 비교

가계 입장에서 아파트와 빌라의 매매·전세·월세 실거래를 같은 지역·계약 기간·전용면적으로 비교한다. 빌라는 국토교통부 자료의 **연립·다세대**다. 단독·다가구와 오피스텔은 별도 주택 유형이다. 첫 수집 범위는 서울 중랑·광진·동대문·노원구다.

## 공식 출처와 호출 방법

| 주택 | 매매 API | 전월세 API |
| --- | --- | --- |
| 아파트 | [상세 15126468](https://www.data.go.kr/data/15126468/openapi.do) · [기본 15126469](https://www.data.go.kr/data/15126469/openapi.do) | [15126474](https://www.data.go.kr/data/15126474/openapi.do) |
| 연립·다세대 | [15126467](https://www.data.go.kr/data/15126467/openapi.do) | [15126473](https://www.data.go.kr/data/15126473/openapi.do) |
| 단독·다가구 | [15126465](https://www.data.go.kr/data/15126465/openapi.do) | [15126472](https://www.data.go.kr/data/15126472/openapi.do) |

전국 API는 `LAWD_CD`에 법정동 코드 앞 5자리, `DEAL_YMD`에 계약 연월 6자리, `serviceKey`에 해당 서비스의 활용 키를 넣는 XML 조회다. 위 포털에서 활용신청과 응답 명세를 확인할 수 있다. 상세 아파트와 연립·다세대 매매는 페이지 요청 항목을 명시한다. 모든 경로에서 실제 행을 `totalCount`와 비교해야 수집 완료를 판정할 수 있다. 아파트 기본·상세 매매는 범위가 겹치므로 첫 도입에서는 한 쪽을 쓴다.

이번 조회에서 기존 키로 정상 실거래 XML을 확인하지 못했다. 수집 도구는 국토교통부 **공개 자료제공 CSV**를 사용한다. 공식 페이지에서 내려받기 조건을 확인하고, 확인 응답의 건수와 파일의 행·주택 유형·지역·계약일을 대조한다. 계약일과 자료 조회일을 따로 보존한다. 국토교통부는 계약일 기준 거래가 조회 이후 추가·정정·해제될 수 있다고 안내하고, 외부 공개 시 신고일 기준 공식 통계 사용을 안내한다. 이 DB와 예시 조회는 PC에서 검토하는 자료다. [원천 정의](https://www.data.go.kr/data/3050988/fileData.do) · [조건별 자료제공과 유의사항](https://rt.molit.go.kr/pt/xls/xls.do?mobileAt=)

## 테이블과 갱신

| 테이블 | 연결과 용도 |
| --- | --- |
| `housing_region` | 보유 공식 코드판에서 시·도 → 시·군·구 → 법정동을 이은다. 거래 주소의 이름이 해당 구에서 유일하게 일치할 때 법정동 코드를 붙인다. |
| `housing_source` · `housing_snapshot` | 출처별 조회일·계약일 범위·지역·주택 유형·응답 건수·원본 SHA-256을 보존한다. 원본 파일 이름은 SHA-256이다. |
| `housing_transaction` | 거래 행을 원본 기록 및 지역 코드와 연결한다. 매매가·보증금·월세는 만원에서 원으로 변환한다. 해제일이 붙은 거래도 보존한다. |
| `housing_unpriced_record` | 거래 구분과 금액의 조합이 어긋나는 행은 원본의 구분·금액·행 순서·사유를 보존한다. 비교 조회에서는 제외한다. |
| `housing_collection_attempt` | 조회 성공과 실패를 남긴다. 새 범위가 완전하게 검증된 뒤 겹치는 기간을 해당 원본으로 교체한다. |
| `district_income_reference` | 국세청의 2024년 귀속 주소지별 근로소득 신고인원과 총급여를 구 코드로 연결한다. 기존 소득 DB를 읽어 연결한 참고값이다. |

`v_housing_effective_transaction`은 해제일이 없는 가격 행을 읽는다. 같은 날짜·가격·면적의 행이 여럿이면 공식 파일의 행 수를 보존한다. 같은 범위를 다시 받을 때는 유형과 구, 겹치는 계약일에 대해서만 새로 확인한 원본으로 바뀐다. 기간 밖의 행과 기존 원본 파일은 유지된다. 재조회로 최신 계약의 추가 신고나 해제를 반영할 수 있다. [지역 코드 원천과 소득 출처](../../Handoffs/2026-11-19-Codex-전국확장-공공자료-수집검증.md)

## 원하는 조건으로 조회

저장소 루트에서 실행한다. 기본 저장 위치는 `artifacts/local/public-data/hongdal-housing-transactions/`이다. 아래는 실제 저장한 이번 판을 읽는 명령이다. 지역 코드는 중랑 11260, 광진 11215, 동대문 11230, 노원 11350이다. 수집은 한 번에 서울 최대 8개 구, 최대 1년의 계약 범위를 지정할 수 있다.

```powershell
python -B -X utf8 eng/data-collection/housing_transactions.py --output artifacts/local/public-data/hongdal-housing-transactions-20261125 collect --start 2026-09-01 --end 2026-11-25
python -B -X utf8 eng/data-collection/housing_transactions.py --output artifacts/local/public-data/hongdal-housing-transactions-20261125 compare --district 11260 --start 2026-09-01 --end 2026-10-31 --area-min 40 --area-max 60 --income-krw 36000000
```

`--legal-dong 1126010100`을 추가하면 중랑구 법정동 면목동으로 좁힌다. `--income-krw`는 입력한 세전 연소득이며 생략하면 금액과 표본 수만 표시한다. `--savings-krw`는 매매가 또는 보증금 중앙값에 대한 단순 현금 차이를 보여 준다. 입력값은 DB에 저장하지 않는다. 월세는 연간 월세를 연소득으로 나눈 비율과 보증금을 각각 표시한다. 보증금·월세 중앙값은 각각 계산하므로, 실제 한 계약의 조합을 보려면 같이 나온 대표 관측을 사용한다. 계약 범위가 수집됐는지는 `coverage`로 확인한다. 가구 PIR·RIR과 지역 근로소득 평균은 모집단과 계산이 다른 참고 지표다. [국토교통부 주거실태조사](https://www.molit.go.kr/USR/NEWS/m_71/dtl.jsp?id=95092607)

## 2026년 11월 25일 첫 수집

9월 1일~11월 25일 계약을 4개 구·4개 자료 구분으로 수집했다. 공식 응답 16개에서 **13,096행**을 확인했다. 해제 매매 66행과 월세 구분에 월세 0원이 적힌 1행을 빼면 비교 가능한 거래는 **13,029행**이다. 가격 행은 28개 법정동에 연결됐다. 11월 등 최근 계약은 추가 신고와 해제를 반영해 다시 조회할 수 있다. 기존 공공자료 예약 중지 상태를 유지한다.

[로컬 SQLite](../../../../artifacts/local/public-data/hongdal-housing-transactions-20261125/housing-transactions.sqlite) · [중랑구 개인 연소득 예시](../../../../artifacts/local/public-data/hongdal-housing-transactions-20261125/jungnang-income-example.json) · [수집 기록](../../../../artifacts/local/public-data/hongdal-housing-transactions-20261125/last-collection.json) · [검증 기록](../../../../artifacts/local/public-data/hongdal-housing-transactions-20261125/verification.json)
