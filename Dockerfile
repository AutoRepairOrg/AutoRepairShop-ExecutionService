# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/ExecutionService.Api/ExecutionService.Api.csproj                       src/ExecutionService.Api/
COPY src/ExecutionService.Application/ExecutionService.Application.csproj       src/ExecutionService.Application/
COPY src/ExecutionService.Domain/ExecutionService.Domain.csproj                 src/ExecutionService.Domain/
COPY src/ExecutionService.Infrastructure/ExecutionService.Infrastructure.csproj src/ExecutionService.Infrastructure/

RUN dotnet restore src/ExecutionService.Api/ExecutionService.Api.csproj

COPY src/ src/
RUN dotnet publish src/ExecutionService.Api/ExecutionService.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ExecutionService.Api.dll"]
