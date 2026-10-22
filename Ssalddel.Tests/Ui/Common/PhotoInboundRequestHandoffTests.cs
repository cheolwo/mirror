using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class PhotoInboundRequestHandoffTests
{
    [Fact]
    public void 첫진입은사진이며_보완진입은기존지도문맥과사진번호를보존한다()
    {
        Assert.False(InboundRequestNavigationContext.Parse(InboundRequestPageRoutes.Create).DetailedEntry);
        var id = Guid.NewGuid();
        var source = new InboundRequestNavigationContext
        {
            DetailedEntry = true, PhotoRequestId = id, Source = "community-map", SourceMarkerId = "marker-1",
            From = "/community/map?layer=spaces", WarehouseId = 5, WarehouseName = "검증 창고"
        };
        var restored = InboundRequestNavigationContext.Parse(source.PathFor(InboundRequestScreenKind.Create));
        Assert.True(restored.DetailedEntry);
        Assert.Equal(id, restored.PhotoRequestId);
        Assert.Equal(source.Source, restored.Source);
        Assert.Equal(source.SourceMarkerId, restored.SourceMarkerId);
        Assert.Equal(source.From, restored.From);
        Assert.Equal(5, restored.WarehouseId);
        Assert.DoesNotContain("photoRequestId", source.PathFor(InboundRequestScreenKind.Detail, 9));
    }

    [Fact]
    public async Task 사진준비번호를정식신청원요청으로사용하고_자동제출하거나중복생성하지않는다()
    {
        var service = new Warehouse();
        using var vm = await Ready(service, new(), new());
        var photoId = Guid.NewGuid();
        Assert.True(vm.BindPhotoRequest(photoId));
        Assert.Equal(0, service.CreateCount);
        Assert.Equal("검증 공급처", vm.CreateDraft.SupplierName);

        Assert.Equal(77, await vm.CreateInboundAsync());
        Assert.Equal(photoId, service.Payload!.ClientRequestId);
        Assert.Equal(photoId, service.Payload.가입고사진신청Id);
        Assert.Equal(77, await vm.CreateInboundAsync());
        Assert.Equal(1, service.CreateCount);
    }

    [Fact]
    public async Task 정식전환응답유실뒤새화면도같은번호를조회하고_입고한건을보존한다()
    {
        var service = new Warehouse { LoseResponse = true };
        var store = new VolatileWorkflowRecoveryStore();
        var context = new Context();
        var id = Guid.NewGuid();
        using (var first = await Ready(service, store, context))
        {
            Assert.True(first.BindPhotoRequest(id));
            Assert.Null(await first.CreateInboundAsync());
        }
        using var reopened = await Ready(service, store, context);
        Assert.True(reopened.BindPhotoRequest(id));
        Assert.Equal(77, await reopened.CreateInboundAsync());
        Assert.Equal(id, service.LastLookup);
        Assert.Equal(1, service.CreateCount);
        Assert.False(reopened.State.HasPendingMutation);
    }

    [Fact]
    public async Task 미확정인입고를다른사진번호로바꿀수없다()
    {
        var service = new Warehouse { LoseResponse = true, LookupUnavailable = true };
        var store = new VolatileWorkflowRecoveryStore();
        var context = new Context();
        var id = Guid.NewGuid();
        using var vm = await Ready(service, store, context);
        Assert.True(vm.BindPhotoRequest(id));
        Assert.Null(await vm.CreateInboundAsync());

        Assert.False(vm.BindPhotoRequest(Guid.NewGuid()));
        Assert.Equal(id, vm.CreateDraft.PhotoRequestId);
        Assert.Null(await vm.CreateInboundAsync());
        Assert.Equal(1, service.CreateCount);
        Assert.True(vm.State.HasPendingMutation);
    }

    [Fact]
    public async Task 사진신청의빈번호는기존작성내용을덮지않는다()
    {
        using var vm = await Ready(new(), new(), new());
        var id = Guid.NewGuid();
        Assert.True(vm.BindPhotoRequest(id));
        Assert.False(vm.BindPhotoRequest(Guid.Empty));
        Assert.Equal(id, vm.CreateDraft.PhotoRequestId);
    }

    [Fact]
    public async Task 서버가사진연결을확인하지않은응답은성공으로표시하지않는다()
    {
        var service = new Warehouse { MissingPhotoLink = true, LookupUnavailable = true };
        using var vm = await Ready(service, new(), new());
        Assert.True(vm.BindPhotoRequest(Guid.NewGuid()));
        Assert.Null(await vm.CreateInboundAsync());
        Assert.False(vm.State.Created);
        Assert.True(vm.State.HasPendingMutation);
        Assert.Null(await vm.CreateInboundAsync());
        Assert.Equal(1, service.CreateCount);
    }

    private static async Task<InboundRequestPageViewModel> Ready(Warehouse service, VolatileWorkflowRecoveryStore store, Context context)
    {
        var vm = new InboundRequestPageViewModel(service, store, context);
        await vm.LoadCreateAsync(new() { WarehouseId = 5, SupplierName = "검증 공급처" });
        return vm;
    }

    private sealed class Context : IWorkflowRecoveryContext
    {
        private readonly WorkflowRecoverySnapshot snapshot = new("owner", 1, new string('a', 32));
        public WorkflowRecoverySnapshot Capture() => snapshot;
        public bool IsCurrent(WorkflowRecoverySnapshot value) => value == snapshot;
    }
    private sealed class Warehouse : IWarehouseWorkspaceService
    {
        public bool LoseResponse { get; init; }
        public bool LookupUnavailable { get; init; }
        public bool MissingPhotoLink { get; init; }
        public int CreateCount { get; private set; }
        public 입고요청저장요청? Payload { get; private set; }
        public Guid? LastLookup { get; private set; }
        private 입고요청항목응답? saved;
        public Task<창고목록응답?> GetWarehousesAsync(CancellationToken ct = default)
            => Task.FromResult<창고목록응답?>(new() { Items = [new() { Id = 5, 창고명 = "검증 창고", IsActive = true }] });
        public Task<창고요약응답?> CreateWarehouseAsync(창고저장요청 payload, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<입고요청목록응답?> GetInboundsAsync(CancellationToken ct = default) => Task.FromResult<입고요청목록응답?>(new());
        public Task<입고요청항목응답?> GetInboundAsync(long id, CancellationToken ct = default) => Task.FromResult(saved?.Id == id ? saved : null);
        public Task<입고요청항목응답?> GetInboundByClientRequestAsync(Guid id, CancellationToken ct = default)
        { LastLookup = id; return Task.FromResult(!LookupUnavailable && saved?.ClientRequestId == id ? saved : null); }
        public Task<입고요청항목응답?> CreateInboundAsync(입고요청저장요청 payload, CancellationToken ct = default)
        {
            Payload = payload; CreateCount++;
            saved = new() { Id = 77, ClientRequestId = payload.ClientRequestId, 가입고사진신청Id = MissingPhotoLink ? null : payload.가입고사진신청Id, 상태 = 입고상태코드.예정 };
            return LoseResponse ? Task.FromException<입고요청항목응답?>(new IOException("response lost")) : Task.FromResult<입고요청항목응답?>(saved);
        }
        public Task<입고상품목록응답?> CompleteInboundAsync(long id, 입고완료요청 payload, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<재고목록응답?> GetInventoryAsync(CancellationToken ct = default) => Task.FromResult<재고목록응답?>(new());
    }
}
