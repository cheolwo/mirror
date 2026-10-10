# 역할 지도 공통 화면 배치 미리보기

예외 복구 책임 분리 r2는 `/exception-refactoring-preview/{scenario}`에서 실제 `FoodRoleActionForm`과 `보호지원관리Page`를 사용합니다. `orderer/restaurant-accept/restaurant-time/food-driver/operator/support-stored/support-retry`의 입력·확인·결과 조회·명시 재시도를 격리 메모리 자료로 확인합니다. 음식 업무의 실제 전송은 차단하며, 지원 사건의 쓰기는 메모리 fixture에만 적용됩니다. 운영 API·DB·알림·지급을 호출하지 않습니다.

기존 실행 중인 호스트를 종료하지 않고 새 출력 폴더에 빌드합니다. 저장소 루트에서 `dotnet build eng/RoleWorkspacePreview/RoleWorkspacePreview.csproj --no-restore -p:OutDir=C:/Users/user/source/repos/Hongdal/artifacts/local/exception-continuity-refactoring-r2-20261010/preview-runtime/`로 빌드한 뒤 `dotnet artifacts/local/exception-continuity-refactoring-r2-20261010/preview-runtime/RoleWorkspacePreview.dll --contentRoot eng/RoleWorkspacePreview --LifeFacilitationPreviewPort=5398`로 실행합니다. `node eng/RoleWorkspacePreview/verify-exception-refactoring.mjs`는 390×844에서 동작·가로 넘침·브라우저 오류·업무 및 외부 HTTP 시도 여부를 확인합니다. 원시는 `artifacts/local/exception-continuity-refactoring-r2-20261010/`에 기록합니다.

이 검증의 `before/`는 리팩터링 직전에 확보한 로컬 자료가 필요합니다. 최초 기준선의 제품 스타일 누락은 그대로 보존하고, 스타일 비교는 원본 DOM·원본 CSS를 같은 테마 아래 정적으로 렌더한 별도 참조를 사용합니다. 이 참조는 과거 대화형 런타임의 복원 증거가 아닙니다. [r2 검증 결과와 한계](../../docs/Changes/2026-10-10-exception-continuity-refactoring-r2.md)를 따릅니다.

생활 배송 처리처 r24는 `/neighborhood-fulfillment-preview/{scenario}`에서 실제 공통 `NeighborhoodDeliveryCreate`·`NeighborhoodDeliveryDetail` Razor와 작성·조회 VM을 사용한다. `create/create-lost/feature-off/expired/accepted/unknown/awaiting-selection/cancelled/completed/error/anonymous`를 제공하며, 비용·정보 제공 동의, 응답 유실 시 같은 요청 확인, 미선정 접수의 복구, 취소 확인 전 재선택 차단, 취소 후 새 자체 배차 선택을 검토한다. 작성 장면에서는 실제 거래 안내를 먼저 확인하고 화면의 견적 버튼을 눌러야 한다. 모든 처리처는 메모리 안의 합성 자료이며 업무 HTTP·DB·외부 업체 접수·실제 기사 배차·지급을 호출하지 않는다. `create-lost`의 접수 결과는 의도적으로 미확정으로 유지하여 실제 제품 경로로 이동하지 않는다. 서버의 영속 멱등성·권한 검증은 별도 서비스 시험이 소유한다.

최종 빌드 후 `dotnet run --project eng/RoleWorkspacePreview --no-build -- --LifeFacilitationPreviewPort=5574`로 루프백 호스트를 시작한다. 설치 Playwright 모듈의 디렉터리를 `NODE_PATH`에 지정하고, 필요하면 설치 Chromium 경로를 `SSALDDEL_PLAYWRIGHT_CHROMIUM`에 지정한다. `SSALDDEL_FULFILLMENT_PREVIEW_URL=http://127.0.0.1:5574`로 `node eng/RoleWorkspacePreview/verify-neighborhood-fulfillment.mjs`를 실행한다. 검사는 업무 API 및 외부 HTTP를 차단하며 320/390px의 실제 DOM 행동·오류·가로 넘침·스타일시트·소스/바이너리 SHA-256을 기록한다. 원시는 `artifacts/local/neighborhood-fulfillment-r24/ui/`이며 `SSALDDEL_FULFILLMENT_PREVIEW_OUTPUT`으로 저장소 안 `artifacts/local` 아래 경로만 변경할 수 있다. 실행 전 구성 준비를 검증 통과로 표현하지 않는다. [페이지 책임 카드](../../docs/ProjectOverview/page-docs/neighborhood-fulfillment-r24.md)를 따른다.

