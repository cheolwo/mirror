using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityDriverAvailabilityServiceTests
{
    [Fact]
    public void Publish_ExposesMaskedProfileWithoutDriverIdContactOrExactLocation()
    {
        var service = CreateService();

        var created = service.Publish(NewPost("driver-secret-1", "김민수"));
        var listed = Assert.Single(service.GetActive().Items);
        var flattened = string.Join("|", listed.PostId, listed.MaskedDriverDisplayName, listed.VehicleSummary, listed.OperatingAreaLabel);

        Assert.Equal(created.PostId, listed.PostId);
        Assert.Equal("김○○ 기사", listed.MaskedDriverDisplayName);
        Assert.DoesNotContain("driver-secret-1", flattened);
        Assert.False(listed.ContactDetailsDisclosed);
        Assert.False(listed.ExactLocationDisclosed);
        Assert.True(listed.CanReceiveDirectInquiries);
    }

    [Fact]
    public void PublishAgain_ClosesPreviousPostAndPendingInquiry()
    {
        var service = CreateService();
        var first = service.Publish(NewPost("driver-1", "김민수"));
        var inquiry = service.CreateInquiry(first.PostId, "buyer-1", "주문자", NewInquiry());

        var second = service.Publish(NewPost("driver-1", "김민수"));

        Assert.NotEqual(first.PostId, second.PostId);
        Assert.Equal(second.PostId, Assert.Single(service.GetActive().Items).PostId);
        var updated = Assert.Single(service.GetRequesterInquiries("buyer-1"));
        Assert.Equal(inquiry.InquiryId, updated.InquiryId);
        Assert.Equal(CommunityDriverInquiryStatusCodes.DriverUnavailable, updated.StatusCode);
    }

    [Fact]
    public void CreateInquiry_RejectsDriverSelfRequestAndContactDetails()
    {
        var service = CreateService();
        var post = service.Publish(NewPost("driver-1", "김민수"));

        var selfError = Assert.Throws<InvalidOperationException>(() => service.CreateInquiry(
            post.PostId,
            "driver-1",
            "기사",
            NewInquiry()));
        Assert.Contains("본인의 운행", selfError.Message);

        var request = NewInquiry();
        request.PublicMessage = "연락처는 010-1234-5678 입니다.";
        var contactError = Assert.Throws<ArgumentException>(() => service.CreateInquiry(
            post.PostId,
            "buyer-1",
            "주문자",
            request));
        Assert.Contains("공개할 수 없습니다", contactError.Message);
    }

    [Fact]
    public void Decide_OnlyTargetDriverCanAccept_AndRequesterSeesResult()
    {
        var service = CreateService();
        var post = service.Publish(NewPost("driver-1", "김민수"));
        var created = service.CreateInquiry(
            post.PostId,
            "buyer-1",
            "주문자",
            NewInquiry(sourceCampaignId: Guid.NewGuid()));

        Assert.Throws<KeyNotFoundException>(() => service.Decide(
            "driver-2",
            created.InquiryId,
            new CommunityDriverInquiryDecisionRequest { DecisionCode = CommunityDriverInquiryDecisionCodes.Accept }));

        var accepted = service.Decide(
            "driver-1",
            created.InquiryId,
            new CommunityDriverInquiryDecisionRequest
            {
                DecisionCode = CommunityDriverInquiryDecisionCodes.Accept,
                DriverPublicMessage = "금요일 오전 운행 가능합니다."
            });

        Assert.Equal(CommunityDriverInquiryStatusCodes.Accepted, accepted.StatusCode);
        Assert.Contains("정식 운송 의뢰", accepted.NextStepMessage);
        Assert.Equal("익명 공동구매 대표·구성원", accepted.RequesterRoleLabel);
        Assert.Equal(accepted.StatusCode, Assert.Single(service.GetRequesterInquiries("buyer-1")).StatusCode);
        Assert.False(accepted.ContactDetailsDisclosed);
    }

    [Fact]
    public void GetActive_After18Hours_AutoClosesAvailability()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock);
        service.Publish(NewPost("driver-1", "김민수", clock.UtcNow));

        clock.Advance(TimeSpan.FromHours(19));

        Assert.Empty(service.GetActive().Items);
    }

    [Fact]
    public void UpdateDistrictLocation_RequiresConsent_AndStoresOnlySidoSigungu()
    {
        var service = CreateService();
        service.Publish(NewPost("driver-1", "김민수"));

        Assert.Null(service.UpdateDistrictLocation("driver-1", "서울특별시", "중랑구"));

        service.Publish(NewPost("driver-1", "김민수", districtConsent: true));
        var updated = service.UpdateDistrictLocation("driver-1", "서울특별시", "중랑구");

        Assert.NotNull(updated);
        Assert.Equal("서울특별시 중랑구", updated.CurrentDistrictLabel);
        Assert.True(updated.DistrictLocationConsentGranted);
        Assert.Equal(CommunityDriverLocationConsentPolicy.CurrentVersion, updated.DistrictLocationConsentPolicyVersion);
        Assert.NotNull(updated.DistrictLocationConsentRecordedAtUtc);
        Assert.NotNull(updated.DistrictLocationUpdatedAtUtc);
        Assert.False(updated.ExactLocationDisclosed);
    }

    [Fact]
    public void UpdateDistrictLocation_RejectsNeighborhoodLevelText()
    {
        var service = CreateService();
        service.Publish(NewPost("driver-1", "김민수", districtConsent: true));

        var error = Assert.Throws<ArgumentException>(() => service.UpdateDistrictLocation(
            "driver-1",
            "서울특별시",
            "중랑구 면목동"));

        Assert.Contains("형식이 올바르지 않습니다", error.Message);
    }

    private static CommunityDriverAvailabilityService CreateService(FakeClock? clock = null)
        => new(clock ?? new FakeClock(DateTimeOffset.UtcNow));

    private static CommunityDriverAvailabilityPublishRequest NewPost(
        string driverId,
        string name,
        DateTimeOffset? startedAtUtc = null,
        bool districtConsent = false)
        => new(
            driverId,
            100,
            name,
            "1톤 카고",
            "파주·고양",
            startedAtUtc ?? DateTimeOffset.UtcNow,
            districtConsent);

    private static CommunityDriverInquiryCreateRequest NewInquiry(Guid? sourceCampaignId = null)
        => new()
        {
            CargoSummary = "감자 공동구매 물량",
            QuantitySummary = "500kg",
            PickupAreaLabel = "파주시",
            DropoffAreaLabel = "고양시 일산서구",
            RequestedPickupWindow = "금요일 오전 9~11시",
            PublicMessage = "10kg 상자 50개 운송 가능 여부를 확인하고 싶습니다.",
            SourceGroupPurchaseCampaignId = sourceCampaignId,
            SourceContextLabel = sourceCampaignId.HasValue ? "감자 공동구매" : null
        };

    private sealed class FakeClock : ICommunityDriverAvailabilityClock
    {
        public FakeClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan duration)
            => UtcNow = UtcNow.Add(duration);
    }
}
