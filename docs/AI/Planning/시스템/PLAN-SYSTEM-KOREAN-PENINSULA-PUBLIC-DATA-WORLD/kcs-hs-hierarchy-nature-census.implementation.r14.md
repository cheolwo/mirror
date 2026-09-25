# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r14]

## 범위

HS 코드별 소매 대표 상품 조사를 장기간 반복할 수 있도록 관세청 2026 HS 부호 계층 총대장과 HSK 성질별 분류를 공식 원본 그대로 동결하고, 기존 로컬 공공자료 MySQL 원장에 비공개 검토 자료로 적재했다. 개별 상품의 법적 세번 확정, 쿠팡 판매 가능성 판정, 외부 게시와 Unity 투영은 포함하지 않는다.

## 공식 원본과 이용 경계

- [관세청 HS부호](https://www.data.go.kr/data/15049722/fileData.do): `HS코드(2026).xlsx`, 1,419,595 bytes, SHA-256 `020661f75ac044be0eec049013b074be77ca8fb6d13f8283666f1f3e594054e5`.
- [관세청 HSK별 신성질별·성질별 분류](https://www.data.go.kr/data/15049720/fileData.do): `HSK별 신성질별_성질별 분류(2025년_2026년).xlsx`, 2,581,321 bytes, SHA-256 `81323474ff803ccd2b0937079a0c258144c7e9e8b74aaf3efb37d14e81a7baa9`.
- 두 자료는 공공누리 제1유형 출처표시 자료다. 현재 저장은 `distributionApproved=false`인 로컬 비공개 검토 원장이고 외부 배포 승인이 아니다.
- 직접 다운로드 URL은 이번 수집 receipt에 동결했다. 장기 정본은 위 데이터셋 페이지이며, 포털의 파일 식별자가 바뀌면 길이·hash·스키마 검증 없이 새 원본으로 교체하지 않는다.

## 구현

`관세청Hs분류전수조사`는 다음 명령을 제공한다.

```powershell
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-acquire .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-self-test .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-apply .
dotnet run --project eng/Ssalddel.PublicDataPortalImport/Ssalddel.PublicDataPortalImport.csproj -- kcs-hs-census-verify .
```

XLSX는 새 외부 패키지 없이 관계 기반 Open XML 파서로 읽는다. 정확한 시트명과 헤더, sparse cell, shared string, Excel 날짜 일련번호를 검증하며 400행 단위로 기존 `public_data_normalized_records`에 저장한다. 원본은 기존 원본 사본 등록 서비스로 hash·길이·비공개 위치를 결속하고, MySQL 잠금·transaction·독립 문맥 재조회·동일 입력 중복 방지를 적용한다.

## 저장 결과

| 자료 | 저장 행 | 의미 |
| --- | ---: | --- |
| HS 부호 계층 총대장 | 12,469 | 7단위 52, 8단위 918, 9단위 172, 말단 10단위 11,327 |
| HSK 2026 성질별 분류 | 11,327 | 말단 HSK10과 정확히 같은 코드 집합 |
| 합계 | 23,796 | 기존 원장 재사용, 새 migration 없음 |

말단 HSK10의 고유 HS6 접두는 `5,613개`다. UN H6 통계 특수코드 `999999`와 한국 국내 특수 `242400 / 2424000000 이사화물`은 서로 다른 계보로 유지한다. 국내 특수행을 WCO 법정 HS6로 승격하지 않는다.

성질별 대분류는 `소비재 3,261 / 원자재 5,183 / 자본재 2,883`이다. 이는 조사 우선순위 보조값일 뿐 `소비재 = 쿠팡 판매 가능`, 인기 상품, 개별 상품 HS 확정을 뜻하지 않는다.

## 검증과 상한

- 실행기 build: 경고 0, 오류 0.
- 자체검사 18개 통과, 최대 `TextValue` 1,553자로 현행 2,000자 제한 이내.
- 최초 MySQL 적용: 신규 23,796, 수정 0.
- 새 DB 문맥 독립 재조회: 23,796행과 두 원본 hash 일치.
- 동일 입력 반복 적용: 신규 0, 수정 0, 기존 23,796.

현재 반복 상품 배치의 대표 상품 후보는 법정 HS6 `20 / 5,612`에만 있다. 다음 단계는 관세청 분류를 이용해 소비재·혼합·비소매 후보를 나눈 뒤, 쿠팡 공개 검색 관찰을 작은 배치로 반복하는 것이다. 접근 제한은 우회하지 않고, 검색 결과 없음과 채널 부적합도 정상 조사 결과로 남긴다. 읽기 API, Unity 투영·Game View, 외부 게시, commit·push는 수행하지 않았다.
