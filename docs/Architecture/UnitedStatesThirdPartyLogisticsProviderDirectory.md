# 미국 3PL 후보 디렉터리

## 목적

이 디렉터리는 미국 공동구매·공동수입 과정에서 참여자들이 필요한 물류 역량을 확인하고 직접 상담할 후보를 찾기 위한 읽기 전용 참고 자료다. 플랫폼이 특정 업체를 추천하거나 운송·보관·통관 업무를 대신 배정하는 목록이 아니다.

2026-07-18에 각 회사의 공식 미국 서비스·위치 페이지를 확인한 스냅샷이다. 범용 후보 카탈로그 버전은 `US-3PL-2026-07-18-03`, 공동구매 역할 프로필 버전은 `US-COLLECTIVE-PURCHASE-3PL-2026-07-18`, 보세시설부터 참여자 주소까지의 프로필 버전은 `US-BONDED-TO-DOOR-2026-07-18`이다. 미국 3PL 전체를 망라하지 않으며 규모·가격·품질 순위도 아니다.

## 데이터 경계

조사 후보는 실제 계약 창고인 `창고` Entity와 분리한다. 모든 항목의 초기 상태는 다음과 같다.

| 필드 | 초기 값 | 의미 |
| --- | --- | --- |
| `DirectoryStatusCode` | `ResearchCandidate` | 공개 자료를 확인한 조사 후보 |
| `PlatformRelationshipStatusCode` | `NoPlatformRelationship` | 홍달과 제휴·계약 관계 없음 |
| `RegulatoryVerificationStatusCode` | `RegulatoryStatusNotVerified` | 역할별 면허·권한을 아직 확인하지 않음 |
| `IsPlatformPartner` | `false` | 플랫폼 참여 사업자가 아님 |
| `CanBeSelectedForOperations` | `false` | 원장·입고·출고·운송 실행에 바로 선택할 수 없음 |
| `RequiresDirectQuote` | `true` | 수량·품목·지역·서비스 수준에 맞춘 직접 견적 필요 |
| `RequiresFacilityCapabilityConfirmation` | `true` | 실제 시설의 가용 공간·온도대·인증·관할을 재확인해야 함 |

회사 공식 페이지에 기재된 역량은 검색 메타데이터일 뿐 계약 보증이 아니다. 특히 식품 취급, 냉장·냉동, 위험물, 보세, Foreign Trade Zone, 통관, 해상 주선, 화물 브로커 권한은 회사 브랜드 단위로 추정하지 않고 **업무 역할과 실제 시설 단위**로 다시 확인한다.

각 공동구매 프로필의 `ExternalResponsibilityCodes`는 3PL 후보 정보만으로 해결되지 않는 책임을 명시한다. Importer of Record, 품목 규제 적합성, 관세·세금, 재고 소유권, 참여자 주문·주소 제공 동의는 공동구매 주체와 계약 당사자가 정해야 한다. 식품 후보에는 정확한 시설의 인증과 cold-chain 적합성 확인을 추가로 요구한다.

수입식품은 3PL 후보 확인 전에 [미국·호주 수입식품 통관 규정 기준](UnitedStatesAustraliaImportedFoodCompliance.md)에 따라 FDA·FSIS·APHIS 관할, 시설·공급자 검증, 사전신고와 품목별 증명을 먼저 분류한다. 물류업체의 식품 취급 가능 표시는 해당 규정 충족이나 정부기관의 방출을 대신하지 않는다.

## 공동구매 물류를 보는 기준

미국 공동구매 물류는 한 업체 이름만 고르는 문제가 아니다. 해외 생산지나 공급자로부터 들어오는 경우에는 다음 역할을 거래별로 조합한다.

1. 국제 반입·통관 조정
2. 항만 drayage·transload
3. 대량 입고와 공동 재고 보관
4. 참여자별 break-pack·kitting·relabeling
5. 참여자 주소로 parcel fulfillment 또는 지역 거점·리테일 배분
6. 반품과 배송 불능 처리

소량 일회성 공동구매는 미국 공개 시장에서 crowdfunding·campaign fulfillment와 운영 형태가 가장 가깝다. 식품, 중량물, 항만 수입은 같은 후보군으로 섞지 않고 별도 취급 역량과 시설 확인이 필요하다.

## 보세시설에서 참여자 주소까지

