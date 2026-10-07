namespace Ssalddel.Contracts.Common.Community;

public static class NeighborhoodTransferMethods
{
    public const string RecipientPickup = "recipient-pickup";
    public const string ProviderDelivery = "provider-delivery";
    public const string DriverDelivery = "driver-delivery";
    public static bool IsKnown(string? value) => value is RecipientPickup or ProviderDelivery or DriverDelivery;
    public static string Label(string? value) => value switch { RecipientPickup => "직접 수령", ProviderDelivery => "직접 전달", DriverDelivery => "기사 배송", _ => "기존 조건 확인" };
}

public static class NeighborhoodDispatchModes
{
    public const string Automatic = "automatic";
    public const string PublicCall = "public-call";
    public const string Hybrid = "hybrid";
    public static bool IsKnown(string? value) => value is Automatic or PublicCall or Hybrid;
    public static string Label(string? value) => value switch { Automatic => "자동 추천", PublicCall => "공개 콜", Hybrid => "자동 추천과 공개 콜", _ => "기존 배차 방식" };
}

public static class NeighborhoodGoodsHandoverNotice
{
    public const string Version = "neighborhood-goods-handover-20261006-v1";
    public const string Text = "현재 전달 조건에 양측이 동의하면 협업 상대방에게 인계 장소와 방법을 제공합니다. 공개 지도에는 동네만 표시합니다. 기사 정보 제공과 완료 기록 공개는 별도 동의입니다.";
}

public sealed class NeighborhoodDispatchChoiceRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedDispatchRevision { get; set; }
    public string DispatchMode { get; set; } = string.Empty;
}
