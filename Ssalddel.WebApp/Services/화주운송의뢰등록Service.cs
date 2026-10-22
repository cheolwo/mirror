using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.WebApp.Services;

public sealed record 화주웹접수기록(string Owner, Guid RequestId, DateTime CreatedAtUtc,
    string? ConfirmedRequestId = null, int? RejectedStatus = null)
{
    // 서버의 기존 모바일 신규접수 CAS 계약을 Web에서도 재사용합니다. 플랫폼 식별자가 아닙니다.
    public string ClientRequestId => $"shipper-app-{RequestId:N}";
    public string ExpectedRequestId => new Guid(SHA256.HashData(
        Encoding.UTF8.GetBytes(Owner + ":" + ClientRequestId)).AsSpan(0, 16)).ToString();
}

public interface I화주운송의뢰등록Service
{
    Task<차량추천응답> RecommendVehiclesAsync(차량추천요청 request, CancellationToken ct = default)
        => throw new NotSupportedException("현재 화물 조건의 차량 추천 연결을 확인해 주세요.");
    Task<화주웹접수기록?> 접수기록조회Async(string owner, CancellationToken ct = default);
    Task<(화주웹접수기록 Record, bool Created)> 접수시작Async(string owner, 운송모델작성Draft draft, CancellationToken ct = default);
    Task<화주웹접수기록> 접수결과저장Async(화주웹접수기록 record, CancellationToken ct = default);
    Task 접수기록종료Async(화주웹접수기록 record, CancellationToken ct = default);
    Task<화주운송의뢰응답> 등록Async(운송의뢰작성ViewModel source, 화주웹접수기록 record,
        Guid? consentId, string sourceCode, CancellationToken ct = default);
    Task<화주운송의뢰응답?> 접수결과조회Async(화주웹접수기록 record, CancellationToken ct = default);
}

