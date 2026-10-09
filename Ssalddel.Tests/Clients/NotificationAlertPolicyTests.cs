using FDriverApp.Services;
using Ssalddel.Contracts.Common.Notifications;

namespace Ssalddel.Tests.Clients;

public sealed class NotificationAlertPolicyTests
{
    [Theory]
    [InlineData(6, 59, false)] [InlineData(7, 0, true)] [InlineData(21, 59, true)] [InlineData(22, 0, false)]
    public void QuietNight_UsesDisplayedKoreanTimeAndKeepsMessage(int hour, int minute, bool audible)
    {
        var plan = NotificationAlertPolicy.Evaluate(new(true, true, true, true), new(2026, 10, 8, hour, minute, 0, TimeSpan.FromHours(9)));
        Assert.True(plan.Enabled); Assert.Equal(audible, plan.Sound); Assert.Equal(audible, plan.Vibrate);
    }
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void SoundAndVibration_AreIndependent(bool sound, bool vibrate)
    {
        var plan = NotificationAlertPolicy.Evaluate(new(true, sound, vibrate), DateTimeOffset.UtcNow);
        Assert.Equal(sound, plan.Sound); Assert.Equal(vibrate, plan.Vibrate);
        Assert.Equal($"alert.s{(sound ? 1 : 0)}v{(vibrate ? 1 : 0)}", NotificationAlertPolicy.ChannelId("alert", plan));
    }
    [Fact]
    public void NewChannels_DoNotBypassLegacyMuteOrAnyChannelBlock()
    {
        var requested = new NotificationAlertPlan(true, true, true);
        var preserved = NotificationAlertPolicy.PreserveLegacy(requested, new("old", false, false, false), []);
        Assert.Equal(new(true, false, false), preserved);
        Assert.False(NotificationAlertPolicy.PreserveLegacy(requested, new("old", true, true, true), []).Enabled);
        Assert.False(NotificationAlertPolicy.PreserveLegacy(requested, null, [new("silent", true, false, false)]).Enabled);
    }
    [Fact]
    public void ChangingModes_DoesNotBypassUserMuteButScheduledSilentChannelDoesNotMuteDaytime()
    {
        var requested = new NotificationAlertPlan(true, true, true);
        Assert.Equal(requested, NotificationAlertPolicy.PreserveLegacy(requested, null, [new("alert.s0v0", false, false, false)]));
        Assert.False(NotificationAlertPolicy.PreserveLegacy(requested, null, [new("alert.s1v1", false, false, true)]).Sound);
        Assert.False(NotificationAlertPolicy.PreserveLegacy(requested, null, [new("alert.s1v1", false, true, false)]).Vibrate);
    }
    [Fact]
    public void PublishedOffer_DeduplicatesSameLiveOfferAndExpires()
    {
        var now = DateTime.UtcNow;
        var alerts = FDriverNotificationPublicationGate.Record([], "offer", now.AddMinutes(2), now);
        Assert.True(FDriverNotificationPublicationGate.IsDuplicate(alerts, "offer", now));
        Assert.False(FDriverNotificationPublicationGate.IsDuplicate(alerts, "other", now));
        Assert.False(FDriverNotificationPublicationGate.IsDuplicate(alerts, "offer", now.AddMinutes(3)));
        Assert.Single(FDriverNotificationPublicationGate.Record(alerts, "offer", now.AddMinutes(4), now));
    }
    [Fact]
    public void ExpiredPublicationIsRemovedAndHistoryIsBounded()
    {
        var now = DateTime.UtcNow;
        var alerts = Enumerable.Range(0, 120).Select(i => new FDriverPublishedAlert(i.ToString(), now.AddMinutes(1))).Append(new("expired", now.AddSeconds(-1)));
        var result = FDriverNotificationPublicationGate.Record(alerts, "new", now.AddMinutes(2), now);
        Assert.Equal(100, result.Count); Assert.DoesNotContain(result, x => x.OfferId == "expired"); Assert.Equal("new", result[^1].OfferId);
    }
}
