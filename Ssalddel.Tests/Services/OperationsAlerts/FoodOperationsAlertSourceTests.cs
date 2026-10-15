using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Services.OperationsAlerts;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.OperationsAlerts;

public sealed class FoodOperationsAlertSourceTests
{
    [Fact]
    public async Task 주의단계와자동회복신호는_운영자알림을만들지않는다()
    {
        await using var db = CreateContext();
        var row = Row();
        row.생명주기조화.업무지연Codes = [음식배달운영업무지연Codes.음식점준비지연];
        row.생명주기조화.음식점준비지연상태Code = 음식배달운영지연상태Codes.주의;
        row.생명주기조화.자동회복Codes = [음식배달운영자동회복Codes.기사추천만료];
        row.생명주기조화.자동회복대상여부 = true;

        var observations = await Source(db, row).ReadCurrentAsync(row.주문번호, default);

        Assert.NotEmpty(observations);
        Assert.All(observations, x => Assert.False(x.Active));
    }

    [Theory]
    [InlineData("RestaurantPreparationDelay", "Warning")]
    [InlineData("DeliveryProgressDelay", "Warning")]
    [InlineData("DriverAssignmentDelay", "Warning")]
    [InlineData("DispatchLedgerMissing", "Urgent")]
    [InlineData("DispatchCorrelationMismatch", "Urgent")]
    [InlineData("SharedLedgerOutboxReviewRequired", "Warning")]
    [InlineData("DriverNotificationOutboxReviewRequired", "Urgent")]
    public async Task 원장에서운영자확인으로판정한문제만_활성관찰한다(string code, string severity)
    {
        await using var db = CreateContext();
        var row = Row();
        row.생명주기조화.예외Codes = [code];
        row.생명주기조화.운영자확인필요여부 = true;

        var observations = await Source(db, row).ReadCurrentAsync(row.주문번호, default);

        var problem = Assert.Single(observations, x => x.Active);
        Assert.Equal(code, problem.ProblemCode);
        Assert.Equal(severity, problem.Severity);
        Assert.Equal("food", problem.SourceKind);
        Assert.DoesNotContain("private-phone", JsonSerializer.Serialize(observations));
        Assert.DoesNotContain("private-address", JsonSerializer.Serialize(observations));
    }

    [Fact]
    public async Task 픽업후중단은_즉시운영확인이필요하고_전달완료재조회시해소된다()
    {
        await using var db = CreateContext();
        var row = Row();
        var attempt = new 음식배달시도운영응답
        {
            시도순번 = 1, 픽업완료시각Utc = DateTime.UtcNow.AddMinutes(-5), 중단시각Utc = DateTime.UtcNow.AddMinutes(-1)
        };
        row.배달시도목록 = [attempt];
        var source = Source(db, row);
        var active = Assert.Single(await source.ReadCurrentAsync(row.주문번호, default), x => x.Active);
        Assert.Equal("AfterPickupRecoveryRequired", active.ProblemCode);
        Assert.Equal("Urgent", active.Severity);

        attempt.전달완료시각Utc = DateTime.UtcNow;
        Assert.All(await source.ReadCurrentAsync(row.주문번호, default), x => Assert.False(x.Active));
    }

    [Fact]
    public async Task 자동복구중에서소진으로바뀐뒤_다시회복하면_같은문제의활성을해제한다()
    {
        await using var db = CreateContext();
        var row = Row();
        var source = Source(db, row);
        Assert.False((await source.ReadCurrentAsync(row.주문번호, default)).Single(x => x.ProblemCode == "DriverNotificationOutboxReviewRequired").Active);
        row.생명주기조화.예외Codes = [음식배달운영기술이상Codes.기사알림확인필요];
        Assert.True((await source.ReadCurrentAsync(row.주문번호, default)).Single(x => x.ProblemCode == "DriverNotificationOutboxReviewRequired").Active);
        row.생명주기조화.예외Codes = [];
        Assert.False((await source.ReadCurrentAsync(row.주문번호, default)).Single(x => x.ProblemCode == "DriverNotificationOutboxReviewRequired").Active);
    }

    private static 음식주문운영추적응답 Row() => new()
    {
        주문번호 = "food-alert-source-fixture", 음식점명 = "private-phone private-address",
        최근변경시각Utc = DateTime.UtcNow
    };
    private static FoodOperationsAlertSource Source(SsalddelContext db, 음식주문운영추적응답 row)
        => new(db, new Trace(row), TimeProvider.System, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["VersionFeatureFlags:FoodDeliveryWorkflow"] = "true" }).Build());
    private sealed class Trace(음식주문운영추적응답 row) : I음식주문운영추적UseCase
    {
        public Task<음식주문운영추적응답?> 조회Async(string orderNo, CancellationToken cancellationToken = default)
            => Task.FromResult<음식주문운영추적응답?>(row);
    }
    private static SsalddelContext CreateContext()
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase($"food-alerts-{Guid.NewGuid():N}").Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
