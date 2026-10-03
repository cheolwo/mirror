using SsalddelApp.Models.Shipper;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperContactWindowMappingTests
{
    [Fact]
    public void 한국시간의연락처와상세주소를_UTC원장시간창으로전달한다()
    {
        var draft = new 운송모델작성Draft
        {
            픽업도로명주소 = "서울 검증 주소", 픽업상세주소 = "A동 상차장",
            픽업연락처이름 = "상차 담당", 픽업연락처전화번호 = "010-1111-2222",
            픽업시간창시작일시 = new(2030, 10, 3, 1, 0, 0),
            픽업시간창종료일시 = new(2030, 10, 3, 2, 0, 0)
        };
        var location = ShipperRequestHandoffMapper.CreatePickup(draft);
        Assert.Equal(draft.픽업도로명주소, location.주소.도로명주소);
        Assert.Equal(draft.픽업상세주소, location.주소.상세주소);
        Assert.Equal(draft.픽업연락처이름, location.연락처.이름);
        Assert.Equal(draft.픽업연락처전화번호, location.연락처.전화번호);
        Assert.Equal(new DateTime(2030, 10, 2, 16, 0, 0, DateTimeKind.Utc), location.시간창?.시작일시);
        Assert.Equal(new DateTime(2030, 10, 2, 17, 0, 0, DateTimeKind.Utc), location.시간창?.종료일시);
    }

    [Fact]
    public void 하차연락처와시간미입력은_가짜담당자전화나예약시간으로채우지않는다()
    {
        var location = ShipperRequestHandoffMapper.CreateDropoff(new() { 하차도로명주소 = "하차 주소" });
        Assert.Empty(location.연락처.이름);
        Assert.Empty(location.연락처.전화번호);
        Assert.Null(location.시간창);
        Assert.Null(ShipperRequestHandoffMapper.Copy(null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 불완전한시간창은_Mapper우회호출에서도거절한다(bool missingEnd)
    {
        var draft = new 운송모델작성Draft { 하차시간창시작일시 = new(2030, 10, 3, 10, 0, 0) };
        if (!missingEnd) draft.하차시간창종료일시 = draft.하차시간창시작일시;
        Assert.Throws<ArgumentException>(() => ShipperRequestHandoffMapper.CreateDropoff(draft));
    }

    [Fact]
    public void 수정용복사는_조회원장개인정보와시간창객체를공유하지않는다()
    {
        var source = ShipperRequestHandoffMapper.CreatePickup(new()
        {
            픽업연락처전화번호 = "010-1111-2222", 픽업상세주소 = "원본 상세",
            픽업시간창시작일시 = new(2030, 10, 3, 10, 0, 0), 픽업시간창종료일시 = new(2030, 10, 3, 11, 0, 0)
        });
        var copy = ShipperRequestHandoffMapper.Copy(source, "수정 주소")!;
        copy.연락처.전화번호 = "010-3333-4444";
        copy.주소.상세주소 = "수정 상세";
        copy.시간창!.종료일시 = copy.시간창.종료일시.AddHours(1);
        Assert.Equal("010-1111-2222", source.연락처.전화번호);
        Assert.Equal("원본 상세", source.주소.상세주소);
        Assert.Equal("수정 주소", copy.주소.도로명주소);
        Assert.NotEqual(source.시간창?.종료일시, copy.시간창.종료일시);
    }
}
