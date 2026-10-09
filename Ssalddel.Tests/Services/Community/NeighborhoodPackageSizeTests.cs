using System.Reflection;
using System.Text.Json;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Migrations;
using Ssalddel.Services.Community;
using Ssalddel.Services.Fulfillment;
using Ssalddel.Services.Privacy;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.External.Google;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>생산용 생활 입력 UseCase의 검증·fingerprint·원천 저장을 검사합니다. 운송/주소/동의 경계는 격리 대역입니다.</summary>
public sealed class NeighborhoodPackageSizeTests
{
    [Fact]
    public void 미입력은구형JSON과fingerprint를보존하고_업체규격을임의생성하지않는다()
    {
        var request = Input();
        var original = JsonSerializer.Serialize(request);
        Assert.DoesNotContain("PackageSizeCode", original);
        request.PackageSizeCode = null;
        Assert.Equal(original, JsonSerializer.Serialize(request));
        Assert.DoesNotContain("PackageSizeCode", JsonSerializer.Serialize(new NeighborhoodDeliveryResponse()));
        Assert.Equal("미입력", NeighborhoodPackageSizes.Label(null));
        Assert.False(NeighborhoodPackageSizes.IsKnown(null));
    }

    [Theory]
    [InlineData("Small", "소형")]
    [InlineData("Medium", "중형")]
    [InlineData("Large", "대형")]
    public async Task 공통크기는서버검증과본인상세에서보존한다(string size, string label)
    {
        await using var f = new Fixture();
        var request = Input(); request.PackageSizeCode = size;
        var created = await f.UseCase().등록Async(request);
        Assert.True(created.IsSuccess, string.Join(";", created.Errors));
        Assert.Equal(size, (await f.Db.화주운송의뢰.SingleAsync()).생활배송포장크기Code);
        Assert.Equal(size, created.Value.PackageSizeCode);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(size, (await f.UseCase().내상세Async(created.Value.RequestId))!.PackageSizeCode);
        Assert.Equal(label, NeighborhoodPackageSizes.Label(size));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("small")]
    [InlineData("XL")]
    public async Task 미지정null외_빈문자나알수없는크기는조회저장전에거절한다(string size)
    {
        await using var f = new Fixture(); var request = Input(); request.PackageSizeCode = size;
        var quoted = await f.UseCase().견적Async(request);
        var created = await f.UseCase().등록Async(request);
        Assert.Equal("InvalidPackageSize", quoted.Errors[0].Metadata["ErrorCode"]);
        Assert.Equal("InvalidPackageSize", created.Errors[0].Metadata["ErrorCode"]);
        Assert.Equal(0, f.TransportCalls);
        Assert.Empty(await f.Db.화주운송의뢰.ToListAsync());
    }

    [Fact]
    public async Task 앞뒤공백을정규화하고_같은번호의크기변경은거절한다()
    {
        await using var f = new Fixture(); var request = Input(); request.PackageSizeCode = " Small ";
        var first = await f.UseCase().등록Async(request); Assert.True(first.IsSuccess);
        Assert.Equal("Small", request.PackageSizeCode);
        request.PackageSizeCode = "Small";
        var retry = await f.UseCase().등록Async(request);
        Assert.True(retry.IsSuccess); Assert.True(retry.Value.IdempotentReplay);
        Assert.Equal(first.Value.RequestId, retry.Value.RequestId);
        request.PackageSizeCode = "Medium";
        var changed = await f.UseCase().등록Async(request);
        Assert.Equal("IdempotencyConflict", changed.Errors[0].Metadata["ErrorCode"]);
        Assert.Single(await f.Db.화주운송의뢰.ToListAsync());
        Assert.Equal("Small", (await f.Db.화주운송의뢰.SingleAsync()).생활배송포장크기Code);
    }

