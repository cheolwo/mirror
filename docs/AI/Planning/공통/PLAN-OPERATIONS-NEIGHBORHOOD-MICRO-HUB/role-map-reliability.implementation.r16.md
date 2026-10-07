# 지도 위치·갱신과 역할별 기본 카드 구현

2026-10-05. 상태 `Implemented / ValidatedLocal / CardReviewPending`. 사용자가 [역할 지도 조사](../../../../ProjectOverview/role-map-coverage-audit-r15.md)의 제안 1·2·3 진행을 승인했다. 사용자와 다시 검토할 역할별·업무 상태별 최종 카드 구성은 미정이며 이번 구현은 기본안이다. 기존 입력·증빙·정산·전용 앱·권한·음식/화물 구분을 유지한다.

## 구현 단위

1. 전용 화물 지도: 카메라 중심과 관측 위치 분리, 위치 근거 없는 GPS 표시 제거, 복귀지 구분, 최근 수신 시각과 기존 화물 10분 기준, 위치 만료·이탈 정리. 현재 운송 좌표는 기존 소유 운송 상세 API에 연결한다.
2. 공통 역할 화면: 표시 중인 역할의 제한적 재조회, 시간 경과에 따른 위치·연결 경로 제거, 숨김·계정/역할 변경·종료 정리, 자동 조회 실패의 마지막 확인 안내와 명령 차단. 수행 역할 완료 후 다음 현재 업무와 경로를 함께 선택한다. 지도 실패에는 목록으로 볼 수 있다.
3. 기본 카드: 기존 `Summary`를 사용해 주문자·음식점·화주·화물 기사·창고·운영자의 현재 상태 핵심을 표시한다. 선택 화물 운송의 좌표는 기사 소유 확인 후 서버에서 원천 의뢰와 연결한다. 연락처·긴 이력·증빙은 기존 상세와 독립 페이지로 유지한다.

## 책임과 쓰기 범위

기준선은 `dev/mirror-integration`, HEAD `b1b7cfc39b5183ed56246080c141d63965bf2704`와 당시 로컬 변경이다. 조사 전 해시 목록은 `artifacts/local/role-map-reliability-r16/baseline.json`에 있다. 명시 승인한 기존 소스의 최소 변경만 수행하고 unrelated dirty 파일은 보존한다.

| 단위 | 허용 쓰기 범위 |
| --- | --- |
| 공통 표시·수명 | `Ssalddel.Ui.Common/Areas/App/Models/NeighborhoodMapRenderState.cs`, `RoleWorkspace/Core/RoleWorkspaceViewModel.cs`, `RoleWorkspaceState.cs`, `RoleMapWorkspace.razor`와 CSS, `wwwroot/Areas/App/js/role-workspace-visibility.js` |
| 음식 역할 | `RoleWorkspace/Food/FoodDriverRoleWorkspaceAdapter.cs`, `OrdererRoleWorkspaceAdapter.cs`, `RestaurantRoleWorkspaceAdapter.cs`, `OperatorRoleWorkspaceAdapter.cs` |
| 화물·창고 | `RoleWorkspace/Cargo/CargoWorkspaceAdapters.cs`, `WarehouseWorkspaceAdapter.cs`, `Ssalddel.Contracts/Driver/Transport/기사운송Dtos.cs`, `Ssalddel/Application/Driver/Transport/Handlers/운송상세조회QueryHandler.cs` |
| 네이티브 위치 | `DriverApp/Controls/DriverNativeMapView.cs`, 지도 handlers, `NativeDriverHomePage.xaml.cs`, `Services/Samples/ServerBackedDriverSampleDataService.cs`, `Services/기사운송표시Mapper.cs`, 위치 샘플 모델과 `DriverNativeLocationPolicy.cs`, `Components/Pages/Driver/02_Recommendation/추천목록Page.razor`의 위치·거리 미확인 표시 |
| 검증·문서 | 위 변경의 전용 r16 시험과 `Ssalddel.Tests.csproj` 순수 정책 연결, `eng/RoleWorkspacePreview` 예시 자료·호스트, 기존 책임 카드, 변경 기록·목차·현재 작업 요약 |

`RoleWorkspace/` 상대 경로는 `Ssalddel.Ui.Common/Areas/App/` 아래다. 지도 키·서버 비밀·출시 정책·실원장 쓰기·전용 앱 삭제·commit/push는 이번 범위가 아니다. UI 검토 자료는 실제 공용 카드·변환기를 예시 DTO로 렌더하며 실제 서버 업무를 완료했다고 보고하지 않는다.

## 검증과 후속 검토

- 상태·권한·위치 만료·늦은 응답·중단/복귀·명령 중복·완료 후 선택과 첫 경로 연결의 집중 시험을 수행한다.
- 직접 영향 Web·공통 UI·서버와 소비 앱을 빌드하고 scoped Fast·Task를 실행한다. 실제 SDK 코드의 Android 빌드와 순수 위치 정책 시험은 물리 기기에서 GPS를 확인한 결과와 구별한다.
- 좁은 화면에서 역할별 기본 카드·상세·목록·오류와 실제 지도 모듈을 확인하고 캡처를 남긴다. 예시의 위치·이름만 사용한다.
- 사용자가 상태별 카드를 검토할 수 있도록 현재 기본 정보와 다음 행동을 표로 남긴다. 모든 상태·페이지 디자인을 최종 승인하거나 실업무·Azure·휴대폰 설치 검증으로 승격하지 않는다.

Fast `20261005-203528` 243/243와 Task `20261005-203725` 전수 7,561/7,561 통과. 실제 예시 웹 UI·위치 만료·목록 전환과 320/390px 기본 카드를 확인했다. [변경 기록](../../../../Changes/2026-10-05-role-map-reliability-r16.md)과 [역할·상태별 검토표](../../../../ProjectOverview/page-docs/role-state-card-review-r16.md)에 캡처와 실행 한계를 남긴다. 실제 휴대폰·GPS·실원장 업무·Azure와 최종 카드 정책은 별도다.
