using MudBlazor;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.WebApp.Pages.DriverCurrentTransport;

public sealed record DriverCurrentTransportNextAction(
    string Heading,
    string ButtonLabel,
    string Icon,
    Color Color,
    string Href);

public sealed record DriverCurrentTransportTimelineStep(
    string Label,
    string Icon,
    string Status,
    Color Color,
    Variant Variant);

public static class DriverCurrentTransportPresentation
{
    public static DriverCurrentTransportNextAction ResolveNextAction(기사운송요약응답 transport)
    {
        var shouldPickup = ResolveStageOrder(transport.상태) <= 2;
        return shouldPickup
            ? new DriverCurrentTransportNextAction(
                "상차 준비",
                "상차 증빙으로",
                Icons.Material.Filled.FileUpload,
                Color.Primary,
                $"/driver/transports/{transport.Id}/pickup")
            : new DriverCurrentTransportNextAction(
                "하차/인수 확인",
                "하차 증빙으로",
                Icons.Material.Filled.TaskAlt,
                Color.Success,
                $"/driver/transports/{transport.Id}/dropoff");
    }

    public static IReadOnlyList<DriverCurrentTransportTimelineStep> BuildTimeline(string? status)
    {
        var currentStageOrder = ResolveStageOrder(status);
        return
        [
            BuildTimelineStep("접수", Icons.Material.Filled.AssignmentTurnedIn, 0, currentStageOrder),
            BuildTimelineStep("배차확정", Icons.Material.Filled.LocalShipping, 1, currentStageOrder),
            BuildTimelineStep("상차지", Icons.Material.Filled.Place, 2, currentStageOrder),
            BuildTimelineStep("상차완료", Icons.Material.Filled.FileUpload, 3, currentStageOrder),
            BuildTimelineStep("하차지", Icons.Material.Filled.Flag, 4, currentStageOrder),
            BuildTimelineStep("인수완료", Icons.Material.Filled.TaskAlt, 5, currentStageOrder)
        ];
    }

    public static Color ResolveTransportStateColor(string? status)
    {
        if (ContainsAny(status, "완료", "인수"))
        {
            return Color.Success;
        }

        if (ContainsAny(status, "예외", "실패", "문제"))
        {
            return Color.Error;
        }

        if (ContainsAny(status, "도착", "상차", "하차", "운송"))
        {
            return Color.Info;
        }

        return Color.Default;
    }

    public static int ResolveStageOrder(string? status)
    {
        if (ContainsAny(status, "상차완료", "운송중"))
        {
            return 3;
        }

        if (ContainsAny(status, "인수", "하차완료", "완료"))
        {
            return 5;
        }

        if (ContainsAny(status, "하차지"))
        {
            return 4;
        }

        if (ContainsAny(status, "상차지", "상차"))
        {
            return 2;
        }

        if (string.Equals(status, "확정", StringComparison.Ordinal)
            || ContainsAny(status, "배차", "매칭"))
        {
            return 1;
        }

        return 0;
    }

    public static string Display(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    public static string DisplayMoney(decimal? value)
        => value.HasValue ? $"{value.Value:N0}원" : "금액 미정";

    private static DriverCurrentTransportTimelineStep BuildTimelineStep(
        string label,
        string icon,
        int order,
        int currentStageOrder)
    {
        var current = order == currentStageOrder;
        var reached = order <= currentStageOrder;
        return new DriverCurrentTransportTimelineStep(
            label,
            icon,
            current ? "현재" : reached ? "완료" : "대기",
            current ? Color.Primary : reached ? Color.Success : Color.Default,
            current || reached ? Variant.Filled : Variant.Outlined);
    }

    private static bool ContainsAny(string? value, params string[] tokens)
        => !string.IsNullOrWhiteSpace(value)
           && tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
