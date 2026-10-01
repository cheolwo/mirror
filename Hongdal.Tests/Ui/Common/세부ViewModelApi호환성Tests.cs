using System.Reflection;
using System.Text.RegularExpressions;
using Hongdal.Controllers.Common;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Hongdal.Tests.Ui.Common;

public sealed partial class 세부ViewModelApi호환성Tests
{
    [Theory]
    [InlineData(BaguaBusinessCodes.Warehouse, typeof(WarehouseOperationsController))]
    [InlineData(BaguaBusinessCodes.Sales, typeof(SalesChannelsController))]
    [InlineData(BaguaBusinessCodes.Order, typeof(주문원장Controller))]
    public void 세부ViewModelApi카탈로그가실제Controller경로와호환된다(
        string businessCode,
        Type controllerType)
    {
        var definition = Bagua업무영역카탈로그.Find(businessCode);
        var controllerRoutes = ControllerRoutes(controllerType);

        foreach (var feature in definition.Api기능)
        {
            var expected = new ControllerRoute(
                feature.Method.Method,
                Normalize(feature.RelativePath));
            Assert.Contains(expected, controllerRoutes);
        }
    }

    [Fact]
    public void 창고판매주문세부ViewModel의Api기능이중복되지않는다()
    {
        var features = new[]
            {
                BaguaBusinessCodes.Warehouse,
                BaguaBusinessCodes.Sales,
                BaguaBusinessCodes.Order
            }
            .SelectMany(code => Bagua업무영역카탈로그.Find(code).Api기능)
            .ToArray();

        Assert.Equal(
            features.Length,
            features.Select(feature => $"{feature.ControllerKey}:{feature.Key}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Equal(44, features.Length);
    }

    [Fact]
    public void Api기능은_Cqrs와_다이얼로그정책을_Http메서드에맞게제공한다()
    {
        var sales = Bagua업무영역카탈로그.Find(BaguaBusinessCodes.Sales);
        var transport = Bagua업무영역카탈로그.Find(BaguaBusinessCodes.Transport);

        var query = sales.Api기능.Single(feature => feature.Key == "accounts");
        var create = sales.Api기능.Single(feature => feature.Key == "create-account");
        var update = transport.Api기능.Single(feature => feature.Key == "update-request");
        var delete = transport.Api기능.Single(feature => feature.Key == "delete-request");
        var multipart = transport.Api기능.Single(feature => feature.Key == "bulk-confirm");

        Assert.Equal(BaguaCqrs요청유형.조회, query.Cqrs요청유형);
        Assert.False(query.명령다이얼로그사용가능);
        Assert.Null(query.다이얼로그정책);

        Assert.Equal(업무조각유형.등록, create.업무유형);
        Assert.Equal("등록", create.다이얼로그정책!.확인버튼문구);
        Assert.Equal(업무조각유형.수정, update.업무유형);
        Assert.Equal("저장", update.다이얼로그정책!.확인버튼문구);
        Assert.Equal(업무조각유형.삭제, delete.업무유형);
        Assert.True(delete.다이얼로그정책!.파괴적명령);

        Assert.Equal(BaguaCqrs요청유형.명령, multipart.Cqrs요청유형);
        Assert.False(multipart.명령다이얼로그사용가능);
    }

    private static IReadOnlySet<ControllerRoute> ControllerRoutes(Type controllerType)
    {
        Assert.True(typeof(ControllerBase).IsAssignableFrom(controllerType));
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.HttpMethods.Select(httpMethod =>
                new ControllerRoute(httpMethod, Normalize(attribute.Template))))
            .ToHashSet();
    }

    private static string Normalize(string? route)
    {
        var normalized = (route ?? string.Empty).Trim('/');
        return RouteConstraintRegex().Replace(normalized, "{$1}");
    }

    private sealed record ControllerRoute(string Method, string RelativePath);

    [GeneratedRegex("\\{([^}:]+)(?::[^}]+)?\\}")]
    private static partial Regex RouteConstraintRegex();
}
