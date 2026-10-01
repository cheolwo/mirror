# 운영 시장 프로필

## 목적

홍달의 한국 운영과 미국 운영은 앱이나 원장을 복제하지 않고, 공통 업무 흐름에 배포별 운영 정책을 적용하는 방식으로 분리한다.

다음 네 축은 서로 독립적으로 유지한다.

- 표시 언어: 현재 `ko-KR` 또는 `en-US`
- 운영 시장: 현재 `KR` 또는 `US`
- 운송 구간: 국내 운송, 수입, 수출, 국가 간 운송
- 법적 역할: 공동행동 촉진 플랫폼, 화주, 운송사, 허가 또는 권한을 확인한 전문 서비스 참여자

운영 시장을 바꿔도 표시 언어는 자동으로 바뀌지 않는다. 미국 서버를 한국어로 사용하거나 한국 서버를 영어로 사용하는 구성이 가능해야 한다.

## 플랫폼 운영자의 역할

홍달의 기본 역할 코드는 `CollectiveActionFacilitator`다. 운영자는 운송 주선업자가 되는 것을 제품의 전제로 삼지 않고, 사람들이 수요·의사·정보를 모아 공동구매, 공동수입, 수출입 준비를 시작할 수 있게 돕는다.

업무는 다음 세 단계로 구분한다.

1. `CommunityIntentCoordination`: 참여 의사, 수량, 조건, 쟁점과 정보를 모은다.
2. `QualifiedProviderParticipationRequest`: 운송주선업자, 운송사, 통관사 등 자격 사업자가 제안할 수 있도록 업무 요청을 게시한다.
3. `RegulatedTransportationArrangement`: 계약, 운송사 선정, 주선 실행처럼 자격과 책임이 필요한 행위를 수행한다.

앞의 두 단계는 플랫폼의 촉진 업무다. 세 번째 단계의 책임 코드는 `ParticipatingQualifiedServiceProvider`이며, 참여한 자격 사업자가 자기 명의와 책임으로 수행해야 한다.

배차에서도 추천과 확정을 분리한다. `CollectiveActionDispatchBoundaryPolicy`는 결정 출처를 다음과 같이 해석한다.

- `PlatformCandidateInformation`: 거리, 시간, 조건과 후보 정보를 제공하지만 배차를 확정할 수 없다.
- `ParticipatingDriverSelfAcceptance`: 인증된 기사와 선택된 기사가 같을 때 본인 수락으로 확정할 수 있다.
- `QualifiedServiceProviderConfirmation`: 현재 검증된 전문 사업자 참여자가 자기 책임으로 결정할 때만 확정할 수 있다.

알 수 없는 결정 출처는 `PlatformCandidateInformation`으로 처리해 확정을 차단한다. 추천 점수, 운영자 검토, AI 판단 사례도 그 자체로 기사 배정이나 운송계약 체결이 되지 않는다.

화면과 계약에서도 다음 경계를 유지한다.

- 홍달은 특정 운송사를 자기 명의로 확정하거나 운송계약의 당사자인 것처럼 표시하지 않는다.
- 전문 사업자의 제안, 자격 상태, 계약 주체, 수수료와 대금 수령 주체를 분리해 표시한다.
- 원장에는 공동 의사 형성 기록과 전문 사업자의 규제 업무 실행 기록을 별도 블록으로 남긴다.
- 커뮤니티 원장은 배차 후보와 진행 정보를 보여주지만 기사 확정과 운송 실행 상태를 RDB로 역전파하지 않는다. 참여자 실행 경로에서 확정된 결과가 RDB에 기록된 뒤 원장으로 동기화된다.
- 플랫폼 명칭만으로 법적 지위가 결정되는 것은 아니므로 실제 권한 행사, 계약과 대금 흐름도 이 경계를 따라야 한다.

## 공통 프로필

공통 기준은 `Hongdal.Contracts/Common/Operations/OperatingMarketProfiles.cs`에 둔다.

