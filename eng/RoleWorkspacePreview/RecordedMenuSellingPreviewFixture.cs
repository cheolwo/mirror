using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

/// <summary>판매 메뉴 컴포넌트의 가격·수량 표시만 확인하는 별도 합성 자료. 관측 가격을 판매가로 바꾸지 않습니다.</summary>
public sealed class RecordedMenuSellingPreviewFixture : I음식점공개읽기Service, I주문자음식주문쓰기Service, I주문자앱인증Service
{
    private readonly 음식점공개상세응답 _detail;
    public 음식점공개상세ViewModel Detail { get; }
    public 음식주문작성ViewModel Writer { get; }
    public 주문자앱인증ViewModel Authentication { get; }
    public const long MenuId = 991001;

    public RecordedMenuSellingPreviewFixture(RecordedMenuCatalogPreviewFixture catalog)
    {
        var image = catalog.Items.FirstOrDefault(x => x.ImageUrl is not null)?.ImageUrl;
        _detail = new()
        {
            음식점 = new() { Id = 991, 상호명 = "수량 표시 검토 매장", 카테고리 = "합성 자료", 주문가능여부 = true,
                소개 = "판매가 4,000원은 수량 계산을 확인하기 위한 합성 값입니다. 참고 사진의 실제 메뉴 가격이 아닙니다." },
            메뉴목록 = [new() { Id = MenuId, 메뉴명 = "수량 검토 메뉴", 판매가 = 4000, 대표이미지Url = image,
                설명 = "공식 참고 사진과 별도인 화면 검토용 합성 메뉴·판매가입니다." },
                new() { Id = 991002, 메뉴명 = "품절 검토 메뉴", 판매가 = 6000, 품절여부 = true,
                    설명 = "사진 없는 품절 메뉴의 표시를 확인하는 합성 자료입니다." }]
        };
        Detail = new(this);
        Writer = new(this);
        Writer.음식점설정(_detail);
        Authentication = new(this);
    }
    public async Task InitializeAsync()
    {
        await Detail.조회Async(991);
        await Authentication.복원Async();
    }
    public Task<음식점공개상세응답?> 상세Async(long restaurantId, CancellationToken cancellationToken = default)
        => Task.FromResult(restaurantId == 991 ? _detail : null);
    public Task<IReadOnlyList<음식점탐색권역응답>> 권역목록Async(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("이 합성 화면은 음식점 상세 표시만 검토합니다.");
    public Task<음식점공개목록응답> 목록Async(음식점공개목록조회요청 request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("이 합성 화면은 음식점 상세 표시만 검토합니다.");
    public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("합성 메뉴 검토에서 실제 주문을 접수하지 않습니다.");
    public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
        => Task.FromResult(new 주문자앱인증결과(주문자앱세션상태.익명));
    public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("합성 메뉴 검토에서 실제 로그인하지 않습니다.");
    public Task 로그아웃Async(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
