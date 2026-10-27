using System.Globalization;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>현재 순회 응답의 작업과 제안 운임만 표시합니다. 보수 약정·포장 동의·지급을 만들지 않습니다.</summary>
public static class 분산순회수락요약표시
{
    private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");

    public static string 운임(decimal? amount) => NonNegative(amount, "원");
    public static string 거리(decimal? distance) => NonNegative(distance, "km");
    public static string 소요(decimal? minutes) => NonNegative(minutes, "분");

    public static string 작업범위(DistributedWaveDto wave)
    {
        var kinds = wave.Stops.Select(stop => stop.Kind).ToHashSet(StringComparer.Ordinal);
        if (kinds.Count == 0 || kinds.Any(kind => kind is not (DistributedFulfillmentCodes.Pickup or DistributedFulfillmentCodes.Delivery)))
            return "작업 범위 확인 필요";
        return (kinds.Contains(DistributedFulfillmentCodes.Pickup), kinds.Contains(DistributedFulfillmentCodes.Delivery)) switch
        {
            (true, true) => "거점 직접 피킹 → 고객 전달",
            (true, false) => "거점 직접 피킹",
            _ => "고객 전달"
        };
    }

    public static string? 약정확인경로(DistributedWaveDto? wave, string? returnHref)
    {
        if (wave is null || wave.Revision < 1 || wave.WaveId is "." or ".."
            || RoleWorkspaceNavigation.StableId(wave.WaveId) is null) return null;
        var canonical = 분산이행Navigation.Href("cargo-driver", "round", wave.WaveId);
        return 활동정산Navigation.Href(returnUrl: SafeRoundReturn(returnHref, wave.WaveId) ?? canonical);
    }

    private static string NonNegative(decimal? value, string unit)
        => value is >= 0m ? value.Value.ToString("#,0.############################", Korean) + " " + unit : "미확정";

    private static string? SafeRoundReturn(string? href, string waveId)
    {
        var safe = RoleWorkspaceNavigation.LocalRoute(href);
        if (safe is null) return null;
        var route = safe.Split('?', 2);
        if (route[0] is not ("/driver/distributed/round" or "/workspace/cargo-driver/distributed/round") || route.Length != 2)
            return null;
        try
        {
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in route[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length != 2 || !query.TryAdd(Uri.UnescapeDataString(pair[0]), Uri.UnescapeDataString(pair[1]))) return null;
            }
            if (!query.TryGetValue("selected", out var selected) || selected != waveId
                || query.Keys.Any(key => key is not ("selected" or "requestId" or "returnUrl"))) return null;
            if (query.TryGetValue("requestId", out var request) && (!Guid.TryParse(request, out var id) || id == Guid.Empty)) return null;
            if (query.TryGetValue("returnUrl", out var home) && RoleWorkspaceNavigation.LocalRoute(home) is null) return null;
            return safe;
        }
        catch (UriFormatException) { return null; }
    }
}
