using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed partial class 배차수락CommandHandlerTests
{
    [Fact]
    public async Task R20_기존분산계획의다중상차는_전문계획을유지하고일반정차인증을만들지않는다()
    {
        await using var f = await Ssalddel.Tests.Application.Warehouse.DistributedTestFixture.Create();
        var wave = await f.Round(accept: false);
        var first = await f.Db.Set<살뜰.도메인.창고.분산배송주문Record>().FirstAsync(x => x.WaveId == wave.WaveId);
        var proof = await 화물분산순차인계Policy.조회Async(f.Db, "driver", first.CargoRequestId, DateTime.UtcNow);
        Assert.NotNull(proof.근거);
        var accepted = await f.Use(Ssalddel.Tests.Application.Warehouse.DistributedTestFixture.Driver).AcceptAsync(
            Ssalddel.Tests.Application.Warehouse.DistributedTestFixture.Driver, wave.WaveId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision }, default);
        Assert.Equal("Accepted", accepted.StatusCode);
        Assert.All(await f.Db.화주운송의뢰.AsNoTracking().ToArrayAsync(), x =>
        { Assert.Null(x.픽업_작업시간분); Assert.Null(x.하차_작업시간분); });
        Assert.All(await f.Db.운송원장.AsNoTracking().ToArrayAsync(), x =>
            Assert.False(JsonNode.Parse(x.추천판정근거Json ?? "{}")!.AsObject().ContainsKey(화물수락정차순서.JsonProperty)));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("selected")]
    [InlineData("early-delivery")]
    [InlineData("extended-window")]
    [InlineData("pickup-coordinate")]
    [InlineData("delivery-coordinate")]
    public async Task R20_분산원요청이나다중상차계획이손상되면_기존순차호환도차단한다(string change)
    {
        await using var f = await Ssalddel.Tests.Application.Warehouse.DistributedTestFixture.Create();
        var wave = await f.Round(accept: false);
        var first = await f.Db.Set<살뜰.도메인.창고.분산배송주문Record>().FirstAsync(x => x.WaveId == wave.WaveId);
        var proof = await 화물분산순차인계Policy.조회Async(f.Db, "driver", first.CargoRequestId, DateTime.UtcNow);
        Assert.NotNull(proof.근거);
        var checkedProof = proof.근거! with { 실차근거지문 = (await new 기사실차적재근거조회Service(f.Db).조회Async("driver")).근거지문 };
        Assert.True(await 화물분산순차인계Policy.현재근거확인Async(f.Db, checkedProof, DateTime.UtcNow, default));
        if (change == "source") (await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == first.CargoRequestId)).화물수량++;
        else
        {
            var record = await f.Db.Set<살뜰.도메인.창고.분산배송WaveRecord>().SingleAsync(x => x.WaveId == wave.WaveId);
            var evidence = JsonNode.Parse(record.RouteEvidenceJson)!.AsObject();
            if (change == "selected") evidence["selected"] = new JsonArray();
            else if (change == "early-delivery")
            {
                var rows = JsonNode.Parse(record.RouteJson)!.AsArray();
                var deliveryIndex = Enumerable.Range(0, rows.Count).First(i => rows[i]!["kind"]!.GetValue<string>() == "Delivery");
                var delivery = rows[deliveryIndex]!.DeepClone();
                rows.RemoveAt(deliveryIndex);
                // 두 상차 중 첫 상차 뒤로 배송을 앞당긴 손상이다. selected도 같이 바꿔 단순 목록 비교만으로 통과하지 않게 한다.
                rows.Insert(1, delivery);
                for (var i = 0; i < rows.Count; i++)
                { rows[i]!["sequence"] = i + 1; rows[i]!["expectedArrivalUtc"] = f.Clock.Now.AddMinutes(15 + i * 2); }
                record.RouteJson = rows.ToJsonString();
                evidence["selected"] = new JsonArray(rows.Select(x => JsonValue.Create(x!["stopId"]!.GetValue<string>())).ToArray());
            }
            else
            {
                var rows = JsonNode.Parse(record.RouteJson)!.AsArray();
                var kind = change == "pickup-coordinate" ? "Pickup" : "Delivery";
                var target = rows.First(x => x!["kind"]!.GetValue<string>() == kind)!;
                if (change == "extended-window") target["windowEndUtc"] = target["windowEndUtc"]!.GetValue<DateTime>().AddDays(1);
                else target["latitude"] = target["latitude"]!.GetValue<decimal>() + .1m;
                record.RouteJson = rows.ToJsonString();
            }
            record.RouteEvidenceJson = evidence.ToJsonString();
        }
        await f.Db.SaveChangesAsync();
        var changed = await 화물분산순차인계Policy.조회Async(f.Db, "driver", first.CargoRequestId, DateTime.UtcNow);
        Assert.True(changed.분산업무있음); Assert.Null(changed.근거);
        Assert.False(await 화물분산순차인계Policy.현재근거확인Async(f.Db, checkedProof, DateTime.UtcNow, default));
    }

    [Fact]
    public async Task R20_분산순차인계도검사뒤실차변경을현재판본으로다시확인한다()
    {
        await using var f = await Ssalddel.Tests.Application.Warehouse.DistributedTestFixture.Create();
        var wave = await f.Round(accept: false);
        var order = await f.Db.Set<살뜰.도메인.창고.분산배송주문Record>().FirstAsync(x => x.WaveId == wave.WaveId);
        var request = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == order.CargoRequestId);
        var vehicles = new 기사실차적재근거조회Service(f.Db);
        var service = new 화물배차수락적격성Service(f.Db, f.Locations, new 차량화물적합성Service(new 차량적재추천Engine()),
            new 기사운송일정구성Service(f.Db), new 운송일정삽입평가Service(f.Routes), vehicles);
        var result = await service.평가Async("driver", request, []);
        Assert.True(result.수락가능); Assert.True(result.기존분산순차계획호환);
        Assert.NotNull(result.기존분산인계근거?.실차근거지문);
        (await f.Db.차량제원.SingleAsync()).최대적재중량Kg--;
        await f.Db.SaveChangesAsync();
        Assert.False(await 화물분산순차인계Policy.현재근거확인Async(f.Db, result.기존분산인계근거!, DateTime.UtcNow, default, vehicles));
    }

    [Fact]
    public async Task R20_일반화물의미확인작업시간은_순차이어가기가가능해도분산호환으로우회하지않는다()
    {
        var f = await R20Setup();
        await using var db = f.Db;
        f.BRequest.픽업_시간창_종료일시 = DateTime.UtcNow.AddHours(2);
        f.BRequest.하차_시간창_종료일시 = DateTime.UtcNow.AddHours(3);
        f.BRequest.픽업_작업시간분 = null;
        await db.SaveChangesAsync();
        var result = await R20Service(db).평가Async("driver-1", f.BRequest, []);
        Assert.False(result.수락가능); Assert.False(result.기존분산순차계획호환);
        Assert.Equal(화물배차수락오류코드.일정근거부족, result.오류코드);
    }

    [Fact]
    public async Task R20_수락과재조회는같은등록차량Provider를재사용하고_판본변경을차단한다()
    {
        var f = await R20Setup();
        await using var db = f.Db;
        var vehicle = new R20VehicleProvider(await new 기사실차적재근거조회Service(db).조회Async("driver-1"));
        var schedule = new 기사운송일정구성Service(db, vehicle);
        var eligibility = new 화물배차수락적격성Service(db, new R20Locations(),
            new 차량화물적합성Service(new 차량적재추천Engine()), schedule, new 운송일정삽입평가Service(new R20Routes()), vehicle);
        var handler = new Ssalddel.Application.Driver.DispatchAction.배차수락CommandHandler(db, new ThrowingPublisher(),
            new TestCurrentUserAccessor("driver-1", "기사"), new Ssalddel.Application.CommandProcessing.참여자실행권한검사(),
            new Ssalddel.Application.CommandProcessing.WorkRelationshipSnapshotCollector(), eligibility, new StubContinuityUseCase(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Ssalddel.Application.Driver.DispatchAction.배차수락CommandHandler>.Instance,
            운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용,
            후속픽업: new Ssalddel.Tests.Services.Dispatch.Common.화물후속픽업시험Service(),
            기사위치: new Ssalddel.Tests.Services.Dispatch.Common.화물현재위치시험Store(),
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance, 실차조회: vehicle);
        var accepted = await handler.Handle(new("driver-1", f.B.의뢰Id), default);
        Assert.True(accepted.IsSuccess, string.Join(" | ", accepted.Errors.Select(x => $"{x.Metadata.GetValueOrDefault("ErrorCode")}: {x.Message}")));
        await using var read = new SsalddelContext(f.Options, new DummyPersonalDataEncryptionService());
        Assert.True((await new 기사운송일정구성Service(read, vehicle).구성Async("driver-1", null)).수락정차순서반영됨);
        vehicle.Revision++;
        var changed = await new 기사운송일정구성Service(read, vehicle).구성Async("driver-1", null);
        Assert.Equal("FreightAcceptedStopOrder.VehicleChanged", changed.수락정차순서보류Code);
    }

    [Fact]
    public async Task R20_SQLite저장후새Context에서_JSON과정차순서를동일하게복구한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options;
        await using (var schema = new SsalddelContext(options, new DummyPersonalDataEncryptionService()))
            await schema.Database.EnsureCreatedAsync();
        var f = await R20Setup(options);
        await using (f.Db) await R20Accept(f);
        await using var read = new SsalddelContext(options, new DummyPersonalDataEncryptionService());
        var plan = await new 기사운송일정구성Service(read).구성Async("driver-1", null);
        Assert.True(plan.수락정차순서반영됨);
        Assert.Null(plan.수락정차순서보류Code);
        Assert.Equal(new[] { (f.B.의뢰Id, "pickup"), (f.B.의뢰Id, "dropoff"), (f.A.의뢰Id, "dropoff") },
            plan.항목목록.Select(x => (x.의뢰Id, x.단계유형)));
        var a = await read.운송원장.SingleAsync(x => x.의뢰Id == f.A.의뢰Id);
        Assert.Equal(7, JsonNode.Parse(a.추천판정근거Json!)!["future"]!["x"]!.GetValue<int>());
    }

    [Fact]
    public async Task R20_B우선수락을저장한뒤_새Context도같은순서와원래JSON을읽는다()
    {
        var f = await R20Setup();
        await using var db = f.Db;
        var eligibility = await R20Service(db).평가Async("driver-1", f.BRequest, []);
        Assert.True(eligibility.수락가능);
        Assert.NotNull(eligibility.수락정차검증);
        Assert.Equal(f.B.의뢰Id, eligibility.수락정차검증.정차목록[0].의뢰Id);
        var accepted = await CreateHandler(db, new StubEligibilityService(eligibility)).Handle(new("driver-1", f.B.의뢰Id), default);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(7, JsonNode.Parse(f.A.추천판정근거Json!)!["future"]!["x"]!.GetValue<int>());
        Assert.Equal("보존", JsonNode.Parse(f.A.추천판정근거Json!)!["수행계획"]!["자료"]!.GetValue<string>());
        await using var read = new SsalddelContext(f.Options, new DummyPersonalDataEncryptionService());
        var plan = await new 기사운송일정구성Service(read).구성Async("driver-1", new(37m, 127m));
        Assert.True(plan.수락정차순서반영됨);
        Assert.Null(plan.수락정차순서보류Code);
        Assert.Equal(new[] { (f.B.의뢰Id, "pickup"), (f.B.의뢰Id, "dropoff"), (f.A.의뢰Id, "dropoff") },
            plan.항목목록.Select(x => (x.의뢰Id, x.단계유형)));
        Assert.All(plan.항목목록, x => Assert.True(x.운송Id > 0));
    }

    [Fact]
    public async Task R20_B상차와완료뒤에는_해당정차만제외하고남은A를유지한다()
    {
        var f = await R20Setup();
        await using var db = f.Db;
        await R20Accept(f);
        f.B.상태 = "상차완료"; await db.SaveChangesAsync();
        await using (var read = new SsalddelContext(f.Options, new DummyPersonalDataEncryptionService()))
        {
            var plan = await new 기사운송일정구성Service(read).구성Async("driver-1", null);
            Assert.True(plan.수락정차순서반영됨);
            Assert.Equal(new[] { (f.B.의뢰Id, "dropoff"), (f.A.의뢰Id, "dropoff") }, plan.항목목록.Select(x => (x.의뢰Id, x.단계유형)));
        }
        f.B.상태 = "인수완료"; await db.SaveChangesAsync();
        await using var finalRead = new SsalddelContext(f.Options, new DummyPersonalDataEncryptionService());
        var final = await new 기사운송일정구성Service(finalRead).구성Async("driver-1", null);
        Assert.True(final.수락정차순서반영됨);
        var only = Assert.Single(final.항목목록);
        Assert.Equal((f.A.의뢰Id, "dropoff"), (only.의뢰Id, only.단계유형));
    }

    [Theory]
    [InlineData("deadline")]
    [InlineData("address")]
    [InlineData("loading")]
    [InlineData("owner")]
    [InlineData("vehicle")]
    [InlineData("vehicle-request")]
    [InlineData("new-work")]
    [InlineData("cancelled")]
    public async Task R20_조건이나귀속이바뀌면_저장순서를현재안전근거로승격하지않는다(string change)
    {
        var f = await R20Setup();
        await using var db = f.Db;
        await R20Accept(f);
        if (change == "deadline") f.ARequest.하차_시간창_종료일시 = DateTime.UtcNow.AddMinutes(1);
        if (change == "address") f.ARequest.하차_도로명주소 += " 변경";
        if (change == "loading") f.ARequest.화물중량Kg += 1;
        if (change == "owner") f.A.확정기사Id = "other-driver";
        if (change == "vehicle") (await db.차량제원.SingleAsync()).최대적재중량Kg -= 1;
        if (change == "vehicle-request") f.ARequest.차량종류 = "다른 차종";
        if (change == "new-work") db.운송원장.Add(new() { 의뢰Id = "C", 기사_운송자 = "driver-1", 확정기사Id = "driver-1", 상태 = "확정", 배차큐단계 = 상태값.배차큐단계.확정 });
        if (change == "cancelled") f.A.상태 = "취소";
        await db.SaveChangesAsync();
        await using var read = new SsalddelContext(f.Options, new DummyPersonalDataEncryptionService());
        var plan = await new 기사운송일정구성Service(read).구성Async("driver-1", null);
        Assert.False(plan.수락정차순서반영됨);
        Assert.StartsWith("FreightAcceptedStopOrder.", plan.수락정차순서보류Code);
    }

    [Theory]
    [InlineData("broken-json")]
    [InlineData("duplicate-property")]
    [InlineData("future-version")]
    [InlineData("duplicate-stop")]
    [InlineData("missing-record")]
    public async Task R20_손상되거나엇갈린수락순서는_기본순서로조용히대체인증하지않는다(string change)
    {
        var f = await R20Setup();
        await using var db = f.Db;
        await R20Accept(f);
        var root = JsonNode.Parse(f.A.추천판정근거Json!)!.AsObject();
        if (change == "future-version") root[화물수락정차순서.JsonProperty]!["판본"] = 2;
        if (change == "duplicate-stop")
        {
            var stops = root[화물수락정차순서.JsonProperty]!["정차목록"]!.AsArray();
            stops[1] = stops[0]!.DeepClone();
        }
        if (change == "missing-record") root.Remove(화물수락정차순서.JsonProperty);
        f.A.추천판정근거Json = change == "broken-json" ? "{broken" : change == "duplicate-property"
            ? "{\"수락정차순서\":null,\"수락정차순서\":null}" : root.ToJsonString();
        await db.SaveChangesAsync();
        await using var read = new SsalddelContext(f.Options, new DummyPersonalDataEncryptionService());
        var plan = await new 기사운송일정구성Service(read).구성Async("driver-1", null);
        Assert.False(plan.수락정차순서반영됨);
        Assert.NotNull(plan.수락정차순서보류Code);
    }

    [Fact]
    public async Task R20_검증뒤약속변경은_수락과JSON갱신모두차단한다()
    {
        var f = await R20Setup();
        await using var db = f.Db;
        var eligibility = await R20Service(db).평가Async("driver-1", f.BRequest, []);
        Assert.NotNull(eligibility.수락정차검증);
        var original = f.A.추천판정근거Json;
        f.ARequest.픽업_작업시간분 += 1; await db.SaveChangesAsync();
        var result = await CreateHandler(db, new StubEligibilityService(eligibility)).Handle(new("driver-1", f.B.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("FreightAcceptedStopOrder.SourceChanged", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Equal(original, f.A.추천판정근거Json);
        Assert.Null(f.B.확정기사Id);
    }

    [Theory]
    [InlineData("decimal-scale")]
    [InlineData("time-kind")]
    public async Task R20_저장형식만다른동일수치와시계값은_조건변경으로오인하지않는다(string form)
    {
        var f = await R20Setup();
        await using var db = f.Db;
        var eligibility = await R20Service(db).평가Async("driver-1", f.BRequest, []);
        Assert.NotNull(eligibility.수락정차검증);
        if (form == "decimal-scale")
        { f.BRequest.화물중량Kg = 10.0m; f.BRequest.픽업_경도 = 127.0m; f.BRequest.하차_경도 = 127.0m; }
        else
        {
            f.BRequest.픽업_시간창_시작일시 = DateTime.SpecifyKind(f.BRequest.픽업_시간창_시작일시, DateTimeKind.Unspecified);
            f.BRequest.픽업_시간창_종료일시 = DateTime.SpecifyKind(f.BRequest.픽업_시간창_종료일시, DateTimeKind.Unspecified);
        }
        await db.SaveChangesAsync();
        var result = await CreateHandler(db, new StubEligibilityService(eligibility)).Handle(new("driver-1", f.B.의뢰Id), default);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(x => $"{x.Metadata.GetValueOrDefault("ErrorCode")}: {x.Message}")));
    }

    [Fact]
    public async Task R20_추가수락의작업시간미확인은_0분으로계산하지않는다()
    {
        var f = await R20Setup();
        await using var db = f.Db;
        f.BRequest.픽업_작업시간분 = null;
        var result = await R20Service(db).평가Async("driver-1", f.BRequest, []);
        Assert.False(result.수락가능);
        Assert.Equal(화물배차수락오류코드.일정근거부족, result.오류코드);
    }

    private sealed record R20Fixture(DbContextOptions<SsalddelContext> Options, SsalddelContext Db,
        운송원장 A, 운송원장 B, 화주운송의뢰 ARequest, 화주운송의뢰 BRequest);

    private static async Task<R20Fixture> R20Setup(DbContextOptions<SsalddelContext>? configured = null)
    {
        var options = configured ?? new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase("cargo-order-r20-" + Guid.NewGuid())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        var db = new SsalddelContext(options, new DummyPersonalDataEncryptionService());
        var (b, br) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        R20Fill(br, 37.001m, 37.002m); br.픽업_시간창_종료일시 = DateTime.UtcNow.AddMinutes(3);
        br.하차_시간창_종료일시 = DateTime.UtcNow.AddMinutes(5);
        var ar = new 화주운송의뢰 { 의뢰Id = "active-A", 화주Id = "shipper-1" }; R20Fill(ar, 36.999m, 37.02m);
        var a = new 운송원장 { 의뢰Id = ar.의뢰Id, 운송번호 = ar.의뢰Id, 기사_운송자 = "driver-1", 확정기사Id = "driver-1",
            배차큐단계 = 상태값.배차큐단계.확정, 상태 = "상차완료", 출발_픽업 = DateTime.UtcNow.AddMinutes(-10),
            추천판정근거Json = "{\"future\":{\"x\":7},\"수행계획\":{\"자료\":\"보존\"}}" };
        db.화주운송의뢰.Add(ar); db.운송원장.Add(a);
        db.용달기사.Add(new() { 기사Id = "driver-1", 차량 = "r20-vehicle" });
        db.차량제원.Add(new() { 차량코드 = "r20-vehicle", 적재함길이Mm = 2000, 적재함폭Mm = 2000, 적재함높이Mm = 2000,
            최대적재중량Kg = 1000, 권장최대CBM = 4m, 팔레트적재개수 = 4 });
        await db.SaveChangesAsync();
        return new(options, db, a, b, ar, br);
    }

    private static void R20Fill(화주운송의뢰 r, decimal pickup, decimal drop)
    {
        r.화물종류 = "합성 상자"; r.화물수량 = 1; r.화물중량Kg = 10m; r.화물입력부피Cbm = .1m; r.화물부피Cbm = .1m;
        r.화물부피근거Code = "UserProvided"; r.화물적재계산판본 = Ssalddel.Contracts.Shipper.Request.화물적재입력Policy.CurrentRevision;
        r.화물바닥회전가능여부 = true; r.화물적층가능여부 = true; r.화물팔레트개수 = 0; r.화물온도조건 = "상온";
        r.픽업_도로명주소 = "합성 상차지"; r.하차_도로명주소 = "합성 하차지";
        r.픽업_위도 = pickup; r.픽업_경도 = 127m; r.하차_위도 = drop; r.하차_경도 = 127m;
        r.픽업_시간창_시작일시 = DateTime.UtcNow.AddMinutes(-10); r.픽업_시간창_종료일시 = DateTime.UtcNow.AddHours(1);
        r.하차_시간창_종료일시 = DateTime.UtcNow.AddHours(2); r.픽업_작업시간분 = .5m; r.하차_작업시간분 = .5m;
    }

    private static 화물배차수락적격성Service R20Service(SsalddelContext db) => new(db, new R20Locations(),
        new 차량화물적합성Service(new 차량적재추천Engine()), new 기사운송일정구성Service(db), new 운송일정삽입평가Service(new R20Routes()));

    private static async Task R20Accept(R20Fixture f)
    {
        var eligible = await R20Service(f.Db).평가Async("driver-1", f.BRequest, []);
        Assert.True(eligible.수락가능); Assert.NotNull(eligible.수락정차검증);
        var accepted = await CreateHandler(f.Db, new StubEligibilityService(eligible)).Handle(new("driver-1", f.B.의뢰Id), default);
        Assert.True(accepted.IsSuccess, string.Join(" | ", accepted.Errors.Select(x => $"{x.Metadata.GetValueOrDefault("ErrorCode")}: {x.Message}")));
    }

    private sealed class R20Locations : IDriverLocationStore
    {
        public void Upsert(DriverLocationSnapshot snapshot) { }
        public bool TryGetLatest(string driverId, out DriverLocationSnapshot snapshot)
        { snapshot = new(driverId, 37m, 127m, 1m, "운행중", DateTime.UtcNow, DateTime.UtcNow); return true; }
    }

    private sealed class R20VehicleProvider(기사실차적재조회결과 original) : I기사실차적재근거조회Service
    {
        public int Revision { get; set; } = 1;
        public Task<기사실차적재조회결과> 조회Async(string driver, CancellationToken ct = default)
            => Task.FromResult(original with { 근거지문 = "r20-synthetic-provider-" + Revision, 판본 = Revision });
    }

    private sealed class R20Routes : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? from, 배차경로좌표? to) => Task.FromResult<배차경로예상결과?>(from is null || to is null ? null
            : new(Math.Abs(from.Latitude - to.Latitude) * 100m, TimeSpan.FromMinutes((double)(Math.Abs(from.Latitude - to.Latitude) * 100m)), null, "합성 경로", true) { RouteOption = "r20-car" });
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? from, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? from, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? drop) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 from, 배차경로좌표 to) => Math.Abs(from.Latitude - to.Latitude) * 100m;
    }
}
