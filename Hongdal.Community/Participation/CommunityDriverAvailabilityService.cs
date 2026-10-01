using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public sealed record CommunityDriverAvailabilityPublishRequest(
    string DriverId,
    long ShiftId,
    string DriverName,
    string VehicleSummary,
    string OperatingAreaLabel,
    DateTimeOffset StartedAtUtc,
    bool DistrictLocationConsentGranted = false);

public interface ICommunityDriverAvailabilityClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemCommunityDriverAvailabilityClock : ICommunityDriverAvailabilityClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface ICommunityDriverAvailabilityService
{
    CommunityDriverAvailabilityPostResponse Publish(CommunityDriverAvailabilityPublishRequest request);
    void Close(string driverId);
    CommunityDriverAvailabilityListResponse GetActive(string? operatingArea = null);
    bool HasDistrictLocationConsent(string driverId);
    CommunityDriverAvailabilityPostResponse? UpdateDistrictLocation(string driverId, string region1, string region2);
    CommunityDriverInquiryResponse CreateInquiry(Guid postId, string requesterUserId, string requesterRole, CommunityDriverInquiryCreateRequest request);
    IReadOnlyList<CommunityDriverInquiryResponse> GetRequesterInquiries(string requesterUserId);
    IReadOnlyList<CommunityDriverInquiryResponse> GetDriverInquiries(string driverId);
    CommunityDriverInquiryResponse Decide(string driverId, Guid inquiryId, CommunityDriverInquiryDecisionRequest request);
}

public sealed partial class CommunityDriverAvailabilityService : ICommunityDriverAvailabilityService
{
    private static readonly TimeSpan MaximumAvailabilityDuration = TimeSpan.FromHours(18);
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, AvailabilityState> posts = [];
    private readonly Dictionary<Guid, InquiryState> inquiries = [];
    private readonly ICommunityDriverAvailabilityClock clock;

    public CommunityDriverAvailabilityService(ICommunityDriverAvailabilityClock clock)
    {
        this.clock = clock;
    }

