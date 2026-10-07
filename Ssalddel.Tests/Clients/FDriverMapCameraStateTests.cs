using FDriverApp.Controls;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverMapCameraStateTests
{
    [Fact]
    public void FirstValidRouteFramesAllCurrentPointsPinsAndGps_WithNoCameraMutation()
    {
        var state = new FDriverMapCameraState();
        var frame = state.ResolveRouteFrame("first:#F57C00",
            [(37.51, 127.01), (37.52, 127.02), (37.50, 127.00), (37.53, 127.03)]);

        Assert.Equal(new FDriverMapRouteFrame(37.50, 127.00, 37.53, 127.03), frame);
        Assert.Null(state.CurrentCamera); // Native fitting supplies its actual center and zoom afterward.
        Assert.Null(state.ResolveRouteFrame("first:#F57C00", [(37.48, 126.98), (37.55, 127.05)]));
    }

    [Fact]
    public void SameRouteGeometryOrGpsChangeDoesNotReframeAfterUserPan()
    {
        var state = new FDriverMapCameraState();
        Assert.NotNull(state.ResolveRouteFrame("first:#F57C00", [(37.50, 127.00), (37.53, 127.03)]));
        state.ObserveNativeCamera(37.56, 127.06, 16);

        Assert.Null(state.ResolveRouteFrame("first:#F57C00", [(37.40, 126.90), (37.60, 127.10)]));
        Assert.Null(state.ResolveUpdate(37.5, 127, true, 37.505, 127.005, false, 16, 0));
        Assert.Equal(new FDriverMapCameraUpdate(37.56, 127.06, 16, false), state.CurrentCamera);
    }

    [Fact]
    public void PhaseAndSelectionTransitionsAllowOneNewFrame_EachContinuousKeyFramesOnce()
    {
        var state = new FDriverMapCameraState();
        (double Latitude, double Longitude)[] points = [(37.50, 127.00), (37.53, 127.03)];

        Assert.NotNull(state.ResolveRouteFrame("first:#F57C00", points));
        Assert.NotNull(state.ResolveRouteFrame("first:#2563EB", points));
        Assert.Null(state.ResolveRouteFrame("first:#2563EB", points));
        Assert.NotNull(state.ResolveRouteFrame("second:#F57C00", points));
        Assert.Null(state.ResolveRouteFrame("second:#F57C00", points));
        Assert.NotNull(state.ResolveRouteFrame("first:#2563EB", points));
    }

    [Fact]
    public void InvalidOrSingleDistinctPointDoesNotConsumeRouteFrameKey()
    {
        var state = new FDriverMapCameraState();
        Assert.Null(state.ResolveRouteFrame("first:#F57C00", []));
        Assert.Null(state.ResolveRouteFrame("first:#F57C00", [(37.5, 127)]));
        Assert.Null(state.ResolveRouteFrame("first:#F57C00", [(37.5, 127), (37.5, 127)]));
        Assert.Null(state.ResolveRouteFrame("first:#F57C00", [(double.NaN, 127), (37.5, 181), (0, 0)]));
        Assert.Null(state.ResolveRouteFrame("first:#F57C00", [(37.5, 127), (double.PositiveInfinity, 127)]));
        Assert.NotNull(state.ResolveRouteFrame("first:#F57C00", [(37.5, 127), (37.51, 127.01)]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingFrameIdentityNeverConsumesValidRoute(string? frameKey)
    {
        var state = new FDriverMapCameraState();
        (double Latitude, double Longitude)[] points = [(37.50, 127.00), (37.53, 127.03)];
        Assert.Null(state.ResolveRouteFrame(frameKey, points));
        Assert.NotNull(state.ResolveRouteFrame("selected:#F57C00", points));
    }

    [Fact]
    public void InvalidOptionalPointCannotExpandBoundsOutsideValidRoute()
    {
        var state = new FDriverMapCameraState();
        var frame = state.ResolveRouteFrame("selected:#F57C00",
            [(37.5, 127), (37.51, 127.01), (91, 127), (37.52, 181), (double.NaN, 127), (0, 0)]);

        Assert.Equal(new FDriverMapRouteFrame(37.5, 127, 37.51, 127.01), frame);
    }

    [Fact]
    public void HorizontalOrVerticalRouteStillHasAUsableFrame()
    {
        var state = new FDriverMapCameraState();
        Assert.Equal(new FDriverMapRouteFrame(37.5, 127, 37.5, 127.01),
            state.ResolveRouteFrame("horizontal:#F57C00", [(37.5, 127), (37.5, 127.01)]));
        Assert.Equal(new FDriverMapRouteFrame(37.5, 127, 37.51, 127),
            state.ResolveRouteFrame("vertical:#F57C00", [(37.5, 127), (37.51, 127)]));
    }

    [Fact]
    public void ViewOwnedStateRetainsNativeSnapshotAndFrameIdentityAcrossHandlerAttachment()
    {
        var viewState = new FDriverMapCameraState();
        Assert.NotNull(viewState.ResolveRouteFrame("selected:#F57C00", [(37.5, 127), (37.53, 127.03)]));
        viewState.ObserveNativeCamera(37.515, 127.015, 12.5);

        // A newly attached native handler reuses the view's owner, not a fresh state.
        var nextHandlerState = viewState;
        Assert.Equal(new FDriverMapCameraUpdate(37.515, 127.015, 12.5, false), nextHandlerState.CurrentCamera);
        Assert.Null(nextHandlerState.ResolveRouteFrame("selected:#F57C00", [(37.5, 127), (37.53, 127.03)]));
        Assert.Null(nextHandlerState.ResolveUpdate(37.5665, 126.978, true, 37.50, 127, false, 12.5, 0));
        var recentered = nextHandlerState.ResolveUpdate(37.5665, 126.978, true, 37.50, 127, false, 12.5, 1);
        Assert.Equal(new FDriverMapCameraUpdate(37.50, 127, 12.5, true), recentered);
    }

    [Fact]
    public void InvalidNativeCameraCannotReplaceRestorableSnapshot()
    {
        var state = new FDriverMapCameraState(); state.ObserveNativeCamera(37.515, 127.015, 12.5);
        var snapshot = state.CurrentCamera;
        state.ObserveNativeCamera(0, 0, 13); state.ObserveNativeCamera(37.5, 127, double.NaN);
        Assert.Same(snapshot, state.CurrentCamera);
    }

    [Fact]
    public void UnknownGpsUsesOnlyInitialViewportAndNeverTreatsItAsCurrentLocation()
    {
        var state = new FDriverMapCameraState();
        var initial = state.ResolveUpdate(37.5665, 126.978, false, 0, 0, true, 13, 0);

        Assert.NotNull(initial);
        Assert.Equal(37.5665, initial.Latitude);
        Assert.False(initial.ResumeFollowing);
        Assert.Null(state.ResolveUpdate(37.5, 127.0, false, 0, 0, true, 13, 1));
    }

    [Fact]
    public void BrowsingViewportSurvivesGpsAndCenterUpdatesUntilExplicitRecenter()
    {
        var state = new FDriverMapCameraState();
        state.ResolveUpdate(37.5, 127, true, 37.5, 127, true, 13, 0);
        state.ObserveNativeCamera(37.6, 127.1, 16);

        Assert.Null(state.ResolveUpdate(37.51, 127.01, true, 37.51, 127.01, false, 16, 0));
        var recentered = state.ResolveUpdate(37.51, 127.01, true, 37.51, 127.01, false, 16, 1);

        Assert.NotNull(recentered);
        Assert.Equal(37.51, recentered.Latitude);
        Assert.Equal(127.01, recentered.Longitude);
        Assert.Equal(16, recentered.Zoom);
        Assert.True(recentered.ResumeFollowing);
        Assert.Null(state.ResolveUpdate(37.51, 127.01, true, 37.51, 127.01, true, 16, 1));
    }

    [Fact]
    public void FollowingGpsPreservesNativeZoomInsteadOfRestoringInitialZoom()
    {
        var state = new FDriverMapCameraState();
        state.ResolveUpdate(37.5, 127, true, 37.5, 127, true, 13, 0);
        state.ObserveNativeCamera(37.5, 127, 17);

        var moved = state.ResolveUpdate(37.51, 127.01, true, 37.51, 127.01, true, 17, 0);

        Assert.NotNull(moved);
        Assert.Equal(17, moved.Zoom);
        Assert.Equal(37.51, moved.Latitude);
        Assert.False(moved.ResumeFollowing);
        Assert.Null(state.ResolveUpdate(37.51, 127.01, true, 37.51, 127.01, true, 17, 0));
    }

    [Fact]
    public void ZoomChangeKeepsBrowsedTargetAndRepeatedPropertyMappingsDoNotMoveAgain()
    {
        var state = new FDriverMapCameraState();
        state.ObserveNativeCamera(37.6, 127.1, 16);

        var zoomed = state.ResolveUpdate(37.5, 127, true, 37.5, 127, false, 17, 0);

        Assert.NotNull(zoomed);
        Assert.Equal(37.6, zoomed.Latitude);
        Assert.Equal(127.1, zoomed.Longitude);
        Assert.Equal(17, zoomed.Zoom);
        Assert.Null(state.ResolveUpdate(37.5, 127, true, 37.5, 127, false, 17, 0));
    }

    [Fact]
    public void GpsLossOrInvalidGpsCannotMoveToFallbackOrResumeFollowing()
    {
        var state = new FDriverMapCameraState();
        state.ObserveNativeCamera(37.6, 127.1, 16);

        Assert.Null(state.ResolveUpdate(37.5, 127, false, 37.5, 127, false, 16, 1));
        Assert.Null(state.ResolveUpdate(37.5, 127, true, double.NaN, 127, false, 16, 2));
        Assert.Null(state.ResolveUpdate(37.5, 127, true, 0, 0, true, 16, 3));
    }

    [Fact]
    public void RecenterAtSameLocationIsAnExplicitCommandAndEachVersionRunsOnce()
    {
        var state = new FDriverMapCameraState();
        state.ResolveUpdate(37.5, 127, true, 37.5, 127, true, 13, 0);

        Assert.NotNull(state.ResolveUpdate(37.5, 127, true, 37.5, 127, true, 13, 1));
        Assert.Null(state.ResolveUpdate(37.5, 127, true, 37.5, 127, true, 13, 1));
        Assert.NotNull(state.ResolveUpdate(37.5, 127, true, 37.5, 127, true, 13, 2));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(91, 127)]
    [InlineData(37, 181)]
    [InlineData(-91, 127)]
    [InlineData(37, -181)]
    [InlineData(double.NaN, 127)]
    [InlineData(37, double.PositiveInfinity)]
    public void InvalidCoordinatesCannotCreateACameraOrOverlayTarget(double latitude, double longitude)
    {
        Assert.False(FDriverMapCameraState.IsValidCoordinate(latitude, longitude));
        Assert.Null(new FDriverMapCameraState().ResolveUpdate(latitude, longitude, true, latitude, longitude, true, 13, 0));
    }
}
