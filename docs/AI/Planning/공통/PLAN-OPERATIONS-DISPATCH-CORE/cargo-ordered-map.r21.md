# 약속을 지키는 다중 정차의 지도 안내 r21

상태: Approved / ReadyToDispatch. 2026-10-27 사용자가 r20 보완 후 B 우선 수행을 지도에서도 강조하고 A 등 뒤 순서는 회색으로 구분하며 2·3·4건의 여러 상하차 조합을 안내하도록 직접 요청했다. 같은 로컬 checkout의 구현 수용이며 다른 작업실의 Git 인계가 아니다.

## 기준과 범위

- 기준 branch `dev/mirror-integration`, HEAD `8a97384604e6c68a1b00fc71a612d699a3651825`. 기존 미커밋 r20 소스·시험·문서와 비공개 다운로드를 보존하고 수정 전 파일/SHA를 `artifacts/local/cargo-ordered-map-r21/`에 남긴다.
- [r20](cargo-feasible-stop-order.r20.md)의 시간창·작업시간·적재·계약 검사와 저장된 typed 정차 순서를 재사용한다. UI는 수락 순서·표시용 번호·간판/주소 문자열로 운행 순서를 재계산하지 않는다.
- 현재 조회에서 검증된 첫 미완료 정차만 상차 주황/하차 파랑으로 강조하고 나머지 정차는 회색과 순번으로 구분한다. 완료 후 기존 정본 재조회로 다음 정차를 갱신한다. 지도 핀 선택은 운송 상태나 수락 순서를 변경하지 않는다.
- 2·3·4건의 교차 정차와 같은 지점의 여러 순서를 지원한다. 현 계산은 기존 정차의 상대순서를 유지한 삽입 후보 중 시간·거리 기준 선택이며 모든 업무의 전역 최단거리 또는 실시간 재탐색을 보장하지 않는다.
- 현재 overlay는 지점 연결선이며 도로 geometry가 아니다. 화면에서 `수락 시 확인한 방문 순서`와 `지점 연결선 · 실제 도로 경로 아님`을 구분한다. 정확한 도로 안내는 기존 길찾기/현재 운송 진입을 유지하고 새 Directions 호출·키·SDK·서버 경로 최적화 엔진을 추가하지 않는다.
- 기존 workspace DTO에 nullable typed 운송Id·허용된 좌표·시간창 시작과 저장 순서 검증 상태만 추가한다. 기존 필드·route·RuleRevision·저장 계약은 보존한다. 공개 보류/무ID/중복ID/목록 밖/잘못된 좌표에는 좌표를 내보내지 않는다.
- 클라이언트는 같은 현재 조회·계정·운송과 결속된 정차만 표시한다. 보류·조회 실패·구판/잘못된 순서는 강조된 전체 경로로 승격하지 않는다. 좌표 누락 사이를 건너뛰어 연결하지 않는다. 현 위치 지연·권한 철회·계정 변경과 보호 상태를 보존한다.
- 새 DB·원장·색상 저장·자동 수락·운영 플래그·FCM·실제 GPS·배포·commit/push는 포함하지 않는다. 현재 서버·기기·앱을 강제로 재시작하지 않는다.

## 소유권과 writePaths

- 서버 담당: `Ssalddel.Contracts/Driver/Transport/기사운송Dtos.cs`, `Ssalddel/Application/Driver/Transport/Handlers/화물운송작업공간조회QueryHandler.cs`, 새 `Ssalddel.Tests/Application/Driver/Transport/CargoOrderedMapProjectionR21Tests.cs`.
- 지도 표현 담당: `Ssalddel.Contracts/Common/Drivers/DriverMapDtos.cs`, `DriverApp/Models/Driver/Samples/DriverRecommendationMarkerPresentationPolicy.cs`, `DriverApp/Services/DriverHomeRoutePlanningService.cs`, `DriverApp/Services/IDriverSampleDataService.cs`, `DriverApp/Services/Samples/ServerBackedDriverSampleDataService.cs`, 새 `Ssalddel.Tests/Clients/CargoOrderedMapPresentationR21Tests.cs` 및 필요한 `CargoWorkspaceRecoveryR75Tests.cs`의 직접 회귀. 기존 파일에 표현 모델을 두어 불필요한 csproj 변경을 피한다.
- 루트: `DriverApp/NativeDriverHomePage.xaml`, `DriverApp/NativeDriverHomePage.xaml.cs`, 필요한 기존 renderer 배선의 좁은 보완과 `Ssalddel.Tests/Clients/CargoOrderedMapNativeWiringR21Tests.cs`, 이 PLAN, `docs/ProjectOverview/page-docs/cargo-driver-native-continuity-r64.md`, `docs/AI/{CURRENT_WORK,PLANNING,DECISIONS}.md`, `docs/Changes/README.md`, `docs/Changes/2026-10-27-cargo-ordered-map-r21.md`.
- 공유 파일은 위 소유자만 수정하고 추가 경로가 필요하면 먼저 조율한다. 이전 r20 검증을 r21 변경의 결과로 승계하지 않는다.

## 검증과 마감

1. 서버의 typed ID/좌표·공개 보류/중복/좌표 유효성·저장 순서 유효/보류/구판 투영을 검사한다.
2. B 상차→B 하차→A 하차 및 3·4건의 교차 순서, 상차 후 전환·완료 제거, 첫 정차만 강조·뒤 순서 회색, 좌표 공백·중복 순번/정차·조회/계정 변경을 확인한다.
3. 기존 추천 강조·네이버/Google 공통 renderer·현재 운송 복귀를 회귀하고 실제 영향 Contracts/DriverApp/server 빌드와 `validate-changes.ps1 -Paths` Fast/Task를 수행한다.
4. 화면 정책/배선·합성 자료와 실제 지도 SDK·휴대폰·GPS/내비·실운행은 구분한다. 실행하지 못한 화면은 미검증으로 보고하며 실제 최단거리·운영 완료를 주장하지 않는다.
