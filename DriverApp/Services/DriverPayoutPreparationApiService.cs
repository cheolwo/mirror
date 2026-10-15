using Ssalddel.Contracts.Driver.Settlement;

namespace DriverApp.Services;

/// <summary>기존 운송대금 지급 준비 조회만 연결하며 승인·송금 명령은 제공하지 않습니다.</summary>
public interface IDriverPayoutPreparationApiService
{
    Task<기사지급준비목록응답?> 월별조회Async(int year, int month, CancellationToken cancellationToken = default);
}

public sealed class DriverPayoutPreparationApiService(IDriverApiClient client) : IDriverPayoutPreparationApiService
{
    public Task<기사지급준비목록응답?> 월별조회Async(int year, int month, CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "조회할 연도와 월을 확인해 주세요.");
        return client.GetAsync<기사지급준비목록응답>(
            $"api/v1/driver/payout-preparations?year={year}&month={month}",
            "운송대금 지급 준비 조회", cancellationToken);
    }
}
