using System.Text.Json;
using DriverApp.Models.Driver;
using DriverApp.Services.Samples;
using Ssalddel.Contracts.Driver.Recommendation;

namespace Ssalddel.Tests.Clients;

public sealed class DriverCargoConditionPresentationTests
{
    [Theory]
    [InlineData("단독", "독차")]
    [InlineData(" 독차 ", "독차")]
    [InlineData("혼적", "혼적")]
    [InlineData(null, "수락")]
    [InlineData("", "수락")]
    [InlineData("협의", "수락")]
    [InlineData("미정", "수락")]
    public void 수락버튼은_확인된제안방식만이름으로쓰고_원조건을바꾸지않는다(string? mode, string button)
    {
        var request = new DriverRequestItem { 운송방식 = mode ?? string.Empty };
        var snapshot = request.CaptureCargoConditions();

        Assert.Equal(button, request.수락버튼표시);
        Assert.Equal(mode ?? string.Empty, request.운송방식);
        Assert.Equal(snapshot, request.CaptureCargoConditions());
    }

    [Fact]
    public void 요약시간은_월일시간이며_상세는연도와한국시각을유지한다()
    {
        var request = new DriverRequestItem
        {
            상차시간창시작일시 = new DateTime(2026, 10, 24, 3, 0, 0, DateTimeKind.Utc),
            상차시간창종료일시 = new DateTime(2026, 10, 24, 4, 0, 0, DateTimeKind.Unspecified),
            하차시간창시작일시 = new DateTime(2026, 12, 31, 14, 30, 0, DateTimeKind.Utc),
            하차시간창종료일시 = new DateTime(2026, 12, 31, 15, 30, 0, DateTimeKind.Utc)
        };
        var snapshot = request.CaptureCargoConditions();

        Assert.Equal("10/24 12:00 ~ 13:00", request.간략상차시간표시);
        Assert.Equal("12/31 23:30 ~ 01/01 00:30", request.간략하차시간표시);
        Assert.Equal("2026-10-24 12:00 ~ 13:00 (한국)", request.상차시간표시);
        Assert.Equal("2026-12-31 23:30 ~ 2027-01-01 00:30 (한국)", request.하차시간표시);
        Assert.Equal(snapshot, request.CaptureCargoConditions());
    }

    [Fact]
    public void 간략시간도_누락과역전된범위를감추지않는다()
    {
        var time = new DateTime(2026, 10, 24, 3, 0, 0, DateTimeKind.Utc);
        var request = new DriverRequestItem { 상차시간창시작일시 = time, 하차시간창종료일시 = time };
        Assert.Equal("10/24 12:00 ~ 종료 미확인", request.간략상차시간표시);
        Assert.Equal("시작 미확인 ~ 10/24 12:00", request.간략하차시간표시);
        request.상차시간창종료일시 = time.AddHours(-1);
        Assert.Equal("시간 범위 확인 필요 · 10/24 12:00 ~ 11:00", request.간략상차시간표시);
        Assert.Equal("미확인", new DriverRequestItem().간략상차시간표시);
        Assert.Equal("미확인", new DriverRequestItem { 하차시간창시작일시 = default(DateTime) }.간략하차시간표시);
    }

    [Theory]
    [InlineData(null, "미확인")]
    [InlineData("", "미확인")]
    [InlineData("단독", "독차")]
    [InlineData("혼적", "혼적")]
    [InlineData("협의", "협의")]
    public void 추천매핑은_원장운송방식을보존하고_표시만정규화한다(string? mode, string display)
    {
        var source = new 기사배차추천항목응답 { 의뢰Id = "condition-request", 운송방식 = mode };

        var mapped = ServerBackedDriverSampleDataService.ToRequestItem(source);

        Assert.Equal(mode, source.운송방식);
        Assert.Equal(mode ?? string.Empty, mapped.운송방식);
        Assert.Equal(display, mapped.운송조건표시);
        Assert.DoesNotContain("서버 추천", mapped.운송조건표시);
    }

