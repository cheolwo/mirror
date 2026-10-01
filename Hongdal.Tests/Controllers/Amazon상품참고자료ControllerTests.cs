using System.Reflection;
using Hongdal.ApiMetadata;
using Hongdal.Controllers.Admin.Content07;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Tests.Controllers;

public sealed class Amazon상품참고자료ControllerTests
{
    [Fact]
    public void Amazon상품참고자료API는_0점0관리자검수경로다()
    {
        var controller = typeof(Amazon상품참고자료Controller);
        var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();
        var route = controller.GetCustomAttribute<RouteAttribute>();
        var version = controller.GetCustomAttribute<HongdalApiVersionAttribute>();
        var preview = controller.GetMethod(nameof(Amazon상품참고자료Controller.미리보기));

        Assert.Equal("서버관리자전용", authorize?.Policy);
        Assert.Equal("api/v1/admin/content/product-research/amazon", route?.Template);
        Assert.Equal(HongdalProductVersion.V0_0, version?.Version);
        Assert.Equal("preview", preview?.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }
}
