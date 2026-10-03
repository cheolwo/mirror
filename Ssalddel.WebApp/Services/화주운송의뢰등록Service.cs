using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.WebApp.Services;

public sealed class 화주운송의뢰등록Service
{
    private readonly HttpClient _httpClient;
    private readonly WebAuthSessionService _authSession;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;

    public 화주운송의뢰등록Service(
        HttpClient httpClient,
        WebAuthSessionService authSession,
        ITransportRequestLedgerObserver ledgerObserver)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _ledgerObserver = ledgerObserver;
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

        var 필수오류목록 = viewModel.필수입력오류목록;
        if (필수오류목록.Count > 0)
        {
            throw new InvalidOperationException($"서버 등록 전에 필수 입력을 보완해야 합니다: {string.Join(", ", 필수오류목록.Select(x => x.내용))}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/shipper/requests");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        request.Content = JsonContent.Create(ToCreateRequest(
            viewModel,
            _authSession.UserId,
            applicationPrivacyConsentEvidenceId,
            applicationSourceCode));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, cancellationToken));
        }

        var created = await response.Content.ReadFromJsonAsync<화주운송의뢰응답>(cancellationToken);
        if (created is null)
        {
            throw new InvalidOperationException("서버 등록 응답을 읽을 수 없습니다.");
        }

        Observe(created);
        _ledgerObserver.RequestRefresh(created.의뢰Id, "Ssalddel.WebApp.RequestCreated");
        return created;
    }

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
                설명 = BuildMemo(source.화물설명, $"적재형태: {source.화물적재형태}"),
                수량 = source.화물수량,
                중량Kg = source.화물중량Kg,
                부피Cbm = source.화물부피Cbm,
                온도조건 = source.온도조건
            },
            픽업 = CreateLocation(source.상차도로명주소, source.상차상세주소, source.상차연락처이름, source.상차연락처전화번호, source.상차시간창시작일시, source.상차시간창종료일시),
            하차 = CreateLocation(source.하차도로명주소, source.하차상세주소, source.하차연락처이름, source.하차연락처전화번호, source.하차시간창시작일시, source.하차시간창종료일시),
            요금옵션 = new PricingDTO
            {
                서비스레벨 = source.서비스레벨,
                요청사항 = BuildMemo(source.요청사항, $"권장운송방식: {source.권장운송방식}", $"추천차량: {string.Join(", ", source.추천차량종류목록)}"),
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
        DateTime? end)
    {
        return new LocationContactDTO
        {
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
