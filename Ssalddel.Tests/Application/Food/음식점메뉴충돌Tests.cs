using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식점메뉴충돌Tests
{
    [Theory]
    [InlineData("김밥")]
    [InlineData(" 김밥 ")]
    public async Task 다른메뉴의이름으로수정하면_저장전거절하고두메뉴를보존한다(string name)
    {
        await using var database = await 메뉴TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var useCase = new 음식점메뉴관리UseCase(db);
        var before = await useCase.목록Async(17, CancellationToken.None);
        var target = Assert.Single(before, item => item.메뉴명 == "떡볶이");

        await Assert.ThrowsAsync<음식점메뉴명충돌Exception>(() => useCase.수정Async(17, target.Id,
            수정요청(target, name), CancellationToken.None));

        db.ChangeTracker.DetectChanges();
        Assert.All(db.ChangeTracker.Entries<음식점메뉴>(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        await 독립조회불변확인Async(database, 17, before);
    }

    [Fact]
    public async Task 자기이름을유지한수정은_저장하고다른Context에서조회된다()
    {
        await using var database = await 메뉴TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var useCase = new 음식점메뉴관리UseCase(db);
        var before = await useCase.목록Async(17, CancellationToken.None);
        var target = Assert.Single(before, item => item.메뉴명 == "김밥");

        var updated = await useCase.수정Async(17, target.Id, 수정요청(target, " 김밥 "), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(target.Id, updated.Id);
        Assert.Equal("김밥", updated.메뉴명);
        Assert.Equal(5500m, updated.판매가);
        Assert.NotEqual(target.Revision, updated.Revision);
        await using var reader = database.CreateContext();
        var reread = await new 음식점메뉴관리UseCase(reader).목록Async(17, CancellationToken.None);
        Assert.Equal(2, reread.Count);
        메뉴동일확인(updated, Assert.Single(reread, item => item.Id == target.Id));
        메뉴동일확인(Assert.Single(before, item => item.Id != target.Id), Assert.Single(reread, item => item.Id != target.Id));
    }

    [Fact]
    public async Task 다른음식점에서쓰는메뉴명은_현재음식점의수정을막지않는다()
    {
        await using var database = await 메뉴TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var useCase = new 음식점메뉴관리UseCase(db);
        var otherStore = await useCase.목록Async(18, CancellationToken.None);
        var target = Assert.Single(await useCase.목록Async(17, CancellationToken.None), item => item.메뉴명 == "떡볶이");

        var updated = await useCase.수정Async(17, target.Id,
            수정요청(target, Assert.Single(otherStore).메뉴명), CancellationToken.None);

        Assert.NotNull(updated);
        await using var reader = database.CreateContext();
        var reread = await new 음식점메뉴관리UseCase(reader).목록Async(17, CancellationToken.None);
        메뉴동일확인(updated, Assert.Single(reread, item => item.Id == target.Id));
        await 독립조회불변확인Async(database, 18, otherStore);
    }

    [Fact]
    public async Task 오래된Revision과이름충돌이겹치면_Revision충돌을먼저반환한다()
    {
        await using var database = await 메뉴TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var useCase = new 음식점메뉴관리UseCase(db);
        var before = await useCase.목록Async(17, CancellationToken.None);
        var target = Assert.Single(before, item => item.메뉴명 == "떡볶이");
        var request = 수정요청(target, "김밥");
        request.예상Revision = target.Revision - 1;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => useCase.수정Async(17, target.Id,
            request, CancellationToken.None));

        await 독립조회불변확인Async(database, 17, before);
    }

    [Fact]
    public async Task Controller직접호출의메뉴명이름충돌은_409이고저장상태를보존한다()
    {
        await using var database = await 메뉴TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var useCase = new 음식점메뉴관리UseCase(db);
        var before = await useCase.목록Async(17, CancellationToken.None);
        var target = Assert.Single(before, item => item.메뉴명 == "떡볶이");
        var controller = 음식점Controller(useCase, 17);

        var response = await controller.수정(target.Id, 수정요청(target, "김밥"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(response.Result).StatusCode);
        await 독립조회불변확인Async(database, 17, before);
    }

    [Fact]
    public async Task Controller직접호출에서다른음식점메뉴수정은_404이고저장상태를보존한다()
    {
        await using var database = await 메뉴TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var useCase = new 음식점메뉴관리UseCase(db);
        var before = await useCase.목록Async(18, CancellationToken.None);
        var target = Assert.Single(before);
        var controller = 음식점Controller(useCase, 17);

        var response = await controller.수정(target.Id, 수정요청(target, "김밥"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
        await 독립조회불변확인Async(database, 18, before);
    }

    [Fact]
    public async Task 같은내용의등록재시도는기존메뉴를반환하고_다른내용의중복등록은거절한다()
    {
        await using var database = await 메뉴TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var useCase = new 음식점메뉴관리UseCase(db);
        var request = new 음식점메뉴등록요청
        {
            클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "새 메뉴", 설명 = "합성 메뉴", 판매가 = 3500m
        };
        var created = await useCase.등록Async(17, request, CancellationToken.None);
        var retry = await useCase.등록Async(17, request, CancellationToken.None);

        Assert.True(created.새로생성됨);
        Assert.False(retry.새로생성됨);
        메뉴동일확인(created, retry);
        var before = await useCase.목록Async(17, CancellationToken.None);
        request.클라이언트요청Id = Guid.NewGuid();
        request.판매가 = 4500m;

        await Assert.ThrowsAsync<음식점메뉴명충돌Exception>(() => useCase.등록Async(17, request, CancellationToken.None));

        await 독립조회불변확인Async(database, 17, before);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Controller는일반처리오류를_메뉴명충돌409로바꾸지않는다(bool register)
    {
        var failure = new InvalidOperationException("합성 내부 처리 오류");
        var controller = 음식점Controller(new 모의실패UseCase(failure), 17);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (register)
                await controller.등록(new 음식점메뉴등록요청
                {
                    클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "합성 메뉴", 판매가 = 4000m
                }, CancellationToken.None);
            else
                await controller.수정(101, new 음식점메뉴수정요청
                {
                    예상Revision = 1, 메뉴명 = "합성 메뉴", 판매가 = 4000m
                }, CancellationToken.None);
        });

        Assert.Same(failure, thrown);
    }

    private static 음식점메뉴수정요청 수정요청(음식점메뉴관리응답 target, string name) => new()
    {
        예상Revision = target.Revision,
        메뉴명 = name,
        설명 = "수정할 소개",
        판매가 = 5500m,
        대표이미지Url = "https://example.test/updated.jpg",
        공개여부 = false,
        품절여부 = true,
        표시순서 = 9
    };

    // 인증 미들웨어를 실행하지 않는 실제 Controller 직접 호출 시험이다.
    private static 음식점메뉴관리Controller 음식점Controller(I음식점메뉴관리UseCase useCase, long restaurantId) => new(useCase)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(음식점접근ClaimTypes.음식점Id, restaurantId.ToString(System.Globalization.CultureInfo.InvariantCulture))],
                    "SyntheticTest"))
            }
        }
    };

    private static async Task 독립조회불변확인Async(메뉴TestDatabase database, long restaurantId, IReadOnlyList<음식점메뉴관리응답> before)
    {
        await using var reader = database.CreateContext();
        var reread = await new 음식점메뉴관리UseCase(reader).목록Async(restaurantId, CancellationToken.None);
        Assert.Equal(before.Count, reread.Count);
        foreach (var expected in before)
            메뉴동일확인(expected, Assert.Single(reread, item => item.Id == expected.Id));
    }

    private static void 메뉴동일확인(음식점메뉴관리응답 expected, 음식점메뉴관리응답 actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.음식점Id, actual.음식점Id);
        Assert.Equal(expected.메뉴명, actual.메뉴명);
        Assert.Equal(expected.설명, actual.설명);
        Assert.Equal(expected.판매가, actual.판매가);
        Assert.Equal(expected.대표이미지Url, actual.대표이미지Url);
        Assert.Equal(expected.공개여부, actual.공개여부);
        Assert.Equal(expected.품절여부, actual.품절여부);
        Assert.Equal(expected.표시순서, actual.표시순서);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private sealed class 모의실패UseCase(InvalidOperationException failure) : I음식점메뉴관리UseCase
    {
        public Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(long 음식점Id, CancellationToken cancellationToken)
            => Task.FromException<IReadOnlyList<음식점메뉴관리응답>>(failure);

        public Task<음식점메뉴관리응답> 등록Async(long 음식점Id, 음식점메뉴등록요청 request, CancellationToken cancellationToken)
            => Task.FromException<음식점메뉴관리응답>(failure);

        public Task<음식점메뉴관리응답?> 수정Async(long 음식점Id, long 메뉴Id, 음식점메뉴수정요청 request, CancellationToken cancellationToken)
            => Task.FromException<음식점메뉴관리응답?>(failure);
    }

    private sealed class 메뉴TestDatabase(SqliteConnection connection) : IAsyncDisposable
    {
        public SsalddelContext CreateContext() => new(
            new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options,
            new PassThroughEncryptionService());

        public static async Task<메뉴TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var database = new 메뉴TestDatabase(connection);
            try
            {
                await using var db = database.CreateContext();
                await db.Database.EnsureCreatedAsync();
                db.음식점공개프로필.AddRange(
                    new 음식점공개프로필 { Id = 17, 상호명 = "합성 가게 A" },
                    new 음식점공개프로필 { Id = 18, 상호명 = "합성 가게 B" });
                var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                foreach (var (id, restaurantId, name) in new[] { (101L, 17L, "김밥"), (102L, 17L, "떡볶이"), (103L, 18L, "다른 가게 메뉴") })
                {
                    db.음식점메뉴.Add(new 음식점메뉴
                    {
                        Id = id, 음식점공개프로필Id = restaurantId, 메뉴명 = name,
                        설명 = "원래 소개", 판매가 = 4000m, 대표이미지Url = "https://example.test/original.jpg",
                        공개여부 = true, 품절여부 = false, 표시순서 = 1,
                        CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
                    });
                }
                await db.SaveChangesAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
