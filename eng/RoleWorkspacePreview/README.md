# 역할 지도 공통 화면 배치 미리보기

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
