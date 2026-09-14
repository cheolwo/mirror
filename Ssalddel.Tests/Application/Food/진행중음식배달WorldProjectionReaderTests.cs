using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed class 진행중음식배달WorldProjectionReaderTests
{
    private const string Area = 음식배달완료WorldAreaStableIds.Myeonmok;
    private const string PrivateOrderNumber = "FOOD-PRIVATE-20260914";
    private const string PrivateUserId = "private-orderer";
    private const string PrivateAddress = "서울특별시 중랑구 면목동 123-45 101동 202호";

    [Fact]
    public async Task 진행주문은_같은가명과판본으로_개인정보없이투영한다()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        db.음식주문.Add(Order(음식주문상태코드.픽업완료, now, historyCount: 5));
        await db.SaveChangesAsync();
        var reader = CreateReader(db);

        var first = Assert.Single(await reader.지역목록Async(Area, now, default));
        var second = Assert.Single(await reader.지역목록Async(Area, now.AddSeconds(10), default));

        Assert.Equal(first.SnapshotStableId, second.SnapshotStableId);
        Assert.Equal(first.WorkStableId, second.WorkStableId);
        Assert.Equal(5, first.Revision);
        Assert.Equal(OperationalWorldSceneItemKinds.ActiveLifecycle, first.ItemKind);
        Assert.Equal("DeliveryDriver", first.RoleCode);
        Assert.Equal("semantic-place:area:food-delivery-route", first.SemanticPlaceStableId);
        Assert.False(first.LocalStorageAllowed);
        Assert.False(first.ReplayAllowed);
        Assert.False(first.IsTombstone);

        var json = JsonSerializer.Serialize(first);
        Assert.DoesNotContain(PrivateOrderNumber, json, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateUserId, json, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateAddress, json, StringComparison.Ordinal);
        Assert.DoesNotContain("101동", json, StringComparison.Ordinal);
        Assert.Contains("projectionHashSha256", json, StringComparison.Ordinal);
        Assert.Contains("\"personalDataIncluded\":false", first.RepresentationDataJson, StringComparison.Ordinal);
        Assert.Contains("\"exactLocationIncluded\":false", first.RepresentationDataJson, StringComparison.Ordinal);
        Assert.Contains("\"distributionApproved\":false", first.RepresentationDataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 완료는_기존완료사본에맡기고_취소는짧은Tombstone으로낸다()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        db.음식주문.Add(Order(음식주문상태코드.수령확인, now, historyCount: 7));
        db.음식주문.Add(Order(음식주문상태코드.취소, now.AddSeconds(1), historyCount: 2, suffix: "CANCEL"));
        await db.SaveChangesAsync();

        var item = Assert.Single(await CreateReader(db).지역목록Async(Area, now.AddSeconds(2), default));

        Assert.True(item.IsTombstone);
        Assert.Equal(OperationalWorldAttentionStateCodes.RecoveryPending, item.AttentionStateCode);
        Assert.Equal(음식주문상태코드.취소, item.LifecycleStageCode);
        Assert.True(item.ExpiresAtUtc <= now.Add(진행중음식배달WorldProjectionPolicy.TombstoneRetention).AddSeconds(2));
    }

    [Fact]
    public async Task 다른지역과오래된자료는제외하고_가명키가없으면닫힌채실패한다()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var other = Order(음식주문상태코드.조리중, now, historyCount: 2);
        other.음식점주소 = "서울특별시 중랑구 중화동 1";
        other.수령지주소 = "서울특별시 중랑구 중화동 2";
        db.음식주문.Add(other);
        db.음식주문.Add(Order(
            음식주문상태코드.조리중,
            now - 진행중음식배달WorldProjectionPolicy.ActiveLookback - TimeSpan.FromMinutes(1),
            historyCount: 2,
            suffix: "OLD"));
        await db.SaveChangesAsync();

        Assert.Empty(await CreateReader(db).지역목록Async(Area, now, default));
        var noKey = new 진행중음식배달WorldProjectionReader(
            db,
            new 음식배달완료WorldAreaResolver(),
            new ConfigurationBuilder().Build());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            noKey.지역목록Async(Area, now, default));
        Assert.Equal("OperationalWorldProjectionPseudonymizationKeyUnavailable", exception.Message);
    }

    private static 진행중음식배달WorldProjectionReader CreateReader(SsalddelContext db)
        => new(
            db,
            new 음식배달완료WorldAreaResolver(),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [진행중음식배달WorldProjectionPolicy.PseudonymizationKeyConfigurationPath] =
                        "test-only-pseudonymization-key-at-least-32-bytes"
                })
                .Build());

    private static 음식주문 Order(
        string state,
        DateTime updatedAt,
        int historyCount,
        string suffix = "ACTIVE")
    {
        var order = new 음식주문
        {
            주문번호 = PrivateOrderNumber + "-" + suffix,
            음식점Id = 12,
            음식점명 = "비공개 음식점",
            음식점주소 = PrivateAddress,
            음식점상세주소 = "비밀번호 1234",
            주문자UserId = PrivateUserId,
            수령인명 = "홍길동",
            수령인연락처 = "010-0000-0000",
            수령지주소 = "서울특별시 중랑구 면목동 456-78",
            수령지상세주소 = "공동현관 호출",
            상태 = state,
            CreatedAt = updatedAt.AddMinutes(-10),
            UpdatedAt = updatedAt
        };
        for (var index = 0; index < historyCount; index++)
        {
            order.상태이력.Add(new 음식주문상태이력
            {
                이전상태 = index == 0 ? string.Empty : 음식주문상태코드.조리중,
                다음상태 = index == historyCount - 1 ? state : 음식주문상태코드.조리중,
                처리UserId = "private-actor-" + index,
                전이시각Utc = updatedAt.AddMinutes(-historyCount + index + 1)
            });
        }
        return order;
    }

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase("active-food-world-" + Guid.NewGuid().ToString("N"))
                .Options,
            new DummyEncryption());

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