`Customs bonded warehouse`와 `Foreign-Trade Zone(FTZ)`은 같은 제도가 아니다. 보세창고는 CBP가 승인한 시설에서 관세 납부 전 수입물품을 보관하는 구조이고, FTZ는 FTZ Board가 지정한 site를 CBP가 별도로 활성화한 뒤 특별 통관 절차를 쓰는 구조다. FTZ 지정만 받고 CBP activation이 되지 않은 site는 FTZ 운영시설로 취급하면 안 된다. FTZ 안에서는 보관·재포장·라벨 변경 등이 가능하지만 retail trade는 금지된다.

실제 공동수입 흐름은 다음 역할을 분리해서 구성한다.

1. Importer of Record와 customs bond를 정한다.
2. licensed customs broker 또는 적법한 self-filer가 entry·withdrawal을 처리한다.
3. 통관 전 시설 간 이동은 ACE in-bond 신고와 CBP 인증 bonded carrier를 사용한다.
4. 정확한 보세창고·FTZ 시설, 현재 authorization·activation, FIRMS code를 확인한다.
5. 미국 내 소비용 반출 승인이 끝난 뒤 일반 물류창고로 이송한다.
6. 참여자별 소분·kitting·label·pick-pack을 거쳐 parcel carrier에 인계한다.
7. 참여자 주소 동의, 배송 불능과 반품 책임을 계약에 넣는다.

업체 공식 자료를 기준으로 다음 10개 역할 후보를 전산화했다. `직접 보세창고`는 회사가 미국 CBP bonded facility라고 공개한 경우, `FTZ`는 미국 FTZ 서비스를 공개한 경우, `외부 보세시설 인계`는 해당 업체의 보세 보관시설을 이 디렉터리에서 확인하지 못해 다른 시설과 조합해야 하는 경우다.

| 업체 | customs-controlled 보관 경계 | 후속 물류에서 확인한 범위 |
| --- | --- | --- |
| Phoenix Warehouse | Jersey City, NJ의 CBP bonded operation 공개 | Port Newark 연계, 입고·보관·fulfillment·kitting·end-customer/last-mile·반품 |
| STG Logistics | customs bonded CFS network 공개. Norfolk는 2026-04-01부터 agent인 Norfolk Bonded Warehouse, 공개 FIRMS `LDF9` | CFS·transload, 국내 운송, order fulfillment, final mile |
| World Distribution Services | Los Angeles, Columbus, Norfolk의 bonded warehouse/CFS 공개 | drayage, 입고·pick-pack·kitting, 지역·전국 배송, B2C fulfillment |
| UPS Supply Chain Solutions | Dallas, Los Angeles, New York JFK, Chicago Gateway FTZ 공개 | customs·in-bond 관리, 창고 입고, multi-channel fulfillment, parcel·반품 |
| FedEx Supply Chain | FTZ·customs trade solution 공개, 이 스냅샷에는 정확한 FTZ 시설 미기록 | 창고·fulfillment·parcel network·반품 |
| GEODIS | 미국 FTZ 서비스 공개, 정확한 시설은 견적 시 확인 | customs·in-bond·창고·eFulfillment·반품. 공식 자료상 미국 bonded warehouse는 현재 제공하지 않음 |
| DHL Express United States | 보세창고/FTZ 사이 Bonded Transit을 제공하나 자체 보관시설은 미기록 | customs service·in-bond·개별 주소 express delivery |
| SEKO Logistics | 자체 customs-controlled 보관시설은 미기록 | customs clearance·창고·ecommerce fulfillment·delivery·반품 |
| NFI Industries | 자체 customs-controlled 보관시설은 미기록 | port drayage·transload 뒤 일반 창고 입고·ecommerce fulfillment·반품 |
| Ryder Supply Chain Solutions | 자체 customs-controlled 보관시설은 미기록 | port-to-door, 일반 창고, kitting·DTC fulfillment·last mile·반품 |

이 표는 한 업체와 단일 end-to-end 계약이 가능하다는 보증이 아니다. 특히 STG Norfolk처럼 브랜드가 아니라 agent 시설이 실제 보세 역할을 맡을 수 있다. 공개된 `LDF9`도 운영 시점의 ACE·CBP 상태를 다시 확인한다. 식품·주류·의약품·위험물은 FDA·USDA·TTB 등 품목별 요건과 시설 취급 가능 여부를 별도로 확인한다.

## 공동구매 우선 후보 12개

