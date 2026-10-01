using Hongdal.Contracts.Common.Warehouse;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 창고목록조회ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-list",
        "창고 목록 조회",
        업무조각유형.목록조회),
        I목록조회ViewModel<창고요약응답>,
        I비동기검색ViewModel<창고요약응답>
{
    public IReadOnlyList<창고요약응답> 항목목록 => 원본.창고목록;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.목록조회Async(cancellationToken);

    public async Task<IReadOnlyList<창고요약응답>> 검색Async(
        string? 검색어,
        CancellationToken cancellationToken = default)
    {
        if (항목목록.Count == 0 && !처리중)
        {
            await 조회Async(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(검색어))
        {
            return 항목목록.Take(20).ToArray();
        }

        var search = 검색어.Trim();
        return 항목목록
            .Where(item => item.창고명.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.주소.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.담당자명.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToArray();
    }

    public bool 선택(long warehouseId) => 원본.창고선택(warehouseId);
}

public sealed class 창고등록ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-create",
        "창고 등록",
        업무조각유형.등록), I등록ViewModel<창고저장요청>
{
    public 창고저장요청 초안 => 원본.창고초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.창고생성Async(cancellationToken);

    public void 입력변경알림() => 원본.입력변경알림();
}

public sealed class 창고사용자조회ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-user-list",
        "창고 담당자 조회",
        업무조각유형.목록조회), I목록조회ViewModel<창고사용자항목응답>
{
    public IReadOnlyList<창고사용자항목응답> 항목목록 => 원본.사용자목록;
    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.사용자목록조회Async(cancellationToken);
}

public sealed class 창고사용자등록ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-user-create",
        "창고 담당자 등록",
        업무조각유형.등록), I등록ViewModel<창고사용자저장요청>
{
    public 창고사용자저장요청 초안 => 원본.사용자초안;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.사용자추가Async(cancellationToken);

    public void 입력변경알림() => 원본.입력변경알림();
}

public sealed class 창고수정ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-update",
        "창고 수정",
        업무조각유형.수정), I수정ViewModel<창고저장요청>
{
    public 창고저장요청 초안 => 원본.창고수정초안;
    public bool 선택항목적용() => 원본.창고수정초안적용();
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.창고수정Async(cancellationToken);
    public void 입력변경알림() => 원본.입력변경알림();
}

public sealed class 창고삭제ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-delete",
        "창고 삭제",
        업무조각유형.삭제), I삭제ViewModel<long>
{
    public long 초안 => 원본.선택된창고?.Id ?? 0;
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.창고삭제Async(cancellationToken);
}

public sealed class 창고사용자수정ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-user-update",
        "창고 담당자 수정",
        업무조각유형.수정), I수정ViewModel<창고사용자저장요청>
{
    public 창고사용자저장요청 초안 => 원본.사용자수정초안;
    public bool 선택(long warehouseUserId) => 원본.사용자선택(warehouseUserId);
    public bool 선택항목적용() => 원본.사용자수정초안적용();
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.사용자수정Async(cancellationToken);
    public void 입력변경알림() => 원본.입력변경알림();
}

public sealed class 창고사용자삭제ViewModel(창고기준정보ViewModel 원본)
    : 위임업무조각ViewModelBase<창고기준정보ViewModel>(
        원본,
        "warehouse-user-delete",
        "창고 담당자 삭제",
        업무조각유형.삭제), I삭제ViewModel<long>
{
    public long 초안 => 원본.선택된사용자?.Id ?? 0;
    public bool 선택(long warehouseUserId) => 원본.사용자선택(warehouseUserId);
    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.사용자삭제Async(cancellationToken);
}

public sealed class 창고CrudViewModel(
    창고목록조회ViewModel 조회,
    창고등록ViewModel 등록,
    창고수정ViewModel 수정,
    창고삭제ViewModel 삭제)
    : 업무단위CrudViewModelBase<창고목록조회ViewModel, 창고등록ViewModel, 창고수정ViewModel, 창고삭제ViewModel>(
        "warehouse",
        "창고",
        조회,
        등록,
        수정,
        삭제);

public sealed class 창고사용자CrudViewModel(
    창고사용자조회ViewModel 조회,
    창고사용자등록ViewModel 등록,
    창고사용자수정ViewModel 수정,
    창고사용자삭제ViewModel 삭제)
    : 업무단위CrudViewModelBase<창고사용자조회ViewModel, 창고사용자등록ViewModel, 창고사용자수정ViewModel, 창고사용자삭제ViewModel>(
        "warehouse-user",
        "창고 담당자",
        조회,
        등록,
        수정,
        삭제);