r22는 `verify-tools-ui.cjs`로 필수·복구 행동과 기본 접힌 업무 도구를 320/390px에서 확인한다. 설치 Playwright는 `NODE_PATH`, 브라우저 경로는 `ROLE_TOOLS_BROWSER_PATH`로 지정한다. 원시 결과는 `artifacts/local/role-tools-r22/ui`다. `/community/map`은 실제 `NeighborhoodExchangeMapPage`를 별도 읽기 전용 예시 포트로 조립하며 도구의 기존 등록/목록 route와 선택 문맥 복귀를 검토한다. 저장·동의·배차·업로드·결제는 차단한다. 아래 `/workspace/community`의 이전 일반 역할 예시는 실제 생활 지도 검증으로 사용하지 않는다. [r22 변경 기록](../../docs/Changes/2026-10-06-role-tools-r22.md)을 따른다.

r21은 운영자 사건 목록/검토, 음식·화물 기사 내역 목록/상세와 `/handoff-preview/neighborhood`의 기본 보류 안내를 제품 공통 컴포넌트로 검토한다. 관련 자료는 [r21 변경 기록](../../docs/Changes/2026-10-06-role-handoff-fixes-r21.md)이다. 사건 목록과 기사 내역은 `scenario=empty/permission/feature-off`, 음식 상세는 `scenario=expired`, 화주 기본은 `scenario=held`를 제공한다. 검토 PUT과 생활 배송 쓰기는 차단한다. `verify-handoff-ui.cjs`는 설치된 Playwright를 `NODE_PATH`로, 설치된 Chromium 계열 브라우저를 선택적으로 `HANDOFF_BROWSER_PATH`로 받아 로컬 320/390px 렌더·이동·캡처를 확인한다. 원시는 Git 제외 `artifacts/local/role-handoff-fixes-r21/ui`에 쓴다.

`dotnet run --project eng/RoleWorkspacePreview`로 로컬 `http://127.0.0.1:5392/`에서 실행한다. 지도 키를 공급하는 실행은 `ASPNETCORE_ENVIRONMENT=Development` 설정도 필요하다. 메인 홈과 공통 카드, 접기, 확인 버튼, 화면 폭, 로그아웃의 렌더링을 검토하는 전용 환경이다.

가상 계정·업무를 사용한다. 생활 역할도 제품의 기존 생활 지도 대신 공통 화면으로 검토한다. 실제 서버·DB·배차·기사 GPS·입금·휴대폰 설치 증거가 아니며 제품 서버로 데이터를 전송하지 않는다. 제품 업무 연결은 역할 adapter/API 시험과 통합 WASM 실행을 별도로 확인한다.

Google 지도는 제품 웹의 지도 host와 두 JavaScript 모듈을 링크해 재사용한다. `Development` 환경의 `GoogleMaps:BrowserApiKey` 설정이 있으면 실제 Google 타일과 예시 픽업·전달 핀을 표시하며, 없으면 기존 목록 안내를 제공한다. 환경 변수 `GoogleMaps__BrowserApiKey` 등 프로세스 설정을 사용하고 실제 키를 소스·로그·캡처에 저장하지 않는다. 키 공급 응답은 루프백과 고정 Origin에만 제공하며 `no-store`다. 업무 정보는 예시이고 실제 GPS·도로 경로가 아니다. Google 지도 로딩을 위해 Google에 요청하며 제품 서버의 배차·입금에는 연결하지 않는다. Android용 키는 별도로 설정한다.

