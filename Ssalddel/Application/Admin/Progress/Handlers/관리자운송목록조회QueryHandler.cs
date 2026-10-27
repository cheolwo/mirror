using System.Text.Json;
using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Application.Common.Transport;
using Ssalddel.Contracts.Common.Transport;

namespace Ssalddel.Application.Admin.Progress;

public sealed class 관리자운송목록조회QueryHandler : IRequestHandler<관리자운송목록조회Query, IReadOnlyList<운송진행응답>>
{
    private readonly SsalddelContext _db;

    public 관리자운송목록조회QueryHandler(SsalddelContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<운송진행응답>> Handle(관리자운송목록조회Query request, CancellationToken cancellationToken)
    {
        var query = _db.운송원장.AsNoTracking().AsQueryable();

        var businessType = request.BusinessCode?.Trim().ToLowerInvariant() switch
        {
            null => (int?)null,
            BusinessPackageCatalog.FreightDelivery => 상태값.배차업무유형.용달운송,
            BusinessPackageCatalog.FoodDelivery => 상태값.배차업무유형.음식배달,
            _ => throw new ArgumentException("운송 업무 구분을 확인해 주세요.")
        };
        if (businessType.HasValue)
            query = query.Where(x => x.배차업무유형 == businessType.Value);

        if (!string.IsNullOrWhiteSpace(request.상태))
        {
            var status = request.상태.Trim();
            query = query.Where(x => x.상태 == status);
        }

        var entities = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        // 화물 원장의 명시 의뢰 관계만 일괄 조회합니다. 음식 운송이나 같은 운송번호의 다른 의뢰로 전파하지 않습니다.
        var requestIds = entities.Where(x => x.배차업무유형 == 상태값.배차업무유형.용달운송)
            .Select(x => string.IsNullOrWhiteSpace(x.의뢰Id) ? x.운송번호 : x.의뢰Id)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        var requests = await _db.화주운송의뢰.AsNoTracking()
            .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);
        var locationRequestIds = entities.Where(x => x.배차업무유형 == 상태값.배차업무유형.용달운송)
            .Select(x => x.의뢰Id).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var locations = await 현재배정기사위치조회.조회Async(_db, locationRequestIds, cancellationToken);

        return entities
            .Select(entity =>
            {
                var response = Map(entity);
                response.기사위치상태Code = 기사위치상태Codes.Unavailable;
                if (entity.배차업무유형 == 상태값.배차업무유형.용달운송)
                {
                    var requestId = string.IsNullOrWhiteSpace(entity.의뢰Id) ? entity.운송번호 : entity.의뢰Id;
                    requests.TryGetValue(requestId, out var source);
                    화물운송조건Projector.투영(response, source, requestId);
                    if (locations.TryGetValue(entity.의뢰Id, out var location) && location.운송Id == entity.Id)
                    {
                        response.기사위치상태Code = location.상태Code;
                        response.기사최근위도 = location.위치?.위도;
                        response.기사최근경도 = location.위치?.경도;
                        response.기사최근위치시각Utc = location.위치?.기록시각;
                    }
                }
                return response;
            })
            .ToArray();
    }

    private static 운송진행응답 Map(운송원장 entity)
    {
        var 예외 = ExtractLatestException(entity.첨부_json);
        return new 운송진행응답
        {
            Id = entity.Id,
            운송번호 = entity.운송번호,
            상태 = entity.상태,
            출발_픽업 = entity.출발_픽업,
            도착 = entity.도착,
            기사_운송자 = entity.기사_운송자,
            출발지 = entity.출발지,
            도착지 = entity.도착지,
            운임 = entity.운임,
            예외신고됨 = 예외 is not null,
            최근예외단계 = 예외?.단계 ?? string.Empty,
            최근예외코드 = 예외?.예외코드 ?? string.Empty,
            최근예외메시지 = 예외?.메시지 ?? string.Empty,
            관리자확인필요 = 예외?.관리자확인필요 ?? false,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static 운송예외요약? ExtractLatestException(string? attachmentJson)
    {
        if (string.IsNullOrWhiteSpace(attachmentJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(attachmentJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            운송예외요약? latest = null;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (GetString(item, "kind") != "transport-field-exception")
                {
                    continue;
                }

                latest = new 운송예외요약(
                    GetString(item, "stage") ?? string.Empty,
                    GetString(item, "exceptionCode") ?? string.Empty,
                    GetString(item, "reason") ?? string.Empty,
                    GetBool(item, "adminReviewRequired"));
            }

            return latest;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool GetBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private sealed record 운송예외요약(string 단계, string 예외코드, string 메시지, bool 관리자확인필요);
}