| 항목 | 한국 `KR` | 미국 `US` |
| --- | --- | --- |
| 통화 | KRW | USD |
| 거리 / 중량 | km / kg | mi / lb |
| 배포 기본 시간대 | Asia/Seoul | UTC, 배포 설정으로 지역 시간대 지정 |
| 주소 기본값 | 행안부 도로명주소 | Census 주소 범위 매칭, 배송 검증 별도 |
| 지도 기본값 | Naver Maps | Google Maps |
| 판매채널 기본값 | SmartStore, Coupang, 11st | Amazon, eBay, Shopify, Walmart, Etsy, TikTok Shop |
| 화물 주선 기본값 | 국내 운송 업무 | 권한을 확인한 브로커 파트너 흐름 |

표시 언어 코드는 `Hongdal.Contracts/Common/Localization/DisplayLanguageCodes.cs`에 별도로 둔다. 시장 프로필의 `FormattingCultureName`은 금액과 날짜의 기본 형식일 뿐 사용자가 선택한 표시 언어가 아니다.

## 적용 규칙

1. 국가 분기는 화면과 서비스마다 직접 작성하지 않고 공통 프로필 또는 시장 정책을 조회한다.
2. 금액, 주소, 거리, 중량을 저장할 때는 값과 함께 통화·국가·단위 코드를 보존한다.
3. 한국 전용 DTO나 외부 API를 미국 운영에서 암묵적으로 재사용하지 않는다.
4. 클라이언트가 보낸 국가 값으로 서버의 법률·외부 API 모듈을 바꾸지 않는다.
5. 규제 검증은 단순 참여자 ID가 아니라 참여 역할, 권한 참조, 확인 시각, 만료 시각, 요구사항별 확인 결과로 기록한다.

## 배포별 서비스 모듈

한국 서버와 미국 서버는 같은 공통 계약을 사용하지만 시작 시 하나의 시장 모듈만 등록한다.

```text
공통 인터페이스
  +-- KR 배포 -> KoreaOperatingMarketServiceModule
  +-- US 배포 -> UnitedStatesOperatingMarketServiceModule
```

배포 시장은 `OperatingMarket:MarketCode`로 설정한다. 환경 변수에서는 `OperatingMarket__MarketCode=KR` 또는 `OperatingMarket__MarketCode=US`를 사용한다. 지원하지 않는 값은 서버 시작 단계에서 오류로 처리한다.

| 공통 인터페이스 | 한국 모듈 | 미국 모듈 |
| --- | --- | --- |
| `IOperatingMarketAddressLookupAdapter` | 행안부 도로명주소 | U.S. Census Geocoder |
| `IOperatingMarketFreightWorkflowPolicy` | 국내 전문 사업자 허가 점검 메타데이터 | 참여 브로커의 권한 강제 검증 |

주소 조회는 `IOperatingMarketAddressLookupService`를 공통 진입점으로 사용한다. 한 서버에는 해당 배포 시장의 주소 어댑터만 존재한다. 다른 시장의 주소 요청은 제공자 우회 없이 `MarketNotAvailableInDeployment`로 거절한다.

## 미국 주소 정보 경계

미국 주소는 하나의 API가 모든 의미를 확정하는 값으로 다루지 않는다. 특정 문화권을 단정해 기능을 강제하지 않고, 참여자가 원할 때 가까운 지역의 공동구매를 찾고 함께 의사를 모을 수 있도록 다음 책임을 분리한다.

| 계층 | 용도 | 현재 제공자 | 판정 의미 |
| --- | --- | --- | --- |
| 주소 입력 | street, city, state, ZIP, 보조 단위 입력 | 공통 계약 | 사용자가 입력한 값 |
| 위치 매칭 | 좌표와 city/state/ZIP 후보 확인 | U.S. Census Geocoder | MAF/TIGER 도로 주소 범위에 매칭됨 |
| 우편 표준화 | ZIP+4, 표준 약어, 배송용 주소 확인 | USPS 후속 연결 | USPS 우편 표준에 맞춘 결과 |
| 커뮤니티 지역 | 가까운 공동구매 탐색과 집계 | 별도 비식별 투영 | 공개 가능한 지역 범위 |

