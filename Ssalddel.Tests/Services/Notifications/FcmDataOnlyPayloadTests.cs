using System.Text.Json;
using 살뜰.Services.Notifications;

namespace Ssalddel.Tests.Services.Notifications;

public sealed class FcmDataOnlyPayloadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DataOnlyOmitsAllSystemNotificationContentInBothFcmProtocols(bool legacy)
    {
        var data = new Dictionary<string, string> { ["type"] = "FoodDeliveryRecommendation", ["offerId"] = "food-offer" };
        var message = new FcmPushMessage("fixture-token", "must-not-escape", "private-address", data,
            "https://private.example.test/image", HighPriority: true, DataOnly: true);
        var json = legacy ? FcmPushPayloadSerializer.SerializeLegacy(message) : FcmPushPayloadSerializer.SerializeHttpV1(message);
        using var document = JsonDocument.Parse(json);
        var root = legacy ? document.RootElement : document.RootElement.GetProperty("message");
        Assert.False(root.TryGetProperty("notification", out _));
        Assert.False(root.TryGetProperty("apns", out _));
        Assert.Equal("FoodDeliveryRecommendation", root.GetProperty("data").GetProperty("type").GetString());
        if (legacy) Assert.Equal("high", root.GetProperty("priority").GetString());
        else
        {
            var android = root.GetProperty("android");
            Assert.Equal("HIGH", android.GetProperty("priority").GetString());
            Assert.False(android.TryGetProperty("notification", out _));
        }
        Assert.DoesNotContain("must-not-escape", json);
        Assert.DoesNotContain("private-address", json);
        Assert.DoesNotContain("private.example.test", json);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingCargoNotificationTitleBodyImageAndDataArePreserved(bool legacy)
    {
        var message = new FcmPushMessage("cargo-token", "cargo-title", "cargo-body",
            new Dictionary<string, string> { ["type"] = "DriverDispatchRecommendation" }, "https://example.test/image");
        using var document = JsonDocument.Parse(legacy ? FcmPushPayloadSerializer.SerializeLegacy(message) : FcmPushPayloadSerializer.SerializeHttpV1(message));
        var root = legacy ? document.RootElement : document.RootElement.GetProperty("message");
        var notification = root.GetProperty("notification");
        Assert.Equal("cargo-title", notification.GetProperty("title").GetString());
        Assert.Equal("cargo-body", notification.GetProperty("body").GetString());
        Assert.Equal("DriverDispatchRecommendation", root.GetProperty("data").GetProperty("type").GetString());
        Assert.Equal("https://example.test/image", legacy ? notification.GetProperty("image").GetString()
            : root.GetProperty("android").GetProperty("notification").GetProperty("image").GetString());
    }
}
