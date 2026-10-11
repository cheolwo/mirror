using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class 운영배차공통계약Tests
{
    [Fact]
    public void 기사수신의사와_서버실효상태는_직렬화후에도분리된다()
    {
        var source = new 운영배차수신상태Dto
        {
            주체Id = "driver-1",
            수신의사Code = 운영배차수신의사Code.On,
            실효상태Code = 운영배차실효상태Code.연결확인불가,
            실효사유Code = "HeartbeatExpired",
            수신의사변경시각Utc = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
            실효상태변경시각Utc = new DateTimeOffset(2026, 9, 10, 0, 5, 0, TimeSpan.Zero),
            서버관측시각Utc = new DateTimeOffset(2026, 9, 10, 0, 6, 0, TimeSpan.Zero)
        };

        var restored = JsonSerializer.Deserialize<운영배차수신상태Dto>(
            JsonSerializer.Serialize(source));

        Assert.NotNull(restored);
        Assert.Equal(운영배차수신의사Code.On, restored.수신의사Code);
        Assert.Equal(운영배차실효상태Code.연결확인불가, restored.실효상태Code);
        Assert.NotEqual(restored.수신의사변경시각Utc, restored.실효상태변경시각Utc);
    }

    [Fact]
    public void 영속원장과_판정투영은_서로다른Port다()
    {
        Assert.False(typeof(I운영배차활동원장Store).IsAssignableFrom(typeof(I운영배차판정ProjectionStore)));
        Assert.False(typeof(I운영배차판정ProjectionStore).IsAssignableFrom(typeof(I운영배차활동원장Store)));

        Assert.Contains(
            typeof(I운영배차활동원장Store).GetMethods(),
            method => method.Name == nameof(I운영배차활동원장Store.사건추가Async));
        Assert.DoesNotContain(
            typeof(I운영배차판정ProjectionStore).GetMethods(),
            method => method.Name == nameof(I운영배차활동원장Store.사건추가Async));
    }

    [Fact]
    public void 코드탐색메타데이터는_계약계산원장투영순서를_드러낸다()
    {
        var metadata = SsalddelCodeMetadataReader.ReadFeature(
            SsalddelCodeFeatureKeys.OperationalDispatchCore,
            typeof(운영배차활동사건Dto).Assembly,
            typeof(운영배차단기지표Calculator).Assembly,
            typeof(I운영배차활동원장Store).Assembly);

        Assert.Equal(
            [
                "contract.operational-dispatch-availability",
                "contract.operational-dispatch-activity",
                "domain.operational-dispatch-availability-policy",
                "domain.operational-dispatch-short-window",
                "contract.food-demand-participation",
                "contract.food-dispatch-timing",
                "application.operational-dispatch-ledger-port",
                "domain.food-demand-participation-policy",
                "domain.food-dispatch-timing-policy",
                "application.food-demand-participation",
                "application.food-dispatch-timing",
                "application.food-regional-dispatch-review",
                "infrastructure.food-dispatch-ollama-selection",
                "infrastructure.food-dispatch-timing-ollama",
                "infrastructure.food-regional-dispatch-ollama",
                "application.operational-dispatch-decision-projection-port",
                "application.operational-dispatch-usecase"
            ],
            metadata.Select(x => x.StepKey).ToArray());
        Assert.All(metadata, item => Assert.False(string.IsNullOrWhiteSpace(item.Boundary)));
    }
}
