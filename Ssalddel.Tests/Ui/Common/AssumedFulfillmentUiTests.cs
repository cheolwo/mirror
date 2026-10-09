using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>서버 후보 목록과 입력·동의·재시도 수명만 검증합니다. 실제 휴대폰 또는 업체 접수 증거가 아닙니다.</summary>
public sealed class AssumedFulfillmentUiTests
{
    [Fact]
    public async Task 서버가반환하지않은후보는기본목록이나가정목록에서만들어노출하지않는다()
    {
        var client = new Client { Providers = [] }; using var vm = await Ready(client);
        await vm.QuoteAsync(); Assert.True(vm.AutoRoutingEnabled); Assert.Empty(vm.AutoConditions.Providers);
        Complete(vm.AutoConditions, AssumedFulfillmentProviderCatalog.Baemin);
        Assert.Empty(vm.AutoConditions.AllowedProviderIds); Assert.False(vm.CanConfirmCurrentQuote);
        vm.DispatchRequested = true; Assert.Null(await vm.SubmitAsync()); Assert.Empty(client.Created);
    }

    [Fact]
    public async Task 서버후보변경은선택을정리하고정보제공과자동접수확인을다시받는다()
    {
        var model = new AutoFulfillmentConditionsViewModel();
        model.Configure(Choices(AllProviders), AutoFulfillmentNotice.Version);
        Complete(model, AssumedFulfillmentProviderCatalog.Baemin); Assert.True(model.IsComplete);
        model.Configure(Choices(AutoFulfillmentNotice.Providers), AutoFulfillmentNotice.Version);
        Assert.False(model.HasAssumedProvider); Assert.Empty(model.AllowedProviderIds);
        Assert.False(model.ProviderInformationSharingAgreed); Assert.False(model.AutomaticSubmissionAgreed);
        Assert.False(model.IsComplete);
    }

    [Theory]
    [InlineData(NeighborhoodPackageSizes.Medium)]
    [InlineData(null)]
    public async Task 크기를바꾸면견적과자동접수확인을무효화하고새견적입력으로전달한다(string? size)
    {
        var client = new Client(); using var vm = await Ready(client);
        await Confirm(vm); Assert.True(vm.HasCurrentQuote); Assert.True(vm.CanConfirmCurrentQuote);
        vm.PackageSizeCode = size;
        Assert.False(vm.HasCurrentQuote); Assert.False(vm.AutoConditions.AutomaticSubmissionAgreed);
        Assert.False(vm.AutoConditions.ProviderInformationSharingAgreed);
        Assert.False(vm.DirectPaymentAgreed);
        await vm.QuoteAsync(); Assert.Equal(size, client.Quoted.Last().PackageSizeCode);
        Assert.True(vm.HasCurrentQuote); Assert.False(vm.CanConfirmCurrentQuote);
    }

    [Fact]
    public async Task 로그아웃은포장크기주소후보동의와견적을메모리에서비운다()
    {
        var client = new Client(); var user = new User(); using var vm = await Ready(client, user);
        await Confirm(vm); user.Current = 현재사용자Snapshot.익명; vm.SynchronizeOwner();
        Assert.False(vm.IsAuthenticated); Assert.Null(vm.PackageSizeCode); Assert.Null(vm.Quote);
        Assert.Null(vm.ConsentEvidenceId); Assert.Empty(vm.AutoConditions.Providers);
        Assert.Empty(vm.AutoConditions.AllowedProviderIds); Assert.False(vm.AutoConditions.AutomaticSubmissionAgreed);
        Assert.Empty(vm.Draft.CargoName); Assert.True(string.IsNullOrEmpty(vm.Draft.Pickup.주소.도로명주소));
        Assert.False(vm.NeedsSubmissionReview);
    }

    [Fact]
    public async Task 로그아웃후늦게온견적은가정후보나비공개입력을복원하지않는다()
    {
        var client = new Client(); var user = new User(); using var vm = await Ready(client, user);
        client.PendingQuote = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = vm.QuoteAsync(); user.Current = 현재사용자Snapshot.익명; vm.SynchronizeOwner();
        client.PendingQuote.SetResult(client.Response()); await pending;
        Assert.Null(vm.Quote); Assert.Null(vm.PackageSizeCode); Assert.Empty(vm.AutoConditions.Providers);
    }