USPS Publication 28은 배달 주소 줄과 city/state/ZIP 줄을 구분하고, 공동주택에는 `APT`, `STE`, `UNIT` 같은 secondary unit designator를 사용한다. PO Box, rural route, 군 우편과 Puerto Rico 주소도 별도 형식을 가진다. 특히 Puerto Rico의 `URB` urbanization은 주소의 중요한 구성 요소이므로 일반 미국 주소 문자열에서 임의로 버리지 않는다.

ZIP Code는 행정구역 폴리곤이 아니라 USPS가 우편 처리와 배송 경로에 부여하는 코드다. Census의 ZCTA는 ZIP Code 분포를 통계·지도 분석용 면으로 일반화한 별도 지리 제품이며 모든 유효 ZIP Code가 ZCTA로 표현되는 것도 아니다. 따라서 ZIP Code를 시·군 경계처럼 사용하거나 ZIP과 ZCTA를 같은 식별자로 저장하지 않는다.

### 현재 구현

미국 배포의 `UnitedStatesAddressLookupAdapter`는 `UnitedStatesCensusAddressGeocoder`를 사용한다.

```text
GET https://geocoding.geo.census.gov/geocoder/geographies/onelineaddress
  ?address={사용자가 입력한 주소}
  &benchmark=Public_AR_Current
  &vintage=Current_Current
  &layers=2,28,30,80,82
  &format=json
```

- API key 없이 공식 단건 Geocoder를 호출한다.
- 반환 주소, city, state, ZIP, 위도·경도와 TIGER line ID를 공통 주소 후보로 변환한다.
- Census State, County, Incorporated Place, Census Designated Place, ZCTA를 `GEOID` 기반 지리 영역으로 함께 반환한다.
- `Public_AR_Current` benchmark와 `Current_Current` geography vintage처럼 응답이 실제로 밝힌 데이터 기준을 함께 보존한다.
- 좌표는 주소 범위에서 계산된 값이므로 `MatchPrecisionCode=AddressRange`로 표시한다.
- 매칭 결과가 없어도 제공자 오류가 아닌 정상 빈 결과로 처리한다.
- 요청 실패 메시지에는 사용자가 입력한 주소를 되풀이하지 않는다.

Census 매칭 성공은 우편 배송 가능 확인이나 건물 출입구의 rooftop 좌표를 뜻하지 않는다. 현재 oneline endpoint는 Puerto Rico의 urbanization/municipio가 포함된 주소를 완전하게 지원하지 않으므로 해당 형식은 향후 `addressPR` 구조화 요청으로 분기한다.

미국 서버 설정은 다음과 같다.

```json
{
  "UnitedStatesAddress": {
    "CensusGeocoder": {
      "Enabled": true,
      "BaseUrl": "https://geocoding.geo.census.gov",
      "GeographiesOneLineAddressPath": "/geocoder/geographies/onelineaddress",
      "Benchmark": "Public_AR_Current",
      "Vintage": "Current_Current",
      "Layers": "2,28,30,80,82",
      "TimeoutSeconds": 15,
      "MaxAddressLength": 500
    },
    "DeliveryScopes": {
      "StateMinimumParticipantsForPublicDisplay": 1,
      "CountyMinimumParticipantsForPublicDisplay": 3,
      "PlaceMinimumParticipantsForPublicDisplay": 5,
      "ZctaMinimumParticipantsForPublicDisplay": 10
    }
  }
}
```

### 미국 공동구매 배달권 분할

`UnitedStatesDeliveryScopePlanner`는 주소 후보에 포함된 Census 지리를 모집 표시뿐 아니라 물류 효율화를 위한 수요 묶음 후보로 분리한다. 가까운 수요를 같은 키로 집계하면 공동 수입·공동 구매의 총량을 계산하고, 집결 창고를 고르고, 출고 배치와 차량 적재를 묶어 배송 횟수와 마지막 배송의 정차 수를 줄일 수 있다.

`IUnitedStatesDeliveryScopeService.ResolveAsync`는 전체 주소 조회와 배달권 계획을 한 경로로 연결한다. 주소가 매칭되지 않거나 State 지리가 없으면 임의의 ZIP·문자열 키를 만들지 않고 실패 결과를 반환한다.

