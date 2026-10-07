using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;
namespace Ssalddel.Tests.Ui.Common;

public sealed class CommerceProtectionUiTests
{
    [Fact]
    public async Task 익명은_판매자_권리_분쟁_API를_호출하지않고_공개안내만_읽는다()
    {
        var client=new CommerceUiFixture(); var user=new User();
        using var seller=new 판매자확인ViewModel(client,user); await seller.LoadAsync();
        using var support=new 보호지원ViewModel(client,user); support.Initialize(true,null,null); await support.LoadAsync();
        using var notice=new 통신판매안내ViewModel(client); await notice.LoadAsync();
        Assert.Equal(0,client.PrivateReads); Assert.NotNull(notice.Value); Assert.True(seller.RequiresLogin); Assert.True(support.RequiresLogin);
    }
    [Fact]
    public async Task 늦은_본인응답은_다른계정에_표시되지않는다()
    {
        var completion=new TaskCompletionSource<보호지원ListResponse>(); var client=new CommerceUiFixture { List=()=>completion.Task }; var user=new User { Id="first" };
        using var vm=new 보호지원ViewModel(client,user); vm.Initialize(true,null,null); var loading=vm.LoadAsync();
        user.Id="second"; vm.SynchronizeOwner(); completion.SetResult(new(){Items=[new(){CaseId="first-private"}]}); await loading;
        Assert.Empty(vm.Items); Assert.Null(vm.Detail); Assert.False(vm.IsLoading);
    }
    [Fact]
    public async Task 접수결과_미확인은_같은번호와_원입력으로_재시도한다()
    {
        var client=new CommerceUiFixture { FailRights=true }; var user=new User { Id="one" }; using var vm=new 보호지원ViewModel(client,user);
        vm.Initialize(true,null,null); vm.Summary="원 요청"; await vm.SubmitAsync(); Assert.True(vm.HasPending);
        vm.Summary="바뀐 입력"; client.FailRights=false; await vm.RetryAsync();
        Assert.Equal(2,client.Rights.Count); Assert.Equal(client.Rights[0].ClientRequestId,client.Rights[1].ClientRequestId); Assert.All(client.Rights,r=>Assert.Equal("원 요청",r.Summary)); Assert.False(vm.HasPending);
    }
    [Fact]
    public async Task 판매자등록은_확인완료를_가정하지않고_미확정등록의_원입력을_유지한다()
    {
        var client=new CommerceUiFixture { FailSeller=true }; using var vm=new 판매자확인ViewModel(client,new User { Id="one" }); await vm.LoadAsync();
        vm.Draft.DisplayName="원 판매자"; vm.Draft.CollectionConsentAccepted=true; await vm.SaveAsync(); Assert.True(vm.HasPending);
        vm.Draft.DisplayName="새 표시명"; client.FailSeller=false; await vm.SaveAsync();
        Assert.All(client.Sellers,r=>Assert.Equal("원 판매자",r.DisplayName)); Assert.Equal(client.Sellers[0].ClientRequestId,client.Sellers[1].ClientRequestId); Assert.Equal("Unverified",vm.Value!.StatusCode); Assert.False(vm.Value.IsAdult);
    }
    [Fact]
    public async Task 로그아웃은_등록입력과_접수내용을_비운다()
    {
        var user=new User { Id="one" }; var client=new CommerceUiFixture(); using var seller=new 판매자확인ViewModel(client,user); await seller.LoadAsync();
        seller.Draft.PhoneNumber="private-phone"; using var support=new 보호지원ViewModel(client,user); support.Initialize(true,null,null); support.Summary="private-description";
        user.Id=null; seller.SynchronizeOwner(); support.SynchronizeOwner(); Assert.Empty(seller.Draft.PhoneNumber); Assert.Empty(support.Summary); Assert.Empty(support.Items);
    }
    [Fact]
    public async Task 허용되지않은_운영자행동은_사용자화면에서_보내지않는다()
    {
        var client=new CommerceUiFixture(); using var vm=new 보호지원ViewModel(client,new User { Id="one" }); vm.Initialize(false,"food-order","order"); await vm.LoadAsync("case");
        vm.AdditionalSummary="처리"; await vm.CommandAsync("close"); Assert.Equal(0,client.Commands);
    }
    [Fact]
    public async Task 목록의_추가페이지_표시는_서버응답을_따른다()
    {
        var client=new CommerceUiFixture { List=()=>Task.FromResult(new 보호지원ListResponse { Page=2,HasMore=true,Items=[new(){CaseId="two"}] }) }; using var vm=new 보호지원ViewModel(client,new User { Id="one" }); vm.Initialize(true,null,null); await vm.LoadAsync(page:2);
        Assert.Equal(2,vm.Page); Assert.True(vm.HasMore); Assert.Single(vm.Items);
    }
    [Fact]
    public async Task 늦게도착한_이전매장_공개정보가_새매장을_덮지않는다()
    {
        var first=new TaskCompletionSource<판매자공개정보Response?>(); var client=new CommerceUiFixture { PublicSeller=(id)=>id==1?first.Task:Task.FromResult<판매자공개정보Response?>(new(){DisplayName="현재 매장"}) };
        using var vm=new 판매자공개정보ViewModel(client); var old=vm.LoadAsync(null,1); await vm.LoadAsync(null,2); first.SetResult(new(){DisplayName="이전 매장"}); await old; Assert.Equal("현재 매장",vm.Value!.DisplayName);
    }
    [Fact]
    public async Task 생활거래는_판매자아이디없이_현재원장의_공개정보를_조회한다()
    {
        var seen=new List<(long?,string?)>(); var client=new CommerceUiFixture { SourceSeller=(postId,workId)=>{seen.Add((postId,workId));return Task.FromResult<판매자공개정보Response?>(new(){Revision=7,StatusCode="Verified"});} };
        using var vm=new 판매자공개정보ViewModel(client); await vm.LoadAsync(null,null,12); await vm.LoadAsync(null,null,collaborationId:"work-17");
        Assert.Equal(new (long?,string?)[]{(12,null),(null,"work-17")},seen); Assert.Equal(7,vm.Value!.Revision);
    }
    [Fact]
    public async Task 이전사건의_늦은응답은_새사건상세를_덮지않는다()
    {
        var first=new TaskCompletionSource<보호지원CaseResponse?>(); var client=new CommerceUiFixture { Detail=(id)=>id=="first"?first.Task:Task.FromResult<보호지원CaseResponse?>(new(){CaseId=id}) };
        using var vm=new 보호지원ViewModel(client,new User{Id="one"}); vm.Initialize(false,"food-order","order"); var old=vm.LoadAsync("first"); await vm.LoadAsync("second"); first.SetResult(new(){CaseId="first"}); await old; Assert.Equal("second",vm.Detail!.CaseId); Assert.False(vm.IsLoading);
    }
    [Theory]
    [InlineData(false, "add-evidence")]
    [InlineData(false, "appeal")]
    [InlineData(true, "add-evidence")]
    [InlineData(true, "appeal")]
    public async Task 추가내용과_이의제기는_실제계약의_비공개원문필드를_채운다(bool privacy, string action)
    {
        var client=new CommerceUiFixture { Detail=id=>Task.FromResult<보호지원CaseResponse?>(new(){CaseId=id,Revision=4,AllowedActions=[action]}) };
        using var vm=new 보호지원ViewModel(client,new User{Id="one"}); vm.Initialize(privacy,"food-order","order"); await vm.LoadAsync("case-1");
        vm.AdditionalSummary="내가 제출한 비공개 내용"; await vm.CommandAsync(action);
        var request=Assert.Single(client.CommandRequests); Assert.Equal(action,request.Action); Assert.Equal(4,request.ExpectedRevision);
        Assert.Equal("내가 제출한 비공개 내용",request.PrivateEvidence); Assert.DoesNotContain("비공개 내용",request.Summary); Assert.False(vm.HasPending); Assert.Empty(vm.AdditionalSummary);
    }
    [Fact]
    public async Task 추가내용의_불명확한전송결과는_원사건_판본_요청번호와원문으로_재시도한다()
    {
        var client=new CommerceUiFixture { FailCommand=true,Detail=id=>Task.FromResult<보호지원CaseResponse?>(new(){CaseId=id,Revision=7,AllowedActions=["add-evidence"]}) };
        using var vm=new 보호지원ViewModel(client,new User{Id="one"}); vm.Initialize(false,"food-order","order"); await vm.LoadAsync("case-1");
        vm.AdditionalSummary="원 제출 내용"; await vm.CommandAsync("add-evidence"); Assert.True(vm.HasPending);
        vm.AdditionalSummary="다른 내용"; await vm.LoadAsync("case-1"); Assert.True(vm.HasPending); client.FailCommand=false; await vm.RetryAsync();
        Assert.Equal(2,client.CommandRequests.Count); Assert.Equal(client.CommandRequests[0].ClientRequestId,client.CommandRequests[1].ClientRequestId);
        Assert.All(client.CommandRequests,r=>{Assert.Equal(7,r.ExpectedRevision);Assert.Equal("원 제출 내용",r.PrivateEvidence);}); Assert.All(client.CommandCaseIds,id=>Assert.Equal("case-1",id)); Assert.False(vm.HasPending);
    }
    [Fact]
    public async Task 계정변경은_미확정추가내용을_비우고_다른계정으로_재전송하지않는다()
    {
        var user=new User{Id="first"}; var client=new CommerceUiFixture{FailCommand=true}; using var vm=new 보호지원ViewModel(client,user);
        vm.Initialize(false,"food-order","order"); await vm.LoadAsync("case-1"); vm.AdditionalSummary="첫 계정 내용"; await vm.CommandAsync("add-evidence"); Assert.True(vm.HasPending);
        user.Id="second"; vm.SynchronizeOwner(); await vm.RetryAsync(); Assert.False(vm.HasPending); Assert.Empty(vm.AdditionalSummary); Assert.Null(vm.Detail); Assert.Single(client.CommandRequests);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 같은거래의_다른사건으로이동하면_이전상세_비공개입력_미확정명령을_비운다(bool parametersChanged)
    {
        var client=new CommerceUiFixture{FailCommand=true}; using var vm=new 보호지원ViewModel(client,new User{Id="one"});
        vm.Initialize(false,"food-order","order","case-1"); await vm.LoadAsync("case-1");
        vm.Summary="이전 사건 입력"; vm.AdditionalSummary="이전 비공개 내용"; await vm.CommandAsync("add-evidence"); Assert.True(vm.HasPending);
        if (parametersChanged)
        {
            vm.Initialize(false,"food-order","order","case-2");
            Assert.Null(vm.Detail); Assert.False(vm.HasPending); Assert.Empty(vm.Summary); Assert.Empty(vm.AdditionalSummary);
        }
        await vm.LoadAsync("case-2"); await vm.RetryAsync();
        Assert.Equal("case-2",vm.Detail!.CaseId); Assert.False(vm.HasPending); Assert.Empty(vm.Summary); Assert.Empty(vm.AdditionalSummary);
        Assert.Single(client.CommandRequests); Assert.Null(vm.Error); Assert.Null(vm.Notice);
    }
    [Fact]
    public async Task 사건전환중_늦은명령응답은_새사건을_덮거나_접수안내를_표시하지않는다()
    {
        var completion=new TaskCompletionSource<보호지원CaseResponse?>(); var client=new CommerceUiFixture{Command=()=>completion.Task};
        using var vm=new 보호지원ViewModel(client,new User{Id="one"}); vm.Initialize(false,"food-order","order"); await vm.LoadAsync("case-1");
        vm.AdditionalSummary="이전 비공개 내용"; var sending=vm.CommandAsync("add-evidence"); Assert.True(vm.IsSending);
        await vm.LoadAsync("case-2"); completion.SetResult(new(){CaseId="case-1"}); await sending;
        Assert.Equal("case-2",vm.Detail!.CaseId); Assert.False(vm.HasPending); Assert.False(vm.IsSending); Assert.Empty(vm.AdditionalSummary); Assert.Null(vm.Notice);
    }
    [Fact]
    public async Task 계정변경중_늦은명령응답은_개인상세를_복원하지않는다()
    {
        var completion=new TaskCompletionSource<보호지원CaseResponse?>(); var client=new CommerceUiFixture{Command=()=>completion.Task}; var user=new User{Id="first"};
        using var vm=new 보호지원ViewModel(client,user); vm.Initialize(true,null,null); await vm.LoadAsync("case-1");
        vm.AdditionalSummary="첫 계정 내용"; var sending=vm.CommandAsync("add-evidence"); user.Id="second"; vm.SynchronizeOwner();
        completion.SetResult(new(){CaseId="first-private"}); await sending; await vm.RetryAsync();
        Assert.Null(vm.Detail); Assert.Empty(vm.Items); Assert.Empty(vm.AdditionalSummary); Assert.False(vm.HasPending); Assert.False(vm.IsSending); Assert.Null(vm.Notice); Assert.Single(client.CommandRequests);
    }
    [Theory]
    [InlineData(401, false)]
    [InlineData(401, true)]
    [InlineData(403, false)]
    [InlineData(403, true)]
    public async Task 목록또는상세조회_인증권한실패는_기존상세_원문_미확정명령을_즉시폐기한다(int status, bool failList)
    {
        var client = new CommerceUiFixture
        {
            FailCommand = true,
            List = () => Task.FromResult(new 보호지원ListResponse { Items = [new() { CaseId = "case-1" }] })
        };
        using var vm = new 보호지원ViewModel(client, new User { Id = "one" });
        vm.Initialize(false, "food-order", "order", "case-1"); await vm.LoadAsync("case-1");
        vm.Summary = "이전 입력"; vm.AdditionalSummary = "비공개 원문"; await vm.CommandAsync("add-evidence");
        Assert.True(vm.HasPending); Assert.NotEmpty(vm.Items);
        if (failList) client.List = () => Task.FromException<보호지원ListResponse>(AccessFailure(status));
        else client.Detail = _ => Task.FromException<보호지원CaseResponse?>(AccessFailure(status));
        await vm.LoadAsync("case-1");
        AssertDiscarded(vm); Assert.Equal(status == 401, vm.RequiresLogin); Assert.NotNull(vm.Error);
        Assert.DoesNotContain("private response", vm.Error); await vm.RetryAsync(); Assert.Single(client.CommandRequests);
    }
    [Theory]
    [InlineData(401, "rights")]
    [InlineData(403, "rights")]
    [InlineData(401, "dispute")]
    [InlineData(403, "dispute")]
    [InlineData(401, "command")]
    [InlineData(403, "command")]
    public async Task 신규접수와추가내용_인증권한실패는_원문과_재시도정본을_즉시폐기한다(int status, string operation)
    {
        var client = new CommerceUiFixture { RightsError = AccessFailure(status), DisputeError = AccessFailure(status),
            Command = () => Task.FromException<보호지원CaseResponse?>(AccessFailure(status)) };
        using var vm = new 보호지원ViewModel(client, new User { Id = "one" });
        vm.Initialize(operation == "rights", "food-order", "order");
        if (operation == "command") await vm.LoadAsync("case-1");
        vm.Summary = "새 접수 원문"; vm.AdditionalSummary = "추가 비공개 원문";
        if (operation == "command") await vm.CommandAsync("add-evidence"); else await vm.SubmitAsync();
        AssertDiscarded(vm); Assert.Equal(status == 401, vm.RequiresLogin); Assert.NotNull(vm.Error);
        var calls = (client.Rights.Count, client.DisputeCalls, client.Commands);
        await vm.RetryAsync(); Assert.Equal(calls, (client.Rights.Count, client.DisputeCalls, client.Commands));
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 접수의_인증권한실패후_이전목록응답은_폐기한개인상태를_복원하지않는다(int status)
    {
        var delayed = new TaskCompletionSource<보호지원ListResponse>();
        var client = new CommerceUiFixture { List = () => delayed.Task, RightsError = AccessFailure(status) };
        using var vm = new 보호지원ViewModel(client, new User { Id = "one" }); vm.Initialize(true, null, null);
        var loading = vm.LoadAsync(); vm.Summary = "접수 원문"; await vm.SubmitAsync(); AssertDiscarded(vm);
        delayed.SetResult(new() { Items = [new() { CaseId = "old-private" }] }); await loading;
        AssertDiscarded(vm); Assert.Equal(status == 401, vm.RequiresLogin); Assert.NotNull(vm.Error);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 이전사건의_지연된인증권한실패는_새사건상태를_폐기하지않는다(int status)
    {
        var delayed = new TaskCompletionSource<보호지원CaseResponse?>();
        var client = new CommerceUiFixture { Detail = id => id == "old" ? delayed.Task
            : Task.FromResult<보호지원CaseResponse?>(new() { CaseId = id, Revision = 9, AllowedActions = ["add-evidence"] }) };
        using var vm = new 보호지원ViewModel(client, new User { Id = "one" }); vm.Initialize(false, "food-order", "order");
        var loading = vm.LoadAsync("old"); await vm.LoadAsync("current"); vm.AdditionalSummary = "현재 사건 입력";
        delayed.SetException(AccessFailure(status)); await loading;
        Assert.Equal("current", vm.Detail!.CaseId); Assert.Equal("현재 사건 입력", vm.AdditionalSummary);
        Assert.False(vm.RequiresLogin); Assert.False(vm.IsLoading); Assert.Null(vm.Error);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 화면이탈은_비공개입력과_미확정명령을_폐기하고_늦은응답을_무시한다(bool delayed)
    {
        var completion = new TaskCompletionSource<보호지원CaseResponse?>();
        var client = new CommerceUiFixture { FailCommand = !delayed, Command = delayed ? () => completion.Task : null };
        using var vm = new 보호지원ViewModel(client, new User { Id = "one" }); vm.Initialize(false, "food-order", "order");
        await vm.LoadAsync("case-1"); vm.Summary = "접수 원문"; vm.AdditionalSummary = "추가 원문";
        var sending = vm.CommandAsync("add-evidence");
        if (!delayed) { await sending; Assert.True(vm.HasPending); }
        vm.Dispose(); AssertDiscarded(vm);
        if (delayed) { completion.SetResult(new() { CaseId = "old-private" }); await sending; }
        await vm.RetryAsync(); AssertDiscarded(vm); Assert.Single(client.CommandRequests); Assert.Null(vm.Notice);
    }
    private static SsalddelApiException AccessFailure(int status) => new("private response", status, "test", "private response", null);
    private static void AssertDiscarded(보호지원ViewModel vm)
    {
        Assert.Empty(vm.Items); Assert.Null(vm.Detail); Assert.Empty(vm.Summary); Assert.Empty(vm.AdditionalSummary);
        Assert.False(vm.Loaded); Assert.False(vm.HasPending); Assert.False(vm.IsSending); Assert.False(vm.IsLoading); Assert.Null(vm.Notice);
    }
    private sealed class User : ISsalddel현재사용자Context { public string? Id {get;set;} public 현재사용자Snapshot 현재사용자=>Id is null?현재사용자Snapshot.익명:new(Id,Id,[]); }
}

internal sealed class CommerceUiFixture : I통신판매보호Client
{
    public int PrivateReads,Commands,DisputeCalls; public bool FailRights,FailSeller,FailCommand;
    public Exception? RightsError {get;set;} public Exception? DisputeError {get;set;}
    public List<보호지원CommandRequest> CommandRequests {get;}=[];
    public List<string> CommandCaseIds {get;}=[];
    public List<개인정보권리접수Request> Rights {get;}=[]; public List<판매자등록Request> Sellers {get;}=[];
    public Func<Task<보호지원ListResponse>>? List {get;set;}
    public Func<long?,Task<판매자공개정보Response?>>? PublicSeller {get;set;}
    public Func<long?,string?,Task<판매자공개정보Response?>>? SourceSeller {get;set;}
    public Func<string,Task<보호지원CaseResponse?>>? Detail {get;set;}
    public Func<Task<보호지원CaseResponse?>>? Command {get;set;}
    public Task<통신판매안내Response?> 안내Async(CancellationToken ct)=>Task.FromResult<통신판매안내Response?>(new(){Version="test-policy",IntermediaryNotice="중개 안내",Documents=[]});
    public Task<판매자확인Response?> 판매자Async(CancellationToken ct){PrivateReads++;return Task.FromResult<판매자확인Response?>(new(){Revision=1});}
    public Task<판매자확인Response?> 등록Async(판매자등록Request r,CancellationToken ct){Sellers.Add(r);return FailSeller?Task.FromException<판매자확인Response?>(new InvalidOperationException("private error")):Task.FromResult<판매자확인Response?>(new(){StatusCode="Unverified",Revision=2});}
    public Task<판매자확인Response?> 확인Async(판매자확인Request r,CancellationToken ct)=>판매자Async(ct);
    public Task<판매자공개정보Response?> 공개판매자Async(string? sellerId,long? restaurantId,CancellationToken ct)=>PublicSeller?.Invoke(restaurantId)??Task.FromResult<판매자공개정보Response?>(null);
    public Task<판매자공개정보Response?> 공개거래판매자Async(long? postId,string? collaborationId,CancellationToken ct)=>SourceSeller?.Invoke(postId,collaborationId)??Task.FromResult<판매자공개정보Response?>(null);
    public Task<보호지원ListResponse> 목록Async(bool privacy,int page,CancellationToken ct){PrivateReads++;return List?.Invoke()??Task.FromResult(new 보호지원ListResponse {Page=page});}
    public Task<보호지원CaseResponse?> 상세Async(bool privacy,string id,CancellationToken ct)=>Detail?.Invoke(id)??Task.FromResult<보호지원CaseResponse?>(new(){CaseId=id,Revision=1,AllowedActions=["add-evidence"]});
    public Task<보호지원CaseResponse?> 분쟁Async(거래분쟁접수Request r,CancellationToken ct){DisputeCalls++;return DisputeError is not null?Task.FromException<보호지원CaseResponse?>(DisputeError):Task.FromResult<보호지원CaseResponse?>(new(){CaseId="new-case"});}
    public Task<보호지원CaseResponse?> 권리Async(개인정보권리접수Request r,CancellationToken ct){Rights.Add(r);return RightsError is not null?Task.FromException<보호지원CaseResponse?>(RightsError):FailRights?Task.FromException<보호지원CaseResponse?>(new InvalidOperationException("private error")):Task.FromResult<보호지원CaseResponse?>(new(){CaseId="new-case"});}
    public Task<보호지원CaseResponse?> 변경Async(bool privacy,string id,보호지원CommandRequest r,CancellationToken ct)
    {
        Commands++; CommandRequests.Add(r); CommandCaseIds.Add(id);
        if (r.Action is "add-evidence" or "appeal" && string.IsNullOrWhiteSpace(r.PrivateEvidence)) return Task.FromException<보호지원CaseResponse?>(new InvalidOperationException("EvidenceRequired"));
        return Command?.Invoke()??(FailCommand?Task.FromException<보호지원CaseResponse?>(new InvalidOperationException("전송 결과 미확인")):상세Async(privacy,id,ct));
    }
}
internal static class CommerceUiFixtureServices
{
    public static IServiceCollection AddCommerceUiFixture(this IServiceCollection services){services.AddSingleton<I통신판매보호Client,CommerceUiFixture>();return services.AddCommerceProtectionUi();}
}
