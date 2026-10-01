using System.Reflection;
using Hongdal.Controllers.Common;
using Microsoft.AspNetCore.Authorization;

namespace Hongdal.Tests.Controllers;

public sealed class CommunityBoardControllerTests
{
    [Fact]
    public void 공개목록만익명접근을허용한다()
    {
        var list = typeof(커뮤니티게시판Controller).GetMethod(nameof(커뮤니티게시판Controller.List));
        var requests = typeof(커뮤니티게시판Controller).GetMethod(nameof(커뮤니티게시판Controller.ListRequests));

        Assert.NotNull(list?.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(
            "서버관리자전용",
            requests?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void 개설신청은로그인이필요하고_승인과반려는관리자전용이다()
    {
        var create = typeof(커뮤니티게시판Controller).GetMethod(nameof(커뮤니티게시판Controller.Create));
        var approve = typeof(커뮤니티게시판Controller).GetMethod(nameof(커뮤니티게시판Controller.Approve));
        var reject = typeof(커뮤니티게시판Controller).GetMethod(nameof(커뮤니티게시판Controller.Reject));

        Assert.NotNull(create?.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(create?.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("서버관리자전용", approve?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("서버관리자전용", reject?.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }
}
