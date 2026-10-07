using FluentResults;
using Ssalddel.Contracts.Common.Dispatch;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Services.Community;
using Ssalddel.Services.Operations;
using Ssalddel.Services.Privacy;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.External.Google;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Recommendation;
namespace NeighborhoodExchangePreview;
// Synthetic route, fare, handoff notification and continuity evidence only. Never use in production DI.
internal static class FlexibleDeliveryFixtures
{
    internal sealed class FixtureGeocoding : IGeocodingService
    {
        public bool Resolve { get; set; } = true;
        public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address)
            => Task.FromResult<(decimal lat, decimal lng)?>(!Resolve ? null : address.Contains("픽업", StringComparison.Ordinal)
                ? (37.5800m, 127.0830m) : (37.5900m, 127.0900m));
    }
    internal sealed class FixtureFare : I화주운송기준운임Service
    {
        public 화주운송기준운임견적요청? LastInput { get; private set; }
        public Task<Result<화주운송기준운임견적응답>> 견적Async(화주운송기준운임견적요청 request, CancellationToken cancellationToken = default)
        {
            LastInput = request;
            return Task.FromResult(Result.Ok(new 화주운송기준운임견적응답
            {
                차량종류 = "오토바이", 예상거리Km = 3.2m, 기본운임 = 5000m, Km당단가 = 1000m,
                거리운임 = 3200m, 최소운임 = 5000m, 최종운임 = 8200m, 거리계산방식 = "FixtureRoute", 단가출처 = "FixtureRate"
            }));
        }
    }
    internal sealed class FreightPolicy : IOperatingMarketFreightWorkflowPolicy
    {
        public string MarketCode => "KR";
        public OperatingMarketFreightWorkflowDecision Evaluate(OperatingMarketFreightWorkflowRequest request) => new() { CanProceed = true };
    }
    internal sealed class Handoff : I화주운송의뢰화물운송인계Service
    {
        public Task<운영체제업무인계Dto> 인계Async(string requestId, CancellationToken cancellationToken = default) => Task.FromResult(new 운영체제업무인계Dto());
    }
    internal sealed class FixtureSender(의뢰생성CommandHandler create, 화주운송의뢰현장지급처리CommandHandler onsite, 의뢰단건조회QueryHandler query) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object? response;
            switch (request)
            {
                case 의뢰생성Command command:
                    response = (object)await create.Handle(command, cancellationToken);
                    break;
                case 화주운송의뢰현장지급처리Command command:
                    response = (object)await onsite.Handle(command, cancellationToken);
                    break;
                case 의뢰단건조회Query detail:
                    response = (object?)await query.Handle(detail, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException(request.GetType().Name);
            }
            return (TResponse)response!;
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }    internal sealed class FixtureContinuity : I화물연속배차UseCase
    {
        public bool FailCompletion { get; set; }
        public Task<화물연속배차상태Dto> 조회Async(string 기사Id, CancellationToken cancellationToken = default) => Task.FromResult(new 화물연속배차상태Dto());
        public Task<화물연속배차상태Dto> 의사변경Async(string 기사Id, 화물연속배차의사변경요청 요청, CancellationToken cancellationToken = default) => 조회Async(기사Id, cancellationToken);
        public Task<화물다음콜예약Dto?> 추천탐색Async(string 기사Id, CancellationToken cancellationToken = default) => Task.FromResult<화물다음콜예약Dto?>(null);
        public Task<화물예약검증결과> 수락예약검증Async(string 기사Id, string 의뢰Id, string? reservationId, long? expectedRevision, CancellationToken cancellationToken = default)
            => Task.FromResult(new 화물예약검증결과(true));
        public Task 수락완료Async(string 기사Id, string 의뢰Id, CancellationToken cancellationToken = default)
            => FailCompletion ? Task.FromException(new InvalidOperationException("격리된 SQL 트랜잭션 실패")) : Task.CompletedTask;
        public Task 시간약속잠금Async(string 기사Id, 화주운송의뢰 의뢰, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task 완료기록Async(string 기사Id, DateTime 완료시각Utc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string 기사Id, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);
        public Task<화물경로위험Dto> 현재위험조회Async(string 기사Id, CancellationToken cancellationToken = default) => Task.FromResult(new 화물경로위험Dto());
    }
}