    [Fact]
    public async Task 원천저장후크기저장이중단된경우_같은요청만복구하고미입력을채우지않는다()
    {
        await using var f = new Fixture(); var request = Input(); request.PackageSizeCode = "Large";
        var first = await f.UseCase().등록Async(request); Assert.True(first.IsSuccess);
        var row = await f.Db.화주운송의뢰.SingleAsync(); row.생활배송포장크기Code = null; await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        var resumed = await f.UseCase().등록Async(request);
        Assert.True(resumed.IsSuccess); Assert.True(resumed.Value.IdempotentReplay);
        Assert.Equal("Large", resumed.Value.PackageSizeCode);
        var unspecified = Input();
        var legacy = await f.UseCase().등록Async(unspecified); Assert.True(legacy.IsSuccess);
        Assert.Null(legacy.Value.PackageSizeCode);
        Assert.Null((await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == legacy.Value.RequestId)).생활배송포장크기Code);
    }

    [Fact]
    public async Task 견적은활성서버후보만전달하고_기능이꺼지면빈목록이다()
    {
        await using var f = new Fixture();
        var automatic = DispatchProxy.Create<I자동수행Coordinator, AutomaticProxy>();
        var proxy = (AutomaticProxy)(object)automatic;
        proxy.Providers = [new("registered-test-only", "등록 검증 후보 · 모의", true)];
        var enabled = await f.UseCase(automatic).견적Async(Input());
        Assert.True(enabled.IsSuccess); Assert.Equal(proxy.Providers, enabled.Value.AutomaticProviders);
        Assert.Equal(FulfillmentSourceKinds.NeighborhoodDelivery, proxy.LastSourceKind);
        proxy.Enabled = false;
        var disabled = await f.UseCase(automatic).견적Async(Input());
        Assert.True(disabled.IsSuccess); Assert.Empty(disabled.Value.AutomaticProviders);
    }

    [Theory]
    [InlineData("Small")]
    [InlineData("Medium")]
    [InlineData("Large")]
    public async Task 생산용Mongo원장builder와SQL투영은크기를왕복하고내부큐를만들지않는다(string size)
    {
        await using var f = new Fixture();
        var source = ProjectionSource(); source.생활배송포장크기Code = size;
        var ledger = Ledger(source);
        Assert.Equal(size, 운송원장업무투영Snapshot.생성(ledger)!.PackageSizeCode);
        await new 운송원장업무투영Handler(f.Db, NullLogger<운송원장업무투영Handler>.Instance).동기화Async(ledger);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(size, (await f.Db.화주운송의뢰.SingleAsync()).생활배송포장크기Code);
        Assert.Empty(await f.Db.운송원장.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XL")]
    [InlineData("Large")]
    public async Task 구형미입력_알수없는값_늦은다른값은기존크기근거를덮지않는다(string? incoming)
    {
        await using var f = new Fixture(); var source = ProjectionSource();
        source.생활배송포장크기Code = incoming; var ledger = Ledger(source);
        source.생활배송포장크기Code = "Small"; f.Db.화주운송의뢰.Add(source); await f.Db.SaveChangesAsync();
        await new 운송원장업무투영Handler(f.Db, NullLogger<운송원장업무투영Handler>.Instance).동기화Async(ledger);
        f.Db.ChangeTracker.Clear(); Assert.Equal("Small", (await f.Db.화주운송의뢰.SingleAsync()).생활배송포장크기Code);
        Assert.Empty(await f.Db.운송원장.ToListAsync());
    }

    [Fact]
    public async Task 크기원장동기화실패는수행전보류하고_같은접수재요청에서중복없이복구한다()
    {
        await using var f = new Fixture(); var request = Input(); request.PackageSizeCode = "Medium";
        var sync = new RecordingSync { FailOnce = true };
        var first = await f.UseCase(ledgerSync: sync).등록Async(request);
        Assert.True(first.IsFailed); Assert.Equal("RegistrationPending", first.Errors[0].Metadata["ErrorCode"]);
        Assert.Single(await f.Db.화주운송의뢰.ToListAsync()); Assert.Empty(await f.Db.운송원장.ToListAsync());
        var source = await f.Db.화주운송의뢰.SingleAsync(); var unchangedRevisionTime = source.UpdatedAt;
        var second = await f.UseCase(ledgerSync: sync).등록Async(request);
        Assert.True(second.IsSuccess); Assert.True(second.Value.IdempotentReplay);
        Assert.Equal("Medium", second.Value.PackageSizeCode); Assert.Equal(2, sync.Calls);
        Assert.Single(await f.Db.화주운송의뢰.ToListAsync());
        Assert.Equal(unchangedRevisionTime, (await f.Db.화주운송의뢰.SingleAsync()).UpdatedAt);
    }

    [Fact]
    public async Task 최초자동동의전에크기를동기화하고_동의후재요청은원천판본을바꾸지않는다()
    {
        await using var f = new Fixture();
        var automatic = DispatchProxy.Create<I자동수행Coordinator, AutomaticProxy>(); var proxy = (AutomaticProxy)(object)automatic;
        proxy.Providers = [new("registered-test-only", "등록 검증 후보 · 모의", true)];
        var sync = new RecordingSync(); var request = Input(); request.PackageSizeCode = "Small";
        request.AutoFulfillment = new() { ClientRequestId = Guid.NewGuid(), MaxUserChargeKrw = 10000,
            AllowedProviderIds = ["registered-test-only"], ProviderInformationSharingAgreed = true, AutomaticSubmissionAgreed = true };
        var first = await f.UseCase(automatic, sync).등록Async(request); Assert.True(first.IsSuccess, string.Join(";", first.Errors));
        Assert.Equal(1, sync.Calls); Assert.Equal(1, proxy.Snapshot!.ConsentRevision);
        var originalTime = (await f.Db.화주운송의뢰.SingleAsync()).UpdatedAt;
        var replay = await f.UseCase(automatic, sync).등록Async(request); Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IdempotentReplay); Assert.Equal(1, sync.Calls);
        Assert.Equal(originalTime, (await f.Db.화주운송의뢰.SingleAsync()).UpdatedAt);
    }

    [Fact]
    public async Task SQLite좁은migration은구형행을null로남기고선택크기를왕복한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE shipper_requests (id INTEGER PRIMARY KEY);");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO shipper_requests (id) VALUES (1);");
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new AddNeighborhoodPackageSize().UpOperations))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        using var read = connection.CreateCommand(); read.CommandText = "SELECT neighborhood_package_size_code FROM shipper_requests WHERE id = 1";
        Assert.Equal(DBNull.Value, await read.ExecuteScalarAsync());
        await db.Database.ExecuteSqlRawAsync("UPDATE shipper_requests SET neighborhood_package_size_code = 'Medium' WHERE id = 1;");
        Assert.Equal("Medium", await read.ExecuteScalarAsync());
    }

    [Fact]
    public void migration은기존값을덮지않는nullable열하나만추가한다()
    {
        var migration = new AddNeighborhoodPackageSize();
        var operation = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("shipper_requests", operation.Table);
        Assert.Equal("neighborhood_package_size_code", operation.Name);
        Assert.True(operation.IsNullable); Assert.Null(operation.DefaultValue); Assert.Equal(16, operation.MaxLength);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Encryption());
        var property = db.Model.FindEntityType(typeof(화주운송의뢰))!.FindProperty(nameof(화주운송의뢰.생활배송포장크기Code))!;
        Assert.True(property.IsNullable); Assert.Equal(16, property.GetMaxLength());
    }

    private static NeighborhoodDeliveryRequest Input() => new()
    {
        ClientRequestId = Guid.NewGuid(), CargoName = "검증 소포", Quantity = 1, WeightKg = 1m,
        PrivacyConsentEvidenceId = Guid.NewGuid(), DispatchRequested = true, DirectPaymentAgreed = true, AgreedFareKrw = 8200m,
        Pickup = new() { 주소 = new() { 도로명주소 = "검증 픽업 주소" }, 연락처 = new() { 이름 = "검증 담당", 전화번호 = "010-0000-0000" },
            시간창 = new() { 시작일시 = DateTime.UtcNow.AddHours(1), 종료일시 = DateTime.UtcNow.AddHours(2) } },
        Dropoff = new() { 주소 = new() { 도로명주소 = "검증 전달 주소" }, 연락처 = new() { 이름 = "검증 수령", 전화번호 = "010-0000-0001" } }
    };

    private static 화주운송의뢰 ProjectionSource() => new()
    {
        의뢰Id = "package-projection", 주문자UserId = "package-owner", 화주Id = "package-owner", 화물종류 = "검증 소포",
        모의처리처수행 = true, 클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "projection",
        픽업_도로명주소 = "검증 픽업 주소", 픽업_연락처_전화번호 = "010-0000-0000", 하차_도로명주소 = "검증 전달 주소",
        픽업_시간창_시작일시 = new(2026, 10, 7, 11, 0, 0, DateTimeKind.Utc), 픽업_시간창_종료일시 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc)
    };

    private static 커뮤니티원장Dto Ledger(화주운송의뢰 source)
    {
        var save = 운송원장Mongo동기화Builder.저장요청생성(source, null);
        return new() { 원장Id = save.원장Id!, 원장템플릿Key = save.원장템플릿Key,
            상태 = save.상태!, 현재단계Key = save.현재단계Key, 블록목록 = save.블록목록,
            참여자목록 = save.참여자목록, 외부참조 = save.외부참조, 확장속성 = save.확장속성 };
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Encryption());
        private readonly I화주운송의뢰UseCase transport;
        private readonly TransportProxy proxy;
        public int TransportCalls => proxy.Calls;
        public Fixture()
        {
            transport = DispatchProxy.Create<I화주운송의뢰UseCase, TransportProxy>(); proxy = (TransportProxy)(object)transport; proxy.Db = Db;
        }
        public 생활배송의뢰UseCase UseCase(I자동수행Coordinator? automatic = null, I운송원장Mongo동기화Service? ledgerSync = null)
            => new(Db, new Current(), transport, new Consent(), new Geo(), new Activation(), automatic: automatic, ledgerSync: ledgerSync);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    public class TransportProxy : DispatchProxy
    {
        public SsalddelContext Db { get; set; } = null!;
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            return method?.Name switch
            {
                nameof(I화주운송의뢰UseCase.기준운임견적Async) => Task.FromResult(Result.Ok(new 화주운송기준운임견적응답 { 최종운임 = 8200m, 예상거리Km = 3.2m })),
                nameof(I화주운송의뢰UseCase.의뢰생성Async) => Create((화주운송의뢰생성요청)args![0]!),
                nameof(I화주운송의뢰UseCase.의뢰단건조회Async) => Task.FromResult<화주운송의뢰응답?>(Response((string)args![0]!)),
                nameof(I화주운송의뢰UseCase.현장지급처리Async) => Task.FromResult(Result.Ok(Response((string)args![0]!))),
                _ => throw new NotSupportedException(method?.Name)
            };
        }
        private async Task<Result<화주운송의뢰응답>> Create(화주운송의뢰생성요청 request)
        {
            var id = Guid.NewGuid().ToString("N");
            Db.화주운송의뢰.Add(new() { 의뢰Id = id, 주문자UserId = "package-owner", 화주Id = "package-owner",
                클라이언트요청Id = request.클라이언트요청Id!, 정산메모 = request.정산조건!.정산메모!, 화물종류 = request.화물!.화물종류,
                모의처리처수행 = request.SimulationFulfillment, 자동처리처수행 = request.SimulationAutomaticFulfillment,
                모의처리희망완료Utc = request.SimulationDesiredCompletionUtc });
            await Db.SaveChangesAsync(); return Result.Ok(Response(id));
        }
        private static 화주운송의뢰응답 Response(string id) => new() { 의뢰Id = id, 의뢰상태 = 상태값.의뢰상태.생성됨, 배차상태 = 상태값.배차상태.미시작 };
    }

    public class AutomaticProxy : DispatchProxy
    {
        public bool Enabled { get; set; } = true;
        public IReadOnlyList<AutoFulfillmentProviderDescriptor> Providers { get; set; } = [];
        public string? LastSourceKind { get; private set; }
        public AutoFulfillmentSnapshot? Snapshot { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == "get_Enabled") return Enabled;
            if (method?.Name == nameof(I자동수행Coordinator.수행처후보)) { LastSourceKind = (string)args![0]!; return Providers; }
            if (method?.Name == nameof(I자동수행Coordinator.상태Async)) return Task.FromResult(Snapshot);
            if (method?.Name == nameof(I자동수행Coordinator.사전동의저장Async))
            {
                var key = (FulfillmentSourceKey)args![0]!;
                Snapshot = new() { SourceKind = key.SourceKind, SourceId = key.SourceId, ConsentRevision = 1, Revision = 1 };
                return Task.FromResult(Result.Ok(Snapshot));
            }
            if (method?.Name == nameof(I자동수행Coordinator.예약Async)) return Task.CompletedTask;
            if (method?.Name == nameof(I자동수행Coordinator.선정실행Async)) return Task.FromResult(Result.Ok(Snapshot!));
            throw new NotSupportedException(method?.Name);
        }
    }
    private sealed class Current : ICurrentUserAccessor { public string? UserId => "package-owner"; public string? Role => "일반회원"; }
    private sealed class RecordingSync : I운송원장Mongo동기화Service
    {
        public bool FailOnce { get; set; }
        public int Calls { get; private set; }
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 source, string user, CancellationToken ct = default)
        { Calls++; return Task.FromResult<커뮤니티원장Dto?>(FailOnce && Calls == 1 ? null : Ledger(source)); }
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(살뜰.도메인.운송.운송원장 source, string user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Geo : IGeocodingService { public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address) => Task.FromResult<(decimal, decimal)?>(address.Contains("픽업") ? (37.58m, 127.08m) : (37.59m, 127.09m)); }
    private sealed class Activation : ISsalddelBackgroundJobActivationPolicy { public SsalddelBackgroundWorkloadActivation Evaluate(string key) => new(false, SsalddelBackgroundWorkloadActivationCodes.OperationalModeRequired, "DomesticTransportWorkflow"); }
    private sealed class Consent : I신청개인정보동의증적Service
    {
        public Task 유효한동의요구Async(Guid? id, string work, string source, string user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<신청개인정보동의증적Response> 동의기록Async(신청개인정보동의기록Request request, string user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response?> 내증적조회Async(Guid id, string user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<신청개인정보동의증적Response> 철회Async(Guid id, 신청개인정보동의철회Request request, string user, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
