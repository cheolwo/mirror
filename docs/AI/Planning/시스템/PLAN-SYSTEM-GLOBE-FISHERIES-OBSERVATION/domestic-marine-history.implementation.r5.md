# [기획 · 국내 해양 공공자료 · PLAN-SYSTEM-GLOBE-FISHERIES-OBSERVATION · r5]

## 확정 범위

2026-09-22 사용자 요청: 대한민국 공공데이터의 수온·수산물 수출입 정보를 수집하고, 갱신을 추적하며 DB까지 저장하는 파이프라인을 마련한다. [r4 NOAA 표본](temperature-replay.implementation.r4.md)을 삭제하지 않고 국내 자료 경로를 별도로 추가한다. 수온, 무역, 가상 어군은 서로 다른 자료다.

첫 수직 범위는 기존 로컬 MySQL에 **원본·수집 결과·값 판본을 보존하고 독립 재조회하는 수동 실행 경로**다. 정기 실행, 전수 수집, 공개 API·Unity 배포는 아직 켜지 않는다.

## 확인한 공식 자료

| 경로 | 실제 제공 의미 | 수집·변경 정책 |
| --- | --- | --- |
| [국립해양조사원 조위관측소 최신 관측데이터](https://www.data.go.kr/data/15155508/openapi.do) | 관측소, 위경도, 시각, 수온 등. 공공누리 제1유형 | 관측소 한 곳·하루·60분 간격·최대 24행 표본. 서비스 활용 승인 확인 필요. 정밀 수심이나 해역 전체 온도로 확대 해석하지 않음 |
| [관세청 품목별 국가별 수출입실적](https://www.data.go.kr/data/15100475/openapi.do) | 월·HS 코드·상대국별 순중량 kg, 수입 CIF·수출 FOB 미화 금액 | 공식 안내상 월별 현행화 때 과거 신고 정정·취하도 반영. 새 월 추가뿐 아니라 같은 월 재조회 필요 |
| [해양수산부 HSK 품목별 수출입 현황](https://www.data.go.kr/data/15102782/fileData.do) | 관세청 자료를 집계한 월별 HSK10 파일, 당월/당해누계 구분 | 2026-06 기준 파일 실제 내려받기 가능 확인. 이번 DB 실적은 관세청 API 표본만이며 이 파일은 후보로 유지. 두 공급원을 더해 중복 집계하지 않음 |
| [국립수산과학원 OpenAPI](https://www.nifs.go.kr/openApi/actionOpenapiInfoList.do) | 정점별 관측 및 어장환경 자료 | 별도 인증키가 필요. 공공데이터포털 공통키와 동일하다고 가정하지 않음 |

현재 표본은 `202606` 관세청 HS6 `030354/NO`, `030214/NO`, `030363/RU` 세 조합이다. 실제 API는 HS6 요청에도 HSK10 상세 행을 반환하므로 요청 코드의 접두어를 검증하고 실제 10자리 코드를 보존한다. 품목명은 원천을 보존하며 HS 통계만으로 생물종·서식지·어군 수를 판정하지 않는다. 수온 요청은 `DT_0001`, `20260921`이며 실제 정상 응답을 확인하기 전 값 매핑을 완료했다고 하지 않는다.

## 재사용과 변경 추적

- 기존 `HsCountryTradeUnitPriceLookupService`의 API·파라미터와 공공자료 저장 구조를 확인했다. 가격 비교 응답만으로는 수집 실패·원본·판본 이력을 충분히 보존하지 못하므로, 원본 수집 CLI와 별도 판본 정규화만 추가한다. 기존 가격 비교와 공통 최신값 저장 동작은 바꾸지 않는다.
- `public_data_raw_snapshots`: 원본 content hash와 비공개 파일 경로. 원문은 `artifacts/local/public-data/domestic-marine/<batchId>`에 보존한다.
- `public_data_ingestion_runs`: 공급원·조회 조합별 성공/실패, 확인 시각, 오류 코드, receipt 경로. 하나의 수온 실패가 다른 무역 수집을 실패로 바꾸지 않는다.
- `public_data_normalized_records`: `StableId`는 월·품목·상대국·지표의 식별자, `DataRevision`은 정규화 내용 hash, `RecordKey`에는 내용 판본을 포함한다. 기존 판본을 덮지 않는다.
- 같은 값 재수집은 새 값 판본을 만들지 않고 마지막 확인 시각만 전진시킨다. 같은 batch 재적용은 실행 기록도 중복 생성하지 않는다. 오래된 batch를 뒤늦게 적용해도 최근 확인 시각을 뒤로 돌리지 않는다.
- 새 값은 별도 판본으로 보존한다. 최신 조회는 같은 식별자에서 마지막 성공 확인 시각을 기준으로 한다. 실패·빈 응답을 0이나 삭제로 처리하지 않는다. 표본의 누락만으로 철회를 추론하지 않는다.
- 공공 통계의 보고기간과 수집일을 분리한다. 수입/수출과 kg/USD를 각각 저장하며 CIF/FOB 근거를 보존한다. 월별 실적과 합계 행을 중복 합산하지 않는다.
- 검토 상태는 `PendingHumanReview`, `PrivateReviewOnly`다. Unity는 향후 승인된 읽기 사본만 이용하며 Tick/카메라에서 공급원 API를 호출하지 않는다.

## 실행 및 소유 범위

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport -- domestic-marine-acquire . 202606 20260921
dotnet run --no-build --project eng/Ssalddel.PublicDataPortalImport -- domestic-marine-apply . <batchId>
dotnet run --no-build --project eng/Ssalddel.PublicDataPortalImport -- domestic-marine-verify . <batchId>
```

acquire는 요청마다 새 비공개 폴더에 원본·공식 metadata·receipt를 남긴다. API 키는 기존 비밀 저장소에서 읽으며 URL·응답·예외를 통해 키가 보관되지 않게 한다. 외부 HTTP 오류는 안전한 코드로만 남긴다. apply는 Compose·포트·DB 검증 후 기존 3개 테이블만 사용하며 migration·초기화·컨테이너 재생성은 하지 않는다.

현재 공유 로컬 컨테이너에서는 기존 `SSALDDEL_PUBLIC_DATA_LOCAL_CONNECTION` 환경변수로 `127.0.0.1:13306 / hongdal_dev`의 비root 연결을 프로세스 메모리에만 주입한다. 비밀값을 문서·명령 이력·출력에 적지 않는다. 비공개 원본 파일과 DB는 함께 보존해야 하며 DB만 백업하면 원문 재검증을 재현할 수 없다.

이번 소유: `국내해양통계RevisionNormalizer.cs`, 해당 시험, `국내해양자료Pipeline.cs`, 기존 `Program.cs`의 전용 분기, 이 기획과 목차·현재 상태. 다른 작업의 변경이나 이전 NOAA 경로는 보존한다.

## 검증 결과

2026-09-22 실제 실행:

- 정규화 집중 시험 **10/10 통과**. 같은 내용 hash, 숫자 표기 정규화, 수정값 별도 판본, 원천 식별자 불일치, HSK10 실제 응답, 빈 응답·잘못된 값·중복 거절을 검증했다.
- CLI build 성공(경고·오류 0). 서버/시험 build에는 이번 범위 밖 nullable 및 xUnit 경고가 있다.
- 8개 변경 경로 한정 Fast 통과: diff 검사, 서버·시험 project build, 집중 시험 10/10. 로그 `artifacts/local/validation/20260922-171024/`. 새 파일 후행 공백 0건과 기획 상대 링크도 확인했다.
- 실제 batch `20260922T080805-61216b99`: 관세청 3개 조회 성공, 국립해양조사원 1개 조회는 HTTP 403 `ServiceKeyNotRegistered`.
- 기존 로컬 `hongdal-mysql-1 / hongdal_dev`에 **통계 12행(3개 품목·국가 조합 × 수입/수출 중량/금액), 원본 3건, 조회 결과 4건**을 저장했다. 원본 등록 서비스의 자체 수집 기록은 별도이며 4건은 이번 batch의 명시적 시도 기록이다.
- 별도 DbContext 및 별도 CLI `verify`에서 12행과 4개 시도 상태를 대조했다. 같은 batch `apply` 재실행은 신규 판본 **0행**, 이력 총 **12행**으로 중복이 없었다.
- 최초 batch `20260922T080106-ac9af9ab`는 HS6/HSK10 차이 때문에 정규화가 중단된 로컬 조사 기록이다. 수정 후 새 batch로 성공했고 최초 실패 사본은 삭제하지 않았다.
- 실제 공급원의 통계 수정 발생은 관찰하지 않았다. 수정 판본 보존은 자동 시험으로 검증했으며 실제 DB 검증은 최초 저장·동일 batch 재처리다.
- 수온 관측값 저장은 **0행**이며 인증 실패만 저장했다. 추정값이나 무역 실적으로 수온을 대체하지 않았다.
- 시험 결과: `artifacts/local/validation/domestic-marine-20260922/domestic-marine-revision.trx`. 수집 근거: `artifacts/local/public-data/domestic-marine/20260922T080805-61216b99/receipt.json` 및 원본·metadata 파일.
- 정기 실행·외부 공개·Unity 연결·commit·push는 수행하지 않았다. 전체 서버 회귀 시험을 통과했다고 주장하지 않는다.

## 미정과 후속

1. 수온 API의 현재 서비스 활용 승인. 승인 뒤 실제 정상 응답으로 관측소·위경도·KST 시각·섭씨·결측·품질·페이지 완결성 매핑을 시험해야 한다. 현재는 접근 실패를 기록하는 경로다.
2. 정기 수집 운영: 수온의 조회 간격, 통계의 월간 갱신 및 최근 월 재조회 창, 요청 상한·보존 기간은 아직 확정하지 않았다. 자동 scheduler 기본 비활성.
3. 수정 이력 화면/공개 조회 API, 장기 감사/보존, 검토 승인, Unity 표시 사본은 후속 범위다. CLI DB 재조회와 클라이언트 연동을 혼동하지 않는다.

다음 결정은 수온 API 접근 준비 후 첫 관측 범위와 주기다. 신규 Scene·어군 규칙·E 자동 승격은 없다. 새 디오라마 규칙 후보 없음.
