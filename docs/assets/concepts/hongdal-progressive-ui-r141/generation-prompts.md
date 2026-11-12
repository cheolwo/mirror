# r141 시안에 실제 사용한 프롬프트

- 도구: 내장 `image_gen.imagegen`. 하위 모델/버전은 도구 응답에 제공되지 않아 미확인으로 둔다.
- 순서: 최초 시안 4회, 국소 보정 2회. 선택 출력 4장. 별도 Google/Kling API 제출은 없다.
- 기존 r137 실제 화면은 색·구성 참조이며 새 이미지는 생성 시안이다. 구매 에셋의 실제 Unity 렌더가 아니다.
- 프롬프트의 좌표·시간·장소·상태는 화면 구성용 합성 조건이다. 정산 금액은 r141이 연결한 기존 합성 회차의 검증 기록을 따른다.

## 1. 01-first-dispatch.png

참조: `artifacts/local/hongdal-unity-operator-expansion-20261111/manual-release/00-overview.png` · 선택 출력

```text
Edit the provided screenshot into image 1 of a four-image progressive game UI concept for the Korean Windows game "홍달 경영 시뮬레이션". This is a visual proposal, not an actual implemented game. One polished full-screen landscape 16:9 game UI image, no monitor/device frame, no collage.

REFERENCE INVARIANTS: Preserve the dark navy/teal map palette, restrained mint accent, professional Korean typography, geographic road layout and map extent from the reference. Keep the large map as the immersive space. Use the reference only for layout/style, do NOT copy its broad franchise panel, bottom four big cards, dense building footprints, inflated scenario metrics or commentary.

FIXED GEOGRAPHIC COMPOSITION for all four images: same near top-down road map, same camera and framing. Store A around 50% width / 55% height of the map; store B around 33% width / 26% height. Delivery point 1 at 70% width / 74% height, point 2 at 63% width / 25% height. Thin clear muted roads with a few subtle flat block outlines. Mint motorcycle courier A initially to southwest of Store A (38%,65%); purple motorcycle courier B near Store B (26%,34%). Represent couriers by recognizable small motorcycle sprites, NOT large text labels or circles. Stores are simple compact shop pins with a small order alert containing "1" above each. No elevated city or crowds. No full-city filler buildings.

STATE 1 FIRST START: exactly two available couriers, two waiting orders, zero settled. Clean flat map with pins, roads, motorcycles; NO raised buildings yet. The header must be compact, leave at least 80% of screen to map. Small top-left title "홍달 경영 시뮬레이션", understated subtitle "가상 운영". Compact status text "11:30" / "대기 2건" / "정산 0건". Top-right a mint "운영 시작" button, small pause/speed controls; no spiritual/time-income dashboard yet. Map labels only "매장 A", "매장 B", "배송지 1", "배송지 2". A small unobtrusive focus guidance card at bottom center with text "기사 선택 → 매장 연결". Bottom-left ONE compact folded menu button "운영 메뉴"; management functions remain accessible here but no giant four-tab row. No right-side detail panel at the start. Map remains clearly visible.

Use crisp visual hierarchy, well-spaced rounded small panels, restrained outlines, readable Korean text. Include a modest top-right badge "① 첫 시작", and bottom-right caption "표현 기획 시안 · 미구현". Rich crafted game UI quality, but minimal ornament. Do not add other metrics, money, buttons, stages, locks, reward cards or tutorial arrows. This is the minimal initial state, later images reveal only related places.
```

## 2. 02-initial-unselected

참조: `docs/assets/concepts/hongdal-progressive-ui-r141/01-first-dispatch.png` · 미채택 중간 출력, 보정 전 원본 보존

