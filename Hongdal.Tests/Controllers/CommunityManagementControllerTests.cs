using System.Reflection;
using Hongdal.Controllers.Admin.Master06;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hongdal.Tests.Controllers;

public sealed class CommunityManagementControllerTests
{
    [Fact]
    public void Community_management_api_is_server_admin_only()
    {
        var authorize = typeof(커뮤니티운영Controller).GetCustomAttribute<AuthorizeAttribute>();
        var route = typeof(커뮤니티운영Controller).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("서버관리자전용", authorize.Policy);
        Assert.Equal("api/v1/admin/community-management", route?.Template);
    }
}
