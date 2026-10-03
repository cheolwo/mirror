using Ssalddel.Contracts.Food;
using Ssalddel.Client.Infrastructure.Security;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Options;

namespace RestaurantDeskApp.Services;

public sealed class 음식점주문SignalRClientService(
    IOptions<RestaurantDeskOptions> options,
    RestaurantAuthService authService,
    ClientAuthSession authSession) : I음식점주문SignalRClientService
{
    private const string ReceiveRestaurantOrderNotificationMethod = "ReceiveRestaurantOrderNotification";
    private const string ReceiveRestaurantOrderStatusChangedMethod = "ReceiveRestaurantOrderStatusChanged";
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private HubConnection? _connection;
    private string? _connectionUserId;
    private long _connectionGeneration;
    private long _statePublication;

    public event Func<음식점주문수신알림, Task>? 주문수신;

    public event Func<음식점주문상태변경알림, Task>? 주문상태변경;

    public event Func<음식점실시간연결상태변경, Task>? 상태변경;

    public event Func<Task>? 재연결후재조회요청;

    public 음식점실시간연결상태 연결상태 { get; private set; } = 음식점실시간연결상태.연결대기;

    public async Task 연결Async(CancellationToken cancellationToken = default)
    {
        HubConnection? observedConnection;
        long observedGeneration;
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            observedConnection = _connection;
            observedGeneration = _connectionGeneration;
        }
        finally { _connectionGate.Release(); }
        var auth = await authService.EnsureAccessTokenAsync(
            cancellationToken: cancellationToken);
        if (!auth.IsSuccess)
        {
            if (auth.RequiresLogin)
            {
                var detachedGeneration = await DisconnectConnectionAsync(
                    observedConnection, observedGeneration, suppressDisposeFailure: true);
                if (detachedGeneration is null) return;
                if (!await Publish상태Async(음식점실시간연결상태.인증필요,
                    auth.ErrorMessage ?? "음식점 주문 허브 인증을 복구할 수 없습니다.",
                    () => _connectionGeneration == detachedGeneration && !authSession.IsAuthenticated)) return;
                throw new UnauthorizedAccessException(auth.ErrorMessage);
            }
            if (!await Publish상태Async(음식점실시간연결상태.연결끊김,
                auth.ErrorMessage ?? "음식점 주문 허브 인증을 복구할 수 없습니다.",
                () => _connectionGeneration == observedGeneration)) return;
            throw new HttpRequestException(auth.ErrorMessage);
        }

        var userId = authSession.UserId!;
        HubConnection? previous = null;
        HubConnection connection;
        bool reused;
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentSession(userId))
            {
                throw new OperationCanceledException("음식점 허브 연결 중 인증 상태가 변경되었습니다.");
            }

            reused = _connection is { State: HubConnectionState.Connected }
                && string.Equals(_connectionUserId, userId, StringComparison.Ordinal);
            if (reused)
            {
                connection = _connection!;
            }
            else
            {
                connection = CreateConnection(userId);
                previous = _connection;
                _connection = connection;
                _connectionUserId = userId;
                _connectionGeneration++;
            }
        }
        finally
        {
            _connectionGate.Release();
        }

        try
        {
            // Network lifecycle and subscribers may re-enter this service; never await them under the gate.
            if (previous is not null) await previous.DisposeAsync();
            if (!IsCurrentConnection(connection, userId))
            {
                await DisconnectConnectionAsync(connection);
                return;
            }
            if (!await Publish상태Async(음식점실시간연결상태.연결중,
                "음식점 주문 허브에 연결하고 있습니다.",
                () => IsCurrentConnection(connection, userId)))
            {
                await DisconnectConnectionAsync(connection);
                return;
            }
            if (!IsCurrentConnection(connection, userId))
            {
                await DisconnectConnectionAsync(connection);
                return;
            }
            if (!reused) await connection.StartAsync(cancellationToken);
            await JoinRestaurantGroupAsync(connection, userId, cancellationToken);
            if (!IsCurrentConnection(connection, userId))
            {
                await DisconnectConnectionAsync(connection);
                return;
            }
            await Publish상태Async(
                음식점실시간연결상태.연결됨,
                reused ? "음식점 주문 허브에 이미 연결되어 있습니다." : "음식점 주문 허브에 연결되었습니다.",
                () => IsCurrentConnection(connection, userId) && connection.State == HubConnectionState.Connected);
        }
        catch
        {
            try { await DisconnectConnectionAsync(connection); }
            catch { /* Preserve the original connection/authentication failure. */ }
            throw;
        }
    }

    private bool IsCurrentSession(string userId)
        => authSession.IsAuthenticated
            && !string.IsNullOrWhiteSpace(userId)
            && string.Equals(authSession.UserId, userId, StringComparison.Ordinal);

    private bool IsCurrentConnection(HubConnection connection, string userId)
        => ReferenceEquals(_connection, connection)
            && string.Equals(_connectionUserId, userId, StringComparison.Ordinal)
            && IsCurrentSession(userId);

    private HubConnection CreateConnection(string userId)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(
                BuildHubUri(),
                connectionOptions =>
                {
                    connectionOptions.AccessTokenProvider = async () =>
                    {
                        var result = await authService.EnsureAccessTokenAsync();
                        return result.IsSuccess && IsCurrentSession(userId) ? authSession.AccessToken : null;
                    };
                })
            .WithAutomaticReconnect()
            .Build();

        connection.On<음식점주문수신알림>(
            ReceiveRestaurantOrderNotificationMethod,
            async notification => await Publish주문수신Async(connection, userId, notification));
        connection.On<음식점주문상태변경알림>(
            ReceiveRestaurantOrderStatusChangedMethod,
            async notification => await Publish주문상태변경Async(connection, userId, notification));

        connection.Reconnecting += async error =>
        {
            if (!IsCurrentConnection(connection, userId)) return;
            await Publish상태Async(
                음식점실시간연결상태.재연결중,
                error is null
                ? "음식점 주문 허브 재연결을 시도합니다."
                : $"음식점 주문 허브 연결이 끊겼습니다. 재연결을 시도합니다. {error.Message}",
                () => IsCurrentConnection(connection, userId));
        };

        connection.Reconnected += async _ =>
        {
            if (!IsCurrentConnection(connection, userId)) return;
            try
            {
                await JoinRestaurantGroupAsync(connection, userId, CancellationToken.None);
                if (!IsCurrentConnection(connection, userId)) return;
                await Publish상태Async(
                    음식점실시간연결상태.연결됨,
                    "음식점 주문 허브에 다시 연결되었습니다. 서버 수신함을 즉시 확인합니다.",
                    () => IsCurrentConnection(connection, userId) && connection.State == HubConnectionState.Connected);
                await Publish재연결후재조회Async(connection, userId);
            }
            catch (UnauthorizedAccessException ex)
            {
                if (!IsCurrentConnection(connection, userId)) return;
                await Publish상태Async(음식점실시간연결상태.인증필요, ex.Message,
                    () => IsCurrentConnection(connection, userId));
            }
            catch (Exception ex)
            {
                if (!IsCurrentConnection(connection, userId)) return;
                await Publish상태Async(
                    음식점실시간연결상태.연결끊김,
                    $"음식점 주문 허브 그룹 재가입에 실패했습니다. {ex.Message}",
                    () => IsCurrentConnection(connection, userId));
            }
        };

        connection.Closed += async error =>
        {
            if (!IsCurrentConnection(connection, userId)) return;
            await Publish상태Async(
                음식점실시간연결상태.연결끊김,
                error is null
                ? "음식점 주문 허브 연결이 종료되었습니다."
                : $"음식점 주문 허브 연결이 종료되었습니다. {error.Message}",
                () => IsCurrentConnection(connection, userId));
        };

        return connection;
    }

    public Task 연결해제Async() => DisconnectConnectionAsync(expected: null);

    private async Task<long?> DisconnectConnectionAsync(HubConnection? expected,
        long? expectedGeneration = null, bool suppressDisposeFailure = false)
    {
        HubConnection? connection;
        long detachedGeneration;
        await _connectionGate.WaitAsync();
        try
        {
            if (expectedGeneration is not null && expectedGeneration != _connectionGeneration) return null;
            if (expected is not null && !ReferenceEquals(_connection, expected)) return null;
            connection = _connection;
            _connection = null;
            _connectionUserId = null;
            detachedGeneration = ++_connectionGeneration;
            _statePublication++;
            연결상태 = 음식점실시간연결상태.연결끊김;
        }
        finally
        {
            _connectionGate.Release();
        }

        try
        {
            if (connection is not null) await connection.DisposeAsync();
        }
        catch when (suppressDisposeFailure)
        {
            // An authentication rejection must still be reported after local ownership is revoked.
        }
        return detachedGeneration;
    }

    public async ValueTask DisposeAsync()
    {
        await 연결해제Async();
    }

    private async Task JoinRestaurantGroupAsync(HubConnection connection, string userId, CancellationToken cancellationToken)
    {
        if (!IsCurrentConnection(connection, userId) || connection.State != HubConnectionState.Connected)
        {
            return;
        }

        await connection.InvokeAsync("JoinRestaurantOrders", cancellationToken);
    }

    private Uri BuildHubUri()
        => new(options.Value.GetServerBaseAddress(), "hubs/restaurant-orders");

    private async Task Publish주문수신Async(HubConnection connection, string userId, 음식점주문수신알림 notification)
    {
        var handler = 주문수신;
        if (handler is null)
        {
            return;
        }

        foreach (Func<음식점주문수신알림, Task> callback in handler.GetInvocationList())
        {
            if (!IsCurrentConnection(connection, userId)) return;
            await callback(notification);
        }
    }

    private async Task<bool> Publish상태Async(
        음식점실시간연결상태 상태,
        string message,
        Func<bool> isCurrent)
    {
        Func<음식점실시간연결상태변경, Task>? handler;
        long publication;
        await _connectionGate.WaitAsync();
        try
        {
            if (!isCurrent()) return false;
            연결상태 = 상태;
            publication = ++_statePublication;
            handler = 상태변경;
        }
        finally { _connectionGate.Release(); }
        if (handler is null) return true;

        var change = new 음식점실시간연결상태변경(상태, message);
        foreach (Func<음식점실시간연결상태변경, Task> callback in handler.GetInvocationList())
        {
            await _connectionGate.WaitAsync();
            try
            {
                if (publication != _statePublication || !isCurrent()) return false;
            }
            finally { _connectionGate.Release(); }
            await callback(change);
        }
        await _connectionGate.WaitAsync();
        try { return publication == _statePublication && isCurrent(); }
        finally { _connectionGate.Release(); }
    }

    private async Task Publish재연결후재조회Async(HubConnection connection, string userId)
    {
        var handler = 재연결후재조회요청;
        if (handler is null)
        {
            return;
        }

        foreach (Func<Task> callback in handler.GetInvocationList())
        {
            if (!IsCurrentConnection(connection, userId)) return;
            await callback();
        }
    }

    private async Task Publish주문상태변경Async(HubConnection connection, string userId, 음식점주문상태변경알림 notification)
    {
        var handler = 주문상태변경;
        if (handler is null)
        {
            return;
        }

        foreach (Func<음식점주문상태변경알림, Task> callback in handler.GetInvocationList())
        {
            if (!IsCurrentConnection(connection, userId)) return;
            await callback(notification);
        }
    }
}
