# 지도 정밀화 공개자료 수집·정규화 — 2026-11-16

**후속 지형 검증 정정:** 서울·전국 확장 중, 원본 Point 격자를 다른 원점에 맞춘 `merge` 전처리에서 높이 위상이 달라지는 결함을 확인했다. 이 문서의 NASADEM **원본은 정상**이지만 기존 정규화 DEM·셀 표·동별 지형 통계는 소비 보류이며, 새 `terrain-grid-phase-correction-20261116-r2` 보정 결과를 사용한다. 앞선 지문/셀 수/최솟값·최댓값 재조회 통과는 원본 대비 좌표 위상 일치를 검증한 것이 아니었다. [확장·보정 현황](2026-11-16-seoul-national-diorama-input-readiness.md)을 따른다. 하천·건물·경계·대장 원본과 공간 색인은 영향이 없다.

사용자는 자료 신청을 모두 마쳤다고 알렸고, Codex에는 **새 신청 없이 받을 수 있는 자료 수집**을 요청했다. 도로 폭 적용과 면목 6개 동 재굽기는 **Claude가 진행 중**이다. 이 결과는 Claude의 [공통 지도 정밀화 계획](../../Handoffs/2026-11-16-Claude-개발계획-공통지도정밀화.md)의 W1·W2·W3·W5를 보완하는 자료 인계다. 게임 기획 승인·구현 인계의 범위를 새로 넓히지 않는다.

## 현재 기준과 이번 차이

- 실행판에서 쓰는 면목 6개 동과, 이미 준비된 [4구 65동 정밀 후보](../../Handoffs/2026-11-15-Codex-행정동별-디오라마-정밀모듈-r3.md)를 구분했다. 이번 자료 수량을 실제 게임 적용 수량으로 해석하지 않는다.
- 기존 r3의 2026-10-01 경계 후보가 이미 있었으므로 새로 만든 것처럼 세지 않았다. 현재 원격 판본을 확인하고 동일 바이트·도형을 재사용했다.
- 새 건축물대장과 지형·하천 원본은 실제 파일을 받았다. 기존 건물 도형 한 판본도 공식 다운로드로 다시 받아 바이트와 이용조건 근거를 대조했다.
- Unity 제품 코드·Resources·굽기 도구와 Claude 작업 파일은 수정하지 않았다. 기존 공용 DB·수집 정책·예약·Copernicus 내부 검토 상태도 유지했다. 별도의 수집용 SQLite와 검토용 출력만 만들었다.
- 주소 서비스의 **신청 완료는 사용자 보고**다. 승인·파일 수령은 이번 수집에서 확인하지 않았다. 로그인된 브이월드 공식 다운로드를 이용했으며 인증정보를 추출하거나 계정 설정·신청서를 변경하지 않았다.

## 실제 확보한 자료

| 묶음 | 실제 수집·정리 결과 | 기준과 한계 |
| --- | --- | --- |
| 건축물대장 표제부 | 전국 8,064,354행 원본, 서울 584,691행 추출, 4구 91,462행을 CSV·별도 SQLite로 저장 | 2026년 9월 자료. 대장 PK·주소·법정동·높이·층수·용도 등 공식 확인 27열 사용 |
| 행정동 경계 후보 | 전국 후보 3,559개 중 서울 427·인천 158·경기 602, 합계 1,187개 정리 | 2026-10-01 SGIS 기반 편집 후보. 공식 최신 행정동 원본을 대신하지 않음 |
| 건물 도형 재취득 | 서울 AL_D010 695,761행, 135,675,376바이트 | 2026-08-09 기존 원본과 SHA-256 일치. 새 건물 도형 695,761개를 추가한 것이 아님 |
| 하천 경계 | 전국 원본 140,273행. 권역 상자 14,274면, 동북권 역사 30동 범위 113면 추출 | 공식 원본 EPSG:5179. 포털 갱신일 2026-09-21이며 조사일·자료 기준일은 미확인 |
| 지형 NASADEM | 원본 2장, 약 24MB. 30m 격자로 권역 10,461,688셀·동북권 역사 30동 35,681셀 | 원관측 2000-02-11~21, 수직기준 EGM96. 최신 국토지리정보원 DEM이나 정밀 지표면 DTM으로 표시하지 않음 |

지형·하천의 **권역 상자**는 동경 126.55~127.75·북위 37.15~37.95다. 행정구역상 수도권 전체와 다르다. 동북권 마스크는 보존된 2023-10-31 역사 경계 30개 동이며, 2026 후보 65동 전체가 아니다. 65동의 현재 실행판 배치·신규 굽기는 이번에 하지 않았다.