    public CommunityDriverAvailabilityPostResponse Publish(CommunityDriverAvailabilityPublishRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DriverId);
        lock (syncRoot)
        {
            CloseActivePosts(request.DriverId.Trim(), CommunityDriverInquiryStatusCodes.DriverUnavailable);
            var startedAt = request.StartedAtUtc == default ? clock.UtcNow : request.StartedAtUtc;
            var response = new CommunityDriverAvailabilityPostResponse
            {
                PostId = Guid.NewGuid(),
                MaskedDriverDisplayName = MaskDriverName(request.DriverName),
                VehicleSummary = SafeProfileText(request.VehicleSummary, "차량 정보 협의", 80),
                OperatingAreaLabel = SafeProfileText(request.OperatingAreaLabel, "활동 지역 협의", 100),
                DistrictLocationConsentGranted = request.DistrictLocationConsentGranted,
                DistrictLocationConsentPolicyVersion = request.DistrictLocationConsentGranted
                    ? CommunityDriverLocationConsentPolicy.CurrentVersion
                    : null,
                DistrictLocationConsentRecordedAtUtc = request.DistrictLocationConsentGranted ? clock.UtcNow : null,
                StartedAtUtc = startedAt,
                ExpiresAtUtc = startedAt.Add(MaximumAvailabilityDuration),
                CanReceiveDirectInquiries = true
            };
            posts[response.PostId] = new AvailabilityState(request.DriverId.Trim(), request.ShiftId, response);
            return CopyPost(response);
        }
    }

    public bool HasDistrictLocationConsent(string driverId)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return false;
        }

        lock (syncRoot)
        {
            return posts.Values.Any(item => string.Equals(item.DriverId, driverId.Trim(), StringComparison.Ordinal)
                                            && IsActive(item.PublicPost)
                                            && item.PublicPost.DistrictLocationConsentGranted);
        }
    }

    public CommunityDriverAvailabilityPostResponse? UpdateDistrictLocation(
        string driverId,
        string region1,
        string region2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        lock (syncRoot)
        {
            var post = posts.Values
                .Where(item => string.Equals(item.DriverId, driverId.Trim(), StringComparison.Ordinal)
                               && IsActive(item.PublicPost)
                               && item.PublicPost.DistrictLocationConsentGranted)
                .OrderByDescending(item => item.PublicPost.StartedAtUtc)
                .FirstOrDefault();
            if (post is null)
            {
                return null;
            }

            var sido = RequiredAdministrativeRegion(region1, "시·도", requireDistrictLevel: false);
            var sigungu = RequiredAdministrativeRegion(region2, "시·군·구", requireDistrictLevel: true);
            post.PublicPost.CurrentDistrictLabel = $"{sido} {sigungu}";
            post.PublicPost.DistrictLocationUpdatedAtUtc = clock.UtcNow;
            return CopyPost(post.PublicPost);
        }
    }

    public void Close(string driverId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        lock (syncRoot)
        {
            CloseActivePosts(driverId.Trim(), CommunityDriverInquiryStatusCodes.DriverUnavailable);
        }
    }

    public CommunityDriverAvailabilityListResponse GetActive(string? operatingArea = null)
    {
        lock (syncRoot)
        {
            CloseExpiredPosts();
            IEnumerable<AvailabilityState> query = posts.Values.Where(item => IsActive(item.PublicPost));
            if (!string.IsNullOrWhiteSpace(operatingArea))
            {
                var search = operatingArea.Trim();
                query = query.Where(item => item.PublicPost.OperatingAreaLabel.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            return new CommunityDriverAvailabilityListResponse
            {
                Items = query
                    .OrderByDescending(item => item.PublicPost.StartedAtUtc)
                    .Select(item => CopyPost(item.PublicPost))
                    .ToList(),
                GeneratedAtUtc = clock.UtcNow
            };
        }
    }

    public CommunityDriverInquiryResponse CreateInquiry(
        Guid postId,
        string requesterUserId,
        string requesterRole,
        CommunityDriverInquiryCreateRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requesterUserId);
        ArgumentNullException.ThrowIfNull(request);
        lock (syncRoot)
        {
            CloseExpiredPosts();
            if (!posts.TryGetValue(postId, out var post) || !IsActive(post.PublicPost))
            {
                throw new InvalidOperationException("현재 운행 중이며 의뢰를 받는 기사 공개 글이 아닙니다.");
            }

            if (string.Equals(post.DriverId, requesterUserId.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("본인의 운행 공개 글에는 운송 의뢰를 보낼 수 없습니다.");
            }

            if (inquiries.Values.Any(item => item.RequesterUserId == requesterUserId.Trim()
                                             && item.PublicInquiry.AvailabilityPostId == postId
                                             && item.PublicInquiry.StatusCode == CommunityDriverInquiryStatusCodes.Pending))
            {
                throw new InvalidOperationException("이 기사에게 이미 답변 대기 중인 의뢰가 있습니다.");
            }

            var now = clock.UtcNow;
            var sourceContext = OptionalPublicText(request.SourceContextLabel, "의뢰 출처", 160);
            var response = new CommunityDriverInquiryResponse
            {
                InquiryId = Guid.NewGuid(),
                AvailabilityPostId = postId,
                MaskedDriverDisplayName = post.PublicPost.MaskedDriverDisplayName,
                RequesterRoleLabel = ResolveRequesterRole(requesterRole, request.SourceGroupPurchaseCampaignId),
                CargoSummary = RequiredPublicText(request.CargoSummary, "화물 요약", 200),
                QuantitySummary = RequiredPublicText(request.QuantitySummary, "물량", 100),
                PickupAreaLabel = RequiredPublicText(request.PickupAreaLabel, "상차 지역", 160),
                DropoffAreaLabel = RequiredPublicText(request.DropoffAreaLabel, "하차 지역", 160),
                RequestedPickupWindow = RequiredPublicText(request.RequestedPickupWindow, "희망 상차 시간", 120),
                PublicMessage = RequiredPublicText(request.PublicMessage, "의뢰 메시지", 1000),
                SourceGroupPurchaseCampaignId = request.SourceGroupPurchaseCampaignId,
                SourceContextLabel = sourceContext,
                NextStepMessage = "기사가 수락하면 양측이 정식 운송 의뢰 원장의 상세 조건을 확인해야 합니다. 아직 배차 확정 상태는 아닙니다.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            inquiries[response.InquiryId] = new InquiryState(post.DriverId, requesterUserId.Trim(), response);
            return CopyInquiry(response);
        }
    }

    public IReadOnlyList<CommunityDriverInquiryResponse> GetDriverInquiries(string driverId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        lock (syncRoot)
        {
            CloseExpiredPosts();
            return inquiries.Values
                .Where(item => string.Equals(item.DriverId, driverId.Trim(), StringComparison.Ordinal))
                .OrderByDescending(item => item.PublicInquiry.CreatedAtUtc)
                .Select(item => CopyInquiry(item.PublicInquiry))
                .ToArray();
        }
    }

    public IReadOnlyList<CommunityDriverInquiryResponse> GetRequesterInquiries(string requesterUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requesterUserId);
        lock (syncRoot)
        {
            CloseExpiredPosts();
            return inquiries.Values
                .Where(item => string.Equals(item.RequesterUserId, requesterUserId.Trim(), StringComparison.Ordinal))
                .OrderByDescending(item => item.PublicInquiry.CreatedAtUtc)
                .Select(item => CopyInquiry(item.PublicInquiry))
                .ToArray();
        }
    }

    public CommunityDriverInquiryResponse Decide(
        string driverId,
        Guid inquiryId,
        CommunityDriverInquiryDecisionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        ArgumentNullException.ThrowIfNull(request);
        lock (syncRoot)
        {
            if (!inquiries.TryGetValue(inquiryId, out var state)
                || !string.Equals(state.DriverId, driverId.Trim(), StringComparison.Ordinal))
            {
                throw new KeyNotFoundException("기사에게 도착한 커뮤니티 운송 의뢰를 찾을 수 없습니다.");
            }

            if (state.PublicInquiry.StatusCode != CommunityDriverInquiryStatusCodes.Pending)
            {
                throw new InvalidOperationException("이미 답변한 의뢰입니다.");
            }

            state.PublicInquiry.StatusCode = request.DecisionCode switch
            {
                CommunityDriverInquiryDecisionCodes.Accept => CommunityDriverInquiryStatusCodes.Accepted,
                CommunityDriverInquiryDecisionCodes.Decline => CommunityDriverInquiryStatusCodes.Declined,
                _ => throw new ArgumentException("의뢰 답변은 수락 또는 거절이어야 합니다.", nameof(request))
            };
            state.PublicInquiry.DriverPublicMessage = OptionalPublicText(request.DriverPublicMessage, "기사 답변", 500);
            state.PublicInquiry.NextStepMessage = state.PublicInquiry.StatusCode == CommunityDriverInquiryStatusCodes.Accepted
                ? "기사가 의뢰 제안을 수락했습니다. 요청자는 정식 운송 의뢰 원장을 만들고 상하차 상세 주소·연락처·운임을 안전한 업무 단계에서 확정해야 합니다."
                : "기사가 이번 의뢰를 수행하기 어렵다고 답했습니다. 다른 운행 중 기사 또는 일반 배차 흐름을 이용해 주세요.";
            state.PublicInquiry.UpdatedAtUtc = clock.UtcNow;
            return CopyInquiry(state.PublicInquiry);
        }
    }

    private void CloseExpiredPosts()
    {
        foreach (var post in posts.Values.Where(item => IsActive(item.PublicPost) && item.PublicPost.ExpiresAtUtc <= clock.UtcNow))
        {
            ClosePost(post, CommunityDriverInquiryStatusCodes.DriverUnavailable);
        }
    }

    private void CloseActivePosts(string driverId, string pendingInquiryStatus)
    {
        foreach (var post in posts.Values.Where(item => item.DriverId == driverId && IsActive(item.PublicPost)))
        {
            ClosePost(post, pendingInquiryStatus);
        }
    }

    private void ClosePost(AvailabilityState post, string pendingInquiryStatus)
    {
        post.PublicPost.StatusCode = CommunityDriverAvailabilityStatusCodes.Closed;
        post.PublicPost.CanReceiveDirectInquiries = false;
        foreach (var inquiry in inquiries.Values.Where(item => item.PublicInquiry.AvailabilityPostId == post.PublicPost.PostId
                                                                && item.PublicInquiry.StatusCode == CommunityDriverInquiryStatusCodes.Pending))
        {
            inquiry.PublicInquiry.StatusCode = pendingInquiryStatus;
            inquiry.PublicInquiry.NextStepMessage = "기사의 운행 공개가 종료되어 이 제안은 자동 마감됐습니다.";
            inquiry.PublicInquiry.UpdatedAtUtc = clock.UtcNow;
        }
    }

    private static bool IsActive(CommunityDriverAvailabilityPostResponse post)
        => post.StatusCode == CommunityDriverAvailabilityStatusCodes.Active && post.CanReceiveDirectInquiries;

    private static string ResolveRequesterRole(string requesterRole, Guid? sourceCampaignId)
    {
        if (sourceCampaignId.HasValue)
        {
            return "익명 공동구매 대표·구성원";
        }

        return string.IsNullOrWhiteSpace(requesterRole)
            ? "익명 운송 요청자"
            : $"익명 {requesterRole.Trim()}";
    }

    private static string MaskDriverName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || ContainsContact(value))
        {
            return "운행 중 익명 기사";
        }

        var compact = new string(value.Trim().Where(character => !char.IsWhiteSpace(character)).ToArray());
        if (compact.Length < 2)
        {
            return "운행 중 익명 기사";
        }

        return $"{compact[0]}{new string('○', Math.Min(2, compact.Length - 1))} 기사";
    }

    private static string SafeProfileText(string? value, string fallback, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || ContainsContact(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static string RequiredPublicText(string? value, string fieldLabel, int maximumLength)
        => OptionalPublicText(value, fieldLabel, maximumLength)
           ?? throw new ArgumentException($"{fieldLabel}을(를) 입력해야 합니다.");

    private static string? OptionalPublicText(string? value, string fieldLabel, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
        {
            throw new ArgumentException($"{fieldLabel}은(는) {maximumLength}자 이하여야 합니다.");
        }

        if (ContainsContact(trimmed))
        {
            throw new ArgumentException($"{fieldLabel}에는 전화번호나 이메일을 공개할 수 없습니다.");
        }

        return trimmed;
    }

    private static string RequiredAdministrativeRegion(string? value, string fieldLabel, bool requireDistrictLevel)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldLabel} 행정구역이 필요합니다.");
        }

        var trimmed = value.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var containsSubdistrict = parts.Any(part => part.EndsWith("동", StringComparison.Ordinal)
                                                    || part.EndsWith("읍", StringComparison.Ordinal)
                                                    || part.EndsWith("면", StringComparison.Ordinal)
                                                    || part.EndsWith("리", StringComparison.Ordinal));
        var hasDistrictLevel = parts.Any(part => part.EndsWith("시", StringComparison.Ordinal)
                                                 || part.EndsWith("군", StringComparison.Ordinal)
                                                 || part.EndsWith("구", StringComparison.Ordinal));
        if (trimmed.Length > 40 || ContainsContact(trimmed)
            || trimmed.Any(character => char.IsDigit(character))
            || containsSubdistrict
            || (requireDistrictLevel && !hasDistrictLevel))
        {
            throw new ArgumentException($"{fieldLabel} 행정구역 형식이 올바르지 않습니다.");
        }

        return trimmed;
    }

    private static bool ContainsContact(string value)
        => EmailPattern().IsMatch(value) || PhonePattern().IsMatch(value);

    private static CommunityDriverAvailabilityPostResponse CopyPost(CommunityDriverAvailabilityPostResponse source)
        => new()
        {
            PostId = source.PostId,
            MaskedDriverDisplayName = source.MaskedDriverDisplayName,
            VehicleSummary = source.VehicleSummary,
            OperatingAreaLabel = source.OperatingAreaLabel,
            CurrentDistrictLabel = source.CurrentDistrictLabel,
            LocationDisclosureLevelCode = source.LocationDisclosureLevelCode,
            DistrictLocationConsentGranted = source.DistrictLocationConsentGranted,
            DistrictLocationConsentPolicyVersion = source.DistrictLocationConsentPolicyVersion,
            DistrictLocationConsentRecordedAtUtc = source.DistrictLocationConsentRecordedAtUtc,
            DistrictLocationUpdatedAtUtc = source.DistrictLocationUpdatedAtUtc,
            StatusCode = source.StatusCode,
            StartedAtUtc = source.StartedAtUtc,
            ExpiresAtUtc = source.ExpiresAtUtc,
            CanReceiveDirectInquiries = source.CanReceiveDirectInquiries
        };

    private static CommunityDriverInquiryResponse CopyInquiry(CommunityDriverInquiryResponse source)
        => new()
        {
            InquiryId = source.InquiryId,
            AvailabilityPostId = source.AvailabilityPostId,
            MaskedDriverDisplayName = source.MaskedDriverDisplayName,
            RequesterRoleLabel = source.RequesterRoleLabel,
            CargoSummary = source.CargoSummary,
            QuantitySummary = source.QuantitySummary,
            PickupAreaLabel = source.PickupAreaLabel,
            DropoffAreaLabel = source.DropoffAreaLabel,
            RequestedPickupWindow = source.RequestedPickupWindow,
            PublicMessage = source.PublicMessage,
            SourceGroupPurchaseCampaignId = source.SourceGroupPurchaseCampaignId,
            SourceContextLabel = source.SourceContextLabel,
            StatusCode = source.StatusCode,
            DriverPublicMessage = source.DriverPublicMessage,
            NextStepMessage = source.NextStepMessage,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };

    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<!\d)(?:01[016789]|0\d{1,2})[-\s]?\d{3,4}[-\s]?\d{4}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    private sealed record AvailabilityState(
        string DriverId,
        long ShiftId,
        CommunityDriverAvailabilityPostResponse PublicPost);

    private sealed record InquiryState(
        string DriverId,
        string RequesterUserId,
        CommunityDriverInquiryResponse PublicInquiry);
}
