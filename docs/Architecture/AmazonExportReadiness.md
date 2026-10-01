# Amazon Export Readiness

This document defines the first skeleton for exporting domestic products through Amazon.

## Purpose

The group-purchase flow currently emphasizes import: overseas purchase, shipment tracking, customs release, bonded-area pickup, and domestic distribution. Hongdal also needs a balanced export path where domestic inventory can be listed on Amazon and fulfilled to overseas buyers.

The first implementation does not call Amazon SP-API directly. It separates readiness gates so the platform can confirm listing, market story, customs, and fulfillment data before connecting real external APIs.

## Flow

1. A domestic inbound product becomes a sales product.
2. The export candidate is limited first to users or sellers who participated in the import flow, so the platform can reuse known logistics evidence and actual buyer experience.
3. Korean logistics process records are collected: inbound, storage, inspection, product journey, outbound batch, and handover evidence.
4. User reviews are selected only when usage consent and review suitability are confirmed.
5. The Amazon detail content is prepared as image assets for listing detail content and advertising creatives, not just as an HTML page.
6. The seller connects an Amazon sales-channel account.
7. The product is prepared as an Amazon listing draft.
8. Export HS review, export restriction checks, customs broker assignment, broker fee, and export declaration plan are confirmed.
9. Commercial invoice and packing list are prepared.
10. Warehouse inventory is reserved and connected to an outbound batch.
11. A fulfillment route is selected:
   - `FbmInternationalShipping`: seller or platform ships internationally after order
   - `FbaInbound`: inventory is shipped to Amazon FBA
   - `ExportForwarderHandover`: inventory is handed to an export forwarder
   - `Manual`: operator manages the route manually
12. International shipping, return policy, settlement currency, and platform fees are confirmed.

## Market Story And Detail Image

Amazon export should use verified domestic records instead of generic product copy. The platform stores whether:

- the exporter is an eligible participant from the import or group-purchase flow
- Korean logistics history has been recorded
- product journey evidence is ready
- user review usage consent is confirmed
- usable review count is greater than zero
- image-form detail page assets are generated and approved
- advertising creatives are ready

This keeps the export page tied to real product handling and buyer experience. Reviews and logistics records should be filtered for privacy and consent before they are transformed into detail images or advertising assets.

## Readiness Gates

| Gate | Required action code |
| --- | --- |
| Amazon seller account is connected | `ConfirmAmazonSellerAccount` |
| Marketplace and seller id are known | `ConfirmMarketplaceAndSellerId` |
| Product type and Product Type Definition are confirmed | `ConfirmProductTypeDefinition` |
| Listing payload, image, and description are mapped | `ConfirmListingPayloadMapping` |
| Exporter is eligible through the import participation flow | `ConfirmImportParticipantEligibility` |
| Korean logistics and product journey evidence are recorded | `ConfirmKoreanLogisticsTrace` |
| Review usage consent and usable review count are confirmed | `ConfirmReviewUsageConsent` |
| Image-form detail page and advertising creative are approved | `ConfirmAmazonDetailPageImageAsset` |
| Export HS code and restriction review are confirmed | `ConfirmHsExportReview` |
| Customs broker consultation, assignment, and declaration plan are confirmed | `ConfirmCustomsBrokerEngagement` |
| Customs broker fee is confirmed | `ConfirmCustomsBrokerFee` |
| Commercial invoice, packing list, and origin are ready | `ConfirmExportDocuments` |
| Inventory reservation and outbound batch are ready | `ConfirmInventoryAndOutboundBatch` |
| Fulfillment route is selected | `ConfirmFulfillmentRoute` |
| Amazon FBA inbound eligibility is confirmed | `ConfirmAmazonFbaInboundEligibility` |
| Chilled/frozen cargo uses a non-FBA cold-chain route | `ConfirmNonFbaColdChainFulfillmentRoute` |
| International shipping plan is confirmed | `ConfirmInternationalShippingPlan` |
| Return policy, currency, and fee settlement are confirmed | `ConfirmReturnsAndSettlementPolicy` |

## Cold Chain And FBA

Amazon Seller Central states that FBA does not fulfill fresh produce, chilled foods, or frozen foods. The planner therefore treats `FbaInbound` plus `RequiresChilledOrFrozenHandling=true` as not ready and opens `ConfirmNonFbaColdChainFulfillmentRoute`.

## Implementation

- Planner: `Hongdal.Contracts/Common/Sales/AmazonExportReadinessPlanner.cs`
- Tests: `Hongdal.Tests/Contracts/Common/Sales/AmazonExportReadinessPlannerTests.cs`
- Payload draft builder: `HongdalApp/Services/Commerce/Amazon/AmazonSpApiProductPayloadBuilder.cs`

The planner returns two separate readiness flags:

- `ReadyForAmazonListingDraft`: listing-side Amazon data is ready enough to build or review a draft payload.
- `ReadyForExportFulfillment`: listing, market story, customs broker, documents, inventory, outbound, shipping, returns, and settlement gates are all clear.

This keeps Amazon export work aligned with the existing channel listing module while leaving room for real SP-API credentials, Product Type Definitions schema mapping, FBA shipment creation, and customs broker workflows later.
