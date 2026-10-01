using HongdalApp.Models.Shipper;
using HongdalApp.Services.Application;

namespace HongdalApp.Services.Samples.Commands;

public sealed record AddShipperRequestCommand(ShipperRequestItem Request, string ShipperUserId)
    : IAppCommand<bool>;
