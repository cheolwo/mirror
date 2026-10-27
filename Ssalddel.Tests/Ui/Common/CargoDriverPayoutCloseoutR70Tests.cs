using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoDriverPayoutCloseoutR70Tests
{
    [Fact]
    public void 일반화물_추천과_수락후보수는_고객청구와_구별한다()
    {
        var offer = CargoDriverWorkspaceAdapter.MapOffer(Offer(6200m));
        Assert.Equal("6,200원", Assert.Single(offer.Summary!.Metrics!, x => x.Label == "기사 제안 보수").Value);
        var current = CargoDriverWorkspaceAdapter.Map(new 기사운송요약응답
        {
            Id = 1, 상태 = "운송중", 운임 = 18000m, 기사지급예정액 = 6200m,
            기사지급예정액근거 = "운송원장.기사지급예정액"
        });
        Assert.Equal("6,200원", Assert.Single(current.Summary!.Metrics!, x => x.Label == "기사 지급 예정액").Value);
        Assert.DoesNotContain(current.Summary.Metrics!, x => x.Value == "18,000원");
        Assert.Contains(current.Sections!.SelectMany(x => x.Fields), x => x.Label == "의뢰 청구 운임" && x.Value == "18,000원");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("6200.5")]
    public void 수락후_동결액이없거나유효하지않으면_고객운임으로채우지않는다(string? raw)
    {
        var value = raw is null ? null : (decimal?)decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        var transport = new 기사운송요약응답 { 운임 = 18000m, 기사지급예정액 = value, 기사지급예정액근거 = "운송원장.기사지급예정액" };
        Assert.Null(CargoDriverPayoutPresentation.FrozenAmount(transport));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수락직전_보수가달라지면_다시확인하고_변함없으면기존수락을한다(bool changed)
    {
        var api = new Api(changed);
        using var model = new CargoOfferActionViewModel(new(api), new Access());
        await model.LoadAsync("quick-r70");
        model.ConditionsConfirmed = model.AcceptConfirmed = true;
        Assert.True(model.CanSubmit);
        await model.SubmitAsync();
        Assert.Equal(changed ? 0 : 1, api.Writes);
        Assert.Equal(!changed, model.Saved);
        Assert.Equal(changed, model.Lifetime.HasError);
        if (changed)
        {
            Assert.False(model.ConditionsConfirmed); Assert.False(model.AcceptConfirmed);
            Assert.Contains("조건", model.Lifetime.Message);
            Assert.Equal(6400m, CargoDriverPayoutPresentation.OfferAmount(model.Offer));
        }
        else Assert.Equal("11", model.TransportId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("6200.5")]
    public async Task 확인되지않은보수는_확인체크를해도_수락명령을보내지않는다(string? raw)
    {
        var amount = raw is null ? null : (decimal?)decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        var api = new Api(false, amount);
        using var model = new CargoOfferActionViewModel(new(api), new Access());
        await model.LoadAsync("quick-r70");
        model.ConditionsConfirmed = model.AcceptConfirmed = true;
        Assert.True(model.ServerCandidateAvailable); Assert.False(model.CanSubmit);
        await model.SubmitAsync(); Assert.Equal(0, api.Writes);
    }

    [Theory]
    [InlineData("payout")]
    [InlineData("round")]
    [InlineData("warning")]
    [InlineData("expiry")]
    [InlineData("start")]
    [InlineData("unchanged")]
    public async Task 같은의뢰를재조회해_조건이변경되면_다시확인한뒤에만_수락한다(string changeKind)
    {
        var changed = changeKind != "unchanged";
        var api = new Api(changed, changeKind: changeKind);
        using var model = new CargoOfferActionViewModel(new(api), new Access());
        await model.LoadAsync("quick-r70");
        model.ConditionsConfirmed = model.AcceptConfirmed = true;
        await model.LoadAsync("quick-r70");
        Assert.False(model.Lifetime.HasError);
        Assert.Equal(!changed, model.ConditionsConfirmed); Assert.Equal(!changed, model.AcceptConfirmed);
        Assert.Equal(!changed, model.CanSubmit);
        Assert.Contains("추천 조건", model.Lifetime.Message);
        Assert.Equal(changeKind == "payout" ? 6400m : 6200m, CargoDriverPayoutPresentation.OfferAmount(model.Offer));
        if (changed)
        {
            await model.SubmitAsync(); Assert.Equal(0, api.Writes);
            model.ConditionsConfirmed = model.AcceptConfirmed = true;
        }
        Assert.True(model.CanSubmit);
        await model.SubmitAsync();
        Assert.Equal(1, api.Writes); Assert.True(model.Saved); Assert.False(model.Lifetime.HasError);
    }

    private static 기사배차추천항목응답 Offer(decimal? amount) => new()
    {
        의뢰Id = "quick-r70", 차량적합여부 = true, 추천라운드 = 1,
        추천시작시각 = DateTime.UtcNow.AddMinutes(-1), 추천만료시각 = DateTime.UtcNow.AddMinutes(5),
        기사지급예정운임 = amount, 기사지급예정운임근거 = "운임구성.기사지급예정운임"
    };

    private sealed class Api(bool changed, decimal? initialAmount = 6200m, string changeKind = "payout") : IRoleWorkspaceApi
    {
        private int offerReads;
        private readonly DateTime recommendationStartedAt = DateTime.UtcNow.AddMinutes(-1);
        public int Writes { get; private set; }
        public Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
        {
            object result = path.EndsWith("/recommendations", StringComparison.Ordinal)
                ? (IReadOnlyList<기사배차추천항목응답>)[ReadOffer()]
                : path.EndsWith("/workspace", StringComparison.Ordinal)
                    ? new 기사화물운송작업공간응답 { 활성운송목록 = [new() { Id = 11, 운송번호 = "quick-r70" }] }
                    : new 기사운송의뢰상세응답 { 의뢰Id = "quick-r70", 차량적합여부 = true, 배차상태 = "대기", 결제예정금액 = 18000 };
            return Task.FromResult((T)result);
        }
        private 기사배차추천항목응답 ReadOffer()
        {
            var updated = ++offerReads > 1 && changed;
            var offer = Offer(updated && changeKind == "payout" ? 6400m : initialAmount);
            offer.추천시작시각 = recommendationStartedAt.AddSeconds(updated && changeKind == "start" ? 10 : 0);
            offer.추천만료시각 = recommendationStartedAt.AddMinutes(updated && changeKind == "expiry" ? 7 : 6);
            if (updated && changeKind == "round") offer.추천라운드++;
            if (updated && changeKind == "warning") offer.경고 = ["변경된 운송 주의사항"];
            return offer;
        }
        public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
        {
            Assert.EndsWith("/quick-r70/accept", path); Assert.IsType<기사화물배차수락요청>(body);
            Writes++; return Task.FromResult((T?)(object)new 기사배차처리응답 { RequestId = "quick-r70" });
        }
        public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string role) => new("driver-r70", 1, true);
        public Task EnsureInitializedAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string role, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
    }
}