    [Fact]
    public async Task 접수응답유실후크기와조건을바꾸려해도원요청과원동의번호로만재확인한다()
    {
        var client = new Client { LoseCreate = true }; using var vm = await Ready(client);
        await Confirm(vm); vm.DispatchRequested = true;
        Assert.Null(await vm.SubmitAsync()); Assert.True(vm.NeedsSubmissionReview);
        vm.PackageSizeCode = NeighborhoodPackageSizes.Large;
        Assert.Equal(NeighborhoodPackageSizes.Small, vm.PackageSizeCode);
        // UI 외부에서 객체를 바꿔도 recovery payload는 최초 접수 복사본을 보존합니다.
        vm.Draft.PackageSizeCode = NeighborhoodPackageSizes.Large;
        vm.AutoConditions.MaximumUserChargeKrw = 99;
        vm.AutoConditions.SetAllowed(AssumedFulfillmentProviderCatalog.CoupangEats, true);
        client.LoseCreate = false; Assert.Equal("simulation-request", await vm.SubmitAsync());
        Assert.Equal(2, client.Created.Count);
        Assert.Equal(JsonSerializer.Serialize(client.Created[0]), JsonSerializer.Serialize(client.Created[1]));
        Assert.Equal(NeighborhoodPackageSizes.Small, client.Created[1].PackageSizeCode);
        Assert.Equal(new[] { AssumedFulfillmentProviderCatalog.Baemin }, client.Created[1].AutoFulfillment!.AllowedProviderIds);
    }

    [Fact]
    public async Task 기능꺼짐응답은이전가정목록을비우고가정접수조건을재사용하지않는다()
    {
        var client = new Client(); using var vm = await Ready(client); await Confirm(vm);
        Assert.True(vm.AutoConditions.HasAssumedProvider);
        client.RoutingEnabled = false; await vm.QuoteAsync();
        Assert.False(vm.AutoRoutingEnabled); Assert.Empty(vm.AutoConditions.Providers);
        Assert.Empty(vm.AutoConditions.AllowedProviderIds); Assert.False(vm.AutoConditions.AutomaticSubmissionAgreed);
    }

    private static IReadOnlyList<AutoFulfillmentProviderDescriptor> AllProviders =>
        AutoFulfillmentNotice.Providers.Concat(AssumedFulfillmentProviderCatalog.Providers).ToArray();
    private static IReadOnlyList<AutoFulfillmentProviderChoice> Choices(IReadOnlyList<AutoFulfillmentProviderDescriptor> providers)
        => providers.Select(x => new AutoFulfillmentProviderChoice(x.ProviderId, x.ProviderName, x.IsSimulation)).ToArray();
    private static void Complete(AutoFulfillmentConditionsViewModel model, string provider)
    {
        model.MaximumUserChargeKrw = 6000; model.DesiredCompletionLocal = DateTime.Now.AddHours(4);
        model.SetAllowed(provider, true); model.ProviderInformationSharingAgreed = true; model.AutomaticSubmissionAgreed = true;
    }
    private static async Task Confirm(NeighborhoodDeliveryAuthoringViewModel vm)
    {
        await vm.QuoteAsync(); Complete(vm.AutoConditions, AssumedFulfillmentProviderCatalog.Baemin);
        await vm.QuoteAsync();
    }
    private static async Task<NeighborhoodDeliveryAuthoringViewModel> Ready(Client client, User? user = null)
    {
        var vm = new NeighborhoodDeliveryAuthoringViewModel(client, user ?? new User()); vm.Initialize(null);
        vm.CollectionConsent = true; vm.AgeConfirmed = true; await vm.AcceptConsentAsync();
        vm.Draft.CargoName = "검토용 생활 물품"; vm.Draft.WeightKg = 1; vm.PackageSizeCode = NeighborhoodPackageSizes.Small;
        foreach (var place in new[] { vm.Draft.Pickup, vm.Draft.Dropoff })
        { place.주소.도로명주소 = "합성 장소"; place.연락처.이름 = "검토 이용자"; place.연락처.전화번호 = "00000000000"; }
        return vm;
    }
    private sealed class User : ISsalddel현재사용자Context
    {
        public 현재사용자Snapshot Current { get; set; } = new("simulation-owner", "검토 이용자", []);
        public 현재사용자Snapshot 현재사용자 => Current;
    }
    private sealed class Client : INeighborhoodDeliveryClient
    {
        public IReadOnlyList<AutoFulfillmentProviderDescriptor> Providers { get; set; } = AllProviders;
        public bool RoutingEnabled = true, LoseCreate;
        public TaskCompletionSource<NeighborhoodDeliveryQuoteResponse?>? PendingQuote;
        public List<NeighborhoodDeliveryRequest> Quoted { get; } = [];
        public List<NeighborhoodDeliveryRequest> Created { get; } = [];
        public NeighborhoodDeliveryQuoteResponse Response() => new()
        { FulfillmentRoutingEnabled = RoutingEnabled, AutomaticProviders = Providers, Fare = new() { 최종운임 = 4000 } };
        public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct)
            => Task.FromResult<신청개인정보동의증적Response?>(new() { 증적Id = request.증적Id, 상태Code = 신청개인정보동의상태Codes.유효 });
        public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        { Quoted.Add(Clone(request)); return PendingQuote?.Task ?? Task.FromResult<NeighborhoodDeliveryQuoteResponse?>(Response()); }
        public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        {
            Created.Add(Clone(request)); return LoseCreate
                ? Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("simulation response lost"))
                : Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "simulation-request" });
        }
        public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
        private static NeighborhoodDeliveryRequest Clone(NeighborhoodDeliveryRequest value)
            => JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(value))!;
    }
}