아래 공개 조건은 2026-07-18 확인값이며 계약 전에 반드시 다시 확인한다. `공개 최소조건`은 최종 견적이나 홍달의 추천 점수가 아니다.

| 업체 | 적합성을 확인할 공동구매 구간 | 공개 최소조건·주요 제한 |
| --- | --- | --- |
| eFulfillment Service | 일반 상품, 소량·캠페인, 입고·보관·kitting·참여자 배송·반품 | 상시 주문은 최소 수량·setup fee·장기 계약 없음으로 안내하지만 crowdfunding은 일반적으로 200 backer 주문을 기대한다고 별도 안내 |
| Fulfillrite | 소형·경량 상품, crowdfunding, kitting, 참여자 parcel 배송 | 월 pick-pack 최소 USD 399, 약 140건, 월 account fee USD 59.99. 월 단위 계약이며 pallet wholesale·EDI retail distribution과 냉동·냉장 상품은 대상 아님 |
| ShipMonk | crowdfunding, batch kitting, 참여자 배송, 반품 | 예상 월 주문량과 pick fee 기반 Monthly Minimum 산식 적용, 개별 견적 필요 |
| ShipBob | 일반 상품과 포장된 상온 식품, lot·유통기한 관리, DTC·B2B 공동 재고 | 가격 요청과 시설별 취급 확인 필요. 미국 반입 시 merchant가 Importer of Record와 관세·규제 책임을 맡음 |
| Red Stag Fulfillment | 10lb 초과 등 크고 무거운 상품, parcel·pallet·kitting·반품 | month-to-month 또는 장기 계약 선택, custom quote 필요 |
| Americold | 냉장·냉동 식품의 수출입 연계, 보관·지역 배분 | 정확한 시설의 온도대·인증·가용 공간을 직접 확인 |
| ODW Logistics | 상온·냉장·냉동 식품, 창고·parcel·리테일 배분 | 정확한 시설과 상품별 food handling 조건을 직접 확인 |
| Barrett Distribution Centers | shared warehouse, 상온·냉장 식품, lot·batch·FIFO/FEFO | 정확한 시설의 FDA/AIB 등 인증과 온도대를 직접 확인 |
| NFI Industries | 항만 drayage·transload, 대량 입고, e-commerce 배송·반품 | 실제 운송·forwarding 수행 법인의 현재 권한과 견적 확인 |
| Ryder Supply Chain Solutions | port-to-door, 창고, kitting, DTC·retail, 반품 | 통합 계약형 후보로 custom consultation 필요 |
| CJ Logistics America | forwarding·customs, 창고, omnichannel fulfillment | 실제 통관·운송 수행 법인과 시설을 역할별로 확인 |
| UPS Supply Chain Solutions | 국제 forwarding·customs, 입고·창고, multi-channel fulfillment·반품 | 역할별 계약과 실제 법인·시설 권한을 별도 확인 |

플랫폼은 위 후보 중 하나를 자동 선택하지 않는다. 예를 들어 `NFI + 소형 campaign 3PL`, `licensed customs broker + 식품 cold-chain 3PL`처럼 역할이 나뉠 수 있다.

## 범용 조사 대상 23개

아래 순서는 중립적인 영문 회사명 알파벳순이다.

