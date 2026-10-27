using FDriverApp.Services;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Driver.Notification;
using DriverWorkKinds = Ssalddel.Contracts.Driver.Settings.DriverWorkKinds;

namespace Ssalddel.Tests.Clients;

public sealed class NotificationAlertPolicyTests
{
    [Theory]
    [InlineData(-1, false, false)] [InlineData(0, false, false)] [InlineData(1, false, false)]
    [InlineData(-1, true, false)] [InlineData(0, true, false)] [InlineData(1, true, true)]
    public void 설정저장중에는명시적사후조회만_허용캐시를복원한다(int offset, bool explicitRead, bool expected)
    {
        var began = DateTime.UtcNow;
        var fence = new FDriverNotificationSettingsFence("driver", began);
        Assert.True(FDriverNotificationPublicationGate.HasPendingSettingsChange(fence, "driver"));
        Assert.Equal(expected, FDriverNotificationPublicationGate.CanApplySettings(fence, "driver", began.AddSeconds(offset), explicitRead));
    }

    [Fact]
    public void 설정확인후에도_확인전시작한겹친조회는_다시적용하지않는다()
    {
        var began = DateTime.UtcNow; var confirmed = began.AddSeconds(3);
        var persisted = System.Text.Json.JsonSerializer.Deserialize<FDriverNotificationSettingsFence>(
            System.Text.Json.JsonSerializer.Serialize(new FDriverNotificationSettingsFence("driver", began, confirmed)))!;
        Assert.False(FDriverNotificationPublicationGate.HasPendingSettingsChange(persisted, "driver"));
        Assert.False(FDriverNotificationPublicationGate.CanApplySettings(persisted, "driver", began.AddSeconds(1), false));
        Assert.True(FDriverNotificationPublicationGate.CanApplySettings(persisted, "driver", confirmed.AddSeconds(1), false));
        Assert.True(FDriverNotificationPublicationGate.CanApplySettings(persisted, "other", began.AddSeconds(-1), false));
    }

    [Fact]
    public void 취소되거나계정이변경된게시와_현재계정검사도중취소된게시를중단한다()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); var calls = 0;
        Assert.False(FDriverNotificationPublicationGate.CanContinuePublication(cancelled.Token, () => { calls++; return true; }));
        Assert.Equal(0, calls);
        Assert.False(FDriverNotificationPublicationGate.CanContinuePublication(CancellationToken.None, static () => false));
        using var duringCheck = new CancellationTokenSource();
        Assert.False(FDriverNotificationPublicationGate.CanContinuePublication(duringCheck.Token, () => { duringCheck.Cancel(); return true; }));
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(false, true, false)]
    [InlineData(true, false, false)] [InlineData(true, true, true)]
    public void 음식추천은전체허용과음식허용을모두확인하고_화물허용으로대체하지않는다(bool all, bool food, bool expected)
    {
        var settings = new 기사알림설정응답 { 배차추천알림사용 = all, 음식배달추천알림사용 = food, 화물운송추천알림사용 = true };
        Assert.Equal(expected, FDriverNotificationPublicationGate.AllowsFoodAlert(settings));
    }

    [Fact]
    public void 업무별허용을확인하지못하면_이전전체허용만으로음식알림을보내지않는다()
    {
        Assert.False(FDriverNotificationPublicationGate.AllowsFoodAlert(null));
        Assert.False(FDriverNotificationPublicationGate.AllowsFoodAlert(new() { 배차추천알림사용 = true }));
    }

    [Theory]
    [InlineData(DriverWorkKinds.FoodDelivery, true, false, true)]
    [InlineData(DriverWorkKinds.QuickDelivery, true, false, false)]
    [InlineData(DriverWorkKinds.QuickDelivery, false, true, true)]
    [InlineData(DriverWorkKinds.FoodDelivery, false, true, false)]
    [InlineData(DriverWorkKinds.CargoTransport, true, true, false)]
    [InlineData("unknown", true, true, false)]
    [InlineData(null, true, true, false)]
    public void 음식앱의마트퀵은화물알림설정을따르고_일반화물이나미확인업무를게시하지않는다(string? kind, bool food, bool cargo, bool expected)
    {
        var settings = new 기사알림설정응답 { 배차추천알림사용 = true, 음식배달추천알림사용 = food, 화물운송추천알림사용 = cargo };
        Assert.Equal(expected, FDriverNotificationPublicationGate.AllowsAlert(settings, kind));
        settings.배차추천알림사용 = false;
        Assert.False(FDriverNotificationPublicationGate.AllowsAlert(settings, kind));
    }

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