기사 예시: `/workspace/food-driver?scenario=offer`, `pickup`, `delivery`. 실제 타일 준비·주황 픽업/파랑 전달 핀·지도 이동/확대와 모바일 카드를 확인할 수 있다. Google 로고·저작권 표시는 유지한다.

r16의 `/workspace/orderer`, `restaurant`, `operator`, `shipper`, `cargo-driver`, `warehouse`는 실제 제품 adapter를 예시 DTO API에 연결한다. 기본 `scenario=progress`, 공통 `waiting/complete/nomap`을 제공한다. 추가 예시는 주문자 `receipt/expired`, 음식점 `recooking`, 운영자 `map-failure/sample-map`, 화주 `expired`, 화물 기사 `expired/next`, 창고 `next`다. 일부 전문 입력 화면과 창고의 다른 공정을 모두 구현한 예시 호스트는 아니다. 조회·창고 진입 확인만 예시로 응답하고 실제 업무 변경·증빙 업로드는 실행하지 않는다. 위치 기록 시각은 최초 예시 값이며 재조회 때 새 측정으로 갱신하지 않는다.

[카드 검토표](../../docs/ProjectOverview/page-docs/role-state-card-review-r16.md)와 [이번 검증·캡처](../../docs/Changes/2026-10-05-role-map-reliability-r16.md)를 따른다. r16 원시 검증은 `artifacts/local/role-map-reliability-r16/`에 보관한다. 모든 상태의 카드 디자인을 최종 승인한 결과는 아니다.

r18은 음식 기사도 실제 제품 adapter에 연결한다. `food-driver`의 `recooking/ready-without-permission/valid-ready`, 음식점의 `pickupdone/recooking`, 주문자의 `awaiting-driver`, 화물의 `held/reviewed`, 창고의 `inspection/outbound-wait/outbound-ready`로 단계 안내를 비교한다. 창고 검수·출고 검토·운송 초안 route도 실제 공유 화면을 사용한다. 역할 카드와 입력 화면은 같은 예시 세션을 읽고, 업무 저장·외부 업로드는 차단한다. 이번 실행은 지도 키를 공급하지 않아 지도 설정 안내/목록 대체 상태에서 카드를 검토했으며 실제 Google 타일·GPS 증거로 확대하지 않는다. [r18 변경·화면](../../docs/Changes/2026-10-05-state-card-polish-r18.md)을 따른다.

화면에는 미리보기 표시가 있고, 예시 자료는 제품 초기값으로 등록하지 않는다. 원시 검증은 `artifacts/local/role-map-workspace-r11/`에 보관한다.

완료 확인 r12의 `/action-preview/after-read-failure`와 `/action-preview/stale-read`는 실제 공용 행동 폼에 예시 API를 연결한다. 명령 응답 뒤 읽기 실패/이전 상태와 조회만 재시도하는 화면을 검토하며 숨겨진 읽기/쓰기 횟수를 확인한다. 외부 HTTP와 실제 DB에는 연결하지 않는다. r12 원시 화면·검증은 `artifacts/local/role-workspace-completion-card-r12/`에 보관한다.
# 플랫폼 자동 배송 r25 화면 검증

`/auto-fulfillment-preview/{scenario}`는 실제 공통 생활 배송 작성·상세, 음식 주문 작성·상세, 식당 지원 설정 Razor와 VM을 사용합니다. `AutoFulfillmentPreviewFixture`는 합성 계정·메뉴·지원 설정과 메모리 내 접수/취소 응답만 제공합니다. 업무 HTTP, DB, 실제 업체, 청구·결제·입금·기사 지급은 실행하지 않습니다. 음식점 조리 시작의 서버 권위 검증과 실제 HTTP 시험은 별도입니다.

