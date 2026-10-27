using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I공간공동운영업무연락Client
{
    Task<공간공동운영업무연락Response> ReadAsync(string ledgerId, string cycleId, string stageKey, long revision, CancellationToken ct);
}

public sealed class 공간공동운영업무연락Client(ISsalddelJsonApiClient api) : I공간공동운영업무연락Client
{
    public async Task<공간공동운영업무연락Response> ReadAsync(string ledgerId, string cycleId, string stageKey, long revision, CancellationToken ct)
    {
        _ = 공간공동운영업무Routes.Detail(ledgerId, stageKey, cycleId);
        if (revision < 1) throw new InvalidDataException();
        var url = $"{공간공동운영Routes.Api}/{Uri.EscapeDataString(ledgerId)}/cycles/{Uri.EscapeDataString(cycleId)}/work-contacts"
            + $"?stageKey={Uri.EscapeDataString(stageKey)}&expectedRevision={revision}";
        var result = await api.GetAsync<공간공동운영업무연락Response>(url, "선택한 업무의 연락처", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException();
        if (result.LedgerId != ledgerId || result.CycleId != cycleId || result.StageKey != stageKey || result.Revision != revision
            || result.AsOfUtc == default || result.Contacts is null || result.Sources is null
            || result.Contacts.Any(contact => contact is null || string.IsNullOrWhiteSpace(contact.ContactKey)
                || string.IsNullOrWhiteSpace(contact.SourceNodeKey) || contact.SourceRevision < 1)
            || result.Contacts.GroupBy(contact => contact.ContactKey, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidDataException("현재 업무의 연락처 응답을 확인하지 못했습니다.");
        return result;
    }
}
