using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace NeighborhoodExchangePreview;

// 배송 화면의 상태·조작 검토 전용입니다. 실제 인증·운임·배차 API의 실행 근거가 아닙니다.
internal sealed class PreviewDeliveryUser : ISsalddel현재사용자Context
{
    public 현재사용자Snapshot 현재사용자 => new("preview-delivery-owner", "화면 검토", []);
}

internal sealed class PreviewDeliveryClient : INeighborhoodDeliveryClient
{
    private readonly Dictionary<Guid, NeighborhoodDeliveryResponse> _requests = [];
    private readonly NeighborhoodDeliveryResponse _assigned = new()
    {
        RequestId = "preview-assigned", CargoName = "선정 기사 확인 화면",
        ProposalStateCode = NeighborhoodDeliveryProposalStates.Assigned,
        FareKrw = 4800, DistanceKm = 2.4m, DistanceBasis = "화면 검토용 거리",
        SettlementStatusCode = "현장수금예정", Request = new()
        {
            운송상태 = "상차지이동중", 차량종류 = "오토바이",
            픽업지 = "화면 검토용 픽업로 1", 픽업상세지 = "문 앞", 하차지 = "화면 검토용 전달로 2",
            픽업 = new() { 주소 = new() { 도로명주소 = "화면 검토용 픽업로 1" }, 연락처 = new() { 이름 = "보내는 사람", 전화번호 = "010-0000-0000" } },
            하차 = new() { 주소 = new() { 도로명주소 = "화면 검토용 전달로 2" }, 연락처 = new() { 이름 = "받는 사람", 전화번호 = "010-0000-0001" } },
            화물 = new() { 화물종류 = "화면 검토용 책", 수량 = 2, 중량Kg = 1 }
        },
        DriverDisclosure = new()
        {
            RequestId = "preview-assigned", ConfirmedDriverId = "preview-driver",
            ConfirmedDriverName = "기사 1", RecommendationRound = 1, CanRecordConsent = true
        }
    };
    public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct)
        => Task.FromResult<신청개인정보동의증적Response?>(new()
        {
            증적Id = request.증적Id, 상태Code = 신청개인정보동의상태Codes.유효,
            업무Code = request.업무Code, 출처Code = request.출처Code
        });
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        => Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(new()
        {
            Fare = new() { 최종운임 = 4800, 예상거리Km = 2.4m, 거리계산방식 = "화면 검토용 거리", 단가출처 = "화면 검토용 금액" },
            AutomaticDispatchEnabled = false, AutomaticDispatchBlockCode = "PreviewOnly"
        });
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
    {
        if (!_requests.TryGetValue(request.ClientRequestId, out var response))
        {
            var snapshot = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(request))!;
            response = new()
            {
                RequestId = "preview-" + request.ClientRequestId.ToString("N"), CargoName = request.CargoName,
                CreatedAtUtc = DateTime.UtcNow, FareKrw = 4800, DistanceKm = 2.4m,
                DistanceBasis = "화면 검토용 거리", RateSource = "화면 검토용 금액",
                RequestStatusCode = "생성됨", DispatchStatusCode = "매칭중", SettlementStatusCode = "현장수금예정",
                PaymentStatusCode = "결제대기", ProposalStateCode = NeighborhoodDeliveryProposalStates.AwaitingActivation,
                AutomaticDispatchBlockCode = "PreviewOnly",
                Request = new()
                {
                    픽업지 = snapshot.Pickup.주소.도로명주소, 픽업상세지 = snapshot.Pickup.주소.상세주소 ?? string.Empty,
                    하차지 = snapshot.Dropoff.주소.도로명주소, 하차상세지 = snapshot.Dropoff.주소.상세주소 ?? string.Empty,
                    픽업 = snapshot.Pickup, 하차 = snapshot.Dropoff, 차량종류 = snapshot.VehicleType,
                    화물 = new() { 화물종류 = snapshot.CargoName, 수량 = snapshot.Quantity, 중량Kg = snapshot.WeightKg },
                    운송상태 = "대기", 요금옵션 = new() { 요청사항 = snapshot.Notes },
                    현장지급메모 = "화면 검토용 상태이며 실제 접수·기사 배정·지급이 아닙니다."
                }
            };
            _requests.Add(request.ClientRequestId, response);
        }
        return Task.FromResult<NeighborhoodDeliveryResponse?>(response);
    }
    public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>(_requests.Values.Skip((page - 1) * 20).Take(20).ToArray());
    public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct)
        => Task.FromResult(requestId == _assigned.RequestId ? _assigned : _requests.Values.FirstOrDefault(x => x.RequestId == requestId));
    public Task<NeighborhoodDeliveryDisclosureResponse?> RecordDisclosureAsync(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken ct)
    {
        if (requestId != _assigned.RequestId) throw new InvalidOperationException("화면 검토용 의뢰가 아닙니다.");
        _assigned.DriverDisclosure!.Consented = request.Consented;
        _assigned.DriverDisclosure.ExpiresAtUtc = request.Consented ? DateTime.UtcNow.AddHours(72) : null;
        return Task.FromResult<NeighborhoodDeliveryDisclosureResponse?>(_assigned.DriverDisclosure);
    }
}
