# ConvoContentBuddy Data Seeder Worker Service
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/ConvoContentBuddy.Data.Seeder/ConvoContentBuddy.Data.Seeder.csproj", "ConvoContentBuddy.Data.Seeder/"]
COPY ["src/ConvoContentBuddy.ServiceDefaults/ConvoContentBuddy.ServiceDefaults.csproj", "ConvoContentBuddy.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "ConvoContentBuddy.Data.Seeder/ConvoContentBuddy.Data.Seeder.csproj"

# Copy source code
COPY src/ConvoContentBuddy.Data.Seeder/ ConvoContentBuddy.Data.Seeder/
COPY src/ConvoContentBuddy.ServiceDefaults/ ConvoContentBuddy.ServiceDefaults/

# Build
WORKDIR "/src/ConvoContentBuddy.Data.Seeder"
RUN dotnet build "ConvoContentBuddy.Data.Seeder.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "ConvoContentBuddy.Data.Seeder.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConvoContentBuddy.Data.Seeder.dll"]
