using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public sealed record FreightBrokerLedgerItem(string Id, string Title, string Status, string Stage, string ParticipationRole);
public sealed record FreightBrokerLedgerBlock(string Title, string Status);
public sealed record FreightBrokerLedgerDetail(string Id, string Title, string Status, string Stage,
    bool WorkflowEnabled, IReadOnlyList<FreightBrokerLedgerBlock> Blocks);

public interface IFreightBrokerWorkspaceClient
{
    Task<IReadOnlyList<FreightBrokerLedgerItem>> MineAsync(CancellationToken cancellationToken = default);
    Task<FreightBrokerLedgerDetail?> DetailAsync(string ledgerId, CancellationToken cancellationToken = default);
}

/// <summary>로그인 본인의 화물 협업 원장 조회만 사용합니다. 업체·허가·배차·금융 권한을 부여하지 않습니다.</summary>
public sealed class FreightBrokerWorkspaceClient(ISsalddelJsonApiClient api) : IFreightBrokerWorkspaceClient
{
    public const string WorkflowTag = "국내 화물 운송";
    public const string WorkspaceHref = "/workspace/freight-broker";
    private const string ApiRoot = "api/v1/community/posts";

    public async Task<IReadOnlyList<FreightBrokerLedgerItem>> MineAsync(CancellationToken cancellationToken = default)
    {
        var rows = await api.GetAsync<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>>(
            $"{ApiRoot}/my-ledgers?workflowTag={Uri.EscapeDataString(WorkflowTag)}", "내 화물 협업 원장", false, cancellationToken)
            ?? throw new InvalidDataException("본인 화물 원장 응답을 확인하지 못했습니다.");
        // 서버의 미상 분류 전체 조회를 수용하지 않고, 요청과 다른 범위가 섞이면 전체 응답을 보류합니다.
        if (rows.Any(row => row is null || !ValidId(row.원장Id) || !row.내접근원장여부
                || !string.Equals(row.WorkflowTag, WorkflowTag, StringComparison.Ordinal)
                || !string.Equals(row.원장템플릿Key, CommunityLedgerTemplateKeys.CargoTransport, StringComparison.Ordinal))
            || rows.Select(row => row.원장Id).Distinct(StringComparer.Ordinal).Count() != rows.Count)
            throw new InvalidDataException("본인 화물 원장의 조회 범위를 다시 확인해야 합니다.");
        return rows.Select(row => new FreightBrokerLedgerItem(row.원장Id, row.제목, row.상태,
            row.현재단계, row.참여역할)).ToArray();
    }

    public async Task<FreightBrokerLedgerDetail?> DetailAsync(string ledgerId, CancellationToken cancellationToken = default)
    {
        if (!ValidId(ledgerId)) throw new ArgumentException("목록에서 원장을 다시 선택해 주세요.", nameof(ledgerId));
        var context = await api.GetAsync<PlatformCommunityPostLedgerContextResponse>(
            $"{ApiRoot}/ledgers/{Uri.EscapeDataString(ledgerId)}/context", "선택한 내 화물 원장", true, cancellationToken);
        if (context is null) return null;
        if (!string.Equals(context.원장Id, ledgerId, StringComparison.Ordinal)
            || !string.Equals(context.원장템플릿Key, CommunityLedgerTemplateKeys.CargoTransport, StringComparison.Ordinal)
            || !string.Equals(context.업무분류Code, "cargo-transport", StringComparison.Ordinal))
            throw new InvalidDataException("선택한 화물 원장과 응답이 일치하지 않습니다.");
        if (!context.상세조회가능여부 || context.역할범위조회여부)
            throw new SsalddelApiException("선택한 원장의 본인 접근 권한을 다시 확인해 주세요.", 403, "내 화물 원장", "", null);
        // 원시 항목·연락처·원본 고객정보·관리자 행동은 별도 화면으로 복제하지 않습니다.
        return new(context.원장Id, context.제목, context.상태, context.현재단계, context.기능활성화여부,
            context.블록목록.Select(block => new FreightBrokerLedgerBlock(block.제목, block.상태 ?? "상태 미표시")).ToArray());
    }

    private static bool ValidId(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && value == value.Trim() && !value.Any(char.IsControl);
}