    [Fact]
    public void 실제추천조건과_네시간값은_수락전표시모델까지손실없이전달된다()
    {
        var pickupStart = new DateTime(2026, 10, 24, 14, 30, 0, DateTimeKind.Utc);
        var pickupEnd = pickupStart.AddHours(1);
        var source = new 기사배차추천항목응답
        {
            의뢰Id = "condition-request", 운송방식 = "단독", 차량종류 = "1톤 냉장",
            화물중량Kg = 12.5m, 화물수량 = 7,
            상차시간창시작일시 = pickupStart, 상차시간창종료일시 = pickupEnd,
            하차시간창시작일시 = pickupEnd.AddHours(1), 하차시간창종료일시 = pickupEnd.AddHours(2),
            차량적합여부 = true
        };

        var mapped = ServerBackedDriverSampleDataService.ToRequestItem(source);

        Assert.Equal(source.차량종류, mapped.차량종류);
        Assert.Equal(source.화물중량Kg, mapped.화물중량Kg);
        Assert.Equal(source.화물수량, mapped.화물수량);
        Assert.Equal(source.상차시간창시작일시, mapped.상차시간창시작일시);
        Assert.Equal(source.상차시간창종료일시, mapped.상차시간창종료일시);
        Assert.Equal(source.하차시간창시작일시, mapped.하차시간창시작일시);
        Assert.Equal(source.하차시간창종료일시, mapped.하차시간창종료일시);
        Assert.Equal("1톤 냉장", mapped.차량조건표시);
        Assert.Equal("12.5 kg", mapped.화물중량표시);
        Assert.Equal("7개", mapped.화물수량표시);
        Assert.Equal("2026-10-24 23:30 ~ 2026-10-25 00:30 (한국)", mapped.상차시간표시);
        Assert.Equal("2026-10-25 01:30 ~ 02:30 (한국)", mapped.하차시간표시);
        Assert.Equal(string.Empty, mapped.차량톤수);
        Assert.Equal(string.Empty, mapped.차량형태);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 적합판정은_누락된차량조건이나시간조건을만들지않는다(bool suitable)
    {
        var mapped = ServerBackedDriverSampleDataService.ToRequestItem(
            new 기사배차추천항목응답 { 의뢰Id = "old-request", 차량적합여부 = suitable });

        Assert.Null(mapped.차량종류);
        Assert.Null(mapped.화물중량Kg);
        Assert.Null(mapped.화물수량);
        Assert.Equal("미확인", mapped.운송조건표시);
        Assert.Equal("미확인", mapped.차량조건표시);
        Assert.Equal("미확인", mapped.화물중량표시);
        Assert.Equal("미확인", mapped.화물수량표시);
        Assert.Equal("미확인", mapped.상차시간표시);
        Assert.Equal("미확인", mapped.하차시간표시);
        Assert.DoesNotContain("협의", mapped.상차시간표시);
    }

    [Fact]
    public void 이전JSON과당일필수표시는_확인되지않은구체시간이나운송방식을채우지않는다()
    {
        var request = JsonSerializer.Deserialize<DriverRequestItem>("{\"당일상차필수\":true,\"당일하차필수\":true}")!;

        Assert.Equal("미확인", request.운송조건표시);
        Assert.Equal("당상·당착", request.시간조건표시);
        Assert.Equal("미확인", request.상차시간표시);
        Assert.Equal("미확인", request.하차시간표시);
        Assert.Equal("미확인", request.차량조건표시);
    }

    [Fact]
    public void 시간창일부누락은_있는시각을보존하면서_없는끝을명시한다()
    {
        var request = new DriverRequestItem
        {
            상차시간창시작일시 = new DateTime(2026, 12, 31, 14, 0, 0, DateTimeKind.Utc),
            하차시간창종료일시 = new DateTime(2026, 12, 31, 16, 0, 0, DateTimeKind.Utc)
        };

        Assert.Equal("2026-12-31 23:00 ~ 종료 미확인 (한국)", request.상차시간표시);
        Assert.Equal("시작 미확인 ~ 2027-01-01 01:00 (한국)", request.하차시간표시);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void 수락전조건snapshot은_같은추천차수의조건변경도식별한다(int changed)
    {
        var request = new DriverRequestItem { 의뢰Id = "same-request", 추천라운드 = 2 };
        var reviewed = request.CaptureCargoConditions();
        var time = new DateTime(2026, 10, 24, 1, 0, 0, DateTimeKind.Utc);
        switch (changed)
        {
            case 0: request.운송방식 = "단독"; break;
            case 1: request.차량종류 = "1톤 카고"; break;
            case 2: request.화물중량Kg = 10; break;
            case 3: request.화물수량 = 2; break;
            case 4: request.상차시간창시작일시 = time; break;
            case 5: request.상차시간창종료일시 = time; break;
            case 6: request.하차시간창시작일시 = time; break;
            case 7: request.하차시간창종료일시 = time; break;
        }

        Assert.Equal("same-request", request.의뢰Id);
        Assert.Equal(2, request.추천라운드);
        Assert.NotEqual(reviewed, request.CaptureCargoConditions());
    }
}
