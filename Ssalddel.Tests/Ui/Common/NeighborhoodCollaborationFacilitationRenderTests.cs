using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodCollaborationFacilitationRenderTests
{
    [Fact]
    public async Task 현재허용행동과철회는_약속조건보다먼저보이며_렌더만으로실행하지않는다()
    {
        var value = Work(NeighborhoodTransferMethods.RecipientPickup);
        value.AllowedActions = [NeighborhoodCollaborationActions.ProposeCompletion, NeighborhoodCollaborationActions.Cancel,
            NeighborhoodCollaborationActions.Reject, NeighborhoodCollaborationActions.WithdrawHandoverConsent,
            NeighborhoodCollaborationActions.WithdrawParticipation];
        var html = await DetailAsync(value);
        var current = html.IndexOf("data-neighborhood-current", StringComparison.Ordinal);
        var terms = html.IndexOf("data-neighborhood-terms", StringComparison.Ordinal);
        Assert.True(current >= 0 && terms > current);
        foreach (var text in new[] { "완료 확인 요청", "협업 취소", "신청 거절", "인계 정보 동의 철회", "참여 철회" })
            Assert.InRange(html.IndexOf(text, current, StringComparison.Ordinal), current, terms - 1);
        Assert.True(html.IndexOf("<h2>약속 내용</h2>", StringComparison.Ordinal) < html.IndexOf("<h2>전달 방법</h2>", StringComparison.Ordinal));
        Assert.True(html.IndexOf("<h2>전달 방법</h2>", StringComparison.Ordinal) < html.IndexOf("<h2>금액과 지급 안내</h2>", StringComparison.Ordinal));
        Assert.Contains("기존 추가 조건", html); Assert.Contains("2 개", html);
    }

    [Theory]
    [InlineData(NeighborhoodTransferMethods.RecipientPickup, false)]
    [InlineData(NeighborhoodTransferMethods.ProviderDelivery, false)]
    [InlineData(NeighborhoodTransferMethods.DriverDelivery, true)]
    [InlineData(null, false)]
    public async Task 기사배송비안내는_기사배송을선택한경우에만보인다(string? method, bool driverFee)
    {
        var html = await DetailAsync(Work(method));
        Assert.Equal(driverFee, html.Contains("기사 배송비는 물품 거래 금액과 별도로 확인합니다.", StringComparison.Ordinal));
        Assert.Equal(method is not null, html.Contains("<h2>전달 방법</h2>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, "미정")]
    [InlineData("0", "0원")]
    [InlineData("5000", "5,000원")]
    public async Task 완료한약속도_미정과명시0을구분하고_입금확인으로바꾸지않는다(string? amount, string expected)
    {
        var value = Work(NeighborhoodTransferMethods.RecipientPickup);
        value.StatusCode = NeighborhoodCollaborationStates.Completed;
        value.Terms!.AgreedCostKrw = amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        var html = await DetailAsync(value);
        Assert.Contains($"<dt>물품 거래 금액</dt><dd>{expected}</dd>", html);
        Assert.Contains("<dt>지급 확인</dt><dd>플랫폼 입금 미확인</dd>", html);
        Assert.Contains("약속 완료는 지급 완료를 뜻하지 않습니다.", html);
        Assert.DoesNotContain("입금 완료", html);
    }

    [Fact]
    public async Task 허용행동이없으면_상태만으로시작이나완료버튼을만들지않는다()
    {
        var value = Work(NeighborhoodTransferMethods.RecipientPickup);
        value.StatusCode = NeighborhoodCollaborationStates.CompletionProposed;
        value.CompletionProposedByMe = true;
        var html = await DetailAsync(value);
        Assert.Contains("상대방의 완료 확인을 기다리고 있습니다.", html);
        Assert.DoesNotContain("완료 확인하기</button>", html);
        Assert.DoesNotContain("협업 시작하기</button>", html);
    }

    [Theory]
    [InlineData("delivery-pending", "배송 접수 결과를 먼저 확인해 주세요.", "배송 접수 결과 확인")]
    [InlineData("delivery-linked", "배송 진행을 확인해 주세요.", "배송 진행 상세 보기")]
    [InlineData("storage-pending", "보관 예약 결과를 먼저 확인해 주세요.", "보관공간에서 예약·인계 확인")]
    public async Task 주행동이없는인계단계는_실제대기상태와기존상세연결을표시한다(string stage, string instruction, string routeLabel)
    {
        var value = Work(NeighborhoodTransferMethods.DriverDelivery);
        if (stage == "delivery-pending") value.DeliveryRegistrationPending = true;
        if (stage == "delivery-linked") value.LinkedDeliveryRequestId = "delivery-original";
        if (stage == "storage-pending")
        {
            value.StorageReservationPending = true;
            value.Terms!.StorageSpaceId = "storage-original";
        }
        var html = await DetailAsync(value);
        Assert.Contains(instruction, html); Assert.Contains(routeLabel, html);
        var terms = html.IndexOf("data-neighborhood-terms", StringComparison.Ordinal);
        Assert.InRange(html.IndexOf(instruction, StringComparison.Ordinal), 0, terms - 1);
        if (stage == "delivery-linked") Assert.Contains("/community/exchange/deliveries/delivery-original", html);
        if (stage == "storage-pending") Assert.Contains("/community/exchange/spaces/storage-original?collaborationId=work-original", html);
    }

    [Fact]
    public async Task 완료공개는별도선택이며_모든당사자동의와문제접수경로를보존한다()
    {
        var value = Work(NeighborhoodTransferMethods.RecipientPickup);
        value.StatusCode = NeighborhoodCollaborationStates.Completed;
        value.AllowedActions = [NeighborhoodCollaborationActions.PublicHistoryConsent];
        value.Participants = [new() { IsMe = true, StatusCode = "accepted" }];
        var html = await DetailAsync(value);
        Assert.Contains("완료 확인과 별도의 선택입니다.", html);
        Assert.Contains("양측과 수락한 참여자가 모두 동의", html);
        Assert.Contains("공개 선택 저장", html); Assert.Contains("현재 기록 공개: 비공개", html);
        Assert.Contains("함께하는 사람", html); Assert.Contains("내 참여 · 참여 수락 완료", html);
        Assert.Contains("commerce/disputes?sourceKind=neighborhood-collaboration&sourceId=work-original", html);
    }

    [Fact]
    public async Task 보호된인계장소가없으면_공개주소나가짜장소를채우지않는다()
    {
        var value = Work(NeighborhoodTransferMethods.RecipientPickup);
        value.Terms!.HandoverPlace = null;
        var html = await DetailAsync(value);
        Assert.Contains("양측 정보 제공 동의 후 인계 장소를 확인할 수 있습니다.", html);
        Assert.DoesNotContain("private-handover-address", html);
    }

    [Theory]
    [InlineData(NeighborhoodTransferMethods.RecipientPickup, false)]
    [InlineData(NeighborhoodTransferMethods.DriverDelivery, true)]
    public async Task 약속제안은_내용과전달과금액을구분하고_명시신청을유지한다(string method, bool driverFee)
    {
        var html = await CreateAsync(method, 0);
        Assert.Contains("<h1>약속 제안</h1>", html);
        Assert.True(html.IndexOf("<h2>약속 내용</h2>", StringComparison.Ordinal) < html.IndexOf("<h2>물건을 전달하는 방법</h2>", StringComparison.Ordinal));
        Assert.True(html.IndexOf("<h2>물건을 전달하는 방법</h2>", StringComparison.Ordinal) < html.IndexOf("<h2>금액과 지급 안내</h2>", StringComparison.Ordinal));
        Assert.Contains("비워 두면 미정이고, 0을 입력하면 0원입니다.", html);
        Assert.Equal(driverFee, html.Contains("기사 배송비는 물품 거래 금액과 별도로 확인합니다.", StringComparison.Ordinal));
        Assert.Matches("<input[^>]*id=\"work-cost\"[^>]*value=\"0\"", html);
        Assert.Contains("신청만으로 상대 동의나 배송이 완료되지는 않습니다.", html);
        Assert.Contains("협업 신청하기", html);
        Assert.Matches("<button[^>]*type=\"submit\"[^>]*disabled", html);
    }

    [Fact]
    public async Task 익명은_로그인진입만보이고_사적약속과작성폼을표시하지않는다()
    {
        var html = await DetailAsync(Work(NeighborhoodTransferMethods.RecipientPickup), authenticated: false);
        Assert.Contains("로그인이 필요해요", html); Assert.Contains("로그인하기", html);
        Assert.DoesNotContain("data-neighborhood-current", html); Assert.DoesNotContain("private-handover-address", html);
        var create = await CreateAsync(NeighborhoodTransferMethods.RecipientPickup, null, authenticated: false);
        Assert.DoesNotContain("<form", create); Assert.DoesNotContain("work-cost", create);
    }

    private static Task<string> DetailAsync(NeighborhoodCollaborationResponse value, bool authenticated = true)
        => RenderAsync<NeighborhoodCollaborationDetail>(value, authenticated,
            new() { [nameof(NeighborhoodCollaborationDetail.WorkId)] = value.StableId });

    private static Task<string> CreateAsync(string method, decimal? amount, bool authenticated = true)
        => RenderAsync<NeighborhoodCollaborationCreate>(Work(method), authenticated,
            new() { [nameof(NeighborhoodCollaborationCreate.PostId)] = 12L }, method, amount);

    private static async Task<string> RenderAsync<T>(NeighborhoodCollaborationResponse value, bool authenticated,
        Dictionary<string, object?> parameters, string? method = null, decimal? amount = null) where T : IComponent
    {
        var fixture = new Fixture(value, authenticated);
        var services = new ServiceCollection(); services.AddLogging(); services.AddCommerceUiFixture();
        services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<ISsalddel현재사용자Context>(fixture);
        services.AddSingleton<INeighborhoodCollaborationClient>(fixture);
        services.AddSingleton<INeighborhoodExchangeClient>(fixture);
        services.AddSingleton<INeighborhoodStorageClient>(fixture);
        services.AddSingleton<NeighborhoodCollaborationDraftSession>();
        services.AddSingleton<NeighborhoodCollaborationQueryViewModel>();
        services.AddSingleton<NeighborhoodCollaborationAuthoringViewModel>();
        await using var provider = services.BuildServiceProvider();
        if (typeof(T) == typeof(NeighborhoodCollaborationCreate) && authenticated)
        {
            var model = provider.GetRequiredService<NeighborhoodCollaborationAuthoringViewModel>();
            await model.InitializeAsync(12, null);
            model.Form.Draft.Terms.TransferMethod = method;
            model.Form.Draft.Terms.AgreedCostKrw = amount;
        }
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        Assert.Equal(0, fixture.Writes);
        var commerce = (CommerceUiFixture)provider.GetRequiredService<Ssalddel.Ui.Common.Areas.App.Services.Commerce.I통신판매보호Client>();
        Assert.Equal(0, commerce.Commands); Assert.Equal(0, commerce.DisputeCalls); Assert.Empty(commerce.Rights);
        if (!authenticated) Assert.Equal(0, fixture.PrivateReads);
        return html;
    }

    private static NeighborhoodCollaborationResponse Work(string? method) => new()
    {
        StableId = "work-original", Revision = 7, SourcePostId = 12, SourceTitle = "동네 물품 나눔", Kind = NeighborhoodCollaborationKinds.Goods,
        StatusCode = NeighborhoodCollaborationStates.InProgress, IsRequester = true, OwnerAgreed = true, RequesterAgreed = true,
        Terms = new() { Summary = "물품 두 개 전달", Quantity = 2, Unit = "개", TransferMethod = method,
            FromUtc = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc), UntilUtc = new(2026, 10, 7, 2, 0, 0, DateTimeKind.Utc),
            HandoverPlace = "private-handover-address", Notes = "기존 추가 조건" }
    };

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/roles/01/", "http://localhost/roles/01/community/map");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class Fixture(NeighborhoodCollaborationResponse value, bool authenticated) : ISsalddel현재사용자Context,
        INeighborhoodCollaborationClient, INeighborhoodExchangeClient, INeighborhoodStorageClient
    {
        public int Writes, PrivateReads;
        public 현재사용자Snapshot 현재사용자 => authenticated ? new("owner", "이웃", []) : 현재사용자Snapshot.익명;
        public Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct) { PrivateReads++; return Task.FromResult<NeighborhoodCollaborationResponse?>(value); }
        public Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct) => Task.FromResult<PlatformCommunityPostResponse?>(new()
        { Id = id, Title = "동네 물품 나눔", RoleTag = NeighborhoodExchange.Offer, WorkflowTag = NeighborhoodExchange.WorkflowTag, Category = PlatformCommunityPostCategories.General });
        public Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct) => Write<NeighborhoodCollaborationResponse>();
        public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct) => Write<NeighborhoodCollaborationResponse>();
        public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid id, string? target, CancellationToken ct) => Write<NeighborhoodCollaborationResponse>();
        public Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct) => Write<PlatformCommunityPostResponse>();
        public Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct) => Write<PlatformCommunityPostCommentResponse>();
        public Task DeleteAsync(long id, string? password, CancellationToken ct) => Write<object>();
        public Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct) => Write<object>();
        public Task ReportCommentAsync(long id, CancellationToken ct) => Write<object>();
        Task<NeighborhoodStorageListResponse> INeighborhoodStorageClient.ListAsync(string? region, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodStorageSpaceDto>> MineAsync(int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodStoragePublicDto?> PublicAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodStorageSpaceDto?> PrivateAsync(string id, string? workId, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodStorageSpaceDto?> SaveAsync(string? id, NeighborhoodStorageSpaceRequest request, CancellationToken ct) => Write<NeighborhoodStorageSpaceDto>();
        public Task<NeighborhoodStorageSpaceDto?> StateAsync(string id, string action, NeighborhoodStorageMutationRequest request, CancellationToken ct) => Write<NeighborhoodStorageSpaceDto>();
        public Task<NeighborhoodStorageSpaceDto?> ReserveAsync(string id, NeighborhoodStorageReservationRequest request, CancellationToken ct) => Write<NeighborhoodStorageSpaceDto>();
        public Task<NeighborhoodStorageSpaceDto?> HandoverAsync(string id, string workId, NeighborhoodStorageReservationActionRequest request, CancellationToken ct) => Write<NeighborhoodStorageSpaceDto>();
        public Task<NeighborhoodStorageSpaceDto?> ReceiptAsync(Guid id, string? spaceId, CancellationToken ct, string? workId = null) => Write<NeighborhoodStorageSpaceDto>();
        private Task<T?> Write<T>() { Writes++; throw new InvalidOperationException("Unexpected mutation during render"); }
    }
}