시나리오는 `living-create`, `living-lost`, `living-feature-off`, `living-accepted`, `living-unknown`, `living-review`, `living-paused`, `food-create`, `food-feature-off`, `food-unknown`, `food-review`, `food-paused`, `merchant-support`입니다. 부모 검증 실행자가 순차 빌드 후 `--LifeFacilitationPreviewPort 5575`로 호스트를 실행합니다. `verify-auto-fulfillment.mjs`는 320/390px의 26개 공통 화면에서 조건/동의·원 요청 복구·취소 확인·지원 설정 저장·기능 OFF 기존 자동 요청의 조회 전용 상태와 가로 넘침을 검증하며 외부 및 `/api/` 요청을 차단합니다. 합성 동의 API도 수행 판본과 분리된 동의 판본을 검사합니다.

`NODE_PATH`에 Playwright가 설치된 `node_modules` 경로를 설정하고 `node eng/RoleWorkspacePreview/verify-auto-fulfillment.mjs`를 실행합니다. 기본 루프백 주소는 `http://127.0.0.1:5575`, 결과는 `artifacts/local/fulfillment-routing-r25/ui`이며 `SSALDDEL_AUTO_FULFILLMENT_PREVIEW_URL`, `SSALDDEL_AUTO_FULFILLMENT_PREVIEW_OUTPUT`, 필요시 `SSALDDEL_PLAYWRIGHT_CHROMIUM`으로 재정의합니다. source/binary SHA-256과 drift는 `verification.json`에 기록합니다. 이 결과를 운영 앱·실제 휴대폰·실운행·결제 완료로 보고하지 않습니다.

# 음식 배송비 분담 정책 r26 화면 검증

`/delivery-cost-sharing-preview/{scenario}`는 실제 공통 음식점 정책 설정·주문 작성·자동 배송 상태 Razor와 VM을 사용합니다. `role-*` 장면은 실제 `RestaurantRoleWorkspaceAdapter`의 조회 결과를 공통 `RoleMapWorkspace`로 표시합니다. `DeliveryCostSharingPreviewFixture`는 메모리 안의 합성 계정·메뉴·정책·명령만 제공하며 업무 HTTP, DB, 실제 업체 접수·결제·지급은 실행하지 않습니다. 금액 예시는 공통 계산기의 배송비 6,000원 가정을 명시하고 실제 견적·분담 결과와 구별합니다. 서버 영속성·권한·조리 시작과 실제 HTTP 검증은 별도 시험이 소유합니다.

설정 장면은 `merchant-new/fixed/percentage/restaurant/lost/legacy`, 주문 작성은 `food-customer/fixed/percentage/restaurant/policy-change`, 상태는 `detail-percentage/restaurant-held/waiting/unknown/lost`, 음식점 업무 카드는 `role-percentage/held/waiting/paused`입니다. 모두 접두사를 포함한 이름을 사용합니다(예: `merchant-fixed`). 설치 Chromium으로 `verify-delivery-cost-sharing.mjs`가 320/390px의 40장면을 검사합니다. 네 정책의 필요한 입력·명시적 한도 선택·자동 사용 분리, 변경 후 동의 무효화, 원 정책/동의 JSON과 요청 ID 복구, 미확정·보류·중지 금액의 0원 오표시 방지, 구형 정액 설정과 모바일 넘침을 확인합니다. r25의 별도 26장면 검증도 회귀 확인에 유지합니다.

부모 실행자가 순차 빌드 후 `dotnet run --project eng/RoleWorkspacePreview --no-build -- --LifeFacilitationPreviewPort=5576`로 호스트를 시작합니다. 현재 PC는 `NODE_PATH=C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules`, `SSALDDEL_PLAYWRIGHT_CHROMIUM=C:/Program Files/Google/Chrome/Application/chrome.exe`를 설정한 뒤 `node eng/RoleWorkspacePreview/verify-delivery-cost-sharing.mjs`로 검사합니다. Playwright 번들 Chromium 설치를 가정하지 않습니다. 기본 주소는 `http://127.0.0.1:5576`, 원시는 `artifacts/local/delivery-cost-sharing-r26/ui`이며 `SSALDDEL_COST_SHARING_PREVIEW_URL`·`SSALDDEL_COST_SHARING_PREVIEW_OUTPUT`으로 재정의합니다. 루프백·저장소의 `artifacts/local` 경계, 업무 API·외부 HTTP 차단, 실제 Razor/VM/adapter 소스 및 빌드 DLL SHA-256과 drift를 기록합니다. 스크립트 준비만으로 렌더 검증이 통과했다고 표현하지 않습니다. [페이지 책임 카드](../../docs/ProjectOverview/page-docs/delivery-cost-sharing-r26.md)를 따릅니다.