public sealed class 화주웹접수거절Exception(int statusCode, string message) : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class 화주운송의뢰등록Service : I화주운송의뢰등록Service
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly WebAuthSessionService _authSession;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;
    private readonly IJSRuntime _js;

    public 화주운송의뢰등록Service(
        HttpClient httpClient,
        WebAuthSessionService authSession,
        ITransportRequestLedgerObserver ledgerObserver,
        IJSRuntime js)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _ledgerObserver = ledgerObserver;
        _js = js;
    }

    public async Task<차량추천응답> RecommendVehiclesAsync(차량추천요청 payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        await _authSession.RestoreAsync(ct);
        if (!_authSession.IsLoggedIn || string.IsNullOrWhiteSpace(_authSession.AccessToken))
            throw new InvalidOperationException("로그인한 뒤 차량 추천을 확인해 주세요.");
        var owner = _authSession.UserId;
        var token = _authSession.AccessToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/shipper/requests/recommend-vehicle");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(payload);
        using var response = await _httpClient.SendAsync(request, ct);
        if (_authSession.UserId != owner || _authSession.AccessToken != token || !_authSession.IsLoggedIn)
            throw new OperationCanceledException();
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<차량추천응답>(ct)
            ?? throw new InvalidDataException("차량 추천 응답을 확인하지 못했습니다.");
        if (_authSession.UserId != owner || _authSession.AccessToken != token || !_authSession.IsLoggedIn)
            throw new OperationCanceledException();
        return result;
    }

    public async Task<화주운송의뢰응답> 등록Async(
        운송의뢰작성ViewModel viewModel,
        Guid? applicationPrivacyConsentEvidenceId = null,
        string applicationSourceCode = "",
        CancellationToken cancellationToken = default)
    {
        await _authSession.RestoreAsync(cancellationToken);
        if (!_authSession.IsLoggedIn || string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            throw new InvalidOperationException("서버 등록을 하려면 먼저 웹 로그인에서 서버 인증을 완료해야 합니다.");
        }

        var (record, created) = await 접수시작Async(_authSession.UserId!, viewModel.ToDraft(), cancellationToken);
        if (!created)
            return await 접수결과조회Async(record, cancellationToken)
                ?? throw new InvalidOperationException("이전 접수 번호를 유지했습니다. 등록 결과를 먼저 확인해 주세요.");
        return await 등록Async(viewModel, record, applicationPrivacyConsentEvidenceId, applicationSourceCode, cancellationToken);
    }

    public async Task<화주운송의뢰응답> 등록Async(운송의뢰작성ViewModel viewModel, 화주웹접수기록 record,
        Guid? consentId, string sourceCode, CancellationToken ct = default)
    {
        RequireOwner(record.Owner);
        Validate(record, record.Owner);
        var stored = await 접수기록조회Async(record.Owner, ct);
        if (stored?.RequestId != record.RequestId || stored.ConfirmedRequestId is not null)
            throw new InvalidOperationException("보관된 접수 번호와 원 요청을 먼저 확인해 주세요. 새 등록은 보내지 않았습니다.");
        var 필수오류목록 = viewModel.필수입력오류목록;
        if (필수오류목록.Count > 0)
        {
            throw new InvalidOperationException($"서버 등록 전에 필수 입력을 보완해야 합니다: {string.Join(", ", 필수오류목록.Select(x => x.내용))}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/shipper/requests");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        var payload = ToCreateRequest(viewModel, record.Owner, consentId, sourceCode);
        payload.클라이언트요청Id = record.ClientRequestId;
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, ct);
        RequireOwner(record.Owner);
        if (!response.IsSuccessStatusCode)
        {
            var message = await BuildFailureMessageAsync(response, ct);
            if ((int)response.StatusCode is >= 400 and < 500 and not 408 and not 429)
                throw new 화주웹접수거절Exception((int)response.StatusCode, message);
            throw new HttpRequestException(message, null, response.StatusCode);
        }

        var created = await response.Content.ReadFromJsonAsync<화주운송의뢰응답>(ct)
            ?? throw new InvalidDataException("서버 등록 응답을 읽을 수 없습니다.");
        RequireOwner(record.Owner);
        ValidateResponse(created, record);
        Observe(created);
        _ledgerObserver.RequestRefresh(created.의뢰Id, "Ssalddel.WebApp.RequestCreated");
        return created;
    }

    public async Task<화주운송의뢰응답?> 접수결과조회Async(화주웹접수기록 record, CancellationToken ct = default)
    {
        RequireOwner(record.Owner);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "api/v1/shipper/requests/by-client-request/" + Uri.EscapeDataString(record.ClientRequestId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        using var response = await _httpClient.SendAsync(request, ct);
        RequireOwner(record.Owner);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(await BuildFailureMessageAsync(response, ct), null, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<화주운송의뢰응답>(ct);
        RequireOwner(record.Owner);
        ValidateResponse(result, record);
        Observe(result!);
        return result;
    }

    public async Task<화주웹접수기록?> 접수기록조회Async(string owner, CancellationToken ct = default)
    {
        RequireOwner(owner);
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", ct, Key(owner));
        RequireOwner(owner);
        return string.IsNullOrWhiteSpace(json) ? null : ReadRecord(json, owner);
    }

    public async Task<(화주웹접수기록 Record, bool Created)> 접수시작Async(string owner, 운송모델작성Draft draft, CancellationToken ct = default)
    {
        RequireOwner(owner);
        var candidate = new 화주웹접수기록(owner, Guid.NewGuid(), DateTime.UtcNow);
        await using var module = await _js.InvokeAsync<IJSObjectReference>("import", ct, "./js/shipper-request-recovery.js");
        var result = await module.InvokeAsync<접수시작결과>("begin", ct, Key(owner), JsonSerializer.Serialize(candidate, JsonOptions),
            운송의뢰자동저장Service.OwnerKey(owner), JsonSerializer.Serialize(draft, JsonOptions));
        RequireOwner(owner);
        return (ReadRecord(result.Json, owner), result.Created);
    }

    public async Task<화주웹접수기록> 접수결과저장Async(화주웹접수기록 record, CancellationToken ct = default)
    {
        RequireOwner(record.Owner);
        Validate(record, record.Owner);
        await using var module = await _js.InvokeAsync<IJSObjectReference>("import", ct, "./js/shipper-request-recovery.js");
        var json = await module.InvokeAsync<string>("update", ct, Key(record.Owner), record.RequestId.ToString(), JsonSerializer.Serialize(record, JsonOptions));
        RequireOwner(record.Owner);
        return ReadRecord(json, record.Owner);
    }

    public async Task 접수기록종료Async(화주웹접수기록 record, CancellationToken ct = default)
    {
        RequireOwner(record.Owner);
        if (record.ConfirmedRequestId != record.ExpectedRequestId)
            throw new InvalidOperationException("등록 정본이 확인된 접수만 닫고 새 의뢰를 작성할 수 있습니다.");
        await using var module = await _js.InvokeAsync<IJSObjectReference>("import", ct, "./js/shipper-request-recovery.js");
        await module.InvokeVoidAsync("removeConfirmed", ct, Key(record.Owner), record.RequestId.ToString(), record.ConfirmedRequestId,
            운송의뢰자동저장Service.OwnerKey(record.Owner));
        RequireOwner(record.Owner);
    }

    private void RequireOwner(string owner)
    {
        if (!_authSession.IsLoggedIn || string.IsNullOrWhiteSpace(owner) || _authSession.UserId != owner)
            throw new OperationCanceledException("현재 로그인한 계정의 접수만 확인할 수 있습니다.");
    }
    internal static string Key(string owner) => "ssalddel.web.shipper-submission.v1."
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner))).ToLowerInvariant();
    private static 화주웹접수기록 ReadRecord(string json, string owner)
    {
        var record = JsonSerializer.Deserialize<화주웹접수기록>(json, JsonOptions)
            ?? throw new InvalidDataException("이전 접수 번호를 읽을 수 없습니다. 새 등록은 보내지 않았습니다.");
        Validate(record, owner);
        return record;
    }
    private static void Validate(화주웹접수기록 record, string owner)
    {
        Ssalddel.Ui.Common.Areas.App.Services.WorkflowRecoveryPolicy.Validate(
            new(record.Owner, "shipper.web.request.submit.v1", record.RequestId, null, record.CreatedAtUtc));
        if (record.Owner != owner || record.RequestId == Guid.Empty || record.CreatedAtUtc.Kind != DateTimeKind.Utc
            || record.ConfirmedRequestId is not null && record.ConfirmedRequestId != record.ExpectedRequestId)
            throw new InvalidDataException("접수 번호의 계정과 확인 결과를 검증하지 못했습니다.");
    }
    private static void ValidateResponse(화주운송의뢰응답? response, 화주웹접수기록 record)
    {
        if (response is null || response.의뢰Id != record.ExpectedRequestId
            || !(response.주문자UserId == record.Owner
                || string.IsNullOrEmpty(response.주문자UserId) && response.화주Id == record.Owner))
            throw new InvalidDataException("등록된 의뢰 번호와 현재 계정의 정본을 확인하지 못했습니다.");
    }
    private sealed record 접수시작결과(string Json, bool Created);

    private void Observe(화주운송의뢰응답 item)
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
                "Ssalddel.WebApp.RequestCreated"),
            "Ssalddel.WebApp.RequestCreated");
    }

    private static 화주운송의뢰생성요청 ToCreateRequest(
        운송의뢰작성ViewModel source,
        string? userId,
        Guid? applicationPrivacyConsentEvidenceId,
        string applicationSourceCode)
    {
        var 결제수단값 = Map결제수단(source.결제수단);
        var 증빙방식값 = source.결제수단.Contains("인수증", StringComparison.OrdinalIgnoreCase)
            ? 증빙방식.인수증
            : 증빙방식.없음;

        return new 화주운송의뢰생성요청
        {
            신청개인정보동의증적Id = applicationPrivacyConsentEvidenceId,
            신청출처Code = applicationSourceCode,
            화주Id = string.IsNullOrWhiteSpace(userId) ? "shipper-web-demo" : userId,
            운송방식 = source.운송방식,
            차량종류 = source.차량종류,
            결제수단 = source.결제수단,
            결제예정금액 = source.결제예정금액,
            정산조건 = new 화주운송정산조건DTO
            {
                정산시점 = Get정산시점(source.결제수단),
                결제수단 = 결제수단값,
                증빙방식 = 증빙방식값,
                수납주체 = 수납주체.플랫폼,
                세금계산서필요 = source.결제수단.Contains("계좌", StringComparison.OrdinalIgnoreCase),
                현금영수증필요 = source.결제수단.Contains("현금", StringComparison.OrdinalIgnoreCase),
                정산메모 = BuildMemo(source.절차메모, $"결제 후속 절차: {string.Join(" / ", source.결제후속절차목록)}")
            },
            화물 = new CargoDTO
            {
                화물종류 = source.화물종류,
                설명 = source.화물설명,
                수량 = source.화물수량,
                길이Mm = source.화물길이Mm,
                폭Mm = source.화물폭Mm,
                높이Mm = source.화물높이Mm,
                팔레트개수 = source.팔레트개수,
                중량Kg = source.화물중량Kg,
                부피Cbm = source.화물부피Cbm,
                바닥회전가능여부 = source.화물바닥회전가능여부,
                적층가능여부 = source.화물적층가능여부,
                화물파손주의여부 = source.화물파손주의여부,
                온도조건 = source.온도조건
            },
            픽업 = CreateLocation(source.상차도로명주소, source.상차상세주소, source.상차연락처이름, source.상차연락처전화번호, source.상차시간창시작일시, source.상차시간창종료일시, source.상차작업시간분),
            하차 = CreateLocation(source.하차도로명주소, source.하차상세주소, source.하차연락처이름, source.하차연락처전화번호, source.하차시간창시작일시, source.하차시간창종료일시, source.하차작업시간분),
            요금옵션 = new PricingDTO
            {
                서비스레벨 = source.서비스레벨,
                요청사항 = source.전송요청사항,
                예상거리Km = source.예상거리Km,
                기본운임 = source.기준운임,
                대기료 = source.대기료,
                수작업비 = source.수작업비,
                할증 = source.할증,
                기사지급예정운임 = source.기사지급예정운임,
                알선정책 = new 화주운송알선정책DTO
                {
                    알선단계 = Math.Max(1, source.알선단계),
                    재알선금지 = source.재알선금지,
                    알선소Id = source.알선소Id,
                    정책메모 = source.절차메모
                }
            },
            클라이언트요청Id = $"shipper-web-{Guid.NewGuid():N}",
            결제상태 = "결제대기"
        };
    }

    private static LocationContactDTO CreateLocation(
        string roadAddress,
        string? detailAddress,
        string contactName,
        string contactPhone,
        DateTime? start,
        DateTime? end, decimal? handlingMinutes)
    {
        return new LocationContactDTO
        {
            작업시간분 = handlingMinutes,
            주소 = new AddressDTO
            {
                도로명주소 = roadAddress,
                상세주소 = detailAddress
            },
            연락처 = new ContactDTO
            {
                이름 = contactName,
                전화번호 = contactPhone
            },
            시간창 = start.HasValue && end.HasValue ? new TimeWindowDTO
            {
                시작일시 = new DateTimeOffset(DateTime.SpecifyKind(start.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime,
                종료일시 = new DateTimeOffset(DateTime.SpecifyKind(end.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime
            } : null
        };
    }

    private static 결제수단 Map결제수단(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 결제수단.카드;
        }

        if (value.Contains("가상계좌", StringComparison.OrdinalIgnoreCase))
        {
            return 결제수단.가상계좌;
        }

        if (value.Contains("계좌", StringComparison.OrdinalIgnoreCase) || value.Contains("이체", StringComparison.OrdinalIgnoreCase))
        {
            return 결제수단.계좌이체;
        }

        if (value.Contains("현금", StringComparison.OrdinalIgnoreCase))
        {
            return 결제수단.현금;
        }

        if (value.Contains("인수증", StringComparison.OrdinalIgnoreCase) || value.Contains("정산", StringComparison.OrdinalIgnoreCase))
        {
            return 결제수단.별도정산;
        }

        return 결제수단.카드;
    }

    private static 정산시점 Get정산시점(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 정산시점.선결제;
        }

        if (value.Contains("인수증", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("정산", StringComparison.OrdinalIgnoreCase))
        {
            return 정산시점.운송완료후정산;
        }

        return 정산시점.선결제;
    }

    private static string? BuildMemo(params string?[] values)
    {
        var lines = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body)
            ? $"서버 운송 의뢰 등록에 실패했습니다. HTTP {(int)response.StatusCode}"
            : $"서버 운송 의뢰 등록에 실패했습니다. HTTP {(int)response.StatusCode}: {body}";
    }
}