```text
Edit the provided image into STAGE 2 of the SAME Korean game UI concept sequence. Keep identical landscape dimensions, camera, geographic road map, framing, Store A and Store B coordinates, delivery point 1 and 2 coordinates, fonts, navy background, mint accents and purple courier identity. Preserve composition from this exact first concept. This is the second frame, not a redesigned game.

SCENE: courier A (mint motorcycle) has been assigned to Store A, accepted, arrived and PICKUP IS NOW CONFIRMED, starting the delivery to destination 1. Purple courier B and Store B's one unassigned order remain in the same positions as before. Exactly one waiting order, one delivery in progress, zero settled.

Only around Store A, replace the flat shop pin illustration with a modest shallow-relief shop building: same mint canopy, a clearly readable small roof, short side wall and short soft shadow. A subtle restrained foundation/nearby two low flat block outlines create local place detail. Do NOT raise the whole district. Store B remains its original small flat shop symbol. The rest of the map stays flat and uncluttered, same roads and geography. Remove Store A's unassigned-order "1" alert after pickup. Store B retains its "1" alert.

Add a clear thin mint delivery path from Store A to destination 1, FOLLOWING THE EXISTING DRAWN ROADS through their bends and intersections, never a straight line across blocks. Move mint courier A a short distance along that path just beyond Store A and turn the motorcycle to face its direction of travel. No duplicate A motorcycle, no extra courier. Purple motorcycle stays unchanged. Keep the four place labels and map positions consistent.

Update header: title remains "홍달 경영 시뮬레이션", subtitle "가상 운영", time "11:32", status "대기 1건" and "배송 중 1건". Replace the large start action with a compact "운영 중" status. Keep speed/pause controls unchanged. Badge at top right "② 선택과 픽업". Bottom-left folded "운영 메뉴" unchanged.
Introduce ONE compact right-edge detail card (about 18% width), not covering Store A or destination 1. Accurate short Korean text only:
"매장 A"
"픽업 완료"
"담당 기사 A"
"배송지 1로 이동 중"
Small subtle card close control. No driver portrait, no tarot, no currency metrics.
The bottom guidance is now a smaller nonblocking strip "픽업한 매장이 동네에 남습니다".
Bottom-right "표현 기획 시안 · 미구현".
Professional polished game UI, readable sparse Korean, map remains primary. Every change must express revealing relevant detail through play; keep all unrelated map and UI unchanged.
```

## 3. 02-pickup-place-reveal.png

참조: `02-initial-unselected` · 선택 출력

```text
Make one precise correction to this game UI concept image. Preserve ALL pixels and visual design as closely as possible except the erroneous mint path branch: courier A is delivering ONLY from 매장 A to 배송지 1 at the LOWER RIGHT. 배송지 2 near the UPPER CENTER belongs to the still-unassigned Store B order and MUST NOT have any highlighted route.
REMOVE the entire mint branch that rises from the road junction near courier A up toward 배송지 2. Restore the same muted underlying road appearance there. Retain ONLY a single unbranched mint path from Store A through courier A's current road position to delivery point 1 in the lower right. No line at all ending at delivery point 2. Follow the drawn roads as closely as possible. Preserve store and courier positions, every label, header, right detail card, Store A shallow building and bottom badges completely unchanged. Do not add objects.
```

## 4. 03-initial-unselected

참조: `docs/assets/concepts/hongdal-progressive-ui-r141/02-pickup-place-reveal.png` · 미채택 중간 출력, 보정 전 원본 보존

```text
Edit the supplied STAGE 2 concept into STAGE 3 of the exact SAME progressive Korean game UI. Keep identical landscape dimensions, geographic road map, camera, positions of Store A and Store B, positions of delivery points 1 and 2, font, layout, navy/mint palette, courier colors, folded "운영 메뉴", and lower-right concept disclaimer.

SCENE 3: time passes in this same fictional round, both original orders have now been picked up, delivered, received and virtually settled. Exactly two couriers, zero waiting orders, two settled orders. NO active delivery routes remain, remove the mint highlighted route entirely. Mint courier A is parked just beside delivery point 1 in lower right; purple courier B is parked beside delivery point 2 in upper center. Couriers remain recognizable small motorcycle sprites. Do not draw duplicate couriers.

KEEP Store A as the exact same little mint shallow-relief shop (roof, low side wall, short shadow). Store B is now also a matching low-relief shop with its own PURPLE awning, at the SAME Store B map coordinate; remove its order alert "1". Both shops now represent completed business relationships and remain visible. Surrounding district stays flat, with only a few subdued footprints; do not create a cityscape. Preserve identical roads and framing to show cumulative place memory.

Header: "홍달 경영 시뮬레이션", subtitle "가상 운영", time "11:38", status "대기 0건" and "정산 2건". Keep pause/speed controls. Compact green status "회차 완료". Top-right stage badge "③ 첫 회차 정산".
Replace right detail card with ONE tidy settlement card, aligned to the same right edge, narrow enough not to cover any store or delivery point. Text MUST be accurate Korean, no other metrics:
"가상 정산"
"수령·정산 2건"
"플랫폼 수입   9,600원"
"기사 보수     7,200원"
"기본 차액     2,400원"
"보너스        1,200원"
"보너스 포함 차액   3,600원"
Use the last total as the mint-emphasized row. Small explanatory caption "기존 합성 회차 금액". Small X close control, no giant modal, no extra proceed/upgrade/claim button.
Bottom-center small calm message "거래한 매장이 동네에 남았습니다".
Bottom-right "표현 기획 시안 · 미구현".
This is an illustrative proposal showing a completed round, not an actual live game screenshot. Keep map dominant and avoid expanding the whole district.
```

