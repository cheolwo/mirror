namespace Ssalddel.Contracts.Common.Fulfillment;

/// <summary>연동 계약을 확인하지 않은 개발 가정입니다. 실제 업체의 API·요금·배송 역량을 나타내지 않습니다.</summary>
public static class AssumedFulfillmentProviderCatalog
{
    public const string KakaoTQuick = "assumed-kakao-t-quick";
    public const string Baemin = "assumed-baemin";
    public const string CoupangEats = "assumed-coupang-eats";

    public static IReadOnlyList<AutoFulfillmentProviderDescriptor> Providers { get; } =
        [new(KakaoTQuick, "카카오 T 퀵 문서 기반 · 모의", true),
         new(Baemin, "배민 연동 가정 · 모의", true),
         new(CoupangEats, "쿠팡이츠 연동 가정 · 모의", true)];

    public static bool IsAssumed(string providerId) => providerId is KakaoTQuick or Baemin or CoupangEats;
}
