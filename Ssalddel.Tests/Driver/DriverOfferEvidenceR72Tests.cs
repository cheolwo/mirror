using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Recommendation;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Driver
{
    public sealed class DriverOfferEvidenceR72Tests
    {
        [Fact]
        public void 실제음식산정결과JSON에서원거리거리비총액을표시로보존하고기존거리는분리한다()
        {
            var expected = Pricing();
            var saved = new 음식배달기사제안요금산정결과(new(3235m, 700m, 0, 3935m, true, false),
                new(false, false, 픽업지기상자료상태Code.Available, "0", "0", DateTime.UnixEpoch,
                    픽업지기상관측결과.공식자료출처, new string('a', 64)), expected.PolicyRevision, DateTime.UnixEpoch)
                { Pricing = expected, 산정거리Km = 1.23456789m, 경로거리Km = 6.5m };
            var queue = new 운송원장 { 의뢰Id = "food", 기사지급예정액 = 3935m, 기사제안요금정책판본 = expected.PolicyRevision,
                기사제안요금계산근거Json = JsonSerializer.Serialize(saved) };
            var evidence = 기사제안표시근거Service.요금근거(queue);
            Assert.Equal(expected, evidence);
            var offer = Offer(evidence) with { AssignedActivities = 기사제안표시근거Service.운송작업(false) };
            var projected = FoodDeliveryCurrentWorkProjector.ToOffer(offer);
            Assert.Equal(6.5m, projected.DistanceKm);
            Assert.Equal(1234.56789m, projected.Pricing!.RawDistanceMeters);
            Assert.Equal(1235m, projected.Pricing.DistanceFeeKrw);
            Assert.Equal(3935m, projected.Pricing.TotalPayoutKrw);
            Assert.Equal(3935m, projected.DriverPayout);
            Assert.Equal([DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver], projected.AssignedActivities);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-json")]
        [InlineData("[]")]
        [InlineData("{\"요금\":{\"기사지급예정액\":5678}}")]
        [InlineData("{\"Pricing\":null}")]
        [InlineData("{\"Pricing\":{\"distanceBasisCode\":\"StraightLine\",\"policyRevision\":\"r72\"}}")]
        [InlineData("{\"Pricing\":{\"rawDistanceMeters\":\"unknown\",\"distanceBasisCode\":\"StraightLine\",\"policyRevision\":\"r72\"}}")]
        public void 구형또는불완전JSON을영원요금근거로만들지않는다(string? json)
        {
            var queue = new 운송원장 { 기사제안요금계산근거Json = json, 기사지급예정액 = 5678m };
            Assert.Null(기사제안표시근거Service.요금근거(queue));
            Assert.Equal(5678m, queue.기사지급예정액);
        }

        [Fact]
        public void 필수값을확인한동일위치의실제영원거리비는미확인과구분한다()
        {
            var zero = Pricing() with { RawDistanceMeters = 0, DistanceFeeKrw = 0, MinimumPayoutKrw = 0, TotalPayoutKrw = 2700 };
            var queue = new 운송원장 { 기사지급예정액 = 2700, 기사제안요금정책판본 = zero.PolicyRevision,
                기사제안요금계산근거Json = JsonSerializer.Serialize(new { Pricing = zero }) };
            Assert.Equal(zero, 기사제안표시근거Service.요금근거(queue));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void 피킹담당표시는실제작업만담고포장작업을추정하지않는다(bool directPicking)
        {
            var activities = 기사제안표시근거Service.운송작업(directPicking);
            Assert.Equal(directPicking ? DriverOfferActivityCodes.Pick : DriverOfferActivityCodes.Pickup, activities[0]);
            Assert.Equal(DriverOfferActivityCodes.Deliver, activities[1]);
            Assert.DoesNotContain(DriverOfferActivityCodes.Pack, activities);
            Assert.DoesNotContain(DriverOfferActivityCodes.OutboundHandover, activities);
            Assert.Equal(2, activities.Count);
        }

        [Fact]
        public void 추가선택필드가없는구형공통제안도기존금액과거리를복원한다()
        {
            var oldJson = JsonNode.Parse(JsonSerializer.Serialize(Offer(null)))!.AsObject();
            oldJson.Remove("assignedActivities"); oldJson.Remove("pricing");
            var restored = JsonSerializer.Deserialize<DriverWorkOfferDto>(oldJson.ToJsonString())!;
            Assert.Equal("offer", restored.OfferId); Assert.Equal(3935m, restored.DriverPayout); Assert.Equal(6.5, restored.DistanceKm);
            Assert.Null(restored.Pricing); Assert.Empty(restored.AssignedActivities);
        }

        [Fact]
        public void 기존음식과화물응답에선택필드가없어도미확인근거를숫자로채우지않는다()
        {
            var food = JsonSerializer.Deserialize<FoodDeliveryDriverOfferDto>("{\"OfferId\":\"old-food\",\"DriverPayout\":5678,\"DistanceKm\":2.4}")!;
            Assert.Null(food.Pricing); Assert.Empty(food.AssignedActivities); Assert.Equal(5678m, food.DriverPayout);
            var cargo = JsonSerializer.Deserialize<기사배차추천항목응답>("{\"의뢰Id\":\"old-cargo\",\"운임\":5000}")!;
            Assert.Null(cargo.Pricing); Assert.Empty(cargo.AssignedActivities); Assert.Equal("old-cargo", cargo.의뢰Id);
        }

        private static DriverOfferPricingDto Pricing() => new()
        {
            DistanceBasisCode = "StraightLine", RawDistanceMeters = 1234.56789m, RatePerKmKrw = 1000,
            IncludedDistanceMeters = 0, RoundingCode = "ProportionalAwayFromZero", DistanceFeeKrw = 1235,
            BaseFeeKrw = 2000, MinimumPayoutKrw = 2500, WorkFeeKrw = 0, SurchargeKrw = 700,
            TotalPayoutKrw = 3935, PolicyRevision = "driver-straight-line-pricing.r1"
        };
        private static DriverWorkOfferDto Offer(DriverOfferPricingDto? pricing) => new("offer", "fdriver", "Food", "FoodDelivery",
            "메뉴", "픽업 후 전달", new("음식점", "합성 공개도로", 37.5, 127), new("전달지", "합성 전달도로", 37.51, 127.01),
            3935, 6.5, "적격 후보", ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(5)) { Pricing = pricing };
    }
}

namespace Ssalddel.Tests.Application.Food
{
    public sealed partial class 음식배달정상수직생명주기Tests
    {
        [Fact]
        public async Task DriverOfferEvidenceR72_소형요금정책미완성은추천대상알림요금저장을모두보류한다()
        {
            await using var database = await TestDatabase.CreateAsync();
            const string id = "small-goods-no-policy-r72";
            var queue = new 운송원장 { 의뢰Id = id, 운송번호 = id, 원본의뢰Id = id,
                배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
                배차큐단계 = 상태값.배차큐단계.계획배차, 배차노출상태 = 상태값.배차노출상태.계획대기,
                생활배송배차방식 = NeighborhoodDispatchModes.Automatic, 생활배송수락준비완료 = true,
                픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.51m, 하차_경도 = 127.01m };
            database.Context.운송원장.Add(queue);
            database.Context.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = id, 차량종류 = "오토바이", 운임구성Id = 1 });
            database.Context.운임구성.Add(new 운임구성 { Id = 1, 의뢰Id = id, 최종운임 = 10000, 기사지급예정운임 = 8888 });
            await database.Context.SaveChangesAsync();
            var before = JsonSerializer.Serialize(await database.Context.운송원장.AsNoTracking().SingleAsync());
            var notification = new R72Notifications();
            var smallGoods = new 소형배송기사제안요금Service(database.Context, Options.Create(new StraightLineDriverPricingOptions { Enabled = true }));
            var transition = new 배차대기원장전환Service(database.Context, Options.Create(new 배차큐정책Options()),
                new R72NoCandidateSelection(), notification, null!, new 음식배달배차흐름Resolver(), smallGoodsPricing: smallGoods,
                운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용,
                화물대상후보: new Ssalddel.Tests.Services.Dispatch.Common.용달대상후보시험Service(),
                신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);
            var result = await transition.추천시작Async(id, "driver");
            Assert.False(result.전환여부); Assert.Equal(배차대기원장전환결과코드.배차구성오류, result.결과코드);
            Assert.Equal(0, notification.Calls);
            await using var read = database.CreateContext();
            var stored = await read.운송원장.AsNoTracking().SingleAsync();
            Assert.Equal(before, JsonSerializer.Serialize(stored));
            Assert.Null(stored.현재추천대상기사Id); Assert.Null(stored.기사제안요금계산근거Json); Assert.Null(stored.기사지급예정액);
            Assert.Empty(await read.운영배차활동사건.AsNoTracking().ToArrayAsync());
            Assert.Equal(8888m, (await read.운임구성.AsNoTracking().SingleAsync()).기사지급예정운임);
        }

        private sealed class R72NoCandidateSelection : I배차추천후보선정Service
        {
            public Task<배차추천후보선정결과> 다음후보선정Async(string id, string? excluded = null, CancellationToken ct = default)
                => throw new InvalidOperationException("직접 지정의 화물 요금 보류는 새 후보 선정에 도달하지 않습니다.");
        }
        private sealed class R72Notifications : I배차추천알림Service
        {
            public int Calls { get; private set; }
            public Task 추천알림요청생성Async(long id, string request, string driver, int round, CancellationToken ct = default)
            { Calls++; return Task.CompletedTask; }
            public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
        }
    }
}
