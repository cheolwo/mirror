using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Community;

/// <summary>격리 원천/권한 포트 시험입니다. 실제 전화 발신·단말·운영 연락의 검증이 아닙니다.</summary>
public sealed class 공간공동운영업무연락Tests
{
    [Fact]
    public async Task 로그인이나_비공개원장권한이없으면_전문연락처를_조회하지않는다()
    {
        await using var f = new Fixture();
        Assert.Null(await f.Service(null).조회Async("hub", "cycle", "storage", 4));
        f.CanRead = false;
        Assert.Null(await f.Service().조회Async("hub", "cycle", "storage", 4));
        Assert.Equal(0, f.SnapshotCalls); Assert.Equal(0, f.WarehouseCalls); Assert.Equal(0, f.SenderCalls);
    }

    [Theory]
    [InlineData("unknown", 4, 400)]
    [InlineData("storage", 0, 400)]
    [InlineData("storage", 3, 409)]
    public async Task 절차나_부모판본이맞지않으면_전화자료를_읽지않는다(string stage, long revision, int status)
    {
        await using var f = new Fixture();
        var error = await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Service().조회Async("hub", "cycle", stage, revision));
        Assert.Equal(status, error.StatusCode); Assert.Equal(0, f.WarehouseCalls); Assert.Equal(0, f.SenderCalls);
    }

    [Fact]
    public async Task 원천판본과_선택회차를_재검증하고_다른회차원천을_보충하지않는다()
    {
        await using var f = new Fixture(); await f.SeedSite();
        f.Cycle.SharedSourceKeys = ["source:DistributedSite:another-site"];
        var error = await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("storage"));
        Assert.Equal(409, error.StatusCode); Assert.Equal(0, f.WarehouseCalls);
    }

    [Fact]
    public async Task 중복원천이나_미확인원천은_연락처_공개권한으로승격하지않는다()
    {
        await using var f = new Fixture(); await f.SeedSite();
        var source = f.Snapshot.SharedSources.Single(); f.Snapshot.SharedSources = [source, source];
        Assert.Equal(409, (await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("storage"))).StatusCode);
        f.Snapshot.SharedSources = [source]; source.Verified = false;
        var unavailable = await f.Read("storage");
        Assert.Empty(unavailable.Contacts); Assert.Equal("not-visible", unavailable.StatusCode); Assert.Equal(0, f.WarehouseCalls);
    }

    [Fact]
    public async Task 거점의실제창고와_기존권한포트의_등록업무번호만반환하고_DB번호나주소를_직접읽지않는다()
    {
        await using var f = new Fixture(); await f.SeedSite();
        f.WarehouseRows = [new() { Id = 9, IsActive = true, 담당자명 = "등록 업무 담당", 연락처 = "02-123-4567", 주소 = "private-address" },
            new() { Id = 77, IsActive = true, 담당자명 = "다른 창고", 연락처 = "other-private-phone" }];
        var response = await f.Read("storage"); var item = Assert.Single(response.Contacts);
        Assert.Equal("available", response.StatusCode); Assert.Equal("등록 창고 업무 연락처", item.Label);
        Assert.Equal("02-123-4567", item.PhoneNumber); Assert.Equal("등록 업무 담당", item.DisplayName);
        Assert.Equal("source:DistributedSite:site", item.SourceNodeKey); Assert.Equal(2, item.SourceRevision);
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("db-phone-must-not-be-used", json); Assert.DoesNotContain("private-address", json);
        Assert.DoesNotContain("other-private-phone", json); Assert.Equal(0, f.SenderCalls);
        Assert.DoesNotContain(f.Db.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Fact]
    public async Task 전문창고접근권한이없거나_번호미등록이면_역할참여자의_번호로대체하지않는다()
    {
        await using var f = new Fixture(); await f.SeedSite();
        f.WarehouseRows = [];
        var denied = await f.Read("storage"); Assert.Equal("not-visible", denied.StatusCode); Assert.Empty(denied.Contacts);
        f.WarehouseRows = [new() { Id = 9, IsActive = true, 담당자명 = "등록 업무 담당", 연락처 = "" }];
        var missing = await f.Read("storage"); Assert.Equal("unregistered", missing.StatusCode); Assert.Empty(missing.Contacts);
    }

    [Fact]
    public async Task 원천에서입증한_화물의_기존현장연락처만_배송단계에_반환한다()
    {
        await using var f = new Fixture(); await f.SeedOrder("order-one", "cargo-one");
        var response = await f.Read("delivery"); Assert.Equal("available", response.StatusCode);
        Assert.Equal(2, response.Contacts.Count); Assert.All(response.Contacts, item => Assert.Equal("source:DistributedOrder:order-one", item.SourceNodeKey));
        Assert.Contains(response.Contacts, item => item.PurposeCode == "pickup" && item.PhoneNumber == "02-222-3333");
        Assert.Contains(response.Contacts, item => item.PurposeCode == "dropoff" && item.PhoneNumber == "010-1111-2222");
        Assert.Equal(new[] { "cargo-one", "cargo-one" }, f.RequestReads);
        Assert.DoesNotContain("private-dropoff-address", JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task 실제주문과_화물당사자관계가다르면_다른의뢰의_번호를조회하지않는다()
    {
        await using var f = new Fixture(); await f.SeedOrder("order-one", "cargo-one");
        var cargo = await f.Db.화주운송의뢰.SingleAsync(); cargo.주문자UserId = "someone-else"; await f.Db.SaveChangesAsync();
        var response = await f.Read("delivery"); Assert.Equal("source-changed", response.StatusCode);
        Assert.Empty(response.Contacts); Assert.Equal(0, f.SenderCalls);
    }

    [Fact]
    public async Task 출고묶음은_이미연결된검증주문만_참고하고_다른주문은_열거하지않는다()
    {
        await using var f = new Fixture(); await f.SeedOrder("order-one", "cargo-one");
        f.Source("DistributedWave", "wave", 3, "Open");
        f.Db.Add(new 분산배송WaveRecord { WaveId = "wave", Revision = 3, StatusCode = "Open" });
        f.Db.Add(new 분산배송주문Record { OrderId = "unlinked-order", WaveId = "wave", CargoRequestId = "unlinked-cargo", Revision = 1 });
        await f.Db.SaveChangesAsync();
        var response = await f.Read("outbound"); Assert.Equal(2, response.Contacts.Count);
        Assert.All(response.Contacts, item => Assert.Equal("source:DistributedWave:wave", item.SourceNodeKey));
        Assert.Equal(new[] { "cargo-one", "cargo-one" }, f.RequestReads); Assert.DoesNotContain("unlinked", JsonSerializer.Serialize(response));
        var previousKey = response.Contacts[0].ContactKey;
        var wave = await f.Db.Set<분산배송WaveRecord>().SingleAsync(); wave.Revision++;
        f.SourceStates.Single(item => item.KindCode == "DistributedWave").VerifiedRevision = 4;
        await f.Db.SaveChangesAsync();
        Assert.NotEqual(previousKey, (await f.Read("outbound")).Contacts[0].ContactKey);
    }

    [Fact]
    public async Task 기사상세가_현재정보제공을보류하면_전화번호를반환하지않는다()
    {
        await using var f = new Fixture("driver"); await f.SeedOrder("order-one", "cargo-one"); f.RequestView = null;
        var updated = new DateTime(2026, 10, 24, 1, 0, 0, DateTimeKind.Utc);
        f.Db.운송원장.Add(new() { Id = 7, 의뢰Id = "cargo-one", 기사_운송자 = "driver", 확정기사Id = "driver",
            배차업무유형 = 상태값.배차업무유형.용달운송, 배차큐단계 = 상태값.배차큐단계.확정, UpdatedAt = updated });
        await f.Db.SaveChangesAsync();
        f.DriverView = new() { Id = 7, 기사_운송자 = "driver", UpdatedAt = updated, 개인정보제공보류 = true,
            상차연락처 = "must-not-return" };
        var response = await f.Read("delivery"); Assert.Empty(response.Contacts); Assert.Equal("not-visible", response.StatusCode);
    }

    [Fact]
    public async Task 읽는동안_부모판본이나_거점관계가변하면_조회한번호를반환하지않는다()
    {
        await using var f = new Fixture(); await f.SeedSite();
        f.OnWarehouseRead = async () => { var site = await f.Db.Set<분산거점Record>().SingleAsync(); site.WarehouseId = 10; await f.Db.SaveChangesAsync(); };
        Assert.Equal(409, (await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("storage"))).StatusCode);
        f.OnWarehouseRead = null; var reset = await f.Db.Set<분산거점Record>().SingleAsync(); reset.WarehouseId = 9; await f.Db.SaveChangesAsync();
        f.OnFinalRead = () => f.Detail.Revision++;
        Assert.Equal(409, (await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("storage"))).StatusCode);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("phone")]
    [InlineData("name")]
    public async Task 창고판본과_별도로_전문권한이나_등록연락정보가바뀌면_번호반환을_보류한다(string changed)
    {
        await using var f = new Fixture(); await f.SeedSite();
        var warehouse = await f.Db.창고.AsNoTracking().SingleAsync();
        f.OnFinalRead = () =>
        {
            if (changed == "permission") f.WarehouseRows = [];
            else if (changed == "phone") f.WarehouseRows[0].연락처 = "02-999-8888";
            else f.WarehouseRows[0].담당자명 = "변경된 담당자";
        };
        var error = await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("storage"));
        Assert.Equal(409, error.StatusCode); Assert.Equal(2, f.WarehouseCalls);
        Assert.Equal(warehouse.UpdatedAt, (await f.Db.창고.AsNoTracking().SingleAsync()).UpdatedAt);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("phone")]
    [InlineData("name")]
    public async Task 화물원장판본이_같아도_현재업무담당조회권한이나_등록정보가바뀌면_번호반환을_보류한다(string changed)
    {
        await using var f = new Fixture(); await f.SeedOrder("order-one", "cargo-one");
        f.OnFinalRead = () =>
        {
            if (changed == "permission") f.RequestView = null;
            else if (changed == "phone") f.RequestView!.하차!.연락처.전화번호 = "010-9999-8888";
            else f.RequestView!.픽업!.연락처.이름 = "변경된 상차 담당";
        };
        var error = await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("delivery"));
        Assert.Equal(409, error.StatusCode); Assert.Equal(new[] { "cargo-one", "cargo-one" }, f.RequestReads);
    }

    [Theory]
    [InlineData("consent")]
    [InlineData("permission")]
    [InlineData("phone")]
    [InlineData("name")]
    public async Task 운송판본과_별도로_기사정보제공동의나_현재상세가_달라지면_번호반환을_보류한다(string changed)
    {
        await using var f = new Fixture("driver"); await f.SeedOrder("order-one", "cargo-one"); f.RequestView = null;
        var updated = new DateTime(2026, 10, 24, 1, 0, 0, DateTimeKind.Utc);
        f.Db.운송원장.Add(new() { Id = 7, 의뢰Id = "cargo-one", 기사_운송자 = "driver", 확정기사Id = "driver",
            배차업무유형 = 상태값.배차업무유형.용달운송, 배차큐단계 = 상태값.배차큐단계.확정, UpdatedAt = updated });
        await f.Db.SaveChangesAsync();
        f.DriverView = new() { Id = 7, 기사_운송자 = "driver", UpdatedAt = updated,
            상차담당자명 = "기존 상차 담당", 상차연락처 = "02-222-3333", 수령자명 = "기존 수령 담당", 수령자연락처 = "010-1111-2222" };
        f.OnFinalRead = () =>
        {
            if (changed == "consent") f.DriverView!.개인정보제공보류 = true;
            else if (changed == "permission") f.DriverView = null;
            else if (changed == "phone") f.DriverView!.수령자연락처 = "010-9999-8888";
            else f.DriverView!.수령자명 = "변경된 수령 담당";
        };
        var error = await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("delivery"));
        Assert.Equal(409, error.StatusCode); Assert.Equal(3, f.SenderCalls);
        Assert.Equal(updated, (await f.Db.운송원장.AsNoTracking().SingleAsync()).UpdatedAt);
    }

    [Fact]
    public async Task 마지막전문권한_재조회가실패해도_기존번호를반환하지않고_판본보류로_처리한다()
    {
        await using var f = new Fixture(); await f.SeedSite(); var calls = 0;
        f.OnWarehouseRead = () => ++calls == 2
            ? Task.FromException(new UnauthorizedAccessException("private-permission-error")) : Task.CompletedTask;
        var error = await Assert.ThrowsAsync<공간공동운영업무연락Exception>(() => f.Read("storage"));
        Assert.Equal(409, error.StatusCode); Assert.DoesNotContain("private-permission-error", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, f.WarehouseCalls);
    }

    [Fact]
    public async Task 연락처가없는_절차와_마트전용원천은_미지원이나미연결로명시한다()
    {
        await using var f = new Fixture();
        Assert.Equal("unsupported", (await f.Read("production")).StatusCode);
        Assert.Equal("not-linked", (await f.Read("delivery")).StatusCode);
        f.Source("MartOrder", "123", 1, "Received");
        var response = await f.Read("delivery"); Assert.Empty(response.Contacts); Assert.Equal("unsupported", response.StatusCode);
        Assert.Equal(0, f.SenderCalls); Assert.Equal(0, f.WarehouseCalls);
    }

    [Fact]
    public async Task API는_인증과_no_store를유지하고_전화필드는_개인정보보호계약을가진다()
    {
        await using var f = new Fixture();
        Assert.NotEmpty(typeof(공간공동운영업무연락Controller).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Contains(IsmsPProtectedDataAttributeReader.Read(typeof(공간공동운영업무연락Dto)), member => member.PropertyName == "PhoneNumber");
        var controller = new 공간공동운영업무연락Controller(f.Service())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<OkObjectResult>(await controller.조회("hub", "cycle", "production", 4, default));
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string actor;
        public Fixture(string actor = "seller") { this.actor = actor; }
        public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
        public 공간공동운영상세Response Detail { get; } = new()
        {
            LedgerId = "hub", Revision = 4, ExecutionMode = "Simulation",
            Cycles = [new() { CycleId = "cycle", Number = 1, TermsRevision = 1, StatusCode = "prepared" }],
            SharedOutcomes = new() { ExecutionMode = "Simulation", MatchesCurrentExecutionMode = true,
                Cycles = [new() { CycleId = "cycle", Number = 1 }] }
        };
        public 공간공동운영통합회차Dto Cycle { get; } = new()
        { NodeKey = "cycle:cycle", CycleId = "cycle", Number = 1, TermsRevision = 1, StatusCode = "prepared" };
        public 공간공동운영통합Response Snapshot { get; } = new() { LedgerId = "hub", Revision = 4, ExecutionMode = "Simulation" };
        public IReadOnlyList<창고요약응답> WarehouseRows { get; set; } = [new() { Id = 9, IsActive = true, 담당자명 = "업무 담당", 연락처 = "02-123-4567" }];
        public 화주운송의뢰응답? RequestView { get; set; }
        public 기사운송상세응답? DriverView { get; set; }
        public IReadOnlyList<공간공동운영원천상태Dto> SourceStates => Detail.SharedOutcomes!.Cycles[0].Sources;
        public bool CanRead { get; set; } = true;
        public int SnapshotCalls { get; private set; }
        public int WarehouseCalls { get; private set; }
        public int SenderCalls { get; private set; }
        public List<string> RequestReads { get; } = [];
        public Func<Task>? OnWarehouseRead { get; set; }
        public Action? OnFinalRead { get; set; }
        public 공간공동운영업무연락Query Service(string? explicitActor = "use-fixture")
        {
            var reads = 0;
            return new(Stub<I공간공동운영UseCase>((method, _) =>
            {
                if (method.Name != "상세Async") throw new NotSupportedException();
                if (++reads > 1) OnFinalRead?.Invoke();
                return Task.FromResult(CanRead ? Detail : null);
            }), Stub<I공간공동운영통합Query>((_, _) =>
            { SnapshotCalls++; Snapshot.Cycles = [Cycle]; return Task.FromResult<공간공동운영통합Response?>(Snapshot); }), Db,
            Stub<IWarehouseOperationService>((method, _) => method.Name == "GetWarehousesAsync" ? ReadWarehouses() : throw new NotSupportedException()),
            Stub<ISender>((_, args) =>
            {
                SenderCalls++;
                return args![0] switch
                {
                    의뢰단건조회Query request => Request(request.RequestId),
                    운송상세조회Query => Task.FromResult(DriverView),
                    _ => throw new NotSupportedException()
                };
            }), new Actor(explicitActor == "use-fixture" ? actor : explicitActor), new ExecutionModeFake(), TimeProvider.System);
        }
        private Task<화주운송의뢰응답?> Request(string id) { RequestReads.Add(id); return Task.FromResult(RequestView); }
        private async Task<창고목록응답> ReadWarehouses()
        { WarehouseCalls++; if (OnWarehouseRead is not null) await OnWarehouseRead(); return new() { Items = WarehouseRows }; }
        public async Task<공간공동운영업무연락Response> Read(string stage)
            => Assert.IsType<공간공동운영업무연락Response>(await Service().조회Async("hub", "cycle", stage, 4));
        public void Source(string kind, string id, long revision, string state)
        {
            var key = "source:" + kind + ":" + id;
            Detail.Cycles[0].References = Detail.Cycles[0].References.Append(new 공간공동운영전문참조Dto { KindCode = kind, ReferenceId = id }).ToArray();
            Cycle.SharedSourceKeys = Cycle.SharedSourceKeys.Append(key).ToArray();
            Snapshot.SharedSources = Snapshot.SharedSources.Append(new 공간공동운영통합원천Dto
            { NodeKey = key, KindCode = kind, ReferenceId = id, Verified = true, StatusCode = state }).ToArray();
            Detail.SharedOutcomes!.Cycles[0].Sources = SourceStates.Append(new 공간공동운영원천상태Dto
            { KindCode = kind, ReferenceId = id, Verified = true, VerifiedRevision = revision, StatusCode = state }).ToArray();
        }
        public async Task SeedSite()
        {
            Source("DistributedSite", "site", 2, "Active");
            Db.Add(new 분산거점Record { SiteId = "site", WarehouseId = 9, Revision = 2, StatusCode = "Active", OwnerConsented = true, ManagerConsented = true });
            Db.창고.Add(new() { Id = 9, 창고명 = "실제 창고", IsActive = true, 연락처 = "db-phone-must-not-be-used" });
            await Db.SaveChangesAsync();
        }
        public async Task SeedOrder(string id, string cargoId)
        {
            Source("DistributedOrder", id, 2, "Reserved");
            Db.Add(new 분산배송주문Record { OrderId = id, WaveId = "wave", CargoRequestId = cargoId,
                SellerUserId = "seller", CustomerUserId = "buyer", Revision = 2 });
            Db.화주운송의뢰.Add(new() { 의뢰Id = cargoId, 화주Id = "seller", 주문자UserId = "buyer" });
            await Db.SaveChangesAsync();
            RequestView = new()
            {
                의뢰Id = cargoId, 화주Id = "seller", 주문자UserId = "buyer",
                픽업 = new() { 연락처 = new() { 이름 = "등록 상차 담당", 전화번호 = "02-222-3333" } },
                하차 = new() { 주소 = new() { 도로명주소 = "private-dropoff-address" }, 연락처 = new() { 이름 = "등록 수령 담당", 전화번호 = "010-1111-2222" } }
            };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    public class PortProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    { var value = DispatchProxy.Create<T, PortProxy>(); ((PortProxy)(object)value).Handler = handler; return value; }
    private sealed record Actor(string? UserId) : ICurrentUserAccessor { public string? Role => "community"; }
    private sealed class ExecutionModeFake : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
