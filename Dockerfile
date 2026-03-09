FROM node:22-alpine AS miniapp-build
WORKDIR /src

COPY src/ClanWarReminder.MiniApp/package.json src/ClanWarReminder.MiniApp/package-lock.json src/ClanWarReminder.MiniApp/
RUN cd src/ClanWarReminder.MiniApp && npm ci

COPY src/ClanWarReminder.MiniApp/ src/ClanWarReminder.MiniApp/
COPY src/ClanWarReminder.Api/wwwroot/ src/ClanWarReminder.Api/wwwroot/
RUN cd src/ClanWarReminder.MiniApp && MINIAPP_TARGET=embed npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ClanWarReminder.Api/ClanWarReminder.Api.csproj src/ClanWarReminder.Api/
COPY src/ClanWarReminder.Application/ClanWarReminder.Application.csproj src/ClanWarReminder.Application/
COPY src/ClanWarReminder.Domain/ClanWarReminder.Domain.csproj src/ClanWarReminder.Domain/
COPY src/ClanWarReminder.Infrastructure/ClanWarReminder.Infrastructure.csproj src/ClanWarReminder.Infrastructure/

RUN dotnet restore src/ClanWarReminder.Api/ClanWarReminder.Api.csproj

COPY src/ src/
COPY --from=miniapp-build /src/src/ClanWarReminder.Api/wwwroot/miniapp/ src/ClanWarReminder.Api/wwwroot/miniapp/
RUN dotnet publish src/ClanWarReminder.Api/ClanWarReminder.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClanWarReminder.Api.dll"]
