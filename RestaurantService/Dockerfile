FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RestaurantService/RestaurantService.csproj", "RestaurantService/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
RUN dotnet restore "RestaurantService/RestaurantService.csproj"
COPY . .
WORKDIR "/src/RestaurantService"
RUN dotnet build "./RestaurantService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RestaurantService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RestaurantService.dll"]
