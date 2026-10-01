using Hongdal.Controllers.Admin.Content07;
using Hongdal.Controllers.Common;
using Hongdal.Extensions;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityPostUseCasePortTests
{
    private static readonly Type[] NarrowPorts =
    [
        typeof(I커뮤니티게시글조회UseCase),
        typeof(I커뮤니티게시글발행UseCase),
        typeof(I커뮤니티게시글예약발행UseCase),
        typeof(I커뮤니티게시글첨부UseCase),
        typeof(I커뮤니티게시글참여UseCase),
        typeof(I커뮤니티게시글운영UseCase)
    ];

    [Fact]
    public void 통합_UseCase는_기능별_port의_호환_경계다()
    {
        Assert.True(typeof(I커뮤니티게시글UseCase)
            .IsAssignableFrom(typeof(커뮤니티게시글UseCase)));

        foreach (var port in NarrowPorts)
        {
            Assert.True(port.IsAssignableFrom(typeof(I커뮤니티게시글UseCase)));
            Assert.True(port.IsAssignableFrom(typeof(커뮤니티게시글UseCase)));
        }

        Assert.True(typeof(I커뮤니티게시글첨부UseCase)
            .IsAssignableFrom(typeof(커뮤니티게시글첨부UseCase)));
        Assert.True(typeof(I커뮤니티게시글운영UseCase)
            .IsAssignableFrom(typeof(커뮤니티게시글운영UseCase)));
        Assert.True(typeof(I커뮤니티게시글참여UseCase)
            .IsAssignableFrom(typeof(커뮤니티게시글참여UseCase)));
        Assert.True(typeof(I커뮤니티게시글조회UseCase)
            .IsAssignableFrom(typeof(커뮤니티게시글조회UseCase)));
        Assert.True(typeof(I커뮤니티게시글발행UseCase)
            .IsAssignableFrom(typeof(커뮤니티게시글발행UseCase)));
        Assert.True(typeof(I커뮤니티게시글예약발행UseCase)
            .IsAssignableFrom(typeof(커뮤니티게시글예약발행UseCase)));
    }

    [Fact]
    public void 게시글_원장_Context는_선택_조회와_표시_Context_port를_제공한다()
    {
        Assert.True(typeof(I게시글원장ContextService)
            .IsAssignableFrom(typeof(게시글원장ContextService)));
        Assert.True(typeof(I게시글원장선택조회Service)
            .IsAssignableFrom(typeof(I게시글원장ContextService)));
        Assert.True(typeof(I게시글원장표시ContextService)
            .IsAssignableFrom(typeof(I게시글원장ContextService)));
        Assert.True(typeof(I게시글원장선택조회Service)
            .IsAssignableFrom(typeof(게시글원장선택조회Service)));
        Assert.True(typeof(I게시글원장표시ContextService)
            .IsAssignableFrom(typeof(게시글원장표시ContextService)));
    }

    [Fact]
    public void 게시글_HTTP_경계는_필요한_기능별_port만_의존한다()
    {
        var postControllerDependencies = ConstructorDependencies<커뮤니티게시글Controller>();
        Assert.DoesNotContain(typeof(I커뮤니티게시글UseCase), postControllerDependencies);
        Assert.Contains(typeof(I커뮤니티게시글조회UseCase), postControllerDependencies);
        Assert.Contains(typeof(I커뮤니티게시글발행UseCase), postControllerDependencies);
        Assert.Contains(typeof(I게시글원장선택조회Service), postControllerDependencies);
        Assert.Contains(typeof(I게시글원장표시ContextService), postControllerDependencies);
        Assert.DoesNotContain(typeof(I게시글원장ContextService), postControllerDependencies);
        Assert.DoesNotContain(typeof(I커뮤니티게시글첨부UseCase), postControllerDependencies);
        Assert.DoesNotContain(typeof(I커뮤니티게시글참여UseCase), postControllerDependencies);
        Assert.DoesNotContain(typeof(I커뮤니티게시글운영UseCase), postControllerDependencies);
        Assert.DoesNotContain(typeof(I커뮤니티게시글예약발행UseCase), postControllerDependencies);

        var attachmentDependencies = ConstructorDependencies<커뮤니티게시글첨부Controller>();
        Assert.Equal([typeof(I커뮤니티게시글첨부UseCase)], attachmentDependencies);

        var participationDependencies = ConstructorDependencies<커뮤니티게시글참여Controller>();
        Assert.Equal([typeof(I커뮤니티게시글참여UseCase)], participationDependencies);

        var moderationDependencies = ConstructorDependencies<커뮤니티게시글운영Controller>();
        Assert.Equal([typeof(I커뮤니티게시글운영UseCase)], moderationDependencies);

        var scheduleDependencies = ConstructorDependencies<CommunityPostScheduleController>();
        Assert.Contains(typeof(I커뮤니티게시글예약발행UseCase), scheduleDependencies);
        Assert.DoesNotContain(typeof(I커뮤니티게시글UseCase), scheduleDependencies);

        var imageDependencies = ConstructorDependencies<CommunityAuthoringImagesController>();
        Assert.Contains(typeof(I커뮤니티게시글첨부UseCase), imageDependencies);
        Assert.DoesNotContain(typeof(I커뮤니티게시글UseCase), imageDependencies);
    }

    [Fact]
    public void 게시글_응용서비스는_원장_선택과_표시_책임을_필요한_만큼만_요청한다()
    {
        var readDependencies = ConstructorDependencies<커뮤니티게시글조회UseCase>();
        Assert.Contains(typeof(I게시글원장표시ContextService), readDependencies);
        Assert.DoesNotContain(typeof(I게시글원장선택조회Service), readDependencies);
        Assert.DoesNotContain(typeof(I게시글원장ContextService), readDependencies);

        var creationDependencies = ConstructorDependencies<커뮤니티게시글생성Service>();
        Assert.Contains(typeof(I게시글원장선택조회Service), creationDependencies);
        Assert.Contains(typeof(I게시글원장표시ContextService), creationDependencies);
        Assert.DoesNotContain(typeof(I게시글원장ContextService), creationDependencies);

        var publishingDependencies = ConstructorDependencies<커뮤니티게시글발행UseCase>();
        Assert.Contains(typeof(I게시글원장선택조회Service), publishingDependencies);
        Assert.Contains(typeof(I게시글원장표시ContextService), publishingDependencies);
        Assert.DoesNotContain(typeof(I게시글원장ContextService), publishingDependencies);
    }

    [Theory]
    [InlineData(typeof(커뮤니티게시글첨부Controller), nameof(커뮤니티게시글첨부Controller.UploadAttachment), "{id:long}/attachments")]
    [InlineData(typeof(커뮤니티게시글참여Controller), nameof(커뮤니티게시글참여Controller.Recommend), "{id:long}/recommendations")]
    [InlineData(typeof(커뮤니티게시글참여Controller), nameof(커뮤니티게시글참여Controller.CreateComment), "{id:long}/comments")]
    [InlineData(typeof(커뮤니티게시글참여Controller), nameof(커뮤니티게시글참여Controller.CreateAttachmentComment), "attachments/{attachmentId:long}/comments")]
    [InlineData(typeof(커뮤니티게시글운영Controller), nameof(커뮤니티게시글운영Controller.SetOperatorPin), "{id:long}/operator-pin")]
    [InlineData(typeof(커뮤니티게시글운영Controller), nameof(커뮤니티게시글운영Controller.ReportComment), "comments/{commentId:long}/reports")]
    [InlineData(typeof(커뮤니티게시글운영Controller), nameof(커뮤니티게시글운영Controller.ReportAttachmentComment), "attachments/comments/{commentId:long}/reports")]
    public void 분리된_Controller는_기존_HTTP_경로를_유지한다(
        Type controllerType,
        string methodName,
        string expectedTemplate)
    {
        var controllerRoute = controllerType
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();
        var methodRoute = controllerType
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true)
            .Cast<HttpMethodAttribute>()
            .Single();

        Assert.Equal("api/v1/community/posts", controllerRoute.Template);
        Assert.Equal(expectedTemplate, methodRoute.Template);
    }

    [Fact]
    public void 게시글_port는_scoped_구현체로_연결되고_첨부는_독립_구현체를_사용한다()
    {
        var services = new ServiceCollection();
        services.AddHongdalApplicationCore();

        var concrete = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(커뮤니티게시글UseCase));
        Assert.Equal(ServiceLifetime.Scoped, concrete.Lifetime);
        Assert.Equal(typeof(커뮤니티게시글UseCase), concrete.ImplementationType);

        var compatibility = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(I커뮤니티게시글UseCase));
        Assert.Equal(ServiceLifetime.Scoped, compatibility.Lifetime);
        Assert.NotNull(compatibility.ImplementationFactory);

        var attachment = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(I커뮤니티게시글첨부UseCase));
        Assert.Equal(ServiceLifetime.Scoped, attachment.Lifetime);
        Assert.Equal(typeof(커뮤니티게시글첨부UseCase), attachment.ImplementationType);

        var moderation = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(I커뮤니티게시글운영UseCase));
        Assert.Equal(ServiceLifetime.Scoped, moderation.Lifetime);
        Assert.Equal(typeof(커뮤니티게시글운영UseCase), moderation.ImplementationType);

        var participation = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(I커뮤니티게시글참여UseCase));
        Assert.Equal(ServiceLifetime.Scoped, participation.Lifetime);
        Assert.Equal(typeof(커뮤니티게시글참여UseCase), participation.ImplementationType);

        var read = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(I커뮤니티게시글조회UseCase));
        Assert.Equal(ServiceLifetime.Scoped, read.Lifetime);
        Assert.Equal(typeof(커뮤니티게시글조회UseCase), read.ImplementationType);

        var publishing = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(I커뮤니티게시글발행UseCase));
        Assert.Equal(ServiceLifetime.Scoped, publishing.Lifetime);
        Assert.Equal(typeof(커뮤니티게시글발행UseCase), publishing.ImplementationType);

        var scheduling = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(I커뮤니티게시글예약발행UseCase));
        Assert.Equal(ServiceLifetime.Scoped, scheduling.Lifetime);
        Assert.Equal(typeof(커뮤니티게시글예약발행UseCase), scheduling.ImplementationType);
    }

    [Fact]
    public void 원장_Context_port는_책임별_scoped_구현체와_호환_adapter로_연결된다()
    {
        var services = new ServiceCollection();
        services.AddHongdalDomainServices();

        var ledgerContext = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(게시글원장ContextService));
        Assert.Equal(ServiceLifetime.Scoped, ledgerContext.Lifetime);
        Assert.NotNull(ledgerContext.ImplementationFactory);

        foreach (var implementation in new[]
                 {
                     typeof(게시글원장선택조회Service),
                     typeof(게시글원장표시ContextService)
                 })
        {
            var registration = Assert.Single(
                services,
                descriptor => descriptor.ServiceType == implementation);
            Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
            Assert.Equal(implementation, registration.ImplementationType);
        }

        foreach (var port in new[]
                 {
                     typeof(I게시글원장ContextService),
                     typeof(I게시글원장선택조회Service),
                     typeof(I게시글원장표시ContextService)
                 })
        {
            var registration = Assert.Single(
                services,
                descriptor => descriptor.ServiceType == port);
            Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
            Assert.NotNull(registration.ImplementationFactory);
        }
    }

    private static Type[] ConstructorDependencies<T>()
        => typeof(T)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
}
