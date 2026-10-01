using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityLedgerRoleAccessPolicyEvaluatorTests
{
    [Fact]
    public void 관세사_기본_조회는_HS코드_선적_통관_반출_노드로_제한한다()
    {
        var ledger = CreateLedger();

        var visibleNodeIds = CommunityLedgerRoleAccessPolicyEvaluator.ResolveVisibleNodeIds(
            ledger,
            CommunityLedgerNodeViewScopes.RoleOnly,
            []);

        Assert.Equal(
            ["hs-code", "overseas-shipment", "customs-state", "domestic-release"],
            visibleNodeIds);
        Assert.DoesNotContain("import-decision", visibleNodeIds);
        Assert.DoesNotContain("distribution", visibleNodeIds);
    }

    [Fact]
    public void 구매측은_선택한_노드만_조회하도록_범위를_줄일_수_있다()
    {
        var ledger = CreateLedger();

        var visibleNodeIds = CommunityLedgerRoleAccessPolicyEvaluator.ResolveVisibleNodeIds(
            ledger,
            CommunityLedgerNodeViewScopes.SelectedNodes,
            ["customs-state", "distribution", "missing", "customs-state"]);

        Assert.Equal(["customs-state", "distribution"], visibleNodeIds);
    }

    [Fact]
    public void 생성자와_수입결정자는_관세사_권한을_관리할_수_있다()
    {
        var ledger = CreateLedger();

        Assert.True(CommunityLedgerRoleAccessPolicyEvaluator.CanManage(ledger, "owner"));
        Assert.True(CommunityLedgerRoleAccessPolicyEvaluator.CanManage(ledger, "buyer-manager"));
        Assert.False(CommunityLedgerRoleAccessPolicyEvaluator.CanManage(ledger, "ordinary-member"));
        Assert.False(CommunityLedgerRoleAccessPolicyEvaluator.CanManage(ledger, "broker"));
    }

    [Fact]
    public async Task 별도설정이_없는_승인_관세사는_역할노드만_조회한다()
    {
        var service = CreateService(policy: null, brokerEligible: true);

        var decision = await service.EvaluateAsync(CreateLedger(), "broker", default);

        Assert.True(decision.HasRoleAccess);
        Assert.True(decision.UseRoleScope);
        Assert.Equal("관세사", decision.RoleName);
        Assert.Equal(
            ["hs-code", "overseas-shipment", "customs-state", "domestic-release"],
            decision.VisibleNodeIds);
        Assert.Empty(decision.EditableNodeIds);
        Assert.False(decision.CanCoordinateTransport);
    }

    [Fact]
    public async Task 구매측이_허용한_조회_편집_운송주선_범위를_적용한다()
    {
        var policy = new CommunityLedgerRoleAccessPolicy
        {
            LedgerId = "group-import-1",
            OwnerUserId = "owner",
            Grants =
            [
                new CommunityLedgerRoleGrant
                {
                    TargetUserId = "broker",
                    AccessEnabled = true,
                    ViewScope = CommunityLedgerNodeViewScopes.SelectedNodes,
                    VisibleNodeIds = ["customs-state", "domestic-release"],
                    EditableNodeIds = ["customs-state", "distribution"],
                    CanCoordinateTransport = true
                }
            ]
        };
        var service = CreateService(policy, brokerEligible: true);

        var decision = await service.EvaluateAsync(CreateLedger(), "broker", default);

        Assert.Equal(["customs-state", "domestic-release"], decision.VisibleNodeIds);
        Assert.Equal(["customs-state"], decision.EditableNodeIds);
        Assert.True(decision.CanCoordinateTransport);
    }

    [Fact]
    public async Task 구매측이_접근을_거부하면_승인_관세사도_원장을_조회하지_못한다()
    {
        var policy = new CommunityLedgerRoleAccessPolicy
        {
            LedgerId = "group-import-1",
            OwnerUserId = "owner",
            Grants =
            [
                new CommunityLedgerRoleGrant
                {
                    TargetUserId = "broker",
                    AccessEnabled = false
                }
            ]
        };
        var service = CreateService(policy, brokerEligible: true);

        var decision = await service.EvaluateAsync(CreateLedger(), "broker", default);

        Assert.Equal(CommunityLedgerRoleAccessDecision.None, decision);
    }

    private static CommunityLedgerRoleAccessService CreateService(
        CommunityLedgerRoleAccessPolicy? policy,
        bool brokerEligible)
        => new(
            null!,
            new PolicyStoreStub(policy),
            new BrokerDirectoryStub(brokerEligible));

    private static 커뮤니티원장Dto CreateLedger()
        => new()
        {
            원장Id = "group-import-1",
            원장템플릿Key = CommunityLedgerTemplateKeys.GroupImport,
            생성자UserId = "owner",
            참여자목록 =
            [
                new 커뮤니티원장참여자Dto
                {
                    UserId = "buyer-manager",
                    DisplayName = "구매 대표",
                    RoleLabel = "수입 결정자"
                },
                new 커뮤니티원장참여자Dto
                {
                    UserId = "ordinary-member",
                    DisplayName = "입주민",
                    RoleLabel = "참여자"
                }
            ],
            다이어그램스냅샷 = new DiagramSnapshotDto
            {
                DiagramId = "diagram-group-import-1",
                LedgerId = "group-import-1",
                LedgerTemplateKey = CommunityLedgerTemplateKeys.GroupImport,
                Nodes =
                [
                    Node("source-demand", "원천 공동구매 수요"),
                    Node("import-decision", "수입 결정"),
                    Node("hs-code", "HS CODE 품목 분류"),
                    Node("overseas-shipment", "해외 선적 서류"),
                    Node("customs-state", "통관 상태"),
                    Node("domestic-release", "국내 반출"),
                    Node("distribution", "세대 분배")
                ]
            }
        };

    private static DiagramNodeDto Node(string id, string title)
        => new() { NodeId = id, Kind = "workflow", Title = title };

    private sealed class PolicyStoreStub : ICommunityLedgerRoleAccessPolicyStore
    {
        private readonly CommunityLedgerRoleAccessPolicy? _policy;

        public PolicyStoreStub(CommunityLedgerRoleAccessPolicy? policy)
        {
            _policy = policy;
        }

        public Task<CommunityLedgerRoleAccessPolicy?> GetAsync(
            string ledgerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_policy);

        public Task<CommunityLedgerRoleAccessPolicy> SaveAsync(
            CommunityLedgerRoleAccessPolicy policy,
            long? expectedRevision,
            CancellationToken cancellationToken = default)
            => Task.FromResult(policy);
    }

    private sealed class BrokerDirectoryStub : ICommunityCustomsBrokerDirectory
    {
        private readonly bool _eligible;

        public BrokerDirectoryStub(bool eligible)
        {
            _eligible = eligible;
        }

        public Task<IReadOnlyList<CommunityLedgerCustomsBrokerCandidateResponse>> ListEligibleAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CommunityLedgerCustomsBrokerCandidateResponse>>([]);

        public Task<bool> IsEligibleAsync(
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_eligible);
    }
}
