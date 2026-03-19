FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/NOC.Shared/NOC.Shared.csproj src/NOC.Shared/
COPY src/NOC.Worker.Messaging/NOC.Worker.Messaging.csproj src/NOC.Worker.Messaging/
RUN dotnet restore src/NOC.Worker.Messaging/NOC.Worker.Messaging.csproj

COPY src/NOC.Shared/ src/NOC.Shared/
COPY src/NOC.Worker.Messaging/ src/NOC.Worker.Messaging/
RUN dotnet publish src/NOC.Worker.Messaging/NOC.Worker.Messaging.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NOC.Worker.Messaging.dll"]
