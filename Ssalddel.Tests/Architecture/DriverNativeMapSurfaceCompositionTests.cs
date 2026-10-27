using System.Text.RegularExpressions;

namespace Ssalddel.Tests.Architecture;

/// <summary>실제 Android 소스의 제공자 자원 결속·호출 순서 검사입니다. SDK 실행·기기 지도 검증은 아닙니다.</summary>
public sealed class DriverNativeMapSurfaceCompositionTests
{
    [Theory]
    [InlineData("Naver")]
    [InlineData("Google")]
    public void 제공자별surface는_뷰생성과ready콜백을_기존SDK순서로소유한다(string provider)
    {
        var source = Surface(provider);
        var create = Method(source, "EnsureCreated");

        Assert.Contains("private sealed class " + provider + "MapSurface", source);
        Assert.Contains("if (View is not null || owner._container is null)", create);
        InOrder(create, "View.OnCreate((Bundle?)null)", "View.OnStart()", "View.OnResume()",
            "View.GetMapAsync(new " + provider + "MapReadyCallback(this, owner._mapLifecycleGeneration))", "owner._container.AddView(View)");
        Assert.Contains("surface.On" + provider + "MapReady", source);
    }

    [Theory]
    [InlineData("Naver")]
    [InlineData("Google")]
    public void ready콜백은_전역generation만검사하며_숨긴제공자도현재콜백을수용한다(string provider)
    {
        var ready = Method(Surface(provider), "On" + provider + "MapReady");

        InOrder(ready, "if (owner._container is null || generation != owner._mapLifecycleGeneration) return;",
            "Map = ", "Apply" + provider + "MapOptions()", "Apply" + provider + "Camera()",
            "Apply" + provider + "Markers()", "Apply" + provider + "RouteOverlays()",
            "owner.RefreshLocationPresentation(force: true)", "owner.StartLocationFreshnessTimer()");
        Assert.DoesNotContain("UsesGoogleMaps", ready);
    }

    [Fact]
    public void 연결해제는_모든overlay제거후_양쪽뷰와listener를_기존전역순서로해제한다()
    {
        var disconnect = Method(Handler(), "DisconnectHandler");

        InOrder(disconnect, "++_mapLifecycleGeneration", "StopDirectionCompass()", "StopLocationFreshnessTimer()",
            "_locationPresentation = null", "ClearNaverMarkers()", "ClearNaverRouteOverlays()", "ClearGoogleMarkers()",
            "ClearGoogleRouteOverlays()", "ClearGoogleCurrentLocationMarker()", "_googleSurface?.DisposeInactiveMarkerIcon()",
            "_naverSurface?.DestroyView()", "_googleSurface?.DestroyView()", "_naverSurface?.ClearMapReference()",
            "_googleSurface?.ClearMapReference()", "_container = null", "_naverSurface?.ClearViewReference()",
            "_googleSurface?.ClearViewReference()", "_googleSurface?.DisposeMarkerClickListener()", "base.DisconnectHandler(platformView)");
    }

    [Fact]
    public void 제공자전환은_비활성뷰를유지하며_표시갱신은양쪽surface에전달한다()
    {
        var handler = Handler();
        var visibility = Method(handler, "ApplyProviderVisibility");

        Assert.Contains("_naverMapView.Visibility = UsesGoogleMaps ? ViewStates.Gone : ViewStates.Visible", visibility);
        Assert.Contains("_googleMapView.Visibility = UsesGoogleMaps ? ViewStates.Visible : ViewStates.Gone", visibility);
        Assert.DoesNotContain("Destroy", visibility);
        Assert.DoesNotContain("Dispose", visibility);
        Assert.DoesNotContain("++_mapLifecycleGeneration", visibility);
        InOrder(Method(handler, "ApplyCamera"), "ApplyNaverCamera()", "ApplyGoogleCamera()");
        InOrder(Method(handler, "ApplyMarkers"), "ApplyNaverMarkers()", "ApplyGoogleMarkers()");
        InOrder(Method(handler, "ApplyRouteOverlays"), "ApplyNaverRouteOverlays()", "ApplyGoogleRouteOverlays()");
    }

