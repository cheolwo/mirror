using System.Globalization;
using Ssalddel.Contracts.Common.Inventory;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>출고 공정 화면을 오갈 때 URL로 전달하는 조회·선택 문맥입니다.</summary>
public sealed record OutboundBatchNavigationState(
    string Stage, long WarehouseId = 0, DateTime? StartUtc = null, DateTime? EndUtc = null,
    int BatchPage = 0, string? BatchKey = null, int MemberPage = 0, int PlanPage = 0)
{
    private const string Route = "/warehouse/general/outbound-plan-review";
    private static readonly DateTime MinimumStartUtc = DateTime.SpecifyKind(DateTime.MinValue.AddDays(9).AddHours(-9), DateTimeKind.Utc);
    private static readonly DateTime MaximumEndUtc = DateTime.SpecifyKind(DateTime.MaxValue.AddDays(-1).AddHours(-9), DateTimeKind.Utc);

    public static bool TryCreate(string? stage, long? warehouseId, string? startUtc, string? endUtc,
        int? batchPage, string? batchKey, int? memberPage, int? planPage,
        out OutboundBatchNavigationState? state, out string? error)
    {
        state = null;
        error = null;
        if (string.IsNullOrEmpty(stage)) return true;
        if (!OutboundWorkflowStageCodes.All.Contains(stage, StringComparer.Ordinal))
        { error = "열려는 출고 공정을 확인해 주세요."; return false; }
        if (warehouseId is < 0 || batchPage is < 0 || memberPage is < 0 || planPage is < 0)
        { error = "창고와 조회 페이지를 확인해 주세요."; return false; }
        var key = string.IsNullOrEmpty(batchKey) ? null : batchKey;
        if (key is not null && (key.Length > 512 || string.IsNullOrWhiteSpace(key) || key.Any(char.IsControl)))
        { error = "선택한 작업 묶음의 연결 정보를 확인해 주세요."; return false; }
        DateTime? start = null;
        DateTime? end = null;
        if (!string.IsNullOrEmpty(startUtc) || !string.IsNullOrEmpty(endUtc))
        {
            if (!TryUtc(startUtc, out var parsedStart) || !TryUtc(endUtc, out var parsedEnd)
                || parsedStart < MinimumStartUtc || parsedEnd > MaximumEndUtc
                || parsedEnd <= parsedStart || parsedEnd - parsedStart > TimeSpan.FromHours(24))
            { error = "UTC 조회 시작·종료 시각과 24시간 이내의 구간을 확인해 주세요."; return false; }
            start = parsedStart;
            end = parsedEnd;
        }
        if (key is not null && (warehouseId is not > 0 || start is null || end is null)
            || key is null && memberPage is > 0)
        { error = "작업 묶음과 창고·조회 구간·구성 페이지를 함께 확인해 주세요."; return false; }
        state = new(stage, warehouseId ?? 0, start, end, batchPage ?? 0, key, memberPage ?? 0, planPage ?? 0);
        return true;
    }

    public string Href(long? outboundPlanId = null)
    {
        if (!TryCreate(Stage, WarehouseId, StartUtc?.ToString("O", CultureInfo.InvariantCulture),
            EndUtc?.ToString("O", CultureInfo.InvariantCulture), BatchPage, BatchKey, MemberPage, PlanPage,
            out var validated, out var error) || validated is null)
            throw new ArgumentException(error ?? "열려는 출고 공정을 확인해 주세요.");
        var values = new List<string> { "stage=" + Uri.EscapeDataString(validated.Stage) };
        if (validated.WarehouseId > 0) Add("warehouseId", validated.WarehouseId.ToString(CultureInfo.InvariantCulture));
        if (validated.StartUtc.HasValue)
        {
            Add("periodStartUtc", validated.StartUtc.Value.ToString("O", CultureInfo.InvariantCulture));
            Add("periodEndUtc", validated.EndUtc!.Value.ToString("O", CultureInfo.InvariantCulture));
        }
        if (validated.BatchPage > 0) Add("batchPage", validated.BatchPage.ToString(CultureInfo.InvariantCulture));
        if (validated.BatchKey is not null) Add("batchKey", validated.BatchKey);
        if (validated.MemberPage > 0) Add("memberPage", validated.MemberPage.ToString(CultureInfo.InvariantCulture));
        if (validated.PlanPage > 0) Add("planPage", validated.PlanPage.ToString(CultureInfo.InvariantCulture));
        if (outboundPlanId is > 0) Add("outboundPlanId", outboundPlanId.Value.ToString(CultureInfo.InvariantCulture));
        return Route + "?" + string.Join("&", values);

        void Add(string name, string value) => values.Add(name + "=" + Uri.EscapeDataString(value));
    }

    public static OutboundBatchNavigationState FromModel(출고회차ViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new(model.StageCode, model.WarehouseId, 출고회차ViewModel.ToUtc(model.StartLocal),
            출고회차ViewModel.ToUtc(model.EndLocal), model.BatchPage, model.BatchKey, model.MemberPage, model.PlanPage);
    }

    private static bool TryUtc(string? value, out DateTime result)
        => DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result)
            && result.Kind == DateTimeKind.Utc;
}
