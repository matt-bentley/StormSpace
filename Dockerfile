FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG BUILD_CONFIGURATION=Release
RUN apt-get update && \
    apt-get install -y curl gnupg2 && \
    mkdir -p /etc/apt/keyrings && \
    curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg && \
    echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_20.x nodistro main" | tee /etc/apt/sources.list.d/nodesource.list && \
    apt-get update && \
    apt-get install -y build-essential nodejs && \
    npm install -g npm@latest
WORKDIR /src
COPY ["EventStormingBoard.Server/EventStormingBoard.Server.csproj", "EventStormingBoard.Server/"]
COPY ["eventstormingboard.client/eventstormingboard.client.esproj", "eventstormingboard.client/"]
RUN dotnet restore "./EventStormingBoard.Server/EventStormingBoard.Server.csproj"
COPY . .
WORKDIR "/src/EventStormingBoard.Server"

RUN dotnet publish "./EventStormingBoard.Server.csproj" --no-restore -c $BUILD_CONFIGURATION -o /app

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
COPY --from=publish /src/eventstormingboard.client/dist/eventstormingboard.client/browser wwwroot
COPY --from=publish /app .
RUN chown -R 1000:1000 /app/wwwroot
USER 1000
ENTRYPOINT ["dotnet", "EventStormingBoard.Server.dll"]