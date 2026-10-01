using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Shipper.Request;
using HongdalApp.Services.Application;

namespace HongdalApp.Services.Warehouse.Reconsignment.Commands;

public sealed record CreateReconsignmentOrderCommand(재고운송의뢰생성요청 Payload, string UserId)
    : IAppCommand<화주운송의뢰응답?>;
