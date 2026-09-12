using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Ssalddel.Controllers.Common;

namespace Ssalddel.Tests.ApiMetadata;

public sealed class 생활권물류거점Controller권한Tests
{
    [Theory]
    [InlineData(nameof(생활권물류거점Controller.용량예약))]
    [InlineData(nameof(생활권물류거점Controller.인계완료))]
    public void 업무상태변경Endpoint는_서버관리자정책을_요구한다(string methodName)
    {
        var method = typeof(생활권물류거점Controller).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        var attribute = Assert.Single(Assert.IsAssignableFrom<MethodInfo>(method)
            .GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("서버관리자전용", attribute.Policy);
    }
}
