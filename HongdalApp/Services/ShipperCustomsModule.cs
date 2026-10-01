using HongdalApp.Services.Application;
using HongdalApp.Services.Customs;
using HongdalApp.Services.Customs.Commands;
using HongdalApp.Services.Customs.Events;
using Microsoft.Extensions.DependencyInjection;

namespace HongdalApp.Services;

internal static class ShipperCustomsModule
{
    internal static IServiceCollection AddShipperCustomsModule(this IServiceCollection services)
    {
        services.AddSingleton<IProductHsCodeInferenceService, ProductHsCodeInferenceService>();
        services.AddSingleton<IHsCodeAgencyCapabilityService, SampleHsCodeAgencyCapabilityService>();
        services.AddSingleton<ICustomsBrokerDirectory, SampleCustomsBrokerDirectory>();
        services.AddSingleton<ICustomsHsReviewService, CustomsHsReviewService>();
        services.AddSingleton<IAppCommandHandler<RequestCustomsHsReviewCommand, CustomsHsReviewRequest?>,
            RequestCustomsHsReviewCommandHandler>();
        services.AddSingleton<IAppCommandHandler<AssignCustomsBrokerCommand, bool>,
            AssignCustomsBrokerCommandHandler>();
        services.AddSingleton<IAppCommandHandler<CompleteCustomsHsReviewCommand, bool>,
            CompleteCustomsHsReviewCommandHandler>();
        services.AddSingleton<IAppEventHandler<CustomsHsReviewRequestedEvent>, CustomsHsReviewRequestedEventHandler>();
        services.AddSingleton<IAppEventHandler<CustomsBrokerAssignedEvent>, CustomsBrokerAssignedEventHandler>();
        services.AddSingleton<IAppEventHandler<CustomsHsReviewCompletedEvent>, CustomsHsReviewCompletedEventHandler>();
        return services;
    }
}