# 기존 배달 기록 로컬 대조 r54

`/historical-delivery-preview`는 확인된 과거 배달 기록을 읽기 전용으로 대조합니다. 기사 날짜 목록은 실제 `FDriverCompletedDeliveryListPageModel`의 조회·20건 페이지·상세 선택을 재사용하고, 음식점 관점은 같은 `recordId`를 지점별로 조회합니다. 진행 중 주문 수신함과 합치지 않습니다. 실제 배달 날짜·배달료·앱 표시 거리와 확인된 메뉴/주문금액을 보존하며, 고객 정보·플랫폼 주문번호·전달 주소·원문 전사는 포함하지 않습니다. 공제·수령액·실입금은 미확인입니다.

개인 자료는 바이너리에 넣거나 기본 자동 탐색하지 않습니다. 실행자가 `SSALDDEL_HISTORICAL_DELIVERY_DATASET`에 저장소 `artifacts/local` 아래의 검토한 JSON 절대 경로를 지정해야 합니다. 지정이 없거나 읽기/검증에 실패하면 안내만 표시하고 예시 자료로 대체하지 않습니다. [r54 계획·페이지 책임](../../docs/AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/delivery-history-local-replay.r54.md)의 분리 기준을 따릅니다.

Native 목록의 날짜 구간 계약을 위한 내부 시각은 원본 날짜의 한국 시간 12시 + 기록 순번 초로 정한 명시적 Simulation 값입니다. 화면에는 이 시각을 실제 완료 시각으로 표시하지 않고 원본 `actualCompletedAtUtc`만 표시합니다. 값이 없으면 `미확인`입니다. 표시 항목금액을 수량으로 나눠 단가로 추정하지 않습니다. 기기 저장소·GPS·외부 HTTP·업무 요청·실제 DB·정산/지급을 호출하지 않습니다.

순차 빌드 후 위 환경변수를 지정한 프로세스에서 `dotnet run --project eng/RoleWorkspacePreview --no-build -- --LifeFacilitationPreviewPort=5581`로 루프백 호스트를 시작합니다. 설치된 Playwright의 `NODE_PATH`와 Chrome 경로 `SSALDDEL_PLAYWRIGHT_CHROMIUM`을 지정한 뒤 `node eng/RoleWorkspacePreview/verify-historical-delivery.mjs`로 확인합니다. 기본 주소는 `http://127.0.0.1:5581`, 결과는 `artifacts/local/delivery-history-replay-r54/ui`이며 `SSALDDEL_HISTORY_PREVIEW_URL`·`SSALDDEL_HISTORY_PREVIEW_OUTPUT`으로 변경할 수 있습니다. 검사는 320/390px에서 모든 날짜/기사 목록 페이지의 건수·배달료·거리와 동일 기록의 음식점 연결·되돌아오기·미확인값·가로 넘침을 확인하고 외부 및 `/api/` 요청을 차단합니다. `SSALDDEL_HISTORY_EXPECT=missing` 또는 `invalid`는 별도로 해당 입력 상태의 호스트를 실행한 뒤 안내/빈 결과를 확인할 때만 사용합니다. 소스·실행 DLL·개인 JSON 지문을 기록하며 실행하지 않은 검증을 완료로 표현하지 않습니다.

# 수집 메뉴·사진 로컬 대조 r55

