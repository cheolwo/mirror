# 생활 배송 간편 입력 작업지시서 r29

2026-10-07. 작업 `WORK-LIFE-SIMPLE-INPUT-R29`, 책임 `operations`, 최초 `ReadyToDispatch / AcceptedLocal`, 현재 `Returned / LocalInputValidated / OperationalE2ePending / VoiceDeferred`. [승인 r29](simple-input.implementation.r29.md) SHA256 `1D6D6CA37CDBEC26B5D5050B968C66F461E1F45AE82D7A2BACD5B0FAB0434BDE`를 구현 직전과 반환 시 대조했다. 기준 branch `dev/mirror-integration`, HEAD `9336637bde31ec97b5740e2a82a802dd490127ca`. 기존 r24~r28·역할 옵션·음식 모바일과 다른 dirty 변경을 보존한다. 기존 r28 승인 hash는 변경하지 않는다.

## 소유 경로

서버 담당:

- `Ssalddel.Contracts/Common/Community/NeighborhoodDeliveryInputDtos.cs`
- `Ssalddel/Services/Community/생활배송엑셀초안Service.cs`
- `Ssalddel/Controllers/Common/NeighborhoodDeliveryController.cs`
- `Ssalddel/Extensions/ServiceCollectionExtensions.DomainServices.Operations.cs`의 최소 등록
- `Ssalddel.Tests/Services/Community/NeighborhoodDeliveryExcelInputTests.cs`

입력 UI 담당:

- `Ssalddel.Ui.Common/Areas/App/ViewModels/NeighborhoodDeliveryAuthoringViewModel.cs`
- `Ssalddel.Ui.Common/Areas/App/ViewModels/생활배송간편입력ViewModel.cs`
- `Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/NeighborhoodDeliveryCreate.razor`
- `Ssalddel.Ui.Common/Areas/App/Components/Community/Exchange/생활배송간편입력.razor`
- `Ssalddel.Ui.Common/wwwroot/Areas/App/css/neighborhood-delivery.css`
- `Ssalddel.Tests/Ui/Common/NeighborhoodDeliverySimpleInputTests.cs`

통합 담당 root:

- `Ssalddel.Ui.Common/Areas/App/Services/NeighborhoodDeliveryClient.cs`
- `Ssalddel/Resources/NeighborhoodDelivery/생활배송입력.r29.xlsx`
- `Ssalddel/Ssalddel.csproj`의 위 template EmbeddedResource만
- `Ssalddel/Middleware/IsmsPEncryptedTransportMiddleware.cs`의 신규 입력 route 전용 크기·메모리 버퍼 경계와 `Ssalddel.Tests/Services/Community/NeighborhoodDeliveryInputMemoryTransportTests.cs`
- `eng/AssumedFulfillmentPreview/App.razor`, `eng/AssumedFulfillmentPreview/Program.cs`, `eng/AssumedFulfillmentPreview/verify-ui.cjs`, `eng/AssumedFulfillmentPreview/AssumedFulfillmentPreview.csproj`
- `eng/NeighborhoodDeliveryInputTemplate/`의 양식 생성/검증 도구
- `docs/ProjectOverview/page-docs/neighborhood-simple-input-r29.md`
- `docs/Changes/2026-10-07-neighborhood-simple-input-r29.md`, `docs/assets/changes/2026-10-07-neighborhood-simple-input-r29/`
- `docs/Changes/README.md`, `docs/ProjectOverview/page-docs/README.md`의 해당 문서 색인만
- `docs/AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/README.md`, `simple-input.design.r29.md`, `simple-input.implementation.r29.md`, `development-work-order.r29.md`
- `docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`, `docs/ProjectOverview/GptProjectContext.md`

공유 계약은 서버 담당이 먼저 확정하고 root가 client, UI 담당이 초안 적용으로 소비한다. 예외·privacy·기존 동의·불명확 접수·견적 무효화를 우회하지 않는다. 임시 파일·시험·원 캡처는 `artifacts/local/neighborhood-simple-input-r29/`에 두고 대표 비식별 샘플만 문서에 보존한다.

검토 호스트의 project 참조는 서버의 순수 엑셀 파서를 재사용하기 위해서만 추가한다. 파일 선택부터 실제 파서·공통 UI까지 검증하되 검토 호스트의 합성 동의·견적·등록 대역을 인증 운영 API나 DB 종단 증거로 주장하지 않는다.

통합 점검에서 기존 전송 middleware의 기본 buffering이 큰 요청을 디스크 임시 파일로 옮기는 점을 발견했다. 승인된 메모리 전용 입력 정책을 만족시키기 위해 신규 `input-preview` 경로에만 4 MiB envelope 제한과 같은 크기의 메모리 threshold를 적용한다. 기존 route는 바꾸지 않는다.

## 통합 반환

읽기 전용 교차 점검의 형식 엄밀화 후보를 통합 담당이 좁게 반영했다. 기존 parser/시험 경로 안에서 workbook·Relationships의 표준 root/namespace와 직접 자식 시트/연결만 허용하고 별도 6개 반례 시험을 추가한다. 승인 입력 경계 안의 비표준 파일 거절이며 원장·업무·외부 효과를 확대하지 않는다. 반영 후 최종 Fast/Task·UI를 다시 실행한다.

[실제 검증 결과와 샘플](../../../../Changes/2026-10-07-neighborhood-simple-input-r29.md)에 신규 70·Fast 453·Task 8,540개 통과, 환경 조건 Mongo 2개 건너뜀, 소비 앱 포함 build 오류 0·기존 경고 55, 실제 공통 UI 36장면·6흐름을 연결했다. 빈 양식은 artifact-tool로 생성·검사·렌더했고 서버 EmbeddedResource와 출력 파일의 hash를 대조했다. 승인 본문/hash는 그대로 유지하며 최종 제품 소스는 마지막 Task 뒤 변경하지 않았다.

UI 검토는 실제 파서·공통 Razor/VM과 합성 메모리 Client로 이루어졌다. 실제 인증 HTTP/DB 종단·APK·휴대폰·음성·실제 업체·실금전·Unity·commit/push·배포 증거로 확대하지 않는다. 음성은 같은 초안의 후속 입력 방식으로 남기고 자동 실행하지 않는다.
