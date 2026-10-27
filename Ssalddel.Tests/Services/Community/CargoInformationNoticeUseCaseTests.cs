using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

public sealed class CargoInformationNoticeUseCaseTests
{
    [Fact]
    public async Task 기본비활성은_빈목록성공이나_저장호출로_대체하지않는다()
    {
        var fixture = new Fixture(enabled: false);
        Error(await fixture.Service.공개목록Async(), "CargoNotice.FeatureDisabled");
        Error(await fixture.Service.초안생성Async(Draft()), "CargoNotice.FeatureDisabled");
        Assert.Equal(0, fixture.Store.Calls);
    }

    [Fact]
    public async Task 초안은_명시동의와현재내용개정을_확인해야만_공개된다()
    {
        var f = new Fixture();
        var created = Value(await f.Service.초안생성Async(Draft()));
        Assert.Equal(CargoNoticeStatus.Draft, created.Notice.Status);
        Assert.Empty(Value(await f.Service.공개목록Async()).Items);
        var publish = Publish(created.Notice, f.Clock);
        publish.ConsentToPublicPublication = false;
        Error(await f.Service.공개Async(created.Notice.NoticeId, publish), "CargoNotice.PublicConsentRequired");
        publish.ConsentToPublicPublication = true; publish.ExpectedContentRevision++;
        Error(await f.Service.공개Async(created.Notice.NoticeId, publish), "CargoNotice.ContentRevisionConflict");
        publish.ExpectedContentRevision--;
        var applied = Value(await f.Service.공개Async(created.Notice.NoticeId, publish));
        Assert.Equal(2, applied.Notice.Revision);
        var publicItem = Assert.Single(Value(await f.Service.공개목록Async()).Items);
        Assert.Equal("서울특별시 중랑구 면목본동", publicItem.PickupRegionName);
        Assert.Equal(created.Notice.NoticeId, publicItem.NoticeId);
        Assert.Equal(0, typeof(CargoNoticePublicDto).GetProperties().Count(property =>
            property.Name.Contains("Owner", StringComparison.Ordinal) || property.Name.Contains("Transport", StringComparison.Ordinal)
            || property.Name.Contains("Contact", StringComparison.Ordinal) || property.Name.Contains("Payment", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task 공개내용을수정하면_초안으로돌아가고_다시동의해야한다()
    {
        var f = new Fixture(); var published = await f.Published();
        var update = Draft(); update.ExpectedRevision = published.Revision; update.Content.GoodsName = "밀봉 상자";
        var changed = Value(await f.Service.수정Async(published.NoticeId, update)).Notice;
        Assert.Equal(CargoNoticeStatus.Draft, changed.Status);
        Assert.Equal(2, changed.ContentRevision);
        Assert.Null(changed.ConsentedContentRevision);
        Assert.Null(changed.ExpiresAtUtc);
        Assert.Empty(Value(await f.Service.공개목록Async()).Items);
        Error(await f.Service.공개상세Async(changed.NoticeId), "CargoNotice.NotFound");
        Assert.Equal(CargoNoticeStatus.Published,
            Value(await f.Service.공개Async(changed.NoticeId, Publish(changed, f.Clock))).Notice.Status);
    }

    [Fact]
    public async Task 정확한만료시각과_철회는_공개목록과상세에서_즉시제외한다()
    {
        var f = new Fixture(); var published = await f.Published();
        f.Clock.UtcNow = published.ExpiresAtUtc!.Value;
        Assert.Empty(Value(await f.Service.공개목록Async()).Items);
        Error(await f.Service.공개상세Async(published.NoticeId), "CargoNotice.NotFound");
        Assert.Equal(CargoNoticeStatus.Expired, Value(await f.Service.내상세Async(published.NoticeId)).Status);
        var withdrawn = Value(await f.Service.철회Async(published.NoticeId,
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = published.Revision })).Notice;
        Assert.Equal(CargoNoticeStatus.Withdrawn, withdrawn.Status);
        Assert.Null(withdrawn.ConsentedContentRevision);
        Assert.Empty(Value(await f.Service.공개목록Async()).Items);
    }

    [Fact]
    public async Task 작성자와익명을분리하고_타인공고변경과요청결과를노출하지않는다()
    {
        var f = new Fixture(); var draft = Draft(); var created = Value(await f.Service.초안생성Async(draft));
        f.User.UserId = "other";
        Error(await f.Service.내상세Async(created.Notice.NoticeId), "CargoNotice.NotFound");
        Error(await f.Service.요청결과Async(draft.ClientRequestId), "CargoNotice.NotFound");
        Error(await f.Service.철회Async(created.Notice.NoticeId,
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = created.Notice.Revision }), "CargoNotice.NotFound");
        Assert.Empty(Value(await f.Service.내목록Async()).Items);
        f.User.UserId = null;
        Error(await f.Service.초안생성Async(Draft()), "CargoNotice.AuthenticationRequired");
        Error(await f.Service.내목록Async(), "CargoNotice.AuthenticationRequired");
        Assert.Empty(Value(await f.Service.공개목록Async()).Items);
    }

    [Fact]
    public async Task 같은요청은_후속수정뒤에도_원결과를돌려주고_다른입력은거절한다()
    {
        var f = new Fixture(); var request = Draft(); var created = Value(await f.Service.초안생성Async(request));
        var update = Draft(); update.ExpectedRevision = created.Notice.Revision;
        Value(await f.Service.수정Async(created.Notice.NoticeId, update));
        var replay = Value(await f.Service.초안생성Async(request));
        Assert.Equal(JsonSerializer.Serialize(created), JsonSerializer.Serialize(replay));
        Assert.Equal(2, Value(await f.Service.내상세Async(created.Notice.NoticeId)).Revision);
        Assert.Equal(1, Value(await f.Service.요청결과Async(request.ClientRequestId)).Notice.Revision);
        request.Content.Title = "다른 입력";
        Error(await f.Service.초안생성Async(request), "CargoNotice.RequestConflict");
    }

    [Fact]
    public async Task 같은요청번호를_다른공고나동작에_재사용할수없다()
    {
        var f = new Fixture(); var first = Value(await f.Service.초안생성Async(Draft()));
        var secondRequest = Draft(); var second = Value(await f.Service.초안생성Async(secondRequest));
        Error(await f.Service.철회Async(first.Notice.NoticeId,
            new() { ClientRequestId = secondRequest.ClientRequestId, ExpectedRevision = first.Notice.Revision }), "CargoNotice.RequestConflict");
        Assert.Equal(1, Value(await f.Service.내상세Async(first.Notice.NoticeId)).Revision);
        Assert.Equal(1, second.Notice.Revision);
    }

    [Fact]
    public async Task 같은개정의동시변경은_하나만확정하고_동일요청동시실행은하나의결과로수렴한다()
    {
        var f = new Fixture(); var draft = Draft();
        var same = await Task.WhenAll(f.Service.초안생성Async(draft), f.Service.초안생성Async(draft));
        Assert.All(same, response => Assert.True(response.IsSuccess));
        Assert.Single(f.Store.Rows);
        var notice = same[0].Value.Notice;
        var a = Draft(); a.ExpectedRevision = notice.Revision;
        var b = Draft(); b.ExpectedRevision = notice.Revision; b.Content.Title = "다른 제목";
        var results = await Task.WhenAll(f.Service.수정Async(notice.NoticeId, a), f.Service.수정Async(notice.NoticeId, b));
        Assert.Single(results, value => value.IsSuccess);
        Error(Assert.Single(results, value => value.IsFailed), "CargoNotice.RevisionConflict");
        Assert.Equal(2, Value(await f.Service.내상세Async(notice.NoticeId)).Revision);
    }

    [Theory]
    [InlineData("전화 010-1234-5678")]
    [InlineData("메일 user@example.com")]
    [InlineData("면목로 203")]
    [InlineData("101동 201호")]
    [InlineData("https://example.com")]
    public async Task 공개문구의대표개인정보형식을_저장전에거절한다(string text)
    {
        var f = new Fixture(); var request = Draft(); request.Content.Title = text;
        Error(await f.Service.초안생성Async(request), "CargoNotice.InvalidInput");
        Assert.Empty(f.Store.Rows);
    }

    [Theory]
    [InlineData("region")]
    [InlineData("quantity")]
    [InlineData("vehicle")]
    [InlineData("weight")]
    [InlineData("utc")]
    public async Task 지역과구조화입력을_추정으로대체하지않는다(string invalid)
    {
        var f = new Fixture(); var request = Draft();
        switch (invalid)
        {
            case "region": request.Content.PickupRegionKey = "region:private-address"; break;
            case "quantity": request.Content.Quantity = 0; break;
            case "vehicle": request.Content.VehicleKind = "unknown"; break;
            case "weight": request.Content.WeightKg = -1; break;
            case "utc": request.Content.DesiredFromUtc = f.Clock.UtcNow.ToOffset(TimeSpan.FromHours(9));
                request.Content.DesiredUntilUtc = request.Content.DesiredFromUtc.Value.AddHours(1); break;
        }
        Error(await f.Service.초안생성Async(request), "CargoNotice.InvalidInput");
        Assert.Empty(f.Store.Rows);
    }

    [Fact]
    public async Task 만료입력은_UTC와현재이후를강제하며_저장오류는빈목록성공으로숨기지않는다()
    {
        var f = new Fixture(); var created = Value(await f.Service.초안생성Async(Draft()));
        var request = Publish(created.Notice, f.Clock); request.ExpiresAtUtc = f.Clock.UtcNow;
        Error(await f.Service.공개Async(created.Notice.NoticeId, request), "CargoNotice.InvalidInput");
        request.ExpiresAtUtc = f.Clock.UtcNow.AddHours(1).ToOffset(TimeSpan.FromHours(9));
        Error(await f.Service.공개Async(created.Notice.NoticeId, request), "CargoNotice.InvalidInput");
        f.Store.FailReads = true;
        await Assert.ThrowsAsync<IOException>(() => f.Service.공개목록Async());
    }

    [Fact]
    public async Task 저장후응답유실은_새UseCase에서_원요청결과와재시도로_복구한다()
    {
        var f = new Fixture(); var request = Draft(); f.Store.LoseResponseOnce = true;
        await Assert.ThrowsAsync<IOException>(() => f.Service.초안생성Async(request));
        var restarted = f.CreateService();
        var receipt = Value(await restarted.요청결과Async(request.ClientRequestId));
        var retried = Value(await restarted.초안생성Async(request));
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(retried));
        Assert.Single(f.Store.Rows);
        Assert.Equal(1, receipt.Notice.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 이력한도여도_공개공고를한번철회하고_중복은원결과를읽으며_새변경은차단한다(bool expired)
    {
        var f = new Fixture(); var published = await f.Published();
        var row = f.Store.Rows[published.NoticeId];
        for (var index = row.Operations.Count; index < 256; index++)
            row.Operations.Add(new() { RequestKey = "previous-command-" + index, Fingerprint = "preserved-evidence" });
        if (expired) f.Clock.UtcNow = published.ExpiresAtUtc!.Value;
        var request = new CargoNoticeMutationRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = published.Revision };
        var result = Value(await f.Service.철회Async(published.NoticeId, request));
        Assert.Equal(CargoNoticeStatus.Withdrawn, result.Notice.Status);
        Assert.Equal(257, f.Store.Rows[published.NoticeId].Operations.Count);
        var same = Value(await f.Service.철회Async(published.NoticeId, request));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(same));
        Error(await f.Service.철회Async(published.NoticeId,
            new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = result.Notice.Revision }), "CargoNotice.RequestHistoryFull");
        var edit = Draft(); edit.ExpectedRevision = result.Notice.Revision;
        Error(await f.Service.수정Async(published.NoticeId, edit), "CargoNotice.RequestHistoryFull");
        Assert.Equal(257, f.Store.Rows[published.NoticeId].Operations.Count);
        Assert.Empty(Value(await f.Service.공개목록Async()).Items);
    }

