# ---- build ----
# Alpine variants are substantially smaller, which matters on VPS hosts with
# slow registry connectivity. The application does not depend on native
# Debian-only libraries, so it can build and run on musl safely.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy just the project files first so `dotnet restore` is cached across
# builds unless a .csproj actually changes.
COPY src/POS.Domain/POS.Domain.csproj src/POS.Domain/
COPY src/POS.Application/POS.Application.csproj src/POS.Application/
COPY src/POS.Infrastructure/POS.Infrastructure.csproj src/POS.Infrastructure/
COPY src/POS.API/POS.API.csproj src/POS.API/
RUN dotnet restore src/POS.API/POS.API.csproj

COPY src/ src/
RUN dotnet publish src/POS.API/POS.API.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "POS.API.dll"]
