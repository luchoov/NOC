FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/NOC.Shared/NOC.Shared.csproj src/NOC.Shared/
COPY src/NOC.Worker.Notifications/NOC.Worker.Notifications.csproj src/NOC.Worker.Notifications/
RUN dotnet restore src/NOC.Worker.Notifications/NOC.Worker.Notifications.csproj

COPY src/NOC.Shared/ src/NOC.Shared/
COPY src/NOC.Worker.Notifications/ src/NOC.Worker.Notifications/
RUN dotnet publish src/NOC.Worker.Notifications/NOC.Worker.Notifications.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NOC.Worker.Notifications.dll"]
