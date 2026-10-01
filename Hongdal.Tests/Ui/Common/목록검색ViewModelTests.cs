using Hongdal.Contracts.Common.Inbound;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class 목록검색ViewModelTests
{
    [Fact]
    public void 목록조회요청은_페이지와빈조건을정규화한다()
    {
        var request = new 목록조회요청
        {
            페이지 = -3,
            페이지크기 = 500,
            검색어 = "  supplier  ",
            정렬조건 =
            [
                new 목록정렬조건("상태", 목록정렬방향.오름차순, 2),
                new 목록정렬조건("Id", 목록정렬방향.내림차순, 0),
                new 목록정렬조건(" ", 목록정렬방향.오름차순, 1)
            ],
            필터조건 =
            [
                new 목록필터조건("상태", "Equal", "입고예정"),
                new 목록필터조건("창고Id", "Equal", " ")
            ]
        };

        var normalized = request.정규화();

        Assert.Equal(0, normalized.페이지);
        Assert.Equal(200, normalized.페이지크기);
        Assert.Equal("supplier", normalized.검색어);
        Assert.Equal(["Id", "상태"], normalized.정렬조건.Select(item => item.필드));
        Assert.Single(normalized.필터조건);
    }

    [Fact]
    public async Task 위임서버목록ViewModel은_정규화된요청과전체건수를보관한다()
    {
        목록조회요청? received = null;
        var viewModel = new 위임서버목록조회ViewModel<string>(
            "test-query",
            "테스트 목록",
            (request, _) =>
            {
                received = request;
                return Task.FromResult(new 목록조회결과<string>(["A", "B"], 17));
            });

        var succeeded = await viewModel.조회Async(new 목록조회요청
        {
            페이지 = -1,
            페이지크기 = 999
        });

        Assert.True(succeeded);
        Assert.NotNull(received);
        Assert.Equal(0, received.페이지);
        Assert.Equal(200, received.페이지크기);
        Assert.Equal(17, viewModel.결과.전체건수);
        Assert.Equal(["A", "B"], viewModel.결과.항목);
        Assert.Same(received, viewModel.최근요청);
    }

    [Fact]
    public void 입고목록Query는_검색정렬페이지를같은순서로적용한다()
    {
        입고요청항목응답[] source =
        [
            new() { Id = 1, 창고Id = 1, 공급처명 = "Alpha", 상태 = "입고예정", 원주문참조번호 = "PO-1" },
            new() { Id = 3, 창고Id = 1, 공급처명 = "Alpha", 상태 = "입고예정", 원주문참조번호 = "PO-3" },
            new() { Id = 2, 창고Id = 2, 공급처명 = "Beta", 상태 = "입고완료", 원주문참조번호 = "PO-2" }
        ];

        var result = 입고요청목록Query.Apply(source, new 입고요청목록조회요청
        {
            Page = 1,
            PageSize = 1,
            Search = "Alpha",
            Status = "입고예정",
            SortBy = nameof(입고요청항목응답.Id),
            SortDescending = true
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public void 입고목록Query는_공급처코드와예정품목을검색한다()
    {
        입고요청항목응답[] source =
        [
            new()
            {
                Id = 11,
                공급처코드 = "SUP-SEOUL-01",
                공급처명 = "서울 식자재",
                예정상품명 = "유기농 감자",
                예정SKU = "POTATO-10KG",
                상태 = 입고상태코드.예정
            },
            new()
            {
                Id = 22,
                공급처코드 = "SUP-BUSAN-02",
                공급처명 = "부산 수산",
                예정상품명 = "고등어",
                예정SKU = "FISH-001",
                상태 = 입고상태코드.예정
            }
        ];

        var bySupplierCode = 입고요청목록Query.Apply(source, new 입고요청목록조회요청
        {
            Search = "SEOUL-01",
            Status = 입고상태코드.예정
        });
        var byProduct = 입고요청목록Query.Apply(source, new 입고요청목록조회요청
        {
            Search = "POTATO-10KG",
            Status = 입고상태코드.예정
        });

        Assert.Equal(11, Assert.Single(bySupplierCode.Items).Id);
        Assert.Equal(11, Assert.Single(byProduct.Items).Id);
    }
}
