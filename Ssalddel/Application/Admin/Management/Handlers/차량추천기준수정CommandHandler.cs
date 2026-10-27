using Ssalddel.Contracts.Admin.Management;

namespace Ssalddel.Application.Admin.Management;

public sealed class 차량추천기준수정CommandHandler : IRequestHandler<차량추천기준수정Command, 차량추천기준응답?>
{
    private readonly SsalddelContext _db;

    public 차량추천기준수정CommandHandler(SsalddelContext db)
    {
        _db = db;
    }

    public async Task<차량추천기준응답?> Handle(차량추천기준수정Command request, CancellationToken cancellationToken)
    {
        var entity = await _db.차량제원.FindAsync([request.차량코드], cancellationToken);
        if (entity == null)
        {
            return null;
        }

        // 저장·추적 객체를 바꾸기 전에 생략 필드를 병합한 최종 상태를 검증한다.
        var length = request.적재함길이Mm ?? entity.적재함길이Mm;
        var width = request.적재함폭Mm ?? entity.적재함폭Mm;
        var height = request.적재함높이Mm ?? entity.적재함높이Mm;
        var maximumWeight = request.최대적재중량Kg ?? entity.최대적재중량Kg;
        if (length < 0 || width < 0 || height < 0 || maximumWeight < 0
            || request.운영권장중량Kg < 0 || request.권장최대CBM < 0 || request.팔레트적재개수 < 0)
        {
            throw new ArgumentException("제원과 운영 권장값은 음수일 수 없습니다.");
        }

        if (request.추천사용여부 && (length <= 0 || width <= 0 || maximumWeight <= 0
            || (entity.차량코드 is "오토바이" or "다마스" && height.GetValueOrDefault() <= 0)))
        {
            throw new ArgumentException("추천 사용 전 적재함 치수와 최대 적재중량을 확인해 등록해야 합니다.");
        }

        if (request.운영권장중량Kg > maximumWeight)
        {
            throw new ArgumentException("운영 권장중량은 최대 적재중량을 넘을 수 없습니다.");
        }

        var physicalCbm = length > 0 && width > 0 && height.GetValueOrDefault() > 0
            ? (length / 1000m) * (width / 1000m) * (height!.Value / 1000m)
            : (decimal?)null;
        decimal? recommendedCbm = request.권장최대CBM.HasValue && request.권장최대CBM.Value > 0
            ? decimal.Round(request.권장최대CBM.Value, 3, MidpointRounding.AwayFromZero)
            : null;
        if (request.권장최대CBM > 0 && recommendedCbm <= 0)
        {
            throw new ArgumentException("운영 권장부피는 0.001CBM 단위에서 양수로 표현할 수 있어야 합니다.");
        }
        if (request.권장최대CBM > 0 && (!physicalCbm.HasValue
            || request.권장최대CBM > physicalCbm || recommendedCbm > physicalCbm))
        {
            throw new ArgumentException("운영 권장부피는 확인된 적재함 부피를 넘을 수 없습니다.");
        }

        entity.적재함길이Mm = length;
        entity.적재함폭Mm = width;
        entity.적재함높이Mm = height == 0 ? null : height;
        entity.최대적재중량Kg = maximumWeight;
        entity.권장최대CBM = recommendedCbm;
        entity.추천우선순위 = request.추천우선순위;
        entity.추천사용여부 = request.추천사용여부;
        entity.운영권장중량Kg = request.운영권장중량Kg.HasValue && request.운영권장중량Kg.Value > 0
            ? request.운영권장중량Kg.Value
            : null;
        entity.팔레트적재개수 = request.팔레트적재개수.HasValue && request.팔레트적재개수.Value > 0
            ? request.팔레트적재개수.Value
            : null;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return 차량추천관리매퍼.To응답(entity);
    }
}
