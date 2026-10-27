using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Sales;
using 살뜰.도메인.판매;

namespace Ssalddel.Services.LogisticsProcessing.SalesOrders;

/// <summary>계정 원장에 고객 정보·외부 원문 없이 보류 관측만 보관합니다. 주문·재고의 정본이 아닙니다.</summary>
internal static class SalesChannelSyncDiagnostics
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal const string ProviderKey = "provider";
    private const int MaximumIssues = 200;
    internal static string OrderKey(SalesChannelOrderFeedEntry order)
        => Hash(order.ChannelType + "|" + order.ChannelOrderNo);
    internal static IReadOnlyList<string> ItemKeys(SalesChannelOrderFeedEntry order)
        => order.Items.Take(20).Select((item, index) => $"{index + 1}:{Hash(item.ChannelProductNo + "|" + item.Sku)[..16]}").ToArray();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal static void Record(판매채널계정 account, string orderKey, string issueCode, IReadOnlyList<string> itemKeys, DateTime now)
    {
        var state = Read(account);
        var existing = state.Issues.LastOrDefault(x => x.OrderKey == orderKey && x.IssueCode == issueCode && x.RetryStateCode != "Resolved");
        if (existing is not null) existing.LastObservedAtUtc = now;
        else if (state.Issues.Count < MaximumIssues)
            state.Issues.Add(new() { OrderKey = orderKey, IssueCode = issueCode, FirstObservedAtUtc = now, LastObservedAtUtc = now, ItemKeys = itemKeys.ToArray() });
        else state.HistoryLimitReached = true;
        state.Revision++; account.동기화진단Json = JsonSerializer.Serialize(state, Json); account.UpdatedAt = now;
    }

    internal static void Resolve(판매채널계정 account, string orderKey, DateTime now)
    {
        var state = Read(account);
        var pending = state.Issues.Where(x => x.OrderKey == orderKey && x.RetryStateCode != "Resolved").ToArray();
        if (pending.Length == 0) return;
        foreach (var issue in pending) { issue.RetryStateCode = "Resolved"; issue.LastObservedAtUtc = now; }
        state.Revision++; account.동기화진단Json = JsonSerializer.Serialize(state, Json); account.UpdatedAt = now;
    }

    internal static bool HasHistoryLimit(판매채널계정 account) => Read(account).HistoryLimitReached;

    internal static IReadOnlyList<판매채널주문보류진단> Pending(판매채널계정 account)
    {
        var state = Read(account);
        var result = state.Issues.Where(x => x.RetryStateCode != "Resolved")
            .Select(x => new 판매채널주문보류진단(account.Id, account.채널종류, x.OrderKey, x.IssueCode,
                x.FirstObservedAtUtc, x.LastObservedAtUtc, x.RetryStateCode, x.ItemKeys)).ToList();
        if (state.HistoryLimitReached)
            result.Add(new(account.Id, account.채널종류, "history", SalesChannelSyncIssueCodes.HistoryLimitReached, account.UpdatedAt, account.UpdatedAt, "OperatorReviewRequired", []));
        return result;
    }

    private static DiagnosticState Read(판매채널계정 account)
    {
        if (account.동기화진단Json is null) return new();
        try
        {
            var state = JsonSerializer.Deserialize<DiagnosticState>(account.동기화진단Json, Json);
            if (state is null || state.SchemaVersion != 1 || state.Issues is null || state.Issues.Count > MaximumIssues
                || state.Issues.Any(x => x is null || string.IsNullOrWhiteSpace(x.OrderKey) || string.IsNullOrWhiteSpace(x.IssueCode)
                    || x.ItemKeys is null || x.ItemKeys.Count > 20 || x.RetryStateCode is not ("WaitingForCorrection" or "Resolved")))
                throw new JsonException();
            return state;
        }
        catch (JsonException) { throw new InvalidOperationException("판매채널 진단 기록을 확인할 수 없습니다. 운영자 확인이 필요합니다."); }
    }
    private sealed class DiagnosticState
    {
        public int SchemaVersion { get; set; } = 1;
        public long Revision { get; set; }
        public bool HistoryLimitReached { get; set; }
        public List<Issue> Issues { get; set; } = [];
    }
    private sealed class Issue
    {
        public string OrderKey { get; set; } = "";
        public string IssueCode { get; set; } = "";
        public DateTime FirstObservedAtUtc { get; set; }
        public DateTime LastObservedAtUtc { get; set; }
        public string RetryStateCode { get; set; } = "WaitingForCorrection";
        public IReadOnlyList<string> ItemKeys { get; set; } = [];
    }
}
