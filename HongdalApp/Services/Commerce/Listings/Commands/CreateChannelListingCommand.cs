using Hongdal.Contracts.Common.Sales;
using HongdalApp.Services.Application;

namespace HongdalApp.Services.Commerce.Listings.Commands;

public sealed record CreateChannelListingCommand(채널출품저장요청 Payload)
    : IAppCommand<채널출품항목응답?>;
