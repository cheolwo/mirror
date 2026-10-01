using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Client.Infrastructure.Transport;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using HongdalApp.Models.Shipper;

namespace HongdalApp.Services;

public sealed class ServerBackedShipperOperationsService : IShipperOperationsService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;

    public ServerBackedShipperOperationsService(
        HttpClient httpClient,
        IAuthSession authSession,
        ITransportRequestLedgerObserver ledgerObserver)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _ledgerObserver = ledgerObserver;
    }

    public async Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken cancellationToken = default)
    {
        var userId = ResolveUserId();
        var path = $"api/v1/shipper/requests?shipperId={Uri.EscapeDataString(userId)}";
        var response = await GetAuthorizedJsonAsync<IReadOnlyList<화주운송의뢰응답>>(path, cancellationToken);
        var items = response?.Select(ToRequestItem).ToArray() ?? [];
        foreach (var item in items)
        {
            Observe(item, "HongdalApp.RequestList");
        }

        return items;
    }

    public async Task<ShipperRequestItem?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new InvalidOperationException("조회할 의뢰 ID가 없습니다.");
        }

        var response = await GetAuthorizedJsonAsync<화주운송의뢰응답>(
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}",
            cancellationToken);
        if (response is null)
        {
            return null;
        }

        var item = ToRequestItem(response);
        Observe(item, "HongdalApp.RequestDetail");
        return item;
    }

    public async Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("api/v1/shipper/requests/public", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, "api/v1/shipper/requests/public", cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<공개화물요약응답>>(cancellationToken)
               ?? [];
    }

    public async Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAuthorizedJsonAsync<창고목록응답>("api/v1/warehouse-operations/warehouses", cancellationToken);
        return response?.Items ?? [];
    }

    public async Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAuthorizedJsonAsync<입고요청목록응답>("api/v1/warehouse-operations/inbounds", cancellationToken);
        return response?.Items ?? [];
    }

    public async Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAuthorizedJsonAsync<재고목록응답>("api/v1/warehouse-operations/inventory", cancellationToken);
        return response?.Items ?? [];
    }

    public async Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken cancellationToken = default)
    {
        var response = await PostAuthorizedJsonAsync<차량추천요청, 차량추천응답>(
            "api/v1/shipper/requests/recommend-vehicle",
            new 차량추천요청
            {
                화물종류 = "일반화물",
                화물수량 = 1
            },
            cancellationToken);

        return response?.후보목록
                   .OrderBy(x => x.우선순위)
                   .Select(x => x.차량종류)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray()
               ?? [];
    }

    public async Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default)
    {
        var response = await PostAuthorizedJsonAsync<화주운송기준운임견적요청, 화주운송기준운임견적응답>(
            "api/v1/shipper/requests/fare-estimate",
            new 화주운송기준운임견적요청
            {
                차량종류 = vehicleType,
                예상거리Km = distanceKm
            },
            cancellationToken);

        return response?.최종운임
            ?? throw new InvalidOperationException("서버 기준운임 견적 응답이 비어 있습니다.");
    }

    public async Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, "api/v1/shipper/requests");
        httpRequest.Content = JsonContent.Create(ToCreateRequest(request));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, "api/v1/shipper/requests", cancellationToken));
        }

        var responseItem = await response.Content.ReadFromJsonAsync<화주운송의뢰응답>(cancellationToken)
            ?? throw new InvalidOperationException("서버 운송의뢰 생성 응답이 비어 있습니다.");
        var created = ToRequestItem(responseItem);
        Observe(created, "HongdalApp.RequestCreated");
        _ledgerObserver.RequestRefresh(created.의뢰Id, "HongdalApp.RequestCreated");
        return created;
    }

    public async Task<ShipperRequestItem> UpdateRequestAsync(
        ShipperRequestItem request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.의뢰Id))
        {
            throw new InvalidOperationException("수정할 의뢰 ID가 없습니다.");
        }

        var path = $"api/v1/shipper/requests/{Uri.EscapeDataString(request.의뢰Id.Trim())}";
        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Put, path);
        httpRequest.Content = JsonContent.Create(ToUpdateRequest(request));
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, path, cancellationToken));
        }

        var responseItem = await response.Content.ReadFromJsonAsync<화주운송의뢰응답>(cancellationToken)
            ?? throw new InvalidOperationException("서버 운송의뢰 수정 응답이 비어 있습니다.");
        var updated = ToRequestItem(responseItem);
        Observe(updated, "HongdalApp.RequestUpdated");
        _ledgerObserver.RequestRefresh(updated.의뢰Id, "HongdalApp.RequestUpdated");
        return updated;
    }

    public async Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new InvalidOperationException("삭제할 의뢰 ID가 없습니다.");
        }

        var path = $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}";
        using var request = CreateAuthorizedRequest(HttpMethod.Delete, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, path, cancellationToken));
        }

        _ledgerObserver.RequestRefresh(requestId, "HongdalApp.RequestDeleted");
    }

    private async Task<T?> GetAuthorizedJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, path, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<TResponse?> PostAuthorizedJsonAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, path, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        EnsureAuthenticated();
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        return request;
    }

    private void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            throw new InvalidOperationException("서버 인증 정보가 없어 화주 API를 호출할 수 없습니다.");
        }
    }

    private string ResolveUserId()
    {
        EnsureAuthenticated();
        return string.IsNullOrWhiteSpace(_authSession.UserId)
            ? throw new InvalidOperationException("화주 사용자 ID가 없어 서버 의뢰 목록을 조회할 수 없습니다.")
            : _authSession.UserId!;
    }

    private static async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"화주 서버 API 요청에 실패했습니다. path={path}, HTTP {(int)response.StatusCode}";
        }

        return $"화주 서버 API 요청에 실패했습니다. path={path}, HTTP {(int)response.StatusCode}: {body}";
    }

    private 화주운송의뢰생성요청 ToCreateRequest(ShipperRequestItem source)
    {
        var amount = source.결제예정금액
            ?? (source.기준운임.HasValue ? decimal.ToInt32(source.기준운임.Value) : null);
        var pickupAddress = string.IsNullOrWhiteSpace(source.픽업지) ? "상차지 미정" : source.픽업지!;
        var dropoffAddress = string.IsNullOrWhiteSpace(source.하차지) ? "하차지 미정" : source.하차지!;
        var now = DateTime.UtcNow;

        return new 화주운송의뢰생성요청
        {
            화주Id = ResolveUserId(),
            운송방식 = string.IsNullOrWhiteSpace(source.운송방식) ? "일반" : source.운송방식,
            차량종류 = string.IsNullOrWhiteSpace(source.차량종류) ? "1톤 카고" : source.차량종류,
            결제수단 = string.IsNullOrWhiteSpace(source.결제수단) ? 결제수단.카드.ToString() : source.결제수단,
            결제예정금액 = amount,
            정산조건 = new 화주운송정산조건DTO
            {
                정산시점 = 정산시점.선결제,
                결제수단 = 결제수단.카드,
                증빙방식 = 증빙방식.인수증,
                수납주체 = 수납주체.플랫폼,
                정산메모 = "HongdalApp 서버 API 생성"
            },
            화물 = new CargoDTO
            {
                화물종류 = string.IsNullOrWhiteSpace(source.화물종류) ? "일반화물" : source.화물종류,
                수량 = 1
            },
            픽업 = CreateLocation(pickupAddress, now.AddHours(1), now.AddHours(3)),
            하차 = CreateLocation(dropoffAddress, now.AddHours(4), now.AddHours(8)),
            요금옵션 = new PricingDTO
            {
                서비스레벨 = "standard",
                요청사항 = "HongdalApp 서버 API 생성",
                예상거리Km = source.예상거리Km,
                최종운임 = source.기준운임,
                기사지급예정운임 = source.기사지급예정운임,
                알선정책 = new 화주운송알선정책DTO
                {
                    알선단계 = source.알선단계,
                    재알선금지 = source.재알선금지
                }
            },
            클라이언트요청Id = string.IsNullOrWhiteSpace(source.의뢰Id) ? $"shipper-app-{Guid.NewGuid():N}" : source.의뢰Id,
            결제상태 = string.IsNullOrWhiteSpace(source.결제상태) ? "결제대기" : source.결제상태
        };
    }

    private static LocationContactDTO CreateLocation(string address, DateTime start, DateTime end)
    {
        return new LocationContactDTO
        {
            주소 = new AddressDTO
            {
                도로명주소 = address
            },
            연락처 = new ContactDTO
            {
                이름 = "살뜰 앱 담당자",
                전화번호 = "010-0000-0000"
            },
            시간창 = new TimeWindowDTO
            {
                시작일시 = start,
                종료일시 = end
            }
        };
    }

    private static ShipperRequestItem ToRequestItem(화주운송의뢰응답 source)
    {
        return new ShipperRequestItem
        {
            의뢰Id = source.의뢰Id,
            화물종류 = source.요약?.화물종류 ?? string.Empty,
            의뢰상태 = source.의뢰상태,
            결제상태 = source.결제상태,
            배차상태 = source.배차상태,
            정산상태 = source.정산상태,
            운송방식 = source.운송방식,
            차량종류 = source.차량종류,
            결제수단 = source.결제수단,
            결제예정금액 = source.결제예정금액,
            기준운임 = source.최종운임,
            기사지급예정운임 = source.최종운임,
            생성일시 = source.생성일시,
            픽업지 = source.픽업지,
            하차지 = source.하차지
        };
    }

    private 화주운송의뢰수정요청 ToUpdateRequest(ShipperRequestItem source)
    {
        var create = ToCreateRequest(source);
        return new 화주운송의뢰수정요청
        {
            운송방식 = create.운송방식,
            차량종류 = create.차량종류,
            결제수단 = create.결제수단,
            결제예정금액 = create.결제예정금액,
            정산조건 = create.정산조건,
            화물 = create.화물,
            픽업 = create.픽업,
            하차 = create.하차,
            요금옵션 = create.요금옵션,
            결제상태 = source.결제상태,
            상태 = source.의뢰상태,
            배차상태 = source.배차상태
        };
    }

    private void Observe(ShipperRequestItem item, string source)
    {
        if (string.IsNullOrWhiteSpace(item.의뢰Id))
        {
            return;
        }

        _ledgerObserver.Observe(
            new TransportRequestLedgerSnapshot(
                item.의뢰Id,
                item.의뢰상태,
                item.결제상태,
                item.배차상태,
                item.정산상태,
                DateTimeOffset.UtcNow,
                source),
            source);
    }
}
