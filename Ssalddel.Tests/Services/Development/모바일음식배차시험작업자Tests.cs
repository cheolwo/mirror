using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.Development.MobileFieldTest;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Services.Development;

public sealed class 모바일음식배차시험작업자Tests
{
    [Fact]
    public async Task 적격후보가없으면대기하고_On후_기존정책과전환코어로요금을동결하여추천한다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        await host.SeedAsync(모바일음식배차시험TestHost.Queue());
        host.ReceivingState.Current.수신의사Code = 운영배차수신의사Code.Off;
        Assert.Equal(1, await host.Worker.한번실행Async(CancellationToken.None));
        var pending = await host.ReadAsync("fixture-order");
        Assert.Equal(상태값.배차노출상태.추천후보없음, pending.배차노출상태);
        Assert.Null(pending.현재추천대상기사Id); Assert.Null(pending.공개전환시각);
        Assert.Empty(host.NotificationState.Created);

        host.ReceivingState.Current.수신의사Code = 운영배차수신의사Code.On;
        Assert.Equal(1, await host.Worker.한번실행Async(CancellationToken.None));
        var offered = await host.ReadAsync("fixture-order");
        Assert.Equal(상태값.배차노출상태.추천중, offered.배차노출상태);
        Assert.Equal(모바일음식배차시험TestHost.DriverId, offered.현재추천대상기사Id);
        Assert.Equal(2500m, offered.기사지급예정액);
        Assert.Equal(1, offered.추천라운드);
        var evidence = JsonSerializer.Deserialize<음식배달기사제안요금산정결과>(offered.기사제안요금계산근거Json!);
        Assert.Equal(0.350m, evidence!.산정거리Km);
        Assert.Equal(모바일음식배차시험자료.DistanceSourceCode, evidence.거리근거Code);
        Assert.Contains("IsEstimated=true|ActualMapRequest=false", evidence.정책판본);
        Assert.False(evidence.기상.유효한강수근거있음); Assert.Null(evidence.경로소요시간초);
        Assert.Equal(["fixture-order"], host.NotificationState.Created);
        Assert.Equal(0, await host.Worker.한번실행Async(CancellationToken.None));
        Assert.Equal(offered.기사제안요금계산근거Json, (await host.ReadAsync("fixture-order")).기사제안요금계산근거Json);
        await using var db = host.CreateContext();
        Assert.Equal("주문확인", (await db.음식주문.SingleAsync()).상태);
        Assert.Empty(await db.음식배달시도.ToListAsync()); // 추천은 수락·조리·배달 완료가 아니다.
    }

    [Fact]
    public async Task 만료는현재기사를제외하고_다음스캔에서재탐색하며_차수와요금근거를보존한다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        await host.SeedAsync(모바일음식배차시험TestHost.Queue());
        await host.Worker.한번실행Async(CancellationToken.None);
        await using (var db = host.CreateContext())
        {
            (await db.운송원장.SingleAsync()).추천만료시각 = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await host.Worker.한번실행Async(CancellationToken.None);
        var pending = await host.ReadAsync("fixture-order");
        Assert.Equal(상태값.배차노출상태.추천후보없음, pending.배차노출상태);
        Assert.Null(pending.현재추천대상기사Id); Assert.Null(pending.공개전환시각);
        Assert.Equal(1, pending.추천라운드);
        await host.Worker.한번실행Async(CancellationToken.None);
        var renewed = await host.ReadAsync("fixture-order");
        Assert.Equal(상태값.배차노출상태.추천중, renewed.배차노출상태);
        Assert.Equal(2, renewed.추천라운드);
        Assert.True(renewed.추천만료시각 > DateTime.UtcNow);
        Assert.Equal(pending.기사제안요금계산근거Json, renewed.기사제안요금계산근거Json);
        Assert.Equal(["fixture-order", "fixture-order"], host.NotificationState.Created);
    }

    [Theory]
    [InlineData("Off")]
    [InlineData("paused")]
    [InlineData("cargo-app")]
    [InlineData("stopped")]
    [InlineData("stale")]
    [InlineData("missing-position")]
    [InlineData("far")]
    [InlineData("rejected")]
    public async Task 기존음식기사자격조건을_시험호스트에서도생략하지않는다(string condition)
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        await host.SeedAsync(모바일음식배차시험TestHost.Queue());
        var state = (await host.States.GetAsync(모바일음식배차시험TestHost.DriverId))!;
        switch (condition)
        {
            case "Off": host.ReceivingState.Current.수신의사Code = "Off"; break;
            case "paused": host.ReceivingState.Current.실효상태Code = 운영배차실효상태Code.서버일시정지; break;
            case "cargo-app": state = state with { AppKey = "CargoYongdalDriverApp" }; break;
            case "stopped": state = state with { 운행상태 = 상태값.기사운행상태.대기 }; break;
            case "stale": state = state with { 위치수신시각Utc = DateTime.UtcNow.AddMinutes(-60) }; break;
            case "missing-position": state = state with { Longitude = null }; break;
            case "far": state = state with { Latitude = 33m }; break;
            case "rejected": await host.Rejections.RejectAsync(모바일음식배차시험TestHost.DriverId, "fixture-order"); break;
        }
        await host.States.UpsertAsync(state);
        await host.Worker.한번실행Async(CancellationToken.None);
        var pending = await host.ReadAsync("fixture-order");
        Assert.Equal(상태값.배차노출상태.추천후보없음, pending.배차노출상태);
        Assert.Null(pending.현재추천대상기사Id);
        Assert.Empty(host.NotificationState.Created);
    }

    [Fact]
    public async Task 시험기사밖후보를_코어가선정해도_시험요금을적용하여추천하지않는다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        await host.SeedAsync(모바일음식배차시험TestHost.Queue());
        var existing = (await host.States.GetAsync(모바일음식배차시험TestHost.DriverId))!;
        await host.States.RemoveAsync(모바일음식배차시험TestHost.DriverId);
        await host.States.UpsertAsync(existing with { DriverId = "ordinary-driver" });
        var scope = 음식배달권정책.판정(new 배차경로좌표(existing.Latitude!.Value, existing.Longitude!.Value), null);
        await host.Scopes.Upsert기사Async(scope.배달권키, "ordinary-driver", []);
        await host.Worker.한번실행Async(CancellationToken.None);
        var pending = await host.ReadAsync("fixture-order");
        Assert.NotEqual(상태값.배차노출상태.추천중, pending.배차노출상태);
        Assert.Null(pending.현재추천대상기사Id); Assert.Null(pending.기사제안요금계산근거Json);
        Assert.Empty(host.NotificationState.Created);
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("address")]
    [InlineData("coordinate")]
    [InlineData("orderer")]
    [InlineData("cancelled")]
    [InlineData("source")]
    public async Task 비시험원장과화물은_시험스캔에서수정하지않는다(string condition)
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        var queue = 모바일음식배차시험TestHost.Queue();
        switch (condition)
        {
            case "cargo": queue.배차업무유형 = 상태값.배차업무유형.용달운송; break;
            case "address": queue.하차_도로명주소 = "실제 주소"; break;
            case "coordinate": queue.하차_경도 = 128m; break;
            case "source": queue.원본의뢰유형 = "MartOrder"; break;
        }
        await host.SeedAsync(queue);
        if (condition is "orderer" or "cancelled")
        {
            await using var db = host.CreateContext();
            var order = await db.음식주문.SingleAsync();
            if (condition == "orderer") order.주문자UserId = "ordinary-orderer";
            else order.상태 = "취소";
            await db.SaveChangesAsync();
        }
        var before = await host.ReadAsync("fixture-order");
        Assert.Equal(0, await host.Worker.한번실행Async(CancellationToken.None));
        var after = await host.ReadAsync("fixture-order");
        Assert.Equal(before.배차큐단계, after.배차큐단계);
        Assert.Equal(before.배차노출상태, after.배차노출상태);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Empty(host.NotificationState.Created);
    }

    [Fact]
    public async Task 실행중옵트인이해제되면_다음스캔도차단한다()
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        await host.SeedAsync(모바일음식배차시험TestHost.Queue());
        host.Options.Enabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Worker.한번실행Async(CancellationToken.None));
        Assert.Empty(host.NotificationState.Created);
    }

    [Theory]
    [InlineData("exact", true)]
    [InlineData("null-pair", true)]
    [InlineData("latitude-only", false)]
    [InlineData("longitude-only", false)]
    [InlineData("wrong-latitude", false)]
    [InlineData("wrong-longitude", false)]
    public async Task 주문좌표는_양쪽미입력이나정확한짝만허용하고_원장좌표를근거로추천한다(string condition, bool allowed)
    {
        await using var host = await 모바일음식배차시험TestHost.CreateAsync();
        await host.SeedAsync(모바일음식배차시험TestHost.Queue());
        await using (var db = host.CreateContext())
        {
            var order = await db.음식주문.SingleAsync();
            switch (condition)
            {
                case "null-pair": order.음식점위도 = null; order.음식점경도 = null; break;
                case "latitude-only": order.음식점경도 = null; break;
                case "longitude-only": order.음식점위도 = null; break;
                case "wrong-latitude": order.음식점위도 = 37.581450m; break;
                case "wrong-longitude": order.음식점경도 = 127.088950m; break;
            }
            await db.SaveChangesAsync();
        }
        var before = await host.ReadAsync("fixture-order");
        Assert.Equal(allowed ? 1 : 0, await host.Worker.한번실행Async(CancellationToken.None));
        var after = await host.ReadAsync("fixture-order");
        if (allowed)
        {
            Assert.Equal(상태값.배차노출상태.추천중, after.배차노출상태);
            Assert.Equal(모바일음식배차시험TestHost.DriverId, after.현재추천대상기사Id);
            var evidence = JsonSerializer.Deserialize<음식배달기사제안요금산정결과>(after.기사제안요금계산근거Json!);
            Assert.Equal(모바일음식배차시험자료.DistanceSourceCode, evidence!.거리근거Code);
            Assert.Equal(0.350m, evidence.산정거리Km);
            Assert.Contains("IsEstimated=true|ActualMapRequest=false", evidence.정책판본);
            Assert.Single(host.NotificationState.Created);
        }
        else
        {
            Assert.Equal(before.배차노출상태, after.배차노출상태);
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
            Assert.Null(after.현재추천대상기사Id);
            Assert.Empty(host.NotificationState.Created);
        }
    }
}
