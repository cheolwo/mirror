using System.Globalization;
using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>기존 관리자 자격으로 화물 운송 투영만 읽고 사건 검토로 연결합니다.</summary>
public sealed class CargoOperatorWorkspaceAdapter(IRoleWorkspaceApi api) : IRoleWorkspaceAdapter
{
    public string RoleKey => RoleWorkspaceCatalog.Operator;
    public string BusinessCode => BusinessPackageCatalog.FreightDelivery;
    public const string ReviewIncidents = "cargo-operator.incidents";
    private const string ListPath = "api/v1/admin/transports?business=freight-delivery";
    private const string WorkspacePath = "/workspace/operator?business=freight-delivery";

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var source = await api.GetAsync<IReadOnlyList<운송진행응답>>(RoleKey, ListPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Any(item => item.Id <= 0) || source.Select(item => item.Id).Distinct().Count() != source.Count)
            throw new InvalidOperationException("화물 운송 목록을 확인해 주세요.");
        var items = source.Select(Map).ToArray();
        var selected = items.FirstOrDefault(item => item.Id == selectedId)?.Id ?? items.FirstOrDefault()?.Id;
        return new(RoleKey, items, SelectedId: selected, EmptyActions: items.Length == 0 ? [IncidentAction()] : []);
    }

    private static RoleWorkspaceItem Map(운송진행응답 source)
    {
        var id = source.Id.ToString(CultureInfo.InvariantCulture);
        var location = CargoDriverLocationPresentation.Create(id, source.기사_운송자, source.기사위치상태Code,
            source.기사최근위도, source.기사최근경도, source.기사최근위치시각Utc, CargoWorkspacePresentation.IsClosed(source.상태));
        var sections = new List<RoleWorkspaceSection>
        {
            CargoWorkspacePresentation.Section("운송 정보", ("운송번호", source.운송번호),
                ("상태", CargoWorkspacePresentation.Display(source.상태)),
                ("기사", CargoWorkspacePresentation.Display(source.기사_운송자, "배정 대기"))),
            CargoWorkspacePresentation.Section("운송 구간", ("상차지", CargoWorkspacePresentation.Display(source.출발지)),
                ("하차지", CargoWorkspacePresentation.Display(source.도착지))),
            CargoWorkspacePresentation.Section("운임", ("운임", CargoWorkspacePresentation.Money(source.운임))),
            new("기사 위치", [location.Field("위치 확인")])
        };
        if (source.예외신고됨 || source.관리자확인필요)
            sections.Add(CargoWorkspacePresentation.Section("신고 기록",
                ("신고 단계", CargoWorkspacePresentation.Display(source.최근예외단계))));
        if (source.UpdatedAt != default)
            sections.Add(CargoWorkspacePresentation.Section("최근 변경", ("시각", CargoWorkspacePresentation.Time(source.UpdatedAt))));
        return new(id,
            CargoWorkspacePresentation.Display(source.도착지, "하차지 확인"),
            CargoWorkspacePresentation.Display(source.상태), source.운송번호, sections,
            [IncidentAction(source.Id)],
            Markers: location.Marker is { } marker ? [marker] : [],
            IsCurrent: !CargoWorkspacePresentation.IsClosed(source.상태),
            Summary: new("하차지", CargoWorkspacePresentation.Display(source.도착지),
                [new("상차지", CargoWorkspacePresentation.Display(source.출발지)),
                 new("운임", CargoWorkspacePresentation.Money(source.운임)),
                 new("기사", CargoWorkspacePresentation.Display(source.기사_운송자, "배정 대기")),
                 location.Field("기사 위치")],
                new("다음 확인", "운송 진행")),
            ComparisonFields: CargoWorkspacePresentation.조건목록(source.운송방식, source.차량종류,
                source.화물중량Kg, source.화물수량, source.상차시간창시작일시, source.상차시간창종료일시,
                source.하차시간창시작일시, source.하차시간창종료일시));
    }

    private static RoleWorkspaceAction IncidentAction(long? transportId = null)
        => new(ReviewIncidents, "화물 사건 목록", RoleWorkspaceNavigation.WithReturn(
            "/workspace/operator/cargo-incidents?business=freight-delivery"
                + (transportId is { } id ? "&transportId=" + id.ToString(CultureInfo.InvariantCulture) : ""),
            transportId is { } selected ? RoleWorkspaceNavigation.Href(RoleWorkspaceCatalog.Operator,
                selected.ToString(CultureInfo.InvariantCulture), BusinessPackageCatalog.FreightDelivery) : WorkspacePath),
            RequiresConfirmation: false);

    public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        => throw new InvalidOperationException("화물 사건 화면에서 검토해 주세요.");

    public void Clear() { }
}