## 건물 높이·층수

### Claude가 읽을 출력

- [4구 공식 확인 열 CSV](../../../../artifacts/local/public-data/map-precision-buildings-20261116-r1/normalized/building-title-2026-09-four-gu-verified-columns.csv)
- [4구 SQLite](../../../../artifacts/local/public-data/map-precision-buildings-20261116-r1/normalized/building-title-2026-09-four-gu.sqlite)
- [서울 전체 공식 확인 열 CSV.gz](../../../../artifacts/local/public-data/map-precision-buildings-20261116-r1/normalized/building-title-2026-09-seoul-verified-columns.csv.gz)
- [필드·원문 대조 검증](../../../../artifacts/local/public-data/map-precision-buildings-20261116-r1/receipt/regional-verification.json) · [높이 이상값 검토](../../../../artifacts/local/public-data/map-precision-buildings-20261116-r1/receipt/height-value-review.json)

| 구 | 대장 행 | 양수 높이 기록 | 양수 지상층수 기록 |
| --- | ---: | ---: | ---: |
| 광진구 | 24,505 | 17,347 | 24,419 |
| 동대문구 | 26,898 | 13,208 | 26,660 |
| 중랑구 | 27,282 | 13,278 | 27,083 |
| 노원구 | 12,777 | 7,632 | 12,504 |
| 합계 | **91,462** | **51,465** | **90,666** |

4구에서 높이 또는 지상층수 기록이 있는 행은 90,808개다. 이는 **값의 존재율**이며 정확도나 건물 도형과의 연결 성공률이 아니다. 지상 1층인데 높이가 7,100m·10,655m인 원문 2행은 검토 목록에 분리했다. SQLite의 원문·양수 파생값은 바꾸지 않았으므로 `heightM`만 읽어 자동 적용하지 않는다. 1,000m 초과는 이번 검토용 선별 기준이며 그 이하 값의 정확성을 인증하지 않는다.

`building_title`의 기본키는 `mgmBldrgstPk`다. `official_column_mapping`에 확인한 27개 원문 열, `source_metadata`에 기준월·원본·스키마 근거를 보관한다. 원문에 좌표와 25자리 건물관리번호는 없다. 대장 PK를 지도 건물의 고유번호로 바꾸어 쓰지 않는다.

`pnuCandidate`는 법정동 10자리·필지 종류·본번 4자리·부번 4자리로 만든 **19자리 필지 후보**다. 4구 91,309행에서 만들었고, 3,063필지에는 대장 행이 여럿이다. 같은 필지의 여러 동·주부속 건물과 시점 차이를 보존한다. 최근접 건물이나 가장 높은 값을 임의로 선택하지 않는다. 행정동 코드와 법정동 코드를 혼용하지 않는다.

### 기존 디오라마와의 필지 후보 대조

[연결 검토 영수증](../../../../artifacts/local/public-data/map-precision-enrichment-20261116-r1/receipt.json) · [건물별 후보 CSV.gz](../../../../artifacts/local/public-data/map-precision-enrichment-20261116-r1/building-candidates.csv.gz) · [동별 집계](../../../../artifacts/local/public-data/map-precision-enrichment-20261116-r1/scope-summary.csv) · [높이 차이 검토](../../../../artifacts/local/public-data/map-precision-enrichment-20261116-r1/height-value-crosscheck.json)

같은 필지의 건물 개수는 화면에 보이는 조각만 세지 않고 서울 원본 전체에서 셌다. 대장도 4구 전체의 같은 필지 행을 세고, **건물 1행·대장 1행·법정 주소/필지 일치·용도 일치**일 때만 `UniqueParcelCandidate`로 남겼다. 이 경우도 건물 고유번호 확정이나 현재 실제 높이 인증이 아니다. 기존 모듈이나 높이를 덮지 않는다.

| 비교 범위 | 원본 건물 | 필지 후보 | 추가 대장 높이 후보 | 상징 높이에서 개선 후보 |
| --- | ---: | ---: | ---: | ---: |
| r3 4구 65동 | 116,780 | 68,197 | **1,000** | 0 |
| 현재 실행판 면목 6동 | 13,383 | 10,128 | **161** | 0 |

