using Ssalddel.Contracts.Admin.Restaurants;

namespace SsalddelAdmin.Services;

/// <summary>리뷰 목록과 운영 정책을 기존 순서대로 조회해 완성된 묶음으로 반환한다.</summary>
internal sealed class 음식리뷰운영Reader(
    Func<Task<음식점리뷰관리목록응답>> readItems,
    Func<Task<음식점리뷰운영정책응답>> readPolicy)
{
    public async Task<음식리뷰운영Snapshot> ReadAsync()
    {
        var items = await readItems();
        var policy = await readPolicy();
        return new(items, policy);
    }
}

internal sealed record 음식리뷰운영Snapshot(음식점리뷰관리목록응답 Items, 음식점리뷰운영정책응답 Policy);
