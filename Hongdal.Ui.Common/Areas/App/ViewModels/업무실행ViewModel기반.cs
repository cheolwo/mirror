using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>API 실패를 화면에서 상태 코드와 복구 방법별로 다룰 수 있게 정규화한 오류입니다.</summary>
public sealed record Api작업오류(
    string 코드,
    string 메시지,
    int? Http상태코드 = null,
    string? TraceId = null,
    bool 재시도가능 = false,
    bool 충돌 = false,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? 필드오류 = null)
{
    public static Api작업오류 변환(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is HongdalApiException apiException)
        {
            var statusCode = apiException.StatusCode;
            return new Api작업오류(
                statusCode == 409 ? "concurrency-conflict" : $"http-{statusCode}",
                apiException.Message,
                statusCode,
                apiException.TraceId,
                statusCode is 408 or 425 or 429 || statusCode >= 500,
                statusCode == 409,
                apiException.FieldErrors.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value,
                    StringComparer.OrdinalIgnoreCase));
        }

        return new Api작업오류(
            exception is TimeoutException ? "timeout" : "view-model-operation-failed",
            exception.Message,
            재시도가능: exception is TimeoutException or HttpRequestException);
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
