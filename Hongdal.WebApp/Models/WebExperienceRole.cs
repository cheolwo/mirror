namespace Hongdal.WebApp.Models;

public sealed record WebExperienceRole(
    string Key,
    string ThemeClass,
    string Eyebrow,
    string Title,
    string Description,
    string ImageUrl,
    string ImageAlt,
    string Icon,
    string StartHref,
    string StartLabel,
    IReadOnlyList<WebAppPageLink> Screens);