| 범위 | 키 기준 | 모집·물류 목적 | 기본 선택 |
| --- | --- | --- | --- |
| State | State GEOID | 넓은 글 탐색, 지역 간 수요 발견, 수입·간선 물량 집계 | 다른 지리가 없을 때만 |
| County / county-equivalent | County GEOID | 비도시·저밀도 수요의 공동 집결과 배송 경로 묶음 | Place가 없을 때 |
| Incorporated Place / CDP | Place GEOID | 도시 생활권 수요 집계, 집결 거점과 출고 배치 후보 구성 | 주소에 Place가 있으면 우선 |
| ZCTA | ZCTA GEOID | 참여자가 충분한 구간의 마지막 배송 정차·경로 묶음 | 기본 선택 아님 |
| 실제 운행권 | 주행시간, 제공자 service area, 차량·품목 조건 | 배송 실행 가능성 판단 | Census 지리와 별도 |

배달권 키는 `us-state:{GEOID}`, `us-county:{GEOID}`, `us-place:{GEOID}`, `us-zcta:{GEOID}` 형식을 사용하고 생성 당시 benchmark와 geography vintage를 계획 결과에 함께 남긴다. 주소의 우편 ZIP과 해당 좌표의 ZCTA가 다를 수 있으므로 `PostalCode`를 ZCTA 키로 복사하지 않는다. Place는 여러 County에 걸칠 수 있고 ZCTA도 행정구역에 완전히 종속되지 않으므로 억지로 `State > County > Place > ZCTA` 단일 트리로 만들지 않고 겹치는 지리 후보로 보존한다.

각 후보에는 `RegionalInboundConsolidation`, `RuralRouteConsolidation`, `UrbanHubConsolidation`, `LastMileStopConsolidation` 중 하나의 물류 역할을 기록한다. `RecommendedScopeKey`는 커뮤니티 모집의 초기 추천을, `RecommendedDemandConsolidationScopeKey`는 물류 수요 집계의 초기 추천을 나타낸다. 현재는 둘 다 Place 우선, Place가 없으면 County를 사용하지만 별도 계약으로 유지해 실제 밀도와 운영 데이터가 쌓인 뒤 서로 다른 권역을 선택할 수 있게 한다. 계획의 `ConsolidateDemandThenValidateFulfillment` 정책은 지리적으로 수요를 먼저 묶은 다음 창고·출고·운행 조건을 검증한다는 순서를 뜻한다.

모든 지리 후보의 `RequiresLogisticsFeasibilityValidation`과 `RequiresOperationalRouteValidation`은 `true`다. 즉 같은 Place나 County 안에 있다는 사실은 수요를 묶어 볼 근거일 뿐, 합배송이 더 저렴하거나 창고·배송 제공자가 실제로 갈 수 있다는 뜻은 아니다. 실행 단계에서는 총 수량·중량·부피, SKU와 온도대 호환성, 재고 창고, 공통 배송 시간창, 차량 적재율, 도로 주행시간, 배송 제공자 관할과 예상 비용을 다시 계산한다.

공개 최소 인원의 기본값은 State 1명, County 3명, Place 5명, ZCTA 10명이다. 이는 법정 기준이나 물류 효율 달성 기준이 아니라 초기 개인정보 보호를 위한 조정 가능한 제품 정책이다. 기준 미달 수요도 내부 집계에는 포함할 수 있지만 지역명과 참여 규모를 공개하지 않는다. 반대로 공개 인원을 충족해도 적재율·배송 시간창·비용 절감 조건을 충족하지 못하면 실행 배치로 확정하지 않는다.

### 개인정보와 공동구매 지역

- 상세 street, apartment/suite/unit, 정확한 좌표는 배송과 상호 동의가 필요한 사적 정보로 유지한다.
- 게시글과 모집 화면에는 원본 주소나 좌표를 직접 투영하지 않는다.
- 초기 공개 지역은 Place를 우선하고, Place가 없는 지역은 County를 사용한다. ZCTA는 설정된 최소 참여 인원을 충족한 경우에만 세분화한다.
- ZIP 단위라도 참여자가 적어 개인 주거지를 추정할 수 있으면 더 넓은 지역으로 올려 표시한다.
- 공동구매 지역은 사용자의 명시적 참여 선택을 나타낼 뿐, 주소가 같거나 가깝다는 이유만으로 자동 가입·알림·거래 상대 추천을 하지 않는다.

