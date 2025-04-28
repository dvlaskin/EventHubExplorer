FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG APP_UID=1000
RUN adduser --disabled-password --uid $APP_UID appuser
WORKDIR /app

RUN mkdir -p /app/Data && chown -R appuser:appuser /app/Data
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/WebUI/WebUI.csproj", "src/WebUI/"]
RUN dotnet restore "src/WebUI/WebUI.csproj"
COPY . .
WORKDIR "/src/src/WebUI"
RUN dotnet build "WebUI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "WebUI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebUI.dll"]
