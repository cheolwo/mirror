using System.Net.Http.Json;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public interface I공개화물Client
{
    Task<IReadOnlyList<공개화물요약응답>> 조회Async(int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>개인 화물·추천 API나 인증 복원 없이 기존 공개 요약 계약만 읽습니다.</summary>
public sealed class 공개화물Client(HttpClient http) : I공개화물Client
{
    public async Task<IReadOnlyList<공개화물요약응답>> 조회Async(int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 50);
        using var response = await http.GetAsync($"api/v1/shipper/requests/public?page={page}&pageSize={pageSize}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<공개화물요약응답>>(cancellationToken) ?? [];
    }
}

/// <summary>공개 탐색의 조회·페이지 상태만 소유하며 신청·추천 명령은 실행하지 않습니다.</summary>
public sealed class 공개화물목록ViewModel(I공개화물Client client) : IDisposable
{
    private CancellationTokenSource? _read;
    private long _generation;
    private bool _disposed;
    public const int PageSize = 20;
    public IReadOnlyList<공개화물요약응답> Items { get; private set; } = [];
    public int Page { get; private set; } = 1;
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public bool HasLoaded { get; private set; }
    public bool CanReadNext => !IsLoading && Error is null && Items.Count == PageSize;

    public async Task LoadAsync(int page = 1)
    {
        if (_disposed || page < 1) return;
        _read?.Cancel();
        _read?.Dispose();
        var read = _read = new CancellationTokenSource();
        var generation = ++_generation;
        IsLoading = true;
        Error = null;
        Items = [];
        Page = page;
        try
        {
            var items = await client.조회Async(page, PageSize, read.Token);
            if (_disposed || generation != _generation) return;
            Items = items;
            HasLoaded = true;
        }
        catch (OperationCanceledException) when (read.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_disposed && generation == _generation)
                Error = "공개 화물 목록을 불러오지 못했습니다. 연결을 확인한 뒤 다시 시도해 주세요.";
        }
        finally
        {
            if (!_disposed && generation == _generation) IsLoading = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_generation;
        _read?.Cancel();
        _read?.Dispose();
    }
}
