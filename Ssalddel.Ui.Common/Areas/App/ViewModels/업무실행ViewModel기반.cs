using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>API 실패를 화면에서 상태 코드와 복구 방법별로 다룰 수 있게 정규화한 오류입니다.</summary>
public sealed record Api작업오류(
    string 코드,
    string 메시지,
    int? Http상태코드 = null,
    string? TraceId = null,
    bool 재시도가능 = false,
    bool 충돌 = false,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? 필드오류 = null,
    string? 실패분류Code = null,
    string? 책임역할Code = null,
    bool 상태전체재조회필요 = false,
    string? 재시도정책Code = null,
    IReadOnlyList<string>? 복구가능행동 = null,
    long? 현재Revision = null,
    DateTime? 재시도가능시각Utc = null)
{
    public static Api작업오류 변환(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is SsalddelApiException apiException)
        {
            var statusCode = apiException.StatusCode;
            return new Api작업오류(
                !string.IsNullOrWhiteSpace(apiException.ErrorCode)
                    ? apiException.ErrorCode
                    : statusCode == 409 ? "concurrency-conflict" : $"http-{statusCode}",
                apiException.Message,
                statusCode,
                apiException.TraceId,
                statusCode is 408 or 425 or 429 || statusCode >= 500,
                statusCode == 409,
                apiException.FieldErrors.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value,
                    StringComparer.OrdinalIgnoreCase),
                apiException.FailureClassCode,
                apiException.ResponsibilityRoleCode,
                apiException.RequiresStateRefresh,
                apiException.RetryPolicyCode,
                apiException.AvailableRecoveryActions,
                apiException.CurrentRevision,
                apiException.RetryAfterUtc);
        }

        var transportFailure = exception as HttpRequestException
                               ?? exception.InnerException as HttpRequestException;
        var timeoutFailure = exception is TimeoutException
                             || exception is TaskCanceledException
                             || exception.InnerException is TimeoutException
                             || exception.InnerException is TaskCanceledException;
        if (transportFailure is not null || timeoutFailure)
        {
            return new Api작업오류(
                timeoutFailure ? "timeout" : "transport-failure",
                exception.Message,
                transportFailure?.StatusCode is { } status ? (int)status : null,
                재시도가능: true,
                실패분류Code: 업무실패분류Codes.일시기술장애,
                재시도정책Code: 업무재시도정책Codes.동일멱등요청1회,
                복구가능행동: [업무복구행동Ids.동일요청멱등재시도]);
        }

        return new Api작업오류(
            "view-model-operation-failed",
            exception.Message);
    }
}

/// <summary>
/// 선택한 원장·주문·업무가 바뀌면 이전 비동기 요청을 취소하고,
/// 늦게 도착한 응답이 새 선택 상태를 덮지 못하도록 세대를 관리합니다.
/// </summary>
public sealed class 업무선택ContextViewModel : ObservableObject, IDisposable
{
    private string? _대상Key;
    private long _세대;
    private CancellationTokenSource _선택취소 = new();

    public string? 대상Key
    {
        get => _대상Key;
        private set => SetProperty(ref _대상Key, value);
    }

    public long 세대
    {
        get => _세대;
        private set => SetProperty(ref _세대, value);
    }

    public bool 선택됨 => !string.IsNullOrWhiteSpace(대상Key);

    public bool 선택(string? targetKey)
    {
        var normalized = string.IsNullOrWhiteSpace(targetKey) ? null : targetKey.Trim();
        if (string.Equals(대상Key, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _선택취소.Cancel();
        _선택취소.Dispose();
        _선택취소 = new CancellationTokenSource();
        대상Key = normalized;
        세대++;
        OnPropertyChanged(nameof(선택됨));
        return true;
    }

    public 업무요청Scope 요청시작(CancellationToken cancellationToken = default)
        => new(this, 대상Key, 세대, _선택취소.Token, cancellationToken);

    public bool 현재요청인가(string? targetKey, long generation)
        => generation == 세대
           && string.Equals(대상Key, targetKey, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _선택취소.Cancel();
        _선택취소.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class 업무요청Scope : IDisposable
{
    private readonly 업무선택ContextViewModel _owner;
    private readonly CancellationTokenSource _linkedCancellation;

    internal 업무요청Scope(
        업무선택ContextViewModel owner,
        string? targetKey,
        long generation,
        CancellationToken selectionToken,
        CancellationToken externalToken)
    {
        _owner = owner;
        대상Key = targetKey;
        세대 = generation;
        _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(selectionToken, externalToken);
    }

    public string? 대상Key { get; }
    public long 세대 { get; }
    public CancellationToken 취소Token => _linkedCancellation.Token;
    public bool 현재요청 => _owner.현재요청인가(대상Key, 세대);

    public void Dispose() => _linkedCancellation.Dispose();
}

/// <summary>필드 검증과 수정 여부를 API 요청 DTO와 분리해 관리하는 입력 ViewModel 기반입니다.</summary>
public abstract class 업무입력ViewModelBase : ObservableValidator
{
    private bool _변경됨;

    public bool 변경됨
    {
        get => _변경됨;
        private set => SetProperty(ref _변경됨, value);
    }

    public bool 유효함 => !HasErrors;
    public bool 저장가능 => 변경됨 && 유효함;

    protected bool 입력값설정<T>(
        ref T field,
        T value,
        bool validate = true,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        변경됨 = true;
        if (validate && propertyName is not null)
        {
            ValidateProperty(value, propertyName);
        }

        OnPropertyChanged(nameof(유효함));
        OnPropertyChanged(nameof(저장가능));
        return true;
    }

    public bool 전체검증()
    {
        ValidateAllProperties();
        OnPropertyChanged(nameof(유효함));
        OnPropertyChanged(nameof(저장가능));
        return 유효함;
    }

    public void 변경확정()
    {
        변경됨 = false;
        OnPropertyChanged(nameof(저장가능));
    }

    public void 검증초기화()
    {
        ClearErrors();
        OnPropertyChanged(nameof(유효함));
        OnPropertyChanged(nameof(저장가능));
    }
}
