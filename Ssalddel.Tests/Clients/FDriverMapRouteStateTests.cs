using FDriverApp.PageModels;
using Ssalddel.Contracts.Driver.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverMapRouteStateTests
{
    [Fact]
    public async Task CurrentLegUsesGps_ThenReferenceUsesPickupToDropoff_AndPreservesProvenance()
    {
        var clock = new TestClock();
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        FoodDeliveryDriverRouteResponseDto? firstResponse = null;
        var state = new FDriverMapRouteState((request, _) =>
        {
            requests.Add(request);
            var response = Road(request);
            firstResponse ??= response;
            return Task.FromResult(response);
        }, clock);
        var first = Intent("first");
        var other = Intent("other", pickupLatitude: 37.52m, dropoffLatitude: 37.53m);
        state.UpdateContext("driver", 1, [first, other], first.OfferId);
        state.ObserveLocation(Location(clock));

        await state.RefreshAsync();

        Assert.Equal(2, requests.Count);
        Assert.Equal(37.5m, requests[0].StartLatitude);
        Assert.Equal(first.TargetLatitude, Assert.Single(requests[0].Stops).Latitude);
        Assert.Equal(other.PickupLatitude, requests[1].StartLatitude);
        Assert.Equal(other.DropoffLatitude, Assert.Single(requests[1].Stops).Latitude);
        var current = Assert.IsType<FDriverStoredMapRoute>(state.Snapshot.CurrentRoute);
        Assert.Equal("NaverDirections5", current.Source);
        Assert.Equal(clock.GetUtcNow(), current.RequestedAtUtc);
        Assert.Equal(clock.GetUtcNow(), current.ReceivedAtUtc);
        Assert.False(current.IsEstimated);
        Assert.False(current.IsReference);
        Assert.True(Assert.Single(state.Snapshot.ReferenceRoutes).IsReference);
        firstResponse!.Points[0].Latitude = 38m;
        Assert.Equal(37.5m, current.Points[0].Latitude);
    }

    [Fact]
    public async Task MovementNeedsThirtySecondsAndOneHundredMeters()
    {
        var clock = new TestClock();
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = RecordingState(clock, requests);
        state.UpdateContext("driver", 1, [Intent("first")], "first");
        state.ObserveLocation(Location(clock));
        await state.TickAsync();

        clock.Advance(29);
        state.ObserveLocation(Location(clock, latitude: 37.5015m));
        await state.TickAsync();
        Assert.Single(requests);
        clock.Advance(1);
        await state.TickAsync();
        Assert.Equal(2, requests.Count);
        Assert.Equal(37.5015m, requests[1].StartLatitude);

        clock.Advance(30);
        state.ObserveLocation(Location(clock, latitude: 37.5016m));
        await state.TickAsync();
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task DeviationNeedsTwoDistinctAccurateFixes_AndThirtySecondGate()
    {
        var clock = new TestClock();
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = RecordingState(clock, requests);
        state.UpdateContext("driver", 1, [HorizontalIntent()], "first");
        state.ObserveLocation(Location(clock));
        await state.TickAsync();
        clock.Advance(20);
        var firstOffRoute = Location(clock, latitude: 37.5006m);
        state.ObserveLocation(firstOffRoute);
        state.ObserveLocation(firstOffRoute);
        await state.TickAsync();
        Assert.Single(requests);
        clock.Advance(1);
        state.ObserveLocation(Location(clock, latitude: 37.5006m));
        await state.TickAsync();
        Assert.Single(requests);
        clock.Advance(9);
        await state.TickAsync();
        Assert.Equal(2, requests.Count);
    }

    [Theory]
    [InlineData("inaccurate")]
    [InlineData("negative_accuracy")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("zero_coordinate")]
    [InlineData("invalid_coordinate")]
    [InlineData("unknown_accuracy")]
    public async Task UnusableFixDoesNotSupplySecondDeviationSample(string kind)
    {
        var clock = new TestClock();
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = RecordingState(clock, requests);
        state.UpdateContext("driver", 1, [HorizontalIntent()], "first");
        state.ObserveLocation(Location(clock));
        await state.TickAsync();
        clock.Advance(30);
        state.ObserveLocation(Location(clock, latitude: 37.5006m));
        clock.Advance(1);
        var invalid = kind switch
        {
            "inaccurate" => Location(clock, latitude: 37.5006m, accuracy: 51),
            "negative_accuracy" => Location(clock, latitude: 37.5006m, accuracy: -1),
            "stale" => Location(clock, latitude: 37.5006m) with { RecordedAtUtc = clock.GetUtcNow().AddSeconds(-31) },
            "future" => Location(clock, latitude: 37.5006m) with { RecordedAtUtc = clock.GetUtcNow().AddSeconds(6) },
            "zero_coordinate" => Location(clock, latitude: 0),
            "invalid_coordinate" => Location(clock, latitude: 91),
            _ => Location(clock, latitude: 37.5006m, accuracy: null)
        };
        state.ObserveLocation(invalid);
        await state.TickAsync();
        Assert.Single(requests);
    }

    [Fact]
    public void DeviationUsesSegmentsRatherThanDistanceToVertices()
    {
        FoodDeliveryDriverRoutePointDto[] points =
        [new() { Latitude = 37.5m, Longitude = 127m }, new() { Latitude = 37.5m, Longitude = 127.02m }];
        Assert.InRange(FDriverMapRouteState.DistanceFromPolylineMeters(37.5m, 127.01m, points), 0, .01);
        Assert.InRange(FDriverMapRouteState.DistanceFromPolylineMeters(37.5006m, 127.01m, points), 66, 68);
        Assert.True(FDriverMapRouteState.DistanceMeters(37.5m, 127m, 37.5m, 127.01m) > 800);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrEstimatedHttpSuccessRetriesAfterSixtySeconds(bool estimated)
    {
        var clock = new TestClock();
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = new FDriverMapRouteState((request, _) =>
        {
            requests.Add(request);
            if (requests.Count == 1)
            {
                if (!estimated)
                    throw new HttpRequestException("offline");
                var response = Road(request);
                response.Source = "CoordinateEstimate";
                response.IsEstimated = true;
                return Task.FromResult(response);
            }
            return Task.FromResult(Road(request));
        }, clock);
        state.UpdateContext("driver", 1, [HorizontalIntent()], "first");
        state.ObserveLocation(Location(clock));
        await state.TickAsync();
        Assert.Equal(clock.GetUtcNow().AddSeconds(60), state.Snapshot.RetryAtUtc);
        Assert.NotNull(state.Snapshot.LastError);
        if (estimated)
        {
            Assert.True(state.Snapshot.CurrentRoute!.IsEstimated);
            Assert.Equal("CoordinateEstimate", state.Snapshot.CurrentRoute.Source);
        }
        clock.Advance(30);
        state.ObserveLocation(Location(clock, latitude: 37.502m));
        clock.Advance(1);
        state.ObserveLocation(Location(clock, latitude: 37.502m));
        await state.TickAsync();
        Assert.Single(requests);
        clock.Advance(28);
        state.ObserveLocation(Location(clock, latitude: 37.502m));
        await state.TickAsync();
        Assert.Single(requests);
        clock.Advance(1);
        await state.TickAsync();
        Assert.Equal(2, requests.Count);
        Assert.Null(state.Snapshot.RetryAtUtc);
        Assert.Null(state.Snapshot.LastError);
    }

    [Fact]
    public async Task GpsDuringQueryDoesNotChangeSemanticRevisionOrDiscardResult()
    {
        var clock = new TestClock();
        var pending = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        FoodDeliveryDriverRouteRequestDto? captured = null;
        var state = new FDriverMapRouteState((request, _) =>
        {
            captured = request;
            return pending.Task;
        }, clock);
        state.UpdateContext("driver", 1, [Intent("first")], "first");
        state.ObserveLocation(Location(clock));
        var task = state.TickAsync();
        var revision = state.Snapshot.Revision;
        clock.Advance(1);
        state.ObserveLocation(Location(clock, latitude: 37.502m));
        Assert.Equal(revision, state.Snapshot.Revision);
        Assert.Same(task, state.TickAsync());
        pending.SetResult(Road(captured!));
        await task;
        Assert.Equal(37.5m, state.Snapshot.CurrentRoute!.OriginLatitude);
        Assert.Equal(revision, state.Snapshot.Revision);
    }

    [Fact]
    public async Task NewFocusPreemptsReferenceWithoutParallelQueries_AndCachesValidLateReference()
    {
        var clock = new TestClock();
        var first = Intent("first");
        var second = Intent("second", pickupLatitude: 37.52m, dropoffLatitude: 37.53m);
        var referencePending = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var concurrent = 0;
        var maximumConcurrent = 0;
        var referenceCancelled = false;
        var state = new FDriverMapRouteState(async (request, token) =>
        {
            var active = Interlocked.Increment(ref concurrent);
            maximumConcurrent = Math.Max(maximumConcurrent, active);
            requests.Add(request);
            try
            {
                if (request.StartLatitude == second.PickupLatitude)
                {
                    using var registration = token.Register(() => referenceCancelled = true);
                    return await referencePending.Task;
                }
                return Road(request);
            }
            finally
            {
                Interlocked.Decrement(ref concurrent);
            }
        }, clock);
        state.UpdateContext("driver", 1, [first, second], first.OfferId);
        state.ObserveLocation(Location(clock));
        var task = state.TickAsync();
        Assert.Equal(2, requests.Count);
        state.UpdateContext("driver", 1, [first, second], second.OfferId);
        var samePump = state.TickAsync();
        Assert.Same(task, samePump);
        Assert.True(referenceCancelled);
        referencePending.SetResult(Road(requests[1]));
        await task;
        Assert.Equal(1, maximumConcurrent);
        Assert.Equal(second.OfferId, state.Snapshot.CurrentRoute!.OfferId);
        Assert.Equal(second.TargetLatitude, requests[2].Stops[0].Latitude);
        state.UpdateContext("driver", 1, [first, second], first.OfferId);
        await state.TickAsync();
        var reference = Assert.Single(state.Snapshot.ReferenceRoutes);
        Assert.Equal(second.OfferId, reference.OfferId);
        Assert.Equal(second.PickupLatitude, reference.OriginLatitude);
        Assert.Single(requests.Where(request => request.StartLatitude == second.PickupLatitude));
    }

    [Fact]
    public async Task OldFocusedResponseCannotReplaceNewPhase_WhilePhaseQueriesImmediately()
    {
        var clock = new TestClock();
        var first = Intent("first");
        var next = first with { Phase = FoodMapRoutePhase.Dropoff, TargetLatitude = first.DropoffLatitude, TargetLongitude = first.DropoffLongitude };
        var pending = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = new FDriverMapRouteState((request, _) =>
        {
            requests.Add(request);
            return requests.Count == 1 ? pending.Task : Task.FromResult(Road(request));
        }, clock);
        state.UpdateContext("driver", 1, [first], first.OfferId);
        state.ObserveLocation(Location(clock));
        var task = state.TickAsync();
        var revision = state.Snapshot.Revision;
        clock.Advance(1);
        state.UpdateContext("driver", 1, [next], next.OfferId);
        Assert.True(state.Snapshot.Revision > revision);
        pending.SetResult(Road(requests[0]));
        await task;
        Assert.Equal(2, requests.Count);
        Assert.Equal(next.TargetLatitude, requests[1].Stops[0].Latitude);
        Assert.Equal(FoodMapRoutePhase.Dropoff, state.Snapshot.CurrentRoute!.Phase);
    }

    [Fact]
    public async Task ReferenceCacheIgnoresGpsAndPhaseButInvalidatesCoordinatesAndCompletion()
    {
        var clock = new TestClock();
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = RecordingState(clock, requests);
        var first = Intent("first");
        var other = Intent("other", pickupLatitude: 37.52m, dropoffLatitude: 37.53m);
        state.UpdateContext("driver", 1, [first, other], first.OfferId);
        state.ObserveLocation(Location(clock));
        await state.TickAsync();
        var revision = state.Snapshot.Revision;
        clock.Advance(1);
        state.ObserveLocation(Location(clock, latitude: 37.50001m));
        other = other with { Phase = FoodMapRoutePhase.Dropoff, TargetLatitude = other.DropoffLatitude, TargetLongitude = other.DropoffLongitude };
        state.UpdateContext("driver", 1, [first with { TargetLabel = "표시명 변경" }, other], first.OfferId);
        await state.TickAsync();
        Assert.Equal(2, requests.Count);
        Assert.Equal(revision, state.Snapshot.Revision);
        other = other with { DropoffLatitude = 37.54m, TargetLatitude = 37.54m };
        state.UpdateContext("driver", 1, [first, other], first.OfferId);
        Assert.Empty(state.Snapshot.ReferenceRoutes);
        await state.TickAsync();
        Assert.Equal(3, requests.Count);
        Assert.Equal(37.54m, requests[2].Stops[0].Latitude);
        state.UpdateContext("driver", 1, [first], first.OfferId);
        Assert.Empty(state.Snapshot.ReferenceRoutes);
        await state.TickAsync();
        Assert.Equal(3, requests.Count);
    }

    [Fact]
    public async Task LateReferenceWithOldCoordinatesCannotPopulateChangedOrderCache()
    {
        var clock = new TestClock();
        var pending = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = new FDriverMapRouteState((request, _) =>
        {
            requests.Add(request);
            return requests.Count == 2 ? pending.Task : Task.FromResult(Road(request));
        }, clock);
        var first = Intent("first");
        var other = Intent("other", 37.52m, 37.53m);
        state.UpdateContext("driver", 1, [first, other], first.OfferId);
        state.ObserveLocation(Location(clock));
        var task = state.TickAsync();
        other = other with { DropoffLatitude = 37.54m };
        state.UpdateContext("driver", 1, [first, other], first.OfferId);
        pending.SetResult(Road(requests[1]));
        await task;
        Assert.Equal(3, requests.Count);
        var stored = Assert.Single(state.Snapshot.ReferenceRoutes);
        Assert.Equal(37.54m, stored.Points[^1].Latitude);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearOrAccountChangeRejectsLateResponse(bool changeAccount)
    {
        var clock = new TestClock();
        var pending = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var cancelled = false;
        var state = new FDriverMapRouteState((request, token) =>
        {
            requests.Add(request);
            if (requests.Count == 1)
            {
                token.Register(() => cancelled = true);
                return pending.Task;
            }
            return Task.FromResult(Road(request));
        }, clock);
        state.UpdateContext("driver", 1, [Intent("first")], "first");
        state.ObserveLocation(Location(clock));
        var task = state.TickAsync();
        if (changeAccount)
            state.UpdateContext("next-driver", 2, [Intent("first")], "first");
        else
            state.Clear();
        Assert.True(cancelled);
        Assert.Null(state.Snapshot.CurrentRoute);
        Assert.False(state.Snapshot.IsLoading);
        pending.SetResult(Road(requests[0]));
        await task;
        Assert.Single(requests);
        Assert.Null(state.Snapshot.CurrentRoute);
        Assert.Empty(state.Snapshot.ReferenceRoutes);
        if (changeAccount)
        {
            state.ObserveLocation(Location(clock));
            await state.TickAsync();
            Assert.Equal(2, requests.Count);
            Assert.NotNull(state.Snapshot.CurrentRoute);
        }
    }

    [Fact]
    public async Task StaleLastFixCannotStartScheduledQuery()
    {
        var clock = new TestClock();
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var state = RecordingState(clock, requests);
        state.UpdateContext("driver", 1, [Intent("first")], "first");
        state.ObserveLocation(Location(clock));
        await state.TickAsync();
        clock.Advance(1);
        state.ObserveLocation(Location(clock, latitude: 37.502m));
        clock.Advance(31);
        await state.TickAsync();
        Assert.Single(requests);
        state.ObserveLocation(Location(clock, latitude: 37.502m));
        await state.TickAsync();
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task LocationLossCancelsCurrentButKeepsReferences_AndNewFixQueriesImmediately()
    {
        var clock = new TestClock();
        var pending = new TaskCompletionSource<FoodDeliveryDriverRouteResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<FoodDeliveryDriverRouteRequestDto>();
        var cancelled = false;
        var state = new FDriverMapRouteState((request, token) =>
        {
            requests.Add(request);
            if (requests.Count == 3)
            {
                token.Register(() => cancelled = true);
                return pending.Task;
            }
            return Task.FromResult(Road(request));
        }, clock);
        state.UpdateContext("driver", 1, [Intent("first"), Intent("other", 37.52m, 37.53m)], "first");
        var oldFix = Location(clock);
        state.ObserveLocation(oldFix);
        await state.TickAsync();
        var reference = Assert.Single(state.Snapshot.ReferenceRoutes);
        var revision = state.Snapshot.Revision;
        var task = state.RefreshAsync();
        state.ForgetLocation();
        Assert.True(cancelled);
        Assert.Null(state.Snapshot.CurrentRoute);
        Assert.False(state.Snapshot.IsLoading);
        Assert.Equal(revision, state.Snapshot.Revision);
        pending.SetResult(Road(requests[2]));
        await task;
        Assert.Null(state.Snapshot.CurrentRoute);
        Assert.Same(reference, Assert.Single(state.Snapshot.ReferenceRoutes));
        state.ObserveLocation(oldFix);
        await state.TickAsync();
        Assert.Equal(3, requests.Count);
        clock.Advance(1);
        state.ObserveLocation(Location(clock));
        await state.TickAsync();
        Assert.Equal(4, requests.Count);
        Assert.NotNull(state.Snapshot.CurrentRoute);
        Assert.Same(reference, Assert.Single(state.Snapshot.ReferenceRoutes));
    }

    [Fact]
    public async Task IncompleteRoadShapeIsEstimatedAndBackedOff()
    {
        var clock = new TestClock();
        var state = new FDriverMapRouteState((_, _) => Task.FromResult(new FoodDeliveryDriverRouteResponseDto
        {
            Source = "NaverDirections5",
            Points = [new() { Latitude = 37.5m, Longitude = 127m }]
        }), clock);
        state.UpdateContext("driver", 1, [Intent("first")], "first");
        state.ObserveLocation(Location(clock));
        await state.TickAsync();
        Assert.True(state.Snapshot.CurrentRoute!.IsEstimated);
        Assert.Equal(clock.GetUtcNow().AddSeconds(60), state.Snapshot.RetryAtUtc);
    }

    private static FDriverMapRouteState RecordingState(TestClock clock, List<FoodDeliveryDriverRouteRequestDto> requests) =>
        new((request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(Road(request));
        }, clock);

    private static FDriverMapRouteIntent Intent(string id, decimal pickupLatitude = 37.501m, decimal dropoffLatitude = 37.51m) =>
        new(id, FoodMapRoutePhase.Pickup, pickupLatitude, 127.002m, dropoffLatitude, 127.01m,
            pickupLatitude, 127.002m, "픽업지");

    private static FDriverMapRouteIntent HorizontalIntent() =>
        Intent("first") with { TargetLatitude = 37.5m, TargetLongitude = 127.02m };

    private static FDriverRouteLocation Location(TestClock clock, decimal latitude = 37.5m, decimal? accuracy = 5) =>
        new(latitude, 127m, accuracy, clock.GetUtcNow());

    private static FoodDeliveryDriverRouteResponseDto Road(FoodDeliveryDriverRouteRequestDto request) => new()
    {
        Source = "NaverDirections5",
        DistanceKm = 1.5m,
        DurationMinutes = 4,
        Points =
        [
            new() { Latitude = request.StartLatitude, Longitude = request.StartLongitude },
            new() { Latitude = request.Stops[0].Latitude, Longitude = request.Stops[0].Longitude }
        ]
    };

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }
}
