# ConvoContentBuddy API Brain Service
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["ConvoContentBuddy.API.Brain/ConvoContentBuddy.API.Brain.csproj", "ConvoContentBuddy.API.Brain/"]
COPY ["ConvoContentBuddy.ServiceDefaults/ConvoContentBuddy.ServiceDefaults.csproj", "ConvoContentBuddy.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "ConvoContentBuddy.API.Brain/ConvoContentBuddy.API.Brain.csproj"

# Copy source code
COPY ConvoContentBuddy.API.Brain/ ConvoContentBuddy.API.Brain/
COPY ConvoContentBuddy.ServiceDefaults/ ConvoContentBuddy.ServiceDefaults/

# Build
WORKDIR "/src/ConvoContentBuddy.API.Brain"
RUN dotnet build "ConvoContentBuddy.API.Brain.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "ConvoContentBuddy.API.Brain.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConvoContentBuddy.API.Brain.dll"]
