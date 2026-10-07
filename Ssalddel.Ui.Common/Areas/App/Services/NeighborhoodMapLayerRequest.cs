namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>레이어별 최신 조회만 반영하고 변경·이탈 때 이전 통신을 취소합니다.</summary>
internal sealed class NeighborhoodMapLayerRequest : IDisposable
{
    private CancellationTokenSource? _source;
    public long Generation { get; private set; }

    public NeighborhoodMapLayerRead Begin(CancellationToken lifetime)
    {
        Invalidate();
        _source = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        return new(Generation, _source.Token);
    }

    public bool IsCurrent(NeighborhoodMapLayerRead read) => read.Generation == Generation && !read.Token.IsCancellationRequested;

    public void Invalidate()
    {
        ++Generation;
        _source?.Cancel();
        _source?.Dispose();
        _source = null;
    }

    public void Dispose() => Invalidate();
}

internal readonly record struct NeighborhoodMapLayerRead(long Generation, CancellationToken Token);
