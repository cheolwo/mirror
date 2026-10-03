using FDriverApp.Controls;

namespace Ssalddel.Tests.Clients;

public sealed class NaverMapReadinessTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("YOUR_NAVER_MAP_SDK_NCP_KEY_ID")]
    [InlineData(" replace_local_key ")]
    public void MissingConfigurationCannotBecomeReadyFromNativeCallbacks(string? key)
    {
        var state = new NaverMapReadiness(key);
        state.OnTilesLoaded();
        state.SetInternetAvailable(true);
        state.OnAuthenticationFailed("401");
        state.OnLoadTimeout();
        Assert.False(state.IsConfigured);
        Assert.Equal(NaverMapReadinessStatus.ConfigurationMissing, state.Status);
        Assert.NotNull(state.Message);
    }

    [Fact]
    public void NativeObjectCreationIsNotTileLoadSuccessAndTimeoutCanRecoverOnActualTiles()
    {
        var state = new NaverMapReadiness("test-key-id");
        Assert.Equal(NaverMapReadinessStatus.Loading, state.Status);
        state.OnLoadTimeout();
        Assert.Equal(NaverMapReadinessStatus.LoadUnavailable, state.Status);
        state.OnTilesLoaded();
        Assert.Equal(NaverMapReadinessStatus.Ready, state.Status);
        Assert.Null(state.Message);
    }

    [Fact]
    public void OfflineCachedTilesDoNotClaimCurrentNetworkSuccess()
    {
        var state = new NaverMapReadiness("test-key-id");
        state.SetInternetAvailable(false);
        state.OnTilesLoaded();
        state.OnLoadTimeout();
        Assert.Equal(NaverMapReadinessStatus.Offline, state.Status);
        Assert.NotNull(state.Message);
        state.SetInternetAvailable(true);
        Assert.Equal(NaverMapReadinessStatus.Ready, state.Status);
    }

    [Theory]
    [InlineData("401")]
    [InlineData("429")]
    [InlineData("800")]
    [InlineData("unknown error with sensitive config")]
    public void AuthenticationFailureRemainsVisibleAfterTileOrConnectivityCallbacks(string code)
    {
        var state = new NaverMapReadiness("test-key-id");
        state.OnTilesLoaded();
        state.OnAuthenticationFailed(code);
        state.OnTilesLoaded();
        state.SetInternetAvailable(false);
        state.SetInternetAvailable(true);
        state.OnLoadTimeout();
        Assert.Equal(NaverMapReadinessStatus.AuthenticationFailed, state.Status);
        Assert.NotNull(state.Message);
        Assert.DoesNotContain(code, state.Message);
    }
}
