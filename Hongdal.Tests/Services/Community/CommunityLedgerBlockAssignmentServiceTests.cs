using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityLedgerBlockAssignmentServiceTests
{
    [Fact]
    public async Task 대표역할_참여자는_원장참여자를_블록담당자로_지정한다()
    {
        var ledger = CreateLedger();
        var store = new LedgerStoreStub(ledger);
        var service = new CommunityLedgerBlockAssignmentService(store);

        var result = await service.UpdateAsync(
            ledger.원장Id,
            "purchase-decision",
            new CommunityLedgerBlockAssignmentUpdateRequest
            {
                ExpectedRevision = ledger.Revision,
                Assignments =
                [
                    new CommunityLedgerBlockAssigneeUpdateRequest
                    {
                        UserId = "buyer-manager",
                        ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Primary
                    },
                    new CommunityLedgerBlockAssigneeUpdateRequest
                    {
                        UserId = "member-1",
                        ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Reviewer
                    }
                ]
            },
            "buyer-manager",
            default);

        Assert.True(result.IsSuccess);
        var request = Assert.IsType<커뮤니티원장저장요청>(store.LastSaveRequest);
        Assert.Equal(ledger.Revision, request.기대Revision);
        Assert.True(request.블록담당자명시적갱신여부);
        var block = Assert.Single(request.블록목록);
        Assert.Collection(
            block.담당자목록,
            primary =>
            {
                Assert.Equal("buyer-manager", primary.UserId);
                Assert.Equal("공동구매 대표", primary.DisplayName);
                Assert.Equal(CommunityLedgerBlockResponsibilityTypes.Primary, primary.ResponsibilityType);
            },
            reviewer =>
            {
                Assert.Equal("member-1", reviewer.UserId);
                Assert.Equal(CommunityLedgerBlockResponsibilityTypes.Reviewer, reviewer.ResponsibilityType);
            });
        Assert.Equal(8, result.Value.Revision);
    }

    [Fact]
    public async Task 원장에_등록되지_않은_사람은_블록담당자가_될_수_없다()
    {
        var ledger = CreateLedger();
        var service = new CommunityLedgerBlockAssignmentService(new LedgerStoreStub(ledger));

        var result = await service.UpdateAsync(
            ledger.원장Id,
            "purchase-decision",
            new CommunityLedgerBlockAssignmentUpdateRequest
            {
                Assignments =
                [
                    new CommunityLedgerBlockAssigneeUpdateRequest
                    {
                        UserId = "outsider",
                        ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Primary
                    }
                ]
            },
            "owner",
            default);

        Assert.True(result.IsFailed);
        Assert.Equal(400, result.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 한_블록의_주담당자는_한_명만_허용한다()
    {
        var ledger = CreateLedger();
        var service = new CommunityLedgerBlockAssignmentService(new LedgerStoreStub(ledger));

        var result = await service.UpdateAsync(
            ledger.원장Id,
            "purchase-decision",
            new CommunityLedgerBlockAssignmentUpdateRequest
            {
                Assignments =
                [
                    new CommunityLedgerBlockAssigneeUpdateRequest { UserId = "buyer-manager", ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Primary },
                    new CommunityLedgerBlockAssigneeUpdateRequest { UserId = "member-1", ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Primary }
                ]
            },
            "owner",
            default);

        Assert.True(result.IsFailed);
        Assert.Equal(400, result.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 담당자를_지정하면_주담당자_한_명이_필요하다()
    {
        var ledger = CreateLedger();
        var service = new CommunityLedgerBlockAssignmentService(new LedgerStoreStub(ledger));

        var result = await service.UpdateAsync(
            ledger.원장Id,
            "purchase-decision",
            new CommunityLedgerBlockAssignmentUpdateRequest
            {
                Assignments =
                [
                    new CommunityLedgerBlockAssigneeUpdateRequest { UserId = "buyer-manager", ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Collaborator },
                    new CommunityLedgerBlockAssigneeUpdateRequest { UserId = "member-1", ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Reviewer }
                ]
            },
            "owner",
            default);

        Assert.True(result.IsFailed);
        Assert.Equal(400, result.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public async Task 일반참여자는_담당자를_조회하지만_변경할_수는_없다()
    {
        var ledger = CreateLedger();
        var service = new CommunityLedgerBlockAssignmentService(new LedgerStoreStub(ledger));

        var settings = await service.GetAsync(ledger.원장Id, "purchase-decision", "member-1", default);
        var update = await service.UpdateAsync(
            ledger.원장Id,
            "purchase-decision",
            new CommunityLedgerBlockAssignmentUpdateRequest(),
            "member-1",
            default);

        Assert.True(settings.IsSuccess);
        Assert.False(settings.Value.CanManage);
        Assert.True(update.IsFailed);
        Assert.Equal(403, update.Errors[0].Metadata["StatusCode"]);
    }

    [Fact]
    public void 후보목록은_생성자와_활성참여자만_포함한다()
    {
        var ledger = CreateLedger();

        var candidates = CommunityLedgerBlockAssignmentPolicy.ResolveCandidates(ledger);

        Assert.Equal(3, candidates.Count);
        Assert.Equal(
            ["buyer-manager", "member-1", "owner"],
            candidates.Select(candidate => candidate.UserId).OrderBy(userId => userId));
        Assert.DoesNotContain(candidates, candidate => candidate.UserId == "withdrawn");
    }

    private static 커뮤니티원장Dto CreateLedger()
        => new()
        {
            원장Id = "group-purchase-1",
            Revision = 7,
            커뮤니티Id = "platform",
            원장템플릿Key = CommunityLedgerTemplateKeys.GroupPurchase,
            제목 = "감자 공동구매",
            상태 = 커뮤니티원장상태.진행중,
            생성자UserId = "owner",
            생성자표시명 = "원장 생성자",
            참여자목록 =
            [
                new 커뮤니티원장참여자Dto { UserId = "buyer-manager", DisplayName = "공동구매 대표", RoleLabel = "구매 담당자" },
                new 커뮤니티원장참여자Dto { UserId = "member-1", DisplayName = "참여 주민", RoleLabel = "주문자" },
                new 커뮤니티원장참여자Dto { UserId = "withdrawn", DisplayName = "탈퇴 주민", RoleLabel = "주문자", ParticipationState = "탈퇴" }
            ],
            블록목록 =
            [
                new 커뮤니티원장블록Dto
                {
                    BlockId = "purchase-decision",
                    BlockType = "decision",
                    Title = "구매 확정",
                    Data = new Dictionary<string, string> { ["수량"] = "100박스" }
                }
            ]
        };

    private sealed class LedgerStoreStub : I커뮤니티원장저장소
    {
        private 커뮤니티원장Dto _ledger;

        public LedgerStoreStub(커뮤니티원장Dto ledger)
        {
            _ledger = ledger;
        }

        public 커뮤니티원장저장요청? LastSaveRequest { get; private set; }

        public Task<커뮤니티원장Dto?> 원장조회Async(
            string 원장Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(_ledger);

        public Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            LastSaveRequest = request;
            _ledger = new 커뮤니티원장Dto
            {
                원장Id = request.원장Id ?? string.Empty,
                Revision = _ledger.Revision + 1,
                커뮤니티Id = request.커뮤니티Id,
                원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목,
                상태 = request.상태 ?? 커뮤니티원장상태.초안,
                생성자UserId = request.생성자UserId,
                생성자표시명 = request.생성자표시명 ?? "익명 참여자",
                참여자목록 = request.참여자목록,
                블록목록 = request.블록목록,
                포함원장목록 = request.포함원장목록 ?? [],
                다이어그램스냅샷 = request.다이어그램스냅샷,
                외부참조 = request.외부참조,
                확장속성 = request.확장속성
            };
            return Task.FromResult(_ledger);
        }

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>([_ledger]);

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
