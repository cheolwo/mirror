namespace Hongdal.WebApp.Models;

public sealed record WebNavigationItem(
    string Title,
    string Href,
    string Icon,
    bool MatchAll = false);