    [Fact]
    public void Handler는_위치불변시에도_매초방향신선도를갱신하고_타이머를해제한다()
    {
        var handler = Handler();

        Assert.Contains("_locationFreshnessTimer.Interval = TimeSpan.FromSeconds(1)", Method(handler, "StartLocationFreshnessTimer"));
        InOrder(Method(handler, "OnLocationFreshnessTick"), "RefreshLocationPresentation()", "RefreshDirectionPresentation()");
        InOrder(Method(handler, "StopLocationFreshnessTimer"), "_locationFreshnessTimer.Stop()",
            "_locationFreshnessTimer.Tick -= OnLocationFreshnessTick", "_locationFreshnessTimer = null");
        Assert.Contains("private long _mapLifecycleGeneration", handler);
        Assert.DoesNotContain("IDispatcherTimer", Surface("Naver"));
        Assert.DoesNotContain("IDispatcherTimer", Surface("Google"));
    }

    [Fact]
    public void 각SDK의overlay분리와뷰파괴방법을_서로바꾸지않는다()
    {
        var naver = Surface("Naver");
        var google = Surface("Google");

        InOrder(Method(naver, "ClearNaverMarkers"), "marker.Map = null", "marker.Dispose()", "_naverMarkers.Clear()");
        InOrder(Method(naver, "ClearNaverRouteOverlays"), "overlay.Map = null", "overlay.Dispose()", "_naverRouteOverlays.Clear()");
        InOrder(Method(google, "ClearGoogleMarkers"), "marker.Remove()", "marker.Dispose()", "_googleMarkers.Clear()", "_googleMarkerItems.Clear()");
        InOrder(Method(google, "ClearGoogleRouteOverlays"), "overlay.Remove()", "overlay.Dispose()", "_googleRouteOverlays.Clear()");
        InOrder(Method(google, "ClearGoogleCurrentLocationMarker"), "_googleCurrentLocationMarker?.Remove()",
            "_googleCurrentLocationMarker?.Dispose()", "_googleCurrentLocationMarker = null");
        foreach (var source in new[] { naver, google })
            InOrder(Method(source, "DestroyView"), "View.OnPause()", "View.OnStop()", "View.OnDestroy()", "View.Dispose()");
    }

    [Fact]
    public void 핀클릭은_기존원본ID와SDK반환값을유지하고_표현정책을재사용한다()
    {
        var naver = Surface("Naver");
        var google = Surface("Google");

        Assert.Contains("owner.VirtualView.SendMarkerSelected(pin.Source)", Method(naver, "AddNaverMarker"));
        Assert.Contains("_googleMarkerItems[marker.Id] = pin.Source", Method(google, "AddGoogleMarker"));
        InOrder(Method(google, "OnGoogleMarkerClicked"), "_googleMarkerItems.TryGetValue(marker.Id, out var item)", "owner.VirtualView.SendMarkerSelected(item)", "return false");
        Assert.Contains("DriverRecommendationMarkerPresentationPolicy.Build", Handler());
        Assert.Contains("DriverNativeLocationPolicy.RoutesForDisplay", naver);
        Assert.Contains("DriverNativeLocationPolicy.RoutesForDisplay", google);
    }

    private static string Handler() => Read("DriverNativeMapViewHandler.Android.cs");
    private static string Surface(string provider) => Read("DriverNativeMapViewHandler." + provider + "Surface.Android.cs");
    private static string Read(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "DriverApp", "Handlers", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new DirectoryNotFoundException("DriverApp 지도 Handler 소스를 찾지 못했습니다.");
    }

    private static string Method(string source, string name)
    {
        var match = Regex.Match(source, @"(?m)^\s*(?:private|protected|public) [^\r\n]*\b" + Regex.Escape(name) + @"\(");
        Assert.True(match.Success, name + " 메서드가 없습니다.");
        var opening = source.IndexOf('{', match.Index);
        var depth = 0;
        for (var end = opening; end < source.Length; end++)
        {
            if (source[end] == '{') depth++;
            else if (source[end] == '}' && --depth == 0) return source[opening..(end + 1)];
        }
        throw new InvalidDataException(name + " 메서드 본문이 닫히지 않았습니다.");
    }

    private static void InOrder(string source, params string[] fragments)
    {
        var offset = 0;
        foreach (var fragment in fragments)
        {
            var found = source.IndexOf(fragment, offset, StringComparison.Ordinal);
            Assert.True(found >= 0, "호출 순서에서 찾지 못했습니다: " + fragment);
            offset = found + fragment.Length;
        }
    }
}
