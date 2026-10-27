using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

/// <summary>기존 보호 원장의 새 약속 부록과 주문 부록 보존 시험입니다. 실제 DB·외부 알림을 실행하지 않습니다.</summary>
public sealed class 공간공동운영예약보존Tests
{
    private const string AppointmentJson = "{\"schemaVersion\":1,\"bookings\":[],\"receipts\":[]}";

    [Fact]
    public async Task 새부록없는_기존보호payload는_그대로읽고_예약을_가정하지않는다()
    {
        var f = new Fixture(includeAppointment: false);
        var record = (await f.Store.조회Async("hub"))!;
        Assert.Null(record.WorkReservationExtensionJson);
        Assert.Equal("private-order-content", record.OrderExtensionJson);
        Assert.Empty(공간공동운영예약UseCase.회차준비보류사유(record, null, DateTimeOffset.UtcNow));
        Assert.Null(f.Ledgers.LastSave);
    }

    [Fact]
    public async Task 예약부록의_보호왕복과_주문갱신은_다른부록과_서명관계다이어그램을_보존한다()
    {
        var f = new Fixture(includeAppointment: true);
        var record = (await f.Store.조회Async("hub"))!;
        Assert.Equal(AppointmentJson, record.WorkReservationExtensionJson);
        record.OrderExtensionJson = "updated-private-order"; record.Revision++;
        Assert.True(await f.Store.교체Async(record, 5, "operator"));
        var reread = (await f.Store.조회Async("hub"))!;
        Assert.Equal(AppointmentJson, reread.WorkReservationExtensionJson);
        Assert.Equal("updated-private-order", reread.OrderExtensionJson);
        var request = Assert.IsType<커뮤니티원장저장요청>(f.Ledgers.LastSave);
        Assert.Same(f.Original.다이어그램스냅샷, request.다이어그램스냅샷);
        Assert.Same(f.Original.포함원장목록, request.포함원장목록);
        Assert.Same(f.Original.참여자목록, request.참여자목록);
        Assert.Equal("signature-preserved", request.확장속성["Signature"]);
        Assert.Equal("source-preserved", request.외부참조["Source"]);
        Assert.Equal("seal-preserved", request.블록목록.Single(block => block.BlockId == "signature").Data["Seal"]);
        var visible = JsonSerializer.Serialize(f.Ledgers.Current);
        Assert.DoesNotContain("updated-private-order", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("schemaVersion", visible, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 기존공동운영_일시중지명령도_예약부록과주문부록을_덮어쓰지않는다()
    {
        var f = new Fixture(includeAppointment: true);
        var service = new 공간공동운영UseCase(f.Store, new Actor(), new TestExecutionPolicy(),
            new Official생활교류공개지역Source(), TimeProvider.System, new Bridge());
        var result = await service.변경Async("hub", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 5,
            ExpectedTermsRevision = 2, Action = 공간공동운영명령Codes.Pause });
        Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(error => error.Message)));
        var record = (await f.Store.조회Async("hub"))!;
        Assert.Equal("paused", record.StatusCode);
        Assert.Equal(AppointmentJson, record.WorkReservationExtensionJson);
        Assert.Equal("private-order-content", record.OrderExtensionJson);
        Assert.Equal("seal-preserved", f.Ledgers.LastSave!.블록목록.Single(block => block.BlockId == "signature").Data["Seal"]);
    }

    private sealed class Fixture
    {
        public LedgerStub Ledgers { get; } = new();
        public 커뮤니티원장Dto Original { get; }
        public 공간공동운영원장Store Store { get; }
        public Fixture(bool includeAppointment)
        {
            var protection = new EphemeralDataProtectionProvider();
            var record = new 공간공동운영Record { LedgerId = "hub", Revision = 5, TermsRevision = 2,
                OwnerUserId = "operator", OperatorUserId = "operator", ExecutionMode = "Simulation",
                CreateRequestId = Guid.NewGuid(), CreateFingerprint = "original-fingerprint",
                OrderExtensionJson = "private-order-content", WorkReservationExtensionJson = includeAppointment ? AppointmentJson : null,
                Participants = [new() { UserId = "operator", RoleCode = 공간공동운영역할Codes.SpaceProvider,
                    StateCode = "accepted", AgreedTermsRevision = 2 }] };
            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            if (!includeAppointment) Assert.DoesNotContain("workReservationExtensionJson", json, StringComparison.Ordinal);
            var payload = protection.CreateProtector("Ssalddel.SpaceCooperation.LedgerBlock.v1").Protect(json);
            Original = new() { 원장Id = record.LedgerId, Revision = record.Revision, 생성자UserId = record.OwnerUserId,
                커뮤니티Id = "platform", 원장템플릿Key = CommunityLedgerTemplateKeys.SpaceCooperation,
                블록목록 = [new() { BlockId = 공간공동운영원장Store.BlockId,
                    Data = new Dictionary<string, string> { ["ProtectedPayload"] = payload } },
                    new() { BlockId = "signature", Data = new Dictionary<string, string> { ["Seal"] = "seal-preserved" } }],
                참여자목록 = [new() { UserId = "operator", DisplayName = "공간 참여자" }],
                포함원장목록 = [new() { 원장Id = "existing-child", 관계유형 = CommunityLedgerRelationTypes.Reference }],
                다이어그램스냅샷 = new() { DiagramId = "original-diagram" },
                외부참조 = new Dictionary<string, string> { [공간공동운영원장Store.CanonicalMarker] = "v1", ["Source"] = "source-preserved" },
                확장속성 = new Dictionary<string, string> { ["Signature"] = "signature-preserved" } };
            Ledgers.Current = Original;
            // 생성자와 collection 선택만 이용하며 Mongo 네트워크·InsertOne은 호출하지 않습니다.
            Store = new(Ledgers, new MongoClient("mongodb://127.0.0.1:1"),
                Options.Create(new MongoDbOptions { Database = "not-used" }), protection);
        }
    }

    private sealed class Actor : ICurrentUserAccessor { public string? UserId => "operator"; public string? Role => "community"; }
    private sealed class TestExecutionPolicy : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Bridge : I공간공동운영전문업무Bridge
    {
        public Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId,
            IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영원천상태Dto>>([]);
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId,
            IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영연결후보Dto>>([]);
    }
    private sealed class LedgerStub : I커뮤니티원장저장소
    {
        public 커뮤니티원장Dto? Current { get; set; }
        public 커뮤니티원장저장요청? LastSave { get; private set; }
        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(Current);
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(Current is null ? [] : [Current]);
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
        {
            if (Current is null || Current.Revision != request.기대Revision) throw new InvalidOperationException("CAS conflict");
            LastSave = request;
            Current = new() { 원장Id = request.원장Id!, Revision = Current.Revision + 1, 원장템플릿Key = request.원장템플릿Key,
                생성자UserId = request.생성자UserId, 블록목록 = request.블록목록, 외부참조 = request.외부참조 };
            return Task.FromResult(Current);
        }
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
