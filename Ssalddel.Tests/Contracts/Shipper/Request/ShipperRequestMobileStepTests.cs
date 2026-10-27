using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Tests.Contracts.Shipper.Request;

public sealed class ShipperRequestMobileStepTests
{
    [Theory]
    [InlineData(ShipperRequestMobileStep.Cargo, "/shipper/request/cargo", ShipperRequestAuthoringStep.Cargo)]
    [InlineData(ShipperRequestMobileStep.Package, "/shipper/request/package", ShipperRequestAuthoringStep.Cargo)]
    [InlineData(ShipperRequestMobileStep.Pickup, "/shipper/request/pickup", ShipperRequestAuthoringStep.Transport)]
    [InlineData(ShipperRequestMobileStep.Dropoff, "/shipper/request/dropoff", ShipperRequestAuthoringStep.Transport)]
    [InlineData(ShipperRequestMobileStep.Schedule, "/shipper/request/schedule", ShipperRequestAuthoringStep.Transport)]
    [InlineData(ShipperRequestMobileStep.Vehicle, "/shipper/request/vehicle", ShipperRequestAuthoringStep.Procedure)]
    [InlineData(ShipperRequestMobileStep.Payment, "/shipper/request/procedure", ShipperRequestAuthoringStep.Procedure)]
    [InlineData(ShipperRequestMobileStep.Review, "/shipper/request/review", ShipperRequestAuthoringStep.Review)]
    public void 모바일단계는승인된경로와기존상위단계를공유한다(ShipperRequestMobileStep step, string path, ShipperRequestAuthoringStep major)
    {
        var definition = ShipperRequestMobileStepCatalog.Get(step);
        Assert.Equal(path, definition.Path); Assert.Equal(path, ShipperRequestPageRoutes.PathFor(step));
        Assert.Equal(major, definition.MajorStep); Assert.False(string.IsNullOrWhiteSpace(definition.Title));
        Assert.False(string.IsNullOrWhiteSpace(definition.Description));
    }

    [Fact]
    public void 모바일순서는명시목록으로이동하고기존네단계의숫자와경로는바뀌지않는다()
    {
        ShipperRequestMobileStep[] expected = [ShipperRequestMobileStep.Cargo, ShipperRequestMobileStep.Package,
            ShipperRequestMobileStep.Pickup, ShipperRequestMobileStep.Dropoff, ShipperRequestMobileStep.Schedule,
            ShipperRequestMobileStep.Vehicle, ShipperRequestMobileStep.Payment, ShipperRequestMobileStep.Review];
        Assert.Equal(expected, ShipperRequestMobileStepCatalog.Steps.Select(x => x.Step));
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(index + 1, ShipperRequestMobileStepCatalog.Get(expected[index]).Number);
            Assert.Equal(index == 0 ? (ShipperRequestMobileStep?)null : expected[index - 1], ShipperRequestMobileStepCatalog.Previous(expected[index]));
            Assert.Equal(index == expected.Length - 1 ? (ShipperRequestMobileStep?)null : expected[index + 1], ShipperRequestMobileStepCatalog.Next(expected[index]));
        }
        Assert.Equal([0, 1, 2, 3], Enum.GetValues<ShipperRequestAuthoringStep>().Select(x => (int)x));
        Assert.Equal("/shipper/request/transport", ShipperRequestPageRoutes.PathFor(ShipperRequestAuthoringStep.Transport));
        Assert.Equal("/shipper/request/procedure", ShipperRequestPageRoutes.PathFor(ShipperRequestAuthoringStep.Procedure));
        Assert.Equal("/shipper/request/summary", ShipperRequestPageRoutes.LegacySummary);
    }

    [Fact]
    public void 모바일모든단계와되돌아가기에서출발문맥은보존하고주소전화인증값은Query로옮기지않는다()
    {
        var context = ShipperRequestNavigationContext.Parse("/shipper/request?sourcePostId=73&source=diagram-node"
            + "&sourceMarkerId=marker-73&ledgerTemplateKey=cargo-transport&nodeTitle=운송&nodeKind=delivery&country=KR"
            + "&from=%2Fcommunity%2Fposts%2F73&pickupAddress=private-place&phone=private-phone&token=private-token");
        foreach (var step in ShipperRequestMobileStepCatalog.Steps.Concat(ShipperRequestMobileStepCatalog.Steps.Reverse()))
        {
            var path = context.PathFor(step.Step);
            Assert.StartsWith(step.Path + "?", path); Assert.Equal(context, ShipperRequestNavigationContext.Parse(path));
            Assert.DoesNotContain("private-place", path); Assert.DoesNotContain("private-phone", path); Assert.DoesNotContain("private-token", path);
        }
        var externalReturn = ShipperRequestNavigationContext.Parse("/shipper/request?from=https%3A%2F%2Foutside.example%2Fprivate");
        Assert.Null(externalReturn.ReturnPath);
        Assert.Equal(ShipperRequestPageRoutes.Package, externalReturn.PathFor(ShipperRequestMobileStep.Package));
    }

    [Fact]
    public void 단계목록을외부에서변경하거나알수없는단계로이동할수없다()
    {
        var list = Assert.IsAssignableFrom<IList<ShipperRequestMobileStepDefinition>>(ShipperRequestMobileStepCatalog.Steps);
        Assert.Throws<NotSupportedException>(() => list[0] = list[1]);
        var unknown = (ShipperRequestMobileStep)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipperRequestMobileStepCatalog.Get(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipperRequestMobileStepCatalog.Previous(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipperRequestPageRoutes.PathFor(unknown));
    }
}
