using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I공간공동운영Client
{
    Task<공간공동운영공개Response> PublicAsync(string? regionKey, int page, CancellationToken ct);
    Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct);
    Task<공간공동운영내목록Response> MineAsync(CancellationToken ct);
    Task<공간공동운영상세Response?> ReadAsync(string ledgerId, CancellationToken ct);
    Task<공간공동운영상세Response?> CreateAsync(공간공동운영생성Request request, CancellationToken ct);
    Task<공간공동운영상세Response?> CommandAsync(string ledgerId, 공간공동운영명령Request request, CancellationToken ct);
    Task<공간공동운영상세Response?> ReceiptAsync(Guid requestId, string? ledgerId, CancellationToken ct);
    Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct);
    Task<공간공동운영공개제안Dto?> InvitationAsync(string ledgerId, CancellationToken ct)
        => Task.FromResult<공간공동운영공개제안Dto?>(null);
}

/// <summary>공개 소개 조회와 로그인한 당사자의 합의 명령을 분리합니다. 전문 물류 업무를 대신 완료하지 않습니다.</summary>
public sealed class 공간공동운영Client(ISsalddelJsonApiClient api) : I공간공동운영Client
{
    public async Task<공간공동운영공개Response> PublicAsync(string? regionKey, int page, CancellationToken ct)
    {
        var query = "?page=" + Math.Clamp(page, 1, 10000)
            + (string.IsNullOrWhiteSpace(regionKey) ? "" : "&regionKey=" + Uri.EscapeDataString(regionKey));
        var response = await api.GetAsync<공간공동운영공개Response>(공간공동운영Routes.Api + "/public" + query,
            "함께하는 사람과 공간", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("공개 현황 응답을 확인하지 못했습니다.");
        if (response.ExecutionMode is not ("Simulation" or "Operational") || response.StatusCode is not ("recorded" or "simulation-excluded")
            || response.AsOfUtc == default || response.PublicParticipantCount < 0 || response.SpaceCount < 0
            || response.RecruitmentCount < 0 || response.CompletedCycleCount < 0 || response.Items is null
            || response.ExecutionMode == "Simulation" && (response.StatusCode != "simulation-excluded"
                || response.PublicParticipantCount != 0 || response.SpaceCount != 0 || response.RecruitmentCount != 0 || response.CompletedCycleCount != 0)
            || response.ExecutionMode == "Operational" && response.StatusCode != "recorded")
            throw new InvalidDataException("공개 현황 형식을 확인하지 못했습니다.");
        foreach (var item in response.Items)
        { ValidatePublic(item); if (item.ExecutionMode != response.ExecutionMode) throw new InvalidDataException("공개 제안의 실행 구분이 다릅니다."); }
        return response;
    }

    public async Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct)
        => await api.GetAsync<NeighborhoodExchangeRegionListResponse>(NeighborhoodExchangeMapRoutes.RegionsApi,
            "동네 목록", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("동네 목록을 확인하지 못했습니다.");

    public async Task<공간공동운영내목록Response> MineAsync(CancellationToken ct)
    {
        var response = await api.GetAsync<공간공동운영내목록Response>(공간공동운영Routes.Api + "/mine",
            "내 공동운영", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("내 공동운영 목록을 확인하지 못했습니다.");
        if (response.Items is null) throw new InvalidDataException("내 공동운영 목록 형식을 확인하지 못했습니다.");
        foreach (var item in response.Items)
        { if (item is null) throw new InvalidDataException("내 공동운영 항목을 확인하지 못했습니다."); ValidateDetail(item, null); }
        return response;
    }

    public async Task<공간공동운영상세Response?> ReadAsync(string ledgerId, CancellationToken ct)
        => ValidateDetail(await api.GetAsync<공간공동운영상세Response>($"{공간공동운영Routes.Api}/{Uri.EscapeDataString(ledgerId)}",
            "공동운영 진행", cancellationToken: ct), ledgerId);

    public async Task<공간공동운영상세Response?> CreateAsync(공간공동운영생성Request request, CancellationToken ct)
        => ValidateDetail(await api.SendAsync<공간공동운영생성Request, 공간공동운영상세Response>(HttpMethod.Post,
            공간공동운영Routes.Api, request, "공동운영 제안", cancellationToken: ct), null);

    public async Task<공간공동운영상세Response?> CommandAsync(string ledgerId, 공간공동운영명령Request request, CancellationToken ct)
        => ValidateDetail(await api.SendAsync<공간공동운영명령Request, 공간공동운영상세Response>(HttpMethod.Post,
            $"{공간공동운영Routes.Api}/{Uri.EscapeDataString(ledgerId)}/commands", request, "공동운영 확인", cancellationToken: ct), ledgerId);

    public async Task<공간공동운영상세Response?> ReceiptAsync(Guid requestId, string? ledgerId, CancellationToken ct)
        => ValidateDetail(await api.GetAsync<공간공동운영상세Response>($"{공간공동운영Routes.Api}/requests/{requestId:D}"
            + (string.IsNullOrWhiteSpace(ledgerId) ? "" : "?ledgerId=" + Uri.EscapeDataString(ledgerId)),
            "원래 공동운영 요청 결과", cancellationToken: ct), ledgerId);

    public async Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<공간공동운영연결후보Dto>>($"{공간공동운영Routes.Api}/{Uri.EscapeDataString(ledgerId)}/cycles/{Uri.EscapeDataString(cycleId)}/candidates",
            "연결할 수 있는 내 업무", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("연결할 업무 목록을 확인하지 못했습니다.");

    public async Task<공간공동운영공개제안Dto?> InvitationAsync(string ledgerId, CancellationToken ct)
    {
        var item = await api.GetAsync<공간공동운영공개제안Dto>($"{공간공동운영Routes.Api}/{Uri.EscapeDataString(ledgerId)}/invitation",
            "공개 공동운영 제안", cancellationToken: ct);
        if (item is null) return null;
        ValidatePublic(item);
        if (item.LedgerId != ledgerId) throw new InvalidDataException("선택한 공개 제안과 응답이 다릅니다.");
        return item;
    }
    private static void ValidatePublic(공간공동운영공개제안Dto? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.LedgerId) || item.Revision < 1 || item.TermsRevision < 1
            || item.ExecutionMode is not ("Simulation" or "Operational") || item.MissingRoleCodes is null || item.Introductions is null)
            throw new InvalidDataException("공개 제안 형식을 확인하지 못했습니다.");
    }
    private static 공간공동운영상세Response? ValidateDetail(공간공동운영상세Response? item, string? expectedId)
    {
        if (item is null) return null;
        if (string.IsNullOrWhiteSpace(item.LedgerId) || expectedId is not null && item.LedgerId != expectedId
            || item.Revision < 1 || item.TermsRevision < 1 || item.ExecutionMode is not ("Simulation" or "Operational")
            || item.Terms is null || item.Terms.RequiredRoleCodes is null || item.Participants is null || item.Cycles is null
            || item.MissingRoleCodes is null || item.AllowedActions is null || item.Readiness is null
            || item.Readiness.Sources is null || item.Readiness.Blockers is null)
            throw new InvalidDataException("공동운영 정본 형식을 확인하지 못했습니다.");
        return item;
    }
}
