using System.Text.Json;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Tests.Contracts;

public sealed class WorkflowFailureContextTests
{
    [Theory]
    [InlineData("NotApplicable")]
    [InlineData("NotApplied")]
    [InlineData("Applied")]
    [InlineData("Unknown")]
    public void 알려진결과는OS의실제단계와JSON왕복된다(string outcome)
    {
        var value = new 업무실패맥락Dto(OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.CargoPickup, outcome);
        Assert.True(value.유효한가());
        var json = JsonSerializer.Serialize(value);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, document.RootElement.GetProperty("operatingSystemId").GetString());
        Assert.Equal("cargo.pickup", document.RootElement.GetProperty("stageId").GetString());
        Assert.Equal(outcome, document.RootElement.GetProperty("outcomeCode").GetString());
        Assert.Equal(value, JsonSerializer.Deserialize<업무실패맥락Dto>(json));
    }

    [Theory]
    [InlineData("DomesticCargoTransportOS", "food.cooking", "NotApplied")]
    [InlineData("unknown-os", "cargo.pickup", "NotApplied")]
    [InlineData("DomesticCargoTransportOS", "cargo.pickup", "Succeeded")]
    [InlineData(null, null, null)]
    public void 잘못된OS단계조합이나결과를채워넣지않는다(string? os, string? stage, string? outcome)
        => Assert.False(new 업무실패맥락Dto(os!, stage!, outcome!).유효한가());
}
