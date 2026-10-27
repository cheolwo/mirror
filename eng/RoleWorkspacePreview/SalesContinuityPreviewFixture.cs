using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

/// <summary>접근 재확인 동안의 입력 지속을 검토하는 루프백 전용 메모리 예시입니다.</summary>
public sealed class SalesContinuityPreviewFixture : I판매채널페이지접근Service, I판매채널주문읽기Service, ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
{
    private TaskCompletionSource<bool>? _pending;
    private bool _failNextRead;
    public 현재사용자Snapshot 현재사용자 { get; private set; } = new("preview-seller-a", "검토용 계정", ["판매자"]);
    public bool Pending => _pending is not null;
    public int ReadCount { get; private set; }
    public 판매채널주문PageViewModel Model { get; }
    public event Action? Changed;

    public SalesContinuityPreviewFixture()
        => Model = new(new(this, this), new(this), new(this));

    public void Begin() => _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void FailNextRead() => _failNextRead = true;

    public void Complete(string result)
    {
        var pending = _pending;
        _pending = null;
        if (result == "error") pending?.TrySetException(new HttpRequestException("검토용 접근 조회 오류"));
        else pending?.TrySetResult(result != "off");
    }

    public void ChangeUser(string kind)
    {
        현재사용자 = kind switch
        {
            "logout" => 현재사용자Snapshot.익명,
            "role" => new("preview-seller-a", "검토용 계정", ["주문자"]),
            "token" => 현재사용자 with { Roles = 현재사용자.Roles.ToArray() },
            "original" => new("preview-seller-a", "검토용 계정", ["판매자"]),
            _ => new("preview-seller-b", "다른 검토용 계정", ["판매자"])
        };
        Changed?.Invoke();
    }

    public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default)
        => _pending is { } pending ? pending.Task.WaitAsync(cancellationToken) : Task.FromResult(true);

    public Task<판매채널주문목록응답> 목록조회Async(판매채널주문목록조회요청 request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        if (_failNextRead)
        {
            _failNextRead = false;
            throw new HttpRequestException("검토용 주문 조회 실패");
        }
        return Task.FromResult(new 판매채널주문목록응답
        {
            Page = request.Page,
            PageSize = 25,
            TotalCount = 1,
            Items = [new()
            {
                OrderId = 1,
                채널종류 = CommerceChannelKeys.SmartStore,
                채널주문번호 = "검토용 주문",
                대표상품명 = 현재사용자.UserId == "preview-seller-b" ? "새 계정 검토용 상품" : "기존 계정 검토용 상품",
                출고창고표시 = "검토용 창고",
                출고상태 = "출고예정",
                출고라인수 = 1,
                총수량 = 1
            }]
        });
    }

    public Task<판매채널주문상세응답?> 상세조회Async(long orderId, CancellationToken cancellationToken = default)
        => Task.FromResult<판매채널주문상세응답?>(null);
}
