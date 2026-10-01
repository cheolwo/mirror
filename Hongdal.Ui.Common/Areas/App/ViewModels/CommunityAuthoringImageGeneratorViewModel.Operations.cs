using Hongdal.Contracts.Common.Content;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed partial class CommunityAuthoringImageGeneratorViewModel
{
    public async Task<bool> GenerateSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGenerateSelected)
        {
            SetStatus("생성할 새 문맥을 선택해 주세요.", CommunityComposerMessageKind.Warning);
            return false;
        }

        var targets = Items
            .Where(item => item.IsIncluded && item.NeedsGeneration && item.CanGenerate)
            .OrderBy(item => item.Sequence)
            .ToArray();
        IsBusy = true;
        try
        {
            var result = await ExecuteBatchAsync(targets, GenerateItemCoreAsync, cancellationToken);
            SetStatus(
                result.Failed == 0
                    ? $"선택한 {result.Succeeded}개 문맥의 Kie.ai 생성 작업을 등록했습니다."
                    : $"{result.Succeeded}개 작업을 등록했고 {result.Failed}개는 실패했습니다. 실패 항목을 확인해 주세요.",
                result.Failed == 0 ? CommunityComposerMessageKind.Success : CommunityComposerMessageKind.Warning);
            return result.Failed == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> GenerateAsync(
        CommunityAuthoringImagePromptItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsBusy || !item.CanGenerate)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var generated = await GenerateItemCoreAsync(item, cancellationToken);
            SetStatus(
                generated
                    ? $"{item.Sequence}번 문맥의 Kie.ai 생성 작업을 등록했습니다."
                    : $"{item.Sequence}번 문맥의 이미지 생성 작업을 등록하지 못했습니다.",
                generated ? CommunityComposerMessageKind.Success : CommunityComposerMessageKind.Error);
            return generated;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> RefreshPendingAsync(CancellationToken cancellationToken = default)
    {
        var targets = Items
            .Where(item => item.CanRefresh)
            .OrderBy(item => item.Sequence)
            .ToArray();
        if (IsBusy || targets.Length == 0)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var result = await ExecuteBatchAsync(targets, RefreshItemCoreAsync, cancellationToken);
            SetStatus(
                result.Failed == 0
                    ? $"{result.Succeeded}개 이미지 작업의 최신 상태를 확인했습니다."
                    : $"{result.Succeeded}개 상태를 확인했고 {result.Failed}개 조회는 실패했습니다.",
                result.Failed == 0 ? CommunityComposerMessageKind.Info : CommunityComposerMessageKind.Warning);
            return result.Failed == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> RefreshAsync(
        CommunityAuthoringImagePromptItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsBusy || !item.CanRefresh)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var refreshed = await RefreshItemCoreAsync(item, cancellationToken);
            SetStatus(
                refreshed ? item.Task?.Message : $"{item.Sequence}번 이미지 작업 상태를 확인하지 못했습니다.",
                refreshed ? ResolveTaskMessageKind(item.Task) : CommunityComposerMessageKind.Error);
            return refreshed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool TogglePostSelection(CommunityAuthoringImagePromptItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsSelectedForPost)
        {
            item.SetSelectedForPost(false);
            SetStatus("게시글 이미지 선택을 해제했습니다.", CommunityComposerMessageKind.Info);
            return true;
        }

        if (!item.CanSelectForPost)
        {
            return false;
        }

        item.SetSelectedForPost(true);
        SetStatus(
            $"{SelectedForPostCount}개 이미지를 선택했습니다. 글을 저장하면 문맥 순서대로 첨부합니다.",
            CommunityComposerMessageKind.Success);
        return true;
    }

    public async Task<bool> AttachSelectedAsync(
        long postId,
        string? password,
        int maxAttachmentCount = CommunityAuthoringImageLimits.MaximumPlannedImages,
        CancellationToken cancellationToken = default)
    {
        var selectedTargets = Items
            .Where(item => item.IsSelectedForPost && item.Task is { IsSuccess: true })
            .OrderBy(item => item.Sequence)
            .ToArray();
        if (selectedTargets.Length == 0)
        {
            return true;
        }

        var availableCount = Math.Clamp(
            maxAttachmentCount,
            0,
            CommunityAuthoringImageLimits.MaximumPlannedImages);
        var targets = selectedTargets.Take(availableCount).ToArray();
        var skipped = selectedTargets.Length - targets.Length;
        if (targets.Length == 0)
        {
            SetStatus(
                $"게시글당 사진은 최대 {CommunityAuthoringImageLimits.MaximumPlannedImages}개입니다. 이미 선택한 사진 때문에 생성 이미지 {skipped}개는 첨부하지 않았습니다.",
                CommunityComposerMessageKind.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatus("게시글 비밀번호를 확인하지 못해 생성 이미지를 첨부하지 못했습니다.", CommunityComposerMessageKind.Error);
            return false;
        }

        IsBusy = true;
        var attached = 0;
        var failed = 0;
        try
        {
            foreach (var item in targets)
            {
                try
                {
                    await _client.AttachAuthoringImageAsync(
                        item.Task!.JobCode,
                        postId,
                        password,
                        cancellationToken);
                    item.SetSelectedForPost(false);
                    item.ClearError();
                    attached++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    item.SetError($"게시글 첨부 실패: {exception.Message}");
                    failed++;
                }
            }

            SetStatus(
                failed == 0 && skipped == 0
                    ? $"생성 이미지 {attached}개를 문맥 순서대로 게시글 사진에 첨부했습니다."
                    : $"게시글은 저장됐지만 생성 이미지 {attached}개만 첨부됐고 실패 {failed}개, 첨부 제한으로 보류 {skipped}개가 남았습니다.",
                failed == 0 && skipped == 0
                    ? CommunityComposerMessageKind.Success
                    : CommunityComposerMessageKind.Warning);
            return failed == 0 && skipped == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task<ImageBatchResult> ExecuteBatchAsync(
        IReadOnlyList<CommunityAuthoringImagePromptItemViewModel> targets,
        Func<CommunityAuthoringImagePromptItemViewModel, CancellationToken, Task<bool>> operation,
        CancellationToken cancellationToken)
    {
        var succeeded = 0;
        foreach (var item in targets)
        {
            if (await operation(item, cancellationToken))
            {
                succeeded++;
            }
        }

        return new ImageBatchResult(succeeded, targets.Count - succeeded);
    }

    private async Task<bool> GenerateItemCoreAsync(
        CommunityAuthoringImagePromptItemViewModel item,
        CancellationToken cancellationToken)
    {
        item.SetBusy(true);
        item.ClearGeneratedState();
        try
        {
            var task = await _client.GenerateAuthoringImageAsync(
                new CommunityAuthoringImageGenerateRequest
                {
                    Prompt = item.Prompt.Trim(),
                    AspectRatio = item.AspectRatio
                },
                cancellationToken);
            item.SetTask(task);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            item.SetError($"생성 요청 실패: {exception.Message}");
            return false;
        }
        finally
        {
            item.SetBusy(false);
        }
    }

    private async Task<bool> RefreshItemCoreAsync(
        CommunityAuthoringImagePromptItemViewModel item,
        CancellationToken cancellationToken)
    {
        item.SetBusy(true);
        try
        {
            var task = await _client.GetAuthoringImageAsync(
                item.Task!.JobCode,
                refreshProvider: true,
                cancellationToken);
            if (task is null)
            {
                item.SetError("이미지 생성 작업을 찾을 수 없습니다.");
                return false;
            }

            item.SetTask(task);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            item.SetError($"상태 조회 실패: {exception.Message}");
            return false;
        }
        finally
        {
            item.SetBusy(false);
        }
    }

    private static CommunityComposerMessageKind ResolveTaskMessageKind(CommunityAuthoringImageTaskResponse? task)
        => task switch
        {
            { IsSuccess: true } => CommunityComposerMessageKind.Success,
            { StatusCode: CommunityAuthoringImageTaskStatusCodes.Failed } => CommunityComposerMessageKind.Error,
            _ => CommunityComposerMessageKind.Info
        };

    private readonly record struct ImageBatchResult(int Succeeded, int Failed);
}
