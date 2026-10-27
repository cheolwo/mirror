using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Recommendation;

namespace 살뜰.Services.Dispatch.Queue;

/// <summary>추적 투영 뒤 음식/화물의 재구성 가능한 실행 인덱스를 갱신한다. SaveChanges는 호출자가 소유한다.</summary>
internal sealed class 배차대기실행공간Projector(
    I운송원장배달권연결Service areaProjection,
    I음식배달권실행공간Store foodSpaces,
    I국내화물배달권실행공간Store cargoSpaces)
{
    internal async Task UpsertAsync(운송원장 배차대기, CancellationToken cancellationToken)
    {
        var 배달권연결 = await areaProjection.투영추적Async(
            배차대기,
            cancellationToken);

        if (배차대기.상태 != 상태값.배차대기상태.대기
            || 배차대기.배차큐단계 is 상태값.배차큐단계.확정 or 상태값.배차큐단계.종료)
        {
            await Remove모든실행공간Async(배차대기.의뢰Id, cancellationToken);
            return;
        }

        var 음식배달 = 배차대기.배차업무유형 == 상태값.배차업무유형.음식배달;
        var 실행배달권 = 음식배달
            ? 음식배달권정책.판정(
                CreatePoint(배차대기.픽업_위도, 배차대기.픽업_경도),
                배차대기.픽업_도로명주소)
            : 배달권연결.픽업배달권;
        if (string.Equals(실행배달권.배달권키, "unknown", StringComparison.Ordinal))
        {
            await Remove모든실행공간Async(배차대기.의뢰Id, cancellationToken);
            return;
        }

        if (음식배달)
        {
            await cargoSpaces.Remove운송의뢰Async(
                배차대기.의뢰Id,
                cancellationToken);
            await foodSpaces.Upsert운송의뢰Async(
                실행배달권.배달권키,
                배차대기.의뢰Id,
                음식배달권정책.인접배달권키조회(실행배달권.배달권키),
                cancellationToken);
        }
        else
        {
            await foodSpaces.Remove운송의뢰Async(
                배차대기.의뢰Id,
                cancellationToken);
            await cargoSpaces.Upsert운송의뢰Async(
                실행배달권.배달권키,
                배차대기.의뢰Id,
                국내행정구역배달권Catalog.인접배달권키조회(실행배달권.배달권키),
                cancellationToken);
        }
    }

    private async Task Remove모든실행공간Async(string 의뢰Id, CancellationToken cancellationToken)
    {
        await foodSpaces.Remove운송의뢰Async(의뢰Id, cancellationToken);
        await cargoSpaces.Remove운송의뢰Async(의뢰Id, cancellationToken);
    }

    private static 배차경로좌표? CreatePoint(decimal? latitude, decimal? longitude)
        => latitude.HasValue && longitude.HasValue
            ? new 배차경로좌표(latitude.Value, longitude.Value)
            : null;

}