`/recorded-menu-catalog-preview`는 제품 공통 `OrdererRecordedMenuCatalog`로 기존 배달의 메뉴·수량·옵션·관측 금액과 공식 메뉴 참고 사진을 보여 줍니다. 현재 판매가가 없는 메뉴의 단가는 `미확인`으로 유지하고, 항목 표시 금액과 주문 전체 합계는 다른 라벨로 표시합니다. 메뉴·지점·옵션 검색과 지점 선택만 제공하며 실제 주문·결제·운영 판매 메뉴 등록은 연결하지 않습니다. 사진이 없거나 읽지 못하면 다른 사진으로 대체하지 않습니다. 이름 일치 후보 사진은 당시 제품·지점·옵션 또는 공개 이용 허락이 확정된 자료가 아닙니다.

실행자가 `SSALDDEL_ORDERER_MENU_CATALOG`에 `artifacts/local` 아래 가져온 `catalog.json`의 절대 경로를 지정해야 합니다. 가져오기는 `import-menu-catalog.py --help`를 참조합니다. 자료를 지정하지 않거나 검증에 실패하면 안내만 보입니다. `SsalddelExecution:Mode`가 명시된 경우 `Simulation`만 허용합니다. 사진은 `/_local/orderer-menu-images/{assetId}`에서 등록된 SHA-256·파일 크기·MIME/시그니처를 요청마다 대조하고, `artifacts/local` 내부 경로·junction/symlink·루프백/요청 출처를 검사합니다. 개인 디렉터리 전체를 정적 파일로 공개하거나 외부 사진 URL을 대리 호출하지 않습니다.

다른 실행 중 호스트를 종료하지 말고 순차 빌드 후 빈 포트를 선택합니다. 저장소 루트에서 실행하는 기본 예시는 `dotnet artifacts/local/orderer-menu-catalog-r55/host/RoleWorkspacePreview.dll --contentRoot eng/RoleWorkspacePreview --LifeFacilitationPreviewPort=5584`입니다. `NODE_PATH`와 `SSALDDEL_PLAYWRIGHT_CHROMIUM`을 지정하고 `node eng/RoleWorkspacePreview/verify-recorded-menu-catalog.mjs`를 실행합니다. 주소·출력은 `SSALDDEL_MENU_PREVIEW_URL`·`SSALDDEL_MENU_PREVIEW_OUTPUT`으로 변경하며 출력은 `artifacts/local` 안에만 저장합니다. 검사는 320/390/1280px의 실제 공통 화면에서 지점·메뉴·옵션 검색, 미확인 단가, 관측 메뉴 금액·주문 합계 분리, 사진 정상/없음/실패, 해시·등록되지 않은 경로·외부 출처 차단과 업무 API/외부 요청 0건을 확인합니다. `SSALDDEL_MENU_EXPECT=missing/invalid/operational`은 해당 설정으로 별도 실행한 호스트의 안내·사진 차단 검증입니다.

다른 실행의 DLL을 검증 근거로 재사용하지 않도록 새 호스트는 `artifacts/local/orderer-menu-catalog-r55/host`처럼 별도 출력에 빌드·실행합니다. 실제 실행한 DLL 디렉터리를 `SSALDDEL_MENU_PREVIEW_BINARY_DIR`에 지정하며, 검증기의 기본값은 위 `host` 경로입니다. 기존 `bin/Debug` DLL이 새 호스트에서 실행됐다고 가정하지 않습니다.

`/recorded-menu-selling-preview`는 실제 `OrdererRestaurantDetailPanel`에 별도 합성 4,000원 메뉴를 넣어 대표 이미지와 수량 증가·감소, 합계 계산만 확인합니다. 공식 사진은 이미지 자리의 표시 검토에만 쓰며 그 사진의 실제 판매가를 뜻하지 않습니다. 익명 세션을 사용하고 주문 등록 포트는 차단합니다. 소스·DLL·카탈로그 해시와 화면 증거는 `artifacts/local/orderer-menu-catalog-r55/ui`에 기록합니다. 이 로컬 검증은 운영 서버·모바일 APK 설치·기기·Azure·이미지 공개 이용 승인 증거가 아닙니다.
