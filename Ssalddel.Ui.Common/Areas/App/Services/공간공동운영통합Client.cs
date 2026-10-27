using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I공간공동운영통합Client
{
    Task<공간공동운영통합Response> ReadAsync(string ledgerId, CancellationToken ct);
    Task<공간공동운영예약목록Response> ReservationsAsync(string ledgerId, CancellationToken ct);
    Task<공간공동운영예약후보Response> CandidatesAsync(string ledgerId, CancellationToken ct);
    Task<공간공동운영예약Dto?> ReservationAsync(string ledgerId, Guid bookingId, CancellationToken ct);
    Task<공간공동운영예약Dto?> ReceiptAsync(string ledgerId, Guid clientRequestId, CancellationToken ct);
    Task<공간공동운영예약취소영향Dto> ImpactAsync(string ledgerId, Guid bookingId, CancellationToken ct);
    Task<공간공동운영예약Dto?> CommandAsync(string ledgerId, Guid bookingId, 공간공동운영예약명령Request request, CancellationToken ct);
}

/// <summary>통합 관계 조회와 명시적 약속 결정을 전달합니다. 조회·인증 복귀는 명령을 보내지 않습니다.</summary>
public sealed class 공간공동운영통합Client(ISsalddelJsonApiClient api) : I공간공동운영통합Client
{
    private static string Base(string id) => $"{공간공동운영Routes.Api}/{Uri.EscapeDataString(id)}";
    private static string Bookings(string id) => Base(id) + "/work-reservations";

    public async Task<공간공동운영통합Response> ReadAsync(string ledgerId, CancellationToken ct)
    {
        var value = await api.GetAsync<공간공동운영통합Response>(Base(ledgerId) + "/integrated-ledger",
            "함께할 일과 약속", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("통합 원장의 현재 관계를 확인하지 못했습니다.");
        if (value.LedgerId != ledgerId || value.Revision < 1 || value.Cycles is null
            || value.SharedSources is null || value.Relations is null || value.AsOfUtc == default
            || value.Cycles.Any(c => c is null || c.Orders is null || c.SharedSourceKeys is null
                || c.Orders.Any(o => o is null || o.Children is null)))
            throw new InvalidDataException("선택한 통합 원장의 응답을 확인하지 못했습니다.");
        return value;
    }

    public async Task<공간공동운영예약목록Response> ReservationsAsync(string ledgerId, CancellationToken ct)
    {
        var value = await api.GetAsync<공간공동운영예약목록Response>(Bookings(ledgerId),
            "내 약속", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("약속 목록을 확인하지 못했습니다.");
        if (value.LedgerId != ledgerId || value.Revision < 1 || value.Items is null || value.Notices is null)
            throw new InvalidDataException("선택한 공동운영의 약속 목록을 확인하지 못했습니다.");
        return value;
    }

    public async Task<공간공동운영예약후보Response> CandidatesAsync(string ledgerId, CancellationToken ct)
        => await api.GetAsync<공간공동운영예약후보Response>(Bookings(ledgerId) + "/candidates",
            "약속을 제안할 참여자", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("현재 제안할 수 있는 참여자를 확인하지 못했습니다.");

    public Task<공간공동운영예약Dto?> ReservationAsync(string ledgerId, Guid bookingId, CancellationToken ct)
        => api.GetAsync<공간공동운영예약Dto>(Bookings(ledgerId) + $"/{bookingId:D}",
            "선택한 약속", cancellationToken: ct);

    public Task<공간공동운영예약Dto?> ReceiptAsync(string ledgerId, Guid clientRequestId, CancellationToken ct)
        => api.GetAsync<공간공동운영예약Dto>(Bookings(ledgerId) + $"/requests/{clientRequestId:D}",
            "원래 약속 요청 결과", cancellationToken: ct);

    public async Task<공간공동운영예약취소영향Dto> ImpactAsync(string ledgerId, Guid bookingId, CancellationToken ct)
        => await api.GetAsync<공간공동운영예약취소영향Dto>(Bookings(ledgerId) + $"/{bookingId:D}/cancel-impact",
            "약속 취소 전 영향", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidDataException("취소 전에 확인할 영향을 불러오지 못했습니다.");

    public Task<공간공동운영예약Dto?> CommandAsync(string ledgerId, Guid bookingId, 공간공동운영예약명령Request request, CancellationToken ct)
        => api.SendAsync<공간공동운영예약명령Request, 공간공동운영예약Dto>(HttpMethod.Post,
            Bookings(ledgerId) + $"/{bookingId:D}/commands", request, "약속 조건 확인", cancellationToken: ct);
}
