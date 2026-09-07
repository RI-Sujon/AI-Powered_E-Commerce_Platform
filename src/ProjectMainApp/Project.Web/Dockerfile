# ============================================
# STAGE 1: Restore & Publish
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["ProjectMainApp.sln", "./"]
COPY ["Project.Web/Project.Web.csproj", "Project.Web/"]
COPY ["Project.Object/Project.Object.csproj", "Project.Object/"]
COPY ["Project.Endpoint/Project.Endpoint.csproj", "Project.Endpoint/"]
COPY ["Project.Application/Project.Application.csproj", "Project.Application/"]
COPY ["Project.Data/Project.Data.csproj", "Project.Data/"]
COPY ["Project.Core/Project.Core.csproj", "Project.Core/"]
COPY ["Project.Test/Project.Test.csproj", "Project.Test/"]

# Restore dependencies
RUN dotnet restore "Project.Web/Project.Web.csproj"

# Copy everything else
COPY . .

# Publish
WORKDIR "/src/Project.Web"
RUN dotnet publish "Project.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ============================================
# STAGE 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && \
    apt-get install -y curl && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appgroup --gid 1001 && \
    useradd -r -g appgroup --uid 1001 --create-home appuser

# Copy published app
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Project.Web.dll"]