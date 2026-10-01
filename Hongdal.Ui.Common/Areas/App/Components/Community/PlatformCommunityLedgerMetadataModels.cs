using Hongdal.Contracts.Common.Community;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public sealed record PlatformCommunityLedgerApiSurfacePresentation(
    CommunityLedgerProcessingSurfaceResponse Surface,
    string Method,
    Color StatusColor,
    string StatusLabel,
    string ResolvedRoute,
    bool HasUnresolvedMetadata,
    bool HasMissingRouteParameters);
