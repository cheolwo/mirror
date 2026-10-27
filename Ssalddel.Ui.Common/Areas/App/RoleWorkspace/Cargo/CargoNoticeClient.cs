using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public interface ICargoNoticeClient
{
    Task<CargoNoticeListResponse?> ListAsync(int page, CancellationToken ct = default);
    Task<IReadOnlyList<CargoNoticeRegion>?> RegionsAsync(CancellationToken ct = default);
    Task<CargoNoticeOwnerListResponse?> MineAsync(int page, CancellationToken ct = default);
    Task<CargoNoticeOwnerDto?> OwnerAsync(string noticeId, CancellationToken ct = default);
    Task<CargoNoticeCommandResponse?> ResultAsync(Guid requestId, string? noticeId, CancellationToken ct = default);
    Task<CargoNoticeCommandResponse?> CreateAsync(CargoNoticeDraftRequest request, CancellationToken ct = default);
    Task<CargoNoticeCommandResponse?> UpdateAsync(string noticeId, CargoNoticeDraftRequest request, CancellationToken ct = default);
    Task<CargoNoticeCommandResponse?> PublishAsync(string noticeId, CargoNoticePublishRequest request, CancellationToken ct = default);
    Task<CargoNoticeCommandResponse?> WithdrawAsync(string noticeId, CargoNoticeMutationRequest request, CancellationToken ct = default);
}

/// <summary>정보 공고 API만 사용합니다. 운송 의뢰·추천·배차·금융 API로 넘기지 않습니다.</summary>
public sealed class CargoNoticeClient(ISsalddelJsonApiClient api) : ICargoNoticeClient
{
    private const string Root = CargoNoticeRoutes.Api;
    public Task<CargoNoticeListResponse?> ListAsync(int page, CancellationToken ct = default)
        => api.GetAsync<CargoNoticeListResponse>($"{Root}?page={Page(page)}", "공개 화물 정보", false, ct);
    public Task<IReadOnlyList<CargoNoticeRegion>?> RegionsAsync(CancellationToken ct = default)
        => api.GetAsync<IReadOnlyList<CargoNoticeRegion>>($"{Root}/regions", "공고 지역", false, ct);
    public Task<CargoNoticeOwnerListResponse?> MineAsync(int page, CancellationToken ct = default)
        => api.GetAsync<CargoNoticeOwnerListResponse>($"{Root}/mine?page={Page(page)}", "내 화물 공고", false, ct);
    public Task<CargoNoticeOwnerDto?> OwnerAsync(string noticeId, CancellationToken ct = default)
        => api.GetAsync<CargoNoticeOwnerDto>($"{Root}/mine/{Id(noticeId)}", "내 공고 확인", false, ct);
    public Task<CargoNoticeCommandResponse?> ResultAsync(Guid requestId, string? noticeId, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("요청을 확인해 주세요.", nameof(requestId));
        var suffix = noticeId is null ? "" : $"?noticeId={Id(noticeId)}";
        return api.GetAsync<CargoNoticeCommandResponse>($"{Root}/requests/{requestId:D}{suffix}", "공고 저장 결과 확인", false, ct);
    }
    public Task<CargoNoticeCommandResponse?> CreateAsync(CargoNoticeDraftRequest request, CancellationToken ct = default)
        => api.SendAsync<CargoNoticeDraftRequest, CargoNoticeCommandResponse>(HttpMethod.Post, Root, request, "공고 초안 저장", false, ct);
    public Task<CargoNoticeCommandResponse?> UpdateAsync(string noticeId, CargoNoticeDraftRequest request, CancellationToken ct = default)
        => api.SendAsync<CargoNoticeDraftRequest, CargoNoticeCommandResponse>(HttpMethod.Put, $"{Root}/{Id(noticeId)}", request, "공고 수정", false, ct);
    public Task<CargoNoticeCommandResponse?> PublishAsync(string noticeId, CargoNoticePublishRequest request, CancellationToken ct = default)
        => api.SendAsync<CargoNoticePublishRequest, CargoNoticeCommandResponse>(HttpMethod.Post, $"{Root}/{Id(noticeId)}/publish", request, "공고 게시", false, ct);
    public Task<CargoNoticeCommandResponse?> WithdrawAsync(string noticeId, CargoNoticeMutationRequest request, CancellationToken ct = default)
        => api.SendAsync<CargoNoticeMutationRequest, CargoNoticeCommandResponse>(HttpMethod.Post, $"{Root}/{Id(noticeId)}/withdraw", request, "공고 철회", false, ct);
    private static int Page(int page) => page >= 1 ? page : throw new ArgumentOutOfRangeException(nameof(page));
    internal static string Id(string id) => id is { Length: 77 } && id.StartsWith("cargo-notice:", StringComparison.Ordinal)
        && id[13..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
        ? id : throw new ArgumentException("공고를 다시 선택해 주세요.", nameof(id));
}
