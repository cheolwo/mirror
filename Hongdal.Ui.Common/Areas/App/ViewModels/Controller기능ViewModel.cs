using Hongdal.Ui.Common.Areas.App.Services;
using System.Text.RegularExpressions;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record Controller기능정의(string Key, string 표시명, string BasePath);

public sealed record ControllerApi경로요청(
    string 상대경로 = "",
    IReadOnlyDictionary<string, string>? 경로값 = null);

public sealed record ControllerApi요청<TRequest>(
    string 상대경로,
    TRequest 요청,
    IReadOnlyDictionary<string, string>? 경로값 = null);

/// <summary>
/// 서버 Controller 하나의 BasePath와 그 아래에 만들어진 타입드 API 작업들을 묶습니다.
/// 페이지는 생성한 작업 ViewModel을 속성으로 보관하여 조립합니다.
/// </summary>
public sealed class Controller기능ViewModel : 조립ViewModelBase
{
    private readonly IHongdalJsonApiClient _client;

    public Controller기능ViewModel(IHongdalJsonApiClient client, Controller기능정의 definition)
    {
        _client = client;
        Key = definition.Key;
        표시명 = definition.표시명;
        BasePath = definition.BasePath.TrimEnd('/');
    }

    public string Key { get; }
    public string 표시명 { get; }
    public string BasePath { get; }

    public Api작업ViewModel<TResult?> 조회<TResult>(
        string 상대경로 = "",
        string? 작업명 = null,
        bool notFound허용 = true)
        => 하위ViewModel등록(new Api작업ViewModel<TResult?>(
            cancellationToken => _client.GetAsync<TResult>(
                경로(상대경로),
                작업명 ?? $"{표시명} 조회",
                notFound허용,
                cancellationToken)), 수명소유: true);

    public Api작업ViewModel<ControllerApi경로요청, TResult?> 경로조회<TResult>(
        string? 작업명 = null,
        bool notFound허용 = true)
        => 하위ViewModel등록(new Api작업ViewModel<ControllerApi경로요청, TResult?>(
            (request, cancellationToken) => _client.GetAsync<TResult>(
                경로(request.상대경로, request.경로값),
                작업명 ?? $"{표시명} 조회",
                notFound허용,
                cancellationToken)), 수명소유: true);

    public Api작업ViewModel<TRequest, TResult?> 명령<TRequest, TResult>(
        HttpMethod method,
        string 상대경로 = "",
        string? 작업명 = null)
        => 하위ViewModel등록(new Api작업ViewModel<TRequest, TResult?>(
            (request, cancellationToken) => _client.SendAsync<TRequest, TResult>(
                method,
                경로(상대경로),
                request,
                작업명 ?? $"{표시명} 명령",
                cancellationToken: cancellationToken)), 수명소유: true);

    public Api작업ViewModel<ControllerApi요청<TRequest>, TResult?> 경로명령<TRequest, TResult>(
        HttpMethod method,
        string? 작업명 = null)
        => 하위ViewModel등록(new Api작업ViewModel<ControllerApi요청<TRequest>, TResult?>(
            (request, cancellationToken) => _client.SendAsync<TRequest, TResult>(
                method,
                경로(request.상대경로, request.경로값),
                request.요청,
                작업명 ?? $"{표시명} 명령",
                cancellationToken: cancellationToken)), 수명소유: true);

    public Api작업ViewModel<TRequest, Api작업완료> 명령<TRequest>(
        HttpMethod method,
        string 상대경로 = "",
        string? 작업명 = null)
        => 하위ViewModel등록(new Api작업ViewModel<TRequest, Api작업완료>(
            async (request, cancellationToken) =>
            {
                await _client.SendAsync(
                    method,
                    경로(상대경로),
                    request,
                    작업명 ?? $"{표시명} 명령",
                    cancellationToken);
                return Api작업완료.값;
            }), 수명소유: true);

    public Api작업ViewModel<ControllerApi경로요청, TResult?> 경로명령<TResult>(
        HttpMethod method,
        string? 작업명 = null)
        => 하위ViewModel등록(new Api작업ViewModel<ControllerApi경로요청, TResult?>(
            (request, cancellationToken) => _client.SendAsync<TResult>(
                method,
                경로(request.상대경로, request.경로값),
                작업명 ?? $"{표시명} 명령",
                cancellationToken: cancellationToken)), 수명소유: true);

    public Api작업ViewModel<ControllerApi경로요청, Api작업완료> 경로명령(
        HttpMethod method,
        string? 작업명 = null)
        => 하위ViewModel등록(new Api작업ViewModel<ControllerApi경로요청, Api작업완료>(
            async (request, cancellationToken) =>
            {
                await _client.SendAsync(
                    method,
                    경로(request.상대경로, request.경로값),
                    작업명 ?? $"{표시명} 명령",
                    cancellationToken);
                return Api작업완료.값;
            }), 수명소유: true);

    public string 경로(
        string? 상대경로 = null,
        IReadOnlyDictionary<string, string>? 경로값 = null)
    {
        var trimmed = 상대경로?.Trim();
        var path = string.IsNullOrWhiteSpace(trimmed)
            ? BasePath
            : trimmed.StartsWith('?')
                ? $"{BasePath}{trimmed}"
                : $"{BasePath}/{trimmed.TrimStart('/')}";

        if (경로값 is not null)
        {
            foreach (var (key, value) in 경로값)
            {
                var pattern = $@"\{{{Regex.Escape(key)}(?::[^}}]+)?\}}";
                path = Regex.Replace(
                    path,
                    pattern,
                    Uri.EscapeDataString(value),
                    RegexOptions.IgnoreCase);
            }
        }

        var unresolved = Regex.Match(path, @"\{(?<key>[^}:]+)(?::[^}]+)?\}");
        if (unresolved.Success)
        {
            throw new InvalidOperationException(
                $"{표시명} API 경로값 '{unresolved.Groups["key"].Value}'이(가) 필요합니다.");
        }

        return path;
    }
}

public abstract class Controller기능모음ViewModel : 조립ViewModelBase
{
    private readonly IReadOnlyDictionary<string, Controller기능ViewModel> _controllers;

    protected Controller기능모음ViewModel(
        IHongdalJsonApiClient client,
        IEnumerable<Controller기능정의> definitions)
    {
        _controllers = definitions
            .Select(definition => 하위ViewModel등록(
                new Controller기능ViewModel(client, definition),
                수명소유: true))
            .ToDictionary(controller => controller.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, Controller기능ViewModel> 컨트롤러 => _controllers;

    public Controller기능ViewModel this[string key]
        => _controllers.TryGetValue(key, out var controller)
            ? controller
            : throw new KeyNotFoundException($"등록되지 않은 Controller 기능입니다: {key}");
}