### USPS 후속 연결 조건

USPS Addresses API는 배송용 주소 표준화와 ZIP+4 확인 단계에만 연결한다. 2026-07-18 확인 기준으로 USPS는 Addresses API 3.3.1 접근을 2026-08-01로 연기했고, 계속 사용하려는 고객에게 별도 license agreement를 요구하고 있다. 개발자 포털은 Address API의 요금 계층 전환도 안내하고 있으므로 다음 준비 전에는 Hongdal의 기본 주소 제공자로 고정하지 않는다.

1. USPS Business Account와 OAuth client credentials를 배포 비밀로 관리한다.
2. 주소 API license, 허용 용도, 보존·재배포 제한과 실제 요금을 검토한다.
3. Census `AddressRange` 매칭과 USPS `PostalStandardized` 결과를 서로 다른 상태로 저장한다.
4. 배송 또는 label 생성 직전처럼 우편 검증이 실제로 필요한 시점에만 호출한다.
5. USPS 장애가 발생해도 Census 결과를 배송 검증 성공으로 승격하지 않는다.

## 미국 3PL 후보 디렉터리

미국 배포에는 공동구매·공동수입 참여자가 필요한 물류 역량을 직접 비교하고 상담할 수 있도록 읽기 전용 3PL 후보 디렉터리를 둔다. 조사 후보는 실제 계약 창고와 분리하고 모두 `NoPlatformRelationship`, `RegulatoryStatusNotVerified`, `CanBeSelectedForOperations=false`로 시작한다.

```http
GET /api/v1/operations/third-party-logistics/providers
GET /api/v1/operations/third-party-logistics/providers/collective-purchase
GET /api/v1/operations/third-party-logistics/providers/bonded-to-door
```

첫 API는 범용 23개 업체를 역량과 업무 구간으로 찾고, 두 번째 API는 그중 공동구매 흐름을 공식 자료로 확인한 12개 업체를 국제 반입·항만·대량 입고·공동 보관·소분·참여자 배송·반품 단계와 상품 취급 조건으로 찾는다. 세 번째 API는 보세창고·FTZ부터 통관, 미국 내 물류창고 입고, 참여자별 parcel 출고까지의 역할 후보 10개를 storage model·단계·시설 소재 주로 찾는다. 추천 점수나 자동 선정을 제공하지 않는다. 서비스 광고 근거와 공개 최소조건은 회사 공식 URL·검토일·재확인 상태로 남기고, 운송 주선·해상 OTI·보세창고·통관 등은 실제 수행 법인과 시설을 정부 자료에서 별도로 확인한 뒤 계약 참여자로 승격한다. 조사 대상, API 계약, 공식 근거와 갱신 절차는 [미국 3PL 후보 디렉터리](UnitedStatesThirdPartyLogisticsProviderDirectory.md)에 정리한다.

도착국이 미국인 공동수입 가원장은 디렉터리 단계와 같은 보세시설 운영, in-bond 운송, 미국 내 풀필먼트, 참여자 주소 배송 역할 슬롯을 연다. 조사 업체는 자동으로 슬롯을 채우지 않는다. 업체 담당자가 플랫폼 계정 역할 확인과 비구속 참여를 명시적으로 수락해야 원장 참여자로 기록되며, 외부 자격과 실제 계약은 계속 미확인 상태로 남긴다.

미국 배포의 관리자는 `POST /api/v1/admin/operations/third-party-logistics/outreach/preview`에서 보세-주소 후보 10개에 대한 영문 문의 초안을 준비할 수 있다. 공개 일반 이메일을 공식 회사 페이지에서 확인한 업체만 이메일 draft로 분류하고 나머지는 회사가 지정한 문의 양식용 본문으로 분류한다. 발신자 신원, 실제 우편 주소, 회신·수신거부 주소, suppression 확인과 업체별 수동 승인이 모두 없으면 차단하며 자동 발송은 제공하지 않는다. 한국 배포에서는 해당 준비 service도 사용할 수 없다.