## 5. 03-settlement-memory.png

참조: `03-initial-unselected` · 선택 출력

```text
Make a strictly localized correction to this UI concept. Only change the tiny building illustration at Store B (upper-left, directly below the text 매장 B). It currently looks like the old flat storefront pin. It should now be a small shallow-relief BUILDING like Store A: clearly visible pale purple flat roof TOP SURFACE, a short right side wall, front door, purple awning and a short soft shadow. Use the same size, low height and illustrative perspective as the mint Store A building. Keep the anchor coordinate and 매장 B label unchanged. Do not add surrounding raised buildings.

Leave ALL other elements exactly unchanged: road map, Store A, two courier positions at destinations, all money amounts, right settlement card, header, stage badge, footer and folded menu. Only Store B gets its earned permanent low-relief building representation after pickup.
```

## 6. 04-capacity-guidance.png

참조: `docs/assets/concepts/hongdal-progressive-ui-r141/03-settlement-memory.png` · 선택 출력

```text
Create STAGE 4 by editing this exact stage 3 game UI concept. Preserve identical landscape dimensions, same camera and geographic road map, same Store A and Store B coordinates, same delivery point coordinates, same navy/mint color scheme, fonts, folded bottom-left 운영 메뉴 and two shallow-relief shops: A mint, B purple. Do NOT add a city, new district, new buildings or new stores. The shops' business-memory appearance remains.

IMPORTANT: This is a SEPARATE FICTIONAL CAPACITY EXAMPLE, not an automatically generated continuation or an actual measured result. Show subtitle "별도 가상 상황 예시" below the main title. It is later noon; two existing couriers are both busy on other fictional delivery jobs, and ONE additional order at Store A is waiting unassigned. There are exactly two couriers, two busy, zero currently eligible to assign, one waiting. No newly recruited couriers or hiring controls.

Header title "홍달 경영 시뮬레이션", time "12:10", concise status "대기 1건" and "배차 가능 0명"; keep 운영 중 and pause/speed controls. Top-right badge "④ 관리 필요".
Move mint motorcycle A to the road just southeast of Store A; purple motorcycle B to the road a little east of Store B, each plausibly riding along the existing road. Exactly one motorcycle per courier. No highlighted route lines needed in this frame. Store A's waiting order alert above the building is a small amber bubble with "1"; Store B has no alert. Keep the map sparsely detailed and focused.

Replace the settlement card at the right with ONE compact attention/management card: navy with restrained AMBER outline at the top to signal a bottleneck rather than danger. Accurate text:
"기사 현황"
"배차 대기 1건"
"현재 배차 가능 0명"
"기사 A · 배달 중"
"기사 B · 배달 중"
"업무 중인 기사 상태를 확인하세요"
A small existing-management entry button "기사 현황 보기".
X close control. No money amounts, no level/upgrade metrics, no hire/recruit button, no locked features, no fake automatically deployed staff. This introduces the appropriate existing management view only when relevant.

Bottom-center discreet guidance "필요할 때 관련 관리 화면을 소개합니다".
Bottom-right "표현 기획 시안 · 미구현".
Keep all map anchors and low-relief place memory consistent with prior images. The operating world stays simple and immersive, management UI unfolds only for the current need.
```
