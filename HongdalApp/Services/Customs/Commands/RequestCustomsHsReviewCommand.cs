using HongdalApp.Models.Shipper;
using HongdalApp.Services.Application;

namespace HongdalApp.Services.Customs.Commands;

public sealed record RequestCustomsHsReviewCommand(ShipperRequestItem Request, string ShipperUserId)
    : IAppCommand<CustomsHsReviewRequest?>;
