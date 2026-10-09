using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Pricing;

/// <summary>역할별 인증 연결을 재사용하는 관계 요금표 client입니다. 금액과 허용 행동은 서버가 소유합니다.</summary>
public sealed class AgreedRateTableClient(IRoleWorkspaceApi api, string roleKey)
{
    public const string Endpoint = "api/v1/agreed-rate-tables";
    public string RoleKey { get; } = NormalizeRole(roleKey);
    public string PartyKind => RoleKey switch
    {
        RoleWorkspaceCatalog.Restaurant => AgreedRatePartyKinds.Restaurant,
        RoleWorkspaceCatalog.FoodDriver => AgreedRatePartyKinds.FoodDriver,
        RoleWorkspaceCatalog.Shipper => AgreedRatePartyKinds.Shipper,
        _ => AgreedRatePartyKinds.CargoDriver
    };
    public string CounterpartyKind => PartyKind switch
    {
        AgreedRatePartyKinds.Restaurant => AgreedRatePartyKinds.FoodDriver,
        AgreedRatePartyKinds.FoodDriver => AgreedRatePartyKinds.Restaurant,
        AgreedRatePartyKinds.Shipper => AgreedRatePartyKinds.CargoDriver,
        _ => AgreedRatePartyKinds.Shipper
    };
    public string BusinessKind => PartyKind is AgreedRatePartyKinds.Restaurant or AgreedRatePartyKinds.FoodDriver
        ? AgreedRateBusinessKinds.FoodDelivery : AgreedRateBusinessKinds.CargoTransport;
    public static bool Supports(string? roleKey) => roleKey is RoleWorkspaceCatalog.Restaurant
        or RoleWorkspaceCatalog.FoodDriver or RoleWorkspaceCatalog.Shipper or RoleWorkspaceCatalog.CargoDriver;
    private static string NormalizeRole(string roleKey) => Supports(roleKey) ? roleKey
        : throw new ArgumentException("합의 요금표를 사용하는 역할이 아닙니다.", nameof(roleKey));
    private string Query => "?partyKind=" + Uri.EscapeDataString(PartyKind);
    private static string Segment(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 256
        ? Uri.EscapeDataString(id) : throw new InvalidOperationException("요금표 번호를 확인해 주세요.");
    public Task<IReadOnlyList<AgreedRateTableDto>> ListAsync(CancellationToken ct)
        => api.GetAsync<IReadOnlyList<AgreedRateTableDto>>(RoleKey, Endpoint + Query, ct);
    public Task<AgreedRateTableDto> DetailAsync(string id, CancellationToken ct)
        => api.GetAsync<AgreedRateTableDto>(RoleKey, $"{Endpoint}/{Segment(id)}" + Query, ct);
    public Task<AgreedRateTableDto?> ProposeAsync(AgreedRateTableProposalRequest body, CancellationToken ct)
        => api.PostAsync<AgreedRateTableDto>(RoleKey, Endpoint, body, ct);
    public Task<AgreedRateTableDto?> ActAsync(string id, AgreedRateTableActionRequest body, CancellationToken ct)
        => api.PostAsync<AgreedRateTableDto>(RoleKey, $"{Endpoint}/{Segment(id)}/actions", body, ct);
    public Task<AgreedRateTablePreviewDto?> PreviewAsync(string id, AgreedRateTablePreviewRequest body, CancellationToken ct)
        => api.PostAsync<AgreedRateTablePreviewDto>(RoleKey, $"{Endpoint}/{Segment(id)}/preview", body, ct);
    public Task<AgreedRateTableSelectionDto?> SelectAsync(string id, AgreedRateTableSelectionRequest body, CancellationToken ct)
        => api.PostAsync<AgreedRateTableSelectionDto>(RoleKey, $"{Endpoint}/{Segment(id)}/selections", body, ct);
    public Task<AgreedRateTableSelectionDto> SelectionAsync(string id, string workId, CancellationToken ct)
        => api.GetAsync<AgreedRateTableSelectionDto>(RoleKey, $"{Endpoint}/{Segment(id)}/selections/{Segment(workId)}" + Query, ct);
}
