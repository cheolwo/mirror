using FluentResults;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Services.Community;
using Ssalddel.Services.Operations;
using 살뜰.Services.External.Google;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 의뢰생성CommandHandler : IRequestHandler<의뢰생성Command, Result<화주운송의뢰응답>>
{
    private static readonly string[] AllowedPaymentStatuses = 상태값.결제상태.허용값;

    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IGeocodingService _geocodingService;
    private readonly I운송원장Mongo동기화Service _원장동기화Service;
    private readonly IOperatingMarketFreightWorkflowPolicy _freightWorkflowPolicy;
    private readonly I화주운송의뢰화물운송인계Service _operatingSystemHandoffService;
    private readonly 살뜰.Services.Options.ISsalddelExecutionModePolicy? _execution;

    public 의뢰생성CommandHandler(
        SsalddelContext db,
        IGeocodingService geocodingService,
        ICurrentUserAccessor currentUserAccessor,
        I운송원장Mongo동기화Service 원장동기화Service,
        IOperatingMarketFreightWorkflowPolicy freightWorkflowPolicy,
        I화주운송의뢰화물운송인계Service operatingSystemHandoffService,
        살뜰.Services.Options.ISsalddelExecutionModePolicy? execution = null)
    {
        _db = db;
        _geocodingService = geocodingService;
        _currentUserAccessor = currentUserAccessor;
        _원장동기화Service = 원장동기화Service;
        _freightWorkflowPolicy = freightWorkflowPolicy;
        _operatingSystemHandoffService = operatingSystemHandoffService;
        _execution = execution;
    }

    public async Task<Result<화주운송의뢰응답>> Handle(의뢰생성Command request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserAccessor.UserId;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Result.Fail<화주운송의뢰응답>("로그인 사용자 정보를 확인할 수 없습니다.");
        }

        var freightWorkflowDecision = _freightWorkflowPolicy.Evaluate(
            new OperatingMarketFreightWorkflowRequest
            {
                ActivityCode = FreightWorkflowActivityCodes.QualifiedProviderParticipationRequest
            });
        if (!freightWorkflowDecision.CanProceed)
        {
            return Result.Fail<화주운송의뢰응답>(
                $"OperatingMarketFreightPolicy:{freightWorkflowDecision.DecisionCode}");
        }

        if (화주운송의뢰생성입력Policy.입력오류(request) is { } inputError)
            return Result.Fail<화주운송의뢰응답>(inputError);

        // 이번 서비스의 내부 운영 물품 등록 기준입니다. 모든 운송수단의 동일 법정 의무라는 뜻은 아닙니다.
        // 생성됨도 이미 서버에 접수되는 원장입니다. 로컬 작성 초안에는 이 검사를 적용하지 않습니다.
        // Simulation은 실제 화물 등록이 아니며 기존 검증 자료의 누락값을 임의 보충하지 않습니다.
        if (_execution?.IsSimulation != true)
        {
            var missing = FreightCargoRegistrationPolicy.MissingFields(new(
                request.화물종류, request.화물중량Kg, request.화물길이Mm, request.화물폭Mm,
                request.화물높이Mm, request.픽업도로명주소, request.하차도로명주소));
            if (missing.Count > 0)
                return Result.Fail<화주운송의뢰응답>(new Error("종류·포장/깔판 포함 중량·길이/너비/높이·출도착지를 확인해 주세요.")
                    .WithMetadata("StatusCode", 400).WithMetadata("ErrorCode", "FreightCargoRequiredInformationMissing")
                    .WithMetadata("MissingFields", string.Join(",", missing)));
        }

        var loadingEvidence = 화주운송의뢰매퍼.CalculateLoadingEvidence(request);
        if (!loadingEvidence.유효여부)
            return Result.Fail<화주운송의뢰응답>(loadingEvidence.오류);

        var isServerAdmin = 주문자권한검사.IsServerAdmin(_currentUserAccessor);
        var shipperId = 주문자권한검사.ResolveShipperId(_currentUserAccessor, currentUserId, request.화주Id);
        var clientRequestId = 화주운송의뢰생성입력Policy.접수요청Id정규화(request.클라이언트요청Id);
        var isMobileSubmission = 화주운송의뢰생성입력Policy.모바일접수(clientRequestId);
        if (!string.IsNullOrWhiteSpace(clientRequestId))
        {
            var duplicate = await _db.화주운송의뢰
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => (isServerAdmin
                            ? r.화주Id == shipperId
                            : r.주문자UserId == currentUserId
                              || (r.주문자UserId == string.Empty && r.화주Id == currentUserId))
                         && r.클라이언트요청Id == clientRequestId,
                    cancellationToken);
            if (duplicate != null)
            {
                return Result.Fail<화주운송의뢰응답>("동일한 클라이언트요청Id로 이미 생성된 의뢰가 있습니다.");
            }
        }

        var paymentStatus = 화주운송의뢰생성입력Policy.결제상태정규화(request.결제상태);
        if (!AllowedPaymentStatuses.Contains(paymentStatus))
        {
            return Result.Fail<화주운송의뢰응답>($"결제상태 must be one of: {string.Join(", ", AllowedPaymentStatuses)}");
        }

        var settlement = 화주운송의뢰생성Factory.정산입력(request);

        var (pickupLat, pickupLng) = await ResolveCoordinatesAsync(request.픽업도로명주소, request.픽업상세주소, cancellationToken);
        var (dropoffLat, dropoffLng) = await ResolveCoordinatesAsync(request.하차도로명주소, request.하차상세주소, cancellationToken);

        // 새 모바일 접수와 생활배송의 동일 요청은 기존과 같은 안정 ID를 사용합니다.
        var requestId = 화주운송의뢰생성입력Policy.고정의뢰Id(currentUserId, clientRequestId, isMobileSubmission)
            ?? Guid.NewGuid().ToString();
        var entity = 화주운송의뢰생성Factory.생성(
            request, requestId, currentUserId, shipperId, clientRequestId, paymentStatus, settlement,
            (pickupLat, pickupLng), (dropoffLat, dropoffLng));
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        화주운송의뢰매퍼.ApplyLoadingEvidence(entity, loadingEvidence);

        var ledger = isMobileSubmission
            ? await _원장동기화Service.화주운송의뢰신규접수Async(entity, currentUserId, cancellationToken)
            : await _원장동기화Service.화주운송의뢰동기화Async(entity, currentUserId, cancellationToken);
        if (ledger is null)
        {
            return Result.Fail<화주운송의뢰응답>(isMobileSubmission
                ? "접수 결과를 아직 확인하지 못했습니다. 같은 접수 번호로 결과를 조회해 주세요. 기존 원장을 수정하거나 새 번호로 재등록하지 않았습니다."
                : "운송 의뢰 원장을 저장하지 못했습니다.");
        }

        var projectedEntity = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == entity.의뢰Id, cancellationToken);
        if (projectedEntity is null)
        {
            return Result.Fail<화주운송의뢰응답>(
                $"운송 의뢰 원장은 저장됐지만 RDB 업무 투영이 아직 완료되지 않았습니다. 원장Id={ledger.원장Id}");
        }

        entity = projectedEntity;
        await 화주운송의뢰매퍼.UpsertCargoRequirementAsync(_db, entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        운임구성? fareComposition = null;
        if (화주운송의뢰생성입력Policy.운임구성필요(request))
        {
            fareComposition = 화주운송의뢰생성Factory.운임구성생성(request, entity);
            fareComposition.CreatedAt = DateTime.UtcNow;
            fareComposition.UpdatedAt = DateTime.UtcNow;

            // 모의 수행의 참고 비용을 실제 기사 지급 예정액으로 영속하지 않습니다.
            if (request.SimulationFulfillment) fareComposition.기사지급예정운임 = null;
            await _db.운임구성.AddAsync(fareComposition, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            entity.운임구성Id = fareComposition.Id;
            await _db.SaveChangesAsync(cancellationToken);
            if (await _원장동기화Service.화주운송의뢰동기화Async(entity, currentUserId, cancellationToken) is null)
                return Result.Fail<화주운송의뢰응답>("의뢰와 견적은 저장됐지만 원장 동기화를 확인하지 못했습니다. 같은 의뢰를 다시 조회해 주세요.");
        }

        var handoff = await _operatingSystemHandoffService.인계Async(entity.의뢰Id, cancellationToken);
        return Result.Ok(화주운송의뢰매퍼.To응답(
            entity,
            operatingSystemHandoff: handoff,
            fareComposition: fareComposition));
    }

    private async Task<(decimal? lat, decimal? lng)> ResolveCoordinatesAsync(string? roadAddress, string? detailAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roadAddress))
        {
            return (null, null);
        }

        var query = roadAddress;
        if (!string.IsNullOrWhiteSpace(detailAddress))
        {
            query += " " + detailAddress;
        }

        var location = await _geocodingService.GeocodeAsync(query);
        if (!location.HasValue)
        {
            return (null, null);
        }

        return (location.Value.lat, location.Value.lng);
    }
}
