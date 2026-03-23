FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build config files
COPY Directory.Build.props Directory.Packages.props ./

# Copy project files for restore
COPY src/NOC.Shared/NOC.Shared.csproj src/NOC.Shared/
COPY src/NOC.Web/NOC.Web.csproj src/NOC.Web/
RUN dotnet restore src/NOC.Web/NOC.Web.csproj

# Copy source
COPY src/NOC.Shared/ src/NOC.Shared/
COPY src/NOC.Web/ src/NOC.Web/
RUN dotnet publish src/NOC.Web/NOC.Web.csproj -c Release -o /app/publish --no-restore

FROM build AS migrate
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef --version 10.0.5
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/NOC.Web/NOC.Web.csproj", "--startup-project", "src/NOC.Web/NOC.Web.csproj", "--configuration", "Release", "--no-build"]

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "NOC.Web.dll"]
