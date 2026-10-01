using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityBoardUseCaseTests
{
    [Fact]
    public async Task 신청과승인은_계정식별자를기록하고_공개목록에서검토내용을숨긴다()
    {
        await using var context = CreateContext();
        var useCase = new 커뮤니티게시판UseCase(context);

        var applied = await useCase.신청Async(
            NewRequest("우리 동네 채소"),
            "user-17",
            "열일곱번째 회원",
            CancellationToken.None);

        Assert.True(applied.IsSuccess);
        var entity = await context.PlatformCommunityBoardRequests.SingleAsync(CancellationToken.None);
        Assert.Equal("user-17", entity.RequestedByUserId);
        Assert.Equal("열일곱번째 회원", entity.RequestedBy);

        var approved = await useCase.승인Async(
            entity.Id,
            new PlatformCommunityBoardReviewRequest { OperatorMemo = "목적과 운영 규칙 확인" },
            "admin-1",
            CancellationToken.None);

        Assert.True(approved.IsSuccess);
        Assert.Equal("admin-1", entity.ReviewedByUserId);

        var publicList = await useCase.목록Async(
            "platform",
            PlatformCommunityBoardRequestStatuses.Approved,
            includeReviewDetails: false,
            CancellationToken.None);
        var publicBoard = Assert.Single(publicList.Value.Items);
        Assert.Equal(string.Empty, publicBoard.RequestReason);
        Assert.Null(publicBoard.OperatorMemo);

        var adminList = await useCase.목록Async(
            "platform",
            PlatformCommunityBoardRequestStatuses.Approved,
            includeReviewDetails: true,
            CancellationToken.None);
        var reviewedBoard = Assert.Single(adminList.Value.Items);
        Assert.Equal("지역 생산자와 소비자 정보 교환", reviewedBoard.RequestReason);
        Assert.Equal("목적과 운영 규칙 확인", reviewedBoard.OperatorMemo);
    }

    [Fact]
    public async Task 이미검토한신청은_다시승인하거나반려할수없다()
    {
        await using var context = CreateContext();
        var useCase = new 커뮤니티게시판UseCase(context);
        var applied = await useCase.신청Async(
            NewRequest("농산물 포장 연구"),
            "user-1",
            "신청자",
            CancellationToken.None);

        var first = await useCase.승인Async(
            applied.Value.Id,
            new PlatformCommunityBoardReviewRequest(),
            "admin-1",
            CancellationToken.None);
        var second = await useCase.반려Async(
            applied.Value.Id,
            new PlatformCommunityBoardReviewRequest(),
            "admin-2",
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailed);
        Assert.Contains("이미 검토", second.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 기본게시판이름과_사용자당네번째대기신청은거절한다()
    {
        await using var context = CreateContext();
        var useCase = new 커뮤니티게시판UseCase(context);

        var reserved = await useCase.신청Async(
            NewRequest(CommunityBoardCatalog.FreeLife.DisplayName),
            "user-1",
            "신청자",
            CancellationToken.None);
        Assert.True(reserved.IsFailed);

        for (var index = 1; index <= 3; index++)
        {
            var result = await useCase.신청Async(
                NewRequest($"사용자 게시판 {index}"),
                "user-1",
                "신청자",
                CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        var fourth = await useCase.신청Async(
            NewRequest("사용자 게시판 4"),
            "user-1",
            "신청자",
            CancellationToken.None);
        Assert.True(fourth.IsFailed);
        Assert.Contains("최대 3개", fourth.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 게시글작성정책은_게시판별익명로그인운영자조건을적용한다()
    {
        await using var context = CreateContext();
        context.PlatformCommunityBoardRequests.AddRange(
            NewEntity("승인 게시판", PlatformCommunityBoardRequestStatuses.Approved),
            NewEntity("대기 게시판", PlatformCommunityBoardRequestStatuses.Pending));
        await context.SaveChangesAsync(CancellationToken.None);
        var policy = new CommunityBoardWritePolicy(context);

        Assert.True(await policy.CanWriteAsync(
            "platform",
            CommunityBoardCatalog.InformationPrices.DisplayName,
            null,
            CancellationToken.None));
        Assert.True(await policy.CanWriteAsync(
            "platform",
            CommunityBoardCatalog.Vow.DisplayName,
            null,
            CancellationToken.None));
        Assert.False(await policy.CanWriteAsync(
            "platform",
            CommunityBoardCatalog.Cargo.DisplayName,
            null,
            CancellationToken.None));
        Assert.True(await policy.CanWriteAsync(
            "platform",
            CommunityBoardCatalog.Cargo.DisplayName,
            "cargo-writer",
            CancellationToken.None));
        Assert.False(await policy.CanWriteAsync(
            "platform",
            "승인 게시판",
            null,
            CancellationToken.None));
        Assert.True(await policy.CanWriteAsync(
            "platform",
            "승인 게시판",
            "custom-board-writer",
            CancellationToken.None));
        Assert.False(await policy.CanWriteAsync(
            "platform",
            CommunityBoardCatalog.Prajna.DisplayName,
            "member-1",
            CancellationToken.None));
        Assert.False(await policy.CanWriteAsync(
            "platform",
            CommunityBoardCatalog.NoticeGuide.DisplayName,
            "member-1",
            CancellationToken.None));
        Assert.True(await policy.CanWriteAsync(
            "platform",
            CommunityBoardCatalog.SafetyReport.DisplayName,
            null,
            CancellationToken.None));
        Assert.False(await policy.CanWriteAsync(
            "platform",
            "대기 게시판",
            "member-1",
            CancellationToken.None));
        Assert.False(await policy.CanWriteAsync(
            "platform",
            "신청하지 않은 게시판",
            "member-1",
            CancellationToken.None));
    }

    private static PlatformCommunityBoardCreateRequest NewRequest(string title)
        => new()
        {
            AppKey = "platform",
            Title = title,
            Description = "지역 정보를 주제별로 정리합니다.",
            RequestReason = "지역 생산자와 소비자 정보 교환"
        };

    private static PlatformCommunityBoardRequest NewEntity(string title, string status)
        => new()
        {
            AppKey = "platform",
            BoardKey = title.Replace(' ', '-'),
            Title = title,
            Description = "설명",
            RequestedByUserId = "user-1",
            RequestedBy = "신청자",
            RequestReason = "개설 이유",
            Status = status
        };

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"community-board-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
