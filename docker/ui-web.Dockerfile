# ConvoContentBuddy UI Web (Blazor WASM)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["ConvoContentBuddy.UI.Web/ConvoContentBuddy.UI.Web.csproj", "ConvoContentBuddy.UI.Web/"]

# Restore dependencies
RUN dotnet restore "ConvoContentBuddy.UI.Web/ConvoContentBuddy.UI.Web.csproj"

# Copy source code
COPY ConvoContentBuddy.UI.Web/ ConvoContentBuddy.UI.Web/

# Build and publish
WORKDIR "/src/ConvoContentBuddy.UI.Web"
RUN dotnet publish "ConvoContentBuddy.UI.Web.csproj" -c Release -o /app/publish

# Final stage - Nginx for serving static files
FROM nginx:alpine AS final
WORKDIR /app
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
ENTRYPOINT ["nginx", "-g", "daemon off;"]
