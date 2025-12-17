# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

WORKDIR /src/SearchEngine.Api
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

# انسخ التطبيق
COPY --from=build /app/publish .

# انسخ الـ Data folder
COPY SearchEngine.Api/data ./data

ENTRYPOINT ["dotnet", "SearchEngine.Api.dll"]
