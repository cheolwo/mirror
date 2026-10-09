# 외부 배송 가정 연결 작업지시서 r28

- 작업 `WORK-LIFE-ASSUMED-PROVIDERS-R28`, 책임 `operations`, 수용 `ReadyToDispatch / AcceptedLocal`, 반환 `Implemented / LocalSimulationValidated / ReturnedForReview`.
- 승인: [r28](assumed-providers.implementation.r28.md)에 대한 현재 사용자의 명시 구현 요청. 같은 현 작업트리에서만 수용하며 원격 교대·배포 승인은 아니다.
- 기준 branch `dev/mirror-integration`, HEAD `9336637bde31ec97b5740e2a82a802dd490127ca`. 기존 r24/r25/r26/r27와 표시 r3의 dirty 변경을 보존한다. 승인 r28 SHA256 `90AF9E79DD06F085785207838094BF511552F7643D89E9E3F81CFC08E8D629B8`을 대조했다.

## 정확 쓰기 경로와 담당

서버 수행처 담당:

- `Ssalddel.Contracts/Common/Fulfillment/AssumedFulfillmentProviderCatalog.cs`
- `Ssalddel/Services/Fulfillment/AutoFulfillmentProviders.cs`
- `Ssalddel/Services/Fulfillment/AutoFulfillmentCoordinator.cs`
- `Ssalddel/Services/Fulfillment/FulfillmentPorts.cs`
- `Ssalddel/Extensions/ServiceCollectionExtensions.FulfillmentRouting.cs`
- `Ssalddel.Tests/Services/Community/AssumedFulfillmentProviderTests.cs`

입력 담당:

- `Ssalddel.Contracts/Common/Community/NeighborhoodDeliveryDtos.cs`
- `Ssalddel.Domain/화주/화주운송의뢰.cs`
- `Ssalddel.Infrastructure/Persistence/Configurations/Shipper/화주운송의뢰Configuration.cs`
- `Ssalddel/Services/Community/NeighborhoodDeliveryUseCase.cs`
- `Ssalddel/Services/Community/운송원장Mongo동기화Service.cs`
- `Ssalddel/Services/Community/TransportLedgerBusinessProjectionHandler.cs`
- `Ssalddel/Migrations/20261007180000_AddNeighborhoodPackageSize.cs`
- `Ssalddel.Tests/Services/Community/NeighborhoodPackageSizeTests.cs`

UI 담당:

- `Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryAuthoringViewModel.cs`
- `Ssalddel.Ui.Common/Areas/App/ViewModels/AutoFulfillmentConditionsViewModel.cs`
- `Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryCreate.razor`
- `Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentConditions.razor`
- `Ssalddel.Ui.Common/Areas/App/Components/Fulfillment/AutoFulfillmentStatus.razor`
- `Ssalddel.Tests/Ui/Common/AssumedFulfillmentUiTests.cs`
- `Ssalddel.Tests/Ui/Common/AutoFulfillmentViewModelTests.cs` (구형 테스트 대역에 서버 후보 목록을 명시하는 최소 보완만)

root 통합 담당:

- `Ssalddel/Services/Fulfillment/NeighborhoodFulfillmentSourceAdapter.cs`
- `Ssalddel/Services/Community/생활협업배송Source.cs`
- `Ssalddel/Services/Community/생활협업Ports.cs`
- `Ssalddel/Services/Community/생활협업UseCase.cs`
- `Ssalddel/Migrations/SsalddelContextModelSnapshot.cs`
- `Ssalddel.Tests/Services/Community/AssumedNeighborhoodCompletionTests.cs`
- `docs/AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/README.md`
- `docs/AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/assumed-providers.implementation.r28.md`
- `docs/AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/development-work-order.r28.md`
- `docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`, `docs/ProjectOverview/GptProjectContext.md`
- `docs/ProjectOverview/page-docs/neighborhood-fulfillment-r28.md`
- `docs/Changes/2026-10-07-assumed-fulfillment-r28.md`
- `eng/AssumedFulfillmentPreview/AssumedFulfillmentPreview.csproj`, `eng/AssumedFulfillmentPreview/Program.cs`, `eng/AssumedFulfillmentPreview/App.razor`
- `eng/AssumedFulfillmentPreview/verify-ui.cjs` (실제 렌더·동작·외부 요청 차단 검증)
- `docs/assets/changes/2026-10-07-assumed-fulfillment-r28/` (합성 자료의 샘플 화면 PNG만)

## 공유 계약과 검증

기존 route와 DTO의 하위 호환을 유지한다. `NeighborhoodDeliveryRequest.PackageSizeCode`는 nullable이며 `NeighborhoodDeliveryQuoteResponse.AutomaticProviders`는 서버 후보 목록이다. 기존 `AutoFulfillmentProviderDescriptor`를 재사용한다. 포트의 후보 조회와 선택적 크기 snapshot은 서버 담당이 소유하고 root가 원 업무에 결속한다.

구현 시작 전에 hash와 담당 경로를 다시 대조한다. 담당 밖 파일은 변경하지 않는다. root는 통합·집중/Fast/Task·실제 렌더를 담당한다. 실행 산출물은 `artifacts/local/assumed-fulfillment-r28/`에 둔다. 결과 불명확에서는 같은 시도만 확인하며 다른 업체에 중복 접수하지 않는다. 실제 사업자·지급·공개 운영의 증거로 승격하지 않는다.

입력 저장 경계 점검에서 선택 크기의 Mongo 원장 재구성 결손을 발견했다. 위 두 기존 동기화 파일을 입력 담당에 추가 배정한다. 승인 범위 안의 동일 입력 보존이며 신규 업무·외부 효과를 추가하지 않는다. null 구형 자료는 기존 값을 지우지 않고 허용 크기만 복원한다. 원천 저장·동기화는 최초 수행 동의 전에 닫는다.

## 통합 반환

[실제 결과](../../../../Changes/2026-10-07-assumed-fulfillment-r28.md)에 신규 73·Fast 701·Task 8,470 통과와 환경 조건 Mongo 2건 건너뜀, 실제 공통 UI 22장면·4흐름을 결속한다. 최종 제품 소스는 Task 검증 뒤 변경하지 않았다. 승인 본문/hash는 유지했다. 공용 DB 적용·현재 인증 HTTP/실DB 종단·실제 업체·APK·실지급·Unity·commit/push/배포는 수행하지 않았다.
