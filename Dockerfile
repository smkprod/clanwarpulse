FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ClanWarReminder.Api/ClanWarReminder.Api.csproj src/ClanWarReminder.Api/
COPY src/ClanWarReminder.Application/ClanWarReminder.Application.csproj src/ClanWarReminder.Application/
COPY src/ClanWarReminder.Domain/ClanWarReminder.Domain.csproj src/ClanWarReminder.Domain/
COPY src/ClanWarReminder.Infrastructure/ClanWarReminder.Infrastructure.csproj src/ClanWarReminder.Infrastructure/

RUN dotnet restore src/ClanWarReminder.Api/ClanWarReminder.Api.csproj

COPY src/ src/
RUN dotnet publish src/ClanWarReminder.Api/ClanWarReminder.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClanWarReminder.Api.dll"]
