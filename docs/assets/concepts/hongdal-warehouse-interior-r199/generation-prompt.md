# 물류센터 작업 상세 시안 r1 — 생성 입력

- 방식: 내장 `image_gen` 도구 · 새 이미지 생성 1회 · 배경 불투명.
- 결과: `warehouse-workflow-r1.png` 한 장. CLI/API 우회·후속 편집/변형 생성 없음.
- 원본은 Codex 생성 이미지 폴더에 유지하고 선택 결과를 프로젝트에 바이트 그대로 복사했다.
- 이 입력의 공간/수치/라벨은 가상이다. 생성 결과를 구현 계약/실제 시설 정보로 승격하지 않는다.

## 최종 프롬프트

```text
Use case: ui-mockup.
Create exactly ONE polished landscape 16:9 concept image for the Korean Windows management game "홍달 경영 시뮬레이션". It is a NEW reference concept, not an edit and not a real facility blueprint. Show a highly legible roofless warehouse cutaway, viewed from an elevated three-quarter isometric angle, with detailed tangible low-poly 3D objects, in a dark navy game interface with restrained mint accents matching Hongdal. The warehouse is the dominant large viewport, roughly 82% of the image. Make the actual spatial work much more detailed than a schematic: open rack bays, cartons on individual shelves, floor-location markings, workers handling goods, packing benches, conveyor outlets and trucks. Clean, crisp, appealing game art with soft ambient shadows and readable spatial depth; no photoreal warehouse photograph.

Composition: a spacious single representative work floor arranged left-to-right for visual understanding. Keep the roof and near walls removed. This is a fictional composite layout gathering the operations on one floor for this one explanatory image; it does not prescribe the final five-floor building.
Left: an inbound dock with a delivery truck partly outside, a worker checking incoming cartons with a handheld reader, a receiving bench, a small separate hold area. Taped rectangular floor storage positions, each visibly carrying a location label with short code and barcode graphic, some holding palletized cartons, some empty.
Middle/back: 3 or 4 separated rows of waist-to-head-height steel storage racks, visible uprights and multiple shelf levels and individual bins containing boxes/products. Rack beams carry distinct white location labels with barcode strips and short location codes. A worker at a reachable lower bay scans the location and places an actual carton into a mint tote on a small picking cart. Leave obvious walkable aisles between the racks. Another worker transports a tote toward the packing stations.
Middle/front: two packing benches, with an open carton containing goods, a mint order tote, packing material, tape and a small label printer. A worker actively packs, another sealed parcel is at a physically connected conveyor induction point.
Right: the conveyor leads through one visible barcode reader arch to a clear sorter split with TWO distinct receiving outlets. Outlets have controlled short sloping chutes/rollers leading into waist-high receiving buffers. One outlet has a worker collecting DIFFERENT parcels onto a wooden pallet in a marked staging rectangle, with a pallet jack and an outbound truck beyond. The other outlet goes toward a separate truck's tail via a short dock conveyor, where a worker directly loads individual parcels. These are two ALTERNATIVE shipping paths. Show stable boxes on supported surfaces, with workers at the handoff points. No boxes falling freely, floating, passing through walls, or duplicated on both branches.
Use only a few thin mint directional cues to make receiving -> racks -> picking tote -> packing -> sorter -> two outbound branches clear. Do not blanket the image in neon paths.

Show three different visual identity types: black-and-white LOCATION labels on racks and floor (e.g. "A-02-01", "B-02"); PRODUCT labels on goods; PARCEL/container labels on sealed cartons or a pallet. No actual GS1 serial claims or real brand logos. Include ONE small integrated close-up callout connected to the selected lower rack bay: clearly show a rack location label, barcode pattern, and the carton/tote below it. This callout is part of this one image, not a separate panel collage.

UI: narrow top bar title exactly "홍달 경영 시뮬레이션", subtitle "물류센터 · 내부 작업", top-right button "지도로 돌아가기". Small unobtrusive badge "가상 배치 · 시안". The main zones get just six clean readable Korean labels placed near the corresponding physical places: "입고·검수", "적치·보관", "피킹", "포장", "분류", "출고·상차". A compact lower-right selection card reads "선택 위치", "A-02-01", "위치 확인 → 물품 확인 → 집품". Slim bottom strip "작업 대기열" with four circular task icons labeled "입고", "피킹", "포장", "상차", plus a visually separate "다음 작업" button. Do not show completed rewards or fake performance dashboards. The short text must be large and spelled correctly. Use typography only as supporting information so the actual work remains the focus.
Constraints: exactly one coherent image, no before/after collage, no extra concept variations, no company branding, no multi-story floors stacked over the detailed work, no gigantic decorative barcodes, no dense fine-print text, no soldiers, no forklifts operating through pedestrian aisles.
```
