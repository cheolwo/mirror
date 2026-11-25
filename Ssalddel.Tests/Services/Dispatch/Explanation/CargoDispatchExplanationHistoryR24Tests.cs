using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Ssalddel.Tests.Services.Dispatch.Explanation;

public sealed partial class CargoDispatchExplanationR24Tests
{
    [Fact]
    public async Task 뒤에다른기사추천이백개넘게쌓여도_이전기사본인역사를미기록으로숨기지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var own = await f.RecordAsync();
        const string laterDriver = "later-driver-r24";
        f.Queue.현재추천대상기사Id = laterDriver;
        for (var round = 2; round <= 103; round++)
        {
            f.Queue.추천라운드 = round;
            await f.Db.SaveChangesAsync();
            var reserved = await f.Recorder.ReserveAsync(f.Queue, laterDriver, Now.AddSeconds(round),
                "engine/history-limit-r24", null);
            Assert.NotNull(reserved);
            await f.Db.SaveChangesAsync();
        }
        Assert.Equal(103, await f.Db.운송이벤트.CountAsync());

        var response = await f.Service.GetAsync(Driver, f.Request.의뢰Id);
        var item = Assert.Single(response.Items);
        Assert.Equal("recorded", response.Status);
        Assert.Equal(own.Id.ToString(CultureInfo.InvariantCulture), item.EvidenceId);
        Assert.Equal(1, item.RecommendationRound);
        Assert.True(item.CanSubmitFeedback);
        Assert.DoesNotContain(response.Items, x => x.RecommendationRound > 1);

        var feedback = await f.Service.SubmitFeedbackAsync(Driver, f.Request.의뢰Id, Feedback(own));
        Assert.Equal(item.EvidenceId, feedback.EvidenceId);
        Assert.Equal(feedback.FeedbackId, Assert.Single((await f.Service.GetAsync(Driver, f.Request.의뢰Id)).Items.Single().MyFeedback).FeedbackId);
        Assert.Equal(100, (await f.Service.GetAsync(Owner, f.Request.의뢰Id)).Items.Count);
        Assert.Equal(100, (await f.Service.GetAsync(laterDriver, f.Request.의뢰Id)).Items.Count);
        Assert.Equal(103, await f.Db.운송이벤트.CountAsync());
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }
}
