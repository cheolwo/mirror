using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화주운송업무담당자UseCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 배정이없으면_화주를암묵적주담당으로조회한다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(CreateRequest());
        await db.SaveChangesAsync();
        var useCase = CreateUseCase(db, "shipper-1");

        var result = await useCase.조회Async("request-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Revision);
        Assert.True(result.Value.암묵적화주주담당여부);
        Assert.True(result.Value.담당자관리가능여부);
        var primary = Assert.Single(result.Value.담당자목록);
        Assert.Equal("shipper-1", primary.UserId);
        Assert.Equal(운송업무담당유형Codes.주담당, primary.담당유형Code);
        Assert.Equal(
            운송업무권한Codes.주담당기본권한.OrderBy(x => x),
            primary.권한Codes.OrderBy(x => x));
    }

    [Fact]
    public async Task 화주는_주담당한명과보조담당자를_판본화해지정하고_같은요청을멱등재시도한다()
    {
        await using var db = CreateContext();
        await SeedUsersAndRequestAsync(db);
        var useCase = CreateUseCase(db, "shipper-1");
        var clientRequestId = Guid.NewGuid();
        var request = AssignmentRequest(
            clientRequestId,
            expectedRevision: 0,
            ("operator-primary", 운송업무담당유형Codes.주담당, []),
            ("operator-assistant", 운송업무담당유형Codes.보조담당, []));

        var result = await useCase.변경Async("request-1", request);
        var replay = await useCase.변경Async("request-1", request);

        Assert.True(result.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(1, result.Value.Revision);
        Assert.Equal(1, replay.Value.Revision);
        Assert.False(result.Value.암묵적화주주담당여부);
        Assert.Equal(2, await db.운송업무담당자배정.CountAsync());
        var assistant = result.Value.담당자목록.Single(x => x.UserId == "operator-assistant");
        Assert.Equal(
            운송업무권한Codes.보조담당기본권한.OrderBy(x => x),
            assistant.권한Codes.OrderBy(x => x));
    }

    [Fact]
    public async Task 담당자는_배정을재위임하지못하고_보조기본권한은조회증빙연락으로제한된다()
    {
        await using var db = CreateContext();
        await SeedUsersAndRequestAsync(db);
        var ownerUseCase = CreateUseCase(db, "shipper-1");
        var assigned = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                0,
                ("operator-primary", 운송업무담당유형Codes.주담당, []),
                ("operator-assistant", 운송업무담당유형Codes.보조담당, [])));
        Assert.True(assigned.IsSuccess);
        var requestEntity = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        var primaryUseCase = CreateUseCase(db, "operator-primary");
        var assistantUseCase = CreateUseCase(db, "operator-assistant");

        Assert.True(await primaryUseCase.권한보유Async(requestEntity, 운송업무권한Codes.정산확인));
        Assert.True(await assistantUseCase.권한보유Async(requestEntity, 운송업무권한Codes.진행조회));
        Assert.True(await assistantUseCase.권한보유Async(requestEntity, 운송업무권한Codes.현장증빙등록));
        Assert.True(await assistantUseCase.권한보유Async(requestEntity, 운송업무권한Codes.연락기록));
        Assert.False(await assistantUseCase.권한보유Async(requestEntity, 운송업무권한Codes.주소시간연락처수정));
        Assert.False(await assistantUseCase.권한보유Async(requestEntity, 운송업무권한Codes.정산확인));

        var redelegation = await primaryUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                1,
                ("operator-primary", 운송업무담당유형Codes.주담당, [])));
        Assert.True(redelegation.IsFailed);
        Assert.Equal(2, await db.운송업무담당자배정.CountAsync());
    }

    [Fact]
    public async Task 화주가보조추가권한을명시하면_새판본부터적용하고_과거판본은보존한다()
    {
        await using var db = CreateContext();
        await SeedUsersAndRequestAsync(db);
        var ownerUseCase = CreateUseCase(db, "shipper-1");
        var first = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                0,
                ("operator-primary", 운송업무담당유형Codes.주담당, []),
                ("operator-assistant", 운송업무담당유형Codes.보조담당, [])));
        Assert.True(first.IsSuccess);

        var second = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                1,
                ("operator-primary", 운송업무담당유형Codes.주담당, []),
                ("operator-assistant", 운송업무담당유형Codes.보조담당,
                    [운송업무권한Codes.주소시간연락처수정])));

        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value.Revision);
        Assert.Equal(4, await db.운송업무담당자배정.CountAsync());
        Assert.Equal(2, await db.운송업무담당자배정.Select(x => x.배정세트Revision).Distinct().CountAsync());
        var requestEntity = await db.화주운송의뢰.AsNoTracking().SingleAsync();
        var assistantUseCase = CreateUseCase(db, "operator-assistant");
        Assert.True(await assistantUseCase.권한보유Async(
            requestEntity,
            운송업무권한Codes.주소시간연락처수정));

        var stale = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                1,
                ("operator-primary", 운송업무담당유형Codes.주담당, [])));
        Assert.True(stale.IsFailed);
        Assert.Equal(4, await db.운송업무담당자배정.CountAsync());
    }

    [Fact]
    public async Task 보조담당자는_의뢰목록과단건을조회하지만_새판본에서제외되면즉시조회할수없다()
    {
        await using var db = CreateContext();
        await SeedUsersAndRequestAsync(db);
        var ownerUseCase = CreateUseCase(db, "shipper-1");
        var first = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                0,
                ("operator-primary", 운송업무담당유형Codes.주담당, []),
                ("operator-assistant", 운송업무담당유형Codes.보조담당, [])));
        Assert.True(first.IsSuccess);
        var assistantAccessor = new TestCurrentUserAccessor("operator-assistant", "담당자");
        var assistantUseCase = new 화주운송업무담당자UseCase(db, assistantAccessor, new FixedTimeProvider(Now));
        var singleHandler = new 의뢰단건조회QueryHandler(db, assistantUseCase);
        var listHandler = new 의뢰목록조회QueryHandler(db, assistantAccessor, assistantUseCase);

        Assert.NotNull(await singleHandler.Handle(new 의뢰단건조회Query("request-1"), CancellationToken.None));
        Assert.Single(await listHandler.Handle(
            new 의뢰목록조회Query(null, null, null, null, 1, 20),
            CancellationToken.None));

        var second = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                1,
                ("operator-primary", 운송업무담당유형Codes.주담당, [])));
        Assert.True(second.IsSuccess);
        Assert.Null(await singleHandler.Handle(new 의뢰단건조회Query("request-1"), CancellationToken.None));
        Assert.Empty(await listHandler.Handle(
            new 의뢰목록조회Query(null, null, null, null, 1, 20),
            CancellationToken.None));
    }

    [Fact]
    public async Task 보조담당자의주소수정은_화주가추가권한을부여한판본부터허용된다()
    {
        await using var db = CreateContext();
        await SeedUsersAndRequestAsync(db);
        var ownerUseCase = CreateUseCase(db, "shipper-1");
        var first = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                0,
                ("operator-primary", 운송업무담당유형Codes.주담당, []),
                ("operator-assistant", 운송업무담당유형Codes.보조담당, [])));
        Assert.True(first.IsSuccess);
        var assistantUseCase = CreateUseCase(db, "operator-assistant");
        var handler = new 의뢰수정CommandHandler(db, assistantUseCase);
        var command = AddressUpdateCommand("변경된 상세 위치");

        var denied = await handler.Handle(command, CancellationToken.None);
        Assert.True(denied.IsFailed);

        var second = await ownerUseCase.변경Async(
            "request-1",
            AssignmentRequest(
                Guid.NewGuid(),
                1,
                ("operator-primary", 운송업무담당유형Codes.주담당, []),
                ("operator-assistant", 운송업무담당유형Codes.보조담당,
                    [운송업무권한Codes.주소시간연락처수정])));
        Assert.True(second.IsSuccess);

        var allowed = await handler.Handle(command, CancellationToken.None);
        Assert.True(allowed.IsSuccess);
        Assert.Equal("변경된 상세 위치", (await db.화주운송의뢰.AsNoTracking().SingleAsync()).픽업_상세주소);
    }

    private static 운송업무담당자배정변경요청 AssignmentRequest(
        Guid requestId,
        long expectedRevision,
        params (string UserId, string RoleCode, string[] Permissions)[] assignments)
        => new()
        {
            클라이언트요청Id = requestId,
            예상Revision = expectedRevision,
            담당자목록 = assignments.Select(x => new 운송업무담당자지정요청
            {
                UserId = x.UserId,
                담당유형Code = x.RoleCode,
                권한Codes = x.Permissions
            }).ToArray()
        };

    private static 의뢰수정Command AddressUpdateCommand(string detailAddress)
        => new(
            "request-1",
            new 운송조건입력값(null, null, null),
            new 화물정보입력값(null, null, null, null, null, null, null),
            new 위치정보입력값(null, detailAddress, null, null, null, null, null, null),
            new 위치정보입력값(null, null, null, null, null, null, null, null),
            new 요청조건입력값(null),
            null);

    private static async Task SeedUsersAndRequestAsync(SsalddelContext db)
    {
        db.Users.AddRange(
            new ApplicationUser { Id = "shipper-1", UserName = "shipper-1" },
            new ApplicationUser { Id = "operator-primary", UserName = "operator-primary" },
            new ApplicationUser { Id = "operator-assistant", UserName = "operator-assistant" });
        db.화주운송의뢰.Add(CreateRequest());
        await db.SaveChangesAsync();
    }

    private static 화주운송의뢰 CreateRequest()
        => new()
        {
            의뢰Id = "request-1",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            상태 = "생성됨",
            결제상태 = "결제대기",
            배차상태 = "미시작",
            픽업_도로명주소 = "서울 중랑구",
            하차_도로명주소 = "서울 동대문구",
            CreatedAt = Now.AddMinutes(-10).UtcDateTime,
            UpdatedAt = Now.AddMinutes(-10).UtcDateTime
        };

    private static 화주운송업무담당자UseCase CreateUseCase(SsalddelContext db, string userId)
        => new(db, new TestCurrentUserAccessor(userId, "화주"), new FixedTimeProvider(Now));

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"transport-operator-{Guid.NewGuid():N}")
                .Options,
            new PassThroughEncryption());

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
