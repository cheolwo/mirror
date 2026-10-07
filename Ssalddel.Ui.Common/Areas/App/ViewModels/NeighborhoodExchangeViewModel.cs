using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>페이지별 수명으로 목록·작성·상세 표시를 조립합니다. 권한은 서버 응답으로 판단합니다.</summary>
public sealed class NeighborhoodExchangeViewModel(INeighborhoodExchangeClient client, INeighborhoodExchangeMapClient? mapClient = null) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private long _readGeneration;
    private bool _disposed;
    private long _regionGeneration;
    public IReadOnlyList<NeighborhoodPublicRegionDto> WriteRegions { get; private set; } = [];
    public string? PublicNeighborhoodRegionKey { get; set; }
    public bool RegionsLoading { get; private set; }
    public string? RegionsError { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public string? IntentFilter { get; private set; }
    public int Page { get; private set; } = 1;
    public int TotalCount { get; private set; }
    public bool HasNext => Page * NeighborhoodExchange.PageSize < TotalCount;
    public IReadOnlyList<PlatformCommunityPostResponse> Items { get; private set; } = [];
    public PlatformCommunityPostResponse? Post { get; private set; }
    public IReadOnlyList<PlatformCommunityPostCommentResponse> Comments { get; private set; } = [];
    public bool CommentsLoaded { get; private set; }
    public string Intent { get; set; } = NeighborhoodExchange.Offer;
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Password { get; set; } = "";
    public string CommentBody { get; set; } = "";
    public string CommentPassword { get; set; } = "";
    public string DeletePassword { get; set; } = "";
    private readonly Dictionary<long, string> _commentDeletePasswords = [];
    public string GetCommentDeletePassword(long id) => _commentDeletePasswords.GetValueOrDefault(id, "");
    public void SetCommentDeletePassword(long id, string? value) => _commentDeletePasswords[id] = value ?? "";

    public async Task InitializeWriteRegionAsync(string? initialRegionKey)
    {
        if (_disposed) return;
        var generation = ++_regionGeneration; RegionsLoading = true; RegionsError = null; Changed();
        try
        {
            if (mapClient is null) { RegionsError = "동네 선택을 확인할 수 없습니다. 동네 없이 글을 올릴 수 있습니다."; return; }
            var result = await mapClient.RegionsAsync(_lifetime.Token);
            if (_disposed || generation != _regionGeneration) return;
            WriteRegions = result.Items.Where(region => region.PrecisionCode == "neighborhood").ToArray();
            PublicNeighborhoodRegionKey = WriteRegions.Any(region => region.RegionKey == initialRegionKey) ? initialRegionKey : null;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed && generation == _regionGeneration) RegionsError = "동네 목록을 불러오지 못했습니다. 동네 없이 글을 올리거나 다시 시도해 주세요."; }
        finally { if (!_disposed && generation == _regionGeneration) { RegionsLoading = false; Changed(); } }
    }

    public async Task LoadListAsync(string? intent, int page = 1)
    {
        var generation = ++_readGeneration;
        IntentFilter = NeighborhoodExchange.IsIntent(intent) ? intent : null;
        Page = Math.Max(1, page); IsLoading = true; Error = null; Items = []; TotalCount = 0; Changed();
        try
        {
            var result = await client.ListAsync(IntentFilter, Page, _lifetime.Token);
            if (generation != _readGeneration) return;
            Items = result.Items.Where(NeighborhoodExchange.IsExchange).ToArray();
            TotalCount = result.TotalCount;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (generation == _readGeneration) Error = ReadError(ex); }
        finally { if (generation == _readGeneration) { IsLoading = false; Changed(); } }
    }

    public async Task LoadDetailAsync(long id)
    {
        var generation = ++_readGeneration;
        Post = null; Comments = []; CommentsLoaded = false;
        IsLoading = true; Error = null; Notice = null; Changed();
        try
        {
            var post = await client.ReadAsync(id, _lifetime.Token);
            if (generation != _readGeneration) return;
            if (post is null || !NeighborhoodExchange.IsExchange(post))
            { Error = "이 글을 찾을 수 없습니다. 삭제되었거나 생활 교류 글이 아닐 수 있습니다."; return; }
            Post = post;
            var comments = await client.CommentsAsync(id, _lifetime.Token);
            if (generation != _readGeneration) return;
            Comments = comments; CommentsLoaded = true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (generation == _readGeneration) Error = ReadError(ex); }
        finally { if (generation == _readGeneration) { IsLoading = false; Changed(); } }
    }

    public async Task<long?> PublishAsync()
    {
        if (IsSending) return null;
        Error = null;
        if (!NeighborhoodExchange.IsIntent(Intent) || string.IsNullOrWhiteSpace(Title) || Title.Trim().Length > 160
            || string.IsNullOrWhiteSpace(Body) || Body.Trim().Length > 4000 || !ValidPassword(Password))
        { Error = "구분, 제목(160자 이하), 설명(4,000자 이하), 비밀번호(4~100자)를 확인해 주세요."; Changed(); return null; }
        if (!string.IsNullOrWhiteSpace(PublicNeighborhoodRegionKey) && !WriteRegions.Any(region => region.RegionKey == PublicNeighborhoodRegionKey))
        { Error = "목록에서 공개할 동네를 선택해 주세요."; Changed(); return null; }
        IsSending = true; Changed();
        try
        {
            var result = await client.PublishAsync(new()
            {
                RoleTag = Intent, Title = Title.Trim(), Body = Body.Trim(), Password = Password.Trim(),
                Nickname = "이웃", OriginalLanguageCode = "ko", PublicNeighborhoodRegionKey = string.IsNullOrWhiteSpace(PublicNeighborhoodRegionKey) ? null : PublicNeighborhoodRegionKey
            }, _lifetime.Token);
            if (result is null || result.Id <= 0) { Error = UncertainWrite; return null; }
            Title = ""; Body = ""; Password = "";
            return result.Id;
        }
        catch (Exception ex) { Error = WriteError(ex); return null; }
        finally { IsSending = false; Changed(); }
    }

    public async Task CommentAsync()
    {
        if (IsSending || Post is null || !CommentsLoaded) return;
        Error = null; Notice = null;
        if (string.IsNullOrWhiteSpace(CommentBody) || CommentBody.Trim().Length > 1000 || !ValidPassword(CommentPassword))
        { Error = "문의 내용(1,000자 이하)과 삭제 비밀번호(4~100자)를 확인해 주세요."; Changed(); return; }
        var postId = Post.Id;
        IsSending = true; Changed();
        try
        {
            var result = await client.CommentAsync(postId, new()
            { Body = CommentBody.Trim(), Password = CommentPassword.Trim(), Nickname = "이웃" }, _lifetime.Token);
            if (result is null) { Error = UncertainWrite; return; }
            Comments = Comments.Append(result).ToArray(); CommentBody = ""; CommentPassword = "";
            Notice = "공개 문의를 남겼습니다.";
        }
        catch (Exception ex) { Error = WriteError(ex); }
        finally { IsSending = false; Changed(); }
    }

    public async Task<bool> DeleteAsync()
    {
        if (IsSending || Post is not { CanDelete: true }) return false;
        Error = null; Notice = null;
        if (Post.DeleteRequiresPassword && !ValidPassword(DeletePassword))
        { Error = "글 작성 때 정한 비밀번호를 입력해 주세요."; Changed(); return false; }
        IsSending = true; Changed();
        try { await client.DeleteAsync(Post.Id, DeletePassword, _lifetime.Token); DeletePassword = ""; return true; }
        catch (Exception ex) { Error = WriteError(ex); return false; }
        finally { IsSending = false; Changed(); }
    }

    public async Task DeleteCommentAsync(long id)
    {
        if (IsSending || Post is null || !Comments.Any(c => c.Id == id)) return;
        Error = null; Notice = null;
        var password = GetCommentDeletePassword(id);
        if (!ValidPassword(password))
        { Error = "댓글 작성 때 정한 비밀번호를 입력해 주세요."; Changed(); return; }
        IsSending = true; Changed();
        try
        {
            await client.DeleteCommentAsync(Post.Id, id, password, _lifetime.Token);
            Comments = Comments.Where(c => c.Id != id).ToArray(); _commentDeletePasswords.Remove(id);
            Notice = "댓글을 삭제했습니다.";
        }
        catch (Exception ex) { Error = WriteError(ex); }
        finally { IsSending = false; Changed(); }
    }

    public async Task ReportCommentAsync(long id)
    {
        if (IsSending || Post is null || !Comments.Any(c => c.Id == id)) return;
        Error = null; Notice = null; IsSending = true; Changed();
        try { await client.ReportCommentAsync(id, _lifetime.Token); Notice = "신고를 접수했습니다."; }
        catch (Exception ex) { Error = WriteError(ex); }
        finally { IsSending = false; Changed(); }
    }

    private const string UncertainWrite = "등록 결과를 확인하지 못했습니다. 목록이나 댓글을 새로 확인한 뒤 다시 시도해 주세요. 입력 내용은 유지됩니다.";
    private static bool ValidPassword(string value) => value.Trim().Length is >= 4 and <= 100;
    private static string ReadError(Exception ex) => ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound }
        ? "글을 찾을 수 없습니다. 삭제되었을 수 있습니다." : "불러오지 못했습니다. 연결을 확인하고 다시 시도해 주세요.";
    private static string WriteError(Exception ex) => ex is HttpRequestException http ? http.StatusCode switch
    {
        HttpStatusCode.BadRequest => "입력 내용을 확인해 주세요. 비밀번호나 글/댓글 길이가 올바르지 않을 수 있습니다.",
        HttpStatusCode.Unauthorized => "로그인이 필요한 작업입니다. 기존 로그인 화면에서 로그인한 뒤 다시 선택해 주세요.",
        HttpStatusCode.Forbidden => "이 작업의 권한이 없거나 비밀번호가 맞지 않습니다.",
        HttpStatusCode.NotFound => "글 또는 댓글이 삭제되었습니다. 다시 불러와 주세요.",
        HttpStatusCode.TooManyRequests => "잠시 후 다시 시도해 주세요.",
        _ => UncertainWrite
    } : UncertainWrite;
    private void Changed() => OnPropertyChanged(string.Empty);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _readGeneration++; _lifetime.Cancel(); _lifetime.Dispose();
        Password = ""; CommentPassword = ""; DeletePassword = ""; _commentDeletePasswords.Clear();
    }
}
