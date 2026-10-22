using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class InboundRequestPageViewModelTests
{
    [Fact]
    public async Task 신규신청은_저장응답Id를같은Adapter에서다시조회한다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var viewModel = new InboundRequestPageViewModel(service);
        await viewModel.LoadCreateAsync(new InboundRequestNavigationContext
        {
            WarehouseId = 5,
            SupplierName = "공동주문 공급처",
            OrderReference = "ORDER-5"
        });

        var inboundId = await viewModel.CreateInboundAsync();

        Assert.Equal(77, inboundId);
        Assert.Equal([77L], service.DetailRequests);
        Assert.Equal(77, viewModel.State.Current?.Id);
        Assert.True(viewModel.State.Created);
        Assert.Contains("같은 ID", viewModel.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 입고완료는_Command응답뒤같은Id를재조회한다()
    {
        var service = new FakeWarehouseWorkspaceService();
        service.Inbounds.Add(new 입고요청항목응답
        {
            Id = 41,
            창고Id = 5,
            공급처명 = "샘플 공급처",
            상태 = 입고상태코드.예정,
            예정상품명 = "공동구매 채소",
            예정수량 = 12
        });
        var viewModel = new InboundRequestPageViewModel(service);
        await viewModel.LoadCompletionAsync(41);
        viewModel.CompletionDraft.StorageLocation = "A-01";

        var completed = await viewModel.CompleteInboundAsync(41);

        Assert.True(completed);
        Assert.Equal([41L], service.CompletedRequests);
        Assert.Equal([41L, 41L], service.DetailRequests);
        Assert.Equal(입고상태코드.완료, viewModel.State.Current?.상태);
        Assert.Single(viewModel.State.CompletionItems);
        Assert.False(viewModel.CanComplete);
    }

    [Fact]
    public async Task 완료수량검증실패는_Command를호출하지않는다()
    {
        var service = new FakeWarehouseWorkspaceService();
        service.Inbounds.Add(new 입고요청항목응답
        {
            Id = 42,
            창고Id = 5,
            공급처명 = "샘플 공급처",
            상태 = 입고상태코드.예정
        });
        var viewModel = new InboundRequestPageViewModel(service);
        await viewModel.LoadCompletionAsync(42);
        viewModel.CompletionDraft.ItemName = "상품";
        viewModel.CompletionDraft.Quantity = 2;
        viewModel.CompletionDraft.DefectQuantity = 3;

        var completed = await viewModel.CompleteInboundAsync(42);

        Assert.False(completed);
        Assert.Empty(service.CompletedRequests);
        Assert.Equal(InboundRequestPageMessageTone.Warning, viewModel.State.MessageTone);
    }

    [Fact]
    public async Task 창고등록은_입고신청Command와분리된다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var viewModel = new InboundRequestPageViewModel(service);
        viewModel.ApplyWarehouseContext(new InboundRequestNavigationContext
        {
            WarehouseName = "생활물류센터",
            WarehouseAddress = "서울시 샘플로 1",
            ProxyType = LogisticsProxySiteTypes.UrbanLogisticsCenter
        });

        var warehouseId = await viewModel.CreateWarehouseAsync();

        Assert.Equal(21, warehouseId);
        Assert.Equal(1, service.WarehouseCreateCount);
        Assert.Equal(0, service.InboundCreateCount);
    }

    [Fact]
    public async Task 실제창고Id없는다이어그램후보는_임의의첫창고를선택하지않는다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var viewModel = new InboundRequestPageViewModel(service);

        await viewModel.LoadCreateAsync(new InboundRequestNavigationContext
        {
            Source = "diagram-warehouse-proxy",
            WarehouseName = "아직 등록되지 않은 후보"
        });

        Assert.Null(viewModel.CreateDraft.WarehouseId);
        Assert.Equal(InboundRequestPageMessageTone.Warning, viewModel.State.MessageTone);
    }

    [Theory]
    [InlineData(입고흐름유형코드.현장임시입고)]
    [InlineData(입고흐름유형코드.주문자동입고예정)]
    public async Task 전용Workflow흐름은_일반신청Command로우회하지않는다(string flowType)
    {
        var service = new FakeWarehouseWorkspaceService();
        var viewModel = new InboundRequestPageViewModel(service);
        await viewModel.LoadCreateAsync(new InboundRequestNavigationContext
        {
            WarehouseId = 5,
            SupplierName = "공급처"
        });
        viewModel.CreateDraft.FlowType = flowType;

        var inboundId = await viewModel.CreateInboundAsync();

        Assert.Null(inboundId);
        Assert.Equal(0, service.InboundCreateCount);
        Assert.Equal(InboundRequestPageMessageTone.Warning, viewModel.State.MessageTone);
    }

    [Fact]
    public async Task 등록성공뒤조회실패는_저장응답과동의를보존하고같은Id만조회한다()
    {
        var service = new FakeWarehouseWorkspaceService { DetailFailure = new HttpRequestException("private trace body") };
        var vm = await CreateReadyAsync(service);
        var consent = Guid.NewGuid();
        vm.CreateDraft.ApplicationPrivacyConsentEvidenceId = consent;
        vm.CreateDraft.Notes = "보존할 신청 초안";

        var id = await vm.CreateInboundAsync();

        Assert.Equal(77, id);
        Assert.True(vm.State.Created);
        Assert.Equal(77, vm.State.SavedInboundId);
        Assert.Equal(77, vm.State.Current?.Id);
        Assert.Equal(업무처리결과Codes.적용, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.Equal(OperatingSystemLifecycleStageIds.WarehouseInboundPlan, vm.State.Failure?.WorkflowContext?.StageId);
        Assert.DoesNotContain("private", vm.State.Message, StringComparison.Ordinal);
        Assert.Equal(consent, vm.CreateDraft.ApplicationPrivacyConsentEvidenceId);
        Assert.Equal("보존할 신청 초안", vm.CreateDraft.Notes);

        service.DetailFailure = null;
        Assert.Equal(77, await vm.CreateInboundAsync());
        Assert.Equal(1, service.InboundCreateCount);
        Assert.Equal([77L, 77L], service.DetailRequests);
        Assert.Null(vm.State.Failure);
    }

    [Fact]
    public async Task 등록응답유실은_미확정으로두고같은화면에서재제출하지않는다()
    {
        var service = new FakeWarehouseWorkspaceService { CreateFailure = new HttpRequestException("disconnected") };
        var vm = await CreateReadyAsync(service);

        Assert.Null(await vm.CreateInboundAsync());
        await vm.RefreshWarehousesAsync();
        Assert.Null(await vm.CreateInboundAsync());

        Assert.Equal(1, service.InboundCreateCount);
        Assert.Empty(service.DetailRequests);
        Assert.True(vm.State.RegistrationResultPending);
        Assert.False(vm.State.Created);
        Assert.Null(vm.State.SavedInboundId);
        Assert.Equal(업무처리결과Codes.미확정, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.Equal("공급처", vm.CreateDraft.SupplierName);
    }

    [Fact]
    public async Task 등록의명시적거절은_미적용이며수정후명시적으로다시신청할수있다()
    {
        var service = new FakeWarehouseWorkspaceService
        {
            CreateFailure = new SsalddelApiException("검증 거절", 400, "입고 신청", string.Empty, null,
                workflowContext: new(OperatingSystemIds.WarehouseCommerceFulfillment,
                    OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.미적용))
        };
        var vm = await CreateReadyAsync(service);

        Assert.Null(await vm.CreateInboundAsync());
        Assert.False(vm.State.RegistrationResultPending);
        Assert.Equal(업무처리결과Codes.미적용, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        service.CreateFailure = null;

        Assert.Equal(77, await vm.CreateInboundAsync());
        Assert.Equal(2, service.InboundCreateCount);
    }

    [Fact]
    public async Task 완료응답뒤조회실패는_재고응답과초안을보존하고완료를반복하지않는다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var vm = await CreateCompletionReadyAsync(service);
        vm.CompletionDraft.StorageLocation = "A-01";
        service.DetailFailure = new HttpRequestException("private route trace");

        Assert.True(await vm.CompleteInboundAsync(41));
        Assert.Single(vm.State.CompletionItems);
        Assert.Equal(41, vm.State.CompletionResultPendingId);
        Assert.False(vm.CanComplete);
        Assert.Equal(업무처리결과Codes.적용, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.Equal(OperatingSystemLifecycleStageIds.WarehouseReceiving, vm.State.Failure?.WorkflowContext?.StageId);
        Assert.False(await vm.CompleteInboundAsync(41));
        Assert.Single(service.CompletedRequests);
        Assert.Equal("A-01", vm.CompletionDraft.StorageLocation);

        service.DetailFailure = null;
        await vm.LoadCompletionAsync(41);
        Assert.Null(vm.State.CompletionResultPendingId);
        Assert.Null(vm.State.Failure);
        Assert.False(vm.CanComplete);
        Assert.Single(service.CompletedRequests);
    }

    [Fact]
    public async Task 완료응답유실은_원장을재조회하기전재처리하지않는다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var vm = await CreateCompletionReadyAsync(service);
        service.CompletionThrowsAfterApply = true;

        Assert.False(await vm.CompleteInboundAsync(41));
        Assert.Equal(업무처리결과Codes.미확정, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.False(vm.CanComplete);
        Assert.False(await vm.CompleteInboundAsync(41));

        await vm.LoadCompletionAsync(41);
        Assert.Equal(입고상태코드.완료, vm.State.Current?.상태);
        Assert.Null(vm.State.CompletionResultPendingId);
        Assert.Null(vm.State.Failure);
        Assert.Single(service.CompletedRequests);
    }

    [Fact]
    public async Task 완료응답뒤이전상태조회는_다시완료할수있는근거가아니다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var vm = await CreateCompletionReadyAsync(service);
        service.DetailResolver = id => new 입고요청항목응답 { Id = id, 상태 = 입고상태코드.예정 };

        Assert.True(await vm.CompleteInboundAsync(41));
        await vm.LoadCompletionAsync(41);

        Assert.False(vm.CanComplete);
        Assert.Equal(41, vm.State.CompletionResultPendingId);
        Assert.Equal(업무처리결과Codes.적용, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.False(await vm.CompleteInboundAsync(41));
        Assert.Single(service.CompletedRequests);
    }

    [Fact]
    public async Task 다른Id조회응답은_확인한저장결과를덮지않는다()
    {
        var service = new FakeWarehouseWorkspaceService
        {
            DetailResolver = _ => new 입고요청항목응답 { Id = 999 }
        };
        var vm = await CreateReadyAsync(service);

        Assert.Equal(77, await vm.CreateInboundAsync());
        Assert.Equal(77, vm.State.Current?.Id);
        Assert.Equal(업무처리결과Codes.적용, vm.State.Failure?.WorkflowContext?.OutcomeCode);
    }

    [Fact]
    public async Task 상세조회실패의맥락은_입고단계와조회결과를명시한다()
    {
        var vm = new InboundRequestPageViewModel(new FakeWarehouseWorkspaceService());
        await vm.LoadDetailAsync(999);

        var context = Assert.IsType<업무실패맥락Dto>(vm.State.Failure?.WorkflowContext);
        Assert.Equal(OperatingSystemIds.WarehouseCommerceFulfillment, context.OperatingSystemId);
        Assert.Equal(OperatingSystemLifecycleStageIds.WarehouseInboundPlan, context.StageId);
        Assert.Equal(업무처리결과Codes.조회, context.OutcomeCode);
        Assert.True(context.유효한가());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(409)]
    [InlineData(408)]
    [InlineData(425)]
    [InlineData(429)]
    public async Task 명시맥락없는충돌이나지연응답은_미적용으로추측하지않는다(int status)
    {
        var service = new FakeWarehouseWorkspaceService
        {
            CreateFailure = new SsalddelApiException("상태 확인 필요", status, "입고 신청", string.Empty, null)
        };
        var vm = await CreateReadyAsync(service);

        Assert.Null(await vm.CreateInboundAsync());
        Assert.Null(await vm.CreateInboundAsync());

        Assert.Equal(업무처리결과Codes.미확정, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.True(vm.State.HasPendingMutation);
        Assert.Equal(1, service.InboundCreateCount);
    }

    [Theory]
    [InlineData(400, 업무처리결과Codes.미확정, true)]
    [InlineData(429, 업무처리결과Codes.적용, true)]
    [InlineData(409, 업무처리결과Codes.미적용, false)]
    public async Task 유효한입고단계결과는_HTTP코드보다우선한다(int status, string outcome, bool pending)
    {
        var service = new FakeWarehouseWorkspaceService
        {
            CreateFailure = new SsalddelApiException("정형 결과", status, "입고 신청", string.Empty, null,
                workflowContext: new(OperatingSystemIds.WarehouseCommerceFulfillment,
                    OperatingSystemLifecycleStageIds.WarehouseInboundPlan, outcome))
        };
        var vm = await CreateReadyAsync(service);

        Assert.Null(await vm.CreateInboundAsync());

        Assert.Equal(outcome, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.Equal(pending, vm.State.RegistrationResultPending);
        Assert.Equal(outcome == 업무처리결과Codes.적용, vm.State.Created);
        Assert.Null(vm.State.SavedInboundId);
    }

    [Theory]
    [InlineData(OperatingSystemIds.DomesticCargoTransport, OperatingSystemLifecycleStageIds.CargoPickup)]
    [InlineData(OperatingSystemIds.WarehouseCommerceFulfillment, OperatingSystemLifecycleStageIds.WarehousePicking)]
    public async Task 다른업무나단계의적용결과는_입고신청완료로사용하지않는다(string os, string stage)
    {
        var service = new FakeWarehouseWorkspaceService
        {
            CreateFailure = new SsalddelApiException("다른 맥락", 409, "입고 신청", string.Empty, null,
                workflowContext: new(os, stage, 업무처리결과Codes.적용))
        };
        var vm = await CreateReadyAsync(service);

        Assert.Null(await vm.CreateInboundAsync());

        Assert.Equal(업무처리결과Codes.미확정, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.False(vm.State.Created);
        Assert.True(vm.State.RegistrationResultPending);
    }

    [Fact]
    public async Task 서버가명시한완료적용은_재전송대신같은원장확인을유지한다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var vm = await CreateCompletionReadyAsync(service);
        service.CompletionFailure = new SsalddelApiException("후속 처리 확인", 429, "입고 완료", string.Empty, null,
            workflowContext: new(OperatingSystemIds.WarehouseCommerceFulfillment,
                OperatingSystemLifecycleStageIds.WarehouseReceiving, 업무처리결과Codes.적용));

        Assert.False(await vm.CompleteInboundAsync(41));
        Assert.False(await vm.CompleteInboundAsync(41));

        Assert.True(vm.State.CompletionApplied);
        Assert.Equal(업무처리결과Codes.적용, vm.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.False(vm.CanComplete);
        Assert.Single(service.CompletedRequests);
    }

    [Fact]
    public async Task 상세A조회중B선택은_B를조회하고늦은A응답을적용하지않는다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var first = service.DeferDetail(41);
        var second = service.DeferDetail(42);
        using var vm = new InboundRequestPageViewModel(service);

        var a = vm.LoadDetailAsync(41);
        var b = vm.LoadDetailAsync(42);
        Assert.True(service.DetailTokens[41].IsCancellationRequested);
        Assert.True(vm.State.IsBusy);
        Assert.Null(vm.State.Current);
        second.SetResult(new 입고요청항목응답 { Id = 42, 공급처명 = "B" });
        await b;
        first.SetResult(new 입고요청항목응답 { Id = 41, 공급처명 = "A" });
        await a;

        Assert.Equal([41L, 42L], service.DetailRequests);
        Assert.Equal(42, vm.State.Current?.Id);
        Assert.False(vm.State.IsBusy);
        Assert.Null(vm.State.Failure);
    }

    [Fact]
    public async Task 상세B실패뒤늦은A실패는_B의오류를덮지않는다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var first = service.DeferDetail(41);
        var second = service.DeferDetail(42);
        using var vm = new InboundRequestPageViewModel(service);

        var a = vm.LoadDetailAsync(41);
        var b = vm.LoadDetailAsync(42);
        second.SetException(new SsalddelApiException("B 실패", 403, "B 조회", string.Empty, null, errorCode: "B-forbidden"));
        await b;
        var currentFailure = vm.State.Failure;
        first.SetException(new SsalddelApiException("A 실패", 404, "A 조회", string.Empty, null, errorCode: "A-not-found"));
        await a;

        Assert.Same(currentFailure, vm.State.Failure);
        Assert.Equal("B-forbidden", vm.State.Failure?.코드);
        Assert.Null(vm.State.Current);
        Assert.False(vm.State.IsBusy);
    }

    [Fact]
    public async Task 이전A조회종료는_진행중B의로딩을끝내지않는다()
    {
        var service = new FakeWarehouseWorkspaceService();
        var first = service.DeferDetail(41);
        var second = service.DeferDetail(42);
        using var vm = new InboundRequestPageViewModel(service);

        var a = vm.LoadDetailAsync(41);
        var b = vm.LoadDetailAsync(42);
        first.SetException(new HttpRequestException("A 늦은 오류"));
        await a;
        Assert.True(vm.State.IsBusy);
        Assert.Null(vm.State.Failure);
        second.SetResult(new 입고요청항목응답 { Id = 42 });
        await b;

        Assert.Equal(42, vm.State.Current?.Id);
        Assert.False(vm.State.IsBusy);
    }

    [Fact]
    public async Task 진행중등록은_조회대상전환으로취소하거나중복제출하지않는다()
    {
        var service = new FakeWarehouseWorkspaceService
        {
            DeferredCreate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var vm = await CreateReadyAsync(service);
        using var cancellation = new CancellationTokenSource();

        var saving = vm.CreateInboundAsync(cancellation.Token);
        await vm.LoadDetailAsync(42);
        Assert.Null(await vm.CreateInboundAsync());
        Assert.Empty(service.DetailRequests);
        Assert.False(service.CreateToken.IsCancellationRequested);
        service.DeferredCreate.SetResult(new 입고요청항목응답 { Id = 77 });
        Assert.Equal(77, await saving);

        Assert.Equal(1, service.InboundCreateCount);
        Assert.Equal([77L], service.DetailRequests);
        Assert.Equal(77, vm.State.SavedInboundId);
    }

    private static async Task<InboundRequestPageViewModel> CreateReadyAsync(FakeWarehouseWorkspaceService service)
    {
        var vm = new InboundRequestPageViewModel(service);
        await vm.LoadCreateAsync(new InboundRequestNavigationContext { WarehouseId = 5, SupplierName = "공급처" });
        return vm;
    }

    private static async Task<InboundRequestPageViewModel> CreateCompletionReadyAsync(FakeWarehouseWorkspaceService service)
    {
        service.Inbounds.Add(new 입고요청항목응답
        {
            Id = 41, 창고Id = 5, 공급처명 = "공급처", 상태 = 입고상태코드.예정, 예정상품명 = "상품", 예정수량 = 2
        });
        var vm = new InboundRequestPageViewModel(service);
        await vm.LoadCompletionAsync(41);
        return vm;
    }

    private sealed class FakeWarehouseWorkspaceService : IWarehouseWorkspaceService
    {
        public List<입고요청항목응답> Inbounds { get; } = [];
        public List<long> DetailRequests { get; } = [];
        public List<long> CompletedRequests { get; } = [];
        public int WarehouseCreateCount { get; private set; }
        public int InboundCreateCount { get; private set; }
        public Exception? DetailFailure { get; set; }
        public Exception? CreateFailure { get; set; }
        public bool CompletionThrowsAfterApply { get; set; }
        public Exception? CompletionFailure { get; set; }
        public Func<long, 입고요청항목응답?>? DetailResolver { get; set; }
        public Dictionary<long, TaskCompletionSource<입고요청항목응답?>> DeferredDetails { get; } = [];
        public Dictionary<long, CancellationToken> DetailTokens { get; } = [];
        public TaskCompletionSource<입고요청항목응답?>? DeferredCreate { get; set; }
        public CancellationToken CreateToken { get; private set; }

        public TaskCompletionSource<입고요청항목응답?> DeferDetail(long id)
        {
            var completion = new TaskCompletionSource<입고요청항목응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
            DeferredDetails.Add(id, completion);
            return completion;
        }

        public Task<창고목록응답?> GetWarehousesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<창고목록응답?>(new()
            {
                Items =
                [
                    new 창고요약응답
                    {
                        Id = 5,
                        창고명 = "샘플 창고",
                        주소 = "서울시 샘플로 1",
                        IsActive = true
                    }
                ]
            });

        public Task<창고요약응답?> CreateWarehouseAsync(
            창고저장요청 payload,
            CancellationToken cancellationToken = default)
        {
            WarehouseCreateCount++;
            return Task.FromResult<창고요약응답?>(new()
            {
                Id = 21,
                창고명 = payload.창고명,
                주소 = payload.주소,
                물류대행지분류 = payload.물류대행지분류,
                IsActive = true
            });
        }

        public Task<입고요청목록응답?> GetInboundsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<입고요청목록응답?>(new() { Items = Inbounds.ToArray() });

        public Task<입고요청항목응답?> GetInboundAsync(long inboundId, CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(inboundId);
            DetailTokens[inboundId] = cancellationToken;
            if (DeferredDetails.TryGetValue(inboundId, out var deferred)) return deferred.Task;
            if (DetailFailure is not null) throw DetailFailure;
            if (DetailResolver is not null) return Task.FromResult(DetailResolver(inboundId));
            return Task.FromResult(Inbounds.FirstOrDefault(item => item.Id == inboundId));
        }

        public Task<입고요청항목응답?> CreateInboundAsync(
            입고요청저장요청 payload,
            CancellationToken cancellationToken = default)
        {
            InboundCreateCount++;
            CreateToken = cancellationToken;
            if (DeferredCreate is not null) return DeferredCreate.Task;
            if (CreateFailure is not null) throw CreateFailure;
            var item = new 입고요청항목응답
            {
                Id = 77,
                창고Id = payload.창고Id,
                공급처명 = payload.공급처명,
                원주문참조번호 = payload.원주문참조번호,
                상태 = 입고상태코드.예정,
                계약정보 = payload.계약정보
            };
            Inbounds.Add(item);
            return Task.FromResult<입고요청항목응답?>(item);
        }

        public Task<입고상품목록응답?> CompleteInboundAsync(
            long inboundId,
            입고완료요청 payload,
            CancellationToken cancellationToken = default)
        {
            CompletedRequests.Add(inboundId);
            if (CompletionFailure is not null) throw CompletionFailure;
            var inbound = Inbounds.Single(item => item.Id == inboundId);
            inbound.상태 = 입고상태코드.완료;
            inbound.입고완료일시 = DateTime.UtcNow;
            if (CompletionThrowsAfterApply) throw new HttpRequestException("완료 응답 유실");
            var item = payload.Items.Single();
            return Task.FromResult<입고상품목록응답?>(new()
            {
                Items =
                [
                    new 입고상품항목응답
                    {
                        Id = 501,
                        입고요청Id = inboundId,
                        창고Id = inbound.창고Id,
                        상품명 = item.상품명,
                        SKU = item.SKU,
                        입고수량 = item.입고수량,
                        불량수량 = item.불량수량,
                        가용수량 = item.입고수량 - item.불량수량,
                        보관위치 = item.보관위치,
                        상태 = "보관중"
                    }
                ]
            });
        }

        public Task<재고목록응답?> GetInventoryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<재고목록응답?>(new());
    }
}
