using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public static class 커뮤니티원장노드행동Policy
{
    private static readonly string[] 상차지도착허용상태 =
    [
        "배차대기",
        "매칭중",
        "배차확정",
        "이동중"
    ];

    public static IReadOnlyList<PlatformCommunityLedgerNodeActionResponse> Build(
        커뮤니티원장Dto 원장,
        string? 사용자UserId,
        bool 상세조회가능,
        bool 기능활성화)
    {
        ArgumentNullException.ThrowIfNull(원장);

        if (!상세조회가능
            || !기능활성화
            || !string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.CargoTransport, StringComparison.OrdinalIgnoreCase)
            || !기사참여자인가(원장, 사용자UserId)
            || !Try운송실행Id(원장, out var 운송실행Id))
        {
            return [];
        }

        var 상차블록 = 원장.블록목록.FirstOrDefault(block =>
            string.Equals(block.BlockId, "pickup", StringComparison.OrdinalIgnoreCase)
            || block.Title.Contains("상차", StringComparison.OrdinalIgnoreCase));
        if (상차블록 is null)
        {
            return [];
        }

        var 현재상태 = Resolve현재상태(원장, 상차블록);
        var 도착가능 = 상차지도착가능한가(현재상태);
        var 완료가능 = string.Equals(현재상태, "상차지도착", StringComparison.OrdinalIgnoreCase)
                     || 현재상태.Contains("상차지 도착", StringComparison.OrdinalIgnoreCase);

        return
        [
            new PlatformCommunityLedgerNodeActionResponse
            {
                행동Code = CommunityLedgerNodeActionCodes.TransportArrivePickup,
                블록Id = 상차블록.BlockId,
                표시명 = "상차지 도착 확인",
                설명 = "기사의 상차지 도착을 기록하고 원장 진행 상태를 갱신합니다.",
                ApiEndpointKey = "기사운송진행Controller.상차지도착",
                실행대상Id = 운송실행Id.ToString(),
                현재상태 = 현재상태,
                실행가능여부 = 도착가능,
                비활성사유 = 도착가능 ? null : Build도착비활성사유(현재상태)
            },
            new PlatformCommunityLedgerNodeActionResponse
            {
                행동Code = CommunityLedgerNodeActionCodes.TransportCompletePickup,
                블록Id = 상차블록.BlockId,
                표시명 = "상차 완료",
                설명 = "상차·인수 증빙 사진을 저장하고 운송 시작 단계로 넘깁니다.",
                ApiEndpointKey = "기사운송진행Controller.상차완료",
                실행대상Id = 운송실행Id.ToString(),
                현재상태 = 현재상태,
                실행가능여부 = 완료가능,
                사진필수여부 = true,
                비활성사유 = 완료가능 ? null : Build완료비활성사유(현재상태)
            }
        ];
    }

    private static bool 기사참여자인가(커뮤니티원장Dto 원장, string? 사용자UserId)
        => !string.IsNullOrWhiteSpace(사용자UserId)
           && 원장.참여자목록.Any(participant =>
               string.Equals(participant.UserId, 사용자UserId.Trim(), StringComparison.OrdinalIgnoreCase)
               && (participant.RoleLabel.Contains("기사", StringComparison.OrdinalIgnoreCase)
                   || participant.RoleLabel.Contains("운반", StringComparison.OrdinalIgnoreCase)));

    private static bool Try운송실행Id(커뮤니티원장Dto 원장, out long 운송실행Id)
    {
        운송실행Id = 0;
        return 원장.외부참조.TryGetValue("운송실행투영Id", out var value)
               && long.TryParse(value, out 운송실행Id)
               && 운송실행Id > 0;
    }

    private static string Resolve현재상태(커뮤니티원장Dto 원장, 커뮤니티원장블록Dto 상차블록)
    {
        if (원장.확장속성.TryGetValue("운송상태", out var transportState)
            && !string.IsNullOrWhiteSpace(transportState))
        {
            return transportState.Trim();
        }

        return Clean(원장.현재단계Key)
               ?? Clean(상차블록.State)
               ?? "상태 확인 필요";
    }

    private static bool 상차지도착가능한가(string 현재상태)
        => 상차지도착허용상태.Contains(현재상태, StringComparer.OrdinalIgnoreCase)
           || 현재상태.Contains("배차 대기", StringComparison.OrdinalIgnoreCase)
           || 현재상태.Contains("상차 대기", StringComparison.OrdinalIgnoreCase);

    private static string Build도착비활성사유(string 현재상태)
        => 현재상태.Contains("상차지도착", StringComparison.OrdinalIgnoreCase)
           || 현재상태.Contains("상차완료", StringComparison.OrdinalIgnoreCase)
           || 현재상태.Contains("하차", StringComparison.OrdinalIgnoreCase)
           || 현재상태.Contains("인수완료", StringComparison.OrdinalIgnoreCase)
            ? "상차지 도착 단계가 이미 처리되었습니다."
            : $"현재 상태({현재상태})에서는 도착 처리할 수 없습니다.";

    private static string Build완료비활성사유(string 현재상태)
        => 현재상태.Contains("상차완료", StringComparison.OrdinalIgnoreCase)
           || 현재상태.Contains("하차", StringComparison.OrdinalIgnoreCase)
           || 현재상태.Contains("인수완료", StringComparison.OrdinalIgnoreCase)
            ? "상차 완료 단계가 이미 처리되었습니다."
            : "상차지 도착 확인 후 실행할 수 있습니다.";

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
