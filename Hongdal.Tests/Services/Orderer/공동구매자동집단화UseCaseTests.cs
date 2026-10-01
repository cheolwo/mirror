using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.Orderer;

namespace Hongdal.Tests.Services.Orderer;

public sealed class 공동구매자동집단화UseCaseTests
{
    [Fact]
    public async Task 예약결제_수요는_가상수령창고와_개별주문_입고예정원장을_연결한다()
    {
        var store = new StubStore();
        var warehouse = new StubReceivingWarehouseService();
        var ledgers = new StubIndividualOrderLedgerService();
        var useCase = new 공동구매자동집단화UseCase(store, warehouse, ledgers);
        var command = new 공동구매자동수요등록Command
        {
            수요출처키 = "paid-demand-1",
            커뮤니티원장Id = "group-purchase-ledger-1",
            상품키 = "apple",
            상품명 = "사과",
            주문자키 = "orderer-1",
            주문자표시명 = "주문자 1",
            배송권키 = "seoul-mapogu",
            희망수량 = 5,
            수량단위 = "kg",
            수요유형 = 공동구매자동수요유형코드.예약결제,
            결제상태 = 공동구매자동결제상태코드.예약됨,
            수령지표시명 = "자택 수령지",
            수령도로명주소 = "서울특별시 마포구 월드컵로 1",
            수령상세주소 = "101동 101호"
        };

        var result = await useCase.수요등록Async(command);

        Assert.True(result.성공);
        Assert.True(warehouse.Called);
        Assert.True(ledgers.Called);
        var demand = Assert.Single(result.값!.수요목록);
        Assert.Equal(101, demand.도착창고Id);
        Assert.Equal(창고유형코드.가상창고, demand.도착창고유형);
        Assert.Equal("warehouse:101:receiving-address", demand.수령지주소참조키);
        Assert.Equal(공동구매개별주문입고상태코드.입고예정, demand.입고의미상태);
        Assert.Equal("group-order-ledger-1", demand.공동구매주문집계원장Id);
        Assert.Equal("individual-order-ledger-1", demand.개별주문원장Id);
        Assert.Equal("inbound-planned-ledger-1", demand.입고예정원장Id);
    }

    [Fact]
    public async Task 비구속_관심수요는_창고나_입고원장을_미리_만들지_않는다()
    {
        var store = new StubStore();
        var warehouse = new StubReceivingWarehouseService();
        var ledgers = new StubIndividualOrderLedgerService();
        var useCase = new 공동구매자동집단화UseCase(store, warehouse, ledgers);

        var result = await useCase.수요등록Async(new 공동구매자동수요등록Command
        {
            수요출처키 = "interest-demand-1",
            상품키 = "apple",
            상품명 = "사과",
            주문자키 = "orderer-1",
            배송권키 = "seoul-mapogu",
            희망수량 = 1,
            수요유형 = 공동구매자동수요유형코드.관심표시,
            결제상태 = 공동구매자동결제상태코드.미결제
        });

        Assert.True(result.성공);
        Assert.False(warehouse.Called);
        Assert.False(ledgers.Called);
        Assert.Equal(
            공동구매개별주문입고상태코드.미지정,
            Assert.Single(result.값!.수요목록).입고의미상태);
    }

    private sealed class StubReceivingWarehouseService : I공동구매수령창고Service
    {
        public bool Called { get; private set; }

        public Task<공동구매수령창고배정결과> 확보Async(
            공동구매자동수요등록Command command,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(new 공동구매수령창고배정결과(
                101,
                창고유형코드.가상창고,
                "자택 수령지 가상 창고",
                "warehouse:101:receiving-address",
                true));
        }
    }

    private sealed class StubIndividualOrderLedgerService : I공동구매개별주문원장Service
    {
        public bool Called { get; private set; }

        public Task<공동구매개별주문원장연결결과> 생성및연결Async(
            공동구매자동집단응답 group,
            공동구매자동수요응답 demand,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(new 공동구매개별주문원장연결결과(
                "group-order-ledger-1",
                "individual-order-ledger-1",
                "inbound-planned-ledger-1"));
        }
    }

    private sealed class StubStore : I공동구매자동집단화저장소
    {
        private 공동구매자동집단응답? _group;

        public Task<공동구매자동집단응답> 수요등록Async(
            공동구매자동수요등록Command command,
            CancellationToken cancellationToken = default)
        {
            var inboundStatus = command.수요유형 == 공동구매자동수요유형코드.예약결제
                ? 공동구매개별주문입고상태코드.입고예정
                : 공동구매개별주문입고상태코드.미지정;
            _group = new 공동구매자동집단응답
            {
                자동집단Id = "auto-group-1",
                상품키 = command.상품키,
                상품명 = command.상품명,
                수요목록 =
                [
                    new 공동구매자동수요응답
                    {
                        수요Id = "demand-1",
                        수요출처키 = command.수요출처키,
                        커뮤니티원장Id = command.커뮤니티원장Id,
                        주문자키 = command.주문자키,
                        주문자표시명 = command.주문자표시명,
                        도착창고Id = command.도착창고Id,
                        도착창고유형 = command.도착창고유형,
                        도착창고명 = command.도착창고명,
                        수령지주소참조키 = command.수령지주소참조키,
                        입고의미상태 = inboundStatus,
                        수요유형 = command.수요유형,
                        결제상태 = command.결제상태,
                        희망수량 = command.희망수량,
                        수량단위 = command.수량단위
                    }
                ]
            };
            return Task.FromResult(_group);
        }

        public Task<IReadOnlyList<공동구매자동집단응답>> 집단목록조회Async(
            공동구매자동집단조회조건 조건,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공동구매자동집단응답>>(_group is null ? [] : [_group]);

        public Task<공동구매자동집단응답?> 집단조회Async(
            string 자동집단Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_group);

        public Task<공동구매자동집단응답> 개별주문원장연결Async(
            string 자동집단Id,
            string 수요Id,
            string 공동구매주문집계원장Id,
            string 개별주문원장Id,
            string 입고예정원장Id,
            CancellationToken cancellationToken = default)
        {
            var demand = _group!.수요목록.Single(x => x.수요Id == 수요Id);
            _group.공동구매주문집계원장Id = 공동구매주문집계원장Id;
            demand.공동구매주문집계원장Id = 공동구매주문집계원장Id;
            demand.개별주문원장Id = 개별주문원장Id;
            demand.입고예정원장Id = 입고예정원장Id;
            demand.입고의미상태 = 공동구매개별주문입고상태코드.입고예정;
            return Task.FromResult(_group);
        }
    }
}