| 업체 | 공식 자료에서 확인한 주요 후보 역량 | 주 대상 구간 |
| --- | --- | --- |
| Americold | 온도관리 창고, 운송, 부가 작업, 수출입 지원 | 식품 콜드체인, 항만 수입 |
| Barrett Distribution Centers | shared·food-grade 창고, 재고, 상온·냉장, lot·batch | 식품·리테일 배분 |
| CJ Logistics America | 창고·풀필먼트, 옴니채널, 운송 관리, 포워딩·통관 서비스 | 기업 물류, 리테일, 수출입 |
| DHL Express United States | customs service, Bonded Transit, 미국 내 express delivery | 통관 전 보세운송과 개별 주소 배송 역할 |
| DHL Supply Chain | 창고, 전자상거래 풀필먼트, 운송 관리, 반품, 부가 작업 | 기업 계약 물류, 옴니채널 |
| DSV | 창고, 전자상거래, 재고, 반품, 운송 관리, 부가 작업 | 기업·성장 사업자 계약 물류 |
| eFulfillment Service | 입고·보관·배송·반품, campaign fulfillment, kitting | 소규모 사업자와 일회성 캠페인 |
| FedEx Supply Chain | 창고·유통, 전자상거래 풀필먼트, 재고, 반품·수리 | 미국 내 풀필먼트 |
| Fulfillrite | 소형 상품 DTC·crowdfunding, kitting, 반품 | 소형·경량 캠페인 배송 |
| GEODIS | 창고, 멀티클라이언트, 옴니채널, 재고, 반품, 부가 작업 | 기업·리테일 풀필먼트 |
| GXO Logistics | 창고·유통, 전자상거래, 옴니채널, 반품, 공유형 창고 | 대규모 계약 물류 |
| NFI Industries | 유통, 전자상거래, 전용 운송, 운송 관리, 브로커리지, 드레이지·환적 | 항만과 북미 내륙 연결 |
| ODW Logistics | 상온·냉장·냉동 식품 창고, 운송, parcel·retail 배분 | 식품·음료 물류 |
| Penske Logistics | 창고·유통, 멀티클라이언트 창고, 운송 관리 | 기업 물류와 공유 창고 |
| Phoenix Warehouse | Jersey City CBP bonded warehouse, drayage·창고·fulfillment·last mile | New York/New Jersey 항만 수입과 미국 유통 |
| Red Stag Fulfillment | 크고 무거운 상품, DTC·retail, pallet, kitting·반품 | 중량·부피가 큰 상품 |
| Ryder Supply Chain Solutions | 창고, 옴니채널, 운송 관리, 전용 운송, 마지막 배송, 반품·브로커리지 | 항만부터 소비자까지의 통합 흐름 |
| SEKO Logistics | customs clearance, forwarding, 창고·ecommerce fulfillment·delivery·반품 | 외부 보세시설 뒤 통관과 미국 유통 역할 조합 |
| ShipBob | 미국 풀필먼트, DTC·B2B, campaign, kitting, 식품 lot·유통기한 | 성장 전자상거래와 포장 식품 |
| ShipMonk | campaign, batch kitting, 옴니채널, 재고·반품 | crowdfunding과 성장 전자상거래 |
| STG Logistics | customs bonded CFS, transload, port-to-door, fulfillment·final mile | 미국 항만 보세화물과 국내 분배 연결 |
| UPS Supply Chain Solutions | 창고·유통, 전자상거래, 반품, 운송 관리, 포워딩·통관 서비스 | 글로벌 수입과 미국 유통 |
| World Distribution Services | bonded warehouse/CFS, drayage, pick-pack·kitting·B2C 배송 | LA·Columbus·Norfolk 반입과 분배 |

## API

미국 배포에서 다음 공개 조회 API를 제공한다.

```http
GET /api/v1/operations/third-party-logistics/providers
GET /api/v1/operations/third-party-logistics/providers/collective-purchase
GET /api/v1/operations/third-party-logistics/providers/bonded-to-door
```

첫 경로는 범용 23개 업체를 역량·시장 구간으로 찾는다.

| Query | 의미 |
| --- | --- |
| `q` | 회사명·키·역량·대상 구간 텍스트 검색 |
| `capabilityCode` | `ColdChain`, `EcommerceFulfillment`, `PortDrayage` 같은 역량 필터 |
| `segmentCode` | `FoodColdChain`, `PortAndImportDistribution` 같은 대상 구간 필터 |
| `page`, `pageSize` | 페이지 조회, `pageSize` 최대 100 |

두 번째 경로는 공동구매 역할 프로필이 있는 12개 업체만 찾는다.

| Query | 의미 |
| --- | --- |
| `q` | 회사명·단계·상품 취급·공개 조건 텍스트 검색 |
| `stageCode` | `PortDrayageAndTransload`, `BreakPackKittingAndRelabeling`, `ParticipantParcelFulfillment` 같은 단계 필터 |
| `productHandlingCode` | `SmallLightParcel`, `FrozenFoodByFacilityReview`, `HeavyOrBulkyGoods` 같은 취급 필터 |
| `engagementSignalCode` | `CampaignFulfillmentAdvertised`, `PublishedMonthlyMinimum` 같은 공개 조건 필터 |
| `page`, `pageSize` | 페이지 조회, `pageSize` 최대 100 |

