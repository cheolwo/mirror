using Ssalddel.Services.Food;

namespace Ssalddel.Services.Development.MobileFieldTest;

/// <summary>허용된 두 시험 주소만 해석한다. 실제 API 실패의 fallback은 제공하지 않는다.</summary>
public sealed class 모바일음식배차시험좌표Service(모바일음식배차시험Guard guard) : IKakao좌표변환Service
{
    public Task<Kakao주소정보?> 주소정보조회Async(string 주소, CancellationToken cancellationToken = default)
    {
        guard.EnsureEnabled(); cancellationToken.ThrowIfCancellationRequested();
        var route = 모바일음식배차시험자료.ConfiguredRoute;
        var coordinates = 주소 switch
        {
            모바일음식배차시험자료.PickupAddress => (route.PickupLatitude, route.PickupLongitude),
            모바일음식배차시험자료.DropoffAddress => (route.DropoffLatitude, route.DropoffLongitude),
            _ => throw new ArgumentException("MobileFoodDispatchAddressOutsideFixture")
        };
        return Task.FromResult<Kakao주소정보?>(new(주소, 주소, "시험 지역", "시험 권역", "시험 구역",
            coordinates.Item1, coordinates.Item2));
    }

    public async Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string 주소, CancellationToken cancellationToken = default)
    {
        var value = await 주소정보조회Async(주소, cancellationToken);
        return ((double)value!.위도!.Value, (double)value.경도!.Value);
    }

    public Task<Kakao지역정보?> 좌표지역정보조회Async(decimal 위도, decimal 경도, CancellationToken cancellationToken = default)
    {
        guard.EnsureEnabled(); cancellationToken.ThrowIfCancellationRequested();
        var route = 모바일음식배차시험자료.ConfiguredRoute;
        if ((위도 != route.PickupLatitude || 경도 != route.PickupLongitude)
            && (위도 != route.DropoffLatitude || 경도 != route.DropoffLongitude)
            && (위도 != 모바일음식배차시험자료.DriverLatitude || 경도 != 모바일음식배차시험자료.DriverLongitude))
            throw new ArgumentException("MobileFoodDispatchCoordinatesOutsideFixture");
        return Task.FromResult<Kakao지역정보?>(new("시험 지역", "시험 권역", "시험 구역", "합성 시험 좌표"));
    }
}
