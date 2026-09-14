using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "음식배달 상태 사본과 Unity 표시 단계의 계약 호환을 검증한다.",
    Boundary = "표현 모델 자동 시험이며 실제 Scene·Game View 증거가 아니다.")]
public sealed class 음식배달수명주기표현Tests
{
    [Fact]
    public void Simulation사본과Unity표현은_같은상태코드를소비한다()
    {
        var source = new Simulation음식배달Snapshot
        {
            FoodOrderStableId = "sim-food-1", Revision = 4, StateCode = "픽업완료",
            RestaurantFacilityStableId = "restaurant-1", OrdererStableId = "orderer-1"
        };
        var snapshot = Simulation음식배달수명주기Adapter.ToLifecycleSnapshot(source, "simulation.r1");
        var presentation = new 음식배달수명주기표현(snapshot);

        Assert.Equal(음식배달상태원천코드.SimulationCore, snapshot.SourceCode);
        Assert.Equal("restaurant-1", presentation.RestaurantStableId);
        Assert.Equal(음식배달상태원천코드.SimulationCore, presentation.SourceCode);
        Assert.Equal("simulation.r1", presentation.SourceRevision);
        Assert.Equal(4, presentation.StepIndex);
        Assert.Contains("전달지", presentation.Label);
    }

    [Fact]
    public void 사가정선택목록은_세가상음식점만보이고_실제사업장연결을표시하지않는다()
    {
        var presenter = new 사가정가상음식점선택Presenter();

        var restaurants = presenter.목록();

        Assert.Equal(new[]
        {
            "가상 사가정 큰길식당",
            "가상 면목 생활길분식",
            "가상 골목안 도시락",
        }, restaurants.Select(value => value.DisplayName));
        Assert.Equal(3, restaurants.Select(value => value.RestaurantStableId).Distinct().Count());
        Assert.Equal(3, restaurants.Select(value => value.DisplayRouteStableId).Distinct().Count());
        Assert.All(restaurants, value =>
        {
            Assert.True(value.SyntheticFixture);
            Assert.False(value.PublicBusinessLinked);
            Assert.False(value.DistributionApproved);
            Assert.False(value.RouteApplied);
            Assert.False(value.TraversalReady);
        });
    }

    [Fact]
    public void 사가정주문표현은_등록된가상음식점과수명주기를결속하고_미등록음식점을거절한다()
    {
        var presenter = new 사가정가상음식점선택Presenter();
        var restaurant = 사가정가상음식점Catalog.목록()[1];
        var source = new 음식배달수명주기Snapshot
        {
            SourceCode = 음식배달상태원천코드.SimulationCore,
            SourceRevision = 사가정가상음식점Policy.Revision,
            OrderStableId = "food-order:synthetic:sagajeong:test",
            OrderRevision = 5,
            OrderStateCode = "픽업완료",
            RestaurantStableId = restaurant.FacilityStableId,
            OrdererStableId = "participant:synthetic:a",
            SourceRefs = new[] { restaurant.ProfileStableId, restaurant.DisplayRouteStableId },
        };

        var binding = presenter.주문표현(source);

        Assert.Equal(restaurant.DisplayName, binding.Restaurant.DisplayName);
        Assert.Equal(restaurant.FacilityStableId, binding.Lifecycle.RestaurantStableId);
        Assert.Equal(4, binding.Lifecycle.StepIndex);
        source.RestaurantStableId = "facility:unregistered";
        var error = Assert.Throws<ArgumentException>(() => presenter.주문표현(source));
        Assert.Equal("source", error.ParamName);
        Assert.StartsWith("SagajeongSyntheticRestaurantBindingMissing", error.Message);

        source.RestaurantStableId = restaurant.FacilityStableId;
        source.SourceCode = 음식배달상태원천코드.OperationalServer;
        error = Assert.Throws<ArgumentException>(() => presenter.주문표현(source));
        Assert.StartsWith("SagajeongSyntheticRestaurantSourceBoundaryInvalid", error.Message);

        source.SourceCode = 음식배달상태원천코드.SimulationCore;
        source.SourceRevision = "synthetic-sagajeong-food-delivery.other";
        error = Assert.Throws<ArgumentException>(() => presenter.주문표현(source));
        Assert.StartsWith("SagajeongSyntheticRestaurantSourceBoundaryInvalid", error.Message);

        source.SourceRevision = 사가정가상음식점Policy.Revision;
        source.SourceRefs = Array.Empty<string>();
        error = Assert.Throws<ArgumentException>(() => presenter.주문표현(source));
        Assert.StartsWith("SagajeongSyntheticRestaurantProvenanceMissing", error.Message);

        source.SourceRefs = new[]
        {
            restaurant.ProfileStableId,
            restaurant.DisplayRouteStableId,
            "claim:restaurant:not-approved",
        };
        error = Assert.Throws<ArgumentException>(() => presenter.주문표현(source));
        Assert.StartsWith("SagajeongSyntheticRestaurantProvenanceMissing", error.Message);

        var other = 사가정가상음식점Catalog.목록()[0];
        source.SourceRefs = new[]
        {
            restaurant.ProfileStableId,
            restaurant.DisplayRouteStableId,
            other.ProfileStableId,
            other.DisplayRouteStableId,
        };
        error = Assert.Throws<ArgumentException>(() => presenter.주문표현(source));
        Assert.StartsWith("SagajeongSyntheticRestaurantProvenanceMissing", error.Message);
    }
}
