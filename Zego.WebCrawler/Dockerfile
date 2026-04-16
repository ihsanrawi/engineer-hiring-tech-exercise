FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Zego.WebCrawler/Zego.WebCrawler.csproj", "Zego.WebCrawler/"]
RUN dotnet restore "Zego.WebCrawler/Zego.WebCrawler.csproj"
COPY . .
WORKDIR "/src/Zego.WebCrawler"
RUN dotnet build "./Zego.WebCrawler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Zego.WebCrawler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Zego.WebCrawler.dll"]