## 서버 컨텍스트와 클라이언트 인터페이스

운영 시장은 요청 claim이나 header가 아니라 서버 배포 설정으로 고정한다. `IOperatingMarketContextAccessor`는 시장과 배포 시간대를 반환한다.

클라이언트는 다음 공개 API로 현재 서버의 비민감 운영 정보를 읽는다.

```http
GET /api/v1/operations/market-profile
```

응답에는 국가, 통화, 형식 문화권, 시간대, 거리·중량 단위, 주소·지도 제공자, 지원 표시 언어, 플랫폼 역할, 공동 의사 형성 가능 여부, 전문 사업자 참여 요청 가능 여부, 규제 업무 실행 가능 여부가 포함된다. `PlatformCanConfirmDispatch`는 항상 `false`이고, 지원하는 참여자 결정 출처를 별도로 제공한다. 참여자 ID와 권한 번호는 포함하지 않는다.

## 화물 주선 규제 경계

`OperatingMarketFreightComplianceProfileCatalog`은 법률 판정을 대신하지 않고 서버가 확인해야 할 항목과 공식 근거 코드를 제공한다.

| 시장 | 집행 모드 | 확인 항목 |
| --- | --- | --- |
| 한국 | `AuditOnly` | 전문 사업자 참여자와 역할, 허가 참조, 검증 유효기간, 운송주선사업 허가 상태 |
| 미국 | `Required` | 전문 사업자 참여자와 역할, MC 등 권한 참조, 검증 유효기간, broker authority, financial security, process-agent designation |

한국의 `AuditOnly`는 기존 국내 흐름을 즉시 중단하지 않기 위한 전환 상태다. 따라서 `Allowed` 결과가 법적 허가를 확인했다는 뜻은 아니다. 실제 주선 운영 전에는 전문 사업자 허가 검증 저장소와 정기 재검증 절차를 연결하고 집행 모드를 강화해야 한다.

미국에서도 공동 의사 형성과 전문 사업자 참여 요청은 자격 사업자 없이 사용할 수 있다. 다만 참여 브로커가 실제 운송 주선을 수행하는 단계는 모든 확인 항목이 현재 유효해야 한다. 참여자 ID만 있는 기존 `VerifiedLicensedBrokerPartnerId` 설정은 호환 목적으로 읽지만, 참여 역할·권한·유효기간·요구사항 증거가 없으므로 규제 업무 실행을 허용하지 않는다.

배포 설정 예시는 다음과 같다.

```json
{
  "OperatingMarket": {
    "MarketCode": "US",
    "TimeZoneId": "America/Chicago",
    "FreightServiceProvider": {
      "ParticipantId": "configured-outside-source-control",
      "ParticipantRoleCode": "US.PropertyBroker",
      "AuthorityReference": "verified-authority-reference",
      "VerifiedAtUtc": "2026-07-01T00:00:00Z",
      "VerificationExpiresAtUtc": "2026-10-01T00:00:00Z",
      "SatisfiedRequirementCodes": [
        "US.BrokerAuthority.Active",
        "US.FinancialSecurity.Active",
        "US.ProcessAgentDesignation.Active"
      ]
    }
  }
}
```

운영 값은 저장소의 `appsettings.json`에 넣지 않고 배포 비밀 또는 별도 보호 설정에서 공급한다.

## 공식 근거

2026-07-18 확인 기준이다. 법령과 행정 요구, 외부 API 조건은 변경될 수 있으므로 운영 전 다시 확인해야 한다.

