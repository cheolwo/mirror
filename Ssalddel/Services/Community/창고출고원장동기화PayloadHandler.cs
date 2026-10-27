using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Data;
using 살뜰.도메인.마트;
using 살뜰.도메인.설정;

namespace Ssalddel.Services.Community;

/// <summary>저장된 출고·입고 식별자로 최신 SQL 원천과 기사 단계를 읽어 투영합니다.</summary>
internal sealed class 창고출고원장동기화PayloadHandler(
    SsalddelContext db,
    I음식마트원장Mongo동기화Service ledgerSync)
{
    public async Task 처리Async(
        음식마트원장동기화Outbox item,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<창고출고원장동기화Payload>(item.PayloadJson, jsonOptions)
                      ?? throw new InvalidOperationException("창고 출고 원장 동기화 payload가 비어 있습니다.");
        var outboundIds = payload.OutboundIds.ToList();
        var outbounds = await db.출고예정.AsNoTracking()
            .Where(x => outboundIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (outbounds.Count == 0)
        {
            throw new InvalidOperationException("원장 동기화 대상 출고예정을 찾을 수 없습니다.");
        }

        var inboundIds = payload.InboundIds.ToList();
        var inbounds = inboundIds.Count == 0
            ? []
            : await db.입고요청.AsNoTracking()
                .Where(x => inboundIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        var driverAttempt = await db.Set<마트배송시도>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.주문참조번호 == item.원천Id, cancellationToken);
        var stage = payload.CurrentStageKey;
        var template = payload.LedgerTemplateKey;
        if (driverAttempt is not null)
        {
            if (outbounds.Any(x => x.주문참조번호 != item.원천Id || x.운송의뢰Id != driverAttempt.배차의뢰Id))
                throw new InvalidOperationException("MartDriverLedgerSourceMismatch");
            // 예전 Pending child의 포장 단계/추정 템플릿을 최신 기사 업무에 덮어쓰지 않습니다.
            stage = driverAttempt.상태Code switch
            {
                "Accepted" => "픽업대기", "PickedUp" => "배송중", "Delivered" => "전달완료",
                _ => throw new InvalidOperationException("MartDriverLedgerStageUnavailable")
            };
            template = CommunityLedgerTemplateKeys.SsalddelMart;
        }
        var ledgerResult = await ledgerSync.출고원장동기화Async(
            outbounds,
            inbounds,
            item.변경자,
            stage,
            template,
            cancellationToken);
        if (ledgerResult is null)
        {
            throw new InvalidOperationException("마트/창고 Mongo 원장 동기화 결과가 없습니다.");
        }
    }
}

internal sealed class 창고출고원장동기화Payload
{
    public 창고출고원장동기화Payload()
    {
    }

    public long[] OutboundIds { get; set; } = [];

    public long[] InboundIds { get; set; } = [];

    public string? CurrentStageKey { get; set; }

    public string? LedgerTemplateKey { get; set; }
}
