# [개발 · 운영·회계 Simulation · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 플랫폼 운영경제성 표준 첫 구현 r1]

- 상태: `ImplementedFirstSlice / ManagementSimulationOnly / CountryCurrencySeparated / AutomatedTestsPassed / OperationalPostingDisabled`
- 상위 기획: `README.md` r106
- 선행 기획: `accountant-role-and-ledger.r34.md`, `accounting-operating-conventions.r35.md`, `operator-integrated-accounting.r36.md`, `weekly-settlement-summary.r39.md`
- 목적: 한국·미국 등 서로 다른 국가의 시나리오를 같은 운영 지표 계약으로 비교하되, 통화·관할·회계 판단을 조용히 합치지 않는 첫 실행 뿈대를 만든다.

## 1. 구현 경계

```text
관리자 평가 요청
  → Controller API
  → UseCase
  → 순수 Calculator
  → 관리 Simulation 결과
```

- API: `POST api/v1/admin/operations/economics/evaluate`
- 권한: `서버관리자전용`
- 버전: `3.5`
- 영향: DB, Event, Outbox, 결제, 정산, 환율, 세금, 회계 전표 쓰기 없음
- 결과 상태: `ManagementSimulationOnly`, `회계매핑승인필요=true`, `운영전표쓰기허용=false`

## 2. 표준화한 금액 분류

| Code | 의미 | 영업이익 후보 포함 |
| --- | --- | --- |
| `GrossTransactionAmount` | 고객 총결제·총거래액 | 아님 |
| `OrderRevenueCandidate` | 주문에서 플랫폼이 보유하는 수익 후보 | 포함 |
| `RecurringRevenueCandidate` | 구독·고정 용역료 후보 | 포함 |
| `PassThroughFunds` | 음식점·기사·지사 등에게 지급할 통과자금 | 아님 |
| `VariableCost` | 주문량과 함께 변하는 플랫폼 비용 | 차감 |
| `FixedCost` | 기간 고정비 | 차감 |
| `CashReceipt` | 실제 현금 유입 | 손익과 독립 |
| `CashPayment` | 실제 현금 유출 | 손익과 독립 |

고객 총결제액과 통과자금을 플랫폼 수익으로 자동 계상하지 않는다. `PrincipalCandidate`, `AgentCandidate`, `Undetermined`는 판단 가정을 보존하지만 가정을 바꾼다고 현금이나 영업이익 후보를 자동으로 바꾸지 않는다.

## 3. 계산식

```text
기여금 = 주문 수익 후보 - 변동비용
주문당 기여금 = 기여금 / 완료 주문 수
영업이익 후보 = 주문 수익 후보 + 반복 수익 후보 - 변동비용 - 고정비용
손익분기 완료 주문 수 = ceil(max(0, 고정비용 - 반복 수익 후보) / 주문당 기여금)
기말 가용현금 = 기초 가용현금 + 현금 유입 - 현금 유출
```

- 완료 주문이 0건이면 주문당 지표를 산출하지 않는다.
- 주문당 기여금이 0 이하면 손익분기 주문수를 제시하지 않는다.
- 요청의 항목 순서가 달라도 같은 입력이면 같은 SHA-256 결과 hash를 만든다.

## 4. 국가 확장 방식

- 한 요청은 한 `CountryCode`, `JurisdictionCode`, `CurrencyCode`만 가진다.
- KRW와 USD를 하나의 손익과 합산하지 않는다. 국가별 시나리오를 각각 평가한 뒤 동일한 항목 구조로 비교한다.
- 환율 적용·연결 재무제표·세무·노무·면허 정책은 국가·관할 Adapter의 후속 책임이다.
- 수익 표시는 실제 계약과 수행의무에 따른 본인·대리인 검토 후 승인된 `AccountingMappingRevision`이 있을 때만 운영 회계로 승격한다.

## 5. 검증한 범위

- 계약·순수 계산기·UseCase·관리자 API·DI를 결속했다.
- 총거래액·통과자금 분리, 기여금·영업이익 후보·손익분기, 국가·통화 무환산, 본인·대리인 가정 중립, 입력 순서 무관 hash, 잘못된 항목 거절을 자동시험한다.
- 실제 원장을 자동으로 읽어 항목을 생성하는 Adapter, 운영자 앱 화면, 환율 변환, 13주 현금 전망, 회계·세무 확정, 실제 지급은 이번 범위에서 제외했다.

## 6. 다음 구현 우선순위

1. 기존 음식배달·기사·지사·음식점 정산 원장을 읽어 이 분류 항목으로 바꾸는 읽기 전용 Adapter를 만든다.
2. 같은 금액을 중복 소유하지 않도록 `FinancialEvent` 및 관리계정 Stable ID와 결속한다.
3. 기준·낙관·스트레스 시나리오를 비교하는 운영자 앱 카드를 붙인다.
4. 독립된 환율·관할 정책 Adapter를 추가한 뒤에도 원본 국가별 결과를 보존한다.

## 7. 기준 참고

- [IFRS 18](https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/)의 영업손익·경영진 정의 성과측정치 표시 방향은 외부 보고 검토 시 따로 적용한다. 이 계산기의 `영업이익후보`는 그 자체로 IFRS 수치가 아니다.
- [IFRS 15 사후검토 자료](https://www.ifrs.org/content/dam/ifrs/project/pir-ifrs-15/rfi-iasb-2023-4-pir-ifrs-15.pdf)의 본인·대리인 평가는 재화나 용역을 고객에게 이전하기 전의 통제 여부를 포함한 계약 사실 검토가 필요하다. 요청 Code는 비교용 가정이며 회계 결론이 아니다.

## 8. 변경 파일

- `Ssalddel.Contracts/Admin/Operations/플랫폼운영경제성Dtos.cs`
- `Ssalddel.Domain/운영/플랫폼운영경제성Calculator.cs`
- `Ssalddel/Application/Admin/Operations/플랫폼운영경제성UseCase.cs`
- `Ssalddel/Controllers/Admin/Operations/플랫폼운영경제성Controller.cs`
- `Ssalddel/Extensions/ServiceCollectionExtensions.ApplicationCore.cs`
- `Ssalddel.CodeMetadata/SsalddelCodeMetadataAttribute.cs`
- `Ssalddel.Tests/Application/Admin/Operations/플랫폼운영경제성UseCaseTests.cs`
