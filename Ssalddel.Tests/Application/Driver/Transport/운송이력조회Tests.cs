using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 운송이력조회Tests
{
    [Fact]
    public async Task 저장후재조회한_이력은_본인화물의완료와진행을포함하고_타기사와음식을제외한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(
            new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options,
            new Encryption());
        await db.Database.EnsureCreatedAsync();
        db.운송원장.AddRange(
            Transport("completed", "me", "인수완료", null),
            Transport("active", "me", "배차확정", 0m),
            Transport("other-driver", "other", "인수완료", 10000m),
            Transport("food", "me", "인수완료", 5000m, 상태값.배차업무유형.음식배달));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new 운송목록조회QueryHandler(db).Handle(
            new 운송목록조회Query("me"), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Null(result.Single(x => x.운송번호 == "completed").운임);
        Assert.Equal("인수완료", result.Single(x => x.운송번호 == "completed").상태);
        Assert.Equal(0m, result.Single(x => x.운송번호 == "active").운임);
    }

    private static 운송원장 Transport(string id, string driver, string status, decimal? fare,
        int businessType = 상태값.배차업무유형.용달운송)
        => new() { 의뢰Id = id, 운송번호 = id, 기사_운송자 = driver, 상태 = status,
            운임 = fare, 배차업무유형 = businessType, UpdatedAt = DateTime.UtcNow };

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
