# Shipper FCL/LCL Planning

FCL/LCL planning belongs before the final transport request. A shipper often needs to decide whether overseas cargo should move as a full container load, less-than-container load, or a comparison quote before asking a shipping agency, forwarder, customs broker, or domestic carrier to proceed.

## Purpose

- Let the shipper understand whether cargo volume is closer to FCL or LCL.
- Help the shipper think before purchase, not only after purchase.
- Estimate pallet-based shipping agency cost and rough landed unit cost.
- Compare purchase cost, customs/tax estimate, shipping agency cost, sale price, and expected margin.
- Prepare a community demand-check post for early buyers or interested customers.
- Prepare the discussion with a shipping agency or forwarder.
- Connect overseas warehouse, customs review, and domestic transport decisions.
- Avoid treating international shipping as a simple domestic transport request too early.

## Current UI

- Route: `/shipper/international/fcl-lcl`
- App: `HongdalApp`
- Entry points:
  - Shipper home quick actions
  - Work mode transport tab
  - Work mode customs tab
  - Shipper navigation menu
  - Profile menu

The first implementation is a planning and negotiation helper. It does not create a final booking or quote yet.

## Decision Inputs

- Product name
- Total CBM
- Total weight
- Pallet count
- Expected unit count
- Purchase unit price
- Expected sale unit price
- Shipping agency fee per pallet
- Estimated customs/tax rate
- Departure urgency
- Whether consolidation is allowed
- Whether exclusive handling is preferred

## Output

The screen produces one of these planning directions:

- `LCL 우선`: smaller cargo, consolidation allowed, schedule not urgent
- `FCL 검토`: cargo volume or pallet count is high enough to compare full-container pricing
- `FCL 우선`: exclusive handling, damage risk, temperature, brand, or security conditions exist
- `비교 견적`: middle range where FCL/LCL quote, sailing schedule, and domestic transport cost must be compared

It also shows:

- Estimated shipping agency fee
- Estimated customs/tax amount
- Estimated landed unit cost
- Expected revenue
- Expected profit
- Expected margin rate
- A community demand-check draft for early purchase interest

## Related Parties

The planning surface exposes these related parties through the sensitive disclosure pattern:

- Shipper
- Shipping agency operator
- Forwarder
- Customs broker
- Domestic carrier
- Early buyers or interested customers

## Next Expansion

- Store FCL/LCL planning drafts server-side.
- Store community demand-check posts as a structured post type.
- Create an import demand campaign from the FCL/LCL planner so orderers can leave non-binding purchase intent before the shipper commits to the import.
- Let the FCL/LCL planner open the community writer with the generated draft already filled in.
- Add quote comparison records from shipping agencies or forwarders.
- Connect selected mode to inbound contract and import/customs fulfillment.
- Convert confirmed overseas movement into domestic transport or reconsignment request after arrival.