추가 후보는 모두 기존에 층수 근거만 있던 자리다. 65동의 117,551개 동별 소속 조각은 고유 원본 116,780개로 중복을 제거했다. 여러 행정동에 걸친 건물의 동별 집계 합과 전체 고유 건물 수는 다를 수 있다. 면목 6동 수량은 실제 Unity 표현 색인을 별도로 읽었다. 보류한 `NoUniqueR3SourceRow` 2항목은 서울 원본 행 192156·191417을 찾았으나 r3 65동 검토 집합 밖인 경우다. 원본 파일 자체가 없다는 뜻이 아니다.

65동에서는 48,583개를 연결 보류했다. 필지에 복수 건물·복수 대장, 같은 필지 대장 없음, 용도 충돌·미확인 등이 이유다. 두 자료에 모두 양수 높이가 있는 38,090개 중 37,836개는 같고 254개는 다르며, **33개는 차이가 10m 이상**이다. 기존 유효 높이를 최신 값이라는 이유로 교체하지 않는다. 앞의 7,100m·10,655m 2행은 이번 대상 필지에 없었고 검토 목록은 그대로 보존했다.

재현 도구는 [필지·대장 높이 검토](../../../../eng/neighborhood/building_register_height_review.py)다. 기존 공간 패키지 경로를 추가해 실행한다. 입력 모듈/필지 판본·SHA·기존 SQLite를 확인하고 별도 검토 출력만 만든다. 새 연구 기획이나 Unity 구현을 수행하는 도구가 아니다.

## 경계 후보

[수도권 GeoJSON.gz](../../../../artifacts/local/public-data/map-precision-boundary-20261116-r1/normalized/capital-region.geojson.gz) · [동별 색인](../../../../artifacts/local/public-data/map-precision-boundary-20261116-r1/normalized/capital-region-index.csv) · [기존 범위 대조](../../../../artifacts/local/public-data/map-precision-boundary-20261116-r1/review/module-scope-comparison.csv) · [영수증](../../../../artifacts/local/public-data/map-precision-boundary-20261116-r1/receipt.json)

원격 `vuski/admdongkor@078ef4ac13b8bfeb215d2eabace49f4fdea71d5e`에서 최신 공개 후보가 `ver20261001`임을 확인했다. 전국 원본은 기존 r3의 검증된 동일 파일을 재사용했다. 1,187개 도형이 유효하며 기존 30동·65동 코드가 모두 대응한다. 65동 도형과 속성은 r3와 **65/65 동일**하다. 원본과 편집본의 이용조건을 별도로 보존했다.

서울 2023 경계와 같은 코드가 422개, 2023에만 있는 코드 3개, 후보에만 있는 코드 5개다. 동대문구의 옛 용신동과 신설동·용두동 등을 같은 이름으로 덮지 않는다. 2026-10-01은 편집 후보의 판본 날짜이며 공식 최신 경계 확인을 뜻하지 않는다. 신청한 공식 경계 원본이 도착하면 판본·코드·겹침·틈을 다시 비교한다.

## 지형·하천과 이용조건 근거

[지형 정규화 영수증](../../../../artifacts/local/public-data/map-precision-terrain-rights-20261116-r1/receipt/terrain-normalization.json) · [하천 정규화 영수증](../../../../artifacts/local/public-data/map-precision-terrain-rights-20261116-r1/receipt/water-normalization.json) · [이용조건·바이트 대조](../../../../artifacts/local/public-data/map-precision-terrain-rights-20261116-r1/receipt/rights-evidence.json) · [검증 결과](../../../../artifacts/local/public-data/map-precision-terrain-rights-20261116-r1/receipt/completion.json)

- 지형은 원본 경위도 1초 격자에서 수평 EPSG:5186·30m로 정규화했다. EGM96 높이를 별도 유지하며 지형 범위 내부 결측은 0이다. 건물별 높이·도로 종단 경사·정밀 출입구 단차를 확정할 자료가 아니다. 레이더 관측과 보간·표면 영향이 있다. 해안 부근 음수 높이도 일괄 0으로 바꾸지 않았다.
- 하천은 원본 EPSG:5179에서 GeoJSON 경도·위도 순서로 변환했다. 권역·동북권 추출 도형의 유효성 검사는 통과했다. 하천 경계가 현재 수면·보행 가능 영역·홍수 위험을 확정하지 않는다.
- 기존 서울 건물 ZIP과 공식 재취득 ZIP의 SHA-256은 `674c5a9583996dd6b8946525edad8197be79a634f1db00d39e2b3133d0d2a755`로 같다. 실제 `.prj`는 EPSG:5186 정의를 사용하며 페이지의 과거 Bessel 설명과 구분했다.
- 공공데이터포털의 현재 공공누리 1유형과 브이월드의 출처표시 조건을 같은 판본 재취득 근거에 연결했다. 기존 `RightsConflictUnresolved` 영수증을 수정하거나 제품 배포 승인을 자동 부여하지 않았다.
- NASADEM 이용조건·가이드, 하천 CC BY 2.0 KR 출처를 보존했다. 모든 묶음의 `distributionApproved`는 `false`다.