공동구매 응답에는 업체 기본 정보와 함께 `CollectivePurchaseProfile`이 포함된다. 프로필은 단계, 상품 취급, 명시적 제한, 공개 상업 조건, 외부 책임, 공식 근거를 분리한다. 공개 조건에는 적용 범위와 검토일, `RequiresReconfirmationBeforeContract=true`가 들어가므로 상시 주문 최소 수량과 일회성 campaign 기준을 같은 의미로 오해하지 않는다.

세 번째 경로는 customs-controlled 보관부터 참여자 주소 배송까지의 역할 후보 10개를 찾는다.

| Query | 의미 |
| --- | --- |
| `q` | 업체·시설·도시·주·FIRMS code 텍스트 검색 |
| `stageCode` | `CustomsControlledStorage`, `InBondTransportation`, `ParticipantAddressFinalMileDelivery` 같은 단계 필터 |
| `storageModelCode` | `CustomsBondedWarehouse`, `ForeignTradeZone`, `ExternalControlledFacilityHandoff` 구분 |
| `stateCode` | 공식 페이지에서 시설 claim을 확인한 미국 주 필터. 시설 미기록 업체는 결과에서 제외 |
| `page`, `pageSize` | 페이지 조회, `pageSize` 최대 100 |

`BondedToDoorProfile`은 업체 서비스 claim과 시설 claim을 분리한다. 시설 claim에는 운영 주체가 provider인지 agent인지, 공개 FIRMS code, 검토일을 기록하지만 `CurrentAuthorizationNotIndependentlyVerified`와 재확인 플래그를 항상 유지한다. customs broker permit, bonded carrier authority와 단일 end-to-end 계약도 확인 전 상태이며 자동 배정·실행은 모두 `false`다.

견적 문의 전 다음 입력을 먼저 모은다.

- 품목·규제 분류, 원산지·공급자, 반입 항만 또는 미국 내 출발지
- unit·case·pallet·SKU 수, 중량·치수
- 온도대, lot·유통기한, 참여자 목적지 분포
- 일회성·반복 일정, kitting·포장·label 요구, 반품 정책
- Importer of Record, customs broker, 관세·세금 책임자

세 응답 모두 회사명 알파벳순이고 추천 점수나 기본 우선순위를 반환하지 않는다. `Evidence`에는 역량을 확인한 공식 URL과 검토일이 들어가며, `RegulatoryVerificationResources`에는 계약 전에 별도로 확인할 정부 자료가 들어간다. 한국 배포에서는 `404`와 `MarketNotAvailableInDeployment`를 반환해 미국 전용 후보가 국내 운영 데이터처럼 노출되지 않게 한다.

## 업체 문의 초안 준비

보세시설부터 참여자 주소 배송까지 조사한 10개 업체에는 공개 디렉터리와 분리된 관리자 전용 문의 준비 경로를 둔다.

```http
POST /api/v1/admin/operations/third-party-logistics/outreach/preview
```

이 API는 이메일이나 문의 양식을 제출하지 않는다. 영문 제목·본문, 공식 문의 채널, 확인한 출처와 준비 차단 사유만 반환한다. `서버관리자전용` 정책이 적용되며 자동 발송 endpoint는 제공하지 않는다.

2026-07-18 공식 연락 페이지를 다시 확인한 결과 Phoenix Warehouse만 회사 페이지에 `info@phoenix-warehouse.com`을 일반 문의 이메일로 공개하고 있었다. 나머지 9개 업체는 각 회사가 지정한 sales·new business·ask an expert 문의 양식을 사용한다. 지원·채용·투자자·개인 담당자 이메일을 영업 문의 주소로 바꾸어 쓰거나 회사명으로 주소를 추정하지 않는다.

