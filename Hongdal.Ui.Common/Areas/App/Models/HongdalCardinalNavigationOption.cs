namespace Hongdal.Ui.Common.Areas.App.Models;

public sealed record HongdalCardinalNavigationOption(
    string TrigramKey,
    string TrigramName,
    string TrigramSymbol,
    string Direction,
    string DestinationLabel,
    string ActionKind,
    string Target = "")
{
    public string BusinessLabel { get; init; } = string.Empty;
}

public static class HongdalCardinalNavigationActionKinds
{
    public const string Route = "route";
    public const string CommunityHome = "community-home";
    public const string Compose = "compose";
    public const string Diagram = "diagram";
    public const string Work = "work";
}
