using System.Security.Cryptography;
using System.Text;
using Hongdal.Contracts.Common.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.Options;
using 홍달.도메인.공통;

namespace Hongdal.Services.Community;

public sealed record CommunityDynamicTopicMatch(
    string DomainKey,
    string DomainDisplayName,
    string TopicKey,
    string DisplayName,
    string Summary,
    IReadOnlyList<string> MatchedSignals);

public interface ICommunityDynamicTopicClassifier
{
    IReadOnlyList<CommunityDynamicTopicMatch> Classify(
        string? title,
        string? body,
        string? category = null,
        string? workflowTag = null);
}

public sealed class CommunityDynamicTopicClassifier : ICommunityDynamicTopicClassifier
{
    private static readonly IReadOnlyList<(string TopicKey, string[] Signals)> Rules =
    [
        (CommunityDynamicTopicCodes.WarehouseInbound,
            ["입고", "입고예정", "입고 예정", "입고원장", "입고 원장", "검수입고", "inbound", "receiving"]),
        (CommunityDynamicTopicCodes.WarehouseOutbound,
            ["출고", "출고예정", "출고 예정", "출고원장", "출고 원장", "피킹", "포장출고", "outbound", "warehouse shipping"]),
        (CommunityDynamicTopicCodes.IndividualOrder,
            ["개별주문", "개별 주문", "개인주문", "개인 주문", "단독주문", "individual order"]),
        (CommunityDynamicTopicCodes.GroupOrder,
            ["공동주문", "공동 주문", "공동구매", "공동 구매", "group order", "group purchase"]),
        (CommunityDynamicTopicCodes.Food,
            ["음식", "음식점", "식당", "맛집", "메뉴", "요리", "반찬", "식재료", "배달음식", "food", "restaurant", "recipe"]),
        (CommunityDynamicTopicCodes.Cargo,
            ["화물", "용달", "트럭", "포워더", "cargo", "freight", "truck", "forwarder"]),
        (CommunityDynamicTopicCodes.TransportLoading,
            ["상차", "상차예정", "상차 예정", "픽업", "집하", "loading", "pickup"]),
        (CommunityDynamicTopicCodes.TransportUnloading,
            ["하차", "하차예정", "하차 예정", "도착지 인수", "인수확인", "unloading", "dropoff"])
    ];

    public IReadOnlyList<CommunityDynamicTopicMatch> Classify(
        string? title,
        string? body,
        string? category = null,
        string? workflowTag = null)
    {
        var text = $"{title}\n{body}\n{category}\n{workflowTag}";
        var matches = new List<CommunityDynamicTopicMatch>();
        foreach (var rule in Rules)
        {
            AddIfMatched(matches, CommunityDynamicTopicCatalog.Find(rule.TopicKey)!, rule.Signals, text);
        }

        return matches;
    }

    private static void AddIfMatched(
        ICollection<CommunityDynamicTopicMatch> target,
        CommunityDynamicTopicDefinition definition,
        IEnumerable<string> signals,
        string text)
    {
        var matched = signals
            .Where(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (matched.Length > 0)
        {
            target.Add(new(
                definition.DomainKey,
                definition.DomainDisplayName,
                definition.TopicKey,
                definition.DisplayName,
                definition.Summary,
                matched));
        }
    }
}

public interface ICommunityDynamicDiscoveryService
{
    CommunityDynamicTopicCatalogResponse GetCatalog();

    Task<CommunityPostContextDiscoveryResponse> DiscoverAsync(
        CommunityPostOpportunitySource source,
        CommunityPostContextDiscoveryRequest? request,
        CancellationToken cancellationToken = default);

