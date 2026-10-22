using Ssalddel.Contracts.Common.PrivacySupport;

namespace Ssalddel.Services.PrivacySupport;

/// <summary>정식 거래 분쟁의 현재 원천 결속만 읽습니다. 보존 정지·사건 종결·송금을 실행하지 않습니다.</summary>
public interface I화물분쟁지급보류Query
{
    Task<화물분쟁지급보류판정> 조회Async(string requestId, CancellationToken cancellationToken = default);
}

public sealed record 화물분쟁지급보류판정(bool Hold, string Code, string Message)
{
    public static 화물분쟁지급보류판정 Clear { get; } = new(false, string.Empty, string.Empty);
    public static 화물분쟁지급보류판정 Open { get; } = new(true, "CargoTransactionDisputeOpen",
        "이 운송의 정식 분쟁 검토가 끝날 때까지 기사 지급을 보류합니다.");
    public static 화물분쟁지급보류판정 Unknown { get; } = new(true, "CargoTransactionDisputeStatusUnknown",
        "이 운송의 분쟁 상태를 확인하지 못해 기사 지급을 보류합니다. 다시 확인해 주세요.");
}

public sealed class 보호지원화물지급보류Query(I보호지원Store store) : I화물분쟁지급보류Query
{
    public async Task<화물분쟁지급보류판정> 조회Async(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId)) return 화물분쟁지급보류판정.Unknown;
        try
        {
            // 두 기존 출처가 같은 화주 운송의뢰를 사용합니다. 원천 종류·ID의 정확한 쌍만 조회합니다.
            var cargo = await store.HasUnresolvedDisputeAsync(보호지원출처Codes.화물의뢰, requestId, cancellationToken);
            if (cargo == true) return 화물분쟁지급보류판정.Open;
            var neighborhood = await store.HasUnresolvedDisputeAsync(보호지원출처Codes.생활배송, requestId, cancellationToken);
            if (neighborhood == true) return 화물분쟁지급보류판정.Open;
            return cargo == false && neighborhood == false
                ? 화물분쟁지급보류판정.Clear : 화물분쟁지급보류판정.Unknown;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return 화물분쟁지급보류판정.Unknown; }
    }

    internal static bool IsUnresolvedDispute(보호지원Record record)
        => record.Kind == 보호지원종류Codes.분쟁 && record.StatusCode is not ("closed" or "denied");

    public static Task<화물분쟁지급보류판정> 확인Async(I화물분쟁지급보류Query? query, string requestId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return query is null ? Task.FromResult(화물분쟁지급보류판정.Unknown) : CheckAsync(query, requestId, cancellationToken);
    }

    private static async Task<화물분쟁지급보류판정> CheckAsync(I화물분쟁지급보류Query query, string requestId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await query.조회Async(requestId, cancellationToken) ?? 화물분쟁지급보류판정.Unknown; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return 화물분쟁지급보류판정.Unknown; }
    }
}
