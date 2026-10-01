using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class BaguaRoleTransitionPageViewModelTests
{
    [Fact]
    public void PageViewModel_ComposesEveryRoleAndTransitionWithoutCreating125Types()
    {
        using var viewModel = CreateViewModel();
        var contexts = 0;

        foreach (var role in BaguaTransitionCatalog.Roles)
        {
            foreach (var transition in BaguaTransitionCatalog.All)
            {
                viewModel.초기화(
                    role.RoleCode,
                    transition.SourceTrigramKey,
                    transition.TargetTrigramKey);

                Assert.Null(viewModel.오류메시지);
                Assert.True(viewModel.역할화면);
                Assert.NotNull(viewModel.페이지);
                Assert.NotNull(viewModel.업무조립);
                Assert.NotNull(viewModel.현재역할관점);
                Assert.Equal(Bagua서버권한상태.확인전, viewModel.서버권한.상태);
                Assert.False(viewModel.서버권한.실행허용);
                Assert.Equal(transition.WorkflowKind, viewModel.전환흐름?.WorkflowKind);
                Assert.Equal(transition.SourceBusinessCode, viewModel.업무조립?.Source.BusinessCode);
                Assert.Equal(transition.TargetBusinessCode, viewModel.업무조립?.Target.BusinessCode);
                Assert.Equal(5, viewModel.역할선택지.Count);
                Assert.Equal(5, viewModel.전환행.Count);
                Assert.All(viewModel.전환행, row => Assert.Equal(5, row.Cells.Count));
                Assert.Single(viewModel.전환행.SelectMany(row => row.Cells), cell => cell.현재셀);
                contexts++;
            }
        }

        Assert.Equal(125, contexts);
        Assert.Equal(5, viewModel.업무모듈.Count);
    }

    [Fact]
    public void RolePicker_HasFivePoliciesButDoesNotGuessAViewerRole()
    {
        using var viewModel = CreateViewModel();

        viewModel.초기화(null, BaguaTrigramKeys.Zhen, BaguaTrigramKeys.Dui);

        Assert.True(viewModel.역할선택화면);
        Assert.False(viewModel.역할화면);
        Assert.Null(viewModel.페이지);
        Assert.Null(viewModel.현재역할관점);
        Assert.Null(viewModel.업무조립);
        Assert.Equal(Bagua서버권한상태.대상선택필요, viewModel.서버권한.상태);
        Assert.Equal(5, viewModel.역할선택지.Count);
        Assert.Empty(viewModel.전환행);
        Assert.Equal("업무 인계", viewModel.전환흐름?.표시명);
        Assert.Equal(
            "/community/bagua/orderer/zhen/dui",
            viewModel.역할경로(BaguaActorRoleCodes.Orderer));
    }

    [Fact]
    public void DomainModules_MapFiveBusinessAreasToControllerActions()
    {
        var definitions = Bagua업무영역카탈로그.All;
        var controllerKeys = Controller기능카탈로그.공통
            .Concat(Controller기능카탈로그.화주)
            .Concat(Controller기능카탈로그.주문자)
            .Select(definition => definition.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(5, definitions.Count);
        Assert.Equal(5, definitions.Select(definition => definition.BusinessCode).Distinct().Count());
        Assert.Equal(67, definitions.Sum(definition => definition.Api기능.Count));
        Assert.Equal(
            2,
            definitions.SelectMany(definition => definition.Api기능)
                .Count(feature => feature.요청형식 == BaguaApi요청형식.MultipartForm));
        Assert.All(definitions, definition =>
        {
            Assert.NotEmpty(definition.ControllerKeys);
            Assert.NotEmpty(definition.Api기능);
            Assert.Equal(
                definition.Api기능.Count,
                definition.Api기능.Select(feature => feature.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(definition.ControllerKeys, key => Assert.Contains(key, controllerKeys));
            Assert.All(definition.Api기능, feature =>
                Assert.Contains(feature.ControllerKey, definition.ControllerKeys));
        });
    }

    [Fact]
    public void WorkflowPolicies_CoverAllSevenCanonicalKinds()
    {
        var workflows = BaguaTransitionCatalog.All
            .Select(Bagua전환흐름ViewModel.Create)
            .GroupBy(workflow => workflow.WorkflowKind, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(7, workflows.Length);
        Assert.All(workflows, group =>
        {
            Assert.All(group, workflow => Assert.False(string.IsNullOrWhiteSpace(workflow.표시명)));
            Assert.All(group, workflow => Assert.False(string.IsNullOrWhiteSpace(workflow.제목)));
            Assert.All(group, workflow => Assert.False(string.IsNullOrWhiteSpace(workflow.설명)));
        });
    }

    [Fact]
    public void PerspectivePolicies_KeepObserverPagesReadOnlyByDefault()
    {
        var policies = BaguaTransitionCatalog.Roles
            .SelectMany(role => BaguaTransitionCatalog.All.Select(transition =>
                Bagua역할관점ViewModel.Create(
                    role,
                    BaguaTransitionCatalog.FindPerspective(role.RoleCode, transition.TransitionKey))))
            .ToArray();

        Assert.Equal(125, policies.Length);
        Assert.Equal(80, policies.Count(policy => policy.조회중심));
        Assert.Equal(45, policies.Count(policy => policy.행동후보표시));
        Assert.All(
            policies.Where(policy => policy.조회중심),
            policy => Assert.Equal(BaguaRolePerspectiveModes.Observer, policy.관점.PerspectiveMode));
    }

    [Fact]
    public void RolePerspective_DoesNotGrantServerPermission()
    {
        using var viewModel = CreateViewModel();

        viewModel.초기화(
            BaguaActorRoleCodes.Orderer,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Dui);

        Assert.Equal(BaguaRolePerspectiveModes.Initiator, viewModel.현재역할관점?.관점.PerspectiveMode);
        Assert.True(viewModel.현재역할관점!.행동후보표시);
        Assert.False(viewModel.서버권한.실행허용);

        viewModel.서버권한.확인시작();
        viewModel.서버권한.권한적용(["create-inbound"]);

        Assert.True(viewModel.서버권한.실행허용);
        Assert.True(viewModel.서버권한.허용됨("create-inbound"));
        Assert.False(viewModel.서버권한.허용됨("complete-inbound"));
    }

    [Fact]
    public void OrderToWarehouse_ComposesBothControllerFamiliesAndReceiverPolicy()
    {
        using var viewModel = CreateViewModel();

        viewModel.초기화(
            BaguaActorRoleCodes.WarehouseManager,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Dui);

        Assert.Equal(
            ["common.order-ledgers", "common.warehouse-operations"],
            viewModel.업무조립?.Controllers.Select(controller => controller.Key));
        Assert.Contains(
            viewModel.업무조립!.Api기능,
            feature => feature.Key == "view-warehouse");
        Assert.Contains(
            viewModel.업무조립.Api기능,
            feature => feature.Key == "create-inbound");
        Assert.Equal(
            "api/v1/community/order-ledgers/order-42/views/warehouse",
            viewModel.업무조립.Source.Api경로(
                "view-warehouse",
                new Dictionary<string, string> { ["주문원장Id"] = "order-42" }));
        Assert.Equal(
            BaguaRolePerspectiveModes.Receiver,
            viewModel.업무조립.RolePerspective.관점.PerspectiveMode);
    }

    [Fact]
    public void OrderToAgreement_ComposesLedgerAndGovernanceControllers()
    {
        using var viewModel = CreateViewModel();

        viewModel.초기화(
            BaguaActorRoleCodes.CooperativeCoordinator,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Gen);

        Assert.Equal(
            [
                "common.order-ledgers",
                "common.community-votes",
                "orderer.demand-votes",
                "orderer.negotiation"
            ],
            viewModel.업무조립?.Controllers.Select(controller => controller.Key));
        Assert.Equal(BaguaTransitionWorkflowKinds.Governance, viewModel.업무조립?.Workflow.WorkflowKind);
        Assert.Equal(
            BaguaRolePerspectiveModes.Governor,
            viewModel.업무조립?.RolePerspective.관점.PerspectiveMode);
    }

    [Fact]
    public void HomeTransition_ReusesOneDomainViewModelInstance()
    {
        using var viewModel = CreateViewModel();

        viewModel.초기화(
            BaguaActorRoleCodes.Seller,
            BaguaTrigramKeys.Li,
            BaguaTrigramKeys.Li);

        Assert.NotNull(viewModel.업무조립);
        Assert.Same(viewModel.업무조립.Source, viewModel.업무조립.Target);
        Assert.Single(viewModel.업무조립.ActiveDomains);
    }

    [Fact]
    public void WorkspaceResolver_CanReplaceTargetRoutePerClientOrRole()
    {
        using var viewModel = CreateViewModel(new TestWorkspaceResolver());

        viewModel.초기화(
            BaguaActorRoleCodes.WarehouseManager,
            BaguaTrigramKeys.Zhen,
            BaguaTrigramKeys.Dui);

        Assert.Equal("창고 관리자 전용 창고", viewModel.페이지?.TargetWorkspaceName);
        Assert.Equal(
            "/test/warehouse-manager/order/warehouse",
            viewModel.목표업무경로);
    }

    [Fact]
    public void InvalidRoute_BecomesPageStateInsteadOfLeakingCatalogException()
    {
        using var viewModel = CreateViewModel();

        viewModel.초기화(
            BaguaActorRoleCodes.Orderer,
            "unknown",
            BaguaTrigramKeys.Gen);

        Assert.NotNull(viewModel.오류메시지);
        Assert.False(viewModel.역할선택화면);
        Assert.False(viewModel.역할화면);
        Assert.Null(viewModel.페이지);
    }

    [Fact]
    public void SharedServices_RegisterBaguaViewModelsWithComponentSafeLifetimes()
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBagua업무영역ViewModelFactory)
            && descriptor.ImplementationType == typeof(Bagua업무영역ViewModelFactory)
            && descriptor.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBaguaTargetWorkspaceResolver)
            && descriptor.ImplementationType == typeof(DefaultBaguaTargetWorkspaceResolver)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(BaguaRoleTransitionPageViewModel)
            && descriptor.ImplementationType == typeof(BaguaRoleTransitionPageViewModel)
            && descriptor.Lifetime == ServiceLifetime.Transient);
    }

    private static BaguaRoleTransitionPageViewModel CreateViewModel(
        IBaguaTargetWorkspaceResolver? workspaceResolver = null)
    {
        var apiClient = new NeverCalledApiClient();
        var domainFactory = new Bagua업무영역ViewModelFactory(apiClient);
        return new BaguaRoleTransitionPageViewModel(
            domainFactory,
            workspaceResolver ?? new DefaultBaguaTargetWorkspaceResolver(),
            apiClient);
    }

    private sealed class TestWorkspaceResolver : IBaguaTargetWorkspaceResolver
    {
        public BaguaTargetWorkspace Resolve(BaguaTargetWorkspaceContext context)
            => new(
                $"{context.Role.RoleName} 전용 {context.TargetArea.BusinessName}",
                $"/test/{context.Role.RoleCode}/{context.SourceArea.BusinessCode}/{context.TargetArea.BusinessCode}");
    }

    private sealed class NeverCalledApiClient : IHongdalJsonApiClient
    {
        public Task<TResponse?> GetAsync<TResponse>(
            string path,
            string operationName,
            bool allowNotFound = true,
            CancellationToken cancellationToken = default)
            => Unexpected<TResponse?>();

        public Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
            => Unexpected<TResponse?>();

        public Task<TResponse?> SendAsync<TRequest, TResponse>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            bool allowNotFound = false,
            CancellationToken cancellationToken = default)
            => Unexpected<TResponse?>();

        public Task SendAsync(
            HttpMethod method,
            string path,
            string operationName,
            CancellationToken cancellationToken = default)
            => Unexpected();

        public Task SendAsync<TRequest>(
            HttpMethod method,
            string path,
            TRequest request,
            string operationName,
            CancellationToken cancellationToken = default)
            => Unexpected();

        private static Task<T> Unexpected<T>()
            => Task.FromException<T>(new InvalidOperationException("PageViewModel 초기화 중 API를 호출하면 안 됩니다."));

        private static Task Unexpected()
            => Task.FromException(new InvalidOperationException("PageViewModel 초기화 중 API를 호출하면 안 됩니다."));
    }
}
