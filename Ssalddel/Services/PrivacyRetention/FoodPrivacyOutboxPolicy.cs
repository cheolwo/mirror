using System.Text.Json;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Participants;

namespace Ssalddel.Services.PrivacyRetention;

internal static class 음식개인정보OutboxPolicy
{
    public const string PrivacyExpired = "PrivacyExpired";
    public static 음식주문응답 Minimized(음식주문응답 order)
    {
        var result = JsonSerializer.Deserialize<음식주문응답>(JsonSerializer.Serialize(order))!;
        result.수령인정보 = new 음식주문수령인정보Dto();
        result.수락메모 = null;
        result.상태이력 = [];
        return result;
    }
}
