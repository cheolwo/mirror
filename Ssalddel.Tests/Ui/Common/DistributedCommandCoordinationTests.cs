using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DistributedCommandCoordinationTests
{
    private const string Scope = "distributed.cargo-driver.round.v1";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 기존_SavedWorkflow_JSON은_구체입력과_원번호를_복원하고_사람의재시도전에_POST하지않는다(bool sent)
    {
        var id = Guid.NewGuid();
        var body = new DistributedRoundAcceptRequest { RequestId = id, ExpectedRevision = 1, AcknowledgedWarningCodes = ["window", "loading"] };
        var bodyJson = JsonSerializer.Serialize(body);
        var oldJson = JsonSerializer.Serialize(new
        {
            SelectedId = "wave-a",
            Command = new { Action = "accept", Suffix = "waves/wave-a/accept", BodyType = nameof(DistributedRoundAcceptRequest), BodyJson = bodyJson,
                AggregateId = "wave-a", Quantity = (int?)null, ReservationId = (string?)null, Sent = sent, Acknowledgement = (DistributedCommandResponse?)null },
            Photo = (object?)null, PhotoOperation = (object?)null
        });
        var store = new VolatileWorkflowRecoveryStore();
        await store.SaveAsync(new("actor", Scope, id, oldJson, DateTime.UtcNow, "wave-a"), default);
        var transport = DistributedUiTransport.Round();
        using var model = Model(transport, store);

        await model.LoadAsync("another-wave");

        Assert.Equal(id, model.RequestId); Assert.Equal("wave-a", model.SelectedId);
        Assert.True(model.HasPendingCommand); Assert.Empty(transport.Posts);
        Assert.Contains("/receipts/", transport.Gets[0]);
        await model.RetryPendingAsync();
        var posted = Assert.IsType<DistributedRoundAcceptRequest>(Assert.Single(transport.Posts).Body);
        Assert.Equal(bodyJson, JsonSerializer.Serialize(posted));
        Assert.False(model.HasPendingCommand); Assert.Null(await store.LoadAsync("actor", Scope, default));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("request")]
    [InlineData("aggregate")]
    [InlineData("revision")]
    [InlineData("status")]
    public async Task 다른_ACK는_확정하지않고_원영수증_GET으로_같은명령의결과를_복구한다(string mismatch)
    {
        var inner = DistributedUiTransport.Round();
        var transport = new AckTransport(inner, ack => mismatch switch
        {
            "null" => null, "request" => ack with { RequestId = Guid.NewGuid() },
            "aggregate" => ack with { AggregateId = "other-wave" }, "revision" => ack with { Revision = 1 },
            _ => ack with { StatusCode = " " }
        });
        var store = new VolatileWorkflowRecoveryStore();
        using var model = Model(transport, store); await model.LoadAsync("wave-a");

        await Accept(model);

        Assert.True(model.HasPendingCommand); Assert.Null(model.Message);
        var saved = await store.LoadAsync("actor", Scope, default);
        using (var json = JsonDocument.Parse(saved!.PayloadJson!))
        {
            var command = json.RootElement.GetProperty("Command");
            Assert.True(command.GetProperty("Sent").GetBoolean());
            Assert.Equal(JsonValueKind.Null, command.GetProperty("Acknowledgement").ValueKind);
        }
        await model.RetryPendingAsync();
        Assert.Single(inner.Posts); Assert.False(model.HasPendingCommand); Assert.NotNull(model.Message);
        Assert.Null(await store.LoadAsync("actor", Scope, default));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("aggregate")]
    [InlineData("revision")]
    [InlineData("quantity")]
    [InlineData("reservation")]
    public async Task 원영수증의_대상_판본_수량_예약이_다르면_보호기록을_지우거나_성공표시하지않는다(string mismatch)
    {
        var transport = DistributedUiTransport.Round();
        transport.AlterReceipt = receipt => mismatch switch
        {
            "request" => receipt with { RequestId = Guid.NewGuid(), ReservationId = "res-a" },
            "aggregate" => receipt with { AggregateId = "other-wave", ReservationId = "res-a" },
            "revision" => receipt with { Revision = 3, ReservationId = "res-a" },
            "quantity" => receipt with { Quantity = 1, ReservationId = "res-a" },
            _ => receipt with { ReservationId = "another-reservation" }
        };
        var store = new VolatileWorkflowRecoveryStore();
        using var model = Model(transport, store); await model.LoadAsync("wave-a");
        await model.ExecuteAsync("accept", "waves/wave-a/accept", new DistributedRoundAcceptRequest { ExpectedRevision = 1 },
            "wave-a", ["accept"], true, 0, "res-a");

        Assert.True(model.HasPendingCommand); Assert.Null(model.Message); Assert.NotNull(model.Error);
        Assert.NotNull(await store.LoadAsync("actor", Scope, default));
        await model.RetryPendingAsync();
        Assert.Single(transport.Posts); Assert.True(model.HasPendingCommand); Assert.Null(model.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sent_ACK_보호저장과_영수증_최신조회가_선행하고_삭제실패중에는_원명령을_유지한다(bool failRemove)
    {
        var events = new List<string>();
        var store = new ObservingStore(events) { FailRemove = failRemove };
        var transport = DistributedUiTransport.Round();
        using var model = Model(transport, store); await model.LoadAsync("wave-a");
        store.BeforeRemove = () =>
        {
            Assert.True(model.HasPendingCommand); Assert.True(model.IsLoaded);
            Assert.Equal(2, model.SelectedWave!.Revision);
        };
        transport.BeforeGet = path => events.Add(path.Contains("/receipts/") ? "receipt" : path.Contains("/workspace?") ? "workspace" : "wave");
        transport.BeforePost = _ => events.Add("post");
        events.Clear();

        await Accept(model);

        Assert.Equal(new[] { "save-sent", "post", "save-ack", "receipt", "workspace", "wave", "remove" }, events);
        Assert.Equal(failRemove, model.HasPendingCommand);
        Assert.Equal(failRemove, await store.LoadAsync("actor", Scope, default) is not null);
        Assert.Equal(failRemove, model.Message is null);
        using (var json = JsonDocument.Parse(store.LastSaved!.PayloadJson!))
        {
            Assert.Equal(new[] { "SelectedId", "Command", "Photo", "PhotoOperation" }, json.RootElement.EnumerateObject().Select(property => property.Name));
            var command = json.RootElement.GetProperty("Command");
            Assert.Equal(new[] { "Action", "Suffix", "BodyType", "BodyJson", "AggregateId", "Quantity", "ReservationId", "Sent", "Acknowledgement" }, command.EnumerateObject().Select(property => property.Name));
            Assert.Equal(nameof(DistributedRoundAcceptRequest), command.GetProperty("BodyType").GetString());
            Assert.Equal(model.RequestId ?? transport.Posts[0].Body.RequestId, store.LastSaved.RequestId);
        }
        if (failRemove)
        {
            store.FailRemove = false; await model.RetryPendingAsync();
            Assert.Single(transport.Posts); Assert.False(model.HasPendingCommand); Assert.NotNull(model.Message);
        }
    }

    [Theory]
    [InlineData("warehouse", "waves", "accept")]
    [InlineData("operator", "waves", "acknowledge")]
    [InlineData("shipper", "orders", "plan")]
    [InlineData("cargo-driver", "round", "acknowledge")]
    [InlineData("orderer", "orders", "accept")]
    public async Task 서버행동목록에있어도_다른역할의명령을_만들지않는다(string role, string screen, string action)
    {
        var transport = role == "orderer" ? DistributedUiTransport.Customer() : DistributedUiTransport.Round();
        using var model = new 분산이행PageViewModel(new(transport, role), screen); await model.LoadAsync("wave-a");
        var body = new DistributedRoundAcceptRequest { ExpectedRevision = 1 };
        Assert.True(model.Allows(action, [action]));

        await model.ExecuteAsync(action, "waves/wave-a/accept", body, "wave-a", [action], true);

        Assert.Empty(transport.Posts); Assert.Equal(Guid.Empty, body.RequestId); Assert.False(model.HasPendingCommand);
    }

    [Theory]
    [InlineData("Receive")]
    [InlineData("Pick")]
    [InlineData("Deliver")]
    [InlineData("Return")]
    [InlineData("ImportLot")]
    [InlineData("StockRelease")]
    public async Task 여섯사진업무는_현재입력만_촬영하고_오래된판본과_기존완료번호를_재사용하지않는다(string kind)
    {
        var scenario = PhotoScenario(kind);
        using var model = new 분산이행PageViewModel(new(scenario.Transport, scenario.Role), scenario.Screen, new Test업무사진촬영Adapter());
        await model.LoadAsync(scenario.SelectedId);

        await model.CapturePhotoAsync(scenario.Work, "완료 근거", scenario.Action, "sites/site-a/evidence", [scenario.Action], 1);

        Assert.Null(model.ValidationError); Assert.Single(model.Photo.Photos);
        Assert.Equal(kind, model.Photo.Work!.Kind); Assert.NotEqual(Guid.Empty, model.Photo.CompletionRequestId!.Value);
        var original = 분산사진업무Session.Body(scenario.Work)!;
        Assert.Equal(Guid.Empty, original.RequestId);
        original.ExpectedRevision = 0;
        await model.CapturePhotoAsync(scenario.Work, "완료 근거", scenario.Action, "sites/site-a/evidence", [scenario.Action], 1);
        Assert.NotNull(model.ValidationError); Assert.Null(model.Photo.Work);
        original.ExpectedRevision = 1; original.RequestId = Guid.NewGuid();
        await model.CapturePhotoAsync(scenario.Work, "완료 근거", scenario.Action, "sites/site-a/evidence", [scenario.Action], 1);
        Assert.Null(model.Photo.Work); Assert.Empty(scenario.Transport.PhotoPosts); Assert.Empty(scenario.Transport.Posts);
    }

    private static 분산이행PageViewModel Model(I분산이행Transport transport, IWorkflowRecoveryStore store)
        => new(new(transport, "cargo-driver"), "round", new Test업무사진촬영Adapter(), store);
    private static Task Accept(분산이행PageViewModel model) => model.ExecuteAsync("accept", "waves/wave-a/accept",
        new DistributedRoundAcceptRequest { ExpectedRevision = 1, AcknowledgedWarningCodes = ["window"] }, "wave-a", ["accept"], true);

    private static PhotoCase PhotoScenario(string kind)
    {
        var transport = DistributedUiTransport.Round();
        var wave = transport.Workspace.Waves[0];
        if (kind == "Receive")
        {
            transport.Workspace.InboundPlans = [new("inbound-a", "source-a", "Expected", 1,
                [new("line-a", "site-a", "loc-a", "owner-a", "sku-a", "lot-a", 2, 0)], ["receive"])];
            return new(transport, "warehouse", "inbound", "inbound-a", "receive", new() { Kind = kind, AggregateId = "inbound-a",
                Receive = new() { ExpectedRevision = 1, LocationBarcode = "barcode-a", Lines = [new() { LineKey = "line-a", Sku = "sku-a", Quantity = 1 }] } });
        }
        if (kind == "ImportLot")
        {
            transport.Workspace.ExistingStockCandidates = [new(1, "site-a", "입고 물품", "actor", "actor", "sku-a", 2, 2)];
            return new(transport, "shipper", "inventory", "site-a", "import-lot", new() { Kind = kind, AggregateId = "site-a",
                ImportLot = new() { ExpectedRevision = 1, InboundItemId = 1, ActualLocationConfirmed = true, LocationId = "loc-a", LocationBarcode = "barcode-a", LotCode = "lot-a" } });
        }
        if (kind == "StockRelease")
        {
            transport.Workspace.StockReleases = [new("release-a", "site-a", "공간", 1, "lot-a", "물품", "owner-a", "actor", "actor",
                "Agreed", 3, 1, true, true, "인계 조건", "담당자", ["handover-stock-release"], "barcode-a")];
            return new(transport, "warehouse", "stock-release", "site-a", "handover-stock-release", new() { Kind = kind, AggregateId = "site-a",
                StockRelease = new() { ExpectedRevision = 1, ReleaseId = "release-a", LocationBarcode = "barcode-a", Quantity = 1 } });
        }
        if (kind == "Pick") return new(transport, "cargo-driver", "round", "wave-a", "pick", DistributedPhotoWorkflowTests.PickWork());
        wave.Reservations = wave.Reservations.Select(line => line.ReservationId == "res-a"
            ? line with { PickedQuantity = line.Quantity, AllowedActions = [kind == "Deliver" ? "deliver" : "return"] } : line).ToArray();
        if (kind == "Deliver")
        {
            wave.CurrentStopId = "stop-c";
            return new(transport, "cargo-driver", "round", "wave-a", "deliver", new() { Kind = kind, AggregateId = "wave-a",
                Deliver = new() { ExpectedRevision = 1, StopId = "stop-c", Lines = [new() { ReservationId = "res-a", Quantity = 1 }] } });
        }
        return new(transport, "warehouse", "returns", "wave-a", "return", new() { Kind = kind, AggregateId = "wave-a",
            Return = new() { ExpectedRevision = 1, LocationBarcode = "barcode-a", Lines = [new() { ReservationId = "res-a", Quantity = 1 }] } });
    }
    private sealed record PhotoCase(DistributedUiTransport Transport, string Role, string Screen, string SelectedId, string Action, DistributedEvidenceWorkRequest Work);

    private sealed class AckTransport(DistributedUiTransport inner, Func<DistributedCommandResponse, DistributedCommandResponse?> alter) : I분산이행Transport
    {
        public event Action? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
        public RoleWorkspaceIdentity Identity(string roleKey) => inner.Identity(roleKey);
        public Task InitializeAsync(string roleKey, CancellationToken ct) => inner.InitializeAsync(roleKey, ct);
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct) => inner.GetAsync<T>(roleKey, path, ct);
        public async Task<T?> PostAsync<TRequest, T>(string roleKey, string path, TRequest body, CancellationToken ct)
        {
            var result = await inner.PostAsync<TRequest, T>(roleKey, path, body, ct);
            if (result is not DistributedCommandResponse ack) return result;
            var changed = alter(ack); return changed is null ? default : (T)(object)changed;
        }
        public void Dispose() { }
    }
    private sealed class ObservingStore(List<string> events) : IWorkflowRecoveryStore
    {
        private readonly VolatileWorkflowRecoveryStore inner = new();
        public bool FailRemove { get; set; }
        public Action? BeforeRemove { get; set; }
        public WorkflowRecoveryRecord? LastSaved { get; private set; }
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => inner.LoadAsync(owner, scope, ct);
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
        {
            LastSaved = record;
            using var json = JsonDocument.Parse(record.PayloadJson!);
            events.Add(json.RootElement.GetProperty("Command").GetProperty("Acknowledgement").ValueKind == JsonValueKind.Null ? "save-sent" : "save-ack");
            return inner.SaveAsync(record, ct);
        }
        public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct)
        {
            events.Add("remove"); BeforeRemove?.Invoke();
            return FailRemove ? Task.FromException(new IOException("protected removal unavailable")) : inner.RemoveAsync(owner, scope, expectedRequestId, ct);
        }
    }
}
