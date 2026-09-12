# 디버그 컨테이너를 사용자 지정하는 방법과 Visual Studio 이 Dockerfile을 사용하여 더 빠른 디버깅을 위해 이미지를 빌드하는 방법을 알아보려면 https://aka.ms/customizecontainer를 참조하세요.

# 이 스테이지는 VS에서 빠른 모드로 실행할 때 사용됩니다(디버그 구성의 기본값).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080


# 이 스테이지는 서비스 프로젝트를 빌드하는 데 사용됩니다.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["Ssalddel/Ssalddel.csproj", "Ssalddel/"]
COPY ["Ssalddel.Interior.Contracts/Ssalddel.Interior.Contracts.csproj", "Ssalddel.Interior.Contracts/"]
COPY ["Ssalddel.Interior.Domain/Ssalddel.Interior.Domain.csproj", "Ssalddel.Interior.Domain/"]
COPY ["Ssalddel.Community/Ssalddel.Community.csproj", "Ssalddel.Community/"]
COPY ["Ssalddel.Contracts/Ssalddel.Contracts.csproj", "Ssalddel.Contracts/"]
COPY ["Ssalddel.Domain/Ssalddel.Domain.csproj", "Ssalddel.Domain/"]
COPY ["Ssalddel.Infrastructure/Ssalddel.Infrastructure.csproj", "Ssalddel.Infrastructure/"]
COPY ["Ssalddel.Simulation.Application/Ssalddel.Simulation.Application.csproj", "Ssalddel.Simulation.Application/"]
COPY ["Ssalddel.Simulation.BusinessWorkflow/Ssalddel.Simulation.BusinessWorkflow.csproj", "Ssalddel.Simulation.BusinessWorkflow/"]
COPY ["Ssalddel.Simulation.Contracts/Ssalddel.Simulation.Contracts.csproj", "Ssalddel.Simulation.Contracts/"]
COPY ["Ssalddel.Simulation.Domain/Ssalddel.Simulation.Domain.csproj", "Ssalddel.Simulation.Domain/"]
COPY ["Ssalddel.Simulation.Hosting/Ssalddel.Simulation.Hosting.csproj", "Ssalddel.Simulation.Hosting/"]
COPY ["Ssalddel.Simulation.Infrastructure/Ssalddel.Simulation.Infrastructure.csproj", "Ssalddel.Simulation.Infrastructure/"]
COPY ["Ssalddel.Simulation.Persistence/Ssalddel.Simulation.Persistence.csproj", "Ssalddel.Simulation.Persistence/"]
COPY ["Ssalddel.WorkflowRules.Contracts/Ssalddel.WorkflowRules.Contracts.csproj", "Ssalddel.WorkflowRules.Contracts/"]
COPY ["Ssalddel.WorkflowRules/Ssalddel.WorkflowRules.csproj", "Ssalddel.WorkflowRules/"]
COPY ["Ssalddel.CodeMetadata/Ssalddel.CodeMetadata.csproj", "Ssalddel.CodeMetadata/"]
COPY ["Ssalddel.CodeMetadata/Directory.Build.props", "Ssalddel.CodeMetadata/"]
RUN dotnet restore "./Ssalddel/Ssalddel.csproj"
COPY . .
WORKDIR "/src/Ssalddel"

# 이 스테이지는 최종 스테이지에 복사할 서비스 프로젝트를 게시하는 데 사용됩니다.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Ssalddel.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

# 이 스테이지는 프로덕션에서 사용되거나 VS에서 일반 모드로 실행할 때 사용됩니다(디버그 구성을 사용하지 않는 경우 기본값).
FROM base AS final
WORKDIR /app
USER root
RUN mkdir -p /app/App_Data /app/uploads /app/community /app/logs \
    && chown -R $APP_UID:$APP_UID /app/App_Data /app/uploads /app/community /app/logs
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Ssalddel.dll"]
