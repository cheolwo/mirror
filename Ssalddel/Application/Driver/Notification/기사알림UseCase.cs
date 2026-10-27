using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Driver.Notification;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services;

namespace Ssalddel.Application.Driver.Notification;

public interface I기사알림UseCase
{
    Task<Result<기사푸시토큰응답>> 푸시토큰조회Async(string? 기사Id);
    Task<Result<기사푸시토큰응답>> 푸시토큰등록Async(string? 기사Id, 기사푸시토큰등록요청? request);
    Task<Result> 푸시토큰삭제Async(string? 기사Id);
    Task<Result<기사알림설정응답>> 설정조회Async(string? 기사Id);
    Task<Result<기사알림설정응답>> 설정수정Async(string? 기사Id, 기사알림설정수정요청? request);
    Task<Result<기사알림함목록응답>> 알림함조회Async(string? 기사Id, CancellationToken cancellationToken = default);
    Task<Result<기사알림함항목응답>> 읽음처리Async(string? 기사Id, long notificationId, CancellationToken cancellationToken = default);
}

[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase("기사 알림 설정", Summary = "기사 추천, 배차, 운송 진행 알림을 받을 수 있도록 푸시토큰과 알림 설정을 관리합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Driver)]
[SsalddelUseCaseRelation(
    SsalddelUseCaseRelationKind.Include,
    "용달기사프로필UseCase",
    Condition = "기사 식별자와 기사 역할을 기준으로 알림 설정을 저장하는 경우",
    Summary = "기사 알림 설정은 기사 프로필과 역할 준비 상태를 전제로 합니다.")]
[SsalddelUseCaseRelation(
    SsalddelUseCaseRelationKind.Extend,
    "기사배차추천UseCase",
    Condition = "추천 화물, 배차 변경, 운송 진행 상태를 기사에게 알려야 하는 경우",
    Summary = "기사 알림 설정을 배차 추천과 운송 진행 알림 흐름으로 확장합니다.")]
public sealed class 기사알림UseCase : I기사알림UseCase
{
    private readonly IDriverPushTokenStore _pushTokenStore;
    private readonly IDriverNotificationSettingsStore _notificationSettingsStore;
    private readonly ILogger<기사알림UseCase> _logger;
    private readonly SsalddelContext _db;

    public 기사알림UseCase(
        IDriverPushTokenStore pushTokenStore,
        IDriverNotificationSettingsStore notificationSettingsStore,
        SsalddelContext db,
        ILogger<기사알림UseCase> logger)
    {
        _pushTokenStore = pushTokenStore;
        _notificationSettingsStore = notificationSettingsStore;
        _db = db;
        _logger = logger;
    }

    public async Task<Result<기사푸시토큰응답>> 푸시토큰조회Async(string? 기사Id)
    {
        if (string.IsNullOrWhiteSpace(기사Id)) return 인증실패<기사푸시토큰응답>();

        var token = await _pushTokenStore.GetAsync(기사Id);
        로그기록("PushTokenViewed", 기사Id);
        return new 기사푸시토큰응답
        {
            DriverId = 기사Id,
            HasToken = !string.IsNullOrWhiteSpace(token),
            PushToken = token ?? string.Empty
        };
    }

    public async Task<Result<기사푸시토큰응답>> 푸시토큰등록Async(string? 기사Id, 기사푸시토큰등록요청? request)
    {
        if (string.IsNullOrWhiteSpace(기사Id)) return 인증실패<기사푸시토큰응답>();
        if (request == null) return Result.Fail<기사푸시토큰응답>("request body is required");
        if (string.IsNullOrWhiteSpace(request.PushToken)) return Result.Fail<기사푸시토큰응답>("pushToken is required");

        var pushToken = request.PushToken.Trim();
        await _pushTokenStore.SetAsync(기사Id, pushToken);
        로그기록("PushTokenRegistered", 기사Id);
        return new 기사푸시토큰응답
        {
            DriverId = 기사Id,
            HasToken = true,
            PushToken = pushToken
        };
    }

    public async Task<Result> 푸시토큰삭제Async(string? 기사Id)
    {
        if (string.IsNullOrWhiteSpace(기사Id))
        {
            return Result.Fail(new Error("기사 인증 정보가 없습니다.").WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));
        }

        await _pushTokenStore.ClearAsync(기사Id);
        로그기록("PushTokenDeleted", 기사Id);
        return Result.Ok();
    }

    public async Task<Result<기사알림설정응답>> 설정조회Async(string? 기사Id)
    {
        if (string.IsNullOrWhiteSpace(기사Id)) return 인증실패<기사알림설정응답>();

        var settings = await _notificationSettingsStore.GetAsync(기사Id);
        로그기록("NotificationSettingsViewed", 기사Id);
        return 설정응답생성(기사Id, settings);
    }

    public async Task<Result<기사알림설정응답>> 설정수정Async(string? 기사Id, 기사알림설정수정요청? request)
    {
        if (string.IsNullOrWhiteSpace(기사Id)) return 인증실패<기사알림설정응답>();
        if (request == null) return Result.Fail<기사알림설정응답>("request body is required");

        var settings = new DriverNotificationSettings(
            request.배차추천알림사용,
            request.운전중푸시만사용,
            request.소리사용,
            request.진동사용,
            request.야간알림제한,
            request.정차후모아보기,
            request.음식배달추천알림사용,
            request.화물운송추천알림사용);

        await _notificationSettingsStore.SetAsync(기사Id, settings);
        로그기록("NotificationSettingsUpdated", 기사Id);
        return 설정응답생성(기사Id, settings);
    }

    public async Task<Result<기사알림함목록응답>> 알림함조회Async(
        string? 기사Id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(기사Id))
        {
            return 인증실패<기사알림함목록응답>();
        }

        var items = await _db.배차추천알림Outbox
            .AsNoTracking()
            .Where(item => item.기사Id == 기사Id)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new 기사알림함항목응답
            {
                Id = item.Id,
                종류 = "배차추천",
                제목 = item.제목,
                내용 = item.본문,
                발생시각 = item.CreatedAt,
                읽은시각 = item.읽은시각
            })
            .ToArrayAsync(cancellationToken);

        return Result.Ok(new 기사알림함목록응답
        {
            Items = items,
            안읽은알림수 = items.Count(item => !item.읽음)
        });
    }

    public async Task<Result<기사알림함항목응답>> 읽음처리Async(
        string? 기사Id,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(기사Id))
        {
            return 인증실패<기사알림함항목응답>();
        }

        var item = await _db.배차추천알림Outbox
            .FirstOrDefaultAsync(
                candidate => candidate.Id == notificationId && candidate.기사Id == 기사Id,
                cancellationToken);
        if (item is null)
        {
            return Result.Fail<기사알림함항목응답>(
                new Error("알림을 찾을 수 없습니다.")
                    .WithMetadata("StatusCode", StatusCodes.Status404NotFound));
        }

        item.읽은시각 ??= DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(new 기사알림함항목응답
        {
            Id = item.Id,
            종류 = "배차추천",
            제목 = item.제목,
            내용 = item.본문,
            발생시각 = item.CreatedAt,
            읽은시각 = item.읽은시각
        });
    }

    private 기사알림설정응답 설정응답생성(string 기사Id, DriverNotificationSettings settings)
        => new()
        {
            DriverId = 기사Id,
            배차추천알림사용 = settings.배차추천알림사용,
            음식배달추천알림사용 = settings.음식배달추천알림사용,
            화물운송추천알림사용 = settings.화물운송추천알림사용,
            운전중푸시만사용 = settings.운전중푸시만사용,
            소리사용 = settings.소리사용,
            진동사용 = settings.진동사용,
            야간알림제한 = settings.야간알림제한,
            정차후모아보기 = settings.정차후모아보기
        };

    private void 로그기록(string action, string 기사Id)
    {
        _logger.LogInformation(
            "Action={Action} DriverId={DriverId} Result={Result} TraceId={TraceId} OccurredAt={OccurredAt}",
            action,
            기사Id,
            "Success",
            System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
            DateTime.UtcNow);
    }

    private static Result<T> 인증실패<T>()
        => Result.Fail<T>(new Error("기사 인증 정보가 없습니다.").WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));
}
