# [기획 · 한반도 철도 거점 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 조사 r18]

## 저장 목적

도라산–판문–봉동–개성으로 이어지는 경의선 서부 교류축 조사 결과를 채팅에만 남기지 않고, 이후 DB 적재와 Unity 읽기 전용 표현에서 재사용할 수 있는 출처·주장 대장으로 보존한다.

이 문서는 조사 결과의 저장소 기준선이다. 실제 원문 동결·hash 생성·로컬 DB 적재·읽기 API·Graph Map·배치맵·Unity 표현을 완료했다는 뜻은 아니다.

## 저장 원칙

- 공식 문서 한 건을 `SourceReceipt` 후보 하나로 보존하고, 문서에서 파생한 사실 주장은 별도 행으로 분리한다.
- 현재 운행, 역사적 운행, 물리적 단절, 장래 구상과 게임 시나리오를 같은 상태로 합치지 않는다.
- `판문역`과 `봉동역`은 공식 문서의 표현 관계가 해소되기 전까지 별도 조사 대상으로 유지한다. 같은 역·개명 관계·상하위 공간 관계 중 하나로 임의 확정하지 않는다.
- 대한민국 공식 자료와 국제기구 자료를 서로 다른 자료 권위와 시점으로 보존한다.
- 군사시설, 경계 통과 절차의 세부, 현재 출입 가능 경로와 정밀 위치는 수집·추론·Unity 투영 대상에서 제외한다.
- 사실 주장마다 근거 문서, 사건 시점, 확인 상태와 반증 또는 갱신 조건을 연결한다.

## 출처 영수증 후보

