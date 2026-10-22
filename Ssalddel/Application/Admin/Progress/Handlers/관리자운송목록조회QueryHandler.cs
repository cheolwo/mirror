using System.Text.Json;
using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Contracts.Admin.Progress;

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

        return entities
            .Select(Map)
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
