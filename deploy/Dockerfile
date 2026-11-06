# Собирать из корня репозитория:
#   docker build -f deploy/Dockerfile -t arma-reforger-hz .
# Один образ содержит и сервис (по умолчанию), и CLI логина.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Сначала только проектные файлы — слой restore кэшируется, пока не меняются зависимости.
COPY arma-reforger-hz.sln ./
COPY ArmaReforger.Identity/ArmaReforger.Identity.csproj ArmaReforger.Identity/
COPY ArmaReforger.Service/ArmaReforger.Service.csproj ArmaReforger.Service/
COPY ArmaReforger.Cli/ArmaReforger.Cli.csproj ArmaReforger.Cli/
RUN dotnet restore -a "$TARGETARCH"

COPY . .
RUN dotnet publish ArmaReforger.Service -c Release -o /app/service --no-restore -a "$TARGETARCH" --self-contained false \
 && dotnet publish ArmaReforger.Cli -c Release -o /app/cli --no-restore -a "$TARGETARCH" --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/service ./service
COPY --from=build /app/cli     ./cli

# Состояние Steam-аутентификации живёт на volume, не в образе.
ENV Steam__AuthStateFilePath=/data/steam-auth.json
VOLUME /data

# aspnet-образ слушает 8080 (ASPNETCORE_HTTP_PORTS), не 5283 из launchSettings.
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "service/ArmaReforger.Service.dll"]
