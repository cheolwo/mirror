using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Contracts.Common.Privacy;

public sealed class NeighborhoodDeliveryProtectionTests
{
    [Fact]
    public void 배송의뢰의_중첩주소와연락처도_전송보호대상으로판단된다()
    {
        // 보호 판정은 요청의 최상위 속성만 읽으므로, 중첩 DTO만 표시하면 누락될 수 있다.
        var plan = SsalddelIsmsPClientEncryptionService.BuildProtectionPlan<NeighborhoodDeliveryRequest>();

        Assert.True(SsalddelIsmsPClientEncryptionService.RequiresEncryptedTransport<NeighborhoodDeliveryRequest>());
        Assert.Contains(plan.Rules, x => x.FieldKey == PersonalDataFieldKey.DetailedAddress);
        Assert.Contains(plan.Rules, x => x.FieldKey == PersonalDataFieldKey.PhoneNumber);
        Assert.False(plan.HasUnknownFields);
    }

    [Theory]
    [InlineData(nameof(NeighborhoodDeliveryRequest.Pickup))]
    [InlineData(nameof(NeighborhoodDeliveryRequest.Dropoff))]
    public void 픽업과전달_양쪽의_비공개정보를_보호대장에포함한다(string property)
    {
        var members = IsmsPProtectedDataAttributeReader.Read<NeighborhoodDeliveryRequest>()
            .Where(x => x.PropertyName == property).ToArray();

        Assert.Contains(members, x => x.FieldKey == PersonalDataFieldKey.DetailedAddress && x.IsPersonalData);
        Assert.Contains(members, x => x.FieldKey == PersonalDataFieldKey.PhoneNumber && x.IsPersonalData);
    }
}
