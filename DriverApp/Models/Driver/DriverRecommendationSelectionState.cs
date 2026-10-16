namespace DriverApp.Models.Driver;

public sealed record DriverRecommendationSelectionKey(string RequestId, int Round);

/// <summary>추천 카드 순서와 선택만 소유합니다. 카드 선택은 배차 수락이나 현재 운송 변경이 아닙니다.</summary>
public sealed class DriverRecommendationSelectionState
{
    public IReadOnlyList<DriverRequestItem> Items { get; private set; } = [];
    public DriverRecommendationSelectionKey? SelectedKey { get; private set; }
    public DriverRequestItem? Selected => Items.FirstOrDefault(x => Key(x) == SelectedKey);
    public int SelectedIndex => SelectedKey is null ? -1 : Items.ToList().FindIndex(x => Key(x) == SelectedKey);
    public static DriverRecommendationSelectionKey Key(DriverRequestItem item) => new(item.의뢰Id, item.추천라운드);

    public void Refresh(IEnumerable<DriverRequestItem> candidates, DateTime nowUtc)
    {
        var previousIndex = SelectedIndex;
        Items = candidates.Where(x => IsLive(x, nowUtc))
            .DistinctBy(Key).ToArray();
        if (SelectedKey is not null && Items.Any(x => Key(x) == SelectedKey)) return;
        SelectedKey = Items.Count == 0 ? null : Key(Items[Math.Clamp(previousIndex, 0, Items.Count - 1)]);
    }

    public bool Select(string requestId, int round)
    {
        var key = new DriverRecommendationSelectionKey(requestId, round);
        if (!Items.Any(x => Key(x) == key)) return false;
        SelectedKey = key;
        return true;
    }

    public DriverRequestItem? FindLive(DriverRecommendationSelectionKey key, DateTime nowUtc)
        => Items.FirstOrDefault(x => Key(x) == key && IsLive(x, nowUtc));

    // 서버 추천 원장은 UTC입니다. MySQL에서 Kind 없이 반환된 UTC를 기기 현지 시각으로 재해석하지 않습니다.
    public static DateTime NormalizeServerUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value
    };

    public static bool IsLive(DriverRequestItem item, DateTime nowUtc)
        => !string.IsNullOrWhiteSpace(item.의뢰Id) && item.서버지정추천 && item.추천라운드 > 0
            && NormalizeServerUtc(item.추천시작시각!.Value) <= NormalizeServerUtc(nowUtc)
            && NormalizeServerUtc(item.추천만료시각!.Value) > NormalizeServerUtc(nowUtc);
}
