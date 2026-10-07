using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Services.Privacy;

namespace Ssalddel.Tests.Services.Privacy;

public sealed class NeighborhoodExchangeConsentTests
{
    private const string UserId = "neighborhood-requester";

    [Fact]
    public async Task 생활교류배송도_명시동의를기록하고_같은계정업무출처로만사용한다()
    {
        var service = CreateService();
        var request = CreateRequest();

        var response = await service.동의기록Async(request, UserId);

        Assert.Equal(request.증적Id, response.증적Id);
        Assert.Equal(신청개인정보출처Codes.생활교류, response.출처Code);
        Assert.Equal(신청개인정보업무Codes.운송대행, response.업무Code);
        Assert.Equal(신청개인정보동의정책.For(신청개인정보업무Codes.운송대행).수집항목, response.수집항목);
        Assert.Equal(신청개인정보동의정책.보유이용기간, response.보유이용기간);
        Assert.Equal(64, response.동의문Hash.Length);
        await Require(service, request.증적Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 생활교류출처가_증적없는신청을묵인하지않는다(bool emptyId)
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Require(CreateService(), emptyId ? Guid.Empty : null));

    [Theory]
    [InlineData("other-user", 신청개인정보업무Codes.운송대행, 신청개인정보출처Codes.생활교류)]
    [InlineData(UserId, 신청개인정보업무Codes.물류대행, 신청개인정보출처Codes.생활교류)]
    [InlineData(UserId, 신청개인정보업무Codes.운송대행, 신청개인정보출처Codes.커뮤니티지도)]
    public async Task 생활교류증적도_소유자업무출처를바꾸어재사용할수없다(
        string userId, string workCode, string sourceCode)
    {
        var service = CreateService();
        var evidence = await service.동의기록Async(CreateRequest(), UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.유효한동의요구Async(
            evidence.증적Id, workCode, sourceCode, userId));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 생활교류도_수집이용동의와연령확인이각각필수다(bool collectionUse, bool age)
    {
        var service = CreateService();
        var request = CreateRequest();
        request.수집이용동의 = collectionUse;
        request.연령요건확인 = age;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.동의기록Async(request, UserId));
        Assert.Null(await service.내증적조회Async(request.증적Id, UserId));
    }

    [Fact]
    public async Task 철회된생활교류동의를_다시신청에쓰거나재기록으로살리지않는다()
    {
        var service = CreateService();
        var request = CreateRequest();
        await service.동의기록Async(request, UserId);
        await service.철회Async(request.증적Id, new 신청개인정보동의철회Request(), UserId);

        var replay = await service.동의기록Async(request, UserId);

        Assert.Equal(신청개인정보동의상태Codes.철회, replay.상태Code);
        Assert.NotNull(replay.철회일시Utc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Require(service, request.증적Id));
    }

    [Fact]
    public async Task 오래된문안은_새출처라고수용하지않는다()
    {
        var request = CreateRequest();
        request.동의문버전 = "previous-version";
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().동의기록Async(request, UserId));
    }

    [Fact]
    public async Task 동의출처의임의변형은_허용목록을넓히지않는다()
    {
        var request = CreateRequest();
        request.출처Code = "neighborhood-exchange-other";
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().동의기록Async(request, UserId));
    }

    private static 신청개인정보동의증적Service CreateService()
        => new(new InMemory신청개인정보동의증적Store());

    private static Task Require(신청개인정보동의증적Service service, Guid? evidenceId)
        => service.유효한동의요구Async(evidenceId, 신청개인정보업무Codes.운송대행,
            신청개인정보출처Codes.생활교류, UserId);

    private static 신청개인정보동의기록Request CreateRequest()
        => new()
        {
            증적Id = Guid.NewGuid(),
            업무Code = 신청개인정보업무Codes.운송대행,
            출처Code = 신청개인정보출처Codes.생활교류,
            동의문버전 = 신청개인정보동의정책.현재버전,
            수집이용동의 = true,
            연령요건확인 = true
        };
}
