using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkforceAvailabilityTests
{
    [Fact] public async Task 로그아웃후늦은조회가_이전인력목록을되살리지않는다()
    {
        var service = new Service(); var delayed = new TaskCompletionSource<근무가능목록응답>();
        service.Read = () => delayed.Task;
        var vm = new 근무가능요청ViewModel(service);
        var load = vm.LoadAsync(new() { WarehouseId = 101 });
        vm.Reset(); delayed.SetResult(new() { Items = [new() { WarehouseName = "이전 계정" }] }); await load;
        Assert.Null(vm.Context); Assert.Null(vm.Requests); Assert.Null(vm.Scope); Assert.False(vm.Busy);
    }
    [Fact] public async Task 주소가바뀌면_이전응답이현재창고결과를덮어쓰지않는다()
    {
        var service = new Service(); var first = new TaskCompletionSource<근무가능문맥응답>();
        service.ContextRead = s => s.WarehouseId == 101 ? first.Task : Task.FromResult(new 근무가능문맥응답 { WarehouseId = 102 });
        var vm = new 근무가능요청ViewModel(service);
        var old = vm.LoadAsync(new() { WarehouseId = 101 }); await vm.LoadAsync(new() { WarehouseId = 102 });
        first.SetResult(new() { WarehouseId = 101 }); await old;
        Assert.Equal(102, vm.Context!.WarehouseId);
    }
    [Fact] public async Task 미확인저장은같은요청으로확인하고_중복클릭을막는다()
    {
        var service = new Service(); var pending = new TaskCompletionSource<근무가능요청응답>();
        service.Create = _ => pending.Task; var vm = new 근무가능요청ViewModel(service);
        var request = new 근무가능요청등록요청 { ClientRequestId = Guid.NewGuid() };
        var first = vm.CreateAsync(request); await vm.CreateAsync(new()); Assert.Single(service.Sent);
        pending.SetException(new HttpRequestException()); await first;
        Assert.Same(request, vm.Pending);
        service.Create = _ => Task.FromResult(new 근무가능요청응답 { RequestId = Guid.NewGuid() });
        await vm.CreateAsync(new() { ClientRequestId = Guid.NewGuid() });
        Assert.Equal(request.ClientRequestId, service.Sent[1].ClientRequestId); Assert.Null(vm.Pending); Assert.Single(vm.Requests!.Items);
    }
    [Theory][InlineData(400)][InlineData(401)][InlineData(403)][InlineData(404)][InlineData(409)]
    public async Task 명확한거부는입력재검토를허용하고_빈목록성공으로숨기지않는다(int code)
    {
        var service = new Service { Create = _ => throw new SsalddelApiException("fixture", code, "test", "", null) };
        var vm = new 근무가능요청ViewModel(service); await vm.CreateAsync(new());
        Assert.Null(vm.Pending); Assert.Null(vm.Requests); Assert.NotNull(vm.Error);
    }
    [Fact] public void 관리자경로는창고Id와기존HR권한을함께요구한다()
    {
        var type = typeof(Ssalddel.Controllers.Common.근무가능요청Controller);
        foreach (var name in new[] { "Context", "Requests", "Create", "Confirm" })
        {
            var method = type.GetMethods().Single(x => x.Name == name);
            Assert.Contains(method.GetCustomAttributes(false), x => x is Ssalddel.Security.RequireHrRoleAttribute);
            Assert.Contains(method.GetCustomAttributes(false).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(), x => x.Policy == "운영사용자전용");
            var route = method.GetCustomAttributes(false).OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single();
            Assert.Contains("{warehouseId:long}", route.Template);
        }
    }
    [Fact] public void 신규마이그레이션은세테이블과중복방지색인만생성한다()
    {
        var migration = new Ssalddel.Migrations.AddWorkforceAvailability();
        var tables = migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>().ToArray();
        Assert.Equal(3, tables.Length);
        Assert.All(tables, x => Assert.StartsWith("hr_work_", x.Name));
        var unique = migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>().Where(x => x.IsUnique).ToArray();
        Assert.Equal(3, unique.Length);
        Assert.Equal(3, migration.TargetModel.GetEntityTypes().Count(x => x.Name.Contains("근무가능") || x.Name.Contains("근무연락동의")));
        Assert.All(tables.SelectMany(x => x.ForeignKeys), x => Assert.True(x.Name.Length <= 64));
        Assert.All(migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>(), x => Assert.True(x.Name.Length <= 64));
    }
    [Fact] public async Task 최초문맥조회실패는_다시조회로복구한다()
    {
        var service = new Service { ContextRead = _ => throw new HttpRequestException() };
        var vm = new 근무가능요청ViewModel(service); await vm.LoadAsync(new() { WarehouseId = 101 });
        Assert.Null(vm.Context); Assert.NotNull(vm.Error);
        service.ContextRead = _ => Task.FromResult(new 근무가능문맥응답 { WarehouseId = 101 });
        await vm.RefreshAsync(); Assert.Equal(101, vm.Context!.WarehouseId); Assert.Null(vm.Error);
    }
    private sealed class Service : I근무가능요청Service
    {
        internal Func<Task<근무가능목록응답>> Read = () => Task.FromResult(new 근무가능목록응답());
        internal Func<근무가능업무범위, Task<근무가능문맥응답>> ContextRead = _ => Task.FromResult(new 근무가능문맥응답());
        internal Func<근무가능요청등록요청, Task<근무가능요청응답>> Create = _ => Task.FromResult(new 근무가능요청응답());
        internal List<근무가능요청등록요청> Sent = [];
        public Task<근무연락동의> 동의조회Async() => Task.FromResult(new 근무연락동의());
        public Task<근무연락동의> 동의저장Async(근무연락동의 request) => Task.FromResult(request);
        public Task<근무가능문맥응답> 문맥Async(근무가능업무범위 scope) => ContextRead(scope);
        public Task<근무가능목록응답> 목록Async(long? warehouseId, int page = 0) => Read();
        public Task<근무가능요청응답> 생성Async(근무가능요청등록요청 request) { Sent.Add(request); return Create(request); }
        public Task<근무가능요청응답> 답변Async(Guid id, 근무가능답변요청 request, long? warehouseId) => Task.FromResult(new 근무가능요청응답());
    }
}
