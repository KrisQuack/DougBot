#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./DougBot/DougBot.csproj", "DougBot/"]
COPY ["./DougBot.API/DougBot.API.csproj", "DougBot.API/"]
COPY ["./DougBot.Shared/DougBot.Shared.csproj", "DougBot.Shared/"]
RUN dotnet restore "./DougBot/DougBot.csproj"
COPY . .
WORKDIR "/src/DougBot"
RUN dotnet build "./DougBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DougBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DougBot.dll"]