using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Driver.Work;

namespace DriverApp.Services;

public sealed class DriverWorkApiService : IDriverWorkApiService
{
    private const string WorkPath = "api/v1/driver/work";
    private const string ShiftPath = "api/v1/driver/shifts";
    private const string CommunityInquiryPath = "api/v1/driver/community-inquiries";
    private readonly IDriverApiClient _client;

    public DriverWorkApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<기사운행상태응답?> 운행상태조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사운행상태응답>(
            $"{WorkPath}/status",
            "기사 운행 상태 조회",
            cancellationToken);

    public Task<기사현재근무응답?> 현재근무조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사현재근무응답>(
            $"{WorkPath}/current",
            "기사 현재 근무 조회",
            cancellationToken);

    public async Task<IReadOnlyList<기사근무요약응답>> 근무목록조회Async(
        CancellationToken cancellationToken = default)
        => await _client.GetAsync<IReadOnlyList<기사근무요약응답>>(
            ShiftPath,
            "기사 근무 목록 조회",
            cancellationToken) ?? [];

    public Task<기사근무요약응답?> 근무상세조회Async(
        long shiftId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync<기사근무요약응답>(
            $"{ShiftPath}/{shiftId}",
            "기사 근무 상세 조회",
            cancellationToken);

    public Task<기사근무요약응답?> 기사별근무상세조회Async(
        string driverId,
        long shiftId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync<기사근무요약응답>(
            $"api/v1/drivers/{Uri.EscapeDataString(driverId)}/shifts/{shiftId}",
            "기사별 근무 상세 조회",
            cancellationToken);

    public Task<기사운행시작응답?> 운행시작Async(
        기사운행시작요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사운행시작요청, 기사운행시작응답>(
            $"{WorkPath}/start",
            request,
            "기사 운행 시작",
            cancellationToken);

    public Task 운행종료Async(CancellationToken cancellationToken = default)
        => _client.PostAsync($"{WorkPath}/stop", "기사 운행 종료", cancellationToken);

    public Task<기사위치갱신응답?> 위치갱신Async(
        기사위치갱신요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사위치갱신요청, 기사위치갱신응답>(
            $"{WorkPath}/location",
            request,
            "기사 위치 갱신",
            cancellationToken);

    public async Task<IReadOnlyList<CommunityDriverInquiryResponse>> 커뮤니티의뢰목록Async(
        CancellationToken cancellationToken = default)
        => await _client.GetAsync<IReadOnlyList<CommunityDriverInquiryResponse>>(
            CommunityInquiryPath,
            "기사 커뮤니티 의뢰 목록 조회",
            cancellationToken) ?? [];

    public Task<CommunityDriverInquiryResponse?> 커뮤니티의뢰답변Async(
        Guid inquiryId,
        CommunityDriverInquiryDecisionRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<CommunityDriverInquiryDecisionRequest, CommunityDriverInquiryResponse>(
            $"{CommunityInquiryPath}/{inquiryId:D}/decision",
            request,
            "기사 커뮤니티 의뢰 답변",
            cancellationToken);
}