- 한국: [화물자동차 운수사업법 제24조, 국가법령정보센터](https://www.law.go.kr/LSW/lsInfoP.do?chrClsCd=010202&lsId=&lsiSeq=286393&urlMode=lsInfoP)
- 미국: [FMCSA Broker Registration](https://www.fmcsa.dot.gov/registration/broker-registration)
- 미국: [FMCSA의 motor carrier, broker, freight forwarder 정의](https://www.fmcsa.dot.gov/faq/what-are-definitions-motor-carrier-broker-and-freight-forwarder-authorities)
- 미국: [FMCSA Broker and Freight Forwarder Financial Responsibility](https://www.fmcsa.dot.gov/registration/broker-and-freight-forwarder-financial-responsibility-rule-overview-and-compliance)
- 미국: [FMCSA Form BOC-3](https://www.fmcsa.dot.gov/registration/form-boc-3-designation-agents-service-process)
- 미국 주소 표준: [USPS Publication 28](https://pe.usps.com/text/pub28/welcome.htm)
- 미국 주소 API: [USPS Addresses API](https://developers.usps.com/addressesv3), [USPS API 이용 조건](https://developers.usps.com/terms-and-conditions)
- 미국 위치 매칭: [Census Geocoding Services API](https://geocoding.geo.census.gov/geocoder/Geocoding_Services_API.html)
- ZIP과 통계 지리: [Census ZIP Code Tabulation Areas](https://www.census.gov/programs-surveys/geography/guidance/geo-areas/zctas.html)

이 문서와 정책 코드는 법률 자문이나 자동 면허 판정이 아니다. 실제 서비스 출시 전 해당 관할의 변호사 또는 규제 전문가와 계약·운영 구조를 검토해야 한다.

## 현재 적용 범위

현재 적용된 범위는 다음과 같다.

- `KR` / `US` 공통 운영 프로필과 배포별 단일 서비스 모듈
- 표시 언어와 운영 시장의 독립 계약
- 시장별 주소 어댑터와 단위 변환
- 미국 Census 주소 범위 매칭, benchmark·정밀도·좌표 계약과 US 배포 전용 등록
- Census State·County·Place·ZCTA 지리 투영과 미국 공동구매 배달권 계획기
- 공동구매 거래경로 판정·투표·가원장·공동수입 원장 메타데이터에 서버 운영 국가 보존
- 공통 UI가 공개 런타임 프로필을 읽어 미국 배포의 제안 기본 국가와 거래경로 판정에 적용
- 미국 구매자 수요에서 보세·통관·풀필먼트·참여자 주소 배송까지의 10단계 비구속 여정 화면
- 공식 회사 근거를 포함한 미국 3PL 범용 23개 후보, 공동구매 역할 12개와 보세시설-참여자 주소 역할 10개 프로필의 읽기 전용 중립 필터 API
- 보세-주소 후보 10개의 공식 문의 채널과 관리자 전용 영문 초안 준비 API. 자동 발송 없이 발신자·수신거부·업체별 승인 요건을 강제
- 배포 시간대와 공개 런타임 프로필 API
- 운송 의뢰 생성을 `QualifiedProviderParticipationRequest`로 분류한 촉진 업무 경계
- 전문 사업자 참여 역할, 검증 상태, 만료, 요구사항별 증거 모델
- 플랫폼 후보 정보와 기사 본인 수락·전문 사업자 결정을 구분하는 배차 확정 경계
- 커뮤니티 원장에서 RDB 기사 확정·운송 실행 상태로의 역투영 차단

아직 남은 주소 범위는 구조화된 미국 주소 입력, Puerto Rico `addressPR`, USPS 배송 표준화, 실제 창고·배송 제공자 service area와 주행시간 검증 연결이다. 공개 최소 인원은 실제 이용 밀도와 개인정보 검토 결과에 따라 조정해야 한다. 10단계 여정 중 보세·통관·풀필먼트·label·배송 실행은 현재 읽기 모델이며, 권한 주체의 별도 계약과 Command로 실제 상태를 기록하는 연결이 남아 있다. 그 밖에는 전문 사업자 권한을 공식 데이터에서 주기적으로 재검증하는 저장소, 전문 사업자가 확정하는 별도 명령에서 `RegulatedTransportationArrangement`를 강제하는 연결, 한국 집행 모드 강화, 국가별 결제·정산 제공자 분리가 남아 있다. 기존 Naver, Toss, 국내 공공데이터처럼 한국 중심 서비스는 해당 업무를 리팩터링할 때 시장 모듈 경계 안으로 단계적으로 이동한다.
