using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Domain.Community;
using Ssalddel.Domain.Notifications;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Services.Community;
using Ssalddel.Services.Privacy;
using Ssalddel.Services.PrivacySupport;
using Ssalddel.Services.Storage;
using SkiaSharp;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>실제 SQLite 원 의뢰·큐·판본·receipt 검증. 신원/경로/비공개 객체/FCM은 어댑터 계약 대역이며 실운영 증거가 아니다.</summary>
public sealed class NeighborhoodMemberDeliveryTests
{
    [Fact]
    public async Task 기본정책은닫히고명시적로컬검증만활성화된다()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.False(new NeighborhoodMemberDeliveryOptions().Enabled);
        var off = f.Service(enabled:false);
        Assert.False((await off.ParticipationAsync()).Enabled);
        Assert.Equal("MemberDeliveryDisabled", (await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => off.SaveParticipationAsync(f.Setting()))).Code);
        Assert.False(f.Service(mode:SsalddelExecutionMode.Operational, activation:false).Enabled);
        Assert.False(f.Service(readOnly:true).Enabled);
        Assert.True(f.Service().Enabled);
    }

    [Fact]
    public async Task 활동점은보호저장되고설정CAS와동일요청복구를한다()
    {
        await using var f = await Fixture.CreateAsync(); var request = f.Setting();
        var first = await f.Service().SaveParticipationAsync(request);
        Assert.Equal(1, first.Revision); Assert.Equal(3m, first.RadiusKm);
        Assert.DoesNotContain("37.57", (await f.Db.Set<NeighborhoodMemberParticipation>().SingleAsync()).PrivatePayload);
        await using var reopened = f.OpenDb();
        Assert.Equal(first.Revision, (await f.Service(reopened).SaveParticipationAsync(request)).Revision);
        request.ClientRequestId = Guid.NewGuid();
        Assert.Equal("MemberRevisionConflict", (await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => f.Service(reopened).SaveParticipationAsync(request))).Code);
        var off = f.Setting(); off.ReceiveOffers = false; off.ExpectedRevision = 1;
        Assert.False((await f.Service(reopened, enabled:false).SaveParticipationAsync(off)).ReceiveOffers);
    }

    [Theory]
    [InlineData("도보")]
    [InlineData("자전거")]
    public async Task 실제경로가없는이동수단은참여설정으로확정하지않는다(string vehicle)
    {
        await using var f = await Fixture.CreateAsync(); var request = f.Setting(); request.VehicleType = vehicle;
        Assert.Equal("MemberParticipationInvalid", (await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => f.Service().SaveParticipationAsync(request))).Code);
        Assert.Empty(await f.Db.Set<NeighborhoodMemberParticipation>().ToListAsync());
    }

    [Fact]
    public async Task 여러회원에게동시에제안하되주소와연락처는수락전에노출하지않는다()
    {
        await using var f = await Fixture.CreateAsync(); await f.Enroll("member-a"); await f.Enroll("member-b");
        await f.Enroll("far", latitude:38m); await f.Enroll("small", capacity:.1m); await f.Enroll("owner");
        await f.Register();
        Assert.Equal(2, await f.Db.Set<NeighborhoodMemberOffer>().CountAsync());
        f.Actor.UserId = "member-a"; var offer = Assert.Single(await f.Service().OffersAsync());
        var json = JsonSerializer.Serialize(offer);
        Assert.DoesNotContain("망우로", json); Assert.DoesNotContain("010", json);
        Assert.Equal(5000m, offer.FareKrw); Assert.Equal(0m, offer.PlatformFeeKrw); Assert.Equal(0m, offer.DispatchFeeKrw);
        Assert.Equal(2.5m, offer.DistanceKm); Assert.True(offer.CanAccept);
        Assert.Equal(f.Clock.Now.AddMinutes(5), offer.ExpiresAtUtc);
        Assert.Null(await f.Service().OfferAsync((await f.Db.Set<NeighborhoodMemberOffer>().SingleAsync(x => x.MemberUserId == "member-b")).OfferId));
    }

    [Theory]
    [InlineData(6, 10)]
    [InlineData(0, 101)]
    public async Task 실제최근위치가아니면수락하지않는다(int ageMinutes, int accuracy)
    {
        await using var f = await Fixture.CreateAsync(); var offer = await f.Ready();
        var request = f.Accept(offer); request.LocationAtUtc = f.Clock.Now.AddMinutes(-ageMinutes); request.AccuracyM = accuracy;
        Assert.Equal("MemberFreshLocationRequired", (await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => f.Service().AcceptAsync(offer.OfferId, request))).Code);
        Assert.Null((await f.Db.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 180)]
    public async Task 근사경로나수행불가능한시간창은수락을막는다(bool actual, int durationMinutes)
    {
        await using var f = await Fixture.CreateAsync(); var offer = await f.Ready(); f.Routes.Actual = actual; f.Routes.Minutes = durationMinutes;
        var ex = await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => f.Service().AcceptAsync(offer.OfferId, f.Accept(offer)));
        Assert.Equal(actual ? "MemberDeliveryWindowUnavailable" : "MemberActualRouteRequired", ex.Code);
        Assert.Null((await f.Db.운송원장.SingleAsync()).일반회원수행자UserId);
    }

    [Fact]
    public async Task 픽업과전달시간창을별도로검사하고원수락약정액을보존한다()
    {
        await using var f = await Fixture.CreateAsync(); await f.Enroll("member-a");
        await f.Seed(); var source = await f.Db.화주운송의뢰.SingleAsync();
        source.픽업_시간창_종료일시 = f.Clock.Now.AddMinutes(3); source.하차_시간창_시작일시 = f.Clock.Now.AddMinutes(20);
        await f.Db.SaveChangesAsync(); await f.Register(seed:false); f.Actor.UserId = "member-a";
        var offer = Assert.Single(await f.Service().OffersAsync()); Assert.Equal(source.픽업_시간창_종료일시, offer.ExpiresAtUtc);
        var request = f.Accept(offer); var work = await f.Service().AcceptAsync(offer.OfferId, request);
        Assert.Equal("Assigned", work.StateCode); Assert.Equal(5000m, work.FareKrw); Assert.NotNull(work.Pickup);
        await using var reopened = f.OpenDb(); var again = await f.Service(reopened).AcceptAsync(offer.OfferId, request);
        Assert.Equal(work.Revision, again.Revision);
        var queue = await reopened.운송원장.SingleAsync(); Assert.Null(queue.확정기사Id);
        Assert.Equal("member-a", queue.일반회원수행자UserId); Assert.Equal("CommunityMember", queue.수행자종류);
        f.Actor.UserId = "owner"; await f.Service(reopened).RegisterAsync("request-1", f.Input(), default);
        Assert.Equal(1, await reopened.Set<NeighborhoodMemberDelivery>().CountAsync());
    }

    [Fact]
    public async Task 참여철회와기존기사업무는미수락제안수락을막는다()
    {
        await using var f = await Fixture.CreateAsync(); var offer = await f.Ready();
        var update = f.Setting(); update.ExpectedRevision = 1; update.ReceiveOffers = false;
        await f.Service().SaveParticipationAsync(update);
        Assert.Equal("Withdrawn", (await f.Service().OfferAsync(offer.OfferId))!.StateCode);
        Assert.Equal("MemberWorkCapacityChanged", (await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => f.Service().AcceptAsync(offer.OfferId, f.Accept(offer)))).Code);
        Assert.Equal(0, await f.Db.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "Accept"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 기존음식혹은화물기사업무가있으면일반회원중복수락을막는다(bool food)
    {
        await using var f = await Fixture.CreateAsync(); var offer = await f.Ready();
        if (food) f.Db.음식배달시도.Add(new() { 기사Id = "member-a", 시도StableId = "food-test", 주문번호 = "food-1", 수락시각Utc = f.Clock.Now });
        else f.Db.운송원장.Add(new() { 의뢰Id = "legacy", 운송번호 = "legacy-1", 확정기사Id = "member-a", 배차큐단계 = 상태값.배차큐단계.확정 });
        await f.Db.SaveChangesAsync();
        Assert.Equal("MemberWorkCapacityChanged", (await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(() => f.Service().AcceptAsync(offer.OfferId, f.Accept(offer)))).Code);
    }

    [Fact]
    public async Task 두별도SQL컨텍스트에서같은제안은한회원만확정된다()
    {
        await using var f = await Fixture.CreateAsync(); await f.Enroll("member-a"); await f.Enroll("member-b"); await f.Register();
        var offers = await f.Db.Set<NeighborhoodMemberOffer>().AsNoTracking().ToListAsync();
        using var barrier = new Barrier(2);
        var tasks = offers.Select(o => Task.Run(async () =>
        {
            await using var db = f.OpenDb(); var actor = new Actor { UserId = o.MemberUserId };
            barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            try { return (await f.Service(db, actor).AcceptAsync(o.OfferId, f.Accept(new() { Revision=o.Revision }))).StateCode; }
            catch (NeighborhoodMemberDeliveryException) { return "Conflict"; }
            catch (SqliteException) { return "Conflict"; }
        })).ToArray();
        var results = await Task.WhenAll(tasks); Assert.Single(results, x => x == "Assigned"); Assert.Single(results, x => x == "Conflict");
        await using var inspection = f.OpenDb(); Assert.Equal(1, await inspection.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.Kind == "Accept"));
        Assert.Equal(1, await inspection.Set<NeighborhoodMemberParticipation>().CountAsync(x => x.ActiveRequestId != null));
        Assert.NotNull((await inspection.운송원장.SingleAsync()).일반회원수행자UserId);
    }

    [Fact]
    public async Task 같은회원의두의뢰수락과구형배차잠금은동일SQL활성슬롯을본다()
    {
        await using var f = await Fixture.CreateAsync(); var first = await f.Ready();
        f.Actor.UserId="owner"; await f.Register("request-2"); f.Actor.UserId="member-a";
        var second = (await f.Service().OffersAsync()).Single(x => x.RequestId=="request-2");
        await f.Service().AcceptAsync(first.OfferId,f.Accept(first));
        await using var other = f.OpenDb();
        Assert.Equal("MemberWorkCapacityChanged",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service(other).AcceptAsync(second.OfferId,f.Accept(second)))).Code);
        await using var tx=await other.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Assert.True(await NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(other,"member-a",default));
        Assert.False(await NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(other,"no-member",default));
    }

    [Fact]
    public async Task 실제사진전송과재조회뒤에만단계완료되고수령및수금완료와분리된다()
    {
        await using var f=await Fixture.CreateAsync(); var offer=await f.Ready(); var work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        Assert.Equal("MemberPhotoRequired",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().PickupAsync(work.RequestId,f.Command(work.Revision)))).Code);
        var evidence=f.Evidence(work,"Pickup"); work=await f.Service().EvidenceAsync(work.RequestId,evidence);
        var receipt=await f.Service().EvidenceReceiptAsync(work.RequestId,evidence.ClientRequestId); Assert.True(receipt!.Committed);
        Assert.True(work.CanPickup); Assert.NotEmpty(f.Storage.Objects);
        work=await f.Service().PickupAsync(work.RequestId,f.Command(work.Revision)); Assert.Equal("PickedUp",work.StateCode);
        Assert.Null(work.Pickup); Assert.NotNull(work.Dropoff);
        work=await f.Service().EvidenceAsync(work.RequestId,f.Evidence(work,"Delivery"));
        work=await f.Service().DeliverAsync(work.RequestId,f.Command(work.Revision)); Assert.Equal("Delivered",work.StateCode);
        Assert.Null(work.Pickup); Assert.Null(work.Dropoff); Assert.Null(work.RequestNotes);
        await using var reread=f.OpenDb(); var source=await reread.화주운송의뢰.SingleAsync();
        Assert.Equal(상태값.배차상태.하차완료,source.배차상태); Assert.Equal(상태값.결제상태.결제대기,source.결제상태);
        Assert.Equal(상태값.결제상태.결제대기,source.정산상태); Assert.Null(source.인수증등록일시);
        Assert.Null((await reread.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        Assert.All(await reread.Set<NeighborhoodMemberPhoto>().ToListAsync(), p=>Assert.Equal("Bound",p.StateCode));
    }

    [Fact]
    public async Task 잘못된사진실제저장무결성혹은다른회원증빙은완료근거가아니다()
    {
        await using var f=await Fixture.CreateAsync(); var offer=await f.Ready(); var work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        var invalid=f.Evidence(work,"Pickup"); invalid.ContentBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes("pretend-image"));
        Assert.Equal("MemberPhotoInvalid",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().EvidenceAsync(work.RequestId,invalid))).Code);
        f.Storage.CorruptDownload=true;
        Assert.Equal("MemberPhotoStorageIntegrityMismatch",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().EvidenceAsync(work.RequestId,f.Evidence(work,"Pickup")))).Code);
        Assert.False((await f.Service().WorkAsync(work.RequestId))!.CanPickup);
        f.Actor.UserId="other"; Assert.Null(await f.Service().WorkAsync(work.RequestId));
        Assert.Null(await f.Service().EvidenceReceiptAsync(work.RequestId,invalid.ClientRequestId));
    }

    [Fact]
    public async Task 픽업전취소는슬롯을풀고사진픽업후에는지원절차를요구한다()
    {
        await using var f=await Fixture.CreateAsync(); var offer=await f.Ready(); var work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        f.Actor.UserId="owner"; await f.Service().CancelAsync(work.RequestId,f.Command(work.Revision));
        f.Db.ChangeTracker.Clear(); Assert.Null((await f.Db.Set<NeighborhoodMemberParticipation>().SingleAsync()).ActiveRequestId);
        f.Actor.UserId="member-a"; Assert.Equal("Cancelled",(await f.Service().WorkAsync(work.RequestId))!.StateCode);
        f.Actor.UserId="owner"; await f.Register("request-2"); f.Actor.UserId="member-a";
        offer=(await f.Service().OffersAsync()).Single(x=>x.RequestId=="request-2"); work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        work=await f.Service().EvidenceAsync(work.RequestId,f.Evidence(work,"Pickup")); work=await f.Service().PickupAsync(work.RequestId,f.Command(work.Revision));
        f.Actor.UserId="owner";
        Assert.Equal("MemberPickupRecoveryRequired",(await Assert.ThrowsAsync<NeighborhoodMemberDeliveryException>(()=>f.Service().CancelAsync(work.RequestId,f.Command(work.Revision)))).Code);
    }

    [Fact]
    public async Task 업무72시간뒤에도금액은보이지만주소요청사항은차단된다()
    {
        await using var f=await Fixture.CreateAsync(); var offer=await f.Ready(); var work=await f.Service().AcceptAsync(offer.OfferId,f.Accept(offer));
        f.Clock.Now=f.Clock.Now.AddHours(73); work=(await f.Service().WorkAsync(work.RequestId))!;
        Assert.Equal(5000m,work.FareKrw); Assert.Null(work.Pickup); Assert.Null(work.Dropoff); Assert.Null(work.RequestNotes); Assert.False(work.CanPickup);
    }

    [Fact]
    public async Task 실제DB재조회한알림은정확한앱수신자UTC만료와데이터전용payload를사용한다()
    {
        await using var f=await Fixture.CreateAsync(); var offer=await f.Ready();
        f.Db.SsalddelMobilePushInstallations.AddRange(new SsalddelMobilePushInstallation { UserId="member-a",InstallationId="ok",AppKey="ssalddel.shipper",PushToken="right",PushTokenHash="hash1" },
            new SsalddelMobilePushInstallation { UserId="other",InstallationId="wrong-owner",AppKey="ssalddel.shipper",PushToken="wrong-owner",PushTokenHash="hash2" },
            new SsalddelMobilePushInstallation { UserId="member-a",InstallationId="wrong-app",AppKey="ssalddel.fdriver",PushToken="wrong-app",PushTokenHash="hash3" });
        await f.Db.SaveChangesAsync(); await using var db=f.OpenDb(); var push=new Push();
        var mode=new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode=SsalddelExecutionMode.Operational }));
        var outbox=new NeighborhoodMemberDeliveryPushOutbox(db,f.Service(db,mode:SsalddelExecutionMode.Operational),push,f.Clock,mode);
        Assert.Equal(1,await outbox.SendPendingAsync()); var msg=Assert.Single(push.Messages); Assert.Equal("right",msg.Token); Assert.True(msg.DataOnly);
        Assert.Equal(7,msg.Data.Count); Assert.Equal("member-a",msg.Data["userId"]); Assert.Equal("ssalddel.shipper",msg.Data["appKey"]);
        Assert.EndsWith("Z",msg.Data["expiresAtUtc"]); Assert.True(DateTimeOffset.TryParseExact(msg.Data["expiresAtUtc"],"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out _));
        Assert.DoesNotContain("망우로",JsonSerializer.Serialize(msg)); Assert.Equal(0,await outbox.SendPendingAsync());
    }

    [Fact]
    public async Task 수신철회후알림은종결억제하고실제발송하지않는다()
    {
        await using var f=await Fixture.CreateAsync(); await f.Ready(); var setting=f.Setting();setting.ExpectedRevision=1;setting.ReceiveOffers=false;
        await f.Service().SaveParticipationAsync(setting); var push=new Push();
        var mode=new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode=SsalddelExecutionMode.Operational }));
        var outbox=new NeighborhoodMemberDeliveryPushOutbox(f.Db,f.Service(mode:SsalddelExecutionMode.Operational),push,f.Clock,mode);
        Assert.Equal(0,await outbox.SendPendingAsync()); Assert.Empty(push.Messages);
        f.Db.ChangeTracker.Clear(); Assert.Equal("Suppressed",(await f.Db.Set<NeighborhoodMemberOffer>().SingleAsync()).PushStateCode);
        Assert.Equal(0,await outbox.SendPendingAsync()); Assert.Equal(1,(await f.Db.Set<NeighborhoodMemberOffer>().SingleAsync()).PushAttempts);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection keeper; private readonly string cs;
        public SsalddelContext Db { get; } public Actor Actor { get; }=new(); public Clock Clock { get; }=new();
        public Route Routes { get; }=new(); public Storage Storage { get; }=new(); public Consent Consent { get; }=new();
        private Fixture(SqliteConnection keeper,string cs,SsalddelContext db) { this.keeper=keeper;this.cs=cs;Db=db; }
        public static async Task<Fixture> CreateAsync()
        {
            var cs="Data Source=member-"+Guid.NewGuid().ToString("N")+";Mode=Memory;Cache=Shared;Default Timeout=2";
            var connection=new SqliteConnection(cs);await connection.OpenAsync();
            var db=new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(cs).Options,new Encryption());
            await db.Database.EnsureCreatedAsync(); return new(connection,cs,db);
        }
        public SsalddelContext OpenDb()=>new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(cs).Options,new Encryption());
        public NeighborhoodMemberDeliveryUseCase Service(SsalddelContext? db=null,Actor? actor=null,bool enabled=true,SsalddelExecutionMode mode=SsalddelExecutionMode.Simulation,bool activation=true,bool readOnly=false,보호지원UseCase? support=null)
            =>new(db??Db,actor??Actor,Options.Create(new NeighborhoodMemberDeliveryOptions { Enabled=enabled,OperationalEnabled=true }),
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode=mode })),Clock,new Encryption(),Consent,Routes,Storage,
                support:support,activation:new Activation(activation),executionOptions:Options.Create(new SsalddelExecutionOptions { Mode=mode,DevelopmentReadOnly=readOnly }));
        public NeighborhoodMemberParticipationSaveRequest Setting()=>new() { ClientRequestId=Guid.NewGuid(),ReceiveOffers=true,ActivityLabel="내 활동 권역",Latitude=37.57m,Longitude=127.07m };
        public async Task Enroll(string actor,decimal latitude=37.57m,decimal capacity=10)
        { Actor.UserId=actor;var r=Setting();r.Latitude=latitude;r.MaxWeightKg=capacity;await Service().SaveParticipationAsync(r);Db.ChangeTracker.Clear(); }
        public async Task Seed(string id="request-1")
        {
            Db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id=id,화주Id="owner",주문자UserId="owner",클라이언트요청Id=NeighborhoodDeliveryRoutes.ClientRequestPrefix+id,
                화물종류="생활 물품",화물수량=1,화물중량Kg=1,차량종류="오토바이",최종운임=5000,
                픽업_도로명주소="망우로71길 20",픽업_상세주소="1층",픽업_연락처_이름="판매자",픽업_연락처_전화번호="01012345678",픽업_위도=37.571m,픽업_경도=127.071m,
                하차_도로명주소="동일로72길 9",하차_상세주소="개인 집",하차_연락처_이름="수령자",하차_연락처_전화번호="01087654321",하차_위도=37.58m,하차_경도=127.08m,
                픽업_시간창_시작일시=Clock.Now,픽업_시간창_종료일시=Clock.Now.AddHours(1),하차_시간창_종료일시=Clock.Now.AddHours(2),요청사항="비공개 요청" });
            Db.운송원장.Add(new 운송원장 { 의뢰Id=id,운송번호="transport-"+id,일반회원배송전용=true,생활배송수락준비완료=false,
                운임=5000,상태=상태값.배차대기상태.대기,배차큐단계=상태값.배차큐단계.계획배차 });
            Db.운임구성.Add(new 운임구성 { 의뢰Id=id,최종운임=5000,예상거리Km=2.5m }); await Db.SaveChangesAsync();Db.ChangeTracker.Clear();
        }
        public NeighborhoodDeliveryRequest Input()=>new() { MemberDeliveryRequested=true,MemberDeliveryDisclosureAgreed=true,
            MemberDeliveryNoticeVersion=NeighborhoodMemberDeliveryRoutes.NoticeVersion,PrivacyConsentEvidenceId=Consent.Id,DirectPaymentAgreed=true,AgreedFareKrw=5000 };
        public async Task Register(string id="request-1",bool seed=true) { Actor.UserId="owner";if(seed)await Seed(id);await Service().RegisterAsync(id,Input(),default);Db.ChangeTracker.Clear(); }
        public async Task<NeighborhoodMemberOfferResponse> Ready() { await Enroll("member-a");await Register();Actor.UserId="member-a";return Assert.Single(await Service().OffersAsync()); }
        public NeighborhoodMemberAcceptRequest Accept(NeighborhoodMemberOfferResponse o)=>new() { ClientRequestId=Guid.NewGuid(),ExpectedRevision=o.Revision,Latitude=37.57m,Longitude=127.07m,AccuracyM=10,LocationAtUtc=Clock.Now };
        public NeighborhoodMemberCommandRequest Command(long rev)=>new() { ClientRequestId=Guid.NewGuid(),ExpectedRevision=rev };
        public NeighborhoodMemberEvidenceRequest Evidence(NeighborhoodMemberWorkResponse w,string stage)=>new() { ClientRequestId=Guid.NewGuid(),ExpectedRevision=w.Revision,StageCode=stage,ContentType="image/png",
            ContentBase64=ValidImage() };
        public static string ValidImage()
        { using var bitmap=new SKBitmap(8,8);bitmap.Erase(SKColors.Blue);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);return Convert.ToBase64String(data.ToArray()); }
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await keeper.DisposeAsync();}
    }
    public sealed class Actor : ICurrentUserAccessor { public string? UserId { get; set; }="member-a";public string? Role { get; set; }="일반회원"; }
    public sealed class Clock : TimeProvider { public DateTime Now { get;set; }=new(2080,1,1,0,0,0,DateTimeKind.Utc);public override DateTimeOffset GetUtcNow()=>new(Now); }
    public sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value)=>value is null?null:"protected:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(value));public string? Unprotect(string? value)=>value is null?null:Encoding.UTF8.GetString(Convert.FromBase64String(value[10..])); }
    public sealed class Activation(bool active) : ISsalddelBackgroundJobActivationPolicy { public SsalddelBackgroundWorkloadActivation Evaluate(string key)=>new(active,active?"Enabled":"FeatureDisabled","DomesticTransportWorkflow"); }
    public sealed class Consent : I신청개인정보동의증적Service
    {
        public Guid Id { get; }=Guid.NewGuid();public bool Valid=true;
        public Task 유효한동의요구Async(Guid? id,string work,string source,string user,CancellationToken ct=default) { if(!Valid||id!=Id||user!="owner")throw new InvalidOperationException();return Task.CompletedTask; }
        public Task<신청개인정보동의증적Response> 동의기록Async(신청개인정보동의기록Request r,string u,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<신청개인정보동의증적Response?> 내증적조회Async(Guid id,string u,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<신청개인정보동의증적Response> 철회Async(Guid id,신청개인정보동의철회Request r,string u,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    public sealed class Route : I배차추천경로Service
    {
        public bool Actual=true;public int Minutes=2;
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? o,배차경로좌표? d,string option,CancellationToken ct)=>Task.FromResult<배차경로예상결과?>(new(2.5m,TimeSpan.FromMinutes(Minutes),0,"verified-adapter-test",Actual){RouteOption=option});
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? o,배차경로좌표? d)=>throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id,용달기사? d,DriverLocationSnapshot? l,배차추천검색조건? c)=>throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id,용달기사? d,DriverLocationSnapshot? l)=>throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? o,IReadOnlyList<배차경로좌표> s,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? o,배차경로좌표? a,배차경로좌표? p,배차경로좌표? d)=>throw new NotSupportedException();
        public decimal? CalculateDistanceKm(배차경로좌표 a,배차경로좌표 b)=>throw new NotSupportedException();
    }
    public sealed class Storage : IObjectStorageService
    {
        public bool CorruptDownload; public bool HistoricalAbsent=true; public Dictionary<string,byte[]> Objects { get; }=[];
        public bool IsConfigured(ObjectStorageAccess access)=>access==ObjectStorageAccess.Private;public string? GetContainerName(ObjectStorageAccess access)=>"private-member";
        public Task<ObjectStorageUploadResult> UploadAsync(Stream s,string f,string? t,string? d,ObjectStorageAccess a,CancellationToken ct=default)=>throw new NotSupportedException();
        public async Task<ObjectStorageUploadResult> UploadImmutableAsync(Stream s,string name,string? type,ObjectStorageAccess access,IReadOnlyDictionary<string,string>? metadata=null,CancellationToken ct=default)
        { using var m=new MemoryStream();await s.CopyToAsync(m,ct);var bytes=m.ToArray();if(Objects.TryGetValue(name,out var prior))Assert.Equal(prior,bytes);else Objects.Add(name,bytes);return new("private-member",name,""); }
        public Task<byte[]> DownloadAsync(string c,string n,CancellationToken ct=default)=>Task.FromResult(CorruptDownload?new byte[]{0}:Objects[n].ToArray());
        public Task<ObjectStorageDeleteResult> DeleteAsync(string c,string n,string prefix,CancellationToken ct=default) { Assert.StartsWith(prefix+"/",n);Objects.Remove(n);return Task.FromResult(new ObjectStorageDeleteResult(true,HistoricalAbsent)); }
    }
    public sealed class Push : IFcmPushService
    { public List<FcmPushMessage> Messages { get; }=[];public Task<bool> SendAsync(FcmPushMessage m,CancellationToken ct=default){Messages.Add(m);return Task.FromResult(true);}public Task<bool> SendToTokenAsync(string t,string h,string b,IReadOnlyDictionary<string,string> d,CancellationToken ct=default)=>throw new NotSupportedException(); }
}
