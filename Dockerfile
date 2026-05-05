# syntax=docker/dockerfile:1.7

# ---- Stage 1: build the React frontend ----
FROM node:20-alpine AS frontend
WORKDIR /src/frontend

COPY frontend/package*.json ./
RUN npm ci --no-audit --no-fund

COPY frontend/ ./
RUN npm run build
# Output: /src/frontend/dist


# ---- Stage 2: build the .NET API ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src

COPY backend/SoccerSchool.Api/SoccerSchool.Api.csproj backend/SoccerSchool.Api/
RUN dotnet restore backend/SoccerSchool.Api/SoccerSchool.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/SoccerSchool.Api/SoccerSchool.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy the .NET publish output, then drop the React build into wwwroot/
COPY --from=backend /app/publish ./
COPY --from=frontend /src/frontend/dist ./wwwroot/

# Container Apps probe + bind
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

EXPOSE 8080

# Run as non-root (the official aspnet image already creates `app` user in net8+)
USER app

ENTRYPOINT ["dotnet", "SoccerSchool.Api.dll"]