    private static CargoNoticeDraftRequest Draft() => new()
    {
        ClientRequestId = Guid.NewGuid(), Content = new() { Title = "상자 운송 정보", GoodsName = "밀봉 생활용품",
            Quantity = 2, WeightKg = 4, VehicleKind = CargoNoticeVehicleKinds.Van,
            PickupRegionKey = "region:kr:hjd:1126056500", DropoffRegionKey = "region:kr:hjd:1126058000" }
    };
    private static CargoNoticePublishRequest Publish(CargoNoticeOwnerDto notice, Clock clock) => new()
    {
        ClientRequestId = Guid.NewGuid(), ExpectedRevision = notice.Revision, ExpectedContentRevision = notice.ContentRevision,
        ConsentToPublicPublication = true, ExpiresAtUtc = clock.UtcNow.AddHours(1)
    };
    private static T Value<T>(Result<T> result) { Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(x => x.Message))); return result.Value; }
    private static void Error<T>(Result<T> result, string code)
    { Assert.True(result.IsFailed); Assert.Contains(result.Errors, error => error.Metadata.GetValueOrDefault("Code")?.ToString() == code); }
    private sealed class User : ICurrentUserAccessor { public string? UserId { get; set; } = "owner"; public string? Role => "일반회원"; }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 25, 1, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => UtcNow; }
    private sealed class Fixture
    {
        public readonly Store Store = new(); public readonly User User = new(); public readonly Clock Clock = new();
        private readonly bool enabled; public readonly 화물정보공고UseCase Service;
        public Fixture(bool enabled = true) { this.enabled = enabled; Service = CreateService(); }
        public 화물정보공고UseCase CreateService() => new(Store, User, new Official생활교류공개지역Source(),
            Options.Create(new 화물정보공고Options { Enabled = enabled }), Clock);
        public async Task<CargoNoticeOwnerDto> Published()
        { var created = Value(await Service.초안생성Async(Draft())).Notice; return Value(await Service.공개Async(created.NoticeId, Publish(created, Clock))).Notice; }
    }
    private sealed class Store : I화물정보공고Store
    {
        private readonly object gate = new(); public readonly Dictionary<string, 화물정보공고문서> Rows = new();
        public int Calls; public bool FailReads; public bool LoseResponseOnce;
        private static 화물정보공고문서 Copy(화물정보공고문서 value) => JsonSerializer.Deserialize<화물정보공고문서>(JsonSerializer.Serialize(value))!;
        public Task<화물정보공고문서?> 조회Async(string id, CancellationToken ct = default)
        { lock (gate) { Calls++; return Task.FromResult(Rows.TryGetValue(id, out var row) ? Copy(row) : null); } }
        public Task<화물정보공고문서?> 요청조회Async(string key, CancellationToken ct = default)
        { lock (gate) { Calls++; var row = Rows.Values.SingleOrDefault(x => x.Operations.Any(op => op.RequestKey == key)); return Task.FromResult(row is null ? null : Copy(row)); } }
        public async Task<bool> 저장Async(화물정보공고문서 row, long expected, CancellationToken ct = default)
        {
            await Task.Yield();
            lock (gate)
            {
                Calls++;
                if ((Rows.GetValueOrDefault(row.NoticeId)?.Revision ?? 0) != expected) return false;
                if (Rows.Values.Where(x => x.NoticeId != row.NoticeId).Any(x => x.Operations.Any(op => row.Operations.Any(next => next.RequestKey == op.RequestKey)))) return false;
                Rows[row.NoticeId] = Copy(row);
                if (LoseResponseOnce) { LoseResponseOnce = false; throw new IOException("simulated response loss"); }
                return true;
            }
        }
        public Task<(IReadOnlyList<화물정보공고문서> Items, int Total)> 목록Async(string? owner, bool publicOnly,
            IReadOnlyList<string> allowed, DateTime now, int page, int pageSize, CancellationToken ct = default)
        {
            lock (gate)
            {
                Calls++; if (FailReads) throw new IOException("simulated store failure");
                var rows = Rows.Values.Where(x => owner is null || x.OwnerUserId == owner)
                    .Where(x => !publicOnly || x.Status == CargoNoticeStatus.Published && x.ExpiresAtUtc > now
                        && x.PublishedAtUtc <= now && x.ConsentedContentRevision == x.ContentRevision
                        && allowed.Contains(x.Content.PickupRegionKey) && allowed.Contains(x.Content.DropoffRegionKey))
                    .OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.NoticeId).ToArray();
                return Task.FromResult<(IReadOnlyList<화물정보공고문서>, int)>((rows.Skip((page - 1) * pageSize).Take(pageSize).Select(Copy).ToArray(), rows.Length));
            }
        }
    }
}