| 업체 | 준비 채널 | 공식 문의 근거 |
| --- | --- | --- |
| Phoenix Warehouse | 공개 일반 문의 이메일 또는 공식 양식 | [Contact Phoenix Warehouse](https://phoenix-warehouse.com/contact-us/) |
| DHL Express United States | 공식 shipping inquiry 양식 | [Contact DHL Express](https://www.dhl.com/us-en/home/express/help-and-support/contact-dhl-express.html) |
| FedEx Supply Chain | supply-chain 문의 양식 | [FedEx Supply Chain](https://www.fedex.com/en-us/logistics/supply-chain.html) |
| GEODIS | Contact an Expert 양식 | [GEODIS Contact Us](https://geodis.com/us-en/contact-us) |
| NFI Industries | NFI Services Request Form | [NFI Services Request Form](https://www.nfiindustries.com/contact-us/nfi-services-request-form/) |
| Ryder Supply Chain Solutions | business needs 문의 양식 | [Ryder Contact Us](https://www.ryder.com/en-us/contact-us) |
| SEKO Logistics | New Business Inquiry 양식 | [SEKO Contact](https://www.sekologistics.com/en/contact/) |
| STG Logistics | Contact Sales 경로 | [STG Contact](https://www.stgusa.com/contact/) |
| UPS Supply Chain Solutions | Ask an Expert 양식 | [UPS SCS Contact](https://www.ups.com/us/en/supplychain/about/contact-us) |
| World Distribution Services | logistics expert 문의 양식 | [WDS Contact Us](https://www.worldds.net/contact-us/) |

초안에는 다음 내용을 명시한다.

- 아직 실제 화물·booking·tender·배차가 없는 탐색 문의임을 밝힌다.
- 홍달이 화물 브로커로 배차·운송계약·운임 수취를 수행하지 않는 경계를 밝힌다.
- 공개 자료에서 본 역량은 미검증 상태이며 정확한 법인, 시설, FIRMS, FTZ activation, carrier authority와 계약을 다시 묻는다.
- 업체 담당자가 원할 경우 플랫폼 계정 확인 뒤 비구속 가원장 역할 슬롯에서 검토에 참여할 수 있지만, 슬롯 참여 자체는 계약이나 지급 의무를 만들지 않는다고 알린다.
- 품목, 출발·도착지, 예상 물량, 일정, 시설·fulfillment·API/EDI 조건을 업체가 판단할 수 있을 정도로 제공한다.

미국 FTC는 B2B를 포함한 상업 이메일에도 정확한 발신·routing 정보, 기만적이지 않은 제목, 상업적 성격 고지, 유효한 실제 우편 주소, 작동하는 수신거부 방법과 신속한 수신거부 처리를 요구한다고 안내한다. 따라서 `SenderName`, 조직명, 발신·회신 이메일, 조직 URL, 유효한 실제 우편 주소가 없거나 발신자 정확성·주소 유효성·suppression list 확인·업체별 수동 검토를 명시적으로 확인하지 않으면 모든 초안을 `MissingSenderRequirements`로 차단한다. 자세한 기준은 [FTC CAN-SPAM compliance guide](https://www.ftc.gov/business-guidance/resources/can-spam-act-compliance-guide-business)를 따른다.

요건을 채워도 결과는 `ReadyForManualApproval`일 뿐 `AutomaticDispatchEnabled=false`를 유지한다. 매번 공식 페이지에서 수신 주소·양식 목적을 다시 확인하고 한 업체씩 승인한 뒤 제출해야 한다. 실제 발송을 붙이기 전에는 수신거부 저장소, 10영업일 이내 처리 절차, SPF·DKIM·DMARC, 발송 감사 이력과 비밀 관리가 별도로 준비되어야 한다.

## 후보에서 실제 참여자로 전환하는 절차

1. 게시글·가원장 참여자들이 품목, 물량, 출발·도착 지역, 온도대, 보관 기간과 필요한 역할을 합의한다.
2. 디렉터리에서 같은 조건을 광고한 업체를 중립적으로 좁힌다.
3. 참여자가 업체에 직접 연락해 시설 주소, 가용 공간, 최소 물량, 요금, SLA와 보험 조건을 확인한다.
4. 운송·해상 주선·통관·보세가 포함되면 해당 업무를 실제 수행하는 법인과 시설의 현재 권한을 공식 기관에서 확인한다.
5. 업체 담당자의 플랫폼 계정 역할을 확인한 뒤 `CustomsControlledFacilityOperator`, `InBondCarrier`, `DomesticFulfillmentOperator`, `ParticipantAddressDeliveryProvider` 중 실제 책임 범위에 맞는 가원장 슬롯에 자발적으로 참여시킨다.
6. 역할 참여는 조사 후보를 제휴 업체로 승격하거나 시설 자격·계약을 확정하지 않는다. 검증 완료 전에는 `ExternalCredentialVerified=false`와 별도 권한·계약 확인 상태를 유지한다.
7. 당사자 동의와 계약이 끝난 뒤에만 실제 계약 창고·운송사 또는 실행 역할로 별도 등록한다.
8. 실제 상태 변경은 기존 `API -> UseCase/Command -> Domain/Infrastructure` 경로와 실행 모드 정책을 따른다.

플랫폼은 후보 정보를 제공하고 공동 의사 형성을 돕지만 견적 수락, 업체 선정, 배차, 통관 대리 또는 계약 체결을 대신 확정하지 않는다.

## 역할별 공식 확인 경로

`3PL`은 여러 물류 서비스를 묶어 설명하는 업계 용어이며 미국의 단일 면허 종류가 아니다. 필요한 업무에 따라 확인 기관이 달라진다.

| 확인 대상 | 공식 경로 | 사용 기준 |
| --- | --- | --- |
| motor carrier·broker·freight forwarder 권한과 재정보증 | [FMCSA Licensing & Insurance Carrier Search](https://li-public.fmcsa.dot.gov/LIVIEW/pkg_html.prc_lisearch), [재정보증 요건](https://www.fmcsa.dot.gov/registration/broker-and-freight-forwarder-financial-responsibility-rule-overview-and-compliance) | 법인명·USDOT·MC/FF 번호로 현재 권한·보험·bond·BOC-3를 별도 확인 |
| Ocean Freight Forwarder·NVOCC | [FMC Licensing and Certification](https://www.fmc.gov/licensing-and-certification/) | 국제 해상운송 중개·주선 역할이 있을 때 Licensed & Bonded OTI 확인 |
| Customs bonded warehouse | [CBP Customs Bonded Warehouse 안내](https://www.help.cbp.gov/s/article/Article1853?language=en_US) | 관세 납부 전 보관이 필요한 경우 정확한 시설과 bond 상태 확인 |
| Customs broker | [CBP Permitted Customs Brokers Listing](https://www.cbp.gov/about/contact/brokers-listing) | entry를 제출할 법인과 관할 port의 현재 permit 확인. broker를 써도 Importer of Record의 책임은 사라지지 않음 |
| In-bond 이동 | [CBP Immediate Transportation 안내](https://www.help.cbp.gov/s/article/Article-1150?language=en_US) | 통관 전 이동의 ACE filing, in-bond number, bonded carrier와 도착 보고 확인 |
| Importer customs bond | [CBP Customs Bond 안내](https://www.help.cbp.gov/s/article/Article1072?language=en_US) | 거래별 single-entry 또는 continuous bond 필요 여부와 금액 확인 |
| FIRMS code | [CBP ACE FIRMS validation 안내](https://www.cbp.gov/sites/default/files/2024-08/Trade_Information%20Notice_ACE%20Validation%20of%20FIRMS%20Codes%20for%20In-bond%20Arrivals508_0.pdf) | 공개 주소가 아니라 ACE에서 현재 인정되는 정확한 시설 code 확인 |
| Foreign-Trade Zone | [U.S. FTZ Board 안내](https://www.trade.gov/about-ftzs) | FTZ designation과 해당 site의 CBP activation을 분리해 확인 |
| SmartWay 참여 | [EPA SmartWay Partner List](https://www.epa.gov/smartway/smartway-partner-list) | 선택적인 지속가능성 근거로만 사용하며 정부 추천으로 해석하지 않음 |

## 업체별 공식 근거

- [Americold cold chain](https://www.americold.com/), [import/export](https://www.americold.com/import-export/)
- [Barrett shared and food-grade warehousing](https://www.barrettdistribution.com/shared-and-dedicated-warehousing)
- [CJ Logistics America United States network](https://america.cjlogistics.com/about/network/united-states/)
- [DHL Express US customs services와 Bonded Transit](https://www.dhl.com/us-en/home/express/products-and-solutions/products-and-services-overview/customs-services.html)
- [DHL Supply Chain US](https://www.dhl.com/us-en/home/supply-chain.html), [warehousing](https://www.dhl.com/us-en/home/supply-chain/solutions/warehousing.html?locale=true)
- [DSV US contract logistics](https://www.dsv.com/en-us/our-solutions/contract-logistics)
- [eFulfillment Service](https://www.efulfillmentservice.com/), [crowdfunding](https://www.efulfillmentservice.com/kickstarter-fulfillment/), [취급 제한](https://www.efulfillmentservice.com/faq/restrictions/)
- [FedEx Supply Chain](https://www.fedex.com/en-us/logistics/supply-chain.html), [Trade Solutions와 FTZ](https://www.fedex.com/en-us/logistics/trade-solutions.html)
- [Fulfillrite crowdfunding](https://www.fulfillrite.com/services/crowdfunding-campaigns/), [서비스·가격·제한 FAQ](https://www.fulfillrite.com/faqs/)
- [GEODIS US warehousing](https://geodis.com/us-en/warehousing-and-value-added-logistics), [FTZ](https://geodis.com/us-en/warehousing-and-value-added-logistics/warehousing/foreign-trade-zone), [미국 bonded warehouse 비제공 고지](https://geodis.com/us-en/blog/bonded-warehouses-manage-us-tariffs-import-costs)
- [GXO](https://gxo.com/), [GXO Direct solutions](https://gxo.com/supply-chain-mgmt/gxo-direct/solutions/)
- [NFI solutions](https://www.nfiindustries.com/solutions/), [port services](https://www.nfiindustries.com/solutions/port-services/)
- [ODW food and beverage logistics](https://www.odwlogistics.com/industries/food-beverage)
- [Penske warehousing and distribution](https://www.penskelogistics.com/solutions/warehousing-and-distribution/), [transportation management](https://www.penskelogistics.com/solutions/transportation-services/transportation-management-solutions/)
- [Phoenix Warehouse Jersey City bonded 3PL](https://phoenix-warehouse.com/about-us/)
- [Red Stag Fulfillment](https://redstagfulfillment.com/), [heavy and bulky products](https://redstagfulfillment.com/products-we-handle-best/)
- [Ryder ecommerce and Port2Door fulfillment](https://www.ryder.com/en-us/e-commerce/e-commerce-fulfillment)
- [SEKO US customs clearance](https://www.sekologistics.com/emea-en/services/freight-forwarding/customs-clearance/), [retail·ecommerce logistics](https://www.sekologistics.com/en/industries/retailers/)
- [ShipBob USA fulfillment](https://www.shipbob.com/shipbob-locations/usa/), [food and beverage](https://www.shipbob.com/categories/food-and-beverage/), [terms](https://www.shipbob.com/terms-of-service/)
- [ShipMonk crowdfunding](https://www.shipmonk.com/fulfillment-solutions/crowd-funding-fulfillment), [pricing](https://www.shipmonk.com/pricing)
- [STG customs bonded CFS](https://www.stgusa.com/services/cfs-transload/), [managed logistics](https://www.stgusa.com/news-notices/stg-logistics-managed-logistics-solutions-built-for-todays-supply-chain/), [Norfolk agent·FIRMS LDF9 고지](https://www.stgusa.com/alerts/notice-stg-norfolk-cfs-transition-effective-april-1-2026/)
- [UPS supply chain services](https://www.ups.com/us/en/ups-supplychain), [warehousing and distribution](https://www.ups.com/us/en/supplychain/logistics-solutions/distribution), [Gateway FTZ](https://www.ups.com/us/en/supplychain/logistics-solutions/customs-brokerage/foreign-trade-zones)
- [WDS Los Angeles bonded warehouse](https://www.worldds.net/los-angeles-california-warehouse/), [Columbus bonded CFS](https://www.worldds.net/columbus-ohio-warehouse/319/), [Norfolk facility specification](https://content.worldds.net/hubfs/WorldDS-Assets/Sell-Sheets-and-Brochures/WDS-NOR-Spec-Sheet_Final.pdf), [retail·direct-to-consumer distribution](https://content.worldds.net/meet-wds-old)

## 갱신 원칙

- 공식 회사·정부 페이지를 우선하고 상업 순위 사이트의 순위를 가져오지 않는다.
- 서비스 역량을 추가할 때는 같은 역량을 뒷받침하는 `Evidence` URL을 반드시 같이 추가한다.
- 회사명 변경, 인수합병, 서비스 중단과 URL 변경을 검토할 때 `CatalogVersion`과 `SnapshotReviewedOn`을 함께 갱신한다.
- 공개 최소조건은 출처·적용 범위·검토일·재확인 필요 상태와 함께만 저장한다. 실제 견적, 시설 여유, 처리 속도와 인증 상태는 정적 카탈로그에 확정값으로 저장하지 않는다.
- 향후 공식 레지스트리 연동을 붙이더라도 회사 광고 근거와 규제 검증 결과를 서로 다른 시각·상태로 보존한다.

이 자료는 법률 자문, 업체 추천 또는 계약상 보증이 아니다. 실제 거래 전 해당 업체와 관할 전문가를 통해 최신 조건을 확인해야 한다.
