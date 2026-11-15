using System;
using System.Collections.Generic;
using System.Linq;
namespace Ssalddel.BatchMonitor.Windows
{

internal sealed record FoodCampaignDistrict(string Key, string District, string TerritoryKey, string TerritoryName,
    string ExpansionTitle);

internal sealed class FoodCampaignStage
{
    private static readonly FoodCampaignDistrict[] DistrictSequence =
    new FoodCampaignDistrict[] {
        new("jungnang", "중랑구", "sagajeong", "사가정", "사가정 첫 배차"),
        new("dongdaemun", "동대문구", "jangan", "장안", "장안으로 확장"),
        new("gwangjin", "광진구", "junggok", "중곡", "중곡으로 확장"),
        new("nowon", "노원구", "gongneung", "공릉", "공릉으로 확장"),
    };

    private static readonly FoodCampaignStage First = new(null, new[] { DistrictSequence[0] });
    private static readonly Dictionary<string, FoodCampaignStage> StagesById = DescendantsAndSelf(First)
        .ToDictionary(stage => stage.Id, StringComparer.Ordinal);

    internal static IReadOnlyList<FoodCampaignStage> All { get; } = Array.AsReadOnly(Enumerable
        .Range(1, DistrictSequence.Length).Select(depth => StagesById[DefaultId(depth)]).ToArray());

    private readonly IReadOnlyList<FoodCampaignStage> nextOptions;

    private FoodCampaignStage(string? parentId, FoodCampaignDistrict[] districts)
    {
        var defaultId = DefaultId(districts.Length);
        Id = districts.SequenceEqual(DistrictSequence.Take(districts.Length))
            ? defaultId : defaultId + "-" + string.Join("-", districts.Select(district => district.Key));
        ParentId = parentId;
        Districts = Array.AsReadOnly(districts);
        RegionLabel = string.Join(" · ", districts.Select(district => district.TerritoryName));
        DispatchLimit = TimeSpan.FromMinutes(4 + 2 * districts.Length);
        nextOptions = Array.AsReadOnly(DistrictSequence.Where(district => !districts.Contains(district))
            .Select(district => new FoodCampaignStage(Id, districts.Concat(new[] { district }).ToArray())).ToArray());
    }

    internal string Id { get; }
    internal string? ParentId { get; }
    internal string Title => ExpansionDistrict.ExpansionTitle;
    internal string RegionLabel { get; }
    internal int OrderCount => Districts.Count + 1;
    internal TimeSpan DispatchLimit { get; }
    internal IReadOnlyList<FoodCampaignDistrict> Districts { get; }
    internal FoodCampaignDistrict ExpansionDistrict => Districts[^1];

    internal static FoodCampaignStage? Find(string? id) => id is not null ? StagesById.GetValueOrDefault(id) : null;

    internal static IReadOnlyList<FoodCampaignStage> NextOptions(FoodCampaignStage stage) => stage.nextOptions;

    private static string DefaultId(int depth) => FormattableString.Invariant($"food-campaign-{depth:00}");

    private static IEnumerable<FoodCampaignStage> DescendantsAndSelf(FoodCampaignStage stage) =>
        stage.nextOptions.SelectMany(DescendantsAndSelf).Prepend(stage);
}

internal sealed record FoodCampaignOrderResult(string OrderId, string PickupStoreId, string TerritoryId,
    bool AssignedOnTime, DateTime AssignedAt, FoodManagementSettlement Settlement);

internal sealed record FoodCampaignResult(string StageId, bool Cleared, int OrderCount, int OnTimeAssignmentCount,
    int PlatformRevenue, int DriverPay, int PlatformBalance, int FastDispatchBonus,
    IReadOnlyList<FoodCampaignOrderResult> Orders);

internal sealed record FoodCampaignCommand(string Id, string OrderId, string DriverId, long ExpectedRevision, TimeSpan At);

internal sealed record FoodCampaignOrderReplay(string OrderId, FoodManagementStage Stage,
    IReadOnlyList<FoodManagementEvent> Events);

internal sealed record FoodCampaignReplay(string StageId, TimeSpan Elapsed, IReadOnlyList<FoodCampaignCommand> Commands,
    IReadOnlyList<FoodCampaignOrderReplay> Orders, FoodCampaignResult? Result);

}
