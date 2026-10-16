using DriverApp.Models.Driver.Samples;

namespace Ssalddel.Tests.Clients.Cargo;

public sealed class DriverLocationDirectionR68Tests
{
    private static readonly DateTime Now = new(2026, 10, 16, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MissingHeading_IsNotAnAssumedNorthDirection()
    {
        var state = new 기사위치방향State();
        Assert.Null(state.Bearing(Now));
        Assert.True(state.Observe(0, 0, Now));
        Assert.Equal(0d, state.Bearing(Now));
        state.Clear();
        Assert.Null(state.Bearing(Now));
    }

    [Fact]
    public void HeadingExpires_WithoutAnotherSensorEvent()
    {
        var state = new 기사위치방향State();
        state.Observe(90, 0, Now);
        Assert.NotNull(state.Bearing(Now + 기사위치방향State.MaximumHeadingAge));
        Assert.Null(state.Bearing(Now + 기사위치방향State.MaximumHeadingAge + TimeSpan.FromMilliseconds(1)));
        Assert.Null(state.Bearing(Now - TimeSpan.FromMilliseconds(1)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteReadings_CannotBecomeDirection(double value)
    {
        var state = new 기사위치방향State();
        Assert.Null(기사위치방향State.NormalizeDegrees(value));
        Assert.False(state.Observe(value, 0, Now));
        Assert.Null(state.Bearing(Now));
        Assert.False(state.Observe(30, value, Now));
        Assert.Null(state.Bearing(Now));
    }

    [Theory]
    [InlineData(350, 20, 10)]
    [InlineData(5, -10, 355)]
    [InlineData(0, 0, 0)]
    public void MagneticHeadingAndDeclination_AreCombinedAcrossNorth(double heading, double declination, double expected)
    {
        var state = new 기사위치방향State();
        Assert.True(state.Observe(heading, declination, Now));
        Assert.Equal(expected, state.Bearing(Now)!.Value, 6);
    }

    [Fact]
    public void CircularSmoothing_DoesNotTurnNorthIntoSouth()
    {
        var state = new 기사위치방향State();
        state.Observe(359, 0, Now);
        state.Observe(1, 0, Now.AddMilliseconds(100));
        var bearing = state.Bearing(Now.AddMilliseconds(100))!.Value;
        Assert.True(bearing < 5 || bearing > 355);
    }

    [Fact]
    public void DelayedReading_CannotReplaceANewerObservation()
    {
        var state = new 기사위치방향State();
        state.Observe(30, 0, Now);
        Assert.False(state.Observe(180, 0, Now.AddSeconds(-1)));
        Assert.Equal(30d, state.Bearing(Now)!.Value, 6);
    }
}
