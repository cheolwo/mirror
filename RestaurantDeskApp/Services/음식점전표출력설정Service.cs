namespace RestaurantDeskApp.Services;

/// <summary>캐시를 두지 않아 다시 실행하거나 다른 서비스에서 저장한 설정도 현재 값으로 읽습니다.</summary>
public sealed class 음식점전표출력설정Service : I음식점전표출력설정Service
{
    public const int DefaultCopies = 2;
    private readonly I음식점전표출력설정Store _store;
    private readonly object _gate = new();

    public 음식점전표출력설정Service(I음식점전표출력설정Store store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public 음식점전표출력설정Snapshot 현재조회()
    {
        lock (_gate)
        {
            var stored = _store.Read();
            return new(stored is 1 or 2 ? stored.Value : DefaultCopies);
        }
    }

    public Task 저장Async(int 출력장수, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (출력장수 is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(출력장수), 출력장수, "빌지 장수는 1장 또는 2장으로 선택해 주세요.");
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _store.Write(출력장수);
        }
        return Task.CompletedTask;
    }
}
