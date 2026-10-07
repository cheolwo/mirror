using Microsoft.AspNetCore.Http;
using 살뜰.Services.Audit;

namespace Ssalddel.Tests.Middleware;

public sealed class 사용자행위로그보호Tests
{
    [Fact]
    public void 요청원문과연락처주소토큰은_저장대장에서제외한다()
    {
        const string json = """{"method":"GET","statusCode":200,"query":{"page":"2","phone":"010-1234-5678","address":"서울 고객주소","token":"private-token"},"url":"https://host.test/?phone=01012345678","email":"private@example.test","orderId":"FOOD-TEST-1","customer":{"name":"실명"}}""";
        var safe = 사용자행위로그보호Policy.SafeMetadata(json);
        Assert.Contains("FOOD-TEST-1", safe);
        Assert.Contains("200", safe);
        Assert.Contains("page", safe);
        Assert.DoesNotContain("010", safe);
        Assert.DoesNotContain("고객주소", safe);
        Assert.DoesNotContain("private", safe);
        Assert.DoesNotContain("customer", safe);
        Assert.DoesNotContain("url", safe);
    }

    [Fact]
    public void 불완전한Json과예외메시지에서도_원문이남지않는다()
    {
        Assert.Equal("{}", 사용자행위로그보호Policy.SafeMetadata("private-token broken"));
        Assert.DoesNotContain("010", 사용자행위로그보호Policy.SafeError("010-1234-5678 서울 고객주소"));
    }

    [Fact]
    public void Query는_단일정수페이징값만허용한다()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?page=2&pageSize=20&take=private-token&phone=01012345678&skip=1&skip=2");
        var query = 사용자행위로그보호Policy.SafeQuery(context.Request.Query);
        Assert.Equal(2, query.Count);
        Assert.Equal("2", query["page"]);
        Assert.Equal("20", query["pageSize"]);
    }
}