## 검증과 후속 경계

[수집 묶음 색인](../../../../artifacts/local/public-data/map-precision-intake-20261116-r1/intake-catalog.json)에 네 묶음의 영수증 경로·SHA-256과 담당·적용 상태를 모았다. 파일을 합치거나 공용 DB에 반입한 색인이 아니다.

[통합 독립 재조회](../../../../artifacts/local/public-data/map-precision-intake-20261116-r1/independent-verification.json)는 **87개 파일의 지문과 10개 검증 묶음**을 통과했다. 건축물 ZIP의 전체 CRC·77필드 검사는 수집 영수증에 있고, 서울 584,691행의 확인 열은 원문과 전수 대조했다. 4구 CSV↔읽기 전용 SQLite 91,462행·27열 전수 대조, 기본키·범위·이상값 목록도 별도로 확인했다. 경계 도형, 래스터의 좌표계·해상도·셀 수·최솟값/최댓값, 하천 도형을 실제 재조회했다.

재현은 저장소 루트에서 기존 Python과 보존된 공간 패키지 경로를 사용하는 [검증 스크립트](../../../../artifacts/local/public-data/map-precision-intake-20261116-r1/scripts/verify_intake.py)로 한다. 입력을 변경하지 않으며 같은 출력 보고서만 갱신한다. 이 검증은 Unity 빌드·Runtime·Game View·주소 정확성·외부 배포 검증을 대신하지 않는다.

다음 소비 단계는 Claude의 현재 도로 폭·6동 굽기를 보존한 상태에서, 한 동의 건물 높이 후보와 지형·하천 표현을 따로 비교하는 것이다. 차선에서 잰 폭은 차선 사이의 폭으로 실제 도로 외곽보다 좁을 수 있다. 미대응 차선 약 84km도 검증된 골목 도로망으로 자동 바꾸지 않는다. 이번에 도로 폭·골목·건물 원문을 덮거나 지도별 기획을 바꾸지 않았다.

국토지리정보원 DEM 공개 안내는 확인했지만 실제 원본은 이번에 받지 않았다. 신청한 주소 전자지도·구역 도형·출입구·내비게이션 DB의 승인·수령 파일은 후속이다. 기존 정기 수집과 다른 담당의 예약을 복제하거나 새로 만들지 않았다.

## 디오라마 증거 점검

기존 `mixed-spatial-layers-preserve-source-time-crs-and-authority` 후보와 `missing-coverage-remains-explicit` 후보를 지지하는 관측을 남긴다. 서로 다른 경계 시점·격자 해상도·좌표계·대장 이상값을 동일한 최신 사실로 합치지 않았다. 기존 후보의 반례·무효화는 발견하지 않았고 공통 승격이나 E 단계 변경은 없다. **새 디오라마 규칙 후보 없음.**

## 공식·원천 링크

- [건축HUB 표제부 제공 목록](https://www.hub.go.kr/portal/opn/lps/idx-lgcpt-pvsn-srvc-list.do?opnLgcptTaskSeCd=03&srvcNm=%ED%91%9C%EC%A0%9C%EB%B6%80) · [공공데이터포털 표제부](https://www.data.go.kr/data/15044720/fileData.do)
- [공공데이터포털 GIS 건물 20260809](https://www.data.go.kr/data/15083092/fileData.do) · [브이월드 서울 해당 판본](https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=NA&dsId=18&startDate=2026-08-09&endDate=2026-08-09&sidoCd=11&fileGbnCd=AL&datPageSize=100)
- [브이월드 하천 경계](https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=MK&dsId=30248)
- [NASADEM 원천 DOI](https://doi.org/10.5067/MEaSUREs/NASADEM/NASADEM_HGT.001) · [실제 취득한 OpenTopography 배포 안내](https://portal.opentopography.org/datasetMetadata?otCollectionID=OT.032021.4326.2)
- [행정동 편집 후보 고정 판본](https://github.com/vuski/admdongkor/tree/078ef4ac13b8bfeb215d2eabace49f4fdea71d5e/ver20261001)
