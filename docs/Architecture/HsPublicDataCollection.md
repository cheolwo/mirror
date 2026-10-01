# HS 공공데이터 수집 모듈

## 목적

홍달은 HS 코드 정보를 우선 커뮤니티의 공동구매·공동수입 검토 자료로 제공합니다. 이 모듈의 응답은 품목분류, 수입요건 충족, 통관 가능성 또는 주선 계약을 확정하지 않습니다.

## 공개 API

```http
GET /api/v1/customs/hs-codes/{hsCode}/public-data
```

주요 query parameter:

- `countryCode`: ISO 2자리 국가부호, 기본값 `CN`
- `referenceMonth`: 수입실적 기준월 `yyyyMM`
- `lookbackMonths`: 수입실적 조회 개월 수, 1~12
- `referenceDate`: 관세환율 기준일 `yyyyMMdd`
- `expectedFxRateKrwPerUsd`: 수입단가의 원화 참고값 계산에 사용할 선택 환율
- `sourceKeys`: 특정 출처만 조회할 때 반복해서 전달하는 출처 키

컨트롤러는 기존 `공동수입HS코드조회UseCase`를 호출합니다. 수집기는 서로 독립적으로 실행되며, 한 외부 API가 실패해도 다른 출처의 결과는 유지됩니다.

성공, 데이터 없음, 적용 안 됨 결과는 동일 조건에서 30분간 메모리에 캐시합니다. 인증키 누락과 외부 오류는 캐시하지 않습니다.

## 수집기

| 출처 키 | 공공데이터 | 용도 |
| --- | --- | --- |
| `customs-import-unit-price` | 관세청 품목별 국가별 수출입실적 | 수입금액/중량 가중평균 CIF 단가 |
| `customs-confirmation-requirements` | 관세청 세관장확인대상물품 | 10자리 HSK 기준 법령·승인기관·구비요건 |
| `customs-weekly-exchange-rate` | 관세청 관세환율정보 | 수입 과세가격 계산에 사용하는 주간 환율 |

출처별 상태는 `success`, `no_data`, `not_configured`, `not_applicable`, `unsupported`, `error`로 구분합니다. `no_data`는 규제나 요건이 없다는 판정이 아닙니다.

## 설정

세 수집기는 `PublicData:DataGoKrServiceKey`를 공통 인증키로 사용합니다. 출처별 키를 분리해야 할 때는 아래 값을 우선 설정할 수 있습니다.

- `PublicData:CustomsTradeStatistics:ServiceKey`
- `PublicData:CustomsRequirements:ServiceKey`
- `PublicData:CustomsExchangeRate:ServiceKey`

실제 인증키는 추적되는 `appsettings.json`이 아니라 무시되는 로컬 설정 또는 운영 환경변수에 둡니다.

## 기준정보 갱신

관세청 연례 HS 부호 XLSX는 실시간 수집기가 아니라 내부 HS 코드 카탈로그의 버전 갱신 자료입니다. 코드 신설·폐지와 품명·단위 변경을 검토한 뒤 카탈로그 버전 단위로 반영합니다.

## 확장 원칙

새 공공데이터는 `IHs공공데이터수집기` 구현으로 추가합니다. 정확한 HS/HSK 연결이 없는 식약처 품목·원재료 데이터는 상품명이나 국가만으로 자동 확정하지 않고, 별도 연결 품질과 검토 상태를 마련한 뒤 추가합니다.