| 영수증 후보 ID | 기관·문서 | 사건·작성 시점 | 확인한 범위 | 이용·보존 상태 |
| --- | --- | --- | --- | --- |
| `source-receipt:unikorea:rail-test-2007-05-13` | [통일부 남북회담본부, 남북철도연결구간 열차시험운행 합의서](https://dialogue.unikorea.go.kr/ukd/ca/usrtalkmanage/View.do?agreement_type=&id=173&tab=5) | 합의 2007-05-13, 시험운행 2007-05-17 | 문산→도라산→판문→개성의 시험운행 구간·시각 | 공공누리 출처표시·상업적 이용 금지·변경 금지. 원문 동결·hash 미수행 |
| `source-receipt:unikorea:freight-committee-2007-12-01` | [통일부 남북회담본부, 남북철도운영공동위원회 제1차 회의](https://dialogue.unikorea.go.kr/ukd/a/ad/usrtaltotal/View.do?id=143) | 회의 2007-12-01, 화물 운행 합의 2007-12 | `문산–봉동(판문역)` 명칭과 실제 도라산–판문 운행 절차 | 공공누리 제4유형. 원문 동결·hash 미수행 |
| `source-receipt:unikorea:dmz-peace-train-2026-04-09` | [통일부, 도라산역 평화이음 열차 운행 재개](https://unikorea.go.kr/web/unikorea/bbs/bbs_0000000000000004/59177) | 작성 2026-04-09, 재개 2026-04-10 | 서울–도라산 국내 관광열차의 현재 운행 범위 | 공공누리 제1유형. 원문 동결·hash 미수행 |
| `source-receipt:mnd:severance-briefing-2024-10-17` | [국방부 일일 정례 브리핑](https://www.korea.kr/news/policyNewsView.do?newsId=156655297) | 2024-10-17 | 경의선·동해선 연결부 폭파와 후속 작업에 관한 공식 브리핑 | 이용 조건 추가 검토 필요. 원문 동결·hash 미수행 |
| `source-receipt:unescap:tar-agreement-2022` | [UN ESCAP, Intergovernmental Agreement on the Trans-Asian Railway Network](https://www.unescap.org/sites/default/d8files/2022-11/Intergovernmental_Agreement_TAR_2022-EN.pdf) | 2022 공개본 | 신의주–평양–개성–봉동–도라산 국제 철도 회랑 표기 | 국제기구 원문. 이용 조건·원문 동결·hash 추가 검토 필요 |

`수집 시각`은 이 조사 문서를 작성한 `2026-09-21 Asia/Seoul`로 기록한다. 실제 DB 적재 단계에서는 HTTP 응답, 파일 길이, MIME, 최종 URL, SHA-256과 재현 가능한 수집 명령을 별도 영수증으로 생성해야 한다.

## 사실 주장 대장

| 주장 후보 ID | 조사 결과 | 상태 | Unity 표현 상한 |
| --- | --- | --- | --- |
| `claim:western-corridor:dorasan-domestic-2026` | 2026년 재개된 열차는 서울–도라산 국내 관광열차다. 도라산 이북 운행 근거가 아니다. | `ConfirmedCurrentDomestic` | 도라산까지 현재 국내선 실선, 이북으로 자동 연장 금지 |
| `claim:western-corridor:test-run-2007` | 2007-05-17 시험운행은 문산→도라산→판문→개성 구간으로 합의됐다. | `ConfirmedHistoricalEvent` | 역사 사건 카드·회색 과거선 |
| `claim:western-corridor:freight-2007` | 2007년 화물 사업은 `문산–봉동`으로 불렸지만 당면 열차 운행과 화물 취급 절차는 도라산–판문역으로 적혔다. | `ConfirmedHistoricalWording` | 사업명과 실제 운행 구간을 분리 표시 |
| `claim:western-corridor:panmun-bongdong-identity` | 공식 문서가 `문산–봉동(판문역)`이라고 병기하지만 판문역과 봉동역의 정확한 동일성·개명·공간 관계는 이번 조사만으로 확정할 수 없다. | `AwaitingIdentityReview` | 두 거점을 합치지 않고 `자료 검토 중` 표시. 정밀 좌표·건물 금지 |
| `claim:western-corridor:kaesong-destination-2007` | 개성역은 2007년 경의선 시험운행의 북측 도착역이었다. | `ConfirmedHistoricalEvent` | 역사 도착 거점 카드 |
| `claim:western-corridor:severed-current` | 2024년 경의선·동해선 연결부 폭파에 관한 대한민국 공식 브리핑이 존재하며, 현재 남북 연결 운행을 전제할 수 없다. | `ConfirmedPhysicalDisconnectionObservation` | 단절선과 기준일 표시. 현재 통행 가능 표현 금지 |
| `claim:western-corridor:trans-asian-corridor` | UN ESCAP 자료에는 신의주–평양–개성–봉동과 도라산으로 이어지는 국제 회랑이 표기된다. | `ConfirmedInternationalCorridorDescription` | 점선 회랑 후보. 현재 운행선으로 표현 금지 |

## DB 적재 계약 후보

첫 적재는 유연한 조사 원본을 보존하는 MongoDB 원장에 두고, 식별 관계가 검토된 뒤 RDB 읽기 투영을 만든다.

```text
PublicEvidenceSourceReceipt
├─ SourceReceiptStableId
├─ ProviderCode / DocumentTitle / SourceUrl
├─ PublishedAt / EventPeriod / CollectedAt
├─ RightsCode / MimeType / ContentLength / Sha256
├─ AuthorityScopeCode
└─ AcquisitionStateCode

PublicEvidenceClaim
├─ ClaimStableId
├─ SubjectResearchId
├─ ClaimKindCode / ClaimText
├─ ValidTime / ObservedAt
├─ EvidenceStateCode
├─ SourceReceiptStableIds[]
├─ SupersedesClaimStableId?
└─ UnityProjectionAllowed=false

PlaceIdentityReview
├─ ReviewStableId
├─ LeftResearchSubjectId
├─ RightResearchSubjectId
├─ RelationCandidateCode
├─ EvidenceStateCode
├─ SourceReceiptStableIds[]
└─ HumanDecisionRequired=true

TransportCorridorEvent
├─ EventStableId
├─ CorridorResearchId
├─ EventKindCode
├─ FromResearchSubjectId / ToResearchSubjectId
├─ StartedAt / EndedAt?
├─ OperationStateCode
└─ SourceReceiptStableIds[]
```

`판문역`과 `봉동역`에는 검토용 `ResearchSubjectId`만 먼저 부여한다. 동일성 검토가 끝나기 전에는 기존 역 stable ID나 Unity 배치 고유 식별자로 승격하지 않는다.

RDB 읽기 투영은 최소한 다음 상태를 서로 구분해야 한다.

- `OperationalDomestic`
- `HistoricalTrialRun`
- `HistoricalFreightOperation`
- `Disconnected`
- `InternationalCorridorDescription`
- `ScenarioOnly`
- `AwaitingIdentityReview`

## Unity 인계 조건

Unity에는 DB에서 공개 가능 판정을 받은 읽기 전용 상태 사본만 전달한다.

- 현재 국내 운행: 실선
- 역사적 시험·화물 운행: 회색 선과 사건 연도
- 현재 물리적 단절: 끊어진 선과 기준일
- 국제 회랑·장래 교류 구상: 점선
- 판문–봉동 관계 미정: 하나의 역으로 합치지 않고 검토 대기 표식

Unity는 자료를 선택·확대·조회할 수 있지만 역 동일성, 현재 통행, 운행 재개나 교류 정책을 확정하지 않는다.

## 검증 관문

1. 공식 원문을 고정 취득해 URL·MIME·길이·SHA-256을 가진 출처 영수증을 만든다.
2. 동일 입력 재적재가 중복 영수증·주장·사건을 만들지 않는다.
3. 주장과 원문 인용 위치를 독립 재조회한다.
4. 판문·봉동 관계가 미정인 동안 두 자료가 하나의 역으로 합쳐지지 않는지 검사한다.
5. 공개 읽기 투영에 군사·보안 세부, 정밀 경계 통과 정보와 임의 좌표가 없는지 검사한다.
6. Unity 투영은 DB·API·표현 검토가 별도 승인된 뒤 진행한다.

## 확정 / 미정 / 다음 질문 하나

- 확정: 이번 조사 결과는 이 문서의 출처·주장 대장으로 저장하며, 후속 DB 적재는 원문 영수증과 주장을 분리한다.
- 미정: 판문역과 봉동역의 정확한 관계, 북측 거점의 검증 가능한 좌표, 국제기구 자료의 배포 이용 조건.
- 다음 질문 하나: 후속 개발 작업실에서 이 대장을 기존 공공자료 로컬 MongoDB 원장에 실제 적재하고 독립 재조회하는 작은 수직 슬라이스를 열지 결정한다. 추천은 `도라산·판문·봉동·개성 4개 조사 대상 + 출처 5건 + 주장 7건`만 먼저 적재하는 것이다.