    Task<CommunityDynamicTopicFeedResponse?> GetFeedAsync(
        string topicKey,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public sealed class CommunityDynamicDiscoveryService : ICommunityDynamicDiscoveryService
{
    private readonly HongdalContext _db;
    private readonly ICommunityDynamicTopicClassifier _classifier;
    private readonly ICommunityNearbyRestaurantDirectory _restaurantDirectory;
    private readonly CommunityContextDiscoveryOptions _options;

    public CommunityDynamicDiscoveryService(
        HongdalContext db,
        ICommunityDynamicTopicClassifier classifier,
        ICommunityNearbyRestaurantDirectory restaurantDirectory,
        IOptions<CommunityContextDiscoveryOptions> options)
    {
        _db = db;
        _classifier = classifier;
        _restaurantDirectory = restaurantDirectory;
        _options = options.Value;
    }

    public CommunityDynamicTopicCatalogResponse GetCatalog()
        => new()
        {
            GenerationPolicy =
                "사용자 게시글을 창고·주문·판매·운송 업무와 세부 주제로 읽기 전용 분류하며 원문 게시판은 변경하지 않습니다.",
            Domains = CommunityDynamicTopicCatalog.All
                .GroupBy(definition => new { definition.DomainKey, definition.DomainDisplayName })
                .Select(group => new CommunityDynamicTopicDomainResponse
                {
                    DomainKey = group.Key.DomainKey,
                    DisplayName = group.Key.DomainDisplayName,
                    Topics = group.OrderBy(definition => definition.SortOrder)
                        .Select(definition => BuildTopicResponse(definition, []))
                        .ToArray()
                })
                .ToArray()
        };

    public async Task<CommunityPostContextDiscoveryResponse> DiscoverAsync(
        CommunityPostOpportunitySource source,
        CommunityPostContextDiscoveryRequest? request,
        CancellationToken cancellationToken = default)
    {
        var topics = source.IsReportBoardPost
            ? []
            : BuildTopicResponses(_classifier.Classify(
                source.Title,
                source.Body,
                source.Category,
                source.WorkflowTag));
        var topicKeys = topics.Select(topic => topic.TopicKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var maximumRadius = Math.Clamp(_options.MaximumNearbyRadiusKm, 0.1m, 7m);
        var requestedRadius = request?.RadiusKm ?? maximumRadius;
        var appliedRadius = Math.Clamp(requestedRadius, 0.1m, maximumRadius);
        var locationProvided = request?.CurrentLatitude.HasValue == true
                               && request.CurrentLongitude.HasValue;
        var consent = request?.ConfirmTransientLocationUse == true;
        var restaurants = Array.Empty<CommunityNearbyRestaurantCandidateResponse>();
        var restaurantSourceAvailable = false;
        var simulationRestaurantSource = _options.RestaurantSourceIsSimulation;

        if (topicKeys.Contains(CommunityDynamicTopicCodes.Food)
            && locationProvided
            && consent)
        {
            ValidateCoordinates(request!.CurrentLatitude!.Value, request.CurrentLongitude!.Value);
            var lookup = await _restaurantDirectory.FindAsync(
                request.CurrentLatitude.Value,
                request.CurrentLongitude.Value,
                appliedRadius,
                Math.Clamp(_options.RestaurantCandidateLimit, 1, 50),
                cancellationToken);
            restaurantSourceAvailable = lookup.SourceAvailable;
            simulationRestaurantSource = lookup.IsSimulationSource;
            restaurants = lookup.Items
                .Where(item => item.거리Km is >= 0m && item.거리Km <= appliedRadius)
                .OrderBy(item => item.거리Km)
                .Select(item => new CommunityNearbyRestaurantCandidateResponse
                {
                    RestaurantId = item.Id,
                    Name = item.상호명,
                    Category = item.카테고리,
                    AreaSummary = AreaSummary(item.주소),
                    DistanceKm = item.거리Km ?? 0m,
                    AverageRating = item.평균평점,
                    ReviewCount = item.리뷰수,
                    OrderAvailable = item.주문가능여부,
                    SourceCode = "Hongdal.FoodApi"
                })
                .ToArray();
        }

        var freightContext = topicKeys.Contains(CommunityDynamicTopicCodes.Cargo)
                             || topicKeys.Contains(CommunityDynamicTopicCodes.TransportLoading)
                             || topicKeys.Contains(CommunityDynamicTopicCodes.TransportUnloading);
        var providers = freightContext
            ? await FindFreightProviderCandidatesAsync(cancellationToken)
            : [];
        var publicFreight = freightContext
            ? await FindPublicFreightCandidatesAsync(cancellationToken)
            : [];

        return new CommunityPostContextDiscoveryResponse
        {
            PostId = source.PostId,
            DynamicTopics = topics,
            LocationPolicy = new CommunityTransientLocationPolicyResponse
            {
                MaximumRadiusKm = maximumRadius,
                AppliedRadiusKm = appliedRadius,
                ConsentConfirmed = consent,
                LocationProvided = locationProvided,
                LocationPersisted = false,
                RestaurantSourceAvailable = restaurantSourceAvailable,
                RestaurantSourceIsSimulation = simulationRestaurantSource,
                Notice = consent && locationProvided
                    ? "현재 위치는 이 조회의 거리 계산에만 사용하며 게시글이나 원장에 저장하지 않습니다."
                    : "주변 음식점은 위치의 일시 사용에 명시적으로 동의하고 위도·경도를 함께 제공할 때만 조회합니다."
            },
            NearbyRestaurants = restaurants,
            FreightProviderCandidates = providers,
            PublicFreightCandidates = publicFreight,
            InformationOnly = true,
            IsBrokerageEnabled = false,
            AutomaticallySelectsProvider = false,
            AutomaticallyDispatchesFreight = false,
            FacilitatorBoundaryNotice =
                "플랫폼은 대화에서 관련 정보를 모아 보여 줄 뿐 운송사를 선정하거나 운임·배차를 확정하지 않습니다. " +
                "주선 역할의 면허·등록과 계약은 당사자가 별도로 확인해야 합니다."
        };
    }

    public async Task<CommunityDynamicTopicFeedResponse?> GetFeedAsync(
        string topicKey,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var definition = CommunityDynamicTopicCatalog.Find(topicKey);
        if (definition is null)
        {
            return null;
        }

        var normalizedTopicKey = definition.TopicKey;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var protectedCategories = CommunityBoardCatalog.CategoryNamesFor(CommunityBoardKeys.SafetyReport);
        var query = _db.PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted
                           && !post.IsReportBoardPost
                           && !protectedCategories.Contains(post.Category));

        query = normalizedTopicKey switch
        {
            CommunityDynamicTopicCodes.WarehouseInbound => query.Where(post =>
                post.Title.Contains("입고")
                || post.Body.Contains("입고")
                || post.WorkflowTag.Contains("입고")
                || post.Title.Contains("inbound")
                || post.Body.Contains("inbound")
                || post.Title.Contains("receiving")
                || post.Body.Contains("receiving")),
            CommunityDynamicTopicCodes.WarehouseOutbound => query.Where(post =>
                post.Title.Contains("출고")
                || post.Body.Contains("출고")
                || post.WorkflowTag.Contains("출고")
                || post.Title.Contains("피킹")
                || post.Body.Contains("피킹")
                || post.Title.Contains("outbound")
                || post.Body.Contains("outbound")
                || post.Title.Contains("warehouse shipping")
                || post.Body.Contains("warehouse shipping")),
            CommunityDynamicTopicCodes.IndividualOrder => query.Where(post =>
                post.Title.Contains("개별주문")
                || post.Body.Contains("개별주문")
                || post.Title.Contains("개별 주문")
                || post.Body.Contains("개별 주문")
                || post.Title.Contains("개인 주문")
                || post.Body.Contains("개인 주문")
                || post.Title.Contains("개인주문")
                || post.Body.Contains("개인주문")
                || post.Title.Contains("단독주문")
                || post.Body.Contains("단독주문")
                || post.Title.Contains("individual order")
                || post.Body.Contains("individual order")),
            CommunityDynamicTopicCodes.GroupOrder => query.Where(post =>
                post.Title.Contains("공동주문")
                || post.Body.Contains("공동주문")
                || post.Title.Contains("공동 주문")
                || post.Body.Contains("공동 주문")
                || post.Title.Contains("공동구매")
                || post.Body.Contains("공동구매")
                || post.Title.Contains("공동 구매")
                || post.Body.Contains("공동 구매")
                || post.Title.Contains("group order")
                || post.Body.Contains("group order")
                || post.Title.Contains("group purchase")
                || post.Body.Contains("group purchase")),
            CommunityDynamicTopicCodes.Food => query.Where(post =>
                post.Category == CommunityBoardCatalog.Food.DisplayName
                || post.Category == "맛집"
                || post.Title.Contains("음식")
                || post.Body.Contains("음식")
                || post.Title.Contains("식당")
                || post.Body.Contains("식당")
                || post.Title.Contains("맛집")
                || post.Body.Contains("맛집")
                || post.Title.Contains("요리")
                || post.Body.Contains("요리")
                || post.Title.Contains("메뉴")
                || post.Body.Contains("메뉴")
                || post.Title.Contains("식재료")
                || post.Body.Contains("식재료")
                || post.Title.Contains("food")
                || post.Body.Contains("food")
                || post.Title.Contains("restaurant")
                || post.Body.Contains("restaurant")),
            CommunityDynamicTopicCodes.Cargo => query.Where(post =>
                post.Category == CommunityBoardCatalog.Cargo.DisplayName
                || post.Category == "화물 운송"
                || post.Title.Contains("화물")
                || post.Body.Contains("화물")
                || post.Title.Contains("용달")
                || post.Body.Contains("용달")
                || post.Title.Contains("cargo")
                || post.Body.Contains("cargo")
                || post.Title.Contains("freight")
                || post.Body.Contains("freight")
                || post.Title.Contains("truck")
                || post.Body.Contains("truck")
                || post.Title.Contains("forwarder")
                || post.Body.Contains("forwarder")),
            CommunityDynamicTopicCodes.TransportLoading => query.Where(post =>
                post.Title.Contains("상차")
                || post.Body.Contains("상차")
                || post.WorkflowTag.Contains("상차")
                || post.Title.Contains("픽업")
                || post.Body.Contains("픽업")
                || post.Title.Contains("집하")
                || post.Body.Contains("집하")
                || post.Title.Contains("loading")
                || post.Body.Contains("loading")
                || post.Title.Contains("pickup")
                || post.Body.Contains("pickup")),
            CommunityDynamicTopicCodes.TransportUnloading => query.Where(post =>
                post.Title.Contains("하차")
                || post.Body.Contains("하차")
                || post.WorkflowTag.Contains("하차")
                || post.Title.Contains("인수확인")
                || post.Body.Contains("인수확인")
                || post.Title.Contains("도착지 인수")
                || post.Body.Contains("도착지 인수")
                || post.Title.Contains("unloading")
                || post.Body.Contains("unloading")
                || post.Title.Contains("dropoff")
                || post.Body.Contains("dropoff")),
            _ => query
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(post => post.IsOperatorPinned)
            .ThenByDescending(post => post.LastEngagedAtUtc)
            .ThenByDescending(post => post.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(post => new
            {
                post.Id,
                post.Category,
                post.Title,
                post.Body,
                post.WorkflowTag,
                post.Nickname,
                post.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new CommunityDynamicTopicFeedResponse
        {
            DomainKey = definition.DomainKey,
            DomainDisplayName = definition.DomainDisplayName,
            TopicKey = normalizedTopicKey,
            DisplayName = definition.DisplayName,
            GenerationPolicy = "사용자 게시글의 제목·본문·게시판·업무 태그 신호를 읽기 전용으로 분류해 구성하며 원문 게시판을 변경하지 않습니다.",
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(row => new CommunityDynamicTopicFeedItemResponse
            {
                PostId = row.Id,
                Category = row.Category,
                Title = row.Title,
                Nickname = row.Nickname,
                CreatedAtUtc = row.CreatedAtUtc,
                MatchedSignals = _classifier
                    .Classify(row.Title, row.Body, row.Category, row.WorkflowTag)
                    .First(match => string.Equals(match.TopicKey, normalizedTopicKey, StringComparison.OrdinalIgnoreCase))
                    .MatchedSignals
            }).ToArray()
        };
    }

    private async Task<IReadOnlyList<CommunityFreightProviderCandidateResponse>> FindFreightProviderCandidatesAsync(
        CancellationToken cancellationToken)
    {
        var candidates = await (
                from participant in _db.홍달참여자.AsNoTracking()
                join userRole in _db.UserRoles.AsNoTracking() on participant.Id equals userRole.UserId
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where participant.활성화여부
                      && role.Name != null
                      && (role.Name == "화물운송주선업자"
                          || role.Name == "FreightBroker"
                          || role.Name == "RoadFreightBroker"
                          || role.Name == "국제물류주선업자"
                          || role.Name == "복합운송주선업자"
                          || role.Name == "MultimodalCoordinator")
                select new { participant.Id, participant.표시이름, RoleName = role.Name! })
            .ToListAsync(cancellationToken);

        return candidates
            .GroupBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.표시이름)
            .Take(Math.Clamp(_options.FreightProviderCandidateLimit, 1, 50))
            .Select(candidate => new CommunityFreightProviderCandidateResponse
            {
                CandidateKey = StableCandidateKey("provider", candidate.Id),
                DisplayName = string.IsNullOrWhiteSpace(candidate.표시이름) ? "등록 주선 역할 참여자" : candidate.표시이름,
                RoleCode = CommunityPostPartyRoleCodes.RoadFreightBroker,
                PlatformRoleVerified = true,
                ExternalLicenseVerificationRequired = true,
                VerificationNotice =
                    "플랫폼 역할 프로필만 확인했습니다. 실제 주선 계약 전 관할 면허·등록과 업무 범위를 별도로 확인해야 합니다."
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<CommunityPublicFreightCandidateResponse>> FindPublicFreightCandidatesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
                from ledger in _db.운송원장.AsNoTracking()
                join request in _db.화주운송의뢰.AsNoTracking() on ledger.의뢰Id equals request.의뢰Id
                where ledger.배차업무유형 == 상태값.배차업무유형.용달운송
                      && ledger.배차큐단계 == 상태값.배차큐단계.공개배차
                      && ledger.배차노출상태 == 상태값.배차노출상태.공개중
                      && ledger.확정기사Id == null
                orderby ledger.공개전환시각 descending, ledger.CreatedAt descending
                select new
                {
                    ledger.의뢰Id,
                    request.화물종류,
                    request.화물중량Kg,
                    request.차량종류,
                    ledger.픽업_도로명주소,
                    ledger.하차_도로명주소,
                    request.픽업_시간창_시작일시
                })
            .Take(Math.Clamp(_options.PublicFreightCandidateLimit, 1, 50))
            .ToListAsync(cancellationToken);

        return rows.Select(row => new CommunityPublicFreightCandidateResponse
        {
            CandidateKey = StableCandidateKey("freight", row.의뢰Id),
            CargoType = row.화물종류,
            CargoWeightKg = row.화물중량Kg,
            VehicleType = row.차량종류,
            PickupAreaSummary = AreaSummary(row.픽업_도로명주소),
            DropoffAreaSummary = AreaSummary(row.하차_도로명주소),
            PickupWindowStartUtc = row.픽업_시간창_시작일시,
            IsExplicitPublicDispatch = true,
            Notice = "공개배차 상태의 비식별 요약입니다. 이 노출만으로 운송계약·주선·배차가 성립하지 않습니다."
        }).ToArray();
    }

    private static IReadOnlyList<CommunityDynamicTopicResponse> BuildTopicResponses(
        IReadOnlyList<CommunityDynamicTopicMatch> matches)
        => matches.Select(match => new CommunityDynamicTopicResponse
        {
            DomainKey = match.DomainKey,
            DomainDisplayName = match.DomainDisplayName,
            TopicKey = match.TopicKey,
            DisplayName = match.DisplayName,
            Summary = match.Summary,
            FeedEndpoint = $"/api/v1/community/dynamic-topic-feeds/{match.TopicKey}",
            MatchedSignals = match.MatchedSignals
        }).ToArray();

    private static CommunityDynamicTopicResponse BuildTopicResponse(
        CommunityDynamicTopicDefinition definition,
        IReadOnlyList<string> matchedSignals)
        => new()
        {
            DomainKey = definition.DomainKey,
            DomainDisplayName = definition.DomainDisplayName,
            TopicKey = definition.TopicKey,
            DisplayName = definition.DisplayName,
            Summary = definition.Summary,
            FeedEndpoint = $"/api/v1/community/dynamic-topic-feeds/{definition.TopicKey}",
            MatchedSignals = matchedSignals
        };

    private static void ValidateCoordinates(decimal latitude, decimal longitude)
    {
        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
        {
            throw new InvalidOperationException("현재 위치의 위도 또는 경도 범위가 올바르지 않습니다.");
        }
    }

    private static string AreaSummary(string? address)
    {
        var parts = (address ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "지역 비공개",
            1 => parts[0],
            _ => $"{parts[0]} {parts[1]}"
        };
    }

    private static string StableCandidateKey(string prefix, string source)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        return $"{prefix}-{hash[..16].ToLowerInvariant()}";
    }
}
