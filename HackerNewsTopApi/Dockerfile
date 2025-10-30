# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["HackerNewsTopApi.csproj", "./"]
RUN dotnet restore "HackerNewsTopApi.csproj"

COPY . .

# Build
RUN dotnet build "HackerNewsTopApi.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "HackerNewsTopApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN mkdir -p /app/data

EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .

COPY --from=build /src/Migrations ./Migrations

ENV ConnectionStrings__HackerNewsTopConnection="Data Source=/app/data/hacker-news-top.db"

ENTRYPOINT ["dotnet", "HackerNewsTopApi.dll"]