using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식점메뉴관리UseCaseTests
{
    [Fact]
    public async Task 같은음식점과메뉴내용의재등록은_기존메뉴를반환한다()
    {
        await using var db = CreateContext();
        db.음식점공개프로필.Add(new 음식점공개프로필 { Id = 17, 상호명 = "분식집" });
        await db.SaveChangesAsync();
        var useCase = new 음식점메뉴관리UseCase(db);
        var request = new 음식점메뉴등록요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            메뉴명 = "김밥",
            설명 = "기본 김밥",
            판매가 = 4000,
            공개여부 = true
        };

        var first = await useCase.등록Async(17, request, CancellationToken.None);
        request.클라이언트요청Id = Guid.NewGuid();
        var retried = await useCase.등록Async(17, request, CancellationToken.None);

        Assert.True(first.새로생성됨);
        Assert.False(retried.새로생성됨);
        Assert.Equal(first.Id, retried.Id);
        Assert.Equal(1, await db.음식점메뉴.CountAsync());
    }

    [Fact]
    public async Task 수정은_음식점범위와Revision을검사한다()
    {
        await using var db = CreateContext();
        db.음식점공개프로필.Add(new 음식점공개프로필 { Id = 17, 상호명 = "분식집" });
        await db.SaveChangesAsync();
        var useCase = new 음식점메뉴관리UseCase(db);
        var created = await useCase.등록Async(17, new 음식점메뉴등록요청
        {
            클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "김밥", 판매가 = 4000, 공개여부 = true
        }, CancellationToken.None);

        Assert.Null(await useCase.수정Async(18, created.Id, new 음식점메뉴수정요청
        {
            예상Revision = created.Revision, 메뉴명 = "김밥", 판매가 = 4500
        }, CancellationToken.None));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => useCase.수정Async(17, created.Id,
            new 음식점메뉴수정요청 { 예상Revision = created.Revision - 1, 메뉴명 = "김밥", 판매가 = 4500 },
            CancellationToken.None));
        var updated = await useCase.수정Async(17, created.Id, new 음식점메뉴수정요청
        {
            예상Revision = created.Revision, 메뉴명 = "김밥", 판매가 = 4500, 품절여부 = true
        }, CancellationToken.None);

        Assert.Equal(4500, updated!.판매가);
        Assert.True(updated.품절여부);
        Assert.NotEqual(created.Revision, updated.Revision);
    }

    public static IEnumerable<object[]> 잘못된입력()
    {
        yield return new object[] { " ", "", 0m, "" };
        yield return new object[] { new string('가', 201), "", 0m, "" };
        yield return new object[] { "메뉴", new string('가', 1001), 0m, "" };
        yield return new object[] { "메뉴", "", -1m, "" };
        foreach (var url in new[] { "http://example.test/a.jpg", "/a.jpg", "file:///a.jpg", "javascript:alert(1)", "https://", "https://example.test/" + new string('a', 981) })
            yield return new object[] { "메뉴", "", 0m, url };
    }

    [Theory]
    [MemberData(nameof(잘못된입력))]
    public async Task 잘못된등록과수정은_저장상태를바꾸지않는다(string name, string description, decimal price, string url)
    {
        await using var db = CreateContext();
        db.음식점공개프로필.Add(new 음식점공개프로필 { Id = 17, 상호명 = "합성 가게" });
        await db.SaveChangesAsync();
        var useCase = new 음식점메뉴관리UseCase(db);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.등록Async(17,
            new 음식점메뉴등록요청 { 클라이언트요청Id = Guid.NewGuid(), 메뉴명 = name, 설명 = description, 판매가 = price, 대표이미지Url = url }, CancellationToken.None));
        Assert.Empty(await useCase.목록Async(17, CancellationToken.None));
        var saved = await useCase.등록Async(17, new 음식점메뉴등록요청 { 클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "원래 메뉴", 판매가 = 4000 }, CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.수정Async(17, saved.Id,
            new 음식점메뉴수정요청 { 예상Revision = saved.Revision, 메뉴명 = name, 설명 = description, 판매가 = price, 대표이미지Url = url }, CancellationToken.None));
        var reread = Assert.Single(await useCase.목록Async(17, CancellationToken.None));
        Assert.Equal(saved.메뉴명, reread.메뉴명);
        Assert.Equal(saved.판매가, reread.판매가);
        Assert.Equal(saved.Revision, reread.Revision);
        Assert.Equal(EntityState.Unchanged, db.Entry(await db.음식점메뉴.SingleAsync()).State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData(" HTTPS://example.test/menu.jpg ")]
    public async Task 제한경계와선택사진은_등록재시도와수정후재조회된다(string? url)
    {
        await using var db = CreateContext();
        db.음식점공개프로필.Add(new 음식점공개프로필 { Id = 17, 상호명 = "합성 가게" });
        await db.SaveChangesAsync();
        var useCase = new 음식점메뉴관리UseCase(db);
        var request = new 음식점메뉴등록요청 { 클라이언트요청Id = Guid.NewGuid(), 메뉴명 = " " + new string('가', 200) + " ", 설명 = " " + new string('나', 1000) + " ", 판매가 = 0, 대표이미지Url = url };
        var saved = await useCase.등록Async(17, request, CancellationToken.None);
        var retry = await useCase.등록Async(17, request, CancellationToken.None);
        Assert.Equal(saved.Id, retry.Id);
        Assert.False(retry.새로생성됨);
        Assert.Equal(200, saved.메뉴명.Length);
        Assert.Equal(1000, saved.설명.Length);
        Assert.Empty(await useCase.목록Async(18, CancellationToken.None));
        const string prefix = "https://example.test/";
        var longUrl = prefix + new string('a', 1000 - prefix.Length);
        Assert.Equal(1000, longUrl.Length);
        await useCase.수정Async(17, saved.Id, new 음식점메뉴수정요청 { 예상Revision = saved.Revision, 메뉴명 = saved.메뉴명, 설명 = saved.설명, 판매가 = 4500, 대표이미지Url = longUrl, 품절여부 = true }, CancellationToken.None);
        var reread = Assert.Single(await useCase.목록Async(17, CancellationToken.None));
        Assert.Equal(longUrl, reread.대표이미지Url);
        Assert.Equal(4500, reread.판매가);
        Assert.True(reread.품절여부);
    }

    private static SsalddelContext CreateContext() => new(
        new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
        new PassThroughEncryptionService());

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
