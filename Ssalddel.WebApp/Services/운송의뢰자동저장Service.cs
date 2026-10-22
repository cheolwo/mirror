using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Microsoft.JSInterop;

namespace Ssalddel.WebApp.Services;

public sealed class 운송의뢰자동저장Service
{
    private const string StorageKey = "ssalddel.web.shipper-request-draft.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _jsRuntime;

    public 운송의뢰자동저장Service(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public Task SaveAsync(운송의뢰작성ViewModel viewModel, CancellationToken cancellationToken = default)
        => SaveAsync(viewModel.ToDraft(), cancellationToken);

    public async Task SaveAsync(운송모델작성Draft draft, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(draft, JsonOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, json);
    }

    public async Task<운송모델작성Draft?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<운송모델작성Draft>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
    }

    // 새 Web 접수는 다른 계정의 주소 초안을 복원하거나 미확정 원문을 덮지 않습니다.
    internal static string OwnerKey(string owner) => StorageKey + "."
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner))).ToLowerInvariant();

    public async Task SaveForOwnerAsync(운송모델작성Draft draft, string owner, CancellationToken ct = default)
    {
        await using var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ct, "./js/shipper-request-recovery.js");
        await module.InvokeVoidAsync("saveDraft", ct, 화주운송의뢰등록Service.Key(owner), OwnerKey(owner), JsonSerializer.Serialize(draft, JsonOptions));
    }

    public async Task<운송모델작성Draft?> LoadForOwnerAsync(string owner, CancellationToken ct = default)
    {
        var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ct, OwnerKey(owner));
        if (string.IsNullOrWhiteSpace(json)) return null;
        var draft = JsonSerializer.Deserialize<운송모델작성Draft>(json, JsonOptions)
            ?? throw new InvalidDataException("계정 초안을 읽을 수 없습니다.");
        if (DateTime.UtcNow - draft.작성일시.ToUniversalTime() < Ssalddel.Ui.Common.Areas.App.Services.WorkflowRecoveryPolicy.Retention)
            return draft;
        await ClearForOwnerAsync(owner, ct);
        return null;
    }

    public Task ClearForOwnerAsync(string owner, CancellationToken ct = default)
        => _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ct, OwnerKey(owner)).AsTask();
}
