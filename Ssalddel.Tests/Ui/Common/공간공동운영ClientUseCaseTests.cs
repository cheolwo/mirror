using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영ClientUseCaseTests
{
    [Fact]
    public async Task 실제Client와UseCase에서_명시동의_운영담당_복수역할_공개철회_연결해제가_이어진다()
    {
        var actor = new Actor(); var store = new Store(); var regions = new Official생활교류공개지역Source();
        var service = new 공간공동운영UseCase(store, actor, new SimulatedExecution(), regions, TimeProvider.System, new Bridge());
        var api = new JsonBoundary(service, regions); var client = new 공간공동운영Client(api);
        using var vm = new 공간공동운영PageViewModel(client, actor, new(), new Scope());
        await vm.LoadAsync();
        vm.Form.Terms = new() { Title = "동네 거점", Purpose = "상온 상품을 함께 준비합니다", PublicNeighborhoodRegionKey = "region:kr:hjd:1126056500", RequiredRoleCodes = [공간공동운영역할Codes.SpaceProvider] };
        vm.Form.RoleCode = 공간공동운영역할Codes.SpaceProvider; vm.Form.ConfirmNonBinding = true; vm.Form.PublishProposal = true;
        var id = await vm.CreateAsync(); Assert.NotNull(id); Assert.False(vm.HasPending);
        await vm.LoadAsync(id);
        vm.ParticipationConfirmed = true; Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.AcceptRole));
        vm.TermsConfirmed = true; Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms));
        vm.OperatorConfirmed = true; Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.AcceptOperator));
        Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.CreateCycle));
        Assert.Single(vm.Detail!.Cycles); Assert.True(vm.Detail.IsOperator);
        Assert.True(api.Commands.Single(value => value.Action == 공간공동운영명령Codes.AgreeTerms).Consented);
        Assert.True(api.Commands.Single(value => value.Action == 공간공동운영명령Codes.AcceptOperator).Consented);

        vm.SelectedRoleCode = 공간공동운영역할Codes.InboundInspector; vm.ParticipationConfirmed = true;
        Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.Join));
        vm.ParticipationConfirmed = true; Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.AcceptRole));
        vm.TermsConfirmed = true; Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.AgreeTerms));
        Assert.Equal(2, vm.Detail!.Participants.Count(value => value.IsMe));
        Assert.All(api.Commands.Where(value => value.Action is 공간공동운영명령Codes.Join or 공간공동운영명령Codes.AcceptRole or 공간공동운영명령Codes.AgreeTerms), value => Assert.False(string.IsNullOrWhiteSpace(value.RoleCode)));

        vm.PublicDisplayName = "이웃"; await vm.ExecuteAsync(공간공동운영명령Codes.IntroductionConsent, consented: true);
        Assert.Single((await client.PublicAsync(null, 1, default)).Items.Single().Introductions);
        await vm.ExecuteAsync(공간공동운영명령Codes.IntroductionConsent, consented: false);
        Assert.Empty((await client.PublicAsync(null, 1, default)).Items.Single().Introductions);

        vm.SelectedReferenceIndex = 0; await vm.ExecuteAsync(공간공동운영명령Codes.LinkReference);
        var reference = Assert.Single(vm.CurrentCycle!.References);
        Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.UnlinkReference, reference: reference));
        Assert.Empty(vm.CurrentCycle!.References);
        Assert.Equal(id, await vm.ExecuteAsync(공간공동운영명령Codes.ProposalVisibility, consented: false));
        Assert.Empty((await client.PublicAsync(null, 1, default)).Items); Assert.Single(store.Rows);
        Assert.True(api.DetailReads >= api.Commands.Count);
    }

    private sealed class Actor : ICurrentUserAccessor, ISsalddel현재사용자Context
    { public string? UserId => "one"; public string? Role => "community"; public 현재사용자Snapshot 현재사용자 => new("one", "이웃", []); }
    private sealed class Scope : IWorkflowRecoveryContext
    { public WorkflowRecoverySnapshot Capture() => new("one", 1, new('a', 32)); public bool IsCurrent(WorkflowRecoverySnapshot value) => value == Capture(); }
    private sealed class SimulatedExecution : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Bridge : I공간공동운영전문업무Bridge
    {
        public Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId, IReadOnlyList<공간공동운영전문참조Dto> refs, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<공간공동운영원천상태Dto>>(refs.Select(value => new 공간공동운영원천상태Dto { KindCode = value.KindCode, ReferenceId = value.ReferenceId, Verified = true, VerifiedRevision = 1, Label = "거점 공간" }).ToArray());
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId, IReadOnlyList<공간공동운영전문참조Dto> refs, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<공간공동운영연결후보Dto>>([new() { Label = "내 거점 공간", Reference = new() { KindCode = "DistributedSite", ReferenceId = "site", ExpectedRevision = 1 } }]);
    }
    private sealed class Store : I공간공동운영Store
    {
        public readonly Dictionary<string, 공간공동운영Record> Rows = [];
        private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        public Task<공간공동운영Record?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(Rows.TryGetValue(id, out var value) ? Copy(value) : null);
        public Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<공간공동운영Record>>(Rows.Values.Skip(skip).Take(take).Select(Copy).ToArray());
        public Task<bool> 생성Async(공간공동운영Record value, CancellationToken ct = default) => Task.FromResult(Rows.TryAdd(value.LedgerId, Copy(value)));
        public Task<bool> 교체Async(공간공동운영Record value, long expectedRevision, string actorUserId, CancellationToken ct = default)
        { if (!Rows.TryGetValue(value.LedgerId, out var prior) || prior.Revision != expectedRevision) return Task.FromResult(false); Rows[value.LedgerId] = Copy(value); return Task.FromResult(true); }
    }
    // 실제 client의 URI/DTO 경계를 JSON 왕복하고 실제 UseCase로 보냅니다. HTTP·Mongo 실증은 별도 검증 호스트가 담당합니다.
    private sealed class JsonBoundary(공간공동운영UseCase service, I생활교류공개지역Source regions) : ISsalddelJsonApiClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        public readonly List<공간공동운영명령Request> Commands = [];
        public int DetailReads;
        private static T? Copy<T>(object? value) => value is null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json);
        public async Task<T?> GetAsync<T>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        {
            object? value;
            if (path == NeighborhoodExchangeMapRoutes.RegionsApi) value = new NeighborhoodExchangeRegionListResponse { Items = await regions.목록Async(cancellationToken) };
            else if (path.StartsWith(공간공동운영Routes.Api + "/public", StringComparison.Ordinal)) value = await service.공개Async(cancellationToken: cancellationToken);
            else if (path == 공간공동운영Routes.Api + "/mine") value = await service.내목록Async(cancellationToken);
            else
            {
                var suffix = path[(공간공동운영Routes.Api.Length + 1)..].Split('/'); var id = Uri.UnescapeDataString(suffix[0]);
                if (suffix.Length == 4 && suffix[1] == "cycles" && suffix[3] == "candidates") value = await service.연결후보Async(id, suffix[2], cancellationToken);
                else { DetailReads++; value = await service.상세Async(id, cancellationToken); }
            }
            return Copy<T>(value);
        }
        public async Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        {
            Result<공간공동운영상세Response> result;
            if (path == 공간공동운영Routes.Api) result = await service.생성Async(Copy<공간공동운영생성Request>(request)!, cancellationToken);
            else { var id = path[(공간공동운영Routes.Api.Length + 1)..].Split('/')[0]; var command = Copy<공간공동운영명령Request>(request)!; Commands.Add(command); result = await service.변경Async(id, command, cancellationToken); }
            if (result.IsFailed) throw new SsalddelApiException("조건 확인 필요", result.Errors[0].Metadata.GetValueOrDefault("StatusCode") as int? ?? 400, operationName, "", null);
            return Copy<TResponse>(result.Value);
        }
        public Task<T?> SendAsync<T>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendAsync<T>(HttpMethod method, string path, T request, string operationName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
